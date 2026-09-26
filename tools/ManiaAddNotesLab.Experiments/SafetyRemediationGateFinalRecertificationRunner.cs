using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualBasic.FileIO;
using ManiaAddNotesLab.Core;

internal static class SafetyRemediationGateFinalRecertificationRunner
{
    private const string MemoryLimit = "0x400000000";
    private static readonly string[] ImplementationFiles =
    [
        "src/ManiaAddNotesLab.Core/AddNotesEngine.cs", "src/ManiaAddNotesLab.Core/BeatTimeline.cs",
        "src/ManiaAddNotesLab.Core/ChartAnalysis.cs", "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
        "src/ManiaAddNotesLab.Core/Model.cs", "src/ManiaAddNotesLab.Core/OsuBeatmap.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateResearch.cs"
    ];
    private static readonly string[] HarnessFiles =
    [
        "src/ManiaAddNotesLab.Core/C11CorpusDiscovery.cs",
        "src/ManiaAddNotesLab.Core/FrozenC11ManifestResearch.cs",
        "src/ManiaAddNotesLab.Core/GenerationProvenanceResearch.cs",
        "src/ManiaAddNotesLab.Core/GeometrySafetyAttributionResearch.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateHardeningResearch.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationFinalRecertificationResearch.cs",
        "tests/ManiaAddNotesLab.Tests/FrozenC11ManifestGuardTests.cs",
        "tests/ManiaAddNotesLab.Tests/PhaseC12ExactHeadRelationTests.cs",
        "tests/ManiaAddNotesLab.Tests/PhaseSafetyRemediationGateHardeningTests.cs",
        "tests/ManiaAddNotesLab.Tests/SafetyRemediationFinalRecertificationTests.cs",
        "tests/ManiaAddNotesLab.Tests/SafetyRemediationFollowupGuardTests.cs",
        "tools/ManiaAddNotesLab.Experiments/FrozenC11CorpusRunner.cs",
        "tools/ManiaAddNotesLab.Experiments/Program.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetyRemediationGateHardeningRunner.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetyRemediationGateUnresolvedFollowupRunner.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetyRemediationGateFinalRecertificationRunner.cs"
    ];
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions Canonical = new(Json) { WriteIndented = false };

