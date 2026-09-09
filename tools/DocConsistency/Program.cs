using System.Security.Cryptography;
using System.Text.Json;
using DocConsistencyTool;

if (args.Length != 1 || args[0] != "--check")
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/DocConsistency -- --check");
    return 2;
}

var root = FindRoot(AppContext.BaseDirectory);
var statePath = Path.Combine(root, "docs", "PROJECT_STATE.json");
var errors = new List<string>();
ValidateRepositoryRootLayout();
ProjectState? state = null;
try
{
    state = JsonSerializer.Deserialize<ProjectState>(File.ReadAllText(statePath), new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });
}
catch (Exception exception)
{
    errors.Add($"PROJECT_STATE.json cannot be read: {exception.Message}");
}

if (state is null)
{
    errors.Add("PROJECT_STATE.json produced no state.");
}
else
{
    ValidateCanonicalState(state);
    ValidatePhaseContracts(state);
    ValidateFilesAndIndex(state);
    ValidateD1GateContract(state);
    ValidateD1Closure(state);
    ValidateD1SafetyClosure(state);
    ValidateSafetyProvClosure(state);
    ValidateVersionContracts(state);
    ValidateMasterStateBlocks(state);
    ValidatePhaseSummaries(state);
    ValidateObsoletePhrases(state);
}

if (errors.Count > 0)
{
    Console.Error.WriteLine("DOCUMENTATION CONSISTENCY: FAIL");
    foreach (var error in errors) Console.Error.WriteLine($"- {error}");
    return 1;
}

Console.WriteLine("DOCUMENTATION CONSISTENCY: PASS");
return 0;

void ValidateRepositoryRootLayout()
{
    var allowlistPath = Path.Combine(root, "tools", "DocConsistency", "repository-root-allowlist.txt");
    try
    {
        var declared = RepositoryRootGuard.ReadAllowlist(allowlistPath);
        var visible = RepositoryRootGuard.GetVisibleTopLevelEntries(root);
        var result = RepositoryRootGuard.Validate(declared, visible);
        foreach (var entry in result.UnexpectedEntries)
            errors.Add($"Unexpected tracked or untracked repository root entry: {entry}");
        foreach (var entry in result.MissingDeclaredEntries)
            errors.Add($"Declared repository root entry is not tracked: {entry}");
    }
    catch (Exception exception)
    {
        errors.Add($"Repository root layout cannot be validated: {exception.Message}");
    }
}

void ValidateCanonicalState(ProjectState value)
{
    if (value.SchemaVersion < 1) errors.Add("schemaVersion must be at least 1.");
    RequireValue(value.LastUpdated, "lastUpdated");
    RequireValue(value.BehaviorPolicyVersion, "behaviorPolicyVersion");
    RequireValue(value.EvidenceProfileVersion, "evidenceProfileVersion");
    RequireValue(value.DiagnosticVersion, "diagnosticVersion");
    var currentPhase = value.Phases.FirstOrDefault(x => x.Id == value.CurrentPhase);
    if (value.BehaviorChange != (currentPhase?.BehaviorChange ?? false))
        errors.Add("behaviorChange must match the current phase contract.");
    RequireValue(value.CurrentPhase, "currentPhase");
    if (value.CurrentPhase == "SAFETY.PROV" && value.NextRecommendedAction != "ROADMAP_REVIEW")
        errors.Add("Closed SAFETY.PROV requires nextRecommendedAction=ROADMAP_REVIEW.");

    var validStatuses = new HashSet<string>(StringComparer.Ordinal)
        { "COMPLETE", "HOLD", "REJECTED", "NEXT", "FUTURE", "DEFERRED", "PENDING", "BLOCKED" };
    var duplicateIds = value.Phases.GroupBy(x => x.Id, StringComparer.Ordinal)
        .Where(x => x.Count() > 1).Select(x => x.Key);
    foreach (var id in duplicateIds) errors.Add($"Duplicate phase id: {id}.");
    foreach (var phase in value.Phases)
    {
        RequireValue(phase.Id, "phase id");
        RequireValue(phase.Name, $"name for phase {phase.Id}");
        if (!validStatuses.Contains(phase.Status)) errors.Add($"Phase {phase.Id} has invalid status {phase.Status}.");
        if (phase.Report is not null) RequireFile(phase.Report, $"report for phase {phase.Id}");
    }

    var current = currentPhase;
    var next = value.Phases.FirstOrDefault(x => x.Id == value.NextRecommendedPhase);
    var nextBehavioral = value.Phases.FirstOrDefault(x => x.Id == value.NextBehavioralPhase);
    if (current is null) errors.Add("currentPhase does not exist in phases.");
    if (value.NextRecommendedPhase is not null && next is null)
        errors.Add("nextRecommendedPhase does not exist in phases.");
    if (value.NextBehavioralPhase is not null && nextBehavioral is null)
        errors.Add("nextBehavioralPhase does not exist in phases.");
    if (value.NextRecommendedPhase is not null && value.CurrentPhase == value.NextRecommendedPhase)
        errors.Add("currentPhase and nextRecommendedPhase must be different.");

    var closedStatuses = new HashSet<string>(StringComparer.Ordinal) { "COMPLETE", "HOLD", "REJECTED" };
    var latestClosed = value.Phases.LastOrDefault(x => closedStatuses.Contains(x.Status));
    if (current is not null && !closedStatuses.Contains(current.Status))
        errors.Add($"currentPhase {current.Id} is not closed: {current.Status}.");
    if (latestClosed is not null && current?.Id != latestClosed.Id)
        errors.Add($"currentPhase must be the latest closed phase ({latestClosed.Id}), not {value.CurrentPhase}.");
    if (next is not null && next.Status != "NEXT")
        errors.Add($"nextRecommendedPhase {next.Id} must have status NEXT, not {next.Status}.");
    var expectedNextCount = value.NextRecommendedPhase is null ? 0 : 1;
    if (value.Phases.Count(x => x.Status == "NEXT") != expectedNextCount)
        errors.Add($"Exactly {expectedNextCount} phase(s) must have status NEXT.");

    var f2Branches = (value.ResearchBranches ?? []).Where(x => x.Id == "F2").ToArray();
    var branch = f2Branches.SingleOrDefault();
    if (f2Branches.Length != 1) errors.Add("researchBranches must describe F2 exactly once.");
    else if (branch is not null)
    {
        if (branch.Decision != "CONTINUE_CONDITIONALLY")
            errors.Add("F2 branch decision must be CONTINUE_CONDITIONALLY.");
        var blocker = value.Phases.FirstOrDefault(x => x.Id == branch.BlockedBy);
        if (blocker?.Status != "BLOCKED")
            errors.Add($"F2 blocker {branch.BlockedBy} must exist with status BLOCKED.");
        if (value.NextRecommendedPhase == branch.BlockedBy)
            errors.Add("A blocked prerequisite cannot also be the next actionable research phase.");
    }
    if (next is not null && next.Authorization != "NOT_AUTHORIZED")
        errors.Add($"Next actionable phase {next?.Id} must be explicitly NOT_AUTHORIZED.");
    if (nextBehavioral is not null && nextBehavioral.Authorization != "NOT_AUTHORIZED")
        errors.Add($"Next behavioral phase {nextBehavioral?.Id} must be explicitly NOT_AUTHORIZED.");
    if (value.NextRecommendedPhase is not null && value.NextRecommendedPhase == value.NextBehavioralPhase)
        errors.Add("nextRecommendedPhase and nextBehavioralPhase must remain separate.");

    foreach (var required in new[] { "C1", "C1.1", "C1.2", "C2", "D0", "D0.1", "D0.2", "D1.0", "D1.GATE", "D1", "D1.SAFETY", "SAFETY.PROV", "E", "E.1", "F1", "F2", "F2.1", "F2.2", "F2.3", "F2.ACQ" })
        if (value.Phases.All(x => x.Id != required)) errors.Add($"Required phase is absent from state: {required}.");

    if (value.TestStatus.Passed < 0 || value.TestStatus.Failed < 0 || value.TestStatus.Skipped < 0)
        errors.Add("testStatus counts cannot be negative.");
    if (value.ValidationCorpus.Families < 1 || value.ValidationCorpus.HumanCharts < 1
        || value.ValidationCorpus.Keymodes.Count == 0)
        errors.Add("validationCorpus must describe at least one family, chart, and keymode.");
}

