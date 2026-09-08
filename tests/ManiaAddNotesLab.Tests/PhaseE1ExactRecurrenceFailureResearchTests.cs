using System.Collections.Immutable;
using System.Reflection;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseE1ExactRecurrenceFailureResearchTests
{
    [Fact]
    public void ExactNeighborDispositionsRemainPhaseEBaselineSemantics()
    {
        var series = Series("P", "X", "N", "Q", "P", "X", "N");
        var e = AdaptiveContextResearch.EvaluateHeldOut(series,
            AdaptiveContextMethodKind.ExactNeighborRecurrence);
        var e1 = ExactRecurrenceFailureResearch.Evaluate(series);

        Assert.Equal(e.Eligible, e1.Diagnostics.Length);
        Assert.Equal(e.Reconstructed, e1.Diagnostics.Count(x => x.Disposition == AdaptiveReconstructionState.Reconstructed));
        Assert.Equal(e.Ambiguous, e1.Diagnostics.Count(x => x.Disposition == AdaptiveReconstructionState.Ambiguous));
        Assert.Equal(e.Mismatch, e1.Diagnostics.Count(x => x.Disposition == AdaptiveReconstructionState.Mismatch));
        Assert.Equal(e.NoContext, e1.Diagnostics.Count(x => x.Disposition == AdaptiveReconstructionState.NoContext));
    }

    [Fact]
    public void ReconstructedControlRetainsExactDisjointDonor()
    {
        var diagnostic = ExactRecurrenceFailureResearch.DiagnoseTarget(
            Series("P", "X", "N", "Q", "P", "X", "N"), 1);

        Assert.Equal(AdaptiveReconstructionState.Reconstructed, diagnostic.Disposition);
        Assert.Equal(1, diagnostic.PostHoldoutDonorCount);
        Assert.Equal(1, diagnostic.DistinctCenterTokenCount);
        Assert.Equal(0, diagnostic.LeakageCount);
    }

    [Fact]
    public void AmbiguousControlIgnoresUnequalMultiplicityAsVote()
    {
        var identities = new List<string> { "P", "T", "N", "S" };
        for (var i = 0; i < 10; i++) identities.AddRange(["P", "A", "N", $"S{i}"]);
        identities.AddRange(["P", "B", "N"]);
        var diagnostic = ExactRecurrenceFailureResearch.DiagnoseTarget(Series(identities.ToArray()), 1);

        Assert.Equal(AdaptiveReconstructionState.Ambiguous, diagnostic.Disposition);
        Assert.Equal(2, diagnostic.DistinctCenterTokenCount);
        Assert.Contains(diagnostic.CenterMultiplicities, x => x.CenterTokenIdentity == "A" && x.OccurrenceCount == 10);
        Assert.Contains(diagnostic.CenterMultiplicities, x => x.CenterTokenIdentity == "B" && x.OccurrenceCount == 1);
    }

    [Fact]
    public void MismatchReportsCenterDifferencesWithoutChangingIdentity()
    {
        var series = Series("P", "X", "N", "Q", "P", "Y", "N");
        series = series with
        {
            Events = series.Events.SetItem(5, WithToken(series.Events[5], "Y", taps: [2], lns: [], held: []))
        };
        var diagnostic = ExactRecurrenceFailureResearch.DiagnoseTarget(series, 1);

        Assert.Equal(AdaptiveReconstructionState.Mismatch, diagnostic.Disposition);
        Assert.True(diagnostic.HasLaneOccupancyDivergence);
        Assert.Equal("neighbor-token-only", diagnostic.Counterfactuals[0].RefinementId);
        Assert.Equal(AdaptiveReconstructionState.Mismatch, diagnostic.Counterfactuals[0].RefinedDisposition);
    }

    [Fact]
    public void UniqueNeighborPairHasExplicitNoContextReason()
    {
        var diagnostic = ExactRecurrenceFailureResearch.DiagnoseTarget(Series("P", "X", "N", "Q"), 1);

        Assert.Equal(AdaptiveReconstructionState.NoContext, diagnostic.Disposition);
        Assert.Equal(ExactNoContextReason.NoExactNeighborPairElsewhere, diagnostic.PrimaryNoContextReason);
        Assert.Equal(1, diagnostic.PreHoldoutCandidateCount);
        Assert.Equal(1, diagnostic.ExcludedCandidateCount);
    }

    [Fact]
    public void TargetTouchingOccurrenceIsSanitizedAndExplained()
    {
        var series = Series("P", "X", "N", "Q", "P", "Y", "N");
        var donorPrevious = series.Events[4] with
        {
            HeadObservationIds = [series.Events[1].HeadObservationIds[0]]
        };
        series = series with { Events = series.Events.SetItem(4, donorPrevious) };
        var diagnostic = ExactRecurrenceFailureResearch.DiagnoseTarget(series, 1);

        Assert.Equal(AdaptiveReconstructionState.NoContext, diagnostic.Disposition);
        Assert.Equal(ExactNoContextReason.OnlyExcludedOccurrences, diagnostic.PrimaryNoContextReason);
        Assert.True(diagnostic.HasEndpointOrGroupCollision);
        Assert.Equal(0, diagnostic.PostHoldoutDonorCount);
        Assert.Equal(0, diagnostic.LeakageCount);
    }

    [Fact]
    public void ExactSpacingDivergenceCanTurnMismatchIntoNoContextButNotSupport()
    {
        var series = SeriesWithTimes([0, 100, 200, 300, 500, 700, 900], "P", "X", "N", "Q", "P", "Y", "N");
        var diagnostic = ExactRecurrenceFailureResearch.DiagnoseTarget(series, 1);
        var spacing = diagnostic.Counterfactuals.Single(x => x.RefinementId == "plus-exact-spacing");

        Assert.Equal(AdaptiveReconstructionState.Mismatch, diagnostic.Disposition);
        Assert.True(diagnostic.HasSerializedSpacingDivergence);
        Assert.Equal(AdaptiveReconstructionState.NoContext, spacing.RefinedDisposition);
        Assert.Equal(CounterfactualTransition.BecameNoContext, spacing.Transition);
    }

    [Fact]
    public void SameSpacingMismatchRemainsMismatch()
    {
        var diagnostic = ExactRecurrenceFailureResearch.DiagnoseTarget(
            Series("P", "X", "N", "Q", "P", "Y", "N"), 1);

        Assert.False(diagnostic.HasSerializedSpacingDivergence);
        Assert.Equal(CounterfactualTransition.PreservedMismatch,
            diagnostic.Counterfactuals.Single(x => x.RefinementId == "plus-exact-spacing").Transition);
    }

    [Fact]
    public void HeldAndTypeDifferencesAreIndependentDiagnosticFlags()
    {
        var series = Series("P", "X", "N", "Q", "P", "Y", "N");
        series = series with
        {
            Events = series.Events.SetItem(5, WithToken(series.Events[5], "Y", taps: [], lns: [1], held: [2]))
        };
        var diagnostic = ExactRecurrenceFailureResearch.DiagnoseTarget(series, 1);

        Assert.True(diagnostic.HasHeldStateDivergence);
        Assert.True(diagnostic.HasTypeCompositionDivergence);
        Assert.False(diagnostic.HasHeadCountDivergence);
    }

    [Fact]
    public void FourBlockOccurrencesEmitThreeConsecutiveLinksNotSixPairs()
    {
        var audit = Assert.Single(ExactRecurrenceFailureResearch.AuditBlockRelationSemantics(
            Series("A", "A", "A", "A"), 1));

        Assert.Equal(4, audit.OccurrenceCount);
        Assert.Equal(3, audit.EmittedConsecutiveRelationCount);
        Assert.Equal(6, audit.AllPairsCount);
    }

    [Fact]
    public void TransitionTaxonomyDistinguishesSupportFromCoverageDestruction()
    {
        Assert.Equal(CounterfactualTransition.BecameReconstructed,
            ExactRecurrenceFailureResearch.DescribeTransition(
                AdaptiveReconstructionState.Mismatch, AdaptiveReconstructionState.Reconstructed));
        Assert.Equal(CounterfactualTransition.BecameNoContext,
            ExactRecurrenceFailureResearch.DescribeTransition(
                AdaptiveReconstructionState.Mismatch, AdaptiveReconstructionState.NoContext));
    }

    [Fact]
    public void ChartEdgesAreDiagnosableButNotInEligiblePopulation()
    {
        var series = Series("A", "B", "C");

        Assert.Equal(ExactNoContextReason.EdgeIneligible,
            ExactRecurrenceFailureResearch.DiagnoseTarget(series, 0).PrimaryNoContextReason);
        Assert.Single(ExactRecurrenceFailureResearch.Evaluate(series).Diagnostics);
    }

    [Fact]
    public void ReleaseContextAndHeldTailStayDistinct()
    {
        var chart = new ManiaChart
        {
            KeyCount = 4,
            Lines = [],
            OriginalObjects = [ManiaObject.Ln(0, 0, 1000), ManiaObject.Tap(1, 500), ManiaObject.Tap(2, 1500)],
            TimingPoints = [new TimingPoint(0, 500)]
        };
        var result = ExactRecurrenceFailureResearch.Evaluate(chart);

        Assert.Contains(result.Diagnostics, x => x.TargetHeldBefore || x.ReleaseInvolvedExternalContext
            || x.TargetReleaseInvolved);
    }

    [Fact]
    public void AddedObjectsCannotChangeDiagnostics()
    {
        var original = new[] { ManiaObject.Tap(0, 0), ManiaObject.Tap(1, 500), ManiaObject.Tap(0, 1000) };
        var left = Chart(4, original, []);
        var right = Chart(4, original, [ManiaObject.Tap(3, 250, synthetic: true)]);

        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(ExactRecurrenceFailureResearch.Evaluate(left)),
            System.Text.Json.JsonSerializer.Serialize(ExactRecurrenceFailureResearch.Evaluate(right)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void KeymodeIsStratificationOnly(int keys)
    {
        var result = ExactRecurrenceFailureResearch.Evaluate(Chart(keys,
            [ManiaObject.Tap(0, 0), ManiaObject.Tap(Math.Min(1, keys - 1), 31), ManiaObject.Tap(0, 62)], []));

        Assert.Equal(keys, result.KeyCount);
        Assert.Equal(0, result.CrossChartEvidenceCount);
        Assert.Equal(0, result.SyntheticTeachingCount);
    }

    [Fact]
    public void FastRiceRecurrenceUsesSameExactSemantics()
    {
        var events = Enumerable.Range(0, 20).Select(i => i % 2 == 0 ? "A" : "B").ToArray();
        var result = ExactRecurrenceFailureResearch.Evaluate(SeriesWithTimes(
            Enumerable.Range(0, 20).Select(i => i * 31).ToArray(), events));

        Assert.Contains(result.Diagnostics, x => x.Disposition == AdaptiveReconstructionState.Reconstructed);
    }

    [Fact]
    public void RedundantRedlineDoesNotChangeFailureDispositionsOrExactBeatSpacing()
    {
        var objects = new[] { ManiaObject.Tap(0, 0), ManiaObject.Tap(1, 500), ManiaObject.Tap(0, 1000),
            ManiaObject.Tap(1, 1500), ManiaObject.Tap(0, 2000) };
        var one = new ManiaChart { KeyCount = 4, Lines = [], OriginalObjects = objects,
            TimingPoints = [new TimingPoint(0, 500)] };
        var redundant = new ManiaChart { KeyCount = 4, Lines = [], OriginalObjects = objects,
            TimingPoints = [new TimingPoint(0, 500), new TimingPoint(1000, 500)] };
        var left = ExactRecurrenceFailureResearch.Evaluate(one).Diagnostics;
        var right = ExactRecurrenceFailureResearch.Evaluate(redundant).Diagnostics;

        Assert.Equal(left.Select(x => (x.Disposition, x.PreviousExactFileDerivedBeatSpacing,
            x.NextExactFileDerivedBeatSpacing)), right.Select(x => (x.Disposition,
            x.PreviousExactFileDerivedBeatSpacing, x.NextExactFileDerivedBeatSpacing)));
    }

    [Fact]
    public void RealBpmChangeRemainsFileDerivedCoordinateNotFailureCause()
    {
        var chart = new ManiaChart { KeyCount = 4, Lines = [],
            OriginalObjects = [ManiaObject.Tap(0, 0), ManiaObject.Tap(1, 500), ManiaObject.Tap(0, 1000),
                ManiaObject.Tap(1, 1250), ManiaObject.Tap(0, 1500)],
            TimingPoints = [new TimingPoint(0, 500), new TimingPoint(1000, 250)] };
        var result = ExactRecurrenceFailureResearch.Evaluate(chart);

        Assert.All(result.Diagnostics, x => Assert.DoesNotContain("Bpm", x.Counterfactuals.Select(a => a.RefinementId)));
        Assert.Contains(result.Diagnostics, x => x.NextExactFileDerivedBeatSpacing == 1m);
    }

    [Fact]
    public void InputPermutationAndRerunAreDeterministic()
    {
        var objects = new[] { ManiaObject.Tap(2, 1000, sequence: 2), ManiaObject.Tap(0, 0),
            ManiaObject.Ln(1, 500, 1500, sequence: 1) };
        var left = ExactRecurrenceFailureResearch.Evaluate(Chart(4, objects, []));
        var right = ExactRecurrenceFailureResearch.Evaluate(Chart(4, objects.Reverse().ToArray(), []));

        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(left), System.Text.Json.JsonSerializer.Serialize(right));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(left),
            System.Text.Json.JsonSerializer.Serialize(ExactRecurrenceFailureResearch.Evaluate(Chart(4, objects, []))));
    }

    [Fact]
    public void E1EvaluationLeavesLegacyBytesAndRngTranscriptExact()
    {
        var chart = Chart(4, [ManiaObject.Tap(0, 0), ManiaObject.Tap(1, 500), ManiaObject.Tap(0, 1000)], []);
        var options = new AddNotesOptions { Chance = .5, ContextualDensityNormalizationEnabled = false };
        var leftRandom = new RecordingRandom(91);
        var rightRandom = new RecordingRandom(91);
        var before = new AddNotesEngine().Apply(chart, options, leftRandom);
        _ = ExactRecurrenceFailureResearch.Evaluate(chart);
        var after = new AddNotesEngine().Apply(chart, options, rightRandom);

        Assert.Equal(OsuBeatmap.Write(before.ModifiedChart, options.Chance), OsuBeatmap.Write(after.ModifiedChart, options.Chance));
        Assert.Equal(leftRandom.Transcript, rightRandom.Transcript);
    }

    [Fact]
    public void ResearchLayerHasNoRngGenerationOrSegmentationDependency()
    {
        var methods = typeof(ExactRecurrenceFailureResearch).GetMethods(BindingFlags.Public | BindingFlags.Static);
        Assert.DoesNotContain(methods.SelectMany(x => x.GetParameters()),
            x => typeof(IRandomSource).IsAssignableFrom(x.ParameterType));
        Assert.DoesNotContain(methods.Select(x => x.ReturnType), x => x == typeof(AddNotesResult));
        Assert.DoesNotContain(methods.Select(x => x.Name), x => x.Contains("Boundary", StringComparison.Ordinal));
    }

    private static ManiaChart Chart(int keys, IReadOnlyList<ManiaObject> originals, IReadOnlyList<ManiaObject> added) =>
        new() { KeyCount = keys, Lines = [], OriginalObjects = originals, AddedObjects = added,
            TimingPoints = [new TimingPoint(0, 500)] };

    private static OriginalTemporalEventSeries Series(params string[] identities) =>
        SeriesWithTimes(Enumerable.Range(0, identities.Length).Select(i => i * 100).ToArray(), identities);

    private static OriginalTemporalEventSeries SeriesWithTimes(int[] times, params string[] identities)
    {
        var events = identities.Select((identity, index) => Event(identity, index, times[index],
            index == 0 ? null : times[index - 1])).ToImmutableArray();
        return new(AdaptiveContextResearch.ResearchSchemaVersion, "synthetic-chart", 4, identities.Length,
            events, 0, 0);
    }

    private static OriginalTemporalEvent Event(string identity, int index, int time, int? previousTime)
    {
        var token = new StructuralEventToken(identity, [index % 4], [], [], [], 1, 0, false);
        return new($"E{index:D8}", "synthetic-chart", time, time / 500m,
            previousTime is null ? null : time - previousTime.Value,
            previousTime is null ? null : (time - previousTime.Value) / 500m,
            [new OriginalObservationId(index)], [], token);
    }

    private static OriginalTemporalEvent WithToken(OriginalTemporalEvent source, string identity,
        ImmutableArray<int> taps, ImmutableArray<int> lns, ImmutableArray<int> held)
    {
        var token = new StructuralEventToken(identity, taps, lns, [], held, taps.Length + lns.Length, 0,
            !taps.IsEmpty && !lns.IsEmpty);
        return source with { Token = token };
    }

    private sealed class RecordingRandom(int seed) : IRandomSource
    {
        private readonly Random random = new(seed);
        public List<string> Transcript { get; } = [];
        public double NextDouble() { var value = random.NextDouble(); Transcript.Add($"D:{value:R}"); return value; }
        public int Next(int maxExclusive) { var value = random.Next(maxExclusive); Transcript.Add($"I:{maxExclusive}:{value}"); return value; }
    }
}
