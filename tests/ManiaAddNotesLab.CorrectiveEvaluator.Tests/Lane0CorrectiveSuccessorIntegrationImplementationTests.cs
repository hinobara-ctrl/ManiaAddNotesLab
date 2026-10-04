using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ManiaAddNotesLab.Core;
using Xunit;

public sealed class Lane0CorrectiveSuccessorIntegrationImplementationTests
{
    [Fact]
    public void OfficialAdapterAdmitsTwelveLocationsButElevenScientificChartsDeterministically()
    {
        var manifest = Manifest();
        var reader = new ManifestReader(manifest);
        var adapter = new Lane0CorrectiveSuccessorFrozenC11Adapter(manifest, reader,
            new ManifestCodec(manifest), PathPlan(manifest));

        var result = adapter.Admit(manifest, Path.GetTempPath());

        Assert.Equal(12, result.LocationEvidence.Length);
        Assert.Equal(11, result.ScientificCharts.Length);
        Assert.Equal(50_836, result.UniqueOriginalObjects);
        Assert.Equal(2, result.LocationEvidence.Count(x => x.ChartId ==
            Lane0CorrectiveSuccessorFrozenC11Adapter.DuplicateChartId));
        Assert.Equal(12, reader.ReadPaths.Count);
        Assert.Equal(result.LocationEvidence.OrderBy(x => x.LocationId), result.LocationEvidence);
        Assert.Equal(result.ScientificCharts.OrderBy(x => x.ChartId), result.ScientificCharts);
    }

