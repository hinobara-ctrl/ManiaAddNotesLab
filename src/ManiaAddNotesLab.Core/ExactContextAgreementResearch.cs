using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum CompletionSetRelation
{
    OneOrBothNotComparable, EqualCompletionSet, LeftStrictSubset, RightStrictSubset, PartialOverlap, Disjoint
}

public enum PairTargetSupport
{
    TargetSupportedByBoth, TargetSupportedLeftOnly, TargetSupportedRightOnly, TargetSupportedByNeither
}

public enum DonorSetRelation
{
    EmptyOrNotComparable, IdenticalDonorSet, LeftStrictSubset, RightStrictSubset, PartialOverlap, Disjoint
}

public enum CompletionIntersectionState
{
    NotComparable, IntersectionEmpty, IntersectionMulti, IntersectionSingletonTarget, IntersectionSingletonWrong
}

public enum ViewPairKind { DependencyOrNesting, CrossDimension }
public enum UniqueViewAgreement
{
    NoUniqueView, SingleUniqueViewOnly, UniqueViewsAgreeOnTarget,
    UniqueViewsAgreeOnSameWrongCompletion, UniqueViewsDisagree
}
public enum InformativeViewMultiplicity { NoInformativeContextView, OneInformativeView, MultipleInformativeViews }
public enum CompletionSetConsensus { NotApplicable, MultipleViewsAllCompletionSetsEqual, MultipleViewsOverlapping, MultipleViewsDisjoint }
public enum ComparableTargetSupportState
{
    NoComparableView, TargetSupportedByAllComparableViews,
    TargetSupportedBySomeComparableViews, TargetSupportedByNone
}
public enum SpecificityStability
{
    NoComparableAtNextLevel, TargetLost, StillCompeting, BecomesUniqueTarget, BecomesUniqueWrong,
    TargetStillUnsupported
}
public enum ObservedJointContextKind { PreviousNext, HeldPrevNext }
public enum JointCompletionSupport
{
    MarginalAgreeAndJointSupported,
    MarginalAgreeButNoJointComparable,
    MarginalAgreeButJointDoesNotSupportCompletion,
    MarginalsDisagreeButJointSupported
}

public sealed record ExactCompletionIdentity(int Lane, OriginalHeadMemberType HeadType);
public sealed record CompletionDonorGroups(
    ExactCompletionIdentity Completion, ImmutableArray<string> DonorGroupIds);
public sealed record ContextViewObservation(
    ExactCompletionContextView ViewId,
    string ExactContextSignature,
    bool Comparable,
    ImmutableArray<CompletionDonorGroups> CompletionSet,
    bool TargetInSet,
    ExactCompletionIdentity? UniqueCompletion,
    int IndependentDonorCount);
public sealed record DonorSetComparison(
    ExactCompletionIdentity Completion,
    DonorSetRelation Relation,
    int LeftCount,
    int RightCount,
    int IntersectionCount);
public sealed record ViewPairAgreement(
    ExactCompletionContextView LeftView,
    ExactCompletionContextView RightView,
    ViewPairKind PairKind,
    CompletionSetRelation CompletionRelation,
    PairTargetSupport TargetSupport,
    CompletionIntersectionState IntersectionState,
    int IntersectionCount,
    int UnionCount,
    ImmutableArray<DonorSetComparison> DonorComparisons);
public sealed record UniqueViewSelection(
    ExactCompletionContextView ViewId, ExactCompletionIdentity Completion);
public sealed record ViewConflictRecord(
    string TargetSourceGroupId,
    OriginalObservationId TargetObservationId,
    ImmutableArray<UniqueViewSelection> Selections,
    bool IncludesTargetAndWrong);
public sealed record SpecificityStabilityRecord(
    ExactCompletionContextView LessSpecific,
    ExactCompletionContextView MoreSpecific,
    SpecificityStability State);
public sealed record JointCompletionObservation(
    ExactCompletionIdentity Completion, JointCompletionSupport Support);
