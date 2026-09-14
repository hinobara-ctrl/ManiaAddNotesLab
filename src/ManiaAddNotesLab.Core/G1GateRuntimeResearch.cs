using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public static class G1GatePolicyVersions
{
    public const string Treatment = "g1-interior-relation-admission.1";
}

public sealed class G1GateEvidenceIndex
{
    private readonly string chartFingerprint;
    private readonly int keyCount;
    private readonly ImmutableDictionary<ObjectKey, OriginalObservation> observations;
    private readonly ImmutableDictionary<AnchorKey, InteriorStructuralAnchor> anchors;
    private readonly ImmutableArray<InteriorRelationVocabularyEntry> vocabulary;

    private G1GateEvidenceIndex(string chartFingerprint, int keyCount,
        ImmutableDictionary<ObjectKey, OriginalObservation> observations,
        ImmutableDictionary<AnchorKey, InteriorStructuralAnchor> anchors,
        ImmutableArray<InteriorRelationVocabularyEntry> vocabulary, string contentHash,
        int ignoredSyntheticObjects)
    {
        this.chartFingerprint = chartFingerprint;
        this.keyCount = keyCount;
        this.observations = observations;
        this.anchors = anchors;
        this.vocabulary = vocabulary;
        ContentHash = contentHash;
        IgnoredSyntheticObjects = ignoredSyntheticObjects;
    }

    public string ChartFingerprint => chartFingerprint;
    public string ContentHash { get; }
    public int IgnoredSyntheticObjects { get; }
    public int VocabularyCount => vocabulary.Length;

    public static G1GateEvidenceIndex Build(ManiaChart source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var normalized = InteriorRelationMembershipResearch.NormalizeOriginalOnly(source);
        var ignored = source.OriginalObjects.Count - normalized.OriginalObjects.Count;
        var profile = MapperEvidenceProfileBuilder.Build(normalized);
        var census = InteriorRelationFeasibilityResearch.Evaluate(normalized,
            InteriorRelationMembershipResearch.CurrentOptions());
        var observations = profile.Observations.ToImmutableDictionary(x => ObjectKey.From(x), x => x);
        var anchors = census.StructuralAnchors.ToImmutableDictionary(
            x => new AnchorKey(x.ParentLongNoteId, x.AnchorTime, x.AnchorBeat), x => x);
        var vocabulary = census.CompleteRelations.Select(x => new InteriorRelationVocabularyEntry(x))
            .OrderBy(x => x.Occurrence.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();
        var semantic = string.Join('\n', vocabulary.Select(x => x.Occurrence.OccurrenceId)
            .Prepend(profile.ChartFingerprint).Prepend(G1GatePolicyVersions.Treatment));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(semantic)));
        return new G1GateEvidenceIndex(profile.ChartFingerprint, normalized.KeyCount, observations, anchors,
            vocabulary, hash, ignored);
    }

    public InteriorRelationMembershipEvaluation Evaluate(ManiaObject parent, int anchorTime,
        decimal anchorBeat, int candidateEndTime, decimal candidateEndBeat)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (!observations.TryGetValue(ObjectKey.From(parent), out var observation)
            || !anchors.TryGetValue(new AnchorKey(observation.Id, anchorTime, anchorBeat), out var anchor))
        {
            var unresolved = new InteriorRelationCandidateIdentity(
                CandidateId(parent, anchorTime, anchorBeat, candidateEndTime, candidateEndBeat),
                chartFingerprint, observation?.Id ?? new OriginalObservationId(-1), parent.Lane,
                anchorTime, anchorBeat, InteriorAnchorKind.None, candidateEndTime, candidateEndBeat,
                InteriorEndRelation.Invalid, "UNRESOLVABLE", null, false);
            return InteriorRelationMembershipResearch.Evaluate(unresolved, vocabulary);
        }

        var release = new ReleaseCandidate(candidateEndBeat, candidateEndTime, 0,
            new CandidateEvidence(0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0));
        return InteriorRelationMembershipResearch.Evaluate(
            InteriorRelationMembershipResearch.Candidate(chartFingerprint, keyCount, observation, anchor, release),
            vocabulary);
    }

    public void ValidateFor(ManiaChart chart)
    {
        var normalized = InteriorRelationMembershipResearch.NormalizeOriginalOnly(chart);
        if (normalized.KeyCount != keyCount
            || MapperEvidenceProfileBuilder.ComputeFingerprint(normalized) != chartFingerprint)
            throw new InvalidOperationException("G1.GATE evidence belongs to a different normalized chart.");
    }

    private static string CandidateId(ManiaObject parent, int anchorTime, decimal anchorBeat,
        int endTime, decimal endBeat) => "unresolvable-" + Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{ObjectKey.From(parent)}|{anchorTime}|{anchorBeat}|{endTime}|{endBeat}")))
        .ToLowerInvariant();

    private readonly record struct ObjectKey(int Sequence, int Lane, ManiaObjectType Type, int StartTime, int? EndTime)
    {
        public static ObjectKey From(ManiaObject value) =>
            new(value.Sequence, value.Lane, value.Type, value.StartTime, value.EndTime);
        public static ObjectKey From(OriginalObservation value) =>
            new(value.SourceSequence, value.Lane, value.Type, value.StartTime, value.EndTime);
    }

    private readonly record struct AnchorKey(OriginalObservationId ParentId, int Time, decimal Beat);
}

