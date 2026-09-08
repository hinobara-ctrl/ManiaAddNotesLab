using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum CompositionStructuralInvalidity
{
    None,
    DuplicateCompletionMember,
    SameLaneCollision,
    LaneOutOfRange,
    MemberAlreadyPresent
}

public enum TargetCompositionReconstructionState
{
    ObservedJointUnique,
    ObservedJointAmongAlternatives,
    TargetAbsentFromJointEvidence,
    NoComparableCompositionContext
}

public enum HypotheticalCompositionState
{
    JointObserved,
    MarginalOnly,
    HardInvalid,
    NotMarginallySupported
}

public enum CompletionPairType { TapTap, TapLongNote, LongNoteLongNote }

public readonly record struct CompletionMemberIdentity(int Lane, OriginalHeadMemberType HeadType)
{
    public string Signature => $"{Lane.ToString(CultureInfo.InvariantCulture)}:{HeadType}";
}

public sealed class CompletionSetIdentity : IEquatable<CompletionSetIdentity>
{
    public ImmutableArray<CompletionMemberIdentity> Members { get; }
    public string Signature { get; }

    private CompletionSetIdentity(ImmutableArray<CompletionMemberIdentity> members)
    {
        Members = members;
        Signature = string.Join(',', members.Select(x => x.Signature));
    }

    public static CompletionSetIdentity Create(IEnumerable<CompletionMemberIdentity> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var supplied = members.ToArray();
        var canonical = supplied.OrderBy(x => x.Lane).ThenBy(x => x.HeadType).ToArray();
        if (canonical.Distinct().Count() != canonical.Length)
            throw new ArgumentException("A CompletionSet cannot contain duplicate members.", nameof(members));
        return new CompletionSetIdentity(canonical.ToImmutableArray());
    }

    public bool Equals(CompletionSetIdentity? other) => other is not null
        && string.Equals(Signature, other.Signature, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is CompletionSetIdentity other && Equals(other);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Signature);
    public override string ToString() => Signature;
}

public sealed record ResultingHeadStateIdentity(
    int KeyCount,
    ImmutableArray<int> TapHeadLanes,
    ImmutableArray<int> LongNoteHeadLanes)
{
    public string Signature => ChordCompletionResearch.HeadSignature(KeyCount, TapHeadLanes, LongNoteHeadLanes);
}

public sealed record ObservedJointCompletionSet(
    string ChartFingerprint,
    string SourceGroupId,
    int HeadTime,
    decimal HeadBeat,
    string ReducedStateSignature,
    CompletionSetIdentity CompletionSet,
    ResultingHeadStateIdentity FullResultingState,
    ImmutableArray<int> HeldBeforeHeadLanes,
    ImmutableArray<OriginalObservationId> FullOriginalMemberObservationIds,
    ImmutableArray<OriginalObservationId> VisibleReducedMemberObservationIds,
    ImmutableArray<OriginalObservationId> CompletionObservationIds,
    ExactCompletionContext Context);

public sealed record CompositionViewAudit(
    ExactCompletionContextView View,
    string ExactContextSignature,
    TargetCompositionReconstructionState TargetState,
    int ComparableDonorGroupCount,
    int DistinctJointCompletionSetCount,
    int TargetJointDonorGroupCount,
    int MarginalMemberCount,
    bool FirstTargetMemberMarginallySupported,
    bool SecondTargetMemberMarginallySupported,
    bool BothTargetMembersMarginallySupported,
    bool TargetCompletionSetJointlyObserved,
    bool TargetMarginalOnly,
    int MarginalPairCandidates,
    int EligibleMarginalPairs,
    int HardInvalidPairs,
    int JointObservedPairs,
    int MarginalOnlyPairs,
    int TargetWholeGroupLeakageCount,
    int TargetObservationLeakageCount,
    int JointWitnessCrossOccurrenceLeakageCount);

