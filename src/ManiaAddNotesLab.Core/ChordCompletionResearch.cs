using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum OriginalHeadMemberType { TapHead, LongNoteHead }
public enum ChordCompletionReconstructionState
{
    NoComparableReducedState,
    ComparableButTargetUnsupported,
    UniqueExactCompletion,
    TargetAmongCompetingCompletions
}

public sealed record OriginalHeadMember(
    OriginalObservationId ObservationId,
    int Lane,
    OriginalHeadMemberType HeadType);

public sealed record OriginalHeadState(
    int KeyCount,
    ImmutableArray<int> TapHeadLanes,
    ImmutableArray<int> LongNoteHeadLanes,
    ImmutableArray<int> HeldBeforeHeadLanes,
    ImmutableArray<OriginalObservationId> MemberObservationIds)
{
    public string StructuralSignature => ChordCompletionResearch.HeadSignature(
        KeyCount, TapHeadLanes, LongNoteHeadLanes);
    public string HeldContextSignature => ChordCompletionResearch.LaneSignature(HeldBeforeHeadLanes);
}

public sealed record OriginalChordGroup(
    string SourceGroupId,
    int HeadTime,
    decimal HeadBeat,
    OriginalHeadState HeadState,
    ImmutableArray<OriginalHeadMember> Members,
    ImmutableArray<OriginalObservationId> HeldBeforeObservationIds);

public sealed record ChordCompletionRelationKey(
    int KeyCount,
    ImmutableArray<int> ReducedTapHeadLanes,
    ImmutableArray<int> ReducedLongNoteHeadLanes,
    int CompletionLane,
    OriginalHeadMemberType CompletionType)
{
    public string ReducedStateSignature => ChordCompletionResearch.HeadSignature(
        KeyCount, ReducedTapHeadLanes, ReducedLongNoteHeadLanes);
    public string RelationSignature => $"{ReducedStateSignature}|C:{CompletionLane}:{CompletionType}";
}

public sealed record ChordCompletionRelationWitness(
    string SourceGroupId,
    int HeadTime,
    decimal HeadBeat,
    ImmutableArray<OriginalObservationId> FullOriginalMemberObservationIds,
    ImmutableArray<OriginalObservationId> VisibleReducedMemberObservationIds,
    OriginalObservationId CompletionObservationId,
    int CompletionLane,
    OriginalHeadMemberType CompletionType,
    ImmutableArray<int> HeldBeforeHeadLanes,
    string ObservedFullStateSignature);

public sealed record ChordCompletionRelation(
    ChordCompletionRelationKey Key,
    ImmutableArray<ChordCompletionRelationWitness> Witnesses)
{
    public int IndependentRelationWitnessCount => Witnesses.Select(x => x.SourceGroupId).Distinct().Count();
}

public sealed record ChordCompletionReconstructionTrial(
    string TargetSourceGroupId,
    OriginalObservationId TargetObservationId,
    int TargetLane,
    OriginalHeadMemberType TargetType,
    int OriginalChordHeadCount,
    int ReducedVisibleMemberCount,
    bool HeldBeforePresent,
    ChordCompletionRelationKey Query,
    ChordCompletionReconstructionState State,
    int ComparableCompletionRelations,
    int ComparableIndependentWitnesses,
    int ExactTargetIndependentWitnesses,
    bool LaneSupportedButTypeDifferent,
    bool ExactHeldContextComparable,
    bool ExactHeldContextTargetSupported)
{
    public bool ExactTargetCompletionSupported => State is ChordCompletionReconstructionState.UniqueExactCompletion
        or ChordCompletionReconstructionState.TargetAmongCompetingCompletions;
}

public sealed record ChordHeadCount(int HeadCount, int GroupCount);

