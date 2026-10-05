using System.Text.Json;

namespace DocConsistencyTool;

public static class IntegrationPrebindingAtomicityProvenanceRemediationGuard
{
    public const string ContractIdentity =
        "27BAFFB4B92E5B30A073E60673D8F2FEDB109FF70A69A23C5783D8D589693FDD";
    public const string AdapterIdentity =
        "032F9660D8C0012C990983CE307CDA9D20B1C49116BEAA7F3C5E3DB1EBD875B0";
    public const string RunnerIdentity =
        "32C600B809F0B021C3806FDBC2BE58656C6DA27B5CE053E4E625AC9A175D72BF";
    public const string AuthorityIdentity =
        "805F70466B503FEBBB14511E595A12E3AB42A583CED1A1D04193BF3DE04474DE";
    public const string VerifierIdentity =
        "67969A60704553AA6CFB134F8B7F67236757EA6A2CF51C9CD7C4EF3C8FA5A4DB";

    public static IReadOnlyList<string> Validate(JsonElement artifact, string repositoryRoot)
    {
        var errors = new List<string>();
        try
        {
            var contract = artifact.GetProperty("contract");
            Expect(errors, IntegrationImplementationGuard.CanonicalJsonHash(contract) == ContractIdentity,
                "atomicity/provenance canonical identity drifted");
            Expect(errors, String(artifact, "canonicalSha256") == ContractIdentity,
                "atomicity/provenance declared identity drifted");
            Expect(errors, String(contract, "schemaVersion") ==
                "lane-0-corrective-successor-integration-prebinding-atomicity-provenance-remediation.1",
                "schema drifted");
            Expect(errors, String(contract, "phaseId") ==
                "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_PREBINDING_ATOMICITY_PROVENANCE_REMEDIATION",
                "phase drifted");
            Expect(errors, String(contract, "baselinePublicHead") ==
                "991dd5e8006d896d604a697a3d11c26d81a82a62", "baseline drifted");
            Expect(errors, String(contract, "parentRemediationContractSha256") ==
                IntegrationFinalAuthorityBoundaryRemediationGuard.ContractIdentity, "parent drifted");
            Expect(errors, String(contract, "priorFinalAuditVerdict") ==
                "FINAL_PREBINDING_REAUDIT_REQUIRES_REMEDIATION_BEFORE_BINDING",
                "prior verdict drifted");

            var findings = contract.GetProperty("rootFindingDispositions");
            Expect(errors, String(findings, "AP-R1") ==
                "CLOSED_SOURCE_ROOT_ONE_SHOT_ATOMICITY", "AP-R1 drifted");
            Expect(errors, String(findings, "AP-R2") ==
                "CLOSED_NO_HARDLINK_PROVENANCE_OWNERSHIP", "AP-R2 drifted");
            var observations = contract.GetProperty("observations");
            Expect(errors, String(observations, "authorityOrder21Step") ==
                "RESOLVED_BY_AP_R1_NOT_SEPARATE_ROOT_FINDING"
                && String(observations, "finalArtifactTiming") == "NON_BLOCKING_PRESERVED",
                "observation disposition drifted");

            var atomicity = contract.GetProperty("receiptAtomicity");
            Expect(errors, String(atomicity, "canonicalCreator") ==
                "SEALED_OFFICIAL_HOST_COORDINATOR"
                && !atomicity.GetProperty("workerCreatesCanonicalReceipt").GetBoolean()
                && String(atomicity, "firstDurableConsumption") ==
                    "CANONICAL_DURABLE_RECEIPT_CREATION"
                && String(atomicity, "preReceiptSignal") == "PRE_RECEIPT_READY"
                && String(atomicity, "postReceiptSignal") == "RECEIPT_VERIFIED"
                && String(atomicity, "receiptNamespace") ==
                    "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION.ONE_SHOT"
                && atomicity.GetProperty("sameWorkerSessionRequired").GetBoolean()
                && !atomicity.GetProperty("freshProcessAttachAfterReceipt").GetBoolean()
                && !atomicity.GetProperty("preReceiptCrashConsumes").GetBoolean()
                && atomicity.GetProperty("postReceiptCrashConsumes").GetBoolean()
                && atomicity.GetProperty("failureConsumes").GetBoolean()
                && atomicity.GetProperty("invalidConsumes").GetBoolean()
                && atomicity.GetProperty("exceptionConsumes").GetBoolean(),
                "receipt atomicity drifted");

            var checkout = contract.GetProperty("checkoutProvenance");
            Expect(errors, String(checkout, "materializer") == "GitDetachedCheckoutMaterializer"
                && checkout.GetProperty("receiptCreatorDirectlyOwnsEvidence").GetBoolean()
                && checkout.GetProperty("requiredDetached").GetBoolean()
                && checkout.GetProperty("requiredTrackedClean").GetBoolean()
                && !checkout.GetProperty("requiredUsedHardlinks").GetBoolean()
                && !checkout.GetProperty("requiredReusedSourceBuildOutputs").GetBoolean()
                && !checkout.GetProperty("callerAttestationIsAuthoritySource").GetBoolean(),
                "checkout provenance drifted");
            var handshake = contract.GetProperty("handshake").EnumerateArray()
                .Select(x => x.GetString()).ToArray();
            Expect(errors, Array.IndexOf(handshake, "PRE_RECEIPT_READY")
                    < Array.IndexOf(handshake, "HOST_CREATES_CANONICAL_DURABLE_RECEIPT")
                && Array.IndexOf(handshake, "HOST_CREATES_CANONICAL_DURABLE_RECEIPT")
                    < Array.IndexOf(handshake, "RECEIPT_VERIFIED")
                && Array.IndexOf(handshake, "RECEIPT_VERIFIED")
                    < Array.IndexOf(handshake, "HOST_SENDS_OPAQUE_CORPUSROOT_TOKEN"),
                "official handshake order drifted");

            var components = contract.GetProperty("currentComponents");
            ValidateLive(errors, components, repositoryRoot, "officialFrozenC11Adapter", AdapterIdentity);
            ValidateLive(errors, components, repositoryRoot, "officialDependencySealedRunner", RunnerIdentity);
            ValidateLive(errors, components, repositoryRoot, "atomicAuthorityRuntimeWorker", AuthorityIdentity);
            ValidateLive(errors, components, repositoryRoot, "deepSemanticPackageVerifier", VerifierIdentity);
            var order = contract.GetProperty("authorityOrder").EnumerateArray()
                .Select(x => x.GetString()).ToArray();
            Expect(errors, order.Length == 21 && order[12] == "DURABLE_RECEIPT_CREATION"
                && order[13] == "CORPUSROOT_INTERPRETATION", "authority order drifted");
            var boundary = contract.GetProperty("phaseBoundary");
            Expect(errors, String(boundary, "status") == "COMPLETE"
                && String(boundary, "outcome") == "READY_FOR_FINAL_INDEPENDENT_PREBINDING_REAUDIT"
                && String(boundary, "authority") ==
                    "AUTHORIZED_FOR_PREBINDING_ATOMICITY_PROVENANCE_REMEDIATION_IMPLEMENTATION_ONLY"
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
            errors.Add($"atomicity/provenance contract malformed: {exception.Message}");
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
        Expect(errors, IntegrationImplementationGuard.NormalizedTextTreeIdentity(root, files) == expected,
            $"{name} live identity drifted");
    }

    private static string String(JsonElement parent, string name) =>
        parent.GetProperty(name).GetString() ?? throw new InvalidDataException($"{name} is null.");
    private static void Expect(List<string> errors, bool condition, string message)
    { if (!condition) errors.Add(message); }
}
