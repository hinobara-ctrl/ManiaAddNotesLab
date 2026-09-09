using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum GenerationProvenanceStage
{
    Pass1BaseOpportunity,
    InteriorOpportunity,
    Articulation,
    Serialization
}

public enum GenerationMutationKind
{
    InsertTap,
    InsertLongNote,
    ArticulationReplace,
    SerializationMaterialization
}

public enum GenerationDecisionDisposition
{
    ProbabilityAbstain,
    NoLegalPlacement,
    Place,
    ExperimentalAbstain,
    ArticulationSkipped,
    ArticulationReplace,
    Materialize
}

public enum GenerationCausalOrigin
{
    Legacy,
    TreatmentDirect,
    TreatmentDownstream,
    Unattributable
}

public sealed record GenerationProvenanceRunIdentity(
    string SemanticRunId,
    string ChartFingerprint,
    int Seed,
    string OptionsIdentity,
    string PolicyVersion,
    string RangeIdentity,
    string OpportunitySequenceHash);

public static class GenerationProvenanceIdentityResearch
{
    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static GenerationProvenanceRunIdentity Create(string chartFingerprint, int seed,
        AddNotesOptions options, string policyVersion, IEnumerable<string> opportunityKeys)
    {
        var optionsIdentity = GenerationProvenanceRecorderResearch.Hash(
            JsonSerializer.Serialize(options, Canonical));
        var range = $"{options.StartMs?.ToString() ?? "ALL"}:{options.EndMs?.ToString() ?? "ALL"}";
        var opportunities = GenerationProvenanceRecorderResearch.Hash(string.Join('\n', opportunityKeys));
        var semantic = GenerationProvenanceRecorderResearch.Hash(
            $"{chartFingerprint}|{seed}|{optionsIdentity}|{policyVersion}|{range}|{opportunities}");
        return new GenerationProvenanceRunIdentity("RUN-" + semantic[..24], chartFingerprint, seed,
            optionsIdentity, policyVersion, range, opportunities);
    }

    public static GenerationProvenanceRunIdentity WithOpportunitySequence(
        GenerationProvenanceRunIdentity run, IEnumerable<string> opportunityKeys)
    {
        var opportunities = GenerationProvenanceRecorderResearch.Hash(string.Join('\n', opportunityKeys));
        var semantic = GenerationProvenanceRecorderResearch.Hash(
            $"{run.ChartFingerprint}|{run.Seed}|{run.OptionsIdentity}|{run.PolicyVersion}|" +
            $"{run.RangeIdentity}|{opportunities}");
        return run with { SemanticRunId = "RUN-" + semantic[..24], OpportunitySequenceHash = opportunities };
    }
}

public sealed record GenerationStateIdentity(
    string GeometryStateHash,
    string GenerationStateHash,
    long? RootRngPosition,
    long? StageRngPosition,
    int OpportunityCursor,
    string PendingArticulationHash,
    bool CompleteForCausalLineage);

public sealed record GenerationDecisionEvent(
    string DecisionId,
    long SequenceIndex,
    GenerationProvenanceStage Stage,
    string? OpportunityKey,
    string PolicyVersion,
    GenerationDecisionDisposition Disposition,
    string? CandidateIdentity,
    GenerationStateIdentity StateBefore,
    GenerationStateIdentity StateAfter,
    string? DirectExperimentalDisposition,
    string? ExperimentalDivergenceId,
    ImmutableArray<string> CausalAncestorIds,
    long? RngPositionBefore,
    long? RngPositionAfter);

public sealed record GenerationMutationEvent(
    string MutationId,
    long SequenceIndex,
    GenerationProvenanceStage Stage,
    GenerationMutationKind MutationKind,
    string? OpportunityKey,
    string PolicyVersion,
    string ObjectIdentity,
    string? ParentObjectIdentity,
    int Lane,
    decimal StartBeat,
    decimal EndBeat,
    ManiaObjectType ObjectKind,
    GenerationStateIdentity StateBefore,
    GenerationStateIdentity StateAfter,
    GenerationCausalOrigin CausalOrigin,
    string? DirectExperimentalDisposition,
    string? ExperimentalDivergenceId,
    ImmutableArray<string> CausalAncestorIds,
    long? RngPositionBefore,
    long? RngPositionAfter,
    string? SerializationIdentity,
    ImmutableArray<string> IntroducedViolationIds,
    ImmutableArray<string> ResolvedViolationIds);

