using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace DocConsistencyTool;

public sealed record RepositoryRootGuardResult(
    ImmutableArray<string> UnexpectedEntries,
    ImmutableArray<string> MissingDeclaredEntries)
{
    public bool Passed => UnexpectedEntries.Length == 0 && MissingDeclaredEntries.Length == 0;
}

public static class RepositoryRootGuard
{
    public static ImmutableArray<string> ReadAllowlist(string path) => File.ReadLines(path)
        .Select(x => x.Trim())
        .Where(x => x.Length > 0 && !x.StartsWith('#'))
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToImmutableArray();

    public static ImmutableArray<string> GetVisibleTopLevelEntries(string repositoryRoot)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("core.quotePath=false");
        start.ArgumentList.Add("ls-files");
        start.ArgumentList.Add("--cached");
        start.ArgumentList.Add("--others");
        start.ArgumentList.Add("--exclude-standard");
        start.ArgumentList.Add("-z");
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start git ls-files for tracked and untracked entries.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git ls-files --cached --others --exclude-standard failed: {error.Trim()}");
        var presentPaths = output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path =>
            {
                var local = Path.Combine(repositoryRoot,
                    path.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(local) || Directory.Exists(local);
            });
        return TopLevelEntries(presentPaths);
    }

    public static ImmutableArray<string> TopLevelEntries(IEnumerable<string> trackedPaths) => trackedPaths
        .Select(path => path.Replace('\\', '/').Split('/', 2)[0])
        .Where(x => x.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToImmutableArray();

    public static RepositoryRootGuardResult Validate(IEnumerable<string> declaredEntries,
        IEnumerable<string> trackedTopLevelEntries)
    {
        var declared = declaredEntries.ToHashSet(StringComparer.Ordinal);
        var tracked = trackedTopLevelEntries.ToHashSet(StringComparer.Ordinal);
        return new RepositoryRootGuardResult(
            tracked.Except(declared, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(),
            declared.Except(tracked, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray());
    }
}
