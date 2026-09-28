using System.Collections.Immutable;
using Xunit;

public sealed class Lane0CorrectiveEvaluatorHardeningTests
{
    [Fact]
    public void IncompleteExternalPrerequisitesRemainBlocked()
    {
        var result = Evaluate(new(false));
        Assert.Equal("BLOCKED", result.Outcome);
        Assert.Empty(result.IntegrityFailures);
    }

    [Fact]
    public void CompleteFlagWithoutScientificInputsRemainsBlocked()
    {
        var result = Evaluate(new(true));
        Assert.Equal("BLOCKED", result.Outcome);
        Assert.Contains(result.BlockedReasons, x => x.Contains("snapshot", StringComparison.Ordinal));
        Assert.Contains(result.BlockedReasons, x => x.Contains("case rule", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingCaseRuleRemainsBlockedBeforeSnapshotReconstruction()
    {
        var result = Evaluate(new(true, Lane0CorrectiveEvaluationRunner.FrozenHistoricalSnapshot));
        Assert.Equal("BLOCKED", result.Outcome);
        Assert.Empty(result.IntegrityFailures);
    }

    [Fact]
    public void MissingSnapshotRemainsBlockedBeforeCaseReconstruction()
    {
        var result = Evaluate(new(true, null, Lane0CorrectiveEvaluationRunner.FrozenHistoricalG1CaseRule));
        Assert.Equal("BLOCKED", result.Outcome);
        Assert.Empty(result.IntegrityFailures);
    }

    [Fact]
    public void CompleteValidClassificationCanDemonstrateFeasibility() =>
        Assert.Equal("FEASIBILITY_DEMONSTRATED",
            Lane0CorrectiveEvaluationRunner.Classify(true, false, true, true));

    [Fact]
    public void CompleteValidClassificationCanRemainLimited() =>
        Assert.Equal("LIMITED_PARK",
            Lane0CorrectiveEvaluationRunner.Classify(true, false, true, false));

    [Fact]
    public void SuppliedHistoricalSnapshotDefinitionMismatchIsInvalid()
    {
        var altered = Lane0CorrectiveEvaluationRunner.FrozenHistoricalSnapshot with
        {
            Rice = Lane0CorrectiveEvaluationRunner.FrozenHistoricalSnapshot.Rice with { Structural = 40359 }
        };
        var result = Evaluate(new(true, altered, Lane0CorrectiveEvaluationRunner.FrozenHistoricalG1CaseRule));
        Assert.Equal("INVALID", result.Outcome);
        Assert.Contains(result.IntegrityFailures, x => x.Contains("canonical snapshot", StringComparison.Ordinal));
    }

    [Fact]
    public void SuppliedHistoricalCaseCountMismatchIsInvalid()
    {
        var altered = Lane0CorrectiveEvaluationRunner.FrozenHistoricalG1CaseRule with { ExpectedCount = 10 };
        AssertRuleMismatchIsInvalid(altered);
    }

    [Fact]
    public void SuppliedHistoricalCaseDistributionMismatchIsInvalid()
    {
        var altered = Lane0CorrectiveEvaluationRunner.FrozenHistoricalG1CaseRule with
        {
            ExpectedChartDistribution = ImmutableSortedDictionary<string, int>.Empty
                .WithComparers(StringComparer.Ordinal).Add("synthetic-chart", 11)
        };
        AssertRuleMismatchIsInvalid(altered);
    }

    [Fact]
    public void SuppliedHistoricalCaseKeymodeMismatchIsInvalid()
    {
        var altered = Lane0CorrectiveEvaluationRunner.FrozenHistoricalG1CaseRule with { ExpectedKeymode = 4 };
        AssertRuleMismatchIsInvalid(altered);
    }

    [Fact]
    public void MissingCorrectedOccurrenceIsInvalid()
    {
        var historical = ImmutableArray.Create(Occurrence("one", 1), Occurrence("two", 2));
        var corrected = ImmutableArray.Create(Occurrence("one", 1) with { AnchorTime = 100 });
        var comparison = Lane0CorrectiveEvaluationRunner.CompareG1Branches(historical, corrected);
        Assert.Contains("Historical/corrected occurrence ID sets differ.", comparison.IntegrityFailures);
        Assert.Equal("INVALID", Lane0CorrectiveEvaluationRunner.Classify(
            comparison.IntegrityFailures.Length == 0, false, true, true));
    }

    [Fact]
    public void ReconstructedRiceSnapshotMismatchIsInvalid()
    {
        var result = Evaluate(new(true, Lane0CorrectiveEvaluationRunner.FrozenHistoricalSnapshot,
            Lane0CorrectiveEvaluationRunner.FrozenHistoricalG1CaseRule));
        Assert.Equal("INVALID", result.Outcome);
        Assert.Contains(result.IntegrityFailures,
            x => x.Contains("RICE_HEAD_COMPLETION: historical snapshot mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void RiceHistoricalCorrectedDifferenceIsInvalid()
    {
        var historical = Population(1);
        var corrected = Population(2);
        var sentinelValid = Lane0CorrectiveEvaluationRunner.ValidateRiceSentinel(historical, corrected);
        Assert.False(sentinelValid);
        Assert.Equal("INVALID", Lane0CorrectiveEvaluationRunner.Classify(sentinelValid, false, true, true));
    }

    [Fact]
    public void NonexistentOutputDirectorySucceeds()
    {
        var root = TempRoot();
        var output = Path.Combine(root, "new-output");
        try
        {
            Lane0CorrectiveEvaluationRunner.WriteArtifacts(BlockedResult(), output);
            Assert.True(File.Exists(Path.Combine(output, "scientific_summary.json")));
            Assert.True(File.Exists(Path.Combine(output, "sha256sums.txt")));
        }
        finally { DeleteTempRoot(root); }
    }

    [Fact]
    public void ExistingEmptyOutputDirectorySucceeds()
    {
        var root = TempRoot();
        var output = Directory.CreateDirectory(Path.Combine(root, "empty-output")).FullName;
        try
        {
            Lane0CorrectiveEvaluationRunner.WriteArtifacts(BlockedResult(), output);
            Assert.Equal(6, Directory.EnumerateFiles(output).Count());
        }
        finally { DeleteTempRoot(root); }
    }

    [Theory]
    [InlineData("scientific_summary.json")]
    [InlineData("unrelated.txt")]
    [InlineData("sha256sums.txt")]
    public void ExistingOutputFileRejectsBeforeWriting(string existingName)
    {
        var root = TempRoot();
        var output = Directory.CreateDirectory(Path.Combine(root, "occupied-output")).FullName;
        var existing = Path.Combine(output, existingName);
        File.WriteAllText(existing, "sentinel");
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                Lane0CorrectiveEvaluationRunner.WriteArtifacts(BlockedResult(), output));
            Assert.Equal([existingName], Directory.EnumerateFiles(output).Select(Path.GetFileName));
            Assert.Equal("sentinel", File.ReadAllText(existing));
        }
        finally { DeleteTempRoot(root); }
    }

    [Fact]
    public void ExistingOutputSubdirectoryRejectsBeforeWriting()
    {
        var root = TempRoot();
        var output = Directory.CreateDirectory(Path.Combine(root, "occupied-output")).FullName;
        Directory.CreateDirectory(Path.Combine(output, "old-run"));
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                Lane0CorrectiveEvaluationRunner.WriteArtifacts(BlockedResult(), output));
            Assert.Empty(Directory.EnumerateFiles(output));
            Assert.Single(Directory.EnumerateDirectories(output));
        }
        finally { DeleteTempRoot(root); }
    }

    [Fact]
    public void InvalidAlwaysPrecedesBlocked() =>
        Assert.Equal("INVALID", Lane0CorrectiveEvaluationRunner.Classify(false, true, true, true));

    [Fact]
    public void SemanticArtifactsContainNoTemporaryPathOrGuid()
    {
        var result = BlockedResult();
        var bytes = Lane0CorrectiveEvaluationRunner.SerializeArtifacts(result);
        var combined = string.Join('\n', bytes.Values.Select(System.Text.Encoding.UTF8.GetString));
        Assert.DoesNotContain(Path.GetTempPath(), combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.MachineName, combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", combined);
    }

    private static Lane0CorrectiveEvaluationResult Evaluate(Lane0CorrectiveEvaluationOptions options) =>
        Lane0CorrectiveEvaluationRunner.Evaluate([], options);

    private static Lane0CorrectiveEvaluationResult BlockedResult() => Evaluate(new(false));

    private static void AssertRuleMismatchIsInvalid(Lane0HistoricalCaseRule altered)
    {
        var result = Evaluate(new(true, Lane0CorrectiveEvaluationRunner.FrozenHistoricalSnapshot, altered));
        Assert.Equal("INVALID", result.Outcome);
        Assert.Contains(result.IntegrityFailures, x => x.Contains("canonical rule", StringComparison.Ordinal));
    }

    private static Lane0Occurrence Occurrence(string id, int observation) =>
        new(Lane0Family.G1InteriorSpatial, "synthetic-chart", 7, id, $"group-{id}", $"event-{id}",
            $"parent-{id}", "Q", "J", "T", "S", [observation], [], [], AnchorTime: null);

    private static Lane0PopulationSummary Population(int structural) =>
        new(structural, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 0);

    private static string TempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lane0-hardening-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
