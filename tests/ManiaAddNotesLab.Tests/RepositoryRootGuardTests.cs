using System.Runtime.CompilerServices;
using DocConsistencyTool;

namespace ManiaAddNotesLab.Tests;

public sealed class RepositoryRootGuardTests
{
    [Fact]
    public void CurrentRepositoryRootMatchesVersionedAllowlist()
    {
        var root = RepositoryRoot();
        var declared = RepositoryRootGuard.ReadAllowlist(Path.Combine(root,
            "tools", "DocConsistency", "repository-root-allowlist.txt"));
        var tracked = RepositoryRootGuard.GetVisibleTopLevelEntries(root);

        Assert.True(RepositoryRootGuard.Validate(declared, tracked).Passed);
    }

    [Fact]
    public void UnexpectedTrackedRootEntryFailsWithExactName()
    {
        var result = RepositoryRootGuard.Validate(["README.md", "src"],
            ["README.md", "src", "terminal-output.txt"]);

        Assert.Equal(new[] { "terminal-output.txt" }, result.UnexpectedEntries.ToArray());
        Assert.Empty(result.MissingDeclaredEntries);
    }

    [Fact]
    public void MissingDeclaredRootEntryFailsWithExactName()
    {
        var result = RepositoryRootGuard.Validate(["README.md", "src", "tests"],
            ["README.md", "src"]);

        Assert.Equal(new[] { "tests" }, result.MissingDeclaredEntries.ToArray());
        Assert.Empty(result.UnexpectedEntries);
    }

    [Fact]
    public void UnexpectedUntrackedRootEntryIsEnumeratedAndFails()
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, $"root-guard-local-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(path, "local-only");
        try
        {
            var visible = RepositoryRootGuard.GetVisibleTopLevelEntries(root);
            Assert.Contains(Path.GetFileName(path), visible);
            var declared = RepositoryRootGuard.ReadAllowlist(Path.Combine(root,
                "tools", "DocConsistency", "repository-root-allowlist.txt"));
            Assert.Contains(Path.GetFileName(path),
                RepositoryRootGuard.Validate(declared, visible).UnexpectedEntries);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void IgnoredArtifactIsNotEnumerated()
    {
        var root = RepositoryRoot();
        var directory = Path.Combine(root, ".artifacts");
        var path = Path.Combine(directory, $"root-guard-{Guid.NewGuid():N}.tmp");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "ignored");
        try
        {
            Assert.DoesNotContain(".artifacts", RepositoryRootGuard.GetVisibleTopLevelEntries(root));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UnicodeTrackedNamesAreEnumeratedWithoutQuotingOrLoss()
    {
        var unicode = "investigación exacta";
        var entries = RepositoryRootGuard.TopLevelEntries([$"{unicode}/resultado.csv"]);

        Assert.Equal(new[] { unicode }, entries.ToArray());
        Assert.True(RepositoryRootGuard.Validate([unicode], entries).Passed);
    }

    private static string RepositoryRoot([CallerFilePath] string source = "")
    {
        for (var directory = new DirectoryInfo(source); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ManiaAddNotesLab.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
