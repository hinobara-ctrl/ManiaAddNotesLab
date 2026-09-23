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
    ValidateG10Closure(state);
    ValidateG1DesignClosure(state);
    ValidateG1GateClosure(state);
    ValidateSafetyCausalClosure(state);
    ValidateSafetyRemediationDesignClosure(state);
    ValidateSafetyRemediationGateClosure(state);
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

    foreach (var required in new[] { "C1", "C1.1", "C1.2", "C2", "D0", "D0.1", "D0.2", "D1.0", "D1.GATE", "D1", "D1.SAFETY", "SAFETY.PROV", "G1.0", "G1.DESIGN", "G1.GATE", "SAFETY.CAUSAL", "SAFETY.REMEDIATION.DESIGN", "SAFETY.REMEDIATION.GATE", "G1", "G2", "H", "E", "E.1", "F1", "F2", "F2.1", "F2.2", "F2.3", "F2.ACQ" })
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

    foreach (var required in new[] { "D1.0", "D1.GATE", "D1", "D1.SAFETY", "SAFETY.PROV", "G1.0", "G1.DESIGN", "G1.GATE", "SAFETY.CAUSAL", "SAFETY.REMEDIATION.DESIGN", "SAFETY.REMEDIATION.GATE", "G1", "G2", "H", "F2.ACQ", "C2" })
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
    if (value.CurrentPhase == "SAFETY.PROV" && (value.NextRecommendedAction != "ROADMAP_REVIEW"
        || value.NextRecommendedPhase is not null || value.NextBehavioralPhase is not null))
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
    CheckContains("PROJECT_STATUS.md", "ROADMAP REVIEW REQUIRED", "historical SAFETY.PROV roadmap boundary");
    CheckContains("ROADMAP.md", "ROADMAP REVIEW REQUIRED", "historical SAFETY.PROV roadmap boundary");
    CheckContains("docs/BEHAVIOR_DECISION_AUDIT.md", "temporally after divergence",
        "temporal versus causal downstream rule");
}