public sealed record PairHoldoutTrial(
    string TrialId,
    string TargetSourceGroupId,
    int TargetHeadTime,
    decimal TargetHeadBeat,
    int OriginalHeadCount,
    int ReducedHeadCount,
    bool HeldBeforePresent,
    CompletionPairType PairType,
    string ReducedStateSignature,
    CompletionSetIdentity TargetCompletionSet,
    ResultingHeadStateIdentity TargetResultingState,
    ImmutableArray<OriginalObservationId> TargetGroupObservationIds,
    ImmutableArray<OriginalObservationId> RemovedObservationIds,
    ImmutableArray<CompositionViewAudit> Views);

public sealed record ResultingStateCompositionResearchResult(
    string ResearchSchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    int ExactHeadGroupCount,
    int K1ControlTrials,
    int K1ControlComparable,
    int K1ControlTargetSupported,
    ImmutableArray<ObservedJointCompletionSet> ObservedPairOccurrences,
    ImmutableArray<PairHoldoutTrial> PairHoldoutTrials,
    int TargetWholeGroupLeakageCount,
    int TargetObservationLeakageCount,
    int FutureHeldLeakageCount,
    int SyntheticTeachingCount,
    int CrossChartEvidenceCount,
    int HeldTailAsHeadCount,
    int JointWitnessCrossOccurrenceLeakageCount,
    int OrderInvarianceViolationCount,
    long PairEnumerationOperations,
    [property: JsonIgnore] double BuildMilliseconds,
    [property: JsonIgnore] double EvaluationMilliseconds)
{
    public int EligiblePairHoldoutTargets => PairHoldoutTrials.Length;
}

/// <summary>
/// D1.0 research-only exact composition audit. It evaluates original pair holdouts and hypothetical sets without
/// accepting candidates, mutating generation state, selecting a context view, consuming RNG, or inferring style.
/// </summary>
public static class ResultingStateCompositionResearch
{
    public const string ResearchSchemaVersion = "phase-d1-0-resulting-state-composition-shadow.1";

    public static ResultingStateCompositionResearchResult Evaluate(ManiaChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        var started = Stopwatch.GetTimestamp();
        var contextResearch = ExactCompletionContextResearch.Evaluate(chart);
        var baseline = ChordCompletionResearch.Evaluate(chart);
        var contexts = contextResearch.Trials.GroupBy(x => x.TargetSourceGroupId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First().TargetContext, StringComparer.Ordinal);
        var occurrences = BuildPairOccurrences(baseline.ChartFingerprint, baseline.ChordGroups, contexts);
        var indexes = BuildIndexes(occurrences);
        var buildMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        var evaluationStarted = Stopwatch.GetTimestamp();
        var trials = ImmutableArray.CreateBuilder<PairHoldoutTrial>();
        long pairOperations = 0;
        var orderViolations = 0;
        foreach (var group in baseline.ChordGroups.OrderBy(x => x.HeadTime).ThenBy(x => x.SourceGroupId,
                     StringComparer.Ordinal))
        {
            var context = contexts[group.SourceGroupId];
            for (var left = 0; left < group.Members.Length - 1; left++)
            for (var right = left + 1; right < group.Members.Length; right++)
            {
                pairOperations++;
                var first = Identity(group.Members[left]);
                var second = Identity(group.Members[right]);
                var targetSet = CompletionSetIdentity.Create([first, second]);
                var reverse = CompletionSetIdentity.Create([second, first]);
                if (!targetSet.Equals(reverse)) orderViolations++;
                var reducedMembers = group.Members.Where((_, index) => index != left && index != right).ToArray();
                var reducedSignature = HeadSignature(group.HeadState.KeyCount, reducedMembers);
                var targetResult = ResultingState(group.HeadState.KeyCount, reducedMembers, targetSet);
                if (!string.Equals(targetResult.Signature, group.HeadState.StructuralSignature,
                        StringComparison.Ordinal))
                    orderViolations++;
                var views = ExactCompletionContextResearch.Views.Select(view => EvaluateView(
                    group, reducedMembers, targetSet, context, view, indexes, ref pairOperations))
                    .ToImmutableArray();
                var removedIds = new[] { group.Members[left].ObservationId, group.Members[right].ObservationId }
                    .OrderBy(x => x.Value).ToImmutableArray();
                trials.Add(new PairHoldoutTrial(
                    $"{group.SourceGroupId}/P-{removedIds[0]}-{removedIds[1]}", group.SourceGroupId,
                    group.HeadTime, group.HeadBeat, group.Members.Length, reducedMembers.Length,
                    context.HeldBeforeHeadLanes.Length > 0, PairType(first, second), reducedSignature,
                    targetSet, targetResult, group.HeadState.MemberObservationIds, removedIds, views));
            }
        }

        var trialArray = trials.ToImmutable();
        var evaluationMs = Stopwatch.GetElapsedTime(evaluationStarted).TotalMilliseconds;
        var k1Comparable = baseline.Trials.Count(x =>
            x.State != ChordCompletionReconstructionState.NoComparableReducedState);
        var targetLeakage = trialArray.SelectMany(x => x.Views).Sum(x => x.TargetWholeGroupLeakageCount);
        var observationLeakage = trialArray.SelectMany(x => x.Views).Sum(x => x.TargetObservationLeakageCount);
        var crossOccurrence = trialArray.SelectMany(x => x.Views)
            .Sum(x => x.JointWitnessCrossOccurrenceLeakageCount);
        var crossChart = occurrences.Count(x => !string.Equals(x.ChartFingerprint,
            baseline.ChartFingerprint, StringComparison.Ordinal));
        return new ResultingStateCompositionResearchResult(ResearchSchemaVersion,
            baseline.ChartFingerprint, baseline.KeyCount, baseline.OriginalObjectCount,
            baseline.ExactHeadGroupCount, baseline.CompletionTrials, k1Comparable,
            baseline.Trials.Count(x => x.ExactTargetCompletionSupported), occurrences, trialArray,
            targetLeakage, observationLeakage, contextResearch.FutureHeldLeakageCount,
            CountSyntheticTeaching(chart, occurrences), crossChart, contextResearch.HeldTailEncodedAsHeadCount,
            crossOccurrence, orderViolations, pairOperations, buildMs, evaluationMs);
    }

