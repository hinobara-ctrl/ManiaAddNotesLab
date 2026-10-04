using System.Text.Json;
using System.Text.Json.Nodes;
using DocConsistencyTool;
using Xunit;

namespace ManiaAddNotesLab.Tests;

public sealed class IntegrationImplementationGuardTests
{
    [Fact]
    public void PublishedHistoricalContractIsInternallyConsistent()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath()));
        Assert.Equal(IntegrationImplementationGuard.ContractIdentity,
            IntegrationImplementationGuard.CanonicalJsonHash(document.RootElement.GetProperty("contract")));
        Assert.Empty(IntegrationImplementationGuard.Validate(document.RootElement, Root()));
    }

    [Fact]
    public void HistoricalRouteRejectsTheRemediatedLiveIdentityWithoutInvalidatingItsSnapshot()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ContractPath()));
        Assert.Empty(IntegrationImplementationGuard.Validate(document.RootElement, Root()));
        Assert.NotEmpty(IntegrationImplementationGuard.ValidateAgainstLive(document.RootElement, Root()));
    }

    [Theory]
    [InlineData("parent")]
    [InlineData("authority")]
    [InlineData("corpus")]
    [InlineData("component")]
    [InlineData("manifest")]
    [InlineData("boundary")]
    [InlineData("declared")]
    public void SemanticOrDeclaredDriftFailsClosed(string mutation)
    {
        var root = JsonNode.Parse(File.ReadAllText(ContractPath()))!.AsObject();
        var contract = root["contract"]!.AsObject();
        switch (mutation)
        {
            case "parent": contract["parentIntegrationPreregistrationSha256"] = "BAD"; break;
            case "authority": contract["authorityOrder"]!.AsArray().RemoveAt(0); break;
            case "corpus": contract["corpusAdmission"]!["locationCount"] = 11; break;
            case "component": contract["components"]!["officialFrozenC11Adapter"]!["normalizedTextTreeSha256"] = "BAD"; break;
            case "manifest": contract["frozenDependencies"]!["verifiedFrozenC11ManifestSha256"] = "BAD"; break;
            case "boundary": contract["phaseBoundary"]!["c11Accessed"] = true; break;
            case "declared": root["canonicalSha256"] = "BAD"; break;
        }
        using var mutated = JsonDocument.Parse(root.ToJsonString());
        Assert.NotEmpty(IntegrationImplementationGuard.Validate(mutated.RootElement, Root()));
    }

    private static string ContractPath() => Path.Combine(Root(), "docs",
        "lane_0_corrective_successor_integration_execution_contract.json");
    private static string Root()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ManiaAddNotesLab.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
