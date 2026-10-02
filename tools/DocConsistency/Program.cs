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
const string lane0PostAttemptPhase = "LANE.0.CORRECTIVE_POST_ATTEMPT_FORENSICS";
const string lane0SuccessorPhase = "LANE.0.CORRECTIVE_SUCCESSOR.PREREGISTRATION";
const string lane0SuccessorAuditPhase = "LANE.0.CORRECTIVE_SUCCESSOR.PHASE1_AUDIT";
const string lane0SuccessorPhase2 = "LANE.0.CORRECTIVE_SUCCESSOR.EXECUTION_PREPARATION";
const string lane0SuccessorPhase2Audit = "LANE.0.CORRECTIVE_SUCCESSOR.PHASE2_AUDIT";
const string lane0IntegrationPreregistration =
    "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_PREREGISTRATION";
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
    ValidateSafetyRemediationFollowup();
    ValidateLane0Closure(state);
    ValidateLane0FutureHeldRemediation(state);
    ValidateLane0FutureHeldHardening(state);
    ValidateLane0CounterClosure(state);
    ValidateLane0CorrectivePostAttemptForensics(state);
    ValidateLane0CorrectiveSuccessorPhase1(state);
    ValidateLane0CorrectiveSuccessorPhase1Audit(state);
    ValidateLane0CorrectiveSuccessorPhase2(state);
    ValidateLane0CorrectiveSuccessorPhase2Audit(state);
    ValidateLane0CorrectiveSuccessorIntegrationPreregistration(state);
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

    foreach (var required in new[] { "C1", "C1.1", "C1.2", "C2", "D0", "D0.1", "D0.2", "D1.0", "D1.GATE", "D1", "D1.SAFETY", "SAFETY.PROV", "G1.0", "G1.DESIGN", "G1.GATE", "SAFETY.CAUSAL", "SAFETY.REMEDIATION.DESIGN", "SAFETY.REMEDIATION.GATE", "G1", "G2", "H", "E", "E.1", "F1", "F2", "F2.1", "F2.2", "F2.3", "F2.ACQ", "LANE.0", "LANE.0.REMEDIATION", "LANE.0.HARDENING", lane0PostAttemptPhase, lane0SuccessorPhase, lane0SuccessorAuditPhase, lane0SuccessorPhase2, lane0SuccessorPhase2Audit, lane0IntegrationPreregistration })
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

    foreach (var required in new[] { "D1.0", "D1.GATE", "D1", "D1.SAFETY", "SAFETY.PROV", "G1.0", "G1.DESIGN", "G1.GATE", "SAFETY.CAUSAL", "SAFETY.REMEDIATION.DESIGN", "SAFETY.REMEDIATION.GATE", "G1", "G2", "H", "F2.ACQ", "C2", lane0PostAttemptPhase, lane0SuccessorPhase, lane0SuccessorAuditPhase, lane0SuccessorPhase2 })
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
        || value.CurrentPhase is not ("G1.GATE" or "SAFETY.CAUSAL" or "SAFETY.REMEDIATION.DESIGN" or "SAFETY.REMEDIATION.GATE" or "LANE.0" or "LANE.0.REMEDIATION" or "LANE.0.HARDENING" or "LANE.0.CORRECTIVE_POST_ATTEMPT_FORENSICS" or "LANE.0.CORRECTIVE_SUCCESSOR.PREREGISTRATION" or "LANE.0.CORRECTIVE_SUCCESSOR.PHASE1_AUDIT" or "LANE.0.CORRECTIVE_SUCCESSOR.EXECUTION_PREPARATION" or "LANE.0.CORRECTIVE_SUCCESSOR.PHASE2_AUDIT" or "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_PREREGISTRATION") || value.NextRecommendedPhase is not null
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
        || value.CurrentPhase is not ("SAFETY.CAUSAL" or "SAFETY.REMEDIATION.DESIGN" or "SAFETY.REMEDIATION.GATE" or "LANE.0" or "LANE.0.REMEDIATION" or "LANE.0.HARDENING" or "LANE.0.CORRECTIVE_POST_ATTEMPT_FORENSICS" or "LANE.0.CORRECTIVE_SUCCESSOR.PREREGISTRATION" or "LANE.0.CORRECTIVE_SUCCESSOR.PHASE1_AUDIT" or "LANE.0.CORRECTIVE_SUCCESSOR.EXECUTION_PREPARATION" or "LANE.0.CORRECTIVE_SUCCESSOR.PHASE2_AUDIT" or "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_PREREGISTRATION") || value.NextRecommendedPhase is not null
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
        || value.CurrentPhase is not ("SAFETY.REMEDIATION.DESIGN" or "SAFETY.REMEDIATION.GATE" or "LANE.0" or "LANE.0.REMEDIATION" or "LANE.0.HARDENING" or "LANE.0.CORRECTIVE_POST_ATTEMPT_FORENSICS" or "LANE.0.CORRECTIVE_SUCCESSOR.PREREGISTRATION" or "LANE.0.CORRECTIVE_SUCCESSOR.PHASE1_AUDIT" or "LANE.0.CORRECTIVE_SUCCESSOR.EXECUTION_PREPARATION" or "LANE.0.CORRECTIVE_SUCCESSOR.PHASE2_AUDIT" or "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_PREREGISTRATION") || value.NextRecommendedPhase is not null
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
    if (phase.Outcome != "NEEDS_REVIEW" || phase.BehaviorChange is not true
        || phase.Authorization != "EXPERIMENT_COMPLETED_NO_PROMOTION"
        || contractState?.Kind != "BehaviorChanging" || contractState.BehaviorChange is not true
        || contractState.Authorization != "EXPERIMENT_COMPLETED_NO_PROMOTION"
        || value.CurrentPhase is not ("SAFETY.REMEDIATION.GATE" or "LANE.0" or "LANE.0.REMEDIATION" or "LANE.0.HARDENING" or "LANE.0.CORRECTIVE_POST_ATTEMPT_FORENSICS" or "LANE.0.CORRECTIVE_SUCCESSOR.PREREGISTRATION" or "LANE.0.CORRECTIVE_SUCCESSOR.PHASE1_AUDIT" or "LANE.0.CORRECTIVE_SUCCESSOR.EXECUTION_PREPARATION" or "LANE.0.CORRECTIVE_SUCCESSOR.PHASE2_AUDIT" or "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_PREREGISTRATION") || value.NextRecommendedPhase is not null
        || value.NextBehavioralPhase is not null || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED")
        errors.Add("SAFETY.REMEDIATION.GATE hardening must close NEEDS_REVIEW, experimental-only and without promotion or successor.");

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

    foreach (var artifact in new[]
    {
        "docs/PHASE_SAFETY_REMEDIATION_GATE_VALIDATION_HARDENING_ADDENDUM.md",
        "docs/safety_remediation_gate_validation_hardening_contract.json",
        "docs/safety_remediation_gate_implementation_inventory.csv",
        "docs/safety_remediation_gate_hardening_runs.csv",
        "docs/safety_remediation_gate_hardened_cases.csv",
        "docs/safety_remediation_gate_causal_unreachable.csv",
        "docs/safety_remediation_gate_validation_hardening_summary.json"
    })
    {
        RequireFile(artifact, "SAFETY.REMEDIATION.GATE hardening artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact,
            "SAFETY.REMEDIATION.GATE hardening artifact index entry");
    }

    const string hardeningContractHash = "7E68F1CDC0E2F783794B229939B1E0C28764D1C3646999CFC1F85DABC30B9539";
    const string hardenedImplementation = "B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835";
    const string frozenHarness = "81F2FA4BF462E169CCE8230C70DC792BF927E2BF57A93D4BDC571AD8F542DF72";
    try
    {
        using var hardeningContractDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "safety_remediation_gate_validation_hardening_contract.json")));
        var hardeningContract = hardeningContractDocument.RootElement.GetProperty("contract");
        var actualHardeningHash = Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(hardeningContract)));
        if (actualHardeningHash != hardeningContractHash
            || hardeningContractDocument.RootElement.GetProperty("canonicalSha256").GetString() != hardeningContractHash
            || hardeningContract.GetProperty("hardenedImplementationSnapshotSha256").GetString() != hardenedImplementation
            || hardeningContract.GetProperty("certificationHarnessSnapshotSha256").GetString() != frozenHarness
            || hardeningContract.GetProperty("executionMemoryLimit").GetString() != "0x400000000"
            || hardeningContract.GetProperty("successorAuthorization").GetString() != "NOT_AUTHORIZED")
            errors.Add("SAFETY.REMEDIATION.GATE hardening contract identity or boundary drifted.");

        using var hardeningSummaryDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "safety_remediation_gate_validation_hardening_summary.json")));
        var hardeningSummary = hardeningSummaryDocument.RootElement;
        var hardeningCorpus = hardeningSummary.GetProperty("corpus");
        var hardeningCases = hardeningSummary.GetProperty("cases");
        var hardeningRuntime = hardeningSummary.GetProperty("runtime");
        var hardeningValidation = hardeningSummary.GetProperty("validation");
        var hardeningBoundaries = hardeningSummary.GetProperty("boundaries");
        if (hardeningSummary.GetProperty("outcome").GetString() != "NEEDS_REVIEW"
            || hardeningSummary.GetProperty("hardeningContractSha256").GetString() != hardeningContractHash
            || hardeningSummary.GetProperty("hardenedImplementationSnapshotSha256").GetString() != hardenedImplementation
            || hardeningSummary.GetProperty("certificationHarnessSnapshotSha256").GetString() != frozenHarness
            || hardeningCorpus.GetProperty("primaryPairs").GetInt32() != 220
            || hardeningCorpus.GetProperty("secondaryPairs").GetInt32() != 4
            || hardeningCorpus.GetProperty("totalPairs").GetInt32() != 224
            || hardeningCases.GetProperty("total").GetInt32() != 215
            || hardeningCases.GetProperty("frozenKnown").GetInt32() != 209
            || hardeningCases.GetProperty("additional").GetInt32() != 6
            || hardeningCases.GetProperty("directCanonicalReject").GetInt32() != 188
            || hardeningCases.GetProperty("causallyProvenUnreachable").GetInt32() != 22
            || hardeningCases.GetProperty("unresolved").GetInt32() != 5
            || hardeningRuntime.GetProperty("controlHardViolations").GetInt32() != 215
            || hardeningRuntime.GetProperty("treatmentHardViolations").GetInt32() != 0
            || hardeningRuntime.GetProperty("treatmentOnlyHardViolations").GetInt32() != 0
            || hardeningRuntime.GetProperty("unexplainedStateDivergences").GetInt32() != 0
            || hardeningRuntime.GetProperty("gateRngCalls").GetInt32() != 0
            || hardeningValidation.GetProperty("defaultEquivalenceFailures").GetInt32() != 0
            || hardeningValidation.GetProperty("determinismFailures").GetInt32() != 0
            || hardeningValidation.GetProperty("reparseFailures").GetInt32() != 0
            || hardeningValidation.GetProperty("g1EvidenceFailures").GetInt32() != 0
            || hardeningBoundaries.GetProperty("behaviorChanged").GetBoolean()
            || hardeningBoundaries.GetProperty("originalArtifactsRewritten").GetBoolean()
            || hardeningBoundaries.GetProperty("promotion").GetString() != "NOT_AUTHORIZED"
            || hardeningBoundaries.GetProperty("successor").GetString() != "NOT_AUTHORIZED")
            errors.Add("SAFETY.REMEDIATION.GATE hardening summary does not preserve the NEEDS_REVIEW result and boundaries.");

        if (File.ReadLines(Path.Combine(root, "docs", "safety_remediation_gate_hardening_runs.csv")).Count() != 225
            || File.ReadLines(Path.Combine(root, "docs", "safety_remediation_gate_hardened_cases.csv")).Count() != 216
            || File.ReadLines(Path.Combine(root, "docs", "safety_remediation_gate_causal_unreachable.csv")).Count() != 23)
            errors.Add("SAFETY.REMEDIATION.GATE hardening CSV denominators drifted.");
    }
    catch (Exception exception)
    {
        errors.Add($"SAFETY.REMEDIATION.GATE hardening artifacts cannot be validated: {exception.Message}");
    }

    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "public const string BehaviorPolicyVersion = \"legacy-experimental.1\";",
        "legacy default after SAFETY.REMEDIATION.GATE");
}

