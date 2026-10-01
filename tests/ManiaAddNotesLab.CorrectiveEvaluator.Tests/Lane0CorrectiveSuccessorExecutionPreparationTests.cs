using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;
using Xunit;

namespace ManiaAddNotesLab.CorrectiveEvaluator.Tests;

public sealed class Lane0CorrectiveSuccessorExecutionPreparationTests
{
    [Fact]
    public void VerifiedManifestIsIndivisibleAndStaticPreparationSucceeds()
    {
        Assert.Empty(typeof(VerifiedFrozenC11Manifest)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        var result = Lane0CorrectiveSuccessorExecutionPreparation.ValidateStatic(
            Contract(), Observed(), Manifest(), References());
        Assert.Equal("READY", result.PreparationOutcome);
        Assert.Equal(0, result.CorpusAccessCalls);
    }

    [Fact]
    public void Phase2SurfaceCannotAcceptFabricatedShaAndChartsOrRedirectPaths()
    {
        var parameters = typeof(Lane0SuccessorExecutionRequest).GetProperties()
            .Select(x => x.Name).ToArray();
        Assert.DoesNotContain("ReceiptPath", parameters);
        Assert.DoesNotContain("FinalOutputPath", parameters);
        Assert.DoesNotContain(typeof(Lane0CorrectiveSuccessorExecutionPreparation)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic), method =>
            method.GetParameters().Any(parameter => parameter.ParameterType
                == typeof(IEnumerable<C11FrozenCorpusExpectation>)));
    }

    [Theory]
    [InlineData("historical-b")]
    [InlineData("unknown")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("other-valid-chart")]
    [InlineData("10+1")]
    [InlineData("9+1")]
    [InlineData("wrong-family")]
    [InlineData("wrong-keymode")]
    public void ReferenceAndDistributionBadControlsFailBeforeCorpusAccess(string mutation)
    {
        var manifest = Manifest();
        var references = References();
        references = mutation switch
        {
            "historical-b" => references.SetItem(1, references[1] with
                { ChartId = Lane0CorrectiveSuccessorReferenceValidator.HistoricalInvalidSpringIdentity }),
            "unknown" => references.SetItem(1, references[1] with { ChartId = new string('F', 64) }),
            "missing" => references.RemoveAt(1),
            "duplicate" => references.Add(references[1]),
            "other-valid-chart" => references.SetItem(1, references[1] with
                { ChartId = manifest.Charts.First(x => references.All(r => r.ChartId != x.Sha256)).Sha256 }),
            "10+1" => references.SetItem(0, references[0] with { HistoricallySupportedCount = 10 })
                .SetItem(1, references[1] with { HistoricallySupportedCount = 1 }),
            "9+1" => references.SetItem(1, references[1] with { HistoricallySupportedCount = 1 }),
            "wrong-family" => references.SetItem(1, references[1] with { ExpectedManifestFamily = "WRONG" }),
            "wrong-keymode" => references.SetItem(1, references[1] with { Keymode = 4 }),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var adapter = new FakeAdapter();
        var result = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            Request(references: references), adapter, new FakeEvaluator());
        Assert.Equal("INVALID", result.PreparationOutcome);
        Assert.Equal(0, adapter.AccessCalls);
        Assert.False(result.ReceiptCreated);
    }