    public static string Prepare(string repositoryRoot, string manifestPath, string contractPath,
        string workDirectory)
    {
        Directory.CreateDirectory(workDirectory);
        var hardeningContract = Path.Combine(workDirectory, "official_hardening_contract.json");
        var inventory = Path.Combine(workDirectory, "official_implementation_inventory.csv");
        var hardeningHash = SafetyRemediationGateHardeningRunner.Prepare(repositoryRoot,
            hardeningContract, inventory);
        var followupContract = Path.Combine(workDirectory, "official_d_contract.json");
        var followupHash = SafetyRemediationGateUnresolvedFollowupRunner.Prepare(repositoryRoot,
            manifestPath, followupContract);
        var manifest = FrozenC11ManifestResearch.Load(manifestPath);
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var chartIds = manifestDocument.RootElement.GetProperty("manifest").GetProperty("charts")
            .EnumerateArray().Select(x => x.GetProperty("chartId").GetString()!).ToImmutableArray();
        var contract = new FinalContract(
            "safety-remediation-gate-final-recertification-contract.1", GitHead(repositoryRoot),
            Snapshot(repositoryRoot, ImplementationFiles), Snapshot(repositoryRoot, HarnessFiles),
            manifest.CanonicalSha256, hardeningHash, followupHash, MemoryLimit,
            chartIds, Enumerable.Range(1, 20).ToImmutableArray(),
            ["02D9D1781418E7442956D4D901C8C611E58256556AE9A828E72128C3B5B8E3EB|9",
             "02D9D1781418E7442956D4D901C8C611E58256556AE9A828E72128C3B5B8E3EB|11",
             "02D9D1781418E7442956D4D901C8C611E58256556AE9A828E72128C3B5B8E3EB|17",
             "20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788|11"],
            new("legacy generation; Chance=.50; articulation OFF; canonical authority OFF",
                "same configuration and RNG; canonical playable geometry governs legal lanes before selection"),
            new(220, 4, 224, 209, 6, 215),
            "A: equivalent selected control commit is rejected by canonical authority before commit. B: active canonical-governed lineage, unequal sufficient parent state, exact historical commit absent, no prior reconvergence. D: target commits identically but every independently audited control blocker is absent in treatment under active unreconverged canonical lineage. Otherwise UNRESOLVED.",
            "Materialized GenerationState, committed latent geometry, RNG positions, opportunity cursor, pending articulation identity and frozen opportunity sequence; compare complete states, not hashes alone.",
            "Full sufficient-state equality closes ancestry permanently; a later divergence requires a new stable lineage identity.",
            "Independent GeometrySafetyAttributionResearch audit over runtime output and serialized/reparsed integer-ms output; occupied LN endpoint equality remains invalid.",
            ["774-test baseline", "DocConsistency", "manifest hash attacks", "follow-up leaf bad controls",
             "OFF/reference equality", "determinism", "reparse", "G1 identity", "gate RNG zero"],
            ["new treatment-only HardValidity", "any historical case unresolved", "unattributed state divergence",
             "reparse mismatch", "determinism failure", "OFF/reference mismatch", "G1 identity drift",
             "corpus or implementation identity drift"],
            ["docs/safety_remediation_gate_final_recertification_contract.json",
             "docs/safety_remediation_gate_final_recertification_runs.csv",
             "docs/safety_remediation_gate_final_recertification_cases.csv",
             "docs/safety_remediation_gate_final_recertification_d_cases.json",
             "docs/safety_remediation_gate_final_recertification_summary.json",
             "docs/PHASE_SAFETY_REMEDIATION_GATE_FINAL_RECERTIFICATION.md"]);
        var hash = Hash(JsonSerializer.Serialize(contract, Canonical));
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        File.WriteAllText(contractPath, JsonSerializer.Serialize(new FinalContractArtifact(contract,
            hash, "UTF-8 System.Text.Json camelCase; ordered arrays; no machine paths or timestamps"), Json)
            + Environment.NewLine, new UTF8Encoding(false));
        return hash;
    }

