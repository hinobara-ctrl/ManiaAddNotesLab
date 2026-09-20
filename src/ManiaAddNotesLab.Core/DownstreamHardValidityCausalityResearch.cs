using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum SafetyCausalClassification
{
    SourcePreExisting, LegacyDirect, LegacyDownstream, TreatmentDownstream,
    ArticulationDerived, TransformationDerived, SerializationDerived, Unattributable
}

public enum SafetyCausalRootCause
{
    G1Specific, PerturbationGeneral, LegacyGeneral, OracleOrSemanticMismatch,
    ProvenanceOrStateModelGap, MultipleCauses, Unresolved
}

public sealed record SafetyCausalCase(
    string ChartId, int Seed, string Arm, string ViolationId, string GeometrySignature,
    GeometryHardViolationKind ViolationKind, SafetyCausalClassification Classification,
    SafetyCausalRootCause RootCause, string MutationId, long MutationSequence,
    string OpportunityId, string? SourceObjectIdentity, string? AnchorIdentity,
    string CandidateIdentity, int Lane, ManiaObjectType ObjectType, int TapTime,
    string BlockingLongNoteIdentity, int BlockingLnLane, int BlockingLnStartTime, int BlockingLnEndTime,
    decimal CandidateInternalBeat, decimal BlockingLnInternalEndBeat,
    decimal CandidateMaterializedBeat, decimal BlockingLnMaterializedEndBeat,
    bool PlacementAccepted, string PlacementAuthority, string PlacementRule,
    bool InternalBeatConflict, bool MaterializedConflict, bool EndpointOperatorDiffers,
    bool GeometrySnapshotWasStale, string? VisiblePreviousIdentity, string? VisibleNextIdentity,
    string PreGeometryState, string PostGeometryState, string PreGenerationState,
    string PostGenerationState, long? PreRngPosition, long? PostRngPosition,
    string? ImmediateG1DivergenceId, string? ImmediateG1OpportunityId, int? OpportunityDistance,
    bool FullStateReconvergedBeforeViolation, bool GeometryReconvergedBeforeViolation,
    bool RngReconvergedBeforeViolation, bool SerializationChangedCoordinates,
    string FirstSemanticDivergence, string EvidenceStatus);

public sealed record SafetyCausalAnalysis(
    ImmutableArray<SafetyCausalCase> Cases, int HardConditions, int SourcePreExisting,
    int LegacyDirect, int LegacyDownstream, int TreatmentDownstream, int Unattributable);

public static class DownstreamHardValidityCausalityResearch
{
    public const string SchemaVersion = "safety-causal-downstream-hard-validity.1";

