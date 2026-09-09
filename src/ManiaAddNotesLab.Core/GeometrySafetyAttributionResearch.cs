using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum GeometrySnapshotRole { Source, Control, Treatment }
public enum GeometryMutationCause { Legacy, TreatmentDirect, TreatmentDownstream, Articulation, Unknown }
public enum RawGeometryRelationKind { SameHead, TouchingBoundary, InclusiveIntersection }
public enum GeometryHardViolationKind
{
    None, OutsideLane, InvalidBeatDuration, InvalidSerializedDuration, DuplicateHead,
    TapOnHeldLongNote, LongNoteOverlap, RequiredSpacing
}
public enum GeometrySafetyAttribution
{
    NoViolation, PreExistingOriginalCondition, PreExistingControlCondition,
    LegacyIntroducedViolation, TreatmentDirectIntroducedViolation,
    TreatmentDownstreamIntroducedViolation, ArticulationIntroducedViolation,
    SerializationIntroducedViolation, ResolvedRelativeToControl, Unattributable
}

public sealed record GeometrySafetyObject(string Id, ManiaObject Object, decimal StartBeat, decimal EndBeat);
public sealed record GeometryMutationProvenance(string MutationId, GeometryMutationCause Cause,
    string PolicyVersion, string Stage, string? OpportunityKey, long? RngPosition,
    string? DirectExperimentalDisposition = null, string? IgnoredParentObjectId = null);
public sealed record GeometryMutation(GeometrySafetyObject Candidate, decimal RequiredGapBeats,
    GeometryMutationProvenance? Provenance);
public sealed record RawGeometryRelation(string Id, string GeometrySignature, int Lane,
    string LeftObjectId, string RightObjectId, RawGeometryRelationKind Kind);
public sealed record GeometrySafetyFinding(string Id, string GeometrySignature,
    GeometryHardViolationKind Violation, GeometrySafetyAttribution Attribution,
    string? MutationId, string Explanation);
public sealed record GeometrySnapshotAudit(ImmutableArray<RawGeometryRelation> RawRelations,
    ImmutableArray<GeometrySafetyFinding> HardViolations, long WorkCount);
public sealed record GeometryComparativeAudit(GeometrySnapshotAudit Source, GeometrySnapshotAudit Control,
    GeometrySnapshotAudit Treatment, ImmutableArray<GeometrySafetyFinding> AttributedTreatment,
    ImmutableArray<GeometrySafetyFinding> ResolvedRelativeToControl);

/// <summary>
/// Research-only, deterministic and read-only hard-validity attribution. It models the placement
/// predicates used by LaneGeometryIndex and never consults mapper evidence or RNG.
/// </summary>
public static class GeometrySafetyAttributionResearch
{
    public const string SchemaVersion = "geometry-safety-attribution-shadow.1";

    public static GeometrySafetyObject Object(string chartFingerprint, GeometrySnapshotRole role,
        ManiaObject value, decimal startBeat, decimal endBeat, int ordinal)
    {
        var semantic = $"{chartFingerprint}|{role}|{ordinal}|{value.Sequence}|{value.Origin}|" +
            $"{value.Lane}|{value.StartTime}|{value.EndTime?.ToString() ?? "TAP"}|{value.Type}";
        return new GeometrySafetyObject("OBJ-" + Hash(semantic)[..24], value, startBeat, endBeat);
    }

