using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum GapTimingIdentityKind { FileExactDecimal, ExactSegmentTraversal }
public enum GapTimingHoldoutKind { F2HeadGroup, TransitionEndpointGroup }

public sealed record GapTimingEndpoint(
    TypedGapEndpointType Type,
    int Time,
    decimal Beat,
    int TimingSegmentIndex,
    decimal TimingSegmentStartTime,
    decimal TimingSegmentStartBeat,
    decimal TimingSegmentBeatLength);

public sealed record ExactTimingTraversalPart(decimal BeatLength, decimal BeatSpan);

public readonly record struct GapTimingIdentityValue(
    GapTimingIdentityKind Kind,
    TypedGapTransitionKind TransitionKind,
    string CanonicalValue);

public sealed record GapTimingIdentityOccurrence(
    string ChartFingerprint,
    OriginalObservationId PreviousObservationId,
    OriginalObservationId NextObservationId,
    int Lane,
    TypedGapTransitionKind TransitionKind,
    decimal FileExactGapBeats,
    GapTimingEndpoint SourceEndpoint,
    GapTimingEndpoint DestinationEndpoint,
    ImmutableArray<ExactTimingTraversalPart> SegmentTraversal,
    GapTimingIdentityValue FileExactIdentity,
    GapTimingIdentityValue ExactSegmentTraversalIdentity,
    ImmutableArray<OriginalObservationId> F2HeadGroupExclusion,
    ImmutableArray<OriginalObservationId> TransitionEndpointGroupExclusion,
    int TimingPointBoundariesCrossed,
    bool SameTimingSegment,
    bool PreviousLongNoteCrossesTimingSegment,
    bool SourceReleaseEventShared);

public sealed record GapTimingEvidenceAssessment(
    OriginalObservationId TargetPreviousObservationId,
    OriginalObservationId TargetNextObservationId,
    GapTimingIdentityKind IdentityKind,
    GapTimingHoldoutKind HoldoutKind,
    ComparableEvidenceState LocalState,
    ComparableEvidenceState? GlobalState,
    TypedGapBackoffAction HypotheticalAction,
    int LocalComparableDonors,
    int LocalSupportingDonors,
    int GlobalComparableDonors,
    int GlobalSupportingDonors);

public sealed record GapTimingEndpointLeakageViolation(
    OriginalObservationId TargetPreviousObservationId,
    OriginalObservationId TargetNextObservationId,
    OriginalObservationId DonorPreviousObservationId,
    OriginalObservationId DonorNextObservationId,
    GapTimingIdentityKind IdentityKind,
    GapTimingHoldoutKind HoldoutKind,
    TypedGapEvidenceScope Scope,
    ImmutableArray<OriginalObservationId> LeakedObservationIds);

public sealed record ExactGapTimingIdentityResearchResult(
    string ResearchSchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    ImmutableArray<GapTimingIdentityOccurrence> Occurrences,
    ImmutableArray<GapTimingEvidenceAssessment> Assessments,
    int SharedReleaseEndpointTargets,
    int EndpointHoldoutAdditionalExcludedReferences,
    ImmutableArray<GapTimingEndpointLeakageViolation> TargetEndpointLeakageViolations,
    int SyntheticTeachingCount,
    int HeldTailEncodedAsHeadCount)
{
    public int TargetEndpointLeakageCount => TargetEndpointLeakageViolations.Length;
}

/// <summary>
/// F2.1 research-only representation. It preserves F2 file-exact gaps and adds an exact
/// timing-segment traversal identity. It neither infers quantization nor participates in generation.
/// </summary>
public static class ExactGapTimingIdentityResearch
{
    public const string ResearchSchemaVersion = "phase-f2-1-exact-gap-timing-shadow.2";

