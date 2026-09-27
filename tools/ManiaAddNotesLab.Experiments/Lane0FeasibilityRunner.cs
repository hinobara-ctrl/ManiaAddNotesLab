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
    int Target, int TargetGroup, int Parent, int ReleaseEndpoint, int FutureHeld, int SameEvent,
    int SyntheticTeaching, int CrossChart, int ArtificialComposition, int DuplicateObservationIdentity,
    int IncompleteOrMispairedIdentity, ImmutableArray<Lane0Occurrence> Eligible)
{
    public int IntegrityFailures => SyntheticTeaching + CrossChart + ArtificialComposition
        + DuplicateObservationIdentity + IncompleteOrMispairedIdentity;
}

internal sealed record Lane0Holdout(
    Lane0Family Family, string ChartId, int KeyCount, string OccurrenceId, Lane0HoldoutState State,
    int ComparableDonors, int MatchingJointDonors, int DistinctAlternatives, bool TemporalMarginalSupported,
    bool SpatialMarginalSupported, bool JointSupported, bool IndependentSupported, Lane0LeakageAudit Leakage);

internal static class Lane0SpatialResearch
{
    public static ImmutableArray<Lane0Holdout> EvaluateAll(ImmutableArray<Lane0Occurrence> occurrences) =>
        occurrences.GroupBy(x => (x.Family, x.ChartId, x.KeyCount, x.QuerySignature))
            .SelectMany(bucket =>
            {
                var values = bucket.OrderBy(x => x.OccurrenceId, StringComparer.Ordinal).ToArray();
                return values.Select(target => Evaluate(target, values));
            }).OrderBy(x => x.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();

    public static Lane0LeakageAudit Audit(Lane0Occurrence target, IEnumerable<Lane0Occurrence> vocabulary)
    {
        var eligible = ImmutableArray.CreateBuilder<Lane0Occurrence>();
        var targetCount = 0; var group = 0; var parent = 0; var release = 0; var future = 0;
        var sameEvent = 0; var synthetic = 0; var crossChart = 0; var composition = 0; var duplicate = 0;
        var incomplete = 0;
        var targetIds = target.ObservationIds.ToHashSet();
        var releaseIds = target.ReleaseEndpointObservationIds.ToHashSet();
        var futureIds = target.FutureHeldObservationIds.ToHashSet();
        foreach (var donor in vocabulary)
        {
            var violations = false;
            if (donor.OccurrenceId == target.OccurrenceId) { targetCount++; violations = true; }
            else if (donor.ObservationIds.Any(targetIds.Contains)) { targetCount++; violations = true; }
            if (donor.GroupId == target.GroupId) { group++; violations = true; }
            if (target.Family == Lane0Family.G1InteriorSpatial && target.ParentId is not null
                && donor.ParentId == target.ParentId) { parent++; violations = true; }
            if (donor.ReleaseEndpointObservationIds.Any(releaseIds.Contains)) { release++; violations = true; }
            // G1.0's frozen holdout semantics are prior-only. Observation identities are an
            // independent provenance guard; time is the authority for the temporal boundary.
            if (target.Family == Lane0Family.G1InteriorSpatial
                && target.AnchorTime is not null && donor.AnchorTime is not null
                && donor.AnchorTime.Value >= target.AnchorTime.Value)
            { future++; violations = true; }
            else if (donor.ObservationIds.Any(futureIds.Contains))
            { future++; violations = true; }
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
                || donor.ObservationIds.IsDefaultOrEmpty)
            { incomplete++; violations = true; }
            if (!violations && donor.Family == target.Family && donor.KeyCount == target.KeyCount
                && donor.QuerySignature == target.QuerySignature) eligible.Add(donor);
        }
        return new(targetCount, group, parent, release, future, sameEvent, synthetic, crossChart,
            composition, duplicate, incomplete, eligible.ToImmutable());
    }