public sealed record GenerationSerializationLink(
    string GeneratedObjectIdentity,
    string BeatSpaceIdentity,
    string SerializedIdentity,
    string? ReparsedIdentity,
    bool ExactReparseIdentity,
    GeometrySafetyAttribution Attribution,
    string Explanation);

public sealed record MutationSafetyDelta(
    ImmutableArray<string> BeforeViolationIds,
    ImmutableArray<string> AfterViolationIds,
    ImmutableArray<string> IntroducedViolationIds,
    ImmutableArray<string> ResolvedViolationIds,
    GeometrySafetyFinding CandidateFinding);

public sealed record GenerationProvenanceTrace(
    string SchemaVersion,
    GenerationProvenanceRunIdentity RunIdentity,
    ImmutableArray<GenerationDecisionEvent> Decisions,
    ImmutableArray<GenerationMutationEvent> Mutations,
    ImmutableArray<GenerationSerializationLink> SerializationLinks,
    long RecorderRngCalls,
    long StateHashWorkCount,
    long SafetyEvaluationCount);

public sealed record ProvenancePairEvent(
    string OpportunityKey,
    string DecisionIdentity,
    GenerationDecisionDisposition Disposition,
    GenerationStateIdentity StateBefore,
    GenerationStateIdentity StateAfter,
    bool DirectExperimentalGoverned = false);

public sealed record ProvenancePairComparison(
    int CommonPrefixCount,
    string? FirstDivergenceOpportunityKey,
    string? DirectDivergenceId,
    ImmutableArray<string> DownstreamAffectedOpportunityKeys,
    ImmutableArray<string> TemporalAfterButNotDownstreamOpportunityKeys,
    ImmutableArray<string> ReconvergenceOpportunityKeys,
    bool ExactMatching,
    string Explanation);

public sealed record ProvenanceValidationResult(bool IsValid, ImmutableArray<string> Errors);

/// <summary>
/// Research-only append-only observer. It snapshots values supplied by generation and cannot return a placement
/// decision. It owns no random source and therefore cannot consume RNG.
/// </summary>
public sealed class GenerationProvenanceRecorderResearch
{
    public const string SchemaVersion = "generation-provenance-shadow.1";
    private GenerationProvenanceRunIdentity _run;
    private readonly List<GenerationDecisionEvent> _decisions = [];
    private readonly List<GenerationMutationEvent> _mutations = [];
    private readonly List<GenerationSerializationLink> _serialization = [];
    private long _sequence;

    public GenerationProvenanceRecorderResearch(GenerationProvenanceRunIdentity run) => _run = run;

    public long RecorderRngCalls => 0;
    public long StateHashWorkCount { get; private set; }
    public long SafetyEvaluationCount { get; private set; }

    public void BindOpportunitySequence(IEnumerable<string> opportunityKeys)
    {
        if (_sequence != 0 || _decisions.Count != 0 || _mutations.Count != 0)
            throw new InvalidOperationException("Opportunity sequence must be bound before the first event.");
        _run = GenerationProvenanceIdentityResearch.WithOpportunitySequence(_run, opportunityKeys);
    }