    public static CompositionStructuralInvalidity ValidateComposition(int keyCount,
        IEnumerable<CompletionMemberIdentity> reducedMembers,
        IEnumerable<CompletionMemberIdentity> completionMembers)
    {
        var reduced = reducedMembers.ToArray();
        var completion = completionMembers.ToArray();
        if (completion.Distinct().Count() != completion.Length)
            return CompositionStructuralInvalidity.DuplicateCompletionMember;
        if (reduced.Concat(completion).Any(x => x.Lane < 0 || x.Lane >= keyCount))
            return CompositionStructuralInvalidity.LaneOutOfRange;
        if (completion.Any(reduced.Contains)) return CompositionStructuralInvalidity.MemberAlreadyPresent;
        if (reduced.Concat(completion).GroupBy(x => x.Lane).Any(x => x.Count() > 1))
            return CompositionStructuralInvalidity.SameLaneCollision;
        return CompositionStructuralInvalidity.None;
    }

    public static ResultingHeadStateIdentity ResultingState(int keyCount,
        IEnumerable<OriginalHeadMember> reducedMembers, CompletionSetIdentity completionSet)
    {
        var reduced = reducedMembers.Select(Identity).ToArray();
        var invalid = ValidateComposition(keyCount, reduced, completionSet.Members);
        if (invalid != CompositionStructuralInvalidity.None)
            throw new InvalidOperationException($"Cannot form resulting state: {invalid}.");
        var all = reduced.Concat(completionSet.Members).ToArray();
        return new ResultingHeadStateIdentity(keyCount,
            all.Where(x => x.HeadType == OriginalHeadMemberType.TapHead).Select(x => x.Lane)
                .Order().ToImmutableArray(),
            all.Where(x => x.HeadType == OriginalHeadMemberType.LongNoteHead).Select(x => x.Lane)
                .Order().ToImmutableArray());
    }