void ValidateSafetyRemediationFollowup()
{
    const string contract = "docs/safety_remediation_gate_followup_contract.json";
    const string followup = "docs/safety_remediation_gate_unresolved_followup.json";
    foreach (var artifact in new[] { contract, followup })
    {
        RequireFile(artifact, "SAFETY.REMEDIATION.GATE follow-up artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact,
            "SAFETY.REMEDIATION.GATE follow-up artifact index entry");
    }
    foreach (var error in SafetyRemediationFollowupGuard.Validate(
                 Path.Combine(root, contract), Path.Combine(root, followup)))
        errors.Add($"SAFETY.REMEDIATION.GATE follow-up: {error}");
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

void ValidateLane0Closure(ProjectState value)
{
    var phase = value.Phases.FirstOrDefault(x => x.Id == "LANE.0");
    var phaseContract = value.PhaseContracts.FirstOrDefault(x => x.Id == "LANE.0");
    if (phase?.Status != "COMPLETE") return;
    if (phase.Outcome != "FEASIBILITY_DEMONSTRATED" || phase.BehaviorChange is not false
        || phase.Authorization != "RESEARCH_COMPLETED_NO_SUCCESSOR_AUTHORIZED"
        || phaseContract?.Kind != "ResearchShadow" || phaseContract.BehaviorChange is not false
        || phaseContract.Authorization != "RESEARCH_COMPLETED_NO_SUCCESSOR_AUTHORIZED"
        || value.NextRecommendedPhase is not null || value.NextBehavioralPhase is not null
        || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED")
        errors.Add("LANE.0 must close FEASIBILITY_DEMONSTRATED, research-only and without an authorized successor.");

    foreach (var artifact in new[]
    {
        "docs/PHASE_LANE_0_FEASIBILITY_DESIGN.md",
        "docs/lane_0_feasibility_contract.json",
        "docs/PHASE_LANE_0_FEASIBILITY_REPORT.md"
    })
    {
        RequireFile(artifact, "LANE.0 closure artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "LANE.0 closure artifact index entry");
    }

    const string frozen = "62B2F4F67C34E8F57893A3A025D567000E1BB12B6517C13D97FE9EEC3FE0A3C2";
    const string manifest = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "lane_0_feasibility_contract.json")));
        var artifact = document.RootElement;
        var contract = artifact.GetProperty("contract");
        if (artifact.GetProperty("canonicalSha256").GetString() != frozen
            || contract.GetProperty("schemaVersion").GetString() != "lane-0-original-spatial-feasibility.1"
            || contract.GetProperty("repositoryEntryHead").GetString() != "be08f3f2f0191a5c972ad16449a7199dd07f2e3f"
            || contract.GetProperty("corpusManifestSha256").GetString() != manifest
            || contract.GetProperty("memoryLimit").GetString() != "0x400000000"
            || contract.GetProperty("families").GetArrayLength() != 2
            || contract.GetProperty("populationUniverses").GetArrayLength() != 3
            || contract.GetProperty("leakageControls").GetArrayLength() != 11
            || contract.GetProperty("outcomes").EnumerateArray().All(x =>
                x.GetString() != "FEASIBILITY_DEMONSTRATED"))
            errors.Add("LANE.0 contract does not preserve its frozen identity, scope and outcome rule.");
    }
    catch (Exception exception)
    {
        errors.Add($"LANE.0 contract cannot be validated: {exception.Message}");
    }

    foreach (var marker in new[] { "40,360", "37,080", "16,881", "3,373", "11" })
        CheckContains("docs/PHASE_LANE_0_FEASIBILITY_REPORT.md", marker, "LANE.0 frozen result");
    CheckContains("src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "public const string BehaviorPolicyVersion = \"legacy-experimental.1\";",
        "legacy default after LANE.0");
}

void ValidateLane0FutureHeldRemediation(ProjectState value)
{
    var phase = value.Phases.FirstOrDefault(x => x.Id == "LANE.0.REMEDIATION");
    var phaseContract = value.PhaseContracts.FirstOrDefault(x => x.Id == "LANE.0.REMEDIATION");
    if (phase?.Status != "COMPLETE") return;
    if (phase.Outcome != "READY_FOR_CORRECTIVE_EVALUATION" || phase.BehaviorChange is not false
        || phase.Authorization != "STEP_2_PENDING_USER_PUBLICATION_AND_APPROVAL"
        || phaseContract?.Kind != "ResearchShadow" || phaseContract.BehaviorChange is not false
        || phaseContract.Authorization != "STEP_2_PENDING_USER_PUBLICATION_AND_APPROVAL")
        errors.Add("Historical LANE.0.REMEDIATION must preserve its research-only closure snapshot.");

    const string addendum = "docs/PHASE_LANE_0_FUTURE_HELD_REMEDIATION_ADDENDUM.md";
    const string contractPath = "docs/lane_0_future_held_remediation_contract.json";
    RequireFile(addendum, "LANE.0 future-held remediation addendum");
    RequireFile(contractPath, "LANE.0 future-held remediation contract");
    CheckContains("DOCUMENTATION_INDEX.md", addendum, "LANE.0 remediation index entry");
    CheckContains("DOCUMENTATION_INDEX.md", contractPath, "LANE.0 remediation contract index entry");
    foreach (var marker in new[]
    {
        "pendiente de recertificación", "donor.AnchorTime >= target.AnchorTime",
        "PENDING_USER_PUBLICATION_AND_APPROVAL", "40.360/37.080", "288/11"
    }) CheckContains(addendum, marker, "LANE.0 corrective finding");

    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, contractPath)));
        var artifact = document.RootElement;
        var contract = artifact.GetProperty("contract");
        var canonicalOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        var normalized = JsonSerializer.Deserialize<Lane0CorrectiveContractDocument>(
            contract.GetRawText(), canonicalOptions)
            ?? throw new InvalidDataException("Corrective contract cannot be normalized.");
        var canonicalBytes = JsonSerializer.SerializeToUtf8Bytes(normalized, canonicalOptions);
        var canonical = Convert.ToHexString(SHA256.HashData(canonicalBytes));
        if (canonical != "F28F35AA991F3AF20BEEBBEE6AC1D9D3C182E71C62E4044E4DB0792857F8B0A7"
            || artifact.GetProperty("canonicalSha256").GetString() != canonical
            || contract.GetProperty("schemaVersion").GetString() != "lane-0-future-held-remediation.1"
            || contract.GetProperty("approvedOriginalHead").GetString()
                != "12ee8799528d9cf9d64d8d9a9ab4b45ba955db0f"
            || contract.GetProperty("historicalBaselineHead").GetString()
                != "be08f3f2f0191a5c972ad16449a7199dd07f2e3f"
            || contract.GetProperty("requiredEvaluationPublishedHead").GetString()
                != "PENDING_USER_PUBLICATION_AND_APPROVAL"
            || contract.GetProperty("corpusManifestSha256").GetString()
                != "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445"
            || contract.GetProperty("historicalContractSha256").GetString()
                != "62B2F4F67C34E8F57893A3A025D567000E1BB12B6517C13D97FE9EEC3FE0A3C2"
            || contract.GetProperty("repairImplementationSha256").GetString()
                != "A69C084D6631E24B49E66229B5D666E1B8F74BAD306FFE1CAF7BEC9619BBF0B4"
            || contract.GetProperty("correctiveHarnessSha256").GetString()
                != "6B1FE0A10C62C6E2D155A2CF279791B5CD431F2F13A6653F4B498696AC0FB49E"
            || contract.GetProperty("requiredControls").GetArrayLength() < 9
            || contract.GetProperty("outcomeRules").GetProperty("INVALID").GetString() is null)
            errors.Add($"LANE.0 corrective contract identity, scope or canonical hash is invalid "
                + $"(declared={artifact.GetProperty("canonicalSha256").GetString()}, recomputed={canonical}).");
    }
    catch (Exception exception)
    {
        errors.Add($"LANE.0 corrective contract cannot be validated: {exception.Message}");
    }
}

