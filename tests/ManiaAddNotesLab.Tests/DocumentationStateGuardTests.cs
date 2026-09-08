using DocConsistencyTool;

namespace ManiaAddNotesLab.Tests;

public sealed class DocumentationStateGuardTests
{
    [Fact]
    public void ConsistentLivingStateBlockPassesRegardlessOfFieldOrder()
    {
        var reordered = StateBlock()
            .Replace("Current phase: E — COMPLETE — OUTCOME B<br>\n", string.Empty)
            .Replace("<!-- PROJECT-STATE:END -->",
                "Current phase: E — COMPLETE — OUTCOME B<br>\n<!-- PROJECT-STATE:END -->");

        var errors = DocumentationStateGuard.ValidateStateBlock("README.md", reordered, Expected);
        Assert.True(errors.IsEmpty, string.Join(Environment.NewLine, errors));
    }

    [Fact]
    public void StaleReadmeCurrentPhaseFails()
    {
        var errors = DocumentationStateGuard.ValidateStateBlock("README.md",
            StateBlock().Replace("Current phase: E", "Current phase: F2.3"), Expected);

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
            Roadmap().Replace("E — Current", "F2.3 — Current"), Expected);

        Assert.Contains(errors, error => error.Contains("current phase E", StringComparison.Ordinal));
    }

    [Fact]
    public void RoadmapStaleNextActionablePhaseFails()
    {
        var errors = DocumentationStateGuard.ValidateRoadmap(
            Roadmap().Replace("E.1 — Adaptive", "D1 — Adaptive"), Expected);

        Assert.Contains(errors, error => error.Contains("next actionable phase E.1", StringComparison.Ordinal));
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
    public void TestSnapshotMismatchFails()
    {
        var errors = DocumentationStateGuard.ValidateStateBlock("README.md",
            StateBlock().Replace("487 passed", "486 passed"), Expected);

        Assert.Contains(errors, error => error.Contains("test snapshot", StringComparison.Ordinal));
    }

    [Fact]
    public void ExactPhaseRowsKeepBlockedPrerequisiteSeparateFromActionableCandidate()
    {
        var roadmap = Roadmap() + "\n| F2 — Parent | COMPLETE | Historical branch row. |";

        Assert.Empty(DocumentationStateGuard.ValidateRoadmap(roadmap, Expected));
        Assert.Contains("E.1 — Adaptive", DocumentationStateGuard.FindPhaseRow(roadmap, "E.1"));
        Assert.Contains("F2.ACQ — Acquisition", DocumentationStateGuard.FindPhaseRow(roadmap, "F2.ACQ"));
        Assert.Contains("F2 — Parent", DocumentationStateGuard.FindPhaseRow(roadmap, "F2"));
    }

    private static DocumentationStateExpectation Expected => new(
        "E", "COMPLETE", "B", "E.1", "Exact Recurrence Failure Stratification / Shadow",
        "NOT_AUTHORIZED", "F2.ACQ", "BLOCKED", "F2", "CONTINUE_CONDITIONALLY",
        "legacy-experimental.1", "none", 487, 0, 0);

    private static string StateBlock() => """
        Historical reports are deliberately outside this living-state projection.
        <!-- PROJECT-STATE:BEGIN -->
        Current phase: E — COMPLETE — OUTCOME B<br>
        Next actionable research candidate: E.1 — Exact Recurrence Failure Stratification / Shadow<br>
        Next actionable authorization: NOT_AUTHORIZED<br>
        Blocked prerequisite: F2.ACQ — BLOCKED<br>
        Research branch: F2 — CONTINUE CONDITIONALLY<br>
        Behavior policy: `legacy-experimental.1`<br>
        Behavior change: none<br>
        Tests: 487 passed / 0 failed / 0 skipped
        <!-- PROJECT-STATE:END -->
        """;

    private static string Roadmap() => """
        | Fase | Estado | Propósito |
        |---|---|---|
        | E — Current | COMPLETE — OUTCOME B | Closed. |
        | E.1 — Adaptive | NEXT / NOT_AUTHORIZED | Actionable candidate. |
        | F2.ACQ — Acquisition | BLOCKED / CONDITIONAL | External dependency. |
        """;
}
