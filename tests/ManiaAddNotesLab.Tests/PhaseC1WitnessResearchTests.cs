using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseC1WitnessResearchTests
{
    [Fact]
    public void OneObservationWithDurationAndReleaseHasOneAuthorityUnit()
    {
        var source = ManiaObject.Ln(0, 0, 1000, sequence: 0);
        var observation = ManiaObject.Ln(1, 0, 1000, sequence: 1);
        var candidate = Assert.Single(Candidates(source, [Observation(observation)]));

        Assert.Equal(2, candidate.Evidence.EvidencePathCount);
        Assert.Equal(1, candidate.Evidence.IndependentWitnessCount);
        Assert.Equal(1, candidate.Evidence.DuplicatePathCount);
        Assert.Equal(2.5, candidate.Evidence.LegacyWeight);
        Assert.Equal(1.25, candidate.Evidence.DeduplicatedObservationWeight);
    }

    [Fact]
    public void TwoObservationsRemainTwoIndependentAuthorityUnits()
    {
        var source = ManiaObject.Ln(0, 0, 1000, sequence: 0);
        var candidates = Candidates(source,
            [Observation(ManiaObject.Ln(1, 0, 1000, sequence: 1)),
             Observation(ManiaObject.Ln(2, 0, 1000, sequence: 2))]);
        var candidate = Assert.Single(candidates);

        Assert.Equal(4, candidate.Evidence.EvidencePathCount);
        Assert.Equal(2, candidate.Evidence.IndependentWitnessCount);
        Assert.Equal(5, candidate.Evidence.LegacyWeight);
        Assert.Equal(2.5, candidate.Evidence.DeduplicatedObservationWeight);
    }

    [Fact]
    public void EveryNoDuplicateCandidateKeepsExactWeight()
    {
        var source = ManiaObject.Ln(0, 0, 1000, sequence: 0);
        var candidates = Candidates(source,
            [Observation(ManiaObject.Ln(1, 500, 1000, sequence: 1))]);

        Assert.NotEmpty(candidates);
        Assert.All(candidates, candidate =>
        {
            Assert.Equal(0, candidate.Evidence.DuplicatePathCount);
            Assert.Equal(candidate.Evidence.LegacyWeight,
                candidate.Evidence.DeduplicatedObservationWeight);
        });
    }

    [Fact]
    public void DuplicateCandidateCanChangeRankingAgainstCleanCandidateWithoutChangingVocabulary()
    {
        var source = ManiaObject.Ln(0, 0, 1000, sequence: 0);
        var observations = new List<LnObservation>
        {
            Observation(ManiaObject.Ln(1, 0, 1000, sequence: 1))
        };
        for (var lane = 2; lane < 6; lane++)
            observations.Add(Observation(ManiaObject.Ln(lane, 1000, 1500, sequence: lane)));
        var candidates = Candidates(source, observations);
        var legacyTop = candidates.OrderByDescending(x => x.Evidence.LegacyWeight)
            .ThenBy(x => x.EndTime).First();
        var deduplicatedTop = candidates.OrderByDescending(x => x.Evidence.DeduplicatedObservationWeight)
            .ThenBy(x => x.EndTime).First();

        Assert.Equal(1000, legacyTop.EndTime);
        Assert.Equal(500, deduplicatedTop.EndTime);
        Assert.Equal(candidates.Select(x => x.EndTime), candidates.Select(x => x.EndTime));
    }

    [Fact]
    public void SourceAffinityMultipliesBothAggregationsWithoutBeingRedefined()
    {
        var source = ManiaObject.Ln(0, 0, 1000, sequence: 0);
        var candidate = Assert.Single(Candidates(source,
            [Observation(ManiaObject.Ln(1, 0, 1000, sequence: 1))]));

        Assert.Equal(AddNotesEngine.SourceAffinityMultiplier, candidate.Evidence.SourceAffinity);
        Assert.Equal(2 * AddNotesEngine.SourceAffinityMultiplier, candidate.Evidence.LegacyWeight);
        Assert.Equal(AddNotesEngine.SourceAffinityMultiplier,
            candidate.Evidence.DeduplicatedObservationWeight);
    }

    [Fact]
    public void DistanceWeightRemainsObservationLocalAndUnchanged()
    {
        var source = ManiaObject.Ln(0, 0, 1000, sequence: 0);
        var candidates = Candidates(source,
        [
            Observation(ManiaObject.Ln(1, 0, 1000, sequence: 1)),
            Observation(ManiaObject.Ln(2, 1000, 2000, sequence: 2))
        ]);
        var candidate = Assert.Single(candidates, x => x.EndTime == 1000);

        Assert.Equal(3.25, candidate.Evidence.LegacyWeight, 10);
        Assert.Equal(2.0, candidate.Evidence.DeduplicatedObservationWeight, 10);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void ShadowAggregationHasNoKeyCountBranch(int keys)
    {
        var objects = new List<ManiaObject>
        {
            ManiaObject.Ln(0, 0, 1000, sequence: 0)
        };
        if (keys > 1) objects.Add(ManiaObject.Ln(1, 0, 1000, sequence: 1));
        var chart = Chart(keys, objects);
        var result = new AddNotesEngine().Apply(chart, new AddNotesOptions
        {
            Chance = 1,
            ContextualDensityNormalizationEnabled = false,
            DensityDecayPerColumn = 1,
            DiagnosticsEnabled = true
        }, new SeededRandom(3));

        Assert.All(result.DecisionDiagnostics!.Decisions.Where(x => x.CandidateKind ==
            DiagnosticCandidateKind.LongNote), x => Assert.Equal(
            LnWitnessDeduplicationResearch.DeduplicatedRule, x.C1ShadowAggregationRule));
    }

    [Fact]
    public void HeldOutTargetCannotVoteForItself()
    {
        var result = LnWitnessDeduplicationResearch.Evaluate(
            Chart(4, [ManiaObject.Ln(0, 0, 1000, sequence: 0)]));

        Assert.Equal(1, result.TargetsEvaluated);
        Assert.Equal(0, result.TargetsWithCandidateVocabulary);
        Assert.Equal(0, result.TrueTargetCandidatePresent);
    }

    [Fact]
    public void DiagnosticsExposeBothWeightsLabelsAndPerObservationContributions()
    {
        var chart = Chart(4,
        [
            ManiaObject.Ln(0, 0, 1000, sequence: 0),
            ManiaObject.Ln(1, 0, 1000, sequence: 1)
        ]);
        var result = new AddNotesEngine().Apply(chart, new AddNotesOptions
        {
            Chance = 1,
            ContextualDensityNormalizationEnabled = false,
            DensityDecayPerColumn = 1,
            DiagnosticsEnabled = true
        }, new SeededRandom(2));
        var decision = result.DecisionDiagnostics!.Decisions.First(x => x.LegacyWeight == 5);

        Assert.Equal(2.5, decision.C1ShadowWeight);
        Assert.Equal(2, decision.IndependentWitnessCount);
        Assert.Equal(2, decision.DuplicateEvidencePaths);
        Assert.Equal(2, decision.PerObservationContributions!.Value.Length);
        Assert.All(decision.PerObservationContributions.Value, contribution =>
        {
            Assert.Equal(new[] { "Duration", "ExactRelease" }, contribution.EvidenceLabels.ToArray());
            Assert.Equal(2, contribution.LegacyPathContributionBeforeAffinity);
            Assert.Equal(1, contribution.DeduplicatedAuthorityBeforeAffinity);
        });
        Assert.Equal("LegacyPathSum", decision.ActiveAggregationRule);
        Assert.Equal(LnWitnessDeduplicationResearch.DeduplicatedRule,
            decision.C1ShadowAggregationRule);
    }

    [Fact]
    public void RareTimingVocabularyIsSharedByBothShadowRankings()
    {
        var timeline = new BeatTimeline([new TimingPoint(37, 500)]);
        var source = ManiaObject.Ln(0, 137, 637, sequence: 0);
        var observations = new[]
        {
            new LnObservation(ManiaObject.Ln(1, 237, 337, sequence: 1), .4m, .6m),
            new LnObservation(ManiaObject.Ln(2, 287, 337, sequence: 2), .5m, .6m)
        };
        var context = new LocalLnContext(.2m, observations, .1m);
        var candidates = new AddNotesEngine().BuildReleaseCandidates(source, context, timeline, 4,
            applySourceAffinity: false);

        Assert.Contains(candidates, x => x.EndBeat - context.SourceHeadBeat == .1m);
        Assert.Contains(candidates, x => x.EndBeat - context.SourceHeadBeat == .2m);
        Assert.All(candidates.Where(x => x.Evidence.DuplicatePathCount == 0), x =>
            Assert.Equal(x.Evidence.LegacyWeight, x.Evidence.DeduplicatedObservationWeight));
    }

    private static IReadOnlyList<ReleaseCandidate> Candidates(ManiaObject source,
        IReadOnlyList<LnObservation> observations)
    {
        var timeline = Timeline();
        var context = new LocalLnContext(0, observations,
            observations.Min(x => x.ReleaseBeat - x.HeadBeat));
        return new AddNotesEngine().BuildReleaseCandidates(source, context, timeline, 4);
    }

    private static LnObservation Observation(ManiaObject value) => new(value,
        value.StartTime / 500m, value.EndTime!.Value / 500m);

    private static BeatTimeline Timeline() => new([new TimingPoint(0, 500)]);

    private static ManiaChart Chart(int keys, IReadOnlyList<ManiaObject> objects) => new()
    {
        KeyCount = keys,
        Lines = [],
        OriginalObjects = objects,
        TimingPoints = [new TimingPoint(0, 500)]
    };
}