    public GenerationStateIdentity State(IReadOnlyList<ManiaObject> originals,
        IReadOnlyList<ManiaObject> added, IReadOnlyList<ArticulationReplacement> replacements,
        long? rootRngPosition, long? stageRngPosition, int opportunityCursor,
        IEnumerable<string>? pendingArticulationIdentities = null)
    {
        var final = MaterializedObjects(originals, added, replacements);
        var rows = final.Select(ObjectSemanticIdentity).Order(StringComparer.Ordinal).ToArray();
        StateHashWorkCount += rows.Length;
        var geometry = Hash(string.Join('\n', rows));
        var pending = Hash(string.Join('\n', (pendingArticulationIdentities ?? [])
            .Order(StringComparer.Ordinal)));
        var complete = rootRngPosition.HasValue && stageRngPosition.HasValue;
        var generation = Hash(string.Join('|', geometry, rootRngPosition?.ToString() ?? "UNKNOWN",
            stageRngPosition?.ToString() ?? "UNKNOWN", opportunityCursor, pending,
            _run.OpportunitySequenceHash));
        return new GenerationStateIdentity(geometry, generation, rootRngPosition, stageRngPosition,
            opportunityCursor, pending, complete);
    }

    public void ObserveDecision(GenerationProvenanceStage stage, string? opportunityKey,
        GenerationDecisionDisposition disposition, string? candidateIdentity,
        GenerationStateIdentity before, GenerationStateIdentity after,
        string? directExperimentalDisposition = null, string? divergenceId = null,
        IEnumerable<string>? causalAncestorIds = null)
    {
        var index = _sequence++;
        _decisions.Add(new GenerationDecisionEvent(EventId(_run, "DEC", index, opportunityKey, disposition.ToString()),
            index, stage, opportunityKey, _run.PolicyVersion, disposition, candidateIdentity, before, after,
            directExperimentalDisposition, divergenceId, Ordered(causalAncestorIds), before.StageRngPosition,
            after.StageRngPosition));
    }

    public void ObserveMutation(GenerationProvenanceStage stage, GenerationMutationKind kind,
        string? opportunityKey, ManiaObject value, decimal startBeat, decimal endBeat,
        GenerationStateIdentity before, GenerationStateIdentity after,
        GenerationCausalOrigin origin = GenerationCausalOrigin.Legacy, ManiaObject? parent = null,
        string? directExperimentalDisposition = null, string? divergenceId = null,
        IEnumerable<string>? causalAncestorIds = null, string? serializationIdentity = null,
        IEnumerable<string>? introducedViolationIds = null, IEnumerable<string>? resolvedViolationIds = null)
    {
        var index = _sequence++;
        var mutationId = EventId(_run, "MUT", index, opportunityKey, kind.ToString());
        _mutations.Add(new GenerationMutationEvent(mutationId, index, stage, kind, opportunityKey,
            _run.PolicyVersion, GeneratedObjectIdentity(mutationId, value),
            parent is null ? null : ObjectSemanticIdentity(parent), value.Lane, startBeat, endBeat, value.Type,
            before, after, origin, directExperimentalDisposition, divergenceId, Ordered(causalAncestorIds),
            before.StageRngPosition, after.StageRngPosition, serializationIdentity,
            Ordered(introducedViolationIds), Ordered(resolvedViolationIds)));
    }

    public void ObserveSerialization(GenerationSerializationLink link)
    {
        _serialization.Add(link with { });
        SafetyEvaluationCount++;
    }

    public void CountSafetyEvaluation() => SafetyEvaluationCount++;

    public MutationSafetyDelta EvaluateSafetyDelta(int keyCount,
        IReadOnlyList<GeometrySafetyObject> parentState, IReadOnlyList<GeometrySafetyObject> childState,
        GeometrySafetyObject candidate,
        string mutationSemanticId, GenerationCausalOrigin origin, GenerationProvenanceStage stage,
        decimal requiredGapBeats = 0, string? ignoredParentObjectId = null, bool provenanceAvailable = true)
    {
        SafetyEvaluationCount++;
        var beforeAudit = GeometrySafetyAttributionResearch.AuditSnapshot(_run.ChartFingerprint, keyCount, parentState);
        var afterAudit = GeometrySafetyAttributionResearch.AuditSnapshot(_run.ChartFingerprint, keyCount, childState);
        var beforeIds = beforeAudit.HardViolations.Select(x => x.GeometrySignature)
            .ToImmutableSortedSet(StringComparer.Ordinal);
        var afterIds = afterAudit.HardViolations.Select(x => x.GeometrySignature)
            .ToImmutableSortedSet(StringComparer.Ordinal);
        GeometryMutationProvenance? mutationProvenance = provenanceAvailable
            ? new GeometryMutationProvenance(mutationSemanticId, OracleCause(origin, stage), _run.PolicyVersion,
                stage.ToString(), null, null, null, ignoredParentObjectId)
            : null;
        var finding = GeometrySafetyAttributionResearch.EvaluateMutation(_run.ChartFingerprint, keyCount,
            parentState, new GeometryMutation(candidate, requiredGapBeats, mutationProvenance));
        return new(beforeIds.ToImmutableArray(), afterIds.ToImmutableArray(),
            afterIds.Except(beforeIds).ToImmutableArray(), beforeIds.Except(afterIds).ToImmutableArray(), finding);
    }

