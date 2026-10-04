using System.Text.Json;

namespace DocConsistencyTool;

public static class IntegrationPrebindingAuditRemediationGuard
{
    public const string ContractIdentity =
        "41F47787C1B233797E988FE23B9CBB1167846A925834BF1B450DC512C2D1D4D2";
    public const string AdapterIdentity =
        "032F9660D8C0012C990983CE307CDA9D20B1C49116BEAA7F3C5E3DB1EBD875B0";
    public const string RunnerIdentity =
        "8B2845436C735B93D9F92D4BE89F5788FBDDE2D82B425124A7FB154E22A0AF39";
    public const string AuthorityIdentity =
        "E48A21353DA5692CB9752D8C5D378D9160550CD42FD6B16BD97AD7D148A49042";
    public const string VerifierIdentity =
        "67969A60704553AA6CFB134F8B7F67236757EA6A2CF51C9CD7C4EF3C8FA5A4DB";

    public static IReadOnlyList<string> Validate(JsonElement artifact, string repositoryRoot)
    {
        var errors = new List<string>();
        try
        {
            var contract = artifact.GetProperty("contract");
            Expect(errors, IntegrationImplementationGuard.CanonicalJsonHash(contract) == ContractIdentity,
                "prebinding audit remediation canonical identity drifted");
            Expect(errors, String(artifact, "canonicalSha256") == ContractIdentity,
                "prebinding audit remediation declared identity drifted");
            Expect(errors, String(contract, "schemaVersion") ==
                "lane-0-corrective-successor-integration-prebinding-audit-remediation.1", "schema drifted");
            Expect(errors, String(contract, "phaseId") ==
                "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_PREBINDING_AUDIT_REMEDIATION", "phase drifted");
            Expect(errors, String(contract, "baselinePublicHead") ==
                "fd6cfad9d2f6a1bab40719e1432a781079bf9444", "baseline drifted");
            Expect(errors, String(contract, "independentAuditResult") ==
                "PREBINDING_AUDIT_REQUIRES_REMEDIATION_BEFORE_BINDING", "audit result drifted");
            var lineage = contract.GetProperty("lineage");
            Expect(errors, String(lineage, "prebindingHardeningContractSha256") ==
                IntegrationPrebindingHardeningGuard.ContractIdentity, "hardening lineage drifted");
            Expect(errors, String(lineage, "integrationAuditRemediationContractSha256") ==
                IntegrationAuditRemediationGuard.ContractIdentity, "remediation lineage drifted");
            Expect(errors, String(lineage, "integrationExecutionContractSha256") ==
                IntegrationImplementationGuard.ContractIdentity, "execution lineage drifted");
            Expect(errors, String(lineage, "integrationPreregistrationSha256") ==
                IntegrationImplementationGuard.ParentIdentity, "preregistration lineage drifted");
            Expect(errors, String(lineage, "explicitPathAuthoritySha256") ==
                IntegrationAuditRemediationGuard.PathAuthorityIdentity, "path authority drifted");

            var findings = contract.GetProperty("findingDispositions");
            var a1 = findings.GetProperty("PB-A1");
            Expect(errors, String(a1, "disposition") ==
                "CONTRACT_CLARIFICATION_NOT_A_FINDING_WITHIN_FROZEN_THREAT_MODEL"
                && String(a1, "authorityScope") ==
                    "ONE_ATTEMPT_PER_EXPLICIT_SOURCE_AUTHORIZATION_ROOT"
                && a1.GetProperty("sourceAuthorizationRootAnchored").GetBoolean()
                && a1.GetProperty("sameRootConcurrencyCovered").GetBoolean()
                && !a1.GetProperty("distributedOrGlobalAuthorityClaimed").GetBoolean(),
                "PB-A1 disposition drifted");
            Expect(errors, String(findings.GetProperty("PB-A2"), "disposition") == "CLOSED",
                "PB-A2 disposition drifted");
            var a3 = findings.GetProperty("PB-A3");
            Expect(errors, String(a3, "disposition") == "CLOSED"
                && a3.GetProperty("verifiedPreReceipt").GetBoolean()
                && a3.GetProperty("verifiedImmediatelyPreScience").GetBoolean()
                && a3.GetProperty("verifiedPostScience").GetBoolean(), "PB-A3 disposition drifted");
            var a4 = findings.GetProperty("PB-A4");
            Expect(errors, String(a4, "disposition") == "CLOSED"
                && String(a4, "receiptNamespace") ==
                    "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION.ONE_SHOT"
                && a4.GetProperty("officialScienceRequiresCanonicalDurableReceipt").GetBoolean()
                && !a4.GetProperty("callerSuppliesReceiptPathOrNamespace").GetBoolean(),
                "PB-A4 disposition drifted");

            var components = contract.GetProperty("currentComponents");
            ValidateLive(errors, components, repositoryRoot, "officialFrozenC11Adapter", AdapterIdentity);
            ValidateLive(errors, components, repositoryRoot, "officialDependencySealedRunner", RunnerIdentity);
            ValidateLive(errors, components, repositoryRoot, "authorityRuntimeReceiptGatedWorker", AuthorityIdentity);
            ValidateLive(errors, components, repositoryRoot, "deepSemanticPackageVerifier", VerifierIdentity);

            var order = contract.GetProperty("authorityOrder").EnumerateArray()
                .Select(x => x.GetString()).ToArray();
            Expect(errors, order.SequenceEqual(new[] {
                "SOURCE_PUBLIC_HEAD_CHECKS", "CONTRACT_IDENTITIES", "RUNTIME_IDENTITIES",
                "CANONICAL_BINDING_PRESENCE", "CANONICAL_BINDING_BYTE_AND_HASH_VERIFICATION",
                "EXPLICIT_AUTHORIZATION_FIELD", "AUTHORIZED_EXECUTION_COUNT_ONE",
                "SOURCE_TRACKED_CLEANLINESS", "SOURCE_EXECUTION_RUNTIME_HEAD_EQUALITY",
                "SOURCE_EXECUTION_ROOT_ISOLATION", "DURABLE_RECEIPT_ABSENCE",
                "OUTPUT_AND_STAGING_ABSENCE", "DURABLE_RECEIPT_CREATION",
                "CORPUSROOT_INTERPRETATION", "CORPUS_SOURCE_EXECUTION_ROOT_ISOLATION",
                "CORPUS_ADMISSION", "SCIENTIFIC_PASS_1", "SCIENTIFIC_PASS_2",
                "BYTE_DETERMINISM", "DEEP_SEMANTIC_VERIFICATION",
                "FINAL_PUBLISHABLE_OR_NON_PUBLISHABLE_CLASSIFICATION" }), "authority order drifted");
            var boundary = contract.GetProperty("phaseBoundary");
            Expect(errors, String(boundary, "status") == "COMPLETE"
                && String(boundary, "outcome") == "READY_FOR_INDEPENDENT_PREBINDING_REAUDIT"
                && String(boundary, "authority") ==
                    "AUTHORIZED_FOR_PREBINDING_AUDIT_REMEDIATION_IMPLEMENTATION_ONLY"
                && String(boundary, "c11Authorization") == "NO_C11_AUTHORIZATION"
                && String(boundary, "bindingAuthorization") == "NO_BINDING_AUTHORIZATION"
                && String(boundary, "receiptAuthorization") == "NO_RECEIPT_AUTHORIZATION"
                && String(boundary, "executionAuthorization") == "NO_EXECUTION_AUTHORIZATION"
                && String(boundary, "productPromotionAuthorization") ==
                    "NO_PRODUCT_PROMOTION_AUTHORIZATION"
                && !boundary.GetProperty("c11Accessed").GetBoolean()
                && !boundary.GetProperty("bindingCreated").GetBoolean()
                && !boundary.GetProperty("realReceiptCreated").GetBoolean()
                && !boundary.GetProperty("realExecution").GetBoolean()
                && !boundary.GetProperty("behaviorChange").GetBoolean()
                && !boundary.GetProperty("rngChange").GetBoolean()
                && !boundary.GetProperty("defaultChange").GetBoolean()
                && !boundary.GetProperty("productExposure").GetBoolean()
                && String(boundary, "nextRequiredAction") == "INDEPENDENT_PREBINDING_REAUDIT",
                "phase boundary drifted");
        }
        catch (Exception exception)
        {
            errors.Add($"integration prebinding audit remediation contract malformed: {exception.Message}");
        }
        return errors;
    }

