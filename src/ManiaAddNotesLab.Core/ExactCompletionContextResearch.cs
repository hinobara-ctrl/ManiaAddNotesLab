using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum ExactCompletionContextView
{
    ReducedOnly,
    ReducedHeld,
    ReducedPrevious,
    ReducedPreviousTransition,
    ReducedNext,
    ReducedNextTransition,
    ReducedPrevNext,
    ReducedHeldPrevNext
}

public enum ExactCompletionContextOutcome
{
    NoComparableExactContext,
    ComparableTargetUnsupported,
    TargetSupportedStillCompeting,
    TargetResolvedUnique,
    UniqueWrongCompletion
}

public sealed record ExactContextHeadState(
    string SourceGroupId,
    int HeadTime,
    decimal HeadBeat,
    int KeyCount,
    ImmutableArray<int> TapHeadLanes,
    ImmutableArray<int> LongNoteHeadLanes,
    ImmutableArray<OriginalObservationId> MemberObservationIds)
{
    public string StructuralSignature => ChordCompletionResearch.HeadSignature(
        KeyCount, TapHeadLanes, LongNoteHeadLanes);
}

public sealed record ExactHeadTransitionContext(
    ExactContextHeadState Neighbor,
    decimal DeltaBeat,
    int DeltaMilliseconds,
    bool BeatMaterializesToObservedMilliseconds);

/// <summary>
/// Exact chart-local context for one original head group. Future held occupancy is explicitly rebuilt with
/// every member of the current group removed; it is provenance/audit data and is not part of any D0.1 view.
/// </summary>
public sealed record ExactCompletionContext(
    string SourceGroupId,
    int HeadTime,
    decimal HeadBeat,
    ImmutableArray<OriginalObservationId> CurrentGroupObservationIds,
    ImmutableArray<int> HeldBeforeHeadLanes,
    ImmutableArray<OriginalObservationId> HeldBeforeObservationIds,
    ExactHeadTransitionContext? PreviousTransition,
    ExactHeadTransitionContext? NextTransition,
    ImmutableArray<int> NextHeldBeforeLanesExcludingCurrentGroup,
    ImmutableArray<OriginalObservationId> NextHeldBeforeObservationIdsExcludingCurrentGroup,
    ImmutableArray<OriginalObservationId> CurrentLongNotesRemovedFromNextHeldContext);

public sealed record ExactCompletionOccurrence(
    ChordCompletionRelationKey Relation,
    ChordCompletionRelationWitness Witness,
    ExactCompletionContext Context);

public sealed record ExactContextViewEvaluation(
    ExactCompletionContextView View,
    string ExactContextSignature,
    ExactCompletionContextOutcome Outcome,
    int BaselineCompletionCount,
    int ContextualCompletionCount,
    int ContextualIndependentWitnessCount,
    int ExactTargetIndependentWitnessCount,
    bool ExactTargetSupported,
    bool LaneSupportedButTypeDifferent,
    int TargetGroupDonorLeakageCount);

public sealed record ExactCompletionCompetitionTrial(
    string TargetSourceGroupId,
    OriginalObservationId TargetObservationId,
    int TargetLane,
    OriginalHeadMemberType TargetType,
    int OriginalChordHeadCount,
    int ReducedVisibleMemberCount,
    bool HeldBeforePresent,
    ChordCompletionReconstructionState BaselineState,
    int BaselineCompletionCount,
    ChordCompletionRelationKey Query,
    ExactCompletionContext TargetContext,
    ImmutableArray<ExactContextViewEvaluation> Views);