    public static ExactGapTimingIdentityResearchResult Evaluate(ManiaChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var observations = profile.Observations.ToDictionary(x => x.Id);
        var segments = BuildSegments(chart.TimingPoints);
        var headGroups = profile.Observations.GroupBy(x => (x.StartTime, x.StartBeat))
            .SelectMany(group => group.Select(item => (item.Id, Members: Ids(group.Select(x => x.Id)))))
            .ToDictionary(x => x.Id, x => x.Members);
        var releaseGroups = profile.Observations.Where(x => x.Type == ManiaObjectType.LongNote)
            .GroupBy(x => (x.EndTime, x.EndBeat))
            .SelectMany(group => group.Select(item => (item.Id, Members: Ids(group.Select(x => x.Id)))))
            .ToDictionary(x => x.Id, x => x.Members);

        var entries = profile.SameLaneTransitions.Where(x => x.GapBeats >= 0)
            .OrderBy(x => x.PreviousId.Value).ThenBy(x => x.NextId.Value)
            .Select(transition => BuildOccurrence(profile.ChartFingerprint, transition, observations,
                segments, headGroups, releaseGroups)).ToArray();
        var byKind = entries.GroupBy(x => x.TransitionKind).ToDictionary(x => x.Key, x => x.ToArray());
        var byLaneKind = entries.GroupBy(x => (x.Lane, x.TransitionKind))
            .ToDictionary(x => x.Key, x => x.ToArray());

        var assessments = ImmutableArray.CreateBuilder<GapTimingEvidenceAssessment>();
        var leakage = ImmutableArray.CreateBuilder<GapTimingEndpointLeakageViolation>();
        foreach (var target in entries)
        foreach (var identity in Enum.GetValues<GapTimingIdentityKind>())
        foreach (var holdout in Enum.GetValues<GapTimingHoldoutKind>())
        {
            var assessed = Assess(target, identity, holdout, byKind, byLaneKind);
            assessments.Add(assessed.Assessment);
            leakage.AddRange(assessed.LeakageViolations);
        }

        var additionalExclusions = entries.Sum(x => x.TransitionEndpointGroupExclusion
            .Except(x.F2HeadGroupExclusion).Count());
        var heldAsHead = entries.Count(x =>
            x.SourceEndpoint.Type == TypedGapEndpointType.LongNoteRelease
            && observations[x.PreviousObservationId].Type != ManiaObjectType.LongNote);
        return new ExactGapTimingIdentityResearchResult(ResearchSchemaVersion, profile.ChartFingerprint,
            profile.KeyCount, profile.OriginalObjectCount, entries.ToImmutableArray(), assessments.ToImmutable(),
            entries.Count(x => x.SourceReleaseEventShared), additionalExclusions, leakage.ToImmutable(), 0, heldAsHead);
    }

    public static bool Equivalent(GapTimingIdentityValue left, GapTimingIdentityValue right) => left == right;

