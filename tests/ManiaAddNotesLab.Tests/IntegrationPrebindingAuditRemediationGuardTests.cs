using System.Text.Json;
using System.Text.Json.Nodes;
using DocConsistencyTool;
using Xunit;

namespace ManiaAddNotesLab.Tests;

public sealed class IntegrationPrebindingAuditRemediationGuardTests
{
    [Fact]
    public void CurrentRemediationContractAndLiveComponentsAreConsistent()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath()));
        var actualIdentity = IntegrationImplementationGuard.CanonicalJsonHash(
            document.RootElement.GetProperty("contract"));
        Assert.True(actualIdentity == IntegrationPrebindingAuditRemediationGuard.ContractIdentity,
            actualIdentity);
        Assert.Empty(IntegrationPrebindingAuditRemediationGuard.Validate(
            document.RootElement, Root()));
        Assert.NotEmpty(IntegrationPrebindingAuditRemediationGuard.ValidateAgainstLive(
            document.RootElement, Root()));
    }

    [Theory]
    [InlineData("lineage")]
    [InlineData("a1")]
    [InlineData("a2")]
    [InlineData("a3")]
    [InlineData("a4")]
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
            case "lineage": contract["lineage"]!["prebindingHardeningContractSha256"] = "BAD"; break;
            case "a1": contract["findingDispositions"]!["PB-A1"]!["distributedOrGlobalAuthorityClaimed"] = true; break;
            case "a2": contract["findingDispositions"]!["PB-A2"]!["disposition"] = "OPEN"; break;
            case "a3": contract["findingDispositions"]!["PB-A3"]!["verifiedPostScience"] = false; break;
            case "a4": contract["findingDispositions"]!["PB-A4"]!["callerSuppliesReceiptPathOrNamespace"] = true; break;
            case "component": contract["currentComponents"]!["officialDependencySealedRunner"]!["normalizedTextTreeSha256"] = "BAD"; break;
            case "order": contract["authorityOrder"]!.AsArray().RemoveAt(0); break;
            case "boundary": contract["phaseBoundary"]!["realReceiptCreated"] = true; break;
            case "declared": root["canonicalSha256"] = "BAD"; break;
        }
        using var changed = JsonDocument.Parse(root.ToJsonString());
        Assert.NotEmpty(IntegrationPrebindingAuditRemediationGuard.Validate(
            changed.RootElement, Root()));
    }

    private static string ContractPath() => Path.Combine(Root(), "docs",
        "lane_0_corrective_successor_integration_prebinding_audit_remediation_contract.json");
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName,
            "ManiaAddNotesLab.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