    public GenerationProvenanceTrace Build() => new(SchemaVersion, _run,
        _decisions.OrderBy(x => x.SequenceIndex).ToImmutableArray(),
        _mutations.OrderBy(x => x.SequenceIndex).ToImmutableArray(),
        _serialization.OrderBy(x => x.GeneratedObjectIdentity, StringComparer.Ordinal).ToImmutableArray(),
        RecorderRngCalls, StateHashWorkCount, SafetyEvaluationCount);

    public string Serialize() => GenerationProvenanceJson.Serialize(Build());

    internal static string EventId(GenerationProvenanceRunIdentity run, string prefix, long index,
        string? opportunity, string kind) =>
        $"{prefix}-{index:D8}-{Hash($"{run.SemanticRunId}|{index}|{opportunity ?? "NONE"}|{kind}")[..20]}";

    private static ImmutableArray<string> Ordered(IEnumerable<string>? values) =>
        (values ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray();

    public static string GeneratedObjectIdentity(string mutationId, ManiaObject value) =>
        "GEN-" + Hash($"{mutationId}|{ObjectSemanticIdentity(value)}")[..24];

    public static string ObjectSemanticIdentity(ManiaObject value) =>
        $"L{value.Lane}|T{value.StartTime}|E{value.EndTime?.ToString() ?? "TAP"}|{value.Type}|" +
        $"S{value.Sequence}|O{value.Origin}|Y{value.IsSynthetic}";

    public static IReadOnlyList<ManiaObject> MaterializedObjects(IReadOnlyList<ManiaObject> originals,
        IReadOnlyList<ManiaObject> added, IReadOnlyList<ArticulationReplacement> replacements) =>
    [
        .. originals.Where(original => !replacements.Any(r => SameOriginal(r.ParentOriginalLn, original))),
        .. replacements.SelectMany(x => new[] { x.LeftSegment, x.RightSegment }),
        .. added
    ];

    private static bool SameOriginal(ManiaObject left, ManiaObject right) => left.Sequence == right.Sequence
        && left.Lane == right.Lane && left.StartTime == right.StartTime && left.EndTime == right.EndTime;

    private static GeometryMutationCause OracleCause(GenerationCausalOrigin origin,
        GenerationProvenanceStage stage) => stage == GenerationProvenanceStage.Serialization
        ? GeometryMutationCause.Unknown
        : stage == GenerationProvenanceStage.Articulation && origin == GenerationCausalOrigin.Legacy
            ? GeometryMutationCause.Articulation
            : origin switch
            {
                GenerationCausalOrigin.Legacy => GeometryMutationCause.Legacy,
                GenerationCausalOrigin.TreatmentDirect => GeometryMutationCause.TreatmentDirect,
                GenerationCausalOrigin.TreatmentDownstream => GeometryMutationCause.TreatmentDownstream,
                _ => GeometryMutationCause.Unknown
            };

    internal static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public static class GenerationProvenanceTraceValidatorResearch
{
    public static ProvenanceValidationResult Validate(GenerationProvenanceTrace trace)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        if (trace.RecorderRngCalls != 0) errors.Add("RecorderRngCalls must be zero.");
        var events = trace.Decisions.Select(x => new OrderedEvent(x.SequenceIndex, x.Stage,
                x.StateBefore, x.StateAfter, x.DecisionId,
                GenerationProvenanceRecorderResearch.EventId(trace.RunIdentity, "DEC", x.SequenceIndex,
                    x.OpportunityKey, x.Disposition.ToString()), false, GenerationCausalOrigin.Legacy,
                x.CausalAncestorIds))
            .Concat(trace.Mutations.Select(x => new OrderedEvent(x.SequenceIndex, x.Stage,
                x.StateBefore, x.StateAfter, x.MutationId,
                GenerationProvenanceRecorderResearch.EventId(trace.RunIdentity, "MUT", x.SequenceIndex,
                    x.OpportunityKey, x.MutationKind.ToString()), true, x.CausalOrigin,
                x.CausalAncestorIds)))
            .OrderBy(x => x.Sequence).ToArray();
        for (var i = 0; i < events.Length; i++)
        {
            var item = events[i];
            if (item.Sequence != i) errors.Add($"Event sequence gap or duplicate at {i}.");
            if (item.Id != item.ExpectedId) errors.Add($"Nondeterministic or invalid event ID at {i}.");
            if (item.IsMutation && item.Origin == GenerationCausalOrigin.TreatmentDownstream
                && item.Ancestors.Length == 0)
                errors.Add($"Downstream mutation {item.Id} has no proven divergence ancestor.");
            if (i > 0 && events[i - 1].Stage == item.Stage
                && events[i - 1].After.GenerationStateHash != item.Before.GenerationStateHash)
                errors.Add($"State continuity break before event {i}.");
        }
        if (trace.Mutations.Any(x => x.CausalOrigin != GenerationCausalOrigin.Unattributable
                && string.IsNullOrWhiteSpace(x.MutationId)))
            errors.Add("A causal mutation label requires mutation provenance.");
        return new(errors.Count == 0, errors.ToImmutable());
    }

    private sealed record OrderedEvent(long Sequence, GenerationProvenanceStage Stage,
        GenerationStateIdentity Before, GenerationStateIdentity After, string Id, string ExpectedId,
        bool IsMutation, GenerationCausalOrigin Origin, ImmutableArray<string> Ancestors);
}

public static class ProvenancePairComparatorResearch
{
    public static ProvenancePairComparison Compare(string pairIdentity,
        IReadOnlyList<ProvenancePairEvent> control, IReadOnlyList<ProvenancePairEvent> treatment)
    {
        if (control.Count != treatment.Count || !control.Select(x => x.OpportunityKey)
                .SequenceEqual(treatment.Select(x => x.OpportunityKey), StringComparer.Ordinal))
            return new(0, null, null, [], [], [], false,
                "Exact OpportunityKey alignment is required; fuzzy or list-position matching is forbidden.");

        var common = 0;
        while (common < control.Count && Equivalent(control[common], treatment[common])) common++;
        if (common == control.Count)
            return new(common, null, null, [], [], [], true, "Paired executions are exactly equivalent.");

        var first = control[common].OpportunityKey;
        if (!treatment[common].DirectExperimentalGoverned)
            return new(common, first, null, [], [], [], false,
                "The first difference was not marked as governed by an experimental decision; causal attribution abstains.");
        var divergence = "DIV-" + GenerationProvenanceRecorderResearch.Hash($"{pairIdentity}|{first}|{common}")[..24];
        var downstream = ImmutableArray.CreateBuilder<string>();
        var temporal = ImmutableArray.CreateBuilder<string>();
        var reconvergence = ImmutableArray.CreateBuilder<string>();
        var lineageActive = true;

        for (var i = common + 1; i < control.Count; i++)
        {
            var beforeEqual = control[i].StateBefore.GenerationStateHash ==
                treatment[i].StateBefore.GenerationStateHash;
            var geometryEqual = control[i].StateBefore.GeometryStateHash ==
                treatment[i].StateBefore.GeometryStateHash;
            if (lineageActive && beforeEqual)
            {
                reconvergence.Add(control[i].OpportunityKey);
                lineageActive = false;
            }
            if (lineageActive && !beforeEqual) downstream.Add(control[i].OpportunityKey);
            else temporal.Add(control[i].OpportunityKey);
            if (!beforeEqual && geometryEqual && (!control[i].StateBefore.CompleteForCausalLineage
                    || !treatment[i].StateBefore.CompleteForCausalLineage))
                return new(common, first, divergence, downstream.ToImmutable(), temporal.ToImmutable(),
                    reconvergence.ToImmutable(), false,
                    "Geometry matched but complete generation-state lineage was unavailable; attribution abstains.");
            if (!lineageActive && !Equivalent(control[i], treatment[i])) lineageActive = true;
        }
        return new(common, first, divergence, downstream.ToImmutable(), temporal.ToImmutable(),
            reconvergence.ToImmutable(), true,
            "Downstream requires unequal complete parent generation state descended from the exact first divergence; reconvergence clears ancestry.");
    }