public sealed record ObservedJointContextSupport(
    ObservedJointContextKind Kind,
    ExactCompletionContextView LeftMarginal,
    ExactCompletionContextView RightMarginal,
    ExactCompletionContextView JointView,
    bool LeftComparable,
    bool RightComparable,
    bool JointComparable,
    ImmutableArray<ExactCompletionIdentity> MarginalIntersection,
    ImmutableArray<ExactCompletionIdentity> JointCompletionSet,
    ImmutableArray<JointCompletionObservation> CompletionObservations,
    bool JointUniqueTarget,
    bool JointUniqueWrong);
public sealed record ContextAgreementCertificate(
    string TargetSourceGroupId,
    OriginalObservationId TargetObservationId,
    int TargetLane,
    OriginalHeadMemberType TargetType,
    int OriginalChordHeadCount,
    int ReducedVisibleMemberCount,
    bool HeldBeforePresent,
    ChordCompletionReconstructionState BaselineState,
    int BaselineCompletionCount,
    string CoverageSignature,
    int ComparableViewCount,
    int TargetResolvingViewCount,
    int UniqueWrongViewCount,
    int DistinctDonorGroupCountAcrossViews,
    InformativeViewMultiplicity InformativeMultiplicity,
    CompletionSetConsensus CompletionSetConsensus,
    UniqueViewAgreement UniqueAgreement,
    ComparableTargetSupportState TargetSupportState,
    ImmutableArray<ContextViewObservation> Views,
    ImmutableArray<ViewPairAgreement> ViewPairs,
    ImmutableArray<SpecificityStabilityRecord> Stability,
    ImmutableArray<ObservedJointContextSupport> JointContexts,
    ViewConflictRecord? Conflict);
public sealed record ExactContextAgreementResearchResult(
    string ResearchSchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    int ExactHeadGroupCount,
    ImmutableArray<ExactCompletionOccurrence> DonorOccurrences,
    ImmutableArray<ContextAgreementCertificate> Certificates,
    double AgreementBuildMilliseconds,
    double PairwiseComparisonMilliseconds,
    double JointWitnessComparisonMilliseconds,
    int TargetGroupLeakageCount,
    int FutureHeldLeakageCount,
    int HeldTailEncodedAsHeadCount,
    int DonorNestingViolationCount)
{
    public int Trials => Certificates.Length;
    public int PrimaryCompetingTrials => Certificates.Count(x =>
        x.BaselineState == ChordCompletionReconstructionState.TargetAmongCompetingCompletions);
    public int ViewObservations => Certificates.Sum(x => x.Views.Length);
    public int PairwiseRecords => Certificates.Sum(x => x.ViewPairs.Length);
    public int ConflictRecords => Certificates.Count(x => x.Conflict is not null);
    public int DonorReferences => Certificates.SelectMany(x => x.Views)
        .SelectMany(x => x.CompletionSet).Sum(x => x.DonorGroupIds.Length);
}

/// <summary>
/// D0.2 research-only description of exact view coverage, agreement, donor overlap and observed joint context.
/// It never ranks, votes, generates, or treats marginal set intersection as a joint observation.
/// </summary>
public static class ExactContextAgreementResearch
{
    public const string ResearchSchemaVersion = "phase-d0-2-research.2";
    public static readonly ImmutableArray<(ExactCompletionContextView Parent, ExactCompletionContextView Child)>
        DependencyGraph = ExactContextViewDependencies.Graph;