    /// <summary>
    /// Audits a concrete set of donors that a caller claims are valid for a target. This is public so
    /// research tests can prove that an intentionally broken holdout is detected rather than merely
    /// asserting a pre-filled zero counter.
    /// </summary>
    public static ImmutableArray<GapTimingEndpointLeakageViolation> AuditEndpointLeakage(
        GapTimingIdentityOccurrence target,
        GapTimingIdentityKind identityKind,
        GapTimingHoldoutKind holdoutKind,
        TypedGapEvidenceScope scope,
        IEnumerable<GapTimingIdentityOccurrence> consideredDonors)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(consideredDonors);
        var excluded = holdoutKind == GapTimingHoldoutKind.F2HeadGroup
            ? target.F2HeadGroupExclusion : target.TransitionEndpointGroupExclusion;
        return consideredDonors.Select(donor => (Donor: donor, Leaked: Ids(
                new[] { donor.PreviousObservationId, donor.NextObservationId }.Where(excluded.Contains))))
            .Where(x => !x.Leaked.IsEmpty)
            .Select(x => new GapTimingEndpointLeakageViolation(
                target.PreviousObservationId, target.NextObservationId,
                x.Donor.PreviousObservationId, x.Donor.NextObservationId,
                identityKind, holdoutKind, scope, x.Leaked))
            .OrderBy(x => x.DonorPreviousObservationId.Value)
            .ThenBy(x => x.DonorNextObservationId.Value)
            .ToImmutableArray();
    }

    private static AssessmentAudit Assess(GapTimingIdentityOccurrence target,
        GapTimingIdentityKind identityKind, GapTimingHoldoutKind holdoutKind,
        IReadOnlyDictionary<TypedGapTransitionKind, GapTimingIdentityOccurrence[]> byKind,
        IReadOnlyDictionary<(int Lane, TypedGapTransitionKind Kind), GapTimingIdentityOccurrence[]> byLaneKind)
    {
        var excluded = holdoutKind == GapTimingHoldoutKind.F2HeadGroup
            ? target.F2HeadGroupExclusion : target.TransitionEndpointGroupExclusion;
        var targetIdentity = Identity(target, identityKind);
        bool Eligible(GapTimingIdentityOccurrence donor) =>
            !excluded.Contains(donor.PreviousObservationId) && !excluded.Contains(donor.NextObservationId);
        var local = byLaneKind.GetValueOrDefault((target.Lane, target.TransitionKind), [])
            .Where(Eligible).ToArray();
        var localSupport = local.Count(x => Identity(x, identityKind) == targetIdentity);
        var localState = TypedGapBackoffResearch.ClassifyEvidence(local.Length, localSupport);
        GapTimingIdentityOccurrence[] global = [];
        ComparableEvidenceState? globalState = null;
        var globalSupport = 0;
        if (localState == ComparableEvidenceState.NoComparableContext)
        {
            global = byKind[target.TransitionKind].Where(Eligible).ToArray();
            globalSupport = global.Count(x => Identity(x, identityKind) == targetIdentity);
            globalState = TypedGapBackoffResearch.ClassifyEvidence(global.Length, globalSupport);
        }
        var action = TypedGapBackoffResearch.ResolveBackoff(localState,
            globalState ?? ComparableEvidenceState.NoComparableContext).Action;
        var assessment = new GapTimingEvidenceAssessment(target.PreviousObservationId, target.NextObservationId,
            identityKind, holdoutKind, localState, globalState, action, local.Length, localSupport,
            global.Length, globalSupport);
        var violations = AuditEndpointLeakage(target, identityKind, holdoutKind,
            TypedGapEvidenceScope.LocalLane, local).AddRange(AuditEndpointLeakage(target, identityKind,
            holdoutKind, TypedGapEvidenceScope.GlobalChart, global));
        return new AssessmentAudit(assessment, violations);
    }

    private static GapTimingIdentityOccurrence BuildOccurrence(string fingerprint,
        SameLaneTransitionObservation transition,
        IReadOnlyDictionary<OriginalObservationId, OriginalObservation> observations,
        IReadOnlyList<TimingSegment> segments,
        IReadOnlyDictionary<OriginalObservationId, ImmutableArray<OriginalObservationId>> headGroups,
        IReadOnlyDictionary<OriginalObservationId, ImmutableArray<OriginalObservationId>> releaseGroups)
    {
        var previous = observations[transition.PreviousId];
        var next = observations[transition.NextId];
        var kind = Kind(previous.Type, next.Type);
        var sourceType = previous.Type == ManiaObjectType.Tap
            ? TypedGapEndpointType.TapHead : TypedGapEndpointType.LongNoteRelease;
        var destinationType = next.Type == ManiaObjectType.Tap
            ? TypedGapEndpointType.TapHead : TypedGapEndpointType.LongNoteHead;
        var sourceTime = previous.Type == ManiaObjectType.Tap ? previous.StartTime : previous.EndTime!.Value;
        var sourceBeat = previous.EndBeat;
        var destinationTime = next.StartTime;
        var sourceSegment = SegmentAt(segments, sourceTime);
        var destinationSegment = SegmentAt(segments, destinationTime);
        var traversal = Traverse(segments, sourceTime, destinationTime);
        var f2Exclusion = Ids(headGroups[previous.Id].Concat(headGroups[next.Id]));
        var sourceGroup = previous.Type == ManiaObjectType.Tap
            ? headGroups[previous.Id] : releaseGroups[previous.Id];
        var endpointExclusion = Ids(sourceGroup.Concat(headGroups[next.Id]));
        var fileIdentity = new GapTimingIdentityValue(GapTimingIdentityKind.FileExactDecimal, kind,
            D(transition.GapBeats));
        var traversalIdentity = new GapTimingIdentityValue(GapTimingIdentityKind.ExactSegmentTraversal, kind,
            string.Join(";", traversal.Select(x => $"{D(x.BeatLength)}@{D(x.BeatSpan)}")));
        var previousHeadSegment = SegmentAt(segments, previous.StartTime);
        return new GapTimingIdentityOccurrence(fingerprint, previous.Id, next.Id, transition.Lane, kind,
            transition.GapBeats,
            Endpoint(sourceType, sourceTime, sourceBeat, sourceSegment),
            Endpoint(destinationType, destinationTime, next.StartBeat, destinationSegment),
            traversal, fileIdentity, traversalIdentity, f2Exclusion, endpointExclusion,
            Math.Max(0, traversal.Length - 1), sourceSegment.Index == destinationSegment.Index,
            previous.Type == ManiaObjectType.LongNote && previousHeadSegment.Index != sourceSegment.Index,
            previous.Type == ManiaObjectType.LongNote && releaseGroups[previous.Id].Length > 1);
    }

    private static GapTimingEndpoint Endpoint(TypedGapEndpointType type, int time, decimal beat,
        TimingSegment segment) => new(type, time, beat, segment.Index, segment.StartTime,
        segment.StartBeat, segment.BeatLength);

    private static GapTimingIdentityValue Identity(GapTimingIdentityOccurrence occurrence,
        GapTimingIdentityKind kind) => kind == GapTimingIdentityKind.FileExactDecimal
        ? occurrence.FileExactIdentity : occurrence.ExactSegmentTraversalIdentity;

    private static ImmutableArray<ExactTimingTraversalPart> Traverse(IReadOnlyList<TimingSegment> segments,
        int sourceTime, int destinationTime)
    {
        if (destinationTime < sourceTime) throw new InvalidDataException("Gap traversal cannot run backwards.");
        var result = ImmutableArray.CreateBuilder<ExactTimingTraversalPart>();
        if (destinationTime == sourceTime)
        {
            var segment = SegmentAt(segments, sourceTime);
            result.Add(new ExactTimingTraversalPart(segment.BeatLength, 0));
            return result.ToImmutable();
        }
        var current = (decimal)sourceTime;
        while (current < destinationTime)
        {
            var segment = SegmentAt(segments, current);
            var nextBoundary = segments.Where(x => x.StartTime > current).Select(x => x.StartTime)
                .DefaultIfEmpty(destinationTime).Min();
            var end = decimal.Min(destinationTime, nextBoundary);
            result.Add(new ExactTimingTraversalPart(segment.BeatLength, (end - current) / segment.BeatLength));
            current = end;
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<TimingSegment> BuildSegments(IEnumerable<TimingPoint> timingPoints)
    {
        var points = timingPoints.Select((point, order) => (point, order)).Where(x => x.point.BeatLength > 0)
            .OrderBy(x => x.point.Time).ThenBy(x => x.order).Select(x => x.point).ToArray();
        if (points.Length == 0) throw new InvalidDataException("The beatmap has no uninherited timing point.");
        var result = ImmutableArray.CreateBuilder<TimingSegment>();
        var accumulatedBeat = 0m;
        for (var index = 0; index < points.Length; index++)
        {
            if (index > 0)
                accumulatedBeat += (points[index].Time - points[index - 1].Time) / points[index - 1].BeatLength;
            result.Add(new TimingSegment(index, points[index].Time, points[index].BeatLength, accumulatedBeat));
        }
        return result.ToImmutable();
    }

    private static TimingSegment SegmentAt(IReadOnlyList<TimingSegment> segments, decimal time) =>
        segments.LastOrDefault(x => x.StartTime <= time) ?? segments[0];

    private static TypedGapTransitionKind Kind(ManiaObjectType previous, ManiaObjectType next) =>
        (previous, next) switch
        {
            (ManiaObjectType.Tap, ManiaObjectType.Tap) => TypedGapTransitionKind.TapHeadToTapHead,
            (ManiaObjectType.Tap, ManiaObjectType.LongNote) => TypedGapTransitionKind.TapHeadToLongNoteHead,
            (ManiaObjectType.LongNote, ManiaObjectType.Tap) => TypedGapTransitionKind.LongNoteReleaseToTapHead,
            _ => TypedGapTransitionKind.LongNoteReleaseToLongNoteHead
        };

    private static ImmutableArray<OriginalObservationId> Ids(IEnumerable<OriginalObservationId> source) =>
        source.Distinct().OrderBy(x => x.Value).ToImmutableArray();
    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private sealed record AssessmentAudit(GapTimingEvidenceAssessment Assessment,
        ImmutableArray<GapTimingEndpointLeakageViolation> LeakageViolations);
    private sealed record TimingSegment(int Index, decimal StartTime, decimal BeatLength, decimal StartBeat);
}

public static class ExactGapTimingIdentityResearchJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(ExactGapTimingIdentityResearchResult result) =>
        JsonSerializer.Serialize(result, Options);
}