void ValidateLane0FutureHeldHardening(ProjectState value)
{
    var phase = value.Phases.FirstOrDefault(x => x.Id == "LANE.0.HARDENING");
    var phaseContract = value.PhaseContracts.FirstOrDefault(x => x.Id == "LANE.0.HARDENING");
    if (phase?.Status != "COMPLETE") return;
    if (phase.Outcome != "READY_FOR_PUBLICATION_REVIEW" || phase.BehaviorChange is not false
        || phase.Authorization != "STEP_2_NOT_AUTHORIZED"
        || phaseContract?.Kind != "ResearchShadow" || phaseContract.BehaviorChange is not false
        || phaseContract.Authorization != "STEP_2_NOT_AUTHORIZED")
        errors.Add("Historical LANE.0.HARDENING must preserve its research-only closure snapshot.");

    const string report = "docs/PHASE_LANE_0_FUTURE_HELD_HARDENING.md";
    const string contractPath = "docs/lane_0_future_held_hardening_contract.json";
    foreach (var artifact in new[]
    {
        report, contractPath, "docs/lane_0_corrective_comparison_template.csv",
        "docs/lane_0_corrective_g1_operational_template.csv"
    })
    {
        RequireFile(artifact, "LANE.0 hardening artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "LANE.0 hardening index entry");
    }

    foreach (var marker in new[]
    {
        "SUSPENDED / PENDING_RECERTIFICATION", "Paso 2 correctivo C11: **NO AUTORIZADO**",
        "temporal_exclusion", "future_held_identity_exclusion", "admitted + unique_excluded == donors_considered",
        "9a8d28f311dc07dc685cc92a23c76b508323fc25"
    }) CheckContains(report, marker, "LANE.0 hardening boundary");

    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, contractPath)));
        var artifact = document.RootElement;
        var contract = artifact.GetProperty("contract");
        var canonicalOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        var normalized = JsonSerializer.Deserialize<Lane0CorrectiveHardeningContractDocument>(
            contract.GetRawText(), canonicalOptions)
            ?? throw new InvalidDataException("Hardening contract cannot be normalized.");
        var canonical = Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(normalized, canonicalOptions)));
        if (artifact.GetProperty("canonicalSha256").GetString() != canonical
            || contract.GetProperty("schemaVersion").GetString() != "lane-0-future-held-hardening.2"
            || contract.GetProperty("publishedRepairHead").GetString()
                != "9a8d28f311dc07dc685cc92a23c76b508323fc25"
            || contract.GetProperty("historicalLane0Head").GetString()
                != "12ee8799528d9cf9d64d8d9a9ab4b45ba955db0f"
            || contract.GetProperty("parentCorrectiveContractSha256").GetString()
                != "F28F35AA991F3AF20BEEBBEE6AC1D9D3C182E71C62E4044E4DB0792857F8B0A7"
            || contract.GetProperty("requiredPublicationBinding").GetString()
                != "docs/lane_0_corrective_publication_binding.json"
            || contract.GetProperty("corpusManifestSha256").GetString()
                != "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445"
            || contract.GetProperty("memoryLimit").GetString() != "0x400000000"
            || !contract.GetProperty("exclusionCounters").EnumerateArray().Select(x => x.GetString())
                .Contains("both_reasons", StringComparer.Ordinal)
            || contract.GetProperty("donorPartitionInvariant").GetString()
                != "admitted + unique_excluded == donors_considered for every target query; both_reasons is diagnostic and never increments unique_excluded twice."
            || contract.GetProperty("futureResultColumns").GetArrayLength() < 18
            || contract.GetProperty("outcomeRules").GetProperty("INVALID").GetString() is null
            || contract.GetProperty("outcomeRules").GetProperty("BLOCKED").GetString() is null)
            errors.Add($"LANE.0 hardening contract identity, scope or canonical hash is invalid "
                + $"(declared={artifact.GetProperty("canonicalSha256").GetString()}, recomputed={canonical}).");

        using var stateDocument = JsonDocument.Parse(File.ReadAllText(statePath));
        var stateRoot = stateDocument.RootElement;
        var closure = stateRoot.GetProperty("lane0Closure");
        var remediation = stateRoot.GetProperty("lane0FutureHeldRemediation");
        var hardening = stateRoot.GetProperty("lane0FutureHeldHardening");
        if (closure.GetProperty("outcomeScope").GetString() != "HISTORICAL_12EE879_ONLY"
            || closure.GetProperty("certificationStatus").GetString() != "SUSPENDED_PENDING_RECERTIFICATION"
            || remediation.GetProperty("contractHash").GetString()
                != "F28F35AA991F3AF20BEEBBEE6AC1D9D3C182E71C62E4044E4DB0792857F8B0A7"
            || remediation.GetProperty("correctiveEvaluationExecuted").GetBoolean()
            || hardening.GetProperty("contractHash").GetString() != canonical
            || hardening.GetProperty("repairImplementationSnapshot").GetString()
                != contract.GetProperty("repairImplementationSha256").GetString()
            || hardening.GetProperty("correctiveHarnessSnapshot").GetString()
                != contract.GetProperty("correctiveHarnessSha256").GetString()
            || hardening.GetProperty("publicationBinding").GetString() != "ABSENT_BY_DESIGN"
            || hardening.GetProperty("correctiveEvaluationExecuted").GetBoolean()
            || hardening.GetProperty("step2Authorized").GetBoolean())
            errors.Add("PROJECT_STATE does not preserve historical LANE.0, suspended certification and unauthorized Step 2.");
    }
    catch (Exception exception)
    {
        errors.Add($"LANE.0 hardening contract cannot be validated: {exception.Message}");
    }

    foreach (var document in new[] { "README.md", "PROJECT_STATUS.md", "ROADMAP.md",
                 "docs/MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md", "docs/BEHAVIOR_DECISION_AUDIT.md" })
    {
        CheckContains(document, "SUSPENDED", "LANE.0 certification suspension");
        CheckContains(document, "PENDING_RECERTIFICATION", "LANE.0 pending recertification");
    }
}

void ValidateLane0CounterClosure(ProjectState value)
{
    const string addendum = "docs/PHASE_LANE_0_COUNTER_CLOSURE_ADDENDUM.md";
    const string contractPath = "docs/lane_0_future_held_counter_closure_contract.json";
    foreach (var artifact in new[] { addendum, contractPath })
    {
        RequireFile(artifact, "LANE.0 counter closure artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "LANE.0 counter closure index entry");
    }
    foreach (var marker in new[]
    {
        "HISTORICAL FEASIBILITY_DEMONSTRATED", "GLOBAL CERTIFICATION SUSPENDED",
        "PENDING_RECERTIFICATION", "C11 STEP 2 NOT_AUTHORIZED", "NO BEHAVIORAL PROMOTION",
        "UniqueFutureHeldExcluded"
    }) CheckContains(addendum, marker, "LANE.0 counter closure boundary");

    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, contractPath)));
        var artifact = document.RootElement;
        var contractElement = artifact.GetProperty("contract");
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        var contract = JsonSerializer.Deserialize<Lane0CounterClosureContractDocument>(
            contractElement.GetRawText(), options)
            ?? throw new InvalidDataException("Counter closure contract cannot be normalized.");
        var canonical = Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(contract, options)));
        const string expectedHistoricalCanonicalContractIdentity =
            "221D5133D8058FEBEAEA0A13F83058219D900261B033282EE389A82BA20FAAF7";
        const string expectedHistoricalImplementationIdentity =
            "2F938F18D98C6299214C080195633A68B68C688CABF49DC0F4DEFFCA9C1897D9";
        const string expectedHistoricalHarnessIdentity =
            "1EFC2B824FAA6F0798C3FFA679FCA073939D88B2C751612C06BB75C240E96309";
        var currentRepairImplementationIdentity =
            NormalizedTextTreeIdentity(contract.RepairImplementationFiles);
        var dependencies = contract.ReusedDependencies.All(pair =>
            NormalizedTextFileHash(pair.Key) == pair.Value);
        if (contract.SchemaVersion != "lane-0-future-held-counter-closure.3"
            || contract.AuditedBaselineHead != "c471d10ed48e42ab33b8a981ef22c6eb26774f3d"
            || contract.ParentHardeningContractSha256
                != "E0BCDA19B3E05E5ECA3EE8FCC7380C4AC74ACBDDE3241D77CD2F1B6EA6274F35"
            || contract.RequiredPublicationBinding
                != "docs/lane_0_counter_closure_publication_binding.json"
            || contract.CorpusManifestSha256
                != "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445"
            || contract.MemoryLimit != "0x400000000"
            || !dependencies
            || !contract.CounterSemantics.Contains("unique_future_held_excluded is their union",
                StringComparison.Ordinal)
            || !contract.RequiredControls.Contains("corrective-execution-denied", StringComparer.Ordinal))
            errors.Add($"LANE.0 counter closure identity or boundary is invalid "
                + $"(declared={artifact.GetProperty("canonicalSha256").GetString()}, canonical={canonical}, "
                + $"implementation={currentRepairImplementationIdentity}).");

        using var stateDocument = JsonDocument.Parse(File.ReadAllText(statePath));
        var state = stateDocument.RootElement.GetProperty("lane0FutureHeldCounterClosure");
        errors.AddRange(DocumentationStateGuard.ValidateHistoricalCounterClosureIdentities(
            expectedHistoricalCanonicalContractIdentity,
            canonical,
            artifact.GetProperty("canonicalSha256").GetString(),
            expectedHistoricalImplementationIdentity,
            currentRepairImplementationIdentity,
            contract.RepairImplementationSha256,
            state.GetProperty("repairImplementationSnapshot").GetString() ?? string.Empty,
            expectedHistoricalHarnessIdentity,
            contract.CorrectiveHarnessSha256,
            state.GetProperty("correctiveHarnessSnapshot").GetString() ?? string.Empty,
            state.GetProperty("contractHash").GetString() ?? string.Empty));

        // The historical harness includes DocConsistency itself. It is a frozen snapshot, so
        // legitimate post-closure guard changes must not be compared with the current tree.
        if (state.GetProperty("parentContractHash").GetString()
                != "E0BCDA19B3E05E5ECA3EE8FCC7380C4AC74ACBDDE3241D77CD2F1B6EA6274F35"
            || state.GetProperty("publicationBinding").GetString() != "ABSENT_BY_DESIGN"
            || state.GetProperty("correctiveEvaluationExecuted").GetBoolean()
            || state.GetProperty("step2Authorized").GetBoolean()
            || state.GetProperty("behaviorChange").GetBoolean())
            errors.Add("PROJECT_STATE does not preserve the counter closure identities and unauthorized C11 boundary.");
    }
    catch (Exception exception)
    {
        errors.Add($"LANE.0 counter closure contract cannot be validated: {exception.Message}");
    }
}

