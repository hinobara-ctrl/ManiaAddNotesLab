using System.Collections.Immutable;
using System.Reflection;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseF1ComparableContextResolverTests
{
    private static readonly ExactCompletionIdentity A = new(1, OriginalHeadMemberType.TapHead);
    private static readonly ExactCompletionIdentity B = new(2, OriginalHeadMemberType.TapHead);
    private static readonly ExactCompletionIdentity C = new(2, OriginalHeadMemberType.LongNoteHead);

    [Theory]
    [InlineData(0, false, false, false, ComparableEvidenceState.NoComparableContext)]
    [InlineData(1, false, true, false, ComparableEvidenceState.LocalMismatch)]
    [InlineData(1, true, false, false, ComparableEvidenceState.ObservedSupport)]
    [InlineData(2, true, true, false, ComparableEvidenceState.AmbiguousEvidence)]
    [InlineData(2, true, false, true, ComparableEvidenceState.AmbiguousEvidence)]
    public void FourEvidenceStatesHaveExactPredeclaredSemantics(int comparable, bool support,
        bool mismatch, bool compositionGap, ComparableEvidenceState expected)
    {
        var ambiguity = compositionGap
            ? new[] { ComparableEvidenceAmbiguity.MarginalAgreementWithoutJointComparable }
            : [];
        Assert.Equal(expected, ComparableContextResolverResearch.ClassifyEvidenceState(
            comparable, support, mismatch, ambiguity));
    }

    [Fact]
    public void BasicObservedSupportIsCandidateCentric()
    {
        var result = Resolve(View(ExactCompletionContextView.ReducedHeld, true, (A, "d1")));
        var target = Target(result);
        Assert.Equal(ComparableEvidenceState.ObservedSupport, target.EvidenceState);
        Assert.Equal([ExactCompletionContextView.ReducedHeld], target.SupportingViews.ToArray());
        Assert.Empty(target.ContradictingViews);
    }

    [Fact]
    public void BasicLocalMismatchDoesNotChooseObservedAlternative()
    {
        var result = Resolve(View(ExactCompletionContextView.ReducedHeld, true, (B, "d1")));
        Assert.Equal(ComparableEvidenceState.LocalMismatch, Target(result).EvidenceState);
        Assert.Equal(ComparableEvidenceState.ObservedSupport,
            result.Resolutions.Single(x => x.CandidateCompletion == B).EvidenceState);
    }

    [Fact]
    public void NoComparableContextIsExplicitAbstention()
    {
        var resolution = Target(Resolve());
        Assert.Equal(ComparableEvidenceState.NoComparableContext, resolution.EvidenceState);
        Assert.Equal(7, resolution.AbstainingViews.Length);
        Assert.False(resolution.HasLocalMismatch);
    }

    [Fact]
    public void SupportInSomeComparableViewsPreservesContradictionAsAmbiguous()
    {
        var resolution = Target(Resolve(
            View(ExactCompletionContextView.ReducedHeld, true, (A, "support")),
            View(ExactCompletionContextView.ReducedPrevious, true, (B, "mismatch"))));
        Assert.Equal(ComparableEvidenceState.AmbiguousEvidence, resolution.EvidenceState);
        Assert.True(resolution.HasObservedSupport);
        Assert.True(resolution.HasLocalMismatch);
        Assert.Contains(ComparableEvidenceAmbiguity.SupportAndLocalMismatch, resolution.Ambiguities);
    }

    [Fact]
    public void CandidateSupportedByAllComparableViewsIsObservedSupport()
    {
        var resolution = Target(Resolve(
            View(ExactCompletionContextView.ReducedHeld, true, (A, "shared")),
            View(ExactCompletionContextView.ReducedPrevious, true, (A, "shared"))));
        Assert.Equal(ComparableEvidenceState.ObservedSupport, resolution.EvidenceState);
        Assert.Equal(2, resolution.SupportingViews.Length);
        Assert.Single(resolution.SupportingDonorGroupIds);
    }

    [Fact]
    public void CandidateSupportedByNoComparableViewIsLocalMismatch()
    {
        var resolution = Target(Resolve(
            View(ExactCompletionContextView.ReducedHeld, true, (B, "x")),
            View(ExactCompletionContextView.ReducedPrevious, true, (C, "y"))));
        Assert.Equal(ComparableEvidenceState.LocalMismatch, resolution.EvidenceState);
        Assert.Equal(2, resolution.ContradictingViews.Length);
    }

    [Fact]
    public void MultipleCompletionsProduceSeparateCertificatesWithoutWinner()
    {
        var result = Resolve(View(ExactCompletionContextView.ReducedHeld, true,
            (A, "a"), (B, "b"), (C, "c")));
        Assert.Equal([A, B, C], result.Resolutions.Select(x => x.CandidateCompletion).ToArray());
        Assert.All(result.Resolutions, x => Assert.Equal(ComparableEvidenceState.ObservedSupport, x.EvidenceState));
    }

    [Fact]
    public void UniqueTargetAndUniqueWrongRemainDistinct()
    {
        var target = Target(Resolve(View(ExactCompletionContextView.ReducedHeld, true, (A, "a"))));
        Assert.Contains(ExactCompletionContextView.ReducedHeld, target.UniqueSupportingViews);
        var wrongResult = Resolve(View(ExactCompletionContextView.ReducedHeld, true, (B, "b")));
        Assert.Empty(Target(wrongResult).UniqueSupportingViews);
        Assert.Contains(ExactCompletionContextView.ReducedHeld,
            wrongResult.Resolutions.Single(x => x.CandidateCompletion == B).UniqueSupportingViews);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UniqueTargetVsWrongAndWrongVsWrongConflictsArePreserved(bool targetParticipates)
    {
        var first = targetParticipates ? A : B;
        var second = targetParticipates ? B : C;
        var conflict = new ViewConflictRecord("target", new OriginalObservationId(99),
            [new(ExactCompletionContextView.ReducedHeld, first),
                new(ExactCompletionContextView.ReducedPrevious, second)], targetParticipates);
        var result = Resolve(conflict,
            View(ExactCompletionContextView.ReducedHeld, true, (first, "x")),
            View(ExactCompletionContextView.ReducedPrevious, true, (second, "y")));
        var resolution = result.Resolutions.Single(x => x.CandidateCompletion == first);
        Assert.Equal(ComparableEvidenceState.AmbiguousEvidence, resolution.EvidenceState);
        Assert.NotNull(resolution.ConflictRecord);
        Assert.Contains(ComparableEvidenceAmbiguity.CandidateInUniqueCompletionConflict, resolution.Ambiguities);
    }

    [Fact]
    public void DependentViewsAreRecordedAsEdgesNotVotes()
    {
        var resolution = Target(Resolve(
            View(ExactCompletionContextView.ReducedPrevious, true, (A, "same")),
            View(ExactCompletionContextView.ReducedPreviousTransition, true, (A, "same"))));
        Assert.Single(resolution.SupportingDependencies);
        Assert.Single(resolution.SupportingDonorGroupIds);
        Assert.DoesNotContain(typeof(ComparableContextResolution).GetProperties(), x =>
            x.Name.Contains("Vote", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SharedAndDisjointDonorGroupsRemainObservable()
    {
        var shared = Target(Resolve(
            View(ExactCompletionContextView.ReducedHeld, true, (A, "same")),
            View(ExactCompletionContextView.ReducedPrevious, true, (A, "same"))));
        Assert.Single(shared.SupportingDonorGroupIds);
        var disjoint = Target(Resolve(
            View(ExactCompletionContextView.ReducedHeld, true, (A, "left")),
            View(ExactCompletionContextView.ReducedPrevious, true, (A, "right"))));
        Assert.Equal(2, disjoint.SupportingDonorGroupIds.Length);
    }

    [Theory]
    [InlineData(true, true, CandidateJointEvidenceState.ObservedJointContext)]
    [InlineData(false, false, CandidateJointEvidenceState.MarginalWithoutJointComparable)]
    [InlineData(true, false, CandidateJointEvidenceState.MarginalContradictedByJoint)]
    public void PreviousNextMarginalAndJointStatesStaySeparate(bool jointComparable, bool jointSupports,
        CandidateJointEvidenceState expected)
    {
        var views = new List<ContextViewObservation>
        {
            View(ExactCompletionContextView.ReducedPreviousTransition, true, (A, "p")),
            View(ExactCompletionContextView.ReducedNextTransition, true, (A, "n")),
            View(ExactCompletionContextView.ReducedPrevNext, jointComparable,
                jointSupports ? new[] { (A, "j") } : Array.Empty<(ExactCompletionIdentity, string)>())
        };
        var joint = Target(Resolve(views.ToArray())).JointEvidence.Single(x =>
            x.Kind == ObservedJointContextKind.PreviousNext);
        Assert.Equal(expected, joint.State);
        Assert.Equal(jointSupports ? 1 : 0, joint.JointDonorGroupIds.Length);
    }

    [Theory]
    [InlineData(true, true, CandidateJointEvidenceState.ObservedJointContext)]
    [InlineData(false, false, CandidateJointEvidenceState.MarginalWithoutJointComparable)]
    [InlineData(true, false, CandidateJointEvidenceState.MarginalContradictedByJoint)]
    public void HeldPrevNextRequiresItsOwnObservedJointWitness(bool jointComparable, bool jointSupports,
        CandidateJointEvidenceState expected)
    {
        var resolution = Target(Resolve(
            View(ExactCompletionContextView.ReducedHeld, true, (A, "h")),
            View(ExactCompletionContextView.ReducedPrevNext, true, (A, "pn")),
            View(ExactCompletionContextView.ReducedHeldPrevNext, jointComparable,
                jointSupports ? new[] { (A, "joint") } : Array.Empty<(ExactCompletionIdentity, string)>()))) ;
        var evidence = resolution.JointEvidence.Single(x => x.Kind == ObservedJointContextKind.HeldPrevNext);
        Assert.Equal(expected, evidence.State);
    }

    [Fact]
    public void LaneTypeBeatAndObservationProvenanceRemainExact()
    {
        var result = Resolve(View(ExactCompletionContextView.ReducedHeld, true,
            (A, "a"), (C, "c")));
        Assert.Contains(result.Resolutions, x => x.CandidateCompletion == A);
        Assert.Contains(result.Resolutions, x => x.CandidateCompletion == C);
        Assert.NotEqual(A, new ExactCompletionIdentity(2, OriginalHeadMemberType.TapHead));
        var donor = Target(result).ViewEvidence.SelectMany(x => x.RelevantCompletionEvidence)
            .Where(x => x.Completion == A).SelectMany(x => x.Donors).Single();
        Assert.Equal(.5m, donor.HeadBeat);
        Assert.NotEmpty(donor.FullGroupObservationIds);
    }

    [Fact]
    public void IntegratedLeakageAndHeldTailProtectionsRemainZero()
    {
        var result = ComparableContextResolverResearch.Evaluate(Chart(4,
            Tap(0, 0), Ln(2, 0, 1500), Tap(1, 500),
            Tap(0, 2000), Ln(2, 2000, 3500), Tap(1, 2500)));
        Assert.Equal(0, result.TargetGroupLeakageCount);
        Assert.Equal(0, result.FutureHeldLeakageCount);
        Assert.Equal(0, result.HeldTailEncodedAsHeadCount);
        Assert.Equal(0, result.DonorNestingViolationCount);
        Assert.All(result.Resolutions, x => Assert.DoesNotContain(x.TargetSourceGroupId,
            x.SupportingDonorGroupIds));
    }

    [Fact]
    public void SeparateChartsCannotTransferResolverEvidence()
    {
        var first = ComparableContextResolverResearch.Evaluate(Chart(4, Tap(0, 0), Tap(2, 0)));
        var second = ComparableContextResolverResearch.Evaluate(Chart(4, Tap(0, 1000), Tap(2, 1000)));
        Assert.NotEqual(first.ChartFingerprint, second.ChartFingerprint);
        Assert.All(first.Resolutions.Concat(second.Resolutions), x =>
            Assert.Equal(ComparableEvidenceState.NoComparableContext, x.EvidenceState));
    }

    [Fact]
    public void CompleteDependencyGraphAndSpecificityClassifierAreSharedHardenings()
    {
        Assert.Equal(9, ExactContextViewDependencies.Graph.Length);
        Assert.Equal(ExactContextViewDependencies.Graph, ExactContextAgreementResearch.DependencyGraph);
        Assert.Contains(SpecificityStability.TargetStillUnsupported,
            Enum.GetValues<SpecificityStability>());
    }

    [Fact]
    public void ResolverIsDeterministicHasStableOrderAndOwnsNoRandomParameter()
    {
        var chart = Chart(4, Tap(1, 0), Tap(0, 500), Tap(2, 500),
            Tap(1, 1000), Tap(0, 1500), Tap(2, 1500));
        var first = ComparableContextResolverResearch.Evaluate(chart);
        var second = ComparableContextResolverResearch.Evaluate(chart);
        var firstJson = ComparableContextResolverResearchJson.Serialize(first);
        var secondJson = ComparableContextResolverResearchJson.Serialize(second);
        Assert.Equal(firstJson, secondJson);
        Assert.Equal(first.Resolutions.OrderBy(x => x.TargetObservationId.Value)
            .ThenBy(x => x.CandidateCompletion.Lane).ThenBy(x => x.CandidateCompletion.HeadType), first.Resolutions);
        Assert.DoesNotContain(typeof(ComparableContextResolverResearch).GetMethods(
                BindingFlags.Public | BindingFlags.Static).SelectMany(x => x.GetParameters()),
            x => typeof(IRandomSource).IsAssignableFrom(x.ParameterType));
    }

    [Fact]
    public void ResolverDoesNotChangeProductiveOutputRandomOrVersions()
    {
        var chart = Chart(4, Tap(0, 0), Ln(2, 0, 1000), Tap(0, 1500), Ln(2, 1500, 2500));
        var options = new AddNotesOptions { Chance = .61, ContextualDensityNormalizationEnabled = false,
            DiagnosticsEnabled = true };
        var leftRandom = new RecordingRandom(73);
        var rightRandom = new RecordingRandom(73);
        var left = new AddNotesEngine().Apply(chart, options, leftRandom);
        _ = ComparableContextResolverResearch.Evaluate(chart);
        var right = new AddNotesEngine().Apply(chart, options, rightRandom);
        Assert.Equal(OsuBeatmap.Write(left.ModifiedChart, options.Chance),
            OsuBeatmap.Write(right.ModifiedChart, options.Chance));
        Assert.Equal(leftRandom.Transcript, rightRandom.Transcript);
        Assert.Equal(left.DecisionDiagnostics!.Decisions.Select(x =>
                (x.CandidateKey, x.LegacyWeight, x.ActiveAggregationRule, x.LegacySelectedLane)),
            right.DecisionDiagnostics!.Decisions.Select(x =>
                (x.CandidateKey, x.LegacyWeight, x.ActiveAggregationRule, x.LegacySelectedLane)));
        Assert.Equal("legacy-experimental.1", MapperEvidenceProfileBuilder.BehaviorPolicyVersion);
        Assert.Equal("phase-a.1", MapperEvidenceProfileBuilder.EvidenceProfileVersion);
        Assert.Equal("phase-c1-2-shadow.1", DecisionDiagnosticVersions.Current);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void OneImplementationHandlesEveryRequiredKeymode(int keys)
    {
        var lane = Math.Max(0, keys - 1);
        var result = ComparableContextResolverResearch.Evaluate(Chart(keys,
            Tap(0, 0), Tap(lane, 0), Tap(0, 1000), Tap(lane, 1000)));
        Assert.Equal(keys, result.KeyCount);
        Assert.All(result.Resolutions, x => Assert.Equal(7,
            x.SupportingViews.Length + x.ContradictingViews.Length + x.AbstainingViews.Length));
    }

    [Fact]
    public void SerializedResearchContainsNoSelectionSignals()
    {
        var json = ComparableContextResolverResearchJson.Serialize(Resolve());
        Assert.Contains("phase-f1-research.1", json);
        foreach (var forbidden in new[] { "score", "probability", "confidence", "likelihood",
                     "authority", "ranking", "weight", "majority", "mapperSupport" })
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
    }

    private static ComparableContextResolverResearchResult Resolve(params ContextViewObservation[] overrides) =>
        Resolve(null, overrides);

    private static ComparableContextResolverResearchResult Resolve(ViewConflictRecord? conflict,
        params ContextViewObservation[] overrides)
    {
        var byView = overrides.ToDictionary(x => x.ViewId);
        var views = ExactCompletionContextResearch.Views.Select(view => byView.GetValueOrDefault(view,
            view == ExactCompletionContextView.ReducedOnly
                ? View(view, true, (A, "baseline")) : View(view, false))).ToImmutableArray();
        var target = A;
        var joints = ImmutableArray.Create(
            Joint(ObservedJointContextKind.PreviousNext,
                ExactCompletionContextView.ReducedPreviousTransition,
                ExactCompletionContextView.ReducedNextTransition,
                ExactCompletionContextView.ReducedPrevNext, views),
            Joint(ObservedJointContextKind.HeldPrevNext,
                ExactCompletionContextView.ReducedHeld,
                ExactCompletionContextView.ReducedPrevNext,
                ExactCompletionContextView.ReducedHeldPrevNext, views));
        var certificate = new ContextAgreementCertificate("target", new OriginalObservationId(99),
            target.Lane, target.HeadType, 2, 1, false,
            ChordCompletionReconstructionState.TargetAmongCompetingCompletions, 2,
            ExactContextAgreementResearch.CoverageSignature(views), views.Count(x => x.Comparable),
            0, 0, views.SelectMany(x => x.CompletionSet).SelectMany(x => x.DonorGroupIds).Distinct().Count(),
            InformativeViewMultiplicity.MultipleInformativeViews, CompletionSetConsensus.NotApplicable,
            conflict is null ? UniqueViewAgreement.NoUniqueView : UniqueViewAgreement.UniqueViewsDisagree,
            ComparableTargetSupportState.TargetSupportedBySomeComparableViews,
            views, [], [], joints, conflict);
        var occurrences = Occurrences(views);
        var source = new ExactContextAgreementResearchResult("phase-d0-2-research.2", "chart", 4,
            10, 5, occurrences, [certificate], 0, 0, 0, 0, 0, 0, 0);
        return ComparableContextResolverResearch.Resolve(source);
    }

    private static ContextViewObservation View(ExactCompletionContextView view, bool comparable,
        params (ExactCompletionIdentity Completion, string Donor)[] evidence)
    {
        var completionSet = evidence.GroupBy(x => x.Completion)
            .OrderBy(x => x.Key.Lane).ThenBy(x => x.Key.HeadType)
            .Select(x => new CompletionDonorGroups(x.Key,
                x.Select(y => y.Donor).Distinct().Order(StringComparer.Ordinal).ToImmutableArray()))
            .ToImmutableArray();
        const string reduced = "K:4|T:|L:";
        return new ContextViewObservation(view, view == ExactCompletionContextView.ReducedOnly
                ? reduced : $"{reduced}|{view}",
            comparable, completionSet, completionSet.Any(x => x.Completion == A),
            completionSet.Length == 1 ? completionSet[0].Completion : null,
            completionSet.SelectMany(x => x.DonorGroupIds).Distinct().Count());
    }

    private static ObservedJointContextSupport Joint(ObservedJointContextKind kind,
        ExactCompletionContextView leftId, ExactCompletionContextView rightId,
        ExactCompletionContextView jointId, ImmutableArray<ContextViewObservation> views)
    {
        var left = views.Single(x => x.ViewId == leftId);
        var right = views.Single(x => x.ViewId == rightId);
        var joint = views.Single(x => x.ViewId == jointId);
        var marginal = left.CompletionSet.Select(x => x.Completion)
            .Intersect(right.CompletionSet.Select(x => x.Completion)).ToImmutableArray();
        return new ObservedJointContextSupport(kind, leftId, rightId, jointId,
            left.Comparable, right.Comparable, joint.Comparable, marginal,
            joint.CompletionSet.Select(x => x.Completion).ToImmutableArray(), [], false, false);
    }

    private static ImmutableArray<ExactCompletionOccurrence> Occurrences(
        ImmutableArray<ContextViewObservation> views)
    {
        var pairs = views.SelectMany(x => x.CompletionSet).SelectMany(x =>
                x.DonorGroupIds.Select(donor => (x.Completion, Donor: donor)))
            .Distinct().OrderBy(x => x.Donor, StringComparer.Ordinal)
            .ThenBy(x => x.Completion.Lane).ThenBy(x => x.Completion.HeadType).ToArray();
        return pairs.Select((x, index) =>
        {
            var completionId = new OriginalObservationId(index * 2 + 1);
            var memberId = new OriginalObservationId(index * 2 + 2);
            var relation = new ChordCompletionRelationKey(4, [], [], x.Completion.Lane, x.Completion.HeadType);
            var witness = new ChordCompletionRelationWitness(x.Donor, 250, .5m,
                [completionId, memberId], [memberId], completionId, x.Completion.Lane,
                x.Completion.HeadType, [], "full");
            var context = new ExactCompletionContext(x.Donor, 250, .5m, [completionId, memberId],
                [], [], null, null, [], [], []);
            return new ExactCompletionOccurrence(relation, witness, context);
        }).ToImmutableArray();
    }

    private static ComparableContextResolution Target(ComparableContextResolverResearchResult result) =>
        result.Resolutions.Single(x => x.IsTargetCandidate);
    private static ManiaObject Tap(int lane, int time) => ManiaObject.Tap(lane, time);
    private static ManiaObject Ln(int lane, int start, int end) => ManiaObject.Ln(lane, start, end);
    private static ManiaChart Chart(int keys, params ManiaObject[] objects) => new()
    {
        KeyCount = keys,
        Lines = [],
        OriginalObjects = objects.Select((x, i) => x with { Sequence = i }).ToArray(),
        TimingPoints = [new TimingPoint(0, 500)]
    };

    private sealed class RecordingRandom(int seed) : IRandomSource
    {
        private readonly SeededRandom _inner = new(seed);
        public List<string> Transcript { get; } = [];
        public double NextDouble() { var value = _inner.NextDouble(); Transcript.Add($"D:{value:R}"); return value; }
        public int Next(int maximum) { var value = _inner.Next(maximum); Transcript.Add($"I:{maximum}:{value}"); return value; }
    }
}