void ValidatePhaseContracts(ProjectState value)
{
    var duplicateContracts = value.PhaseContracts.GroupBy(x => x.Id, StringComparer.Ordinal)
        .Where(x => x.Count() > 1).Select(x => x.Key);
    foreach (var id in duplicateContracts) errors.Add($"Duplicate phase contract: {id}.");

    foreach (var contract in value.PhaseContracts)
    {
        var phase = value.Phases.FirstOrDefault(x => x.Id == contract.Id);
        if (phase is null)
        {
            errors.Add($"Phase contract {contract.Id} has no matching phase.");
            continue;
        }
        if (phase.BehaviorChange != contract.BehaviorChange)
            errors.Add($"Phase {contract.Id} behaviorChange disagrees with its canonical contract.");
        if (contract.Authorization is not null && phase.Authorization != contract.Authorization)
            errors.Add($"Phase {contract.Id} authorization disagrees with its canonical contract.");

        errors.AddRange(DocumentationStateGuard.ValidatePhaseContracts([
            new PhaseContractProjection(phase.Id, contract.Kind, phase.BehaviorChange,
                phase.Status, phase.Authorization)
        ]));

        var behaviorMarker = contract.BehaviorChange is null
            ? "null"
            : contract.BehaviorChange.Value.ToString().ToLowerInvariant();
        var marker = $"<!-- PHASE-CONTRACT:{contract.Id};kind={contract.Kind};" +
            $"behaviorChange={behaviorMarker};" +
            $"authorization={contract.Authorization ?? "N/A"} -->";
        CheckContains("docs/MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md", marker,
            $"canonical phase contract {contract.Id}");
    }

    foreach (var required in new[] { "D1.0", "D1.GATE", "D1", "D1.SAFETY", "SAFETY.PROV", "F2.ACQ", "C2" })
        if (value.PhaseContracts.All(x => x.Id != required))
            errors.Add($"Required canonical phase contract is absent: {required}.");

    var research = value.NextRecommendedPhase is null
        ? null
        : value.PhaseContracts.FirstOrDefault(x => x.Id == value.NextRecommendedPhase);
    if (value.NextRecommendedPhase is not null && research?.Kind != "ResearchShadow")
        errors.Add("nextRecommendedPhase must reference a ResearchShadow contract.");
    var behavioral = value.NextBehavioralPhase is null
        ? null
        : value.PhaseContracts.FirstOrDefault(x => x.Id == value.NextBehavioralPhase);
    if (value.NextBehavioralPhase is not null && behavioral?.Kind != "BehaviorChanging")
        errors.Add("nextBehavioralPhase must reference a BehaviorChanging contract.");
}

