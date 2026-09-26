using System.Security.Cryptography;
using System.Text.Json;

namespace DocConsistencyTool;

public static class SafetyRemediationFollowupGuard
{
    public const string ContractSha256 = "645DAD6346130FBF0D4753053A35A40890A33FEE8F7F928768B8DDE9F4701083";
    public const string ImplementationSha256 = "B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835";
    public const string HistoricalHarnessSha256 = "31FC70DE3C856D4E4EB9046520169C2A9E5899AE6E82777C390D366A3EE6E44F";
    public const string CorpusManifestSha256 = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    public const string RepositoryEntryHead = "62615bbf5874bed04a7cdd7dc1fba4b43f31ea45";
    public const string Classification = "D_CONFLICTING_OBJECT_ABSENT";

    private sealed record ExpectedCase(string ChartId, int Seed, string OpportunityKey,
        string Target, string Blocker, string Geometry, string BlockerCommit, int CommitOrder,
        string LineageId, string GovernedOrigin);

    private static readonly ExpectedCase[] ExpectedCases =
    [
        new("02D9D1781418E7442956D4D901C8C611E58256556AE9A828E72128C3B5B8E3EB", 8,
            "OP-00004470-BaseHead-S4470-T222416-ANA", "1|222416|TAP|Tap|4470|HeadOpportunity",
            "1|222331|222416|LongNote|4468|HeadOpportunity",
            "L1:LongNote:651.24986666666649300003555556:651.49919999999982626688:222331:222416",
            "OP-00004468-BaseHead-S4468-T222331-ANA", 4552,
            "SRG-DIV-548ED9D227B29F519914AEE3", "OP-00003072-BaseHead-S3072-T163268-ANA"),
        new("20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788", 3,
            "OP-00004664-BaseHead-S4664-T242439-ANA", "6|242439|TAP|Tap|4664|HeadOpportunity",
            "6|242351|242439|LongNote|4663|HeadOpportunity",
            "L6:LongNote:565.94705000000023573652750001:566.19638333333356923608305557:242351:242439",
            "OP-00004663-BaseHead-S4663-T242351-ANA", 4695,
            "SRG-DIV-BE75AFE7197441CC21BBB903", "OP-00000370-BaseHead-S370-T29933-ANA"),
        new("20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788", 6,
            "OP-00001515-BaseHead-S1515-T89821-ANA", "6|89821|TAP|Tap|1515|HeadOpportunity",
            "6|89595|89821|LongNote|1510|HeadOpportunity",
            "L6:LongNote:195.49891666666674486623333333:195.99988333333341173328666667:89595:89821",
            "OP-00001510-BaseHead-S1510-T89595-ANA", 1512,
            "SRG-DIV-35ABEC50B3D9E55C1F4A852D", "OP-00000370-BaseHead-S370-T29933-ANA"),
        new("20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788", 7,
            "OP-00001936-BaseHead-S1936-T110572-ANA", "5|110572|TAP|Tap|1936|HeadOpportunity",
            "5|110460|110572|LongNote|1934|HeadOpportunity",
            "L5:LongNote:241.74966666666676336653333333:241.99793333333343013250666667:110460:110572",
            "OP-00001934-BaseHead-S1934-T110460-ANA", 1936,
            "SRG-DIV-6E6075896ACA1A85FFD77979", "OP-00000011-BaseHead-S11-T2527-ANA"),
        new("20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788", 13,
            "OP-00006173-BaseHead-S6173-T296968-ANA", "0|296968|TAP|Tap|6173|HeadOpportunity",
            "0|296880|296968|LongNote|6170|HeadOpportunity",
            "L0:LongNote:720.44588333333367206908305557:720.69521666666700556863861112:296880:296968",
            "OP-00006170-BaseHead-S6170-T296880-ANA", 6208,
            "SRG-DIV-06A25634BF670F3AB9325F59", "OP-00001936-BaseHead-S1936-T110572-ANA")
    ];

