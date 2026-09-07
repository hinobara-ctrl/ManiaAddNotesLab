using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseD01ExactCompletionContextTests
{
    [Fact]
    public void ExactPreviousContextCanResolveBaselineCompetition()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4,
            Tap(1, 0), Tap(0, 500), Tap(2, 500),
            Tap(1, 1000), Tap(0, 1500), Tap(2, 1500),
            Tap(3, 2000), Tap(0, 2500), Tap(3, 2500)));

        var target = Trial(result, 1500, 2);
        Assert.Equal(ChordCompletionReconstructionState.TargetAmongCompetingCompletions, target.BaselineState);
        Assert.Equal(ExactCompletionContextOutcome.TargetResolvedUnique,
            View(target, ExactCompletionContextView.ReducedPrevious).Outcome);
    }

    [Fact]
    public void ExactContextCanProduceUniqueWrongCompletion()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4,
            Tap(1, 0), Tap(0, 500), Tap(3, 500),
            Tap(1, 1000), Tap(0, 1500), Tap(2, 1500)));

        Assert.Equal(ExactCompletionContextOutcome.UniqueWrongCompletion,
            View(Trial(result, 1500, 2), ExactCompletionContextView.ReducedPrevious).Outcome);
    }

    [Fact]
    public void OverSpecificExactContextCanHaveNoComparableDonor()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4,
            Tap(1, 0), Tap(0, 500), Tap(2, 500),
            Tap(1, 1250), Tap(0, 1500), Tap(2, 1500)));

        var target = Trial(result, 1500, 2);
        Assert.True(View(target, ExactCompletionContextView.ReducedPrevious).ExactTargetSupported);
        Assert.Equal(ExactCompletionContextOutcome.NoComparableExactContext,
            View(target, ExactCompletionContextView.ReducedPreviousTransition).Outcome);
    }

    [Fact]
    public void HeldBeforeSeparatesOtherwiseCompetingCompletions()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4,
            Ln(1, 0, 1000), Tap(0, 500), Tap(3, 500),
            Tap(0, 1500), Tap(2, 1500), Tap(0, 2500), Tap(2, 2500)));

        var target = Trial(result, 1500, 2);
        Assert.Equal(ExactCompletionContextOutcome.TargetResolvedUnique,
            View(target, ExactCompletionContextView.ReducedHeld).Outcome);
    }

    [Fact]
    public void HeldTailIsNeverEncodedAsAHead()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4,
            Ln(3, 0, 1500), Tap(0, 500), Tap(2, 500)));
        var context = Assert.Single(result.Trials.Select(x => x.TargetContext).DistinctBy(x => x.SourceGroupId));

        Assert.Contains(3, context.HeldBeforeHeadLanes);
        Assert.DoesNotContain(3, context.CurrentGroupObservationIds.Select(id =>
            result.Occurrences.FirstOrDefault(x => x.Witness.CompletionObservationId == id)?.Relation.CompletionLane ?? -1));
        Assert.Equal(0, result.HeldTailEncodedAsHeadCount);
    }

    [Fact]
    public void PreviousHeadStateKeepsTapAndLongNoteTypesExact()
    {
        var tapPrevious = ExactCompletionContextResearch.Evaluate(Chart(4,
            Tap(1, 0), Tap(0, 500), Tap(2, 500), Tap(1, 1000), Tap(0, 1500), Tap(2, 1500)));
        var lnPrevious = ExactCompletionContextResearch.Evaluate(Chart(4,
            Ln(1, 0, 250), Tap(0, 500), Tap(2, 500), Tap(1, 1000), Tap(0, 1500), Tap(2, 1500)));

        Assert.NotEqual(View(Trial(tapPrevious, 500, 2), ExactCompletionContextView.ReducedPrevious).ExactContextSignature,
            View(Trial(lnPrevious, 500, 2), ExactCompletionContextView.ReducedPrevious).ExactContextSignature);
    }

    [Fact]
    public void NextHeadStateIsImmediateAndExact()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(2, 0), Tap(1, 500), Tap(0, 1000), Tap(2, 1000), Tap(1, 1500)));

        var target = Trial(result, 0, 2);
        Assert.Equal(500, target.TargetContext.NextTransition!.Neighbor.HeadTime);
        Assert.Equal("K:4|T:1|L:", target.TargetContext.NextTransition.Neighbor.StructuralSignature);
    }

    [Fact]
    public void PreviousTransitionUsesExactBeatGap()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4,
            Tap(1, 0), Tap(0, 500), Tap(2, 500), Tap(1, 1250), Tap(0, 1500), Tap(2, 1500)));

        Assert.Equal(.5m, Trial(result, 1500, 2).TargetContext.PreviousTransition!.DeltaBeat);
        Assert.Contains("DB:0.5", View(Trial(result, 1500, 2),
            ExactCompletionContextView.ReducedPreviousTransition).ExactContextSignature);
    }

    [Fact]
    public void NextTransitionUsesExactBeatGap()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(2, 0), Tap(1, 375), Tap(0, 1000), Tap(2, 1000), Tap(1, 1500)));

        Assert.Equal(.75m, Trial(result, 0, 2).TargetContext.NextTransition!.DeltaBeat);
        Assert.Contains("DB:0.75", View(Trial(result, 0, 2),
            ExactCompletionContextView.ReducedNextTransition).ExactContextSignature);
    }

    [Fact]
    public void WholeTargetGroupCannotDonateToAnyView()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4, Tap(0, 0), Tap(2, 0)));
        Assert.All(result.Trials.SelectMany(x => x.Views), x =>
            Assert.Equal(ExactCompletionContextOutcome.NoComparableExactContext, x.Outcome));
        Assert.Equal(0, result.TargetGroupLeakageCount);
    }

    [Fact]
    public void TargetLongNoteCannotLeakIntoNextHeldContext()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4,
            Tap(0, 500), Ln(2, 500, 2000), Tap(1, 1000)));
        var context = Trial(result, 500, 2).TargetContext;

        Assert.DoesNotContain(2, context.NextHeldBeforeLanesExcludingCurrentGroup);
        Assert.Contains(new OriginalObservationId(1), context.CurrentLongNotesRemovedFromNextHeldContext);
        Assert.Equal(0, result.FutureHeldLeakageCount);
    }

    [Fact]
    public void DonorLongNoteUsesTheSameLeakFreeContextConstruction()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4,
            Tap(0, 0), Ln(2, 0, 1500), Tap(1, 500),
            Tap(0, 2000), Ln(2, 2000, 3500), Tap(1, 2500)));

        Assert.All(result.Occurrences.Where(x => x.Relation.CompletionType == OriginalHeadMemberType.LongNoteHead),
            occurrence => Assert.DoesNotContain(occurrence.Witness.CompletionObservationId,
                occurrence.Context.NextHeldBeforeObservationIdsExcludingCurrentGroup));
    }

    [Fact]
    public void MoreSpecificDonorSetsAreNestedInTheirDeclaredParents()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4,
            Tap(1, 0), Tap(0, 500), Tap(2, 500),
            Tap(1, 1000), Tap(0, 1500), Tap(2, 1500)));

        Assert.Equal(0, result.DonorNestingViolationCount);
    }

    [Fact]
    public void SeparateChartsNeverShareContextDonors()
    {
        var first = ExactCompletionContextResearch.Evaluate(Chart(4, Tap(0, 0), Tap(2, 0)));
        var second = ExactCompletionContextResearch.Evaluate(Chart(4, Tap(0, 1000), Tap(2, 1000)));

        Assert.NotEqual(first.ChartFingerprint, second.ChartFingerprint);
        Assert.All(first.Trials.Concat(second.Trials).SelectMany(x => x.Views), x =>
            Assert.Equal(ExactCompletionContextOutcome.NoComparableExactContext, x.Outcome));
    }

    [Fact]
    public void ResearchIsDeterministicContainsNoPolicySignalsAndConsumesNoRandom()
    {
        var chart = Chart(4, Tap(0, 0), Ln(2, 0, 1000), Tap(0, 1500), Ln(2, 1500, 2500));
        var first = ExactCompletionContextResearch.Evaluate(chart);
        var second = ExactCompletionContextResearch.Evaluate(chart);
        var firstJson = ExactCompletionContextResearchJson.Serialize(first with
            { ContextBuildMilliseconds = 0, ExactMatchMilliseconds = 0 });
        var secondJson = ExactCompletionContextResearchJson.Serialize(second with
            { ContextBuildMilliseconds = 0, ExactMatchMilliseconds = 0 });

        Assert.Equal(firstJson, secondJson);
        Assert.Contains("phase-d0-1-research.1", firstJson);
        Assert.DoesNotContain("confidence", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("probability", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mapperSupport", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weight", firstJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResearchDoesNotChangeGenerationOrRandomTranscript()
    {
        var chart = Chart(4, Tap(0, 0), Tap(2, 0), Ln(1, 500, 1500));
        var options = new AddNotesOptions { Chance = .73, ContextualDensityNormalizationEnabled = false };
        var beforeRandom = new RecordingRandom(44);
        var afterRandom = new RecordingRandom(44);
        var before = new AddNotesEngine().Apply(chart, options, beforeRandom);

        _ = ExactCompletionContextResearch.Evaluate(chart);
        var after = new AddNotesEngine().Apply(chart, options, afterRandom);

        Assert.Equal(OsuBeatmap.Write(before.ModifiedChart, options.Chance),
            OsuBeatmap.Write(after.ModifiedChart, options.Chance));
        Assert.Equal(beforeRandom.Transcript, afterRandom.Transcript);
        Assert.Equal("legacy-experimental.1", MapperEvidenceProfileBuilder.BehaviorPolicyVersion);
        Assert.Equal("phase-a.1", MapperEvidenceProfileBuilder.EvidenceProfileVersion);
        Assert.Equal("phase-c1-2-shadow.1", DecisionDiagnosticVersions.Current);
    }

    [Fact]
    public void TapAndLongNoteCompletionsRemainExactAndSeparate()
    {
        var result = ExactCompletionContextResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(2, 0), Tap(0, 1000), Ln(2, 1000, 1500),
            Tap(0, 2000), Tap(2, 2000)));
        var tap = Trial(result, 0, 2);
        var view = View(tap, ExactCompletionContextView.ReducedOnly);

        Assert.True(view.LaneSupportedButTypeDifferent);
        Assert.Equal(2, view.ContextualCompletionCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void SameContextImplementationSupportsAllKeyCounts(int keys)
    {
        var second = Math.Max(0, keys - 1);
        var result = ExactCompletionContextResearch.Evaluate(Chart(keys,
            Tap(0, 0), Tap(second, 0), Tap(0, 1000), Tap(second, 1000)));

        Assert.Equal(keys, result.KeyCount);
        Assert.All(result.Trials.SelectMany(x => x.Views), x => Assert.True(x.ContextualCompletionCount >= 0));
    }

    [Fact]
    public void LanePermutationPreservesOutcomeCountsForEveryView()
    {
        var permutation = new[] { 2, 0, 3, 1 };
        var source = new[] { Tap(1, 0), Tap(0, 500), Ln(2, 500, 900),
            Tap(1, 1000), Tap(0, 1500), Ln(2, 1500, 1900) };
        var original = ExactCompletionContextResearch.Evaluate(Chart(4, source));
        var permuted = ExactCompletionContextResearch.Evaluate(Chart(4,
            source.Select(x => x with { Lane = permutation[x.Lane] }).ToArray()));

        foreach (var view in ExactCompletionContextResearch.Views)
            Assert.Equal(OutcomeCounts(original, view), OutcomeCounts(permuted, view));
    }

    private static string OutcomeCounts(ExactCompletionContextResearchResult result,
        ExactCompletionContextView view) => string.Join(';', result.Trials.Select(x => View(x, view).Outcome)
        .GroupBy(x => x).OrderBy(x => x).Select(x => $"{x.Key}:{x.Count()}"));

    private static ExactCompletionCompetitionTrial Trial(ExactCompletionContextResearchResult result,
        int headTime, int lane) => result.Trials.Single(x => x.TargetContext.HeadTime == headTime
            && x.TargetLane == lane);
    private static ExactContextViewEvaluation View(ExactCompletionCompetitionTrial trial,
        ExactCompletionContextView view) => trial.Views.Single(x => x.View == view);
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
        public int Next(int maxExclusive) { var value = _inner.Next(maxExclusive); Transcript.Add($"I:{maxExclusive}:{value}"); return value; }
    }
}