    public static ExactContextAgreementResearchResult Evaluate(ManiaChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        var source = ExactCompletionContextResearch.Evaluate(chart);
        var indexes = ExactCompletionContextResearch.Views.ToDictionary(view => view,
            view => source.Occurrences.GroupBy(x =>
                    ExactCompletionContextResearch.Signature(view, x.Relation.ReducedStateSignature, x.Context),
                    StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.ToImmutableArray(), StringComparer.Ordinal));

        var agreementTicks = 0L;
        var pairTicks = 0L;
        var jointTicks = 0L;
        var certificates = ImmutableArray.CreateBuilder<ContextAgreementCertificate>(source.Trials.Length);
        foreach (var trial in source.Trials)
        {
            var started = Stopwatch.GetTimestamp();
            var views = ExactCompletionContextResearch.Views.Select(view =>
                Observe(trial, view, indexes[view])).ToImmutableArray();
            agreementTicks += Stopwatch.GetTimestamp() - started;

            started = Stopwatch.GetTimestamp();
            var pairs = Pairs(views, trial).ToImmutableArray();
            var stability = Stability(views, trial).ToImmutableArray();
            pairTicks += Stopwatch.GetTimestamp() - started;

            started = Stopwatch.GetTimestamp();
            var joints = ImmutableArray.Create(
                Joint(ObservedJointContextKind.PreviousNext,
                    ExactCompletionContextView.ReducedPreviousTransition,
                    ExactCompletionContextView.ReducedNextTransition,
                    ExactCompletionContextView.ReducedPrevNext, views, trial),
                Joint(ObservedJointContextKind.HeldPrevNext,
                    ExactCompletionContextView.ReducedHeld,
                    ExactCompletionContextView.ReducedPrevNext,
                    ExactCompletionContextView.ReducedHeldPrevNext, views, trial));
            jointTicks += Stopwatch.GetTimestamp() - started;

            certificates.Add(Certificate(trial, views, pairs, stability, joints));
        }

        return new ExactContextAgreementResearchResult(ResearchSchemaVersion, source.ChartFingerprint,
            source.KeyCount, source.OriginalObjectCount, source.ExactHeadGroupCount, source.Occurrences,
            certificates.ToImmutable(), Milliseconds(agreementTicks), Milliseconds(pairTicks),
            Milliseconds(jointTicks), source.TargetGroupLeakageCount, source.FutureHeldLeakageCount,
            source.HeldTailEncodedAsHeadCount, source.DonorNestingViolationCount);
    }

    public static CompletionSetRelation CompareCompletionSets(
        bool leftComparable, IEnumerable<ExactCompletionIdentity> left,
        bool rightComparable, IEnumerable<ExactCompletionIdentity> right)
    {
        if (!leftComparable || !rightComparable) return CompletionSetRelation.OneOrBothNotComparable;
        var l = left.ToHashSet();
        var r = right.ToHashSet();
        if (l.SetEquals(r)) return CompletionSetRelation.EqualCompletionSet;
        if (l.IsProperSubsetOf(r)) return CompletionSetRelation.LeftStrictSubset;
        if (r.IsProperSubsetOf(l)) return CompletionSetRelation.RightStrictSubset;
        if (l.Overlaps(r)) return CompletionSetRelation.PartialOverlap;
        return CompletionSetRelation.Disjoint;
    }

    public static DonorSetComparison CompareDonors(ExactCompletionIdentity completion,
        bool leftComparable, IEnumerable<string> left, bool rightComparable, IEnumerable<string> right)
    {
        var l = left.ToHashSet(StringComparer.Ordinal);
        var r = right.ToHashSet(StringComparer.Ordinal);
        var intersection = l.Intersect(r, StringComparer.Ordinal).Count();
        var relation = !leftComparable || !rightComparable || l.Count == 0 || r.Count == 0
            ? DonorSetRelation.EmptyOrNotComparable
            : l.SetEquals(r) ? DonorSetRelation.IdenticalDonorSet
            : l.IsProperSubsetOf(r) ? DonorSetRelation.LeftStrictSubset
            : r.IsProperSubsetOf(l) ? DonorSetRelation.RightStrictSubset
            : l.Overlaps(r) ? DonorSetRelation.PartialOverlap
            : DonorSetRelation.Disjoint;
        return new DonorSetComparison(completion, relation, l.Count, r.Count, intersection);
    }