    private static bool Equivalent(ProvenancePairEvent left, ProvenancePairEvent right) =>
        left.DecisionIdentity == right.DecisionIdentity && left.Disposition == right.Disposition
        && left.StateBefore.GenerationStateHash == right.StateBefore.GenerationStateHash
        && left.StateAfter.GenerationStateHash == right.StateAfter.GenerationStateHash;
}

public static class GenerationSerializationProvenanceResearch
{
    public static ImmutableArray<GenerationSerializationLink> LinkExact(ManiaChart generated, string serialized)
    {
        ManiaChart reparsed;
        try { reparsed = OsuBeatmap.Parse(serialized); }
        catch (Exception exception)
        {
            return generated.AllObjects.Where(x => x.IsSynthetic).Select(x => new GenerationSerializationLink(
                UntrackedGeneratedIdentity(x), BeatIdentity(x), SerializedIdentity(x), null, false,
                GeometrySafetyAttribution.Unattributable,
                $"Reparse failed; exact identity unavailable: {exception.GetType().Name}.")).ToImmutableArray();
        }

        var remaining = reparsed.OriginalObjects.ToList();
        var links = ImmutableArray.CreateBuilder<GenerationSerializationLink>();
        foreach (var value in generated.AllObjects.Where(x => x.IsSynthetic)
                     .OrderBy(x => x.StartTime).ThenBy(x => x.Lane).ThenBy(x => x.EndTime))
        {
            var matches = remaining.Where(x => x.Lane == value.Lane && x.StartTime == value.StartTime
                && x.EndTime == value.EndTime && x.Type == value.Type).ToArray();
            if (matches.Length != 1)
            {
                links.Add(new(UntrackedGeneratedIdentity(value), BeatIdentity(value), SerializedIdentity(value),
                    null, false, GeometrySafetyAttribution.Unattributable,
                    "Exact semantic tuple did not map one-to-one after reparse; no fuzzy matching was used."));
                continue;
            }
            remaining.Remove(matches[0]);
            var invalid = value.Type == ManiaObjectType.LongNote && value.EndTime <= value.StartTime;
            links.Add(new(UntrackedGeneratedIdentity(value), BeatIdentity(value), SerializedIdentity(value),
                ReparsedIdentity(matches[0]), true,
                invalid ? GeometrySafetyAttribution.SerializationIntroducedViolation
                    : GeometrySafetyAttribution.NoViolation,
                invalid ? "Materialized LN duration is not positive." : "Exact serialized tuple reparsed one-to-one."));
        }
        return links.ToImmutable();
    }

