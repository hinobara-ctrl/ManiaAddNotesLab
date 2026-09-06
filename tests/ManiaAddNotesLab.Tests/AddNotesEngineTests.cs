using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class AddNotesEngineTests
{
    [Fact]
    public void ZeroChanceAddsNothing()
    {
        var result = Apply(Chart(4, ManiaObject.Tap(0, 0), ManiaObject.Ln(1, 500, 1000)), chance: 0);
        Assert.Empty(result.ModifiedChart.AddedObjects);
        Assert.Equal(2, result.Statistics.TotalOpportunities);
        Assert.Equal(0, result.Statistics.SuccessfulProbabilityRolls);
    }

    [Fact]
    public void FullChanceRollsOncePerOriginalAndNeverRecurses()
    {
        var result = Apply(Chart(4, ManiaObject.Tap(0, 0)), chance: 1);
        Assert.Single(result.ModifiedChart.AddedObjects);
        Assert.Equal(1, result.Statistics.TotalOpportunities);
        Assert.Equal(1, result.Statistics.SuccessfulProbabilityRolls);
        Assert.Equal(0, result.Statistics.DensityAdjustedOpportunities);
        Assert.Equal(1, result.Statistics.MeanEffectiveChance);
    }

    [Fact]
    public void DenseChordReducesEffectiveChanceForEveryAdditionalOccupiedColumn()
    {
        var chord = Enumerable.Range(0, 5).Select(lane => ManiaObject.Tap(lane, 0)).ToArray();
        var options = new AddNotesOptions { Chance = 1, DensityGraceColumns = 2, DensityDecayPerColumn = .5,
            ContextualDensityNormalizationEnabled = false,
            VerticalDensityScaleMode = VerticalDensityScaleMode.AbsoluteLegacy };
        var result = new AddNotesEngine().Apply(Chart(7, chord), options,
            new SequenceRandom(Enumerable.Repeat(.2, 5), []));

        Assert.Empty(result.ModifiedChart.AddedObjects);
        Assert.Equal(5, result.Statistics.DensityAdjustedOpportunities);
        Assert.Equal(.125, result.Statistics.MeanEffectiveChance, 6);
    }

    [Fact]
    public void TapUsesSameTimestampAndAvoidsOccupiedLaneAndActiveLn()
    {
        var chart = Chart(4, ManiaObject.Ln(0, 0, 1000), ManiaObject.Tap(1, 500), ManiaObject.Tap(2, 500));
        var result = Apply(chart, chance: 1, start: 500, end: 500);
        var added = Assert.Single(result.ModifiedChart.AddedObjects);
        Assert.Equal(500, added.StartTime);
        Assert.Equal(3, added.Lane);
        Assert.Equal(ManiaObjectType.Tap, added.Type);
    }

    [Fact]
    public void ImpossibleShapesAreFilteredBeforeWeightedSelection()
    {
        var chart = Chart(3,
            ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, -500, 0), ManiaObject.Tap(2, 750));
        var result = new AddNotesEngine().Apply(chart, new AddNotesOptions { Chance = 1, StartMs = 0, EndMs = 0,
            ContextualDensityNormalizationEnabled = false,
            VerticalDensityScaleMode = VerticalDensityScaleMode.AbsoluteLegacy },
            new SequenceRandom([.99, 0], [0]));
        var added = Assert.Single(result.ModifiedChart.AddedObjects);
        Assert.Equal(500, added.EndTime);
        Assert.Equal(2, added.Lane);
        Assert.Equal(1, result.Statistics.LnCandidateRetries);
        Assert.Equal(result.Statistics.LnCandidatesImpossible, result.Statistics.LnCandidateRetries);
    }

    [Fact]
    public void LnSkipsRatherThanConvertingWhenNoCandidateFits()
    {
        var chart = Chart(2, ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, -500, 1000));
        var result = Apply(chart, 1, 0, 0);
        Assert.Empty(result.ModifiedChart.AddedObjects);
        Assert.Equal(1, result.Statistics.LnSkipsDueToGeometry);
        Assert.Equal(1, result.Statistics.FailedPlacements);
    }

    [Fact]
    public void ContextUsesOnlyPassedOriginalsAndFusesDurationWithRelease()
    {
        var source = ManiaObject.Ln(0, 0, 500);
        var originalPeer = ManiaObject.Ln(1, 0, 500);
        var synthetic = ManiaObject.Ln(2, 0, 250, true);
        var engine = new AddNotesEngine();
        var timeline = Timeline();
        var context = engine.BuildLocalLnContext(source, [source, originalPeer], timeline, 4);
        var candidates = engine.BuildReleaseCandidates(source, context, timeline, 4);
        var fused = Assert.Single(candidates);

        Assert.Equal(2, context.Observations.Count);
        Assert.DoesNotContain(context.Observations, o => o.Object == synthetic);
        Assert.Equal(2, fused.Evidence.DurationVotes);
        Assert.Equal(2, fused.Evidence.ReleaseVotes);
    }

    [Fact]
    public void SyntheticSourceIsNeverInsertedIntoPublicOriginalContext()
    {
        var syntheticSource = ManiaObject.Ln(3, 0, 2000, true);
        var originals = new[] { ManiaObject.Ln(0, 0, 500), ManiaObject.Ln(1, 0, 750),
            ManiaObject.Ln(2, 0, 1000) };
        var context = new AddNotesEngine().BuildLocalLnContext(syntheticSource, originals, Timeline(), 4);
        Assert.Equal(originals, context.Observations.Select(o => o.Object));
        Assert.DoesNotContain(context.Observations, o => o.Object.IsSynthetic);
    }

    [Fact]
    public void FrequentDurationAndReleaseIncreaseEvidenceAndSourceAffinityIsModerate()
    {
        var source = ManiaObject.Ln(0, 0, 1000);
        var originals = new[] { source, ManiaObject.Ln(1, 0, 500), ManiaObject.Ln(2, 500, 1000) };
        var engine = new AddNotesEngine();
        var context = engine.BuildLocalLnContext(source, originals, Timeline(), 4);
        var candidates = engine.BuildReleaseCandidates(source, context, Timeline(), 4);

        var at1000 = Assert.Single(candidates, c => c.EndTime == 1000);
        Assert.True(at1000.Evidence.DurationVotes >= 1);
        Assert.True(at1000.Evidence.ReleaseVotes >= 2);
        Assert.Equal(AddNotesEngine.SourceAffinityMultiplier, at1000.Evidence.SourceAffinity);
        Assert.Contains(candidates, c => c.EndTime == 500);
    }

    [Fact]
    public void NewLnIsNeverShorterThanLocalMinimum()
    {
        var source = ManiaObject.Ln(0, 0, 1000);
        var engine = new AddNotesEngine();
        var context = engine.BuildLocalLnContext(source, [source, ManiaObject.Ln(1, 250, 750)], Timeline(), 4);
        var candidates = engine.BuildReleaseCandidates(source, context, Timeline(), 4);
        Assert.All(candidates, c => Assert.True(c.EndTime >= 500));
    }

    [Fact]
    public void ExactMapReleaseAnchorIsPreserved()
    {
        var source = ManiaObject.Ln(0, 0, 117);
        var engine = new AddNotesEngine();
        var timeline = Timeline();
        var context = engine.BuildLocalLnContext(source, [source], timeline, 4);
        var candidates = engine.BuildReleaseCandidates(source, context, timeline, 4);

        var candidate = Assert.Single(candidates);
        Assert.Equal(117, candidate.EndTime);
        Assert.Equal(1, candidate.Evidence.ExactReleaseVotes);
    }

    [Fact]
    public void LnPlacementKeepsConfiguredGapFromNeighbouringObjectsInSameLane()
    {
        var chart = Chart(3, ManiaObject.Ln(1, 0, 950), ManiaObject.Ln(0, 1000, 1500));
        var result = new AddNotesEngine().Apply(chart, new AddNotesOptions
        {
            Chance = 1,
            StartMs = 1000,
            EndMs = 1000,
            UseLocalLaneGap = false,
            FallbackMinimumLaneGapBeats = .125
        }, new SequenceRandom([0], [0]));

        var added = Assert.Single(result.ModifiedChart.AddedObjects);
        Assert.Equal(2, added.Lane);
    }

    [Fact]
    public void SyntheticObjectsAffectFutureCollisionGeometry()
    {
        var occupied = new[] { ManiaObject.Ln(1, 0, 1000, true) };
        Assert.DoesNotContain(1, AddNotesEngine.FindLegalLanes(4, 500, null, occupied));
        Assert.DoesNotContain(1, AddNotesEngine.FindLegalLanes(4, 500, 1500, occupied));
    }

    [Fact]
    public void SelectedRangeFiltersHeadsButExternalLnStillBlocks()
    {
        var chart = Chart(2, ManiaObject.Ln(0, 0, 2000), ManiaObject.Tap(1, 1000));
        var result = Apply(chart, 1, 1000, 1000);
        Assert.Equal(1, result.Statistics.TotalOpportunities);
        Assert.Empty(result.ModifiedChart.AddedObjects);
    }

    [Fact]
    public void SameInputConfigAndSeedProduceSameBeatmapAndMetrics()
    {
        var chart = Chart(4, ManiaObject.Tap(0, 0), ManiaObject.Tap(1, 500), ManiaObject.Ln(2, 1000, 1500));
        var a = Apply(chart, .7, rng: new SeededRandom(12345));
        var b = Apply(chart, .7, rng: new SeededRandom(12345));
        Assert.Equal(a.ModifiedChart.AddedObjects, b.ModifiedChart.AddedObjects);
        Assert.Equal(a.Statistics.PlacedObjects, b.Statistics.PlacedObjects);
    }

    private static AddNotesResult Apply(ManiaChart chart, double chance, int? start = null, int? end = null, IRandomSource? rng = null) =>
        new AddNotesEngine().Apply(chart, new AddNotesOptions { Chance = chance, StartMs = start, EndMs = end,
            ContextualDensityNormalizationEnabled = false }, rng ?? new SeededRandom(7));

    private static BeatTimeline Timeline() => new([new TimingPoint(0, 500)]);

    private static ManiaChart Chart(int keys, params ManiaObject[] objects) => new()
    {
        KeyCount = keys,
        Lines = ["osu file format v14", "", "[General]", "Mode:3", "", "[Metadata]", "Version:Test", "BeatmapID:12", "", "[Difficulty]", $"CircleSize:{keys}", "", "[TimingPoints]", "0,500,4,2,0,100,1,0", "", "[HitObjects]"],
        OriginalObjects = objects.Select((o, i) => o with { Sequence = i, RawLine = Raw(o, keys) }).ToArray(),
        TimingPoints = [new TimingPoint(0, 500)]
    };

    private static string Raw(ManiaObject o, int keys)
    {
        var x = (int)Math.Floor((o.Lane + .5) * 512 / keys);
        return o.Type == ManiaObjectType.Tap ? $"{x},192,{o.StartTime},1,0,0:0:0:0:" : $"{x},192,{o.StartTime},128,0,{o.EndTime}:0:0:0:0:";
    }

    private sealed class SequenceRandom(IEnumerable<double> doubles, IEnumerable<int> ints) : IRandomSource
    {
        private readonly Queue<double> _doubles = new(doubles);
        private readonly Queue<int> _ints = new(ints);
        public double NextDouble() => _doubles.Count > 0 ? _doubles.Dequeue() : 0;
        public int Next(int maxExclusive) => _ints.Count > 0 ? Math.Clamp(_ints.Dequeue(), 0, maxExclusive - 1) : 0;
    }
}