    public static IReadOnlyList<string> Validate(string contractPath, string followupPath)
    {
        var errors = new List<string>();
        try
        {
            using var contractDocument = JsonDocument.Parse(File.ReadAllText(contractPath));
            ValidateContract(contractDocument.RootElement, errors);
        }
        catch (Exception exception)
        {
            errors.Add($"follow-up contract cannot be validated: {exception.Message}");
        }
        try
        {
            using var followupDocument = JsonDocument.Parse(File.ReadAllText(followupPath));
            ValidateFollowup(followupDocument.RootElement, errors);
        }
        catch (Exception exception)
        {
            errors.Add($"unresolved follow-up cannot be validated: {exception.Message}");
        }
        return errors;
    }

    private static void ValidateContract(JsonElement root, List<string> errors)
    {
        RequireShape(root, "follow-up contract artifact", "contract", "canonicalSha256");
        var contract = root.GetProperty("contract");
        RequireShape(contract, "follow-up contract", "schemaVersion", "scope", "repositoryEntryHead",
            "implementationSnapshotSha256", "harnessSnapshotSha256", "corpusManifestSha256",
            "executionMemoryLimit", "focusedPairCount", "fullMatrixAuthorized",
            "historicalClassificationRule", "prohibitedChanges");
        var actual = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contract)));
        Expect(errors, actual == ContractSha256, "contract canonical content hash drifted");
        Expect(errors, String(root, "canonicalSha256") == ContractSha256, "contract declared hash drifted");
        Expect(errors, String(contract, "schemaVersion") == "safety-remediation-gate-followup-contract.1", "contract schema drifted");
        Expect(errors, String(contract, "scope") == "SAFETY.REMEDIATION.GATE focused classifier/C11/unresolved follow-up", "contract scope drifted");
        Expect(errors, String(contract, "repositoryEntryHead") == RepositoryEntryHead, "contract repository entry identity drifted");
        Expect(errors, String(contract, "implementationSnapshotSha256") == ImplementationSha256, "contract implementation identity drifted");
        Expect(errors, String(contract, "harnessSnapshotSha256") == HistoricalHarnessSha256, "contract harness identity drifted");
        Expect(errors, String(contract, "corpusManifestSha256") == CorpusManifestSha256, "contract corpus identity drifted");
        Expect(errors, String(contract, "executionMemoryLimit") == "0x400000000", "contract memory limit drifted");
        Expect(errors, contract.GetProperty("focusedPairCount").GetInt32() == 5, "contract focused pair count drifted");
        Expect(errors, !contract.GetProperty("fullMatrixAuthorized").GetBoolean(), "contract matrix authorization drifted");
        Expect(errors, String(contract, "historicalClassificationRule") ==
            "Historical 188/22/5 and NEEDS_REVIEW remain immutable; follow-up mechanisms are additive.",
            "contract historical preservation rule drifted");
        Expect(errors, String(contract, "prohibitedChanges") ==
            "No defaults, remediation, G1, lane selection, AddChance, HardValidity or canonical authority changes.",
            "contract prohibited-change boundary drifted");
    }

    private static void ValidateFollowup(JsonElement root, List<string> errors)
    {
        RequireShape(root, "unresolved follow-up", "schemaVersion", "contractSha256",
            "implementationSnapshotSha256", "harnessSnapshotSha256", "repositoryEntryHead",
            "historicalState", "corpus", "execution", "result", "cases");
        Expect(errors, String(root, "schemaVersion") == "safety-remediation-gate-unresolved-followup.1", "follow-up schema drifted");
        Expect(errors, String(root, "contractSha256") == ContractSha256, "follow-up contract identity drifted");
        Expect(errors, String(root, "implementationSnapshotSha256") == ImplementationSha256, "follow-up implementation identity drifted");
        Expect(errors, String(root, "harnessSnapshotSha256") == HistoricalHarnessSha256, "follow-up harness identity drifted");
        Expect(errors, String(root, "repositoryEntryHead") == RepositoryEntryHead, "follow-up repository entry identity drifted");

        var historical = root.GetProperty("historicalState");
        RequireShape(historical, "historical state", "direct", "causallyUnreachable", "unresolved",
            "outcome", "retrospectiveClassificationChanged");
        Expect(errors, historical.GetProperty("direct").GetInt32() == 188, "historical direct count drifted");
        Expect(errors, historical.GetProperty("causallyUnreachable").GetInt32() == 22, "historical unreachable count drifted");
        Expect(errors, historical.GetProperty("unresolved").GetInt32() == 5, "historical unresolved count drifted");
        Expect(errors, String(historical, "outcome") == "NEEDS_REVIEW", "historical NEEDS_REVIEW result drifted");
        Expect(errors, !historical.GetProperty("retrospectiveClassificationChanged").GetBoolean(), "historical classifications were rewritten");

        var corpus = root.GetProperty("corpus");
        RequireShape(corpus, "follow-up corpus", "manifest", "uniqueCharts", "physicalLocations", "exactDuplicateHashes");
        Expect(errors, String(corpus, "manifest") == CorpusManifestSha256, "follow-up corpus identity drifted");
        Expect(errors, corpus.GetProperty("uniqueCharts").GetInt32() == 11, "follow-up unique chart count drifted");
        Expect(errors, corpus.GetProperty("physicalLocations").GetInt32() == 12, "follow-up physical location count drifted");
        Expect(errors, corpus.GetProperty("exactDuplicateHashes").GetInt32() == 1, "follow-up duplicate count drifted");

        var execution = root.GetProperty("execution");
        RequireShape(execution, "follow-up execution", "pairs", "executionMemoryLimit",
            "fullMatrixExecuted", "defaultsChanged", "hardValidityChanged");
        Expect(errors, execution.GetProperty("pairs").GetInt32() == 5, "follow-up pair count drifted");
        Expect(errors, String(execution, "executionMemoryLimit") == "0x400000000", "follow-up memory limit drifted");
        Expect(errors, !execution.GetProperty("fullMatrixExecuted").GetBoolean(), "follow-up unexpectedly claims a full matrix");
        Expect(errors, !execution.GetProperty("defaultsChanged").GetBoolean(), "follow-up defaults boundary drifted");
        Expect(errors, !execution.GetProperty("hardValidityChanged").GetBoolean(), "follow-up HardValidity boundary drifted");

        var result = root.GetProperty("result");
        RequireShape(result, "follow-up result", "causallyDemonstrated", "stillUnresolved", "proposedMechanisms");
        Expect(errors, result.GetProperty("causallyDemonstrated").GetInt32() == 5, "demonstrated count drifted");
        Expect(errors, result.GetProperty("stillUnresolved").GetInt32() == 0, "follow-up unresolved count drifted");
        var mechanisms = result.GetProperty("proposedMechanisms").EnumerateArray().Select(x => x.GetString()).ToArray();
        Expect(errors, mechanisms.SequenceEqual([Classification]), "follow-up result classification drifted");

        var cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Expect(errors, cases.Length == ExpectedCases.Length, "follow-up case count drifted");
        for (var i = 0; i < Math.Min(cases.Length, ExpectedCases.Length); i++)
            ValidateCase(cases[i], ExpectedCases[i], i, errors);
    }

    private static void ValidateCase(JsonElement item, ExpectedCase expected, int index, List<string> errors)
    {
        var label = $"follow-up case {index + 1}";
        RequireShape(item, label, "chartId", "seed", "opportunityKey", "historicalTarget",
            "controlReferenceExact", "treatmentDeterministic", "independentControlViolations",
            "independentTreatmentViolationAbsent", "targetHasActivePriorLineage", "conflictingObjects",
            "proposedMechanism", "causalMechanismDemonstrated", "conclusion");
        Expect(errors, String(item, "chartId") == expected.ChartId, $"{label} chart identity drifted");
        Expect(errors, item.GetProperty("seed").GetInt32() == expected.Seed, $"{label} seed identity drifted");
        Expect(errors, String(item, "opportunityKey") == expected.OpportunityKey, $"{label} opportunity identity drifted");
        Expect(errors, String(item, "historicalTarget") == expected.Target, $"{label} historical target drifted");
        Expect(errors, item.GetProperty("controlReferenceExact").GetBoolean(), $"{label} lost exact control reference");
        Expect(errors, item.GetProperty("treatmentDeterministic").GetBoolean(), $"{label} lost deterministic treatment");
        Expect(errors, item.GetProperty("independentControlViolations").GetInt32() == 1, $"{label} violation count drifted");
        Expect(errors, item.GetProperty("independentTreatmentViolationAbsent").GetBoolean(), $"{label} treatment absence drifted");
        Expect(errors, item.GetProperty("targetHasActivePriorLineage").GetBoolean(), $"{label} lineage state drifted");
        Expect(errors, String(item, "proposedMechanism") == Classification, $"{label} classification drifted");
        Expect(errors, item.GetProperty("causalMechanismDemonstrated").GetBoolean(), $"{label} causal result drifted");
        Expect(errors, String(item, "conclusion") ==
            "The historical tap commits identically, while every independently audited control blocker is absent or changed in treatment under an unreconverged canonical-governed lineage.",
            $"{label} conclusion drifted");

        var blockers = item.GetProperty("conflictingObjects").EnumerateArray().ToArray();
        Expect(errors, blockers.Length == 1, $"{label} blocker count drifted");
        if (blockers.Length == 0) return;
        var blocker = blockers[0];
        RequireShape(blocker, $"{label} blocker", "controlObject", "geometrySignature", "provenance",
            "controlCommitOpportunity", "controlCommitOrder", "treatmentCommitAtSameOpportunity",
            "treatmentExactObject", "treatmentSameProvenanceAlternatives", "exactObjectAbsentOrChanged",
            "lineageId", "governedOriginOpportunity", "governedLineageEstablished",
            "noReconvergenceBeforeTarget");
        Expect(errors, String(blocker, "controlObject") == expected.Blocker, $"{label} LN blocker identity drifted");
        Expect(errors, String(blocker, "geometrySignature") == expected.Geometry, $"{label} blocker geometry drifted");
        Expect(errors, String(blocker, "provenance") == "HeadOpportunity", $"{label} blocker provenance drifted");
        Expect(errors, String(blocker, "controlCommitOpportunity") == expected.BlockerCommit, $"{label} blocker commit drifted");
        Expect(errors, blocker.GetProperty("controlCommitOrder").GetInt32() == expected.CommitOrder, $"{label} blocker order drifted");
        Expect(errors, blocker.GetProperty("treatmentCommitAtSameOpportunity").ValueKind == JsonValueKind.Null, $"{label} treatment commit is no longer absent");
        Expect(errors, blocker.GetProperty("treatmentExactObject").ValueKind == JsonValueKind.Null, $"{label} exact treatment object is no longer absent");
        Expect(errors, !blocker.GetProperty("treatmentSameProvenanceAlternatives").EnumerateArray().Any(), $"{label} treatment alternatives drifted");
        Expect(errors, blocker.GetProperty("exactObjectAbsentOrChanged").GetBoolean(), $"{label} exact blocker absence drifted");
        Expect(errors, String(blocker, "lineageId") == expected.LineageId, $"{label} lineage identity drifted");
        Expect(errors, String(blocker, "governedOriginOpportunity") == expected.GovernedOrigin, $"{label} governed origin drifted");
        Expect(errors, blocker.GetProperty("governedLineageEstablished").GetBoolean(), $"{label} governed lineage proof drifted");
        Expect(errors, blocker.GetProperty("noReconvergenceBeforeTarget").GetBoolean(), $"{label} reconvergence proof drifted");
    }

    private static string String(JsonElement element, string property) =>
        element.GetProperty(property).GetString() ?? throw new InvalidDataException($"{property} is null.");

    private static void Expect(List<string> errors, bool condition, string error)
    {
        if (!condition) errors.Add(error);
    }

    private static void RequireShape(JsonElement element, string context, params string[] expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{context} must be an object.");
        var actual = element.EnumerateObject().Select(x => x.Name).ToArray();
        var missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
        var unexpected = actual.Except(expected, StringComparer.Ordinal).ToArray();
        if (missing.Length != 0 || unexpected.Length != 0 || actual.Length != expected.Length)
            throw new InvalidDataException($"{context} shape drifted; missing=[{string.Join(',', missing)}], "
                + $"unexpected=[{string.Join(',', unexpected)}].");
    }
}
