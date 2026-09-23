using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum SafetyRemediationCandidateFamily
{
    MaterializedGeometryCanonicalization,
    CanonicalPreCommitHardValidity,
    SharedCanonicalPlayableGeometry,
    LatentValuesAsEvidenceOnly
}

public enum SafetyRemediationEvidenceGrade { Proven, Supported, Unknown, Incompatible }

public sealed record CanonicalPlayableCoordinate(int Milliseconds, decimal CanonicalBeat);

public sealed record SafetyRemediationShadowDecision(
    GeometryHardViolationKind LegacyLatentResult,
    GeometryHardViolationKind CanonicalPlayableResult,
    bool LegacyAccepted,
    bool CanonicalAccepted,
    ManiaObjectType ObjectType,
    bool StateUnchanged,
    string Scope);

/// <summary>Append-only research observer. It receives committed proposal values and has no RNG or decision API.</summary>
public sealed class SafetyRemediationPlacementObserverResearch
{
    private readonly List<TimedManiaObject> proposals = [];
    public long RngCalls => 0;
    public void ObserveCommittedProposal(TimedManiaObject proposal) => proposals.Add(proposal with { });
    public ImmutableArray<TimedManiaObject> Build() => proposals.ToImmutableArray();
}

public sealed class SafetyRemediationShadowGeometry
{
    private readonly BeatTimeline timeline;
    private readonly LaneGeometryIndex latent;
    private readonly LaneGeometryIndex canonical;

    public SafetyRemediationShadowGeometry(int keyCount, BeatTimeline timeline,
        IReadOnlyList<TimedManiaObject> originals)
    {
        this.timeline = timeline;
        latent = new LaneGeometryIndex(keyCount, originals);
        canonical = new LaneGeometryIndex(keyCount,
            originals.Select(x => SafetyRemediationDesignResearch.CanonicalizePlayableObject(timeline, x)).ToArray());
    }

    public SafetyRemediationShadowDecision EvaluateAndInsert(TimedManiaObject candidate)
    {
        var current = Inspect(latent, candidate);
        var canonicalCandidate = SafetyRemediationDesignResearch.CanonicalizePlayableObject(timeline, candidate);
        var proposed = Inspect(canonical, canonicalCandidate);
        latent.Insert(candidate);
        canonical.Insert(canonicalCandidate);
        return new(current, proposed, current == GeometryHardViolationKind.None,
            proposed == GeometryHardViolationKind.None, candidate.Object.Type, true,
            "Concrete committed proposal; lane-index shadow; collision-only; required spacing excluded.");
    }

    private static GeometryHardViolationKind Inspect(LaneGeometryIndex geometry, TimedManiaObject candidate)
    {
        if (candidate.Object.Type == ManiaObjectType.LongNote)
            return geometry.InspectLnLane(candidate.Object.Lane, candidate.StartBeat, candidate.EndBeat).Violation;
        var tap = geometry.InspectTapLane(candidate.Object.Lane, candidate.StartBeat);
        if (tap.IsLegal) return GeometryHardViolationKind.None;
        if (tap.Previous?.Object.Type == ManiaObjectType.LongNote && tap.Previous.EndBeat >= candidate.StartBeat)
            return GeometryHardViolationKind.TapOnHeldLongNote;
        return GeometryHardViolationKind.DuplicateHead;
    }
}

public sealed record SafetyRemediationRepresentationInventoryRow(
    int Order, string Stage, string SourceValue, string DestinationValue, string NumericType,
    string RoundingRule, string TimingPointDependency, bool InformationLost,
    bool LatentPrecisionRetained, bool CollisionAuthorityCurrent,
    bool EvidenceAuthority, bool G1IdentityDependency, string DesignAuthority);