void ValidateD1GateContract(ProjectState value)
{
    var gate = value.Phases.FirstOrDefault(x => x.Id == "D1.GATE");
    if (gate?.Status != "COMPLETE") return;

    var required = new[]
    {
        "docs/D1_BEHAVIORAL_EXPERIMENT_CONTRACT.md",
        "docs/PHASE_D1_GATE_BEHAVIORAL_EXPERIMENT_GATE_REPORT.md",
        "docs/d1_gate_behavioral_experiment_contract.json",
        "docs/d1_gate_contract_validation_summary.csv",
        "docs/d1_gate_engine_insertion_audit.csv",
        "docs/d1_gate_scope_audit.csv",
        "docs/d1_gate_d1_0_reproduction.csv"
    };
    foreach (var artifact in required)
    {
        RequireFile(artifact, "D1.GATE closure artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "D1.GATE closure artifact index entry");
    }

    const string frozenHash = "D574631B3E713AC3D08C159B605A742D50C907B1BB4589D7FCADCFA8027A7824";
    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "d1_gate_behavioral_experiment_contract.json")));
        var contract = document.RootElement.GetProperty("contract");
        Equal(contract, "gatePhase", "D1.GATE");
        Equal(contract, "gateDecision", "READY");
        Equal(contract, "baselineCommit", "77638edad9b41c8d6e3a361ce68c296ed12c90bc");
        Equal(contract, "controlPolicyVersion", value.BehaviorPolicyVersion);
        Equal(contract, "proposedTreatmentPolicyVersion", "d1-resulting-state-ab.1");
        Equal(contract, "evidenceView", "ReducedOnly");
        Equal(contract, "futureD1Authorization", "NOT_AUTHORIZED");
        if (contract.GetProperty("behaviorChangeCurrent").GetBoolean())
            errors.Add("D1.GATE contract must keep currentBehaviorChange=false.");
        if (!contract.GetProperty("futureBehaviorChange").GetBoolean())
            errors.Add("D1.GATE contract must declare futureD1BehaviorChange=true.");
        if (contract.GetProperty("rngContract").GetProperty("gateRngCalls").GetInt32() != 0)
            errors.Add("D1.GATE contract must consume zero RNG calls.");
        if (contract.GetProperty("scopeCardinality").GetInt32() != 2)
            errors.Add("D1.GATE contract must govern only the k=1 to k=2 transition.");
        if (contract.GetProperty("proposedTreatmentPolicyVersion").GetString() == value.BehaviorPolicyVersion)
            errors.Add("D1.GATE proposed treatment must differ from the active behavior policy.");
        if (contract.GetProperty("rollbackPolicy").GetProperty("disabledBehavior").GetString()
            != value.BehaviorPolicyVersion)
            errors.Add("D1.GATE rollback must point exactly to the active legacy behavior policy.");
        if (!contract.GetProperty("noRerollRule").GetString()!.Contains("no lane, shape, candidate, chance, or RNG retry", StringComparison.Ordinal))
            errors.Add("D1.GATE contract must explicitly forbid retrying the same opportunity.");
        if (document.RootElement.GetProperty("contractContentHash").GetString() != frozenHash)
            errors.Add("D1.GATE contract hash differs from the frozen preregistration.");
        CheckContains("docs/PHASE_D1_GATE_BEHAVIORAL_EXPERIMENT_GATE_REPORT.md", frozenHash,
            "D1.GATE frozen contract hash");
    }
    catch (Exception exception)
    {
        errors.Add($"D1.GATE machine contract cannot be validated: {exception.Message}");
    }

    if (gate.Outcome != "READY" || gate.BehaviorChange is not false)
        errors.Add("D1.GATE must close COMPLETE/READY with behaviorChange=false.");
    var behavioral = value.Phases.FirstOrDefault(x => x.Id == "D1");
    if (behavioral is null || behavioral.BehaviorChange is not true)
        errors.Add("D1 must remain behavior-changing after D1.GATE.");
    if (behavioral?.Status == "COMPLETE")
    {
        if (behavioral.Authorization != "EXPERIMENT_COMPLETED_NO_PROMOTION")
            errors.Add("Closed D1 must remain experimental and explicitly unpromoted.");
    }
    else if (behavioral?.Authorization != "NOT_AUTHORIZED")
        errors.Add("Unclosed D1 must remain NOT_AUTHORIZED after D1.GATE.");

    var d1Closed = value.Phases.FirstOrDefault(x => x.Id == "D1")?.Status == "COMPLETE";
    foreach (var productionFile in new[]
    {
        "src/ManiaAddNotesLab.Core/AddNotesEngine.cs",
        "src/ManiaAddNotesLab.Core/Model.cs",
        "src/ManiaAddNotesLab.Cli/Program.cs",
        "src/ManiaAddNotesLab.Web/Program.cs"
    })
        if (!d1Closed && File.ReadAllText(Path.Combine(root, productionFile.Replace('/', Path.DirectorySeparatorChar)))
            .Contains("D1BehavioralExperimentContractResearch", StringComparison.Ordinal))
            errors.Add($"D1.GATE research contract is connected to production: {productionFile}");

    void Equal(JsonElement contract, string property, string expected)
    {
        if (contract.GetProperty(property).GetString() != expected)
            errors.Add($"D1.GATE contract {property} must be {expected}.");
    }
}