    public static string CoverageSignature(IEnumerable<ContextViewObservation> views) =>
        string.Concat(ExactCompletionContextResearch.Views.Select(view =>
            views.Single(x => x.ViewId == view).Comparable ? '1' : '0'));

    public static UniqueViewAgreement ClassifyUniqueViews(
        IEnumerable<ExactCompletionIdentity> uniqueCompletions, ExactCompletionIdentity target)
    {
        var unique = uniqueCompletions.ToArray();
        return unique.Length == 0 ? UniqueViewAgreement.NoUniqueView
            : unique.Length == 1 ? UniqueViewAgreement.SingleUniqueViewOnly
            : unique.Distinct().Count() > 1 ? UniqueViewAgreement.UniqueViewsDisagree
            : unique[0] == target ? UniqueViewAgreement.UniqueViewsAgreeOnTarget
            : UniqueViewAgreement.UniqueViewsAgreeOnSameWrongCompletion;
    }

    public static SpecificityStability ClassifySpecificityStability(
        ContextViewObservation lessSpecific, ContextViewObservation moreSpecific,
        ExactCompletionIdentity target) =>
        !moreSpecific.Comparable ? SpecificityStability.NoComparableAtNextLevel
        : lessSpecific.TargetInSet && !moreSpecific.TargetInSet ? SpecificityStability.TargetLost
        : moreSpecific.UniqueCompletion == target ? SpecificityStability.BecomesUniqueTarget
        : moreSpecific.UniqueCompletion is not null ? SpecificityStability.BecomesUniqueWrong
        : moreSpecific.TargetInSet ? SpecificityStability.StillCompeting
        : SpecificityStability.TargetStillUnsupported;

    private static ContextViewObservation Observe(ExactCompletionCompetitionTrial trial,
        ExactCompletionContextView view,
        IReadOnlyDictionary<string, ImmutableArray<ExactCompletionOccurrence>> index)
    {
        var signature = trial.Views.Single(x => x.View == view).ExactContextSignature;
        var matches = index.GetValueOrDefault(signature, []).Where(x =>
            x.Witness.SourceGroupId != trial.TargetSourceGroupId).ToArray();
        var completions = matches.GroupBy(x => new ExactCompletionIdentity(
                x.Relation.CompletionLane, x.Relation.CompletionType))
            .OrderBy(x => x.Key.Lane).ThenBy(x => x.Key.HeadType)
            .Select(x => new CompletionDonorGroups(x.Key, x.Select(y => y.Witness.SourceGroupId)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray()))
            .ToImmutableArray();
        var target = Target(trial);
        return new ContextViewObservation(view, signature, completions.Length > 0, completions,
            completions.Any(x => x.Completion == target),
            completions.Length == 1 ? completions[0].Completion : null,
            completions.SelectMany(x => x.DonorGroupIds).Distinct(StringComparer.Ordinal).Count());
    }