public sealed record SafetyRemediationCandidateAssessment(
    SafetyRemediationCandidateFamily Candidate, SafetyRemediationEvidenceGrade RootCauseCoverage,
    SafetyRemediationEvidenceGrade SingleSourceOfTruth,
    SafetyRemediationEvidenceGrade HardValidityConsistency,
    SafetyRemediationEvidenceGrade G1IdentityCompatibility,
    SafetyRemediationEvidenceGrade EvidenceCompatibility,
    SafetyRemediationEvidenceGrade SourceCompatibility,
    SafetyRemediationEvidenceGrade GeneratedCompatibility,
    SafetyRemediationEvidenceGrade TimingPointSafety,
    SafetyRemediationEvidenceGrade SerializationCompatibility,
    SafetyRemediationEvidenceGrade CrossPlatformDeterminism,
    string DirectFootprint, string DownstreamRisk, string RngPathRisk, string DensityRisk,
    string DuplicationRisk, string StaleStateRisk, string ImplementationLocality,
    string RefactorBreadth, string RollbackClarity, string Testability, string MigrationRisk,
    string FutureDriftRisk, bool Viable, bool Preferred, string Conclusion);

public sealed record SafetyRemediationDesignContract(
    string SchemaVersion, string Phase, string HumanAuthorization, string RepositoryEntryHead,
    string ScientificDependency, string SafetyCausalContractSha256,
    ImmutableArray<string> ResearchQuestions, ImmutableArray<string> RepresentationDistinctions,
    ImmutableArray<string> CandidateFamilies, ImmutableArray<string> EvaluationAxes,
    string CanonicalPlayableAuthority, bool ShadowOnly, bool BehaviorChange,
    bool GenerationSemanticsChange, bool DefaultBehaviorChange, bool G1SemanticsChange,
    bool DirectDeltaIsFinalOutputDelta, bool RemediationImplemented, string DefaultPolicy,
    string NextPhaseAuthorization);

public sealed record SafetyRemediationDesignContractArtifact(
    SafetyRemediationDesignContract Contract, string CanonicalSha256, string Canonicalization);

/// <summary>
/// Pure research model for the proposed representation boundary. It does not participate in generation.
/// </summary>
public static class SafetyRemediationDesignResearch
{
    public const string SchemaVersion = "safety-remediation-design-shadow.1";

    public static CanonicalPlayableCoordinate Canonicalize(BeatTimeline timeline, decimal intendedBeat)
    {
        var milliseconds = timeline.ToTimeMilliseconds(intendedBeat);
        return new(milliseconds, timeline.ToBeatDecimal(milliseconds));
    }

    public static CanonicalPlayableCoordinate Canonicalize(BeatTimeline timeline, int materializedMilliseconds) =>
        new(materializedMilliseconds, timeline.ToBeatDecimal(materializedMilliseconds));

    public static TimedManiaObject CanonicalizePlayableObject(BeatTimeline timeline, TimedManiaObject value)
    {
        var start = Canonicalize(timeline, value.Object.StartTime).CanonicalBeat;
        var end = value.Object.Type == ManiaObjectType.Tap
            ? start
            : Canonicalize(timeline, value.Object.EndTime!.Value).CanonicalBeat;
        return new(value.Object, start, end);
    }