void ValidateD1Closure(ProjectState value)
{
    var d1 = value.Phases.FirstOrDefault(x => x.Id == "D1");
    if (d1?.Status != "COMPLETE") return;
    if (d1.Outcome != "C" || d1.BehaviorChange is not true)
        errors.Add("Closed D1 must reflect COMPLETE/OUTCOME C with behaviorChange=true.");
    foreach (var artifact in new[]
    {
        "docs/PHASE_D1_PRE_RUN_DESIGN.md",
        "docs/PHASE_D1_RESULTING_STATE_AB_REPORT.md",
        "docs/d1_behavioral_ab_run_manifest.json",
        "docs/d1_behavioral_ab_abort_summary.csv",
        "docs/d1_synthetic_gate_summary.csv",
        "docs/d1_safety_summary.csv",
        "docs/d1_determinism_summary.csv"
    })
    {
        RequireFile(artifact, "D1 closure artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "D1 closure artifact index entry");
    }
    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "public const string BehaviorPolicyVersion = \"legacy-experimental.1\";", "legacy default after D1");
    CheckContains("src/ManiaAddNotesLab.Core/D1BehavioralExperiment.cs",
        "public const string Treatment = \"d1-resulting-state-ab.1\";", "experimental D1 treatment version");
    foreach (var product in new[] { "src/ManiaAddNotesLab.Cli/Program.cs", "src/ManiaAddNotesLab.Web/Program.cs" })
        if (File.ReadAllText(Path.Combine(root, product.Replace('/', Path.DirectorySeparatorChar)))
            .Contains("d1-resulting-state-ab.1", StringComparison.Ordinal))
            errors.Add($"D1 treatment is exposed by normal product path: {product}");

    const string frozenManifestHash = "4C87F8B98B5B22B81CD903F26E3EF861B608893B50B46DBC20AF13158E8A4F00";
    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "d1_behavioral_ab_run_manifest.json")));
        var manifest = document.RootElement.GetProperty("manifest");
        var actualHash = Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(manifest)));
        if (actualHash != frozenManifestHash
            || document.RootElement.GetProperty("contentHash").GetString() != frozenManifestHash)
            errors.Add("D1 run manifest differs from the frozen pre-run identity.");
        if (manifest.GetProperty("expectedPairedRunCount").GetInt32() != 220
            || manifest.GetProperty("charts").GetArrayLength() != 11
            || manifest.GetProperty("seeds").GetArrayLength() != 20)
            errors.Add("D1 run manifest matrix differs from the frozen 11 chart x 20 seed design.");
    }
    catch (Exception exception)
    {
        errors.Add($"D1 run manifest cannot be validated: {exception.Message}");
    }
}

void ValidateD1SafetyClosure(ProjectState value)
{
    var safety = value.Phases.FirstOrDefault(x => x.Id == "D1.SAFETY");
    if (safety?.Status != "COMPLETE") return;
    if (safety.Outcome != "C" || safety.BehaviorChange is not false
        || safety.Authorization != "RESEARCH_COMPLETED_PARKED")
        errors.Add("D1.SAFETY must close COMPLETE/OUTCOME C, behaviorChange=false and PARKED.");
    var d1 = value.Phases.FirstOrDefault(x => x.Id == "D1");
    if (d1?.Status != "COMPLETE" || d1.Outcome != "C"
        || d1.Authorization != "EXPERIMENT_COMPLETED_NO_PROMOTION")
        errors.Add("D1.SAFETY must preserve D1 COMPLETE/C with no promotion.");
    foreach (var artifact in new[]
    {
        "docs/PHASE_D1_SAFETY_PRE_FORENSIC_DESIGN.md",
        "docs/PHASE_D1_SAFETY_ATTRIBUTABLE_GEOMETRY_REPORT.md",
        "docs/d1_safety_attribution_contract.json",
        "docs/d1_safety_synthetic_summary.csv",
        "docs/d1_safety_semantic_matrix.csv",
        "docs/d1_safety_determinism_summary.csv",
        "docs/d1_safety_baseline_identity_summary.csv",
        "docs/d1_safety_abort_summary.csv"
    })
    {
        RequireFile(artifact, "D1.SAFETY closure artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "D1.SAFETY closure artifact index entry");
    }
    const string frozenHash = "C4C1273AB4029D8AAF68EFA25AB64159B56D524AF6B8B26D74054A57A2096133";
    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "d1_safety_attribution_contract.json")));
        var contract = document.RootElement.GetProperty("contract");
        var actual = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contract)));
        if (actual != frozenHash || document.RootElement.GetProperty("contractContentHash").GetString() != frozenHash)
            errors.Add("D1.SAFETY contract hash differs from its frozen semantic identity.");
        if (contract.GetProperty("behaviorChange").GetBoolean()
            || contract.GetProperty("d1HistoricalOutcome").GetString() != "C"
            || !contract.GetProperty("d1RemainsParked").GetBoolean())
            errors.Add("D1.SAFETY contract must preserve shadow behavior and historical D1 Outcome C/PARKED.");
    }
    catch (Exception exception)
    {
        errors.Add($"D1.SAFETY machine contract cannot be validated: {exception.Message}");
    }
    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "public const string BehaviorPolicyVersion = \"legacy-experimental.1\";", "legacy default after D1.SAFETY");
    foreach (var product in new[] { "src/ManiaAddNotesLab.Cli/Program.cs", "src/ManiaAddNotesLab.Web/Program.cs" })
        if (File.ReadAllText(Path.Combine(root, product.Replace('/', Path.DirectorySeparatorChar)))
            .Contains("GeometrySafetyAttributionResearch", StringComparison.Ordinal))
            errors.Add($"D1.SAFETY shadow oracle is exposed by normal product path: {product}");
    if (File.Exists(Path.Combine(root, "docs", "d1_safety_forensic_summary.csv")))
        errors.Add("Invalid D1.SAFETY forensic attribution aggregate must remain withdrawn after hard abort.");
}

