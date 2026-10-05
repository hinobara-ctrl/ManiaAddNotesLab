using System.Text.Json;
using System.Text.Json.Nodes;
using DocConsistencyTool;
using Xunit;

namespace ManiaAddNotesLab.Tests;

public sealed class IntegrationPrebindingAtomicityProvenanceRemediationGuardTests
{
    [Fact]
    public void CurrentContractAndLiveComponentsAreConsistent()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath()));
        Assert.Empty(IntegrationPrebindingAtomicityProvenanceRemediationGuard.Validate(
            document.RootElement, Root()));
    }

    [Theory]
    [InlineData("finding")]
    [InlineData("creator")]
    [InlineData("pre-crash")]
    [InlineData("hardlink")]
    [InlineData("attestation")]
    [InlineData("handshake")]
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
            case "finding": contract["rootFindingDispositions"]!["AP-R1"] = "OPEN"; break;
            case "creator": contract["receiptAtomicity"]!["workerCreatesCanonicalReceipt"] = true; break;
            case "pre-crash": contract["receiptAtomicity"]!["preReceiptCrashConsumes"] = true; break;
            case "hardlink": contract["checkoutProvenance"]!["requiredUsedHardlinks"] = true; break;
            case "attestation": contract["checkoutProvenance"]!["callerAttestationIsAuthoritySource"] = true; break;
            case "handshake":
                var handshake = contract["handshake"]!.AsArray();
                var first = handshake[0]; handshake.RemoveAt(0); handshake.Add(first); break;
            case "component": contract["currentComponents"]!["atomicAuthorityRuntimeWorker"]!["normalizedTextTreeSha256"] = "BAD"; break;
            case "order": contract["authorityOrder"]!.AsArray().RemoveAt(12); break;
            case "boundary": contract["phaseBoundary"]!["realReceiptCreated"] = true; break;
            case "declared": root["canonicalSha256"] = "BAD"; break;
        }
        using var changed = JsonDocument.Parse(root.ToJsonString());
        Assert.NotEmpty(IntegrationPrebindingAtomicityProvenanceRemediationGuard.Validate(
            changed.RootElement, Root()));
    }

    [Fact]
    public void HistoricalB4489DRemainsInternallyValidButRejectsCurrentLiveIdentity()
    {
        var path = Path.Combine(Root(), "docs",
            "lane_0_corrective_successor_integration_final_authority_boundary_remediation_contract.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Empty(IntegrationFinalAuthorityBoundaryRemediationGuard.Validate(
            document.RootElement, Root()));
        Assert.NotEmpty(IntegrationFinalAuthorityBoundaryRemediationGuard.ValidateAgainstLive(
            document.RootElement, Root()));
    }

    private static string ContractPath() => Path.Combine(Root(), "docs",
        "lane_0_corrective_successor_integration_prebinding_atomicity_provenance_remediation_contract.json");
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName,
            "ManiaAddNotesLab.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