public sealed record ChordCompletionResearchResult(
    string ResearchSchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    int ExactHeadGroupCount,
    ImmutableArray<OriginalChordGroup> ChordGroups,
    ImmutableArray<ChordCompletionRelation> Relations,
    ImmutableArray<ChordCompletionReconstructionTrial> Trials,
    double RelationBuildMilliseconds,
    double HeldOutReconstructionMilliseconds)
{
    public int CompletionTrials => Trials.Length;
    public int TapOnlyChordGroups => ChordGroups.Count(x =>
        x.Members.All(m => m.HeadType == OriginalHeadMemberType.TapHead));
    public int LongNoteHeadOnlyChordGroups => ChordGroups.Count(x =>
        x.Members.All(m => m.HeadType == OriginalHeadMemberType.LongNoteHead));
    public int MixedTapLongNoteChordGroups => ChordGroups.Length - TapOnlyChordGroups - LongNoteHeadOnlyChordGroups;
    public int GroupsWithHeldBeforeContext => ChordGroups.Count(x => x.HeadState.HeldBeforeHeadLanes.Length > 0);
    public ImmutableArray<ChordHeadCount> ChordGroupsByHeadCount => ChordGroups.GroupBy(x => x.Members.Length)
        .OrderBy(x => x.Key).Select(x => new ChordHeadCount(x.Key, x.Count())).ToImmutableArray();
    public int DistinctReducedStates => Relations.Select(x => x.Key.ReducedStateSignature).Distinct().Count();
    public int DistinctCompletionRelations => Relations.Length;
    public int IndependentRelationWitnesses => Relations.Sum(x => x.IndependentRelationWitnessCount);
    public int WitnessReferenceCount => Relations.Sum(x => x.Witnesses.Length);
    public int DistinctObservedFullStates => Relations.SelectMany(x => x.Witnesses)
        .Select(x => x.ObservedFullStateSignature).Distinct().Count();
}

/// <summary>
/// D0 research-only exact chord-completion model. It does not consume RNG, inspect synthetics, score candidates,
/// or participate in generation.
/// </summary>
public static class ChordCompletionResearch
{
    public const string ResearchSchemaVersion = "phase-d0-research.1";

    public static ChordCompletionResearchResult Evaluate(ManiaChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var relationStarted = Stopwatch.GetTimestamp();
        var exactGroups = ExactHeadRelationResearch.BuildAllGroups(profile);
        var chordGroups = exactGroups.Where(x => x.Members.Length >= 2)
            .Select(x => BuildChordGroup(profile, x)).ToImmutableArray();
        var relations = BuildRelations(chordGroups);
        var relationMs = Stopwatch.GetElapsedTime(relationStarted).TotalMilliseconds;

        var heldOutStarted = Stopwatch.GetTimestamp();
        var trials = BuildHeldOutTrials(chordGroups, relations);
        var heldOutMs = Stopwatch.GetElapsedTime(heldOutStarted).TotalMilliseconds;
        return new ChordCompletionResearchResult(ResearchSchemaVersion, profile.ChartFingerprint,
            chart.KeyCount, chart.OriginalObjects.Count, exactGroups.Length, chordGroups, relations, trials,
            relationMs, heldOutMs);
    }

