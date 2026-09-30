using System.Collections.Immutable;
using ManiaAddNotesLab.Core;
using Xunit;

public sealed class Lane0CorrectiveExecutionPreparationTests
{
    [Fact]
    public void BindingAbsentBlocksBeforeCorpusAccess()
    {
        var adapter = new FakeAdapter([]);
        var result = Run(null, adapter);
        Assert.Equal("BLOCKED", result.Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Fact]
    public void HumanAuthorizationFalseBlocksBeforeCorpusAccess()
    {
        var adapter = new FakeAdapter([]);
        var result = Run(Binding(authorization: false), adapter);
        Assert.Equal("BLOCKED", result.Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Fact]
    public void ApprovedHeadCheckPrecedesHumanAuthorizationCheck()
    {
        var adapter = new FakeAdapter([]);
        var result = Run(Binding(authorization: false, replacements: new()
        { ["approvedPublishedHead"] = "1111111111111111111111111111111111111111" }), adapter);
        Assert.Equal("INVALID", result.Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":null}")]
    public void MalformedBindingIsInvalidBeforeCorpusAccess(string json)
    {
        var adapter = new FakeAdapter([]);
        Assert.Equal("INVALID", Run(json, adapter).Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Fact]
    public void ExtraBindingPropertyIsInvalid()
    {
        var adapter = new FakeAdapter([]);
        var json = Binding()[..^1] + ",\"extra\":true}";
        Assert.Equal("INVALID", Run(json, adapter).Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Theory]
    [InlineData("approvedPublishedHead", "1111111111111111111111111111111111111111")]
    [InlineData("executionPreparationContractSha256", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("preregistrationCanonicalSha256", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("evaluatorHardeningCanonicalSha256", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("expectedC11ManifestSha256", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void IdentityDriftIsInvalidBeforeCorpusAccess(string property, string value)
    {
        var adapter = new FakeAdapter([]);
        var result = Run(Binding(replacements: new() { [property] = value }), adapter);
        Assert.Equal("INVALID", result.Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ExecutionCountMustBeExactlyOne(int count)
    {
        var adapter = new FakeAdapter([]);
        Assert.Equal("INVALID", Run(Binding(count: count), adapter).Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Fact]
    public void TemplatePlaceholderCannotAuthorize()
    {
        var adapter = new FakeAdapter([]);
        var result = Run(Binding(replacements: new()
        { ["approvedPublishedHead"] = "PENDING_MANUAL_PUBLICATION" }), adapter);
        Assert.Equal("INVALID", result.Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Fact]
    public void CorpusResolverStartsOnlyAfterEveryGuardPasses()
    {
        var adapter = new FakeAdapter([]);
        var result = Run(Binding(), adapter);
        Assert.Equal(2, adapter.AccessCalls);
        Assert.True(result.CorpusAccessPerformed);
        Assert.True(result.CorrectiveEvaluationExecuted);
    }

    [Fact]
    public void MissingHashInventoryIsInvalid()
    {
        var verification = ExactInventory() with { MissingHashes = ["A"] };
        Assert.Throws<InvalidDataException>(() =>
            Lane0FrozenCorrectiveCorpusAdapter.ValidateInventory(verification));
    }

    [Fact]
    public void UnexpectedContentInventoryIsInvalid()
    {
        var verification = ExactInventory() with
        { UnexpectedContents = [new("extra.osu", new string('A', 64))] };
        Assert.Throws<InvalidDataException>(() =>
            Lane0FrozenCorrectiveCorpusAdapter.ValidateInventory(verification));
    }

    [Fact]
    public void MetadataMismatchInventoryIsInvalid()
    {
        var verification = ExactInventory() with { MetadataMismatches = ["mismatch"] };
        Assert.Throws<InvalidDataException>(() =>
            Lane0FrozenCorrectiveCorpusAdapter.ValidateInventory(verification));
    }

    [Fact]
    public void DuplicateCountMismatchIsInvalid()
    {
        var verification = ExactInventory() with { Duplicates = [] };
        Assert.Throws<InvalidDataException>(() =>
            Lane0FrozenCorrectiveCorpusAdapter.ValidateInventory(verification));
    }

    [Fact]
    public void ExactSyntheticInventoryReturnsStableOrdinalInputs()
    {
        var inventory = ExactInventory();
        var first = Lane0FrozenCorrectiveCorpusAdapter.BuildInputs(inventory,
            path => SyntheticOsu(KeyForPath(path)));
        var second = Lane0FrozenCorrectiveCorpusAdapter.BuildInputs(inventory,
            path => SyntheticOsu(KeyForPath(path)));
        Assert.Equal(11, first.Length);
        Assert.Equal(first.Select(x => x.ChartId), first.Select(x => x.ChartId).Order(StringComparer.Ordinal));
        Assert.Equal(first.Select(x => x.ChartId), second.Select(x => x.ChartId));
        Assert.Equal(first.Select(x => x.Chart.KeyCount), second.Select(x => x.Chart.KeyCount));
    }

    [Fact]
    public void AdapterParsesEveryUniqueContentExactlyOncePerLoad()
    {
        var calls = 0;
        var inputs = Lane0FrozenCorrectiveCorpusAdapter.BuildInputs(ExactInventory(), path =>
        { calls++; return SyntheticOsu(KeyForPath(path)); });
        Assert.Equal(11, calls);
        Assert.Equal(11, inputs.Length);
    }

    [Fact]
    public void EvaluationConsumesNoRngAndDoesNotMutateChart()
    {
        var chart = OsuBeatmap.Parse(SyntheticOsu(4));
        var before = MapperEvidenceProfileBuilder.ComputeFingerprint(chart);
        var result = Lane0CorrectiveEvaluationRunner.Evaluate([new("synthetic", chart)],
            new(true, Lane0CorrectiveEvaluationRunner.FrozenHistoricalSnapshot,
                Lane0CorrectiveEvaluationRunner.FrozenHistoricalG1CaseRule));
        Assert.Equal(0, result.ResearchRngCalls);
        Assert.True(result.InputsUnchanged);
        Assert.False(result.BehaviorChanged);
        Assert.Equal(before, MapperEvidenceProfileBuilder.ComputeFingerprint(chart));
    }

    [Fact]
    public void IndependentLoadsDriveTwoRealByteIdenticalEvaluations()
    {
        var output = Path.Combine(Path.GetTempPath(), $"lane0-never-written-{Guid.NewGuid():N}");
        var firstChart = OsuBeatmap.Parse(SyntheticOsu(4));
        var secondChart = OsuBeatmap.Parse(SyntheticOsu(4));
        var adapter = new FakeAdapter(call => call == 1
            ? [new("synthetic", firstChart)] : [new("synthetic", secondChart)]);
        var result = Run(Binding(), adapter, output);
        Assert.Equal(2, adapter.AccessCalls);
        Assert.True(result.CorrectiveEvaluationExecuted);
        Assert.Equal("INVALID", result.Outcome); // synthetic data cannot reproduce frozen C11 facts
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void NonCanonicalBindingPathFailsBeforeCorpusAccess()
    {
        var root = RepositoryRoot();
        var adapter = new FakeAdapter([]);
        var result = Lane0CorrectiveExecutionPreparation.Execute(new(root,
            Path.Combine(root, "docs/not-the-binding.json"),
            Path.Combine(root, "docs/lane_0_corrective_execution_preparation_contract.json"),
            Path.Combine(root, "docs/lane_0_corrective_evaluator_hardening_contract.json"),
            "unused-manifest", "unused-corpus",
            Path.Combine(root, Lane0CorrectiveExecutionPreparation.OfficialOutputPath)), adapter);
        Assert.Equal("INVALID", result.Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Fact]
    public void CanonicalBindingAbsenceBlocksFullRouteBeforeCorpusAccess()
    {
        var root = RepositoryRoot();
        var adapter = new FakeAdapter([]);
        var result = Lane0CorrectiveExecutionPreparation.Execute(new(root,
            Path.Combine(root, Lane0CorrectiveExecutionPreparation.CanonicalBindingPath),
            Path.Combine(root, "docs/lane_0_corrective_execution_preparation_contract.json"),
            Path.Combine(root, "docs/lane_0_corrective_evaluator_hardening_contract.json"),
            "unused-manifest", "unused-corpus",
            Path.Combine(root, Lane0CorrectiveExecutionPreparation.OfficialOutputPath)), adapter);
        Assert.Equal("BLOCKED", result.Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Fact]
    public void NonOfficialOutputPathFailsBeforeCorpusAccess()
    {
        var root = RepositoryRoot();
        var adapter = new FakeAdapter([]);
        var result = Lane0CorrectiveExecutionPreparation.Execute(new(root,
            Path.Combine(root, Lane0CorrectiveExecutionPreparation.CanonicalBindingPath),
            Path.Combine(root, "docs/lane_0_corrective_execution_preparation_contract.json"),
            Path.Combine(root, "docs/lane_0_corrective_evaluator_hardening_contract.json"),
            "unused-manifest", "unused-corpus", Path.Combine(root, ".artifacts/not-official")), adapter);
        Assert.Equal("INVALID", result.Outcome);
        Assert.Equal(0, adapter.AccessCalls);
    }

    [Fact]
    public void PreexistingOutputFileIsRejectedWithoutOverwrite()
    {
        var root = TempRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "old.dat"), "sentinel");
            Assert.Throws<InvalidOperationException>(() =>
                Lane0CorrectiveEvaluationRunner.WriteArtifacts(BlockedResult(), root));
            Assert.Equal("sentinel", File.ReadAllText(Path.Combine(root, "old.dat")));
            Assert.Single(Directory.EnumerateFiles(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void PreexistingOutputDirectoryIsRejectedWithoutPartialArtifacts()
    {
        var root = TempRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "old-run"));
            Assert.Throws<InvalidOperationException>(() =>
                Lane0CorrectiveEvaluationRunner.WriteArtifacts(BlockedResult(), root));
            Assert.Empty(Directory.EnumerateFiles(root));
            Assert.Single(Directory.EnumerateDirectories(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void PublishedContractReproducesScientificInputsAndElevenCaseRule()
    {
        var root = RepositoryRoot();
        var state = Lane0CorrectiveExecutionPreparation.LoadPublishedState(root,
            Path.Combine(root, "docs/lane_0_corrective_execution_preparation_contract.json"),
            Path.Combine(root, "docs/lane_0_corrective_evaluator_hardening_contract.json"));
        Assert.Equal(Lane0CorrectiveEvaluationRunner.FrozenHistoricalSnapshot, state.HistoricalSnapshot);
        Assert.Equal(11, state.HistoricalCaseRule.ExpectedCount);
        Assert.Equal(7, state.HistoricalCaseRule.ExpectedKeymode);
        Assert.Equal(9, state.HistoricalCaseRule.ExpectedChartDistribution[
            "20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788"]);
        Assert.Equal(2, state.HistoricalCaseRule.ExpectedChartDistribution[
            "E73B098A4D4C99D716D2C53B3EA3A4BFD1A75059881F30DB416631B5EDB972D4"]);
    }

    [Fact]
    public void FinalCanonicalBindingPathIsAbsent()
    {
        var root = RepositoryRoot();
        Assert.False(File.Exists(Path.Combine(root,
            Lane0CorrectiveExecutionPreparation.CanonicalBindingPath)));
    }

    private static Lane0CorrectiveExecutionPreparationResult Run(string? binding, FakeAdapter adapter,
        string? output = null)
    {
        var state = new Lane0CorrectivePublishedState(
            "2222222222222222222222222222222222222222", new string('B', 64),
            Lane0CorrectiveEvaluationRunner.FrozenHistoricalSnapshot,
            Lane0CorrectiveEvaluationRunner.FrozenHistoricalG1CaseRule);
        return Lane0CorrectiveExecutionPreparation.ExecuteAfterPublishedIdentity(state, binding,
            adapter, output ?? Path.Combine(Path.GetTempPath(), "never-written-lane0"));
    }

    private static string Binding(bool authorization = true, int count = 1,
        Dictionary<string, string>? replacements = null)
    {
        var values = new Dictionary<string, object>
        {
            ["schemaVersion"] = Lane0CorrectiveExecutionPreparation.BindingSchema,
            ["approvedPublishedHead"] = "2222222222222222222222222222222222222222",
            ["executionPreparationContractSha256"] = new string('B', 64),
            ["preregistrationCanonicalSha256"] = Lane0CorrectiveExecutionPreparation.PreregistrationSha256,
            ["evaluatorHardeningCanonicalSha256"] = Lane0CorrectiveExecutionPreparation.HardeningSha256,
            ["expectedC11ManifestSha256"] = Lane0CorrectiveExecutionPreparation.ExpectedManifestSha256,
            ["explicitHumanAuthorization"] = authorization,
            ["authorizedExecutionCount"] = count
        };
        if (replacements is not null)
            foreach (var pair in replacements) values[pair.Key] = pair.Value;
        return System.Text.Json.JsonSerializer.Serialize(values);
    }

    private static C11FrozenCorpusVerification ExactInventory()
    {
        var descriptors = Enumerable.Range(0, 11).Select(i => Descriptor(i)).ToImmutableArray();
        return new("synthetic-root", 12, descriptors, [], [], [], [],
            [new(descriptors[0].Sha256, ["chart-00.osu", "duplicate/chart-00.osu"])]);
    }

    private static C11CorpusChartDescriptor Descriptor(int index)
    {
        var keys = new[] { 4, 7, 10 }[index % 3];
        var hash = index.ToString("X64");
        return new($"chart-{index:D2}-{keys}.osu", $"chart-{index:D2}.osu", hash,
            $"Artist {index}", $"Title {index}", $"Creator {index}", $"Version {index}",
            $"FAMILY {index}", keys, 1, 1, 0, 1, 120, 120,
            index == 0 ? ["chart-00.osu", "duplicate/chart-00.osu"] : [$"chart-{index:D2}.osu"]);
    }

    private static int KeyForPath(string path) => int.Parse(Path.GetFileNameWithoutExtension(path).Split('-')[2]);

    private static string SyntheticOsu(int keys) => $"""
        osu file format v14
        [General]
        Mode:3
        [Metadata]
        Artist:Synthetic
        Title:Synthetic
        Creator:Test
        Version:{keys}K
        [Difficulty]
        CircleSize:{keys}
        [TimingPoints]
        0,500,4,2,0,100,1,0
        [HitObjects]
        64,192,0,1,0,0:0:0:0:
        """;

    private static Lane0CorrectiveEvaluationResult BlockedResult() =>
        Lane0CorrectiveEvaluationRunner.Evaluate([], new(false));

    private static string TempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lane0-execution-prep-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "ManiaAddNotesLab.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class FakeAdapter : ILane0CorrectiveCorpusAdapter
    {
        private readonly Func<int, ImmutableArray<Lane0CorrectiveChartInput>> _factory;
        public int AccessCalls { get; private set; }
        public FakeAdapter(ImmutableArray<Lane0CorrectiveChartInput> values) : this(_ => values) { }
        public FakeAdapter(Func<int, ImmutableArray<Lane0CorrectiveChartInput>> factory) => _factory = factory;
        public ImmutableArray<Lane0CorrectiveChartInput> Load() => _factory(++AccessCalls);
    }
}
