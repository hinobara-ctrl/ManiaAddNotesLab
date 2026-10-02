using System.Text.Json;
using System.Text.Json.Nodes;
using DocConsistencyTool;

namespace ManiaAddNotesLab.Tests;

public sealed class IntegrationPreregistrationGuardTests
{
    [Fact]
    public void CanonicalPreregistrationPasses()
    {
        using var document = ContractDocument();
        Assert.Empty(IntegrationPreregistrationGuard.Validate(
            document.RootElement.GetProperty("contract")));
    }

    [Theory]
    [InlineData("missing-adapter")]
    [InlineData("missing-duplicate-rule")]
    [InlineData("missing-semantic-verifier")]
    [InlineData("semantic-verifier-after-binding")]
    [InlineData("binding-targets-current-baseline")]
    [InlineData("missing-one-shot")]
    [InlineData("corpus-before-receipt")]
    [InlineData("cli-exposure")]
    [InlineData("behavior-change")]
    [InlineData("wrong-duplicate-sha")]
    [InlineData("wrong-duplicate-location-count")]
    [InlineData("missing-package-verifier-binding-identity")]
    [InlineData("missing-receipt-binding-sha")]
    [InlineData("missing-receipt-approved-head")]
    [InlineData("missing-receipt-prereg-lineage")]
    [InlineData("corpus-root-isolation-before-receipt")]
    [InlineData("c11-authorization-enabled-without-access")]
    [InlineData("c11-accessed-without-authorization")]
    [InlineData("missing-preregistration-binding-identity")]
    [InlineData("missing-execution-contract-binding-identity")]
    [InlineData("missing-execution-contract-parent-lineage")]
    [InlineData("integration-started")]
    public void AdversarialContractMutationFails(string mutation)
    {
        var root = JsonNode.Parse(File.ReadAllText(ContractPath()))!.AsObject();
        var contract = root["contract"]!.AsObject();
        switch (mutation)
        {
            case "missing-adapter":
                contract["components"]!.AsObject().Remove("officialFrozenC11Adapter");
                break;
            case "missing-duplicate-rule":
                contract["duplicateSemantics"]!["pathMembershipDistinctFromContentIdentity"] = false;
                break;
            case "missing-semantic-verifier":
                contract["components"]!.AsObject().Remove("deepSemanticPackageVerifier");
                break;
            case "semantic-verifier-after-binding":
                contract["semanticVerifier"]!["deepVerifierLifecycle"] = "REQUIRED_AFTER_BINDING";
                break;
            case "binding-targets-current-baseline":
                contract["futureBinding"]!["issuanceTarget"] =
                    "89c0ea9dcd55a5a0f9a34f580373df9115683821";
                contract["futureBinding"]!["currentBaselineIsEligibleTarget"] = true;
                break;
            case "missing-one-shot":
                contract["futureReceipt"]!["durableFlush"] = false;
                break;
            case "corpus-before-receipt":
                var order = contract["authorityOrder"]!.AsArray();
                var corpus = order.Select((node, index) => (node, index))
                    .Single(item => item.node!.GetValue<string>() == "CORPUSROOT_INTERPRETATION").index;
                order.RemoveAt(corpus);
                order.Insert(0, "CORPUSROOT_INTERPRETATION");
                break;
            case "cli-exposure":
                contract["runnerContract"]!["productCliExposure"] = true;
                break;
            case "behavior-change":
                contract["phaseBoundary"]!["behaviorChange"] = true;
                break;
            case "wrong-duplicate-sha":
                contract["duplicateSemantics"]!["expectedDuplicateContentSha256"] =
                    "E73B098AA0CB6FA3BECE45E9551EF225412F5E01F6398184093A40B944F0AAE0";
                break;
            case "wrong-duplicate-location-count":
                contract["duplicateSemantics"]!["expectedDuplicateLocationCount"] = 1;
                break;
            case "missing-package-verifier-binding-identity":
                RemoveArrayValue(contract["futureBinding"]!.AsObject(), "fields",
                    "phase2PackageVerifierSha256");
                break;
            case "missing-receipt-binding-sha":
                RemoveArrayValue(contract["futureReceipt"]!.AsObject(), "fields",
                    "canonicalBindingSha256");
                break;
            case "missing-receipt-approved-head":
                RemoveArrayValue(contract["futureReceipt"]!.AsObject(), "fields",
                    "approvedPublicHead");
                break;
            case "missing-receipt-prereg-lineage":
                RemoveArrayValue(contract["futureReceipt"]!.AsObject(), "fields",
                    "integrationPreregistrationSha256");
                break;
            case "corpus-root-isolation-before-receipt":
                var authorityOrder = contract["authorityOrder"]!.AsArray();
                var isolation = ArrayIndex(authorityOrder, "CORPUS_SOURCE_EXECUTION_ROOT_ISOLATION");
                authorityOrder.RemoveAt(isolation);
                var receipt = ArrayIndex(authorityOrder, "DURABLE_RECEIPT_CREATION");
                authorityOrder.Insert(receipt, "CORPUS_SOURCE_EXECUTION_ROOT_ISOLATION");
                break;
            case "c11-authorization-enabled-without-access":
                contract["phaseBoundary"]!["c11Authorization"] = "AUTHORIZED";
                break;
            case "c11-accessed-without-authorization":
                contract["phaseBoundary"]!["c11Accessed"] = true;
                break;
            case "missing-preregistration-binding-identity":
                RemoveArrayValue(contract["futureBinding"]!.AsObject(), "fields",
                    "integrationPreregistrationSha256");
                break;
            case "missing-execution-contract-binding-identity":
                RemoveArrayValue(contract["futureBinding"]!.AsObject(), "fields",
                    "integrationExecutionContractSha256");
                break;
            case "missing-execution-contract-parent-lineage":
                contract["futureExecutionContract"]!.AsObject()
                    .Remove("requiredParentLineageField");
                break;
            case "integration-started":
                contract["phaseBoundary"]!["integrationStatus"] = "STARTED";
                contract["phaseBoundary"]!["integrationAuthorization"] = "AUTHORIZED";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        using var mutated = JsonDocument.Parse(contract.ToJsonString());
        Assert.NotEmpty(IntegrationPreregistrationGuard.Validate(mutated.RootElement));
    }

    private static JsonDocument ContractDocument() => JsonDocument.Parse(File.ReadAllText(ContractPath()));
    private static void RemoveArrayValue(JsonObject parent, string arrayName, string value)
    {
        var array = parent[arrayName]!.AsArray();
        array.RemoveAt(ArrayIndex(array, value));
    }
    private static int ArrayIndex(JsonArray array, string value) => array
        .Select((node, index) => (node, index))
        .Single(item => item.node!.GetValue<string>() == value).index;
    private static string ContractPath() => Path.Combine(Root(), "docs",
        "lane_0_corrective_successor_integration_preregistration_contract.json");
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ManiaAddNotesLab.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