void ValidateSafetyProvClosure(ProjectState value)
{
    var provenance = value.Phases.FirstOrDefault(x => x.Id == "SAFETY.PROV");
    if (provenance?.Status != "COMPLETE") return;
    if (provenance.Outcome != "A" || provenance.BehaviorChange is not false
        || provenance.Authorization != "RESEARCH_COMPLETED_ROADMAP_REVIEW_REQUIRED")
        errors.Add("SAFETY.PROV must close COMPLETE/OUTCOME A, behaviorChange=false and require roadmap review.");
    if (value.NextRecommendedAction != "ROADMAP_REVIEW" || value.NextRecommendedPhase is not null
        || value.NextBehavioralPhase is not null)
        errors.Add("SAFETY.PROV closure must recommend ROADMAP_REVIEW and authorize no next phase.");
    var d1 = value.Phases.FirstOrDefault(x => x.Id == "D1");
    var safety = value.Phases.FirstOrDefault(x => x.Id == "D1.SAFETY");
    if (d1?.Status != "COMPLETE" || d1.Outcome != "C"
        || d1.Authorization != "EXPERIMENT_COMPLETED_NO_PROMOTION"
        || safety?.Status != "COMPLETE" || safety.Outcome != "C"
        || safety.Authorization != "RESEARCH_COMPLETED_PARKED")
        errors.Add("SAFETY.PROV must preserve historical D1 and D1.SAFETY COMPLETE/C/PARKED states.");
    foreach (var artifact in new[]
    {
        "docs/PHASE_SAFETY_PROV_DESIGN.md",
        "docs/PHASE_SAFETY_PROV_MUTATION_LEVEL_PROVENANCE_REPORT.md",
        "docs/safety_prov_contract.json",
        "docs/safety_prov_synthetic_summary.csv",
        "docs/safety_prov_behavior_equivalence_summary.csv",
        "docs/safety_prov_causal_lineage_summary.csv",
        "docs/safety_prov_serialization_summary.csv",
        "docs/safety_prov_determinism_summary.csv",
        "docs/safety_prov_baseline_identity_summary.csv"
    })
    {
        RequireFile(artifact, "SAFETY.PROV closure artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "SAFETY.PROV artifact index entry");
    }
    const string frozenHash = "3562A0A7746F0E6BFFD80E93B51E5D9ADAA6E646C607E9B8994E36F1AE264B8B";
    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "safety_prov_contract.json")));
        var contract = document.RootElement.GetProperty("contract");
        var actual = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contract)));
        if (actual != frozenHash || document.RootElement.GetProperty("safetyProvContractHash").GetString() != frozenHash)
            errors.Add("SAFETY.PROV contract hash differs from its frozen semantic identity.");
        if (contract.GetProperty("behaviorChange").GetBoolean()
            || contract.GetProperty("d1HistoricalOutcome").GetString() != "C"
            || contract.GetProperty("d1SafetyHistoricalOutcome").GetString() != "C"
            || contract.GetProperty("defaultPolicy").GetString() != "legacy-experimental.1"
            || contract.GetProperty("nextRecommendedAction").GetString() != "ROADMAP_REVIEW")
            errors.Add("SAFETY.PROV contract does not preserve historical/default/roadmap boundaries.");
    }
    catch (Exception exception)
    {
        errors.Add($"SAFETY.PROV machine contract cannot be validated: {exception.Message}");
    }
    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "public const string BehaviorPolicyVersion = \"legacy-experimental.1\";", "legacy default after SAFETY.PROV");
    foreach (var product in new[] { "src/ManiaAddNotesLab.Cli/Program.cs", "src/ManiaAddNotesLab.Web/Program.cs" })
    {
        var text = File.ReadAllText(Path.Combine(root, product.Replace('/', Path.DirectorySeparatorChar)));
        if (text.Contains("GenerationProvenanceRecorderResearch", StringComparison.Ordinal)
            || text.Contains("SafetyProvContractResearch", StringComparison.Ordinal))
            errors.Add($"SAFETY.PROV research instrumentation is exposed by normal product path: {product}");
    }
    CheckContains("README.md", "ROADMAP REVIEW REQUIRED", "post-SAFETY.PROV roadmap boundary");
    CheckContains("PROJECT_STATUS.md", "ROADMAP REVIEW REQUIRED", "post-SAFETY.PROV roadmap boundary");
    CheckContains("ROADMAP.md", "ROADMAP REVIEW REQUIRED", "post-SAFETY.PROV roadmap boundary");
    CheckContains("docs/BEHAVIOR_DECISION_AUDIT.md", "temporally after divergence",
        "temporal versus causal downstream rule");
}

