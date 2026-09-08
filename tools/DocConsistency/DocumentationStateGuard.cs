using System.Collections.Immutable;

namespace DocConsistencyTool;

public sealed record DocumentationStateExpectation(
    string CurrentPhaseId,
    string CurrentPhaseStatus,
    string? CurrentPhaseOutcome,
    string NextActionablePhaseId,
    string NextActionablePhaseName,
    string NextActionableAuthorization,
    string BlockedPrerequisiteId,
    string BlockedPrerequisiteStatus,
    string BranchId,
    string BranchDecision,
    string BehaviorPolicyVersion,
    string BehaviorChange,
    int TestsPassed,
    int TestsFailed,
    int TestsSkipped);

public static class DocumentationStateGuard
{
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
            $"{expected.NextActionablePhaseId} — {expected.NextActionablePhaseName}",
            "next actionable phase");
        Equal("Next actionable authorization", expected.NextActionableAuthorization,
            "next actionable authorization");
        EqualNormalized("Blocked prerequisite",
            $"{expected.BlockedPrerequisiteId} — {expected.BlockedPrerequisiteStatus}",
            "blocked prerequisite");
        EqualNormalized("Research branch", $"{expected.BranchId} — {expected.BranchDecision}",
            "research branch");
        Equal("Behavior policy", $"`{expected.BehaviorPolicyVersion}`", "behavior policy");
        Equal("Behavior change", expected.BehaviorChange, "behavior change");
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

    public static ImmutableArray<string> ValidateRoadmap(string markdown,
        DocumentationStateExpectation expected)
    {
        var errors = ImmutableArray.CreateBuilder<string>();
        CheckRow(expected.CurrentPhaseId, expected.CurrentPhaseStatus, expected.CurrentPhaseOutcome,
            "current phase");
        CheckRow(expected.NextActionablePhaseId, "NEXT", expected.NextActionableAuthorization,
            "next actionable phase");
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