    public static GeometrySnapshotAudit AuditSnapshot(string chartFingerprint, int keyCount,
        IEnumerable<GeometrySafetyObject> objects)
    {
        var values = objects.OrderBy(x => x.Object.Lane).ThenBy(x => x.StartBeat)
            .ThenBy(x => x.EndBeat).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        var raw = ImmutableArray.CreateBuilder<RawGeometryRelation>();
        var violations = ImmutableArray.CreateBuilder<GeometrySafetyFinding>();
        long work = 0;
        foreach (var item in values)
        {
            work++;
            var standalone = StandaloneViolation(item, keyCount);
            if (standalone != GeometryHardViolationKind.None)
                violations.Add(Finding(chartFingerprint, GeometrySignature(item), standalone,
                    GeometrySafetyAttribution.Unattributable, null, "Object violates a scalar hard-validity predicate."));
        }
        foreach (var lane in values.Where(x => x.Object.Lane >= 0 && x.Object.Lane < keyCount)
                     .GroupBy(x => x.Object.Lane))
        {
            var ordered = lane.ToArray();
            for (var i = 0; i < ordered.Length; i++)
            for (var j = i + 1; j < ordered.Length; j++)
            {
                work++;
                if (ordered[j].StartBeat > ordered[i].EndBeat) break;
                var signature = GeometrySignature(ordered[i], ordered[j]);
                var kind = RelationKind(ordered[i], ordered[j]);
                raw.Add(new RawGeometryRelation("RAW-" + Hash($"{chartFingerprint}|{signature}|{kind}")[..24],
                    signature, lane.Key, ordered[i].Id, ordered[j].Id, kind));
                var violation = PairViolation(ordered[i], ordered[j]);
                if (violation != GeometryHardViolationKind.None)
                    violations.Add(Finding(chartFingerprint, signature, violation,
                        GeometrySafetyAttribution.Unattributable, null, "Final-state relation violates engine placement semantics."));
            }
        }
        return new GeometrySnapshotAudit(raw.OrderBy(x => x.Id, StringComparer.Ordinal).ToImmutableArray(),
            violations.OrderBy(x => x.Id, StringComparer.Ordinal).ToImmutableArray(), work);
    }

    public static GeometrySafetyFinding EvaluateMutation(string chartFingerprint, int keyCount,
        IReadOnlyCollection<GeometrySafetyObject> parentState, GeometryMutation mutation)
    {
        var candidate = mutation.Candidate;
        var violation = StandaloneViolation(candidate, keyCount);
        var explanation = "Candidate satisfies hard-validity predicates.";
        if (violation == GeometryHardViolationKind.None)
        {
            foreach (var blocker in parentState.Where(x => x.Id != mutation.Provenance?.IgnoredParentObjectId
                         && x.Object.Lane == candidate.Object.Lane))
            {
                violation = PairViolation(blocker, candidate);
                if (violation != GeometryHardViolationKind.None)
                {
                    explanation = $"Candidate conflicts with causal parent object {blocker.Id}.";
                    break;
                }
                if (candidate.Object.Type == ManiaObjectType.LongNote
                    && ViolatesGap(blocker, candidate, mutation.RequiredGapBeats))
                {
                    violation = GeometryHardViolationKind.RequiredSpacing;
                    explanation = $"Candidate violates the frozen placement gap against {blocker.Id}.";
                    break;
                }
            }
        }
        if (violation == GeometryHardViolationKind.None)
            return Finding(chartFingerprint, GeometrySignature(candidate), violation,
                GeometrySafetyAttribution.NoViolation, mutation.Provenance?.MutationId, explanation);
        var attribution = mutation.Provenance is null ? GeometrySafetyAttribution.Unattributable
            : violation == GeometryHardViolationKind.InvalidSerializedDuration
                ? GeometrySafetyAttribution.SerializationIntroducedViolation
                : mutation.Provenance.Cause switch
                {
                    GeometryMutationCause.Legacy => GeometrySafetyAttribution.LegacyIntroducedViolation,
                    GeometryMutationCause.TreatmentDirect => GeometrySafetyAttribution.TreatmentDirectIntroducedViolation,
                    GeometryMutationCause.TreatmentDownstream => GeometrySafetyAttribution.TreatmentDownstreamIntroducedViolation,
                    GeometryMutationCause.Articulation => GeometrySafetyAttribution.ArticulationIntroducedViolation,
                    _ => GeometrySafetyAttribution.Unattributable
                };
        return Finding(chartFingerprint, GeometrySignature(candidate), violation, attribution,
            mutation.Provenance?.MutationId, explanation);
    }

