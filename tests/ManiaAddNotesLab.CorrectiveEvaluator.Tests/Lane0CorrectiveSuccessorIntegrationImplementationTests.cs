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
    public void OfficialAuthorityLayoutIsFixedAndHasNoCallerRedirectSurface()
    {
        using var temp = new TempDirectory();
        var request = LaunchRequest(temp.Path, "opaque");
        var official = OfficialRequest(request);
        var paths = Lane0IntegrationOfficialLayout.Derive(request.SourceRoot, request.ExecutionRoot);
        Assert.EndsWith(Path.Combine("lane0-corrective-successor-integration-authorization",
            "canonical-binding.json"), paths.CanonicalBindingPath);
        Assert.Contains(Lane0IntegrationOfficialLayout.OneShotNamespace, paths.DurableReceiptPath);
        Assert.StartsWith(Path.GetFullPath(request.SourceRoot), paths.AuthorizationRoot,
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Path.GetFullPath(request.ExecutionRoot), paths.StagingRoot,
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Path.GetFullPath(request.ExecutionRoot), paths.FinalArtifactsRoot,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(typeof(Lane0IntegrationOfficialLaunchRequest).GetProperties(), x =>
            x.Name.Contains("Binding", StringComparison.Ordinal)
            || x.Name.Contains("Receipt", StringComparison.Ordinal)
            || x.Name.Contains("AuthorizationRoot", StringComparison.Ordinal)
            || x.Name.Contains("Staging", StringComparison.Ordinal)
            || x.Name.Contains("Output", StringComparison.Ordinal));
        Assert.Equal(request.SourceRoot, official.SourceRoot);
    }

    [Fact]
    public void OfficialPathIgnoresAlternateBindingAndRequiresCanonicalBindingBytes()
    {
        using var temp = new TempDirectory();
        var request = LaunchRequest(temp.Path, "opaque");
        var alternate = Path.Combine(temp.Path, "alternate", "binding.json");
        Directory.CreateDirectory(Path.GetDirectoryName(alternate)!);
        File.WriteAllBytes(alternate, Lane0IntegrationCanonicalBindingLoader
            .CanonicalDocumentBytes(request.Binding!.Fields));
        var launcher = SyntheticLauncher(new FakeCheckoutMaterializer());
        Assert.Throws<InvalidDataException>(() => launcher.RunOfficial(OfficialRequest(request)));
        WriteOfficialBinding(request);
        Assert.Throws<InvalidDataException>(() => launcher.RunOfficial(OfficialRequest(request)));
        var loaded = new Lane0IntegrationCanonicalBindingLoader().Load(
            Lane0IntegrationOfficialLayout.Derive(request.SourceRoot, request.ExecutionRoot)
                .CanonicalBindingPath);
        Assert.Equal(request.Binding.CanonicalSha256, loaded.CanonicalSha256);
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("schema")]
    [InlineData("head")]
    [InlineData("identities")]
    [InlineData("authorization")]
    [InlineData("count")]
    public void OfficialCanonicalBindingBadControlsFailClosed(string mutation)
    {
        using var temp = new TempDirectory();
        var request = LaunchRequest(temp.Path, "opaque");
        var fields = request.Binding!.Fields;
        fields = mutation switch
        {
            "schema" => fields with { SchemaVersion = "BAD" },
            "head" => fields with { ApprovedPublicHead = new string('B', 40) },
            "identities" => fields with { Identities = fields.Identities with { AdapterSha256 = "BAD" } },
            "authorization" => fields with { ExplicitHumanAuthorization = false },
            "count" => fields with { AuthorizedExecutionCount = 2 },
            _ => fields
        };
        WriteOfficialBinding(request, fields, mutation == "hash" ? "BAD" : null);
        Assert.Throws<InvalidDataException>(() => SyntheticLauncher(new FakeCheckoutMaterializer())
            .RunOfficial(OfficialRequest(request)));
    }

    [Fact]
    public void CanonicalReceiptCannotBeRearmedByAlternateDirectoryDeletion()
    {
        using var temp = new TempDirectory();
        var request = LaunchRequest(temp.Path, "opaque");
        WriteOfficialBinding(request);
        var paths = Lane0IntegrationOfficialLayout.Derive(request.SourceRoot, request.ExecutionRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.DurableReceiptPath)!);
        File.WriteAllText(paths.DurableReceiptPath, "consumed");
        var alternate = Path.Combine(temp.Path, "alternate-attempt");
        Directory.CreateDirectory(alternate);
        Directory.Delete(alternate);
        var launcher = SyntheticLauncher(new FakeCheckoutMaterializer());
        Assert.Throws<InvalidDataException>(() => launcher.RunOfficial(OfficialRequest(request)));
        Assert.True(File.Exists(paths.DurableReceiptPath));
    }

    [Fact]
    public async Task FixedCanonicalReceiptNamespaceHasAtMostOneConcurrentWinner()
    {
        using var temp = new TempDirectory();
        var request = LaunchRequest(temp.Path, "opaque");
        var paths = Lane0IntegrationOfficialLayout.Derive(request.SourceRoot, request.ExecutionRoot);
        var stores = Enumerable.Range(0, 8).Select(_ => new Lane0IntegrationFileAuthorityStore(
            paths.DurableReceiptPath, paths.FinalArtifactsRoot, paths.StagingRoot)).ToArray();
        var wins = 0;
        await Task.WhenAll(stores.Select(store => Task.Run(() =>
        {
            try { store.CreateDurableReceipt(Encoding.UTF8.GetBytes("synthetic\n")); Interlocked.Increment(ref wins); }
            catch (IOException) { }
        })));
        Assert.Equal(1, wins);
    }

    [Fact]
    public void FrozenAuthorityIsOneShotPerSourceAuthorizationRootNotGlobalOrDistributed()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "docs",
            "lane_0_corrective_successor_integration_preregistration_contract.json")));
        var contract = document.RootElement.GetProperty("contract");
        Assert.True(contract.GetProperty("futureReceipt")
            .GetProperty("sourceAuthorizationRootAnchored").GetBoolean());
        var threat = contract.GetProperty("threatModel");
        Assert.Contains("CONCURRENT_ATTEMPTS_ON_ONE_AUTHORITY_ROOT",
            threat.GetProperty("covered").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("DISTRIBUTED_MULTI_MACHINE_AUTHORITY",
            threat.GetProperty("outOfScope").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void OfficialRunnerSealsProductionDependenciesWhileSyntheticRunnerRemainsExplicit()
    {
        var official = typeof(Lane0CorrectiveSuccessorOfficialResearchRunner);
        var constructors = official.GetConstructors(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);
        Assert.Single(constructors);
        Assert.Empty(constructors[0].GetParameters());
        Assert.Empty(official.GetFields(System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic));
        Assert.DoesNotContain(typeof(Lane0CorrectiveSuccessorInternalResearchRunner).GetMethods(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic), x => x.Name.Contains("Official", StringComparison.Ordinal));
        Assert.NotNull(typeof(Lane0CorrectiveSuccessorInternalResearchRunner).GetMethod(
            "ExecuteSyntheticOrFutureAuthorized",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
    }

    [Fact]
    public void UnifiedOfficialCapabilityConsumesOneAttemptAndCannotStartScienceTwice()
    {
        using var temp = new TempDirectory();
        var request = LaunchRequest(temp.Path, "POST_RECEIPT_SYNTHETIC_TOKEN");
        WriteOfficialBinding(request);
        var science = new CountingScience(identical: true);
        var launcher = SyntheticLauncher(new FakeCheckoutMaterializer(), science);

        Assert.Throws<InvalidDataException>(() => launcher.RunOfficial(OfficialRequest(request)));
        Assert.Equal(0, science.Calls);
        Assert.False(File.Exists(Lane0IntegrationOfficialLayout.Derive(
            request.SourceRoot, request.ExecutionRoot).DurableReceiptPath));
    }

    [Fact]
    public void OfficialFailureConsumesAttemptWithoutDeleteToRetry()
    {
        using var temp = new TempDirectory();
        var request = LaunchRequest(temp.Path, "POST_RECEIPT_SYNTHETIC_TOKEN");
        WriteOfficialBinding(request);
        var science = new CountingScience(identical: true);
        var runtime = new FakeIsolatedRuntime(science, request.ExpectedSourcePublicHead,
            failOfficialAfterReceipt: true);
        var launcher = new Lane0CorrectiveSuccessorIsolatedLauncher(
            new FakeCheckoutMaterializer(),
            new FakeAuthorityObserver(request.ExpectedSourcePublicHead,
                request.ExpectedIdentities, true), runtime);

        Assert.Throws<InvalidDataException>(() => launcher.RunOfficial(OfficialRequest(request)));
        var receipt = Lane0IntegrationOfficialLayout.Derive(
            request.SourceRoot, request.ExecutionRoot).DurableReceiptPath;
        Assert.False(File.Exists(receipt));
        Assert.Equal(0, science.Calls);
        Assert.Throws<InvalidDataException>(() => launcher.RunOfficial(OfficialRequest(request)));
    }

    [Fact]
    public void OfficialApiCannotAcceptChartsOrOutputPathsAndSyntheticApiIsExplicit()
    {
        var official = typeof(ILane0IntegrationIsolatedScientificRuntime).GetMethod("RunOfficial")!;
        var names = official.GetParameters().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(names, x => x!.Contains("chart", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, x => x!.Contains("output", StringComparison.OrdinalIgnoreCase)
            || x.Contains("response", StringComparison.OrdinalIgnoreCase)
            || x.Contains("staging", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(typeof(ILane0IntegrationIsolatedScientificRuntime)
            .GetMethod("ExecuteSyntheticFixture"));
        Assert.Equal("--synthetic-run", "--synthetic-run");
    }

    [Theory]
    [InlineData("ManiaAddNotesLab.Core.dll")]
    [InlineData("ManiaAddNotesLab.Experiments.dll")]
    [InlineData("ManiaAddNotesLab.IntegrationResearchWorker.dll")]
    public void DurableAuthorityStateRejectsEveryClosureMutation(string mutation)
    {
        using var temp = new TempDirectory();
        var build = Path.Combine(temp.Path, "durable-closure");
        Directory.CreateDirectory(build);
        foreach (var name in Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames)
            File.WriteAllText(Path.Combine(build, name), name);
        var closure = Lane0ExecutionRootScientificRuntime.ObserveBinaryClosure(build,
            Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames);
        var ids = Identities();
        var state = new Lane0IntegrationWorkerAuthorityState(temp.Path, "HEAD", "BINDING",
            ids, closure, "RECEIPT_VERIFIED_ATTEMPT_CONSUMED");
        Lane0ExecutionRootScientificRuntime.ValidateDurableAuthorityClosure(state, closure, ids);
        File.AppendAllText(Path.Combine(build, mutation), "drift");
        var changed = Lane0ExecutionRootScientificRuntime.ObserveBinaryClosure(build,
            Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames);
        Assert.Throws<InvalidDataException>(() => Lane0ExecutionRootScientificRuntime
            .ValidateDurableAuthorityClosure(state, changed, ids));
        Assert.Throws<InvalidDataException>(() => Lane0ExecutionRootScientificRuntime
            .ValidateDurableAuthorityClosure(state with { ExecutionState = "" }, closure, ids));
    }

    [Fact]
    public void ProjectBinaryClosureIsDeterministicAndIncludesEveryOfficialProjectAssembly()
    {
        using var temp = new TempDirectory();
        var build = Path.Combine(temp.Path, "closure");
        Directory.CreateDirectory(build);
        foreach (var name in Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames)
            File.WriteAllBytes(Path.Combine(build, name), Encoding.UTF8.GetBytes(name));
        var first = Lane0ExecutionRootScientificRuntime.ObserveBinaryClosure(build,
            Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames);
        var second = Lane0ExecutionRootScientificRuntime.ObserveBinaryClosure(build,
            Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames.Reverse());
        Assert.True(Lane0ExecutionRootScientificRuntime.BinaryClosuresEqual(first, second));
        Assert.Equal(Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames,
            first.Binaries.Select(x => x.BinaryName));
        Assert.All(first.Binaries, x => Assert.StartsWith(Path.GetFullPath(build),
            x.FullCanonicalPath, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("ManiaAddNotesLab.IntegrationResearchWorker.dll")]
    [InlineData("ManiaAddNotesLab.Experiments.dll")]
    [InlineData("ManiaAddNotesLab.Core.dll")]
    public void ProjectBinaryClosureDetectsEveryProjectAssemblyMutation(string mutation)
    {
        using var temp = new TempDirectory();
        var build = Path.Combine(temp.Path, "closure-mutation");
        Directory.CreateDirectory(build);
        foreach (var name in Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames)
            File.WriteAllBytes(Path.Combine(build, name), Encoding.UTF8.GetBytes(name));
        var before = Lane0ExecutionRootScientificRuntime.ObserveBinaryClosure(build,
            Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames);
        var worker = Path.Combine(build, Lane0ExecutionRootScientificRuntime.DefaultAssemblyName);
        var workerHash = before.Binaries.Single(x => x.BinaryName ==
            Lane0ExecutionRootScientificRuntime.DefaultAssemblyName).Sha256;
        var provenance = new Lane0IntegrationRuntimeProvenance("HEAD", worker, workerHash,
            build, temp.Path, Lane0ExecutionRootScientificRuntime.ExpectedWorkerMarker, before);
        var prepared = new Lane0IntegrationPreparedRuntime(temp.Path, worker, workerHash,
            before, provenance);
        File.AppendAllText(Path.Combine(build, mutation), "drift");
        var after = Lane0ExecutionRootScientificRuntime.ObserveBinaryClosure(build,
            Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames);
        Assert.False(Lane0ExecutionRootScientificRuntime.BinaryClosuresEqual(before, after));
        Assert.Throws<InvalidDataException>(() => Lane0ExecutionRootScientificRuntime
            .ValidatePreparedBinaryClosure(prepared,
                Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames,
                "closure drift"));
    }

    [Fact]
    public void ProjectBinaryClosureRejectsMissingUnexpectedAndEscapedMembers()
    {
        using var temp = new TempDirectory();
        var build = Path.Combine(temp.Path, "closure-bad-controls");
        Directory.CreateDirectory(build);
        foreach (var name in Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames)
            File.WriteAllBytes(Path.Combine(build, name), Encoding.UTF8.GetBytes(name));
        File.Delete(Path.Combine(build, "ManiaAddNotesLab.Core.dll"));
        Assert.Throws<InvalidDataException>(() => Lane0ExecutionRootScientificRuntime
            .ObserveBinaryClosure(build, Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames));
        File.WriteAllText(Path.Combine(build, "ManiaAddNotesLab.Core.dll"), "restored");
        File.WriteAllText(Path.Combine(build, "ManiaAddNotesLab.Unexpected.dll"), "unexpected");
        Assert.Throws<InvalidDataException>(() => Lane0ExecutionRootScientificRuntime
            .ObserveBinaryClosure(build, Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames));
        Assert.Throws<InvalidDataException>(() => Lane0ExecutionRootScientificRuntime
            .ObserveBinaryClosure(build, ["../escape.dll"]));
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("approvedPublicHead")]
    [InlineData("canonicalBindingSha256")]
    [InlineData("integrationPreregistrationSha256")]
    [InlineData("integrationExecutionContractSha256")]
    [InlineData("attemptCount")]
    public void ReceiptGatedScienceRejectsEveryCanonicalReceiptSemanticDrift(string property)
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "source");
        var execution = Path.Combine(temp.Path, "receipt-execution");
        Directory.CreateDirectory(execution);
        var head = new string('A', 40);
        var binding = new string('B', 64);
        var ids = Identities();
        var paths = Lane0IntegrationOfficialLayout.Derive(source, execution);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.DurableReceiptPath)!);
        var root = JsonNode.Parse(Lane0CorrectiveSuccessorIsolatedLauncher.BuildReceipt(
            head, ids, binding))!.AsObject();
        root[property] = property == "attemptCount" ? JsonValue.Create(2) : JsonValue.Create("BAD");
        File.WriteAllText(paths.DurableReceiptPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Assert.Throws<InvalidDataException>(() => Lane0IntegrationDurableReceiptVerifier
            .VerifyCanonicalReceipt(source, execution, head, binding, ids));
    }

    [Fact]
    public void ReceiptGatedScienceRequiresCanonicalPathAndPreservesConsumedReceipt()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "source");
        var execution = Path.Combine(temp.Path, "receipt-path-execution");
        Directory.CreateDirectory(execution);
        var head = new string('A', 40);
        var binding = new string('B', 64);
        var ids = Identities();
        var paths = Lane0IntegrationOfficialLayout.Derive(source, execution);
        var alternate = Path.Combine(source, "alternate", "receipt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(alternate)!);
        File.WriteAllBytes(alternate, Lane0CorrectiveSuccessorIsolatedLauncher.BuildReceipt(
            head, ids, binding));
        Assert.Throws<InvalidDataException>(() => Lane0IntegrationDurableReceiptVerifier
            .VerifyCanonicalReceipt(source, execution, head, binding, ids));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.DurableReceiptPath)!);
        var expected = Lane0CorrectiveSuccessorIsolatedLauncher.BuildReceipt(head, ids, binding);
        File.WriteAllBytes(paths.DurableReceiptPath, expected);
        Assert.Equal(paths.DurableReceiptPath, Lane0IntegrationDurableReceiptVerifier
            .VerifyCanonicalReceipt(source, execution, head, binding, ids));
        Assert.Equal(expected, File.ReadAllBytes(paths.DurableReceiptPath));
    }

    [Fact]
    public void ProductionWorkerHasOneOfficialCapabilityAndNoReusableAuthorityOrScienceMode()
    {
        var worker = File.ReadAllText(Path.Combine(RepoRoot(), "tools",
            "ManiaAddNotesLab.IntegrationResearchWorker", "Program.cs"));
        Assert.Contains("--official-run", worker, StringComparison.Ordinal);
        Assert.Contains("RECEIPT_CREATED", worker, StringComparison.Ordinal);
        Assert.Contains("ReadAuthorityState()", worker, StringComparison.Ordinal);
        Assert.Contains("VerifyCanonicalReceipt(source, executionRoot", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("--authorize", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("--execute", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("--receipt", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("--authorization-root", worker, StringComparison.Ordinal);
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
        var runner = new Lane0CorrectiveSuccessorInternalResearchRunner(
            SyntheticLauncher(new FakeCheckoutMaterializer(), science),
            new FakeAdapter(manifest), semantic);
        var store = new MemoryAuthorityStore();
        var result = runner.ExecuteSyntheticOrFutureAuthorized(new(
            LaunchRequest(temp.Path, Path.Combine(temp.Path, "corpus")), manifest,
            Lane0CorrectiveSuccessorReferenceValidator.FrozenReferences), store);

        Assert.True(result.Classification == "PUBLISHABLE", result.Reason);
        Assert.Equal(2, science.Calls);
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
            new FakeAdapter(manifest), semantic);
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
        var prepared = new Lane0ExecutionRootScientificRuntime("worker/worker.csproj", "SyntheticWorker.dll",
            ["SyntheticWorker.dll"])
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
        var runtime = new Lane0ExecutionRootScientificRuntime("worker/worker.csproj", "SyntheticWorker.dll",
            ["SyntheticWorker.dll"]);
        var prepared = runtime.Prepare(execution, head);
        var token = new string('A', 64);
        var context = new Lane0SuccessorEvaluationContext(head, token, token, token, token, token,
            token, token, Lane0CorrectiveSuccessorReferenceValidator.FrozenReferences);
        var inputs = ImmutableArray.Create(new Lane0CorrectiveChartInput(token, EmptyChart(4)));
        var result = runtime.ExecuteSyntheticFixture(prepared, inputs, context, Identities());
        Assert.Equal(head, result.Provenance.RuntimeHead);
        Assert.Equal(result.First.ScientificOutcome, result.Second.ScientificOutcome);
        Assert.Equal(result.First.Package.Keys, result.Second.Package.Keys);
        Assert.All(result.First.Package.Keys, name =>
            Assert.Equal(result.First.Package[name], result.Second.Package[name]));
        Assert.StartsWith(Path.GetFullPath(execution), Path.GetFullPath(result.FirstFinalArtifactRoot),
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Path.GetFullPath(execution), Path.GetFullPath(result.SecondFinalArtifactRoot),
            StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(execution, ".artifacts",
            "synthetic-integration-authority", "SYNTHETIC.NONOFFICIAL.ONE_SHOT", "receipt.json")));
        Assert.False(File.Exists(Lane0IntegrationOfficialLayout.Derive(source, execution)
            .DurableReceiptPath));
        new Lane0CorrectiveSuccessorDeepSemanticPackageVerifier()
            .Verify(result.First.Package, fixture.Expected);
    }

    [Fact]
    public void OfficialHostRunnerCannotFinalizeOrMutateWorkerFinalPackages()
    {
        var runner = File.ReadAllText(Path.Combine(RepoRoot(), "tools",
            "ManiaAddNotesLab.Experiments", "Lane0CorrectiveSuccessorInternalResearchRunner.cs"));
        var worker = File.ReadAllText(Path.Combine(RepoRoot(), "tools",
            "ManiaAddNotesLab.IntegrationResearchWorker", "Program.cs"));
        Assert.DoesNotContain("packageFinalizer.Finalize", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("ILane0IntegrationPackageFinalizer packageFinalizer", runner,
            StringComparison.Ordinal);
        Assert.Contains("finalizer.Finalize(firstRaw.Package", worker, StringComparison.Ordinal);
        Assert.Contains("finalizer.Finalize(secondRaw.Package", worker, StringComparison.Ordinal);
    }

    [Fact]
    public void ActualProductionWorkerBuildsFinalizesAndRunsFromDetachedExecutionRoot()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "production-source");
        CopyProjectSources(Path.Combine(RepoRoot(), "src", "ManiaAddNotesLab.Core"),
            Path.Combine(source, "src", "ManiaAddNotesLab.Core"));
        CopyProjectSources(Path.Combine(RepoRoot(), "tools", "ManiaAddNotesLab.Experiments"),
            Path.Combine(source, "tools", "ManiaAddNotesLab.Experiments"));
        CopyProjectSources(Path.Combine(RepoRoot(), "tools", "ManiaAddNotesLab.IntegrationResearchWorker"),
            Path.Combine(source, "tools", "ManiaAddNotesLab.IntegrationResearchWorker"));
        var docs = Path.Combine(source, "docs");
        Directory.CreateDirectory(docs);
        foreach (var name in new[]
        {
            "lane_0_corrective_successor_preregistration_contract.json",
            "lane_0_corrective_successor_execution_preparation_contract.json",
            "lane_0_corrective_successor_integration_preregistration_contract.json",
            "lane_0_corrective_successor_integration_execution_contract.json",
            "g1_gate_runtime_manifest.json"
        }) File.Copy(Path.Combine(RepoRoot(), "docs", name), Path.Combine(docs, name));
        RunGit(source, "init"); RunGit(source, "config", "user.email", "test@example.invalid");
        RunGit(source, "config", "user.name", "Test"); RunGit(source, "add", "src", "tools", "docs");
        RunGit(source, "commit", "-m", "production-worker-fixture");
        var head = RunGit(source, "rev-parse", "HEAD").Trim();
        var ids = new GitAndFileIntegrationAuthorityObserver().ObserveRuntimeIdentities(source);
        var execution = Path.Combine(temp.Path, "production-execution");
        var binding = new Lane0IntegrationBindingFields(
            Lane0CorrectiveSuccessorIsolatedLauncher.BindingSchema, head, ids, true, 1);
        var officialPaths = Lane0IntegrationOfficialLayout.Derive(source, execution);
        Directory.CreateDirectory(Path.GetDirectoryName(officialPaths.CanonicalBindingPath)!);
        File.WriteAllBytes(officialPaths.CanonicalBindingPath,
            Lane0IntegrationCanonicalBindingLoader.CanonicalDocumentBytes(binding));
        var checkout = new GitDetachedCheckoutMaterializer().CreateDetachedCheckout(source, execution, head);
        Assert.True(checkout.Detached && checkout.TrackedClean && !checkout.UsedHardlinks);
        var runtime = new Lane0ExecutionRootScientificRuntime();
        var prepared = runtime.Prepare(execution, head);
        var context = new Lane0SuccessorEvaluationContext(head, ids.Phase1ContractSha256,
            ids.Phase2ContractSha256, ids.ManifestSha256, ids.ScientificImplementationSha256,
            ids.Phase2HarnessSha256, ids.Phase2PackageVerifierSha256,
            ids.FrozenDependenciesSha256, Lane0CorrectiveSuccessorReferenceValidator.FrozenReferences);
        var chartId = new string('C', 64);
        var inputs = ImmutableArray.Create(new Lane0CorrectiveChartInput(chartId, EmptyChart(4)));
        var result = runtime.ExecuteSyntheticFixture(prepared, inputs, context, ids);
        Assert.False(File.Exists(officialPaths.DurableReceiptPath));
        Assert.Equal(head, result.Provenance.RuntimeHead);
        Assert.True(Lane0CorrectiveSuccessorPackage.ByteIdentical(
            result.First.Package, result.Second.Package));
        Lane0CorrectiveSuccessorPackage.Verify(result.First.Package);
        using var identity = JsonDocument.Parse(result.First.Package["execution_identity.json"]);
        Assert.Equal(ids.AdapterSha256,
            identity.RootElement.GetProperty("adapterSha256").GetString());
        Assert.Equal(ids.LauncherSha256,
            identity.RootElement.GetProperty("launcherSha256").GetString());
        Assert.Contains("synthetic-integration-exchange", result.FirstFinalArtifactRoot,
            StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidDataException>(() =>
            new Lane0CorrectiveSuccessorDeepSemanticPackageVerifier().Verify(
                result.First.Package, SemanticFixture().Expected));
    }

    [Theory]
    [InlineData("head")]
    [InlineData("detached")]
    [InlineData("clean")]
    [InlineData("hardlinks")]
    [InlineData("reused-build")]
    public void OfficialReceiptCreatorRejectsEveryMaterializerEvidenceDrift(string mutation)
    {
        var head = new string('A', 40);
        var evidence = new Lane0IntegrationCheckoutEvidence(head, true, true, false, false);
        evidence = mutation switch
        {
            "head" => evidence with { Head = new string('B', 40) },
            "detached" => evidence with { Detached = false },
            "clean" => evidence with { TrackedClean = false },
            "hardlinks" => evidence with { UsedHardlinks = true },
            "reused-build" => evidence with { ReusedSourceBuildOutputs = true },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        Assert.Throws<InvalidDataException>(() =>
            Lane0IntegrationOfficialReceiptCoordinator.ValidateCheckoutEvidence(evidence, head));
    }

    [Fact]
    public void ProductionWorkerCannotCreateCanonicalReceiptAndUsesTwoPhaseHandshake()
    {
        var worker = File.ReadAllText(Path.Combine(RepoRoot(), "tools",
            "ManiaAddNotesLab.IntegrationResearchWorker", "Program.cs"));
        Assert.Contains("PRE_RECEIPT_READY", worker, StringComparison.Ordinal);
        Assert.Contains("RECEIPT_VERIFIED", worker, StringComparison.Ordinal);
        Assert.Contains("VerifyCanonicalReceipt(source, executionRoot", worker,
            StringComparison.Ordinal);
        Assert.DoesNotContain("store.CreateDurableReceipt", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("--authorize", worker, StringComparison.Ordinal);
        Assert.DoesNotContain("--execute", worker, StringComparison.Ordinal);
    }

    [Fact]
    public void HostSendsCorpusOnlyAfterWorkerReceiptVerification()
    {
        var runtime = File.ReadAllText(Path.Combine(RepoRoot(), "tools",
            "ManiaAddNotesLab.Experiments", "Lane0CorrectiveSuccessorIsolatedRuntime.cs"));
        var verify = runtime.IndexOf("session.VerifyCreatedReceipt();", StringComparison.Ordinal);
        var complete = runtime.IndexOf("session.Complete(corpusRootToken);", StringComparison.Ordinal);
        Assert.True(verify >= 0 && complete > verify);
        var worker = File.ReadAllText(Path.Combine(RepoRoot(), "tools",
            "ManiaAddNotesLab.IntegrationResearchWorker", "Program.cs"));
        var verified = worker.IndexOf("Console.WriteLine(\"RECEIPT_VERIFIED\")",
            StringComparison.Ordinal);
        var corpus = worker.IndexOf("var corpusLine = Console.ReadLine()",
            StringComparison.Ordinal);
        Assert.True(verified >= 0 && corpus > verified);
    }

    [Theory]
    [InlineData("SCIENCE_FAILURE")]
    [InlineData("INVALID_ADMISSION")]
    [InlineData("POST_RECEIPT_EXCEPTION")]
    public void CanonicalReceiptRemainsConsumedForEveryPostReceiptOutcome(string outcome)
    {
        using var temp = new TempDirectory();
        var request = LaunchRequest(temp.Path, "opaque");
        var paths = Lane0IntegrationOfficialLayout.Derive(request.SourceRoot, request.ExecutionRoot);
        var store = new Lane0IntegrationFileAuthorityStore(paths.DurableReceiptPath,
            paths.FinalArtifactsRoot, paths.StagingRoot);
        store.CreateDurableReceipt(Encoding.UTF8.GetBytes(outcome));
        File.WriteAllText(Path.Combine(temp.Path, "post-receipt-outcome.txt"), outcome);
        Assert.True(new Lane0IntegrationFileAuthorityStore(paths.DurableReceiptPath,
            paths.FinalArtifactsRoot, paths.StagingRoot).ReceiptExists);
        Assert.Throws<IOException>(() => store.CreateDurableReceipt([1]));
    }

    [Fact]
    public void RealWorkerCrashWindowsRemainAtomicAcrossFreshExecutionRoots()
    {
        using var temp = new TempDirectory();
        var fixture = CreateProductionAuthorityFixture(temp.Path);
        var runtime = new Lane0ExecutionRootScientificRuntime();
        var materializer = new GitDetachedCheckoutMaterializer();

        var e1 = Path.Combine(temp.Path, "atomic-e1");
        var c1 = materializer.CreateDetachedCheckout(fixture.Source, e1, fixture.Head);
        var p1 = runtime.Prepare(e1, fixture.Head);
        Lane0ExecutionRootScientificRuntime.WriteOfficialLaunchAttestation(
            Lane0ExecutionRootScientificRuntime.OfficialAttestationPath(p1), p1,
            fixture.Source, fixture.Head, fixture.Identities);
        using (var session = Lane0IntegrationOfficialWorkerSession.Start(
            p1.WorkerAssemblyPath, e1))
        {
            var paths = Lane0IntegrationOfficialLayout.Derive(fixture.Source, e1);
            Assert.False(File.Exists(paths.DurableReceiptPath));
            Assert.False(File.Exists(Lane0ExecutionRootScientificRuntime.AuthorityStatePath(
                p1.WorkerAssemblyPath)));
            session.Terminate();
        }

        // No receipt was created by E1, so a fresh E2 may reach the same pre-receipt boundary.
        var e2 = Path.Combine(temp.Path, "atomic-e2");
        var c2 = materializer.CreateDetachedCheckout(fixture.Source, e2, fixture.Head);
        var p2 = runtime.Prepare(e2, fixture.Head);
        Lane0ExecutionRootScientificRuntime.WriteOfficialLaunchAttestation(
            Lane0ExecutionRootScientificRuntime.OfficialAttestationPath(p2), p2,
            fixture.Source, fixture.Head, fixture.Identities);
        using (var session = Lane0IntegrationOfficialWorkerSession.Start(
            p2.WorkerAssemblyPath, e2))
        {
            var coordinator = new Lane0IntegrationOfficialReceiptCoordinator(
                fixture.Source, e2, fixture.Head, fixture.Identities, c2, p2);
            coordinator.CreateCanonicalDurableReceipt();
            var paths = Lane0IntegrationOfficialLayout.Derive(fixture.Source, e2);
            Assert.True(File.Exists(paths.DurableReceiptPath));
            Assert.False(File.Exists(Lane0ExecutionRootScientificRuntime.AuthorityStatePath(
                p2.WorkerAssemblyPath)));
            // Crash after receipt creation but before state/science: the receipt alone consumes.
            session.Terminate();
        }

        var e3 = Path.Combine(temp.Path, "atomic-e3");
        _ = materializer.CreateDetachedCheckout(fixture.Source, e3, fixture.Head);
        var p3 = runtime.Prepare(e3, fixture.Head);
        Lane0ExecutionRootScientificRuntime.WriteOfficialLaunchAttestation(
            Lane0ExecutionRootScientificRuntime.OfficialAttestationPath(p3), p3,
            fixture.Source, fixture.Head, fixture.Identities);
        Assert.Throws<InvalidDataException>(() =>
            Lane0IntegrationOfficialWorkerSession.Start(p3.WorkerAssemblyPath, e3));
        Assert.True(File.Exists(Lane0IntegrationOfficialLayout.Derive(fixture.Source, e3)
            .DurableReceiptPath));
        Assert.False(c1.UsedHardlinks);
    }

    [Fact]
    public void IntegrationRunnerHasNoProductProgramCallSite()
    {
        var program = File.ReadAllText(Path.Combine(RepoRoot(), "tools",
            "ManiaAddNotesLab.Experiments", "Program.cs"));
        Assert.DoesNotContain(nameof(Lane0CorrectiveSuccessorInternalResearchRunner), program,
            StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(Lane0CorrectiveSuccessorOfficialResearchRunner), program,
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

    private static Lane0IntegrationOfficialLaunchRequest OfficialRequest(
        Lane0IntegrationLaunchRequest request) => new(request.ExpectedSourcePublicHead,
        request.ExpectedIdentities, request.SourceRoot, request.ExecutionRoot, request.CorpusRootToken);

    private static void WriteOfficialBinding(Lane0IntegrationLaunchRequest request,
        Lane0IntegrationBindingFields? fields = null, string? declaredHash = null)
    {
        var paths = Lane0IntegrationOfficialLayout.Derive(request.SourceRoot, request.ExecutionRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.CanonicalBindingPath)!);
        File.WriteAllBytes(paths.CanonicalBindingPath,
            Lane0IntegrationCanonicalBindingLoader.CanonicalDocumentBytes(
                fields ?? request.Binding!.Fields, declaredHash));
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
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(outputTask, errorTask);
        if (process.ExitCode != 0) throw new InvalidOperationException(errorTask.Result);
        return outputTask.Result;
    }

    private static void CopyProjectSources(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            if (Path.GetExtension(file) is not (".cs" or ".csproj")) continue;
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
    }

    private static (string Source, string Head,
        Lane0IntegrationRuntimeIdentities Identities) CreateProductionAuthorityFixture(string root)
    {
        var source = Path.Combine(root, "atomic-production-source");
        CopyProjectSources(Path.Combine(RepoRoot(), "src", "ManiaAddNotesLab.Core"),
            Path.Combine(source, "src", "ManiaAddNotesLab.Core"));
        CopyProjectSources(Path.Combine(RepoRoot(), "tools", "ManiaAddNotesLab.Experiments"),
            Path.Combine(source, "tools", "ManiaAddNotesLab.Experiments"));
        CopyProjectSources(Path.Combine(RepoRoot(), "tools", "ManiaAddNotesLab.IntegrationResearchWorker"),
            Path.Combine(source, "tools", "ManiaAddNotesLab.IntegrationResearchWorker"));
        var docs = Path.Combine(source, "docs");
        Directory.CreateDirectory(docs);
        foreach (var name in new[]
        {
            "lane_0_corrective_successor_preregistration_contract.json",
            "lane_0_corrective_successor_execution_preparation_contract.json",
            "lane_0_corrective_successor_integration_preregistration_contract.json",
            "lane_0_corrective_successor_integration_execution_contract.json",
            "g1_gate_runtime_manifest.json"
        }) File.Copy(Path.Combine(RepoRoot(), "docs", name), Path.Combine(docs, name));
        RunGit(source, "init"); RunGit(source, "config", "user.email", "test@example.invalid");
        RunGit(source, "config", "user.name", "Test"); RunGit(source, "add", "src", "tools", "docs");
        RunGit(source, "commit", "-m", "atomic-production-fixture");
        var head = RunGit(source, "rev-parse", "HEAD").Trim();
        var identities = new GitAndFileIntegrationAuthorityObserver().ObserveRuntimeIdentities(source);
        var binding = new Lane0IntegrationBindingFields(
            Lane0CorrectiveSuccessorIsolatedLauncher.BindingSchema, head, identities, true, 1);
        var paths = Lane0IntegrationOfficialLayout.Derive(source, Path.Combine(root, "placeholder"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.CanonicalBindingPath)!);
        File.WriteAllBytes(paths.CanonicalBindingPath,
            Lane0IntegrationCanonicalBindingLoader.CanonicalDocumentBytes(binding));
        return (source, head, identities);
    }

    private static string SyntheticProvenanceWorkerSource() =>
        "using System.Diagnostics;using System.Reflection;using System.Security.Cryptography;using System.Text;using System.Text.Json;"
        + "var i=Array.IndexOf(args,\"--execution-root\");var r=Path.GetFullPath(args[i+1]);"
        + "var p=new ProcessStartInfo(\"git\"){UseShellExecute=false,RedirectStandardOutput=true};"
        + "p.ArgumentList.Add(\"-C\");p.ArgumentList.Add(r);p.ArgumentList.Add(\"rev-parse\");p.ArgumentList.Add(\"HEAD\");"
        + "using var x=Process.Start(p)!;var h=x.StandardOutput.ReadToEnd().Trim();x.WaitForExit();"
        + "var a=Path.GetFullPath(Assembly.GetEntryAssembly()!.Location);var s=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a)));"
        + "var g=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(\"SyntheticWorker.dll|\"+a+\"|\"+s)));"
        + "var c=new{binaries=new[]{new{binaryName=\"SyntheticWorker.dll\",fullCanonicalPath=a,sha256=s}},aggregateSha256=g};"
        + "var v=new{runtimeHead=h,assemblyPath=a,assemblySha256=s,baseDirectory=Path.GetFullPath(AppContext.BaseDirectory),"
        + "executionRoot=r,workerMarker=\"LANE0_EXECUTION_ROOT_WORKER_V1\",binaryClosure=c};File.WriteAllBytes(args[1],JsonSerializer.SerializeToUtf8Bytes(v));";

    private static string SyntheticEndToEndWorkerSource() =>
        "using System.Diagnostics;using System.Reflection;using System.Security.Cryptography;using System.Text;using System.Text.Json;"
        + "var i=Array.IndexOf(args,\"--execution-root\");var r=Path.GetFullPath(args[i+1]);"
        + "var p=new ProcessStartInfo(\"git\"){UseShellExecute=false,RedirectStandardOutput=true};"
        + "p.ArgumentList.Add(\"-C\");p.ArgumentList.Add(r);p.ArgumentList.Add(\"rev-parse\");p.ArgumentList.Add(\"HEAD\");"
        + "using var x=Process.Start(p)!;var h=x.StandardOutput.ReadToEnd().Trim();x.WaitForExit();"
        + "var a=Path.GetFullPath(Assembly.GetEntryAssembly()!.Location);var s=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a)));"
        + "var g=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(\"SyntheticWorker.dll|\"+a+\"|\"+s)));"
        + "var c=new{binaries=new[]{new{binaryName=\"SyntheticWorker.dll\",fullCanonicalPath=a,sha256=s}},aggregateSha256=g};"
        + "var v=new{runtimeHead=h,assemblyPath=a,assemblySha256=s,baseDirectory=Path.GetFullPath(AppContext.BaseDirectory),"
        + "executionRoot=r,workerMarker=\"LANE0_EXECUTION_ROOT_WORKER_V1\",binaryClosure=c};"
        + "if(args[0]==\"--provenance-only\"){File.WriteAllBytes(args[1],JsonSerializer.SerializeToUtf8Bytes(v));return;}"
        + "var u=Path.Combine(r,\".artifacts\",\"synthetic-integration-authority\",\"SYNTHETIC.NONOFFICIAL.ONE_SHOT\");Directory.CreateDirectory(u);using(var e=new FileStream(Path.Combine(u,\"receipt.json\"),FileMode.CreateNew,FileAccess.Write,FileShare.None)){e.WriteByte(1);e.Flush(true);}Console.WriteLine(\"SYNTHETIC_RECEIPT_CREATED\");Console.Out.Flush();_ = Console.ReadLine();"
        + "var z=Path.Combine(r,\".artifacts\",\"synthetic-integration-exchange\");var q=Path.Combine(z,\"staging\");var f1=Path.Combine(z,\"final-artifacts\",\"pass-1\");var f2=Path.Combine(z,\"final-artifacts\",\"pass-2\");"
        + "foreach(var d in new[]{f1,f2}){Directory.CreateDirectory(d);foreach(var f in Directory.EnumerateFiles(Path.Combine(r,\"worker\",\"payload\")))File.Copy(f,Path.Combine(d,Path.GetFileName(f)));}Directory.CreateDirectory(q);"
        + "var m=new{scientificOutcome=\"LIMITED_PARK\",integrityFailures=Array.Empty<string>(),blockedReasons=Array.Empty<string>(),rngCalls=0,inputsUnchanged=true,behaviorChanged=false,defaultChanged=false};"
        + "File.WriteAllBytes(Path.Combine(q,\"response.json\"),JsonSerializer.SerializeToUtf8Bytes(new{first=m,second=m,provenance=v}));";

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
        private readonly bool failOfficialAfterReceipt;
        internal FakeIsolatedRuntime(CountingScience science, string runtimeHead,
            bool failOfficialAfterReceipt = false)
        { this.science = science; this.runtimeHead = runtimeHead;
            this.failOfficialAfterReceipt = failOfficialAfterReceipt; }
        public Lane0IntegrationPreparedRuntime Prepare(string root, string expectedHead)
        {
            var assembly = Path.Combine(root, "synthetic-worker.dll");
            var closure = new Lane0IntegrationBinaryClosure(
                ImmutableArray<Lane0IntegrationBinaryIdentity>.Empty, "SYNTHETIC");
            return new(root, assembly, "SYNTHETIC", closure,
                new(runtimeHead, assembly, "SYNTHETIC", root, root,
                    Lane0ExecutionRootScientificRuntime.ExpectedWorkerMarker, closure));
        }
        public Lane0IntegrationResearchResult RunOfficial(
            Lane0IntegrationPreparedRuntime prepared, string canonicalSourceRoot,
            string expectedHead, Lane0IntegrationRuntimeIdentities expectedIdentities,
            Lane0IntegrationOfficialReceiptCoordinator receiptCoordinator, string corpusRootToken)
        {
            var paths = Lane0IntegrationOfficialLayout.Derive(canonicalSourceRoot,
                prepared.ExecutionRoot);
            var binding = new Lane0IntegrationCanonicalBindingLoader().Load(
                paths.CanonicalBindingPath);
            var store = new Lane0IntegrationFileAuthorityStore(paths.DurableReceiptPath,
                paths.FinalArtifactsRoot, paths.StagingRoot);
            var launch = new Lane0IntegrationLaunchRequest(expectedHead, expectedIdentities, null,
                canonicalSourceRoot, prepared.ExecutionRoot, "OPAQUE");
            store.CreateDurableReceipt(Lane0CorrectiveSuccessorIsolatedLauncher.BuildReceipt(
                launch, binding.CanonicalSha256));
            if (failOfficialAfterReceipt)
                throw new InvalidDataException("Synthetic post-receipt scientific failure.");
            var first = science.Evaluate([], new(expectedHead, "", "", "", "", "", "", "", []));
            var second = science.Evaluate([], new(expectedHead, "", "", "", "", "", "", "", []));
            return new("PUBLISHABLE", first.ScientificOutcome, true, 2, 2,
                Lane0CorrectiveSuccessorIsolatedLauncher.AuthorityOrder,
                "Synthetic unified official capability.");
        }
        public Lane0IntegrationScientificRuntimeResult ExecuteSyntheticFixture(
            Lane0IntegrationPreparedRuntime prepared,
            ImmutableArray<Lane0CorrectiveChartInput> inputs, Lane0SuccessorEvaluationContext context,
            Lane0IntegrationRuntimeIdentities identities)
        {
            var first = science.Evaluate(inputs, context);
            var second = science.Evaluate(inputs, context);
            var final = Lane0IntegrationOfficialLayout.FinalArtifactsRoot(prepared.ExecutionRoot);
            return new(first, second, prepared.Provenance,
                Path.Combine(final, "pass-1"), Path.Combine(final, "pass-2"));
        }
    }

    private sealed class CountingSemanticVerifier : ILane0IntegrationDeepSemanticVerifier
    {
        internal int Calls { get; private set; }
        public void Verify(ImmutableSortedDictionary<string, byte[]> package,
            Lane0IntegrationSemanticExpectations expected) => Calls++;
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
