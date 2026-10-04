using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            new ManifestCodec(manifest));

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
        var plan = Lane0CorrectiveSuccessorFrozenC11Adapter.BuildOfficialPlan(manifest);
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
    }

    [Fact]
    public void AdapterRejectsWrongContentIdentityWithoutEnumerationOrSongsDiscovery()
    {
        var manifest = Manifest();
        var reader = new ManifestReader(manifest) { CorruptFirstRead = true };
        var adapter = new Lane0CorrectiveSuccessorFrozenC11Adapter(manifest, reader,
            new ManifestCodec(manifest));
        Assert.Throws<InvalidDataException>(() => adapter.Admit(manifest, Path.GetTempPath()));
        Assert.Single(reader.ReadPaths);
        Assert.DoesNotContain(reader.ReadPaths, x => x.Contains("Songs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PreReceiptGateTreatsPoisonCorpusRootAsOpaque()
    {
        using var temp = new TempDirectory();
        var launcher = new Lane0CorrectiveSuccessorIsolatedLauncher(new FakeCheckoutMaterializer());
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
        request = mutation switch
        {
            "absent-binding" => request with { Binding = null },
            "wrong-head" => request with { SourceHead = new string('F', 40) },
            "runtime-head" => request with { RuntimeHead = new string('E', 40) },
            "dirty" => request with { SourceTrackedClean = false },
            "no-authorization" => Rebind(request, authorization: false),
            "wrong-count" => Rebind(request, count: 2),
            "receipt" => request,
            "output" => request,
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        store.ReceiptExistsValue = mutation == "receipt";
        store.OutputExistsValue = mutation == "output";
        Assert.Throws<InvalidDataException>(() =>
            new Lane0CorrectiveSuccessorIsolatedLauncher(new FakeCheckoutMaterializer())
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
            new Lane0CorrectiveSuccessorIsolatedLauncher(new FakeCheckoutMaterializer())
                .Acquire(request, new MemoryAuthorityStore()));
    }

    [Fact]
    public void LauncherRejectsHardlinkOrNonDetachedCheckoutEvidence()
    {
        using var temp = new TempDirectory();
        var request = LaunchRequest(temp.Path, "opaque");
        Assert.Throws<InvalidDataException>(() =>
            new Lane0CorrectiveSuccessorIsolatedLauncher(new BadCheckoutMaterializer())
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
            new Lane0CorrectiveSuccessorIsolatedLauncher(new FakeCheckoutMaterializer()),
            new FakeAdapter(manifest), science, semantic, finalizer);
        var store = new MemoryAuthorityStore();
        var result = runner.ExecuteSyntheticOrFutureAuthorized(new(
            LaunchRequest(temp.Path, Path.Combine(temp.Path, "corpus")), manifest,
            Lane0CorrectiveSuccessorReferenceValidator.FrozenReferences), store);

        Assert.Equal("PUBLISHABLE", result.Classification);
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
            new Lane0CorrectiveSuccessorIsolatedLauncher(new FakeCheckoutMaterializer()),
            new FakeAdapter(manifest), science, semantic, new IdentityPackageFinalizer());
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
        return new(head, ids, ids,
            Lane0CorrectiveSuccessorIsolatedLauncher.CreateSyntheticVerifiedBinding(fields),
            head, head, head, true, Path.Combine(root, "source"), Path.Combine(root, "execution"), corpus);
    }

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
        var charts = new[] { new { chartId = "SYNTHETIC", keyCount = 4, family = "RICE_HEAD_COMPLETION" } };
        var cases = Enumerable.Range(0, 11).Select(index => new
        {
            chartId = index < 9 ? Lane0CorrectiveSuccessorReferenceValidator.SecondHistoricalIdentity
                : Lane0CorrectiveSuccessorReferenceValidator.CorrectedSpringIdentity,
            keyCount = 7, occurrenceId = $"O{index}", historicalState = "OPERATIONAL",
            correctedState = "SUPPORTED", correctedSupported = true
        }).ToArray();
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
            outcome = "LIMITED_PARK", charts, g1Transitions = Array.Empty<object>(),
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
        files["chart_family_keymode_results.csv"] = Encoding.UTF8.GetBytes(ChartCsv("SYNTHETIC"));
        files["g1_historical_operational_cases.csv"] = Encoding.UTF8.GetBytes(G1Cases(9, 2));
        files["g1_state_transitions.csv"] = Encoding.UTF8.GetBytes(
            "chart_id,keymode,occurrence_id,group_id,historical_state,corrected_state,historical_joint_supported,corrected_joint_supported,historical_operational,corrected_operational,transition_kind\n");
        var semantic = files.ToImmutable();
        files["sha256sums.txt"] = ChecksumBytes(semantic);
        var locations = Enumerable.Range(0, 11).Select(index =>
            new Lane0IntegrationCorpusLocation($"L{index:D2}", $"p{index}", $"C{index}", "F", 4, 1)).ToList();
        locations[0] = locations[0] with
        {
            ChartId = Lane0CorrectiveSuccessorFrozenC11Adapter.DuplicateChartId,
            Family = "ORGT | DESTINY | BAIO", KeyCount = 7, OriginalObjects = 1653
        };
        locations.Add(locations[0] with { LocationId = "L11", RelativePath = "p11" });
        var scientific = locations.Take(11).Select(x => new Lane0CorrectiveChartInput(x.ChartId,
            EmptyChart(x.KeyCount))).ToImmutableArray();
        var admitted = new Lane0IntegrationAdmittedCorpus(locations.ToImmutableArray(), scientific, 50_836);
        return (files.ToImmutable(), new(ids, head, "LIMITED_PARK", admitted, false));
    }

    private static string ChartCsv(string id) =>
        "chart_id,keymode,family,historical_structural,historical_joint_support,historical_operational,historical_operational_supported,corrected_structural,corrected_joint_support,corrected_operational,corrected_operational_supported,corrected_integrity_failures\n"
        + $"{id},4,RICE_HEAD_COMPLETION,0,0,0,0,0,0,0,0,0\n";

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

    private static byte[] ChecksumBytes(ImmutableSortedDictionary<string, byte[]> files) =>
        Encoding.UTF8.GetBytes(string.Join('\n', files.Select(x =>
            $"{Convert.ToHexString(SHA256.HashData(x.Value))}  {x.Key}")) + "\n");
    private static byte[] Json(object value) => JsonSerializer.SerializeToUtf8Bytes(value,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    private static ManiaChart EmptyChart(int keys) => new()
    {
        KeyCount = keys, Lines = [], OriginalObjects = [], TimingPoints = [new(0, 500)]
    };

    private sealed class ManifestReader : ILane0IntegrationCorpusReader
    {
        private readonly Dictionary<string, string> byPath;
        internal List<string> ReadPaths { get; } = [];
        internal bool CorruptFirstRead { get; init; }
        internal ManifestReader(VerifiedFrozenC11Manifest manifest) => byPath =
            Lane0CorrectiveSuccessorFrozenC11Adapter.BuildOfficialPlan(manifest)
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
            var plan = Lane0CorrectiveSuccessorFrozenC11Adapter.BuildOfficialPlan(manifest);
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
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }
}
