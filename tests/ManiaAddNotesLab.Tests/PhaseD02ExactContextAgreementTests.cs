using System.Collections.Immutable;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseD02ExactContextAgreementTests
{
    private static readonly ExactCompletionIdentity A = new(1, OriginalHeadMemberType.TapHead);
    private static readonly ExactCompletionIdentity B = new(2, OriginalHeadMemberType.TapHead);
    private static readonly ExactCompletionIdentity C = new(2, OriginalHeadMemberType.LongNoteHead);

    [Theory]
    [MemberData(nameof(SetRelations))]
    public void CompletionSetRelationsAreExact(bool lc, ExactCompletionIdentity[] left, bool rc,
        ExactCompletionIdentity[] right, CompletionSetRelation expected) =>
        Assert.Equal(expected, ExactContextAgreementResearch.CompareCompletionSets(lc, left, rc, right));

    public static TheoryData<bool, ExactCompletionIdentity[], bool, ExactCompletionIdentity[], CompletionSetRelation>
        SetRelations => new()
        {
            { true, [A], true, [A], CompletionSetRelation.EqualCompletionSet },
            { true, [A], true, [A, B], CompletionSetRelation.LeftStrictSubset },
            { true, [A, B], true, [B], CompletionSetRelation.RightStrictSubset },
            { true, [A, B], true, [B, C], CompletionSetRelation.PartialOverlap },
            { true, [A], true, [B], CompletionSetRelation.Disjoint },
            { true, [A], false, [], CompletionSetRelation.OneOrBothNotComparable }
        };

    [Theory]
    [MemberData(nameof(DonorRelations))]
    public void DonorRelationsPreserveExactCounts(string[] left, string[] right,
        DonorSetRelation expected, int intersection)
    {
        var result = ExactContextAgreementResearch.CompareDonors(A, true, left, true, right);
        Assert.Equal(expected, result.Relation);
        Assert.Equal(intersection, result.IntersectionCount);
        Assert.Equal(left.Distinct().Count(), result.LeftCount);
        Assert.Equal(right.Distinct().Count(), result.RightCount);
    }

    public static TheoryData<string[], string[], DonorSetRelation, int> DonorRelations => new()
    {
        { ["x", "y"], ["x", "y"], DonorSetRelation.IdenticalDonorSet, 2 },
        { ["x"], ["x", "y"], DonorSetRelation.LeftStrictSubset, 1 },
        { ["x", "y"], ["y", "z"], DonorSetRelation.PartialOverlap, 1 },
        { ["x"], ["y"], DonorSetRelation.Disjoint, 0 },
        { [], ["y"], DonorSetRelation.EmptyOrNotComparable, 0 }
    };

    [Theory]
    [InlineData(0, 0, UniqueViewAgreement.NoUniqueView)]
    [InlineData(1, 0, UniqueViewAgreement.SingleUniqueViewOnly)]
    [InlineData(2, 0, UniqueViewAgreement.UniqueViewsAgreeOnTarget)]
    [InlineData(2, 1, UniqueViewAgreement.UniqueViewsAgreeOnSameWrongCompletion)]
    [InlineData(2, 2, UniqueViewAgreement.UniqueViewsDisagree)]
    public void UniqueViewAgreementNeverChoosesAWinner(int count, int mode, UniqueViewAgreement expected)
    {
        ExactCompletionIdentity[] completions = count == 0 ? [] : count == 1 ? [A]
            : mode == 0 ? [A, A] : mode == 1 ? [B, B] : [A, B];
        Assert.Equal(expected, ExactContextAgreementResearch.ClassifyUniqueViews(completions, A));
    }

    [Fact]
    public void CoverageSignatureIsDeterministicAndAbsenceIsExplicit()
    {
        var observations = ExactCompletionContextResearch.Views.Select((view, index) =>
            Observation(view, index % 2 == 0, index % 2 == 0 ? [A] : [])).ToImmutableArray();
        Assert.Equal("10101010", ExactContextAgreementResearch.CoverageSignature(observations));
        Assert.Equal(ExactContextAgreementResearch.CoverageSignature(observations),
            ExactContextAgreementResearch.CoverageSignature(observations.Reverse()));
    }

    [Fact]
    public void DependencyGraphDeclaresActualConjunctions()
    {
        Assert.Contains((ExactCompletionContextView.ReducedPreviousTransition,
            ExactCompletionContextView.ReducedPrevNext), ExactContextAgreementResearch.DependencyGraph);
        Assert.Contains((ExactCompletionContextView.ReducedNextTransition,
            ExactCompletionContextView.ReducedPrevNext), ExactContextAgreementResearch.DependencyGraph);
        Assert.Contains((ExactCompletionContextView.ReducedHeld,
            ExactCompletionContextView.ReducedHeldPrevNext), ExactContextAgreementResearch.DependencyGraph);
    }

    [Theory]
    [MemberData(nameof(StabilityBranches))]
    public void SpecificityStabilityNamesEverySemanticBranch(bool lessHasTarget, bool moreComparable,
        ExactCompletionIdentity[] moreCompletions, SpecificityStability expected)
    {
        var less = Observation(ExactCompletionContextView.ReducedPrevious, true,
            lessHasTarget ? [A, B] : [B]);
        var more = Observation(ExactCompletionContextView.ReducedPreviousTransition,
            moreComparable, moreCompletions);
        Assert.Equal(expected, ExactContextAgreementResearch.ClassifySpecificityStability(less, more, A));
    }

    public static TheoryData<bool, bool, ExactCompletionIdentity[], SpecificityStability> StabilityBranches => new()
    {
        { true, false, [], SpecificityStability.NoComparableAtNextLevel },
        { true, true, [B, C], SpecificityStability.TargetLost },
        { true, true, [A], SpecificityStability.BecomesUniqueTarget },
        { false, true, [B], SpecificityStability.BecomesUniqueWrong },
        { true, true, [A, B], SpecificityStability.StillCompeting },
        { false, true, [B, C], SpecificityStability.TargetStillUnsupported }
    };

    [Fact]
    public void EveryDeclaredDependencyEdgeIsAuditedAsDonorCompletionNesting()
    {
        Assert.Equal(9, ExactContextViewDependencies.Graph.Length);
        var result = ExactContextAgreementResearch.Evaluate(Chart(4,
            Tap(1, 0), Tap(0, 500), Tap(2, 500),
            Tap(1, 1000), Tap(0, 1500), Tap(2, 1500),
            Tap(1, 2000), Tap(0, 2500), Tap(2, 2500)));

        foreach (var certificate in result.Certificates)
        foreach (var edge in ExactContextViewDependencies.Graph)
        {
            var parent = DonorCompletions(certificate.Views.Single(x => x.ViewId == edge.Parent));
            var child = DonorCompletions(certificate.Views.Single(x => x.ViewId == edge.Child));
            Assert.True(child.IsSubsetOf(parent), $"{edge.Child} must refine {edge.Parent}.");
        }
        Assert.Equal(0, result.DonorNestingViolationCount);
    }

    [Fact]
    public void IntegratedResultKeepsCompletionAndDonorProvenanceWithoutTargetLeakage()
    {
        var result = ExactContextAgreementResearch.Evaluate(Chart(4,
            Tap(1, 0), Tap(0, 500), Tap(2, 500),
            Tap(1, 1000), Tap(0, 1500), Tap(2, 1500)));

        Assert.Equal("phase-d0-2-research.2", result.ResearchSchemaVersion);
        Assert.Equal(0, result.TargetGroupLeakageCount);
        Assert.Equal(0, result.FutureHeldLeakageCount);
        Assert.Equal(0, result.HeldTailEncodedAsHeadCount);
        Assert.Equal(0, result.DonorNestingViolationCount);
        Assert.All(result.Certificates, certificate =>
            Assert.All(certificate.Views.SelectMany(x => x.CompletionSet)
                .SelectMany(x => x.DonorGroupIds), donor => Assert.NotEqual(certificate.TargetSourceGroupId, donor)));
        Assert.All(result.DonorOccurrences, occurrence =>
            Assert.NotEmpty(occurrence.Witness.FullOriginalMemberObservationIds));
    }

    [Fact]
    public void MarginalAgreementIsComparedAgainstObservedJointContext()
    {
        var result = ExactContextAgreementResearch.Evaluate(Chart(4,
            Tap(1, 0), Tap(0, 500), Tap(2, 500), Tap(1, 1000),
            Tap(0, 1500), Tap(2, 1500), Tap(1, 2000),
            Tap(0, 2500), Tap(2, 2500)));
        var joints = result.Certificates.SelectMany(x => x.JointContexts).ToArray();

        Assert.Contains(joints, x => x.Kind == ObservedJointContextKind.PreviousNext);
        Assert.Contains(joints, x => x.Kind == ObservedJointContextKind.HeldPrevNext);
        Assert.All(joints.SelectMany(x => x.CompletionObservations), observation =>
            Assert.True(Enum.IsDefined(observation.Support)));
    }

    [Fact]
    public void LongNoteLeakageProtectionsRemainActive()
    {
        var result = ExactContextAgreementResearch.Evaluate(Chart(4,
            Tap(0, 0), Ln(2, 0, 1500), Tap(1, 500),
            Tap(0, 2000), Ln(2, 2000, 3500), Tap(1, 2500)));
        Assert.Equal(0, result.FutureHeldLeakageCount);
        Assert.Equal(0, result.HeldTailEncodedAsHeadCount);
        Assert.All(result.DonorOccurrences.Where(x =>
                x.Relation.CompletionType == OriginalHeadMemberType.LongNoteHead), occurrence =>
            Assert.DoesNotContain(occurrence.Witness.CompletionObservationId,
                occurrence.Context.NextHeldBeforeObservationIdsExcludingCurrentGroup));
    }

    [Fact]
    public void ExactBeatGapAndTapLnIdentityRemainSeparate()
    {
        var result = ExactContextAgreementResearch.Evaluate(Chart(4,
            Tap(1, 0), Tap(0, 500), Tap(2, 500), Tap(1, 1250),
            Tap(0, 1500), Ln(2, 1500, 2000)));
        Assert.Contains(result.Certificates.SelectMany(x => x.Views), x =>
            x.ExactContextSignature.Contains("DB:0.5", StringComparison.Ordinal));
        Assert.NotEqual(A, C);
    }

    [Fact]
    public void ResearchIsDeterministicUsesNoRandomAndDoesNotChangeGeneration()
    {
        var chart = Chart(4, Tap(0, 0), Ln(2, 0, 1000), Tap(0, 1500), Ln(2, 1500, 2500));
        var beforeRandom = new RecordingRandom(17);
        var afterRandom = new RecordingRandom(17);
        var options = new AddNotesOptions { Chance = .61, ContextualDensityNormalizationEnabled = false };
        var before = new AddNotesEngine().Apply(chart, options, beforeRandom);
        var first = ExactContextAgreementResearch.Evaluate(chart);
        var second = ExactContextAgreementResearch.Evaluate(chart);
        var after = new AddNotesEngine().Apply(chart, options, afterRandom);
        var firstJson = ExactContextAgreementResearchJson.Serialize(first with
            { AgreementBuildMilliseconds = 0, PairwiseComparisonMilliseconds = 0, JointWitnessComparisonMilliseconds = 0 });
        var secondJson = ExactContextAgreementResearchJson.Serialize(second with
            { AgreementBuildMilliseconds = 0, PairwiseComparisonMilliseconds = 0, JointWitnessComparisonMilliseconds = 0 });

        Assert.Equal(firstJson, secondJson);
        Assert.Equal(OsuBeatmap.Write(before.ModifiedChart, options.Chance),
            OsuBeatmap.Write(after.ModifiedChart, options.Chance));
        Assert.Equal(beforeRandom.Transcript, afterRandom.Transcript);
        Assert.DoesNotContain("confidence", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("probability", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mapperSupport", firstJson, StringComparison.OrdinalIgnoreCase);
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
    public void ImplementationIsGenericAcrossKeyCounts(int keys)
    {
        var lane = Math.Max(0, keys - 1);
        var result = ExactContextAgreementResearch.Evaluate(Chart(keys,
            Tap(0, 0), Tap(lane, 0), Tap(0, 1000), Tap(lane, 1000)));
        Assert.Equal(keys, result.KeyCount);
        Assert.All(result.Certificates, x => Assert.Equal(28, x.ViewPairs.Length));
    }

    [Fact]
    public void LanePermutationPreservesDescriptiveOutcomeCounts()
    {
        var source = new[] { Tap(1, 0), Tap(0, 500), Ln(2, 500, 900),
            Tap(1, 1000), Tap(0, 1500), Ln(2, 1500, 1900) };
        var permutation = new[] { 2, 0, 3, 1 };
        var first = ExactContextAgreementResearch.Evaluate(Chart(4, source));
        var second = ExactContextAgreementResearch.Evaluate(Chart(4,
            source.Select(x => x with { Lane = permutation[x.Lane] }).ToArray()));
        Assert.Equal(Counts(first), Counts(second));
    }

    [Fact]
    public void SeparateChartsCannotTransferEvidence()
    {
        var first = ExactContextAgreementResearch.Evaluate(Chart(4, Tap(0, 0), Tap(2, 0)));
        var second = ExactContextAgreementResearch.Evaluate(Chart(4, Tap(0, 1000), Tap(2, 1000)));
        Assert.NotEqual(first.ChartFingerprint, second.ChartFingerprint);
        Assert.All(first.Certificates.Concat(second.Certificates).SelectMany(x => x.Views),
            x => Assert.False(x.Comparable));
    }

    private static string Counts(ExactContextAgreementResearchResult result) => string.Join('|',
        result.Certificates.GroupBy(x => (x.UniqueAgreement, x.TargetSupportState, x.CoverageSignature))
            .OrderBy(x => x.Key.UniqueAgreement).ThenBy(x => x.Key.TargetSupportState)
            .ThenBy(x => x.Key.CoverageSignature)
            .Select(x => $"{x.Key}:{x.Count()}"));
    private static ContextViewObservation Observation(ExactCompletionContextView view, bool comparable,
        ExactCompletionIdentity[] completions) => new(view, view.ToString(), comparable,
        completions.Select(x => new CompletionDonorGroups(x, ["donor"])).ToImmutableArray(),
        completions.Contains(A), completions.Length == 1 ? completions[0] : null,
        comparable ? 1 : 0);
    private static HashSet<string> DonorCompletions(ContextViewObservation view) => view.CompletionSet
        .SelectMany(x => x.DonorGroupIds.Select(donor => $"{donor}|{x.Completion.Lane}|{x.Completion.HeadType}"))
        .ToHashSet(StringComparer.Ordinal);
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