public sealed record ExactCompletionContextResearchResult(
    string ResearchSchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    int ExactHeadGroupCount,
    ImmutableArray<OriginalChordGroup> ChordGroups,
    ImmutableArray<ExactCompletionOccurrence> Occurrences,
    ImmutableArray<ExactCompletionCompetitionTrial> Trials,
    double ContextBuildMilliseconds,
    double ExactMatchMilliseconds,
    int TargetGroupLeakageCount,
    int FutureHeldLeakageCount,
    int HeldTailEncodedAsHeadCount,
    int DonorNestingViolationCount)
{
    public int CompletionTrials => Trials.Length;
    public int BaselineCompetingTrials => Trials.Count(x =>
        x.BaselineState == ChordCompletionReconstructionState.TargetAmongCompetingCompletions);
    public int ContextWitnessReferences => Trials.SelectMany(x => x.Views)
        .Sum(x => x.ContextualIndependentWitnessCount);
    public int BeatMillisecondMaterializationDifferences => Trials.Select(x => x.TargetContext)
        .DistinctBy(x => x.SourceGroupId)
        .Sum(x => (x.PreviousTransition is { BeatMaterializesToObservedMilliseconds: false } ? 1 : 0)
            + (x.NextTransition is { BeatMaterializesToObservedMilliseconds: false } ? 1 : 0));
}

/// <summary>
/// D0.1 research-only exact-context reconstruction. Views are independent descriptive queries, never a backoff
/// ladder. The model uses no RNG, score, probability, confidence, tolerance, window, similarity or generation state.
/// </summary>
public static class ExactCompletionContextResearch
{
    public const string ResearchSchemaVersion = "phase-d0-1-research.1";
    public static readonly ImmutableArray<ExactCompletionContextView> Views =
        Enum.GetValues<ExactCompletionContextView>().ToImmutableArray();

    public static ExactCompletionContextResearchResult Evaluate(ManiaChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        var baseline = ChordCompletionResearch.Evaluate(chart);
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var timeline = new BeatTimeline(chart.TimingPoints);

        var contextStarted = Stopwatch.GetTimestamp();
        var allGroups = ExactHeadRelationResearch.BuildAllGroups(profile);
        var contexts = BuildContexts(profile, timeline, allGroups, baseline.ChordGroups);
        var occurrences = baseline.Relations.SelectMany(relation => relation.Witnesses.Select(witness =>
                new ExactCompletionOccurrence(relation.Key, witness, contexts[witness.SourceGroupId])))
            .OrderBy(x => x.Witness.HeadTime).ThenBy(x => x.Witness.HeadBeat)
            .ThenBy(x => x.Witness.SourceGroupId, StringComparer.Ordinal)
            .ThenBy(x => x.Relation.RelationSignature, StringComparer.Ordinal).ToImmutableArray();
        var indexes = BuildIndexes(occurrences);
        var contextMs = Stopwatch.GetElapsedTime(contextStarted).TotalMilliseconds;

        var matchStarted = Stopwatch.GetTimestamp();
        var baselineTrials = baseline.Trials.ToDictionary(
            x => (x.TargetSourceGroupId, x.TargetObservationId));
        var trials = ImmutableArray.CreateBuilder<ExactCompletionCompetitionTrial>();
        foreach (var group in baseline.ChordGroups)
        foreach (var member in group.Members)
        {
            var original = baselineTrials[(group.SourceGroupId, member.ObservationId)];
            var context = contexts[group.SourceGroupId];
            var views = Views.Select(view => EvaluateView(original, context, view, indexes)).ToImmutableArray();
            trials.Add(new ExactCompletionCompetitionTrial(group.SourceGroupId, member.ObservationId,
                member.Lane, member.HeadType, group.Members.Length, group.Members.Length - 1,
                context.HeldBeforeHeadLanes.Length > 0, original.State, original.ComparableCompletionRelations,
                original.Query, context, views));
        }
        var trialArray = trials.ToImmutable();
        var matchMs = Stopwatch.GetElapsedTime(matchStarted).TotalMilliseconds;

        var targetLeakage = trialArray.SelectMany(x => x.Views).Sum(x => x.TargetGroupDonorLeakageCount);
        var futureLeakage = contexts.Values.Sum(context =>
            context.NextHeldBeforeObservationIdsExcludingCurrentGroup.Intersect(
                context.CurrentGroupObservationIds).Count());
        var heldTailAsHead = contexts.Values.Sum(x => x.HeldBeforeObservationIds
            .Intersect(x.CurrentGroupObservationIds).Count());
        var nestingViolations = CountNestingViolations(indexes, trialArray);

        return new ExactCompletionContextResearchResult(ResearchSchemaVersion, baseline.ChartFingerprint,
            baseline.KeyCount, baseline.OriginalObjectCount, baseline.ExactHeadGroupCount, baseline.ChordGroups,
            occurrences, trialArray, contextMs, matchMs, targetLeakage, futureLeakage, heldTailAsHead,
            nestingViolations);
    }