void ValidateLane0CorrectivePostAttemptForensics(ProjectState value)
{
    const string report = "docs/PHASE_LANE_0_CORRECTIVE_POST_ATTEMPT_FORENSIC_ADDENDUM.md";
    var phase = value.Phases.FirstOrDefault(x => x.Id == lane0PostAttemptPhase);
    var phaseContract = value.PhaseContracts.FirstOrDefault(x => x.Id == lane0PostAttemptPhase);
    var forensic = value.Lane0CorrectivePostAttemptForensics;

    if (phase?.Status != "COMPLETE"
        || phase.Outcome != "POST_ATTEMPT_FORENSIC_DOCUMENTED"
        || phase.BehaviorChange is not false
        || phase.Authorization != "NO_SUCCESSOR_AUTHORIZED"
        || phase.Report != report)
        errors.Add("LANE.0 post-attempt forensic phase does not preserve its non-behavioral closure semantics.");
    if (phaseContract?.Kind != "ResearchShadow"
        || phaseContract.BehaviorChange is not false
        || phaseContract.Authorization != "NO_SUCCESSOR_AUTHORIZED")
        errors.Add("LANE.0 post-attempt forensic phase contract must remain research-only with no successor authority.");

    errors.AddRange(DocumentationStateGuard.ValidatePostAttemptCurrentState(
        value.CurrentPhase, forensic is not null, phase is not null, forensic?.Attempt, forensic?.Retry,
        forensic?.Successor));

    if (value.CurrentPhase is not (lane0PostAttemptPhase or lane0SuccessorPhase or lane0SuccessorAuditPhase or lane0SuccessorPhase2 or lane0SuccessorPhase2Audit or lane0IntegrationPreregistration)
        || value.NextRecommendedPhase is not null
        || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED"
        || value.NextBehavioralPhase is not null
        || value.BehaviorChange)
        errors.Add("Canonical current state must close post-attempt forensics without selecting or authorizing a successor.");

    if (forensic is null) return;
    if (forensic.Status != "POST_ATTEMPT_FORENSIC_DOCUMENTED"
        || forensic.PublicHead != "243c43a589512a12a69b9a4c0d68d1c4fd54a8ca"
        || forensic.Attempt != "AUTHORIZED_CORRECTIVE_ATTEMPT_CONSUMED"
        || forensic.ScientificEvaluator != "INVALID"
        || forensic.ScientificResult != "NOT_PUBLISHABLE"
        || forensic.ForensicFinding != "SCI-01_SCIENTIFIC_REFERENCE_IDENTITY_MISMATCH"
        || forensic.ForensicVerdict != "SUFFICIENT_CAUSE_ESTABLISHED_EXCLUSIVE_CAUSE_NOT_PROVABLE"
        || forensic.GlobalCertification != "SUSPENDED_PENDING_RECERTIFICATION"
        || !forensic.HistoricalFeasibilityRetained
        || forensic.Promotion != "NONE"
        || forensic.Retry != "PROHIBITED"
        || forensic.Successor != "NOT_YET_PREREGISTERED_NOT_AUTHORIZED"
        || forensic.Report != report)
        errors.Add("LANE.0 post-attempt forensic current-state facts are incomplete or contradictory.");

    RequireFile(report, "LANE.0 post-attempt forensic report");
    CheckContains("DOCUMENTATION_INDEX.md", report, "LANE.0 post-attempt forensic index entry");
}

void ValidateLane0CorrectiveSuccessorPhase1(ProjectState value)
{
    const string report = "docs/PHASE_LANE_0_CORRECTIVE_SUCCESSOR_PREREGISTRATION.md";
    const string contractPath = "docs/lane_0_corrective_successor_preregistration_contract.json";
    const string canonicalIdentity =
        "66B0D27535B8EEE5B466DDAB1209DD0C548B982C24913E1993E611D57E1BE505";
    const string manifestIdentity =
        "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    const string correctedA =
        "E73B098AA0CB6FA3BECE45E9551EF225412F5E01F6398184093A40B944F0AAE0";
    const string historicalB =
        "E73B098A4D4C99D716D2C53B3EA3A4BFD1A75059881F30DB416631B5EDB972D4";
    const string second =
        "20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788";

    var phase = value.Phases.FirstOrDefault(x => x.Id == lane0SuccessorPhase);
    var phaseContract = value.PhaseContracts.FirstOrDefault(x => x.Id == lane0SuccessorPhase);
    var successor = value.Lane0CorrectiveSuccessorPhase1;
    if (phase?.Status != "COMPLETE" || phase.Outcome != "READY_FOR_HUMAN_REVIEW"
        || phase.BehaviorChange is not false || phase.Authorization != "NO_C11_AUTHORIZATION"
        || phase.Report != report || phaseContract?.Kind != "ResearchShadow"
        || phaseContract.BehaviorChange is not false
        || phaseContract.Authorization != "NO_C11_AUTHORIZATION")
        errors.Add("LANE.0 successor Phase 1 must remain research-only and have no C11 authorization.");
    if (value.CurrentPhase is not (lane0SuccessorPhase or lane0SuccessorAuditPhase or lane0SuccessorPhase2 or lane0SuccessorPhase2Audit or lane0IntegrationPreregistration)
        || value.NextRecommendedPhase is not null
        || value.NextBehavioralPhase is not null || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED"
        || value.BehaviorChange)
        errors.Add("LANE.0 successor Phase 1 must stop at human review with no actionable successor.");
    if (successor is null
        || successor.Status != "READY_FOR_HUMAN_REVIEW"
        || successor.SchemaVersion != "lane-0-corrective-successor-preregistration.1"
        || successor.Contract != contractPath || successor.ContractHash != canonicalIdentity
        || successor.Report != report
        || successor.ScientificRemediation != "SCIENTIFIC_REFERENCE_REMEDIATION_ONLY"
        || successor.Authorization != "NO_C11_AUTHORIZATION"
        || successor.BindingPresent || successor.ReceiptPresent || successor.C11Accessed
        || successor.BehaviorChange || successor.RngChange || successor.DefaultChange)
        errors.Add("PROJECT_STATE does not preserve the static, non-authorizing successor Phase 1 boundary.");

    foreach (var artifact in new[] { report, contractPath })
    {
        RequireFile(artifact, "LANE.0 successor Phase 1 artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "LANE.0 successor Phase 1 index entry");
    }

    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, contractPath)));
        var artifact = document.RootElement;
        var contract = artifact.GetProperty("contract");
        var canonical = CanonicalJsonHash(contract);
        var rule = contract.GetProperty("historicalG1CaseRule");
        var distribution = rule.GetProperty("distribution").EnumerateArray().ToArray();
        var ids = distribution.Select(x => x.GetProperty("chartId").GetString()).ToArray();
        var authority = contract.GetProperty("authorityNamespace");
        var boundary = contract.GetProperty("phaseBoundary");
        var preserved = contract.GetProperty("preservedScientificBoundaries");

        if (canonical != canonicalIdentity
            || artifact.GetProperty("canonicalSha256").GetString() != canonicalIdentity
            || contract.GetProperty("schemaVersion").GetString()
                != "lane-0-corrective-successor-preregistration.1"
            || contract.GetProperty("phaseId").GetString() != lane0SuccessorPhase
            || contract.GetProperty("authorization").GetString() != "NO_C11_AUTHORIZATION"
            || contract.GetProperty("scientificRemediation").GetString()
                != "SCIENTIFIC_REFERENCE_REMEDIATION_ONLY"
            || contract.GetProperty("expectedC11ManifestSha256").GetString() != manifestIdentity
            || distribution.Length != 2 || ids.Distinct(StringComparer.Ordinal).Count() != 2
            || !ids.Contains(correctedA, StringComparer.Ordinal)
            || !ids.Contains(second, StringComparer.Ordinal)
            || ids.Contains(historicalB, StringComparer.Ordinal)
            || rule.GetProperty("expectedTotalHistoricalOperationalCount").GetInt32() != 11
            || distribution.Sum(x => x.GetProperty("historicallySupportedCount").GetInt32()) != 11
            || distribution.Single(x => x.GetProperty("chartId").GetString() == second)
                .GetProperty("historicallySupportedCount").GetInt32() != 9
            || distribution.Single(x => x.GetProperty("chartId").GetString() == correctedA)
                .GetProperty("historicallySupportedCount").GetInt32() != 2
            || distribution.Any(x => x.GetProperty("keymode").GetInt32() != 7)
            || preserved.GetProperty("historicalDistribution").GetString() != "9+2"
            || preserved.GetProperty("zeroRng").GetBoolean() is not true
            || preserved.GetProperty("behaviorChange").GetBoolean()
            || preserved.GetProperty("defaultChange").GetBoolean()
            || authority.GetProperty("historicalBindingPath").GetString()
                == authority.GetProperty("successorBindingPath").GetString()
            || authority.GetProperty("historicalReceiptPath").GetString()
                == authority.GetProperty("successorReceiptPath").GetString()
            || authority.GetProperty("successorBindingPath").GetString()
                != "docs/lane_0_corrective_successor_publication_binding.json"
            || authority.GetProperty("successorReceiptPath").GetString()
                != ".artifacts/lane_0_corrective_successor.attempt.json"
            || authority.GetProperty("bindingPresent").GetBoolean()
            || authority.GetProperty("receiptPresent").GetBoolean()
            || authority.GetProperty("humanAuthorizationGranted").GetBoolean()
            || authority.GetProperty("corpusAccessAuthorized").GetBoolean()
            || boundary.GetProperty("bindingCreationAuthorized").GetBoolean()
            || boundary.GetProperty("receiptCreationAuthorized").GetBoolean()
            || boundary.GetProperty("c11AdapterPresent").GetBoolean()
            || boundary.GetProperty("corpusRootAccepted").GetBoolean()
            || boundary.GetProperty("osuDiscoveryAuthorized").GetBoolean()
            || boundary.GetProperty("osuParsingAuthorized").GetBoolean()
            || boundary.GetProperty("executionLauncherPresent").GetBoolean()
            || boundary.GetProperty("officialRunnerPresent").GetBoolean())
            errors.Add("LANE.0 successor preregistration identity, references, distribution or authority boundary is invalid.");

        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "docs", "g1_gate_runtime_manifest.json")));
        var manifestArtifact = manifestDocument.RootElement;
        var charts = manifestArtifact.GetProperty("manifest").GetProperty("charts")
            .EnumerateArray().ToArray();
        if (manifestArtifact.GetProperty("canonicalSha256").GetString() != manifestIdentity)
            errors.Add("Successor preregistration does not join against the trusted public manifest identity.");
        foreach (var reference in distribution)
        {
            var id = reference.GetProperty("chartId").GetString();
            var matches = charts.Where(x => x.GetProperty("chartId").GetString() == id).ToArray();
            if (matches.Length != 1
                || matches[0].GetProperty("family").GetString()
                    != reference.GetProperty("expectedManifestFamily").GetString()
                || matches[0].GetProperty("keyCount").GetInt32()
                    != reference.GetProperty("keymode").GetInt32())
                errors.Add($"Successor reference does not match public manifest metadata: {id}.");
        }

        using var historical = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "docs", "lane_0_corrective_evaluation_preregistration_contract.json")));
        var historicalIds = historical.RootElement.GetProperty("contract")
            .GetProperty("historicalG1OperationalReferences").EnumerateArray()
            .Select(x => x.GetProperty("chartId").GetString()).ToArray();
        if (!historicalIds.Contains(historicalB, StringComparer.Ordinal))
            errors.Add("Historical corrective preregistration no longer preserves erroneous identity B.");

        if (File.Exists(Path.Combine(root, authority.GetProperty("successorBindingPath").GetString()!))
            || File.Exists(Path.Combine(root, authority.GetProperty("successorReceiptPath").GetString()!)))
            errors.Add("Successor binding or receipt exists before future authorization.");
    }
    catch (Exception exception)
    {
        errors.Add($"LANE.0 successor Phase 1 cannot be validated: {exception.Message}");
    }

    foreach (var marker in new[]
    {
        "READY FOR HUMAN REVIEW / NO C11 AUTHORIZATION", "SCIENTIFIC_REFERENCE_REMEDIATION_ONLY",
        correctedA, historicalB, "9 + 2 = 11", "no C11 adapter"
    }) CheckContains(report, marker, "LANE.0 successor Phase 1 boundary");
}

