using System.Text.Json;
using System.Text.Json.Nodes;
using DocConsistencyTool;
using Xunit;

namespace ManiaAddNotesLab.Tests;

public sealed class IntegrationFinalAuthorityBoundaryRemediationGuardTests
{
    [Fact]
    public void HistoricalContractRemainsInternallyConsistentAndRejectsLiveDrift()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath()));
        Assert.Empty(IntegrationFinalAuthorityBoundaryRemediationGuard.Validate(
            document.RootElement, Root()));
        Assert.NotEmpty(IntegrationFinalAuthorityBoundaryRemediationGuard.ValidateAgainstLive(
            document.RootElement, Root()));
    }

    [Theory]
    [InlineData("finding")]
    [InlineData("mode")]
    [InlineData("charts")]
    [InlineData("synthetic")]
    [InlineData("component")]
    [InlineData("order")]
    [InlineData("boundary")]
    [InlineData("declared")]
    public void SemanticOrDeclaredDriftFailsClosed(string mutation)
    {
        var root = JsonNode.Parse(File.ReadAllText(ContractPath()))!.AsObject();
        var contract = root["contract"]!.AsObject();
        switch (mutation)
        {
            case "finding": contract["findingDispositions"]!["FB-R1"] = "OPEN"; break;
            case "mode": contract["officialCapability"]!["standaloneExecuteMode"] = true; break;
            case "charts": contract["officialCapability"]!["callerSuppliedCharts"] = true; break;
            case "synthetic": contract["syntheticSeparation"]!["officialAuthorityNamespaceAccessible"] = true; break;
            case "component": contract["currentComponents"]!["unifiedAuthorityRuntimeWorker"]!["normalizedTextTreeSha256"] = "BAD"; break;
            case "order": contract["authorityOrder"]!.AsArray().RemoveAt(14); break;
            case "boundary": contract["phaseBoundary"]!["realReceiptCreated"] = true; break;
            case "declared": root["canonicalSha256"] = "BAD"; break;
        }
        using var changed = JsonDocument.Parse(root.ToJsonString());
        Assert.NotEmpty(IntegrationFinalAuthorityBoundaryRemediationGuard.Validate(
            changed.RootElement, Root()));
    }

    [Fact]
    public void Historical41FContractStaysInternallyValidButRejectsCurrentLiveIdentity()
    {
        var path = Path.Combine(Root(), "docs",
            "lane_0_corrective_successor_integration_prebinding_audit_remediation_contract.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Empty(IntegrationPrebindingAuditRemediationGuard.Validate(
            document.RootElement, Root()));
        Assert.NotEmpty(IntegrationPrebindingAuditRemediationGuard.ValidateAgainstLive(
            document.RootElement, Root()));
    }

    private static string ContractPath() => Path.Combine(Root(), "docs",
        "lane_0_corrective_successor_integration_final_authority_boundary_remediation_contract.json");
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName,
            "ManiaAddNotesLab.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
