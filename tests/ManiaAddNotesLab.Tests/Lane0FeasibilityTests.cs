using System.Collections.Immutable;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class Lane0FeasibilityTests
{
    [Fact]
    public void CanonicalContractIdentitySurvivesPrettyJsonRoundTrip()
    {
        var contract = new Lane0Contract("s", "h", "i", "r", "c", "m", "q", ["f"], ["p"],
            ["l"], ["o"], "d", ImmutableSortedDictionary<string, string>.Empty.Add("z", "x"));
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(
                System.Text.Json.JsonNamingPolicy.CamelCase) }
        };
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<Lane0Contract>(
            System.Text.Json.JsonSerializer.Serialize(contract, options), options)!;
        Assert.Equal(Lane0FeasibilityRunner.CanonicalContractHash(contract),
            Lane0FeasibilityRunner.CanonicalContractHash(roundTrip));
    }

    [Fact]
    public void ExactJointSupportPreservesAlternativesAndMarginals()
    {
        var target = Occurrence("T", "GT", "ET", "P0", "Q", "TJ|SJ", "TJ", "SJ", [1, 2]);
        var exact = Occurrence("D1", "G1", "E1", "P1", "Q", "TJ|SJ", "TJ", "SJ", [3, 4]);
        var alternative = Occurrence("D2", "G2", "E2", "P2", "Q", "TA|SA", "TA", "SA", [5, 6]);
        var result = Lane0SpatialResearch.Evaluate(target, [target, exact, alternative]);
        Assert.Equal(Lane0HoldoutState.JointAmongAlternatives, result.State);
        Assert.True(result.JointSupported);
        Assert.True(result.IndependentSupported);
        Assert.Equal(2, result.DistinctAlternatives);
    }

    [Fact]
    public void MarginalFragmentsNeverBecomeJointSupport()
    {
        var target = Occurrence("T", "GT", "ET", "P0", "Q", "TJ|SJ", "TJ", "SJ", [1, 2]);
        var temporal = Occurrence("D1", "G1", "E1", "P1", "Q", "TJ|SX", "TJ", "SX", [3]);
        var spatial = Occurrence("D2", "G2", "E2", "P2", "Q", "TX|SJ", "TX", "SJ", [4]);
        var result = Lane0SpatialResearch.Evaluate(target, [temporal, spatial]);
        Assert.Equal(Lane0HoldoutState.Contradiction, result.State);
        Assert.True(result.TemporalMarginalSupported);
        Assert.True(result.SpatialMarginalSupported);
        Assert.False(result.JointSupported);
    }

    [Fact]
    public void EveryPreregisteredLeakageControlIsInducibleAndDetected()
    {
        var target = Occurrence("T", "GT", "ET", "P0", "Q", "R", "T", "S", [1, 2], [7], [8]);
        var candidates = new[]
        {
            target,
            Occurrence("G", "GT", "EG", "P1", "Q", "R", "T", "S", [3]),
            Occurrence("P", "GP", "EP", "P0", "Q", "R", "T", "S", [4]),
            Occurrence("R", "GR", "ER", "P2", "Q", "R", "T", "S", [5], [7]),
            Occurrence("F", "GF", "EF", "P3", "Q", "R", "T", "S", [8], [], [8]),
            Occurrence("E", "GE", "ET", "P4", "Q", "R", "T", "S", [9]),
            Occurrence("S", "GS", "ES", "P5", "Q", "R", "T", "S", [10]) with { IsSynthetic = true },
            Occurrence("C", "GC", "EC", "P6", "Q", "R", "T", "S", [11]) with { ChartId = "OTHER" },
            Occurrence("A", "GA", "EA", "P7", "Q", "R", "T", "S", [12]) with
                { ComponentOccurrenceIds = ["X", "Y"] },
            Occurrence("D", "GD", "ED", "P8", "Q", "R", "T", "S", [13, 13]),
            Occurrence("I", "GI", "EI", "P9", "Q", "R", "T", "S", [14]) with { CompleteIdentity = false }
        };
        var audit = Lane0SpatialResearch.Audit(target, candidates);
        Assert.True(audit.Target > 0); Assert.True(audit.TargetGroup > 0); Assert.True(audit.Parent > 0);
        Assert.True(audit.ReleaseEndpoint > 0); Assert.True(audit.FutureHeld > 0); Assert.True(audit.SameEvent > 0);
        Assert.True(audit.SyntheticTeaching > 0); Assert.True(audit.CrossChart > 0);
        Assert.True(audit.ArtificialComposition > 0); Assert.True(audit.DuplicateObservationIdentity > 0);
        Assert.True(audit.IncompleteOrMispairedIdentity > 0);
    }

    [Fact]
    public void WholeGroupAndParentHoldoutExcludeConstructiveEvidence()
    {
        var target = Occurrence("T", "G", "E", "P", "Q", "R", "T", "S", [1]);
        var sameGroup = Occurrence("D1", "G", "E2", "P2", "Q", "R", "T", "S", [2]);
        var sameParent = Occurrence("D2", "G2", "E3", "P", "Q", "R", "T", "S", [3]);
        var independent = Occurrence("D3", "G3", "E4", "P3", "Q", "R", "T", "S", [4]);
        var result = Lane0SpatialResearch.Evaluate(target, [sameGroup, sameParent, independent]);
        Assert.Single(result.Leakage.Eligible);
        Assert.Equal("D3", result.Leakage.Eligible[0].OccurrenceId);
        Assert.Equal(Lane0HoldoutState.JointUnique, result.State);
    }

    [Fact]
    public void RealG1OccurrenceBuilderReproducesAndRepairsMissingFutureHeldProvenance()
    {
        var chart = RepeatedG1Relations();
        var before = MapperEvidenceProfileBuilder.ComputeFingerprint(chart);
        var census = InteriorRelationFeasibilityResearch.Evaluate(chart);

        var historical = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", census,
            reproduceHistoricalDefect: true);
        var repaired = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", census);
        var historicalTarget = historical.OrderBy(x => x.AnchorTime ?? int.MinValue).First();
        var repairedTarget = repaired.OrderBy(x => x.AnchorTime).First();
        var repairedFuture = repaired.Where(x => x.QuerySignature == repairedTarget.QuerySignature)
            .OrderBy(x => x.AnchorTime).Skip(1).First();

        Assert.All(historical, x => Assert.Empty(x.FutureHeldObservationIds));
        Assert.All(historical, x => Assert.Null(x.AnchorTime));
        Assert.False(historicalTarget.FutureHeldObservationIds.Intersect(
            historical.Skip(1).SelectMany(x => x.ObservationIds)).Any());

        Assert.All(repaired, x => Assert.NotNull(x.AnchorTime));
        Assert.All(repaired, x => Assert.NotEmpty(x.FutureHeldObservationIds));
        Assert.True(repairedTarget.FutureHeldObservationIds.Intersect(repairedFuture.ObservationIds).Any());
        var audit = Lane0SpatialResearch.Audit(repairedTarget, [repairedFuture]);
        Assert.Equal(1, audit.FutureHeld);
        Assert.Empty(audit.Eligible);
        Assert.Equal(before, MapperEvidenceProfileBuilder.ComputeFingerprint(chart));
        Assert.Equal(0, census.ResearchRngCalls);
    }

    [Fact]
    public void CorrectedG1PipelineKeepsPriorDonorAndRejectsSameOrLaterAnchor()
    {
        var census = InteriorRelationFeasibilityResearch.Evaluate(RepeatedG1Relations());
        var occurrences = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", census);
        var bucket = occurrences.GroupBy(x => x.QuerySignature).OrderByDescending(x => x.Count()).First()
            .OrderBy(x => x.AnchorTime).ToArray();
        Assert.True(bucket.Length >= 3);

        var early = bucket[0];
        var middle = bucket[1];
        var late = bucket[2];
        var earlyAudit = Lane0SpatialResearch.Audit(early, [middle, late]);
        var lateAudit = Lane0SpatialResearch.Audit(late, [early]);
        var sameTime = middle with
        {
            OccurrenceId = middle.OccurrenceId + "-same-time",
            GroupId = middle.GroupId + "-independent",
            EventId = middle.EventId + "-independent",
            ParentId = middle.ParentId + "-independent",
            ObservationIds = [9001, 9002],
            ReleaseEndpointObservationIds = [],
            FutureHeldObservationIds = [9001, 9002]
        };
        var boundaryAudit = Lane0SpatialResearch.Audit(middle, [sameTime]);

        Assert.Equal(2, earlyAudit.FutureHeld);
        Assert.Empty(earlyAudit.Eligible);
        Assert.Equal(0, lateAudit.FutureHeld);
        Assert.Single(lateAudit.Eligible);
        Assert.Equal(1, boundaryAudit.FutureHeld);
        Assert.Empty(boundaryAudit.Eligible);
    }

    [Fact]
    public void RealG1BuilderPropagatesReleaseWitnessIdentity()
    {
        var chart = Chart(
            Ln(0, 0, 5000),
            Ln(1, 500, 2000),
            Ln(2, 2000, 3000));
        var census = InteriorRelationFeasibilityResearch.Evaluate(chart);
        var occurrences = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", census);

        var atRelease = occurrences.Where(x => x.AnchorTime == 2000).ToArray();
        Assert.NotEmpty(atRelease);
        Assert.All(atRelease, x => Assert.NotEmpty(x.ReleaseEndpointObservationIds));
        Assert.All(atRelease, x => Assert.True(x.ReleaseEndpointObservationIds
            .All(id => x.ObservationIds.Contains(id))));
    }

    [Theory]
    [InlineData(false, false, true, true, 0, true, true, 0, "LIMITED_PARK")]
    [InlineData(true, false, true, true, 0, true, true, 0, "FEASIBILITY_DEMONSTRATED")]
    [InlineData(true, true, true, true, 0, true, true, 0, "BLOCKED")]
    [InlineData(true, false, false, true, 0, true, true, 0, "INVALID")]
    [InlineData(true, false, true, false, 0, true, true, 0, "INVALID")]
    [InlineData(true, false, true, true, 1, true, true, 0, "INVALID")]
    [InlineData(true, false, true, true, 0, false, true, 0, "INVALID")]
    [InlineData(true, false, true, true, 0, true, false, 0, "INVALID")]
    [InlineData(true, false, true, true, 0, true, true, 1, "INVALID")]
    public void OutcomeClassifierSeparatesInsufficientEvidenceFromInvalidExecution(
        bool evidence, bool blocked, bool contract, bool controls, int integrity, bool deterministic,
        bool nonInterference, int rng, string expected)
    {
        var actual = Lane0OutcomeClassifier.Classify(new(evidence, blocked, contract, controls,
            integrity, deterministic, nonInterference, rng));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CorrectedBuilderPreservesDuplicateAndArtificialCompositionControls()
    {
        var census = InteriorRelationFeasibilityResearch.Evaluate(RepeatedG1Relations());
        var target = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", census).First();
        var duplicate = target with
        {
            OccurrenceId = "duplicate",
            GroupId = "duplicate-group",
            EventId = "duplicate-event",
            ParentId = "duplicate-parent",
            AnchorTime = target.AnchorTime - 1,
            ObservationIds = [42, 42]
        };
        var composition = duplicate with
        {
            OccurrenceId = "composition",
            ObservationIds = [43, 44],
            ComponentOccurrenceIds = ["temporal-source", "spatial-source"]
        };
        var audit = Lane0SpatialResearch.Audit(target, [duplicate, composition]);
        Assert.Equal(1, audit.DuplicateObservationIdentity);
        Assert.Equal(1, audit.ArtificialComposition);
        Assert.Empty(audit.Eligible);
    }

    private static ManiaChart RepeatedG1Relations() => Chart(
        Ln(0, 0, 4000), Ln(1, 1000, 2000),
        Ln(0, 5000, 9000), Ln(1, 6000, 7000),
        Ln(0, 10000, 14000), Ln(1, 11000, 12000));

    private static ManiaChart Chart(params ManiaObject[] objects) => new()
    {
        KeyCount = 7,
        Lines = [],
        OriginalObjects = objects.Select((x, i) => x with { Sequence = i }).ToArray(),
        TimingPoints = [new TimingPoint(0, 500)]
    };

    private static ManiaObject Ln(int lane, int start, int end) => ManiaObject.Ln(lane, start, end);

    private static Lane0Occurrence Occurrence(string id, string group, string eventId, string parent,
        string query, string joint, string temporal, string spatial, int[] ids, int[]? releases = null,
        int[]? future = null) => new(Lane0Family.G1InteriorSpatial, "CHART", 7, id, group, eventId,
            parent, query, joint, temporal, spatial, ids.ToImmutableArray(),
            (releases ?? []).ToImmutableArray(), (future ?? []).ToImmutableArray());
}