    public static IReadOnlyList<string> ValidateAgainstLive(JsonElement artifact, string repositoryRoot)
    {
        var errors = new List<string>();
        try
        {
            var components = artifact.GetProperty("contract").GetProperty("currentComponents");
            CompareLive(errors, components, repositoryRoot, "officialFrozenC11Adapter");
            CompareLive(errors, components, repositoryRoot, "officialDependencySealedRunner");
            CompareLive(errors, components, repositoryRoot, "authorityRuntimeReceiptGatedWorker");
            CompareLive(errors, components, repositoryRoot, "deepSemanticPackageVerifier");
        }
        catch (Exception exception)
        {
            errors.Add($"historical 41F477 live comparison malformed: {exception.Message}");
        }
        return errors;
    }

    private static void ValidateLive(List<string> errors, JsonElement components, string root,
        string name, string expected)
    {
        var component = components.GetProperty(name);
        var files = component.GetProperty("files").EnumerateArray()
            .Select(x => x.GetString() ?? "").ToArray();
        Expect(errors, String(component, "normalizedTextTreeSha256") == expected,
            $"{name} declared identity drifted");
    }

    private static void CompareLive(List<string> errors, JsonElement components, string root,
        string name)
    {
        var component = components.GetProperty(name);
        var files = component.GetProperty("files").EnumerateArray()
            .Select(x => x.GetString() ?? "").ToArray();
        var declared = String(component, "normalizedTextTreeSha256");
        if (IntegrationImplementationGuard.NormalizedTextTreeIdentity(root, files) != declared)
            errors.Add($"historical {name} live identity drifted as expected");
    }

    private static string String(JsonElement parent, string name) =>
        parent.GetProperty(name).GetString() ?? throw new InvalidDataException($"{name} is null.");
    private static void Expect(List<string> errors, bool condition, string message)
    {
        if (!condition) errors.Add(message);
    }
}
