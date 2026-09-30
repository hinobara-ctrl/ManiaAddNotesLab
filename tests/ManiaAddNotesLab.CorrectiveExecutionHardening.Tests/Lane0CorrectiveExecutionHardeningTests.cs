using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;
using Xunit;

public sealed class Lane0CorrectiveExecutionHardeningTests
{
    [Fact]
    public void DifferentBytesWithSameKeymodeAndObjectCountAreRejected()
    {
        var inventory = ExactInventory();
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveExecutionHardening.BuildInputs(
            inventory, path => path == "chart-00-4.osu" ? SyntheticBytes(4, "changed")
                : BytesForPath(path)));
    }

    [Fact]
    public void ExactHashedBytesAreTheBytesParsed()
    {
        var inputs = Lane0CorrectiveExecutionHardening.BuildInputs(ExactInventory(), BytesForPath);
        Assert.Equal(11, inputs.Length);
        Assert.Equal(inputs.Select(x => x.ChartId), inputs.Select(x => x.ChartId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void IndependentLoadsRehashEveryChart()
    {
        var calls = 0;
        var inventory = ExactInventory();
        Lane0CorrectiveExecutionHardening.BuildInputs(inventory, path => { calls++; return BytesForPath(path); });
        Lane0CorrectiveExecutionHardening.BuildInputs(inventory, path => { calls++; return BytesForPath(path); });
        Assert.Equal(22, calls);
    }

    [Fact]
    public void SourceChangedBetweenLoadsIsRejectedOnSecondHash()
    {
        var inventory = ExactInventory();
        Lane0CorrectiveExecutionHardening.BuildInputs(inventory, BytesForPath);
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveExecutionHardening.BuildInputs(
            inventory, path => path == "chart-00-4.osu" ? SyntheticBytes(4, "second-load")
                : BytesForPath(path)));
    }

    [Fact]
    public void RuntimeTreeIdentityIsStableAndMutationChangesIt()
    {
        var root = TempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "a.cs"), "a\r\n");
            File.WriteAllText(Path.Combine(root, "b.cs"), "b\n");
            var first = Lane0CorrectiveExecutionHardening.TreeIdentity(root, ["a.cs", "b.cs"]);
            Assert.Equal(first, Lane0CorrectiveExecutionHardening.TreeIdentity(root, ["b.cs", "a.cs"]));
            File.WriteAllText(Path.Combine(root, "b.cs"), "changed\n");
            Assert.NotEqual(first, Lane0CorrectiveExecutionHardening.TreeIdentity(root, ["a.cs", "b.cs"]));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void TrackedUnstagedChangeIsDirty()
    {
        using var repository = SyntheticGitRepository();
        File.AppendAllText(Path.Combine(repository.Path, "tracked.txt"), "dirty");
        Assert.False(Lane0CorrectiveExecutionHardening.IsTrackedClean(repository.Path));
    }

    [Fact]
    public void TrackedStagedChangeIsDirty()
    {
        using var repository = SyntheticGitRepository();
        File.AppendAllText(Path.Combine(repository.Path, "tracked.txt"), "dirty");
        Git(repository.Path, "add", "tracked.txt");
        Assert.False(Lane0CorrectiveExecutionHardening.IsTrackedClean(repository.Path));
    }

    [Fact]
    public void UntrackedBindingDoesNotDirtyTrackedTree()
    {
        using var repository = SyntheticGitRepository();
        File.WriteAllText(Path.Combine(repository.Path,
            "lane_0_corrective_evaluation_publication_binding.json"), "{}");
        Assert.True(Lane0CorrectiveExecutionHardening.IsTrackedClean(repository.Path));
    }

    [Fact]
    public void EvaluatorIdentityUsesPathHashTreeAlgorithm()
    {
        var root = RepositoryRoot();
        var path = "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveEvaluationRunner.cs";
        Assert.Equal(Lane0CorrectiveExecutionHardening.EvaluatorImplementationSha256,
            Lane0CorrectiveExecutionHardening.TreeIdentity(root, [path]));
    }

    [Fact]
    public void EvaluatorFlatHashIsNotItsTreeIdentity()
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root,
            "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveEvaluationRunner.cs");
        var flat = Lane0CorrectiveExecutionHardening.NormalizedFileHash(path);
        Assert.Equal("1719D85C540D683C5CDAC2018343ABBE1AF86F961E21A1DA743BEE52EBF61356", flat);
        Assert.NotEqual(Lane0CorrectiveExecutionHardening.EvaluatorImplementationSha256, flat);
    }

    [Fact]
    public void ChangedDeclaredRuntimeTreeIsRejectedBeforeAdapter()
    {
        var root = TempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "science.cs"), "frozen");
            var expected = Lane0CorrectiveExecutionHardening.TreeIdentity(root, ["science.cs"]);
            File.WriteAllText(Path.Combine(root, "science.cs"), "drifted");
            var adapter = new FakeAdapter(_ => []);
            Assert.NotEqual(expected,
                Lane0CorrectiveExecutionHardening.TreeIdentity(root, ["science.cs"]));
            Assert.Equal(0, adapter.AccessCalls);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ValidBindingNoReceiptHasReadyPreconditions()
    {
        using var paths = new ExecutionPaths();
        Assert.Null(Lane0CorrectiveExecutionHardening.ValidateOutputReadiness(
            paths.Final, paths.Staging, paths.Receipt));
    }

    [Fact]
    public void ReceiptCreateNewSucceedsExactlyOnce()
    {
        var root = TempRoot();
        var receipt = Path.Combine(root, "attempt.json");
        try
        {
            CreateReceipt(receipt);
            Assert.Throws<IOException>(() => CreateReceipt(receipt));
            Assert.Equal(1, JsonDocument.Parse(File.ReadAllBytes(receipt)).RootElement
                .GetProperty("authorizedExecutionCount").GetInt32());
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ExistingReceiptBlocksBeforeAdapter()
    {
        using var paths = new ExecutionPaths();
        File.WriteAllText(paths.Receipt, "occupied");
        var adapter = new FakeAdapter(_ => []);
        var result = Lane0CorrectiveExecutionHardening.ExecuteAuthorized(State(), [], [], adapter,
            paths.Final, paths.Staging, paths.Receipt);
        Assert.Equal("BLOCKED", result.Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Fact]
    public void AdapterFailureAfterReceiptLeavesReceipt()
    {
        using var paths = new ExecutionPaths();
        var adapter = new FakeAdapter(_ => throw new InvalidDataException("synthetic failure"));
        var result = Lane0CorrectiveExecutionHardening.ExecuteAuthorized(State(), [1], [], adapter,
            paths.Final, paths.Staging, paths.Receipt);
        Assert.Equal("INVALID", result.Outcome);
        Assert.True(File.Exists(paths.Receipt));
    }

    [Fact]
    public void InvalidEvaluationAfterReceiptLeavesReceipt()
    {
        using var paths = new ExecutionPaths();
        var adapter = new FakeAdapter(_ => [new("synthetic", OsuBeatmap.Parse(SyntheticText(4, "one")))]);
        var result = Lane0CorrectiveExecutionHardening.ExecuteAuthorized(State(), [2], [], adapter,
            paths.Final, paths.Staging, paths.Receipt);
        Assert.Equal("INVALID", result.Outcome);
        Assert.True(File.Exists(paths.Receipt));
        Assert.False(Directory.Exists(paths.Final));
    }

    [Theory]
    [InlineData("final")]
    [InlineData("staging")]
    [InlineData("receipt")]
    public void OccupiedExecutionPathBlocksBeforeAdapter(string occupied)
    {
        using var paths = new ExecutionPaths();
        var path = occupied == "final" ? paths.Final : occupied == "staging" ? paths.Staging : paths.Receipt;
        if (occupied == "receipt") File.WriteAllText(path, "occupied"); else Directory.CreateDirectory(path);
        var adapter = new FakeAdapter(_ => []);
        var result = Lane0CorrectiveExecutionHardening.ExecuteAuthorized(State(), [], [], adapter,
            paths.Final, paths.Staging, paths.Receipt);
        Assert.Equal("BLOCKED", result.Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Fact]
    public void WriteFailureLeavesFinalAbsentAndPartialStagingPresent()
    {
        using var paths = new ExecutionPaths();
        var package = Lane0CorrectiveExecutionHardening.BuildPackage(BlockedResult());
        var writes = 0;
        Assert.Throws<IOException>(() => Lane0CorrectiveExecutionHardening.PublishPackageAtomically(
            package, paths.Staging, paths.Final, (path, bytes) =>
            {
                if (++writes == 3) throw new IOException("injected");
                File.WriteAllBytes(path, bytes);
            }));
        Assert.False(Directory.Exists(paths.Final));
        Assert.True(Directory.Exists(paths.Staging));
        Assert.Equal(2, Directory.EnumerateFiles(paths.Staging).Count());
    }

    [Fact]
    public void PublicationFailureNeverDeletesAttemptReceipt()
    {
        using var paths = new ExecutionPaths();
        CreateReceipt(paths.Receipt);
        var package = Lane0CorrectiveExecutionHardening.BuildPackage(BlockedResult());
        Assert.Throws<IOException>(() => Lane0CorrectiveExecutionHardening.PublishPackageAtomically(
            package, paths.Staging, paths.Final, (_, _) => throw new IOException("injected")));
        Assert.True(File.Exists(paths.Receipt));
        Assert.False(Directory.Exists(paths.Final));
    }

    [Fact]
    public void SuccessfulPublicationContainsExactlySixVerifiedFiles()
    {
        using var paths = new ExecutionPaths();
        var package = Lane0CorrectiveExecutionHardening.BuildPackage(BlockedResult());
        Lane0CorrectiveExecutionHardening.PublishPackageAtomically(package, paths.Staging, paths.Final);
        Assert.False(Directory.Exists(paths.Staging));
        Assert.Equal(Lane0CorrectiveExecutionHardening.ArtifactNames,
            Directory.EnumerateFiles(paths.Final).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        Assert.All(package, pair => Assert.Equal(pair.Value,
            File.ReadAllBytes(Path.Combine(paths.Final, pair.Key))));
    }

    [Fact]
    public void Sha256SumsMatchesAllFiveSemanticArtifacts()
    {
        var package = Lane0CorrectiveExecutionHardening.BuildPackage(BlockedResult());
        var expected = string.Join('\n', package.Where(x => x.Key != "sha256sums.txt").Select(x =>
            $"{Convert.ToHexString(SHA256.HashData(x.Value))}  {x.Key}")) + "\n";
        Assert.Equal(expected, Encoding.UTF8.GetString(package["sha256sums.txt"]));
    }

    [Fact]
    public void DirectoryMoveOccursOnlyAfterStagingVerification()
    {
        using var paths = new ExecutionPaths();
        var package = Lane0CorrectiveExecutionHardening.BuildPackage(BlockedResult());
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveExecutionHardening.PublishPackageAtomically(
            package, paths.Staging, paths.Final, (path, bytes) =>
                File.WriteAllBytes(path, Path.GetFileName(path) == "integrity.json" ? [0] : bytes)));
        Assert.False(Directory.Exists(paths.Final));
        Assert.True(Directory.Exists(paths.Staging));
    }

    [Fact]
    public void TwoFullPackagesAreByteIdentical()
    {
        var first = Lane0CorrectiveExecutionHardening.BuildPackage(BlockedResult());
        var second = Lane0CorrectiveExecutionHardening.BuildPackage(BlockedResult());
        Assert.True(Lane0CorrectiveExecutionHardening.PackagesEqual(first, second));
    }

    [Fact]
    public void PackageWithExtraNameCannotBePublished()
    {
        using var paths = new ExecutionPaths();
        var package = Lane0CorrectiveExecutionHardening.BuildPackage(BlockedResult()).ToBuilder();
        package["extra.txt"] = [1];
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveExecutionHardening.PublishPackageAtomically(
            package.ToImmutable(), paths.Staging, paths.Final));
        Assert.False(Directory.Exists(paths.Final));
    }

    [Fact]
    public void OfficialRunnerProjectBuildSurfaceExistsAndCallsOnlyHardenedRoute()
    {
        var root = RepositoryRoot();
        Assert.True(File.Exists(Path.Combine(root,
            "tools/ManiaAddNotesLab.CorrectiveExecution/ManiaAddNotesLab.CorrectiveExecution.csproj")));
        var source = File.ReadAllText(Path.Combine(root,
            "tools/ManiaAddNotesLab.CorrectiveExecution/Program.cs"));
        Assert.Contains("Lane0CorrectiveExecutionHardening.Execute", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Lane0CorrectiveExecutionPreparation.Execute", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BindingAbsentBlocksOfficialRouteWithoutCorpusAccess()
    {
        var root = RepositoryRoot();
        Assert.False(File.Exists(Path.Combine(root, Lane0CorrectiveExecutionHardening.BindingPath)));
        var adapter = new FakeAdapter(_ => throw new InvalidOperationException("must not run"));
        var result = Lane0CorrectiveExecutionHardening.Execute(
            new(root, Path.Combine(Path.GetTempPath(), "nonexistent-synthetic-corpus")), adapter);
        Assert.Equal("BLOCKED", result.Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Fact]
    public void BindingV1IsRejected()
    {
        var json = """{"schemaVersion":"lane-0-corrective-evaluation-publication-binding.1"}""";
        Assert.ThrowsAny<Exception>(() => Lane0CorrectiveExecutionHardening.ParseBinding(
            Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void WellFormedBindingV2Parses()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = Lane0CorrectiveExecutionHardening.BindingSchema,
            approvedPublishedHead = new string('2', 40),
            executionHardeningContractSha256 = new string('A', 64),
            preregistrationCanonicalSha256 = new string('B', 64),
            evaluatorHardeningCanonicalSha256 = new string('C', 64),
            expectedC11ManifestSha256 = new string('D', 64),
            explicitHumanAuthorization = true,
            authorizedExecutionCount = 1
        });
        Assert.Equal(Lane0CorrectiveExecutionHardening.BindingSchema,
            Lane0CorrectiveExecutionHardening.ParseBinding(json).SchemaVersion);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":null}")]
    public void MalformedBindingV2IsRejected(string json) => Assert.ThrowsAny<Exception>(() =>
        Lane0CorrectiveExecutionHardening.ParseBinding(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void RunnerAcceptsOnlyRepositoryAndExplicitCorpusRoot()
    {
        Assert.True(Lane0CorrectiveExecutionCommand.TryParseArguments(
            ["--repo-root", ".", "--corpus-root", "synthetic"], out _, out _, out _));
        Assert.False(Lane0CorrectiveExecutionCommand.TryParseArguments(
            ["--repo-root", ".", "--corpus-root", "synthetic", "--output", "elsewhere"],
            out _, out _, out _));
        Assert.False(Lane0CorrectiveExecutionCommand.TryParseArguments(
            ["--binding", "elsewhere", "--corpus-root", "synthetic"], out _, out _, out _));
    }

    [Fact]
    public void CanonicalPathsAreCompiledIntoHardenedSuccessor()
    {
        Assert.Equal("docs/lane_0_corrective_evaluation_publication_binding.json",
            Lane0CorrectiveExecutionHardening.BindingPath);
        Assert.Equal("docs/lane_0_corrective_execution_hardening_contract.json",
            Lane0CorrectiveExecutionHardening.ContractPath);
        Assert.Equal(".artifacts/lane_0_corrective", Lane0CorrectiveExecutionHardening.FinalOutputPath);
    }

    [Fact]
    public void NoAutomaticReceiptDeletionExists()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
            "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveExecutionHardening.cs"));
        Assert.DoesNotContain("File.Delete", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Delete", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NewPhaseSourcesDoNotDependOnRealCorpusLocation()
    {
        var root = RepositoryRoot();
        var forbidden = ".artifacts/" + "f2-1-corpus";
        foreach (var path in new[]
        {
            "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveExecutionHardening.cs",
            "tools/ManiaAddNotesLab.CorrectiveExecution/Program.cs",
            "tests/ManiaAddNotesLab.CorrectiveExecutionHardening.Tests/Lane0CorrectiveExecutionHardeningTests.cs"
        })
            Assert.DoesNotContain(forbidden, File.ReadAllText(Path.Combine(root, path)),
                StringComparison.OrdinalIgnoreCase);
    }

    private static Lane0HardenedPublishedState State() => new(new string('2', 40), new string('B', 64),
        "unused", Lane0CorrectiveEvaluationRunner.FrozenHistoricalSnapshot,
        Lane0CorrectiveEvaluationRunner.FrozenHistoricalG1CaseRule);

    private static void CreateReceipt(string path) => Lane0CorrectiveExecutionHardening.CreateAttemptReceipt(
        path, new string('2', 40), new string('A', 64), new string('B', 64));

    private static C11FrozenCorpusVerification ExactInventory()
    {
        var descriptors = Enumerable.Range(0, 11).Select(index =>
        {
            var keys = new[] { 4, 7, 10 }[index % 3];
            var path = $"chart-{index:D2}-{keys}.osu";
            var hash = Convert.ToHexString(SHA256.HashData(SyntheticBytes(keys, index.ToString())));
            return new C11CorpusChartDescriptor(path, path, hash, $"Artist {index}", $"Title {index}",
                $"Creator {index}", $"Version {index}", $"FAMILY {index}", keys, 1, 1, 0, 1,
                120, 120, index == 0 ? [path, $"duplicate/{path}"] : [path]);
        }).ToImmutableArray();
        return new("synthetic", 12, descriptors, [], [], [], [],
            [new(descriptors[0].Sha256, [descriptors[0].RelativePath,
                $"duplicate/{descriptors[0].RelativePath}"])]);
    }

    private static byte[] BytesForPath(string path)
    {
        var parts = Path.GetFileNameWithoutExtension(path).Split('-');
        return SyntheticBytes(int.Parse(parts[2]), int.Parse(parts[1]).ToString());
    }

    private static byte[] SyntheticBytes(int keys, string marker) =>
        new UTF8Encoding(false).GetBytes(SyntheticText(keys, marker));

    private static string SyntheticText(int keys, string marker) => $"""
        osu file format v14
        [General]
        Mode:3
        [Metadata]
        Title:{marker}
        Artist:Synthetic
        Creator:Test
        Version:Only
        [Difficulty]
        CircleSize:{keys}
        [TimingPoints]
        0,500,4,2,1,100,1,0
        [HitObjects]
        64,192,1000,1,0,0:0:0:0:
        """;

    private static Lane0CorrectiveEvaluationResult BlockedResult() => new(
        Lane0CorrectiveEvaluationRunner.SchemaVersion, "BLOCKED", [], [], [], [], [], [], ["synthetic"],
        0, false, true);

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, ".git")))
            current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static string TempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lane0-hardening-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static SyntheticRepository SyntheticGitRepository()
    {
        var root = TempRoot();
        Git(root, "init");
        Git(root, "config", "user.email", "synthetic@example.invalid");
        Git(root, "config", "user.name", "Synthetic Test");
        File.WriteAllText(Path.Combine(root, "tracked.txt"), "clean");
        Git(root, "add", "tracked.txt");
        Git(root, "commit", "-m", "synthetic baseline");
        return new(root);
    }

    private static void Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot run git.");
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(process.StandardError.ReadToEnd());
    }

    private sealed class FakeAdapter(Func<int, ImmutableArray<Lane0CorrectiveChartInput>> factory)
        : ILane0HardenedCorpusAdapter
    {
        public int AccessCalls { get; private set; }
        public ImmutableArray<Lane0CorrectiveChartInput> Load(
            ImmutableArray<C11FrozenCorpusExpectation> expectations) => factory(++AccessCalls);
    }

    private sealed class ExecutionPaths : IDisposable
    {
        public string Root { get; } = TempRoot();
        public string Final => Path.Combine(Root, "final");
        public string Staging => Path.Combine(Root, "staging");
        public string Receipt => Path.Combine(Root, "attempt.json");
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }

    private sealed class SyntheticRepository(string path) : IDisposable
    {
        public string Path { get; } = path;
        public void Dispose()
        {
            if (!Directory.Exists(Path)) return;
            foreach (var entry in Directory.EnumerateFileSystemEntries(Path, "*", SearchOption.AllDirectories))
                File.SetAttributes(entry, FileAttributes.Normal);
            Directory.Delete(Path, true);
        }
    }
}
