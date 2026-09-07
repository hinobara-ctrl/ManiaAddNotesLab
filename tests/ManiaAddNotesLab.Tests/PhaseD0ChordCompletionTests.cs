using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseD0ChordCompletionTests
{
    [Fact]
    public void RepeatedExactChordSupportsHiddenMember()
    {
        var result = ChordCompletionResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(2, 0), Tap(0, 1000), Tap(2, 1000)));
        var target = Trial(result, 0, 2);

        Assert.True(target.ExactTargetCompletionSupported);
        Assert.Equal(ChordCompletionReconstructionState.UniqueExactCompletion, target.State);
        Assert.Equal(1, target.ExactTargetIndependentWitnesses);
    }

    [Fact]
    public void CompetingExactCompletionsRemainSeparate()
    {
        var result = ChordCompletionResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(2, 0),
            Tap(0, 1000), Tap(2, 1000),
            Tap(0, 2000), Tap(3, 2000)));
        var target = Trial(result, 0, 2);

        Assert.Equal(ChordCompletionReconstructionState.TargetAmongCompetingCompletions, target.State);
        Assert.Equal(2, target.ComparableCompletionRelations);
        Assert.Equal(2, target.ComparableIndependentWitnesses);
    }

    [Fact]
    public void TapAndLongNoteHeadAreDifferentCompletionRelations()
    {
        var result = ChordCompletionResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(2, 0), Tap(0, 1000), Ln(2, 1000, 1500)));
        var completions = result.Relations.Where(x =>
                x.Key.ReducedTapHeadLanes.SequenceEqual([0]) && x.Key.ReducedLongNoteHeadLanes.Length == 0)
            .Select(x => (x.Key.CompletionLane, x.Key.CompletionType)).ToArray();

        Assert.Contains((2, OriginalHeadMemberType.TapHead), completions);
        Assert.Contains((2, OriginalHeadMemberType.LongNoteHead), completions);
        Assert.True(Trial(result, 0, 2).LaneSupportedButTypeDifferent);
    }

    [Fact]
    public void HeldTailIsContextAndNeverAHeadMember()
    {
        var result = ChordCompletionResearch.Evaluate(Chart(4,
            Ln(2, 0, 1000), Tap(0, 500), Tap(1, 500)));
        var group = Assert.Single(result.ChordGroups);

        Assert.Equal([0, 1], group.HeadState.TapHeadLanes.ToArray());
        Assert.Empty(group.HeadState.LongNoteHeadLanes);
        Assert.Equal([2], group.HeadState.HeldBeforeHeadLanes.ToArray());
        Assert.DoesNotContain(result.Relations, x => x.Key.CompletionLane == 2);
    }

    [Fact]
    public void LongNoteStartingAtChordTimestampCannotLeakThroughHeldContext()
    {
        var result = ChordCompletionResearch.Evaluate(Chart(4, Tap(0, 500), Ln(2, 500, 1000)));
        var group = Assert.Single(result.ChordGroups);

        Assert.Empty(group.HeadState.HeldBeforeHeadLanes);
        Assert.Equal([2], group.HeadState.LongNoteHeadLanes.ToArray());
    }

    [Fact]
    public void WholeTargetGroupIsExcludedFromDonors()
    {
        var result = ChordCompletionResearch.Evaluate(Chart(4, Tap(0, 0), Tap(2, 0)));

        Assert.All(result.Trials, x =>
        {
            Assert.Equal(ChordCompletionReconstructionState.NoComparableReducedState, x.State);
            Assert.Equal(0, x.ComparableIndependentWitnesses);
        });
    }

    [Fact]
    public void DifferentDonorGroupsAreIndependentRelationWitnesses()
    {
        var result = ChordCompletionResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(2, 0),
            Tap(0, 1000), Tap(2, 1000),
            Tap(0, 2000), Tap(2, 2000)));
        var relation = result.Relations.Single(x => x.Key.ReducedStateSignature == "K:4|T:0|L:"
            && x.Key.CompletionLane == 2 && x.Key.CompletionType == OriginalHeadMemberType.TapHead);

        Assert.Equal(3, relation.IndependentRelationWitnessCount);
        Assert.Equal(2, Trial(result, 0, 2).ExactTargetIndependentWitnesses);
    }

    [Fact]
    public void ChartsNeverDonateCompletionEvidenceToEachOther()
    {
        var first = ChordCompletionResearch.Evaluate(Chart(4, Tap(0, 0), Tap(2, 0)));
        var second = ChordCompletionResearch.Evaluate(Chart(4, Tap(0, 1000), Tap(2, 1000)));

        Assert.All(first.Trials.Concat(second.Trials), x =>
            Assert.Equal(ChordCompletionReconstructionState.NoComparableReducedState, x.State));
        Assert.NotEqual(first.ChartFingerprint, second.ChartFingerprint);
    }

    [Fact]
    public void ResearchIsDeterministicAndContainsNoScores()
    {
        var chart = Chart(4, Tap(0, 0), Ln(2, 0, 1000), Tap(0, 1500), Ln(2, 1500, 2000));
        var first = ChordCompletionResearch.Evaluate(chart);
        var second = ChordCompletionResearch.Evaluate(chart);
        var firstJson = ChordCompletionResearchJson.Serialize(first with
            { RelationBuildMilliseconds = 0, HeldOutReconstructionMilliseconds = 0 });
        var secondJson = ChordCompletionResearchJson.Serialize(second with
            { RelationBuildMilliseconds = 0, HeldOutReconstructionMilliseconds = 0 });

        Assert.Equal(firstJson, secondJson);
        Assert.Contains("phase-d0-research.1", firstJson);
        Assert.DoesNotContain("confidence", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mapperSupport", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("score", firstJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResearchConsumesNoRandomAndDoesNotChangeGeneration()
    {
        var chart = Chart(4, Tap(0, 0), Tap(2, 0), Ln(1, 500, 1500));
        var options = new AddNotesOptions { Chance = .73, ContextualDensityNormalizationEnabled = false };
        var beforeRandom = new RecordingRandom(44);
        var afterRandom = new RecordingRandom(44);
        var before = new AddNotesEngine().Apply(chart, options, beforeRandom);

        _ = ChordCompletionResearch.Evaluate(chart);
        var after = new AddNotesEngine().Apply(chart, options, afterRandom);

        Assert.Equal(OsuBeatmap.Write(before.ModifiedChart, options.Chance),
            OsuBeatmap.Write(after.ModifiedChart, options.Chance));
        Assert.Equal(beforeRandom.Transcript, afterRandom.Transcript);
        Assert.Equal("legacy-experimental.1", MapperEvidenceProfileBuilder.BehaviorPolicyVersion);
        Assert.Equal("phase-a.1", MapperEvidenceProfileBuilder.EvidenceProfileVersion);
        Assert.Equal("phase-c1-2-shadow.1", DecisionDiagnosticVersions.Current);
    }

    [Fact]
    public void OneKeyChartWithoutSimultaneousHeadsHasNoChordTrials()
    {
        var result = ChordCompletionResearch.Evaluate(Chart(1, Tap(0, 0), Tap(0, 500)));

        Assert.Empty(result.ChordGroups);
        Assert.Empty(result.Relations);
        Assert.Empty(result.Trials);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void SameImplementationSupportsAllKeyCounts(int keys)
    {
        var result = ChordCompletionResearch.Evaluate(Chart(keys,
            Tap(0, 0), Tap(keys - 1, 0), Tap(0, 1000), Tap(keys - 1, 1000)));

        Assert.Equal(keys, result.KeyCount);
        Assert.Equal(2, result.ChordGroups.Length);
        Assert.All(result.Trials, x => Assert.True(x.ExactTargetCompletionSupported));
    }

    [Fact]
    public void WholeChartLanePermutationPermutesExactRelations()
    {
        var permutation = new[] { 2, 0, 3, 1 };
        var source = new[] { Tap(0, 0), Ln(2, 0, 500), Tap(0, 1000), Ln(2, 1000, 1500) };
        var original = ChordCompletionResearch.Evaluate(Chart(4, source));
        var permuted = ChordCompletionResearch.Evaluate(Chart(4,
            source.Select(x => x with { Lane = permutation[x.Lane] }).ToArray()));

        Assert.Equal(original.Relations.Length, permuted.Relations.Length);
        foreach (var relation in original.Relations)
        {
            var expectedReducedTaps = relation.Key.ReducedTapHeadLanes.Select(x => permutation[x]).Order().ToArray();
            var expectedReducedLns = relation.Key.ReducedLongNoteHeadLanes.Select(x => permutation[x]).Order().ToArray();
            Assert.Contains(permuted.Relations, candidate =>
                candidate.Key.CompletionLane == permutation[relation.Key.CompletionLane]
                && candidate.Key.CompletionType == relation.Key.CompletionType
                && candidate.Key.ReducedTapHeadLanes.SequenceEqual(expectedReducedTaps)
                && candidate.Key.ReducedLongNoteHeadLanes.SequenceEqual(expectedReducedLns));
        }
    }

    private static ChordCompletionReconstructionTrial Trial(ChordCompletionResearchResult result,
        int headTime, int targetLane) => result.Trials.Single(x =>
        result.ChordGroups.Single(g => g.SourceGroupId == x.TargetSourceGroupId).HeadTime == headTime
        && x.TargetLane == targetLane);

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

        public double NextDouble()
        {
            var value = _inner.NextDouble();
            Transcript.Add($"D:{BitConverter.DoubleToInt64Bits(value)}");
            return value;
        }

        public int Next(int maxExclusive)
        {
            var value = _inner.Next(maxExclusive);
            Transcript.Add($"I:{maxExclusive}:{value}");
            return value;
        }
    }
}
