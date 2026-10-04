using System.Text.Json;
using DocConsistencyTool;

namespace ManiaAddNotesLab.Tests;

public sealed class IntegrationPreregistrationAuditClosureTests
{
    [Fact]
    public void PublishedPreregistrationRemainsFrozenAndAuditClosureIsNonAuthorizing()
    {
        var root = Root();
        using var contractArtifact = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs",
            "lane_0_corrective_successor_integration_preregistration_contract.json")));
        const string expectedIdentity =
            "7C0A86BF3C6647CA7092C4D3390EB97EA6E11D7E5429ECBA6F499D55C5417A02";
        Assert.Equal(expectedIdentity,
            contractArtifact.RootElement.GetProperty("canonicalSha256").GetString());
        var contract = contractArtifact.RootElement.GetProperty("contract");
        Assert.Equal(expectedIdentity, IntegrationPreregistrationGuard.CanonicalJsonHash(contract));
        Assert.Empty(IntegrationPreregistrationGuard.Validate(contract));

        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs",
            "PROJECT_STATE.json")));
        var stateRoot = state.RootElement;
        // The historical audit remains frozen even though the canonical current phase advances.
        Assert.Equal("LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_PREBINDING_HARDENING",
            stateRoot.GetProperty("currentPhase").GetString());
        Assert.Equal(JsonValueKind.Null, stateRoot.GetProperty("nextRecommendedPhase").ValueKind);
        Assert.Equal(JsonValueKind.Null, stateRoot.GetProperty("nextBehavioralPhase").ValueKind);
        Assert.Equal("HUMAN_REVIEW_REQUIRED",
            stateRoot.GetProperty("nextRecommendedAction").GetString());

        var audit = stateRoot.GetProperty(
            "lane0CorrectiveSuccessorIntegrationPreregistrationAudit");
        Assert.Equal("AUDIT_CLEAN_WITH_NON_BLOCKING_OBSERVATIONS",
            audit.GetProperty("outcome").GetString());
        Assert.Equal(expectedIdentity, audit.GetProperty("preregistrationContractHash").GetString());
        Assert.Equal("NOT_STARTED", audit.GetProperty("integrationStatus").GetString());
        Assert.Equal("NOT_AUTHORIZED", audit.GetProperty("integrationAuthorization").GetString());
        Assert.Equal("NO_C11_AUTHORIZATION", audit.GetProperty("c11Authorization").GetString());
        Assert.False(audit.GetProperty("bindingPresent").GetBoolean());
        Assert.False(audit.GetProperty("receiptPresent").GetBoolean());
        Assert.False(audit.GetProperty("c11Accessed").GetBoolean());
        Assert.False(audit.GetProperty("executionAuthorized").GetBoolean());
        Assert.False(audit.GetProperty("behaviorChange").GetBoolean());
        Assert.False(audit.GetProperty("rngChange").GetBoolean());
        Assert.False(audit.GetProperty("defaultChange").GetBoolean());

        var implementation = stateRoot.GetProperty(
            "lane0CorrectiveSuccessorIntegrationImplementation");
        Assert.Equal(expectedIdentity,
            implementation.GetProperty("parentIntegrationPreregistrationSha256").GetString());
        Assert.Equal("AUTHORIZED_FOR_IMPLEMENTATION_ONLY",
            implementation.GetProperty("integrationAuthorization").GetString());
        Assert.Equal("NO_C11_AUTHORIZATION",
            implementation.GetProperty("c11Authorization").GetString());
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ManiaAddNotesLab.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
