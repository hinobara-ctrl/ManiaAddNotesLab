using System.Collections.Immutable;

namespace DocConsistencyTool;

public sealed record DocumentationStateExpectation(
    string CurrentPhaseId,
    string CurrentPhaseStatus,
    string? CurrentPhaseOutcome,
    string? NextActionablePhaseId,
    string? NextActionablePhaseName,
    string NextActionableAuthorization,
    string? NextBehavioralPhaseId,
    string? NextBehavioralPhaseName,
    string NextBehavioralAuthorization,
    string BlockedPrerequisiteId,
    string BlockedPrerequisiteStatus,
    string BranchId,
    string BranchDecision,
    string BehaviorPolicyVersion,
    string BehaviorChange,
    string Retry,
    string Successor,
    int TestsPassed,
    int TestsFailed,
    int TestsSkipped);

public sealed record PhaseContractProjection(
    string Id,
    string ContractKind,
    bool? BehaviorChange,
    string Status,
    string? Authorization);

public static class DocumentationStateGuard
{
    public static ImmutableArray<string> ValidatePhaseContracts(
        IEnumerable<PhaseContractProjection> phases)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        var values = phases.ToArray();
        foreach (var duplicate in values.GroupBy(x => x.Id, StringComparer.Ordinal)
                     .Where(x => x.Count() > 1))
            errors.Add($"Duplicate phase contract: {duplicate.Key}.");

        foreach (var phase in values)
        {
            switch (phase.ContractKind)
            {
                case "ResearchShadow":
                    if (phase.BehaviorChange is not false)
                        errors.Add($"ResearchShadow phase {phase.Id} must declare behaviorChange=false.");
                    break;
                case "BehaviorChanging":
                    if (phase.BehaviorChange is not true)
                        errors.Add($"BehaviorChanging phase {phase.Id} must declare behaviorChange=true.");
                    break;
                case "BlockedPrerequisite":
                    if (phase.Status != "BLOCKED")
                        errors.Add($"BlockedPrerequisite phase {phase.Id} must have status BLOCKED.");
                    break;
                case "Deferred":
                    if (phase.Status != "DEFERRED")
                        errors.Add($"Deferred phase {phase.Id} must have status DEFERRED.");
                    break;
                default:
                    errors.Add($"Phase {phase.Id} has unknown contract kind {phase.ContractKind}.");
                    break;
            }
        }