    public static ImmutableArray<ExactCompletionOccurrence> MatchDonors(
        IEnumerable<ExactCompletionOccurrence> occurrences,
        ExactCompletionCompetitionTrial trial,
        ExactCompletionContextView view)
    {
        var signature = Signature(view, trial.Query.ReducedStateSignature, trial.TargetContext);
        return occurrences.Where(x => x.Witness.SourceGroupId != trial.TargetSourceGroupId
                && Signature(view, x.Relation.ReducedStateSignature, x.Context) == signature)
            .ToImmutableArray();
    }

    public static string Signature(ExactCompletionContextView view, string reducedStateSignature,
        ExactCompletionContext context)
    {
        var previous = State(context.PreviousTransition?.Neighbor);
        var next = State(context.NextTransition?.Neighbor);
        var previousTransition = Transition(context.PreviousTransition);
        var nextTransition = Transition(context.NextTransition);
        var held = ChordCompletionResearch.LaneSignature(context.HeldBeforeHeadLanes);
        return view switch
        {
            ExactCompletionContextView.ReducedOnly => reducedStateSignature,
            ExactCompletionContextView.ReducedHeld => $"{reducedStateSignature}|H:{held}",
            ExactCompletionContextView.ReducedPrevious => $"{reducedStateSignature}|P:{previous}",
            ExactCompletionContextView.ReducedPreviousTransition =>
                $"{reducedStateSignature}|PT:{previousTransition}",
            ExactCompletionContextView.ReducedNext => $"{reducedStateSignature}|N:{next}",
            ExactCompletionContextView.ReducedNextTransition =>
                $"{reducedStateSignature}|NT:{nextTransition}",
            ExactCompletionContextView.ReducedPrevNext =>
                $"{reducedStateSignature}|PT:{previousTransition}|NT:{nextTransition}",
            ExactCompletionContextView.ReducedHeldPrevNext =>
                $"{reducedStateSignature}|H:{held}|PT:{previousTransition}|NT:{nextTransition}",
            _ => throw new ArgumentOutOfRangeException(nameof(view))
        };
    }

