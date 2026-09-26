using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;

public static class SafetySelectionSetRemappingPostPublicationVerifier
{
    public const string HistoricalEntryHead = "52f21af56f8034e07fcaa9d849cb9440b9429e73";
    public const string PublishedExperimentCommit = "61f7e14f5f4462876de6a9263f07f881f05f3dfd";
    public const string HistoricalImplementation = "B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835";
    public const string HistoricalHarness = "917F8157247C05968736DFE883BC578EE6332E4665CD9EA890FD89B79E0CB220";
    public const string HistoricalCorpus = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    public const string HistoricalContract = "A1A83F4515ADDD18398842B5FECC1BD0F43BC60E1D62F6B2B18B84A369244E66";

    private static readonly string[] ImplementationFiles =
    [
        "src/ManiaAddNotesLab.Core/AddNotesEngine.cs", "src/ManiaAddNotesLab.Core/BeatTimeline.cs",
        "src/ManiaAddNotesLab.Core/ChartAnalysis.cs", "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
        "src/ManiaAddNotesLab.Core/Model.cs", "src/ManiaAddNotesLab.Core/OsuBeatmap.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateResearch.cs"
    ];

    private static readonly string[] HistoricalHarnessFiles =
    [
        "src/ManiaAddNotesLab.Core/GenerationProvenanceResearch.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateHardeningResearch.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationForensicAttributionResearch.cs",
        "src/ManiaAddNotesLab.Core/SelectionSetRemappingResearch.cs",
        "tests/ManiaAddNotesLab.Tests/SelectionSetRemappingResearchTests.cs",
        "tools/ManiaAddNotesLab.Experiments/Program.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetySelectionSetRemappingRunner.cs"
    ];

    private static readonly string[] MaintenanceHarnessFiles =
    [
        "src/ManiaAddNotesLab.Core/SelectionSetRemappingResearch.cs",
        "tests/ManiaAddNotesLab.Tests/SelectionSetRemappingResearchTests.cs",
        "tests/ManiaAddNotesLab.Tests/SelectionSetRemappingPostPublicationVerifierTests.cs",
        "tools/ManiaAddNotesLab.Experiments/Program.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetySelectionSetRemappingRunner.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetySelectionSetRemappingPostPublicationVerifier.cs"
    ];