void ValidateLane0CorrectiveSuccessorPhase1Audit(ProjectState value)
{
    const string report = "docs/PHASE_LANE_0_CORRECTIVE_SUCCESSOR_PHASE1_AUDIT.md";
    const string preregistrationContract =
        "docs/lane_0_corrective_successor_preregistration_contract.json";
    const string publicationHead = "6a7c44dab91ced9abd35085e0a7930ace054715e";
    const string contractIdentity =
        "66B0D27535B8EEE5B466DDAB1209DD0C548B982C24913E1993E611D57E1BE505";
    const string historicalB =
        "E73B098A4D4C99D716D2C53B3EA3A4BFD1A75059881F30DB416631B5EDB972D4";
    const string correctedA =
        "E73B098AA0CB6FA3BECE45E9551EF225412F5E01F6398184093A40B944F0AAE0";
    const string requiredHardening = "VERIFIED_MANIFEST_OBJECT_COUPLING";

    var phase = value.Phases.FirstOrDefault(x => x.Id == lane0SuccessorAuditPhase);
    var phaseContract = value.PhaseContracts.FirstOrDefault(x => x.Id == lane0SuccessorAuditPhase);
    var preregistration = value.Phases.FirstOrDefault(x => x.Id == lane0SuccessorPhase);
    var audit = value.Lane0CorrectiveSuccessorPhase1Audit;

    if (phase?.Status != "COMPLETE"
        || phase.Outcome != "AUDIT_CLEAN_WITH_NON_BLOCKING_OBSERVATIONS"
        || phase.BehaviorChange is not false
        || phase.Authorization != "NO_C11_AUTHORIZATION"
        || phase.Report != report
        || phaseContract?.Kind != "ResearchShadow"
        || phaseContract.BehaviorChange is not false
        || phaseContract.Authorization != "NO_C11_AUTHORIZATION")
        errors.Add("LANE.0 successor Phase 1 audit closure must remain research-only and non-authorizing.");

    if (value.CurrentPhase is not (lane0SuccessorAuditPhase or lane0SuccessorPhase2 or lane0SuccessorPhase2Audit or lane0IntegrationPreregistration)
        || value.NextRecommendedPhase is not null
        || value.NextBehavioralPhase is not null
        || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED"
        || value.BehaviorChange)
        errors.Add("LANE.0 successor Phase 1 audit must be the non-behavioral current closure without an active Phase 2.");

    if (preregistration?.Status != "COMPLETE"
        || preregistration.Outcome != "READY_FOR_HUMAN_REVIEW"
        || preregistration.BehaviorChange is not false
        || preregistration.Authorization != "NO_C11_AUTHORIZATION")
        errors.Add("Historical successor preregistration semantics changed after publication.");

    if (audit is null
        || audit.Status != "COMPLETE"
        || audit.Outcome != "AUDIT_CLEAN_WITH_NON_BLOCKING_OBSERVATIONS"
        || audit.PublicationHead != publicationHead
        || audit.PreregistrationContractHash != contractIdentity
        || audit.IndependentAudit != "COMPLETE"
        || audit.ScientificValidity != "RETAINED"
        || audit.Authorization != "NO_C11_AUTHORIZATION"
        || audit.BindingPresent || audit.ReceiptPresent || audit.C11Accessed
        || audit.Phase2Status != "NOT_STARTED"
        || audit.Phase2Authorization != "NOT_AUTHORIZED"
        || audit.Phase2RequiredHardening != requiredHardening
        || audit.BehaviorChange || audit.RngChange || audit.DefaultChange
        || audit.Report != report)
        errors.Add("PROJECT_STATE does not preserve the exact Phase 1 audit and future Phase 2 boundary.");

    RequireFile(report, "LANE.0 successor Phase 1 audit report");
    CheckContains("DOCUMENTATION_INDEX.md", report, "LANE.0 successor Phase 1 audit index entry");

    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            preregistrationContract.Replace('/', Path.DirectorySeparatorChar))));
        var artifact = document.RootElement;
        if (CanonicalJsonHash(artifact.GetProperty("contract")) != contractIdentity
            || artifact.GetProperty("canonicalSha256").GetString() != contractIdentity)
            errors.Add("Published successor preregistration contract changed during Phase 1 audit closure.");
    }
    catch (Exception exception)
    {
        errors.Add($"Published successor preregistration contract cannot be validated: {exception.Message}");
    }

    foreach (var marker in new[]
    {
        publicationHead, contractIdentity, "PHASE 1 AUDIT CLEAN WITH NON-BLOCKING OBSERVATIONS",
        "scientific validity", "public-manifest semantic join", "historical B", "corrected successor A",
        "9+2=11", "No C11", "No binding", "No receipt", requiredHardening,
        "NOT_STARTED / NOT_AUTHORIZED"
    }) CheckContains(report, marker, "LANE.0 successor Phase 1 audit closure");

    foreach (var marker in new[] { historicalB, correctedA })
        CheckContains(preregistrationContract, marker, "LANE.0 successor preregistration historical/reference identity");

    foreach (var forbidden in new[]
    {
        "docs/lane_0_corrective_successor_publication_binding.json",
        ".artifacts/lane_0_corrective_successor.attempt.json"
    })
        if (File.Exists(Path.Combine(root, forbidden.Replace('/', Path.DirectorySeparatorChar))))
            errors.Add($"Successor authority artifact exists before Phase 2 authorization: {forbidden}.");

    foreach (var document in new[] { "README.md", "PROJECT_STATUS.md" })
        if (File.ReadAllText(Path.Combine(root, document)).Contains("aguarda publicación", StringComparison.OrdinalIgnoreCase))
            errors.Add($"Stale live successor publication wording remains: {document}.");
}

