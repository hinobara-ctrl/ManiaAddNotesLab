using ManiaAddNotesLab.Core;
using System.Text;
using System.Text.Json;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseBDecisionDiagnosticsTests
{
    [Fact]
    public void DiagnosticsOnOffPreserveOutputRngAndLegacyMetrics()
    {
        var chart = Chart(7, [ManiaObject.Tap(0, 0), ManiaObject.Ln(1, 500, 1500),
            ManiaObject.Ln(2, 500, 1000), ManiaObject.Tap(3, 1000)]);
        var options = new AddNotesOptions { Chance = .73, ContextualDensityNormalizationEnabled = false };
        var offRandom = new RecordingRandom(991);
        var onRandom = new RecordingRandom(991);
        var off = new AddNotesEngine().Apply(chart, options, offRandom);
        var on = new AddNotesEngine().Apply(chart, options with { DiagnosticsEnabled = true }, onRandom);

        Assert.Equal(OsuBeatmap.Write(off.ModifiedChart, options.Chance),
            OsuBeatmap.Write(on.ModifiedChart, options.Chance));
        Assert.Equal(offRandom.Transcript, onRandom.Transcript);
        Assert.Equal(LegacyMetrics(off.Statistics), LegacyMetrics(on.Statistics));
        Assert.NotNull(on.DecisionDiagnostics);
        Assert.Null(off.DecisionDiagnostics);
    }

    [Fact]
    public void DiagnosticKeysAreDeterministicUniqueAndPolicyLocal()
    {
        var chart = Chart(4, [ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, 0, 1000)]);
        var options = new AddNotesOptions { Chance = 1, ContextualDensityNormalizationEnabled = false,
            DiagnosticsEnabled = true };
        var a = new AddNotesEngine().Apply(chart, options, new SeededRandom(12)).DecisionDiagnostics!;
        var b = new AddNotesEngine().Apply(chart, options, new SeededRandom(12)).DecisionDiagnostics!;

        Assert.Equal(a.Decisions.Select(x => x.CandidateKey), b.Decisions.Select(x => x.CandidateKey));
        Assert.Equal(a.Decisions.Length, a.Decisions.Select(x => x.CandidateKey).Distinct().Count());
        Assert.All(a.Decisions, x => Assert.Contains(MapperEvidenceProfileBuilder.BehaviorPolicyVersion,
            x.CandidateKey.Value));
        Assert.All(a.Decisions, x => Assert.StartsWith(MapperEvidenceProfileBuilder.BehaviorPolicyVersion,
            x.CandidateKey.Value));
    }

    [Fact]
    public void DetailedGeometryIdentifiesOriginalHoldAndObservation()
    {
        var chart = Chart(1, [ManiaObject.Ln(0, 0, 1000)]);
        var (geometry, profile) = Geometry(chart);
        var detail = geometry.ExplainLnPlacement(.5m, .75m, 0, 250, 375, profile);
        var failure = Assert.Single(Assert.Single(detail.LaneEvaluations).Failures);
        Assert.Equal(HardValidityFailureCause.BlockedByOriginalHold, failure.Cause);
        Assert.Equal(new OriginalObservationId(0), Assert.Single(failure.Blockers).ObservationId);
        Assert.False(detail.IsValid);
    }

    [Fact]
    public void DetailedGeometryDistinguishesOriginalTapAndLnHead()
    {
        var chart = Chart(2, [ManiaObject.Tap(0, 500), ManiaObject.Ln(1, 500, 1000)]);
        var (geometry, profile) = Geometry(chart);
        var detail = geometry.ExplainLnPlacement(.5m, 1.5m, 0, 250, 750, profile);
        Assert.Contains(detail.LaneEvaluations[0].Failures,
            x => x.Cause == HardValidityFailureCause.BlockedByOriginalTap);
        Assert.Contains(detail.LaneEvaluations[1].Failures,
            x => x.Cause == HardValidityFailureCause.BlockedByOriginalHead);
    }

    [Fact]
    public void DetailedGeometryDistinguishesSyntheticTapLnAndReplacement()
    {
        var chart = Chart(3, []);
        var timeline = new BeatTimeline(chart.TimingPoints);
        var items = new[]
        {
            new TimedManiaObject(ManiaObject.Tap(0, 500, true), 1, 1),
            new TimedManiaObject(ManiaObject.Ln(1, 500, 1000, true), 1, 2),
            new TimedManiaObject(ManiaObject.Ln(2, 500, 1000, true, origin: AddedObjectOrigin.ArticulationReplacement), 1, 2)
        };
        var geometry = new LaneGeometryIndex(3, items);
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var detail = geometry.ExplainLnPlacement(1, 1.5m, 0, 500, 750, profile);

        Assert.Equal(HardValidityFailureCause.BlockedBySyntheticTap, Assert.Single(detail.LaneEvaluations[0].Failures).Cause);
        Assert.Equal(HardValidityFailureCause.BlockedBySyntheticLn, Assert.Single(detail.LaneEvaluations[1].Failures).Cause);
        Assert.Equal(HardValidityFailureCause.BlockedByArticulationReplacement, Assert.Single(detail.LaneEvaluations[2].Failures).Cause);
        Assert.All(detail.LaneEvaluations.SelectMany(x => x.Failures).SelectMany(x => x.Blockers),
            x => Assert.NotNull(x.SyntheticDiagnosticId));
        _ = timeline;
    }

    [Fact]
    public void DetailedGeometryDistinguishesSpacingBeforeAndAfterAndMixedLanes()
    {
        var chart = Chart(3, [ManiaObject.Tap(0, 0), ManiaObject.Tap(1, 500), ManiaObject.Ln(2, 0, 1000)]);
        var (geometry, profile) = Geometry(chart);
        var detail = geometry.ExplainLnPlacement(.1m, .9m, .25m, 50, 450, profile);

        Assert.Contains(detail.LaneEvaluations[0].Failures,
            x => x.Cause == HardValidityFailureCause.BlockedBySpacingBefore);
        Assert.Contains(detail.LaneEvaluations[1].Failures,
            x => x.Cause == HardValidityFailureCause.BlockedBySpacingAfter);
        Assert.Contains(detail.LaneEvaluations[2].Failures,
            x => x.Cause == HardValidityFailureCause.BlockedByOriginalHold);
        Assert.False(detail.IsValid);
    }

    [Fact]
    public void DiagnosticAndLegacyLegalityAgreeForTapAndLn()
    {
        var chart = Chart(4, [ManiaObject.Tap(0, 0), ManiaObject.Ln(1, 0, 1000),
            ManiaObject.Tap(2, 1500)]);
        var (geometry, profile) = Geometry(chart);
        var stats = new AddNotesStatistics();
        var legacyTap = geometry.FindLegalTapLanes(1, stats);
        var detailTap = geometry.ExplainTapPlacement(1, profile).LaneEvaluations.Where(x => x.IsValid).Select(x => x.Lane);
        var legacyLn = geometry.FindLegalLnLanes(1, 2, .125m, stats, out _, out _);
        var detailLn = geometry.ExplainLnPlacement(1, 2, .125m, 500, 1000, profile)
            .LaneEvaluations.Where(x => x.IsValid).Select(x => x.Lane);
        Assert.Equal(legacyTap, detailTap);
        Assert.Equal(legacyLn, detailLn);
    }

    [Fact]
    public void InvalidMaterializationAndOutsideLaneRemainHardValidityFailures()
    {
        var chart = Chart(1, []);
        var (geometry, profile) = Geometry(chart);
        var invalid = geometry.ExplainLnPlacement(1, 1.1m, 0, 500, 500, profile);
        Assert.Contains(HardValidityFailureCause.InvalidAfterMillisecondMaterialization, invalid.FailureReasons);
        var outside = geometry.ExplainLnLane(2, 1, 2, 0, profile);
        Assert.Equal(HardValidityFailureCause.OutsideLane, Assert.Single(outside.Failures).Cause);
    }

    [Fact]
    public void OneOriginalLnCanProduceTwoLabelsButOneIndependentWitness()
    {
        var chart = Chart(4, [ManiaObject.Ln(0, 0, 1000)]);
        var result = new AddNotesEngine().Apply(chart, new AddNotesOptions { Chance = 1,
            ContextualDensityNormalizationEnabled = false, DiagnosticsEnabled = true }, new SeededRandom(3));
        var candidate = Assert.Single(result.DecisionDiagnostics!.Decisions,
            x => x.CandidateKind == DiagnosticCandidateKind.LongNote);
        Assert.Equal(2, candidate.LegacyEvidencePaths.Length);
        Assert.Equal(1, candidate.IndependentWitnessCount);
        Assert.Equal(1, candidate.DuplicateEvidencePaths);
        Assert.Equal(2, Assert.Single(candidate.Certificate.Witnesses).EvidenceTags.Length);
        Assert.Equal(2.5, candidate.LegacyWeight);
    }

    [Fact]
    public void TwoOriginalLnsRemainTwoIndependentWitnessesWithoutChangingLegacyWeight()
    {
        var chart = Chart(4, [ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, 0, 1000)]);
        var result = new AddNotesEngine().Apply(chart, new AddNotesOptions { Chance = 1,
            ContextualDensityNormalizationEnabled = false, DiagnosticsEnabled = true }, new SeededRandom(3));
        var candidate = result.DecisionDiagnostics!.Decisions.First(x => x.CandidateKind == DiagnosticCandidateKind.LongNote);
        Assert.Equal(4, candidate.LegacyEvidencePaths.Length);
        Assert.Equal(2, candidate.IndependentWitnessCount);
        Assert.Equal(5, candidate.LegacyWeight);
    }

    [Fact]
    public void RetriggerShadowKeepsGapSpecificFrequencyAndTransitionType()
    {
        var objects = new List<ManiaObject>();
        for (var i = 0; i < 10; i++)
        {
            var start = i * 3000;
            var gap = i < 8 ? 250 : 500;
            objects.Add(ManiaObject.Ln(0, start, start + 1000));
            objects.Add(i % 2 == 0 ? ManiaObject.Tap(0, start + 1000 + gap)
                : ManiaObject.Ln(0, start + 1000 + gap, start + 1750 + gap));
        }
        var profile = MapperEvidenceProfileBuilder.Build(Chart(1, objects.ToArray(), 1000));
        var evidence = DecisionDiagnosticQueries.BuildRetriggerEvidence(profile, 0, 15,
            new AddNotesOptions { RetriggerGapWindowBeats = 100, RetriggerGapMinimumSupport = 1 });
        Assert.Equal(8, evidence.Where(x => x.GapBeats == .25m).Sum(x => x.TransitionWitnessCount));
        Assert.Equal(2, evidence.Where(x => x.GapBeats == .5m).Sum(x => x.TransitionWitnessCount));
        Assert.Contains(evidence, x => x.TransitionType == RetriggerTransitionType.ReleaseToTap);
        Assert.Contains(evidence, x => x.TransitionType == RetriggerTransitionType.ReleaseToLongNoteHead);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void DiagnosticSchemaWorksWithoutKeySpecificLogic(int keys)
    {
        var chart = Chart(keys, [ManiaObject.Ln(0, 0, 1000)]);
        var result = new AddNotesEngine().Apply(chart, new AddNotesOptions { Chance = 1,
            ContextualDensityNormalizationEnabled = false, DiagnosticsEnabled = true }, new SeededRandom(1));
        Assert.Equal(DecisionDiagnosticVersions.Current, result.DecisionDiagnostics!.DecisionDiagnosticVersion);
        Assert.All(result.DecisionDiagnostics.Decisions.SelectMany(x => x.HardValidity.LaneEvaluations),
            x => Assert.InRange(x.Lane, 0, keys - 1));
    }

    [Fact]
    public void InteriorDiagnosticsExposeContainedCrossingEqualAndAnchorProvenance()
    {
        var chart = Chart(7, [
            ManiaObject.Ln(0, 0, 4000), ManiaObject.Ln(1, 0, 500), ManiaObject.Ln(2, 0, 3000),
            ManiaObject.Ln(3, 500, 4000), ManiaObject.Tap(4, 1000), ManiaObject.Tap(5, 2000)]);
        var result = new AddNotesEngine().Apply(chart, new AddNotesOptions { Chance = 1,
            ContextualDensityNormalizationEnabled = false, DensityDecayPerColumn = 1,
            InteriorLnOpportunitiesEnabled = true, InteriorMinimumSourceBeats = 3,
            InteriorMinimumContextLnCount = 3, InteriorMinimumSupportedAnchors = 2,
            DiagnosticsEnabled = true }, new ZeroRandom());
        var interior = result.DecisionDiagnostics!.Decisions.Where(x => x.OpportunityKind == OpportunityKind.LnInterior).ToArray();
        Assert.NotEmpty(interior);
        Assert.All(interior, x => Assert.True(x.StartInsideParent));
        Assert.Contains(interior, x => x.InteriorEndRelation == InteriorEndRelation.Contained);
        Assert.Contains(interior, x => x.InteriorEndRelation is InteriorEndRelation.Crossing or InteriorEndRelation.EqualEnd);
        Assert.Contains(interior, x => x.InteriorAnchorProvenance.HasFlag(InteriorAnchorKind.Head));
    }

    [Fact]
    public void ArticulationDiagnosticsCompareLegacyGateWithActualCause()
    {
        var chart = ArticulationFixture(7, 0);
        var result = new AddNotesEngine().Apply(chart, ArticulationOptions(), new ZeroRandom());
        Assert.NotEmpty(result.DecisionDiagnostics!.ArticulationIntents);
        Assert.All(result.DecisionDiagnostics.ArticulationIntents, x => Assert.True(x.LegacySaturationGate));
        Assert.Contains(result.DecisionDiagnostics.ArticulationIntents, x => x.BlockedByOriginalHolds);
        Assert.True(result.DecisionDiagnostics.Summary.LegacySaturationEligible >=
            result.DecisionDiagnostics.Summary.ActuallyOriginalHoldBlocked);
    }

    [Fact]
    public void DiagnosticsPreserveArticulationSelectionAndDerivedRngTranscript()
    {
        var chart = ArticulationFixture(7, 0);
        var offRandom = new RecordingRandom(37);
        var onRandom = new RecordingRandom(37);
        var off = new AddNotesEngine().Apply(chart, ArticulationOptions() with { DiagnosticsEnabled = false }, offRandom);
        var on = new AddNotesEngine().Apply(chart, ArticulationOptions(), onRandom);
        Assert.Equal(off.ModifiedChart.AddedObjects, on.ModifiedChart.AddedObjects);
        Assert.Equal(off.ModifiedChart.ArticulationReplacements, on.ModifiedChart.ArticulationReplacements);
        Assert.Equal(offRandom.Transcript, onRandom.Transcript);
    }

    [Fact]
    public void DiagnosticJsonContainsNoConfidenceOrMapperSupport()
    {
        var result = new AddNotesEngine().Apply(Chart(3, [ManiaObject.Ln(0, 0, 1000), ManiaObject.Tap(1, 1500)]),
            new AddNotesOptions { Chance = 1, ContextualDensityNormalizationEnabled = false,
                DiagnosticsEnabled = true }, new SeededRandom(1));
        var json = DecisionDiagnosticsJson.Serialize(result.DecisionDiagnostics!, DiagnosticDetailMode.All);
        Assert.DoesNotContain("confidence", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mapperSupport", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("notEvaluated", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MeasuredDiagnosticJsonReportsItsExactUtf8Size()
    {
        var result = new AddNotesEngine().Apply(Chart(4, [ManiaObject.Ln(0, 0, 1000)]),
            new AddNotesOptions { Chance = 1, ContextualDensityNormalizationEnabled = false,
                DiagnosticsEnabled = true }, new SeededRandom(1));

        var json = DecisionDiagnosticsJson.SerializeMeasured(
            result.DecisionDiagnostics!, DiagnosticDetailMode.Relevant);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(Encoding.UTF8.GetByteCount(json),
            document.RootElement.GetProperty("detailedExportBytes").GetInt64());
    }

    private static (LaneGeometryIndex Geometry, MapperEvidenceProfile Profile) Geometry(ManiaChart chart)
    {
        var timeline = new BeatTimeline(chart.TimingPoints);
        return (new LaneGeometryIndex(chart.KeyCount, new OriginalChartAnalysis(chart, timeline).Objects),
            MapperEvidenceProfileBuilder.Build(chart));
    }

    private static AddNotesOptions ArticulationOptions() => new()
    {
        Chance = 1,
        ContextualDensityNormalizationEnabled = false,
        DensityDecayPerColumn = 1,
        InteriorLnOpportunitiesEnabled = true,
        MaxInteriorOpportunitiesPerSource = 2,
        InteriorMinimumSourceBeats = 3,
        InteriorMinimumContextLnCount = 3,
        InteriorMinimumSupportedAnchors = 2,
        ArticulationEnabled = true,
        ArticulationMaxNonHeldColumns = 1,
        RetriggerGapMinimumSupport = 2,
        RetriggerGapWindowBeats = 4,
        DiagnosticsEnabled = true
    };

    private static ManiaChart ArticulationFixture(int keys, int nonHeldAtAnchor)
    {
        const decimal beatLength = 500;
        int T(decimal beat) => (int)(beat * beatLength);
        const decimal headBeat = 4;
        const decimal gap = .25m;
        var objects = new List<ManiaObject>
        {
            ManiaObject.Ln(0, T(0), T(8)), ManiaObject.Ln(1, T(0), T(headBeat - gap)),
            ManiaObject.Ln(1, T(headBeat), T(8))
        };
        var heldOtherLanes = Math.Max(0, keys - nonHeldAtAnchor - 2);
        for (var lane = 2; lane < keys; lane++)
        {
            objects.Add(ManiaObject.Ln(lane, T(-2), T(-gap)));
            objects.Add(ManiaObject.Tap(lane, T(0)));
            if (lane - 2 < heldOtherLanes) objects.Add(ManiaObject.Ln(lane, T(headBeat), T(8)));
            else objects.Add(ManiaObject.Tap(lane, T(headBeat + .2m)));
        }
        return Chart(keys, objects.ToArray(), beatLength);
    }

    private static object LegacyMetrics(AddNotesStatistics s) => new
    {
        s.InputObjects, s.OutputObjects, s.AddedTaps, s.AddedLongNotes, s.TotalOpportunities,
        s.SuccessfulProbabilityRolls, s.FailedPlacements, s.LnCandidateRetries, s.LnSkipsDueToGeometry,
        s.DensityAdjustedOpportunities, s.ContextualDensityAdjustedOpportunities, s.BaseHeadOpportunities,
        s.InteriorLnOpportunities, s.SuccessfulBaseRolls, s.SuccessfulInteriorRolls,
        s.AddedLongNotesFromHeadOpportunities, s.AddedLongNotesFromInteriorOpportunities,
        s.GeometryChecks, s.GeometryObjectsExamined, s.LnGeometryChecks, s.BeatConversions,
        s.LnCandidatesBuilt, s.LnCandidatesImpossible, s.ArticulationEligible, s.ArticulationPlaced
    };

    private static ManiaChart Chart(int keys, ManiaObject[] objects, decimal beatLength = 500) => new()
    {
        KeyCount = keys,
        Lines = ["osu file format v14", "[General]", "Mode:3", "[Metadata]", "Version:Test",
            "BeatmapID:0", "[Difficulty]", $"CircleSize:{keys}", "[TimingPoints]",
            $"0,{beatLength},4,2,0,100,1,0", "[HitObjects]"],
        OriginalObjects = objects.Select((item, index) => item with
        {
            Sequence = index,
            RawLine = Raw(item, keys)
        }).ToArray(),
        TimingPoints = [new TimingPoint(0, beatLength)]
    };

    private static string Raw(ManiaObject item, int keys)
    {
        var x = (int)Math.Floor((item.Lane + .5) * 512 / keys);
        return item.Type == ManiaObjectType.Tap
            ? $"{x},192,{item.StartTime},1,0,0:0:0:0:"
            : $"{x},192,{item.StartTime},128,0,{item.EndTime}:0:0:0:0:";
    }

    private sealed class RecordingRandom(int seed) : IDerivableRandomSource
    {
        private readonly Random _random = new(seed);
        public List<string> Transcript { get; } = [];
        public double NextDouble() { var value = _random.NextDouble(); Transcript.Add($"D:{value:R}"); return value; }
        public int Next(int maxExclusive) { var value = _random.Next(maxExclusive); Transcript.Add($"N:{maxExclusive}:{value}"); return value; }
        public IRandomSource Derive(int salt) { Transcript.Add($"R:{salt}"); return new RecordingRandom(unchecked(seed * 16777619 ^ salt ^ 0x41A7)); }
    }

    private sealed class ZeroRandom : IDerivableRandomSource
    {
        public double NextDouble() => 0;
        public int Next(int maxExclusive) => 0;
        public IRandomSource Derive(int salt) => this;
    }
}
