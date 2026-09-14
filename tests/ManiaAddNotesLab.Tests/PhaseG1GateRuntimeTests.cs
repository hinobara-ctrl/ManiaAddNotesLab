using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseG1GateRuntimeTests
{
    [Fact]
    public void RuntimeGateUsesPlacedProposalConsumesNoRngAndNeverReplacesOrArticulates()
    {
        var chart = Fixture();
        var options = Options();
        var index = G1GateEvidenceIndex.Build(chart);
        var rng = new RecordingRandom(3);
        var result = new AddNotesEngine().Apply(chart, options, rng,
            MapperEvidenceProfileBuilder.Build(chart), null, null,
            G1GateRuntimeConfiguration.Frozen(index, "fixture"));
        var diagnostics = Assert.IsType<G1GateRuntimeDiagnostics>(result.G1GateDiagnostics);

        Assert.True(diagnostics.DirectDecisions.Length > 0, result.Trace ?? "no trace");
        Assert.Equal(0, diagnostics.GateRngCalls);
        Assert.Equal(index.ContentHash, diagnostics.EvidenceIndexHashAfter);
        Assert.All(diagnostics.DirectDecisions, decision =>
        {
            Assert.Equal(decision.RngPositionBeforeGate, decision.RngPositionAfterGate);
            Assert.Equal(decision.Admitted, decision.MutationCommitted);
            Assert.False(decision.ReplacementAttempted);
            Assert.False(decision.ArticulationIntentCreated);
        });
        Assert.All(diagnostics.DirectDecisions.Where(x => x.Admitted), decision =>
            Assert.Contains(result.ModifiedChart.AddedObjects, value => value.Lane == decision.SelectedLane
                && value.StartTime == decision.AnchorTime && value.EndTime == decision.CandidateEndTime));
        Assert.All(diagnostics.DirectDecisions.Where(x => !x.Admitted), decision =>
            Assert.DoesNotContain(result.ModifiedChart.AddedObjects, value =>
                value.Origin == AddedObjectOrigin.LnInteriorOpportunity
                && value.StartTime == decision.AnchorTime && value.EndTime == decision.CandidateEndTime
                && value.Lane == decision.SelectedLane));
        Assert.Empty(result.ModifiedChart.ArticulationReplacements);
    }

    [Fact]
    public void TreatmentIsDeterministicAndDefaultNullGateRemainsByteExactLegacy()
    {
        var chart = Fixture();
        var options = Options();
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var legacyA = new AddNotesEngine().Apply(chart, options, new RecordingRandom(8), profile);
        var legacyB = new AddNotesEngine().Apply(chart, options, new RecordingRandom(8), profile,
            null, null, null);
        Assert.Equal(OsuBeatmap.Write(legacyA.ModifiedChart, options.Chance),
            OsuBeatmap.Write(legacyB.ModifiedChart, options.Chance));

        var configuration = G1GateRuntimeConfiguration.Frozen(G1GateEvidenceIndex.Build(chart), "fixture");
        var firstRng = new RecordingRandom(8);
        var secondRng = new RecordingRandom(8);
        var first = new AddNotesEngine().Apply(chart, options, firstRng, profile, null, null, configuration);
        var second = new AddNotesEngine().Apply(chart, options, secondRng, profile, null, null, configuration);
        Assert.Equal(OsuBeatmap.Write(first.ModifiedChart, options.Chance),
            OsuBeatmap.Write(second.ModifiedChart, options.Chance));
        Assert.Equal(firstRng.Transcript, secondRng.Transcript);
        Assert.Equal(first.G1GateDiagnostics!.DirectDecisions.ToArray(),
            second.G1GateDiagnostics!.DirectDecisions.ToArray());
    }

    [Fact]
    public void RuntimeAbstainSuppressesOnlyExactLegacyProposalAtEqualRngPosition()
    {
        var chart = Fixture();
        var options = Options();
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var configuration = G1GateRuntimeConfiguration.Frozen(G1GateEvidenceIndex.Build(chart), "fixture");
        for (var seed = 1; seed <= 64; seed++)
        {
            var controlRng = new RecordingRandom(seed);
            var treatmentRng = new RecordingRandom(seed);
            var control = new AddNotesEngine().Apply(chart, options, controlRng, profile);
            var treatment = new AddNotesEngine().Apply(chart, options, treatmentRng, profile,
                null, null, configuration);
            var abstain = treatment.G1GateDiagnostics!.DirectDecisions.FirstOrDefault(x => !x.Admitted);
            if (abstain is null) continue;

            Assert.Equal(abstain.RngPositionBeforeGate, abstain.RngPositionAfterGate);
            Assert.Contains(control.ModifiedChart.AddedObjects, value =>
                value.Origin == AddedObjectOrigin.LnInteriorOpportunity
                && value.StartTime == abstain.AnchorTime && value.EndTime == abstain.CandidateEndTime
                && value.Lane == abstain.SelectedLane);
            Assert.DoesNotContain(treatment.ModifiedChart.AddedObjects, value =>
                value.Origin == AddedObjectOrigin.LnInteriorOpportunity
                && value.StartTime == abstain.AnchorTime && value.EndTime == abstain.CandidateEndTime
                && value.Lane == abstain.SelectedLane);
            Assert.Equal(control.Statistics.PlacedObjects - 1, treatment.Statistics.PlacedObjects);
            Assert.Equal(controlRng.Transcript, treatmentRng.Transcript);
            Assert.False(abstain.ReplacementAttempted);
            Assert.False(abstain.ArticulationIntentCreated);
            return;
        }
        Assert.Fail("The exact fixture never selected its known non-member proposal.");
    }

    [Fact]
    public void EvidenceIsOriginalOnlyChartLocalAndCacheCannotLeak()
    {
        var chart = Fixture();
        var synthetic = ManiaObject.Ln(6, 9000, 9500, true, 99,
            AddedObjectOrigin.LnInteriorOpportunity);
        var contaminated = Copy(chart, [synthetic, .. chart.OriginalObjects]);
        var clean = G1GateEvidenceIndex.Build(chart);
        var dirty = G1GateEvidenceIndex.Build(contaminated);
        Assert.Equal(clean.ChartFingerprint, dirty.ChartFingerprint);
        Assert.Equal(clean.ContentHash, dirty.ContentHash);
        Assert.Equal(1, dirty.IgnoredSyntheticObjects);

        var other = Copy(chart, [.. chart.OriginalObjects, ManiaObject.Tap(6, 8750) with { Sequence = 100 }]);
        Assert.Throws<InvalidOperationException>(() => clean.ValidateFor(other));
    }

    [Fact]
    public void FrozenMembershipMappingCoversUniqueAlternativeAndAllAbstentions()
    {
        var map = G1GateContractResearch.Create().MembershipMapping;
        Assert.Equal("ADMIT", map[nameof(InteriorRelationMembershipState.CandidateObservedUnique)]);
        Assert.Equal("ADMIT", map[nameof(InteriorRelationMembershipState.CandidateObservedAmongAlternatives)]);
        Assert.Equal("ABSTAIN", map[nameof(InteriorRelationMembershipState.CandidateNotObserved)]);
        Assert.Equal("ABSTAIN", map[nameof(InteriorRelationMembershipState.NoObservedRelation)]);
        Assert.Equal("ABSTAIN", map[nameof(InteriorRelationMembershipState.UnresolvableExactIdentity)]);
    }

    [Fact]
    public void RuntimeRejectsArticulationEligibilityDriftWrongChartAndRngWithoutPosition()
    {
        var chart = Fixture();
        var configuration = G1GateRuntimeConfiguration.Frozen(G1GateEvidenceIndex.Build(chart), "fixture");
        var engine = new AddNotesEngine();
        Assert.Throws<InvalidOperationException>(() => engine.Apply(chart,
            Options() with { ArticulationEnabled = true }, new RecordingRandom(1), null, null, null, configuration));
        Assert.Throws<InvalidOperationException>(() => engine.Apply(chart,
            Options() with { MaxInteriorOpportunitiesPerSource = 1 }, new RecordingRandom(1), null, null, null,
            configuration));
        Assert.Throws<InvalidOperationException>(() => engine.Apply(chart, Options(), new ZeroRandom(),
            null, null, null, configuration));
        var other = Copy(chart, [.. chart.OriginalObjects, ManiaObject.Tap(6, 8750) with { Sequence = 100 }]);
        Assert.Throws<InvalidOperationException>(() => engine.Apply(other, Options(), new RecordingRandom(1),
            null, null, null, configuration));
    }

    [Fact]
    public void AdversarialRuntimeTraceDetectsEveryForbiddenDirectMutationMechanism()
    {
        var clean = new G1GateDirectDecision("OP", "P", "P", "P", new OriginalObservationId(1),
            1000, 2m, 2000, 4m, 2, 2, InteriorRelationMembershipState.CandidateObservedUnique,
            true, true, false, false, false, 10, 10, "G", "S");
        Assert.Equal(0, G1GateInvariantAuditResearch.Audit(clean).Total);
        Assert.True(G1GateInvariantAuditResearch.Audit(clean with { EvaluatedSemanticId = "OTHER" })
            .CandidateSubstitutionViolations > 0);
        Assert.True(G1GateInvariantAuditResearch.Audit(clean with { CommittedSemanticId = "OTHER" })
            .AdmitReconstructionViolations > 0);
        Assert.True(G1GateInvariantAuditResearch.Audit(clean with { CommittedLane = 3 })
            .LaneSubstitutionViolations > 0);
        Assert.True(G1GateInvariantAuditResearch.Audit(clean with { RngPositionAfterGate = 11 })
            .RngConsumptionViolations > 0);
        Assert.True(G1GateInvariantAuditResearch.Audit(clean with { ReplacementAttempted = true })
            .RerollViolations > 0);
        Assert.True(G1GateInvariantAuditResearch.Audit(clean with { ArticulationIntentCreated = true })
            .ArticulationRoutingViolations > 0);
        Assert.True(G1GateInvariantAuditResearch.Audit(clean with { MutationObservedBeforeDecision = true })
            .MutationBeforeDecisionViolations > 0);
        var abstain = clean with { MembershipState = InteriorRelationMembershipState.CandidateNotObserved,
            Admitted = false, MutationCommitted = false, CommittedSemanticId = null, CommittedLane = null };
        Assert.Equal(0, G1GateInvariantAuditResearch.Audit(abstain).Total);
        Assert.True(G1GateInvariantAuditResearch.Audit(abstain with { MutationCommitted = true,
            CommittedSemanticId = "P", CommittedLane = 2 }).AbstainMutationViolations > 0);
    }

    private static AddNotesOptions Options() => InteriorRelationMembershipResearch.CurrentOptions() with
    {
        Chance = 1,
        ArticulationEnabled = false,
        ContextualDensityNormalizationEnabled = false,
        StartMs = 1500,
        EndMs = 1500
        ,Trace = true
    };

    private static ManiaChart Fixture() => new()
    {
        KeyCount = 7,
        Lines = [],
        OriginalObjects = new[]
        {
            ManiaObject.Ln(0, 0, 4000),
            ManiaObject.Ln(1, 500, 1500),
            ManiaObject.Ln(2, 1000, 2500),
            ManiaObject.Ln(3, 1500, 3000)
        }.Select((x, i) => x with { Sequence = i }).ToArray(),
        TimingPoints = [new TimingPoint(0, 500)]
    };

    private static ManiaChart Copy(ManiaChart source, IReadOnlyList<ManiaObject> originals) => new()
    {
        KeyCount = source.KeyCount,
        Lines = source.Lines,
        OriginalObjects = originals,
        TimingPoints = source.TimingPoints
    };

    private sealed class RecordingRandom(int seed) : IRandomPositionSource
    {
        private readonly SeededRandom inner = new(seed);
        public List<string> Transcript { get; } = [];
        public long CallCount => Transcript.Count;
        public double NextDouble()
        {
            var value = inner.NextDouble();
            Transcript.Add($"D:{value:R}");
            return value;
        }
        public int Next(int maximum)
        {
            var value = inner.Next(maximum);
            Transcript.Add($"I:{maximum}:{value}");
            return value;
        }
    }

    private sealed class ZeroRandom : IRandomSource
    {
        public double NextDouble() => 0;
        public int Next(int maxExclusive) => 0;
    }
}
