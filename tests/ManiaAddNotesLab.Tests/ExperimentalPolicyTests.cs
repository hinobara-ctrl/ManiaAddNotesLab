using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class ExperimentalPolicyTests
{
    [Fact]
    public void SparsePassageKeepsFullContextualChance()
    {
        var chart = Chart(7, [ManiaObject.Tap(0, 0), ManiaObject.Tap(1, 2000)]);
        var density = new AddNotesEngine().AnalyzeDensity(chart, new AddNotesOptions(), 0);
        Assert.Equal(1, density.ContextualFactor);
    }

    [Fact]
    public void BriefChordBurstGetsStrongestContextualReduction()
    {
        var chart = Chart(7, Enumerable.Range(0, 5).Select(lane => ManiaObject.Tap(lane, 0)).ToArray());
        var density = new AddNotesEngine().AnalyzeDensity(chart, new AddNotesOptions(), 0);
        Assert.Equal(.45, density.ContextualFactor, 6);
        Assert.Equal(1, density.ElevatedSpanBeats);
    }

    [Fact]
    public void SustainedRiceIsNotTreatedAsAnIsolatedBurst()
    {
        var notes = Enumerable.Range(-16, 33).Select(i => ManiaObject.Tap(Math.Abs(i) % 7, i * 125)).ToArray();
        var density = new AddNotesEngine().AnalyzeDensity(Chart(7, notes), new AddNotesOptions(), 0);
        Assert.Equal(1, density.ContextualFactor);
        Assert.InRange(density.DensityRatio, .9, 1.1);
    }

    [Fact]
    public void TwoAndThreeBeatBurstsUseProgressiveFactors()
    {
        static ManiaObject[] Burst(int beats) => Enumerable.Range(0, beats)
            .SelectMany(beat => Enumerable.Range(0, 4).Select(lane => ManiaObject.Tap(lane, beat * 500))).ToArray();
        var engine = new AddNotesEngine();
        Assert.Equal(.60, engine.AnalyzeDensity(Chart(7, Burst(2)), new AddNotesOptions(), 0).ContextualFactor, 6);
        Assert.Equal(.80, engine.AnalyzeDensity(Chart(7, Burst(3)), new AddNotesOptions(), 500).ContextualFactor, 6);
    }

    [Fact]
    public void DenseSectionBoundaryCanDiscoverTheWholeSpanForwardAndBackward()
    {
        var objects = Enumerable.Range(0, 5).SelectMany(beat => Enumerable.Range(0, 4)
            .Select(lane => ManiaObject.Tap(lane, beat * 500))).ToArray();
        var engine = new AddNotesEngine();
        var start = engine.AnalyzeDensity(Chart(7, objects), new(), 0);
        var end = engine.AnalyzeDensity(Chart(7, objects), new(), 2000);
        Assert.Equal(1, start.ContextualFactor);
        Assert.Equal(1, end.ContextualFactor);
        Assert.True(start.ElevatedSpanBeats >= 4);
        Assert.True(end.ElevatedSpanBeats >= 4);
    }

    [Fact]
    public void ValleyNeverRaisesChanceAboveBase()
    {
        var surrounding = Enumerable.Range(-4, 9).Where(i => i != 0)
            .SelectMany(beat => Enumerable.Range(0, 4).Select(lane => ManiaObject.Tap(lane, beat * 500)))
            .Append(ManiaObject.Tap(0, 0)).ToArray();
        var result = Apply(Chart(7, surrounding), new AddNotesOptions
        {
            Chance = .37, StartMs = 0, EndMs = 0, DensityDecayPerColumn = 1
        });
        Assert.Equal(.37, result.Statistics.MeanEffectiveChance, 6);
    }

    [Fact]
    public void TapLnAndMixedHeadsShareTheSameDensityDefinition()
    {
        var taps = Chart(7, Enumerable.Range(0, 4).Select(i => ManiaObject.Tap(i, 0)).ToArray());
        var lns = Chart(7, Enumerable.Range(0, 4).Select(i => ManiaObject.Ln(i, 0, 500)).ToArray());
        var mixed = Chart(7, [ManiaObject.Tap(0, 0), ManiaObject.Ln(1, 0, 500), ManiaObject.Tap(2, 0), ManiaObject.Ln(3, 0, 1000)]);
        var engine = new AddNotesEngine();
        var options = new AddNotesOptions();
        Assert.Equal(engine.AnalyzeDensity(taps, options, 0), engine.AnalyzeDensity(lns, options, 0));
        Assert.Equal(engine.AnalyzeDensity(taps, options, 0), engine.AnalyzeDensity(mixed, options, 0));
    }

    [Fact]
    public void AddedObjectsDoNotTeachDensityOrLaneGap()
    {
        var originals = new[] { ManiaObject.Tap(0, 0), ManiaObject.Tap(0, 250), ManiaObject.Tap(0, 500) };
        var baseChart = Chart(4, originals);
        var withAdded = baseChart.WithAdded([ManiaObject.Tap(1, 0, true), ManiaObject.Tap(1, 10, true)]);
        var engine = new AddNotesEngine();
        Assert.Equal(engine.AnalyzeDensity(baseChart, new(), 0), engine.AnalyzeDensity(withAdded, new(), 0));
        Assert.Equal(engine.ResolveLocalLaneGap(baseChart, new(), 250), engine.ResolveLocalLaneGap(withAdded, new(), 250));
    }

    [Fact]
    public void LocalGapRequiresRepeatedSupportAndIgnoresOutlier()
    {
        var chart = Chart(4, [
            ManiaObject.Tap(0, 0), ManiaObject.Tap(0, 125), ManiaObject.Tap(0, 250),
            ManiaObject.Tap(1, 0), ManiaObject.Tap(1, 375)]);
        var decision = new AddNotesEngine().ResolveLocalLaneGap(chart, new AddNotesOptions
        {
            LaneGapMinimumSupport = 2, FallbackMinimumLaneGapBeats = .5
        }, 125);
        Assert.True(decision.IsLocal);
        Assert.Equal(.25, decision.GapBeats, 6);
        Assert.Equal(2, decision.EvidenceCount);
    }

    [Fact]
    public void UnsupportedLocalGapUsesFallback()
    {
        var chart = Chart(4, [ManiaObject.Tap(0, 0), ManiaObject.Tap(0, 125)]);
        var decision = new AddNotesEngine().ResolveLocalLaneGap(chart, new AddNotesOptions
        {
            LaneGapMinimumSupport = 2, FallbackMinimumLaneGapBeats = .125
        }, 0);
        Assert.False(decision.IsLocal);
        Assert.Equal(.125, decision.GapBeats, 6);
    }

    [Fact]
    public void ShortestRepeatedEighthGapWinsOverRepeatedQuarterGap()
    {
        var chart = Chart(4, [
            ManiaObject.Tap(0, 0), ManiaObject.Tap(0, 200), ManiaObject.Tap(0, 400),
            ManiaObject.Tap(1, 0), ManiaObject.Tap(1, 100), ManiaObject.Tap(1, 200)], beatLength: 800);
        var gap = new AddNotesEngine().ResolveLocalLaneGap(chart, new(), 200);
        Assert.True(gap.IsLocal);
        Assert.Equal(.125, gap.GapBeats, 6);
    }

    [Fact]
    public void SingleSixtyFourthOutlierDoesNotOverrideSupportedQuarterGap()
    {
        var chart = Chart(4, [
            ManiaObject.Tap(0, 0), ManiaObject.Tap(0, 160), ManiaObject.Tap(0, 320), ManiaObject.Tap(0, 480),
            ManiaObject.Tap(1, 0), ManiaObject.Tap(1, 10)], beatLength: 640);
        var gap = new AddNotesEngine().ResolveLocalLaneGap(chart, new(), 160);
        Assert.True(gap.IsLocal);
        Assert.Equal(.25, gap.GapBeats, 6);
    }

    [Theory]
    [InlineData(107)] // 1/5 beat from a 7 ms offset
    [InlineData(57)]  // 1/10 beat from a 7 ms offset
    public void MapRelativeVocabularyPreservesNonStandardExactAnchors(int release)
    {
        var source = ManiaObject.Ln(0, 7, release);
        var timeline = new BeatTimeline([new TimingPoint(7, 500)]);
        var engine = new AddNotesEngine();
        var context = engine.BuildLocalLnContext(source, [source], timeline, 4);
        Assert.Contains(engine.BuildReleaseCandidates(source, context, timeline, 4), c => c.EndTime == release);
    }

    [Fact]
    public void ExactReleaseAnchorSurvivesBpmChange()
    {
        var source = ManiaObject.Ln(0, 7, 807);
        var timeline = new BeatTimeline([new TimingPoint(7, 500), new TimingPoint(507, 300)]);
        var context = new AddNotesEngine().BuildLocalLnContext(source, [source], timeline, 4);
        Assert.Contains(new AddNotesEngine().BuildReleaseCandidates(source, context, timeline, 4), c => c.EndTime == 807);
    }

    [Fact]
    public void DecimalTimelineDoesNotAccumulateRoundTripDrift()
    {
        var timeline = new BeatTimeline([new TimingPoint(7, 500)]);
        for (var i = 0; i <= 1000; i++)
        {
            var beat = i / 10m;
            var time = timeline.ToTimeMilliseconds(beat);
            Assert.Equal(beat, timeline.ToBeatDecimal(time));
        }
    }

    [Fact]
    public void LegacyStandardSnapRemainsAvailableAsAnAbToggle()
    {
        var chart = Chart(2, [ManiaObject.Ln(0, 0, 117)]);
        var mapRelative = Apply(chart, new AddNotesOptions { Chance = 1, ContextualDensityNormalizationEnabled = false });
        var standard = Apply(chart, new AddNotesOptions { Chance = 1, ContextualDensityNormalizationEnabled = false,
            MapRelativeSnapEnabled = false });
        Assert.Equal(117, Assert.Single(mapRelative.ModifiedChart.AddedObjects).EndTime);
        Assert.Equal(125, Assert.Single(standard.ModifiedChart.AddedObjects).EndTime);
    }

    [Fact]
    public void ShortOrIsolatedLnProducesNoInteriorOpportunity()
    {
        var shortChart = Chart(7, [ManiaObject.Ln(0, 0, 1500), ManiaObject.Ln(1, 0, 500), ManiaObject.Ln(2, 0, 500)]);
        var isolated = Chart(7, [ManiaObject.Ln(0, 0, 5000)]);
        var options = InteriorOptions();
        Assert.Equal(0, Apply(shortChart, options).Statistics.InteriorLnOpportunities);
        Assert.Equal(0, Apply(isolated, options).Statistics.InteriorLnOpportunities);
    }

    [Fact]
    public void EligibleLongLnUsesOnlyOriginalInteriorAnchorsAndRespectsMaximum()
    {
        var chart = Chart(7, [
            ManiaObject.Ln(0, 0, 5000), ManiaObject.Ln(1, 0, 1000), ManiaObject.Ln(2, 250, 1250),
            ManiaObject.Ln(5, 500, 1500), ManiaObject.Tap(3, 750), ManiaObject.Tap(4, 1500)]);
        var result = Apply(chart, InteriorOptions() with { MaxInteriorOpportunitiesPerSource = 2 });
        Assert.InRange(result.Statistics.InteriorLnOpportunities, 1, 2);
        var originalAnchors = chart.OriginalObjects.SelectMany(o => o.EndTime is int end ? new[] { o.StartTime, end } : [o.StartTime]).ToHashSet();
        Assert.All(result.ModifiedChart.AddedObjects.Where(o => o.Origin == AddedObjectOrigin.LnInteriorOpportunity),
            o => Assert.Contains(o.StartTime, originalAnchors));
    }

    [Fact]
    public void OriginalOnlyInteriorContextExcludesParentAndItsVirtualTail()
    {
        var parent = ManiaObject.Ln(0, 0, 4000);
        var originals = new[] { parent, ManiaObject.Ln(1, 500, 1000), ManiaObject.Ln(2, 500, 1500),
            ManiaObject.Ln(3, 0, 500) };
        var timeline = new BeatTimeline([new TimingPoint(0, 500)]);
        var engine = new AddNotesEngine();
        var context = engine.BuildOriginalInteriorLnContext(parent, 1000, originals, timeline, 4);
        var virtualSource = ManiaObject.Ln(0, 1000, 4000);
        var candidates = engine.BuildReleaseCandidates(virtualSource, context, timeline, 4,
            applySourceAffinity: false);

        Assert.DoesNotContain(context.Observations, o => o.Object == parent);
        Assert.DoesNotContain(context.Observations, o => o.Object == virtualSource);
        Assert.DoesNotContain(candidates, c => c.EndTime == parent.EndTime);
        Assert.Equal(3, candidates.Sum(c => c.Evidence.DurationVotes));
        Assert.Equal(1, candidates.Sum(c => c.Evidence.ExactReleaseVotes));
        Assert.All(candidates, c => Assert.Equal(1, c.Evidence.SourceAffinity));
    }

    [Fact]
    public void InteriorContextFallsBackToParentHeadUsingOnlyOriginalPeers()
    {
        var parent = ManiaObject.Ln(0, 0, 5000);
        var peers = new[] { ManiaObject.Ln(1, 0, 500), ManiaObject.Ln(2, 0, 750), ManiaObject.Ln(3, 0, 1000) };
        var originals = new[] { parent }.Concat(peers).ToArray();
        var timeline = new BeatTimeline([new TimingPoint(0, 500)]);
        var context = new AddNotesEngine().BuildOriginalInteriorLnContext(parent, 3000, originals, timeline, 1, 3);
        Assert.Equal(peers, context.Observations.Select(o => o.Object));
        Assert.All(context.Observations, o => Assert.False(o.Object.IsSynthetic));
    }

    [Fact]
    public void InteriorVocabularyPreservesObservedOneFifthAndOneEighthDurations()
    {
        var timeline = new BeatTimeline([new TimingPoint(0, 800)]);
        var source = ManiaObject.Ln(0, 0, 800);
        var originals = new[] { source, ManiaObject.Ln(1, 0, 160), ManiaObject.Ln(2, 0, 100) };
        var context = new AddNotesEngine().BuildLocalLnContext(source, originals, timeline, 4);
        var candidates = new AddNotesEngine().BuildReleaseCandidates(source, context, timeline, 4,
            applySourceAffinity: false);
        Assert.Contains(candidates, c => c.EndTime == 160);
        Assert.Contains(candidates, c => c.EndTime == 100);
    }

    [Fact]
    public void AnchorSupportedEligibilityDoesNotUseLegacyRelativeLengthAsHardGate()
    {
        var chart = Chart(7, [ManiaObject.Ln(0, 0, 2000), ManiaObject.Ln(1, 0, 1500),
            ManiaObject.Ln(2, 250, 1750), ManiaObject.Ln(3, 500, 2000)]);
        var modern = Apply(chart, InteriorOptions() with { InteriorEligibilityMode = InteriorEligibilityMode.AnchorSupported });
        var legacy = Apply(chart, InteriorOptions() with { InteriorEligibilityMode = InteriorEligibilityMode.Legacy });

        Assert.True(modern.Statistics.InteriorLnOpportunities > 0);
        Assert.Equal(0, legacy.Statistics.InteriorLnOpportunities);
        Assert.True(legacy.Statistics.InteriorRejectedRelativeLengthLegacy > 0);
    }

    [Fact]
    public void InteriorOpportunityRequiresSupportedOriginalAnchors()
    {
        var chart = Chart(7, [ManiaObject.Ln(0, 0, 5000), ManiaObject.Ln(1, -1000, 6000),
            ManiaObject.Ln(2, -1000, 6000), ManiaObject.Ln(3, -1000, 6000)]);
        var result = Apply(chart, InteriorOptions() with { InteriorMinimumSupportedAnchors = 3 });
        Assert.Equal(0, result.Statistics.InteriorLnOpportunities);
        Assert.True(result.Statistics.InteriorRejectedNoSupportedAnchors > 0);
    }

    [Fact]
    public void HeldLnTailsDoNotChangeSimultaneousHeadPenalty()
    {
        var heads = new[] { ManiaObject.Tap(4, 0), ManiaObject.Tap(5, 0) };
        var tails = Enumerable.Range(0, 4).Select(lane => ManiaObject.Ln(lane, -1000, 1000));
        var sparse = Chart(7, heads);
        var held = Chart(7, tails.Concat(heads).ToArray());
        var headOptions = new AddNotesOptions { Chance = 1, StartMs = 0, EndMs = 0,
            ContextualDensityNormalizationEnabled = false, VerticalDensityMode = VerticalDensityMode.SimultaneousHeads };
        var legacyOptions = headOptions with { VerticalDensityMode = VerticalDensityMode.OccupiedColumnsLegacy };

        var sparseResult = Apply(sparse, headOptions);
        var heldResult = Apply(held, headOptions);
        var legacyResult = Apply(held, legacyOptions);
        Assert.Equal(sparseResult.Statistics.MeanEffectiveChance, heldResult.Statistics.MeanEffectiveChance, 6);
        Assert.True(heldResult.Statistics.HeldLnColumnsMean >= 4);
        Assert.True(heldResult.Statistics.MeanEffectiveChance > legacyResult.Statistics.MeanEffectiveChance);
        Assert.True(heldResult.Statistics.FailedPlacements > sparseResult.Statistics.FailedPlacements);
    }

    [Fact]
    public void InteriorCandidateBandsAndFailureReasonsAreObservable()
    {
        var chart = Chart(4, [ManiaObject.Ln(0, 0, 5000), ManiaObject.Ln(1, 0, 1000),
            ManiaObject.Ln(2, 250, 1500), ManiaObject.Ln(3, 500, 2000)]);
        var result = Apply(chart, InteriorOptions());
        Assert.True(result.Statistics.InteriorCandidatesShort + result.Statistics.InteriorCandidatesMedium
            + result.Statistics.InteriorCandidatesLong > 0);
        Assert.True(result.Statistics.LnCandidatesImpossible >= result.Statistics.InteriorCandidatesImpossible);
        Assert.True(result.Statistics.LnShapeRejectedGap + result.Statistics.LnShapeRejectedOverlap > 0);
    }

    [Fact]
    public void AddedObjectsCannotCreateInteriorAnchors()
    {
        var baseChart = Chart(7, [
            ManiaObject.Ln(0, 0, 5000), ManiaObject.Ln(1, 0, 1000), ManiaObject.Ln(2, 0, 1000)]);
        var chart = baseChart.WithAdded([ManiaObject.Tap(3, 750, true)]);
        var result = Apply(chart, InteriorOptions());
        Assert.DoesNotContain(result.ModifiedChart.AddedObjects,
            o => o.Origin == AddedObjectOrigin.LnInteriorOpportunity && o.StartTime == 750);
    }

    [Fact]
    public void InteriorOpportunityReceivesContextFactorAndGeometryMayRejectIt()
    {
        var objects = new List<ManiaObject>
        {
            ManiaObject.Ln(0, 0, 5000), ManiaObject.Ln(1, 0, 1000), ManiaObject.Ln(2, 0, 1000),
            ManiaObject.Ln(3, 0, 1000), ManiaObject.Ln(4, 0, 1000)
        };
        objects.AddRange(Enumerable.Range(0, 5).Select(lane => ManiaObject.Tap(lane, 500)));
        var result = Apply(Chart(5, objects.ToArray()), InteriorOptions() with
        {
            Trace = true, ContextualDensityNormalizationEnabled = true
        });
        Assert.True(result.Statistics.SuccessfulInteriorRolls > 0);
        Assert.True(result.Statistics.ContextualDensityAdjustedOpportunities > 0);
        Assert.DoesNotContain(result.ModifiedChart.AddedObjects, o => o.Origin == AddedObjectOrigin.LnInteriorOpportunity);
        Assert.Contains("OPPORTUNITY kind=LnInterior", result.Trace);
    }

    [Fact]
    public void GeometryIndexExaminesOnlyNeighboursOnLargeChart()
    {
        var objects = Enumerable.Range(0, 1000).Select(i => ManiaObject.Tap(i % 7, i * 125)).ToArray();
        var result = Apply(Chart(7, objects), new AddNotesOptions { Chance = 0,
            ContextualDensityNormalizationEnabled = false });
        Assert.Equal(7000, result.Statistics.GeometryChecks);
        Assert.True(result.Statistics.GeometryObjectsExamined < 14000);
        Assert.InRange(result.Statistics.BeatConversions, 1000, 1010);
    }

    [Fact]
    public void SameSeedKeepsOutputsDecisionMetricsAndDecisionTraceDeterministic()
    {
        var chart = Chart(7, [ManiaObject.Tap(0, 0), ManiaObject.Ln(1, 500, 1500),
            ManiaObject.Ln(2, 750, 1750), ManiaObject.Tap(3, 1000)]);
        var options = new AddNotesOptions { Chance = .65, Trace = true, InteriorLnOpportunitiesEnabled = true };
        var a = new AddNotesEngine().Apply(chart, options, new SeededRandom(991));
        var b = new AddNotesEngine().Apply(chart, options, new SeededRandom(991));
        Assert.Equal(a.ModifiedChart.AddedObjects, b.ModifiedChart.AddedObjects);
        Assert.Equal(DecisionMetrics(a.Statistics), DecisionMetrics(b.Statistics));
        Assert.Equal(DecisionTrace(a.Trace!), DecisionTrace(b.Trace!));
    }

    [Fact]
    public void GeneratedObjectsRemainPairwiseNonOverlappingPerLane()
    {
        var chart = Chart(7, Enumerable.Range(0, 40).Select(i => i % 3 == 0
            ? ManiaObject.Ln(i % 7, i * 125, i * 125 + 500)
            : ManiaObject.Tap(i % 7, i * 125)).ToArray());
        var result = new AddNotesEngine().Apply(chart, new AddNotesOptions
        {
            Chance = 1, ContextualDensityNormalizationEnabled = false
        }, new SeededRandom(23));
        foreach (var lane in result.ModifiedChart.AllObjects.GroupBy(o => o.Lane))
        {
            var sorted = lane.OrderBy(o => o.StartTime).ThenBy(o => o.EndTime ?? o.StartTime).ToArray();
            for (var i = 1; i < sorted.Length; i++)
                Assert.True((sorted[i - 1].EndTime ?? sorted[i - 1].StartTime) < sorted[i].StartTime);
        }
    }

    [Fact]
    public void ApplyingExperimentalModesPreservesEveryOriginalObject()
    {
        var chart = Chart(7, [ManiaObject.Ln(0, 0, 5000), ManiaObject.Ln(1, 0, 1000),
            ManiaObject.Ln(2, 250, 1500), ManiaObject.Ln(3, 500, 2000), ManiaObject.Tap(4, 750)]);
        var result = Apply(chart, InteriorOptions());
        Assert.Equal(chart.OriginalObjects, result.ModifiedChart.OriginalObjects);
    }

    private static AddNotesOptions InteriorOptions() => new()
    {
        Chance = 1, ContextualDensityNormalizationEnabled = false, InteriorLnOpportunitiesEnabled = true,
        MaxInteriorOpportunitiesPerSource = 1
    };

    private static AddNotesResult Apply(ManiaChart chart, AddNotesOptions options) =>
        new AddNotesEngine().Apply(chart, options, new ZeroRandom());

    private static object DecisionMetrics(AddNotesStatistics s) => new
    {
        s.OutputObjects, s.AddedTaps, s.AddedLongNotes, s.TotalOpportunities, s.SuccessfulProbabilityRolls,
        s.FailedPlacements, s.LnCandidatesBuilt, s.LnCandidatesImpossible, s.GeometryChecks,
        s.GeometryObjectsExamined, s.BeatConversions, s.MeanEffectiveChance
    };

    private static string DecisionTrace(string trace) => trace.Split("PERFORMANCE", 2)[0];

    private static ManiaChart Chart(int keys, ManiaObject[] objects, decimal beatLength = 500) => new()
    {
        KeyCount = keys,
        Lines = [],
        OriginalObjects = objects.Select((o, i) => o with { Sequence = i }).ToArray(),
        TimingPoints = [new TimingPoint(0, beatLength)]
    };

    private sealed class ZeroRandom : IRandomSource
    {
        public double NextDouble() => 0;
        public int Next(int maxExclusive) => 0;
    }
}

file static class ChartTestExtensions
{
    public static ManiaChart WithAdded(this ManiaChart chart, IReadOnlyList<ManiaObject> added) => new()
    {
        KeyCount = chart.KeyCount, Lines = chart.Lines, OriginalObjects = chart.OriginalObjects,
        TimingPoints = chart.TimingPoints, AddedObjects = added
    };
}