    public static SafetyCausalAnalysis Analyze(string chartId, int seed, string arm, ManiaChart source,
        ManiaChart generated, GenerationProvenanceTrace trace,
        IReadOnlySet<string>? comparisonSignatures = null, ProvenancePairComparison? pair = null,
        GenerationProvenanceTrace? pairedControlTrace = null)
    {
        var timeline = new BeatTimeline(source.TimingPoints);
        var profile = MapperEvidenceProfileBuilder.Build(source);
        var role = arm == "control" ? GeometrySnapshotRole.Control : GeometrySnapshotRole.Treatment;
        var finalObjects = generated.AllObjects.ToArray();
        var reparsedObjects = OsuBeatmap.Parse(OsuBeatmap.Write(generated, 0)).OriginalObjects;
        if (generated.ArticulationReplacements.Count != 0)
            throw new InvalidOperationException("SAFETY.CAUSAL frozen certification requires articulation OFF.");
        var insertions = trace.Mutations.Where(x => x.MutationKind is GenerationMutationKind.InsertTap
            or GenerationMutationKind.InsertLongNote).OrderBy(x => x.SequenceIndex).ToArray();
        if (insertions.Length != generated.AddedObjects.Count)
            throw new InvalidOperationException("Mutation provenance does not map one-to-one to AddedObjects.");

        var safetyObjects = finalObjects.Select((x, i) => GeometrySafetyAttributionResearch.Object(
            profile.ChartFingerprint, role, x, timeline.ToBeatDecimal(x.StartTime),
            x.EndTime is null ? timeline.ToBeatDecimal(x.StartTime) : timeline.ToBeatDecimal(x.EndTime.Value), i))
            .ToArray();
        var audit = GeometrySafetyAttributionResearch.AuditSnapshot(profile.ChartFingerprint,
            source.KeyCount, safetyObjects);
        var sourceObjects = source.OriginalObjects.Select((x, i) => GeometrySafetyAttributionResearch.Object(
            profile.ChartFingerprint, GeometrySnapshotRole.Source, x, timeline.ToBeatDecimal(x.StartTime),
            x.EndTime is null ? timeline.ToBeatDecimal(x.StartTime) : timeline.ToBeatDecimal(x.EndTime.Value), i));
        var sourceSignatures = GeometrySafetyAttributionResearch.AuditSnapshot(profile.ChartFingerprint,
            source.KeyCount, sourceObjects).HardViolations.Select(x => x.GeometrySignature)
            .ToHashSet(StringComparer.Ordinal);
        var rawBySignature = audit.RawRelations.GroupBy(x => x.GeometrySignature, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var entries = BuildEntries(source, generated, safetyObjects, insertions, timeline);
        var byId = entries.ToDictionary(x => x.Safety.Id, StringComparer.Ordinal);
        var decisions = trace.Decisions.Where(x => x.OpportunityKey is not null).ToArray();
        var decisionOrder = decisions.Select((x, i) => (x.OpportunityKey!, i))
            .GroupBy(x => x.Item1, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.First().i,
                StringComparer.Ordinal);
        var pairDownstream = pair?.DownstreamAffectedOpportunityKeys.ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);

        var result = ImmutableArray.CreateBuilder<SafetyCausalCase>();
        foreach (var finding in audit.HardViolations)
        {
            if (!rawBySignature.TryGetValue(finding.GeometrySignature, out var relation)) continue;
            var left = byId[relation.LeftObjectId];
            var right = byId[relation.RightObjectId];
            var generatedEntries = new[] { left, right }.Where(x => x.Mutation is not null).ToArray();
            var introducing = generatedEntries.OrderByDescending(x => x.Mutation!.SequenceIndex).FirstOrDefault();
            var blocker = ReferenceEquals(introducing, left) ? right : left;
            var sourcePreExisting = sourceSignatures.Contains(finding.GeometrySignature);
            var comparisonExisting = comparisonSignatures?.Contains(finding.GeometrySignature) == true;
            if (sourcePreExisting)
            {
                result.Add(Unattributed(chartId, seed, arm, finding, SafetyCausalClassification.SourcePreExisting,
                    SafetyCausalRootCause.Unresolved, "Condition already exists in SourceState."));
                continue;
            }
            if (comparisonExisting && arm == "treatment") continue;
            if (introducing?.Mutation is null)
            {
                result.Add(Unattributed(chartId, seed, arm, finding, SafetyCausalClassification.Unattributable,
                    SafetyCausalRootCause.Unresolved, "No introducing mutation maps to the final relation."));
                continue;
            }

            var mutation = introducing.Mutation;
            var preEntries = entries.Where(x => x.Mutation is null
                || x.Mutation.SequenceIndex < mutation.SequenceIndex).ToArray();
            var geometry = new LaneGeometryIndex(source.KeyCount,
                preEntries.Select(x => new TimedManiaObject(x.Object, x.InternalStartBeat, x.InternalEndBeat)).ToArray());
            var inspection = geometry.InspectTapLane(mutation.Lane, mutation.StartBeat);
            var placement = geometry.ExplainTapPlacement(mutation.StartBeat, profile)
                .LaneEvaluations.Single(x => x.Lane == mutation.Lane).IsValid;
            var blockingLn = new[] { left, right }.Single(x => x.Object.Type == ManiaObjectType.LongNote);
            var tap = new[] { left, right }.Single(x => x.Object.Type == ManiaObjectType.Tap);
            var internalConflict = blockingLn.InternalEndBeat >= tap.InternalStartBeat;
            var materializedConflict = blockingLn.Safety.EndBeat >= tap.Safety.StartBeat;
            var serializationChanged = !ContainsMaterialized(reparsedObjects, blockingLn.Object)
                || !ContainsMaterialized(reparsedObjects, tap.Object);
            var stale = inspection.Previous is not null
                && !Same(inspection.Previous.Object, blockingLn.Object) && internalConflict;
            var targetOrder = decisionOrder.GetValueOrDefault(mutation.OpportunityKey!, -1);
            var firstOrder = pair?.FirstDivergenceOpportunityKey is null ? -1
                : decisionOrder.GetValueOrDefault(pair.FirstDivergenceOpportunityKey, -1);
            var reconverged = pair?.ReconvergenceOpportunityKeys.Any(x =>
                decisionOrder.GetValueOrDefault(x, int.MaxValue) < targetOrder) == true;
            var causallyDownstream = pair?.ExactMatching == true && pair.DirectDivergenceId is not null
                && firstOrder >= 0 && targetOrder > firstOrder
                && !reconverged && pairDownstream.Contains(mutation.OpportunityKey!);
            var classification = arm == "treatment" && causallyDownstream
                ? SafetyCausalClassification.TreatmentDownstream
                : arm == "control" && blockingLn.Mutation is not null
                    ? SafetyCausalClassification.LegacyDownstream
                    : arm == "control" ? SafetyCausalClassification.LegacyDirect
                    : SafetyCausalClassification.Unattributable;
            var root = internalConflict != materializedConflict
                ? SafetyCausalRootCause.OracleOrSemanticMismatch : SafetyCausalRootCause.Unresolved;
            var geometryReconverged = pairedControlTrace is not null && GeometryReconvergedBefore(
                pairedControlTrace, trace, firstOrder, targetOrder);
            var rngReconverged = pairedControlTrace is not null && RngReconvergedBefore(
                pairedControlTrace, trace, firstOrder, targetOrder);
            var sourceIdentity = SourceIdentity(source, mutation.OpportunityKey);
            var previousIdentity = inspection.Previous is null ? null
                : GenerationProvenanceRecorderResearch.ObjectSemanticIdentity(inspection.Previous.Object);
            var nextIdentity = inspection.Next is null ? null
                : GenerationProvenanceRecorderResearch.ObjectSemanticIdentity(inspection.Next.Object);
            result.Add(new SafetyCausalCase(chartId, seed, arm, finding.Id, finding.GeometrySignature,
                finding.Violation, classification, root, mutation.MutationId, mutation.SequenceIndex,
                mutation.OpportunityKey!, sourceIdentity, AnchorIdentity(mutation.OpportunityKey),
                GenerationProvenanceRecorderResearch.ObjectSemanticIdentity(introducing.Object), mutation.Lane,
                mutation.ObjectKind, tap.Object.StartTime, StableIdentity(blockingLn), blockingLn.Object.Lane,
                blockingLn.Object.StartTime, blockingLn.Object.EndTime!.Value, tap.InternalStartBeat,
                blockingLn.InternalEndBeat, tap.Safety.StartBeat, blockingLn.Safety.EndBeat, placement,
                "AddNotesEngine.PlaceTap -> LaneGeometryIndex.FindLegalTapLanes -> CanPlaceTap",
                inspection.Rule, internalConflict, materializedConflict, false, stale,
                previousIdentity, nextIdentity, mutation.StateBefore.GeometryStateHash,
                mutation.StateAfter.GeometryStateHash, mutation.StateBefore.GenerationStateHash,
                mutation.StateAfter.GenerationStateHash, mutation.RngPositionBefore, mutation.RngPositionAfter,
                pair?.DirectDivergenceId, pair?.FirstDivergenceOpportunityKey,
                firstOrder < 0 || targetOrder < 0 ? null : targetOrder - firstOrder,
                reconverged, geometryReconverged, rngReconverged,
                serializationChanged,
                internalConflict == materializedConflict
                    ? "No proven placement/oracle divergence."
                    : "The candidate LN endpoint was retained as an intended decimal beat although it materialized to the tap millisecond; placement saw endBeat < tapBeat while final HardValidity normalized both from milliseconds and saw equality.",
                root == SafetyCausalRootCause.Unresolved ? "UNRESOLVED" : "PROVEN_EXACT_REPLAY"));
        }
        return new(result.ToImmutable(), audit.HardViolations.Length,
            result.Count(x => x.Classification == SafetyCausalClassification.SourcePreExisting),
            result.Count(x => x.Classification == SafetyCausalClassification.LegacyDirect),
            result.Count(x => x.Classification == SafetyCausalClassification.LegacyDownstream),
            result.Count(x => x.Classification == SafetyCausalClassification.TreatmentDownstream),
            result.Count(x => x.Classification == SafetyCausalClassification.Unattributable));
    }