        return errors.ToImmutable();
    }

    public static ImmutableArray<string> ValidateStateBlock(string documentName, string markdown,
        DocumentationStateExpectation expected)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        var fields = ParseStateBlock(documentName, markdown, errors);
        if (fields is null) return errors.ToImmutable();
        var currentValue = $"{expected.CurrentPhaseId} — {expected.CurrentPhaseStatus}"
            + (expected.CurrentPhaseOutcome is null ? string.Empty : $" — OUTCOME {expected.CurrentPhaseOutcome}");
        EqualNormalized("Current phase", currentValue, "current phase");
        EqualNormalized("Next actionable research candidate",
            expected.NextActionablePhaseId is null
                ? "none"
                : $"{expected.NextActionablePhaseId} — {expected.NextActionablePhaseName}",
            "next actionable phase");
        Equal("Next actionable authorization", expected.NextActionableAuthorization,
            "next actionable authorization");
        EqualNormalized("Next behavioral phase",
            expected.NextBehavioralPhaseId is null
                ? "none"
                : $"{expected.NextBehavioralPhaseId} — {expected.NextBehavioralPhaseName}",
            "next behavioral phase");
        Equal("Next behavioral authorization", expected.NextBehavioralAuthorization,
            "next behavioral authorization");
        EqualNormalized("Blocked prerequisite",
            $"{expected.BlockedPrerequisiteId} — {expected.BlockedPrerequisiteStatus}",
            "blocked prerequisite");
        EqualNormalized("Research branch", $"{expected.BranchId} — {expected.BranchDecision}",
            "research branch");
        Equal("Behavior policy", $"`{expected.BehaviorPolicyVersion}`", "behavior policy");
        Equal("Behavior change", expected.BehaviorChange, "behavior change");
        Equal("Retry", expected.Retry, "retry authority");
        Equal("Successor", expected.Successor, "successor authority");
        Equal("Tests", $"{expected.TestsPassed} passed / {expected.TestsFailed} failed / " +
            $"{expected.TestsSkipped} skipped", "test snapshot");
        return errors.ToImmutable();

        void Equal(string field, string wanted, string meaning)
        {
            if (!fields.TryGetValue(field, out var actual) || !string.Equals(actual, wanted,
                    StringComparison.Ordinal))
                errors.Add($"{documentName} PROJECT-STATE block does not reflect {meaning}: {wanted}");
        }
        void EqualNormalized(string field, string wanted, string meaning)
        {
            if (!fields.TryGetValue(field, out var actual)
                || !string.Equals(Normalize(actual), Normalize(wanted), StringComparison.Ordinal))
                errors.Add($"{documentName} PROJECT-STATE block does not reflect {meaning}: {wanted}");
        }
    }

    public static ImmutableArray<string> ValidatePostAttemptCurrentState(
        string currentPhase,
        bool postAttemptForensicsPresent,
        bool postAttemptPhasePresent,
        string? attempt,
        string? retry,
        string? successor)
    {
        const string expectedPhase = "LANE.0.CORRECTIVE_POST_ATTEMPT_FORENSICS";
        const string successorPhase = "LANE.0.CORRECTIVE_SUCCESSOR.PREREGISTRATION";
        const string auditPhase = "LANE.0.CORRECTIVE_SUCCESSOR.PHASE1_AUDIT";
        const string phase2 = "LANE.0.CORRECTIVE_SUCCESSOR.EXECUTION_PREPARATION";
        const string phase2Audit = "LANE.0.CORRECTIVE_SUCCESSOR.PHASE2_AUDIT";
        var errors = ImmutableArray.CreateBuilder<string>();
        if (!postAttemptForensicsPresent)
            errors.Add("Current state omits the post-attempt forensic object.");
        if (!postAttemptPhasePresent)
            errors.Add("Current state omits the post-attempt forensic phase.");
        if (postAttemptForensicsPresent
            && currentPhase is not (expectedPhase or successorPhase or auditPhase or phase2 or phase2Audit))
            errors.Add($"Consumed corrective attempt current phase requires post-attempt closure or its preregistered successor, not {currentPhase}.");
        if (attempt != "AUTHORIZED_CORRECTIVE_ATTEMPT_CONSUMED")
            errors.Add("Post-attempt current state must record the authorized corrective attempt as consumed.");
        if (retry != "PROHIBITED")
            errors.Add("Post-attempt current state must prohibit retry.");
        if (successor != "NOT_YET_PREREGISTERED_NOT_AUTHORIZED")
            errors.Add("Post-attempt current state must not authorize a successor.");
        return errors.ToImmutable();
    }

    public static ImmutableArray<string> ValidateHistoricalCounterClosureIdentities(
        string expectedCanonicalContractIdentity,
        string recomputedCanonicalContractIdentity,
        string? declaredCanonicalContractIdentity,
        string expectedHistoricalImplementationIdentity,
        string currentRepairImplementationIdentity,
        string contractRepairImplementationIdentity,
        string stateRepairImplementationIdentity,
        string expectedHistoricalHarnessIdentity,
        string contractHarnessIdentity,
        string stateHarnessIdentity,
        string stateContractIdentity)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        if (recomputedCanonicalContractIdentity != expectedCanonicalContractIdentity
            || declaredCanonicalContractIdentity != expectedCanonicalContractIdentity
            || stateContractIdentity != expectedCanonicalContractIdentity)
            errors.Add("Historical counter-closure canonical contract identity drifted.");
        if (currentRepairImplementationIdentity != expectedHistoricalImplementationIdentity
            || contractRepairImplementationIdentity != expectedHistoricalImplementationIdentity
            || stateRepairImplementationIdentity != expectedHistoricalImplementationIdentity)
            errors.Add("Historical counter-closure repair implementation identity drifted.");
        if (contractHarnessIdentity != expectedHistoricalHarnessIdentity
            || stateHarnessIdentity != expectedHistoricalHarnessIdentity)
            errors.Add("Historical counter-closure harness snapshot drifted.");
        return errors.ToImmutable();
    }

    public static ImmutableArray<string> ValidateRoadmap(string markdown,
        DocumentationStateExpectation expected)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        CheckRow(expected.CurrentPhaseId, expected.CurrentPhaseStatus, expected.CurrentPhaseOutcome,
            "current phase");
        if (expected.NextActionablePhaseId is not null)
            CheckRow(expected.NextActionablePhaseId, "NEXT", expected.NextActionableAuthorization,
                "next actionable phase");
        if (expected.NextBehavioralPhaseId is not null)
            CheckRow(expected.NextBehavioralPhaseId, "FUTURE", expected.NextBehavioralAuthorization,
                "next behavioral phase");
        CheckRow(expected.BlockedPrerequisiteId, expected.BlockedPrerequisiteStatus, null,
            "blocked prerequisite");
        return errors.ToImmutable();

        void CheckRow(string id, string status, string? extra, string meaning)
        {
            var row = FindPhaseRow(markdown, id);
            if (row is null)
            {
                errors.Add($"ROADMAP.md has no exact row for {meaning} {id}.");
                return;
            }
            if (!Normalize(row).Contains(Normalize(status), StringComparison.Ordinal))
                errors.Add($"ROADMAP.md {meaning} {id} does not reflect status {status}.");
            if (extra is not null && !Normalize(row).Contains(Normalize(extra), StringComparison.Ordinal))
                errors.Add($"ROADMAP.md {meaning} {id} does not reflect {extra}.");
        }
    }

    public static string? FindPhaseRow(string markdown, string phaseId) => markdown
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault(line =>
        {
            if (!line.StartsWith('|')) return false;
            var firstCell = line.Split('|').Skip(1).FirstOrDefault()?.Trim() ?? string.Empty;
            var id = firstCell.Split('—', 2)[0].Trim();
            return string.Equals(id, phaseId, StringComparison.Ordinal);
        });

    private static Dictionary<string, string>? ParseStateBlock(string documentName, string markdown,
        ImmutableArray<string>.Builder errors)
    {
        const string begin = "<!-- PROJECT-STATE:BEGIN -->";
        const string end = "<!-- PROJECT-STATE:END -->";
        var start = markdown.IndexOf(begin, StringComparison.Ordinal);
        var finish = markdown.IndexOf(end, StringComparison.Ordinal);
        if (start < 0 || finish <= start
            || markdown.IndexOf(begin, start + begin.Length, StringComparison.Ordinal) >= 0
            || markdown.IndexOf(end, finish + end.Length, StringComparison.Ordinal) >= 0)
        {
            errors.Add($"{documentName} has no single valid PROJECT-STATE block.");
            return null;
        }
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in markdown[(start + begin.Length)..finish]
                     .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.EndsWith("<br>", StringComparison.Ordinal)) line = line[..^4];
            var separator = line.IndexOf(':');
            if (separator <= 0 || !fields.TryAdd(line[..separator].Trim(), line[(separator + 1)..].Trim()))
                errors.Add($"{documentName} PROJECT-STATE block contains an invalid or duplicate field: {raw}");
        }
        return fields;
    }

    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit)
        .Select(char.ToUpperInvariant).ToArray());
}