    public static HypotheticalCompositionState ClassifyHypothetical(int keyCount,
        IEnumerable<CompletionMemberIdentity> reducedMembers, CompletionSetIdentity candidate,
        IEnumerable<CompletionMemberIdentity> marginalMembers,
        IEnumerable<CompletionSetIdentity> observedJointSets)
    {
        if (ValidateComposition(keyCount, reducedMembers, candidate.Members) !=
            CompositionStructuralInvalidity.None) return HypotheticalCompositionState.HardInvalid;
        var marginal = marginalMembers.ToHashSet();
        if (candidate.Members.Any(x => !marginal.Contains(x)))
            return HypotheticalCompositionState.NotMarginallySupported;
        return observedJointSets.Any(x => x.Equals(candidate))
            ? HypotheticalCompositionState.JointObserved
            : HypotheticalCompositionState.MarginalOnly;
    }

    private static ImmutableArray<ObservedJointCompletionSet> BuildPairOccurrences(string chartFingerprint,
        ImmutableArray<OriginalChordGroup> groups,
        IReadOnlyDictionary<string, ExactCompletionContext> contexts)
    {
        var result = ImmutableArray.CreateBuilder<ObservedJointCompletionSet>();
        foreach (var group in groups.OrderBy(x => x.HeadTime).ThenBy(x => x.SourceGroupId,
                     StringComparer.Ordinal))
        for (var left = 0; left < group.Members.Length - 1; left++)
        for (var right = left + 1; right < group.Members.Length; right++)
        {
            var completion = CompletionSetIdentity.Create([
                Identity(group.Members[left]), Identity(group.Members[right])
            ]);
            var reduced = group.Members.Where((_, index) => index != left && index != right).ToArray();
            result.Add(new ObservedJointCompletionSet(chartFingerprint, group.SourceGroupId,
                group.HeadTime, group.HeadBeat,
                HeadSignature(group.HeadState.KeyCount, reduced), completion,
                ResultingState(group.HeadState.KeyCount, reduced, completion),
                group.HeadState.HeldBeforeHeadLanes, group.HeadState.MemberObservationIds,
                reduced.Select(x => x.ObservationId).OrderBy(x => x.Value).ToImmutableArray(),
                new[] { group.Members[left].ObservationId, group.Members[right].ObservationId }
                    .OrderBy(x => x.Value).ToImmutableArray(), contexts[group.SourceGroupId]));
        }
        return result.ToImmutable();
    }

    private static Dictionary<ExactCompletionContextView, Dictionary<string,
        ImmutableArray<ObservedJointCompletionSet>>> BuildIndexes(
        ImmutableArray<ObservedJointCompletionSet> occurrences) =>
        ExactCompletionContextResearch.Views.ToDictionary(view => view,
            view => occurrences.GroupBy(x => ExactCompletionContextResearch.Signature(view,
                        x.ReducedStateSignature, x.Context), StringComparer.Ordinal)
                .ToDictionary(x => x.Key,
                    x => x.OrderBy(y => y.SourceGroupId, StringComparer.Ordinal)
                        .ThenBy(y => y.CompletionSet.Signature, StringComparer.Ordinal).ToImmutableArray(),
                    StringComparer.Ordinal));

