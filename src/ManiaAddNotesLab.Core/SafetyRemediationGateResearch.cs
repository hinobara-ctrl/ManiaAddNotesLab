using System.Collections.Immutable;
using System.Globalization;
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
    bool TreatmentEnabled,
    bool CompactDiagnostics,
    bool RetainCandidateDecisions,
    ISafetyRemediationGateOpportunitySink? OpportunitySink)
{
    public static SafetyRemediationGateRuntimeConfiguration Frozen(bool treatmentEnabled,
        bool compactDiagnostics = false,
        bool retainCandidateDecisions = true,
        ISafetyRemediationGateOpportunitySink? opportunitySink = null) => new(
        SafetyRemediationGateContractResearch.ComputeHash(SafetyRemediationGateContractResearch.Create()),
        treatmentEnabled, compactDiagnostics, retainCandidateDecisions, opportunitySink);
}

public interface ISafetyRemediationGateOpportunitySink
{
    void ObserveCandidate(SafetyRemediationCandidateGeometryDecision decision);
    void Observe(SafetyRemediationGateCompactOpportunityState state);
}

public sealed record SafetyRemediationCandidateGeometryDecision(
    string OpportunityKey,
    int OpportunityOrder,
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
    ImmutableArray<SafetyRemediationCandidateGeometryDecision> CandidateDecisions,
    ImmutableArray<SafetyRemediationGateOpportunityState> OpportunityStates,
    ImmutableArray<SafetyRemediationGateCompactOpportunityState> CompactOpportunityStates,
    int OpportunityCount,
    string OpportunityTraceFingerprint,
    int CandidateDecisionCount,
    string CandidateTraceFingerprint)
{
    public int GeometrySetDifferences => CandidateDecisions.Count(x => x.GeometrySetsDiffer);
    public int SelectedLegacyAcceptCanonicalReject => CandidateDecisions.Count(x =>
        x.LegacyAcceptsSelectedLane && !x.CanonicalAcceptsSelectedLane);
}

public sealed record SafetyRemediationGateOpportunityState(
    string OpportunityKey,
    int OpportunityOrder,
    SafetyRemediationGateSufficientState StateBefore,
    SafetyRemediationGateSufficientState StateAfter,
    string? CommittedObjectIdentity);