    public static GeometryComparativeAudit Compare(string chartFingerprint, int keyCount,
        IEnumerable<GeometrySafetyObject> source, IEnumerable<GeometrySafetyObject> control,
        IEnumerable<GeometrySafetyObject> treatment)
    {
        var sourceAudit = AuditSnapshot(chartFingerprint, keyCount, source);
        var controlAudit = AuditSnapshot(chartFingerprint, keyCount, control);
        var treatmentAudit = AuditSnapshot(chartFingerprint, keyCount, treatment);
        var sourceViolations = sourceAudit.HardViolations.Select(x => x.GeometrySignature).ToHashSet(StringComparer.Ordinal);
        var controlViolations = controlAudit.HardViolations.Select(x => x.GeometrySignature).ToHashSet(StringComparer.Ordinal);
        var attributed = treatmentAudit.HardViolations.Select(x => x with
        {
            Attribution = sourceViolations.Contains(x.GeometrySignature)
                ? GeometrySafetyAttribution.PreExistingOriginalCondition
                : controlViolations.Contains(x.GeometrySignature)
                    ? GeometrySafetyAttribution.PreExistingControlCondition
                    : GeometrySafetyAttribution.Unattributable,
            Explanation = sourceViolations.Contains(x.GeometrySignature)
                ? "Condition already exists in source."
                : controlViolations.Contains(x.GeometrySignature)
                    ? "Condition already exists in legacy control."
                    : "Final snapshots alone cannot prove the causal mutation."
        }).ToImmutableArray();
        var treatmentRaw = treatmentAudit.RawRelations.Select(x => x.GeometrySignature).ToHashSet(StringComparer.Ordinal);
        var resolved = controlAudit.RawRelations.Where(x => !treatmentRaw.Contains(x.GeometrySignature))
            .Select(x => Finding(chartFingerprint, x.GeometrySignature, GeometryHardViolationKind.None,
                GeometrySafetyAttribution.ResolvedRelativeToControl, null,
                "Raw control relation is absent from treatment; this is descriptive, not a safety improvement claim."))
            .ToImmutableArray();
        return new GeometryComparativeAudit(sourceAudit, controlAudit, treatmentAudit, attributed, resolved);
    }