    private static ImmutableArray<ChordCompletionRelation> BuildRelations(
        IEnumerable<OriginalChordGroup> groups)
    {
        var occurrences = groups.SelectMany(group => group.Members.Select(member => Occurrence(group, member)))
            .GroupBy(x => new { x.Key.RelationSignature, x.Witness.SourceGroupId })
            .Select(x => x.OrderBy(y => y.Witness.CompletionObservationId.Value).First())
            .ToArray();
        return occurrences.GroupBy(x => x.Key.RelationSignature, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(group => new ChordCompletionRelation(group.First().Key,
                group.Select(x => x.Witness).OrderBy(x => x.HeadTime).ThenBy(x => x.HeadBeat)
                    .ThenBy(x => x.SourceGroupId, StringComparer.Ordinal).ToImmutableArray()))
            .ToImmutableArray();
    }

    internal static string HeadSignature(int keyCount, IEnumerable<int> tapLanes, IEnumerable<int> lnLanes) =>
        $"K:{keyCount}|T:{LaneSignature(tapLanes)}|L:{LaneSignature(lnLanes)}";

    internal static string LaneSignature(IEnumerable<int> lanes) =>
        string.Join(',', lanes.OrderBy(x => x).Select(x => x.ToString(CultureInfo.InvariantCulture)));

    private static OriginalChordGroup BuildChordGroup(MapperEvidenceProfile profile,
        SimultaneousOriginalEventGroup group)
    {
        var members = group.Members.Select(x => new OriginalHeadMember(x.ObservationId, x.Lane,
                x.ObjectType == ManiaObjectType.Tap ? OriginalHeadMemberType.TapHead
                    : OriginalHeadMemberType.LongNoteHead))
            .OrderBy(x => x.Lane).ThenBy(x => x.HeadType).ThenBy(x => x.ObservationId.Value).ToImmutableArray();
        var held = profile.Observations.Where(x => x.Type == ManiaObjectType.LongNote
                && x.StartTime < group.HeadTime && x.StartBeat < group.HeadBeat
                && x.EndTime >= group.HeadTime && x.EndBeat >= group.HeadBeat)
            .OrderBy(x => x.Lane).ThenBy(x => x.Id.Value).ToImmutableArray();
        var state = new OriginalHeadState(profile.KeyCount,
            members.Where(x => x.HeadType == OriginalHeadMemberType.TapHead).Select(x => x.Lane).ToImmutableArray(),
            members.Where(x => x.HeadType == OriginalHeadMemberType.LongNoteHead).Select(x => x.Lane).ToImmutableArray(),
            held.Select(x => x.Lane).Distinct().Order().ToImmutableArray(),
            members.Select(x => x.ObservationId).OrderBy(x => x.Value).ToImmutableArray());
        var id = $"{profile.ChartFingerprint}/H-{group.HeadTime}-{D(group.HeadBeat)}-" +
            string.Join('-', state.MemberObservationIds.Select(x => x.ToString()));
        return new OriginalChordGroup(id, group.HeadTime, group.HeadBeat, state, members,
            held.Select(x => x.Id).ToImmutableArray());
    }

    private static RelationOccurrence Occurrence(OriginalChordGroup group, OriginalHeadMember completion)
    {
        var visible = group.Members.Where(x => x.ObservationId != completion.ObservationId).ToArray();
        var key = new ChordCompletionRelationKey(group.HeadState.KeyCount,
            visible.Where(x => x.HeadType == OriginalHeadMemberType.TapHead).Select(x => x.Lane)
                .Order().ToImmutableArray(),
            visible.Where(x => x.HeadType == OriginalHeadMemberType.LongNoteHead).Select(x => x.Lane)
                .Order().ToImmutableArray(), completion.Lane, completion.HeadType);
        var witness = new ChordCompletionRelationWitness(group.SourceGroupId, group.HeadTime, group.HeadBeat,
            group.HeadState.MemberObservationIds,
            visible.Select(x => x.ObservationId).OrderBy(x => x.Value).ToImmutableArray(),
            completion.ObservationId, completion.Lane, completion.HeadType,
            group.HeadState.HeldBeforeHeadLanes, group.HeadState.StructuralSignature);
        return new RelationOccurrence(key, witness);
    }

    private static ImmutableArray<ChordCompletionReconstructionTrial> BuildHeldOutTrials(
        ImmutableArray<OriginalChordGroup> groups, ImmutableArray<ChordCompletionRelation> relations)
    {
        var trials = ImmutableArray.CreateBuilder<ChordCompletionReconstructionTrial>();
        foreach (var targetGroup in groups)
        foreach (var target in targetGroup.Members)
        {
            var query = Occurrence(targetGroup, target).Key;
            var comparable = relations.Select(relation => new ChordCompletionRelation(relation.Key,
                    relation.Witnesses.Where(x => x.SourceGroupId != targetGroup.SourceGroupId).ToImmutableArray()))
                .Where(x => x.Witnesses.Length > 0 &&
                    x.Key.ReducedStateSignature == query.ReducedStateSignature).ToArray();
            var exact = comparable.SingleOrDefault(x => x.Key.CompletionLane == target.Lane
                && x.Key.CompletionType == target.HeadType);
            var state = comparable.Length == 0
                ? ChordCompletionReconstructionState.NoComparableReducedState
                : exact is null ? ChordCompletionReconstructionState.ComparableButTargetUnsupported
                : comparable.Length == 1 ? ChordCompletionReconstructionState.UniqueExactCompletion
                : ChordCompletionReconstructionState.TargetAmongCompetingCompletions;
            var exactHeld = comparable.SelectMany(x => x.Witnesses).Where(x =>
                x.HeldBeforeHeadLanes.SequenceEqual(targetGroup.HeadState.HeldBeforeHeadLanes)).ToArray();
            var exactHeldTarget = exact?.Witnesses.Any(x =>
                x.HeldBeforeHeadLanes.SequenceEqual(targetGroup.HeadState.HeldBeforeHeadLanes)) == true;
            trials.Add(new ChordCompletionReconstructionTrial(targetGroup.SourceGroupId, target.ObservationId,
                target.Lane, target.HeadType, targetGroup.Members.Length, targetGroup.Members.Length - 1,
                targetGroup.HeadState.HeldBeforeHeadLanes.Length > 0, query, state, comparable.Length,
                comparable.Sum(x => x.IndependentRelationWitnessCount),
                exact?.IndependentRelationWitnessCount ?? 0,
                comparable.Any(x => x.Key.CompletionLane == target.Lane && x.Key.CompletionType != target.HeadType),
                exactHeld.Length > 0, exactHeldTarget));
        }
        return trials.ToImmutable();
    }

    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private sealed record RelationOccurrence(ChordCompletionRelationKey Key, ChordCompletionRelationWitness Witness);
}

public static class ChordCompletionResearchJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(ChordCompletionResearchResult result) => JsonSerializer.Serialize(result, Options);
}