void ValidateFilesAndIndex(ProjectState value)
{
    var requiredMasterDocuments = new[]
    {
        "README.md", "PROJECT_STATUS.md", "ROADMAP.md", "ARCHITECTURE.md", "DOCUMENTATION_INDEX.md",
        "docs/MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md", "docs/BEHAVIOR_DECISION_AUDIT.md",
        "docs/PHASE_CLOSURE_PROTOCOL.md"
    };
    foreach (var document in value.MasterDocuments)
        RequireFile(document, "master document");
    foreach (var required in requiredMasterDocuments)
    {
        if (!value.MasterDocuments.Contains(required, StringComparer.Ordinal))
            errors.Add($"masterDocuments does not include required document: {required}");
        RequireFile(required, "required master document");
    }

    CheckContains("DOCUMENTATION_INDEX.md", "docs/PROJECT_STATE.json", "PROJECT_STATE index entry");
    CheckContains("DOCUMENTATION_INDEX.md", "docs/PHASE_CLOSURE_PROTOCOL.md", "phase closure protocol index entry");
    foreach (var phase in value.Phases.Where(x => x.Report is not null))
        CheckContains("DOCUMENTATION_INDEX.md", phase.Report!, $"report {phase.Id} index entry");

    var c12 = value.Phases.FirstOrDefault(x => x.Id == "C1.2");
    if (c12?.Status == "COMPLETE")
    {
        foreach (var artifact in new[]
        {
            "docs/PHASE_C1_2_EXACT_HEAD_RELATION_MODELING_REPORT.md",
            "docs/c1_2_chart_summary.csv",
            "docs/c1_2_family_summary.csv",
            "docs/c1_2_global_summary.csv"
        })
        {
            RequireFile(artifact, "C1.2 closure artifact");
            CheckContains("DOCUMENTATION_INDEX.md", artifact, "C1.2 closure artifact index entry");
        }
    }

    var d0 = value.Phases.FirstOrDefault(x => x.Id == "D0");
    if (d0?.Status == "COMPLETE")
    {
        foreach (var artifact in new[]
        {
            "docs/PHASE_D0_CHORD_COMPLETION_RECONSTRUCTION_REPORT.md",
            "docs/d0_chart_summary.csv",
            "docs/d0_family_summary.csv",
            "docs/d0_global_summary.csv"
        })
        {
            RequireFile(artifact, "D0 closure artifact");
            CheckContains("DOCUMENTATION_INDEX.md", artifact, "D0 closure artifact index entry");
        }
    }

    var d01 = value.Phases.FirstOrDefault(x => x.Id == "D0.1");
    if (d01?.Status == "COMPLETE")
    {
        foreach (var artifact in new[]
        {
            "docs/PHASE_D0_1_EXACT_COMPLETION_COMPETITION_CONTEXT_REPORT.md",
            "docs/d0_1_chart_summary.csv",
            "docs/d0_1_family_summary.csv",
            "docs/d0_1_global_summary.csv",
            "docs/d0_1_context_view_summary.csv"
        })
        {
            RequireFile(artifact, "D0.1 closure artifact");
            CheckContains("DOCUMENTATION_INDEX.md", artifact, "D0.1 closure artifact index entry");
        }
    }

    var d02 = value.Phases.FirstOrDefault(x => x.Id == "D0.2");
    if (d02?.Status == "COMPLETE")
    {
        foreach (var artifact in new[]
        {
            "docs/PHASE_D0_2_EXACT_CONTEXT_COVERAGE_VIEW_AGREEMENT_REPORT.md",
            "docs/d0_2_chart_summary.csv",
            "docs/d0_2_family_summary.csv",
            "docs/d0_2_global_summary.csv",
            "docs/d0_2_view_pair_summary.csv",
            "docs/d0_2_joint_context_summary.csv",
            "docs/d0_2_coverage_signature_summary.csv",
            "docs/d0_2_conflict_summary.csv",
            "docs/d0_2_stratification_summary.csv"
        })
        {
            RequireFile(artifact, "D0.2 closure artifact");
            CheckContains("DOCUMENTATION_INDEX.md", artifact, "D0.2 closure artifact index entry");
        }
    }

    var d10 = value.Phases.FirstOrDefault(x => x.Id == "D1.0");
    if (d10?.Status == "COMPLETE")
    {
        foreach (var artifact in new[]
        {
            "docs/PHASE_D1_0_PRE_HUMAN_DESIGN.md",
            "docs/PHASE_D1_0_RESULTING_STATE_COMPOSITION_FEASIBILITY_REPORT.md",
            "docs/d1_0_phase_d0_reproduction.csv",
            "docs/d1_0_synthetic_gate_summary.csv",
            "docs/d1_0_target_reconstruction_summary.csv",
            "docs/d1_0_marginal_joint_summary.csv",
            "docs/d1_0_composition_trap_summary.csv",
            "docs/d1_0_context_view_summary.csv",
            "docs/d1_0_chart_summary.csv",
            "docs/d1_0_family_summary.csv",
            "docs/d1_0_stratification_summary.csv",
            "docs/d1_0_pair_type_summary.csv",
            "docs/d1_0_reduced_state_summary.csv",
            "docs/d1_0_leakage_summary.csv",
            "docs/d1_0_order_invariance_summary.csv"
        })
        {
            RequireFile(artifact, "D1.0 closure artifact");
            CheckContains("DOCUMENTATION_INDEX.md", artifact, "D1.0 closure artifact index entry");
        }
    }

    var f1 = value.Phases.FirstOrDefault(x => x.Id == "F1");
    if (f1?.Status == "COMPLETE")
    {
        foreach (var artifact in new[]
        {
            "docs/PHASE_F1_COMPARABLE_CONTEXT_RESOLVER_REPORT.md",
            "docs/f1_chart_summary.csv",
            "docs/f1_family_summary.csv",
            "docs/f1_global_summary.csv",
            "docs/f1_evidence_state_summary.csv",
            "docs/f1_coverage_summary.csv",
            "docs/f1_conflict_summary.csv",
            "docs/f1_joint_context_summary.csv",
            "docs/f1_stratification_summary.csv",
            "docs/f1_view_evidence_summary.csv",
            "docs/f1_unique_support_summary.csv",
            "docs/f1_dependency_summary.csv"
        })
        {
            RequireFile(artifact, "F1 closure artifact");
            CheckContains("DOCUMENTATION_INDEX.md", artifact, "F1 closure artifact index entry");
        }
    }

    var f2 = value.Phases.FirstOrDefault(x => x.Id == "F2");
    if (f2?.Status == "COMPLETE")
    {
        foreach (var artifact in new[]
        {
            "docs/PHASE_F2_TYPED_GAPS_EVIDENCE_BACKOFF_REPORT.md",
            "docs/f2_chart_summary.csv",
            "docs/f2_family_summary.csv",
            "docs/f2_global_summary.csv",
            "docs/f2_transition_summary.csv",
            "docs/f2_gap_summary.csv",
            "docs/f2_evidence_state_summary.csv",
            "docs/f2_backoff_summary.csv",
            "docs/f2_skip_summary.csv",
            "docs/f2_stratification_summary.csv"
        })
        {
            RequireFile(artifact, "F2 closure artifact");
            CheckContains("DOCUMENTATION_INDEX.md", artifact, "F2 closure artifact index entry");
        }
    }

    var f21 = value.Phases.FirstOrDefault(x => x.Id == "F2.1");
    if (f21?.Status == "COMPLETE")
    {
        foreach (var artifact in new[]
        {
            "docs/PHASE_F2_1_EXACT_GAP_TIMING_IDENTITY_REPORT.md",
            "docs/f2_1_identity_model_summary.csv",
            "docs/f2_1_synthetic_validation_summary.csv",
            "docs/f2_1_negative_control_summary.csv",
            "docs/f2_1_timing_segment_summary.csv",
            "docs/f2_1_endpoint_holdout_summary.csv"
        })
        {
            RequireFile(artifact, "F2.1 closure artifact");
            CheckContains("DOCUMENTATION_INDEX.md", artifact, "F2.1 closure artifact index entry");
        }
    }
}

