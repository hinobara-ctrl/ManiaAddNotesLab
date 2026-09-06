using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public readonly record struct OriginalObservationId(int Value)
{
    public override string ToString() => $"O{Value:D8}";
}

public enum OriginalTransitionType { TapToTap, TapToLongNote, LongNoteToTap, LongNoteToLongNote }
public enum RetriggerTransitionType { ReleaseToTap, ReleaseToLongNoteHead }

[Flags]
public enum InteriorAnchorKind { None = 0, Head = 1, Release = 2 }

public sealed record OriginalObservation(
    OriginalObservationId Id,
    int SourceSequence,
    int Lane,
    ManiaObjectType Type,
    int StartTime,
    int? EndTime,
    decimal StartBeat,
    decimal EndBeat)
{
    public decimal DurationBeats => EndBeat - StartBeat;
}

public sealed record ChordObservation(
    int Time,
    decimal Beat,
    ImmutableArray<OriginalObservationId> HeadObservationIds,
    int SimultaneousHeadCount,
    int HeldLnColumns,
    decimal HeadOccupancyRatio,
    decimal HeldOccupancyRatio);

public sealed record ObservedLnDuration(
    decimal DurationBeats,
    ImmutableArray<OriginalObservationId> WitnessIds);

public sealed record ObservedExactRelease(
    int ReleaseTime,
    decimal ReleaseBeat,
    ImmutableArray<OriginalObservationId> WitnessIds);

public sealed record SameLaneTransitionObservation(
    OriginalObservationId PreviousId,
    OriginalObservationId NextId,
    int Lane,
    OriginalTransitionType TransitionType,
    decimal GapBeats);

public sealed record RetriggerObservation(
    OriginalObservationId LongNoteId,
    OriginalObservationId NextHeadId,
    int Lane,
    RetriggerTransitionType TransitionType,
    decimal GapBeats);

public sealed record InteriorAnchorObservation(
    OriginalObservationId ParentLongNoteId,
    int AnchorTime,
    decimal AnchorBeat,
    InteriorAnchorKind Kind,
    ImmutableArray<OriginalObservationId> HeadWitnessIds,
    ImmutableArray<OriginalObservationId> ReleaseWitnessIds);

public sealed record TimingVocabularySummary(
    ImmutableArray<decimal> UninheritedBeatLengths,
    ImmutableArray<decimal> HeadBeatFractions,
    ImmutableArray<decimal> ReleaseBeatFractions);

public enum EvidenceClaimLevel { ObservedValue, ObservedRelation, CompatibleComposition, Unknown }
public enum EvidenceScope { Local, StructuralContext, Global }
public enum EvidenceResolutionState { ObservedSupport, LocalMismatch, NoComparableContext, AmbiguousEvidence, NotEvaluated }

/// <summary>
/// Phase A skeleton. It records provenance without defining how witnesses authorize generation.
/// </summary>
public sealed record TransformationWitness(
    string WitnessId,
    EvidenceClaimLevel ClaimLevel,
    ImmutableArray<OriginalObservationId> ObservationIds,
    ImmutableArray<string> EvidenceTags);

public sealed record CertificateObservedValue(
    OriginalObservationId ObservationId,
    string ValueKind,
    string ExactValue);

public sealed record CertificateObservedRelation(
    string RelationKind,
    int HeadTime,
    decimal HeadBeat,
    int? EndpointTime,
    decimal? EndpointBeat,
    ImmutableArray<OriginalObservationId> WitnessIds,
    ImmutableArray<string> Claims);

/// <summary>
/// Explanatory contract only. Phase B populates this in shadow mode; it deliberately contains no scalar
/// confidence or MapperSupport score and never authorizes generation.
/// </summary>
public sealed record SupportCertificate(
    DiagnosticCandidateKey CandidateKey,
    EvidenceClaimLevel ClaimLevel,
    EvidenceScope Scope,
    EvidenceResolutionState State,
    ImmutableArray<TransformationWitness> Witnesses,
    ImmutableArray<string> SatisfiedRequirements,
    ImmutableArray<string> MissingRequirements,
    ImmutableArray<string> Generalizations,
    int IndependentWitnessCount,
    HardValidityResult HardValidity,
    ImmutableArray<CertificateObservedValue> ObservedValues = default,
    ImmutableArray<CertificateObservedRelation> ObservedRelations = default);

