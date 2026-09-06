using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum OriginalEventClaim { Duration, ExactRelease, ExactHead, HeadToRelease }
public enum LnReleaseStructure { OneLongNote, MultipleLongNotesSameRelease, MultipleLongNotesDifferentReleases }
public enum HeldOutRelationState { NoSameHeadEvidence, SingleEndpointRelation, CompetingEndpointRelations }

public sealed record ExactTimingProvenance(
    int HeadTime,
    decimal HeadBeat,
    int? EndpointTime,
    decimal? EndpointBeat);

public sealed record SimultaneousOriginalEventMember(
    OriginalObservationId ObservationId,
    int Lane,
    ManiaObjectType ObjectType,
    int? ReleaseTime,
    decimal? ReleaseBeat,
    decimal DurationBeats,
    ExactTimingProvenance Timing,
    ImmutableArray<OriginalEventClaim> Claims);

public sealed record ExactHeadRelationWitness(
    OriginalObservationId ObservationId,
    ImmutableArray<OriginalEventClaim> Claims);

public sealed record ExactHeadEndpointRelation(
    int HeadTime,
    decimal HeadBeat,
    int EndpointTime,
    decimal EndpointBeat,
    ImmutableArray<ExactHeadRelationWitness> Witnesses)
{
    public int IndependentWitnessCount => Witnesses.Select(x => x.ObservationId).Distinct().Count();
}

public sealed record SimultaneousOriginalEventGroup(
    int HeadTime,
    decimal HeadBeat,
    ImmutableArray<SimultaneousOriginalEventMember> Members,
    ImmutableArray<ExactHeadEndpointRelation> EndpointRelations,
    int DistinctWitnessCount,
    int DistinctEndpointCount,
    LnReleaseStructure ReleaseStructure,
    bool HasMixedObjectTypes)
{
    public int LongNoteMemberCount => Members.Count(x => x.ObjectType == ManiaObjectType.LongNote);
}

public sealed record HeldOutExactHeadRelationResult(
    OriginalObservationId TargetObservationId,
    int HeadTime,
    decimal HeadBeat,
    int TargetEndpointTime,
    decimal TargetEndpointBeat,
    HeldOutRelationState State,
    bool ExactTargetEndpointSupported,
    int ExactTargetEndpointWitnessCount,
    int DistinctAlternativeEndpoints,
    bool HasExactStructuralTwin,
    bool WasWorsenedByC11UniqueWitness);

public sealed record PriorTargetComparison(
    string ChartFingerprint,
    ImmutableHashSet<OriginalObservationId> WorsenedTargetIds);

public sealed record ExactHeadRelationResearchResult(
    string ResearchSchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    int OriginalLnCount,
    ImmutableArray<SimultaneousOriginalEventGroup> ExactHeadGroups,
    ImmutableArray<HeldOutExactHeadRelationResult> HeldOutTargets,
    double ElapsedMilliseconds)
{
    public int MultiLnGroups => ExactHeadGroups.Count(x => x.LongNoteMemberCount > 1);
    public int OneEndpointGroups => ExactHeadGroups.Count(x => x.DistinctEndpointCount == 1);
    public int CompetingEndpointGroups => ExactHeadGroups.Count(x => x.DistinctEndpointCount > 1);
    public int RelationCount => ExactHeadGroups.Sum(x => x.EndpointRelations.Length);
    public int IndependentRelationWitnessCount => ExactHeadGroups.Sum(x => x.DistinctWitnessCount);
    public int RepeatedRelationCount => ExactHeadGroups.Sum(x =>
        x.EndpointRelations.Count(r => r.IndependentWitnessCount > 1));
    public int RepeatedRelationWitnessCount => ExactHeadGroups.Sum(x =>
        x.EndpointRelations.Sum(r => Math.Max(0, r.IndependentWitnessCount - 1)));
}

/// <summary>
/// C1.2 research only. The general primitive represents simultaneous original events; endpoint relations are the
/// smallest LN-specific specialization needed by the phase. It never assigns authority, scores, or probabilities.
/// </summary>
public static class ExactHeadRelationResearch
{
    public const string ResearchSchemaVersion = "phase-c1-2-research.1";