void ValidateLane0CorrectiveSuccessorPhase2(ProjectState value)
{
    const string report = "docs/PHASE_LANE_0_CORRECTIVE_SUCCESSOR_EXECUTION_PREPARATION.md";
    const string contractPath = "docs/lane_0_corrective_successor_execution_preparation_contract.json";
    const string contractIdentity =
        "11F55A6770BA78F008A6690C88561C714CAF45BA15B96E472C0C80B219C4D5B6";
    const string phase1Identity =
        "66B0D27535B8EEE5B466DDAB1209DD0C548B982C24913E1993E611D57E1BE505";
    const string manifestIdentity =
        "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    const string scientificIdentity =
        "8F9AE545027F0F1364E3378324646C7F7711CDF7EFFC8CC879660644A3D905D0";
    const string harnessIdentity =
        "15DC978C24119E9832DD70C0C61F5BFB1BBAF11520A107A7728220009B793A61";
    const string packageIdentity =
        "DFF48221A6FB087367B1C6B1062B7355806C53668C6876A4BB4EC59983232D38";
    const string dependencyIdentity =
        "D5931A815701C0FAB854B878373A8ED03F3E29BDEA17C98A428C7AF6B0F7023B";
    const string publicationHead = "9e239f8d74d3624d54568af74ff1e420e48c7083";

    var phase = value.Phases.FirstOrDefault(x => x.Id == lane0SuccessorPhase2);
    var phaseContract = value.PhaseContracts.FirstOrDefault(x => x.Id == lane0SuccessorPhase2);
    var phase2 = value.Lane0CorrectiveSuccessorPhase2;
    if (phase?.Status != "COMPLETE"
        || phase.Outcome != "READY_FOR_INDEPENDENT_EXECUTION_AUDIT"
        || phase.BehaviorChange is not false
        || phase.Authorization != "NO_C11_AUTHORIZATION"
        || phase.Report != report
        || phaseContract?.Kind != "ResearchShadow"
        || phaseContract.BehaviorChange is not false
        || phaseContract.Authorization != "NO_C11_AUTHORIZATION")
        errors.Add("LANE.0 successor Phase 2 must remain research-only execution preparation.");
    if (value.BehaviorChange)
        errors.Add("LANE.0 successor Phase 2 must remain behavior-neutral.");
    if (phase2 is null
        || phase2.Status != "COMPLETE"
        || phase2.Outcome != "READY_FOR_INDEPENDENT_EXECUTION_AUDIT"
        || phase2.BaselinePublicHead != publicationHead
        || phase2.SchemaVersion != "lane-0-corrective-successor-execution-preparation.1"
        || phase2.Contract != contractPath || phase2.ContractHash != contractIdentity
        || phase2.Report != report
        || phase2.ManifestCoupling != "VERIFIED_MANIFEST_OBJECT_COUPLING"
        || phase2.ManifestHash != manifestIdentity
        || phase2.ScientificImplementationHash != scientificIdentity
        || phase2.ExecutionHarnessHash != harnessIdentity
        || phase2.PackageVerifierHash != packageIdentity
        || phase2.FrozenDependenciesHash != dependencyIdentity
        || phase2.Authorization != "NO_C11_AUTHORIZATION"
        || phase2.BindingPresent || phase2.ReceiptPresent || phase2.C11Accessed
        || phase2.ExecutionAuthorized || phase2.ScientificEvaluationExecuted
        || phase2.BehaviorChange || phase2.RngChange || phase2.DefaultChange
        || phase2.NextRequiredAction != "INDEPENDENT_EXECUTION_AUDIT")
        errors.Add("PROJECT_STATE does not preserve the exact non-authorizing Phase 2 preparation boundary.");

    foreach (var artifact in new[] { report, contractPath })
    {
        RequireFile(artifact, "LANE.0 successor Phase 2 artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact, "LANE.0 successor Phase 2 index entry");
    }

    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            contractPath.Replace('/', Path.DirectorySeparatorChar))));
        var artifact = document.RootElement;
        var contract = artifact.GetProperty("contract");
        var scientificFiles = contract.GetProperty("scientificImplementationFiles")
            .EnumerateArray().Select(x => x.GetString()!).ToArray();
        var harnessFiles = contract.GetProperty("executionHarnessFiles")
            .EnumerateArray().Select(x => x.GetString()!).ToArray();
        var packageFiles = contract.GetProperty("packageVerifierFiles")
            .EnumerateArray().Select(x => x.GetString()!).ToArray();
        var dependencyFiles = contract.GetProperty("frozenDependencyFiles")
            .EnumerateArray().Select(x => x.GetString()!).ToArray();
        var references = contract.GetProperty("references").EnumerateArray().ToArray();
        var authority = contract.GetProperty("authorityNamespace");
        var boundary = contract.GetProperty("phaseBoundary");
        var order = contract.GetProperty("preCorpusOrder").EnumerateArray()
            .Select(x => x.GetString()).ToArray();
        var packageArtifacts = contract.GetProperty("packageArtifacts").EnumerateArray()
            .Select(x => x.GetString()).ToArray();
        var expectedArtifacts = new[]
        {
            "chart_family_keymode_results.csv", "execution_identity.json",
            "g1_historical_operational_cases.csv", "g1_state_transitions.csv", "integrity.json",
            "scientific_summary.json", "sha256sums.txt", "successor_references.json"
        };
        var expectedScientificFiles = new[]
        {
            "src/ManiaAddNotesLab.Core/Lane0CorrectiveSuccessorReferenceValidator.cs",
            "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorEvaluator.cs"
        };
        var expectedHarnessFiles = new[]
        {
            "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorExecutionPreparation.cs"
        };
        var expectedPackageFiles = new[]
        {
            "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorPackage.cs"
        };
        var expectedDependencyFiles = new[]
        {
            "src/ManiaAddNotesLab.Core/ChordCompletionResearch.cs",
            "src/ManiaAddNotesLab.Core/FrozenC11ManifestResearch.cs",
            "src/ManiaAddNotesLab.Core/InteriorRelationFeasibilityResearch.cs",
            "src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
            "src/ManiaAddNotesLab.Core/Model.cs",
            "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveEvaluationRunner.cs",
            "tools/ManiaAddNotesLab.Experiments/Lane0FeasibilityRunner.cs"
        };
        var actualContractIdentity = CanonicalJsonHash(contract);
        var actualScientificIdentity = NormalizedTextTreeIdentity(scientificFiles);
        var actualHarnessIdentity = NormalizedTextTreeIdentity(harnessFiles);
        var actualPackageIdentity = NormalizedTextTreeIdentity(packageFiles);
        var actualDependencyIdentity = NormalizedTextTreeIdentity(dependencyFiles);

        if (actualContractIdentity != contractIdentity
            || artifact.GetProperty("canonicalSha256").GetString() != contractIdentity
            || contract.GetProperty("schemaVersion").GetString()
                != "lane-0-corrective-successor-execution-preparation.1"
            || contract.GetProperty("phaseId").GetString() != lane0SuccessorPhase2
            || contract.GetProperty("baselinePublicHead").GetString() != publicationHead
            || contract.GetProperty("phase1ContractSha256").GetString() != phase1Identity
            || contract.GetProperty("verifiedManifestSha256").GetString() != manifestIdentity
            || contract.GetProperty("manifestAuthorityModel").GetString()
                != "VERIFIED_MANIFEST_OBJECT_COUPLING"
            || contract.GetProperty("authorization").GetString() != "NO_C11_AUTHORIZATION"
            || contract.GetProperty("outcome").GetString()
                != "READY_FOR_INDEPENDENT_EXECUTION_AUDIT"
            || contract.GetProperty("historicalInvalidSpringB").GetString()
                != "E73B098A4D4C99D716D2C53B3EA3A4BFD1A75059881F30DB416631B5EDB972D4"
            || contract.GetProperty("historicalDistribution").GetString() != "9+2=11"
            || contract.GetProperty("successorScientificSchema").GetString()
                != "lane-0-corrective-successor-evaluation.1"
            || references.Length != 2
            || references.Sum(x => x.GetProperty("historicallySupportedCount").GetInt32()) != 11
            || references.Any(x => x.GetProperty("keymode").GetInt32() != 7)
            || !references.Any(x => x.GetProperty("chartId").GetString()
                == "E73B098AA0CB6FA3BECE45E9551EF225412F5E01F6398184093A40B944F0AAE0")
            || !references.Any(x => x.GetProperty("chartId").GetString()
                == "20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788")
            || contract.GetProperty("scientificImplementationSha256").GetString() != scientificIdentity
            || contract.GetProperty("executionHarnessSha256").GetString() != harnessIdentity
            || contract.GetProperty("packageVerifierSha256").GetString() != packageIdentity
            || contract.GetProperty("frozenDependenciesSha256").GetString() != dependencyIdentity
            || !scientificFiles.SequenceEqual(expectedScientificFiles)
            || !harnessFiles.SequenceEqual(expectedHarnessFiles)
            || !packageFiles.SequenceEqual(expectedPackageFiles)
            || !dependencyFiles.SequenceEqual(expectedDependencyFiles)
            || actualScientificIdentity != scientificIdentity
            || actualHarnessIdentity != harnessIdentity
            || actualPackageIdentity != packageIdentity
            || actualDependencyIdentity != dependencyIdentity
            || scientificFiles.Intersect(harnessFiles, StringComparer.Ordinal).Any()
            || scientificFiles.Intersect(packageFiles, StringComparer.Ordinal).Any()
            || harnessFiles.Intersect(packageFiles, StringComparer.Ordinal).Any()
            || scientificFiles.Concat(harnessFiles).Concat(packageFiles)
                .Any(x => x.StartsWith("tests/", StringComparison.Ordinal)
                    || x.StartsWith("docs/", StringComparison.Ordinal))
            || !packageArtifacts.SequenceEqual(expectedArtifacts)
            || authority.GetProperty("bindingPresent").GetBoolean()
            || authority.GetProperty("receiptPresent").GetBoolean()
            || authority.GetProperty("executionAuthorized").GetBoolean()
            || authority.GetProperty("scientificEvaluationExecuted").GetBoolean()
            || boundary.EnumerateObject().Where(x => x.Name != "publicationRequiresIndependentAudit")
                .Any(x => x.Value.GetBoolean())
            || !boundary.GetProperty("publicationRequiresIndependentAudit").GetBoolean()
            || Array.IndexOf(order, "DURABLE_ONE_SHOT_RECEIPT")
                >= Array.IndexOf(order, "CORPUS_ROOT_CANONICALIZATION_AND_SEPARATION")
            || Array.IndexOf(order, "CORPUS_ROOT_CANONICALIZATION_AND_SEPARATION")
                >= Array.IndexOf(order, "CORPUS_ACCESS")
            || contract.GetProperty("authorityNamespace").GetProperty("receiptPathDerivation").GetString()
                != "AUTHORIZATION_ROOT_PLUS_FROZEN_RELATIVE_PATH"
            || contract.GetProperty("authorityNamespace").GetProperty("outputPathDerivation").GetString()
                != "EXECUTION_ROOT_PLUS_FROZEN_RELATIVE_PATH"
            || !contract.GetProperty("rootSeparation").GetProperty("ancestorOrDescendantRootsRejected").GetBoolean()
            || contract.GetProperty("bindingCanonicalization").GetProperty("whitespace").GetString()
                != "COMPACT"
            || !contract.GetProperty("bindingCanonicalization")
                .GetProperty("receiptHashesExactVerifiedCanonicalBytes").GetBoolean()
            || contract.GetProperty("scientificOutcomePolicy").GetProperty("INVALID").GetString()
                != "INVALID"
            || contract.GetProperty("scientificOutcomePolicy")
                .GetProperty("BLOCKED_AFTER_RECEIPT").GetString() != "INVALID")
            errors.Add("LANE.0 successor Phase 2 contract identity, science, packaging, ordering or authority boundary drifted. "
                + $"Actual contract/science/harness/package/dependencies: {actualContractIdentity}/"
                + $"{actualScientificIdentity}/{actualHarnessIdentity}/{actualPackageIdentity}/{actualDependencyIdentity}.");
    }
    catch (Exception exception)
    {
        errors.Add($"LANE.0 successor Phase 2 contract cannot be validated: {exception.Message}");
    }

    CheckContains("src/ManiaAddNotesLab.Core/FrozenC11ManifestResearch.cs",
        "internal VerifiedFrozenC11Manifest", "non-public verified-manifest construction");
    CheckContains("src/ManiaAddNotesLab.Core/Lane0CorrectiveSuccessorReferenceValidator.cs",
        "VerifiedFrozenC11Manifest manifest", "execution-facing manifest-object coupling");
    CheckContains("tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorExecutionPreparation.cs",
        "FileMode.CreateNew", "successor one-shot receipt exclusive creation");
    CheckContains("tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorExecutionPreparation.cs",
        "Path.Combine(source, CanonicalReceiptRelativePath)", "authority-root-derived successor receipt");
    CheckContains("tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorExecutionPreparation.cs",
        "Path.Combine(execution, CanonicalOutputRelativePath)", "execution-root-derived successor output");
    CheckContains("tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorEvaluator.cs",
        "lane-0-corrective-successor-evaluation.1", "successor scientific schema");
    var successorEvaluator = File.ReadAllText(Path.Combine(root, "tools",
        "ManiaAddNotesLab.Experiments", "Lane0CorrectiveSuccessorEvaluator.cs"));
    if (successorEvaluator.Contains("Lane0CorrectiveEvaluationRunner.Evaluate(", StringComparison.Ordinal))
        errors.Add("Successor evaluator calls the historical Evaluate entrypoint.");

    foreach (var forbidden in new[]
    {
        "docs/lane_0_corrective_successor_publication_binding.json",
        ".artifacts/lane_0_corrective_successor.attempt.json"
    })
        if (File.Exists(Path.Combine(root, forbidden.Replace('/', Path.DirectorySeparatorChar))))
            errors.Add($"Real successor authority artifact exists during Phase 2 preparation: {forbidden}.");
}