    private static string UntrackedGeneratedIdentity(ManiaObject value) =>
        "GENOBJ-" + GenerationProvenanceRecorderResearch.Hash(
            GenerationProvenanceRecorderResearch.ObjectSemanticIdentity(value))[..24];
    private static string BeatIdentity(ManiaObject value) =>
        $"BEAT-SEMANTIC-{GenerationProvenanceRecorderResearch.ObjectSemanticIdentity(value)}";
    private static string SerializedIdentity(ManiaObject value) =>
        $"MS-L{value.Lane}-T{value.StartTime}-E{value.EndTime?.ToString() ?? "TAP"}-{value.Type}";
    private static string ReparsedIdentity(ManiaObject value) =>
        "REPARSED-" + GenerationProvenanceRecorderResearch.Hash(
            GenerationProvenanceRecorderResearch.ObjectSemanticIdentity(value))[..24];
}

public static class GenerationProvenanceJson
{
    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions Pretty = new(Canonical) { WriteIndented = true };
    public static string Serialize(GenerationProvenanceTrace trace) => JsonSerializer.Serialize(trace, Canonical);
    public static string SerializePretty<T>(T value) => JsonSerializer.Serialize(value, Pretty);
}

public sealed record SafetyProvContract(
    string SchemaVersion,
    string Phase,
    bool BehaviorChange,
    string D1HistoricalOutcome,
    string D1SafetyHistoricalOutcome,
    string DefaultPolicy,
    ImmutableArray<string> EventModel,
    ImmutableArray<string> StageSemantics,
    ImmutableArray<string> CausalAncestrySemantics,
    ImmutableArray<string> SerializationSemantics,
    ImmutableArray<string> BehaviorEquivalenceRequirements,
    ImmutableArray<string> HardStops,
    ImmutableArray<string> Outcomes,
    string Authorization,
    string NextRecommendedAction);

public sealed record SafetyProvContractArtifact(SafetyProvContract Contract, string SafetyProvContractHash,
    string Canonicalization);

public static class SafetyProvContractResearch
{
    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions Pretty = new(Canonical) { WriteIndented = true };

