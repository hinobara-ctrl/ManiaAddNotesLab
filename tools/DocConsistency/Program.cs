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
    ValidateFilesAndIndex(state);
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
        var tracked = RepositoryRootGuard.GetTrackedTopLevelEntries(root);
        var result = RepositoryRootGuard.Validate(declared, tracked);
        foreach (var entry in result.UnexpectedTrackedEntries)
            errors.Add($"Unexpected tracked repository root entry: {entry}");
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
    RequireValue(value.CurrentPhase, "currentPhase");
    RequireValue(value.NextRecommendedPhase, "nextRecommendedPhase");

    var validStatuses = new HashSet<string>(StringComparer.Ordinal)
        { "COMPLETE", "HOLD", "REJECTED", "NEXT", "DEFERRED", "PENDING" };
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

    var current = value.Phases.FirstOrDefault(x => x.Id == value.CurrentPhase);
    var next = value.Phases.FirstOrDefault(x => x.Id == value.NextRecommendedPhase);
    if (current is null) errors.Add("currentPhase does not exist in phases.");
    if (next is null) errors.Add("nextRecommendedPhase does not exist in phases.");
    if (value.CurrentPhase == value.NextRecommendedPhase)
        errors.Add("currentPhase and nextRecommendedPhase must be different.");

    var closedStatuses = new HashSet<string>(StringComparer.Ordinal) { "COMPLETE", "HOLD", "REJECTED" };
    var latestClosed = value.Phases.LastOrDefault(x => closedStatuses.Contains(x.Status));
    if (current is not null && !closedStatuses.Contains(current.Status))
        errors.Add($"currentPhase {current.Id} is not closed: {current.Status}.");
    if (latestClosed is not null && current?.Id != latestClosed.Id)
        errors.Add($"currentPhase must be the latest closed phase ({latestClosed.Id}), not {value.CurrentPhase}.");
    if (next is not null && next.Status != "NEXT")
        errors.Add($"nextRecommendedPhase {next.Id} must have status NEXT, not {next.Status}.");
    if (value.Phases.Count(x => x.Status == "NEXT") != 1)
        errors.Add("Exactly one phase must have status NEXT.");

    foreach (var required in new[] { "C1", "C1.1", "C1.2", "C2", "D0", "D0.1", "D0.2", "F1", "F2" })
        if (value.Phases.All(x => x.Id != required)) errors.Add($"Required phase is absent from state: {required}.");

    if (value.TestStatus.Passed < 0 || value.TestStatus.Failed < 0 || value.TestStatus.Skipped < 0)
        errors.Add("testStatus counts cannot be negative.");
    if (value.ValidationCorpus.Families < 1 || value.ValidationCorpus.HumanCharts < 1
        || value.ValidationCorpus.Keymodes.Count == 0)
        errors.Add("validationCorpus must describe at least one family, chart, and keymode.");
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
    var testSnapshot = $"Tests: {value.TestStatus.Passed} passed / {value.TestStatus.Failed} failed / {value.TestStatus.Skipped} skipped";
    foreach (var document in new[] { "README.md", "PROJECT_STATUS.md" })
    {
        var block = ReadStateBlock(document);
        if (block is null) continue;
        if (current is not null)
        {
            CheckBlockContains(document, block, $"Current phase: {current.Id}", "currentPhase value");
            CheckBlockContains(document, block, current.Status, "current phase status");
            if (current.Outcome is not null)
                CheckNormalizedBlockContains(document, block, current.Outcome, "current phase outcome");
        }
        if (next is not null)
        {
            CheckBlockContains(document, block, $"Next recommended phase: {next.Id}", "nextRecommendedPhase value");
            CheckBlockContains(document, block, next.Name, "next recommended phase name");
        }
        CheckBlockContains(document, block, $"Behavior policy: `{value.BehaviorPolicyVersion}`", "behavior policy value");
        CheckBlockContains(document, block, $"Evidence profile: `{value.EvidenceProfileVersion}`", "evidence profile value");
        CheckBlockContains(document, block, $"Diagnostic schema: `{value.DiagnosticVersion}`", "diagnostic value");
        CheckBlockContains(document, block, testSnapshot, "testStatus value");
    }
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
    var row = File.ReadLines(path).FirstOrDefault(line =>
        line.StartsWith('|') && line.Contains($"{phase.Id} —", StringComparison.Ordinal));
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

void CheckNormalizedBlockContains(string relative, string block, string expected, string meaning)
{
    if (!Normalize(block).Contains(Normalize(expected), StringComparison.Ordinal))
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
    IReadOnlyList<PhaseState> Phases,
    string CurrentPhase,
    string NextRecommendedPhase,
    TestState TestStatus,
    CorpusState ValidationCorpus,
    IReadOnlyList<string> MasterDocuments);

sealed record PhaseState(string Id, string Name, string Status, string? Outcome, bool? BehaviorChange, string? Report);
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