void ValidateLane0CorrectiveSuccessorPhase2Audit(ProjectState value)
{
    const string report = "docs/PHASE_LANE_0_CORRECTIVE_SUCCESSOR_PHASE2_AUDIT.md";
    const string auditedHead = "6b1a1730c23d3429a533aa95ae251ecfb1eef18a";
    const string implementationCommit = "60776c0b9acbfc5ded89397c36ffdbe609fb720d";
    const string contractIdentity = "11F55A6770BA78F008A6690C88561C714CAF45BA15B96E472C0C80B219C4D5B6";
    const string scientificIdentity = "8F9AE545027F0F1364E3378324646C7F7711CDF7EFFC8CC879660644A3D905D0";
    const string harnessIdentity = "15DC978C24119E9832DD70C0C61F5BFB1BBAF11520A107A7728220009B793A61";
    const string packageIdentity = "DFF48221A6FB087367B1C6B1062B7355806C53668C6876A4BB4EC59983232D38";
    const string dependencyIdentity = "D5931A815701C0FAB854B878373A8ED03F3E29BDEA17C98A428C7AF6B0F7023B";
    var phase = value.Phases.FirstOrDefault(x => x.Id == lane0SuccessorPhase2Audit);
    var phaseContract = value.PhaseContracts.FirstOrDefault(x => x.Id == lane0SuccessorPhase2Audit);
    var audit = value.Lane0CorrectiveSuccessorPhase2Audit;
    if (phase?.Status != "COMPLETE"
        || phase.Outcome != "AUDIT_CLEAN_WITH_NON_BLOCKING_OBSERVATIONS"
        || phase.BehaviorChange is not false
        || phase.Authorization != "NO_C11_AUTHORIZATION"
        || phase.Report != report
        || phaseContract?.Kind != "ResearchShadow"
        || phaseContract.BehaviorChange is not false
        || phaseContract.Authorization != "NO_C11_AUTHORIZATION")
        errors.Add("LANE.0 successor Phase 2 audit closure phase drifted.");
    if (value.CurrentPhase is not (lane0SuccessorPhase2Audit or lane0IntegrationPreregistration)
        || value.NextRecommendedPhase is not null
        || value.NextBehavioralPhase is not null
        || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED"
        || value.BehaviorChange)
        errors.Add("Phase 2 audit closure must remain the current non-authorizing human-review stop.");
    if (audit is null
        || audit.Status != "COMPLETE"
        || audit.Outcome != "AUDIT_CLEAN_WITH_NON_BLOCKING_OBSERVATIONS"
        || audit.AuditedPublicHead != auditedHead
        || audit.ImplementationCommit != implementationCommit
        || audit.Phase2ContractHash != contractIdentity
        || audit.ScientificImplementationHash != scientificIdentity
        || audit.ExecutionHarnessHash != harnessIdentity
        || audit.PackageVerifierHash != packageIdentity
        || audit.FrozenDependenciesHash != dependencyIdentity
        || audit.IndependentAudit != "COMPLETE"
        || audit.R2Closure != "R2_01_THROUGH_R2_08_CLOSED"
        || audit.CurrentPackageVerifier != "SUFFICIENT_FOR_PHASE2_IN_PROCESS_DETERMINISM"
        || audit.SemanticVerifierRequirement != "REQUIRED_BEFORE_BINDING"
        || audit.IntegrationStatus != "NOT_STARTED"
        || audit.IntegrationAuthorization != "NOT_AUTHORIZED"
        || audit.Authorization != "NO_C11_AUTHORIZATION"
        || audit.BindingPresent || audit.ReceiptPresent || audit.C11Accessed
        || audit.ExecutionAuthorized || audit.ScientificEvaluationExecuted
        || audit.BehaviorChange || audit.RngChange || audit.DefaultChange
        || audit.Phase2PublishedValidation.Passed != 995
        || audit.Phase2PublishedValidation.Failed != 0
        || audit.Phase2PublishedValidation.Skipped != 0
        || audit.Phase2PublishedValidation.GithubCi != "NONE"
        || audit.Report != report)
        errors.Add("PROJECT_STATE does not preserve the exact Phase 2 independent-audit closure boundary.");

    RequireFile(report, "LANE.0 successor Phase 2 audit closure");
    CheckContains("DOCUMENTATION_INDEX.md", report, "LANE.0 successor Phase 2 audit index entry");
    foreach (var marker in new[]
    {
        "PHASE 2 AUDIT CLEAN WITH NON-BLOCKING OBSERVATIONS",
        "R2-01 through R2-08 are **CLOSED**",
        "HistoricalV2RouteRejectsChangedInstrumentIdentity",
        "passing rejection test",
        "995 passed / 0 failed / 0 skipped",
        "GitHub remote CI/check-run certification is **NONE**",
        "REQUIRED_BEFORE_BINDING_FOR_REAL_EXECUTION",
        "official internal research runner",
        "isolated execution launcher",
        "Independent audit closure **is not** binding issuance",
        "NOT_STARTED / NOT_AUTHORIZED / NO_C11_AUTHORIZATION"
    }) CheckContains(report, marker, "Phase 2 audit closure precision");
    foreach (var forbidden in new[]
    {
        "legally binding Phase 2 audit", "CLI Runner Pipeline", "physical path testing"
    })
        if (File.ReadAllText(Path.Combine(root, report)).Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Phase 2 audit closure contains forbidden overclaim/terminology: {forbidden}.");
}

void ValidateLane0CorrectiveSuccessorIntegrationPreregistration(ProjectState value)
{
    const string report = "docs/PHASE_LANE_0_CORRECTIVE_SUCCESSOR_INTEGRATION_PREREGISTRATION.md";
    const string contractPath = "docs/lane_0_corrective_successor_integration_preregistration_contract.json";
    const string contractIdentity = "7C0A86BF3C6647CA7092C4D3390EB97EA6E11D7E5429ECBA6F499D55C5417A02";
    const string baseline = "89c0ea9dcd55a5a0f9a34f580373df9115683821";
    const string phase2Contract = "11F55A6770BA78F008A6690C88561C714CAF45BA15B96E472C0C80B219C4D5B6";
    const string manifest = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    var phase = value.Phases.FirstOrDefault(x => x.Id == lane0IntegrationPreregistration);
    var phaseContract = value.PhaseContracts.FirstOrDefault(x => x.Id == lane0IntegrationPreregistration);
    var preregistration = value.Lane0CorrectiveSuccessorIntegrationPreregistration;
    if (phase?.Status != "COMPLETE" || phase.Outcome != "READY_FOR_HUMAN_REVIEW"
        || phase.BehaviorChange is not false || phase.Authorization != "NO_C11_AUTHORIZATION"
        || phase.Report != report || phaseContract?.Kind != "ResearchShadow"
        || phaseContract.BehaviorChange is not false
        || phaseContract.Authorization != "NO_C11_AUTHORIZATION")
        errors.Add("Successor integration preregistration phase must remain research-only and non-authorizing.");
    if (value.CurrentPhase != lane0IntegrationPreregistration
        || value.NextRecommendedPhase is not null || value.NextBehavioralPhase is not null
        || value.NextRecommendedAction != "HUMAN_REVIEW_REQUIRED" || value.BehaviorChange)
        errors.Add("Integration preregistration must stop at human review without activating implementation.");
    if (preregistration is null
        || preregistration.Status != "COMPLETE"
        || preregistration.Outcome != "READY_FOR_HUMAN_REVIEW"
        || preregistration.BaselinePublicHead != baseline
        || preregistration.SchemaVersion
            != "lane-0-corrective-successor-integration-preregistration.1"
        || preregistration.Contract != contractPath || preregistration.ContractHash != contractIdentity
        || preregistration.Report != report
        || preregistration.ParentAudit != lane0SuccessorPhase2Audit
        || preregistration.Phase2ContractHash != phase2Contract
        || preregistration.VerifiedManifestHash != manifest
        || preregistration.IntegrationStatus != "NOT_STARTED"
        || preregistration.IntegrationAuthorization != "NOT_AUTHORIZED"
        || preregistration.C11Authorization != "NO_C11_AUTHORIZATION"
        || preregistration.SemanticVerifierRequirement != "REQUIRED_BEFORE_BINDING"
        || preregistration.Authorization != "NO_C11_AUTHORIZATION"
        || preregistration.BindingPresent || preregistration.ReceiptPresent
        || preregistration.C11Accessed || preregistration.ExecutionAuthorized
        || preregistration.BehaviorChange || preregistration.RngChange || preregistration.DefaultChange
        || preregistration.NextRequiredAction != "HUMAN_REVIEW_REQUIRED")
        errors.Add("PROJECT_STATE integration preregistration boundary drifted.");

    foreach (var artifact in new[] { report, contractPath })
    {
        RequireFile(artifact, "successor integration preregistration artifact");
        CheckContains("DOCUMENTATION_INDEX.md", artifact,
            "successor integration preregistration index entry");
    }
    try
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            contractPath.Replace('/', Path.DirectorySeparatorChar))));
        var artifact = document.RootElement;
        var contract = artifact.GetProperty("contract");
        var actual = IntegrationPreregistrationGuard.CanonicalJsonHash(contract);
        if (actual != contractIdentity
            || artifact.GetProperty("canonicalSha256").GetString() != contractIdentity)
            errors.Add($"Integration preregistration canonical identity drifted; actual={actual}.");
        errors.AddRange(IntegrationPreregistrationGuard.Validate(contract));
    }
    catch (Exception exception)
    {
        errors.Add($"Integration preregistration contract cannot be validated: {exception.Message}");
    }

    foreach (var marker in new[]
    {
        "12 explicit paths, 11 unique original contents, one exact duplicate relationship",
        "322F995448A73713DCBECE3D140F4D516EF687DBC43CC140CFF14948566F906B",
        "exactly two admitted locations",
        "`CorpusRoot` is an opaque token until durable receipt creation",
        "Pre-receipt isolation is limited to source and execution roots",
        "Phase 2 package-verifier identity `DFF48221A6FB087367B1C6B1062B7355806C53668C6876A4BB4EC59983232D38`",
        "`canonicalBindingSha256`",
        "`parentIntegrationPreregistrationSha256`",
        "`c11Authorization = NO_C11_AUTHORIZATION` and `c11Accessed = false`",
        "REQUIRED_BEFORE_BINDING",
        "future independently audited integration public SHA",
        "Audit, binding issuance, explicit human authorization, receipt and execution remain distinct",
        "Phase 2 execution-preparation publication snapshot remains `995/0/0`",
        "audited public baseline repository at `89c0ea9dcd55a5a0f9a34f580373df9115683821` is `996/0/0`",
        "NOT_STARTED / NOT_AUTHORIZED"
    }) CheckContains(report, marker, "integration preregistration frozen boundary");
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
        value.BehaviorChange ? "true" : "none", "prohibited",
        "Integration preregistered / implementation not started / C11 not authorized", value.TestStatus.Passed,
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