    public static SafetyProvContract Create() => new(
        GenerationProvenanceRecorderResearch.SchemaVersion, "SAFETY.PROV", false, "C", "C",
        "legacy-experimental.1",
        ["DecisionEvent may produce zero, one, or multiple physical mutations",
         "MutationEvent freezes before/after state and physical delta",
         "SequenceIndex is causal execution order; beat/time is not",
         "OpportunityKey is reused wherever the engine already defines it"],
        ["Pass1BaseOpportunity", "InteriorOpportunity", "Articulation", "Serialization"],
        ["Direct requires the exact paired decision that first changes state",
         "Downstream requires a complete unequal parent GenerationState descended from that divergence",
         "Temporal-after is never sufficient",
         "Exact GenerationState reconvergence clears prior ancestry",
         "Missing or incomplete provenance is Unattributable",
         "Stage and CausalOrigin are independent dimensions"],
        ["Placement and millisecond materialization remain separate",
         "Generated-to-reparsed identity requires an exact one-to-one semantic tuple",
         "Ambiguous or failed reparse mapping is Unattributable; fuzzy matching is forbidden",
         "No identifier is written into osu output"],
        ["Output bytes", "AddedObjects and order", "Articulation replacements",
         "RNG transcript", "Opportunity and candidate sequence", "Placement and skip decisions",
         "Final geometry", "Serialization"],
        ["Behavior drift", "RNG consumption", "Observer decision feedback", "Heuristic ancestry",
         "Missing provenance given a causal label", "Nondeterministic IDs or traces",
         "D1 or D1.SAFETY reinterpretation", "Community corpus use"],
        ["A: exact viable mutation-level provenance", "B: useful neutral infrastructure with one precise frontier",
         "C: exactness requires drift, guessing, or unsafe coupling"],
        "RESEARCH_SHADOW_ONLY_NO_BEHAVIORAL_EXPERIMENT_AUTHORIZED", "ROADMAP_REVIEW");

    public static string ComputeHash(SafetyProvContract contract) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contract, Canonical)));

    public static string SerializeArtifact()
    {
        var contract = Create();
        return JsonSerializer.Serialize(new SafetyProvContractArtifact(contract, ComputeHash(contract),
            "UTF-8 System.Text.Json camelCase; frozen ordered arrays; no timestamps, paths, UUIDs or runtime IDs"), Pretty);
    }
}