    private static Dictionary<string, ExactCompletionContext> BuildContexts(MapperEvidenceProfile profile,
        BeatTimeline timeline, ImmutableArray<SimultaneousOriginalEventGroup> allGroups,
        ImmutableArray<OriginalChordGroup> chordGroups)
    {
        var chordByTiming = chordGroups.ToDictionary(x => (x.HeadTime, x.HeadBeat));
        var result = new Dictionary<string, ExactCompletionContext>(StringComparer.Ordinal);
        for (var index = 0; index < allGroups.Length; index++)
        {
            var current = allGroups[index];
            if (!chordByTiming.TryGetValue((current.HeadTime, current.HeadBeat), out var chord)) continue;
            var previous = index > 0 ? allGroups[index - 1] : null;
            var next = index + 1 < allGroups.Length ? allGroups[index + 1] : null;
            var currentIds = current.Members.Select(x => x.ObservationId).OrderBy(x => x.Value).ToImmutableArray();
            var excluded = currentIds.ToHashSet();
            var safeNextHeld = next is null ? [] : profile.Observations.Where(x =>
                    x.Type == ManiaObjectType.LongNote && !excluded.Contains(x.Id)
                    && x.StartTime < next.HeadTime && x.StartBeat < next.HeadBeat
                    && x.EndTime >= next.HeadTime && x.EndBeat >= next.HeadBeat)
                .OrderBy(x => x.Lane).ThenBy(x => x.Id.Value).ToImmutableArray();
            var removed = next is null ? [] : profile.Observations.Where(x =>
                    excluded.Contains(x.Id) && x.Type == ManiaObjectType.LongNote
                    && x.StartTime < next.HeadTime && x.StartBeat < next.HeadBeat
                    && x.EndTime >= next.HeadTime && x.EndBeat >= next.HeadBeat)
                .Select(x => x.Id).OrderBy(x => x.Value).ToImmutableArray();
            result[chord.SourceGroupId] = new ExactCompletionContext(chord.SourceGroupId,
                chord.HeadTime, chord.HeadBeat, currentIds, chord.HeadState.HeldBeforeHeadLanes,
                chord.HeldBeforeObservationIds,
                previous is null ? null : Previous(timeline, previous, current, profile),
                next is null ? null : Next(timeline, current, next, profile),
                safeNextHeld.Select(x => x.Lane).Distinct().Order().ToImmutableArray(),
                safeNextHeld.Select(x => x.Id).ToImmutableArray(), removed);
        }
        return result;
    }

    private static ExactHeadTransitionContext Previous(BeatTimeline timeline,
        SimultaneousOriginalEventGroup previous, SimultaneousOriginalEventGroup current,
        MapperEvidenceProfile profile)
    {
        var delta = current.HeadBeat - previous.HeadBeat;
        return new ExactHeadTransitionContext(Head(previous, profile), delta,
            current.HeadTime - previous.HeadTime,
            timeline.ToTimeMilliseconds(previous.HeadBeat + delta) == current.HeadTime);
    }

    private static ExactHeadTransitionContext Next(BeatTimeline timeline,
        SimultaneousOriginalEventGroup current, SimultaneousOriginalEventGroup next,
        MapperEvidenceProfile profile)
    {
        var delta = next.HeadBeat - current.HeadBeat;
        return new ExactHeadTransitionContext(Head(next, profile), delta,
            next.HeadTime - current.HeadTime,
            timeline.ToTimeMilliseconds(current.HeadBeat + delta) == next.HeadTime);
    }

    private static ExactContextHeadState Head(SimultaneousOriginalEventGroup group,
        MapperEvidenceProfile profile)
    {
        var members = group.Members.OrderBy(x => x.Lane).ThenBy(x => x.ObjectType)
            .ThenBy(x => x.ObservationId.Value).ToArray();
        var ids = members.Select(x => x.ObservationId).OrderBy(x => x.Value).ToImmutableArray();
        var id = $"{profile.ChartFingerprint}/H-{group.HeadTime}-{D(group.HeadBeat)}-" +
            string.Join('-', ids.Select(x => x.ToString()));
        return new ExactContextHeadState(id, group.HeadTime, group.HeadBeat, profile.KeyCount,
            members.Where(x => x.ObjectType == ManiaObjectType.Tap).Select(x => x.Lane).Order().ToImmutableArray(),
            members.Where(x => x.ObjectType == ManiaObjectType.LongNote).Select(x => x.Lane).Order().ToImmutableArray(),
            ids);
    }

    private static Dictionary<ExactCompletionContextView, Dictionary<string, ImmutableArray<ExactCompletionOccurrence>>>
        BuildIndexes(ImmutableArray<ExactCompletionOccurrence> occurrences) => Views.ToDictionary(view => view,
            view => occurrences.GroupBy(x => Signature(view, x.Relation.ReducedStateSignature, x.Context),
                    StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.ToImmutableArray(), StringComparer.Ordinal));