    public static string Run(string repositoryRoot, string manifestPath, string contractPath,
        string artifactDirectory, string outputDirectory)
    {
        using var contractDocument = JsonDocument.Parse(File.ReadAllText(contractPath));
        var root = contractDocument.RootElement;
        var contract = root.GetProperty("contract");
        var declaredContractHash = RequiredString(root, "canonicalSha256");
        var computedContractHash = Hash(JsonSerializer.SerializeToUtf8Bytes(contract));
        RequireEqual(HistoricalContract, declaredContractHash, "declared contract identity");
        RequireEqual(HistoricalContract, computedContractHash, "canonical contract content identity");
        RequireEqual(HistoricalEntryHead, RequiredString(contract, "repositoryEntryHead"), "historical entry HEAD");
        RequireEqual(HistoricalImplementation, RequiredString(contract, "implementationSnapshotSha256"),
            "contract implementation identity");
        RequireEqual(HistoricalHarness, RequiredString(contract, "harnessSnapshotSha256"),
            "contract harness identity");
        RequireEqual(HistoricalCorpus, RequiredString(contract, "corpusManifestSha256"),
            "contract corpus identity");

        var currentImplementation = SnapshotFromDisk(repositoryRoot, ImplementationFiles);
        RequireEqual(HistoricalImplementation, currentImplementation, "current behavioral implementation identity");
        var publishedHarness = SnapshotFromGit(repositoryRoot, PublishedExperimentCommit, HistoricalHarnessFiles);
        RequireEqual(HistoricalHarness, publishedHarness, "published historical harness identity");
        var manifest = FrozenC11ManifestResearch.Load(manifestPath);
        RequireEqual(HistoricalCorpus, manifest.CanonicalSha256, "trusted C11 identity");

        VerifyHistoricalInputs(repositoryRoot, contract);
        VerifyHashManifest(artifactDirectory, Path.Combine(artifactDirectory, "sha256sums.txt"),
            requireExactFileSet: true);
        VerifyHashManifest(Path.Combine(artifactDirectory, "priority"),
            Path.Combine(artifactDirectory, "priority", "sha256sums.txt"), requireExactFileSet: true);
        RequireEqual(FileHash(contractPath), FileHash(Path.Combine(artifactDirectory, "frozen_contract.json")),
            "documentation/artifact frozen contract bytes");
        VerifySummary(Path.Combine(artifactDirectory, "summary.json"));
        VerifyExperimentalResults(Path.Combine(artifactDirectory, "experimental_e_results.csv"));
        VerifyNoInterference(Path.Combine(artifactDirectory, "no_interference.csv"));

        var maintenanceHarness = SnapshotFromDisk(repositoryRoot, MaintenanceHarnessFiles);
        Directory.CreateDirectory(outputDirectory);
        var summaryPath = Path.Combine(outputDirectory, "verification_summary.json");
        var summary = new
        {
            schemaVersion = "safety-selection-set-remapping-post-publication-verification.1",
            verificationKind = "OFFLINE_POST_PUBLICATION_ARTIFACT_VERIFICATION_NOT_EXPERIMENTAL_REPRODUCTION",
            checkoutHead = Git(repositoryRoot, "rev-parse", "HEAD").Trim(),
            historicalEntryHead = HistoricalEntryHead,
            publishedExperimentCommit = PublishedExperimentCommit,
            contractSha256 = computedContractHash,
            implementationSnapshotSha256 = currentImplementation,
            historicalHarnessSnapshotSha256 = publishedHarness,
            maintenanceHarnessSnapshotSha256 = maintenanceHarness,
            corpusManifestSha256 = manifest.CanonicalSha256,
            primaryArtifactHashEntriesVerified = File.ReadLines(
                Path.Combine(artifactDirectory, "sha256sums.txt")).Count(),
            priorityArtifactHashEntriesVerified = File.ReadLines(
                Path.Combine(artifactDirectory, "priority", "sha256sums.txt")).Count(),
            focusedRuns = 8,
            eDemonstrated = 8,
            abstained = 0,
            historicalObservations = 70835,
            diagnosticEpisodes = 8,
            noInterferenceRows = 8,
            outcome = "VERIFIED_OFFLINE",
            historicalScientificOutcome = "E_DEMONSTRATED",
            globalStatus = "NEEDS_REVIEW",
            promotion = "NOT_AUTHORIZED",
            fullMatrixExecuted = false,
            experimentRerun = false,
            behaviorChanged = false
        };
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, new JsonSerializerOptions
            { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outputDirectory, "sha256sums.txt"),
            $"{FileHash(summaryPath)}  verification_summary.json{Environment.NewLine}", new UTF8Encoding(false));
        return maintenanceHarness;
    }

    public static void VerifyHashManifest(string artifactDirectory, string manifestPath,
        bool requireExactFileSet)
    {
        var entries = File.ReadLines(manifestPath).Where(x => !string.IsNullOrWhiteSpace(x)).Select(line =>
        {
            var split = line.IndexOf("  ", StringComparison.Ordinal);
            if (split != 64) throw new InvalidDataException($"Malformed SHA-256 manifest row: {line}");
            return (Hash: line[..split], Path: line[(split + 2)..].Replace('/', Path.DirectorySeparatorChar));
        }).ToArray();
        if (entries.Select(x => x.Path).Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw new InvalidDataException("SHA-256 manifest contains duplicate paths.");
        foreach (var entry in entries)
        {
            var fullPath = Path.GetFullPath(Path.Combine(artifactDirectory, entry.Path));
            if (!fullPath.StartsWith(Path.GetFullPath(artifactDirectory) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                throw new InvalidDataException($"Hashed artifact is missing or outside its root: {entry.Path}");
            RequireEqual(entry.Hash, FileHash(fullPath), $"artifact {entry.Path}");
        }
        if (!requireExactFileSet) return;
        var nestedHashManifests = Directory.EnumerateFiles(artifactDirectory, "sha256sums.txt",
                SearchOption.AllDirectories)
            .Select(x => Path.GetRelativePath(artifactDirectory, x).Replace('\\', '/'));
        var expected = entries.Select(x => x.Path.Replace(Path.DirectorySeparatorChar, '/'))
            .Concat(nestedHashManifests).Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var actual = Directory.EnumerateFiles(artifactDirectory, "*", SearchOption.AllDirectories)
            .Select(x => Path.GetRelativePath(artifactDirectory, x).Replace('\\', '/'))
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
            throw new InvalidDataException("Artifact directory has missing or unexpected files relative to sha256sums.txt.");
    }

    private static void VerifySummary(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        RequireEqual(HistoricalContract, RequiredString(root, "contractSha256"), "summary contract identity");
        RequireEqual(HistoricalEntryHead, RequiredString(root, "repositoryEntryHead"), "summary entry HEAD");
        RequireEqual(HistoricalImplementation, RequiredString(root, "implementationSnapshotSha256"), "summary implementation");
        RequireEqual(HistoricalHarness, RequiredString(root, "harnessSnapshotSha256"), "summary harness");
        RequireEqual(HistoricalCorpus, RequiredString(root, "corpusManifestSha256"), "summary corpus");
        RequireInt(root, "focusedRuns", 8); RequireInt(root, "eDemonstrated", 8);
        RequireInt(root, "abstained", 0); RequireInt(root, "historicalObservations", 70835);
        RequireInt(root, "diagnosticEpisodes", 8);
        RequireBoolean(root, "priorityOnly", false); RequireBoolean(root, "fullMatrixExecuted", false);
        RequireBoolean(root, "behaviorChanged", false); RequireBoolean(root, "downstreamTargetAttributed", false);
        RequireEqual("E_DEMONSTRATED", RequiredString(root, "outcome"), "historical E outcome");
        RequireEqual("NOT_AUTHORIZED", RequiredString(root, "promotion"), "promotion status");
    }

    private static void VerifyExperimentalResults(string path)
    {
        var rows = Csv(path);
        if (rows.Count != 8) throw new InvalidDataException($"Expected 8 E result rows, found {rows.Count}.");
        if (rows.Any(x => x[4] != "ESelectionSetRemap" || x[5] != "true" || x[9] != string.Empty))
            throw new InvalidDataException("E results contain an abstention, failed criterion or non-E disposition.");
        if (rows.Sum(x => int.Parse(x[6], CultureInfo.InvariantCulture)) != 70835
            || rows.Sum(x => int.Parse(x[7], CultureInfo.InvariantCulture)) != 8)
            throw new InvalidDataException("E result observation or episode totals drifted.");
    }

    private static void VerifyNoInterference(string path)
    {
        var rows = Csv(path);
        if (rows.Count != 8) throw new InvalidDataException($"Expected 8 no-interference rows, found {rows.Count}.");
        if (rows.Any(x => x.Skip(3).Take(4).Any(value => value != "true")))
            throw new InvalidDataException("A no-interference invariant is false.");
    }

    private static List<string[]> Csv(string path)
    {
        var lines = File.ReadAllLines(path);
        return lines.Skip(1).Where(x => x.Length != 0).Select(ParseCsvLine).ToList();
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>(); var value = new StringBuilder(); var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                    { value.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted) { fields.Add(value.ToString()); value.Clear(); }
            else value.Append(character);
        }
        if (quoted) throw new InvalidDataException("CSV row has an unterminated quoted field.");
        fields.Add(value.ToString()); return fields.ToArray();
    }

    private static void VerifyHistoricalInputs(string repositoryRoot, JsonElement contract)
    {
        foreach (var item in contract.GetProperty("historicalInputs").EnumerateArray())
        {
            var relative = RequiredString(item, "path");
            var path = Path.Combine(repositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            RequireEqual(RequiredString(item, "sha256"), FileHash(path), $"historical input {relative}");
        }
    }

    private static string SnapshotFromDisk(string root, IEnumerable<string> paths) =>
        ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(paths.Select(path =>
            new NamedImplementationContent(path, File.ReadAllBytes(Path.Combine(root,
                path.Replace('/', Path.DirectorySeparatorChar))))));

    private static string SnapshotFromGit(string root, string commit, IEnumerable<string> paths) =>
        ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(paths.Select(path =>
            new NamedImplementationContent(path, GitBytes(root, commit, path))));

    private static byte[] GitBytes(string root, string commit, string path)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        start.ArgumentList.Add("show"); start.ArgumentList.Add($"{commit}:{path}");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start git.");
        using var memory = new MemoryStream(); process.StandardOutput.BaseStream.CopyTo(memory);
        var error = process.StandardError.ReadToEnd(); process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidDataException($"Cannot read {path} at {commit}: {error.Trim()}");
        return memory.ToArray();
    }

    private static string Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start git.");
        var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidDataException($"git {string.Join(' ', arguments)} failed: {error.Trim()}");
        return output;
    }

    private static void RequireInt(JsonElement root, string property, int expected)
    {
        if (root.GetProperty(property).GetInt32() != expected)
            throw new InvalidDataException($"{property} does not equal {expected}.");
    }
    private static void RequireBoolean(JsonElement root, string property, bool expected)
    {
        if (root.GetProperty(property).GetBoolean() != expected)
            throw new InvalidDataException($"{property} does not equal {expected}.");
    }
    private static string RequiredString(JsonElement root, string property) =>
        root.GetProperty(property).GetString() ?? throw new InvalidDataException($"{property} is null.");
    private static void RequireEqual(string expected, string actual, string description)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidDataException($"{description} drifted: expected {expected}, actual {actual}.");
    }
    private static string FileHash(string path) => Hash(File.ReadAllBytes(path));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