    public static SafetyRemediationShadowDecision EvaluateCommittedMutation(
        string chartFingerprint, int keyCount, BeatTimeline timeline,
        IReadOnlyList<TimedManiaObject> parentState, TimedManiaObject candidate)
    {
        var stateBefore = parentState.Select(StateIdentity).ToArray();
        var before = parentState.Select((value, index) => SafetyObject(chartFingerprint, value, index)).ToArray();
        var currentCandidate = SafetyObject(chartFingerprint, candidate, parentState.Count);
        var current = GeometrySafetyAttributionResearch.EvaluateMutation(chartFingerprint, keyCount, before,
            new GeometryMutation(currentCandidate, 0, null));

        var canonicalParent = parentState.Select(value => CanonicalizePlayableObject(timeline, value)).ToArray();
        var canonicalCandidate = CanonicalizePlayableObject(timeline, candidate);
        var canonicalBefore = canonicalParent.Select((value, index) =>
            SafetyObject(chartFingerprint, value, index)).ToArray();
        var canonical = GeometrySafetyAttributionResearch.EvaluateMutation(chartFingerprint, keyCount,
            canonicalBefore, new GeometryMutation(SafetyObject(chartFingerprint, canonicalCandidate,
                parentState.Count), 0, null));

        var stateUnchanged = stateBefore.SequenceEqual(parentState.Select(StateIdentity),
            StringComparer.Ordinal);
        return new(current.Violation, canonical.Violation,
            current.Violation == GeometryHardViolationKind.None,
            canonical.Violation == GeometryHardViolationKind.None,
            candidate.Object.Type, stateUnchanged,
            "Concrete committed proposal; collision-only direct shadow, required spacing excluded.");
    }

    public static ImmutableArray<SafetyRemediationRepresentationInventoryRow> RepresentationInventory() =>
    [
        new(1, "Authored .osu", "integer object coordinates", "parsed ManiaObject", "Int32 milliseconds",
            "none", "none", false, false, true, true, true, "authored source; playable authority is its integer coordinate"),
        new(2, "Original analysis", "ManiaObject milliseconds", "TimedManiaObject beats", "decimal beat",
            "exact division", "active positive timing point", false, false, true, true, true,
            "canonical beat derived from authored milliseconds"),
        new(3, "Candidate intent", "observed beat relations", "intended endpoint", "decimal beat",
            "exact decimal arithmetic", "candidate timing segment", false, true, false, true, true,
            "intent/evidence only"),
        new(4, "Materialization", "intended decimal beat", "ManiaObject time", "Int32 milliseconds",
            "MidpointRounding.AwayFromZero", "active positive timing point", true, true, true, false, false,
            "integer milliseconds define playable coordinate"),
        new(5, "Placed proposal", "materialized time + intended beat", "TimedManiaObject", "Int32 + decimal",
            "conditional latent retention", "same timeline", false, true, true, true, true,
            "future design separates playable canonical beat from latent intent"),
        new(6, "Current geometry", "TimedManiaObject", "lane legality", "decimal beat",
            "none", "already converted", false, true, true, false, false,
            "future collision authority must consume canonical playable coordinates only"),
        new(7, "Mutation commit", "placed proposal", "AddedObjects", "integer milliseconds",
            "none", "none", true, false, true, false, false, "materialized playable object"),
        new(8, "Serialization", "AddedObjects", ".osu hit objects", "integer milliseconds",
            "none", "none", false, false, true, false, false, "preserve materialized playable coordinate"),
        new(9, "Reparse", ".osu integer milliseconds", "canonical beats", "decimal beat",
            "exact division", "active positive timing point", false, false, true, true, true,
            "canonical playable reconstruction"),
        new(10, "HardValidity", "canonical reparsed beats", "hard-validity result", "decimal predicates",
            "none", "canonical timeline", false, false, true, false, false,
            "same canonical playable model as placement")
    ];

