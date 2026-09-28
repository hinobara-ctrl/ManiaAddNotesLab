using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal enum Lane0Family { RiceHeadCompletion, G1InteriorSpatial }
internal enum Lane0HoldoutState { JointUnique, JointAmongAlternatives, Contradiction, NoContext, AmbiguousIntegrity }

internal sealed record Lane0Occurrence(
    Lane0Family Family, string ChartId, int KeyCount, string OccurrenceId, string GroupId, string EventId,
    string? ParentId, string QuerySignature, string JointResultSignature, string TemporalResultSignature,
    string SpatialResultSignature, ImmutableArray<int> ObservationIds,
    ImmutableArray<int> ReleaseEndpointObservationIds, ImmutableArray<int> FutureHeldObservationIds,
    bool IsSynthetic = false, ImmutableArray<string> ComponentOccurrenceIds = default, bool CompleteIdentity = true,
    int? AnchorTime = null);

internal sealed record Lane0LeakageAudit(
    int Target, int TargetGroup, int Parent, int ReleaseEndpoint,
    int TemporalExclusion, int FutureHeldIdentityExclusion, int BothTemporalAndIdentity, int SameEvent,
    int SyntheticTeaching, int CrossChart, int ArtificialComposition, int DuplicateObservationIdentity,
    int IncompleteOrMispairedIdentity, int DonorsConsidered, int UniqueExcluded,
    ImmutableArray<Lane0Occurrence> Eligible)
{
    public int UniqueFutureHeldExcluded => TemporalExclusion + FutureHeldIdentityExclusion
        - BothTemporalAndIdentity;
    public int IntegrityFailures => SyntheticTeaching + CrossChart + ArtificialComposition
        + DuplicateObservationIdentity + IncompleteOrMispairedIdentity;
    public bool PartitionValid => Eligible.Length + UniqueExcluded == DonorsConsidered;
}

internal sealed record Lane0Holdout(
    Lane0Family Family, string ChartId, int KeyCount, string OccurrenceId, Lane0HoldoutState State,
    int ComparableDonors, int MatchingJointDonors, int DistinctAlternatives, bool TemporalMarginalSupported,
    bool SpatialMarginalSupported, bool JointSupported, bool IndependentSupported, Lane0LeakageAudit Leakage);

