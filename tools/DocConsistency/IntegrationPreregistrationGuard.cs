using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace DocConsistencyTool;

public static class IntegrationPreregistrationGuard
{
    public static ImmutableArray<string> Validate(JsonElement contract)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        try
        {
            Equal(contract, "schemaVersion",
                "lane-0-corrective-successor-integration-preregistration.1", errors);
            Equal(contract, "phaseId",
                "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_PREREGISTRATION", errors);
            Equal(contract, "baselinePublicHead",
                "89c0ea9dcd55a5a0f9a34f580373df9115683821", errors);
            var parent = contract.GetProperty("parentAudit");
            Equal(parent, "outcome", "AUDIT_CLEAN_WITH_NON_BLOCKING_OBSERVATIONS", errors);
            Equal(parent, "publicHead", "6b1a1730c23d3429a533aa95ae251ecfb1eef18a", errors);
            var phase2 = contract.GetProperty("frozenPhase2Identities");
            Equal(phase2, "contractSha256",
                "11F55A6770BA78F008A6690C88561C714CAF45BA15B96E472C0C80B219C4D5B6", errors);
            Equal(phase2, "scientificImplementationSha256",
                "8F9AE545027F0F1364E3378324646C7F7711CDF7EFFC8CC879660644A3D905D0", errors);
            Equal(phase2, "executionHarnessSha256",
                "15DC978C24119E9832DD70C0C61F5BFB1BBAF11520A107A7728220009B793A61", errors);
            Equal(phase2, "packageVerifierSha256",
                "DFF48221A6FB087367B1C6B1062B7355806C53668C6876A4BB4EC59983232D38", errors);
            Equal(phase2, "frozenDependenciesSha256",
                "D5931A815701C0FAB854B878373A8ED03F3E29BDEA17C98A428C7AF6B0F7023B", errors);
            Equal(phase2, "verifiedManifestSha256",
                "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445", errors);

            var components = contract.GetProperty("components");
            foreach (var component in new[]
            {
                "officialFrozenC11Adapter", "officialInternalResearchRunner",
                "isolatedExecutionLauncher", "deepSemanticPackageVerifier"
            }) Equal(components, component, "NOT_YET_IMPLEMENTED", errors);

            var corpus = contract.GetProperty("corpusAdmission");
            Equal(corpus, "manifestAuthorityModel", "VerifiedFrozenC11Manifest", errors);
            Number(corpus, "explicitPathCount", 12, errors);
            Number(corpus, "uniqueContentCount", 11, errors);
            Number(corpus, "exactDuplicateRelationships", 1, errors);
            Number(corpus, "originalObjectCount", 50836, errors);
            ExactIntArray(corpus, "keymodes", [4, 7, 10], errors);
            False(corpus, "songsDiscovery", errors);
            False(corpus, "arbitraryOsuEnumeration", errors);
            False(corpus, "realCorpusAccessInThisPhase", errors);
            RequireArray(corpus, "requirements", ["EXACT_EXPLICIT_PATH_MEMBERSHIP",
                "EXACT_SHA256_MEMBERSHIP", "DETERMINISTIC_ORDERING"], errors);

            var duplicate = contract.GetProperty("duplicateSemantics");
            True(duplicate, "pathMembershipDistinctFromContentIdentity", errors);
            Number(duplicate, "admittedPathEvidenceCount", 12, errors);
            Number(duplicate, "scientificUniqueContentCount", 11, errors);
            Equal(duplicate, "expectedDuplicateContentSha256",
                "322F995448A73713DCBECE3D140F4D516EF687DBC43CC140CFF14948566F906B", errors);
            Number(duplicate, "expectedDuplicateLocationCount", 2, errors);
            Equal(duplicate, "expectedDuplicateFamily", "ORGT | DESTINY | BAIO", errors);
            Number(duplicate, "expectedDuplicateKeymode", 7, errors);
            Number(duplicate, "expectedDuplicateOriginalObjects", 1653, errors);
            Number(duplicate, "allOtherContentIdentitiesExpectedLocationCount", 1, errors);
            True(duplicate, "exactExpectedDuplicatePreservedAsIntegrityEvidence", errors);
            False(duplicate, "duplicateTeachesStyleTwice", errors);
            False(duplicate, "duplicateCountsAsUniqueChartTwice", errors);
            False(duplicate, "duplicateSilentlyDiscardedFromIntegrityEvidence", errors);

            var runner = contract.GetProperty("runnerContract");
            False(runner, "productCliExposure", errors);
            False(runner, "webExposure", errors);
            False(runner, "addNotesEngineExposure", errors);
            RequireArray(runner, "requirements", ["DURABLE_RECEIPT_BEFORE_CORPUS",
                "TWO_INDEPENDENT_SCIENTIFIC_PASSES", "DEEP_SEMANTIC_VERIFIER"], errors);

            var launcher = contract.GetProperty("launcherContract");
            ExactStringArray(launcher, "preReceiptIsolationScope",
                ["SOURCE_ROOT", "EXECUTION_ROOT"], errors);
            False(launcher, "preReceiptCorpusRootInspection", errors);
            ExactStringArray(launcher, "postReceiptIsolationScope",
                ["SOURCE_ROOT", "EXECUTION_ROOT", "CORPUS_ROOT"], errors);
            Equal(launcher, "pathIsolationSemantics", "CANONICAL_LEXICAL_ONLY", errors);

            var opacity = contract.GetProperty("corpusRootOpacity");
            Equal(opacity, "stateBeforeReceipt", "OPAQUE_TOKEN", errors);
            RequireArray(opacity, "preReceiptForbidden", ["Path.GetFullPath",
                "DIRECTORY_ENUMERATION", "OSU_PARSE", "CORPUS_HASHING",
                "CORPUS_NON_NESTING_CHECKS"], errors);
            True(opacity, "interpretationAllowedAfterDurableReceipt", errors);
            True(opacity, "invalidAfterReceiptConsumesAttempt", errors);

            var verifier = contract.GetProperty("semanticVerifier");
            Equal(verifier, "currentPhase2Verifier",
                "SUFFICIENT_FOR_PHASE2_IN_PROCESS_DETERMINISM", errors);
            Equal(verifier, "deepVerifierLifecycle", "REQUIRED_BEFORE_BINDING", errors);
            True(verifier, "independentFromBuilderSerializationPath", errors);
            RequireArray(verifier, "artifacts", ["execution_identity.json",
                "successor_references.json", "scientific_summary.json", "integrity.json",
                "chart_family_keymode_results.csv", "g1_historical_operational_cases.csv",
                "g1_state_transitions.csv", "sha256sums.txt"], errors);
            False(verifier, "implementationInThisPhase", errors);

            var identity = contract.GetProperty("identityModel");
            True(identity, "setsFinite", errors);
            True(identity, "setsNonOverlappingUnlessExplicitlyJustified", errors);
            True(identity, "testsAndDocsExcluded", errors);
            var categories = identity.GetProperty("categories");
            foreach (var category in new[]
                     { "adapter", "internalRunner", "isolatedLauncher", "deepSemanticVerifier" })
                Equal(categories, category, "NOT_YET_IMPLEMENTED", errors);

            var binding = contract.GetProperty("futureBinding");
            Equal(binding, "status", "ABSENT", errors);
            Equal(binding, "issuanceTarget",
                "FUTURE_INDEPENDENTLY_AUDITED_INTEGRATION_PUBLIC_SHA", errors);
            False(binding, "currentBaselineIsEligibleTarget", errors);
            False(binding, "creationAuthorizedInThisPhase", errors);
            Number(binding, "authorizedExecutionCount", 1, errors);
            RequireArray(binding, "fields", ["approvedPublicHead", "phase1ContractSha256",
                "phase2ContractSha256", "integrationPreregistrationSha256",
                "integrationExecutionContractSha256", "manifestSha256",
                "scientificImplementationSha256", "phase2HarnessSha256",
                "phase2PackageVerifierSha256", "adapterSha256", "runnerSha256",
                "launcherSha256", "semanticVerifierSha256", "frozenDependenciesSha256",
                "explicitHumanAuthorization", "authorizedExecutionCount"], errors);
            True(binding, "artifactCreationDoesNotConstituteHumanAuthorization", errors);

            var executionContract = contract.GetProperty("futureExecutionContract");
            Equal(executionContract, "status", "NOT_YET_IMPLEMENTED", errors);
            Equal(executionContract, "requiredParentLineageField",
                "parentIntegrationPreregistrationSha256", errors);
            Equal(executionContract, "requiredParentLineageValue",
                "FINAL_CANONICAL_SHA256_OF_THIS_PREREGISTRATION_CONTRACT", errors);

            var receipt = contract.GetProperty("futureReceipt");
            Equal(receipt, "status", "ABSENT", errors);
            True(receipt, "sourceAuthorizationRootAnchored", errors);
            True(receipt, "exclusiveCreate", errors);
            Equal(receipt, "fileMode", "CreateNew", errors);
            True(receipt, "durableFlush", errors);
            Number(receipt, "attemptCount", 1, errors);
            RequireArray(receipt, "fields", ["schemaVersion", "approvedPublicHead",
                "canonicalBindingSha256", "integrationPreregistrationSha256",
                "integrationExecutionContractSha256", "attemptCount"], errors);
            Equal(receipt, "canonicalBindingSha256Semantics",
                "SHA256_OF_EXACT_CANONICAL_BINDING_BYTES_VERIFIED_IMMEDIATELY_BEFORE_RECEIPT_CREATION",
                errors);
            foreach (var property in new[]
                     { "successConsumes", "failureConsumes", "invalidConsumes", "exceptionConsumes" })
                True(receipt, property, errors);
            foreach (var property in new[]
                     { "automaticRetry", "automaticRearm", "deleteToRetry", "creationAuthorizedInThisPhase" })
                False(receipt, property, errors);

            var order = contract.GetProperty("authorityOrder").EnumerateArray()
                .Select(x => x.GetString()!).ToArray();
            Before(order, "SOURCE_EXECUTION_ROOT_ISOLATION", "DURABLE_RECEIPT_CREATION", errors);
            Before(order, "DURABLE_RECEIPT_CREATION", "CORPUSROOT_INTERPRETATION", errors);
            Before(order, "CORPUSROOT_INTERPRETATION",
                "CORPUS_SOURCE_EXECUTION_ROOT_ISOLATION", errors);
            Before(order, "CORPUS_SOURCE_EXECUTION_ROOT_ISOLATION", "CORPUS_ADMISSION", errors);
            Before(order, "CANONICAL_BINDING_BYTE_AND_HASH_VERIFICATION",
                "DURABLE_RECEIPT_CREATION", errors);
            Before(order, "BYTE_DETERMINISM", "DEEP_SEMANTIC_VERIFICATION", errors);

            var governance = contract.GetProperty("governanceSequence").EnumerateArray()
                .Select(x => x.GetString()!).ToArray();
            Before(governance, "INDEPENDENT_INTEGRATION_AUDIT",
                "BINDING_ARTIFACT_ISSUANCE_OR_PREPARATION", errors);
            Before(governance, "BINDING_ARTIFACT_ISSUANCE_OR_PREPARATION",
                "SEPARATE_EXPLICIT_HUMAN_AUTHORIZATION_EVENT", errors);
            Before(governance, "SEPARATE_EXPLICIT_HUMAN_AUTHORIZATION_EVENT",
                "FINAL_AUTHORIZED_BINDING_STATE", errors);
            Before(governance, "FINAL_AUTHORIZED_BINDING_STATE", "DURABLE_RECEIPT_CREATION", errors);
            Before(governance, "DURABLE_RECEIPT_CREATION", "C11_ACCESS", errors);

            var boundary = contract.GetProperty("phaseBoundary");
            Equal(boundary, "integrationStatus", "NOT_STARTED", errors);
            Equal(boundary, "integrationAuthorization", "NOT_AUTHORIZED", errors);
            Equal(boundary, "c11Authorization", "NO_C11_AUTHORIZATION", errors);
            foreach (var property in new[]
            {
                "implementationAuthorized", "bindingPresent", "receiptPresent", "c11Accessed",
                "executionAuthorized", "behaviorChange", "rngChange", "defaultChange", "productExposure"
            }) False(boundary, property, errors);
            Equal(boundary, "nextRequiredAction", "HUMAN_REVIEW_REQUIRED", errors);

            RequireArray(contract, "exclusions", ["REAL_C11_ACCESS", "REAL_BINDING_OR_RECEIPT",
                "ADAPTER_RUNNER_LAUNCHER_OR_SEMANTIC_VERIFIER_IMPLEMENTATION",
                "NORMAL_CLI_OR_WEB_EXPOSURE"], errors);
            var outcomes = contract.GetProperty("outcomeTaxonomy");
            RequireArray(outcomes, "allowed", ["READY_FOR_HUMAN_REVIEW", "BLOCKED", "INVALID"], errors);
            Equal(outcomes, "selected", "READY_FOR_HUMAN_REVIEW", errors);
        }
        catch (Exception exception)
        {
            errors.Add($"Integration preregistration contract shape is invalid: {exception.Message}");
        }
        return errors.ToImmutable();
    }

    public static string CanonicalJsonHash(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, element);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }

    private static void Equal(JsonElement parent, string name, string expected,
        ImmutableArray<string>.Builder errors)
    {
        if (!parent.TryGetProperty(name, out var value) || value.GetString() != expected)
            errors.Add($"{name} must equal {expected}.");
    }
    private static void Number(JsonElement parent, string name, int expected,
        ImmutableArray<string>.Builder errors)
    {
        if (!parent.TryGetProperty(name, out var value) || value.GetInt32() != expected)
            errors.Add($"{name} must equal {expected}.");
    }
    private static void True(JsonElement parent, string name, ImmutableArray<string>.Builder errors)
    {
        if (!parent.TryGetProperty(name, out var value) || !value.GetBoolean())
            errors.Add($"{name} must remain true.");
    }
    private static void False(JsonElement parent, string name, ImmutableArray<string>.Builder errors)
    {
        if (!parent.TryGetProperty(name, out var value) || value.GetBoolean())
            errors.Add($"{name} must remain false.");
    }
    private static void RequireArray(JsonElement parent, string name, IEnumerable<string> required,
        ImmutableArray<string>.Builder errors)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{name} must be an array.");
            return;
        }
        var values = value.EnumerateArray().Select(x => x.GetString()).ToHashSet(StringComparer.Ordinal);
        foreach (var item in required)
            if (!values.Contains(item)) errors.Add($"{name} is missing {item}.");
    }
    private static void ExactIntArray(JsonElement parent, string name, int[] expected,
        ImmutableArray<string>.Builder errors)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array
            || !value.EnumerateArray().Select(x => x.GetInt32()).SequenceEqual(expected))
            errors.Add($"{name} must equal [{string.Join(",", expected)}].");
    }
    private static void ExactStringArray(JsonElement parent, string name, string[] expected,
        ImmutableArray<string>.Builder errors)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array
            || !value.EnumerateArray().Select(x => x.GetString()).SequenceEqual(expected))
            errors.Add($"{name} must preserve the exact preregistered sequence.");
    }
    private static void Before(string[] order, string first, string second,
        ImmutableArray<string>.Builder errors)
    {
        var left = Array.IndexOf(order, first);
        var right = Array.IndexOf(order, second);
        if (left < 0 || right < 0 || left >= right)
            errors.Add($"Authority order must place {first} before {second}.");
    }
}