    [Fact]
    public void AdapterRejectsPathUniqueDuplicateAndMetadataDrift()
    {
        var manifest = Manifest();
        var plan = PathPlan(manifest);
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveSuccessorFrozenC11Adapter
            .ValidatePlan(manifest, plan.RemoveAt(0)));
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveSuccessorFrozenC11Adapter
            .ValidatePlan(manifest, plan.SetItem(0, plan[0] with { ChartId = "UNKNOWN" })));
        var duplicateIndex = Array.FindIndex(plan.ToArray(), x => x.ChartId ==
            Lane0CorrectiveSuccessorFrozenC11Adapter.DuplicateChartId);
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveSuccessorFrozenC11Adapter
            .ValidatePlan(manifest, plan.RemoveAt(duplicateIndex)));
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveSuccessorFrozenC11Adapter
            .ValidatePlan(manifest, plan.SetItem(0, plan[0] with { KeyCount = 18 })));
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveSuccessorFrozenC11Adapter
            .ValidatePlan(manifest, plan.Add(plan[0] with { LocationId = "EXTRA", RelativePath = "extra.osu" })));
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveSuccessorFrozenC11Adapter
            .ValidatePlan(manifest, plan.SetItem(0, plan[0] with { RelativePath = "renamed.osu" }), plan));
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveSuccessorFrozenC11Adapter
            .ValidatePlan(manifest, plan.SetItem(0, plan[0] with { Family = "DRIFT" })));
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveSuccessorFrozenC11Adapter
            .ValidatePlan(manifest, plan.SetItem(0, plan[0] with { OriginalObjects = 1 })));
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveSuccessorFrozenC11Adapter
            .ValidatePlan(manifest, plan.SetItem(0, plan[0] with { RelativePath = "../escape.osu" })));
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveSuccessorFrozenC11Adapter
            .ValidatePlan(manifest, plan.SetItem(0, plan[0] with { RelativePath = Path.GetFullPath("absolute.osu") })));
    }

    [Fact]
    public void AdapterRejectsWrongContentIdentityWithoutEnumerationOrSongsDiscovery()
    {
        var manifest = Manifest();
        var reader = new ManifestReader(manifest) { CorruptFirstRead = true };
        var adapter = new Lane0CorrectiveSuccessorFrozenC11Adapter(manifest, reader,
            new ManifestCodec(manifest), PathPlan(manifest));
        Assert.Throws<InvalidDataException>(() => adapter.Admit(manifest, Path.GetTempPath()));
        Assert.Single(reader.ReadPaths);
        Assert.DoesNotContain(reader.ReadPaths, x => x.Contains("Songs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FrozenPathAuthorityRejectsSemanticDriftEvenWhenDeclaredHashIsRecomputed()
    {
        var manifest = Manifest();
        Assert.Equal(12, Lane0CorrectiveSuccessorFrozenC11Adapter
            .LoadFrozenPathAuthority(RepoRoot(), manifest).Length);
        using var temp = new TempDirectory();
        var docs = Path.Combine(temp.Path, "docs");
        Directory.CreateDirectory(docs);
        var source = Path.Combine(RepoRoot(),
            Lane0CorrectiveSuccessorFrozenC11Adapter.PathAuthorityRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var target = Path.Combine(temp.Path,
            Lane0CorrectiveSuccessorFrozenC11Adapter.PathAuthorityRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var root = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
        root["authority"]!["locations"]![0]!["relativePath"] = "substituted.osu";
        File.WriteAllText(target, root.ToJsonString());
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveSuccessorFrozenC11Adapter
            .LoadFrozenPathAuthority(temp.Path, manifest));
        using var changed = JsonDocument.Parse(root["authority"]!.ToJsonString());
        root["canonicalSha256"] = Lane0CorrectiveSuccessorFrozenC11Adapter
            .CanonicalJsonHash(changed.RootElement);
        File.WriteAllText(target, root.ToJsonString());
        Assert.Throws<InvalidDataException>(() => Lane0CorrectiveSuccessorFrozenC11Adapter
            .LoadFrozenPathAuthority(temp.Path, manifest));
    }

    [Fact]
    public void PreReceiptGateTreatsPoisonCorpusRootAsOpaque()
    {
        using var temp = new TempDirectory();
        var launcher = SyntheticLauncher(new FakeCheckoutMaterializer());
        var store = new MemoryAuthorityStore();
        var request = LaunchRequest(temp.Path, "\0POISON-CORPUS");

        var lease = launcher.Acquire(request, store);

        Assert.True(store.ReceiptExists);
        Assert.Equal(13, lease.CompletedAuthoritySteps.Length);
        Assert.ThrowsAny<Exception>(() => launcher.InterpretCorpusRoot(lease));
    }

    [Theory]
    [InlineData("absent-binding")]
    [InlineData("wrong-head")]
    [InlineData("runtime-head")]
    [InlineData("dirty")]
    [InlineData("no-authorization")]
    [InlineData("wrong-count")]
    [InlineData("receipt")]
    [InlineData("output")]
    public void LauncherRejectsInvalidAuthorityBeforeCorpusInterpretation(string mutation)
    {
        using var temp = new TempDirectory();
        var request = LaunchRequest(temp.Path, "\0STILL-OPAQUE");
        var store = new MemoryAuthorityStore();
        var observer = new FakeAuthorityObserver(request.ExpectedSourcePublicHead,
            request.ExpectedIdentities, Clean: mutation != "dirty");
        var runtime = new FakeIsolatedRuntime(new CountingScience(identical: true),
            runtimeHead: mutation == "runtime-head" ? new string('E', 40) : request.ExpectedSourcePublicHead);
        request = mutation switch
        {
            "absent-binding" => request with { Binding = null },
            "wrong-head" or "runtime-head" or "dirty" => request,
            "no-authorization" => Rebind(request, authorization: false),
            "wrong-count" => Rebind(request, count: 2),
            "receipt" => request,
            "output" => request,
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        store.ReceiptExistsValue = mutation == "receipt";
        store.OutputExistsValue = mutation == "output";
        Assert.Throws<InvalidDataException>(() =>
            new Lane0CorrectiveSuccessorIsolatedLauncher(new FakeCheckoutMaterializer(),
                mutation == "wrong-head" ? observer with { Head = new string('F', 40) } : observer,
                runtime)
                .Acquire(request, store));
        Assert.False(store.CreateCalled);
    }

    [Fact]
    public void LauncherRejectsInvalidExecutionRootRelation()
    {
        using var temp = new TempDirectory();
        var request = LaunchRequest(temp.Path, "opaque") with
        {
            ExecutionRoot = Path.Combine(temp.Path, "source", "nested")
        };
        Assert.Throws<InvalidDataException>(() =>
            SyntheticLauncher(new FakeCheckoutMaterializer())
                .Acquire(request, new MemoryAuthorityStore()));
    }

    [Fact]
    public void LauncherRejectsHardlinkOrNonDetachedCheckoutEvidence()
    {
        using var temp = new TempDirectory();
        var request = LaunchRequest(temp.Path, "opaque");
        Assert.Throws<InvalidDataException>(() =>
            SyntheticLauncher(new BadCheckoutMaterializer())
                .Acquire(request, new MemoryAuthorityStore()));
    }

    [Fact]
    public async Task SyntheticDurableReceiptHasAtMostOneConcurrentWinnerAndCannotBeReused()
    {
        using var temp = new TempDirectory();
        var receipt = Path.Combine(temp.Path, "authority", "attempt.json");
        var stores = Enumerable.Range(0, 8).Select(_ => new Lane0IntegrationFileAuthorityStore(
            receipt, Path.Combine(temp.Path, "out"), Path.Combine(temp.Path, "stage"))).ToArray();
        var wins = 0;
        await Task.WhenAll(stores.Select(store => Task.Run(() =>
        {
            try { store.CreateDurableReceipt(Encoding.UTF8.GetBytes("synthetic\n")); Interlocked.Increment(ref wins); }
            catch (IOException) { }
        })));
        Assert.Equal(1, wins);
        Assert.All(stores, x => Assert.True(x.ReceiptExists));
        Assert.Throws<IOException>(() => stores[0].CreateDurableReceipt([1]));
    }

    [Fact]
    public void InternalRunnerInvokesTwoIndependentPassesAndSemanticVerificationInOrder()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest();
        var science = new CountingScience(identical: true);
        var semantic = new CountingSemanticVerifier();
        var finalizer = new IdentityPackageFinalizer();
        var runner = new Lane0CorrectiveSuccessorInternalResearchRunner(
            SyntheticLauncher(new FakeCheckoutMaterializer(), science),
            new FakeAdapter(manifest), semantic, finalizer);
        var store = new MemoryAuthorityStore();
        var result = runner.ExecuteSyntheticOrFutureAuthorized(new(
            LaunchRequest(temp.Path, Path.Combine(temp.Path, "corpus")), manifest,
            Lane0CorrectiveSuccessorReferenceValidator.FrozenReferences), store);

        Assert.True(result.Classification == "PUBLISHABLE", result.Reason);
        Assert.Equal(2, science.Calls);
        Assert.Equal(2, finalizer.Calls);
        Assert.Equal(1, semantic.Calls);
        Assert.Equal(21, result.CompletedAuthoritySteps.Length);
        Assert.True(result.ReceiptConsumed);
    }

    [Fact]
    public void InternalRunnerFailsClosedOnByteNondeterminismAndConsumesAttempt()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest();
        var science = new CountingScience(identical: false);
        var semantic = new CountingSemanticVerifier();
        var runner = new Lane0CorrectiveSuccessorInternalResearchRunner(
            SyntheticLauncher(new FakeCheckoutMaterializer(), science),
            new FakeAdapter(manifest), semantic, new IdentityPackageFinalizer());
        var result = runner.ExecuteSyntheticOrFutureAuthorized(new(
            LaunchRequest(temp.Path, Path.Combine(temp.Path, "corpus")), manifest,
            Lane0CorrectiveSuccessorReferenceValidator.FrozenReferences), new MemoryAuthorityStore());
        Assert.Equal("NON_PUBLISHABLE", result.Classification);
        Assert.True(result.ReceiptConsumed);
        Assert.Equal(0, semantic.Calls);
    }

    [Fact]
    public void DeepSemanticVerifierAcceptsConsistentSyntheticPackage()
    {
        var fixture = SemanticFixture();
        new Lane0CorrectiveSuccessorDeepSemanticPackageVerifier().Verify(fixture.Package, fixture.Expected);
    }

    [Fact]
    public void IntegrationPackageFinalizerFreezesNewIdentitiesAndRebuildsChecksums()
    {
        var fixture = SemanticFixture();
        var finalized = new Lane0IntegrationPackageFinalizer().Finalize(
            fixture.Package, fixture.Expected.Identities, defaultChanged: false);
        new Lane0CorrectiveSuccessorDeepSemanticPackageVerifier().Verify(finalized, fixture.Expected);
        Assert.NotEqual(fixture.Package["sha256sums.txt"], finalized["sha256sums.txt"]);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("checksum")]
    [InlineData("manifest")]
    [InlineData("integration")]
    [InlineData("rng")]
    [InlineData("behavior")]
    [InlineData("spring")]
    [InlineData("distribution")]
    [InlineData("aggregate")]
    [InlineData("defaults")]
    public void DeepSemanticVerifierRejectsSyntheticCorruption(string mutation)
    {
        var fixture = SemanticFixture();
        var package = fixture.Package;
        var expected = fixture.Expected;
        package = mutation switch
        {
            "missing" => package.Remove("integrity.json"),
            "checksum" => package.SetItem("integrity.json", [.. package["integrity.json"], (byte)' ']),
            "manifest" => MutateJson(package, "execution_identity.json", "manifestSha256", "BAD"),
            "integration" => MutateJson(package, "execution_identity.json",
                "integrationExecutionContractSha256", "BAD"),
            "rng" => MutateJson(package, "integrity.json", "researchRngCalls", 1),
            "behavior" => MutateJson(package, "scientific_summary.json", "behaviorChanged", true),
            "spring" => MutateJson(package, "successor_references.json", "historicalDistribution", "BROKEN"),
            "distribution" => ReplaceAndRehash(package, "g1_historical_operational_cases.csv",
                Encoding.UTF8.GetBytes(G1Cases(10, 1))),
            "aggregate" => ReplaceAndRehash(package, "chart_family_keymode_results.csv",
                Encoding.UTF8.GetBytes(ChartCsv("WRONG"))),
            "defaults" => package,
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        if (mutation == "defaults") expected = expected with { DefaultChanged = true };
        Assert.Throws<InvalidDataException>(() =>
            new Lane0CorrectiveSuccessorDeepSemanticPackageVerifier().Verify(package, expected));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void DeepSemanticVerifierRejectsEveryRehashedChartNumericDrift(int column)
    {
        var fixture = SemanticFixture();
        var changed = MutateCsvCell(fixture.Package, "chart_family_keymode_results.csv", 1, column, "1");
        Assert.Throws<InvalidDataException>(() =>
            new Lane0CorrectiveSuccessorDeepSemanticPackageVerifier().Verify(changed, fixture.Expected));
    }

    [Theory]
    [InlineData("g1_historical_operational_cases.csv", 1, 1, "4")]
    [InlineData("g1_historical_operational_cases.csv", 1, 2, "OTHER")]
    [InlineData("g1_historical_operational_cases.csv", 1, 3, "DRIFT")]
    [InlineData("g1_historical_operational_cases.csv", 1, 4, "DRIFT")]
    [InlineData("g1_historical_operational_cases.csv", 1, 5, "False")]
    [InlineData("g1_state_transitions.csv", 1, 1, "4")]
    [InlineData("g1_state_transitions.csv", 1, 2, "OTHER")]
    [InlineData("g1_state_transitions.csv", 1, 3, "OTHER")]
    [InlineData("g1_state_transitions.csv", 1, 4, "DRIFT")]
    [InlineData("g1_state_transitions.csv", 1, 5, "DRIFT")]
    [InlineData("g1_state_transitions.csv", 1, 6, "True")]
    [InlineData("g1_state_transitions.csv", 1, 7, "False")]
    [InlineData("g1_state_transitions.csv", 1, 8, "False")]
    [InlineData("g1_state_transitions.csv", 1, 9, "False")]
    [InlineData("g1_state_transitions.csv", 1, 10, "DRIFT")]
    public void DeepSemanticVerifierRejectsRehashedG1SemanticDrift(
        string artifact, int row, int column, string value)
    {
        var fixture = SemanticFixture();
        var changed = MutateCsvCell(fixture.Package, artifact, row, column, value);
        Assert.Throws<InvalidDataException>(() =>
            new Lane0CorrectiveSuccessorDeepSemanticPackageVerifier().Verify(changed, fixture.Expected));
    }

    [Fact]
    public void DeepSemanticVerifierRejectsScientificLocationSetAndMetadataDrift()
    {
        var fixture = SemanticFixture();
        var scientific = fixture.Expected.AdmittedCorpus.ScientificCharts;
        var missing = fixture.Expected with { AdmittedCorpus = fixture.Expected.AdmittedCorpus with
            { ScientificCharts = scientific.RemoveAt(0) } };
        Assert.Throws<InvalidDataException>(() =>
            new Lane0CorrectiveSuccessorDeepSemanticPackageVerifier().Verify(fixture.Package, missing));
        var first = scientific[0];
        var metadata = fixture.Expected with { AdmittedCorpus = fixture.Expected.AdmittedCorpus with
            { ScientificCharts = scientific.SetItem(0, first with { Chart = EmptyChart(first.Chart.KeyCount + 1,
                first.Chart.OriginalObjects.Count) }) } };
        Assert.Throws<InvalidDataException>(() =>
            new Lane0CorrectiveSuccessorDeepSemanticPackageVerifier().Verify(fixture.Package, metadata));
    }

    [Fact]
    public void ActualGitObservationRejectsDirtyStateAndCannotBeSuppliedByCaller()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "source");
        File.WriteAllText(Path.Combine(source, "tracked.txt"), "clean\n");
        RunGit(source, "init"); RunGit(source, "config", "user.email", "test@example.invalid");
        RunGit(source, "config", "user.name", "Test"); RunGit(source, "add", "tracked.txt");
        RunGit(source, "commit", "-m", "fixture");
        var observer = new GitAndFileIntegrationAuthorityObserver();
        Assert.Equal(RunGit(source, "rev-parse", "HEAD").Trim(), observer.ObserveHead(source));
        Assert.True(observer.ObserveTrackedClean(source));
        File.WriteAllText(Path.Combine(source, "tracked.txt"), "dirty\n");
        Assert.False(observer.ObserveTrackedClean(source));
        Assert.DoesNotContain(typeof(Lane0IntegrationLaunchRequest).GetProperties(), x =>
            x.Name is "SourceHead" or "RuntimeHead" or "SourceTrackedClean" or "ObservedIdentities");
    }

    [Fact]
    public void ExecutionRootRuntimeBuildsAndSelfReportsFromDetachedCheckout()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "source");
        var worker = Path.Combine(source, "worker");
        Directory.CreateDirectory(worker);
        File.WriteAllText(Path.Combine(worker, "worker.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><AssemblyName>SyntheticWorker</AssemblyName><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(worker, "Program.cs"), SyntheticProvenanceWorkerSource());
        RunGit(source, "init"); RunGit(source, "config", "user.email", "test@example.invalid");
        RunGit(source, "config", "user.name", "Test"); RunGit(source, "add", "worker");
        RunGit(source, "commit", "-m", "isolated-worker");
        var head = RunGit(source, "rev-parse", "HEAD").Trim();
        var execution = Path.Combine(temp.Path, "execution-real");
        var checkout = new GitDetachedCheckoutMaterializer().CreateDetachedCheckout(source, execution, head);
        Assert.True(checkout.Detached && checkout.TrackedClean && !checkout.UsedHardlinks);
        File.WriteAllText(Path.Combine(worker, "Program.cs"), "// host drift must not execute\n");
        var prepared = new Lane0ExecutionRootScientificRuntime("worker/worker.csproj", "SyntheticWorker.dll")
            .Prepare(execution, head);
        Assert.Equal(head, prepared.Provenance.RuntimeHead);
        Assert.StartsWith(Path.GetFullPath(execution), Path.GetFullPath(prepared.Provenance.AssemblyPath),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        Assert.Equal(Lane0ExecutionRootScientificRuntime.ExpectedWorkerMarker,
            prepared.Provenance.WorkerMarker);
    }

    [Fact]
    public void LowFakeExecutionRootWorkerRunsSyntheticScienceInRealChildProcess()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "source");
        var worker = Path.Combine(source, "worker");
        var payload = Path.Combine(worker, "payload");
        Directory.CreateDirectory(payload);
        File.WriteAllText(Path.Combine(worker, "worker.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><AssemblyName>SyntheticWorker</AssemblyName><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(worker, "Program.cs"), SyntheticEndToEndWorkerSource());
        File.WriteAllText(Path.Combine(worker, ".gitattributes"), "payload/* -text\n");
        var fixture = SemanticFixture();
        foreach (var pair in fixture.Package) File.WriteAllBytes(Path.Combine(payload, pair.Key), pair.Value);
        RunGit(source, "init"); RunGit(source, "config", "user.email", "test@example.invalid");
        RunGit(source, "config", "user.name", "Test"); RunGit(source, "add", "worker");
        RunGit(source, "commit", "-m", "real-worker-fixture");
        var head = RunGit(source, "rev-parse", "HEAD").Trim();
        var execution = Path.Combine(temp.Path, "execution-science");
        var checkout = new GitDetachedCheckoutMaterializer().CreateDetachedCheckout(source, execution, head);
        Assert.True(checkout.Detached && checkout.TrackedClean && !checkout.UsedHardlinks);
        var runtime = new Lane0ExecutionRootScientificRuntime("worker/worker.csproj", "SyntheticWorker.dll");
        var prepared = runtime.Prepare(execution, head);
        var token = new string('A', 64);
        var context = new Lane0SuccessorEvaluationContext(head, token, token, token, token, token,
            token, token, Lane0CorrectiveSuccessorReferenceValidator.FrozenReferences);
        var inputs = ImmutableArray.Create(new Lane0CorrectiveChartInput(token, EmptyChart(4)));
        var result = runtime.Execute(prepared, inputs, context);
        Assert.Equal(head, result.Provenance.RuntimeHead);
        Assert.Equal(result.First.ScientificOutcome, result.Second.ScientificOutcome);
        Assert.Equal(result.First.Package.Keys, result.Second.Package.Keys);
        Assert.All(result.First.Package.Keys, name =>
            Assert.Equal(result.First.Package[name], result.Second.Package[name]));
        new Lane0CorrectiveSuccessorDeepSemanticPackageVerifier()
            .Verify(result.First.Package, fixture.Expected);
    }

    [Fact]
    public void IntegrationRunnerHasNoProductProgramCallSite()
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot(), "tools",
            "ManiaAddNotesLab.Experiments", "Program.cs"));
        Assert.DoesNotContain(nameof(Lane0CorrectiveSuccessorInternalResearchRunner), program,
            StringComparison.Ordinal);
    }

    private static Lane0IntegrationLaunchRequest Rebind(Lane0IntegrationLaunchRequest request,
        bool authorization = true, int count = 1)
    {
        var fields = request.Binding!.Fields with
        {
            ExplicitHumanAuthorization = authorization, AuthorizedExecutionCount = count
        };
        return request with
        {
            Binding = Lane0CorrectiveSuccessorIsolatedLauncher.CreateSyntheticVerifiedBinding(fields)
        };
    }

    private static Lane0IntegrationLaunchRequest LaunchRequest(string root, string corpus)
    {
        var head = new string('A', 40);
        var ids = Identities();
        var fields = new Lane0IntegrationBindingFields(
            Lane0CorrectiveSuccessorIsolatedLauncher.BindingSchema, head, ids, true, 1);
        return new(head, ids,
            Lane0CorrectiveSuccessorIsolatedLauncher.CreateSyntheticVerifiedBinding(fields),
            Path.Combine(root, "source"), Path.Combine(root, "execution"), corpus);
    }

    private static Lane0CorrectiveSuccessorIsolatedLauncher SyntheticLauncher(
        ILane0IntegrationCheckoutMaterializer checkout,
        CountingScience? science = null)
    {
        var ids = Identities();
        var head = new string('A', 40);
        return new(checkout, new FakeAuthorityObserver(head, ids, true),
            new FakeIsolatedRuntime(science ?? new CountingScience(identical: true), head));
    }

    private static ImmutableArray<Lane0IntegrationCorpusLocation> PathPlan(
        VerifiedFrozenC11Manifest manifest) =>
        Lane0CorrectiveSuccessorFrozenC11Adapter.LoadFrozenPathAuthority(RepoRoot(), manifest);

    private static Lane0IntegrationRuntimeIdentities Identities() => new(
        new string('1', 64), "11F55A6770BA78F008A6690C88561C714CAF45BA15B96E472C0C80B219C4D5B6",
        "7C0A86BF3C6647CA7092C4D3390EB97EA6E11D7E5429ECBA6F499D55C5417A02",
        new string('2', 64), FrozenC11ManifestResearch.TrustedCanonicalSha256,
        "8F9AE545027F0F1364E3378324646C7F7711CDF7EFFC8CC879660644A3D905D0",
        "15DC978C24119E9832DD70C0C61F5BFB1BBAF11520A107A7728220009B793A61",
        "DFF48221A6FB087367B1C6B1062B7355806C53668C6876A4BB4EC59983232D38",
        new string('3', 64), new string('4', 64), new string('5', 64), new string('6', 64),
        "D5931A815701C0FAB854B878373A8ED03F3E29BDEA17C98A428C7AF6B0F7023B");

    private static VerifiedFrozenC11Manifest Manifest() => FrozenC11ManifestResearch.Load(
        Path.Combine(RepoRoot(), "docs", "g1_gate_runtime_manifest.json"));

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName,
                   "docs", "g1_gate_runtime_manifest.json"))) current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static (ImmutableSortedDictionary<string, byte[]> Package,
        Lane0IntegrationSemanticExpectations Expected) SemanticFixture()
    {
        var ids = Identities();
        var head = new string('A', 40);
        var locations = PathPlan(Manifest());
        var uniqueLocations = locations.GroupBy(x => x.ChartId, StringComparer.Ordinal)
            .Select(x => x.First()).OrderBy(x => x.ChartId, StringComparer.Ordinal).ToArray();
        var zero = new { structural = 0, holdouts = 0, operational = 0, comparableContext = 0,
            jointSupport = 0, jointUnique = 0, jointAmongAlternatives = 0, contradiction = 0,
            noContext = 0, marginalOnly = 0, ambiguousIntegrity = 0,
            operationalSupported = 0, integrityFailures = 0 };
        var charts = uniqueLocations.Select(x => new { chartId = x.ChartId, keyCount = x.KeyCount,
            family = "RICE_HEAD_COMPLETION", historical = zero, corrected = zero }).ToArray();
        var cases = Enumerable.Range(0, 11).Select(index => new
        {
            chartId = index < 9 ? Lane0CorrectiveSuccessorReferenceValidator.SecondHistoricalIdentity
                : Lane0CorrectiveSuccessorReferenceValidator.CorrectedSpringIdentity,
            keyCount = 7, occurrenceId = index < 9 ? $"A{index}" : $"B{index - 9}",
            historicalState = "OPERATIONAL",
            correctedState = "SUPPORTED", correctedSupported = true
        }).ToArray();
        var transitions = new[] { new
        {
            chartId = Lane0CorrectiveSuccessorReferenceValidator.SecondHistoricalIdentity,
            keyCount = 7, occurrenceId = "T0", groupId = "G0", historicalState = "NO_SUPPORT",
            correctedState = "SUPPORTED", historicalJointSupported = false,
            correctedJointSupported = true, historicalOperational = true,
            correctedOperational = true, transitionKind = "GAINED_SUPPORT"
        } };
        var files = ImmutableSortedDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        files["execution_identity.json"] = Json(new
        {
            schemaVersion = Lane0CorrectiveEvaluationRunner.SchemaVersion, approvedPublicHead = head,
            phase1ContractSha256 = ids.Phase1ContractSha256, phase2ContractSha256 = ids.Phase2ContractSha256,
            integrationPreregistrationSha256 = ids.IntegrationPreregistrationSha256,
            integrationExecutionContractSha256 = ids.IntegrationExecutionContractSha256,
            manifestSha256 = ids.ManifestSha256,
            scientificImplementationSha256 = ids.ScientificImplementationSha256,
            harnessSha256 = ids.Phase2HarnessSha256,
            packageVerifierSha256 = ids.Phase2PackageVerifierSha256,
            frozenDependenciesSha256 = ids.FrozenDependenciesSha256,
            adapterSha256 = ids.AdapterSha256, runnerSha256 = ids.RunnerSha256,
            launcherSha256 = ids.LauncherSha256, semanticVerifierSha256 = ids.SemanticVerifierSha256,
            defaultChanged = false, scientificOutcome = "LIMITED_PARK"
        });
        files["scientific_summary.json"] = Json(new
        {
            outcome = "LIMITED_PARK", charts, g1Transitions = transitions,
            historicalOperationalG1Cases = cases, researchRngCalls = 0, behaviorChanged = false,
            inputsUnchanged = true, integrityFailures = Array.Empty<string>(), blockedReasons = Array.Empty<string>()
        });
        files["integrity.json"] = Json(new
        {
            outcome = "LIMITED_PARK", integrityFailures = Array.Empty<string>(),
            blockedReasons = Array.Empty<string>(), researchRngCalls = 0,
            behaviorChanged = false, inputsUnchanged = true
        });
        files["successor_references.json"] = Json(new
        {
            historicalInvalidSpringB = Lane0CorrectiveSuccessorReferenceValidator.HistoricalInvalidSpringIdentity,
            activeReferences = Lane0CorrectiveSuccessorReferenceValidator.FrozenReferences,
            historicalDistribution = "9+2=11"
        });
        files["chart_family_keymode_results.csv"] = Encoding.UTF8.GetBytes(ChartCsvRows(
            uniqueLocations.Select(x => $"{x.ChartId},{x.KeyCount},RICE_HEAD_COMPLETION,0,0,0,0,0,0,0,0,0")));
        files["g1_historical_operational_cases.csv"] = Encoding.UTF8.GetBytes(G1Cases(9, 2));
        files["g1_state_transitions.csv"] = Encoding.UTF8.GetBytes(
            "chart_id,keymode,occurrence_id,group_id,historical_state,corrected_state,historical_joint_supported,corrected_joint_supported,historical_operational,corrected_operational,transition_kind\n"
            + $"{Lane0CorrectiveSuccessorReferenceValidator.SecondHistoricalIdentity},7,T0,G0,NO_SUPPORT,SUPPORTED,False,True,True,True,GAINED_SUPPORT\n");
        var semantic = files.ToImmutable();
        files["sha256sums.txt"] = ChecksumBytes(semantic);
        var scientific = uniqueLocations.Select(x => new Lane0CorrectiveChartInput(x.ChartId,
            EmptyChart(x.KeyCount, x.OriginalObjects))).ToImmutableArray();
        var admitted = new Lane0IntegrationAdmittedCorpus(locations, scientific, 50_836);
        return (files.ToImmutable(), new(ids, head, "LIMITED_PARK", admitted, false));
    }

    private static string ChartCsv(string id) =>
        ChartCsvRows([$"{id},4,RICE_HEAD_COMPLETION,0,0,0,0,0,0,0,0,0"]);

    private static string ChartCsvRows(IEnumerable<string> rows) =>
        "chart_id,keymode,family,historical_structural,historical_joint_support,historical_operational,historical_operational_supported,corrected_structural,corrected_joint_support,corrected_operational,corrected_operational_supported,corrected_integrity_failures\n"
        + string.Join('\n', rows) + "\n";

    private static string G1Cases(int second, int spring)
    {
        var rows = Enumerable.Range(0, second).Select(index =>
                $"{Lane0CorrectiveSuccessorReferenceValidator.SecondHistoricalIdentity},7,A{index},OPERATIONAL,SUPPORTED,True")
            .Concat(Enumerable.Range(0, spring).Select(index =>
                $"{Lane0CorrectiveSuccessorReferenceValidator.CorrectedSpringIdentity},7,B{index},OPERATIONAL,SUPPORTED,True"));
        return "chart_id,keymode,occurrence_id,historical_state,corrected_state,corrected_supported\n"
            + string.Join('\n', rows) + "\n";
    }

    private static ImmutableSortedDictionary<string, byte[]> MutateJson(
        ImmutableSortedDictionary<string, byte[]> package, string name, string property, object value)
    {
        using var document = JsonDocument.Parse(package[name]);
        var dictionary = document.RootElement.EnumerateObject().ToDictionary(x => x.Name,
            x => (object)x.Value.Clone(), StringComparer.Ordinal);
        dictionary[property] = value;
        return ReplaceAndRehash(package, name, Json(dictionary));
    }

    private static ImmutableSortedDictionary<string, byte[]> ReplaceAndRehash(
        ImmutableSortedDictionary<string, byte[]> package, string name, byte[] bytes)
    {
        var changed = package.SetItem(name, bytes).Remove("sha256sums.txt");
        return changed.SetItem("sha256sums.txt", ChecksumBytes(changed));
    }

    private static ImmutableSortedDictionary<string, byte[]> MutateCsvCell(
        ImmutableSortedDictionary<string, byte[]> package, string name, int row, int column, string value)
    {
        var rows = Encoding.UTF8.GetString(package[name]).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split(',')).ToArray();
        rows[row][column] = value;
        return ReplaceAndRehash(package, name,
            Encoding.UTF8.GetBytes(string.Join('\n', rows.Select(x => string.Join(',', x))) + "\n"));
    }

    private static byte[] ChecksumBytes(ImmutableSortedDictionary<string, byte[]> files) =>
        Encoding.UTF8.GetBytes(string.Join('\n', files.Select(x =>
            $"{Convert.ToHexString(SHA256.HashData(x.Value))}  {x.Key}")) + "\n");
    private static byte[] Json(object value) => JsonSerializer.SerializeToUtf8Bytes(value,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    private static ManiaChart EmptyChart(int keys, int objects = 0) => new()
    {
        KeyCount = keys, Lines = [], OriginalObjects = Enumerable.Range(0, objects)
            .Select(index => ManiaObject.Tap(index % keys, index, sequence: index)).ToArray(),
        TimingPoints = [new(0, 500)]
    };

    private static string RunGit(string root, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true };
        info.ArgumentList.Add("-C"); info.ArgumentList.Add(root);
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(error);
        return output;
    }

    private static string SyntheticProvenanceWorkerSource() =>
        "using System.Diagnostics;using System.Reflection;using System.Security.Cryptography;using System.Text.Json;"
        + "var i=Array.IndexOf(args,\"--execution-root\");var r=Path.GetFullPath(args[i+1]);"
        + "var p=new ProcessStartInfo(\"git\"){UseShellExecute=false,RedirectStandardOutput=true};"
        + "p.ArgumentList.Add(\"-C\");p.ArgumentList.Add(r);p.ArgumentList.Add(\"rev-parse\");p.ArgumentList.Add(\"HEAD\");"
        + "using var x=Process.Start(p)!;var h=x.StandardOutput.ReadToEnd().Trim();x.WaitForExit();"
        + "var a=Path.GetFullPath(Assembly.GetEntryAssembly()!.Location);var v=new{runtimeHead=h,assemblyPath=a,"
        + "assemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a))),baseDirectory=Path.GetFullPath(AppContext.BaseDirectory),"
        + "executionRoot=r,workerMarker=\"LANE0_EXECUTION_ROOT_WORKER_V1\"};File.WriteAllBytes(args[1],JsonSerializer.SerializeToUtf8Bytes(v));";

    private static string SyntheticEndToEndWorkerSource() =>
        "using System.Diagnostics;using System.Reflection;using System.Security.Cryptography;using System.Text.Json;"
        + "var i=Array.IndexOf(args,\"--execution-root\");var r=Path.GetFullPath(args[i+1]);"
        + "var p=new ProcessStartInfo(\"git\"){UseShellExecute=false,RedirectStandardOutput=true};"
        + "p.ArgumentList.Add(\"-C\");p.ArgumentList.Add(r);p.ArgumentList.Add(\"rev-parse\");p.ArgumentList.Add(\"HEAD\");"
        + "using var x=Process.Start(p)!;var h=x.StandardOutput.ReadToEnd().Trim();x.WaitForExit();"
        + "var a=Path.GetFullPath(Assembly.GetEntryAssembly()!.Location);var v=new{runtimeHead=h,assemblyPath=a,"
        + "assemblySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a))),baseDirectory=Path.GetFullPath(AppContext.BaseDirectory),"
        + "executionRoot=r,workerMarker=\"LANE0_EXECUTION_ROOT_WORKER_V1\"};"
        + "if(args[0]==\"--provenance-only\"){File.WriteAllBytes(args[1],JsonSerializer.SerializeToUtf8Bytes(v));return;}"
        + "foreach(var d in new[]{args[3],args[4]}){Directory.CreateDirectory(d);foreach(var f in Directory.EnumerateFiles(Path.Combine(r,\"worker\",\"payload\")))File.Copy(f,Path.Combine(d,Path.GetFileName(f)));}"
        + "var m=new{scientificOutcome=\"LIMITED_PARK\",integrityFailures=Array.Empty<string>(),blockedReasons=Array.Empty<string>(),rngCalls=0,inputsUnchanged=true,behaviorChanged=false,defaultChanged=false};"
        + "File.WriteAllBytes(args[2],JsonSerializer.SerializeToUtf8Bytes(new{first=m,second=m,provenance=v}));";

    private sealed class ManifestReader : ILane0IntegrationCorpusReader
    {
        private readonly Dictionary<string, string> byPath;
        internal List<string> ReadPaths { get; } = [];
        internal bool CorruptFirstRead { get; init; }
        internal ManifestReader(VerifiedFrozenC11Manifest manifest) => byPath =
            PathPlan(manifest)
                .ToDictionary(x => x.RelativePath, x => x.ChartId, StringComparer.Ordinal);
        public byte[] ReadExact(string canonicalCorpusRoot, string relativePath)
        {
            ReadPaths.Add(relativePath);
            return Encoding.UTF8.GetBytes(CorruptFirstRead && ReadPaths.Count == 1
                ? "CORRUPTED" : byPath[relativePath]);
        }
    }

    private sealed class ManifestCodec : ILane0IntegrationCorpusCodec
    {
        private readonly Dictionary<string, C11FrozenCorpusExpectation> charts;
        internal ManifestCodec(VerifiedFrozenC11Manifest manifest) => charts =
            manifest.Charts.ToDictionary(x => x.Sha256, StringComparer.Ordinal);
        public string Sha256(byte[] bytes) => Encoding.UTF8.GetString(bytes);
        public ManiaChart Parse(byte[] bytes)
        {
            var item = charts[Encoding.UTF8.GetString(bytes)];
            var one = ManiaObject.Tap(0, 0);
            return new()
            {
                KeyCount = item.KeyCount, Lines = [],
                OriginalObjects = Enumerable.Repeat(one, item.ObjectCount).ToArray(),
                TimingPoints = [new(0, 500)]
            };
        }
    }

    private sealed class MemoryAuthorityStore : ILane0IntegrationAuthorityStore
    {
        internal bool ReceiptExistsValue;
        internal bool OutputExistsValue;
        internal bool CreateCalled;
        public bool ReceiptExists => ReceiptExistsValue;
        public bool OutputOrStagingExists => OutputExistsValue;
        public void CreateDurableReceipt(byte[] bytes) { CreateCalled = true; ReceiptExistsValue = true; }
    }

    private sealed class FakeCheckoutMaterializer : ILane0IntegrationCheckoutMaterializer
    {
        public Lane0IntegrationCheckoutEvidence CreateDetachedCheckout(
            string canonicalSourceRoot, string canonicalExecutionRoot, string exactHead)
        {
            Directory.CreateDirectory(canonicalExecutionRoot);
            return new(exactHead, true, true, false, false);
        }
    }

    private sealed class BadCheckoutMaterializer : ILane0IntegrationCheckoutMaterializer
    {
        public Lane0IntegrationCheckoutEvidence CreateDetachedCheckout(
            string canonicalSourceRoot, string canonicalExecutionRoot, string exactHead) =>
            new(exactHead, false, true, true, false);
    }

    private sealed class FakeAdapter : ILane0IntegrationFrozenCorpusAdapter
    {
        private readonly VerifiedFrozenC11Manifest manifest;
        internal FakeAdapter(VerifiedFrozenC11Manifest manifest) => this.manifest = manifest;
        public Lane0IntegrationAdmittedCorpus Admit(VerifiedFrozenC11Manifest _, string root)
        {
            var plan = PathPlan(manifest);
            var charts = manifest.Charts.OrderBy(x => x.Sha256).Select(x =>
                new Lane0CorrectiveChartInput(x.Sha256, EmptyChart(x.KeyCount))).ToImmutableArray();
            return new(plan, charts, 50_836);
        }
    }

    private sealed class CountingScience : ILane0SuccessorScientificEvaluator
    {
        private readonly bool identical;
        internal int Calls { get; private set; }
        internal CountingScience(bool identical) => this.identical = identical;
        public Lane0SuccessorEvaluationPass Evaluate(ImmutableArray<Lane0CorrectiveChartInput> inputs,
            Lane0SuccessorEvaluationContext context)
        {
            Calls++;
            var value = identical ? "same" : $"pass-{Calls}";
            return new("LIMITED_PARK",
                ImmutableSortedDictionary<string, byte[]>.Empty.Add("synthetic", Encoding.UTF8.GetBytes(value)),
                [], [], 0, true, false, false);
        }
    }

    private sealed record FakeAuthorityObserver(string Head,
        Lane0IntegrationRuntimeIdentities Identities, bool Clean) : ILane0IntegrationAuthorityObserver
    {
        public string ObserveHead(string canonicalRepositoryRoot) => Head;
        public bool ObserveTrackedClean(string canonicalRepositoryRoot) => Clean;
        public Lane0IntegrationRuntimeIdentities ObserveRuntimeIdentities(string canonicalRepositoryRoot) =>
            Identities;
    }

    private sealed class FakeIsolatedRuntime : ILane0IntegrationIsolatedScientificRuntime
    {
        private readonly CountingScience science;
        private readonly string runtimeHead;
        internal FakeIsolatedRuntime(CountingScience science, string runtimeHead)
        { this.science = science; this.runtimeHead = runtimeHead; }
        public Lane0IntegrationPreparedRuntime Prepare(string root, string expectedHead)
        {
            var assembly = Path.Combine(root, "synthetic-worker.dll");
            return new(root, assembly, "SYNTHETIC", new(runtimeHead, assembly, "SYNTHETIC",
                root, root, Lane0ExecutionRootScientificRuntime.ExpectedWorkerMarker));
        }
        public Lane0IntegrationScientificRuntimeResult Execute(Lane0IntegrationPreparedRuntime prepared,
            ImmutableArray<Lane0CorrectiveChartInput> inputs, Lane0SuccessorEvaluationContext context)
        {
            var first = science.Evaluate(inputs, context);
            var second = science.Evaluate(inputs, context);
            return new(first, second, prepared.Provenance);
        }
    }

    private sealed class CountingSemanticVerifier : ILane0IntegrationDeepSemanticVerifier
    {
        internal int Calls { get; private set; }
        public void Verify(ImmutableSortedDictionary<string, byte[]> package,
            Lane0IntegrationSemanticExpectations expected) => Calls++;
    }

    private sealed class IdentityPackageFinalizer : ILane0IntegrationPackageFinalizer
    {
        internal int Calls { get; private set; }
        public ImmutableSortedDictionary<string, byte[]> Finalize(
            ImmutableSortedDictionary<string, byte[]> phase2Package,
            Lane0IntegrationRuntimeIdentities identities, bool defaultChanged)
        {
            Calls++;
            return phase2Package;
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "mania-add-notes-integration-" + Guid.NewGuid().ToString("N"));
        internal TempDirectory() => Directory.CreateDirectory(System.IO.Path.Combine(Path, "source"));
        public void Dispose()
        {
            if (!Directory.Exists(Path)) return;
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Path, true);
        }
    }
}