void ValidateG10Closure(ProjectState value)
{
    var phase = value.Phases.FirstOrDefault(x => x.Id == "G1.0");
    if (phase?.Status != "COMPLETE") return;
    if (phase.Outcome != "A" || phase.BehaviorChange is not false
        || phase.Authorization != "RESEARCH_COMPLETED_BEHAVIOR_NOT_AUTHORIZED")
        errors.Add("G1.0 must close COMPLETE/OUTCOME A with behaviorChange=false and no behavior authority.");
    if (value.CurrentPhase == "G1.0" && (value.NextRecommendedPhase != "G1.DESIGN"
        || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED" || value.NextBehavioralPhase != "G1"))
        errors.Add("G1.0 closure must expose only G1.DESIGN review and future G1 as NOT_AUTHORIZED.");
    var design = value.Phases.FirstOrDefault(x => x.Id == "G1.DESIGN");
    var behavior = value.Phases.FirstOrDefault(x => x.Id == "G1");
    if (design?.BehaviorChange is not false || behavior?.Status != "FUTURE"
        || behavior.Authorization != "NOT_AUTHORIZED" || behavior.BehaviorChange is not true)
        errors.Add("G1.DESIGN and G1 authorization boundaries are inconsistent.");

    foreach (var artifact in new[]
    {
        "docs/g1_0_interior_relation_contract.json",
        "docs/PHASE_G1_0_INTERIOR_RELATION_FEASIBILITY_DESIGN.md",
        "docs/PHASE_G1_0_INTERIOR_RELATION_FEASIBILITY_REPORT.md",
        "docs/g1_0_relation_census.csv",
        "docs/g1_0_relation_by_family.csv",
        "docs/g1_0_anchor_kind_summary.csv",
        "docs/g1_0_marginal_vs_joint_summary.csv",
        "docs/g1_0_alternative_summary.csv",
        "docs/g1_0_holdout_summary.csv",
        "docs/g1_0_legacy_gate_attrition.csv",
        "docs/g1_0_magic_number_ownership.csv",
        "docs/g1_0_determinism_summary.csv",
        "docs/PHASE_G1_0_VALIDATION_HARDENING_ADDENDUM.md",
        "docs/g1_0_validation_hardening_summary.json"
    })
    {
        RequireFile(artifact, "G1.0 closure artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "G1.0 artifact index entry");
    }

    const string frozenHash = "7C04E4CD9B45EE9351FBDC3C179083AAE43A5906115815A9F7987E0C51412194";
    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "g1_0_interior_relation_contract.json")));
        var contract = document.RootElement;
        var actual = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contract)));
        if (actual != frozenHash)
            errors.Add("G1.0 contract hash differs from its frozen canonical identity.");
        if (contract.GetProperty("behaviorChange").GetBoolean()
            || contract.GetProperty("defaultPolicy").GetString() != "legacy-experimental.1"
            || contract.GetProperty("authorizationBoundaries").GetProperty("g1Behavior").GetString()
                != "NOT_AUTHORIZED"
            || contract.GetProperty("authorizationBoundaries").GetProperty("g2Articulation").GetString()
                != "NOT_AUTHORIZED"
            || contract.GetProperty("authorizationBoundaries").GetProperty("hMultipleArticulation").GetString()
                != "NOT_AUTHORIZED")
            errors.Add("G1.0 machine contract does not preserve default/G1/G2/H boundaries.");
    }
    catch (Exception exception)
    {
        errors.Add($"G1.0 machine contract cannot be validated: {exception.Message}");
    }

    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "public const string BehaviorPolicyVersion = \"legacy-experimental.1\";", "legacy default after G1.0");
    foreach (var product in new[] { "src/ManiaAddNotesLab.Cli/Program.cs", "src/ManiaAddNotesLab.Web/Program.cs" })
    {
        var text = File.ReadAllText(Path.Combine(root, product.Replace('/', Path.DirectorySeparatorChar)));
        if (text.Contains("InteriorRelationFeasibilityResearch", StringComparison.Ordinal)
            || text.Contains("G10InteriorRelationRunner", StringComparison.Ordinal))
            errors.Add($"G1.0 research model is exposed by normal product path: {product}");
    }
    var d1 = value.Phases.FirstOrDefault(x => x.Id == "D1");
    var safety = value.Phases.FirstOrDefault(x => x.Id == "D1.SAFETY");
    var provenance = value.Phases.FirstOrDefault(x => x.Id == "SAFETY.PROV");
    var f2Acq = value.Phases.FirstOrDefault(x => x.Id == "F2.ACQ");
    var c2 = value.Phases.FirstOrDefault(x => x.Id == "C2");
    if (d1?.Outcome != "C" || safety?.Outcome != "C" || provenance?.Outcome != "A"
        || f2Acq?.Status != "BLOCKED" || c2?.Status != "DEFERRED")
        errors.Add("G1.0 must preserve D1/D1.SAFETY, SAFETY.PROV, F2.ACQ and C2 history.");
    CheckContains("docs/PHASE_G1_0_INTERIOR_RELATION_FEASIBILITY_REPORT.md",
        "COMPLETE — OUTCOME A", "G1.0 report outcome");
    CheckContains("docs/PHASE_G1_0_INTERIOR_RELATION_FEASIBILITY_REPORT.md",
        "behaviorChange=false", "G1.0 behavior neutrality");

    const string addendum = "docs/PHASE_G1_0_VALIDATION_HARDENING_ADDENDUM.md";
    const string summaryPath = "docs/g1_0_validation_hardening_summary.json";
    CheckContains(addendum, "RECERTIFICATION PASS", "G1.0 recertification result");
    CheckContains(addendum, "behaviorChange=false", "G1.0 recertification behavior neutrality");
    CheckContains(addendum, "G1.DESIGN", "G1.0 next candidate boundary");
    CheckContains(addendum, "NOT_AUTHORIZED", "G1.0 post-recertification authority boundary");
    CheckContains("docs/PROJECT_STATE.json", "\"safetyProvHistoricalClosureRequirement\": \"ROADMAP_REVIEW_REQUIRED\"",
        "historical SAFETY.PROV review requirement");
    CheckContains("docs/PROJECT_STATE.json", "\"safetyProvCurrentBlockingRequirement\": \"SATISFIED_",
        "satisfied current SAFETY.PROV review state");
    CheckContains("PROJECT_STATUS.md", "requisito histórico de cierre", "historical/current SAFETY.PROV distinction");

    var magic = File.ReadAllText(Path.Combine(root, "docs", "g1_0_magic_number_ownership.csv"));
    var articulation = magic.Split('\n').FirstOrDefault(x => x.StartsWith(
        "ArticulationMaxNonHeldColumns,", StringComparison.Ordinal));
    if (articulation is null || !articulation.Contains(",LEGACY_UNRESOLVED,", StringComparison.Ordinal)
        || articulation.Contains("INTENSITY_OR_CAPACITY_CANDIDATE", StringComparison.Ordinal))
        errors.Add("ArticulationMaxNonHeldColumns must remain a LEGACY_UNRESOLVED G2 boundary, not G1-established intensity evidence.");

    try
    {
        using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            summaryPath.Replace('/', Path.DirectorySeparatorChar))));
        var s = summary.RootElement;
        if (s.GetProperty("contractHash").GetString() != frozenHash
            || s.GetProperty("historicalOutcome").GetString() != "COMPLETE_OUTCOME_A_SHADOW"
            || s.GetProperty("recertificationStatus").GetString() != "PASS"
            || !s.GetProperty("relationAggregateMatch").GetBoolean()
            || !s.GetProperty("currentGateAggregateMatch").GetBoolean()
            || s.GetProperty("behaviorChange").GetBoolean()
            || s.GetProperty("rngCalls").GetInt32() != 0
            || s.GetProperty("nextCandidate").GetString() != "G1.DESIGN"
            || s.GetProperty("nextAuthorized").GetBoolean())
            errors.Add("G1.0 hardening summary does not preserve its PASS/outcome/contract/authority invariants.");
        if (s.GetProperty("actualLeakage").EnumerateObject().Any(x => x.Value.GetInt32() != 0))
            errors.Add("G1.0 Outcome A recertification cannot contain actual accepted leakage.");
        if (s.GetProperty("negativeControls").EnumerateObject().Any(x => !x.Value.GetBoolean()))
            errors.Add("Every G1.0 adversarial negative control must demonstrate detection.");
    }
    catch (Exception exception)
    {
        errors.Add($"G1.0 validation hardening summary cannot be validated: {exception.Message}");
    }
}

