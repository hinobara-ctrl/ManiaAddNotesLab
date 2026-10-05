using System.Text.Json;

namespace DocConsistencyTool;

public static class IntegrationHistoricalValidationPortabilityRemediationGuard
{
    public const string ContractIdentity =
        "4BC0C5B4DCE583B8CC9A42BB64FF11A2E180E68E20E52269B91861F29521A4E1";
    public const string ParentIdentity =
        "27BAFFB4B92E5B30A073E60673D8F2FEDB109FF70A69A23C5783D8D589693FDD";
    public const string AdapterIdentity =
        "032F9660D8C0012C990983CE307CDA9D20B1C49116BEAA7F3C5E3DB1EBD875B0";
    public const string RunnerIdentity =
        "32C600B809F0B021C3806FDBC2BE58656C6DA27B5CE053E4E625AC9A175D72BF";
    public const string AuthorityIdentity =
        "805F70466B503FEBBB14511E595A12E3AB42A583CED1A1D04193BF3DE04474DE";
    public const string VerifierIdentity =
        "67969A60704553AA6CFB134F8B7F67236757EA6A2CF51C9CD7C4EF3C8FA5A4DB";

    private const string HistoricalContractIdentity =
        "E0BCDA19B3E05E5ECA3EE8FCC7380C4AC74ACBDDE3241D77CD2F1B6EA6274F35";
    private const string HistoricalContractPath =
        "docs/lane_0_future_held_hardening_contract.json";