    public static Lane0Holdout Evaluate(Lane0Occurrence target, IEnumerable<Lane0Occurrence> vocabulary)
    {
        var audit = Audit(target, vocabulary);
        if (!target.CompleteIdentity || target.ObservationIds.IsDefaultOrEmpty || audit.IntegrityFailures > 0)
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

    public static string PrepareCorrectiveContract(string root, string contractPath)
    {
        var dependencies = CorrectiveDependencies.ToImmutableSortedDictionary(x => x,
            x => FileHash(Path.Combine(root, x)), StringComparer.Ordinal);
        var contract = new Lane0CorrectiveContract(
            "lane-0-future-held-remediation.1",
            "12ee8799528d9cf9d64d8d9a9ab4b45ba955db0f",
            EntryHead,
            "PENDING_USER_PUBLICATION_AND_APPROVAL",
            TreeIdentity(root, CorrectiveImplementationFiles),
            TreeIdentity(root, CorrectiveHarnessFiles),
            CorpusIdentity,
            "62B2F4F67C34E8F57893A3A025D567000E1BB12B6517C13D97FE9EEC3FE0A3C2",
            "0x400000000",
            "Historical G1 occurrence construction omitted future-held identities and AnchorTime, so the official LANE.0 auditor could not enforce the frozen G1.0 prior-only boundary.",
            "For G1 only, carry exact AnchorTime, derive target future-held provenance from complete original relation identities at the same or later anchor, reject donor.AnchorTime >= target.AnchorTime, and independently reject donor observations intersecting the target future-held set.",
            ["RICE_HEAD_COMPLETION_UNCHANGED_SENTINEL", "G1_INTERIOR_SPATIAL_CORRECTED"],
            ImmutableSortedDictionary<string, int>.Empty
                .Add("g1Operational", 288).Add("g1OperationalHistoricallySupported", 11)
                .Add("g1Structural", 16881).Add("riceOperational", 40360)
                .Add("riceOperationalHistoricallySupported", 37080).Add("riceStructural", 40360),
            ["target", "target-group", "parent", "anchor", "release-endpoint", "future-anchor-time",
             "future-held-original-identity", "same-event", "synthetic-teaching", "cross-chart",
             "artificial-donor-composition", "duplicate-observation", "incomplete-or-mispaired-identity"],
            ["auditor-detects-each-contamination", "official-builder-supplies-required-identity",
             "experimental-flow-applies-each-exclusion", "same-time-boundary-rejected",
             "prior-independent-donor-preserved", "byte-identical-repeat", "zero-rng",
             "no-chart-mutation", "no-production-call-site"],
            ImmutableSortedDictionary<string, string>.Empty
                .Add("BLOCKED", "Required published HEAD, exact frozen identities, explicit C11 corpus, or runtime resources unavailable before evidence is consulted.")
                .Add("FEASIBILITY_DEMONSTRATED", "Both frozen families satisfy the historical support criterion after corrected exclusions, including operational support, with every validity condition passing.")
                .Add("INVALID", "Any contract, identity, corpus, adversarial-control, integrity, determinism, RNG, or non-interference failure.")
                .Add("LIMITED_PARK", "Execution is valid and complete, but corrected evidence is insufficient for FEASIBILITY_DEMONSTRATED."),
            ["Pair every corrected chart/family/keymode result with the immutable historical aggregate.",
             "Report structural, contextual, joint-unique, joint-among-alternatives, contradiction, no-context, marginal-only, independent-support and operational-support deltas.",
             "Keep rice as an unchanged sentinel; do not infer a rice defect from the G1 defect."],
            "List all 11 historically supported operational G1 occurrence IDs individually and report corrected donor count, temporal exclusions, identity exclusions, state and support disposition without changing thresholds.",
            ["Stop INVALID on any validity failure.", "Stop BLOCKED before evidence on unavailable required inputs.",
             "Execute exactly one corrected C11 evaluation only after user publishes and approves its exact HEAD.",
             "Do not adapt exclusions, thresholds, populations or outcome rules after results are visible."],
            ["The historical figures remain historical evidence, not corrected estimates.",
             "A corrected result does not authorize LANE.DESIGN, LANE.GATE or production behavior.",
             "No claim extends beyond frozen C11 or from G1 to rice without direct evidence."],
            dependencies);
        var artifact = new Lane0CorrectiveContractArtifact(contract,
            CanonicalCorrectiveContractHash(contract),
            "UTF-8 System.Text.Json semantic round-trip, camelCase, unindented; ordinal ordered arrays/maps; no timestamps, private corpus paths or result-derived fields");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        WriteJson(contractPath, artifact);
        return artifact.CanonicalSha256;
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
            var g1 = BuildG1Occurrences(descriptor.Sha256, g1Research);
            var riceTrials = Lane0SpatialResearch.EvaluateAll(rice);
            var g1Trials = Lane0SpatialResearch.EvaluateAll(g1);
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
        var integrityFailures = charts.Sum(x => x.Rice.IntegrityFailures + x.G1.IntegrityFailures
            + x.G1LegacyLeakageFailures);
        var outcome = Lane0OutcomeClassifier.Classify(new(criteria.All(x => x.Passed), false,
            true, true, integrityFailures, true, true, rng));
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
                : result.CompleteRelations.Where(x => x.AnchorTime >= occurrence.AnchorTime)
                    .SelectMany(x =>
                    {
                        var futureAnchorId = $"{x.ParentLongNoteId}-A{x.AnchorTime}-B{D(x.AnchorBeat)}";
                        var futureAnchor = anchors[futureAnchorId];
                        return new[] { x.ParentLongNoteId.Value, x.WitnessLongNoteId.Value }
                            .Concat(futureAnchor.HeadWitnessIds.Select(id => id.Value))
                            .Concat(futureAnchor.ReleaseWitnessIds.Select(id => id.Value));
                    })
                    .Distinct().Order().ToImmutableArray();
            return new Lane0Occurrence(Lane0Family.G1InteriorSpatial, chart, result.KeyCount,
                occurrence.OccurrenceId, anchorId, anchorId, occurrence.ParentLongNoteId.ToString(),
                $"{occurrence.QuerySignature}|PL:{occurrence.ParentLane}", $"{temporal}|{spatial}", temporal,
                spatial, ids, anchor.ReleaseWitnessIds.Select(x => x.Value).ToImmutableArray(), futureHeld,
                AnchorTime: reproduceHistoricalDefect ? null : occurrence.AnchorTime);
        }).OrderBy(x => x.OccurrenceId, StringComparer.Ordinal).ToImmutableArray();
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
        var start = new ProcessStartInfo("git", "rev-parse HEAD") { WorkingDirectory = root,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot run git.");
        var value = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit();
        if (process.ExitCode != 0 || value != EntryHead) throw new InvalidDataException($"Entry HEAD mismatch: {value}");
    }

    private static string TreeIdentity(string root, IEnumerable<string> files)
    {
        var canonical = string.Join('\n', files.Order(StringComparer.Ordinal).Select(path =>
            $"{path.Replace('\\','/')}|{FileHash(Path.Combine(root, path))}"));
        return Hash(Encoding.UTF8.GetBytes(canonical));
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