    private static RuntimeEntry[] BuildEntries(ManiaChart source, ManiaChart generated,
        GeometrySafetyObject[] safety, GenerationMutationEvent[] mutations, BeatTimeline timeline)
    {
        var result = new List<RuntimeEntry>();
        for (var i = 0; i < source.OriginalObjects.Count; i++)
        {
            var value = source.OriginalObjects[i];
            result.Add(new RuntimeEntry(safety[i], value, timeline.ToBeatDecimal(value.StartTime),
                value.EndTime is null ? timeline.ToBeatDecimal(value.StartTime)
                    : timeline.ToBeatDecimal(value.EndTime.Value), null));
        }
        for (var i = 0; i < generated.AddedObjects.Count; i++)
        {
            var value = generated.AddedObjects[i];
            var mutation = mutations[i];
            if (value.Lane != mutation.Lane || value.Type != mutation.ObjectKind
                || timeline.ToTimeMilliseconds(mutation.StartBeat) != value.StartTime
                || (value.EndTime is not null && timeline.ToTimeMilliseconds(mutation.EndBeat) != value.EndTime))
                throw new InvalidOperationException("AddedObjects order no longer matches mutation provenance.");
            result.Add(new RuntimeEntry(safety[source.OriginalObjects.Count + i], value,
                mutation.StartBeat, mutation.EndBeat, mutation));
        }
        return result.ToArray();
    }