void ValidateG1DesignClosure(ProjectState value)
{
    var phase = value.Phases.FirstOrDefault(x => x.Id == "G1.DESIGN");
    if (phase?.Status != "COMPLETE") return;
    if (phase.Outcome != "READY" || phase.BehaviorChange is not false
        || phase.Authorization != "RESEARCH_COMPLETED_BEHAVIOR_NOT_AUTHORIZED")
        errors.Add("G1.DESIGN must close COMPLETE/READY with behaviorChange=false and no behavior authority.");
    var gate = value.Phases.FirstOrDefault(x => x.Id == "G1.GATE");
    var behavior = value.Phases.FirstOrDefault(x => x.Id == "G1");
    if (gate?.Status != "COMPLETE"
        && (value.CurrentPhase != "G1.DESIGN" || value.NextRecommendedPhase != "G1.GATE"
            || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED"
            || gate?.Status != "NEXT" || gate.Authorization != "NOT_AUTHORIZED"
            || gate.BehaviorChange is not false || behavior?.Status != "FUTURE"
            || behavior.Authorization != "NOT_AUTHORIZED"))
        errors.Add("Before runtime closure, G1.DESIGN must expose only G1.GATE review and future G1 as NOT_AUTHORIZED.");

    foreach (var artifact in new[]
    {
        "docs/PHASE_G1_DESIGN_INTERIOR_RELATION_ADMISSION.md",
        "docs/g1_design_interior_relation_admission_contract.json",
        "docs/g1_design_membership_summary.csv",
        "docs/g1_design_membership_by_family.csv",
        "docs/g1_design_support_provenance.csv",
        "docs/g1_design_construction_support_provenance.csv",
        "docs/g1_design_opportunity_summary.csv",
        "docs/g1_design_determinism_summary.csv",
        "docs/g1_design_summary.json",
        "docs/PHASE_G1_DESIGN_VALIDATION_HARDENING_ADDENDUM.md",
        "docs/g1_design_validation_hardening_summary.json"
    })
    {
        RequireFile(artifact, "G1.DESIGN closure artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "G1.DESIGN artifact index entry");
    }

    const string frozenHash = "15A16B6EFBF779CFF2C42A8C9A0DD46E025019252BEFA968E831233DC802AA68";
    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "g1_design_interior_relation_admission_contract.json")));
        var contract = document.RootElement;
        var actual = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contract)));
        if (actual != frozenHash)
            errors.Add("G1.DESIGN contract hash differs from its frozen canonical identity.");
        if (contract.GetProperty("behaviorChange").GetBoolean()
            || contract.GetProperty("productEvidenceScope").GetProperty("originalObjectsOnly").GetBoolean() is not true
            || contract.GetProperty("productEvidenceScope").GetProperty("fullChart").GetBoolean() is not true
            || contract.GetProperty("productEvidenceScope").GetProperty("crossChartProhibited").GetBoolean() is not true
            || contract.GetProperty("productEvidenceScope").GetProperty("syntheticProhibited").GetBoolean() is not true
            || contract.GetProperty("futureGate").GetProperty("rngCalls").GetInt32() != 0
            || contract.GetProperty("futureGate").GetProperty("reroll").GetBoolean()
            || contract.GetProperty("futureGate").GetProperty("candidateSubstitution").GetBoolean()
            || contract.GetProperty("authorizationBoundaries").GetProperty("g2Articulation").GetString() != "NOT_AUTHORIZED"
            || contract.GetProperty("authorizationBoundaries").GetProperty("hMultipleArticulation").GetString() != "NOT_AUTHORIZED")
            errors.Add("G1.DESIGN contract does not preserve exact scope/RNG/G2/H boundaries.");
        if (contract.GetProperty("excludedAuthority").EnumerateArray()
            .All(x => x.GetString() != "MapperSupport"))
            errors.Add("G1.DESIGN contract must exclude MapperSupport authority.");
    }
    catch (Exception exception)
    {
        errors.Add($"G1.DESIGN machine contract cannot be validated: {exception.Message}");
    }

    try
    {
        using var summary = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "g1_design_summary.json")));
        var s = summary.RootElement;
        if (s.GetProperty("status").GetString() != "READY"
            || s.GetProperty("behaviorChange").GetBoolean()
            || s.GetProperty("contractHash").GetString() != frozenHash
            || s.GetProperty("researchRngCalls").GetInt32() != 0
            || !s.GetProperty("deterministic").GetBoolean()
            || s.GetProperty("candidateShapes").GetInt32() != s.GetProperty("exactlyRepresentable").GetInt32()
            || s.GetProperty("unresolvableExactIdentity").GetInt32() != 0
            || s.GetProperty("hypotheticalAdmit").GetInt32() <= 0
            || s.GetProperty("nextCandidate").GetString() != "G1.GATE"
            || s.GetProperty("nextAuthorized").GetBoolean())
            errors.Add("G1.DESIGN summary does not preserve READY/representability/RNG/authority invariants.");
        var opportunities = s.GetProperty("opportunityDistribution");
        if (opportunities.GetProperty("totalOpportunities").GetInt32() != 298
            || opportunities.GetProperty("zeroAdmitted").GetInt32()
                + opportunities.GetProperty("atLeastOneAdmitted").GetInt32() != 298)
            errors.Add("G1.DESIGN opportunity distribution does not preserve its exact denominator.");
        if (s.GetProperty("reachedDistinctExactQueries").GetInt32()
            != s.GetProperty("queriesWithNoObservedResult").GetInt32()
                + s.GetProperty("queriesWithOneObservedResult").GetInt32()
                + s.GetProperty("queriesWithMultipleObservedResults").GetInt32())
            errors.Add("G1.DESIGN reached-query denominator is inconsistent.");
    }
    catch (Exception exception)
    {
        errors.Add($"G1.DESIGN summary cannot be validated: {exception.Message}");
    }

    var productPaths = gate?.Status == "COMPLETE"
        ? new[] { "src/ManiaAddNotesLab.Cli/Program.cs", "src/ManiaAddNotesLab.Web/Program.cs" }
        : new[] { "src/ManiaAddNotesLab.Core/AddNotesEngine.cs", "src/ManiaAddNotesLab.Core/Model.cs",
            "src/ManiaAddNotesLab.Cli/Program.cs", "src/ManiaAddNotesLab.Web/Program.cs" };
    foreach (var product in productPaths)
    {
        var text = File.ReadAllText(Path.Combine(root, product.Replace('/', Path.DirectorySeparatorChar)));
        if (text.Contains("InteriorRelationMembershipResearch", StringComparison.Ordinal)
            || text.Contains("g1-design", StringComparison.OrdinalIgnoreCase)
            || text.Contains("G1Membership", StringComparison.Ordinal)
            || text.Contains("InteriorRelationAdmission", StringComparison.Ordinal))
            errors.Add($"G1.DESIGN research/gate is exposed by normal product path: {product}");
    }
    var g10 = value.Phases.FirstOrDefault(x => x.Id == "G1.0");
    var d1 = value.Phases.FirstOrDefault(x => x.Id == "D1");
    var safety = value.Phases.FirstOrDefault(x => x.Id == "D1.SAFETY");
    var provenance = value.Phases.FirstOrDefault(x => x.Id == "SAFETY.PROV");
    var f2Acq = value.Phases.FirstOrDefault(x => x.Id == "F2.ACQ");
    var c2 = value.Phases.FirstOrDefault(x => x.Id == "C2");
    var g2 = value.Phases.FirstOrDefault(x => x.Id == "G2");
    var h = value.Phases.FirstOrDefault(x => x.Id == "H");
    if (g10?.Outcome != "A" || d1?.Outcome != "C" || safety?.Outcome != "C"
        || provenance?.Outcome != "A" || f2Acq?.Status != "BLOCKED" || c2?.Status != "DEFERRED"
        || g2?.Authorization != "NOT_AUTHORIZED" || h?.Authorization != "NOT_AUTHORIZED")
        errors.Add("G1.DESIGN must preserve G1.0, D1/D1.SAFETY, SAFETY.PROV, F2.ACQ and C2 history.");
    CheckContains("docs/PHASE_G1_0_VALIDATION_HARDENING_ADDENDUM.md",
        "RECERTIFICATION PASS", "G1.0 recertification dependency after G1.DESIGN");
    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "public const string BehaviorPolicyVersion = \"legacy-experimental.1\";", "legacy default after G1.DESIGN");
    foreach (var researchPath in new[] { "src/ManiaAddNotesLab.Core/InteriorRelationMembershipResearch.cs",
                 "tools/ManiaAddNotesLab.Experiments/G1DesignMembershipRunner.cs" })
    {
        var researchText = File.ReadAllText(Path.Combine(root,
            researchPath.Replace('/', Path.DirectorySeparatorChar)));
        foreach (var forbidden in new[] { "IRandomSource", "SeededRandom", "System.Random", "TakeWeighted(" })
            if (researchText.Contains(forbidden, StringComparison.Ordinal))
                errors.Add($"G1.DESIGN structural RNG boundary violated by {forbidden} in {researchPath}.");
    }

    const string hardeningAddendum = "docs/PHASE_G1_DESIGN_VALIDATION_HARDENING_ADDENDUM.md";
    CheckContains(hardeningAddendum, "RECERTIFICATION PASS", "G1.DESIGN recertification result");
    CheckContains(hardeningAddendum, "READY denotes design readiness only",
        "G1.DESIGN READY non-promotion boundary");
    CheckContains(hardeningAddendum, "candidate-universe membership potential",
        "corrected candidate-universe interpretation");
    try
    {
        using var hardening = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "g1_design_validation_hardening_summary.json")));
        var hardeningRoot = hardening.RootElement;
        if (hardeningRoot.GetProperty("recertificationStatus").GetString() != "PASS"
            || hardeningRoot.GetProperty("behaviorChange").GetBoolean()
            || hardeningRoot.GetProperty("contractHash").GetString() != frozenHash
            || !hardeningRoot.GetProperty("contractUnchanged").GetBoolean()
            || !hardeningRoot.GetProperty("aggregateReproduction").GetProperty("exact").GetBoolean()
            || !hardeningRoot.GetProperty("syntheticNormalization").GetProperty("aggregateUnchanged").GetBoolean()
            || hardeningRoot.GetProperty("syntheticNormalization").GetProperty("injectedCharts").GetInt32() != 11
            || hardeningRoot.GetProperty("constructionMembershipProvenance").GetProperty("counts")
                .GetProperty("Unattributable").GetInt32() != 0
            || hardeningRoot.GetProperty("rngCertification").GetProperty("evaluatorApiAcceptsRng").GetBoolean()
            || hardeningRoot.GetProperty("rngCertification").GetProperty("zeroCountersAreProof").GetBoolean()
            || !hardeningRoot.GetProperty("badControls").GetProperty("realEvaluatorControlsPass").GetBoolean()
            || !hardeningRoot.GetProperty("badControls").GetProperty("designContractControlsPass").GetBoolean()
            || hardeningRoot.GetProperty("nextAuthorized").GetBoolean())
            errors.Add("G1.DESIGN hardening summary does not preserve recertification invariants.");
        CheckContains(hardeningAddendum, hardeningRoot.GetProperty("implementationSnapshotHash").GetString()!,
            "G1.DESIGN hardening implementation snapshot");
    }
    catch (Exception exception)
    {
        errors.Add($"G1.DESIGN validation hardening summary cannot be validated: {exception.Message}");
    }

    foreach (var document in new[] { "README.md", "PROJECT_STATUS.md", "ROADMAP.md",
                 "docs/EXPERIMENTS.md", "docs/MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md",
                 "docs/BEHAVIOR_DECISION_AUDIT.md", "docs/PHASE_G1_DESIGN_INTERIOR_RELATION_ADMISSION.md" })
        if (File.ReadAllText(Path.Combine(root, document.Replace('/', Path.DirectorySeparatorChar)))
            .Contains("direct-effect potential", StringComparison.OrdinalIgnoreCase))
            errors.Add($"G1.DESIGN overclaim remains outside frozen contract/addendum history: {document}");
}

