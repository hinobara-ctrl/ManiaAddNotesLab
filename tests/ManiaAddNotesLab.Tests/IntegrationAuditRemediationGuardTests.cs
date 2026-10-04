using System.Text.Json;
using System.Text.Json.Nodes;
using DocConsistencyTool;
using Xunit;

namespace ManiaAddNotesLab.Tests;

public sealed class IntegrationAuditRemediationGuardTests
{
    [Fact]
    public void PublishedRemediationContractRemainsInternallyConsistent()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath()));
        var actual = IntegrationImplementationGuard.CanonicalJsonHash(document.RootElement.GetProperty("contract"));
        Assert.True(actual == IntegrationAuditRemediationGuard.ContractIdentity, $"actual={actual}");
        Assert.Empty(IntegrationAuditRemediationGuard.Validate(document.RootElement, Root()));
    }

    [Fact]
    public void HistoricalIntegrationRemediationRouteRejectsPrebindingHardenedLiveIdentity()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath()));
        Assert.Empty(IntegrationAuditRemediationGuard.Validate(document.RootElement, Root()));
        Assert.NotEmpty(IntegrationAuditRemediationGuard.ValidateAgainstLive(
            document.RootElement, Root()));
    }

    [Theory]
    [InlineData("parent")]
    [InlineData("path")]
    [InlineData("component")]
    [InlineData("authority")]
    [InlineData("runtime")]
    [InlineData("inventory")]
    [InlineData("boundary")]
    [InlineData("declared")]
    public void SemanticOrDeclaredDriftFailsClosed(string mutation)
    {
        var root = JsonNode.Parse(File.ReadAllText(ContractPath()))!.AsObject();
        var contract = root["contract"]!.AsObject();
        switch (mutation)
        {
            case "parent": contract["parentIntegrationExecutionContractSha256"] = "BAD"; break;
            case "path": contract["explicitPathAuthoritySha256"] = "BAD"; break;
            case "component": contract["remediatedComponents"]!["officialFrozenC11Adapter"]!["normalizedTextTreeSha256"] = "BAD"; break;
            case "authority": contract["observedAuthorityModel"]!["callerClaimsAuthorizeContradictions"] = true; break;
            case "runtime": contract["isolatedRuntimeProvenanceModel"]!["buildOrigin"] = "HOST"; break;
            case "inventory": contract["limitedC11PathInventoryAuthorization"]!["scientificEvaluation"] = true; break;
            case "boundary": contract["phaseBoundary"]!["bindingCreated"] = true; break;
            case "declared": root["canonicalSha256"] = "BAD"; break;
        }
        using var changed = JsonDocument.Parse(root.ToJsonString());
        Assert.NotEmpty(IntegrationAuditRemediationGuard.Validate(changed.RootElement, Root()));
    }

    private static string ContractPath() => Path.Combine(Root(), "docs",
        "lane_0_corrective_successor_integration_audit_remediation_contract.json");
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ManiaAddNotesLab.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
