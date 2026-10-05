using System.Collections.Immutable;
using System.Text.Json;
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
        var target = Occurrence("T", "GT", "ET", "P0", "Q", "TJ|SJ", "TJ", "SJ", [1, 2], anchorTime: 2000);
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
        var target = Occurrence("T", "GT", "ET", "P0", "Q", "TJ|SJ", "TJ", "SJ", [1, 2], anchorTime: 2000);
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
        var target = Occurrence("T", "GT", "ET", "P0", "Q", "R", "T", "S", [1, 2], [7], [8], 2000);
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
        Assert.True(audit.ReleaseEndpoint > 0); Assert.True(audit.UniqueFutureHeldExcluded > 0);
        Assert.True(audit.SameEvent > 0);
        Assert.True(audit.SyntheticTeaching > 0); Assert.True(audit.CrossChart > 0);
        Assert.True(audit.ArtificialComposition > 0); Assert.True(audit.DuplicateObservationIdentity > 0);
        Assert.True(audit.IncompleteOrMispairedIdentity > 0);
    }

    [Fact]
    public void WholeGroupAndParentHoldoutExcludeConstructiveEvidence()
    {
        var target = Occurrence("T", "G", "E", "P", "Q", "R", "T", "S", [1], anchorTime: 2000);
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
        Assert.Equal(1, audit.TemporalExclusion);
        Assert.Equal(1, audit.FutureHeldIdentityExclusion);
        Assert.Equal(1, audit.BothTemporalAndIdentity);
        Assert.Equal(1, audit.UniqueExcluded);
        Assert.Empty(audit.Eligible);
        Assert.Equal(before, MapperEvidenceProfileBuilder.ComputeFingerprint(chart));
        Assert.Equal(0, census.ResearchRngCalls);
    }

    [Fact]
    public void HistoricalEvaluationModePreservesPublishedMissingTimeSemantics()
    {
        var census = InteriorRelationFeasibilityResearch.Evaluate(RepeatedG1Relations());
        var historical = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", census,
            reproduceHistoricalDefect: true);
        var trials = Lane0SpatialResearch.EvaluateAll(historical, requireTemporalIntegrity: false);

        Assert.All(historical, x => Assert.Null(x.AnchorTime));
        Assert.DoesNotContain(trials, x => x.State == Lane0HoldoutState.AmbiguousIntegrity);
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

        Assert.Equal(2, earlyAudit.TemporalExclusion);
        Assert.Equal(2, earlyAudit.FutureHeldIdentityExclusion);
        Assert.Equal(2, earlyAudit.BothTemporalAndIdentity);
        Assert.Equal(2, earlyAudit.UniqueExcluded);
        Assert.Empty(earlyAudit.Eligible);
        Assert.Equal(0, lateAudit.TemporalExclusion);
        Assert.Equal(0, lateAudit.FutureHeldIdentityExclusion);
        Assert.Single(lateAudit.Eligible);
        Assert.Equal(1, boundaryAudit.TemporalExclusion);
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
        var target = atRelease[0];
        var releaseCollision = target with
        {
            OccurrenceId = "release-control", GroupId = "release-group", EventId = "release-event",
            ParentId = "release-parent", ObservationIds = [9001, 9002],
            AnchorTime = target.AnchorTime - 1
        };
        Assert.Equal(1, Lane0SpatialResearch.Audit(target, [releaseCollision]).ReleaseEndpoint);
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

    [Fact]
    public void OfficialBuilderExposesFourOrthogonalTemporalIdentityStatesWithoutDoubleCounting()
    {
        var census = InteriorRelationFeasibilityResearch.Evaluate(RepeatedG1Relations());
        var bucket = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", census)
            .GroupBy(x => x.QuerySignature).OrderByDescending(x => x.Count()).First()
            .OrderBy(x => x.AnchorTime).ToArray();
        var prior = bucket[0];
        var target = bucket[1];
        var future = bucket[2];
        var futureIdentity = target.FutureHeldObservationIds.First();
        var temporalOnly = future with
        {
            OccurrenceId = "temporal-only", GroupId = "tg", EventId = "te", ParentId = "tp",
            ObservationIds = [9001, 9002], ReleaseEndpointObservationIds = []
        };
        var identityOnly = prior with
        {
            OccurrenceId = "identity-only", GroupId = "ig", EventId = "ie", ParentId = "ip",
            ObservationIds = [futureIdentity, 9003], ReleaseEndpointObservationIds = []
        };
        var both = future with
        {
            OccurrenceId = "both", GroupId = "bg", EventId = "be", ParentId = "bp"
        };
        var audit = Lane0SpatialResearch.Audit(target, [prior, temporalOnly, identityOnly, both]);

        Assert.Equal(4, audit.DonorsConsidered);
        Assert.Single(audit.Eligible);
        Assert.Equal(3, audit.UniqueExcluded);
        Assert.Equal(2, audit.TemporalExclusion);
        Assert.Equal(2, audit.FutureHeldIdentityExclusion);
        Assert.Equal(1, audit.BothTemporalAndIdentity);
        Assert.Equal(3, audit.UniqueFutureHeldExcluded);
        Assert.True(audit.PartitionValid);
        Assert.Equal(4, audit.Eligible.Length + audit.UniqueExcluded);
    }

    [Fact]
    public void FutureHeldUnionAndGlobalExclusionRemainDistinctAcrossFiveDonorStates()
    {
        var census = InteriorRelationFeasibilityResearch.Evaluate(RepeatedG1Relations());
        var bucket = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", census)
            .GroupBy(x => x.QuerySignature).OrderByDescending(x => x.Count()).First()
            .OrderBy(x => x.AnchorTime).ToArray();
        var prior = bucket[0];
        var target = bucket[1];
        var future = bucket[2];
        var futureIdentity = target.FutureHeldObservationIds.First();
        var temporalOnly = future with
        {
            OccurrenceId = "temporal-only", GroupId = "tg", EventId = "te", ParentId = "tp",
            ObservationIds = [9001, 9002], ReleaseEndpointObservationIds = []
        };
        var identityOnly = prior with
        {
            OccurrenceId = "identity-only", GroupId = "ig", EventId = "ie", ParentId = "ip",
            ObservationIds = [futureIdentity, 9003], ReleaseEndpointObservationIds = []
        };
        var both = future with
        {
            OccurrenceId = "both", GroupId = "bg", EventId = "be", ParentId = "bp"
        };
        var otherBarrier = prior with
        {
            OccurrenceId = "other", GroupId = target.GroupId, EventId = "oe", ParentId = "op",
            ObservationIds = [9010, 9011], ReleaseEndpointObservationIds = []
        };

        AssertAudit(prior, 0, 0, 0, 0, 0, 1);
        AssertAudit(temporalOnly, 1, 0, 0, 1, 1, 0);
        AssertAudit(identityOnly, 0, 1, 0, 1, 1, 0);
        AssertAudit(both, 1, 1, 1, 1, 1, 0);
        AssertAudit(otherBarrier, 0, 0, 0, 0, 1, 0);

        void AssertAudit(Lane0Occurrence donor, int temporal, int identity, int bothReasons,
            int uniqueFutureHeld, int uniqueExcluded, int eligible)
        {
            var audit = Lane0SpatialResearch.Audit(target, [donor]);
            Assert.Equal(temporal, audit.TemporalExclusion);
            Assert.Equal(identity, audit.FutureHeldIdentityExclusion);
            Assert.Equal(bothReasons, audit.BothTemporalAndIdentity);
            Assert.Equal(uniqueFutureHeld, audit.UniqueFutureHeldExcluded);
            Assert.Equal(uniqueExcluded, audit.UniqueExcluded);
            Assert.Equal(1, audit.DonorsConsidered);
            Assert.Equal(eligible, audit.Eligible.Length);
            Assert.True(audit.PartitionValid);
            Assert.Equal(audit.DonorsConsidered, audit.Eligible.Length + audit.UniqueExcluded);
        }
    }

    [Fact]
    public void MissingG1AnchorTimeIsExplicitIntegrityFailure()
    {
        var census = InteriorRelationFeasibilityResearch.Evaluate(RepeatedG1Relations());
        var occurrences = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", census);
        var target = occurrences[0] with { AnchorTime = null };
        var result = Lane0SpatialResearch.Evaluate(target, occurrences.Skip(1));

        Assert.Equal(Lane0HoldoutState.AmbiguousIntegrity, result.State);
        Assert.False(result.IndependentSupported);
    }

    [Fact]
    public void OfficialBuilderOccurrencesDriveNonTemporalAdversarialControls()
    {
        var census = InteriorRelationFeasibilityResearch.Evaluate(RepeatedG1Relations());
        var built = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", census)
            .OrderBy(x => x.AnchorTime).ToArray();
        var target = built[^1];
        var prior = built[0];
        var candidates = new[]
        {
            target,
            prior with { OccurrenceId = "group", GroupId = target.GroupId },
            prior with { OccurrenceId = "parent", ParentId = target.ParentId },
            prior with { OccurrenceId = "event", EventId = target.EventId },
            prior with { OccurrenceId = "synthetic", IsSynthetic = true },
            prior with { OccurrenceId = "cross-chart", ChartId = "OTHER" },
            prior with { OccurrenceId = "composition", ComponentOccurrenceIds = ["T", "S"] }
        };
        var audit = Lane0SpatialResearch.Audit(target, candidates);

        Assert.True(audit.Target > 0);
        Assert.True(audit.TargetGroup > 0);
        Assert.True(audit.Parent > 0);
        Assert.True(audit.SameEvent > 0);
        Assert.True(audit.SyntheticTeaching > 0);
        Assert.True(audit.CrossChart > 0);
        Assert.True(audit.ArtificialComposition > 0);
        Assert.True(audit.PartitionValid);
        Assert.Empty(audit.Eligible);
    }

    [Fact]
    public void IndexedFutureHeldBuilderEqualsSimpleBoundedReferenceAndSharesAnchorSnapshot()
    {
        var chart = Chart(Ln(0, 0, 5000), Ln(1, 1000, 2000), Ln(2, 1000, 2500),
            Ln(0, 6000, 11000), Ln(1, 7000, 8000));
        var census = InteriorRelationFeasibilityResearch.Evaluate(chart);
        var occurrences = Lane0FeasibilityRunner.BuildG1Occurrences("CHART", census);
        var anchors = census.StructuralAnchors.ToDictionary(x => x.AnchorId, StringComparer.Ordinal);

        foreach (var occurrence in census.CompleteRelations)
        {
            var expected = census.CompleteRelations.Where(x => x.AnchorTime >= occurrence.AnchorTime)
                .SelectMany(x =>
                {
                    var anchor = anchors[$"{x.ParentLongNoteId}-A{x.AnchorTime}-B{x.AnchorBeat}"];
                    return new[] { x.ParentLongNoteId.Value, x.WitnessLongNoteId.Value }
                        .Concat(anchor.HeadWitnessIds.Select(id => id.Value))
                        .Concat(anchor.ReleaseWitnessIds.Select(id => id.Value));
                }).Distinct().Order().ToArray();
            var actual = occurrences.Single(x => x.OccurrenceId == occurrence.OccurrenceId);
            Assert.Equal(expected, actual.FutureHeldObservationIds);
        }
        foreach (var group in occurrences.GroupBy(x => x.AnchorTime).Where(x => x.Count() > 1))
        {
            var values = group.ToArray();
            Assert.True(values.Skip(1).All(x => x.FutureHeldObservationIds == values[0].FutureHeldObservationIds));
        }
    }

    [Theory]
    [InlineData(false, true, true, true, true, true, true, true, "INVALID")]
    [InlineData(true, false, true, true, true, true, true, true, "INVALID")]
    [InlineData(true, true, false, true, true, true, true, true, "INVALID")]
    [InlineData(true, true, true, false, true, true, true, true, "INVALID")]
    [InlineData(true, true, true, true, false, false, false, false, "BLOCKED")]
    [InlineData(true, true, true, true, true, true, true, false, "BLOCKED")]
    [InlineData(true, true, true, true, true, true, true, true, "READY_FOR_AUTHORIZED_CORRECTIVE_EXECUTION")]
    public void CorrectiveReadinessUsesVerifiedConditionsAndPrecedence(bool contract, bool implementation,
        bool harness, bool dependencies, bool binding, bool head, bool authorized, bool manifest,
        string expected)
    {
        var report = Lane0FeasibilityRunner.ClassifyCorrectiveReadiness(new(contract, implementation,
            harness, dependencies, binding, head, authorized, manifest));
        Assert.Equal(expected, report.Outcome);
    }

    [Fact]
    public void HistoricalV2RouteRejectsChangedInstrumentIdentity()
    {
        var root = FindRepositoryRoot();
        var contract = Path.Combine(root, "docs", "lane_0_future_held_hardening_contract.json");
        var readiness = Lane0FeasibilityRunner.ValidateCorrectiveReadiness(root, contract, null, null);

        Assert.Equal("INVALID", readiness.Outcome);
        Assert.True(readiness.Checks.ContractCanonical);
        Assert.False(readiness.Checks.ImplementationIdentity);
        Assert.False(readiness.Checks.HarnessIdentity);
    }

    [Fact]
    public void HistoricalV2ExactByteDependencyDriftRemainsFailClosed()
    {
        var readiness = Lane0FeasibilityRunner.ClassifyCorrectiveReadiness(new(
            ContractCanonical: true,
            ImplementationIdentity: true,
            HarnessIdentity: true,
            ReusedDependencies: false,
            PublicationBindingPresent: true,
            PublishedHeadMatches: true,
            HumanAuthorized: true,
            ManifestVerified: true));

        Assert.Equal("INVALID", readiness.Outcome);
    }

    [Fact]
    public void HistoricalV2ContractDeclaresExactByteDependencySemantics()
    {
        var path = Path.Combine(FindRepositoryRoot(), "docs",
            "lane_0_future_held_hardening_contract.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var canonicalization = document.RootElement.GetProperty("canonicalization").GetString();

        Assert.Contains("file hashes use exact working-tree bytes", canonicalization,
            StringComparison.Ordinal);
        Assert.Contains("CRLF/LF changes identity", canonicalization, StringComparison.Ordinal);
    }

    [Fact]
    public void CounterClosureHistoricalRouteRejectsCurrentHarnessDriftBeforeManifest()
    {
        var root = FindRepositoryRoot();
        var contract = Path.Combine(root, "docs", "lane_0_future_held_counter_closure_contract.json");
        var readiness = Lane0FeasibilityRunner.ValidateCounterClosureReadiness(root, contract, null, null);
        var execution = Lane0FeasibilityRunner.DenyCounterClosureExecution(root, contract, null, null);

        Assert.Equal("INVALID", readiness.Outcome);
        Assert.True(readiness.Checks.ContractCanonical);
        Assert.True(readiness.Checks.ImplementationIdentity);
        Assert.False(readiness.Checks.HarnessIdentity);
        Assert.True(readiness.Checks.ReusedDependencies);
        Assert.False(readiness.Checks.PublicationBindingPresent);
        Assert.False(readiness.Checks.ManifestVerified);
        Assert.Equal("INVALID", execution.Outcome);
    }

    [Fact]
    public void CounterClosureRouteRejectsTamperedCanonicalIdentityBeforeCorpusAccess()
    {
        var root = FindRepositoryRoot();
        var contract = Path.Combine(root, "docs", "lane_0_future_held_counter_closure_contract.json");
        var temporary = Path.Combine(Path.GetTempPath(), $"lane0-counter-{Guid.NewGuid():N}.json");
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(contract))!;
            node["canonicalSha256"] = new string('0', 64);
            File.WriteAllText(temporary, node.ToJsonString());
            var readiness = Lane0FeasibilityRunner.ValidateCounterClosureReadiness(
                root, temporary, null, null);

            Assert.Equal("INVALID", readiness.Outcome);
            Assert.False(readiness.Checks.ContractCanonical);
            Assert.False(readiness.Checks.ManifestVerified);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
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

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ManiaAddNotesLab.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static Lane0Occurrence Occurrence(string id, string group, string eventId, string parent,
        string query, string joint, string temporal, string spatial, int[] ids, int[]? releases = null,
        int[]? future = null, int anchorTime = 1000) => new(Lane0Family.G1InteriorSpatial, "CHART", 7, id, group, eventId,
            parent, query, joint, temporal, spatial, ids.ToImmutableArray(),
            (releases ?? []).ToImmutableArray(), (future ?? []).ToImmutableArray(), AnchorTime: anchorTime);
}