void ValidateG1GateClosure(ProjectState value)
{
    var gate = value.Phases.FirstOrDefault(x => x.Id == "G1.GATE");
    if (gate?.Status != "COMPLETE") return;
    if (gate.Outcome != "NEEDS_REVIEW" || gate.BehaviorChange is not true
        || gate.Authorization != "EXPERIMENT_COMPLETED_NO_PROMOTION"
        || value.CurrentPhase is not ("G1.GATE" or "SAFETY.CAUSAL" or "SAFETY.REMEDIATION.DESIGN" or "SAFETY.REMEDIATION.GATE") || value.NextRecommendedPhase is not null
        || value.NextBehavioralPhase is not null || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED")
        errors.Add("G1.GATE must close COMPLETE/NEEDS_REVIEW with no promotion or authorized successor.");

    foreach (var artifact in new[]
    {
        "docs/PHASE_G1_GATE_INTERIOR_RELATION_ADMISSION_RUNTIME.md",
        "docs/g1_gate_interior_relation_admission_contract.json",
        "docs/g1_gate_runtime_manifest.json",
        "docs/g1_gate_summary.json",
        "docs/g1_gate_paired_runs.csv",
        "docs/g1_gate_decisions.csv",
        "docs/g1_gate_family_summary.csv",
        "docs/g1_gate_safety_provenance.csv",
        "docs/g1_gate_forensic_summary.csv",
        "docs/g1_gate_forensic_summary.json"
    })
    {
        RequireFile(artifact, "G1.GATE closure artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "G1.GATE closure artifact index entry");
    }

    const string frozenHash = "0281FDA2A16E26CD9A176D2A59BF80DA421D2A419D70FAD33B0F02790088FB9A";
    try
    {
        using var contractDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "g1_gate_interior_relation_admission_contract.json")));
        var actual = Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(contractDocument.RootElement.GetProperty("contract"))));
        if (actual != frozenHash)
            errors.Add("G1.GATE contract hash differs from its frozen canonical identity.");

        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "g1_gate_summary.json")));
        var summary = summaryDocument.RootElement;
        var safety = summary.GetProperty("safety");
        var direct = summary.GetProperty("direct");
        if (summary.GetProperty("decision").GetString() != "NEEDS_REVIEW"
            || summary.GetProperty("contractSha256").GetString() != frozenHash
            || safety.GetProperty("attributableIntroducedViolations").GetInt32() != 9
            || safety.GetProperty("unattributableSafetyCases").GetInt32() != 0
            || direct.GetProperty("gateRngCalls").GetInt32() != 0
            || direct.GetProperty("replacementAttempts").GetInt32() != 0
            || direct.GetProperty("articulationIntents").GetInt32() != 0
            || !summary.GetProperty("deterministicRepeat").GetBoolean()
            || summary.GetProperty("behavior").GetProperty("promotion").GetString() != "PROHIBITED"
            || summary.GetProperty("badControls").EnumerateObject().Any(x => !x.Value.GetBoolean()))
            errors.Add("G1.GATE summary does not preserve stop, isolation, determinism and bad-control invariants.");

        using var forensicDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "g1_gate_forensic_summary.json")));
        var forensic = forensicDocument.RootElement;
        if (!forensic.GetProperty("stopConditionMet").GetBoolean()
            || forensic.GetProperty("attributableIntroducedViolations").GetInt32() != 9
            || forensic.GetProperty("remainingUnattributable").GetInt32() != 0)
            errors.Add("G1.GATE forensic summary does not preserve the causal stop result.");
    }
    catch (Exception exception)
    {
        errors.Add($"G1.GATE closure artifacts cannot be validated: {exception.Message}");
    }

    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "public const string BehaviorPolicyVersion = \"legacy-experimental.1\";",
        "legacy default after G1.GATE");
}

