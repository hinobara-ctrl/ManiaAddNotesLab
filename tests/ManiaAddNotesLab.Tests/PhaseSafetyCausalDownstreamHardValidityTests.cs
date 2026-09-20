using ManiaAddNotesLab.Core;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseSafetyCausalDownstreamHardValidityTests
{
    [Fact]
    public void MinimalRuntimeCounterexampleProvesMaterializationMismatch()
    {
        var latentEnd = 0.99999999999999999999999999m;
        var index = new LaneGeometryIndex(1,
            [new TimedManiaObject(ManiaObject.Ln(0, 0, 500), 0, latentEnd)]);

        var placement = index.InspectTapLane(0, 1);
        var oracle = GeometrySafetyAttributionResearch.AuditSnapshot("MINIMAL", 1,
        [
            new GeometrySafetyObject("LN", ManiaObject.Ln(0, 0, 500), 0, 1),
            new GeometrySafetyObject("TAP", ManiaObject.Tap(0, 500), 1, 1)
        ]);

        Assert.True(placement.IsLegal);
        Assert.Equal(latentEnd, placement.Previous!.EndBeat);
        Assert.Equal(GeometryHardViolationKind.TapOnHeldLongNote,
            Assert.Single(oracle.HardViolations).Violation);
    }

    [Fact]
    public void BadControlEndpointEqualityIsRejectedByBothRealAuthorities()
    {
        var index = new LaneGeometryIndex(1,
            [new TimedManiaObject(ManiaObject.Ln(0, 0, 500), 0, 1)]);
        Assert.False(index.InspectTapLane(0, 1).IsLegal);
        var oracle = GeometrySafetyAttributionResearch.AuditSnapshot("ENDPOINT", 1,
        [
            new GeometrySafetyObject("LN", ManiaObject.Ln(0, 0, 500), 0, 1),
            new GeometrySafetyObject("TAP", ManiaObject.Tap(0, 500), 1, 1)
        ]);
        Assert.Single(oracle.HardViolations);
    }

    [Fact]
    public void BadControlStaleGeometryCannotDescribeCurrentPlacement()
    {
        var index = new LaneGeometryIndex(1,
            [new TimedManiaObject(ManiaObject.Ln(0, 0, 400), 0, .8m)]);
        Assert.True(index.InspectTapLane(0, 1).IsLegal);
        index.Insert(new TimedManiaObject(ManiaObject.Ln(0, 450, 600), .9m, 1.2m));
        var current = index.InspectTapLane(0, 1);
        Assert.False(current.IsLegal);
        Assert.Equal(600, current.Previous!.Object.EndTime);
    }

    [Fact]
    public void BadControlTemporalAfterReconvergenceNeverReinheritsOldDivergence()
    {
        var control = new[]
        {
            Pair("DIRECT", "place", "S0", "C1"),
            Pair("RECONVERGED", "same", "R", "R2"),
            Pair("UNRELATED", "left", "R2", "U1"),
            Pair("AFTER", "left-after", "U1", "U2")
        };
        var treatment = new[]
        {
            Pair("DIRECT", "abstain", "S0", "T1", direct: true,
                disposition: GenerationDecisionDisposition.ExperimentalAbstain),
            Pair("RECONVERGED", "same", "R", "R2"),
            Pair("UNRELATED", "right", "R2", "V1"),
            Pair("AFTER", "right-after", "V1", "V2")
        };
        var result = ProvenancePairComparatorResearch.Compare("BAD-RECONVERGENCE", control, treatment);
        Assert.Contains("RECONVERGED", result.ReconvergenceOpportunityKeys);
        Assert.DoesNotContain("AFTER", result.DownstreamAffectedOpportunityKeys);
        Assert.Contains("AFTER", result.TemporalAfterButNotDownstreamOpportunityKeys);
    }

    [Fact]
    public void BadControlHiddenIncompleteStateForcesAbstention()
    {
        var control = new[] { Pair("DIRECT", "place", "S0", "C1"),
            Pair("LATER", "same", "C1", "C2", geometry: "G", complete: false) };
        var treatment = new[] { Pair("DIRECT", "abstain", "S0", "T1", direct: true,
                disposition: GenerationDecisionDisposition.ExperimentalAbstain),
            Pair("LATER", "same", "T1", "T2", geometry: "G", complete: false) };
        Assert.False(ProvenancePairComparatorResearch.Compare("BAD-HIDDEN", control, treatment).ExactMatching);
    }

    [Fact]
    public void BadControlUngovernedDifferenceCannotBecomeG1Direct()
    {
        var result = ProvenancePairComparatorResearch.Compare("BAD-UNGOVERNED",
            [Pair("OP", "left", "S", "L")], [Pair("OP", "right", "S", "R")]);
        Assert.False(result.ExactMatching);
        Assert.Null(result.DirectDivergenceId);
    }

    [Fact]
    public void BadControlOutputOnlyEvidenceCannotAttributeMutation()
    {
        var recorder = Recorder();
        var existing = new GeometrySafetyObject("A", ManiaObject.Tap(0, 500), 1, 1);
        var candidate = new GeometrySafetyObject("B", ManiaObject.Tap(0, 500), 1, 1);
        var delta = recorder.EvaluateSafetyDelta(1, [existing], [existing, candidate], candidate,
            "NO-PROVENANCE", GenerationCausalOrigin.Legacy,
            GenerationProvenanceStage.Pass1BaseOpportunity, provenanceAvailable: false);
        Assert.Equal(GeometrySafetyAttribution.Unattributable, delta.CandidateFinding.Attribution);
    }

    [Fact]
    public void BadControlSourcePreExistingIsNotLegacyIntroduced()
    {
        var recorder = Recorder();
        var a = new GeometrySafetyObject("A", ManiaObject.Tap(0, 500), 1, 1);
        var b = new GeometrySafetyObject("B", ManiaObject.Tap(0, 500), 1, 1);
        var valid = new GeometrySafetyObject("C", ManiaObject.Tap(1, 1000), 2, 2);
        var delta = recorder.EvaluateSafetyDelta(2, [a, b], [a, b, valid], valid, "MUT",
            GenerationCausalOrigin.Legacy, GenerationProvenanceStage.Pass1BaseOpportunity);
        Assert.Empty(delta.IntroducedViolationIds);
        Assert.Equal(GeometrySafetyAttribution.NoViolation, delta.CandidateFinding.Attribution);
    }

    [Fact]
    public void BadControlSerializationOnlyEvidenceRemainsSerializationScoped()
    {
        var bad = new GeometrySafetyObject("BAD", ManiaObject.Ln(0, 500, 500), 1, 1.01m);
        var delta = Recorder().EvaluateSafetyDelta(1, [], [bad], bad, "SER",
            GenerationCausalOrigin.Legacy, GenerationProvenanceStage.Serialization);
        Assert.Equal(GeometrySafetyAttribution.SerializationIntroducedViolation,
            delta.CandidateFinding.Attribution);
    }

    [Fact]
    public void BadControlInstrumentationRngConsumptionIsObservable()
    {
        var clean = new CountingRandom(17);
        var interfered = new CountingRandom(17);
        _ = clean.NextDouble();
        _ = interfered.NextDouble();
        _ = interfered.NextDouble(); // deliberately bad diagnostic consumption
        Assert.NotEqual(clean.CallCount, interfered.CallCount);
        Assert.NotEqual(clean.Transcript, interfered.Transcript);
    }

    [Fact]
    public void ContractIsStableResearchOnlyAndForbidsRemediation()
    {
        var contract = SafetyCausalContractResearch.Create();
        Assert.True(contract.NoRemediation);
        Assert.False(contract.DefaultBehaviorChanged);
        Assert.False(contract.GenerationSemanticsChanged);
        Assert.Equal("NOT_AUTHORIZED", contract.NextPhaseAuthorization);
        Assert.Equal(SafetyCausalContractResearch.ComputeHash(contract),
            SafetyCausalContractResearch.ComputeHash(SafetyCausalContractResearch.Create()));
        Assert.Equal(SafetyCausalContractResearch.SerializeArtifact(),
            SafetyCausalContractResearch.SerializeArtifact());
    }

    [Fact]
    public void CertifiedArtifactsCoverAllNineMutationsFourRunsAndTwoHundredControls()
    {
        var root = RepositoryRoot();
        using var treatment = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs",
            "safety_causal_treatment_violations.json")));
        using var control = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs",
            "safety_causal_control_classification.json")));
        var treatmentRows = treatment.RootElement.EnumerateArray().ToArray();
        var controlRows = control.RootElement.EnumerateArray().ToArray();
        Assert.Equal(9, treatmentRows.Length);
        Assert.Equal(9, treatmentRows.Select(x => x.GetProperty("mutationId").GetString()).Distinct().Count());
        Assert.Equal(4, treatmentRows.Select(x => $"{x.GetProperty("chartId").GetString()}|" +
            x.GetProperty("seed").GetInt32()).Distinct().Count());
        Assert.All(treatmentRows, x =>
        {
            Assert.Equal("treatmentDownstream", x.GetProperty("classification").GetString());
            Assert.Equal("oracleOrSemanticMismatch", x.GetProperty("rootCause").GetString());
            Assert.True(x.GetProperty("placementAccepted").GetBoolean());
            Assert.False(x.GetProperty("internalBeatConflict").GetBoolean());
            Assert.True(x.GetProperty("materializedConflict").GetBoolean());
            Assert.False(x.GetProperty("fullStateReconvergedBeforeViolation").GetBoolean());
        });
        Assert.Equal(200, controlRows.Length);
        Assert.All(controlRows, x =>
        {
            Assert.Equal("legacyDownstream", x.GetProperty("classification").GetString());
            Assert.Equal("oracleOrSemanticMismatch", x.GetProperty("rootCause").GetString());
        });
    }

    private static GenerationProvenanceRecorderResearch Recorder() => new(
        GenerationProvenanceIdentityResearch.Create("SAFETY-CAUSAL", 1, new AddNotesOptions(),
            "legacy-experimental.1", ["OP"]));

    private static string RepositoryRoot([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", ".."));

    private static ProvenancePairEvent Pair(string opportunity, string decision, string before, string after,
        string? geometry = null, bool complete = true, bool direct = false,
        GenerationDecisionDisposition disposition = GenerationDecisionDisposition.Place) => new(
            opportunity, decision, disposition, State(before, geometry ?? before, complete),
            State(after, geometry ?? after, complete), direct);

    private static GenerationStateIdentity State(string generation, string geometry, bool complete) => new(
        geometry, generation, complete ? 0 : null, complete ? 0 : null, 0, "PENDING", complete);

    private sealed class CountingRandom(int seed) : IRandomPositionSource
    {
        private readonly SeededRandom inner = new(seed);
        public List<double> Transcript { get; } = [];
        public long CallCount => Transcript.Count;
        public double NextDouble() { var value = inner.NextDouble(); Transcript.Add(value); return value; }
        public int Next(int maximum) { var value = inner.Next(maximum); Transcript.Add(value); return value; }
    }
}
