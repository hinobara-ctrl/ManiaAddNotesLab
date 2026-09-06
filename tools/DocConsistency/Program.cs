using System.Text.Json;

if (args.Length != 1 || args[0] != "--check")
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/DocConsistency -- --check");
    return 2;
}

var root = FindRoot(AppContext.BaseDirectory);
var statePath = Path.Combine(root, "docs", "PROJECT_STATE.json");
var errors = new List<string>();
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
    var validStatuses = new HashSet<string>(StringComparer.Ordinal)
        { "COMPLETE", "HOLD", "REJECTED", "NEXT", "DEFERRED", "PENDING" };
    foreach (var phase in state.Phases)
    {
        if (!validStatuses.Contains(phase.Status)) errors.Add($"Phase {phase.Id} has invalid status {phase.Status}.");
        if (phase.Report is not null) RequireFile(phase.Report, $"report for phase {phase.Id}");
    }
    if (state.Phases.All(x => x.Id != state.CurrentPhase)) errors.Add("currentPhase does not exist in phases.");
    if (state.Phases.All(x => x.Id != state.NextRecommendedPhase)) errors.Add("nextRecommendedPhase does not exist in phases.");
    foreach (var document in state.MasterDocuments) RequireFile(document, "master document");

    var current = state.Phases.FirstOrDefault(x => x.Id == state.CurrentPhase);
    var next = state.Phases.FirstOrDefault(x => x.Id == state.NextRecommendedPhase);
    CheckContains("README.md", state.BehaviorPolicyVersion, "behavior policy");
    CheckContains("PROJECT_STATUS.md", state.BehaviorPolicyVersion, "behavior policy");
    if (current is not null)
    {
        CheckContains("README.md", $"Current phase: {current.Id}", "current phase state block");
        CheckContains("PROJECT_STATUS.md", $"Current phase: {current.Id}", "current phase state block");
        CheckContains("ROADMAP.md", $"{current.Id} —", "current phase");
    }
    if (next is not null)
    {
        CheckContains("README.md", $"Next recommended phase: {next.Id}", "next phase state block");
        CheckContains("PROJECT_STATUS.md", $"Next recommended phase: {next.Id}", "next phase state block");
        CheckContains("ROADMAP.md", $"{next.Id} —", "next phase");
    }
    foreach (var phase in state.Phases.Where(x => x.Report is not null))
        CheckContains("DOCUMENTATION_INDEX.md", phase.Report!, $"report {phase.Id}");

    var scanned = state.MasterDocuments.Where(x => x.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        .Select(x => Path.Combine(root, x)).Where(File.Exists).ToArray();
    var obsolete = new[]
    {
        "única familia humana disponible",
        "Double-counting witnesses | duration+release duplica autoridad | deduplicación por ObservationId",
        "Deduplicar por `(candidate, ObservationId)`; varias etiquetas no multiplican testigos."
    };
    foreach (var phrase in obsolete)
    foreach (var file in scanned)
        if (File.ReadAllText(file).Contains(phrase, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Obsolete phrase in {Path.GetRelativePath(root, file)}: {phrase}");
}

if (errors.Count > 0)
{
    Console.Error.WriteLine("DOCUMENTATION CONSISTENCY: FAIL");
    foreach (var error in errors) Console.Error.WriteLine($"- {error}");
    return 1;
}

Console.WriteLine("DOCUMENTATION CONSISTENCY: PASS");
return 0;

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
sealed record CorpusState(int Families, int HumanCharts, int ChartsWithLn, IReadOnlyList<int> Keymodes, int LnTargets);