public sealed record MapperEvidenceProfile(
    string EvidenceProfileVersion,
    string BehaviorPolicyVersion,
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    ImmutableArray<OriginalObservation> Observations,
    ImmutableArray<ChordObservation> ChordObservations,
    ImmutableArray<ObservedLnDuration> LnDurations,
    ImmutableArray<ObservedExactRelease> ExactReleases,
    ImmutableArray<SameLaneTransitionObservation> SameLaneTransitions,
    ImmutableArray<RetriggerObservation> RetriggerObservations,
    ImmutableArray<InteriorAnchorObservation> InteriorAnchorObservations,
    TimingVocabularySummary TimingVocabulary,
    long EstimatedSizeBytes)
{
    public int ObservationCount => Observations.Length;
    public int RelationCount => ChordObservations.Length + SameLaneTransitions.Length
        + RetriggerObservations.Length + InteriorAnchorObservations.Length;
}

public static class MapperEvidenceProfileBuilder
{
    public const string EvidenceProfileVersion = "phase-a.1";
    public const string BehaviorPolicyVersion = "legacy-experimental.1";

    public static MapperEvidenceProfile Build(ManiaChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        if (chart.KeyCount is < 1 or > 18) throw new ArgumentOutOfRangeException(nameof(chart.KeyCount));

        // This timeline is private to the Evidence Layer. Its diagnostic counter cannot alter generation metrics.
        var timeline = new BeatTimeline(chart.TimingPoints);
        var entries = chart.OriginalObjects.Select((source, index) =>
        {
            var start = timeline.ToBeatDecimal(source.StartTime);
            var end = source.Type == ManiaObjectType.LongNote
                ? timeline.ToBeatDecimal(source.EndTime!.Value)
                : start;
            var observation = new OriginalObservation(new OriginalObservationId(index), source.Sequence,
                source.Lane, source.Type, source.StartTime, source.EndTime, start, end);
            return new Entry(observation);
        }).ToArray();

        var observations = entries.Select(x => x.Observation).ToImmutableArray();
        var chords = BuildChords(entries, chart.KeyCount);
        var durations = entries.Where(x => x.Observation.Type == ManiaObjectType.LongNote)
            .GroupBy(x => x.Observation.DurationBeats)
            .OrderBy(x => x.Key)
            .Select(group => new ObservedLnDuration(group.Key, Ids(group.Select(x => x.Observation.Id))))
            .ToImmutableArray();
        var releases = entries.Where(x => x.Observation.Type == ManiaObjectType.LongNote)
            .GroupBy(x => new { Time = x.Observation.EndTime!.Value, Beat = x.Observation.EndBeat })
            .OrderBy(x => x.Key.Time).ThenBy(x => x.Key.Beat)
            .Select(group => new ObservedExactRelease(group.Key.Time, group.Key.Beat,
                Ids(group.Select(x => x.Observation.Id))))
            .ToImmutableArray();
        var transitions = BuildSameLaneTransitions(entries);
        var retriggers = transitions.Where(x => x.Previous.Observation.Type == ManiaObjectType.LongNote
                && x.GapBeats > 0)
            .Select(x => new RetriggerObservation(x.Previous.Observation.Id, x.Next.Observation.Id,
                x.Previous.Observation.Lane,
                x.Next.Observation.Type == ManiaObjectType.Tap
                    ? RetriggerTransitionType.ReleaseToTap
                    : RetriggerTransitionType.ReleaseToLongNoteHead,
                x.GapBeats))
            .ToImmutableArray();
        var publicTransitions = transitions.Select(x => new SameLaneTransitionObservation(
            x.Previous.Observation.Id, x.Next.Observation.Id, x.Previous.Observation.Lane,
            ResolveTransitionType(x.Previous.Observation.Type, x.Next.Observation.Type), x.GapBeats))
            .ToImmutableArray();
        var interiorAnchors = BuildInteriorAnchors(entries);
        var timing = new TimingVocabularySummary(
            chart.TimingPoints.Where(x => x.BeatLength > 0).Select(x => x.BeatLength).Distinct().Order().ToImmutableArray(),
            entries.Select(x => Fraction(x.Observation.StartBeat)).Distinct().Order().ToImmutableArray(),
            entries.Where(x => x.Observation.Type == ManiaObjectType.LongNote)
                .Select(x => Fraction(x.Observation.EndBeat)).Distinct().Order().ToImmutableArray());
        var fingerprint = ComputeFingerprint(chart);
        var estimate = EstimateSize(observations, chords, durations, releases, publicTransitions, retriggers,
            interiorAnchors, timing);

        return new MapperEvidenceProfile(EvidenceProfileVersion, BehaviorPolicyVersion, fingerprint,
            chart.KeyCount, chart.OriginalObjects.Count, observations, chords, durations, releases,
            publicTransitions, retriggers, interiorAnchors, timing, estimate);
    }

