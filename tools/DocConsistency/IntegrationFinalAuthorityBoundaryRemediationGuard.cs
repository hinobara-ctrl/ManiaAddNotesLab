using System.Text.Json;

namespace DocConsistencyTool;

public static class IntegrationFinalAuthorityBoundaryRemediationGuard
{
    public const string ContractIdentity =
        "B4489D605EFF3E0AB2DF2768CAE2530DBA071E7AC4EF4009AEE7383E764AEFF0";
    public const string AdapterIdentity =
        "032F9660D8C0012C990983CE307CDA9D20B1C49116BEAA7F3C5E3DB1EBD875B0";
    public const string RunnerIdentity =
        "32C600B809F0B021C3806FDBC2BE58656C6DA27B5CE053E4E625AC9A175D72BF";
    public const string AuthorityIdentity =
        "CA6C9317E346A8E4FF229CED02AE3B92803C50A41C0B9AF953F5342C014E9024";
    public const string VerifierIdentity =
        "67969A60704553AA6CFB134F8B7F67236757EA6A2CF51C9CD7C4EF3C8FA5A4DB";

    public static IReadOnlyList<string> Validate(JsonElement artifact, string repositoryRoot)
    {
        var errors = new List<string>();
        try
        {
            var contract = artifact.GetProperty("contract");
            Expect(errors, IntegrationImplementationGuard.CanonicalJsonHash(contract) == ContractIdentity,
                "final authority-boundary canonical identity drifted");
            Expect(errors, String(artifact, "canonicalSha256") == ContractIdentity,
                "final authority-boundary declared identity drifted");
            Expect(errors, String(contract, "schemaVersion") ==
                "lane-0-corrective-successor-integration-final-authority-boundary-remediation.1",
                "schema drifted");
            Expect(errors, String(contract, "phaseId") ==
                "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_FINAL_AUTHORITY_BOUNDARY_REMEDIATION",
                "phase drifted");
            Expect(errors, String(contract, "baselinePublicHead") ==
                "1443cf17eb50938c8f3e1ebd18a9d521ac9e409b", "baseline drifted");
            Expect(errors, String(contract, "parentRemediationContractSha256") ==
                IntegrationPrebindingAuditRemediationGuard.ContractIdentity, "parent drifted");
            Expect(errors, String(contract, "priorReauditVerdict") ==
                "PREBINDING_REAUDIT_REQUIRES_REMEDIATION_BEFORE_BINDING", "reaudit drifted");
            var findings = contract.GetProperty("findingDispositions");
            foreach (var id in new[] { "FB-R1", "FB-R2", "FB-R3", "FB-R4", "FB-R5" })
                Expect(errors, String(findings, id).StartsWith("CLOSED_", StringComparison.Ordinal),
                    $"{id} disposition drifted");
            var official = contract.GetProperty("officialCapability");
            Expect(errors, String(official, "workerMode") == "--official-run"
                && !official.GetProperty("standaloneAuthorizeMode").GetBoolean()
                && !official.GetProperty("standaloneExecuteMode").GetBoolean()
                && official.GetProperty("corpusDeliveredOnlyAfterReceiptHandshake").GetBoolean()
                && !official.GetProperty("callerSuppliedCharts").GetBoolean()
                && !official.GetProperty("callerSuppliedOfficialOutputPaths").GetBoolean()
                && String(official, "receiptNamespace") ==
                    "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION.ONE_SHOT",
                "official capability boundary drifted");
            var synthetic = contract.GetProperty("syntheticSeparation");
            Expect(errors, String(synthetic, "workerMode") == "--synthetic-run"
                && !synthetic.GetProperty("officialAuthorityNamespaceAccessible").GetBoolean(),
                "synthetic separation drifted");
            var components = contract.GetProperty("currentComponents");
            ValidateFrozen(errors, components, "officialFrozenC11Adapter", AdapterIdentity);
            ValidateFrozen(errors, components, "officialDependencySealedRunner", RunnerIdentity);
            ValidateFrozen(errors, components, "unifiedAuthorityRuntimeWorker", AuthorityIdentity);
            ValidateFrozen(errors, components, "deepSemanticPackageVerifier", VerifierIdentity);
            var order = contract.GetProperty("authorityOrder").EnumerateArray()
                .Select(x => x.GetString()).ToArray();
            Expect(errors, order.Length == 21 && order[12] == "DURABLE_RECEIPT_CREATION"
                && order[13] == "CORPUSROOT_INTERPRETATION" && order[15] == "CORPUS_ADMISSION"
                && order[16] == "SCIENTIFIC_PASS_1", "authority order drifted");
            var boundary = contract.GetProperty("phaseBoundary");
            Expect(errors, String(boundary, "status") == "COMPLETE"
                && String(boundary, "outcome") == "READY_FOR_FINAL_INDEPENDENT_PREBINDING_REAUDIT"
                && String(boundary, "authority") ==
                    "AUTHORIZED_FOR_FINAL_AUTHORITY_BOUNDARY_REMEDIATION_IMPLEMENTATION_ONLY"
                && String(boundary, "c11Authorization") == "NO_C11_AUTHORIZATION"
                && String(boundary, "bindingAuthorization") == "NO_BINDING_AUTHORIZATION"
                && String(boundary, "receiptAuthorization") == "NO_RECEIPT_AUTHORIZATION"
                && String(boundary, "executionAuthorization") == "NO_EXECUTION_AUTHORIZATION"
                && !boundary.GetProperty("c11Accessed").GetBoolean()
                && !boundary.GetProperty("bindingCreated").GetBoolean()
                && !boundary.GetProperty("realReceiptCreated").GetBoolean()
                && !boundary.GetProperty("realExecution").GetBoolean()
                && !boundary.GetProperty("behaviorChange").GetBoolean()
                && !boundary.GetProperty("rngChange").GetBoolean()
                && !boundary.GetProperty("defaultChange").GetBoolean()
                && !boundary.GetProperty("productExposure").GetBoolean()
                && String(boundary, "nextRequiredAction") ==
                    "FINAL_INDEPENDENT_PREBINDING_REAUDIT", "phase boundary drifted");
        }
        catch (Exception exception)
        {
            errors.Add($"final authority-boundary contract malformed: {exception.Message}");
        }
        return errors;
    }

