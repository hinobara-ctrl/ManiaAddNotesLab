using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public static class SafetyRemediationGatePolicyVersions
{
    public const string Treatment = "safety-remediation-canonical-playable.1";
}

/// <summary>
/// The only playable projection used by the remediation treatment. Durable integer milliseconds remain on the
/// ManiaObject; beat-space collision coordinates are reconstructed exclusively from those integers.
/// </summary>
public sealed class CanonicalPlayableGeometry(BeatTimeline timeline)
{
    public decimal Beat(int milliseconds) => timeline.ToBeatDecimal(milliseconds);

    public TimedManiaObject Project(TimedManiaObject value)
    {
        var start = Beat(value.Object.StartTime);
        var end = value.Object.Type == ManiaObjectType.Tap ? start : Beat(value.Object.EndTime!.Value);
        return new(value.Object, start, end);
    }
}

public sealed record SafetyRemediationGateRuntimeConfiguration(
    string ContractContentHash,
    bool TreatmentEnabled)
{
    public static SafetyRemediationGateRuntimeConfiguration Frozen(bool treatmentEnabled) => new(
        SafetyRemediationGateContractResearch.ComputeHash(SafetyRemediationGateContractResearch.Create()),
        treatmentEnabled);
}

public sealed record SafetyRemediationCandidateGeometryDecision(
    string OpportunityKey,
    int CandidateOrder,
    ManiaObjectType ObjectType,
    int StartTime,
    int? EndTime,
    decimal LatentStartBeat,
    decimal? LatentEndBeat,
    decimal CanonicalStartBeat,
    decimal? CanonicalEndBeat,
    ImmutableArray<int> LegacyLegalLanes,
    ImmutableArray<int> CanonicalLegalLanes,
    string MaterializedGeometryStateHash,
    long RngPositionBeforeDecision,
    int? SelectedLane,
    bool TreatmentEnabled)
{
    public bool GeometrySetsDiffer => !LegacyLegalLanes.SequenceEqual(CanonicalLegalLanes);
    public bool LegacyAcceptsSelectedLane => SelectedLane is not null && LegacyLegalLanes.Contains(SelectedLane.Value);
    public bool CanonicalAcceptsSelectedLane => SelectedLane is not null && CanonicalLegalLanes.Contains(SelectedLane.Value);
}

public sealed record SafetyRemediationGateRuntimeDiagnostics(
    string SchemaVersion,
    string BehaviorPolicyVersion,
    string ContractContentHash,
    bool TreatmentEnabled,
    int GateRngCalls,
    ImmutableArray<SafetyRemediationCandidateGeometryDecision> CandidateDecisions)
{
    public int GeometrySetDifferences => CandidateDecisions.Count(x => x.GeometrySetsDiffer);
    public int SelectedLegacyAcceptCanonicalReject => CandidateDecisions.Count(x =>
        x.LegacyAcceptsSelectedLane && !x.CanonicalAcceptsSelectedLane);
}

internal sealed class SafetyRemediationGateRuntimeDiagnosticsBuilder(
    SafetyRemediationGateRuntimeConfiguration configuration)
{
    private readonly List<SafetyRemediationCandidateGeometryDecision> decisions = [];

    public void Add(string opportunityKey, int candidateOrder, ManiaObjectType type,
        int startTime, int? endTime, decimal latentStartBeat, decimal? latentEndBeat,
        decimal canonicalStartBeat, decimal? canonicalEndBeat,
        IEnumerable<int> legacyLanes, IEnumerable<int> canonicalLanes,
        string materializedGeometryStateHash, long rngPositionBeforeDecision)
    {
        decisions.Add(new(opportunityKey, candidateOrder, type, startTime, endTime,
            latentStartBeat, latentEndBeat, canonicalStartBeat, canonicalEndBeat,
            legacyLanes.ToImmutableArray(), canonicalLanes.ToImmutableArray(),
            materializedGeometryStateHash, rngPositionBeforeDecision, null,
            configuration.TreatmentEnabled));
    }

    public void MarkSelected(string opportunityKey, int candidateOrder, int lane)
    {
        for (var i = decisions.Count - 1; i >= 0; i--)
        {
            if (decisions[i].OpportunityKey != opportunityKey
                || decisions[i].CandidateOrder != candidateOrder) continue;
            decisions[i] = decisions[i] with { SelectedLane = lane };
            return;
        }
        throw new InvalidOperationException("Selected remediation candidate has no geometry decision.");
    }

    public SafetyRemediationGateRuntimeDiagnostics Build() => new(
        "safety-remediation-gate-runtime.1",
        configuration.TreatmentEnabled ? SafetyRemediationGatePolicyVersions.Treatment
            : MapperEvidenceProfileBuilder.BehaviorPolicyVersion,
        configuration.ContractContentHash, configuration.TreatmentEnabled, 0,
        decisions.ToImmutableArray());
}

