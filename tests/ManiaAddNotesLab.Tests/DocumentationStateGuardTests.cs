using DocConsistencyTool;

namespace ManiaAddNotesLab.Tests;

public sealed class DocumentationStateGuardTests
{
    [Fact]
    public void ConsistentLivingStateBlockPassesRegardlessOfFieldOrder()
    {
        var reordered = StateBlock()
            .Replace("Current phase: E.1 — COMPLETE — OUTCOME B<br>\n", string.Empty)
            .Replace("<!-- PROJECT-STATE:END -->",
                "Current phase: E.1 — COMPLETE — OUTCOME B<br>\n<!-- PROJECT-STATE:END -->");

        var errors = DocumentationStateGuard.ValidateStateBlock("README.md", reordered, Expected);
        Assert.True(errors.IsEmpty, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void StaleReadmeCurrentPhaseFails()
    {
        var errors = DocumentationStateGuard.ValidateStateBlock("README.md",
            StateBlock().Replace("Current phase: E.1", "Current phase: E"), Expected);

        Assert.Contains(errors, error => error.Contains("current phase", StringComparison.Ordinal));
    }

    [Fact]
    public void StaleProjectStatusBlockerFails()
    {
        var errors = DocumentationStateGuard.ValidateStateBlock("PROJECT_STATUS.md",
            StateBlock().Replace("F2.ACQ — BLOCKED", "F2.ACQ — NEXT"), Expected);

        Assert.Contains(errors, error => error.Contains("blocked prerequisite", StringComparison.Ordinal));
    }

    [Fact]
    public void RoadmapStaleCurrentPhaseFails()
    {
        var errors = DocumentationStateGuard.ValidateRoadmap(
            Roadmap().Replace("E.1 — Current", "E — Current"), Expected);

        Assert.Contains(errors, error => error.Contains("current phase E.1", StringComparison.Ordinal));
    }

    [Fact]
    public void RoadmapStaleNextActionablePhaseFails()
    {
        var errors = DocumentationStateGuard.ValidateRoadmap(
            Roadmap().Replace("D1.0 — Composition research", "E.2 — Adaptive"), Expected);

        Assert.Contains(errors, error => error.Contains("next actionable phase D1", StringComparison.Ordinal));
    }

    [Fact]
    public void RoadmapStaleBlockedPrerequisiteFails()
    {
        var errors = DocumentationStateGuard.ValidateRoadmap(
            Roadmap().Replace("BLOCKED / CONDITIONAL", "NEXT / CONDITIONAL"), Expected);

        Assert.Contains(errors, error => error.Contains("blocked prerequisite F2.ACQ", StringComparison.Ordinal));
    }

    [Fact]
    public void BehaviorPolicyMismatchFails()
    {
        var errors = DocumentationStateGuard.ValidateStateBlock("README.md",
            StateBlock().Replace("legacy-experimental.1", "experimental.2"), Expected);

        Assert.Contains(errors, error => error.Contains("behavior policy", StringComparison.Ordinal));
    }

    [Fact]
    public void NextResearchCandidateDoesNotAuthorizeBehavioralPhase()
    {
        var errors = DocumentationStateGuard.ValidateStateBlock("README.md",
            StateBlock().Replace("Next behavioral authorization: NOT_AUTHORIZED",
                "Next behavioral authorization: AUTHORIZED"), Expected);

        Assert.Contains(errors, error => error.Contains("behavioral authorization", StringComparison.Ordinal));
    }

    [Fact]
    public void ResearchShadowContractWithNoBehaviorChangePasses()
    {
        Assert.Empty(DocumentationStateGuard.ValidatePhaseContracts([
            new PhaseContractProjection("D1.0", "ResearchShadow", false, "NEXT", "NOT_AUTHORIZED")
        ]));
    }

    [Fact]
    public void BehavioralContractWithBehaviorChangePasses()
    {
        Assert.Empty(DocumentationStateGuard.ValidatePhaseContracts([
            new PhaseContractProjection("D1", "BehaviorChanging", true, "FUTURE", "NOT_AUTHORIZED")
        ]));
    }

    [Fact]
    public void BehavioralContractMarkedNoBehaviorChangeFails()
    {
        var errors = DocumentationStateGuard.ValidatePhaseContracts([
            new PhaseContractProjection("D1", "BehaviorChanging", false, "FUTURE", "NOT_AUTHORIZED")
        ]);

        Assert.Contains(errors, error => error.Contains("behaviorChange=true", StringComparison.Ordinal));
    }

    [Fact]
    public void ResearchShadowContractMarkedBehaviorChangeFails()
    {
        var errors = DocumentationStateGuard.ValidatePhaseContracts([
            new PhaseContractProjection("D1.0", "ResearchShadow", true, "NEXT", "NOT_AUTHORIZED")
        ]);

        Assert.Contains(errors, error => error.Contains("behaviorChange=false", StringComparison.Ordinal));
    }

    [Fact]
    public void BlockedAndDeferredContractsRemainDistinct()
    {
        Assert.Empty(DocumentationStateGuard.ValidatePhaseContracts([
            new PhaseContractProjection("F2.ACQ", "BlockedPrerequisite", false, "BLOCKED", "CONDITIONAL_ON_EXTERNAL_DATA"),
            new PhaseContractProjection("C2", "Deferred", null, "DEFERRED", null)
        ]));
    }

    [Fact]
    public void TestSnapshotMismatchFails()
    {
        var errors = DocumentationStateGuard.ValidateStateBlock("README.md",
            StateBlock().Replace("518 passed", "517 passed"), Expected);

        Assert.Contains(errors, error => error.Contains("test snapshot", StringComparison.Ordinal));
    }

    [Fact]
    public void ExactPhaseRowsKeepBlockedPrerequisiteSeparateFromActionableCandidate()
    {
        var roadmap = Roadmap() + "\n| F2 — Parent | COMPLETE | Historical branch row. |";

        Assert.Empty(DocumentationStateGuard.ValidateRoadmap(roadmap, Expected));
        Assert.Contains("D1.0 — Composition research", DocumentationStateGuard.FindPhaseRow(roadmap, "D1.0"));
        Assert.Contains("D1 — ChordCompletion", DocumentationStateGuard.FindPhaseRow(roadmap, "D1"));
        Assert.Contains("F2.ACQ — Acquisition", DocumentationStateGuard.FindPhaseRow(roadmap, "F2.ACQ"));
        Assert.Contains("F2 — Parent", DocumentationStateGuard.FindPhaseRow(roadmap, "F2"));
    }

    private static DocumentationStateExpectation Expected => new(
        "E.1", "COMPLETE", "B", "D1.0", "Resulting-State Composition Feasibility / Shadow",
        "NOT_AUTHORIZED", "D1", "ChordCompletion Resulting-State A/B", "NOT_AUTHORIZED",
        "F2.ACQ", "BLOCKED", "F2", "CONTINUE_CONDITIONALLY",
        "legacy-experimental.1", "none", 518, 0, 0);

    private static string StateBlock() => """
        Historical reports are deliberately outside this living-state projection.
        <!-- PROJECT-STATE:BEGIN -->
        Current phase: E.1 — COMPLETE — OUTCOME B<br>
        Next actionable research candidate: D1.0 — Resulting-State Composition Feasibility / Shadow<br>
        Next actionable authorization: NOT_AUTHORIZED<br>
        Next behavioral phase: D1 — ChordCompletion Resulting-State A/B<br>
        Next behavioral authorization: NOT_AUTHORIZED<br>
        Blocked prerequisite: F2.ACQ — BLOCKED<br>
        Research branch: F2 — CONTINUE CONDITIONALLY<br>
        Behavior policy: `legacy-experimental.1`<br>
        Behavior change: none<br>
        Tests: 518 passed / 0 failed / 0 skipped
        <!-- PROJECT-STATE:END -->
        """;

    private static string Roadmap() => """
        | Fase | Estado | Propósito |
        |---|---|---|
        | E.1 — Current | COMPLETE — OUTCOME B | Closed. |
        | D1.0 — Composition research | NEXT / NOT_AUTHORIZED | Actionable candidate. |
        | D1 — ChordCompletion Resulting-State A/B | FUTURE / NOT_AUTHORIZED | Behavioral phase. |
        | F2.ACQ — Acquisition | BLOCKED / CONDITIONAL | External dependency. |
        """;
}