    public static ExactHeadRelationResearchResult Evaluate(ManiaChart chart,
        PriorTargetComparison? priorComparison = null)
    {
        ArgumentNullException.ThrowIfNull(chart);
        var started = Stopwatch.GetTimestamp();
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        if (priorComparison is not null && priorComparison.ChartFingerprint != profile.ChartFingerprint)
            throw new ArgumentException("Prior comparison data belongs to a different chart.", nameof(priorComparison));
        var groups = BuildGroups(profile);
        var targets = profile.Observations.Where(x => x.Type == ManiaObjectType.LongNote).Select(target =>
        {
            var group = groups.Single(x => x.HeadTime == target.StartTime && x.HeadBeat == target.StartBeat);
            var relations = BuildEndpointRelations(group.Members
                .Where(x => x.ObjectType == ManiaObjectType.LongNote && x.ObservationId != target.Id));
            var exact = relations.SingleOrDefault(x => x.EndpointTime == target.EndTime &&
                x.EndpointBeat == target.EndBeat);
            var state = relations.Length == 0 ? HeldOutRelationState.NoSameHeadEvidence
                : relations.Length == 1 ? HeldOutRelationState.SingleEndpointRelation
                : HeldOutRelationState.CompetingEndpointRelations;
            return new HeldOutExactHeadRelationResult(target.Id, target.StartTime, target.StartBeat,
                target.EndTime!.Value, target.EndBeat, state, exact is not null,
                exact?.IndependentWitnessCount ?? 0,
                relations.Count(x => x.EndpointTime != target.EndTime || x.EndpointBeat != target.EndBeat),
                group.Members.Any(x => x.ObservationId != target.Id && x.ObjectType == ManiaObjectType.LongNote
                    && x.ReleaseTime == target.EndTime && x.ReleaseBeat == target.EndBeat),
                priorComparison?.WorsenedTargetIds.Contains(target.Id) == true);
        }).ToImmutableArray();
        return new ExactHeadRelationResearchResult(ResearchSchemaVersion, profile.ChartFingerprint, chart.KeyCount,
            chart.OriginalObjects.Count, targets.Length, groups, targets,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    public static ImmutableArray<SimultaneousOriginalEventGroup> BuildGroups(MapperEvidenceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.Observations.GroupBy(x => new { x.StartTime, x.StartBeat })
            .OrderBy(x => x.Key.StartTime).ThenBy(x => x.Key.StartBeat)
            .Select(group =>
            {
                var members = group.OrderBy(x => x.Id.Value).Select(Member).ToImmutableArray();
                var relations = BuildEndpointRelations(members.Where(x => x.ObjectType == ManiaObjectType.LongNote));
                return new { members, relations, group.Key.StartTime, group.Key.StartBeat };
            })
            .Where(x => x.relations.Length > 0)
            .Select(x => new SimultaneousOriginalEventGroup(x.StartTime, x.StartBeat, x.members, x.relations,
                x.relations.Sum(r => r.IndependentWitnessCount), x.relations.Length,
                x.relations.Sum(r => r.IndependentWitnessCount) == 1 ? LnReleaseStructure.OneLongNote
                    : x.relations.Length == 1 ? LnReleaseStructure.MultipleLongNotesSameRelease
                    : LnReleaseStructure.MultipleLongNotesDifferentReleases,
                x.members.Select(m => m.ObjectType).Distinct().Count() > 1))
            .ToImmutableArray();
    }

    public static ImmutableArray<ExactHeadEndpointRelation> BuildEndpointRelations(
        IEnumerable<SimultaneousOriginalEventMember> members) => members
        .Where(x => x.ObjectType == ManiaObjectType.LongNote)
        .GroupBy(x => new { Time = x.ReleaseTime!.Value, Beat = x.ReleaseBeat!.Value })
        .OrderBy(x => x.Key.Time).ThenBy(x => x.Key.Beat)
        .Select(group => new ExactHeadEndpointRelation(group.First().Timing.HeadTime,
            group.First().Timing.HeadBeat, group.Key.Time, group.Key.Beat,
            group.OrderBy(x => x.ObservationId.Value)
                .Select(x => new ExactHeadRelationWitness(x.ObservationId, x.Claims))
                .GroupBy(x => x.ObservationId).Select(x => x.First()).ToImmutableArray()))
        .ToImmutableArray();

    private static SimultaneousOriginalEventMember Member(OriginalObservation observation)
    {
        var claims = observation.Type == ManiaObjectType.LongNote
            ? ImmutableArray.Create(OriginalEventClaim.Duration, OriginalEventClaim.ExactRelease,
                OriginalEventClaim.ExactHead, OriginalEventClaim.HeadToRelease)
            : ImmutableArray.Create(OriginalEventClaim.ExactHead);
        return new SimultaneousOriginalEventMember(observation.Id, observation.Lane, observation.Type,
            observation.EndTime, observation.Type == ManiaObjectType.LongNote ? observation.EndBeat : null,
            observation.DurationBeats, new ExactTimingProvenance(observation.StartTime, observation.StartBeat,
                observation.EndTime, observation.Type == ManiaObjectType.LongNote ? observation.EndBeat : null), claims);
    }
}

public static class ExactHeadRelationResearchJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(ExactHeadRelationResearchResult result) => JsonSerializer.Serialize(result, Options);
}