void ValidateSafetyCausalClosure(ProjectState value)
{
    var phase = value.Phases.FirstOrDefault(x => x.Id == "SAFETY.CAUSAL");
    if (phase?.Status != "COMPLETE") return;
    if (phase.Outcome != "A" || phase.BehaviorChange is not false
        || phase.Authorization != "RESEARCH_COMPLETED_NO_REMEDIATION"
        || value.CurrentPhase is not ("SAFETY.CAUSAL" or "SAFETY.REMEDIATION.DESIGN" or "SAFETY.REMEDIATION.GATE") || value.NextRecommendedPhase is not null
        || value.NextBehavioralPhase is not null || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED")
        errors.Add("SAFETY.CAUSAL must close COMPLETE/A with no remediation, promotion or successor.");

    var design = value.Phases.FirstOrDefault(x => x.Id == "SAFETY.REMEDIATION.DESIGN");
    var designContract = value.PhaseContracts.FirstOrDefault(x => x.Id == "SAFETY.REMEDIATION.DESIGN");
    if (design?.Status != "COMPLETE" || design.Outcome != "READY_FOR_SEPARATE_REMEDIATION_GATE"
        || design.BehaviorChange is not false || design.Authorization != "RESEARCH_COMPLETED_NO_IMPLEMENTATION"
        || designContract?.Kind != "ResearchShadow" || designContract.BehaviorChange is not false
        || designContract.Authorization != "RESEARCH_COMPLETED_NO_IMPLEMENTATION")
        errors.Add("SAFETY.REMEDIATION.DESIGN must close research-only without implementing or authorizing remediation.");

    foreach (var artifact in new[]
    {
        "docs/PHASE_SAFETY_CAUSAL_DOWNSTREAM_HARD_VALIDITY.md",
        "docs/safety_causal_downstream_hard_validity_contract.json",
        "docs/safety_causal_summary.json",
        "docs/safety_causal_treatment_violations.csv",
        "docs/safety_causal_treatment_violations.json",
        "docs/safety_causal_control_classification.csv",
        "docs/safety_causal_control_classification.json",
        "docs/safety_causal_placement_oracle_comparison.csv",
        "docs/safety_causal_family_summary.csv",
        "docs/safety_causal_cases.json"
    })
    {
        RequireFile(artifact, "SAFETY.CAUSAL closure artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "SAFETY.CAUSAL artifact index entry");
    }

    const string frozenHash = "300E879BCB479F77A704BFFB89BF304D9D04ECC556067F921C49C45E4E8FCB84";
    try
    {
        using var contractDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "safety_causal_downstream_hard_validity_contract.json")));
        var actual = Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(contractDocument.RootElement.GetProperty("contract"))));
        if (actual != frozenHash || contractDocument.RootElement.GetProperty("canonicalSha256").GetString() != frozenHash)
            errors.Add("SAFETY.CAUSAL contract hash differs from its frozen canonical identity.");

        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "safety_causal_summary.json")));
        var summary = summaryDocument.RootElement;
        var treatment = summary.GetProperty("treatment");
        var control = summary.GetProperty("control");
        var validation = summary.GetProperty("validation");
        var behavior = summary.GetProperty("behavior");
        if (summary.GetProperty("outcome").GetString() != "A"
            || summary.GetProperty("contractSha256").GetString() != frozenHash
            || treatment.GetProperty("hardValidityViolations").GetInt32() != 9
            || treatment.GetProperty("treatmentDownstream").GetInt32() != 9
            || treatment.GetProperty("unattributable").GetInt32() != 0
            || control.GetProperty("hardValidityConditions").GetInt32() != 200
            || control.GetProperty("legacyDownstream").GetInt32() != 200
            || control.GetProperty("sourcePreExisting").GetInt32() != 0
            || control.GetProperty("unattributable").GetInt32() != 0
            || validation.GetProperty("nonInterferenceFailures").GetInt32() != 0
            || validation.GetProperty("traceValidationFailures").GetInt32() != 0
            || validation.GetProperty("recorderRngCalls").GetInt32() != 0
            || !validation.GetProperty("artifactProjectionDeterministic").GetBoolean()
            || !validation.GetProperty("independentReplayDeterministic").GetBoolean()
            || behavior.GetProperty("behaviorChange").GetBoolean()
            || behavior.GetProperty("defaultBehaviorChange").GetBoolean()
            || behavior.GetProperty("generationSemanticsChange").GetBoolean()
            || behavior.GetProperty("remediationImplemented").GetBoolean())
            errors.Add("SAFETY.CAUSAL summary does not preserve counts, evidence and no-remediation boundaries.");
    }
    catch (Exception exception)
    {
        errors.Add($"SAFETY.CAUSAL closure artifacts cannot be validated: {exception.Message}");
    }

    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "public const string BehaviorPolicyVersion = \"legacy-experimental.1\";",
        "legacy default after SAFETY.CAUSAL");
}

