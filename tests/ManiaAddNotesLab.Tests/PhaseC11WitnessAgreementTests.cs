using ManiaAddNotesLab.Core;
using System.Security.Cryptography;
using System.Text;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseC11WitnessAgreementTests
{
    [Fact]
    public void SameHeadAgreementIsExactInBeatAndMillisecondSpace()
    {
        var build = Build(ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, 0, 1000));
        var agreement = Assert.Single(LnWitnessAgreementResearch.ClassifyAgreements(build, 0, 0));

        Assert.Equal(WitnessAgreementKind.SameHeadAgreement, agreement.AgreementKind);
        Assert.True(agreement.BeatSpaceExactEquality);
        Assert.True(agreement.DecimalEquality);
        Assert.True(agreement.MillisecondEquality);
        Assert.True(agreement.CandidateEndpointEquality);
        Assert.False(agreement.EqualityOnlyThroughTolerance);
        Assert.Equal(agreement.DurationEndBeat, agreement.ExactReleaseBeat);
        Assert.Equal(agreement.DurationRawEndTime, agreement.ReleaseRawEndTime);
    }

    [Fact]
    public void MaterializationCoincidenceIsSeparatedFromSameHeadAgreement()
    {
        var timeline = Timeline();
        var source = ManiaObject.Ln(0, 0, 1000, sequence: 0);
        var donor = ManiaObject.Ln(1, 10, 510, sequence: 1);
        var context = new LocalLnContext(0, [new LnObservation(donor, .02m, 1.02m)], 1);
        var build = new AddNotesEngine().BuildReleaseCandidatesForResearch(source, context, timeline, 4,
            applySourceAffinity: false, mapRelativeSnapEnabled: false);
        var agreement = Assert.Single(LnWitnessAgreementResearch.ClassifyAgreements(build, 0, 0));

        Assert.Equal(WitnessAgreementKind.MaterializationCoincidence, agreement.AgreementKind);
        Assert.False(agreement.BeatSpaceExactEquality);
        Assert.False(agreement.MillisecondEquality);
        Assert.Equal(500, agreement.DurationRawEndTime);
        Assert.Equal(510, agreement.ReleaseRawEndTime);
        Assert.Equal(500, agreement.MaterializedEndTime);
        Assert.True(agreement.SnapAdjustmentMilliseconds > 0);
        Assert.False(agreement.EqualityOnlyThroughTolerance);
    }

    [Fact]
    public void TwoLabelsFromOneObservationStillHaveOneIndependentWitness()
    {
        var build = Build(ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, 0, 1000));
        var candidate = Assert.Single(build.Candidates);

        Assert.Equal(2, candidate.Evidence.EvidencePathCount);
        Assert.Equal(1, candidate.Evidence.IndependentWitnessCount);
        Assert.Equal(1, candidate.Evidence.DuplicatePathCount);
        var agreement = Assert.Single(LnWitnessAgreementResearch.ClassifyAgreements(build, 0, 0));
        Assert.Equal(new[] { LnEvidenceLabel.Duration, LnEvidenceLabel.ExactRelease },
            agreement.Labels.ToArray());
    }

    [Fact]
    public void NoDuplicateRouteProducesNoAgreement()
    {
        var build = Build(ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, 500, 1000));

        Assert.Empty(LnWitnessAgreementResearch.ClassifyAgreements(build, 0, 0));
        Assert.All(build.Candidates, candidate => Assert.Equal(0, candidate.Evidence.DuplicatePathCount));
    }

    [Fact]
    public void LeaveOneHeadGroupOutRemovesSameHeadStructuralTwin()
    {
        var chart = Chart(4,
            ManiaObject.Ln(0, 0, 1000),
            ManiaObject.Ln(1, 0, 1000),
            ManiaObject.Ln(2, 500, 1500));

        var objectOut = LnWitnessAgreementResearch.Evaluate(chart, HeldOutExclusionMode.LeaveOneObjectOut);
        var headOut = LnWitnessAgreementResearch.Evaluate(chart, HeldOutExclusionMode.LeaveOneHeadGroupOut);
        var targetObjectOut = objectOut.Targets.Single(x => x.TargetObservationId.Value == 0);
        var targetHeadOut = headOut.Targets.Single(x => x.TargetObservationId.Value == 0);

        Assert.True(targetObjectOut.HasExactStructuralTwin);
        Assert.True(targetObjectOut.VocabularySameHeadAgreements > 0);
        Assert.Equal(0, targetHeadOut.VocabularySameHeadAgreements);
    }

    [Fact]
    public void HeldOutTargetCannotVoteForItselfInEitherExclusionMode()
    {
        var chart = Chart(4, ManiaObject.Ln(0, 0, 1000));

        Assert.All(new[] { HeldOutExclusionMode.LeaveOneObjectOut,
            HeldOutExclusionMode.LeaveOneHeadGroupOut }, mode =>
        {
            var target = Assert.Single(LnWitnessAgreementResearch.Evaluate(chart, mode).Targets);
            Assert.False(target.HasCandidateVocabulary);
            Assert.Equal(0, target.EvidencePathCount);
        });
    }

    [Fact]
    public void GeometryRemovesHeldOutTargetAndDoesNotLetItBlockItsOwnLane()
    {
        var chart = Chart(2,
            ManiaObject.Ln(0, 0, 1000),
            ManiaObject.Ln(1, 0, 1000));
        var target = LnWitnessAgreementResearch.Evaluate(chart,
            HeldOutExclusionMode.LeaveOneObjectOut).Targets.Single(x => x.TargetObservationId.Value == 0);

        Assert.True(target.TrueTargetCandidatePresent);
        Assert.True(target.OriginalEndpointHasAnyLegalLane);
        Assert.True(target.OriginalEndpointIsLegalInTargetLane);
    }

    [Fact]
    public void GeometryAwareDiagnosticUsesSameLegacyLegalityRule()
    {
        var chart = Chart(3, ManiaObject.Ln(0, 0, 1000), ManiaObject.Tap(1, 500));
        var timeline = Timeline();
        var analysis = new OriginalChartAnalysis(chart, timeline);
        var geometry = new LaneGeometryIndex(3, analysis.Objects);
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var stats = new AddNotesStatistics();

        var legacy = geometry.FindLegalLnLanes(1, 2, .125m, stats, out _, out _);
        var diagnostic = geometry.ExplainLnPlacement(1, 2, .125m, 500, 1000, profile)
            .LaneEvaluations.Where(x => x.IsValid).Select(x => x.Lane);

        Assert.Equal(legacy, diagnostic);
    }

    [Fact]
    public void NormalizedTargetWeightUsesExactTakeWeightedDenominator()
    {
        var chart = Chart(4,
            ManiaObject.Ln(0, 0, 1000),
            ManiaObject.Ln(1, 0, 1000),
            ManiaObject.Ln(2, 500, 1000));
        var target = LnWitnessAgreementResearch.Evaluate(chart,
            HeldOutExclusionMode.LeaveOneObjectOut).Targets.Single(x => x.TargetObservationId.Value == 0);

        Assert.NotNull(target.LegacyTargetWeight);
        Assert.NotNull(target.UniqueWitnessTargetWeight);
        Assert.Equal(target.LegacyTargetWeight!.Value / target.LegacyTotalLegalCandidateWeight,
            target.LegacyTargetNormalizedWeight!.Value, 12);
        Assert.Equal(target.UniqueWitnessTargetWeight!.Value / target.UniqueWitnessTotalLegalCandidateWeight,
            target.UniqueWitnessTargetNormalizedWeight!.Value, 12);
        Assert.Equal(target.UniqueWitnessTargetNormalizedWeight.Value - target.LegacyTargetNormalizedWeight.Value,
            target.DeltaNormalizedWeight!.Value, 12);
    }

    [Fact]
    public void ResearchEvaluationIsDeterministicAndConsumesNoRandomSource()
    {
        var chart = Chart(4, ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, 0, 1000));
        var first = LnWitnessAgreementResearch.Evaluate(chart, HeldOutExclusionMode.LeaveOneObjectOut);
        var second = LnWitnessAgreementResearch.Evaluate(chart, HeldOutExclusionMode.LeaveOneObjectOut);

        Assert.Equal(first.ChartFingerprint, second.ChartFingerprint);
        Assert.Equal(first.KeyCount, second.KeyCount);
        Assert.Equal(first.ExclusionMode, second.ExclusionMode);
        Assert.Equal(first.Targets.ToArray(), second.Targets.ToArray());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void ValidationHasNoKeyCountSpecificBranch(int keys)
    {
        var chart = Chart(keys, ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(0, 0, 1000));
        var result = LnWitnessAgreementResearch.Evaluate(chart,
            HeldOutExclusionMode.LeaveOneObjectOut);

        Assert.Equal(keys, result.KeyCount);
        Assert.Equal(2, result.TargetsEvaluated);
        Assert.All(result.Targets, target => Assert.True(target.TrueTargetCandidatePresent));
    }

    [Fact]
    public void CorpusDiscoveryDoesNotModifyItsSourceAndCollapsesExactDuplicates()
    {
        var root = Path.Combine(Path.GetTempPath(), "mania-c11-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var original = Path.Combine(root, "artist - title (mapper) [hard].osu");
            var duplicate = Path.Combine(root, "duplicate.osu");
            var generated = Path.Combine(root, "artist - title (mapper) [hard] [ADD 50 seed 1 run 1].osu");
            var text = BeatmapText();
            File.WriteAllText(original, text);
            File.WriteAllText(duplicate, text);
            File.WriteAllText(generated, text);
            var before = Directory.GetFiles(root).ToDictionary(path => Path.GetFileName(path)!,
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));

            var result = C11CorpusDiscovery.Discover(root);
            var after = Directory.GetFiles(root).ToDictionary(path => Path.GetFileName(path)!,
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));

            Assert.Equal(before, after);
            Assert.Equal(3, result.OsuFileCount);
            Assert.Equal(1, result.GeneratedOutputCount);
            Assert.Equal(2, result.HumanOriginalLocationCount);
            Assert.Equal(1, result.ExactDuplicateLocationCount);
            Assert.Single(result.UniqueHumanCharts);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HistoricalPhaseCSnapshotsRemainByteExact()
    {
        var root = AppContext.BaseDirectory;
        var cases = new[]
        {
            ("samples/01-rice-4k.osu", .65, 611, new AddNotesOptions { Chance = .65 },
                "1D3C7C1C93B3D4C9C86D9D542AB0780AF0FA6B0A6A05BF490735731BC98550B0"),
            ("samples/02-isolated-ln-4k.osu", 1.0, 1, new AddNotesOptions { Chance = 1 },
                "11B5FD207202BF9CEE16E4506AABA92C5D1FEE3CF754F369C21D736D8F698EB9"),
            ("samples/10-basic-7k.osu", .7, 77, new AddNotesOptions { Chance = .7 },
                "3D874B5F038E9D4371A5DBDDEB7D73CD9758FDEF338100E862F461EC97149104"),
            ("samples/11-high-keymode-18k.osu", .7, 2, new AddNotesOptions { Chance = .7 },
                "219B1F9BC9D07B99F928CCC9AE4BB75F6C8E5A4FA5EC60365E89010D7176E391"),
            ("samples/experimental-ln-interior.osu", 1.0, 926, new AddNotesOptions { Chance = 1,
                ContextualDensityNormalizationEnabled = false, DensityDecayPerColumn = 1,
                InteriorLnOpportunitiesEnabled = true, ArticulationEnabled = true },
                "FBE3FDB25059000FFD3DD8B894B10CE040651D4A8989EAD72E458FE023A5CB1A")
        };

        foreach (var (relative, chance, seed, options, expected) in cases)
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(Path.Combine(root, relative)));
            var output = new AddNotesEngine().Apply(chart, options, new SeededRandom(seed));
            var bytes = Encoding.UTF8.GetBytes(OsuBeatmap.Write(output.ModifiedChart, chance));
            Assert.Equal(expected, Convert.ToHexString(SHA256.HashData(bytes)));
        }
    }

    private static ReleaseCandidateResearchBuild Build(ManiaObject source, ManiaObject donor)
    {
        var timeline = Timeline();
        var observation = new LnObservation(donor, donor.StartTime / 500m, donor.EndTime!.Value / 500m);
        return new AddNotesEngine().BuildReleaseCandidatesForResearch(source,
            new LocalLnContext(source.StartTime / 500m, [observation],
                observation.ReleaseBeat - observation.HeadBeat), timeline, 4, applySourceAffinity: false);
    }

    private static BeatTimeline Timeline() => new([new TimingPoint(0, 500)]);

    private static string BeatmapText() => """
        osu file format v14

        [General]
        Mode:3

        [Metadata]
        Artist:Artist
        Title:Title
        Creator:Mapper
        Version:Hard

        [Difficulty]
        CircleSize:4

        [TimingPoints]
        0,500,4,2,0,100,1,0

        [HitObjects]
        64,192,0,128,0,1000:0:0:0:0:
        """;

    private static ManiaChart Chart(int keys, params ManiaObject[] objects) => new()
    {
        KeyCount = keys,
        Lines = [],
        OriginalObjects = objects.Select((x, i) => x with { Sequence = i }).ToArray(),
        TimingPoints = [new TimingPoint(0, 500)]
    };
}