    private static bool GeometryReconvergedBefore(GenerationProvenanceTrace control,
        GenerationProvenanceTrace treatment, int firstOrder, int targetOrder)
    {
        var a = control.Decisions.Where(x => x.OpportunityKey is not null).ToArray();
        var b = treatment.Decisions.Where(x => x.OpportunityKey is not null).ToArray();
        for (var i = Math.Max(0, firstOrder + 1); i < Math.Min(targetOrder, Math.Min(a.Length, b.Length)); i++)
            if (a[i].StateBefore.GeometryStateHash == b[i].StateBefore.GeometryStateHash) return true;
        return false;
    }

    private static bool RngReconvergedBefore(GenerationProvenanceTrace control,
        GenerationProvenanceTrace treatment, int firstOrder, int targetOrder)
    {
        var a = control.Decisions.Where(x => x.OpportunityKey is not null).ToArray();
        var b = treatment.Decisions.Where(x => x.OpportunityKey is not null).ToArray();
        for (var i = Math.Max(0, firstOrder + 1); i < Math.Min(targetOrder, Math.Min(a.Length, b.Length)); i++)
            if (a[i].StateBefore.RootRngPosition == b[i].StateBefore.RootRngPosition) return true;
        return false;
    }

    private static string? SourceIdentity(ManiaChart source, string? opportunity)
    {
        if (opportunity is null) return null;
        var marker = "-S";
        var start = opportunity.IndexOf(marker, StringComparison.Ordinal);
        var finish = opportunity.IndexOf("-T", start + marker.Length, StringComparison.Ordinal);
        if (start < 0 || finish < 0 || !int.TryParse(opportunity[(start + 2)..finish], out var sequence)) return null;
        var value = source.OriginalObjects.FirstOrDefault(x => x.Sequence == sequence);
        return value is null ? null : GenerationProvenanceRecorderResearch.ObjectSemanticIdentity(value);
    }

    private static string? AnchorIdentity(string? opportunity)
    {
        if (opportunity is null) return null;
        var marker = "-A";
        var start = opportunity.LastIndexOf(marker, StringComparison.Ordinal);
        return start < 0 || opportunity.EndsWith("-ANA", StringComparison.Ordinal)
            ? null : opportunity[(start + 2)..];
    }