void ValidateSafetyRemediationDesignClosure(ProjectState value)
{
    var phase = value.Phases.FirstOrDefault(x => x.Id == "SAFETY.REMEDIATION.DESIGN");
    if (phase?.Status != "COMPLETE") return;
    if (phase.Outcome != "READY_FOR_SEPARATE_REMEDIATION_GATE" || phase.BehaviorChange is not false
        || phase.Authorization != "RESEARCH_COMPLETED_NO_IMPLEMENTATION"
        || value.CurrentPhase is not ("SAFETY.REMEDIATION.DESIGN" or "SAFETY.REMEDIATION.GATE") || value.NextRecommendedPhase is not null
        || value.NextBehavioralPhase is not null || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED")
        errors.Add("SAFETY.REMEDIATION.DESIGN must close READY_FOR_SEPARATE_REMEDIATION_GATE with no implementation or authorized successor.");

    foreach (var artifact in new[]
    {
        "docs/PHASE_SAFETY_REMEDIATION_DESIGN.md",
        "docs/safety_remediation_design_contract.json",
        "docs/safety_remediation_design_summary.json",
        "docs/safety_remediation_candidate_matrix.csv",
        "docs/safety_remediation_representation_inventory.csv",
        "docs/safety_remediation_shadow_footprint.csv"
    })
    {
        RequireFile(artifact, "SAFETY.REMEDIATION.DESIGN closure artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact,
            "SAFETY.REMEDIATION.DESIGN closure artifact index entry");
    }

    const string frozenHash = "EFB31F2BF5026BE7353ACB30C15768389D077CC7244B0C91D66F8B6B6BD002F8";
    try
    {
        using var contractDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "safety_remediation_design_contract.json")));
        var actual = Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(contractDocument.RootElement.GetProperty("contract"))));
        if (actual != frozenHash
            || contractDocument.RootElement.GetProperty("canonicalSha256").GetString() != frozenHash)
            errors.Add("SAFETY.REMEDIATION.DESIGN contract hash differs from its frozen canonical identity.");

        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "safety_remediation_design_summary.json")));
        var summary = summaryDocument.RootElement;
        var shadow = summary.GetProperty("directShadow");
        var validation = summary.GetProperty("validation");
        var boundaries = summary.GetProperty("boundaries");
        if (summary.GetProperty("outcome").GetString() != "READY_FOR_SEPARATE_REMEDIATION_GATE"
            || summary.GetProperty("contractSha256").GetString() != frozenHash
            || shadow.GetProperty("totalEvaluated").GetInt32() != 307167
            || shadow.GetProperty("legacyAcceptToCandidateReject").GetInt32() != 215
            || shadow.GetProperty("known209Addressed").GetInt32() != 209
            || shadow.GetProperty("known209NotAddressed").GetInt32() != 0
            || shadow.GetProperty("additionalDirectDeltasOutsideKnown209").GetInt32() != 6
            || shadow.GetProperty("directDeltaIsFinalOutputDelta").GetBoolean()
            || validation.GetProperty("observerProjectionFailures").GetInt32() != 0
            || validation.GetProperty("observerRngCalls").GetInt64() != 0
            || !validation.GetProperty("artifactOrderingDeterministic").GetBoolean()
            || boundaries.GetProperty("behaviorChange").GetBoolean()
            || boundaries.GetProperty("generationSemanticsChange").GetBoolean()
            || boundaries.GetProperty("defaultBehaviorChange").GetBoolean()
            || boundaries.GetProperty("g1SemanticsChange").GetBoolean()
            || boundaries.GetProperty("remediationImplemented").GetBoolean()
            || boundaries.GetProperty("nextPhase").GetString() != "NOT_AUTHORIZED")
            errors.Add("SAFETY.REMEDIATION.DESIGN summary does not preserve footprint, non-interference and authorization boundaries.");
    }
    catch (Exception exception)
    {
        errors.Add($"SAFETY.REMEDIATION.DESIGN artifacts cannot be validated: {exception.Message}");
    }

    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "public const string BehaviorPolicyVersion = \"legacy-experimental.1\";",
        "legacy default after SAFETY.REMEDIATION.DESIGN");
}

