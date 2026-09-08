using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseD10ResultingStateCompositionTests
{
    [Fact]
    public void CompletionSetIsCanonicalAndOrderIndependent()
    {
        var a = new CompletionMemberIdentity(3, OriginalHeadMemberType.LongNoteHead);
        var b = new CompletionMemberIdentity(1, OriginalHeadMemberType.TapHead);

        var left = CompletionSetIdentity.Create([a, b]);
        var right = CompletionSetIdentity.Create([b, a]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.Equal("1:TapHead,3:LongNoteHead", left.Signature);
    }

    [Fact]
    public void DuplicateCompletionMemberIsRejectedByIdentity()
    {
        var member = new CompletionMemberIdentity(1, OriginalHeadMemberType.TapHead);
        Assert.Throws<ArgumentException>(() => CompletionSetIdentity.Create([member, member]));
    }

    [Fact]
    public void DuplicateCompletionMemberIsHardInvalidBeforeEvidence()
    {
        var member = new CompletionMemberIdentity(1, OriginalHeadMemberType.TapHead);
        Assert.Equal(CompositionStructuralInvalidity.DuplicateCompletionMember,
            ResultingStateCompositionResearch.ValidateComposition(4, [], [member, member]));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void OutOfRangeLaneIsHardInvalid(int lane)
    {
        Assert.Equal(CompositionStructuralInvalidity.LaneOutOfRange,
            ResultingStateCompositionResearch.ValidateComposition(4, [],
                [new CompletionMemberIdentity(lane, OriginalHeadMemberType.TapHead)]));
    }

    [Fact]
    public void ReaddingExistingMemberIsHardInvalid()
    {
        var member = new CompletionMemberIdentity(0, OriginalHeadMemberType.TapHead);
        Assert.Equal(CompositionStructuralInvalidity.MemberAlreadyPresent,
            ResultingStateCompositionResearch.ValidateComposition(4, [member], [member]));
    }

    [Fact]
    public void TwoHeadTypesInSameLaneAreHardInvalid()
    {
        Assert.Equal(CompositionStructuralInvalidity.SameLaneCollision,
            ResultingStateCompositionResearch.ValidateComposition(4, [], [
                new CompletionMemberIdentity(0, OriginalHeadMemberType.TapHead),
                new CompletionMemberIdentity(0, OriginalHeadMemberType.LongNoteHead)
            ]));
    }

    [Fact]
    public void ResultingStateIsCanonicalAndReconstructsFullState()
    {
        var reduced = new[] { Member(0, 2, OriginalHeadMemberType.TapHead) };
        var completion = CompletionSetIdentity.Create([
            new CompletionMemberIdentity(3, OriginalHeadMemberType.LongNoteHead),
            new CompletionMemberIdentity(0, OriginalHeadMemberType.TapHead)
        ]);
        var reversed = CompletionSetIdentity.Create(completion.Members.Reverse());

        var left = ResultingStateCompositionResearch.ResultingState(4, reduced, completion);
        var right = ResultingStateCompositionResearch.ResultingState(4, reduced, reversed);

        Assert.Equal(left.Signature, right.Signature);
        Assert.Equal("K:4|T:0,2|L:3", left.Signature);
    }

    [Fact]
    public void SameOccurrencePairIsJointlyReconstructedUnique()
    {
        var result = ResultingStateCompositionResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(1, 1000)));
        var target = Trial(result, 0);
        var reducedOnly = View(target, ExactCompletionContextView.ReducedOnly);

        Assert.Equal(TargetCompositionReconstructionState.ObservedJointUnique, reducedOnly.TargetState);
        Assert.True(reducedOnly.TargetCompletionSetJointlyObserved);
        Assert.Equal(1, reducedOnly.DistinctJointCompletionSetCount);
    }

    [Fact]
    public void MultipleJointSetsPreserveTargetAmongAlternatives()
    {
        var result = ResultingStateCompositionResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(1, 1000), Tap(2, 2000), Tap(3, 2000)));
        var reducedOnly = View(Trial(result, 0), ExactCompletionContextView.ReducedOnly);

        Assert.Equal(TargetCompositionReconstructionState.ObservedJointAmongAlternatives,
            reducedOnly.TargetState);
        Assert.Equal(2, reducedOnly.DistinctJointCompletionSetCount);
    }

    [Fact]
    public void CrossDonorMarginalsDoNotFabricateJointWitness()
    {
        var result = ResultingStateCompositionResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(1, 0),
            Tap(0, 1000), Tap(2, 1000),
            Tap(1, 2000), Tap(3, 2000)));
        var reducedOnly = View(Trial(result, 0), ExactCompletionContextView.ReducedOnly);

        Assert.True(reducedOnly.BothTargetMembersMarginallySupported);
        Assert.False(reducedOnly.TargetCompletionSetJointlyObserved);
        Assert.True(reducedOnly.TargetMarginalOnly);
        Assert.Equal(TargetCompositionReconstructionState.TargetAbsentFromJointEvidence,
            reducedOnly.TargetState);
        Assert.True(reducedOnly.MarginalOnlyPairs > 0);
        Assert.Equal(0, reducedOnly.JointWitnessCrossOccurrenceLeakageCount);
    }

    [Fact]
    public void SingleGroupHasNoComparableCompositionContext()
    {
        var result = ResultingStateCompositionResearch.Evaluate(Chart(4, Tap(0, 0), Tap(1, 0)));
        Assert.Equal(TargetCompositionReconstructionState.NoComparableCompositionContext,
            View(Trial(result, 0), ExactCompletionContextView.ReducedOnly).TargetState);
    }

    [Fact]
    public void WholeTargetGroupIsExcludedFromDonors()
    {
        var result = ResultingStateCompositionResearch.Evaluate(Chart(4, Tap(0, 0), Tap(1, 0)));
        Assert.Equal(0, result.TargetWholeGroupLeakageCount);
        Assert.All(result.PairHoldoutTrials.SelectMany(x => x.Views),
            view => Assert.Equal(0, view.TargetWholeGroupLeakageCount));
    }

    [Fact]
    public void NonEmptyReducedStateReconstructsOriginalGroup()
    {
        var result = ResultingStateCompositionResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(1, 0), Tap(2, 0),
            Tap(0, 1000), Tap(1, 1000), Tap(2, 1000)));
        var target = result.PairHoldoutTrials.First(x => x.TargetHeadTime == 0 && x.ReducedHeadCount == 1
            && x.TargetCompletionSet.Members.All(m => m.Lane is 1 or 2));

        Assert.Equal("K:4|T:0|L:", target.ReducedStateSignature);
        Assert.Equal("K:4|T:0,1,2|L:", target.TargetResultingState.Signature);
        Assert.Equal(TargetCompositionReconstructionState.ObservedJointUnique,
            View(target, ExactCompletionContextView.ReducedOnly).TargetState);
    }

    [Theory]
    [InlineData(false, false, CompletionPairType.TapTap)]
    [InlineData(false, true, CompletionPairType.TapLongNote)]
    [InlineData(true, true, CompletionPairType.LongNoteLongNote)]
    public void PairTypesRemainDistinct(bool firstLn, bool secondLn, CompletionPairType expected)
    {
        ManiaObject First(int time) => firstLn ? Ln(0, time, time + 400) : Tap(0, time);
        ManiaObject Second(int time) => secondLn ? Ln(1, time, time + 400) : Tap(1, time);
        var result = ResultingStateCompositionResearch.Evaluate(Chart(4,
            First(0), Second(0), First(1000), Second(1000)));

        Assert.Equal(expected, Trial(result, 0).PairType);
    }

    [Fact]
    public void HeldTailAndReleaseAreNeverCompletionHeads()
    {
        var result = ResultingStateCompositionResearch.Evaluate(Chart(4,
            Ln(3, -500, 1500), Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(1, 1000)));

        Assert.Equal(0, result.HeldTailAsHeadCount);
        Assert.All(result.ObservedPairOccurrences.SelectMany(x => x.CompletionSet.Members),
            member => Assert.NotEqual(3, member.Lane));
        Assert.Contains(result.PairHoldoutTrials, x => x.HeldBeforePresent);
    }

    [Fact]
    public void FutureHeldHygieneRemovesTargetLongNotes()
    {
        var result = ResultingStateCompositionResearch.Evaluate(Chart(4,
            Ln(0, 0, 1500), Tap(1, 0), Tap(2, 1000), Tap(3, 1000),
            Ln(0, 2000, 3500), Tap(1, 2000)));

        Assert.Equal(0, result.FutureHeldLeakageCount);
    }

    [Fact]
    public void HypotheticalClassifierSeparatesAllFourStates()
    {
        var a = new CompletionMemberIdentity(0, OriginalHeadMemberType.TapHead);
        var b = new CompletionMemberIdentity(1, OriginalHeadMemberType.TapHead);
        var c = new CompletionMemberIdentity(2, OriginalHeadMemberType.TapHead);
        var ab = CompletionSetIdentity.Create([a, b]);
        var ac = CompletionSetIdentity.Create([a, c]);

        Assert.Equal(HypotheticalCompositionState.JointObserved,
            ResultingStateCompositionResearch.ClassifyHypothetical(4, [], ab, [a, b], [ab]));
        Assert.Equal(HypotheticalCompositionState.MarginalOnly,
            ResultingStateCompositionResearch.ClassifyHypothetical(4, [], ab, [a, b], [ac]));
        Assert.Equal(HypotheticalCompositionState.NotMarginallySupported,
            ResultingStateCompositionResearch.ClassifyHypothetical(4, [], ab, [a], [ab]));
        Assert.Equal(HypotheticalCompositionState.HardInvalid,
            ResultingStateCompositionResearch.ClassifyHypothetical(4, [a], ab, [a, b], [ab]));
    }

    [Fact]
    public void D0K1ControlIsReproducedExactly()
    {
        var chart = Chart(4, Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(1, 1000));
        var d0 = ChordCompletionResearch.Evaluate(chart);
        var d10 = ResultingStateCompositionResearch.Evaluate(chart);

        Assert.Equal(d0.CompletionTrials, d10.K1ControlTrials);
        Assert.Equal(d0.Trials.Count(x => x.State != ChordCompletionReconstructionState.NoComparableReducedState),
            d10.K1ControlComparable);
        Assert.Equal(d0.Trials.Count(x => x.ExactTargetCompletionSupported), d10.K1ControlTargetSupported);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void MultikeyEvaluationHasNoKeymodeBranches(int keys)
    {
        var objects = keys == 1
            ? new[] { Tap(0, 0), Tap(0, 1000) }
            : new[] { Tap(0, 0), Tap(keys - 1, 0), Tap(0, 1000), Tap(keys - 1, 1000) };
        var result = ResultingStateCompositionResearch.Evaluate(Chart(keys, objects));

        Assert.Equal(keys, result.KeyCount);
        Assert.Equal(keys == 1 ? 0 : 2, result.PairHoldoutTrials.Length);
    }

    [Fact]
    public void DenseRiceSentinelEnumeratesEveryPairWithoutSampling()
    {
        var result = ResultingStateCompositionResearch.Evaluate(Chart(7,
            Tap(0, 0), Tap(1, 0), Tap(2, 0), Tap(3, 0),
            Tap(0, 31), Tap(1, 31), Tap(2, 31), Tap(3, 31)));

        Assert.Equal(12, result.PairHoldoutTrials.Length);
        Assert.True(result.PairEnumerationOperations >= 12);
    }

    [Fact]
    public void EveryExactContextViewIsEvaluatedIndependently()
    {
        var trial = Trial(ResultingStateCompositionResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(1, 1000))), 0);

        Assert.Equal(ExactCompletionContextResearch.Views, trial.Views.Select(x => x.View));
        Assert.Equal(8, trial.Views.Length);
    }

    [Fact]
    public void AddedObjectsCannotTeachCompositionEvidence()
    {
        var original = Chart(4, Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(1, 1000));
        var contaminated = new ManiaChart
        {
            KeyCount = original.KeyCount,
            Lines = original.Lines,
            OriginalObjects = original.OriginalObjects,
            TimingPoints = original.TimingPoints,
            AddedObjects = [ManiaObject.Tap(2, 0, true)]
        };

        Assert.Equal(ResultingStateCompositionResearchJson.Serialize(
                ResultingStateCompositionResearch.Evaluate(original)),
            ResultingStateCompositionResearchJson.Serialize(
                ResultingStateCompositionResearch.Evaluate(contaminated)));
    }

    [Fact]
    public void RerunIsByteDeterministic()
    {
        var chart = Chart(4, Tap(1, 0), Tap(0, 0), Tap(1, 1000), Tap(0, 1000));

        var first = ResultingStateCompositionResearchJson.Serialize(
            ResultingStateCompositionResearch.Evaluate(chart));
        var second = ResultingStateCompositionResearchJson.Serialize(
            ResultingStateCompositionResearch.Evaluate(chart));

        Assert.Equal(first, second);
    }

    [Fact]
    public void ResearchDoesNotChangeLegacyOutputOrRandomTranscript()
    {
        var chart = Chart(4, Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(1, 1000));
        var options = new AddNotesOptions { Chance = .5, ContextualDensityNormalizationEnabled = false };
        var beforeRandom = new RecordingRandom(73);
        var afterRandom = new RecordingRandom(73);
        var before = new AddNotesEngine().Apply(chart, options, beforeRandom);
        _ = ResultingStateCompositionResearch.Evaluate(chart);
        var after = new AddNotesEngine().Apply(chart, options, afterRandom);

        Assert.Equal(OsuBeatmap.Write(before.ModifiedChart, options.Chance),
            OsuBeatmap.Write(after.ModifiedChart, options.Chance));
        Assert.Equal(beforeRandom.Transcript, afterRandom.Transcript);
    }

    [Fact]
    public void CorrectModelHasZeroLeakageAudits()
    {
        var result = ResultingStateCompositionResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(2, 1000), Tap(1, 2000), Tap(3, 2000)));

        Assert.Equal(0, result.TargetWholeGroupLeakageCount);
        Assert.Equal(0, result.TargetObservationLeakageCount);
        Assert.Equal(0, result.FutureHeldLeakageCount);
        Assert.Equal(0, result.SyntheticTeachingCount);
        Assert.Equal(0, result.CrossChartEvidenceCount);
        Assert.Equal(0, result.HeldTailAsHeadCount);
        Assert.Equal(0, result.JointWitnessCrossOccurrenceLeakageCount);
        Assert.Equal(0, result.OrderInvarianceViolationCount);
    }

    private static PairHoldoutTrial Trial(ResultingStateCompositionResearchResult result, int time) =>
        result.PairHoldoutTrials.First(x => x.TargetHeadTime == time);

    private static CompositionViewAudit View(PairHoldoutTrial trial, ExactCompletionContextView view) =>
        trial.Views.Single(x => x.View == view);

    private static OriginalHeadMember Member(int id, int lane, OriginalHeadMemberType type) =>
        new(new OriginalObservationId(id), lane, type);

    private static ManiaObject Tap(int lane, int time) => ManiaObject.Tap(lane, time);
    private static ManiaObject Ln(int lane, int start, int end) => ManiaObject.Ln(lane, start, end);

    private static ManiaChart Chart(int keys, params ManiaObject[] objects) => new()
    {
        KeyCount = keys,
        Lines = [],
        OriginalObjects = objects.Select((item, index) => item with { Sequence = index }).ToArray(),
        TimingPoints = [new TimingPoint(0, 500)]
    };

    private sealed class RecordingRandom(int seed) : IRandomSource
    {
        private readonly Random random = new(seed);
        public List<string> Transcript { get; } = [];
        public double NextDouble() { var value = random.NextDouble(); Transcript.Add($"D:{value:R}"); return value; }
        public int Next(int maxExclusive) { var value = random.Next(maxExclusive); Transcript.Add($"I:{maxExclusive}:{value}"); return value; }
    }
}