    public static ImmutableArray<SafetyRemediationCandidateAssessment> CandidateMatrix() =>
    [
        new(SafetyRemediationCandidateFamily.MaterializedGeometryCanonicalization,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Supported,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            SafetyRemediationEvidenceGrade.Supported, SafetyRemediationEvidenceGrade.Supported,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            "measured in shadow", "high/path-dependent", "high after any changed decision",
            "possible", "medium", "low if canonicalized on insertion", "geometry boundary",
            "medium", "clear policy rollback", "high", "medium", "medium", true, false,
            "Viable, but canonicalization alone leaves placement and HardValidity as separately implemented consumers."),
        new(SafetyRemediationCandidateFamily.CanonicalPreCommitHardValidity,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Incompatible,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            SafetyRemediationEvidenceGrade.Supported, SafetyRemediationEvidenceGrade.Proven,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            "measured in shadow", "high/path-dependent", "high after veto", "possible",
            "high: placement plus veto", "medium", "pre-commit", "low", "clear toggle rollback",
            "high", "medium", "high", true, false,
            "Covers the symptom but creates a second selector/authority and is not preferred as the primary architecture."),
        new(SafetyRemediationCandidateFamily.SharedCanonicalPlayableGeometry,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            SafetyRemediationEvidenceGrade.Supported, SafetyRemediationEvidenceGrade.Supported,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            "measured in shadow", "high/path-dependent", "high after any changed decision",
            "possible", "low when predicates share the model", "low", "shared geometry boundary",
            "broad but explicit", "versioned model rollback", "high", "medium", "low", true, true,
            "Preferred: one explicit materialized playable model consumed by placement and HardValidity."),
        new(SafetyRemediationCandidateFamily.LatentValuesAsEvidenceOnly,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Supported,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            SafetyRemediationEvidenceGrade.Supported, SafetyRemediationEvidenceGrade.Supported,
            SafetyRemediationEvidenceGrade.Proven, SafetyRemediationEvidenceGrade.Proven,
            "same measured boundary", "high/path-dependent", "high after any changed decision",
            "possible", "low only with A or C", "low", "cross-cutting invariant", "medium",
            "clear", "high", "medium", "low", true, false,
            "Required invariant, but not a complete architecture; it composes naturally with candidate C.")
    ];

    private static GeometrySafetyObject SafetyObject(string chart, TimedManiaObject value, int ordinal) =>
        GeometrySafetyAttributionResearch.Object(chart, GeometrySnapshotRole.Control, value.Object,
            value.StartBeat, value.EndBeat, ordinal);

    private static string StateIdentity(TimedManiaObject value) =>
        $"{value.Object.Lane}|{value.Object.StartTime}|{value.Object.EndTime}|{value.StartBeat}|{value.EndBeat}";
}

public static class SafetyRemediationDesignContractResearch
{
    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions Pretty = new(Canonical) { WriteIndented = true };

    public static SafetyRemediationDesignContract Create() => new(
        SafetyRemediationDesignResearch.SchemaVersion, "SAFETY.REMEDIATION.DESIGN",
        "AUTHORIZED_HARD_VALIDITY_REMEDIATION_DESIGN_CANONICAL_GEOMETRY_AUTHORITY_RESEARCH",
        "04661593e377aa261c8bcbd7a14a3087bc766964", "SAFETY.CAUSAL",
        "300E879BCB479F77A704BFFB89BF304D9D04ECC556067F921C49C45E4E8FCB84",
        ["What representation is canonical authority for playable collision geometry?",
         "Which latent values remain evidence/intent/identity without granting runtime permission?"],
        ["authored != candidate intent", "latent precision != playable authority",
         "evidence identity != collision authority", "serialization coordinate != evidence identity",
         "direct shadow delta != final behavioral delta"],
        Enum.GetNames<SafetyRemediationCandidateFamily>().ToImmutableArray(),
        ["root-cause coverage", "single source of truth", "HardValidity consistency",
         "G1/evidence compatibility", "timing and serialization", "direct footprint",
         "RNG/path/density risk", "duplication/stale-state risk", "rollback/testability/migration"],
        "Integer milliseconds are the durable playable coordinate; a canonical beat reconstructed from that exact integer and timing map is the only beat-space collision authority.",
        true, false, false, false, false, false, false, "legacy-experimental.1", "NOT_AUTHORIZED");

    public static string ComputeHash(SafetyRemediationDesignContract contract) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contract, Canonical)));

    public static string SerializeArtifact()
    {
        var contract = Create();
        return JsonSerializer.Serialize(new SafetyRemediationDesignContractArtifact(contract,
            ComputeHash(contract), "UTF-8 System.Text.Json camelCase; frozen ordered arrays; no timestamps or machine paths"), Pretty);
    }
}