internal sealed class SafetyRemediationGateRuntimeDiagnosticsBuilder(
    SafetyRemediationGateRuntimeConfiguration configuration,
    IReadOnlyList<TimedManiaObject> originals)
{
    private readonly List<SafetyRemediationCandidateGeometryDecision> decisions = [];
    private readonly List<SafetyRemediationCandidateGeometryDecision> currentDecisions = [];
    private readonly List<SafetyRemediationGateOpportunityState> opportunities = [];
    private readonly List<SafetyRemediationGateCompactOpportunityState> compactOpportunities = [];
    private readonly CommutativeAccumulator materialized = new(originals.Select(MaterializedIdentity));
    private readonly CommutativeAccumulator latent = new(originals.Select(LatentIdentity));
    private readonly SafetyRemediationGateOpportunityFingerprint opportunityFingerprint = new();
    private readonly SafetyRemediationGateCandidateFingerprint candidateFingerprint = new();
    private string opportunitySequenceHash = GenerationProvenanceRecorderResearch.Hash(string.Empty);
    private (string Key, int Order, SafetyRemediationGateSufficientState Before)? pending;
    private SafetyRemediationGateSufficientState? previousAfter;
    private int opportunityCount;
    private int candidateDecisionCount;

    public void BindOpportunitySequence(IEnumerable<string> keys) => opportunitySequenceHash =
        GenerationProvenanceRecorderResearch.Hash(string.Join('\n', keys));

    public void BeginOpportunity(string key, int order, long rngPosition)
    {
        if (pending is not null) throw new InvalidOperationException("Previous remediation opportunity was not completed.");
        var before = previousAfter is not null
            && previousAfter.OpportunityCursor == order
            && previousAfter.RootRngPosition == rngPosition
            && previousAfter.StageRngPosition == rngPosition
                ? previousAfter
                : State(rngPosition, order);
        pending = (key, order, before);
    }

    public void CompleteOpportunity(long rngPosition, TimedManiaObject? committed)
    {
        var current = pending ?? throw new InvalidOperationException("Remediation opportunity was not started.");
        string? identity = null;
        if (committed is not null)
        {
            materialized.Add(MaterializedIdentity(committed));
            latent.Add(LatentIdentity(committed));
            identity = LatentIdentity(committed);
        }
        var after = State(rngPosition, current.Order + 1);
        var compact = new SafetyRemediationGateCompactOpportunityState(current.Key, current.Order,
                current.Before.Hash,
                current.Before.CompleteForCausalLineage, after.Hash,
                after.CompleteForCausalLineage, identity);
        opportunityFingerprint.Add(compact);
        opportunityCount++;
        foreach (var decision in currentDecisions)
        {
            candidateFingerprint.Add(decision);
            candidateDecisionCount++;
            configuration.OpportunitySink?.ObserveCandidate(decision);
            if (configuration.RetainCandidateDecisions) decisions.Add(decision);
        }
        currentDecisions.Clear();
        if (configuration.OpportunitySink is not null)
            configuration.OpportunitySink.Observe(compact);
        else if (configuration.CompactDiagnostics)
            compactOpportunities.Add(compact);
        else
            opportunities.Add(new(current.Key, current.Order, current.Before, after, identity));
        previousAfter = after;
        pending = null;
    }

    public void Add(string opportunityKey, int candidateOrder, ManiaObjectType type,
        int startTime, int? endTime, decimal latentStartBeat, decimal? latentEndBeat,
        decimal canonicalStartBeat, decimal? canonicalEndBeat,
        IEnumerable<int> legacyLanes, IEnumerable<int> canonicalLanes,
        string materializedGeometryStateHash, long rngPositionBeforeDecision)
    {
        var opportunityOrder = pending?.Order
            ?? throw new InvalidOperationException("Candidate remediation decision has no active opportunity.");
        currentDecisions.Add(new(opportunityKey, opportunityOrder, candidateOrder, type, startTime, endTime,
            latentStartBeat, latentEndBeat, canonicalStartBeat, canonicalEndBeat,
            legacyLanes.ToImmutableArray(), canonicalLanes.ToImmutableArray(),
            materializedGeometryStateHash, rngPositionBeforeDecision, null,
            configuration.TreatmentEnabled));
    }

    public void MarkSelected(string opportunityKey, int candidateOrder, int lane)
    {
        for (var i = currentDecisions.Count - 1; i >= 0; i--)
        {
            if (currentDecisions[i].OpportunityKey != opportunityKey
                || currentDecisions[i].CandidateOrder != candidateOrder) continue;
            currentDecisions[i] = currentDecisions[i] with { SelectedLane = lane };
            return;
        }
        throw new InvalidOperationException("Selected remediation candidate has no geometry decision.");
    }

    public SafetyRemediationGateRuntimeDiagnostics Build() => new(
        "safety-remediation-gate-runtime.1",
        configuration.TreatmentEnabled ? SafetyRemediationGatePolicyVersions.Treatment
            : MapperEvidenceProfileBuilder.BehaviorPolicyVersion,
        configuration.ContractContentHash, configuration.TreatmentEnabled, 0,
        decisions.ToImmutableArray(), opportunities.ToImmutableArray(),
        compactOpportunities.ToImmutableArray(), opportunityCount,
        opportunityFingerprint.Complete(), candidateDecisionCount,
        candidateFingerprint.Complete());

    private SafetyRemediationGateSufficientState State(long rngPosition, int cursor)
    {
        var pendingHash = GenerationProvenanceRecorderResearch.Hash(string.Empty);
        var geometry = materialized.Hash;
        var generation = GenerationProvenanceRecorderResearch.Hash(string.Join('|', geometry,
            rngPosition, rngPosition, cursor, pendingHash, opportunitySequenceHash));
        return SafetyRemediationGateHardeningResearch.State(new(geometry, generation, rngPosition,
            rngPosition, cursor, pendingHash, true), latent.Hash);
    }

    private static string MaterializedIdentity(TimedManiaObject value) =>
        $"{value.Object.Lane}|{value.Object.StartTime}|{value.Object.EndTime}|{value.Object.Type}|" +
        $"{value.Object.Sequence}|{value.Object.Origin}";

    private static string LatentIdentity(TimedManiaObject value) => MaterializedIdentity(value) +
        $"|{value.StartBeat.ToString(CultureInfo.InvariantCulture)}|" +
        value.EndBeat.ToString(CultureInfo.InvariantCulture);

    private sealed class CommutativeAccumulator
    {
        private readonly byte[] value = new byte[32];
        private string? cachedHash;
        public CommutativeAccumulator(IEnumerable<string> rows)
        {
            foreach (var row in rows) Add(row);
        }
        public string Hash => cachedHash ??= Convert.ToHexString(value);
        public void Add(string row)
        {
            var digest = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(row));
            var carry = 0;
            for (var index = value.Length - 1; index >= 0; index--)
            {
                var sum = value[index] + digest[index] + carry;
                value[index] = (byte)sum;
                carry = sum >> 8;
            }
            cachedHash = null;
        }
    }
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