    public static IReadOnlyList<string> Validate(JsonElement artifact, string repositoryRoot)
    {
        var errors = new List<string>();
        try
        {
            var contract = artifact.GetProperty("contract");
            Expect(errors, IntegrationImplementationGuard.CanonicalJsonHash(contract) == ContractIdentity,
                "historical portability canonical identity drifted");
            Expect(errors, String(artifact, "canonicalSha256") == ContractIdentity,
                "historical portability declared identity drifted");
            Expect(errors, String(contract, "schemaVersion") ==
                "lane-0-corrective-successor-integration-historical-validation-portability-remediation.1",
                "schema drifted");
            Expect(errors, String(contract, "phaseId") ==
                "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_HISTORICAL_VALIDATION_PORTABILITY_REMEDIATION",
                "phase drifted");
            Expect(errors, String(contract, "baselinePublicHead") ==
                "9eaf82e8aa933ab730af107042884c591e597576", "baseline drifted");
            Expect(errors, String(contract, "parentRemediationContractSha256") == ParentIdentity,
                "parent remediation identity drifted");
            Expect(errors, String(contract, "priorAuditVerdict") ==
                "FINAL_PREBINDING_REAUDIT_BLOCKED", "prior audit verdict drifted");

            var finding = contract.GetProperty("rootFinding");
            Expect(errors, String(finding, "id") == "HV-P1"
                && String(finding, "classification") ==
                    "HISTORICAL_EXACT_BYTE_REUSED_DEPENDENCY_ASSERTION_NOT_PORTABLE_ACROSS_CHECKOUTS"
                && String(finding, "disposition") ==
                    "CLOSED_CURRENT_REGRESSION_TEST_NO_LONGER_REQUIRES_CHECKOUT_DEPENDENT_TRUE_VALUE",
                "HV-P1 disposition drifted");

            var historical = contract.GetProperty("historicalBoundary");
            Expect(errors, String(historical, "historicalContractPath") == HistoricalContractPath
                && String(historical, "historicalContractCanonicalSha256") == HistoricalContractIdentity
                && !historical.GetProperty("historicalContractModified").GetBoolean()
                && !historical.GetProperty("historicalValidatorModified").GetBoolean()
                && historical.GetProperty("historicalExactByteSemanticsPreserved").GetBoolean()
                && String(historical, "exactByteSemantics") ==
                    "file hashes use exact working-tree bytes"
                && String(historical, "lineEndingSensitivity") == "CRLF/LF changes identity"
                && !historical.GetProperty("currentCheckoutReusedDependenciesPortableInvariant").GetBoolean()
                && historical.GetProperty("reusedDependencyFalseStillInvalid").GetBoolean(),
                "historical exact-byte boundary drifted");
            ValidateHistoricalDeclaration(errors, repositoryRoot);

            var intent = contract.GetProperty("testIntent");
            Expect(errors, String(intent, "test") ==
                    "HistoricalV2RouteRejectsChangedInstrumentIdentity"
                && String(intent, "outcome") == "INVALID"
                && intent.GetProperty("contractCanonical").GetBoolean()
                && !intent.GetProperty("currentImplementationIdentityMatchesHistorical").GetBoolean()
                && !intent.GetProperty("currentHarnessIdentityMatchesHistorical").GetBoolean()
                && !intent.GetProperty("requiresReusedDependenciesTrue").GetBoolean(),
                "portable historical rejection intent drifted");

            var closures = contract.GetProperty("preservedAuditClosures");
            Expect(errors, String(closures, "AP-R1") == "CLOSED_SOURCE_ROOT_ONE_SHOT_ATOMICITY"
                && String(closures, "AP-R2") == "CLOSED_NO_HARDLINK_PROVENANCE_OWNERSHIP"
                && String(closures, "legacyInjectableAuthorityStoreSeam") == "NOT_A_FINDING"
                && String(closures, "finalArtifactTiming") == "NON_BLOCKING_PRESERVED"
                && String(closures, "staleRuntimeComment") ==
                    "DOCUMENTATION_ONLY_PRESERVED_TO_AVOID_RUNTIME_IDENTITY_CHURN",
                "preserved audit closure drifted");

            var components = contract.GetProperty("currentComponents");
            ValidateLive(errors, components, repositoryRoot, "officialFrozenC11Adapter", AdapterIdentity);
            ValidateLive(errors, components, repositoryRoot, "officialDependencySealedRunner", RunnerIdentity);
            ValidateLive(errors, components, repositoryRoot, "atomicAuthorityRuntimeWorker", AuthorityIdentity);
            ValidateLive(errors, components, repositoryRoot, "deepSemanticPackageVerifier", VerifierIdentity);

            var boundary = contract.GetProperty("phaseBoundary");
            Expect(errors, String(boundary, "status") == "COMPLETE"
                && String(boundary, "outcome") == "READY_FOR_FINAL_INDEPENDENT_PREBINDING_REAUDIT"
                && String(boundary, "authority") ==
                    "AUTHORIZED_FOR_HISTORICAL_VALIDATION_PORTABILITY_REMEDIATION_IMPLEMENTATION_ONLY"
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
                && String(boundary, "nextRequiredAction") ==
                    "FINAL_INDEPENDENT_PREBINDING_REAUDIT", "phase boundary drifted");
        }
        catch (Exception exception)
        {
            errors.Add($"historical portability contract malformed: {exception.Message}");
        }
        return errors;
    }

    private static void ValidateHistoricalDeclaration(List<string> errors, string root)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            HistoricalContractPath.Replace('/', Path.DirectorySeparatorChar))));
        var canonicalization = String(document.RootElement, "canonicalization");
        Expect(errors, String(document.RootElement, "canonicalSha256") == HistoricalContractIdentity,
            "historical hardening declared identity drifted");
        Expect(errors, canonicalization.Contains("file hashes use exact working-tree bytes",
                StringComparison.Ordinal)
            && canonicalization.Contains("CRLF/LF changes identity", StringComparison.Ordinal),
            "historical hardening exact-byte declaration drifted");
    }

    private static void ValidateLive(List<string> errors, JsonElement components, string root,
        string name, string expected)
    {
        var component = components.GetProperty(name);
        var files = component.GetProperty("files").EnumerateArray()
            .Select(x => x.GetString() ?? "").ToArray();
        Expect(errors, String(component, "normalizedTextTreeSha256") == expected,
            $"{name} declared identity drifted");
        Expect(errors, IntegrationImplementationGuard.NormalizedTextTreeIdentity(root, files) == expected,
            $"{name} live identity drifted");
    }

    private static string String(JsonElement parent, string name) =>
        parent.GetProperty(name).GetString() ?? throw new InvalidDataException($"{name} is null.");
    private static void Expect(List<string> errors, bool condition, string message)
    { if (!condition) errors.Add(message); }
}