    public static string SemanticStateHash(IEnumerable<GeometrySafetyObject> objects) => Hash(string.Join('\n',
        objects.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => $"{x.Id}|{GeometrySignature(x)}")));

    private static GeometryHardViolationKind StandaloneViolation(GeometrySafetyObject value, int keyCount)
    {
        if (value.Object.Lane < 0 || value.Object.Lane >= keyCount) return GeometryHardViolationKind.OutsideLane;
        if (value.Object.Type != ManiaObjectType.LongNote) return GeometryHardViolationKind.None;
        if (value.EndBeat <= value.StartBeat) return GeometryHardViolationKind.InvalidBeatDuration;
        return value.Object.EndTime is null || value.Object.EndTime <= value.Object.StartTime
            ? GeometryHardViolationKind.InvalidSerializedDuration : GeometryHardViolationKind.None;
    }

    private static GeometryHardViolationKind PairViolation(GeometrySafetyObject a, GeometrySafetyObject b)
    {
        if (a.Object.Lane != b.Object.Lane) return GeometryHardViolationKind.None;
        var (left, right) = Order(a, b);
        if (left.StartBeat == right.StartBeat) return GeometryHardViolationKind.DuplicateHead;
        if (left.Object.Type != ManiaObjectType.LongNote) return GeometryHardViolationKind.None;
        if (right.Object.Type == ManiaObjectType.Tap && left.EndBeat >= right.StartBeat)
            return GeometryHardViolationKind.TapOnHeldLongNote;
        if (right.Object.Type == ManiaObjectType.LongNote && left.EndBeat > right.StartBeat)
            return GeometryHardViolationKind.LongNoteOverlap;
        return GeometryHardViolationKind.None;
    }

    private static bool ViolatesGap(GeometrySafetyObject existing, GeometrySafetyObject candidate, decimal gap)
    {
        if (gap <= 0) return false;
        var (left, right) = Order(existing, candidate);
        return left.EndBeat + gap > right.StartBeat;
    }

    private static RawGeometryRelationKind RelationKind(GeometrySafetyObject left, GeometrySafetyObject right) =>
        left.StartBeat == right.StartBeat ? RawGeometryRelationKind.SameHead
        : left.EndBeat == right.StartBeat || right.EndBeat == left.StartBeat
            ? RawGeometryRelationKind.TouchingBoundary : RawGeometryRelationKind.InclusiveIntersection;

    private static (GeometrySafetyObject Left, GeometrySafetyObject Right) Order(GeometrySafetyObject a,
        GeometrySafetyObject b) => a.StartBeat < b.StartBeat || a.StartBeat == b.StartBeat
        && string.CompareOrdinal(a.Id, b.Id) <= 0 ? (a, b) : (b, a);

    private static GeometrySafetyFinding Finding(string chart, string signature,
        GeometryHardViolationKind violation, GeometrySafetyAttribution attribution, string? mutation,
        string explanation) => new("SAFE-" + Hash($"{chart}|{signature}|{violation}|{mutation ?? "SNAPSHOT"}")[..24],
            signature, violation, attribution, mutation, explanation);

    private static string GeometrySignature(GeometrySafetyObject value) =>
        $"L{value.Object.Lane}:{value.Object.Type}:{value.StartBeat}:{value.EndBeat}:{value.Object.StartTime}:{value.Object.EndTime?.ToString() ?? "TAP"}";

    private static string GeometrySignature(GeometrySafetyObject a, GeometrySafetyObject b)
    {
        var parts = new[] { GeometrySignature(a), GeometrySignature(b) }.Order(StringComparer.Ordinal);
        return string.Join("<->", parts);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed record D1SafetyAttributionContract(string SchemaVersion, string Phase, bool BehaviorChange,
    string D1HistoricalOutcome, bool D1RemainsParked, ImmutableArray<string> Taxonomy,
    ImmutableArray<string> PredicateSemantics, ImmutableArray<string> AttributionRules,
    ImmutableArray<string> SerializationSemantics, ImmutableArray<string> StateDefinitions,
    ImmutableArray<string> HardStops, ImmutableArray<string> ForensicLimitations, string Authorization);
public sealed record D1SafetyAttributionContractArtifact(D1SafetyAttributionContract Contract,
    string ContractContentHash, string Canonicalization);

public static class D1SafetyAttributionContractResearch
{
    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions Pretty = new(Canonical) { WriteIndented = true };

    public static D1SafetyAttributionContract Create() => new(
        GeometrySafetyAttributionResearch.SchemaVersion, "D1.SAFETY", false, "C", true,
        Enum.GetNames<GeometrySafetyAttribution>().Order(StringComparer.Ordinal).ToImmutableArray(),
        ["Tap conflicts with a prior LN at end >= tap beat and with any same-beat head",
         "LN overlap uses strict interval crossing; boundary equality is not overlap",
         "LN required spacing is mutation-specific and separate from raw intersection",
         "LN duration must be positive in beat space and serialized milliseconds",
         "Articulation ignores only its identified parent and validates each replacement segment"],
        ["Attribution requires absence in causal parent, a real hard violation, and mutation provenance",
         "Legacy, treatment-direct, treatment-downstream, articulation, and serialization causes remain distinct",
         "Missing causal provenance is Unattributable, never guessed",
         "Conditions present in source or control are not charged to treatment",
         "ResolvedRelativeToControl is descriptive and not an automatic safety improvement"],
        ["PreSerializationHardValidity and FinalSerializedValidity are separate",
         "Materialization may introduce invalid duration and receives serialization attribution",
         "Reparse success is recorded separately from pairwise hard validity"],
        ["SourceState is the parsed original osu chart",
         "ControlState is the legacy-generated result",
         "TreatmentState is a separately authorized future experimental result",
         "RepositoryEntryHead differs from ImplementationSnapshotHash"],
        ["Normal generation changes", "Default policy changes", "D1 reactivation or reinterpretation",
         "RNG or mapper evidence used by oracle", "Source mutation", "Guessed causal attribution",
         "Contract or artifact nondeterminism", "Community corpus use"],
        ["Final snapshots cannot distinguish direct from downstream without mutation provenance",
         "Old D1 behavioral metrics remain withheld", "Old D1 Outcome C cannot be repaired",
         "Raw relation counts are not hard-validity counts"],
        "RESEARCH_SHADOW_ONLY_NO_BEHAVIORAL_EXPERIMENT_AUTHORIZED");

    public static string ComputeHash(D1SafetyAttributionContract contract) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contract, Canonical)));
    public static string SerializeArtifact()
    {
        var contract = Create();
        return JsonSerializer.Serialize(new D1SafetyAttributionContractArtifact(contract,
            ComputeHash(contract), "UTF-8 System.Text.Json camelCase; set-like arrays ordinal-sorted; no timestamp or path"), Pretty);
    }
}

public sealed record ExperimentRepositoryIdentity(string RepositoryEntryHead, string Branch,
    string OriginMainHead, bool DirtyWorktree, string SemanticContractHash, string RunManifestHash,
    string ImplementationSnapshotHash);
public sealed record NamedImplementationContent(string Name, byte[] Content);

public static class ExperimentBaselineIdentityResearch
{
    public static string ComputeImplementationSnapshot(IEnumerable<NamedImplementationContent> files)
    {
        var rows = files.OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => $"{x.Name}:{Convert.ToHexString(SHA256.HashData(x.Content))}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', rows))));
    }

    public static bool CertificateMatches(ExperimentRepositoryIdentity captured,
        ExperimentRepositoryIdentity certificate) => captured == certificate;
}
