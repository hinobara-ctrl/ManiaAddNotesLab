using System.Text.Json;

namespace DocConsistencyTool;

public static class IntegrationPrebindingHardeningGuard
{
    public const string ContractIdentity =
        "9CC3961686885766A546996604AC237453A0725E832CF509FC27EA715E8A4665";

    public static IReadOnlyList<string> Validate(JsonElement artifact, string repositoryRoot)
    {
        var errors = new List<string>();
        try
        {
            var contract = artifact.GetProperty("contract");
            Expect(errors, IntegrationImplementationGuard.CanonicalJsonHash(contract) == ContractIdentity,
                "prebinding hardening canonical identity drifted");
            Expect(errors, String(artifact, "canonicalSha256") == ContractIdentity,
                "prebinding hardening declared identity drifted");
            Expect(errors, String(contract, "schemaVersion") ==
                "lane-0-corrective-successor-integration-prebinding-hardening.1", "schema drifted");
            Expect(errors, String(contract, "phaseId") ==
                "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_PREBINDING_HARDENING", "phase drifted");
            Expect(errors, String(contract, "baselinePublicHead") ==
                "1d379ecd89eb7f202e6537e9f1e420b75fb35425", "baseline drifted");
            Expect(errors, String(contract, "parentRemediationContractSha256") ==
                IntegrationAuditRemediationGuard.ContractIdentity, "remediation lineage drifted");
            Expect(errors, String(contract, "parentIntegrationExecutionContractSha256") ==
                IntegrationImplementationGuard.ContractIdentity, "execution lineage drifted");
            Expect(errors, String(contract, "parentIntegrationPreregistrationSha256") ==
                IntegrationImplementationGuard.ParentIdentity, "preregistration lineage drifted");
            Expect(errors, String(contract, "explicitPathAuthoritySha256") ==
                IntegrationAuditRemediationGuard.PathAuthorityIdentity, "path authority drifted");

            var components = contract.GetProperty("hardeningComponents");
            ValidateLive(errors, components, repositoryRoot, "officialFrozenC11Adapter",
                "032F9660D8C0012C990983CE307CDA9D20B1C49116BEAA7F3C5E3DB1EBD875B0");
            ValidateLive(errors, components, repositoryRoot, "officialInternalResearchRunner",
                "C2983EC9CD0CC8B2547864C29C62F11280000B2039AEB7154B7D216F3F2DFE74");
            ValidateLive(errors, components, repositoryRoot, "authorityRuntimeWorker",
                "32F8B90A44F3B4601EF3C978A70B3D21C514168FA1A5D00B28B7E0A37E1D6380");
            ValidateLive(errors, components, repositoryRoot, "deepSemanticPackageVerifier",
                "67969A60704553AA6CFB134F8B7F67236757EA6A2CF51C9CD7C4EF3C8FA5A4DB");

            var authorization = contract.GetProperty("canonicalAuthorizationModel");
            Expect(errors, String(authorization, "authorizationRoot") ==
                "CanonicalSourceRoot/.artifacts/lane0-corrective-successor-integration-authorization"
                && String(authorization, "bindingPath") == "AuthorizationRoot/canonical-binding.json"
                && String(authorization, "receiptNamespace") ==
                    "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION.ONE_SHOT"
                && !authorization.GetProperty("callerRedirectable").GetBoolean()
                && authorization.GetProperty("bindingLoadedAsExactCanonicalBytes").GetBoolean()
                && authorization.GetProperty("receiptExclusiveCreateDurableFlush").GetBoolean()
                && authorization.GetProperty("attemptCount").GetInt32() == 1,
                "canonical authorization model drifted");
            var ownership = contract.GetProperty("executionRootOwnershipModel");
            Expect(errors, String(ownership, "runtime") ==
                    "CHILD_PROCESS_FROM_EXECUTION_ROOT_BUILD"
                && ownership.GetProperty("workerFinalizesPackage").GetBoolean()
                && !ownership.GetProperty("hostMayTransformFinalBytes").GetBoolean()
                && !ownership.GetProperty("callerRedirectable").GetBoolean(),
                "ExecutionRoot ownership model drifted");
            var provenance = contract.GetProperty("runtimeProvenanceModel");
            Expect(errors, String(provenance, "requiredHeadChain") ==
                    "AUTHORIZED_HEAD==SOURCE_HEAD==ISOLATED_HEAD==AUTHORITY_RUNTIME_HEAD==SCIENTIFIC_RUNTIME_HEAD"
                && String(provenance, "authorityRuntime") ==
                    "EXECUTION_ROOT_BUILT_WORKER_PRE_RECEIPT_PROVENANCE"
                && String(provenance, "scientificRuntime") ==
                    "SAME_EXECUTION_ROOT_BUILT_WORKER_POST_RECEIPT_PROVENANCE"
                && !provenance.GetProperty("hostClaimsOverrideObservedState").GetBoolean(),
                "runtime provenance model drifted");
            var corpus = contract.GetProperty("corpusRootOpacityModel");
            Expect(errors, String(corpus, "beforeDurableReceipt") == "OPAQUE_TOKEN"
                && !corpus.GetProperty("pathOrMetadataInspectionBeforeReceipt").GetBoolean()
                && String(corpus, "exactPathMembershipAuthoritySha256") ==
                    IntegrationAuditRemediationGuard.PathAuthorityIdentity,
                "CorpusRoot opacity model drifted");
            var boundary = contract.GetProperty("phaseBoundary");
            Expect(errors, String(boundary, "status") == "COMPLETE"
                && String(boundary, "outcome") == "READY_FOR_INDEPENDENT_PREBINDING_AUDIT"
                && String(boundary, "authority") ==
                    "AUTHORIZED_FOR_PREBINDING_HARDENING_IMPLEMENTATION_ONLY"
                && String(boundary, "c11Authorization") == "NO_C11_AUTHORIZATION"
                && !boundary.GetProperty("bindingCreated").GetBoolean()
                && !boundary.GetProperty("realReceiptCreated").GetBoolean()
                && !boundary.GetProperty("realOneShotExecuted").GetBoolean()
                && !boundary.GetProperty("c11AccessedInThisPhase").GetBoolean()
                && !boundary.GetProperty("c11ScientificEvaluation").GetBoolean()
                && !boundary.GetProperty("behaviorChange").GetBoolean()
                && !boundary.GetProperty("rngChange").GetBoolean()
                && !boundary.GetProperty("defaultChange").GetBoolean()
                && !boundary.GetProperty("productExposure").GetBoolean()
                && String(boundary, "nextRequiredAction") == "HUMAN_REVIEW_REQUIRED",
                "phase boundary drifted");
        }
        catch (Exception exception)
        {
            errors.Add($"integration prebinding hardening contract malformed: {exception.Message}");
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
    {
        if (!condition) errors.Add(message);
    }
}
