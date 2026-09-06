using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class KeymodeAndArticulationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(18)]
    public void ParserSupportsDocumentedKeymodeRange(int keys)
    {
        var chart = OsuBeatmap.Parse(BeatmapText(keys));
        Assert.Equal(keys, chart.KeyCount);
        Assert.All(chart.OriginalObjects, item => Assert.InRange(item.Lane, 0, keys - 1));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void RelativeVerticalDensityGivesFullChordsEquivalentPenalty(int keys)
    {
        var density = new AddNotesEngine().AnalyzeVerticalDensity(keys, keys, new AddNotesOptions
        {
            VerticalDensityScaleMode = VerticalDensityScaleMode.KeymodeRelative
        });

        Assert.Equal(5, density.EquivalentPenaltySteps, 10);
        Assert.Equal(Math.Pow(.65, 5), density.ChordFactor, 10);
    }

    [Fact]
    public void AbsoluteLegacyVerticalDensityRemainsAvailableForComparison()
    {
        var engine = new AddNotesEngine();
        var options = new AddNotesOptions { VerticalDensityScaleMode = VerticalDensityScaleMode.AbsoluteLegacy };
        Assert.Equal(2, engine.AnalyzeVerticalDensity(4, 4, options).EquivalentPenaltySteps);
        Assert.Equal(8, engine.AnalyzeVerticalDensity(10, 10, options).EquivalentPenaltySteps);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void RelativeContextTreatsEquivalentFullChordBurstTheSameAcrossKeys(int keys)
    {
        var objects = Enumerable.Range(0, keys).Select(lane => ManiaObject.Tap(lane, 0)).ToArray();
        var density = new AddNotesEngine().AnalyzeDensity(Chart(keys, objects), new AddNotesOptions
        {
            ContextualDensityScaleMode = ContextualDensityScaleMode.KeymodeRelative
        }, 0);

        Assert.Equal(.45, density.ContextualFactor, 10);
        Assert.Equal(1, density.ElevatedSpanBeats);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void SustainedRelativeDensityIsNotPenalizedAcrossKeys(int keys)
    {
        var notes = Enumerable.Range(-16, 33)
            .Select(i => ManiaObject.Tap(Math.Abs(i) % keys, i * 125)).ToArray();
        var density = new AddNotesEngine().AnalyzeDensity(Chart(keys, notes), new(), 0);
        Assert.Equal(1, density.ContextualFactor);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    public void HeldTailsDoNotChangeRelativeHeadChordFactorAcrossKeys(int keys)
    {
        var engine = new AddNotesEngine();
        var factor = engine.AnalyzeVerticalDensity(keys, 2, new AddNotesOptions
        {
            VerticalDensityMode = VerticalDensityMode.SimultaneousHeads,
            VerticalDensityScaleMode = VerticalDensityScaleMode.KeymodeRelative
        }).ChordFactor;
        var sparse = Chart(keys, [ManiaObject.Tap(0, 0), ManiaObject.Tap(1, 0)]);
        var held = Chart(keys, Enumerable.Range(2, keys - 2)
            .Select(lane => ManiaObject.Ln(lane, -1000, 1000))
            .Concat(sparse.OriginalObjects).ToArray());
        var options = BaseOptions() with { Chance = .5, DensityDecayPerColumn = .65, StartMs = 0, EndMs = 0 };
        var sparseResult = Apply(sparse, options);
        var heldResult = Apply(held, options);
        Assert.Equal(factor, sparseResult.Statistics.MeanChordFactorForTap, 10);
        Assert.Equal(factor, heldResult.Statistics.MeanChordFactorForTap, 10);
        Assert.True(AddNotesEngine.FindLegalLanes(keys, 0, null, held.OriginalObjects).Count
            < AddNotesEngine.FindLegalLanes(keys, 0, null, sparse.OriginalObjects).Count);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void RiceOpportunityAddsAtMostOneTapAndNeverFeedsBack(int keys)
    {
        var result = Apply(Chart(keys, Enumerable.Range(0, keys - 1)
            .Select(lane => ManiaObject.Tap(lane, 0)).ToArray()), BaseOptions() with
        {
            Chance = 1,
            StartMs = 0,
            EndMs = 0
        });

        Assert.Equal(keys - 1, result.Statistics.TapOpportunities);
        Assert.InRange(result.Statistics.TapPlaced, 0, keys - 1);
        Assert.All(result.ModifiedChart.AddedObjects, item =>
        {
            Assert.Equal(ManiaObjectType.Tap, item.Type);
            Assert.Equal(0, item.StartTime);
        });
    }

    [Theory]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    public void OneOriginalTapCreatesExactlyOneOpportunityAndOneSyntheticTap(int keys)
    {
        var result = Apply(Chart(keys, [ManiaObject.Tap(0, 125)]), BaseOptions() with { Chance = 1 });
        Assert.Equal(1, result.Statistics.TapOpportunities);
        var added = Assert.Single(result.ModifiedChart.AddedObjects);
        Assert.Equal(ManiaObjectType.Tap, added.Type);
        Assert.Equal(125, added.StartTime);
    }

    [Fact]
    public void TapLaneChoiceCanSelectEveryLegalLaneUniformlyByIndex()
    {
        var chart = Chart(4, [ManiaObject.Tap(0, 0)]);
        for (var index = 0; index < 3; index++)
        {
            var result = new AddNotesEngine().Apply(chart, BaseOptions() with { Chance = 1 },
                new ChoiceRandom(index));
            Assert.Equal(index + 1, Assert.Single(result.ModifiedChart.AddedObjects).Lane);
        }
    }

    [Theory]
    [InlineData(4, 0, 1)]
    [InlineData(5, 0, 1)]
    [InlineData(6, 0, 1)]
    [InlineData(7, 0, 1)]
    [InlineData(8, 0, 1)]
    [InlineData(9, 0, 1)]
    [InlineData(10, 0, 1)]
    [InlineData(4, 1, 1)]
    [InlineData(7, 1, 1)]
    [InlineData(10, 1, 1)]
    [InlineData(4, 2, 0)]
    [InlineData(7, 2, 0)]
    [InlineData(10, 2, 0)]
    public void ArticulationMatchesSaturationPolicyAcrossKeymodes(int keys, int nonHeldAtAnchor,
        int expectedArticulations)
    {
        var result = Apply(ArticulationFixture(keys, nonHeldAtAnchor), ArticulationOptions());

        var targetParentArticulations = result.ModifiedChart.ArticulationReplacements
            .Count(item => item.ParentOriginalLn.Lane == 0);
        Assert.Equal(expectedArticulations, targetParentArticulations);
        if (nonHeldAtAnchor > 1)
            Assert.True(result.Statistics.RejectedNotSaturated > 0);
        AssertValidLaneGeometry(result.ModifiedChart);
    }

    [Fact]
    public void ArticulationReplacesOneOriginalParentWithTwoSameLaneSegments()
    {
        var chart = ArticulationFixture(7, 0);
        var result = Apply(chart, ArticulationOptions());
        var replacement = Assert.Single(result.ModifiedChart.ArticulationReplacements,
            item => item.ParentOriginalLn.Lane == 0);

        Assert.Equal(0, replacement.ParentOriginalLn.Lane);
        Assert.Equal(replacement.ParentOriginalLn.Lane, replacement.LeftSegment.Lane);
        Assert.Equal(replacement.ParentOriginalLn.Lane, replacement.RightSegment.Lane);
        Assert.Equal(replacement.ParentOriginalLn.StartTime, replacement.LeftSegment.StartTime);
        Assert.Equal(replacement.ParentOriginalLn.EndTime, replacement.RightSegment.EndTime);
        Assert.True(replacement.LeftSegment.EndTime < replacement.RightSegment.StartTime);
        Assert.Equal(chart.OriginalObjects, result.ModifiedChart.OriginalObjects);
        Assert.DoesNotContain(result.ModifiedChart.AllObjects, item => !item.IsSynthetic
            && item.Sequence == replacement.ParentOriginalLn.Sequence);
        Assert.Equal(chart.OriginalObjects.Count + result.Statistics.PlacedObjects
            + result.Statistics.ArticulationPlaced,
            result.ModifiedChart.AllObjects.Count);
    }

    [Fact]
    public void ArticulationUsesMapRelativeOneFifthAndOneTenthRetriggerGaps()
    {
        var fifth = Apply(ArticulationFixture(7, 0, beatLength: 1000, gapBeats: .2m),
            ArticulationOptions());
        var tenth = Apply(ArticulationFixture(7, 0, beatLength: 1000, gapBeats: .1m),
            ArticulationOptions());

        var a = Assert.Single(fifth.ModifiedChart.ArticulationReplacements,
            item => item.ParentOriginalLn.Lane == 0);
        var b = Assert.Single(tenth.ModifiedChart.ArticulationReplacements,
            item => item.ParentOriginalLn.Lane == 0);
        Assert.Equal(200, a.RepressTime - a.ReleaseTime);
        Assert.Equal(100, b.RepressTime - b.ReleaseTime);
    }

    [Fact]
    public void ArticulationPreservesOffsetAndLocalBpmTiming()
    {
        var chart = ArticulationFixture(7, 0, beatLength: 500, gapBeats: .2m, offset: 7);
        var replacement = Assert.Single(Apply(chart, ArticulationOptions()).ModifiedChart.ArticulationReplacements,
            item => item.ParentOriginalLn.Lane == 0);
        Assert.Equal(100, replacement.RepressTime - replacement.ReleaseTime);
        Assert.Equal(7, replacement.LeftSegment.StartTime);
    }

    [Fact]
    public void ArticulationUsesLocalBeatLengthAcrossBpmChange()
    {
        var replacement = Assert.Single(Apply(BpmChangeFixture(), ArticulationOptions())
            .ModifiedChart.ArticulationReplacements, item => item.ParentOriginalLn.Lane == 0);
        Assert.Equal(50, replacement.RepressTime - replacement.ReleaseTime);
        Assert.Equal(7, replacement.LeftSegment.StartTime);
        Assert.Equal(3507, replacement.RightSegment.EndTime);
    }

    [Fact]
    public void ArticulationRejectsSegmentShorterThanLocalLnMinimum()
    {
        var result = Apply(BpmChangeFixture(parentEndBeat: 7), ArticulationOptions());
        Assert.DoesNotContain(result.ModifiedChart.ArticulationReplacements,
            item => item.ParentOriginalLn.Lane == 0);
        Assert.True(result.Statistics.RejectedRightTooShort > 0);
    }

    [Fact]
    public void ArticulationOnDoesNotConsumeOrChangePassOneDecisions()
    {
        var chart = ArticulationFixture(7, 0);
        var off = Apply(chart, ArticulationOptions() with { ArticulationEnabled = false }, 991);
        var on = Apply(chart, ArticulationOptions(), 991);

        Assert.Equal(off.ModifiedChart.AddedObjects, on.ModifiedChart.AddedObjects);
        Assert.Equal(off.Statistics.TapPlaced, on.Statistics.TapPlaced);
        Assert.Equal(off.Statistics.BaseLnPlaced, on.Statistics.BaseLnPlaced);
        Assert.Equal(off.Statistics.InteriorFreeLanePlaced, on.Statistics.InteriorFreeLanePlaced);
        Assert.Equal(off.Statistics.SuccessfulProbabilityRolls, on.Statistics.SuccessfulProbabilityRolls);
    }

    [Fact]
    public void SameSeedKeepsArticulationOutputAndFunnelDeterministic()
    {
        var chart = ArticulationFixture(7, 0);
        var a = Apply(chart, ArticulationOptions(), 1234);
        var b = Apply(chart, ArticulationOptions(), 1234);

        Assert.Equal(a.ModifiedChart.ArticulationReplacements, b.ModifiedChart.ArticulationReplacements);
        Assert.Equal(ArticulationMetrics(a.Statistics), ArticulationMetrics(b.Statistics));
        Assert.All(a.ModifiedChart.ArticulationReplacements.GroupBy(x => x.ParentOriginalLn.Sequence),
            group => Assert.Single(group));
    }

    [Theory]
    [InlineData(.10)]
    [InlineData(.30)]
    [InlineData(.50)]
    public void CrossKeySeedMatrixKeepsGeometryAndPassOneIdentity(double chance)
    {
        foreach (var keys in Enumerable.Range(4, 7))
        foreach (var seed in Enumerable.Range(1, 8))
        {
            var chart = ArticulationFixture(keys, seed % 3);
            var options = ArticulationOptions() with { Chance = chance };
            var off = Apply(chart, options with { ArticulationEnabled = false }, seed);
            var on = Apply(chart, options, seed);
            Assert.Equal(off.ModifiedChart.AddedObjects, on.ModifiedChart.AddedObjects);
            Assert.Equal(off.Statistics.SuccessfulProbabilityRolls, on.Statistics.SuccessfulProbabilityRolls);
            AssertValidLaneGeometry(on.ModifiedChart);
        }
    }

    [Fact]
    public void WriterEmitsValidArticulatedBeatmap()
    {
        var result = Apply(ArticulationFixture(7, 0), ArticulationOptions());
        var reparsed = OsuBeatmap.Parse(OsuBeatmap.Write(result.ModifiedChart, 1));
        Assert.Equal(result.ModifiedChart.AllObjects.Count, reparsed.OriginalObjects.Count);
        Assert.All(reparsed.OriginalObjects.Where(x => x.Type == ManiaObjectType.LongNote),
            item => Assert.True(item.EndTime > item.StartTime));
    }

    [Fact]
    public void ReleaseOnlyAnchorIsRejectedAsArticulationHead()
    {
        var objects = new List<ManiaObject> { ManiaObject.Ln(0, 0, 4000), ManiaObject.Ln(1, 0, 1875) };
        objects.AddRange(Enumerable.Range(2, 5).Select(lane => ManiaObject.Ln(lane, 0, 4000)));
        var result = Apply(Chart(7, objects.ToArray()), ArticulationOptions() with
        {
            InteriorMinimumSupportedAnchors = 1,
            RetriggerGapMinimumSupport = 1
        });

        Assert.Empty(result.ModifiedChart.ArticulationReplacements);
        Assert.True(result.Statistics.RejectedAnchorNotHead > 0);
    }

    private static AddNotesOptions BaseOptions() => new()
    {
        ContextualDensityNormalizationEnabled = false,
        DensityDecayPerColumn = 1
    };

    private static AddNotesOptions ArticulationOptions() => BaseOptions() with
    {
        Chance = 1,
        InteriorLnOpportunitiesEnabled = true,
        MaxInteriorOpportunitiesPerSource = 2,
        InteriorMinimumSourceBeats = 3,
        InteriorMinimumContextLnCount = 3,
        InteriorMinimumSupportedAnchors = 2,
        ArticulationEnabled = true,
        ArticulationMaxNonHeldColumns = 1,
        RetriggerGapMinimumSupport = 2,
        RetriggerGapWindowBeats = 4
    };

    private static ManiaChart ArticulationFixture(int keys, int nonHeldAtAnchor, decimal beatLength = 500,
        decimal gapBeats = .25m, int offset = 0)
    {
        const decimal parentEndBeat = 8;
        const decimal headBeat = 4;
        var releaseBeat = headBeat - gapBeats;
        int T(decimal beat) => checked(offset + (int)decimal.Round(beat * beatLength, 0,
            MidpointRounding.AwayFromZero));

        var objects = new List<ManiaObject>
        {
            ManiaObject.Ln(0, T(0), T(parentEndBeat)),
            ManiaObject.Ln(1, T(0), T(releaseBeat)),
            ManiaObject.Ln(1, T(headBeat), T(parentEndBeat))
        };

        var heldOtherLanes = Math.Max(0, keys - nonHeldAtAnchor - 2);
        for (var lane = 2; lane < keys; lane++)
        {
            // Original LN->head transitions teach the local retrigger gap. They end well before H,
            // so saturation at H can be controlled independently.
            objects.Add(ManiaObject.Ln(lane, T(-2), T(-gapBeats)));
            objects.Add(ManiaObject.Tap(lane, T(0)));
            if (lane - 2 < heldOtherLanes)
                objects.Add(ManiaObject.Ln(lane, T(headBeat), T(parentEndBeat)));
            else
                objects.Add(ManiaObject.Tap(lane, T(headBeat + .2m)));
        }

        return Chart(keys, objects.ToArray(), beatLength, offset);
    }

    private static ManiaChart BpmChangeFixture(decimal parentEndBeat = 10)
    {
        var timings = new[] { new TimingPoint(7, 500), new TimingPoint(2007, 250) };
        var timeline = new BeatTimeline(timings);
        int T(decimal beat) => timeline.ToTimeMilliseconds(beat);
        const decimal headBeat = 6;
        const decimal gap = .2m;
        var objects = new List<ManiaObject>
        {
            ManiaObject.Ln(0, T(0), T(parentEndBeat)),
            ManiaObject.Ln(1, T(0), T(headBeat - gap)),
            ManiaObject.Ln(1, T(headBeat), T(10))
        };
        for (var lane = 2; lane < 7; lane++)
        {
            objects.Add(ManiaObject.Ln(lane, T(0), T(2 - gap)));
            objects.Add(ManiaObject.Tap(lane, T(2)));
            objects.Add(ManiaObject.Ln(lane, T(headBeat), T(10)));
        }
        var chart = Chart(7, objects.ToArray(), 500, 7);
        return new ManiaChart
        {
            KeyCount = 7,
            Lines = chart.Lines,
            OriginalObjects = chart.OriginalObjects,
            TimingPoints = timings
        };
    }

    private static AddNotesResult Apply(ManiaChart chart, AddNotesOptions options, int seed = 7) =>
        new AddNotesEngine().Apply(chart, options, new SeededRandom(seed));

    private static ManiaChart Chart(int keys, ManiaObject[] objects, decimal beatLength = 500, int offset = 0) => new()
    {
        KeyCount = keys,
        Lines = ["osu file format v14", "", "[General]", "Mode:3", "", "[Metadata]", "Version:Test",
            "BeatmapID:0", "", "[Difficulty]", $"CircleSize:{keys}", "", "[TimingPoints]",
            $"{offset},{beatLength},4,2,0,100,1,0", "", "[HitObjects]"],
        OriginalObjects = objects.Select((item, index) => item with
        {
            Sequence = index,
            RawLine = Raw(item, keys)
        }).ToArray(),
        TimingPoints = [new TimingPoint(offset, beatLength)]
    };

    private static string BeatmapText(int keys) => string.Join('\n',
        "osu file format v14", "[General]", "Mode:3", "[Difficulty]", $"CircleSize:{keys}",
        "[TimingPoints]", "0,500,4,2,0,100,1,0", "[HitObjects]", "256,192,0,1,0,0:0:0:0:");

    private static string Raw(ManiaObject item, int keys)
    {
        var x = (int)Math.Floor((item.Lane + .5) * 512 / keys);
        return item.Type == ManiaObjectType.Tap
            ? $"{x},192,{item.StartTime},1,0,0:0:0:0:"
            : $"{x},192,{item.StartTime},128,0,{item.EndTime}:0:0:0:0:";
    }

    private static object ArticulationMetrics(AddNotesStatistics stats) => new
    {
        stats.ArticulationEligible,
        stats.ArticulationPlaced,
        stats.RejectedAnchorNotHead,
        stats.RejectedNotSaturated,
        stats.RejectedNoRetriggerGap,
        stats.RejectedLeftTooShort,
        stats.RejectedRightTooShort,
        stats.RejectedParentCap,
        stats.RejectedGeometry,
        stats.RejectedAlreadyModified
    };

    private static void AssertValidLaneGeometry(ManiaChart chart)
    {
        foreach (var lane in chart.AllObjects.GroupBy(item => item.Lane))
        {
            var ordered = lane.OrderBy(item => item.StartTime).ThenBy(item => item.EndTime ?? item.StartTime).ToArray();
            for (var i = 1; i < ordered.Length; i++)
                Assert.True((ordered[i - 1].EndTime ?? ordered[i - 1].StartTime) < ordered[i].StartTime,
                    $"lane {lane.Key}: {ordered[i - 1]} overlaps {ordered[i]}");
        }
    }

    private sealed class ChoiceRandom(int choice) : IRandomSource
    {
        public double NextDouble() => 0;
        public int Next(int maxExclusive) => Math.Clamp(choice, 0, maxExclusive - 1);
    }
}