    private static IEnumerable<ViewPairAgreement> Pairs(ImmutableArray<ContextViewObservation> views,
        ExactCompletionCompetitionTrial trial)
    {
        for (var leftIndex = 0; leftIndex < views.Length; leftIndex++)
        for (var rightIndex = leftIndex + 1; rightIndex < views.Length; rightIndex++)
        {
            var left = views[leftIndex];
            var right = views[rightIndex];
            var leftSet = left.CompletionSet.Select(x => x.Completion).ToHashSet();
            var rightSet = right.CompletionSet.Select(x => x.Completion).ToHashSet();
            var intersection = leftSet.Intersect(rightSet).ToArray();
            var union = leftSet.Union(rightSet).OrderBy(x => x.Lane).ThenBy(x => x.HeadType).ToArray();
            var target = Target(trial);
            var intersectionState = !left.Comparable || !right.Comparable
                ? CompletionIntersectionState.NotComparable
                : intersection.Length == 0 ? CompletionIntersectionState.IntersectionEmpty
                : intersection.Length > 1 ? CompletionIntersectionState.IntersectionMulti
                : intersection[0] == target ? CompletionIntersectionState.IntersectionSingletonTarget
                : CompletionIntersectionState.IntersectionSingletonWrong;
            var donor = union.Select(completion => CompareDonors(completion, left.Comparable,
                    left.CompletionSet.SingleOrDefault(x => x.Completion == completion)?.DonorGroupIds ?? [],
                    right.Comparable,
                    right.CompletionSet.SingleOrDefault(x => x.Completion == completion)?.DonorGroupIds ?? []))
                .ToImmutableArray();
            yield return new ViewPairAgreement(left.ViewId, right.ViewId,
                ExactContextViewDependencies.IsDependent(left.ViewId, right.ViewId)
                    ? ViewPairKind.DependencyOrNesting : ViewPairKind.CrossDimension,
                CompareCompletionSets(left.Comparable, leftSet, right.Comparable, rightSet),
                left.TargetInSet && right.TargetInSet ? PairTargetSupport.TargetSupportedByBoth
                    : left.TargetInSet ? PairTargetSupport.TargetSupportedLeftOnly
                    : right.TargetInSet ? PairTargetSupport.TargetSupportedRightOnly
                    : PairTargetSupport.TargetSupportedByNeither,
                intersectionState, intersection.Length, union.Length, donor);
        }
    }

    private static IEnumerable<SpecificityStabilityRecord> Stability(
        ImmutableArray<ContextViewObservation> views, ExactCompletionCompetitionTrial trial)
    {
        var chains = new[]
        {
            new[] { ExactCompletionContextView.ReducedPrevious,
                ExactCompletionContextView.ReducedPreviousTransition,
                ExactCompletionContextView.ReducedPrevNext,
                ExactCompletionContextView.ReducedHeldPrevNext },
            new[] { ExactCompletionContextView.ReducedNext,
                ExactCompletionContextView.ReducedNextTransition,
                ExactCompletionContextView.ReducedPrevNext,
                ExactCompletionContextView.ReducedHeldPrevNext }
        };
        foreach (var chain in chains)
        for (var i = 0; i < chain.Length - 1; i++)
        {
            var less = views.Single(x => x.ViewId == chain[i]);
            var more = views.Single(x => x.ViewId == chain[i + 1]);
            var target = Target(trial);
            var state = ClassifySpecificityStability(less, more, target);
            yield return new SpecificityStabilityRecord(less.ViewId, more.ViewId, state);
        }
    }

    private static ObservedJointContextSupport Joint(ObservedJointContextKind kind,
        ExactCompletionContextView leftId, ExactCompletionContextView rightId,
        ExactCompletionContextView jointId, ImmutableArray<ContextViewObservation> views,
        ExactCompletionCompetitionTrial trial)
    {
        var left = views.Single(x => x.ViewId == leftId);
        var right = views.Single(x => x.ViewId == rightId);
        var joint = views.Single(x => x.ViewId == jointId);
        var marginal = left.CompletionSet.Select(x => x.Completion)
            .Intersect(right.CompletionSet.Select(x => x.Completion)).OrderBy(x => x.Lane)
            .ThenBy(x => x.HeadType).ToImmutableArray();
        var jointSet = joint.CompletionSet.Select(x => x.Completion).ToImmutableArray();
        var observations = ImmutableArray.CreateBuilder<JointCompletionObservation>();
        foreach (var completion in marginal)
            observations.Add(new JointCompletionObservation(completion,
                !joint.Comparable ? JointCompletionSupport.MarginalAgreeButNoJointComparable
                : jointSet.Contains(completion) ? JointCompletionSupport.MarginalAgreeAndJointSupported
                : JointCompletionSupport.MarginalAgreeButJointDoesNotSupportCompletion));
        foreach (var completion in jointSet.Where(x => !marginal.Contains(x)))
            observations.Add(new JointCompletionObservation(completion,
                JointCompletionSupport.MarginalsDisagreeButJointSupported));
        var target = Target(trial);
        return new ObservedJointContextSupport(kind, leftId, rightId, jointId,
            left.Comparable, right.Comparable, joint.Comparable, marginal, jointSet,
            observations.ToImmutable(), joint.UniqueCompletion == target,
            joint.UniqueCompletion is not null && joint.UniqueCompletion != target);
    }