public sealed record SafetyRemediationGateContract(
    string SchemaVersion,
    string Phase,
    string HumanAuthorization,
    string RepositoryEntryHead,
    string ImplementationSnapshotSha256,
    string DesignDependencySha256,
    string CorpusManifestSha256,
    ImmutableArray<int> Seeds,
    string PrimaryStratum,
    string SecondaryStratum,
    string Control,
    string Treatment,
    string Claim,
    ImmutableArray<string> Exclusions,
    ImmutableArray<string> Denominators,
    ImmutableArray<string> StopConditions,
    ImmutableArray<string> ExpectedArtifacts,
    string PlayableAuthority,
    string LatentAuthority,
    string RngRule,
    string DefaultPolicy,
    bool DefaultBehaviorChanged,
    string SuccessorAuthorization);

public sealed record SafetyRemediationGateContractArtifact(
    SafetyRemediationGateContract Contract,
    string CanonicalSha256,
    string Canonicalization);

public static class SafetyRemediationGateContractResearch
{
    public const string ImplementationSnapshotSha256 = "93DD2E5E2D7FC6C49FB33B34C7CCDCC58870FA6BE835892FF7903A09D4ADCBE7";
    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions Pretty = new(Canonical) { WriteIndented = true };

    public static SafetyRemediationGateContract Create() => new(
        "safety-remediation-gate-runtime.1", "SAFETY.REMEDIATION.GATE",
        "AUTHORIZED_CANONICAL_PLAYABLE_GEOMETRY_RUNTIME_INTEGRATION_AND_CERTIFICATION",
        "53f1475cb14186b2a6a4eef5a9c71d8aa8777d6f", ImplementationSnapshotSha256,
        "EFB31F2BF5026BE7353ACB30C15768389D077CC7244B0C91D66F8B6B6BD002F8",
        "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445",
        Enumerable.Range(1, 20).ToImmutableArray(),
        "C11 11 charts x seeds 1-20; legacy remediation OFF versus legacy remediation ON; G1 and articulation OFF",
        "Four frozen G1.GATE violation runs (02D9 seeds 9,11,17; 20651 seed 11); G1 treatment in both arms; remediation OFF versus ON; articulation OFF",
        "legacy placement authority and legacy-experimental.1 behavior",
        "same candidates, eligibility, ordering, lane-selection algorithm and RNG; only playable geometry authority is canonical materialized geometry",
        "Canonical playable authority eliminates latent-vs-materialized runtime contradictions without new hard-validity violations or G1 identity drift.",
        ["G1 utility or promotion", "product defaults", "CLI/Web exposure", "articulation", "G2", "H",
         "lane style", "composition", "difficulty", "AddChance/budget", "serializer or HardValidity semantic changes"],
        ["11 charts", "20 seeds", "220 primary paired runs", "4 secondary paired runs", "209 frozen known cases",
         "6 additional frozen shadow cases", "307167 design-shadow committed proposals"],
        ["treatment-attributable hard-validity violation", "G1 evidence/identity mutation",
         "placement/serialization geometry disagreement", "nondeterminism", "HardValidity relaxation",
         "divergent geometric source of truth", "unexplained significant frozen cases",
         "direct/downstream effects not distinguishable"],
        ["safety_remediation_gate_contract.json", "safety_remediation_gate_runs.csv",
         "safety_remediation_gate_known_cases.csv", "safety_remediation_gate_summary.json",
         "PHASE_SAFETY_REMEDIATION_GATE.md"],
        "integer milliseconds; beat-space values reconstructed exclusively from integer milliseconds and the timing map",
        "candidate intent, provenance, research, evidence and exact frozen G1 identity only",
        "the gate consumes zero RNG; downstream RNG positions may diverge after a governed decision",
        "legacy-experimental.1", false, "NOT_AUTHORIZED");

    public static string ComputeHash(SafetyRemediationGateContract contract) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contract, Canonical)));

    public static string SerializeArtifact()
    {
        var contract = Create();
        return JsonSerializer.Serialize(new SafetyRemediationGateContractArtifact(contract,
            ComputeHash(contract), "UTF-8 System.Text.Json camelCase; frozen ordered arrays; no timestamps or machine paths"), Pretty)
            + Environment.NewLine;
    }
}