    private static string StableIdentity(RuntimeEntry value) => value.Mutation?.ObjectIdentity
        ?? GenerationProvenanceRecorderResearch.ObjectSemanticIdentity(value.Object);
    private static bool Same(ManiaObject a, ManiaObject b) => a.Lane == b.Lane && a.StartTime == b.StartTime
        && a.EndTime == b.EndTime && a.Type == b.Type && a.Sequence == b.Sequence && a.Origin == b.Origin;
    private static bool ContainsMaterialized(IEnumerable<ManiaObject> values, ManiaObject expected) =>
        values.Any(x => x.Lane == expected.Lane && x.StartTime == expected.StartTime
            && x.EndTime == expected.EndTime && x.Type == expected.Type);

    private static SafetyCausalCase Unattributed(string chart, int seed, string arm,
        GeometrySafetyFinding finding, SafetyCausalClassification classification,
        SafetyCausalRootCause root, string explanation) => new(chart, seed, arm, finding.Id,
        finding.GeometrySignature, finding.Violation, classification, root, "", -1, "", null, null,
        "", -1, ManiaObjectType.Tap, -1, "", -1, -1, -1, 0, 0, 0, 0, false, "", "", false,
        false, false, false, null, null, "", "", "", "", null, null, null, null, null, false,
        false, false, false, explanation, "UNATTRIBUTABLE");

    private sealed record RuntimeEntry(GeometrySafetyObject Safety, ManiaObject Object,
        decimal InternalStartBeat, decimal InternalEndBeat, GenerationMutationEvent? Mutation);
}

public sealed record SafetyCausalContract(string SchemaVersion, string Phase, string HumanAuthorization,
    string RepositoryEntryHead, string ResearchQuestion, ImmutableArray<string> TreatmentCases,
    string ControlScope, ImmutableArray<string> CausalTaxonomy, ImmutableArray<string> ProvenanceSemantics,
    string ReconvergenceSemantics, bool NoRemediation, bool InstrumentationMustNotInterfere,
    bool DeterministicReplayRequired, bool SourcePreExistingMustRemainDistinct,
    bool PlacementOracleComparisonRequired, string DefaultPolicy, bool DefaultBehaviorChanged,
    bool GenerationSemanticsChanged, string NextPhaseAuthorization);
public sealed record SafetyCausalContractArtifact(SafetyCausalContract Contract, string CanonicalSha256,
    string Canonicalization);

public static class SafetyCausalContractResearch
{
    private static readonly JsonSerializerOptions Canonical = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions Pretty = new(Canonical) { WriteIndented = true };

    public static SafetyCausalContract Create() => new(DownstreamHardValidityCausalityResearch.SchemaVersion,
        "SAFETY.CAUSAL", "AUTHORIZED_DOWNSTREAM_HARD_VALIDITY_CAUSALITY_FORENSICS",
        "f54c3be692fe4f9bfeffe2dc892297e61cf327c1",
        "Why can runtime placement accept a mutation that canonical HardValidity later proves introduced TapOnHeldLongNote?",
        ["02D9...E3EB seeds 9,11,17", "20651...C788 seed 11"],
        "All 200 legacy control hard-validity conditions in frozen G1.GATE C11 seeds 1-20",
        Enum.GetNames<SafetyCausalClassification>().Order(StringComparer.Ordinal).ToImmutableArray(),
        ["Exact mutation identity and pre/post GenerationState are required",
         "Temporal-after is not causal downstream", "Full GenerationState reconvergence clears G1 ancestry",
         "Missing evidence is Unattributable"],
        "Full GenerationState equality clears ancestry; geometry-only or RNG-only equality does not",
        true, true, true, true, true, "legacy-experimental.1", false, false,
        "NOT_AUTHORIZED");

    public static string ComputeHash(SafetyCausalContract contract) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contract, Canonical)));
    public static string SerializeArtifact()
    {
        var contract = Create();
        return JsonSerializer.Serialize(new SafetyCausalContractArtifact(contract, ComputeHash(contract),
            "UTF-8 System.Text.Json camelCase; frozen ordered arrays; no timestamps or machine paths"), Pretty);
    }
}