public sealed record G1GateRuntimeConfiguration(
    G1GateEvidenceIndex EvidenceIndex,
    string ContractContentHash,
    string RunManifestContentHash,
    bool TreatmentEnabled)
{
    public static G1GateRuntimeConfiguration Frozen(G1GateEvidenceIndex evidenceIndex,
        string runManifestContentHash) => new(evidenceIndex,
            G1GateContractResearch.ComputeContentHash(G1GateContractResearch.Create()), runManifestContentHash, true);
    public static G1GateRuntimeConfiguration ControlObserver(G1GateEvidenceIndex evidenceIndex,
        string runManifestContentHash) => new(evidenceIndex,
            G1GateContractResearch.ComputeContentHash(G1GateContractResearch.Create()), runManifestContentHash, false);
}

public sealed record G1GateDirectDecision(
    string OpportunityKey,
    string ProposalSemanticId,
    string EvaluatedSemanticId,
    string? CommittedSemanticId,
    OriginalObservationId ParentLongNoteId,
    int AnchorTime,
    decimal AnchorBeat,
    int CandidateEndTime,
    decimal CandidateEndBeat,
    int SelectedLane,
    int? CommittedLane,
    InteriorRelationMembershipState MembershipState,
    bool Admitted,
    bool MutationCommitted,
    bool ReplacementAttempted,
    bool ArticulationIntentCreated,
    bool MutationObservedBeforeDecision,
    long RngPositionBeforeGate,
    long RngPositionAfterGate,
    string PreGateGeometryStateHash,
    string PreGateGenerationStateHash);

public sealed record G1GateInvariantAudit(
    int CandidateSubstitutionViolations,
    int LaneSubstitutionViolations,
    int RngConsumptionViolations,
    int RerollViolations,
    int ArticulationRoutingViolations,
    int MutationBeforeDecisionViolations,
    int AbstainMutationViolations,
    int AdmitReconstructionViolations)
{
    public int Total => CandidateSubstitutionViolations + LaneSubstitutionViolations
        + RngConsumptionViolations + RerollViolations + ArticulationRoutingViolations
        + MutationBeforeDecisionViolations + AbstainMutationViolations + AdmitReconstructionViolations;
}

public static class G1GateInvariantAuditResearch
{
    public static G1GateInvariantAudit Audit(G1GateDirectDecision value) => new(
        value.ProposalSemanticId == value.EvaluatedSemanticId
            && (value.CommittedSemanticId is null || value.CommittedSemanticId == value.ProposalSemanticId) ? 0 : 1,
        value.CommittedLane is null || value.CommittedLane == value.SelectedLane ? 0 : 1,
        value.RngPositionBeforeGate == value.RngPositionAfterGate ? 0 : 1,
        value.ReplacementAttempted ? 1 : 0,
        value.ArticulationIntentCreated ? 1 : 0,
        value.MutationObservedBeforeDecision ? 1 : 0,
        !value.Admitted && (value.MutationCommitted || value.CommittedSemanticId is not null) ? 1 : 0,
        value.Admitted && (!value.MutationCommitted || value.CommittedSemanticId != value.ProposalSemanticId
            || value.CommittedLane != value.SelectedLane) ? 1 : 0);
}

public sealed record G1GateRuntimeDiagnostics(
    string SchemaVersion,
    string BehaviorPolicyVersion,
    string ContractContentHash,
    string RunManifestContentHash,
    string ChartFingerprint,
    string EvidenceIndexHashBefore,
    string EvidenceIndexHashAfter,
    int IgnoredSyntheticObjects,
    int GateRngCalls,
    ImmutableArray<G1GateDirectDecision> DirectDecisions)
{
    public int Admissions => DirectDecisions.Count(x => x.Admitted);
    public int Abstentions => DirectDecisions.Length - Admissions;
    public int SuppressedMutations => DirectDecisions.Count(x => !x.MutationCommitted);
}

internal sealed class G1GateRuntimeDiagnosticsBuilder(G1GateRuntimeConfiguration configuration)
{
    private readonly List<G1GateDirectDecision> decisions = [];
    private readonly string hashBefore = configuration.EvidenceIndex.ContentHash;

