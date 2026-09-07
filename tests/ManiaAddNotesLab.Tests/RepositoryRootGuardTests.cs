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
        var tracked = RepositoryRootGuard.GetTrackedTopLevelEntries(root);

        Assert.True(RepositoryRootGuard.Validate(declared, tracked).Passed);
    }

    [Fact]
    public void UnexpectedTrackedRootEntryFailsWithExactName()
    {
        var result = RepositoryRootGuard.Validate(["README.md", "src"],
            ["README.md", "src", "terminal-output.txt"]);

        Assert.Equal(new[] { "terminal-output.txt" }, result.UnexpectedTrackedEntries.ToArray());
        Assert.Empty(result.MissingDeclaredEntries);
    }

    [Fact]
    public void MissingDeclaredRootEntryFailsWithExactName()
    {
        var result = RepositoryRootGuard.Validate(["README.md", "src", "tests"],
            ["README.md", "src"]);

        Assert.Equal(new[] { "tests" }, result.MissingDeclaredEntries.ToArray());
        Assert.Empty(result.UnexpectedTrackedEntries);
    }

    [Fact]
    public void LocalUntrackedArtifactIsNotPartOfGitEnumeration()
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, $"root-guard-local-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(path, "local-only");
        try
        {
            Assert.DoesNotContain(Path.GetFileName(path),
                RepositoryRootGuard.GetTrackedTopLevelEntries(root));
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