    private static CompositionViewAudit EvaluateView(OriginalChordGroup targetGroup,
        IReadOnlyList<OriginalHeadMember> reducedMembers, CompletionSetIdentity targetSet,
        ExactCompletionContext targetContext, ExactCompletionContextView view,
        IReadOnlyDictionary<ExactCompletionContextView, Dictionary<string,
            ImmutableArray<ObservedJointCompletionSet>>> indexes, ref long pairOperations)
    {
        var reducedSignature = HeadSignature(targetGroup.HeadState.KeyCount, reducedMembers);
        var signature = ExactCompletionContextResearch.Signature(view, reducedSignature, targetContext);
        var raw = indexes[view].GetValueOrDefault(signature, []);
        var matches = raw.Where(x => x.SourceGroupId != targetGroup.SourceGroupId).ToArray();
        var targetLeakage = matches.Count(x => x.SourceGroupId == targetGroup.SourceGroupId);
        var targetIds = targetGroup.HeadState.MemberObservationIds.ToHashSet();
        var observationLeakage = matches.Count(x => x.FullOriginalMemberObservationIds.Any(targetIds.Contains));
        var jointSets = matches.Select(x => x.CompletionSet).Distinct().OrderBy(x => x.Signature,
            StringComparer.Ordinal).ToArray();
        var targetJointDonors = matches.Where(x => x.CompletionSet.Equals(targetSet))
            .Select(x => x.SourceGroupId).Distinct(StringComparer.Ordinal).Count();
        var marginal = jointSets.SelectMany(x => x.Members).Distinct().OrderBy(x => x.Lane)
            .ThenBy(x => x.HeadType).ToArray();
        var firstMarginal = marginal.Contains(targetSet.Members[0]);
        var secondMarginal = marginal.Contains(targetSet.Members[1]);
        var targetJoint = targetJointDonors > 0;
        var state = matches.Length == 0
            ? TargetCompositionReconstructionState.NoComparableCompositionContext
            : !targetJoint ? TargetCompositionReconstructionState.TargetAbsentFromJointEvidence
            : jointSets.Length == 1 ? TargetCompositionReconstructionState.ObservedJointUnique
            : TargetCompositionReconstructionState.ObservedJointAmongAlternatives;

        var candidates = 0;
        var eligible = 0;
        var hardInvalid = 0;
        var joint = 0;
        var marginalOnly = 0;
        var reducedIdentity = reducedMembers.Select(Identity).ToArray();
        for (var left = 0; left < marginal.Length - 1; left++)
        for (var right = left + 1; right < marginal.Length; right++)
        {
            candidates++;
            pairOperations++;
            var candidate = CompletionSetIdentity.Create([marginal[left], marginal[right]]);
            var classification = ClassifyHypothetical(targetGroup.HeadState.KeyCount, reducedIdentity,
                candidate, marginal, jointSets);
            switch (classification)
            {
                case HypotheticalCompositionState.HardInvalid: hardInvalid++; break;
                case HypotheticalCompositionState.JointObserved: eligible++; joint++; break;
                case HypotheticalCompositionState.MarginalOnly: eligible++; marginalOnly++; break;
            }
        }

        var crossOccurrenceLeakage = matches.Count(x => x.CompletionObservationIds.Length != 2
            || x.CompletionObservationIds.Any(id => !x.FullOriginalMemberObservationIds.Contains(id)));
        return new CompositionViewAudit(view, signature, state,
            matches.Select(x => x.SourceGroupId).Distinct(StringComparer.Ordinal).Count(), jointSets.Length,
            targetJointDonors, marginal.Length, firstMarginal, secondMarginal,
            firstMarginal && secondMarginal, targetJoint, firstMarginal && secondMarginal && !targetJoint,
            candidates, eligible, hardInvalid, joint, marginalOnly, targetLeakage, observationLeakage,
            crossOccurrenceLeakage);
    }

    private static int CountSyntheticTeaching(ManiaChart chart,
        ImmutableArray<ObservedJointCompletionSet> occurrences)
    {
        var originalIds = MapperEvidenceProfileBuilder.Build(chart).Observations.Select(x => x.Id).ToHashSet();
        return occurrences.Count(x => x.FullOriginalMemberObservationIds.Any(id => !originalIds.Contains(id)));
    }

    private static CompletionPairType PairType(CompletionMemberIdentity first,
        CompletionMemberIdentity second) => (first.HeadType, second.HeadType) switch
    {
        (OriginalHeadMemberType.TapHead, OriginalHeadMemberType.TapHead) => CompletionPairType.TapTap,
        (OriginalHeadMemberType.LongNoteHead, OriginalHeadMemberType.LongNoteHead) =>
            CompletionPairType.LongNoteLongNote,
        _ => CompletionPairType.TapLongNote
    };

    private static CompletionMemberIdentity Identity(OriginalHeadMember member) =>
        new(member.Lane, member.HeadType);

    private static string HeadSignature(int keyCount, IEnumerable<OriginalHeadMember> members) =>
        ChordCompletionResearch.HeadSignature(keyCount,
            members.Where(x => x.HeadType == OriginalHeadMemberType.TapHead).Select(x => x.Lane),
            members.Where(x => x.HeadType == OriginalHeadMemberType.LongNoteHead).Select(x => x.Lane));
}

public static class ResultingStateCompositionResearchJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(ResultingStateCompositionResearchResult result) =>
        JsonSerializer.Serialize(result, Options);
}