    public static string Run(string repositoryRoot, string corpusRoot, string sourceDirectory,
        string outputDirectory, string workDirectory, string contractPath)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_GCHeapHardLimit"), MemoryLimit,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Final recertification requires DOTNET_GCHeapHardLimit={MemoryLimit}.");
        var artifact = JsonSerializer.Deserialize<FinalContractArtifact>(File.ReadAllText(contractPath), Json)
            ?? throw new InvalidDataException("Final recertification contract could not be decoded.");
        if (Hash(JsonSerializer.Serialize(artifact.Contract, Canonical)) != artifact.CanonicalSha256
            || artifact.Contract.RepositoryEntryHead != GitHead(repositoryRoot)
            || artifact.Contract.ImplementationSnapshotSha256 != Snapshot(repositoryRoot, ImplementationFiles)
            || artifact.Contract.HarnessSnapshotSha256 != Snapshot(repositoryRoot, HarnessFiles))
            throw new InvalidDataException("Final contract or frozen implementation/harness identity drifted.");
        var manifestPath = Path.Combine(sourceDirectory, "g1_gate_runtime_manifest.json");
        var manifest = FrozenC11ManifestResearch.Load(manifestPath);
        if (manifest.CanonicalSha256 != artifact.Contract.CorpusManifestSha256)
            throw new InvalidDataException("Final contract corpus identity drifted.");
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(workDirectory);
        var official = Path.Combine(workDirectory, "official-matrix");
        Directory.CreateDirectory(official);
        var hardeningContract = Path.Combine(workDirectory, "official_hardening_contract.json");
        var hardeningOutcome = SafetyRemediationGateHardeningRunner.Run(repositoryRoot, corpusRoot,
            sourceDirectory, official, official, hardeningContract);
        var followupPath = Path.Combine(workDirectory, "official_d_result.json");
        var followupOutcome = SafetyRemediationGateUnresolvedFollowupRunner.Run(repositoryRoot, corpusRoot,
            Path.Combine(official, "safety_remediation_gate_hardened_cases.csv"), manifestPath,
            Path.Combine(workDirectory, "official_d_contract.json"), followupPath);

        var runsSource = Path.Combine(official, "safety_remediation_gate_hardening_runs.csv");
        var casesSource = Path.Combine(official, "safety_remediation_gate_hardened_cases.csv");
        var runsOutput = Path.Combine(outputDirectory, "safety_remediation_gate_final_recertification_runs.csv");
        var casesOutput = Path.Combine(outputDirectory, "safety_remediation_gate_final_recertification_cases.csv");
        File.Copy(runsSource, runsOutput, overwrite: true);
        var finalCases = MergeCases(casesSource, followupPath, casesOutput);
        File.Copy(followupPath, Path.Combine(outputDirectory,
            "safety_remediation_gate_final_recertification_d_cases.json"), overwrite: true);

        using var hardeningSummary = JsonDocument.Parse(File.ReadAllText(Path.Combine(official,
            "safety_remediation_gate_validation_hardening_summary.json")));
        var root = hardeningSummary.RootElement;
        var runtime = root.GetProperty("runtime");
        var validation = root.GetProperty("validation");
        var safe = runtime.GetProperty("treatmentHardViolations").GetInt32() == 0
            && runtime.GetProperty("treatmentOnlyHardViolations").GetInt32() == 0
            && runtime.GetProperty("unexplainedStateDivergences").GetInt32() == 0
            && runtime.GetProperty("gateRngCalls").GetInt32() == 0
            && validation.GetProperty("defaultEquivalenceFailures").GetInt32() == 0
            && validation.GetProperty("determinismFailures").GetInt32() == 0
            && validation.GetProperty("reparseFailures").GetInt32() == 0
            && validation.GetProperty("g1EvidenceFailures").GetInt32() == 0;
        var complete = finalCases.Length == 215
            && finalCases.Count(x => x.FinalClass == SafetyRemediationFinalRecertificationResearch.Direct) == 188
            && finalCases.Count(x => x.FinalClass == SafetyRemediationFinalRecertificationResearch.Unreachable) == 22
            && finalCases.Count(x => x.FinalClass == SafetyRemediationFinalRecertificationResearch.ConflictingObjectAbsent) == 5;
        var outcome = safe && complete && followupOutcome == "FIVE_MECHANISMS_DEMONSTRATED"
            ? "RECERTIFIED_WITHIN_C11" : "NEEDS_REVIEW";
        var summary = new
        {
            schemaVersion = "safety-remediation-gate-final-recertification-summary.1",
            contractSha256 = artifact.CanonicalSha256,
            artifact.Contract.RepositoryEntryHead,
            artifact.Contract.ImplementationSnapshotSha256,
            artifact.Contract.HarnessSnapshotSha256,
            corpusManifestSha256 = manifest.CanonicalSha256,
            matrix = new { primaryPairs = 220, g1CompatibilityPairs = 4, totalPairs = 224 },
            cases = new { total = 215, directCanonicalReject = 188, causallyProvenUnreachable = 22,
                conflictingObjectAbsent = 5, unresolved = complete ? 0 : 215 - finalCases.Length },
            runtime = new { controlHardViolations = runtime.GetProperty("controlHardViolations").GetInt32(),
                treatmentHardViolations = runtime.GetProperty("treatmentHardViolations").GetInt32(),
                treatmentOnlyHardViolations = runtime.GetProperty("treatmentOnlyHardViolations").GetInt32(),
                unexplainedStateDivergences = runtime.GetProperty("unexplainedStateDivergences").GetInt32(),
                pairsWithReconvergence = runtime.GetProperty("pairsWithReconvergence").GetInt32(),
                gateRngCalls = runtime.GetProperty("gateRngCalls").GetInt32() },
            validation = new { defaultEquivalenceFailures = validation.GetProperty("defaultEquivalenceFailures").GetInt32(),
                determinismFailures = validation.GetProperty("determinismFailures").GetInt32(),
                reparseFailures = validation.GetProperty("reparseFailures").GetInt32(),
                g1EvidenceFailures = validation.GetProperty("g1EvidenceFailures").GetInt32(),
                dMechanismsDemonstrated = followupOutcome == "FIVE_MECHANISMS_DEMONSTRATED" ? 5 : 0 },
            componentOutcomes = new { hardeningOutcome, followupOutcome }, outcome,
            boundaries = new { engineBehaviorChanged = false, defaultsChanged = false,
                g1Promoted = false, successorAuthorized = false }
        };
        File.WriteAllText(Path.Combine(outputDirectory,
            "safety_remediation_gate_final_recertification_summary.json"),
            JsonSerializer.Serialize(summary, Json) + Environment.NewLine, new UTF8Encoding(false));
        return outcome;
    }

    private static ImmutableArray<FinalRecertificationCase> MergeCases(string casesPath,
        string dPath, string outputPath)
    {
        using var dDocument = JsonDocument.Parse(File.ReadAllText(dPath));
        var d = dDocument.RootElement.GetProperty("cases").EnumerateArray().Select(x =>
            new FinalRecertificationCaseIdentity(x.GetProperty("chartId").GetString()!,
                x.GetProperty("seed").GetInt32(), x.GetProperty("opportunityKey").GetString()!)).ToArray();
        using var parser = new TextFieldParser(casesPath) { TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        var header = parser.ReadFields() ?? throw new InvalidDataException("Missing final case header.");
        var index = header.Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);
        var rows = new List<string[]>();
        var inputs = new List<(FinalRecertificationCaseIdentity, string)>();
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields()!;
            rows.Add(row);
            inputs.Add((new(row[index["chart_id"]], int.Parse(row[index["seed"]]),
                row[index["opportunity_key"]]), row[index["hardened_class"]]));
        }
        var final = SafetyRemediationFinalRecertificationResearch.Finalize(inputs, d);
        using var writer = new StreamWriter(outputPath, false, new UTF8Encoding(false));
        writer.WriteLine(string.Join(',', header.Select(Csv)) + ",final_class");
        for (var i = 0; i < rows.Count; i++)
            writer.WriteLine(string.Join(',', rows[i].Select(Csv)) + ',' + final[i].FinalClass);
        return final;
    }

    private static string Snapshot(string root, IEnumerable<string> paths) =>
        ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(paths.Select(path =>
            new NamedImplementationContent(path, File.ReadAllBytes(Path.Combine(root,
                path.Replace('/', Path.DirectorySeparatorChar))))));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string GitHead(string root)
    {
        var head = File.ReadAllText(Path.Combine(root, ".git", "HEAD")).Trim();
        if (!head.StartsWith("ref: ", StringComparison.Ordinal)) return head;
        return File.ReadAllText(Path.Combine(root, ".git", head[5..].Replace('/', Path.DirectorySeparatorChar))).Trim();
    }
    private static string Csv(string value) => value.IndexOfAny([',', '"', '\r', '\n']) >= 0
        ? '"' + value.Replace("\"", "\"\"") + '"' : value;

    private sealed record ArmConfiguration(string Control, string Treatment);
    private sealed record Denominators(int PrimaryPairs, int G1CompatibilityPairs, int TotalPairs,
        int FrozenKnownCases, int AdditionalCases, int HistoricalCases);
    private sealed record FinalContract(string SchemaVersion, string RepositoryEntryHead,
        string ImplementationSnapshotSha256, string HarnessSnapshotSha256, string CorpusManifestSha256,
        string HardeningContractSha256, string DFollowupContractSha256, string ExecutionMemoryLimit,
        ImmutableArray<string> Charts, ImmutableArray<int> PrimarySeeds,
        ImmutableArray<string> G1CompatibilityPairs, ArmConfiguration Arms, Denominators Denominators,
        string CausalDefinition, string SufficientStateDefinition, string ReconvergenceDefinition,
        string HardValidityOracle, ImmutableArray<string> RequiredTestsAndControls,
        ImmutableArray<string> StopConditions, ImmutableArray<string> ExpectedArtifacts);
    private sealed record FinalContractArtifact(FinalContract Contract, string CanonicalSha256,
        string Canonicalization);
}