    private static ExactContextViewEvaluation EvaluateView(ChordCompletionReconstructionTrial target,
        ExactCompletionContext context, ExactCompletionContextView view,
        Dictionary<ExactCompletionContextView, Dictionary<string, ImmutableArray<ExactCompletionOccurrence>>> indexes)
    {
        var signature = Signature(view, target.Query.ReducedStateSignature, context);
        var matches = Match(indexes, view, signature).Where(x =>
            x.Witness.SourceGroupId != target.TargetSourceGroupId).ToArray();
        var completions = matches.GroupBy(x => (x.Relation.CompletionLane, x.Relation.CompletionType)).ToArray();
        var exact = completions.SingleOrDefault(x => x.Key.CompletionLane == target.TargetLane
            && x.Key.CompletionType == target.TargetType);
        var targetSupported = exact is not null;
        var outcome = completions.Length == 0 ? ExactCompletionContextOutcome.NoComparableExactContext
            : !targetSupported && completions.Length == 1 ? ExactCompletionContextOutcome.UniqueWrongCompletion
            : !targetSupported ? ExactCompletionContextOutcome.ComparableTargetUnsupported
            : completions.Length == 1 ? ExactCompletionContextOutcome.TargetResolvedUnique
            : ExactCompletionContextOutcome.TargetSupportedStillCompeting;
        return new ExactContextViewEvaluation(view, signature, outcome, target.ComparableCompletionRelations,
            completions.Length, matches.Select(x => x.Witness.SourceGroupId).Distinct().Count(),
            exact?.Select(x => x.Witness.SourceGroupId).Distinct().Count() ?? 0, targetSupported,
            completions.Any(x => x.Key.CompletionLane == target.TargetLane
                && x.Key.CompletionType != target.TargetType),
            matches.Count(x => x.Witness.SourceGroupId == target.TargetSourceGroupId));
    }

    private static ImmutableArray<ExactCompletionOccurrence> Match(
        Dictionary<ExactCompletionContextView, Dictionary<string, ImmutableArray<ExactCompletionOccurrence>>> indexes,
        ExactCompletionContextView view, string signature) =>
        indexes[view].GetValueOrDefault(signature, []);

    private static int CountNestingViolations(
        Dictionary<ExactCompletionContextView, Dictionary<string, ImmutableArray<ExactCompletionOccurrence>>> indexes,
        ImmutableArray<ExactCompletionCompetitionTrial> trials)
    {
        var violations = 0;
        foreach (var trial in trials)
        {
            HashSet<string> Donors(ExactCompletionContextView view)
            {
                var signature = trial.Views.Single(x => x.View == view).ExactContextSignature;
                return Match(indexes, view, signature).Where(x =>
                        x.Witness.SourceGroupId != trial.TargetSourceGroupId)
                    .Select(x => x.Witness.SourceGroupId).ToHashSet(StringComparer.Ordinal);
            }
            var held = Donors(ExactCompletionContextView.ReducedHeld);
            var previous = Donors(ExactCompletionContextView.ReducedPrevious);
            var previousTransition = Donors(ExactCompletionContextView.ReducedPreviousTransition);
            var prevNext = Donors(ExactCompletionContextView.ReducedPrevNext);
            var heldPrevNext = Donors(ExactCompletionContextView.ReducedHeldPrevNext);
            if (!previousTransition.IsSubsetOf(previous)) violations++;
            if (!heldPrevNext.IsSubsetOf(held) || !heldPrevNext.IsSubsetOf(prevNext)) violations++;
        }
        return violations;
    }

    private static string State(ExactContextHeadState? state) => state?.StructuralSignature ?? "<ABSENT>";
    private static string Transition(ExactHeadTransitionContext? transition) => transition is null
        ? "<ABSENT>"
        : $"{transition.Neighbor.StructuralSignature}@DB:{D(transition.DeltaBeat)}";
    private static string D(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}

public static class ExactCompletionContextResearchJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(ExactCompletionContextResearchResult result) =>
        JsonSerializer.Serialize(result, Options);
}