static string CanonicalJsonHash(JsonElement element)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream)) WriteCanonicalJson(writer, element);
    return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
}

static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject()
                         .OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonicalJson(writer, property.Value);
            }
            writer.WriteEndObject();
            break;
        case JsonValueKind.Array:
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) WriteCanonicalJson(writer, item);
            writer.WriteEndArray();
            break;
        default:
            element.WriteTo(writer);
            break;
    }
}

string NormalizedTextTreeIdentity(IEnumerable<string> files)
{
    var rows = files.Order(StringComparer.Ordinal)
        .Select(path => $"{path.Replace('\\', '/')}|{NormalizedTextFileHash(path)}");
    return Convert.ToHexString(SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(string.Join('\n', rows))));
}

string NormalizedTextFileHash(string relative)
{
    var text = File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)),
            System.Text.Encoding.UTF8)
        .TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    return Convert.ToHexString(SHA256.HashData(
        new System.Text.UTF8Encoding(false).GetBytes(text)));
}

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
    Lane0CorrectivePostAttemptForensicsState? Lane0CorrectivePostAttemptForensics,
    Lane0CorrectiveSuccessorPhase1State? Lane0CorrectiveSuccessorPhase1,
    Lane0CorrectiveSuccessorPhase1AuditState? Lane0CorrectiveSuccessorPhase1Audit,
    Lane0CorrectiveSuccessorPhase2State? Lane0CorrectiveSuccessorPhase2,
    Lane0CorrectiveSuccessorPhase2AuditState? Lane0CorrectiveSuccessorPhase2Audit,
    Lane0CorrectiveSuccessorIntegrationPreregistrationState? Lane0CorrectiveSuccessorIntegrationPreregistration,
    TestState TestStatus,
    CorpusState ValidationCorpus,
    IReadOnlyList<string> MasterDocuments);

sealed record PhaseState(string Id, string Name, string Status, string? Outcome, bool? BehaviorChange,
    string? Report, string? Authorization, string? BlockedReason);
sealed record BranchState(string Id, string Decision, string BlockedBy);
sealed record PhaseContractState(string Id, string Kind, bool? BehaviorChange, string? Authorization);
sealed record Lane0CorrectivePostAttemptForensicsState(
    string Status,
    string PublicHead,
    string Attempt,
    string ScientificEvaluator,
    string ScientificResult,
    string ForensicFinding,
    string ForensicVerdict,
    string GlobalCertification,
    bool HistoricalFeasibilityRetained,
    string Promotion,
    string Retry,
    string Successor,
    string Report);
sealed record Lane0CorrectiveSuccessorPhase1State(
    string Status,
    string SchemaVersion,
    string Contract,
    string ContractHash,
    string Report,
    string ScientificRemediation,
    string Authorization,
    bool BindingPresent,
    bool ReceiptPresent,
    bool C11Accessed,
    bool BehaviorChange,
    bool RngChange,
    bool DefaultChange);
sealed record Lane0CorrectiveSuccessorPhase1AuditState(
    string Status,
    string Outcome,
    string PublicationHead,
    string PreregistrationContractHash,
    string IndependentAudit,
    string ScientificValidity,
    string Authorization,
    bool BindingPresent,
    bool ReceiptPresent,
    bool C11Accessed,
    string Phase2Status,
    string Phase2Authorization,
    string Phase2RequiredHardening,
    bool BehaviorChange,
    bool RngChange,
    bool DefaultChange,
    string Report);
sealed record Lane0CorrectiveSuccessorPhase2State(
    string Status,
    string Outcome,
    string BaselinePublicHead,
    string SchemaVersion,
    string Contract,
    string ContractHash,
    string Report,
    string ManifestCoupling,
    string ManifestHash,
    string ScientificImplementationHash,
    string ExecutionHarnessHash,
    string PackageVerifierHash,
    string FrozenDependenciesHash,
    string Authorization,
    bool BindingPresent,
    bool ReceiptPresent,
    bool C11Accessed,
    bool ExecutionAuthorized,
    bool ScientificEvaluationExecuted,
    bool BehaviorChange,
    bool RngChange,
    bool DefaultChange,
    string NextRequiredAction);
sealed record Lane0CorrectiveSuccessorPhase2AuditState(
    string Status,
    string Outcome,
    string AuditedPublicHead,
    string ImplementationCommit,
    string Phase2ContractHash,
    string ScientificImplementationHash,
    string ExecutionHarnessHash,
    string PackageVerifierHash,
    string FrozenDependenciesHash,
    string IndependentAudit,
    string R2Closure,
    string CurrentPackageVerifier,
    string SemanticVerifierRequirement,
    string IntegrationStatus,
    string IntegrationAuthorization,
    string Authorization,
    bool BindingPresent,
    bool ReceiptPresent,
    bool C11Accessed,
    bool ExecutionAuthorized,
    bool ScientificEvaluationExecuted,
    bool BehaviorChange,
    bool RngChange,
    bool DefaultChange,
    PublishedValidationState Phase2PublishedValidation,
    string Report);
sealed record Lane0CorrectiveSuccessorIntegrationPreregistrationState(
    string Status,
    string Outcome,
    string BaselinePublicHead,
    string SchemaVersion,
    string Contract,
    string ContractHash,
    string Report,
    string ParentAudit,
    string Phase2ContractHash,
    string VerifiedManifestHash,
    string IntegrationStatus,
    string IntegrationAuthorization,
    string C11Authorization,
    string SemanticVerifierRequirement,
    string Authorization,
    bool BindingPresent,
    bool ReceiptPresent,
    bool C11Accessed,
    bool ExecutionAuthorized,
    bool BehaviorChange,
    bool RngChange,
    bool DefaultChange,
    string NextRequiredAction);
sealed record TestState(int Passed, int Failed, int Skipped);
sealed record PublishedValidationState(int Passed, int Failed, int Skipped, string GithubCi);
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

sealed record Lane0CorrectiveContractDocument(
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
    IReadOnlyList<string> Families,
    SortedDictionary<string, int> HistoricalDenominators,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<string> RequiredControls,
    SortedDictionary<string, string> OutcomeRules,
    IReadOnlyList<string> PairedComparison,
    string HistoricalOperationalG1Analysis,
    IReadOnlyList<string> StopConditions,
    IReadOnlyList<string> InferenceLimits,
    SortedDictionary<string, string> ReusedDependencies);

sealed record Lane0CorrectiveHardeningContractDocument(
    string SchemaVersion,
    string PublishedRepairHead,
    string HistoricalLane0Head,
    string ParentCorrectiveContractSha256,
    string RequiredPublicationBinding,
    string RepairImplementationSha256,
    IReadOnlyList<string> RepairImplementationFiles,
    string CorrectiveHarnessSha256,
    IReadOnlyList<string> CorrectiveHarnessFiles,
    string CorpusManifestSha256,
    string MemoryLimit,
    string FutureHeldIndexSemantics,
    IReadOnlyList<string> ExclusionCounters,
    string DonorPartitionInvariant,
    IReadOnlyList<string> RequiredControls,
    SortedDictionary<string, string> OutcomeRules,
    IReadOnlyList<string> FutureResultColumns,
    SortedDictionary<string, string> ReusedDependencies);

sealed record Lane0CounterClosureContractDocument(
    string SchemaVersion,
    string AuditedBaselineHead,
    string ParentHardeningContractSha256,
    string IdentityAlgorithm,
    string RequiredPublicationBinding,
    string RepairImplementationSha256,
    IReadOnlyList<string> RepairImplementationFiles,
    string CorrectiveHarnessSha256,
    IReadOnlyList<string> CorrectiveHarnessFiles,
    string CorpusManifestSha256,
    string MemoryLimit,
    string CounterSemantics,
    IReadOnlyList<string> RequiredControls,
    SortedDictionary<string, string> OutcomeRules,
    SortedDictionary<string, string> ReusedDependencies);
