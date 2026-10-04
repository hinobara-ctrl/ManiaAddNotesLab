using System.Text.Json;

namespace DocConsistencyTool;

public static class IntegrationAuditRemediationGuard
{
    public const string ContractIdentity =
        "F2FD07BC0D120A7B2CA7350BA582141C4EDC3277AD06C6D9E92139244F8A39EB";
    public const string PathAuthorityIdentity =
        "DA86B97DA2E0309BE4C5FC035E54397846EA5B2DBA6989BDEDE4424939EDF884";

    public static IReadOnlyList<string> Validate(JsonElement artifact, string repositoryRoot)
    {
        var errors = new List<string>();
        try
        {
            var contract = artifact.GetProperty("contract");
            Expect(errors, IntegrationImplementationGuard.CanonicalJsonHash(contract) == ContractIdentity,
                "remediation canonical contract identity drifted");
            Expect(errors, String(artifact, "canonicalSha256") == ContractIdentity,
                "remediation declared canonical identity drifted");
            Expect(errors, String(contract, "schemaVersion") ==
                "lane-0-corrective-successor-integration-audit-remediation.1", "schema drifted");
            Expect(errors, String(contract, "phaseId") ==
                "LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_AUDIT_REMEDIATION", "phase drifted");
            Expect(errors, String(contract, "baselinePublicHead") ==
                "7a1e55c5ddd415398ee750063c75697f4af47cec", "baseline drifted");
            Expect(errors, String(contract, "parentIntegrationExecutionContractSha256") ==
                IntegrationImplementationGuard.ContractIdentity, "historical execution lineage drifted");
            Expect(errors, String(contract, "parentIntegrationPreregistrationSha256") ==
                IntegrationImplementationGuard.ParentIdentity, "preregistration lineage drifted");
            Expect(errors, String(contract, "explicitPathAuthoritySha256") == PathAuthorityIdentity,
                "explicit path authority lineage drifted");

            ValidatePathAuthority(errors, repositoryRoot);
            var components = contract.GetProperty("remediatedComponents");
            ValidateComponent(errors, components, repositoryRoot, "officialFrozenC11Adapter",
                "032F9660D8C0012C990983CE307CDA9D20B1C49116BEAA7F3C5E3DB1EBD875B0");
            ValidateComponent(errors, components, repositoryRoot, "officialInternalResearchRunner",
                "DAF26670B68F6621303DA6F3D49FD0E8B3DCDB61489E1AB0A33523267602F784");
            ValidateComponent(errors, components, repositoryRoot, "isolatedExecutionLauncher",
                "498FF1C8CF83C54F5EF31F1D2C127CA688F36500C9CD25C34EE7E53DB6979DE9");
            ValidateComponent(errors, components, repositoryRoot, "deepSemanticPackageVerifier",
                "67969A60704553AA6CFB134F8B7F67236757EA6A2CF51C9CD7C4EF3C8FA5A4DB");

            var authority = contract.GetProperty("observedAuthorityModel");
            Expect(errors, String(authority, "sourceHead") == "OBSERVED_BY_GIT"
                && String(authority, "sourceTrackedCleanliness") == "OBSERVED_BY_GIT"
                && String(authority, "isolatedHeadAndDetachedState") == "OBSERVED_BY_GIT"
                && String(authority, "isolatedTrackedCleanliness") == "OBSERVED_BY_GIT"
                && String(authority, "runtimeComponents") == "OBSERVED_FROM_LIVE_SOURCE_TREE"
                && !authority.GetProperty("callerClaimsAuthorizeContradictions").GetBoolean(),
                "observed-authority model drifted");
            var runtime = contract.GetProperty("isolatedRuntimeProvenanceModel");
            Expect(errors, String(runtime, "checkout") == "DETACHED_NO_HARDLINK"
                && String(runtime, "buildOrigin") == "EXECUTION_ROOT"
                && String(runtime, "runtime") == "CHILD_PROCESS_FROM_EXECUTION_ROOT_BUILD"
                && runtime.GetProperty("selfReport").GetBoolean()
                && runtime.GetProperty("independentAuthorityVerification").GetBoolean(),
                "isolated runtime model drifted");
            var inventory = contract.GetProperty("limitedC11PathInventoryAuthorization");
            Expect(errors, String(inventory, "authority") ==
                "AUTHORIZED_FOR_FROZEN_C11_EXPLICIT_PATH_INVENTORY_READ_ONLY"
                && String(inventory, "root") == ".artifacts/f2-1-corpus"
                && inventory.GetProperty("filesRead").GetInt32() == 12
                && !inventory.GetProperty("songsDiscovery").GetBoolean()
                && !inventory.GetProperty("scientificEvaluation").GetBoolean()
                && !inventory.GetProperty("successorExecution").GetBoolean()
                && !inventory.GetProperty("behaviorExperiment").GetBoolean()
                && !inventory.GetProperty("rngExecution").GetBoolean(), "inventory authority drifted");
            var boundary = contract.GetProperty("phaseBoundary");
            Expect(errors, String(boundary, "status") == "COMPLETE"
                && String(boundary, "outcome") == "READY_FOR_INDEPENDENT_REMEDIATION_AUDIT"
                && String(boundary, "authority") ==
                    "AUTHORIZED_FOR_INTEGRATION_AUDIT_REMEDIATION_IMPLEMENTATION_ONLY"
                && !boundary.GetProperty("bindingCreated").GetBoolean()
                && !boundary.GetProperty("realReceiptCreated").GetBoolean()
                && !boundary.GetProperty("realOneShotExecuted").GetBoolean()
                && !boundary.GetProperty("behaviorChange").GetBoolean()
                && !boundary.GetProperty("rngChange").GetBoolean()
                && !boundary.GetProperty("defaultChange").GetBoolean()
                && !boundary.GetProperty("productExposure").GetBoolean()
                && String(boundary, "nextRequiredAction") == "HUMAN_REVIEW_REQUIRED",
                "phase boundary drifted");
        }
        catch (Exception exception)
        {
            errors.Add($"integration audit remediation contract malformed: {exception.Message}");
        }
        return errors;
    }

    private static void ValidatePathAuthority(List<string> errors, string root)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs",
            "lane_0_corrective_successor_frozen_c11_explicit_path_authority.json")));
        var authority = document.RootElement.GetProperty("authority");
        var actual = IntegrationImplementationGuard.CanonicalJsonHash(authority);
        Expect(errors, actual == PathAuthorityIdentity
            && String(document.RootElement, "canonicalSha256") == PathAuthorityIdentity,
            "explicit path authority canonical identity drifted");
        var locations = authority.GetProperty("locations").EnumerateArray().ToArray();
        Expect(errors, locations.Length == 12
            && locations.Select(x => String(x, "relativePath")).Distinct(StringComparer.Ordinal).Count() == 12
            && locations.Select(x => String(x, "chartSha256")).Distinct(StringComparer.Ordinal).Count() == 11,
            "explicit path membership/count drifted");
    }

    private static void ValidateComponent(List<string> errors, JsonElement components, string root,
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
