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
    public void DurableReceiptBlocksSecondAttemptAcrossDifferentExecutionRoots()
    {
        var authorizationRoot = TempRoot();
        var executionOne = TempRoot();
        var executionTwo = TempRoot();
        try
        {
            var receipt = Path.Combine(authorizationRoot,
                Lane0CorrectiveExecutionHardening.AttemptReceiptPath);
            var firstAdapter = new FakeAdapter(_ =>
                throw new InvalidDataException("synthetic post-receipt failure"));
            var first = Lane0CorrectiveExecutionHardening.ExecuteAuthorized(
                State(), [7], [], firstAdapter,
                Path.Combine(executionOne, "final"), Path.Combine(executionOne, "staging"), receipt);
            Assert.Equal("INVALID", first.Outcome);
            Assert.Equal(1, firstAdapter.AccessCalls);
            Assert.True(File.Exists(receipt));
            var originalReceipt = File.ReadAllBytes(receipt);

            var secondAdapter = new FakeAdapter(_ =>
                throw new InvalidOperationException("second attempt must not access corpus"));
            var secondFinal = Path.Combine(executionTwo, "final");
            var second = Lane0CorrectiveExecutionHardening.ExecuteAuthorized(
                State(), [7], [], secondAdapter, secondFinal,
                Path.Combine(executionTwo, "staging"), receipt);
            Assert.Equal("BLOCKED", second.Outcome);
            Assert.Equal(0, secondAdapter.AccessCalls);
            Assert.Equal(originalReceipt, File.ReadAllBytes(receipt));
            Assert.False(Directory.Exists(secondFinal));
            Assert.Equal(Path.Combine(authorizationRoot,
                Lane0CorrectiveExecutionHardening.AttemptReceiptPath), receipt);
            Assert.NotEqual(Path.Combine(executionOne,
                Lane0CorrectiveExecutionHardening.AttemptReceiptPath), receipt);
        }
        finally
        {
            DeleteTree(authorizationRoot);
            DeleteTree(executionOne);
            DeleteTree(executionTwo);
        }
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
    public void SuccessfulPublicationDoesNotRelocateOrDeleteDurableReceipt()
    {
        var authorizationRoot = TempRoot();
        using var paths = new ExecutionPaths();
        try
        {
            var receipt = Path.Combine(authorizationRoot,
                Lane0CorrectiveExecutionHardening.AttemptReceiptPath);
            CreateReceipt(receipt);
            var before = File.ReadAllBytes(receipt);
            Lane0CorrectiveExecutionHardening.PublishPackageAtomically(
                Lane0CorrectiveExecutionHardening.BuildPackage(BlockedResult()),
                paths.Staging, paths.Final);
            Assert.Equal(before, File.ReadAllBytes(receipt));
            Assert.False(File.Exists(Path.Combine(paths.Root,
                Lane0CorrectiveExecutionHardening.AttemptReceiptPath)));
        }
        finally { DeleteTree(authorizationRoot); }
    }

    [Fact]
    public void AuthorizationRootValidationDerivesDurableReceiptAndMatchesBindingBytes()
    {
        using var source = SyntheticLauncherRepository(bindingPresent: true);
        var execution = TempRoot();
        try
        {
            var bytes = File.ReadAllBytes(Path.Combine(source.Path,
                Lane0CorrectiveExecutionHardening.BindingPath));
            var head = GitOutput(source.Path, "rev-parse", "HEAD");
            var state = Lane0CorrectiveExecutionHardening.LoadAuthorizationRoot(
                execution, source.Path, State(head), bytes);
            Assert.Equal(Path.GetFullPath(source.Path), state.Root);
            Assert.Equal(bytes, state.BindingBytes);
            Assert.Equal(Path.Combine(source.Path,
                Lane0CorrectiveExecutionHardening.AttemptReceiptPath), state.ReceiptPath);
            Assert.NotEqual(Path.Combine(execution,
                Lane0CorrectiveExecutionHardening.AttemptReceiptPath), state.ReceiptPath);
        }
        finally { DeleteTree(execution); }
    }

    [Fact]
    public void AuthorizationRootHeadMismatchIsRejected()
    {
        using var source = SyntheticLauncherRepository(bindingPresent: true);
        var execution = TempRoot();
        try
        {
            var bytes = File.ReadAllBytes(Path.Combine(source.Path,
                Lane0CorrectiveExecutionHardening.BindingPath));
            Assert.Throws<InvalidDataException>(() =>
                Lane0CorrectiveExecutionHardening.LoadAuthorizationRoot(
                    execution, source.Path, State(new string('A', 40)), bytes));
        }
        finally { DeleteTree(execution); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AuthorizationRootTrackedDirtyIsRejected(bool staged)
    {
        using var source = SyntheticLauncherRepository(bindingPresent: true);
        var execution = TempRoot();
        try
        {
            var bytes = File.ReadAllBytes(Path.Combine(source.Path,
                Lane0CorrectiveExecutionHardening.BindingPath));
            var head = GitOutput(source.Path, "rev-parse", "HEAD");
            File.AppendAllText(Path.Combine(source.Path, "tracked.txt"), "dirty");
            if (staged) Git(source.Path, "add", "tracked.txt");
            Assert.Throws<InvalidDataException>(() =>
                Lane0CorrectiveExecutionHardening.LoadAuthorizationRoot(
                    execution, source.Path, State(head), bytes));
        }
        finally { DeleteTree(execution); }
    }

    [Fact]
    public void AuthorizationRootBindingAbsentIsRejected()
    {
        using var source = SyntheticLauncherRepository(bindingPresent: false);
        var execution = TempRoot();
        try
        {
            var head = GitOutput(source.Path, "rev-parse", "HEAD");
            Assert.Throws<FileNotFoundException>(() =>
                Lane0CorrectiveExecutionHardening.LoadAuthorizationRoot(
                    execution, source.Path, State(head), [1]));
        }
        finally { DeleteTree(execution); }
    }

    [Fact]
    public void AuthorizationRootTrackedBindingIsRejected()
    {
        using var source = SyntheticLauncherRepository(bindingPresent: true, bindingTracked: true);
        var execution = TempRoot();
        try
        {
            var head = GitOutput(source.Path, "rev-parse", "HEAD");
            Assert.Throws<InvalidDataException>(() =>
                Lane0CorrectiveExecutionHardening.LoadAuthorizationRoot(
                    execution, source.Path, State(head), [1]));
        }
        finally { DeleteTree(execution); }
    }

    [Fact]
    public void AuthorizationRootBindingByteMismatchIsRejected()
    {
        using var source = SyntheticLauncherRepository(bindingPresent: true);
        var execution = TempRoot();
        try
        {
            var head = GitOutput(source.Path, "rev-parse", "HEAD");
            Assert.Throws<InvalidDataException>(() =>
                Lane0CorrectiveExecutionHardening.LoadAuthorizationRoot(
                    execution, source.Path, State(head), Encoding.UTF8.GetBytes("different")));
        }
        finally { DeleteTree(execution); }
    }

    [Fact]
    public void AuthorizationRootCannotEqualExecutionRepositoryRoot()
    {
        using var source = SyntheticLauncherRepository(bindingPresent: true);
        var head = GitOutput(source.Path, "rev-parse", "HEAD");
        var bytes = File.ReadAllBytes(Path.Combine(source.Path,
            Lane0CorrectiveExecutionHardening.BindingPath));
        Assert.Throws<InvalidDataException>(() =>
            Lane0CorrectiveExecutionHardening.LoadAuthorizationRoot(
                source.Path, source.Path, State(head), bytes));
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
        Assert.Empty(Directory.EnumerateDirectories(paths.Final));
        Assert.Equal(6, Directory.EnumerateFileSystemEntries(paths.Final).Count());
    }

    [Fact]
    public void UnexpectedStagingDirectoryPreventsFinalPublicationAndRemainsAsEvidence()
    {
        using var paths = new ExecutionPaths();
        var package = Lane0CorrectiveExecutionHardening.BuildPackage(BlockedResult());
        var injected = false;
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveExecutionHardening.PublishPackageAtomically(
            package, paths.Staging, paths.Final, (path, bytes) =>
            {
                File.WriteAllBytes(path, bytes);
                if (injected) return;
                Directory.CreateDirectory(Path.Combine(paths.Staging, "unexpected-directory"));
                injected = true;
            }));
        Assert.False(Directory.Exists(paths.Final));
        Assert.True(Directory.Exists(paths.Staging));
        Assert.True(Directory.Exists(Path.Combine(paths.Staging, "unexpected-directory")));
    }

    [Fact]
    public void UnexpectedStagingFilePreventsFinalPublicationAndRemainsAsEvidence()
    {
        using var paths = new ExecutionPaths();
        var package = Lane0CorrectiveExecutionHardening.BuildPackage(BlockedResult());
        var injected = false;
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveExecutionHardening.PublishPackageAtomically(
            package, paths.Staging, paths.Final, (path, bytes) =>
            {
                File.WriteAllBytes(path, bytes);
                if (injected) return;
                File.WriteAllText(Path.Combine(paths.Staging, "unexpected.txt"), "evidence");
                injected = true;
            }));
        Assert.False(Directory.Exists(paths.Final));
        Assert.True(File.Exists(Path.Combine(paths.Staging, "unexpected.txt")));
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
    public void ExternalHostPreflightMatchingHeadPermitsTrackedLauncherStageDespiteBenignUntrackedFiles()
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true);
        File.WriteAllText(Path.Combine(repository.Path, "benign-local-note.txt"), "not copied");
        var invoked = false;
        RunExternalHostPreflight(repository.Path, () => invoked = true);
        Assert.True(invoked);
    }

    [Fact]
    public void ExternalHostPreflightExistingDurableReceiptNeverInvokesLauncher()
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true);
        var receipt = Path.Combine(repository.Path,
            Lane0CorrectiveExecutionHardening.AttemptReceiptPath);
        Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
        File.WriteAllText(receipt, "durable receipt");
        var invoked = false;

        Assert.Throws<InvalidDataException>(() =>
            RunExternalHostPreflight(repository.Path, () => invoked = true));

        Assert.False(invoked);
        Assert.Equal("durable receipt", File.ReadAllText(receipt));
    }

    [Fact]
    public void ExternalHostPreflightHeadMismatchNeverExecutesUnauthorizedRepositoryLauncher()
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true);
        var approvedHead = GitOutput(repository.Path, "rev-parse", "HEAD");
        var unauthorizedLauncher = Path.Combine(repository.Path, "tools", "unauthorized-launcher.ps1");
        File.WriteAllText(unauthorizedLauncher,
            "param([string]$Marker,[string]$CorpusMarker) " +
            "[IO.File]::WriteAllText($Marker,'ran'); [IO.File]::WriteAllText($CorpusMarker,'touched')");
        Git(repository.Path, "add", "tools/unauthorized-launcher.ps1");
        Git(repository.Path, "commit", "-m", "unauthorized head B");
        Assert.NotEqual(approvedHead, GitOutput(repository.Path, "rev-parse", "HEAD"));

        var marker = NonexistentTempPath("unauthorized-launcher-ran.marker");
        var execution = NonexistentTempPath("lane0-host-preflight-mismatch");
        var opaqueCorpusMarker = NonexistentTempPath("opaque-corpus-untouched.marker");
        Assert.Throws<InvalidDataException>(() => RunExternalHostPreflight(repository.Path, () =>
        {
            RunPowerShellScript(unauthorizedLauncher, marker, opaqueCorpusMarker);
            Directory.CreateDirectory(execution);
        }));
        Assert.False(File.Exists(marker));
        Assert.False(File.Exists(opaqueCorpusMarker));
        Assert.False(Directory.Exists(execution));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("schema-v1")]
    [InlineData("malformed-head")]
    public void ExternalHostPreflightRejectsMalformedBindingBeforeLauncher(string mutation)
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true);
        var binding = Path.Combine(repository.Path, Lane0CorrectiveExecutionHardening.BindingPath);
        if (mutation == "malformed") File.WriteAllText(binding, "not-json");
        else WriteSyntheticBinding(repository.Path,
            mutation == "malformed-head" ? "not-a-head" : GitOutput(repository.Path, "rev-parse", "HEAD"),
            mutation == "schema-v1" ? "lane-0-corrective-evaluation-publication-binding.1"
                : Lane0CorrectiveExecutionHardening.BindingSchema);
        var invoked = false;
        Assert.ThrowsAny<Exception>(() => RunExternalHostPreflight(repository.Path, () => invoked = true));
        Assert.False(invoked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExternalHostPreflightRejectsTrackedDirtySourceBeforeLauncher(bool staged)
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true);
        File.AppendAllText(Path.Combine(repository.Path, "tracked.txt"), "dirty");
        if (staged) Git(repository.Path, "add", "tracked.txt");
        var invoked = false;
        Assert.Throws<InvalidDataException>(() =>
            RunExternalHostPreflight(repository.Path, () => invoked = true));
        Assert.False(invoked);
    }

    [Fact]
    public void ExternalHostPreflightRejectsTrackedBindingBeforeLauncher()
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true, bindingTracked: true);
        Assert.Equal(0, GitExitCode(repository.Path, "cat-file", "-e",
            $"HEAD:{Lane0CorrectiveExecutionHardening.BindingPath}"));
        var invoked = false;
        Assert.Throws<InvalidDataException>(() =>
            RunExternalHostPreflight(repository.Path, () => invoked = true));
        Assert.False(invoked);
    }

    [Fact]
    public void BindingAbsentBlocksIsolatedLauncherBeforeCloneOrCorpusProbe()
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: false);
        var execution = NonexistentTempPath("lane0-isolated-absent");
        var result = RunIsolatedLauncher(repository.Path, execution, "Z:\\opaque-never-probed");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("BLOCKED: canonical publication binding is absent", result.AllOutput,
            StringComparison.Ordinal);
        Assert.False(Directory.Exists(execution));
    }

    [Fact]
    public void IsolatedLauncherClonesExactHeadAndExcludesAllSourceOnlyInputs()
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true);
        File.WriteAllText(Path.Combine(repository.Path, "benign-note.txt"), "local only");
        File.WriteAllText(Path.Combine(repository.Path, "Evil.cs"), "this is not valid C#");
        File.WriteAllText(Path.Combine(repository.Path, "Directory.Build.props"), "<not-xml>");
        File.WriteAllText(Path.Combine(repository.Path, "ignored.local"), "ignored local input");
        var execution = NonexistentTempPath("lane0-isolated-success");
        try
        {
            const string opaqueCorpus = "Z:\\opaque-c11-root-that-is-not-probed";
            var result = RunIsolatedLauncher(repository.Path, execution, opaqueCorpus);
            Assert.True(result.ExitCode == 0, result.AllOutput);
            Assert.Contains($"--corpus-root|{opaqueCorpus}", result.AllOutput, StringComparison.Ordinal);
            Assert.Contains($"--authorization-root|{repository.Path}", result.AllOutput,
                StringComparison.Ordinal);
            Assert.Equal(GitOutput(repository.Path, "rev-parse", "HEAD"),
                GitOutput(execution, "rev-parse", "HEAD"));
            Assert.NotEqual(0, GitExitCode(execution, "symbolic-ref", "-q", "HEAD"));
            Assert.False(File.Exists(Path.Combine(execution, "benign-note.txt")));
            Assert.False(File.Exists(Path.Combine(execution, "Evil.cs")));
            Assert.False(File.Exists(Path.Combine(execution, "Directory.Build.props")));
            Assert.False(File.Exists(Path.Combine(execution, "ignored.local")));
            var sourceBinding = File.ReadAllBytes(Path.Combine(repository.Path,
                Lane0CorrectiveExecutionHardening.BindingPath));
            var isolatedBinding = File.ReadAllBytes(Path.Combine(execution,
                Lane0CorrectiveExecutionHardening.BindingPath));
            Assert.Equal(sourceBinding, isolatedBinding);
            Assert.Equal(Lane0CorrectiveExecutionHardening.BindingPath,
                GitOutput(execution, "ls-files", "--others", "--exclude-standard"));
        }
        finally { DeleteTree(execution); }
    }

    [Fact]
    public void IsolatedLauncherDefenseInDepthRejectsMismatchedBindingBeforeExecutionRoot()
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true);
        WriteSyntheticBinding(repository.Path, new string('A', 40));
        var execution = NonexistentTempPath("lane0-launcher-head-mismatch");
        var opaqueCorpus = NonexistentTempPath("lane0-opaque-corpus");
        var result = RunIsolatedLauncher(repository.Path, execution, opaqueCorpus);
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("approvedPublishedHead does not equal source HEAD", result.AllOutput,
            StringComparison.Ordinal);
        Assert.False(Directory.Exists(execution));
        Assert.False(File.Exists(opaqueCorpus));
        Assert.False(Directory.Exists(opaqueCorpus));
    }

    [Fact]
    public void IsolatedLauncherDefenseInDepthRejectsNonIntegerCountBeforeExecutionRoot()
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true);
        WriteSyntheticBinding(repository.Path, GitOutput(repository.Path, "rev-parse", "HEAD"),
            authorizedExecutionCount: "1");
        var execution = NonexistentTempPath("lane0-launcher-invalid-count");
        var result = RunIsolatedLauncher(repository.Path, execution, "opaque");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("authorizedExecutionCount must be a valid integer", result.AllOutput,
            StringComparison.Ordinal);
        Assert.False(Directory.Exists(execution));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsolatedLauncherRejectsTrackedDirtySourceBeforeClone(bool staged)
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true);
        File.AppendAllText(Path.Combine(repository.Path, "tracked.txt"), "dirty");
        if (staged) Git(repository.Path, "add", "tracked.txt");
        var execution = NonexistentTempPath("lane0-isolated-dirty");
        var result = RunIsolatedLauncher(repository.Path, execution, "opaque");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("INVALID: source repository contains tracked staged or unstaged changes",
            result.AllOutput, StringComparison.Ordinal);
        Assert.False(Directory.Exists(execution));
    }

    [Fact]
    public void IsolatedLauncherRejectsPreexistingExecutionRootWithoutChangingIt()
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true);
        var execution = TempRoot();
        var marker = Path.Combine(execution, "keep.txt");
        File.WriteAllText(marker, "preserve");
        try
        {
            var result = RunIsolatedLauncher(repository.Path, execution, "opaque");
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("ExecutionRoot must be absolutely nonexistent", result.AllOutput,
                StringComparison.Ordinal);
            Assert.Equal("preserve", File.ReadAllText(marker));
        }
        finally { DeleteTree(execution); }
    }

    [Fact]
    public void IsolatedLauncherBlocksOnDurableSourceReceiptBeforeClone()
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true);
        var receipt = Path.Combine(repository.Path,
            Lane0CorrectiveExecutionHardening.AttemptReceiptPath);
        Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
        File.WriteAllText(receipt, "consumed");
        var execution = NonexistentTempPath("lane0-durable-receipt-block");
        var result = RunIsolatedLauncher(repository.Path, execution, "opaque");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("durable authorization-root attempt receipt already exists",
            result.AllOutput, StringComparison.Ordinal);
        Assert.False(Directory.Exists(execution));
        Assert.Equal("consumed", File.ReadAllText(receipt));
    }

    [Fact]
    public void IsolatedLauncherRejectsRelativeOrInsideRepositoryExecutionRoot()
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true);
        var relative = RunIsolatedLauncher(repository.Path, "relative-execution-root", "opaque");
        Assert.NotEqual(0, relative.ExitCode);
        Assert.Contains("ExecutionRoot must be an absolute path", relative.AllOutput,
            StringComparison.Ordinal);

        var inside = Path.Combine(repository.Path, "nonexistent-execution-root");
        var nested = RunIsolatedLauncher(repository.Path, inside, "opaque");
        Assert.NotEqual(0, nested.ExitCode);
        Assert.Contains("ExecutionRoot must be outside RepositoryRoot", nested.AllOutput,
            StringComparison.Ordinal);
        Assert.False(Directory.Exists(inside));
    }

    [Fact]
    public void IsolatedLauncherRejectsBindingTrackedAtSourceHead()
    {
        using var repository = SyntheticLauncherRepository(bindingPresent: true, bindingTracked: true);
        var execution = NonexistentTempPath("lane0-isolated-tracked-binding");
        var result = RunIsolatedLauncher(repository.Path, execution, "opaque");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("canonical publication binding is tracked at source HEAD", result.AllOutput,
            StringComparison.Ordinal);
        Assert.False(Directory.Exists(execution));
    }

    [Fact]
    public void IsolatedLauncherHasNoFetchPullCleanupRetryOrNoBuildPath()
    {
        var source = File.ReadAllText(IsolatedLauncherPath());
        Assert.Contains("'clone', '--no-hardlinks', '--no-checkout'", source, StringComparison.Ordinal);
        Assert.DoesNotContain("'fetch'", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("'pull'", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--no-build", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Remove-Item", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Directory]::Delete", source, StringComparison.OrdinalIgnoreCase);
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
    public void RunnerAcceptsOnlyExecutionAuthorizationAndExplicitCorpusRoots()
    {
        Assert.True(Lane0CorrectiveExecutionCommand.TryParseArguments(
            ["--repo-root", "isolated", "--authorization-root", "source",
                "--corpus-root", "synthetic"], out _, out _, out _, out _));
        Assert.False(Lane0CorrectiveExecutionCommand.TryParseArguments(
            ["--repo-root", "isolated", "--corpus-root", "synthetic"],
            out _, out _, out _, out _));
        Assert.False(Lane0CorrectiveExecutionCommand.TryParseArguments(
            ["--repo-root", "isolated", "--authorization-root", "source",
                "--corpus-root", "synthetic", "--output", "elsewhere"],
            out _, out _, out _, out _));
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
            "tools/InvokeLane0CorrectiveExecutionIsolated.ps1",
            "tests/ManiaAddNotesLab.CorrectiveExecutionHardening.Tests/Lane0CorrectiveExecutionHardeningTests.cs"
        })
            Assert.DoesNotContain(forbidden, File.ReadAllText(Path.Combine(root, path)),
                StringComparison.OrdinalIgnoreCase);
    }

    private static Lane0HardenedPublishedState State(string? head = null) => new(
        head ?? new string('2', 40), new string('B', 64),
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

    private static SyntheticRepository SyntheticLauncherRepository(bool bindingPresent,
        bool bindingTracked = false)
    {
        var root = TempRoot();
        Git(root, "init");
        Git(root, "config", "user.email", "synthetic@example.invalid");
        Git(root, "config", "user.name", "Synthetic Test");
        File.WriteAllText(Path.Combine(root, ".gitignore"), "bin/\nobj/\nignored.local\n");
        File.WriteAllText(Path.Combine(root, "tracked.txt"), "clean");
        var runner = Path.Combine(root, "tools", "ManiaAddNotesLab.CorrectiveExecution");
        Directory.CreateDirectory(runner);
        File.WriteAllText(Path.Combine(runner, "ManiaAddNotesLab.CorrectiveExecution.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType>" +
            "<TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings>" +
            "</PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(runner, "Program.cs"),
            "Console.WriteLine(string.Join(\"|\", args)); return 0;");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "docs", ".gitkeep"), string.Empty);
        if (bindingPresent && bindingTracked)
            File.WriteAllBytes(Path.Combine(root, Lane0CorrectiveExecutionHardening.BindingPath),
                JsonSerializer.SerializeToUtf8Bytes(new
                {
                    schemaVersion = Lane0CorrectiveExecutionHardening.BindingSchema,
                    approvedPublishedHead = new string('0', 40),
                    authorizedExecutionCount = 1
                }));
        Git(root, "add", ".gitignore", "tracked.txt", "tools", "docs");
        Git(root, "commit", "-m", "synthetic launcher baseline");
        if (bindingPresent && !bindingTracked)
            WriteSyntheticBinding(root, GitOutput(root, "rev-parse", "HEAD"));
        return new(root);
    }

    private static void WriteSyntheticBinding(string root, string approvedHead,
        string schema = "lane-0-corrective-evaluation-publication-binding.2",
        object? authorizedExecutionCount = null)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = schema,
            approvedPublishedHead = approvedHead,
            authorizedExecutionCount = authorizedExecutionCount ?? 1
        });
        File.WriteAllBytes(Path.Combine(root, Lane0CorrectiveExecutionHardening.BindingPath), bytes);
    }

    private static void RunExternalHostPreflight(string repository, Action invokeTrackedLauncher)
    {
        var bindingPath = Path.Combine(repository, Lane0CorrectiveExecutionHardening.BindingPath);
        if (!File.Exists(bindingPath)) throw new FileNotFoundException("Canonical binding absent.");
        using var document = JsonDocument.Parse(File.ReadAllBytes(bindingPath));
        var root = document.RootElement;
        var schema = root.GetProperty("schemaVersion").GetString();
        var approvedHead = root.GetProperty("approvedPublishedHead").GetString();
        if (schema != Lane0CorrectiveExecutionHardening.BindingSchema)
            throw new InvalidDataException("Binding schema is not v2.");
        if (approvedHead is null || approvedHead.Length != 40 || !approvedHead.All(Uri.IsHexDigit))
            throw new InvalidDataException("Approved HEAD is malformed.");
        var sourceHead = GitOutput(repository, "rev-parse", "--verify", "HEAD");
        if (sourceHead.Length != 40 || !sourceHead.All(Uri.IsHexDigit)
            || !string.Equals(approvedHead, sourceHead, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Binding HEAD does not equal source HEAD.");
        var unstaged = GitExitCode(repository, "diff", "--quiet", "HEAD", "--");
        var staged = GitExitCode(repository, "diff", "--cached", "--quiet");
        if (unstaged != 0 || staged != 0)
            throw new InvalidDataException("Tracked source is dirty.");
        if (GitExitCode(repository, "cat-file", "-e",
                $"{sourceHead}:{Lane0CorrectiveExecutionHardening.BindingPath}") == 0)
            throw new InvalidDataException("Canonical binding is tracked at source HEAD.");
        var receipt = Path.Combine(repository, Lane0CorrectiveExecutionHardening.AttemptReceiptPath);
        if (File.Exists(receipt) || Directory.Exists(receipt))
            throw new InvalidDataException("Durable authorization-root receipt already exists.");
        invokeTrackedLauncher();
    }

    private static void RunPowerShellScript(string path, params string[] arguments)
    {
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(path);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot run pwsh.");
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Synthetic launcher failed.");
    }

    private static ProcessResult RunIsolatedLauncher(string repository, string execution, string corpus)
    {
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = RepositoryRoot(), RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false
        };
        foreach (var argument in new[]
        {
            "-NoProfile", "-File", IsolatedLauncherPath(), "-RepositoryRoot", repository,
            "-ExecutionRoot", execution, "-CorpusRoot", corpus
        }) start.ArgumentList.Add(argument);
        start.Environment["DOTNET_GCHeapHardLimit"] = "0x400000000";
        start.Environment["DOTNET_GCConserveMemory"] = "9";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot run pwsh.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new(process.ExitCode, stdout, stderr);
    }

    private static string IsolatedLauncherPath() => Path.Combine(RepositoryRoot(),
        "tools", "InvokeLane0CorrectiveExecutionIsolated.ps1");

    private static string NonexistentTempPath(string prefix) => Path.Combine(Path.GetTempPath(),
        $"{prefix}-{Guid.NewGuid():N}");

    private static string GitOutput(string root, params string[] arguments)
    {
        var result = RunGit(root, arguments);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.StandardError);
        return result.StandardOutput.Trim();
    }

    private static int GitExitCode(string root, params string[] arguments) =>
        RunGit(root, arguments).ExitCode;

    private static ProcessResult RunGit(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot run git.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new(process.ExitCode, stdout, stderr);
    }

    private static void Git(string root, params string[] arguments)
    {
        var result = RunGit(root, arguments);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.StandardError);
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(entry, FileAttributes.Normal);
        Directory.Delete(path, true);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string AllOutput => StandardOutput + StandardError;
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
            DeleteTree(Path);
        }
    }
}