void ValidateSafetyRemediationGateClosure(ProjectState value)
{
    var phase = value.Phases.FirstOrDefault(x => x.Id == "SAFETY.REMEDIATION.GATE");
    var contractState = value.PhaseContracts.FirstOrDefault(x => x.Id == "SAFETY.REMEDIATION.GATE");
    if (phase?.Status != "COMPLETE") return;
    if (phase.Outcome != "REMEDIATION_RUNTIME_CERTIFIED" || phase.BehaviorChange is not true
        || phase.Authorization != "EXPERIMENT_COMPLETED_NO_PROMOTION"
        || contractState?.Kind != "BehaviorChanging" || contractState.BehaviorChange is not true
        || contractState.Authorization != "EXPERIMENT_COMPLETED_NO_PROMOTION"
        || value.CurrentPhase != "SAFETY.REMEDIATION.GATE" || value.NextRecommendedPhase is not null
        || value.NextBehavioralPhase is not null || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED")
        errors.Add("SAFETY.REMEDIATION.GATE must close certified, experimental-only and without promotion or successor.");

    foreach (var artifact in new[]
    {
        "docs/PHASE_SAFETY_REMEDIATION_GATE.md",
        "docs/safety_remediation_gate_contract.json",
        "docs/safety_remediation_gate_runs.csv",
        "docs/safety_remediation_gate_known_cases.csv",
        "docs/safety_remediation_gate_summary.json"
    })
    {
        RequireFile(artifact, "SAFETY.REMEDIATION.GATE closure artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact,
            "SAFETY.REMEDIATION.GATE closure artifact index entry");
    }

    const string frozenHash = "9415E4710E44163D26BF56123C3166EF9F52C43AA376E53BA7D2E8305E3293D8";
    const string snapshot = "93DD2E5E2D7FC6C49FB33B34C7CCDCC58870FA6BE835892FF7903A09D4ADCBE7";
    try
    {
        using var contractDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "safety_remediation_gate_contract.json")));
        var actual = Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(contractDocument.RootElement.GetProperty("contract"))));
        var contract = contractDocument.RootElement.GetProperty("contract");
        if (actual != frozenHash
            || contractDocument.RootElement.GetProperty("canonicalSha256").GetString() != frozenHash
            || contract.GetProperty("implementationSnapshotSha256").GetString() != snapshot
            || contract.GetProperty("defaultBehaviorChanged").GetBoolean()
            || contract.GetProperty("successorAuthorization").GetString() != "NOT_AUTHORIZED")
            errors.Add("SAFETY.REMEDIATION.GATE contract identity or authorization boundary drifted.");

        using var summaryDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "safety_remediation_gate_summary.json")));
        var summary = summaryDocument.RootElement;
        var corpus = summary.GetProperty("corpus");
        var runtime = summary.GetProperty("runtime");
        var cases = summary.GetProperty("frozenCases");
        var validation = summary.GetProperty("validation");
        var boundaries = summary.GetProperty("boundaries");
        if (summary.GetProperty("outcome").GetString() != "REMEDIATION_RUNTIME_CERTIFIED"
            || summary.GetProperty("contractSha256").GetString() != frozenHash
            || summary.GetProperty("implementationSnapshotSha256").GetString() != snapshot
            || corpus.GetProperty("primaryPairs").GetInt32() != 220
            || corpus.GetProperty("secondaryPairs").GetInt32() != 4
            || runtime.GetProperty("controlHardViolations").GetInt32() != 215
            || runtime.GetProperty("treatmentHardViolations").GetInt32() != 0
            || runtime.GetProperty("treatmentOnlyHardViolations").GetInt32() != 0
            || runtime.GetProperty("gateRngCalls").GetInt32() != 0
            || cases.GetProperty("observedKnown").GetInt32() != 209
            || cases.GetProperty("observedAdditional").GetInt32() != 6
            || cases.GetProperty("unexpected").GetInt32() != 0
            || validation.GetProperty("defaultEquivalenceFailures").GetInt32() != 0
            || validation.GetProperty("deterministicRerunFailures").GetInt32() != 0
            || validation.GetProperty("serializationReparseFailures").GetInt32() != 0
            || validation.GetProperty("g1EvidenceHashFailures").GetInt32() != 0
            || validation.GetProperty("hardValiditySemanticsChanged").GetBoolean()
            || validation.GetProperty("latentG1IdentityChanged").GetBoolean()
            || validation.GetProperty("productDefaultChanged").GetBoolean()
            || boundaries.GetProperty("g1Promotion").GetString() != "NOT_AUTHORIZED"
            || boundaries.GetProperty("successor").GetString() != "NOT_AUTHORIZED")
            errors.Add("SAFETY.REMEDIATION.GATE summary does not preserve certification and no-promotion invariants.");

        if (File.ReadLines(Path.Combine(root, "docs", "safety_remediation_gate_runs.csv")).Count() != 225
            || File.ReadLines(Path.Combine(root, "docs", "safety_remediation_gate_known_cases.csv")).Count() != 216)
            errors.Add("SAFETY.REMEDIATION.GATE public CSV denominators drifted.");
    }
    catch (Exception exception)
    {
        errors.Add($"SAFETY.REMEDIATION.GATE closure artifacts cannot be validated: {exception.Message}");
    }

    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "public const string BehaviorPolicyVersion = \"legacy-experimental.1\";",
        "legacy default after SAFETY.REMEDIATION.GATE");
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