void ValidateVersionContracts(ProjectState value)
{
    foreach (var document in new[] { "README.md", "PROJECT_STATUS.md", "ARCHITECTURE.md" })
    {
        CheckContains(document, value.BehaviorPolicyVersion, "behavior policy version");
        CheckContains(document, value.EvidenceProfileVersion, "evidence profile version");
        CheckContains(document, value.DiagnosticVersion, "diagnostic version");
    }
    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        $"public const string BehaviorPolicyVersion = \"{value.BehaviorPolicyVersion}\";",
        "compiled behavior policy version");
    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        $"public const string EvidenceProfileVersion = \"{value.EvidenceProfileVersion}\";",
        "compiled evidence profile version");
    CheckContains("src/ManiaAddNotesLab.Core/DecisionDiagnostics.cs",
        $"public const string Current = \"{value.DiagnosticVersion}\";",
        "compiled diagnostic version");
}

void ValidateMasterStateBlocks(ProjectState value)
{
    var current = value.Phases.FirstOrDefault(x => x.Id == value.CurrentPhase);
    var next = value.Phases.FirstOrDefault(x => x.Id == value.NextRecommendedPhase);
    var nextBehavioral = value.Phases.FirstOrDefault(x => x.Id == value.NextBehavioralPhase);
    var branch = (value.ResearchBranches ?? []).FirstOrDefault(x => x.Id == "F2");
    var blocker = value.Phases.FirstOrDefault(x => x.Id == branch?.BlockedBy);
    if (current is null || branch is null || blocker is null) return;
    var expectation = new DocumentationStateExpectation(current.Id, current.Status, current.Outcome,
        next?.Id, next?.Name, next?.Authorization ?? "N/A",
        nextBehavioral?.Id, nextBehavioral?.Name,
        nextBehavioral?.Authorization ?? "N/A", blocker.Id, blocker.Status,
        branch.Id, branch.Decision, value.BehaviorPolicyVersion,
        value.BehaviorChange ? "true" : "none", value.TestStatus.Passed,
        value.TestStatus.Failed, value.TestStatus.Skipped);
    foreach (var document in new[] { "README.md", "PROJECT_STATUS.md" })
    {
        var markdown = File.Exists(Path.Combine(root, document))
            ? File.ReadAllText(Path.Combine(root, document)) : string.Empty;
        errors.AddRange(DocumentationStateGuard.ValidateStateBlock(document, markdown, expectation));
        var block = ReadStateBlock(document);
        if (block is null) continue;
        CheckBlockContains(document, block, $"Evidence profile: `{value.EvidenceProfileVersion}`", "evidence profile value");
        CheckBlockContains(document, block, $"Diagnostic schema: `{value.DiagnosticVersion}`", "diagnostic value");
    }
    errors.AddRange(DocumentationStateGuard.ValidateRoadmap(
        File.ReadAllText(Path.Combine(root, "ROADMAP.md")), expectation));
}

void ValidatePhaseSummaries(ProjectState value)
{
    foreach (var phase in value.Phases)
    {
        CheckPhaseRow("PROJECT_STATUS.md", phase);
        CheckPhaseRow("ROADMAP.md", phase);
    }

    var current = value.Phases.FirstOrDefault(x => x.Id == value.CurrentPhase);
    var next = value.Phases.FirstOrDefault(x => x.Id == value.NextRecommendedPhase);
    if (current is not null)
        CheckNormalizedContains("docs/MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md",
            $"{current.Id} COMPLETE OUTCOME {current.Outcome}", "technical roadmap current phase closure");
    if (next is not null)
        CheckContains("docs/MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md", $"Phase {next.Id} —",
            "technical roadmap next phase");
}

