using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;

internal sealed record Lane0IntegrationSemanticExpectations(
    Lane0IntegrationRuntimeIdentities Identities,
    string ApprovedPublicHead,
    string ExpectedOutcome,
    Lane0IntegrationAdmittedCorpus AdmittedCorpus,
    bool DefaultChanged);

internal interface ILane0IntegrationDeepSemanticVerifier
{
    void Verify(ImmutableSortedDictionary<string, byte[]> package,
        Lane0IntegrationSemanticExpectations expected);
}

/// <summary>
/// Independent reader for the eight successor artifacts. It does not call the package builder or
/// its verifier and fails closed on byte, schema or cross-artifact semantic disagreement.
/// </summary>
internal sealed class Lane0CorrectiveSuccessorDeepSemanticPackageVerifier
    : ILane0IntegrationDeepSemanticVerifier
{
    private static readonly ImmutableArray<string> RequiredArtifacts =
    [
        "chart_family_keymode_results.csv", "execution_identity.json",
        "g1_historical_operational_cases.csv", "g1_state_transitions.csv", "integrity.json",
        "scientific_summary.json", "sha256sums.txt", "successor_references.json"
    ];

    public void Verify(ImmutableSortedDictionary<string, byte[]> package,
        Lane0IntegrationSemanticExpectations expected)
    {
        Require(package.Keys.SequenceEqual(RequiredArtifacts), "Artifact inventory drifted.");
        VerifyChecksums(package);
        Require(!expected.DefaultChanged, "Defaults changed.");
        VerifyAdmittedCorpus(expected.AdmittedCorpus);

        using var identity = ParseJson(package, "execution_identity.json");
        using var summary = ParseJson(package, "scientific_summary.json");
        using var integrity = ParseJson(package, "integrity.json");
        using var references = ParseJson(package, "successor_references.json");
        var i = identity.RootElement;
        var s = summary.RootElement;
        var g = integrity.RootElement;
        var r = references.RootElement;

        Require(String(i, "approvedPublicHead") == expected.ApprovedPublicHead, "Approved HEAD drifted.");
        Require(String(i, "phase1ContractSha256") == expected.Identities.Phase1ContractSha256,
            "Phase 1 identity drifted.");
        Require(String(i, "phase2ContractSha256") == expected.Identities.Phase2ContractSha256,
            "Phase 2 identity drifted.");
        Require(String(i, "integrationPreregistrationSha256") ==
            expected.Identities.IntegrationPreregistrationSha256,
            "Integration preregistration identity drifted.");
        Require(String(i, "integrationExecutionContractSha256") ==
            expected.Identities.IntegrationExecutionContractSha256,
            "Integration execution contract identity drifted.");
        Require(String(i, "manifestSha256") == expected.Identities.ManifestSha256,
            "Manifest identity drifted.");
        Require(String(i, "scientificImplementationSha256") == expected.Identities.ScientificImplementationSha256,
            "Scientific implementation identity drifted.");
        Require(String(i, "harnessSha256") == expected.Identities.Phase2HarnessSha256,
            "Phase 2 harness identity drifted.");
        Require(String(i, "packageVerifierSha256") == expected.Identities.Phase2PackageVerifierSha256,
            "Phase 2 package verifier identity drifted.");
        Require(String(i, "frozenDependenciesSha256") == expected.Identities.FrozenDependenciesSha256,
            "Frozen dependency identity drifted.");
        Require(String(i, "adapterSha256") == expected.Identities.AdapterSha256
            && String(i, "runnerSha256") == expected.Identities.RunnerSha256
            && String(i, "launcherSha256") == expected.Identities.LauncherSha256
            && String(i, "semanticVerifierSha256") == expected.Identities.SemanticVerifierSha256,
            "Integration runtime component identity drifted.");
        Require(!Bool(i, "defaultChanged") && !expected.DefaultChanged, "Defaults changed.");

        var identityOutcome = String(i, "scientificOutcome");
        var summaryOutcome = String(s, "outcome");
        var integrityOutcome = String(g, "outcome");
        Require(identityOutcome == expected.ExpectedOutcome && summaryOutcome == expected.ExpectedOutcome
            && integrityOutcome == expected.ExpectedOutcome, "Outcome classification disagrees across artifacts.");
        Require(Int(s, "researchRngCalls") == 0 && Int(g, "researchRngCalls") == 0,
            "Research RNG count is nonzero.");
        Require(Bool(s, "inputsUnchanged") && Bool(g, "inputsUnchanged"), "Inputs changed.");
        Require(!Bool(s, "behaviorChanged") && !Bool(g, "behaviorChanged"), "Behavior changed.");
        Require(EmptyArray(s, "integrityFailures") && EmptyArray(g, "integrityFailures"),
            "Integrity failures are present.");
        Require(EmptyArray(s, "blockedReasons") && EmptyArray(g, "blockedReasons"),
            "Blocked reasons are present.");

        VerifyReferences(r);
        VerifyChartAggregates(s, ReadCsv(package, "chart_family_keymode_results.csv"));
        VerifyG1Aggregates(s, ReadCsv(package, "g1_historical_operational_cases.csv"),
            ReadCsv(package, "g1_state_transitions.csv"));
    }

    private static void VerifyChecksums(ImmutableSortedDictionary<string, byte[]> package)
    {
        var lines = Encoding.UTF8.GetString(package["sha256sums.txt"])
            .Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Require(lines.Length == RequiredArtifacts.Length - 1, "Checksum inventory count drifted.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var separator = line.IndexOf("  ", StringComparison.Ordinal);
            Require(separator == 64, "Malformed checksum inventory row.");
            var hash = line[..separator];
            var name = line[(separator + 2)..];
            Require(name != "sha256sums.txt" && package.ContainsKey(name) && seen.Add(name),
                "Checksum inventory membership drifted.");
            Require(hash == Convert.ToHexString(SHA256.HashData(package[name])),
                $"Checksum mismatch for {name}.");
        }
        Require(package.Keys.Where(x => x != "sha256sums.txt").All(seen.Contains),
            "Checksum inventory is incomplete.");
    }

    private static void VerifyAdmittedCorpus(Lane0IntegrationAdmittedCorpus admitted)
    {
        Require(admitted.LocationEvidence.Length == 12 && admitted.ScientificCharts.Length == 11,
            "Duplicate path/content semantics drifted.");
        Require(admitted.UniqueOriginalObjects == 50_836, "Original object total drifted.");
        var groups = admitted.LocationEvidence.GroupBy(x => x.ChartId, StringComparer.Ordinal).ToArray();
        Require(groups.Length == 11, "Unique admitted content count drifted.");
        Require(groups.Single(x => x.Key == Lane0CorrectiveSuccessorFrozenC11Adapter.DuplicateChartId).Count() == 2
            && groups.Where(x => x.Key != Lane0CorrectiveSuccessorFrozenC11Adapter.DuplicateChartId)
                .All(x => x.Count() == 1), "Exact duplicate relationship drifted.");
    }

    private static void VerifyReferences(JsonElement root)
    {
        Require(String(root, "historicalInvalidSpringB") ==
            Lane0CorrectiveSuccessorReferenceValidator.HistoricalInvalidSpringIdentity,
            "Historical invalid Spring reference drifted.");
        Require(String(root, "historicalDistribution") == "9+2=11", "G1 distribution marker drifted.");
        var active = root.GetProperty("activeReferences").EnumerateArray().ToArray();
        Require(active.Length == 2, "Active successor reference count drifted.");
        var counts = active.ToDictionary(x => String(x, "chartId"),
            x => Int(x, "historicallySupportedCount"), StringComparer.Ordinal);
        Require(counts.Count == 2
            && counts.GetValueOrDefault(Lane0CorrectiveSuccessorReferenceValidator.SecondHistoricalIdentity) == 9
            && counts.GetValueOrDefault(Lane0CorrectiveSuccessorReferenceValidator.CorrectedSpringIdentity) == 2,
            "Corrected Spring/second-chart 9+2 references drifted.");
        Require(!counts.ContainsKey(Lane0CorrectiveSuccessorReferenceValidator.HistoricalInvalidSpringIdentity),
            "Historical invalid Spring B became active.");
    }

    private static void VerifyChartAggregates(JsonElement summary, string[][] csv)
    {
        Require(csv.Length >= 1 && csv[0].SequenceEqual(new[]
        {
            "chart_id", "keymode", "family", "historical_structural", "historical_joint_support",
            "historical_operational", "historical_operational_supported", "corrected_structural",
            "corrected_joint_support", "corrected_operational", "corrected_operational_supported",
            "corrected_integrity_failures"
        }), "Chart aggregate CSV header drifted.");
        var charts = summary.GetProperty("charts").EnumerateArray().ToArray();
        Require(csv.Length - 1 == charts.Length, "Chart aggregate row count disagrees with summary.");
        var summaryKeys = charts.Select(x => $"{String(x, "chartId")}|{Int(x, "keyCount")}|{String(x, "family")}")
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var csvKeys = csv.Skip(1).Select(x =>
        {
            Require(x.Length == 12, "Malformed chart aggregate row.");
            return $"{x[0]}|{x[1]}|{x[2]}";
        }).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Require(summaryKeys.SequenceEqual(csvKeys), "Chart family/keymode totals disagree with summary.");
    }

    private static void VerifyG1Aggregates(JsonElement summary, string[][] cases, string[][] transitions)
    {
        Require(cases.Length == 12, "Historical operational G1 case count must equal eleven.");
        Require(cases.Skip(1).All(x => x.Length == 6 && x[1] == "7"), "Malformed G1 case row.");
        var distribution = cases.Skip(1).GroupBy(x => x[0], StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        Require(distribution.Count == 2
            && distribution.GetValueOrDefault(Lane0CorrectiveSuccessorReferenceValidator.SecondHistoricalIdentity) == 9
            && distribution.GetValueOrDefault(Lane0CorrectiveSuccessorReferenceValidator.CorrectedSpringIdentity) == 2,
            "G1 case CSV does not preserve exact 9+2.");
        Require(summary.GetProperty("historicalOperationalG1Cases").GetArrayLength() == 11,
            "G1 case summary count drifted.");
        Require(transitions.Length - 1 == summary.GetProperty("g1Transitions").GetArrayLength(),
            "G1 transition CSV and summary disagree.");
        Require(transitions.Skip(1).All(x => x.Length == 11), "Malformed G1 transition row.");
    }

    private static JsonDocument ParseJson(ImmutableSortedDictionary<string, byte[]> package, string name)
    {
        try { return JsonDocument.Parse(package[name]); }
        catch (JsonException exception) { throw new InvalidDataException($"Malformed {name}.", exception); }
    }

    private static string[][] ReadCsv(ImmutableSortedDictionary<string, byte[]> package, string name) =>
        Encoding.UTF8.GetString(package[name]).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(',')).ToArray();
    private static string String(JsonElement root, string name) =>
        root.GetProperty(name).GetString() ?? throw new InvalidDataException($"{name} is null.");
    private static int Int(JsonElement root, string name) => root.GetProperty(name).GetInt32();
    private static bool Bool(JsonElement root, string name) => root.GetProperty(name).GetBoolean();
    private static bool EmptyArray(JsonElement root, string name) =>
        root.GetProperty(name).ValueKind == JsonValueKind.Array
        && root.GetProperty(name).GetArrayLength() == 0;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
