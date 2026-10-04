using System.Text.Json;
using System.Text.Json.Nodes;
using DocConsistencyTool;
using Xunit;

namespace ManiaAddNotesLab.Tests;

public sealed class IntegrationPrebindingHardeningGuardTests
{
    [Fact]
    public void HistoricalHardeningContractRemainsInternallyConsistent()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath()));
        Assert.Equal(IntegrationPrebindingHardeningGuard.ContractIdentity,
            IntegrationImplementationGuard.CanonicalJsonHash(
                document.RootElement.GetProperty("contract")));
        Assert.Empty(IntegrationPrebindingHardeningGuard.Validate(document.RootElement, Root()));
    }

    [Fact]
    public void HistoricalPrebindingHardeningRouteRejectsAuditRemediatedLiveIdentity()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath()));
        Assert.Empty(IntegrationPrebindingHardeningGuard.Validate(document.RootElement, Root()));
        Assert.NotEmpty(IntegrationPrebindingHardeningGuard.ValidateAgainstLive(
            document.RootElement, Root()));
    }

    [Theory]
    [InlineData("lineage")]
    [InlineData("path")]
    [InlineData("component")]
    [InlineData("authorization")]
    [InlineData("ownership")]
    [InlineData("provenance")]
    [InlineData("corpus")]
    [InlineData("boundary")]
    [InlineData("declared")]
    public void SemanticOrDeclaredDriftFailsClosed(string mutation)
    {
        var root = JsonNode.Parse(File.ReadAllText(ContractPath()))!.AsObject();
        var contract = root["contract"]!.AsObject();
        switch (mutation)
        {
            case "lineage": contract["parentRemediationContractSha256"] = "BAD"; break;
            case "path": contract["explicitPathAuthoritySha256"] = "BAD"; break;
            case "component": contract["hardeningComponents"]!["authorityRuntimeWorker"]!["normalizedTextTreeSha256"] = "BAD"; break;
            case "authorization": contract["canonicalAuthorizationModel"]!["callerRedirectable"] = true; break;
            case "ownership": contract["executionRootOwnershipModel"]!["hostMayTransformFinalBytes"] = true; break;
            case "provenance": contract["runtimeProvenanceModel"]!["hostClaimsOverrideObservedState"] = true; break;
            case "corpus": contract["corpusRootOpacityModel"]!["pathOrMetadataInspectionBeforeReceipt"] = true; break;
            case "boundary": contract["phaseBoundary"]!["bindingCreated"] = true; break;
            case "declared": root["canonicalSha256"] = "BAD"; break;
        }
        using var changed = JsonDocument.Parse(root.ToJsonString());
        Assert.NotEmpty(IntegrationPrebindingHardeningGuard.Validate(changed.RootElement, Root()));
    }

    private static string ContractPath() => Path.Combine(Root(), "docs",
        "lane_0_corrective_successor_integration_prebinding_hardening_contract.json");
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName,
            "ManiaAddNotesLab.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