    private static ImmutableArray<ChordObservation> BuildChords(IReadOnlyList<Entry> entries, int keyCount) =>
        entries.GroupBy(x => new { x.Observation.StartTime, x.Observation.StartBeat })
            .OrderBy(x => x.Key.StartTime).ThenBy(x => x.Key.StartBeat)
            .Select(group =>
            {
                var headLanes = group.Select(x => x.Observation.Lane).Distinct().Count();
                var beat = group.Key.StartBeat;
                var held = entries.Where(x => x.Observation.Type == ManiaObjectType.LongNote
                        && x.Observation.StartBeat <= beat && x.Observation.EndBeat >= beat)
                    .Select(x => x.Observation.Lane).Distinct().Count();
                return new ChordObservation(group.Key.StartTime, beat, Ids(group.Select(x => x.Observation.Id)),
                    headLanes, held, (decimal)headLanes / keyCount, (decimal)held / keyCount);
            }).ToImmutableArray();

    private static ImmutableArray<TransitionEntry> BuildSameLaneTransitions(IReadOnlyList<Entry> entries)
    {
        var result = ImmutableArray.CreateBuilder<TransitionEntry>();
        foreach (var lane in entries.GroupBy(x => x.Observation.Lane).OrderBy(x => x.Key))
        {
            var ordered = lane.OrderBy(x => x.Observation.StartBeat).ThenBy(x => x.Observation.EndBeat)
                .ThenBy(x => x.Observation.Id.Value).ToArray();
            for (var i = 1; i < ordered.Length; i++)
                result.Add(new TransitionEntry(ordered[i - 1], ordered[i],
                    ordered[i].Observation.StartBeat - ordered[i - 1].Observation.EndBeat));
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<InteriorAnchorObservation> BuildInteriorAnchors(IReadOnlyList<Entry> entries)
    {
        var heads = entries.OrderBy(x => x.Observation.StartBeat).ThenBy(x => x.Observation.Id.Value).ToArray();
        var releases = entries.Where(x => x.Observation.Type == ManiaObjectType.LongNote)
            .OrderBy(x => x.Observation.EndBeat).ThenBy(x => x.Observation.Id.Value).ToArray();
        var result = ImmutableArray.CreateBuilder<InteriorAnchorObservation>();

        foreach (var parent in entries.Where(x => x.Observation.Type == ManiaObjectType.LongNote))
        {
            var anchors = new SortedDictionary<(int Time, decimal Beat), AnchorBuilder>();
            for (var i = FirstHeadAfter(heads, parent.Observation.StartBeat);
                 i < heads.Length && heads[i].Observation.StartBeat < parent.Observation.EndBeat; i++)
            {
                var head = heads[i].Observation;
                Add(head.StartTime, head.StartBeat).Heads.Add(head.Id);
            }
            for (var i = FirstReleaseAfter(releases, parent.Observation.StartBeat);
                 i < releases.Length && releases[i].Observation.EndBeat < parent.Observation.EndBeat; i++)
            {
                var release = releases[i].Observation;
                Add(release.EndTime!.Value, release.EndBeat).Releases.Add(release.Id);
            }
            foreach (var pair in anchors)
            {
                var kind = (pair.Value.Heads.Count > 0 ? InteriorAnchorKind.Head : InteriorAnchorKind.None)
                    | (pair.Value.Releases.Count > 0 ? InteriorAnchorKind.Release : InteriorAnchorKind.None);
                result.Add(new InteriorAnchorObservation(parent.Observation.Id, pair.Key.Time, pair.Key.Beat, kind,
                    Ids(pair.Value.Heads), Ids(pair.Value.Releases)));
            }

            AnchorBuilder Add(int time, decimal beat)
            {
                var key = (time, beat);
                if (!anchors.TryGetValue(key, out var value)) anchors[key] = value = new AnchorBuilder();
                return value;
            }
        }
        return result.ToImmutable();
    }

    private static int FirstHeadAfter(IReadOnlyList<Entry> source, decimal beat)
    {
        var lo = 0;
        var hi = source.Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (source[mid].Observation.StartBeat <= beat) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    private static int FirstReleaseAfter(IReadOnlyList<Entry> source, decimal beat)
    {
        var lo = 0;
        var hi = source.Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (source[mid].Observation.EndBeat <= beat) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    private static OriginalTransitionType ResolveTransitionType(ManiaObjectType previous, ManiaObjectType next) =>
        (previous, next) switch
        {
            (ManiaObjectType.Tap, ManiaObjectType.Tap) => OriginalTransitionType.TapToTap,
            (ManiaObjectType.Tap, ManiaObjectType.LongNote) => OriginalTransitionType.TapToLongNote,
            (ManiaObjectType.LongNote, ManiaObjectType.Tap) => OriginalTransitionType.LongNoteToTap,
            _ => OriginalTransitionType.LongNoteToLongNote
        };

    public static string ComputeFingerprint(ManiaChart chart)
    {
        var canonical = new StringBuilder();
        canonical.Append("mapper-evidence-chart-v1\nK=").Append(chart.KeyCount).Append('\n');
        foreach (var point in chart.TimingPoints.Where(x => x.BeatLength > 0)
                     .OrderBy(x => x.Time).ThenBy(x => x.BeatLength))
            canonical.Append("T|").Append(D(point.Time)).Append('|').Append(D(point.BeatLength)).Append('\n');
        for (var i = 0; i < chart.OriginalObjects.Count; i++)
        {
            var item = chart.OriginalObjects[i];
            canonical.Append("O|").Append(i).Append('|').Append(item.Sequence).Append('|').Append(item.Lane)
                .Append('|').Append((int)item.Type).Append('|').Append(item.StartTime).Append('|')
                .Append(item.EndTime?.ToString(CultureInfo.InvariantCulture) ?? "-").Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static long EstimateSize(ImmutableArray<OriginalObservation> observations,
        ImmutableArray<ChordObservation> chords, ImmutableArray<ObservedLnDuration> durations,
        ImmutableArray<ObservedExactRelease> releases, ImmutableArray<SameLaneTransitionObservation> transitions,
        ImmutableArray<RetriggerObservation> retriggers, ImmutableArray<InteriorAnchorObservation> anchors,
        TimingVocabularySummary timing) => 256L + observations.Length * 72L + chords.Sum(x => 64L + 4L * x.HeadObservationIds.Length)
        + durations.Sum(x => 32L + 4L * x.WitnessIds.Length)
        + releases.Sum(x => 32L + 4L * x.WitnessIds.Length) + transitions.Length * 40L
        + retriggers.Length * 40L + anchors.Sum(x => 56L + 4L * (x.HeadWitnessIds.Length + x.ReleaseWitnessIds.Length))
        + 16L * (timing.UninheritedBeatLengths.Length + timing.HeadBeatFractions.Length
            + timing.ReleaseBeatFractions.Length);

    private static ImmutableArray<OriginalObservationId> Ids(IEnumerable<OriginalObservationId> source) =>
        source.Distinct().OrderBy(x => x.Value).ToImmutableArray();

    private static decimal Fraction(decimal beat) => beat - decimal.Floor(beat);
    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed record Entry(OriginalObservation Observation);
    private sealed record TransitionEntry(Entry Previous, Entry Next, decimal GapBeats);
    private sealed class AnchorBuilder
    {
        public List<OriginalObservationId> Heads { get; } = [];
        public List<OriginalObservationId> Releases { get; } = [];
    }
}

public static class MapperEvidenceProfileJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(MapperEvidenceProfile profile) => JsonSerializer.Serialize(profile, Options);
}