    private static ContextAgreementCertificate Certificate(ExactCompletionCompetitionTrial trial,
        ImmutableArray<ContextViewObservation> views, ImmutableArray<ViewPairAgreement> pairs,
        ImmutableArray<SpecificityStabilityRecord> stability,
        ImmutableArray<ObservedJointContextSupport> joints)
    {
        var comparable = views.Where(x => x.Comparable).ToArray();
        var unique = views.Where(x => x.UniqueCompletion is not null)
            .Select(x => new UniqueViewSelection(x.ViewId, x.UniqueCompletion!)).ToImmutableArray();
        var target = Target(trial);
        var uniqueAgreement = ClassifyUniqueViews(unique.Select(x => x.Completion), target);
        var conflict = uniqueAgreement == UniqueViewAgreement.UniqueViewsDisagree
            ? new ViewConflictRecord(trial.TargetSourceGroupId, trial.TargetObservationId, unique,
                unique.Any(x => x.Completion == target) && unique.Any(x => x.Completion != target))
            : null;
        var consensus = comparable.Length < 2 ? CompletionSetConsensus.NotApplicable
            : comparable.Select(SetKey).Distinct(StringComparer.Ordinal).Count() == 1
                ? CompletionSetConsensus.MultipleViewsAllCompletionSetsEqual
            : pairs.Where(x => x.CompletionRelation != CompletionSetRelation.OneOrBothNotComparable)
                .Any(x => x.IntersectionCount > 0) ? CompletionSetConsensus.MultipleViewsOverlapping
            : CompletionSetConsensus.MultipleViewsDisjoint;
        var support = comparable.Length == 0 ? ComparableTargetSupportState.NoComparableView
            : comparable.All(x => x.TargetInSet) ? ComparableTargetSupportState.TargetSupportedByAllComparableViews
            : comparable.Any(x => x.TargetInSet) ? ComparableTargetSupportState.TargetSupportedBySomeComparableViews
            : ComparableTargetSupportState.TargetSupportedByNone;
        return new ContextAgreementCertificate(trial.TargetSourceGroupId, trial.TargetObservationId,
            trial.TargetLane, trial.TargetType, trial.OriginalChordHeadCount, trial.ReducedVisibleMemberCount,
            trial.HeldBeforePresent, trial.BaselineState, trial.BaselineCompletionCount, CoverageSignature(views),
            comparable.Length, views.Count(x => x.UniqueCompletion == target),
            views.Count(x => x.UniqueCompletion is not null && x.UniqueCompletion != target),
            views.SelectMany(x => x.CompletionSet).SelectMany(x => x.DonorGroupIds)
                .Distinct(StringComparer.Ordinal).Count(),
            comparable.Length == 0 ? InformativeViewMultiplicity.NoInformativeContextView
                : comparable.Length == 1 ? InformativeViewMultiplicity.OneInformativeView
                : InformativeViewMultiplicity.MultipleInformativeViews,
            consensus, uniqueAgreement, support, views, pairs, stability, joints, conflict);
    }

    private static ExactCompletionIdentity Target(ExactCompletionCompetitionTrial trial) =>
        new(trial.TargetLane, trial.TargetType);
    private static string SetKey(ContextViewObservation view) => string.Join(';',
        view.CompletionSet.Select(x => $"{x.Completion.Lane}:{x.Completion.HeadType}"));
    private static double Milliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;
}

public static class ExactContextAgreementResearchJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    public static string Serialize(ExactContextAgreementResearchResult result) =>
        JsonSerializer.Serialize(result, Options);
}
