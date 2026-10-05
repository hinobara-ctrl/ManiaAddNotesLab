using System.Text.Json;
using System.Text.Json.Nodes;
using DocConsistencyTool;
using Xunit;

namespace ManiaAddNotesLab.Tests;

public sealed class IntegrationHistoricalValidationPortabilityRemediationGuardTests
{
    [Fact]
    public void CurrentContractHistoricalDeclarationAndLiveComponentsAreConsistent()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath()));

        Assert.Equal(IntegrationHistoricalValidationPortabilityRemediationGuard.ContractIdentity,
            IntegrationImplementationGuard.CanonicalJsonHash(
                document.RootElement.GetProperty("contract")));
        Assert.Empty(IntegrationHistoricalValidationPortabilityRemediationGuard.Validate(
            document.RootElement, Root()));
    }

    [Theory]
    [InlineData("finding")]
    [InlineData("historical-contract")]
    [InlineData("historical-validator")]
    [InlineData("exact-bytes")]
    [InlineData("portable-invariant")]
    [InlineData("fail-closed")]
    [InlineData("intent")]
    [InlineData("closure")]
    [InlineData("component")]
    [InlineData("boundary")]
    [InlineData("declared")]
    public void SemanticOrDeclaredDriftFailsClosed(string mutation)
    {
        var root = JsonNode.Parse(File.ReadAllText(ContractPath()))!.AsObject();
        var contract = root["contract"]!.AsObject();
        switch (mutation)
        {
            case "finding": contract["rootFinding"]!["disposition"] = "OPEN"; break;
            case "historical-contract":
                contract["historicalBoundary"]!["historicalContractModified"] = true; break;
            case "historical-validator":
                contract["historicalBoundary"]!["historicalValidatorModified"] = true; break;
            case "exact-bytes":
                contract["historicalBoundary"]!["exactByteSemantics"] = "normalized"; break;
            case "portable-invariant":
                contract["historicalBoundary"]!["currentCheckoutReusedDependenciesPortableInvariant"] = true;
                break;
            case "fail-closed":
                contract["historicalBoundary"]!["reusedDependencyFalseStillInvalid"] = false; break;
            case "intent": contract["testIntent"]!["requiresReusedDependenciesTrue"] = true; break;
            case "closure": contract["preservedAuditClosures"]!["AP-R1"] = "OPEN"; break;
            case "component":
                contract["currentComponents"]!["atomicAuthorityRuntimeWorker"]![
                    "normalizedTextTreeSha256"] = "BAD"; break;
            case "boundary": contract["phaseBoundary"]!["realExecution"] = true; break;
            case "declared": root["canonicalSha256"] = "BAD"; break;
        }

        using var changed = JsonDocument.Parse(root.ToJsonString());
        Assert.NotEmpty(IntegrationHistoricalValidationPortabilityRemediationGuard.Validate(
            changed.RootElement, Root()));
    }

    [Fact]
    public void Historical27BARemainsInternallyAndLiveValid()
    {
        var path = Path.Combine(Root(), "docs",
            "lane_0_corrective_successor_integration_prebinding_atomicity_provenance_remediation_contract.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        Assert.Empty(IntegrationPrebindingAtomicityProvenanceRemediationGuard.Validate(
            document.RootElement, Root()));
    }

    private static string ContractPath() => Path.Combine(Root(), "docs",
        "lane_0_corrective_successor_integration_historical_validation_portability_remediation_contract.json");

    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName,
            "ManiaAddNotesLab.sln"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