    public void Add(G1GateDirectDecision decision) => decisions.Add(decision);

    public G1GateRuntimeDiagnostics Build() => new("g1-gate-runtime-certification.1",
        configuration.TreatmentEnabled ? G1GatePolicyVersions.Treatment
            : MapperEvidenceProfileBuilder.BehaviorPolicyVersion, configuration.ContractContentHash,
        configuration.RunManifestContentHash, configuration.EvidenceIndex.ChartFingerprint,
        hashBefore, configuration.EvidenceIndex.ContentHash,
        configuration.EvidenceIndex.IgnoredSyntheticObjects,
        decisions.Sum(x => checked((int)(x.RngPositionAfterGate - x.RngPositionBeforeGate))),
        decisions.ToImmutableArray());
}

public static class G1GateStateResearch
{
    public static GenerationStateIdentity Snapshot(IReadOnlyList<ManiaObject> originals,
        IReadOnlyList<ManiaObject> added, long rngPosition, int opportunityCursor)
    {
        var rows = originals.Concat(added).Select(GenerationProvenanceRecorderResearch.ObjectSemanticIdentity)
            .Order(StringComparer.Ordinal).ToArray();
        var geometry = Hash(string.Join('\n', rows));
        var generation = Hash(string.Join('|', geometry, rngPosition, opportunityCursor,
            "G1_GATE_NO_ARTICULATION", "FROZEN_OPPORTUNITY_SEQUENCE"));
        return new GenerationStateIdentity(geometry, generation, rngPosition, rngPosition,
            opportunityCursor, "G1_GATE_NO_ARTICULATION", true);
    }

    private static string Hash(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed record G1GateContract(
    string Phase,
    string HumanAuthorization,
    string RepositoryEntryHead,
    string ControlPolicyVersion,
    string TreatmentPolicyVersion,
    string InsertionPoint,
    string G1DesignDependencyHash,
    ImmutableSortedDictionary<string, string> MembershipMapping,
    string NoReroll,
    string Articulation,
    string Eligibility,
    string Rng,
    string EvidenceScope,
    string SafetyProvenance,
    bool DefaultBehaviorChanged,
    string CertificationCorpus,
    string Promotion,
    string NextPhase);

public static class G1GateContractResearch
{
    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions Pretty = new(Canonical) { WriteIndented = true };

    public static G1GateContract Create() => new(
        "G1.GATE", "AUTHORIZED_FOR_RESEARCH_BY_HUMAN_PROMPT",
        "22707d40fbd92d39885a1caee5bcde005aecdb04",
        MapperEvidenceProfileBuilder.BehaviorPolicyVersion, G1GatePolicyVersions.Treatment,
        "after exact legacy LN shape/lane/geometry proposal; before added.Add and geometry.Insert",
        "15A16B6EFBF779CFF2C42A8C9A0DD46E025019252BEFA968E831233DC802AA68",
        new Dictionary<string, string>
        {
            [nameof(InteriorRelationMembershipState.CandidateObservedUnique)] = "ADMIT",
            [nameof(InteriorRelationMembershipState.CandidateObservedAmongAlternatives)] = "ADMIT",
            [nameof(InteriorRelationMembershipState.CandidateNotObserved)] = "ABSTAIN",
            [nameof(InteriorRelationMembershipState.NoObservedRelation)] = "ABSTAIN",
            [nameof(InteriorRelationMembershipState.UnresolvableExactIdentity)] = "ABSTAIN"
        }.ToImmutableSortedDictionary(StringComparer.Ordinal),
        "ABSTAIN terminates the opportunity; no reroll, replacement, fallback, or compensation",
        "OFF in control and treatment; G1 ABSTAIN cannot create ArticulationIntent",
        "frozen legacy opportunity construction, ordering, thresholds, candidates, lanes, and geometry",
        "gate consumes zero RNG; paired RNG positions equal at direct decision",
        "chart-local full-chart normalized IsSynthetic=false and Origin=None only; immutable per run",
        "SAFETY.PROV exact state, divergence, ancestry, reconvergence, and serialization semantics",
        false, "frozen C11 development corpus; seeds 1-20; mechanical certification only",
        "PROHIBITED; no utility, quality, preference, safety-benefit, or default claim",
        "G1 behavioral utility, G2, and H remain NOT_AUTHORIZED");

    public static string ComputeContentHash(G1GateContract contract) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contract, Canonical)));

    public static string SerializeArtifact()
    {
        var contract = Create();
        return JsonSerializer.Serialize(new
        {
            contract,
            canonicalSha256 = ComputeContentHash(contract),
            canonicalization = "UTF-8 System.Text.Json camelCase; frozen ordered membership map; no timestamps or paths"
        }, Pretty) + Environment.NewLine;
    }
}
