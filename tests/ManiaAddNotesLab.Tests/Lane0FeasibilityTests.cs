using System.Collections.Immutable;

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
            Occurrence("F", "GF", "EF", "P3", "Q", "R", "T", "S", [6], [], [8]),
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

    private static Lane0Occurrence Occurrence(string id, string group, string eventId, string parent,
        string query, string joint, string temporal, string spatial, int[] ids, int[]? releases = null,
        int[]? future = null) => new(Lane0Family.G1InteriorSpatial, "CHART", 7, id, group, eventId,
            parent, query, joint, temporal, spatial, ids.ToImmutableArray(),
            (releases ?? []).ToImmutableArray(), (future ?? []).ToImmutableArray());
}