    public static IReadOnlyList<string> ValidateAgainstLive(
        JsonElement artifact, string repositoryRoot)
    {
        var errors = new List<string>();
        try
        {
            var components = artifact.GetProperty("contract").GetProperty("currentComponents");
            ValidateLive(errors, components, repositoryRoot,
                "officialFrozenC11Adapter", AdapterIdentity);
            ValidateLive(errors, components, repositoryRoot,
                "officialDependencySealedRunner", RunnerIdentity);
            ValidateLive(errors, components, repositoryRoot,
                "unifiedAuthorityRuntimeWorker", AuthorityIdentity);
            ValidateLive(errors, components, repositoryRoot,
                "deepSemanticPackageVerifier", VerifierIdentity);
        }
        catch (Exception exception)
        {
            errors.Add($"final authority-boundary live comparison malformed: {exception.Message}");
        }
        return errors;
    }

    private static void ValidateFrozen(List<string> errors, JsonElement components,
        string name, string expected) =>
        Expect(errors, String(components.GetProperty(name), "normalizedTextTreeSha256") == expected,
            $"{name} declared identity drifted");

    private static void ValidateLive(List<string> errors, JsonElement components, string root,
        string name, string expected)
    {
        var component = components.GetProperty(name);
        var files = component.GetProperty("files").EnumerateArray()
            .Select(x => x.GetString() ?? "").ToArray();
        Expect(errors, IntegrationImplementationGuard.NormalizedTextTreeIdentity(root, files) == expected,
            $"{name} live identity drifted");
    }

    private static string String(JsonElement parent, string name) =>
        parent.GetProperty(name).GetString() ?? throw new InvalidDataException($"{name} is null.");
    private static void Expect(List<string> errors, bool condition, string message)
    { if (!condition) errors.Add(message); }
}