    [Theory]
    [InlineData("phase1")]
    [InlineData("phase2")]
    [InlineData("manifest")]
    [InlineData("implementation")]
    [InlineData("harness")]
    [InlineData("package")]
    [InlineData("dependencies")]
    public void IdentityBadControlsFailBeforeCorpusAccess(string mutation)
    {
        var contract = Contract();
        var observed = Observed();
        if (mutation == "phase1") observed = observed with { Phase1ContractSha256 = Hash('1') };
        if (mutation == "phase2") observed = observed with { Phase2ContractSha256 = Hash('2') };
        if (mutation == "manifest") contract = contract with { ManifestSha256 = Hash('3') };
        if (mutation == "implementation") observed = observed with { ScientificImplementationSha256 = Hash('4') };
        if (mutation == "harness") observed = observed with { HarnessSha256 = Hash('5') };
        if (mutation == "package") observed = observed with { PackageVerifierSha256 = Hash('6') };
        if (mutation == "dependencies") observed = observed with { FrozenDependenciesSha256 = Hash('7') };
        var adapter = new FakeAdapter();
        var result = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            Request(contract: contract, observed: observed), adapter, new FakeEvaluator());
        Assert.Equal("INVALID", result.PreparationOutcome);
        Assert.Equal(0, adapter.AccessCalls);
        Assert.False(result.ReceiptCreated);
    }

    [Theory]
    [InlineData("missing-binding", "BLOCKED")]
    [InlineData("false-authorization", "BLOCKED")]
    [InlineData("binding-head", "INVALID")]
    [InlineData("binding-bytes", "INVALID")]
    [InlineData("source-head", "INVALID")]
    [InlineData("execution-head", "INVALID")]
    [InlineData("runtime-head", "INVALID")]
    [InlineData("dirty-source", "INVALID")]
    [InlineData("same-source-execution", "INVALID")]
    [InlineData("execution-under-source", "INVALID")]
    [InlineData("source-under-execution", "INVALID")]
    [InlineData("existing-receipt", "BLOCKED")]
    [InlineData("existing-destination", "BLOCKED")]
    public void AuthorityAndRootFailuresOccurBeforeCorpusAccess(string mutation, string outcome)
    {
        using var temp = new TempRoots();
        var request = Request(temp);
        if (mutation == "missing-binding") request = request with { Binding = null };
        if (mutation == "false-authorization") request = Rebind(request,
            fields => fields with { ExplicitHumanAuthorization = false });
        if (mutation == "binding-head") request = Rebind(request,
            fields => fields with { ApprovedPublicHead = new string('a', 40) });
        if (mutation == "binding-bytes") request = request with
        {
            Binding = request.Binding! with { CanonicalBytes = [(byte)'{', (byte)'}', (byte)'\n'] }
        };
        if (mutation == "source-head") request = request with { SourceHead = new string('b', 40) };
        if (mutation == "execution-head") request = request with { ExecutionHead = new string('c', 40) };
        if (mutation == "runtime-head") request = request with { RuntimeHead = new string('d', 40) };
        if (mutation == "dirty-source") request = request with { SourceTrackedClean = false };
        if (mutation == "same-source-execution") request = request with { ExecutionRoot = request.SourceRoot };
        if (mutation == "execution-under-source") request = request with
            { ExecutionRoot = Path.Combine(request.SourceRoot, "nested") };
        if (mutation == "source-under-execution") request = request with
            { SourceRoot = Path.Combine(request.ExecutionRoot, "nested") };
        if (mutation == "existing-receipt")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(temp.Receipt)!);
            File.WriteAllText(temp.Receipt, "occupied");
        }
        if (mutation == "existing-destination") Directory.CreateDirectory(temp.Output);
        var adapter = new FakeAdapter();
        var result = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            request, adapter, new FakeEvaluator());
        Assert.Equal(outcome, result.PreparationOutcome);
        Assert.Equal(0, adapter.AccessCalls);
        Assert.Equal(0, result.ScientificPassesStarted);
    }

    [Fact]
    public void MalformedCorpusTokenIsOpaqueUntilReceipt()
    {
        using var temp = new TempRoots();
        var adapter = new FakeAdapter();
        var request = Request(temp) with { Binding = null, CorpusRoot = "bad\0corpus" };
        var result = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            request, adapter, new FakeEvaluator());
        Assert.Equal("BLOCKED", result.PreparationOutcome);
        Assert.False(result.ReceiptCreated);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Fact]
    public void MalformedCorpusTokenAfterAuthorityIsInvalidAndConsumesReceipt()
    {
        using var temp = new TempRoots();
        var adapter = new FakeAdapter();
        var result = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            Request(temp) with { CorpusRoot = "bad\0corpus" }, adapter, new FakeEvaluator());
        Assert.Equal("INVALID", result.PreparationOutcome);
        Assert.True(result.ReceiptCreated);
        Assert.True(File.Exists(temp.Receipt));
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("execution")]
    public void CorpusNestedUnderAuthorityRootIsRejectedAfterReceipt(string parent)
    {
        using var temp = new TempRoots();
        var request = Request(temp);
        request = request with { CorpusRoot = Path.Combine(
            parent == "source" ? request.SourceRoot : request.ExecutionRoot, "corpus") };
        var result = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            request, new FakeAdapter(), new FakeEvaluator());
        Assert.Equal("INVALID", result.PreparationOutcome);
        Assert.True(result.ReceiptCreated);
        Assert.Equal(0, result.CorpusAccessCalls);
    }

    [Theory]
    [InlineData("FEASIBILITY_DEMONSTRATED")]
    [InlineData("LIMITED_PARK")]
    public void AllowedScientificOutcomeIsPropagated(string scientificOutcome)
    {
        using var temp = new TempRoots();
        var result = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            Request(temp), new FakeAdapter(), new FakeEvaluator(scientificOutcome));
        Assert.Equal("PREPARATION_VALIDATED", result.PreparationOutcome);
        Assert.Equal(scientificOutcome, result.ScientificOutcome);
        Assert.Equal(2, result.ScientificPassesStarted);
        Assert.Equal(2, result.ScientificPassesCompleted);
    }

    [Theory]
    [InlineData("INVALID")]
    [InlineData("BLOCKED")]
    public void InvalidOrBlockedScienceCannotBecomePreparationValidated(string scientificOutcome)
    {
        using var temp = new TempRoots();
        var result = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            Request(temp), new FakeAdapter(), new FakeEvaluator(scientificOutcome));
        Assert.Equal("INVALID", result.PreparationOutcome);
        Assert.Equal(scientificOutcome, result.ScientificOutcome);
        Assert.True(result.ReceiptCreated);
        Assert.True(File.Exists(temp.Receipt));
    }

    [Fact]
    public async Task ConcurrentSyntheticAttemptsAllowOnlyOneAcrossReceiptBoundary()
    {
        using var temp = new TempRoots();
        var start = new ManualResetEventSlim(false);
        Task<Lane0SuccessorPreparationResult> Run() => Task.Run(() =>
        {
            start.Wait();
            return Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
                Request(temp), new FakeAdapter(), new FakeEvaluator());
        });
        var first = Run();
        var second = Run();
        start.Set();
        var results = await Task.WhenAll(first, second);
        Assert.Single(results, x => x.PreparationOutcome == "PREPARATION_VALIDATED");
        Assert.Single(results, x => x.PreparationOutcome == "BLOCKED");
        Assert.True(File.Exists(temp.Receipt));
    }

    [Theory]
    [InlineData("package")]
    [InlineData("rng")]
    [InlineData("input")]
    [InlineData("behavior")]
    [InlineData("default")]
    public void TwoPassAndNonInterferenceBadControlsFailClosedAfterReceipt(string mutation)
    {
        using var temp = new TempRoots();
        var evaluator = new FakeEvaluator(diverge: mutation == "package",
            rng: mutation == "rng" ? 1 : 0, inputsUnchanged: mutation != "input",
            behaviorChanged: mutation == "behavior", defaultChanged: mutation == "default");
        var result = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            Request(temp), new FakeAdapter(), evaluator);
        Assert.Equal("INVALID", result.PreparationOutcome);
        Assert.True(result.ReceiptCreated);
        Assert.True(File.Exists(temp.Receipt));
    }

    [Fact]
    public void AdapterFailureDoesNotClaimScientificPassStarted()
    {
        using var temp = new TempRoots();
        var result = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            Request(temp), new FakeAdapter(throwOnLoad: true), new FakeEvaluator());
        Assert.Equal("INVALID", result.PreparationOutcome);
        Assert.True(result.ReceiptCreated);
        Assert.Equal(0, result.ScientificPassesStarted);
        Assert.Equal(0, result.ScientificPassesCompleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnyPostReceiptExceptionConsumesAuthorityAndRetryIsBlocked(bool unexpected)
    {
        using var temp = new TempRoots();
        var first = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            Request(temp), new FakeAdapter(), new ThrowingEvaluator(unexpected));
        var retryAdapter = new FakeAdapter();
        var retry = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            Request(temp), retryAdapter, new FakeEvaluator());
        Assert.Equal("INVALID", first.PreparationOutcome);
        Assert.True(first.ReceiptCreated);
        Assert.Equal(1, first.ScientificPassesStarted);
        Assert.Equal(0, first.ScientificPassesCompleted);
        Assert.Equal("BLOCKED", retry.PreparationOutcome);
        Assert.Equal(0, retryAdapter.AccessCalls);
    }

    [Fact]
    public void ReceiptHashesExactCanonicalBindingBytes()
    {
        using var temp = new TempRoots();
        var request = Request(temp);
        _ = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            request, new FakeAdapter(), new FakeEvaluator());
        using var receipt = JsonDocument.Parse(File.ReadAllText(temp.Receipt));
        Assert.Equal(request.Binding!.CanonicalSha256,
            receipt.RootElement.GetProperty("bindingSha256").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(request.Binding.CanonicalBytes.AsSpan())),
            request.Binding.CanonicalSha256);
    }

    [Fact]
    public void RealSuccessorEvaluatorUsesAAndSuccessorSchemaWithoutHistoricalBGate()
    {
        var inputs = ImmutableArray.Create(new Lane0CorrectiveChartInput("synthetic-safe",
            OsuBeatmap.Parse(File.ReadAllText(Path.Combine(Root(), "samples", "09-basic-4k.osu")))));
        var pass = new Lane0CorrectiveSuccessorEvaluationRunner().Evaluate(inputs, Context());
        Assert.DoesNotContain(pass.IntegrityFailures, failure => failure.Contains(
            "contradicts the canonical rule", StringComparison.OrdinalIgnoreCase));
        Lane0CorrectiveSuccessorPackage.Verify(pass.Package);
        using var summary = JsonDocument.Parse(pass.Package["scientific_summary.json"]);
        Assert.Equal(Lane0CorrectiveSuccessorEvaluationRunner.SchemaVersion,
            summary.RootElement.GetProperty("schemaVersion").GetString());
        using var references = JsonDocument.Parse(pass.Package["successor_references.json"]);
        var active = references.RootElement.GetProperty("activeReferences").GetRawText();
        Assert.Contains(Lane0CorrectiveSuccessorReferenceValidator.CorrectedSpringIdentity, active);
        Assert.DoesNotContain(Lane0CorrectiveSuccessorReferenceValidator.HistoricalInvalidSpringIdentity, active);
        Assert.Equal("9+2=11", references.RootElement.GetProperty("historicalDistribution").GetString());
    }

    [Fact]
    public void ActualHarnessWiresRealEvaluatorAndFailsClosedOnSyntheticSnapshotMismatch()
    {
        using var temp = new TempRoots();
        var chart = new Lane0CorrectiveChartInput("synthetic-safe",
            OsuBeatmap.Parse(File.ReadAllText(Path.Combine(Root(), "samples", "09-basic-4k.osu"))));
        var result = Lane0CorrectiveSuccessorExecutionPreparation.ExecuteSynthetic(
            Request(temp), new FakeAdapter([chart]), new Lane0CorrectiveSuccessorEvaluationRunner());
        Assert.Equal("INVALID", result.PreparationOutcome);
        Assert.Equal("INVALID", result.ScientificOutcome);
        Assert.Equal(2, result.ScientificPassesCompleted);
        Assert.True(File.Exists(temp.Receipt));
    }

    [Fact]
    public void SuccessorSourceDoesNotCallHistoricalEvaluateEntrypoint()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "tools", "ManiaAddNotesLab.Experiments",
            "Lane0CorrectiveSuccessorEvaluator.cs"));
        Assert.DoesNotContain("Lane0CorrectiveEvaluationRunner.Evaluate(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageOrderAndChecksumsAreFrozenAndExcludeSelfHash()
    {
        var package = Package("same");
        Lane0CorrectiveSuccessorPackage.Verify(package);
        Assert.Equal(8, package.Count);
        var lines = Encoding.UTF8.GetString(package["sha256sums.txt"])
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(7, lines.Length);
        Assert.DoesNotContain(lines, line => line.EndsWith("sha256sums.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void ContractRuntimeIdentitiesAreOrthogonalAndExcludeTestsAndDocs()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "docs",
            "lane_0_corrective_successor_execution_preparation_contract.json")));
        var contract = document.RootElement.GetProperty("contract");
        var science = Paths(contract, "scientificImplementationFiles");
        var harness = Paths(contract, "executionHarnessFiles");
        var package = Paths(contract, "packageVerifierFiles");
        var dependencies = Paths(contract, "frozenDependencyFiles");
        Assert.Equal([
            "src/ManiaAddNotesLab.Core/Lane0CorrectiveSuccessorReferenceValidator.cs",
            "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorEvaluator.cs"
        ], science);
        Assert.Equal(["tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorExecutionPreparation.cs"], harness);
        Assert.Equal(["tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorPackage.cs"], package);
        Assert.Empty(science.Intersect(harness).Concat(science.Intersect(package)).Concat(harness.Intersect(package)));
        Assert.DoesNotContain(science.Concat(harness).Concat(package), path =>
            path.StartsWith("tests/", StringComparison.Ordinal) || path.StartsWith("docs/", StringComparison.Ordinal));
        Assert.Equal(contract.GetProperty("scientificImplementationSha256").GetString(), TreeIdentity(science));
        Assert.Equal(contract.GetProperty("executionHarnessSha256").GetString(), TreeIdentity(harness));
        Assert.Equal(contract.GetProperty("packageVerifierSha256").GetString(), TreeIdentity(package));
        Assert.Equal(contract.GetProperty("frozenDependenciesSha256").GetString(), TreeIdentity(dependencies));
    }

    private static Lane0SuccessorExecutionRequest Rebind(Lane0SuccessorExecutionRequest request,
        Func<Lane0SuccessorBindingFields, Lane0SuccessorBindingFields> mutate) => request with
        { Binding = Lane0CorrectiveSuccessorExecutionPreparation.CreateVerifiedBinding(mutate(request.Binding!.Fields)) };

    private static Lane0SuccessorExecutionRequest Request(
        ImmutableArray<Lane0CorrectiveSuccessorReference>? references = null,
        Lane0SuccessorExecutionContractState? contract = null,
        Lane0SuccessorObservedIdentities? observed = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "lane0-successor-unused", Guid.NewGuid().ToString("N"));
        return RequestCore(root, references, contract, observed);
    }

    private static Lane0SuccessorExecutionRequest Request(TempRoots temp) =>
        RequestCore(temp.Root, null, null, null);

    private static Lane0SuccessorExecutionRequest RequestCore(string root,
        ImmutableArray<Lane0CorrectiveSuccessorReference>? references,
        Lane0SuccessorExecutionContractState? contract,
        Lane0SuccessorObservedIdentities? observed)
    {
        contract ??= Contract();
        observed ??= Observed();
        var fields = new Lane0SuccessorBindingFields(
            Lane0CorrectiveSuccessorExecutionPreparation.BindingSchema,
            contract.ApprovedPublicHead, contract.Phase1ContractSha256,
            contract.Phase2ContractSha256, contract.ScientificImplementationSha256,
            contract.HarnessSha256, contract.PackageVerifierSha256,
            contract.FrozenDependenciesSha256, true, 1);
        return new(contract, observed, Manifest(), references ?? References(),
            Lane0CorrectiveSuccessorExecutionPreparation.CreateVerifiedBinding(fields),
            Path.Combine(root, "source"), Path.Combine(root, "execution"), Path.Combine(root, "corpus"),
            contract.ApprovedPublicHead, contract.ApprovedPublicHead, contract.ApprovedPublicHead, true);
    }

    private static Lane0SuccessorExecutionContractState Contract() => new(
        Lane0CorrectiveSuccessorExecutionPreparation.Phase1ContractSha256,
        Hash('A'), Lane0CorrectiveSuccessorExecutionPreparation.ManifestSha256,
        Hash('B'), Hash('C'), Hash('D'), Hash('E'),
        "9e239f8d74d3624d54568af74ff1e420e48c7083");

    private static Lane0SuccessorObservedIdentities Observed() => new(
        Lane0CorrectiveSuccessorExecutionPreparation.Phase1ContractSha256,
        Hash('A'), Hash('B'), Hash('C'), Hash('D'), Hash('E'));

    private static Lane0SuccessorEvaluationContext Context() => new(
        Contract().ApprovedPublicHead, Contract().Phase1ContractSha256, Contract().Phase2ContractSha256,
        Contract().ManifestSha256, Contract().ScientificImplementationSha256, Contract().HarnessSha256,
        Contract().PackageVerifierSha256, Contract().FrozenDependenciesSha256,
        References());

    private static ImmutableArray<Lane0CorrectiveSuccessorReference> References() =>
        Lane0CorrectiveSuccessorReferenceValidator.FrozenReferences;

    private static VerifiedFrozenC11Manifest Manifest() => FrozenC11ManifestResearch.Load(
        Path.Combine(Root(), "docs", "g1_gate_runtime_manifest.json"));

    private static string Hash(char value) => new(value, 64);

    private static string[] Paths(JsonElement contract, string property) => contract.GetProperty(property)
        .EnumerateArray().Select(x => x.GetString()!).ToArray();

    private static string TreeIdentity(IEnumerable<string> paths)
    {
        var rows = paths.Order(StringComparer.Ordinal).Select(path =>
        {
            var text = File.ReadAllText(Path.Combine(Root(), path.Replace('/', Path.DirectorySeparatorChar)));
            if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
            text = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);
            return $"{path}|{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))}";
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', rows))));
    }

    private sealed class FakeAdapter(
        ImmutableArray<Lane0CorrectiveChartInput>? inputs = null,
        bool throwOnLoad = false) : ILane0SuccessorCorpusAdapter
    {
        public int AccessCalls { get; private set; }
        public ImmutableArray<Lane0CorrectiveChartInput> Load(VerifiedFrozenC11Manifest manifest)
        {
            AccessCalls++;
            if (throwOnLoad) throw new IOException("synthetic adapter failure");
            return inputs ?? [];
        }
    }

    private sealed class FakeEvaluator(
        string scientificOutcome = "LIMITED_PARK",
        bool diverge = false,
        int rng = 0,
        bool inputsUnchanged = true,
        bool behaviorChanged = false,
        bool defaultChanged = false) : ILane0SuccessorScientificEvaluator
    {
        public int Calls { get; private set; }
        public Lane0SuccessorEvaluationPass Evaluate(
            ImmutableArray<Lane0CorrectiveChartInput> inputs,
            Lane0SuccessorEvaluationContext context)
        {
            Calls++;
            return new(scientificOutcome, Package(diverge && Calls == 2 ? "different" : "same"),
                [], [], rng, inputsUnchanged, behaviorChanged, defaultChanged);
        }
    }

    private sealed class ThrowingEvaluator(bool unexpected) : ILane0SuccessorScientificEvaluator
    {
        public Lane0SuccessorEvaluationPass Evaluate(
            ImmutableArray<Lane0CorrectiveChartInput> inputs,
            Lane0SuccessorEvaluationContext context) => unexpected
            ? throw new NullReferenceException("synthetic unexpected failure")
            : throw new InvalidDataException("synthetic expected failure");
    }

    private static ImmutableSortedDictionary<string, byte[]> Package(string value)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var name in Lane0CorrectiveSuccessorPackage.ArtifactNames.Where(x => x != "sha256sums.txt"))
            builder[name] = Encoding.UTF8.GetBytes(value + ":" + name);
        var semantic = builder.ToImmutable();
        builder["sha256sums.txt"] = Encoding.UTF8.GetBytes(string.Join('\n', semantic.Select(pair =>
            $"{Convert.ToHexString(SHA256.HashData(pair.Value))}  {pair.Key}")) + "\n");
        return builder.ToImmutable();
    }

    private sealed class TempRoots : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(),
            "lane0-successor-phase2-" + Guid.NewGuid().ToString("N"));
        public string Receipt => Path.Combine(Root, "source", ".artifacts",
            "lane_0_corrective_successor.attempt.json");
        public string Output => Path.Combine(Root, "execution", ".artifacts",
            "lane_0_corrective_successor");
        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ManiaAddNotesLab.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
