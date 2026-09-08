using System.Reflection;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseEAdaptiveContextResearchTests
{
    [Fact]
    public void EventSeriesUsesOnlyOriginalObjectsAndIgnoresSyntheticAdditions()
    {
        var source = Chart(4, [Tap(0, 0), Tap(1, 500)]);
        var withAdded = Chart(4, source.OriginalObjects, added: [ManiaObject.Tap(3, 250, true)]);

        Assert.Equal(AdaptiveContextResearchJson.Serialize(AdaptiveContextResearch.Evaluate(source)),
            AdaptiveContextResearchJson.Serialize(AdaptiveContextResearch.Evaluate(withAdded)));
        Assert.Equal(0, AdaptiveContextResearch.BuildEventSeries(withAdded).SyntheticTeachingCount);
    }

    [Fact]
    public void EventSeriesSeparatesTapLnHeadReleaseAndHeldBefore()
    {
        var series = AdaptiveContextResearch.BuildEventSeries(Chart(4,
            [Ln(0, 0, 1000), Tap(1, 500), Ln(2, 500, 1500)]));
        var atZero = series.Events.Single(x => x.SerializedTime == 0);
        var atFiveHundred = series.Events.Single(x => x.SerializedTime == 500);
        var atOneThousand = series.Events.Single(x => x.SerializedTime == 1000);

        Assert.Equal([0], atZero.Token.LongNoteHeadLanes.ToArray());
        Assert.Equal([1], atFiveHundred.Token.TapHeadLanes.ToArray());
        Assert.Equal([2], atFiveHundred.Token.LongNoteHeadLanes.ToArray());
        Assert.Equal([0], atFiveHundred.Token.HeldBeforeLanes.ToArray());
        Assert.Equal([0], atOneThousand.Token.LongNoteReleaseLanes.ToArray());
        Assert.DoesNotContain(0, atOneThousand.Token.HeldBeforeLanes);
    }

    [Fact]
    public void SimultaneousHeadsRemainOneDeterministicEvent()
    {
        var series = AdaptiveContextResearch.BuildEventSeries(Chart(7,
            [Tap(5, 0), Ln(2, 0, 500), Tap(1, 0)]));

        Assert.Equal(2, series.Events.Length);
        Assert.Equal([1, 5], series.Events[0].Token.TapHeadLanes.ToArray());
        Assert.Equal([2], series.Events[0].Token.LongNoteHeadLanes.ToArray());
        Assert.Equal(3, series.Events[0].Token.HeadCount);
    }

    [Fact]
    public void InputPermutationProducesExactSameSeries()
    {
        var objects = new[] { Tap(2, 500, 2), Tap(0, 0, 0), Ln(1, 1000, 1500, 1) };
        var left = AdaptiveContextResearch.BuildEventSeries(Chart(4, objects));
        var right = AdaptiveContextResearch.BuildEventSeries(Chart(4, objects.Reverse().ToArray()));

        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(left),
            System.Text.Json.JsonSerializer.Serialize(right));
    }

    [Fact]
    public void ExactRecurrenceFindsDistantReturnWithoutCreatingRegions()
    {
        var series = Series(4, [A(0), A(500), B(1000), B(1500), A(2000), A(2500)]);
        var recurrences = AdaptiveContextResearch.FindExactRecurrences(series, 2);
        var segmentation = AdaptiveContextResearch.InferStableRunBoundaries(series, 2);

        Assert.Contains(recurrences, x => x.Left.StartIndex == 0 && x.Right.StartIndex == 4);
        Assert.Empty(segmentation.Boundaries);
        Assert.Equal(AdaptiveSegmentationState.GlobalOnly, segmentation.State);
    }

    [Fact]
    public void AlternatingExactRecurrenceDoesNotForceSegmentation()
    {
        var series = Series(4, [A(0), B(500), A(1000), B(1500), A(2000), B(2500)]);

        Assert.NotEmpty(AdaptiveContextResearch.FindExactRecurrences(series, 2));
        Assert.Empty(AdaptiveContextResearch.InferStableRunBoundaries(series, 2).Boundaries);
    }

    [Fact]
    public void StationarySeriesHasNoBoundary()
    {
        var proposal = AdaptiveContextResearch.InferStableRunBoundaries(
            Series(4, [A(0), A(500), A(1000), A(1500), A(2000)]), 2);

        Assert.Equal(AdaptiveSegmentationState.GlobalOnly, proposal.State);
        Assert.Empty(proposal.Boundaries);
    }

    [Fact]
    public void AbruptStructureChangeAtSameBpmHasExactBoundary()
    {
        var proposal = AdaptiveContextResearch.InferStableRunBoundaries(Series(4,
            Pattern((0, 1), (500, 1), (1000, 1), (1500, 2), (2000, 2), (2500, 2))), 2);

        var boundary = Assert.Single(proposal.Boundaries);
        Assert.Equal("E00000003", boundary.BoundaryEventId);
        Assert.Equal(1500, boundary.SerializedTime);
        Assert.Equal(3, boundary.LeftRunLength);
        Assert.Equal(3, boundary.RightRunLength);
    }

    [Fact]
    public void MultipleStableRegimesProduceSeparateInferredBoundaries()
    {
        var proposal = AdaptiveContextResearch.InferStableRunBoundaries(Series(4,
            Pattern((0, 1), (500, 1), (1000, 1), (1500, 2), (2000, 2), (2500, 2),
                (3000, 1), (3500, 1), (4000, 1))), 2);

        Assert.Equal(2, proposal.Boundaries.Length);
        Assert.Equal(3, proposal.Regions.Length);
    }

    [Fact]
    public void IsolatedOutlierDoesNotBecomeRegime()
    {
        var proposal = AdaptiveContextResearch.InferStableRunBoundaries(Series(4,
            Pattern((0, 1), (500, 1), (1000, 1), (1500, 2), (2000, 1), (2500, 1), (3000, 1))), 2);

        Assert.Empty(proposal.Boundaries);
        Assert.Equal(AdaptiveSegmentationState.NoStableBoundary, proposal.State);
    }

    [Fact]
    public void GradualOneEventChangesDoNotClaimAbruptBoundary()
    {
        var proposal = AdaptiveContextResearch.InferStableRunBoundaries(Series(4,
            Pattern((0, 1), (500, 2), (1000, 3), (1500, 4))), 2);

        Assert.Empty(proposal.Boundaries);
    }

    [Fact]
    public void ShortChartAbstainsInsteadOfInventingRegions()
    {
        var proposal = AdaptiveContextResearch.InferStableRunBoundaries(Series(4, [A(0), B(500), A(1000)]), 2);

        Assert.Equal(AdaptiveSegmentationState.InsufficientStructure, proposal.State);
        Assert.Empty(proposal.Regions);
    }

    [Fact]
    public void BpmOnlyChangeDoesNotCreateStructuralBoundary()
    {
        var chart = Chart(4, [A(0), A(500), A(1000), A(1250), A(1500), A(1750)],
            timing: [new TimingPoint(0, 500), new TimingPoint(1000, 250)]);

        Assert.Empty(AdaptiveContextResearch.InferStableRunBoundaries(
            AdaptiveContextResearch.BuildEventSeries(chart), 2).Boundaries);
    }

    [Fact]
    public void RedundantSameBpmRedlineDoesNotChangeStructuralResult()
    {
        var objects = new[] { A(0), A(500), A(1000), A(1500) };
        var one = AdaptiveContextResearch.BuildEventSeries(Chart(4, objects));
        var redundant = AdaptiveContextResearch.BuildEventSeries(Chart(4, objects,
            timing: [new TimingPoint(0, 500), new TimingPoint(1000, 500)]));

        Assert.Equal(one.Events.Select(x => x.Token.Identity), redundant.Events.Select(x => x.Token.Identity));
        Assert.Empty(AdaptiveContextResearch.InferStableRunBoundaries(redundant, 2).Boundaries);
    }

    [Fact]
    public void LnCrossingBpmChangeKeepsReleaseDistinctFromHead()
    {
        var series = AdaptiveContextResearch.BuildEventSeries(Chart(4, [Ln(0, 500, 1250), Tap(1, 1500)],
            timing: [new TimingPoint(0, 500), new TimingPoint(1000, 250)]));

        var release = series.Events.Single(x => x.SerializedTime == 1250);
        Assert.Equal([0], release.Token.LongNoteReleaseLanes.ToArray());
        Assert.Equal(0, release.Token.HeadCount);
    }

    [Fact]
    public void ExactNeighborHeldOutReconstructsFromDisjointOccurrence()
    {
        var summary = AdaptiveContextResearch.EvaluateHeldOut(
            Series(4, [A(0), B(500), A(1000), A(1500), B(2000), A(2500)]),
            AdaptiveContextMethodKind.ExactNeighborRecurrence);

        Assert.True(summary.Reconstructed >= 2);
        Assert.Equal(0, summary.LeakageCount);
        Assert.Contains(summary.Blocks, x => x.TargetEventId == "E00000001"
            && x.State == AdaptiveReconstructionState.Reconstructed);
    }

    [Fact]
    public void WholeEventGroupCannotDonateToItsOwnReconstruction()
    {
        var summary = AdaptiveContextResearch.EvaluateHeldOut(
            Series(4, Pattern((0, 1), (500, 2), (1000, 1))), AdaptiveContextMethodKind.GlobalChart);
        var target = summary.Blocks.Single(x => x.TargetEventId == "E00000001");

        Assert.Empty(target.DonorObservationIds.Intersect(target.ExcludedObservationIds));
        Assert.Equal(0, target.LeakageCount);
    }

    [Fact]
    public void LeakageAuditHasBadPositiveControlAndCorrectZero()
    {
        var excluded = new[] { new OriginalObservationId(2), new OriginalObservationId(3) };

        Assert.Equal(1, AdaptiveContextResearch.AuditLeakage(excluded,
            [new OriginalObservationId(1), new OriginalObservationId(2)]));
        Assert.Equal(0, AdaptiveContextResearch.AuditLeakage(excluded,
            [new OriginalObservationId(0), new OriginalObservationId(1)]));
    }

    [Fact]
    public void BlockHoldoutMeasuresBoundaryPerturbationWithoutLeakage()
    {
        var series = Series(4,
            Pattern((0, 1), (500, 1), (1000, 1), (1500, 2), (2000, 2), (2500, 2),
                (3000, 1), (3500, 1)));
        var stability = AdaptiveContextResearch.EvaluateBoundaryStability(series,
            ["E00000003", "E00000004"], 2);

        Assert.Equal(0, stability.LeakageCount);
        Assert.Equal(4, stability.ExcludedObservationIds.Length);
        Assert.True(stability.BoundaryChanges >= 0);
    }

    [Fact]
    public void RerunIsByteExactAndContainsNoRuntimeTiming()
    {
        var chart = Chart(4, [A(0), B(500), A(1000), B(1500), A(2000)]);

        Assert.Equal(AdaptiveContextResearchJson.Serialize(AdaptiveContextResearch.Evaluate(chart)),
            AdaptiveContextResearchJson.Serialize(AdaptiveContextResearch.Evaluate(chart)));
    }

    [Fact]
    public void ParameterSetsAreExplicitAndNoWinnerIsEncoded()
    {
        var catalog = AdaptiveContextResearch.PreHumanMethodCatalog;

        Assert.Contains(catalog, x => x.Identity.ParameterSetId == "min-run-2");
        Assert.Contains(catalog, x => x.Identity.ParameterSetId == "min-run-3");
        Assert.All(catalog.Where(x => x.Identity.MethodId == "stable-run-boundary.1"),
            x => Assert.Equal(AdaptiveParameterKind.ResearchHypothesis, Assert.Single(x.Parameters).Kind));
        Assert.DoesNotContain(catalog, x => x.Identity.MethodId.Contains("winner", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SyntheticBoundaryEvaluatorDetectsEveryEventAndNoBoundaryFailures()
    {
        var expected = new[] { "E00000003" };
        var everyEvent = AdaptiveContextResearch.EvaluateSyntheticBoundaries(expected,
            Enumerable.Range(1, 5).Select(x => $"E{x:D8}"));
        var none = AdaptiveContextResearch.EvaluateSyntheticBoundaries(expected, []);

        Assert.True(everyEvent.FalsePositive > 0);
        Assert.Equal(1, none.FalseNegative);
    }

    [Fact]
    public void SyntheticRecurrenceEvaluatorDetectsFalseMergeAndSplit()
    {
        var falseMerge = AdaptiveContextResearch.EvaluateSyntheticRecurrences(["A=>A"], ["A=>A", "A=>B"]);
        var falseSplit = AdaptiveContextResearch.EvaluateSyntheticRecurrences(["A=>A"], []);

        Assert.Equal(1, falseMerge.FalseMerge);
        Assert.Equal(1, falseSplit.FalseSplit);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void ResearchSupportsAllKeymodesWithoutStylisticBranches(int keys)
    {
        var result = AdaptiveContextResearch.Evaluate(Chart(keys, [Tap(keys - 1, 0), Tap(0, 500)]));

        Assert.Equal(keys, result.KeyCount);
        Assert.Equal(AdaptiveContextResearch.ResearchSchemaVersion, result.ResearchSchemaVersion);
    }

    [Fact]
    public void StableFastRiceAndJackLikeRepetitionDoNotFragment()
    {
        var stream = Enumerable.Range(0, 12).Select(x => Tap(x % 2, x * 31)).ToArray();
        var jack = Enumerable.Range(0, 12).Select(x => Tap(0, x * 31)).ToArray();

        Assert.Empty(AdaptiveContextResearch.InferStableRunBoundaries(Series(4, stream), 2).Boundaries);
        Assert.Empty(AdaptiveContextResearch.InferStableRunBoundaries(Series(4, jack), 2).Boundaries);
    }

    [Fact]
    public void SparseToDenseChangeIsVisibleWithoutTimingThreshold()
    {
        var proposal = AdaptiveContextResearch.InferStableRunBoundaries(Series(4,
            Pattern((0, 1), (1000, 1), (2000, 1), (2500, 2), (2600, 2), (2700, 2))), 2);

        Assert.Single(proposal.Boundaries);
    }

    [Fact]
    public void ResearchApiHasNoRandomOrGenerationResultDependency()
    {
        var methods = typeof(AdaptiveContextResearch).GetMethods(BindingFlags.Public | BindingFlags.Static);
        Assert.DoesNotContain(methods.SelectMany(x => x.GetParameters()),
            x => typeof(IRandomSource).IsAssignableFrom(x.ParameterType));
        Assert.DoesNotContain(methods.Select(x => x.ReturnType), x => x == typeof(AddNotesResult));
        Assert.DoesNotContain(typeof(AddNotesEngine).Assembly.GetTypes()
            .Where(x => x == typeof(AddNotesEngine)).SelectMany(x => x.GetFields()),
            x => x.FieldType == typeof(AdaptiveContextResearchResult));
    }

    [Fact]
    public void ResearchEvaluationDoesNotChangeLegacyOutputOrRngTranscript()
    {
        var chart = Chart(4, [A(0), B(500), A(1000), B(1500)]);
        var options = new AddNotesOptions { Chance = .5, ContextualDensityNormalizationEnabled = false };
        var leftRandom = new RecordingRandom(42);
        var rightRandom = new RecordingRandom(42);
        var before = new AddNotesEngine().Apply(chart, options, leftRandom);
        _ = AdaptiveContextResearch.Evaluate(chart);
        var after = new AddNotesEngine().Apply(chart, options, rightRandom);

        Assert.Equal(OsuBeatmap.Write(before.ModifiedChart, options.Chance),
            OsuBeatmap.Write(after.ModifiedChart, options.Chance));
        Assert.Equal(leftRandom.Transcript, rightRandom.Transcript);
        Assert.Equal("legacy-experimental.1", before.EvidenceProfile.BehaviorPolicyVersion);
    }

    private static OriginalTemporalEventSeries Series(int keys, IReadOnlyList<ManiaObject> objects) =>
        AdaptiveContextResearch.BuildEventSeries(Chart(keys, objects));

    private static ManiaChart Chart(int keys, IReadOnlyList<ManiaObject> objects,
        IReadOnlyList<ManiaObject>? added = null, IReadOnlyList<TimingPoint>? timing = null) => new()
        {
            KeyCount = keys,
            Lines = [],
            OriginalObjects = objects,
            AddedObjects = added ?? [],
            TimingPoints = timing ?? [new TimingPoint(0, 500)]
        };

    private static ManiaObject A(int time) => Tap(0, time);
    private static ManiaObject B(int time) => Tap(1, time);
    private static ManiaObject Tap(int lane, int time, int sequence = 0) => ManiaObject.Tap(lane, time, sequence: sequence);
    private static ManiaObject Ln(int lane, int start, int end, int sequence = 0) =>
        ManiaObject.Ln(lane, start, end, sequence: sequence);
    private static ManiaObject[] Pattern(params (int Time, int HeadCount)[] events) => events
        .SelectMany(e => Enumerable.Range(0, e.HeadCount).Select(lane => Tap(lane, e.Time))).ToArray();

    private sealed class RecordingRandom(int seed) : IRandomSource
    {
        private readonly Random _random = new(seed);
        public List<string> Transcript { get; } = [];
        public double NextDouble()
        {
            var value = _random.NextDouble();
            Transcript.Add($"D:{value:R}");
            return value;
        }
        public int Next(int maxExclusive)
        {
            var value = _random.Next(maxExclusive);
            Transcript.Add($"I:{maxExclusive}:{value}");
            return value;
        }
    }
}