internal static class Lane0SpatialResearch
{
    public static ImmutableArray<Lane0Holdout> EvaluateAll(ImmutableArray<Lane0Occurrence> occurrences,
        bool requireTemporalIntegrity = true) =>
        occurrences.GroupBy(x => (x.Family, x.ChartId, x.KeyCount, x.QuerySignature))
            .SelectMany(bucket =>
            {
                var values = bucket.OrderBy(x => x.OccurrenceId, StringComparer.Ordinal).ToArray();
                return values.Select(target => Evaluate(target, values, requireTemporalIntegrity));
            }).OrderBy(x => x.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();

    public static Lane0LeakageAudit Audit(Lane0Occurrence target, IEnumerable<Lane0Occurrence> vocabulary,
        bool requireTemporalIntegrity = true)
    {
        var eligible = ImmutableArray.CreateBuilder<Lane0Occurrence>();
        var targetCount = 0; var group = 0; var parent = 0; var release = 0;
        var temporal = 0; var futureIdentity = 0; var both = 0;
        var sameEvent = 0; var synthetic = 0; var crossChart = 0; var composition = 0; var duplicate = 0;
        var incomplete = 0; var considered = 0; var excluded = 0;
        var targetIds = target.ObservationIds.ToHashSet();
        var releaseIds = target.ReleaseEndpointObservationIds.ToHashSet();
        var futureIds = target.FutureHeldObservationIds.ToHashSet();
        foreach (var donor in vocabulary)
        {
            considered++;
            var violations = false;
            if (donor.OccurrenceId == target.OccurrenceId) { targetCount++; violations = true; }
            else if (donor.ObservationIds.Any(targetIds.Contains)) { targetCount++; violations = true; }
            if (donor.GroupId == target.GroupId) { group++; violations = true; }
            if (target.Family == Lane0Family.G1InteriorSpatial && target.ParentId is not null
                && donor.ParentId == target.ParentId) { parent++; violations = true; }
            if (donor.ReleaseEndpointObservationIds.Any(releaseIds.Contains)) { release++; violations = true; }
            // These reasons are deliberately orthogonal. One donor can trigger both counters,
            // while UniqueExcluded still counts the donor exactly once.
            var temporalViolation = target.Family == Lane0Family.G1InteriorSpatial
                && target.AnchorTime is not null && donor.AnchorTime is not null
                && donor.AnchorTime.Value >= target.AnchorTime.Value;
            var identityViolation = donor.ObservationIds.Any(futureIds.Contains);
            if (temporalViolation) temporal++;
            if (identityViolation) futureIdentity++;
            if (temporalViolation && identityViolation) both++;
            if (temporalViolation || identityViolation) violations = true;
            if (donor.EventId == target.EventId) { sameEvent++; violations = true; }
            if (donor.IsSynthetic) { synthetic++; violations = true; }
            if (!string.Equals(donor.ChartId, target.ChartId, StringComparison.Ordinal))
            { crossChart++; violations = true; }
            if (!donor.ComponentOccurrenceIds.IsDefaultOrEmpty
                && donor.ComponentOccurrenceIds.Distinct(StringComparer.Ordinal).Count() > 1)
            { composition++; violations = true; }
            if (donor.ObservationIds.Distinct().Count() != donor.ObservationIds.Length)
            { duplicate++; violations = true; }
            if (!donor.CompleteIdentity || string.IsNullOrWhiteSpace(donor.OccurrenceId)
                || string.IsNullOrWhiteSpace(donor.GroupId) || string.IsNullOrWhiteSpace(donor.EventId)
                || string.IsNullOrWhiteSpace(donor.QuerySignature)
                || string.IsNullOrWhiteSpace(donor.JointResultSignature)
                || donor.ObservationIds.IsDefaultOrEmpty
                || (requireTemporalIntegrity && donor.Family == Lane0Family.G1InteriorSpatial
                    && donor.AnchorTime is null))
            { incomplete++; violations = true; }
            if (!violations && donor.Family == target.Family && donor.KeyCount == target.KeyCount
                && donor.QuerySignature == target.QuerySignature) eligible.Add(donor);
            else excluded++;
        }
        return new(targetCount, group, parent, release, temporal, futureIdentity, both, sameEvent,
            synthetic, crossChart, composition, duplicate, incomplete, considered, excluded,
            eligible.ToImmutable());
    }

    public static Lane0Holdout Evaluate(Lane0Occurrence target, IEnumerable<Lane0Occurrence> vocabulary,
        bool requireTemporalIntegrity = true)
    {
        var audit = Audit(target, vocabulary, requireTemporalIntegrity);
        if (!target.CompleteIdentity || target.ObservationIds.IsDefaultOrEmpty
            || (requireTemporalIntegrity && target.Family == Lane0Family.G1InteriorSpatial
                && target.AnchorTime is null)
            || audit.IntegrityFailures > 0 || !audit.PartitionValid)
            return new(target.Family, target.ChartId, target.KeyCount, target.OccurrenceId,
                Lane0HoldoutState.AmbiguousIntegrity, 0, 0, 0, false, false, false, false, audit);
        var donors = audit.Eligible;
        var matching = donors.Count(x => x.JointResultSignature == target.JointResultSignature);
        var alternatives = donors.Select(x => x.JointResultSignature).Distinct(StringComparer.Ordinal).Count();
        var temporal = donors.Any(x => x.TemporalResultSignature == target.TemporalResultSignature);
        var spatial = donors.Any(x => x.SpatialResultSignature == target.SpatialResultSignature);
        var state = donors.Length == 0 ? Lane0HoldoutState.NoContext
            : matching == 0 ? Lane0HoldoutState.Contradiction
            : alternatives == 1 ? Lane0HoldoutState.JointUnique
            : Lane0HoldoutState.JointAmongAlternatives;
        return new(target.Family, target.ChartId, target.KeyCount, target.OccurrenceId, state,
            donors.Length, matching, alternatives, temporal, spatial, matching > 0, matching > 0, audit);
    }
}

internal sealed record Lane0OutcomeInputs(
    bool EvidenceCriteriaPassed,
    bool EnvironmentBlocked,
    bool PreregisteredContractValid,
    bool AdversarialControlsPassed,
    int IntegrityFailures,
    bool Deterministic,
    bool NonInterferencePassed,
    int RngCalls);

internal static class Lane0OutcomeClassifier
{
    public static string Classify(Lane0OutcomeInputs input)
    {
        if (!input.PreregisteredContractValid || !input.AdversarialControlsPassed
            || input.IntegrityFailures != 0 || !input.Deterministic
            || !input.NonInterferencePassed || input.RngCalls != 0)
            return "INVALID";
        if (input.EnvironmentBlocked) return "BLOCKED";
        return input.EvidenceCriteriaPassed ? "FEASIBILITY_DEMONSTRATED" : "LIMITED_PARK";
    }
}

internal sealed record Lane0FamilyCounts(int Structural, int Holdouts, int Operational, int WithContext,
    int JointUnique, int JointAmongAlternatives, int Contradiction, int NoContext, int MarginalOnly,
    int IndependentSupported, int OperationalIndependentSupported, int IntegrityFailures);
internal sealed record Lane0ChartResult(string ChartId, string Family, int KeyCount, int OriginalObjects,
    Lane0FamilyCounts Rice, Lane0FamilyCounts G1, int G1LegacyLeakageFailures, int ResearchRngCalls,
    string SemanticSha256);
internal sealed record Lane0OutcomeCriteria(string Family, int SupportedCharts, int SupportedKeymodes,
    int OperationalSupportedTargets, bool Passed);
internal sealed record Lane0OfficialResult(string SchemaVersion, string ContractSha256, string CorpusManifestSha256,
    int UniqueCharts, int CorpusLocations, int ExactDuplicates, ImmutableArray<Lane0ChartResult> Charts,
    ImmutableArray<Lane0OutcomeCriteria> Criteria, string Outcome, int RngCalls, bool BehaviorChanged,
    bool Deterministic, string SemanticSha256);

internal sealed record Lane0Contract(
    string SchemaVersion, string RepositoryEntryHead, string ImplementationSha256, string HarnessSha256,
    string CorpusManifestSha256, string MemoryLimit, string Question, ImmutableArray<string> Families,
    ImmutableArray<string> PopulationUniverses, ImmutableArray<string> LeakageControls,
    ImmutableArray<string> Outcomes, string DemonstratedCriterion, ImmutableSortedDictionary<string,string> ReusedIdentities);
internal sealed record Lane0ContractArtifact(Lane0Contract Contract, string CanonicalSha256, string Canonicalization);

internal sealed record Lane0CorrectiveContract(
    string SchemaVersion,
    string ApprovedOriginalHead,
    string HistoricalBaselineHead,
    string RequiredEvaluationPublishedHead,
    string RepairImplementationSha256,
    string CorrectiveHarnessSha256,
    string CorpusManifestSha256,
    string HistoricalContractSha256,
    string MemoryLimit,
    string Defect,
    string RepairSemantics,
    ImmutableArray<string> Families,
    ImmutableSortedDictionary<string, int> HistoricalDenominators,
    ImmutableArray<string> Exclusions,
    ImmutableArray<string> RequiredControls,
    ImmutableSortedDictionary<string, string> OutcomeRules,
    ImmutableArray<string> PairedComparison,
    string HistoricalOperationalG1Analysis,
    ImmutableArray<string> StopConditions,
    ImmutableArray<string> InferenceLimits,
    ImmutableSortedDictionary<string, string> ReusedDependencies);
internal sealed record Lane0CorrectiveContractArtifact(
    Lane0CorrectiveContract Contract, string CanonicalSha256, string Canonicalization);

internal sealed record Lane0CorrectiveHardeningContract(
    string SchemaVersion,
    string PublishedRepairHead,
    string HistoricalLane0Head,
    string ParentCorrectiveContractSha256,
    string RequiredPublicationBinding,
    string RepairImplementationSha256,
    ImmutableArray<string> RepairImplementationFiles,
    string CorrectiveHarnessSha256,
    ImmutableArray<string> CorrectiveHarnessFiles,
    string CorpusManifestSha256,
    string MemoryLimit,
    string FutureHeldIndexSemantics,
    ImmutableArray<string> ExclusionCounters,
    string DonorPartitionInvariant,
    ImmutableArray<string> RequiredControls,
    ImmutableSortedDictionary<string, string> OutcomeRules,
    ImmutableArray<string> FutureResultColumns,
    ImmutableSortedDictionary<string, string> ReusedDependencies);
internal sealed record Lane0CorrectiveHardeningContractArtifact(
    Lane0CorrectiveHardeningContract Contract, string CanonicalSha256, string Canonicalization);
internal sealed record Lane0CounterClosureContract(
    string SchemaVersion,
    string AuditedBaselineHead,
    string ParentHardeningContractSha256,
    string IdentityAlgorithm,
    string RequiredPublicationBinding,
    string RepairImplementationSha256,
    ImmutableArray<string> RepairImplementationFiles,
    string CorrectiveHarnessSha256,
    ImmutableArray<string> CorrectiveHarnessFiles,
    string CorpusManifestSha256,
    string MemoryLimit,
    string CounterSemantics,
    ImmutableArray<string> RequiredControls,
    ImmutableSortedDictionary<string, string> OutcomeRules,
    ImmutableSortedDictionary<string, string> ReusedDependencies);
internal sealed record Lane0CounterClosureContractArtifact(
    Lane0CounterClosureContract Contract, string CanonicalSha256, string Canonicalization);
internal sealed record Lane0CorrectivePublicationBinding(
    string ContractSha256, string ApprovedPublishedHead, bool ExplicitHumanAuthorization);
internal sealed record Lane0CorrectiveReadinessChecks(
    bool ContractCanonical,
    bool ImplementationIdentity,
    bool HarnessIdentity,
    bool ReusedDependencies,
    bool PublicationBindingPresent,
    bool PublishedHeadMatches,
    bool HumanAuthorized,
    bool ManifestVerified);
internal sealed record Lane0CorrectiveReadinessReport(
    string Outcome, string Reason, Lane0CorrectiveReadinessChecks Checks);

internal static class Lane0FeasibilityRunner
{
    public const string EntryHead = "be08f3f2f0191a5c972ad16449a7199dd07f2e3f";
    public const string CorpusIdentity = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    private const string Schema = "lane-0-original-spatial-feasibility.1";
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions CanonicalJson = new()
    {
        WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly string[] ImplementationFiles =
    [
        "src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "src/ManiaAddNotesLab.Core/ChartAnalysis.cs",
        "src/ManiaAddNotesLab.Core/ExactHeadRelationResearch.cs",
        "src/ManiaAddNotesLab.Core/ChordCompletionResearch.cs",
        "src/ManiaAddNotesLab.Core/ResultingStateCompositionResearch.cs",
        "src/ManiaAddNotesLab.Core/InteriorRelationFeasibilityResearch.cs",
        "src/ManiaAddNotesLab.Core/InteriorRelationMembershipResearch.cs",
        "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
        "src/ManiaAddNotesLab.Core/AddNotesEngine.cs",
        "src/ManiaAddNotesLab.Core/FrozenC11ManifestResearch.cs"
    ];
    private static readonly string[] HarnessFiles =
    [
        "tools/ManiaAddNotesLab.Experiments/Lane0FeasibilityRunner.cs",
        "tools/ManiaAddNotesLab.Experiments/Program.cs",
        "tests/ManiaAddNotesLab.Tests/Lane0FeasibilityTests.cs",
        "tests/ManiaAddNotesLab.Tests/ManiaAddNotesLab.Tests.csproj",
        "docs/PHASE_LANE_0_FEASIBILITY_DESIGN.md"
    ];
    private static readonly string[] ReusedFiles =
    [
        "docs/PHASE_D0_CHORD_COMPLETION_RECONSTRUCTION_REPORT.md",
        "docs/g1_0_interior_relation_contract.json",
        "docs/g1_design_interior_relation_admission_contract.json",
        "docs/safety_remediation_gate_final_recertification_contract.json"
    ];
    private static readonly string[] CorrectiveImplementationFiles =
    [
        "tools/ManiaAddNotesLab.Experiments/Lane0FeasibilityRunner.cs"
    ];
    private static readonly string[] CorrectiveHarnessFiles =
    [
        "tools/ManiaAddNotesLab.Experiments/Program.cs",
        "tests/ManiaAddNotesLab.Tests/Lane0FeasibilityTests.cs",
        "tests/ManiaAddNotesLab.Tests/ManiaAddNotesLab.Tests.csproj",
        "tools/DocConsistency/Program.cs"
    ];
    private static readonly string[] CorrectiveDependencies =
    [
        "src/ManiaAddNotesLab.Core/InteriorRelationFeasibilityResearch.cs",
        "src/ManiaAddNotesLab.Core/InteriorRelationHoldoutAuditor.cs",
        "src/ManiaAddNotesLab.Core/InteriorRelationMembershipResearch.cs",
        "docs/lane_0_feasibility_contract.json",
        "docs/PHASE_LANE_0_FEASIBILITY_REPORT.md",
        "docs/g1_0_interior_relation_contract.json",
        "docs/g1_design_interior_relation_admission_contract.json"
    ];
    private static readonly string[] CorrectiveHardeningDependencies =
    [
        "docs/lane_0_future_held_remediation_contract.json",
        "docs/lane_0_feasibility_contract.json",
        "docs/PHASE_LANE_0_FEASIBILITY_REPORT.md",
        "src/ManiaAddNotesLab.Core/InteriorRelationFeasibilityResearch.cs",
        "src/ManiaAddNotesLab.Core/InteriorRelationHoldoutAuditor.cs",
        "src/ManiaAddNotesLab.Core/InteriorRelationMembershipResearch.cs"
    ];
    private static readonly string[] CounterClosureHarnessFiles =
    [
        "tools/ManiaAddNotesLab.Experiments/Program.cs",
        "tests/ManiaAddNotesLab.Tests/Lane0FeasibilityTests.cs",
        "tests/ManiaAddNotesLab.Tests/ManiaAddNotesLab.Tests.csproj",
        "tools/DocConsistency/Program.cs",
        "tools/IndependentLane0IdentityVerifier.ps1"
    ];

    public static string PrepareCorrectiveContract(string root, string contractPath)
    {
        throw new InvalidOperationException(
            "lane-0-future-held-remediation.1 is frozen at F28F35AA...F8B0A7; use the versioned hardening preparation command.");
    }

    public static string PrepareCorrectiveHardeningContract(string root, string contractPath)
    {
        const string publishedRepairHead = "9a8d28f311dc07dc685cc92a23c76b508323fc25";
        if (ReadHead(root) != publishedRepairHead)
            throw new InvalidDataException("Corrective hardening must start from published repair HEAD 9a8d28f.");
        var dependencies = CorrectiveHardeningDependencies.ToImmutableSortedDictionary(x => x,
            x => FileHash(Path.Combine(root, x)), StringComparer.Ordinal);
        var contract = new Lane0CorrectiveHardeningContract(
            "lane-0-future-held-hardening.2",
            publishedRepairHead,
            "12ee8799528d9cf9d64d8d9a9ab4b45ba955db0f",
            "F28F35AA991F3AF20BEEBBEE6AC1D9D3C182E71C62E4044E4DB0792857F8B0A7",
            "docs/lane_0_corrective_publication_binding.json",
            TreeIdentity(root, CorrectiveImplementationFiles),
            CorrectiveImplementationFiles.Order(StringComparer.Ordinal).ToImmutableArray(),
            TreeIdentity(root, CorrectiveHarnessFiles),
            CorrectiveHarnessFiles.Order(StringComparer.Ordinal).ToImmutableArray(),
            CorpusIdentity,
            "0x400000000",
            "Chart-local complete-relation identities are grouped once by AnchorTime; descending cumulative original-ID sets are materialized once per distinct anchor and shared by occurrences at that anchor.",
            ["temporal_exclusion", "future_held_identity_exclusion", "both_reasons", "unique_excluded",
             "donors_considered", "admitted"],
            "admitted + unique_excluded == donors_considered for every target query; both_reasons is diagnostic and never increments unique_excluded twice.",
            ["official-builder-prior-admitted", "official-builder-same-time-excluded",
             "official-builder-future-excluded", "official-builder-prior-identity-collision-excluded",
             "official-builder-both-reasons-excluded-once", "missing-anchor-invalid",
             "target-group-parent-release-event-synthetic-cross-chart-composition-controls",
             "indexed-builder-naive-equivalence", "zero-rng", "no-chart-mutation"],
            ImmutableSortedDictionary<string, string>.Empty
                .Add("BLOCKED", "A required publication binding, approved HEAD, explicit authorization or manifest verification is absent before corpus access.")
                .Add("FEASIBILITY_DEMONSTRATED", "Reserved for one later authorized valid C11 evaluation satisfying the frozen scientific criterion.")
                .Add("INVALID", "Canonical contract, implementation, harness, dependency, integrity, controls, determinism, RNG or non-interference verification fails.")
                .Add("LIMITED_PARK", "Reserved for one later authorized valid complete C11 evaluation whose corrected evidence is insufficient."),
            ["chart_id", "family", "keymode", "universe_a_structural", "universe_b_holdout",
             "universe_c_operational", "historical_support", "corrected_support", "rice_sentinel",
             "g1_occurrence_id", "donors_considered", "admitted", "temporal_exclusion",
             "future_held_identity_exclusion", "both_reasons", "unique_excluded", "state",
             "placement_count_separate_not_inferred"],
            dependencies);
        var artifact = new Lane0CorrectiveHardeningContractArtifact(contract,
            CanonicalCorrectiveHardeningContractHash(contract),
            "UTF-8 System.Text.Json semantic round-trip, camelCase, unindented; ordinal ordered file inventories/maps; file hashes use exact working-tree bytes, therefore CRLF/LF changes identity");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        WriteJson(contractPath, artifact);
        return artifact.CanonicalSha256;
    }

    public static Lane0CorrectiveReadinessReport ValidateCorrectiveReadiness(string root,
        string contractPath, string? bindingPath, string? manifestPath)
    {
        try
        {
            var artifact = JsonSerializer.Deserialize<Lane0CorrectiveHardeningContractArtifact>(
                File.ReadAllText(contractPath), Json)
                ?? throw new InvalidDataException("Corrective hardening contract is empty.");
            var canonical = CanonicalCorrectiveHardeningContractHash(artifact.Contract)
                == artifact.CanonicalSha256;
            var implementation = artifact.Contract.RepairImplementationSha256
                == TreeIdentity(root, artifact.Contract.RepairImplementationFiles);
            var harness = artifact.Contract.CorrectiveHarnessSha256
                == TreeIdentity(root, artifact.Contract.CorrectiveHarnessFiles);
            var dependencies = artifact.Contract.ReusedDependencies.All(pair =>
                FileHash(Path.Combine(root, pair.Key)) == pair.Value);
            var bindingPresent = bindingPath is not null && File.Exists(bindingPath);
            Lane0CorrectivePublicationBinding? binding = null;
            if (bindingPresent)
                binding = JsonSerializer.Deserialize<Lane0CorrectivePublicationBinding>(
                    File.ReadAllText(bindingPath!), Json);
            var headMatches = binding is not null
                && binding.ContractSha256 == artifact.CanonicalSha256
                && binding.ApprovedPublishedHead == ReadHead(root);
            var authorized = binding?.ExplicitHumanAuthorization == true;
            var manifestVerified = false;
            if (canonical && implementation && harness && dependencies && headMatches && authorized
                && manifestPath is not null && File.Exists(manifestPath))
                manifestVerified = FrozenC11ManifestResearch.Load(manifestPath).CanonicalSha256
                    == artifact.Contract.CorpusManifestSha256;
            var checks = new Lane0CorrectiveReadinessChecks(canonical, implementation, harness,
                dependencies, bindingPresent, headMatches, authorized, manifestVerified);
            return ClassifyCorrectiveReadiness(checks);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or JsonException)
        {
            var checks = new Lane0CorrectiveReadinessChecks(false, false, false, false,
                false, false, false, false);
            return new("INVALID", exception.Message, checks);
        }
    }

    internal static Lane0CorrectiveReadinessReport ClassifyCorrectiveReadiness(
        Lane0CorrectiveReadinessChecks checks)
    {
        if (!checks.ContractCanonical || !checks.ImplementationIdentity || !checks.HarnessIdentity
            || !checks.ReusedDependencies)
            return new("INVALID", "A frozen identity or dependency failed verification.", checks);
        if (!checks.PublicationBindingPresent || !checks.PublishedHeadMatches
            || !checks.HumanAuthorized || !checks.ManifestVerified)
            return new("BLOCKED", "Publication binding, approved HEAD, authorization and verified manifest are prerequisites.", checks);
        return new("READY_FOR_AUTHORIZED_CORRECTIVE_EXECUTION", "All pre-corpus prerequisites passed.", checks);
    }

    public static Lane0CorrectiveReadinessReport DenyCorrectiveExecution(string root,
        string contractPath, string? bindingPath, string? manifestPath)
    {
        var readiness = ValidateCorrectiveReadiness(root, contractPath, bindingPath, manifestPath);
        if (readiness.Outcome != "READY_FOR_AUTHORIZED_CORRECTIVE_EXECUTION") return readiness;
        return readiness with
        {
            Outcome = "BLOCKED",
            Reason = "This hardening command intentionally contains no C11 evaluation body; Step 2 requires a separately authorized published runner."
        };
    }

    public static string PrepareCounterClosureContract(string root, string contractPath)
    {
        const string baseline = "c471d10ed48e42ab33b8a981ef22c6eb26774f3d";
        if (ReadHead(root) != baseline)
            throw new InvalidDataException("Counter closure must start from published baseline c471d10.");
        var dependencies = CorrectiveHardeningDependencies.ToImmutableSortedDictionary(x => x,
            x => NormalizedTextFileHash(Path.Combine(root, x)), StringComparer.Ordinal);
        var contract = new Lane0CounterClosureContract(
            "lane-0-future-held-counter-closure.3",
            baseline,
            "E0BCDA19B3E05E5ECA3EE8FCC7380C4AC74ACBDDE3241D77CD2F1B6EA6274F35",
            "Decode UTF-8 with optional BOM removed; normalize CRLF and CR to LF; hash normalized UTF-8 bytes; tree hash is SHA-256 of ordinal path|fileHash rows joined by LF.",
            "docs/lane_0_counter_closure_publication_binding.json",
            NormalizedTextTreeIdentity(root, CorrectiveImplementationFiles),
            CorrectiveImplementationFiles.Order(StringComparer.Ordinal).ToImmutableArray(),
            NormalizedTextTreeIdentity(root, CounterClosureHarnessFiles),
            CounterClosureHarnessFiles.Order(StringComparer.Ordinal).ToImmutableArray(),
            CorpusIdentity,
            "0x400000000",
            "temporal_exclusion and future_held_identity_exclusion are overlapping diagnostics; both_reasons is their intersection; unique_future_held_excluded is their union; unique_excluded is the union of every exclusion barrier.",
            ["five-counter-states", "donor-partition", "official-builder", "independent-identity-verifier",
             "historical-runner-preserved", "bad-identity-invalid", "missing-authorization-blocked",
             "manifest-last-before-corpus", "corrective-execution-denied"],
            ImmutableSortedDictionary<string, string>.Empty
                .Add("BLOCKED", "Publication binding, approved HEAD, explicit authorization or manifest verification is absent before corpus access.")
                .Add("INVALID", "Canonical contract, normalized implementation/harness identity or dependency verification fails.")
                .Add("READY_FOR_AUTHORIZED_CORRECTIVE_EXECUTION", "Reserved for a later separately published and explicitly authorized C11 runner."),
            dependencies);
        var artifact = new Lane0CounterClosureContractArtifact(contract,
            CanonicalCounterClosureContractHash(contract),
            "UTF-8 System.Text.Json typed semantic round-trip, camelCase, unindented; code identities use the separately declared UTF-8/LF-normalized tree algorithm");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        WriteJson(contractPath, artifact);
        return artifact.CanonicalSha256;
    }

    public static Lane0CorrectiveReadinessReport ValidateCounterClosureReadiness(string root,
        string contractPath, string? bindingPath, string? manifestPath)
    {
        try
        {
            var artifact = JsonSerializer.Deserialize<Lane0CounterClosureContractArtifact>(
                File.ReadAllText(contractPath), Json)
                ?? throw new InvalidDataException("Counter closure contract is empty.");
            var canonical = CanonicalCounterClosureContractHash(artifact.Contract)
                == artifact.CanonicalSha256;
            var implementation = artifact.Contract.RepairImplementationSha256
                == NormalizedTextTreeIdentity(root, artifact.Contract.RepairImplementationFiles);
            var harness = artifact.Contract.CorrectiveHarnessSha256
                == NormalizedTextTreeIdentity(root, artifact.Contract.CorrectiveHarnessFiles);
            var dependencies = artifact.Contract.ReusedDependencies.All(pair =>
                NormalizedTextFileHash(Path.Combine(root, pair.Key)) == pair.Value);
            var bindingPresent = bindingPath is not null && File.Exists(bindingPath);
            Lane0CorrectivePublicationBinding? binding = null;
            if (bindingPresent)
                binding = JsonSerializer.Deserialize<Lane0CorrectivePublicationBinding>(
                    File.ReadAllText(bindingPath!), Json);
            var headMatches = binding is not null
                && binding.ContractSha256 == artifact.CanonicalSha256
                && binding.ApprovedPublishedHead == ReadHead(root);
            var authorized = binding?.ExplicitHumanAuthorization == true;
            var manifestVerified = false;
            if (canonical && implementation && harness && dependencies && headMatches && authorized
                && manifestPath is not null && File.Exists(manifestPath))
                manifestVerified = FrozenC11ManifestResearch.Load(manifestPath).CanonicalSha256
                    == artifact.Contract.CorpusManifestSha256;
            return ClassifyCorrectiveReadiness(new(canonical, implementation, harness, dependencies,
                bindingPresent, headMatches, authorized, manifestVerified));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or JsonException)
        {
            return new("INVALID", exception.Message, new(false, false, false, false,
                false, false, false, false));
        }
    }

    public static Lane0CorrectiveReadinessReport DenyCounterClosureExecution(string root,
        string contractPath, string? bindingPath, string? manifestPath)
    {
        var readiness = ValidateCounterClosureReadiness(root, contractPath, bindingPath, manifestPath);
        if (readiness.Outcome != "READY_FOR_AUTHORIZED_CORRECTIVE_EXECUTION") return readiness;
        return readiness with
        {
            Outcome = "BLOCKED",
            Reason = "Counter closure contains no C11 evaluation body; execution remains separately unauthorized."
        };
    }

    public static string Prepare(string root, string manifestPath, string contractPath, string artifactDirectory)
    {
        RequireHead(root);
        var manifest = FrozenC11ManifestResearch.Load(manifestPath);
        if (manifest.CanonicalSha256 != CorpusIdentity) throw new InvalidDataException("C11 identity drifted.");
        var reused = ReusedFiles.ToImmutableSortedDictionary(x => x, x => FileHash(Path.Combine(root, x)),
            StringComparer.Ordinal);
        var contract = new Lane0Contract(Schema, EntryHead, TreeIdentity(root, ImplementationFiles),
            TreeIdentity(root, HarnessFiles), manifest.CanonicalSha256, "0x400000000",
            "Do exact complete original-only rice and G1 spatial relations recur under strict chart-local holdout in >=2 charts and >=2 keymodes per family, including current operational occurrences, with zero integrity failures?",
            ["RICE_HEAD_COMPLETION", "G1_INTERIOR_SPATIAL"],
            ["A_STRUCTURAL_ORIGINAL", "B_HOLDOUT_RECONSTRUCTIBLE", "C_CURRENT_OPERATIONAL"],
            ["target", "target-group", "parent", "release-endpoint", "future-held", "same-event",
             "synthetic-teaching", "cross-chart", "artificial-donor-composition", "duplicate-observation",
             "incomplete-or-mispaired-identity"],
            ["FEASIBILITY_DEMONSTRATED", "LIMITED_PARK", "BLOCKED", "INVALID"],
            "Each family has independent joint support in >=2 charts and >=2 keymodes, at least one supported operational target, zero integrity failures, zero RNG, no behavior change and byte-identical repeat.", reused);
        var artifact = new Lane0ContractArtifact(contract, CanonicalContractHash(contract),
            "UTF-8 System.Text.Json semantic round-trip, camelCase, unindented; ordered arrays/maps; no timestamps or private corpus paths");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        Directory.CreateDirectory(artifactDirectory);
        WriteJson(contractPath, artifact);
        WriteJson(Path.Combine(artifactDirectory, "frozen_contract.json"), artifact);
        return artifact.CanonicalSha256;
    }

    public static string Run(string root, string corpusRoot, string manifestPath, string contractPath,
        string outputDirectory)
    {
        var artifact = JsonSerializer.Deserialize<Lane0ContractArtifact>(File.ReadAllText(contractPath), Json)
            ?? throw new InvalidDataException("LANE.0 contract is empty.");
        RequireContract(root, manifestPath, artifact);
        var manifest = FrozenC11ManifestResearch.Load(manifestPath);
        var corpus = C11CorpusDiscovery.ResolveFrozen(corpusRoot, manifest.Charts);
        var first = EvaluateCorpus(corpus, artifact.CanonicalSha256, manifest.CanonicalSha256);
        var second = EvaluateCorpus(corpus, artifact.CanonicalSha256, manifest.CanonicalSha256);
        var firstBytes = JsonSerializer.SerializeToUtf8Bytes(first, Json);
        var secondBytes = JsonSerializer.SerializeToUtf8Bytes(second, Json);
        if (!firstBytes.SequenceEqual(secondBytes)) throw new InvalidDataException("LANE.0 repeat is not byte-identical.");
        var final = first with { Deterministic = true, SemanticSha256 = Hash(firstBytes) };
        Directory.CreateDirectory(outputDirectory);
        WriteJson(Path.Combine(outputDirectory, "scientific_summary.json"), final);
        WriteJson(Path.Combine(outputDirectory, "chart_results.json"), final.Charts);
        WriteJson(Path.Combine(outputDirectory, "integrity.json"), new
        {
            contract = artifact.CanonicalSha256, manifest = manifest.CanonicalSha256,
            corpus.IsExact, uniqueCharts = corpus.Charts.Length, locations = corpus.OsuFileCount,
            duplicates = corpus.Duplicates.Length, rngCalls = final.RngCalls,
            behaviorChanged = final.BehaviorChanged, deterministic = final.Deterministic,
            integrityFailures = final.Charts.Sum(x => x.Rice.IntegrityFailures + x.G1.IntegrityFailures
                + x.G1LegacyLeakageFailures)
        });
        WriteChecksums(outputDirectory);
        return final.Outcome;
    }

    private static Lane0OfficialResult EvaluateCorpus(C11FrozenCorpusVerification corpus, string contract,
        string manifest)
    {
        var charts = corpus.Charts.OrderBy(x => x.Sha256, StringComparer.Ordinal).Select(descriptor =>
        {
            var source = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var before = MapperEvidenceProfileBuilder.ComputeFingerprint(source);
            var riceResearch = ChordCompletionResearch.Evaluate(source);
            var g1Research = InteriorRelationFeasibilityResearch.Evaluate(source);
            var rice = RiceOccurrences(descriptor.Sha256, riceResearch);
            // This command is the immutable historical runner. Corrective evaluation has a
            // separate preregistration/validation route and must not reinterpret this result.
            var g1 = BuildG1Occurrences(descriptor.Sha256, g1Research, reproduceHistoricalDefect: true);
            var riceTrials = Lane0SpatialResearch.EvaluateAll(rice);
            var g1Trials = Lane0SpatialResearch.EvaluateAll(g1, requireTemporalIntegrity: false);
            var operationalAnchors = g1Research.CurrentGateAudit.Where(x => x.CurrentOpportunity)
                .Select(x => x.AnchorId).ToHashSet(StringComparer.Ordinal);
            var g1Operational = g1.Count(x => operationalAnchors.Contains(x.GroupId));
            var legacyLeakage = g1Research.Holdouts.Sum(x => x.TargetLeakageCount + x.ParentLeakageCount
                + x.ReleaseLeakageCount + x.FutureLeakageCount + x.SameEventLeakageCount
                + x.SyntheticLeakageCount + x.CrossChartLeakageCount);
            if (before != MapperEvidenceProfileBuilder.ComputeFingerprint(source))
                throw new InvalidDataException("Research mutated chart identity.");
            var riceCounts = Counts(rice, riceTrials, _ => true);
            var g1Counts = Counts(g1, g1Trials, occurrence => operationalAnchors.Contains(occurrence.GroupId));
            var semantic = Hash(JsonSerializer.SerializeToUtf8Bytes(new { descriptor.Sha256, riceCounts, g1Counts,
                legacyLeakage }, Json));
            return new Lane0ChartResult(descriptor.Sha256, descriptor.FamilyKey, descriptor.KeyCount,
                descriptor.ObjectCount, riceCounts, g1Counts, legacyLeakage,
                g1Research.ResearchRngCalls, semantic);
        }).ToImmutableArray();
        var criteria = Enum.GetValues<Lane0Family>().Select(family =>
        {
            var selected = charts.Select(x => (x.ChartId, x.KeyCount, Counts: family == Lane0Family.RiceHeadCompletion
                ? x.Rice : x.G1)).Where(x => x.Counts.IndependentSupported > 0).ToArray();
            var supportedCharts = selected.Select(x => x.ChartId).Distinct().Count();
            var keymodes = selected.Select(x => x.KeyCount).Distinct().Count();
            var operational = selected.Sum(x => x.Counts.OperationalIndependentSupported);
            var integrity = charts.Sum(x => family == Lane0Family.RiceHeadCompletion
                ? x.Rice.IntegrityFailures : x.G1.IntegrityFailures + x.G1LegacyLeakageFailures);
            return new Lane0OutcomeCriteria(family.ToString(), supportedCharts, keymodes, operational,
                supportedCharts >= 2 && keymodes >= 2 && operational > 0 && integrity == 0);
        }).ToImmutableArray();
        var rng = charts.Sum(x => x.ResearchRngCalls);
        var outcome = criteria.All(x => x.Passed) && rng == 0
            ? "FEASIBILITY_DEMONSTRATED" : "LIMITED_PARK";
        return new(Schema, contract, manifest, charts.Length, corpus.OsuFileCount, corpus.Duplicates.Length,
            charts, criteria, outcome, rng, false, false, "PENDING_REPEAT");
    }

    private static ImmutableArray<Lane0Occurrence> RiceOccurrences(string chart,
        ChordCompletionResearchResult result) => result.ChordGroups.SelectMany(group => group.Members.Select(target =>
    {
        var visible = group.Members.Where(x => x.ObservationId != target.ObservationId).ToArray();
        var query = HeadSignature(result.KeyCount,
            visible.Where(x => x.HeadType == OriginalHeadMemberType.TapHead).Select(x => x.Lane),
            visible.Where(x => x.HeadType == OriginalHeadMemberType.LongNoteHead).Select(x => x.Lane));
        var spatial = $"L:{target.Lane}";
        var temporal = $"HT:{target.HeadType}";
        return new Lane0Occurrence(Lane0Family.RiceHeadCompletion, chart, result.KeyCount,
            $"{group.SourceGroupId}/{target.ObservationId}", group.SourceGroupId, group.SourceGroupId, null,
            query, $"{spatial}|{temporal}", temporal, spatial,
            group.HeadState.MemberObservationIds.Select(x => x.Value).ToImmutableArray(), [], []);
    })).OrderBy(x => x.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();

    internal static ImmutableArray<Lane0Occurrence> BuildG1Occurrences(string chart,
        InteriorRelationFeasibilityResult result, bool reproduceHistoricalDefect = false)
    {
        var anchors = result.StructuralAnchors.ToDictionary(x => x.AnchorId, StringComparer.Ordinal);
        var futureHeldByAnchorTime = reproduceHistoricalDefect
            ? ImmutableDictionary<int, ImmutableArray<int>>.Empty
            : BuildG1FutureHeldIndex(result, anchors);
        return result.CompleteRelations.Select(occurrence =>
        {
            var anchorId = $"{occurrence.ParentLongNoteId}-A{occurrence.AnchorTime}-B{D(occurrence.AnchorBeat)}";
            var anchor = anchors[anchorId];
            var spatial = $"WL:{occurrence.WitnessLane}|DP:{occurrence.WitnessLane - occurrence.ParentLane}|SL:{occurrence.SameLane}";
            var temporal = occurrence.RelationSignature;
            var ids = new[] { occurrence.ParentLongNoteId.Value, occurrence.WitnessLongNoteId.Value }
                .Concat(anchor.HeadWitnessIds.Select(x => x.Value)).Concat(anchor.ReleaseWitnessIds.Select(x => x.Value))
                .Distinct().Order().ToImmutableArray();
            var futureHeld = reproduceHistoricalDefect ? ImmutableArray<int>.Empty
                : futureHeldByAnchorTime[occurrence.AnchorTime];
            return new Lane0Occurrence(Lane0Family.G1InteriorSpatial, chart, result.KeyCount,
                occurrence.OccurrenceId, anchorId, anchorId, occurrence.ParentLongNoteId.ToString(),
                $"{occurrence.QuerySignature}|PL:{occurrence.ParentLane}", $"{temporal}|{spatial}", temporal,
                spatial, ids, anchor.ReleaseWitnessIds.Select(x => x.Value).ToImmutableArray(), futureHeld,
                AnchorTime: reproduceHistoricalDefect ? null : occurrence.AnchorTime);
        }).OrderBy(x => x.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();
    }

    internal static ImmutableDictionary<int, ImmutableArray<int>> BuildG1FutureHeldIndex(
        InteriorRelationFeasibilityResult result) => BuildG1FutureHeldIndex(result,
            result.StructuralAnchors.ToDictionary(x => x.AnchorId, StringComparer.Ordinal));

    private static ImmutableDictionary<int, ImmutableArray<int>> BuildG1FutureHeldIndex(
        InteriorRelationFeasibilityResult result,
        IReadOnlyDictionary<string, InteriorStructuralAnchor> anchors)
    {
        var identitiesAtTime = result.CompleteRelations.GroupBy(x => x.AnchorTime)
            .ToDictionary(group => group.Key, group => group.SelectMany(occurrence =>
            {
                var anchorId = $"{occurrence.ParentLongNoteId}-A{occurrence.AnchorTime}-B{D(occurrence.AnchorBeat)}";
                var anchor = anchors[anchorId];
                return new[] { occurrence.ParentLongNoteId.Value, occurrence.WitnessLongNoteId.Value }
                    .Concat(anchor.HeadWitnessIds.Select(id => id.Value))
                    .Concat(anchor.ReleaseWitnessIds.Select(id => id.Value));
            }).ToImmutableArray());
        var cumulative = ImmutableSortedSet<int>.Empty;
        var index = ImmutableDictionary.CreateBuilder<int, ImmutableArray<int>>();
        foreach (var time in identitiesAtTime.Keys.OrderDescending())
        {
            cumulative = cumulative.Union(identitiesAtTime[time]);
            index[time] = cumulative.ToImmutableArray();
        }
        return index.ToImmutable();
    }

    private static Lane0FamilyCounts Counts(ImmutableArray<Lane0Occurrence> occurrences,
        IReadOnlyList<Lane0Holdout> trials, Func<Lane0Occurrence, bool> operationalPredicate)
    {
        var operationalIds = occurrences.Where(operationalPredicate).Select(x => x.OccurrenceId)
            .ToHashSet(StringComparer.Ordinal);
        return new(occurrences.Length, trials.Count, operationalIds.Count,
            trials.Count(x => x.ComparableDonors > 0),
            trials.Count(x => x.State == Lane0HoldoutState.JointUnique),
            trials.Count(x => x.State == Lane0HoldoutState.JointAmongAlternatives),
            trials.Count(x => x.State == Lane0HoldoutState.Contradiction),
            trials.Count(x => x.State == Lane0HoldoutState.NoContext),
            trials.Count(x => x.TemporalMarginalSupported && x.SpatialMarginalSupported && !x.JointSupported),
            trials.Count(x => x.IndependentSupported),
            trials.Count(x => x.IndependentSupported && operationalIds.Contains(x.OccurrenceId)),
            trials.Sum(x => x.Leakage.IntegrityFailures)
                + trials.Count(x => x.State == Lane0HoldoutState.AmbiguousIntegrity));
    }

    private static void RequireContract(string root, string manifestPath, Lane0ContractArtifact artifact)
    {
        RequireHead(root);
        var canonical = CanonicalContractHash(artifact.Contract);
        if (canonical != artifact.CanonicalSha256) throw new InvalidDataException(
            $"Contract canonical hash drifted: declared={artifact.CanonicalSha256}, recomputed={canonical}.");
        if (artifact.Contract.RepositoryEntryHead != EntryHead
            || artifact.Contract.ImplementationSha256 != TreeIdentity(root, ImplementationFiles)
            || artifact.Contract.HarnessSha256 != TreeIdentity(root, HarnessFiles))
            throw new InvalidDataException("Frozen LANE.0 code identity drifted.");
        var manifest = FrozenC11ManifestResearch.Load(manifestPath);
        if (manifest.CanonicalSha256 != CorpusIdentity || manifest.CanonicalSha256 != artifact.Contract.CorpusManifestSha256)
            throw new InvalidDataException("Frozen C11 identity drifted.");
        foreach (var pair in artifact.Contract.ReusedIdentities)
            if (FileHash(Path.Combine(root, pair.Key)) != pair.Value)
                throw new InvalidDataException($"Reused identity drifted: {pair.Key}");
    }

    private static void RequireHead(string root)
    {
        var value = ReadHead(root);
        if (value != EntryHead) throw new InvalidDataException($"Entry HEAD mismatch: {value}");
    }

    private static string ReadHead(string root)
    {
        var start = new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = root,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot run git.");
        var value = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidDataException($"Cannot read repository HEAD: {value}");
        return value;
    }

    private static string TreeIdentity(string root, IEnumerable<string> files)
    {
        var canonical = string.Join('\n', files.Order(StringComparer.Ordinal).Select(path =>
            $"{path.Replace('\\','/')}|{FileHash(Path.Combine(root, path))}"));
        return Hash(Encoding.UTF8.GetBytes(canonical));
    }
    private static string NormalizedTextTreeIdentity(string root, IEnumerable<string> files)
    {
        var canonical = string.Join('\n', files.Order(StringComparer.Ordinal).Select(path =>
            $"{path.Replace('\\','/')}|{NormalizedTextFileHash(Path.Combine(root, path))}"));
        return Hash(Encoding.UTF8.GetBytes(canonical));
    }
    private static string NormalizedTextFileHash(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8).TrimStart('\uFEFF')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return Hash(new UTF8Encoding(false).GetBytes(text));
    }
    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static string CanonicalContractHash(Lane0Contract contract)
    {
        var semanticJson = JsonSerializer.Serialize(contract, Json);
        var normalized = JsonSerializer.Deserialize<Lane0Contract>(semanticJson, Json)
            ?? throw new InvalidDataException("Cannot normalize LANE.0 contract.");
        return Hash(JsonSerializer.SerializeToUtf8Bytes(normalized, CanonicalJson));
    }
    internal static string CanonicalCorrectiveContractHash(Lane0CorrectiveContract contract)
    {
        var semanticJson = JsonSerializer.Serialize(contract, Json);
        var normalized = JsonSerializer.Deserialize<Lane0CorrectiveContract>(semanticJson, Json)
            ?? throw new InvalidDataException("Cannot normalize LANE.0 corrective contract.");
        return Hash(JsonSerializer.SerializeToUtf8Bytes(normalized, CanonicalJson));
    }
    internal static string CanonicalCorrectiveHardeningContractHash(
        Lane0CorrectiveHardeningContract contract)
    {
        var semanticJson = JsonSerializer.Serialize(contract, Json);
        var normalized = JsonSerializer.Deserialize<Lane0CorrectiveHardeningContract>(semanticJson, Json)
            ?? throw new InvalidDataException("Cannot normalize LANE.0 corrective hardening contract.");
        return Hash(JsonSerializer.SerializeToUtf8Bytes(normalized, CanonicalJson));
    }
    internal static string CanonicalCounterClosureContractHash(Lane0CounterClosureContract contract)
    {
        var semanticJson = JsonSerializer.Serialize(contract, Json);
        var normalized = JsonSerializer.Deserialize<Lane0CounterClosureContract>(semanticJson, Json)
            ?? throw new InvalidDataException("Cannot normalize LANE.0 counter closure contract.");
        return Hash(JsonSerializer.SerializeToUtf8Bytes(normalized, CanonicalJson));
    }
    private static string HeadSignature(int keys, IEnumerable<int> taps, IEnumerable<int> lns) =>
        $"K:{keys}|T:{string.Join(',', taps.Order())}|L:{string.Join(',', lns.Order())}";
    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static void WriteJson(string path, object value) => File.WriteAllText(path,
        JsonSerializer.Serialize(value, Json) + Environment.NewLine, new UTF8Encoding(false));
    private static void WriteChecksums(string directory)
    {
        var files = Directory.EnumerateFiles(directory).Where(x => Path.GetFileName(x) != "sha256sums.txt")
            .OrderBy(x => Path.GetFileName(x), StringComparer.Ordinal).Select(x =>
                $"{FileHash(x)}  {Path.GetFileName(x)}");
        File.WriteAllLines(Path.Combine(directory, "sha256sums.txt"), files, new UTF8Encoding(false));
    }
}