void ValidateObsoletePhrases(ProjectState value)
{
    var scanned = value.MasterDocuments.Where(x => x.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        .Select(x => Path.Combine(root, x)).Where(File.Exists).ToArray();
    var obsolete = new[]
    {
        "única familia humana disponible",
        "Double-counting witnesses | duration+release duplica autoridad | deduplicación por ObservationId",
        "Deduplicar por `(candidate, ObservationId)`; varias etiquetas no multiplican testigos.",
        "C1.2 — Agreement modeling/shadow | ⏳ Recommended next",
        "C1.2 SHADOW RECOMMENDED NEXT",
        "D0.1 — Exact completion competition context | ➡️ **NEXT / NOT AUTHORIZED YET**"
    };
    foreach (var phrase in obsolete)
    foreach (var file in scanned)
        if (File.ReadAllText(file).Contains(phrase, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Obsolete phrase in {Path.GetRelativePath(root, file)}: {phrase}");
}

void CheckPhaseRow(string relative, PhaseState phase)
{
    var path = Path.Combine(root, relative);
    if (!File.Exists(path))
    {
        errors.Add($"Missing phase summary: {relative}");
        return;
    }
    var row = DocumentationStateGuard.FindPhaseRow(File.ReadAllText(path), phase.Id);
    if (row is null)
    {
        errors.Add($"{relative} has no summary row for phase {phase.Id}.");
        return;
    }
    if (!Normalize(row).Contains(Normalize(phase.Status), StringComparison.Ordinal))
        errors.Add($"{relative} phase {phase.Id} row does not reflect status {phase.Status}.");
    if (phase.Outcome is not null && !Normalize(row).Contains(Normalize(phase.Outcome), StringComparison.Ordinal))
        errors.Add($"{relative} phase {phase.Id} row does not reflect outcome {phase.Outcome}.");
}

void RequireValue(string? value, string property)
{
    if (string.IsNullOrWhiteSpace(value)) errors.Add($"PROJECT_STATE.json has an empty {property}.");
}

void RequireFile(string relative, string role)
{
    if (!File.Exists(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))))
        errors.Add($"Missing {role}: {relative}");
}

void CheckContains(string relative, string expected, string meaning)
{
    var path = Path.Combine(root, relative);
    if (!File.Exists(path) || !File.ReadAllText(path).Contains(expected, StringComparison.Ordinal))
        errors.Add($"{relative} does not reflect {meaning}: {expected}");
}

void CheckNormalizedContains(string relative, string expected, string meaning)
{
    var path = Path.Combine(root, relative);
    if (!File.Exists(path) || !Normalize(File.ReadAllText(path)).Contains(Normalize(expected), StringComparison.Ordinal))
        errors.Add($"{relative} does not reflect {meaning}: {expected}");
}

string? ReadStateBlock(string relative)
{
    var path = Path.Combine(root, relative);
    if (!File.Exists(path))
    {
        errors.Add($"Missing state block document: {relative}");
        return null;
    }
    const string begin = "<!-- PROJECT-STATE:BEGIN -->";
    const string end = "<!-- PROJECT-STATE:END -->";
    var text = File.ReadAllText(path);
    var start = text.IndexOf(begin, StringComparison.Ordinal);
    var finish = text.IndexOf(end, StringComparison.Ordinal);
    if (start < 0 || finish < 0 || finish <= start)
    {
        errors.Add($"{relative} has no valid PROJECT-STATE block.");
        return null;
    }
    if (text.IndexOf(begin, start + begin.Length, StringComparison.Ordinal) >= 0 ||
        text.IndexOf(end, finish + end.Length, StringComparison.Ordinal) >= 0)
    {
        errors.Add($"{relative} has more than one PROJECT-STATE block.");
        return null;
    }
    return text[(start + begin.Length)..finish];
}

void CheckBlockContains(string relative, string block, string expected, string meaning)
{
    if (!block.Contains(expected, StringComparison.Ordinal))
        errors.Add($"{relative} PROJECT-STATE block does not reflect {meaning}: {expected}");
}

static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit)
    .Select(char.ToUpperInvariant).ToArray());

static string FindRoot(string start)
{
    for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "ManiaAddNotesLab.sln"))) return directory.FullName;
    throw new DirectoryNotFoundException("Could not locate ManiaAddNotesLab repository root.");
}

sealed record ProjectState(
    int SchemaVersion,
    string LastUpdated,
    string BehaviorPolicyVersion,
    string EvidenceProfileVersion,
    string DiagnosticVersion,
    bool BehaviorChange,
    IReadOnlyList<PhaseState> Phases,
    IReadOnlyList<BranchState> ResearchBranches,
    IReadOnlyList<PhaseContractState> PhaseContracts,
    string CurrentPhase,
    string? NextRecommendedPhase,
    string? NextRecommendedAction,
    string? NextBehavioralPhase,
    TestState TestStatus,
    CorpusState ValidationCorpus,
    IReadOnlyList<string> MasterDocuments);

sealed record PhaseState(string Id, string Name, string Status, string? Outcome, bool? BehaviorChange,
    string? Report, string? Authorization, string? BlockedReason);
sealed record BranchState(string Id, string Decision, string BlockedBy);
sealed record PhaseContractState(string Id, string Kind, bool? BehaviorChange, string? Authorization);
sealed record TestState(int Passed, int Failed, int Skipped);
sealed record CorpusState(
    int Families,
    int HumanCharts,
    IReadOnlyList<int> Keymodes,
    int? ChartsWithLn,
    int? LnTargets,
    int? OriginalObjects,
    int? ExactHeadGroups,
    int? ChordGroups,
    int? CompletionTrials);
