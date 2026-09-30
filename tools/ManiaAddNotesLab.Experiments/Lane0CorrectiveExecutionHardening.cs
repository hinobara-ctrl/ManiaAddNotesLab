using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;

internal sealed record Lane0HardenedPublicationBinding(
    string SchemaVersion,
    string ApprovedPublishedHead,
    string ExecutionHardeningContractSha256,
    string PreregistrationCanonicalSha256,
    string EvaluatorHardeningCanonicalSha256,
    string ExpectedC11ManifestSha256,
    bool ExplicitHumanAuthorization,
    int AuthorizedExecutionCount);

internal sealed record Lane0HardenedExecutionRequest(string RepositoryRoot, string CorpusRoot);

internal sealed record Lane0HardenedExecutionResult(
    string Outcome,
    string Reason,
    bool AttemptConsumed,
    bool CorpusAccessPerformed,
    bool CorrectiveEvaluationExecuted);

internal sealed record Lane0HardenedPublishedState(
    string CurrentHead,
    string ContractSha256,
    string ManifestPath,
    Lane0FrozenSnapshot HistoricalSnapshot,
    Lane0HistoricalCaseRule HistoricalCaseRule);

internal interface ILane0HardenedCorpusAdapter
{
    int AccessCalls { get; }
    ImmutableArray<Lane0CorrectiveChartInput> Load(ImmutableArray<C11FrozenCorpusExpectation> expectations);
}

internal sealed class Lane0HardenedCorpusAdapter : ILane0HardenedCorpusAdapter
{
    private readonly string _corpusRoot;
    public int AccessCalls { get; private set; }

    public Lane0HardenedCorpusAdapter(string corpusRoot) => _corpusRoot = Path.GetFullPath(corpusRoot);

    public ImmutableArray<Lane0CorrectiveChartInput> Load(
        ImmutableArray<C11FrozenCorpusExpectation> expectations)
    {
        AccessCalls++;
        var verification = C11CorpusDiscovery.ResolveFrozen(_corpusRoot, expectations);
        return Lane0CorrectiveExecutionHardening.BuildInputs(verification, File.ReadAllBytes);
    }
}

internal static class Lane0CorrectiveExecutionCommand
{
    internal static bool TryParseArguments(string[] args, out string? repositoryRoot,
        out string? corpusRoot, out string error)
    {
        repositoryRoot = null;
        corpusRoot = null;
        error = "Usage: --repo-root <path> --corpus-root <explicit-frozen-c11-root>";
        if (args.Length != 4) return false;
        for (var index = 0; index < args.Length; index += 2)
        {
            var value = args[index + 1];
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (args[index] == "--repo-root" && repositoryRoot is null) repositoryRoot = value;
            else if (args[index] == "--corpus-root" && corpusRoot is null) corpusRoot = value;
            else return false;
        }
        return repositoryRoot is not null && corpusRoot is not null;
    }
}

internal static class Lane0CorrectiveExecutionHardening
{
    internal const string BindingSchema = "lane-0-corrective-evaluation-publication-binding.2";
    internal const string ContractSchema = "lane-0-corrective-execution-hardening.1";
    internal const string BaselineHead = "b3a87c5420371af212ff5f0512e5137dffb1e2bc";
    internal const string PreregistrationSha256 =
        "6392C579B87EC318C1DE381201D797B004650AB961398E3E2AB23F7A49A83D33";
    internal const string EvaluatorHardeningSha256 =
        "A61F0933A49534857096D3A568E9C07F37805F9468CC94001546767073CD2E18";
    internal const string EvaluatorImplementationSha256 =
        "20D0AC6BB0AF197ACC6BE1B5E71D1DF65BD2CDC0FCAB0E49FD6F036D03A0D8F4";
    internal const string EvaluatorHarnessSha256 =
        "724837E42F2308DA3159BAE00F3339656C03EE936B9E350F416124A7E92AA173";
    internal const string SolutionSha256 =
        "BFEB1689414A2FB712170A967B9BA6DAD70BFE656D307A25821A976D81942A37";
    internal const string ExpectedManifestSha256 =
        "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    internal const string ContractPath = "docs/lane_0_corrective_execution_hardening_contract.json";
    internal const string BindingPath = "docs/lane_0_corrective_evaluation_publication_binding.json";
    internal const string FinalOutputPath = ".artifacts/lane_0_corrective";
    internal const string StagingOutputPath = ".artifacts/lane_0_corrective.staging";
    internal const string AttemptReceiptPath = ".artifacts/lane_0_corrective.attempt.json";

    internal static readonly ImmutableArray<string> ArtifactNames =
    [
        "chart_family_keymode_results.csv",
        "g1_historical_operational_cases.csv",
        "g1_state_transitions.csv",
        "integrity.json",
        "scientific_summary.json",
        "sha256sums.txt"
    ];

    public static Lane0HardenedExecutionResult Execute(
        Lane0HardenedExecutionRequest request, ILane0HardenedCorpusAdapter? adapter = null)
    {
        var root = Path.GetFullPath(request.RepositoryRoot);
        Lane0HardenedPublishedState state;
        try
        {
            state = LoadPublishedState(root);
        }
        catch (Exception exception) when (IsValidationException(exception))
        {
            return new("INVALID", exception.Message, false, false, false);
        }

        var bindingPath = Path.Combine(root, BindingPath);
        if (!File.Exists(bindingPath))
            return new("BLOCKED", "Canonical publication binding v2 is absent.", false, false, false);

        byte[] bindingBytes;
        Lane0HardenedPublicationBinding binding;
        try
        {
            bindingBytes = File.ReadAllBytes(bindingPath);
            binding = ParseBinding(bindingBytes);
        }
        catch (Exception exception) when (IsValidationException(exception))
        {
            return new("INVALID", exception.Message, false, false, false);
        }

        var authorization = ValidateBinding(state, binding);
        if (authorization is not null) return authorization;

        var final = Path.Combine(root, FinalOutputPath);
        var staging = Path.Combine(root, StagingOutputPath);
        var receipt = Path.Combine(root, AttemptReceiptPath);
        var readiness = ValidateOutputReadiness(final, staging, receipt);
        if (readiness is not null) return readiness;

        VerifiedFrozenC11Manifest manifest;
        try
        {
            manifest = FrozenC11ManifestResearch.Load(Path.Combine(root, state.ManifestPath));
            if (!string.Equals(manifest.CanonicalSha256, ExpectedManifestSha256,
                    StringComparison.Ordinal))
                throw new InvalidDataException("Frozen public manifest identity drifted.");
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new("BLOCKED", "Public manifest prerequisite is unavailable.", false, false, false);
        }
        catch (Exception exception) when (IsValidationException(exception))
        {
            return new("INVALID", exception.Message, false, false, false);
        }

        adapter ??= new Lane0HardenedCorpusAdapter(request.CorpusRoot);
        return ExecuteAuthorized(state, bindingBytes, manifest.Charts, adapter, final, staging, receipt);
    }

    internal static Lane0HardenedExecutionResult ExecuteAuthorized(
        Lane0HardenedPublishedState state,
        byte[] bindingBytes,
        ImmutableArray<C11FrozenCorpusExpectation> expectations,
        ILane0HardenedCorpusAdapter adapter,
        string final,
        string staging,
        string receipt,
        Action<string, byte[]>? writeFile = null)
    {
        var readiness = ValidateOutputReadiness(final, staging, receipt);
        if (readiness is not null) return readiness;
        try
        {
            CreateAttemptReceipt(receipt, state.CurrentHead,
                Convert.ToHexString(SHA256.HashData(bindingBytes)), state.ContractSha256);
        }
        catch (IOException exception)
        {
            return new("BLOCKED", $"Attempt receipt could not be acquired: {exception.Message}",
                false, false, false);
        }

        ImmutableArray<Lane0CorrectiveChartInput> firstInputs;
        ImmutableArray<Lane0CorrectiveChartInput> secondInputs;
        try
        {
            firstInputs = adapter.Load(expectations);
            secondInputs = adapter.Load(expectations);
        }
        catch (Exception exception) when (IsValidationException(exception))
        {
            return new("INVALID", exception.Message, true, adapter.AccessCalls != 0, false);
        }

        var options = new Lane0CorrectiveEvaluationOptions(true,
            state.HistoricalSnapshot, state.HistoricalCaseRule);
        var first = Lane0CorrectiveEvaluationRunner.Evaluate(firstInputs, options);
        var second = Lane0CorrectiveEvaluationRunner.Evaluate(secondInputs, options);
        var firstPackage = BuildPackage(first);
        var secondPackage = BuildPackage(second);
        if (!PackagesEqual(firstPackage, secondPackage))
            return new("INVALID", "Independent six-artifact packages are not byte-identical.",
                true, true, true);
        if (first.ResearchRngCalls != 0 || second.ResearchRngCalls != 0
            || !first.InputsUnchanged || !second.InputsUnchanged
            || first.BehaviorChanged || second.BehaviorChanged)
            return new("INVALID", "Corrective evaluation violated non-interference.", true, true, true);
        if (first.Outcome is "INVALID" or "BLOCKED")
            return new(first.Outcome, "Corrective evaluator did not produce a publishable outcome.",
                true, true, true);

        try
        {
            PublishPackageAtomically(firstPackage, staging, final, writeFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or InvalidOperationException)
        {
            return new("INVALID", $"Atomic publication failed: {exception.Message}", true, true, true);
        }
        return new(first.Outcome, "The single authorized attempt completed atomically.", true, true, true);
    }

    internal static ImmutableArray<Lane0CorrectiveChartInput> BuildInputs(
        C11FrozenCorpusVerification verification, Func<string, byte[]> readBytes)
    {
        ArgumentNullException.ThrowIfNull(verification);
        ArgumentNullException.ThrowIfNull(readBytes);
        ValidateInventory(verification);
        return verification.Charts.OrderBy(x => x.Sha256, StringComparer.Ordinal).Select(descriptor =>
        {
            var bytes = readBytes(descriptor.RuntimePath);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(hash, descriptor.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Evaluated byte identity drifted for {descriptor.Sha256}.");
            var text = new UTF8Encoding(false, true).GetString(bytes);
            var chart = OsuBeatmap.Parse(text);
            if (chart.KeyCount != descriptor.KeyCount
                || chart.OriginalObjects.Count != descriptor.ObjectCount)
                throw new InvalidDataException($"Adapter metadata drifted for {descriptor.Sha256}.");
            return new Lane0CorrectiveChartInput(descriptor.Sha256, chart);
        }).ToImmutableArray();
    }

    internal static void ValidateInventory(C11FrozenCorpusVerification verification)
    {
        if (!verification.IsExact || verification.OsuFileCount != 12 || verification.Charts.Length != 11)
            throw new InvalidDataException("Frozen corpus identity or cardinality drifted.");
        var duplicateLocations = verification.Duplicates.Sum(x => x.RelativePaths.Length - 1);
        if (verification.Duplicates.Length != 1 || duplicateLocations != 1)
            throw new InvalidDataException("Frozen corpus duplicate-location cardinality drifted.");
        if (verification.Charts.Select(x => x.FamilyKey).Distinct(StringComparer.Ordinal).Count() != 11
            || verification.Charts.Select(x => x.Sha256).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 11
            || !verification.Charts.Select(x => x.KeyCount).Distinct().Order().SequenceEqual([4, 7, 10]))
            throw new InvalidDataException("Frozen corpus family, hash or keymode inventory drifted.");
    }

    internal static Lane0HardenedExecutionResult? ValidateOutputReadiness(
        string final, string staging, string receipt)
    {
        if (File.Exists(final) || Directory.Exists(final))
            return new("BLOCKED", "Final output path must be absent.", false, false, false);
        if (File.Exists(staging) || Directory.Exists(staging))
            return new("BLOCKED", "Staging output path must be absent.", false, false, false);
        if (File.Exists(receipt) || Directory.Exists(receipt))
            return new("BLOCKED", "Attempt receipt already exists.", false, false, false);
        return null;
    }

    internal static void CreateAttemptReceipt(string path, string head, string bindingSha256,
        string contractSha256)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "lane-0-corrective-execution-attempt.1",
            approvedPublishedHead = head,
            bindingSha256,
            executionHardeningContractSha256 = contractSha256,
            authorizedExecutionCount = 1
        }, new JsonSerializerOptions { WriteIndented = true });
        bytes = [.. bytes, (byte)'\n'];
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(true);
    }

    internal static ImmutableSortedDictionary<string, byte[]> BuildPackage(
        Lane0CorrectiveEvaluationResult result)
    {
        var semantic = Lane0CorrectiveEvaluationRunner.SerializeArtifacts(result);
        var package = semantic.ToBuilder();
        var checksums = string.Join('\n', semantic.Select(pair =>
            $"{Convert.ToHexString(SHA256.HashData(pair.Value))}  {pair.Key}")) + "\n";
        package["sha256sums.txt"] = new UTF8Encoding(false).GetBytes(checksums);
        return package.ToImmutable();
    }

    internal static bool PackagesEqual(ImmutableSortedDictionary<string, byte[]> left,
        ImmutableSortedDictionary<string, byte[]> right) => left.Keys.SequenceEqual(right.Keys)
        && left.All(pair => pair.Value.AsSpan().SequenceEqual(right[pair.Key]));

    internal static void PublishPackageAtomically(
        ImmutableSortedDictionary<string, byte[]> package,
        string staging,
        string final,
        Action<string, byte[]>? writeFile = null)
    {
        if (!package.Keys.SequenceEqual(ArtifactNames))
            throw new InvalidDataException("Artifact package names drifted.");
        if (File.Exists(final) || Directory.Exists(final)
            || File.Exists(staging) || Directory.Exists(staging))
            throw new InvalidOperationException("Final and staging paths must be absent.");
        writeFile ??= File.WriteAllBytes;
        Directory.CreateDirectory(staging);
        foreach (var pair in package) writeFile(Path.Combine(staging, pair.Key), pair.Value);

        var actualNames = Directory.EnumerateFiles(staging).Select(Path.GetFileName)
            .Order(StringComparer.Ordinal).ToArray();
        if (!actualNames.SequenceEqual(ArtifactNames))
            throw new InvalidDataException("Staging contains missing or extra artifacts.");
        foreach (var pair in package)
        {
            var actual = File.ReadAllBytes(Path.Combine(staging, pair.Key));
            if (!actual.AsSpan().SequenceEqual(pair.Value))
                throw new InvalidDataException($"Staging bytes drifted for {pair.Key}.");
        }
        Directory.Move(staging, final);
    }

    internal static Lane0HardenedPublicationBinding ParseBinding(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        RequireShape(root, "publication binding v2", "schemaVersion", "approvedPublishedHead",
            "executionHardeningContractSha256", "preregistrationCanonicalSha256",
            "evaluatorHardeningCanonicalSha256", "expectedC11ManifestSha256",
            "explicitHumanAuthorization", "authorizedExecutionCount");
        var binding = new Lane0HardenedPublicationBinding(
            RequiredString(root, "schemaVersion"), RequiredString(root, "approvedPublishedHead"),
            RequiredString(root, "executionHardeningContractSha256"),
            RequiredString(root, "preregistrationCanonicalSha256"),
            RequiredString(root, "evaluatorHardeningCanonicalSha256"),
            RequiredString(root, "expectedC11ManifestSha256"),
            root.GetProperty("explicitHumanAuthorization").GetBoolean(),
            root.GetProperty("authorizedExecutionCount").GetInt32());
        if (!string.Equals(binding.SchemaVersion, BindingSchema, StringComparison.Ordinal))
            throw new InvalidDataException("Publication binding v2 schema is invalid.");
        if (!IsGitHead(binding.ApprovedPublishedHead))
            throw new InvalidDataException("Approved published HEAD must be exactly 40 hex characters.");
        foreach (var hash in new[] { binding.ExecutionHardeningContractSha256,
                     binding.PreregistrationCanonicalSha256, binding.EvaluatorHardeningCanonicalSha256,
                     binding.ExpectedC11ManifestSha256 })
            if (!IsSha256(hash)) throw new InvalidDataException("Publication binding contains malformed SHA-256.");
        return binding;
    }

    internal static bool IsTrackedClean(string root)
    {
        var unstaged = RunGit(root, "diff", "--quiet", "HEAD", "--");
        var staged = RunGit(root, "diff", "--cached", "--quiet");
        if (unstaged is > 1 || staged is > 1)
            throw new InvalidDataException("Cannot determine tracked working-tree state.");
        return unstaged == 0 && staged == 0;
    }

    internal static string TreeIdentity(string root, IEnumerable<string> paths)
    {
        var rows = paths.Order(StringComparer.Ordinal).Select(path =>
            $"{path.Replace('\\', '/')}|{NormalizedFileHash(Path.Combine(root, path))}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', rows))));
    }

    internal static string NormalizedFileHash(string path)
    {
        var text = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path));
        if (text.Length != 0 && text[0] == '\uFEFF') text = text[1..];
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        return Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false).GetBytes(text)));
    }

    internal static string CanonicalJsonHash(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, value);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static Lane0HardenedPublishedState LoadPublishedState(string root)
    {
        using var artifact = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ContractPath), Encoding.UTF8));
        RequireShape(artifact.RootElement, "execution hardening contract artifact",
            "contract", "canonicalSha256", "canonicalization");
        var contract = artifact.RootElement.GetProperty("contract");
        var canonical = CanonicalJsonHash(contract);
        Equal(canonical, RequiredString(artifact.RootElement, "canonicalSha256"), "contract canonical identity");
        Equal(RequiredString(contract, "schemaVersion"), ContractSchema, "contract schema");
        Equal(RequiredString(contract, "baselinePublishedHead"), BaselineHead, "baseline");
        Equal(RequiredString(contract, "parentPreparationContractCanonicalSha256"),
            "61C05856AC8BB48ABFC5BCFD3AA47599D2D5F567CBA6E39EC7AF27DD235B8F1E",
            "parent preparation contract declaration");
        Equal(RequiredString(contract, "parentPreparationImplementationSha256"),
            "A518278E7B0CCBEA8C7916720035C598073382CFF2616E790340DD6F45F8D219",
            "parent preparation implementation declaration");
        Equal(RequiredString(contract, "parentPreparationHarnessSha256"),
            "0501B1AA2625259AB66249A749C0963CE725C6678E2591CEF5D3CC3D7ADDC170",
            "parent preparation harness declaration");
        Equal(RequiredString(contract, "parentPreparationVerifierSha256"),
            "182CBAEFCD3B010EBA7C4505DA8F105BF3732E9D5D2BCD4D042531B920B201E0",
            "parent preparation verifier declaration");
        Equal(RequiredString(contract, "preregistrationCanonicalSha256"),
            PreregistrationSha256, "preregistration declaration");
        Equal(RequiredString(contract, "evaluatorHardeningCanonicalSha256"),
            EvaluatorHardeningSha256, "evaluator hardening declaration");
        Equal(RequiredString(contract, "evaluatorImplementationSha256"),
            EvaluatorImplementationSha256, "evaluator implementation declaration");
        Equal(RequiredString(contract, "evaluatorHarnessSha256"),
            EvaluatorHarnessSha256, "evaluator harness declaration");
        Equal(RequiredString(contract, "solutionNormalizedSha256"), SolutionSha256,
            "solution declaration");
        Equal(RequiredString(contract, "expectedC11ManifestSha256"), ExpectedManifestSha256,
            "manifest declaration");
        Equal(RequiredString(contract, "bindingSchema"), BindingSchema, "binding schema declaration");
        Equal(RequiredString(contract, "bindingCanonicalPath"), BindingPath, "binding path declaration");
        Equal(RequiredString(contract, "attemptReceiptPath"), AttemptReceiptPath,
            "attempt receipt path declaration");
        Equal(RequiredString(contract, "stagingPath"), StagingOutputPath, "staging path declaration");
        Equal(RequiredString(contract, "finalOutputPath"), FinalOutputPath, "final path declaration");

        var head = ReadHead(root);
        if (!IsTrackedClean(root)) throw new InvalidDataException("Tracked working tree is dirty.");

        ValidateCanonicalArtifact(root, RequiredString(contract, "parentPreparationContractPath"),
            RequiredString(contract, "parentPreparationContractCanonicalSha256"));
        ValidateCanonicalArtifact(root, RequiredString(contract, "preregistrationContractPath"),
            PreregistrationSha256);
        using var evaluatorHardening = ValidateCanonicalArtifact(root,
            RequiredString(contract, "evaluatorHardeningContractPath"), EvaluatorHardeningSha256);

        Equal(TreeIdentity(root, ReadStringArray(contract, "evaluatorImplementationFiles")),
            EvaluatorImplementationSha256, "evaluator implementation tree");
        Equal(TreeIdentity(root, ReadStringArray(contract, "evaluatorHarnessFiles")),
            EvaluatorHarnessSha256, "evaluator harness tree");
        Equal(NormalizedFileHash(Path.Combine(root, RequiredString(contract, "solutionPath"))),
            SolutionSha256, "solution identity");
        Equal(TreeIdentity(root, ReadStringArray(contract, "runtimeScientificFiles")),
            RequiredString(contract, "runtimeScientificTreeSha256"), "runtime scientific tree");
        Equal(TreeIdentity(root, ReadStringArray(contract, "hardeningImplementationFiles")),
            RequiredString(contract, "hardeningImplementationSha256"), "hardening implementation tree");
        Equal(TreeIdentity(root, ReadStringArray(contract, "officialRunnerFiles")),
            RequiredString(contract, "officialRunnerTreeSha256"), "official runner tree");

        var evaluatorContract = evaluatorHardening.RootElement.GetProperty("contract");
        var snapshot = ParseSnapshot(evaluatorContract.GetProperty("frozenHistoricalSnapshot"));
        var rule = ParseCaseRule(evaluatorContract.GetProperty("frozenHistoricalG1CaseRule"));
        if (snapshot != Lane0CorrectiveEvaluationRunner.FrozenHistoricalSnapshot
            || !RuleEquals(rule, Lane0CorrectiveEvaluationRunner.FrozenHistoricalG1CaseRule))
            throw new InvalidDataException("Frozen scientific inputs do not reproduce evaluator constants.");
        return new(head, canonical, RequiredString(contract, "manifestPath"), snapshot, rule);
    }

    private static JsonDocument ValidateCanonicalArtifact(string root, string relativePath, string expected)
    {
        var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, relativePath), Encoding.UTF8));
        var actual = CanonicalJsonHash(document.RootElement.GetProperty("contract"));
        Equal(actual, expected, $"{relativePath} canonical identity");
        Equal(RequiredString(document.RootElement, "canonicalSha256"), expected,
            $"{relativePath} declared identity");
        return document;
    }

    private static Lane0HardenedExecutionResult? ValidateBinding(
        Lane0HardenedPublishedState state, Lane0HardenedPublicationBinding binding)
    {
        if (!string.Equals(binding.ExecutionHardeningContractSha256, state.ContractSha256,
                StringComparison.Ordinal))
            return new("INVALID", "Execution hardening contract identity drifted.", false, false, false);
        if (!string.Equals(binding.PreregistrationCanonicalSha256, PreregistrationSha256,
                StringComparison.Ordinal)
            || !string.Equals(binding.EvaluatorHardeningCanonicalSha256, EvaluatorHardeningSha256,
                StringComparison.Ordinal)
            || !string.Equals(binding.ExpectedC11ManifestSha256, ExpectedManifestSha256,
                StringComparison.Ordinal))
            return new("INVALID", "Binding parent or manifest identity drifted.", false, false, false);
        if (!string.Equals(binding.ApprovedPublishedHead, state.CurrentHead,
                StringComparison.OrdinalIgnoreCase))
            return new("INVALID", "Approved published HEAD does not match current HEAD.", false, false, false);
        if (!binding.ExplicitHumanAuthorization)
            return new("BLOCKED", "Explicit human authorization is false.", false, false, false);
        if (binding.AuthorizedExecutionCount != 1)
            return new("INVALID", "Authorized execution count must equal one.", false, false, false);
        return null;
    }

    private static Lane0FrozenSnapshot ParseSnapshot(JsonElement value) => new(
        ParseFamily(value.GetProperty("rice")), ParseFamily(value.GetProperty("g1")));

    private static Lane0FrozenFamilySnapshot ParseFamily(JsonElement value) => new(
        value.GetProperty("structural").GetInt32(), value.GetProperty("comparableContext").GetInt32(),
        value.GetProperty("jointSupport").GetInt32(), value.GetProperty("jointUnique").GetInt32(),
        value.GetProperty("jointAmongAlternatives").GetInt32(), value.GetProperty("contradiction").GetInt32(),
        value.GetProperty("noContext").GetInt32(), value.GetProperty("operational").GetInt32(),
        value.GetProperty("operationalSupported").GetInt32(), value.GetProperty("supportedCharts").GetInt32(),
        value.GetProperty("supportedKeymodes").GetInt32());

    private static Lane0HistoricalCaseRule ParseCaseRule(JsonElement value) => new(
        value.GetProperty("total").GetInt32(),
        value.GetProperty("chartDistribution").EnumerateObject().ToImmutableSortedDictionary(
            x => x.Name, x => x.Value.GetInt32(), StringComparer.Ordinal),
        value.GetProperty("keymode").GetInt32());

    private static bool RuleEquals(Lane0HistoricalCaseRule left, Lane0HistoricalCaseRule right) =>
        left.ExpectedCount == right.ExpectedCount && left.ExpectedKeymode == right.ExpectedKeymode
        && left.ExpectedChartDistribution.SequenceEqual(right.ExpectedChartDistribution);

    private static string ReadHead(string root)
    {
        var (exit, output) = RunGitWithOutput(root, "rev-parse", "HEAD");
        var value = output.Trim();
        if (exit != 0 || !IsGitHead(value)) throw new InvalidDataException("Cannot read Git HEAD.");
        return value;
    }

    private static int RunGit(string root, params string[] arguments) =>
        RunGitWithOutput(root, arguments).ExitCode;

    private static (int ExitCode, string Output) RunGitWithOutput(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidDataException("Cannot run Git.");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    private static string[] ReadStringArray(JsonElement value, string property) =>
        value.GetProperty(property).EnumerateArray().Select(x => x.GetString()
            ?? throw new InvalidDataException($"{property} contains null.")).ToArray();

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }

    private static string RequiredString(JsonElement value, string property) =>
        value.GetProperty(property).GetString()
        ?? throw new InvalidDataException($"{property} is null.");

    private static void RequireShape(JsonElement value, string context, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{context} must be an object.");
        var actual = value.EnumerateObject().Select(x => x.Name).ToArray();
        var missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
        var extra = actual.Except(expected, StringComparer.Ordinal).ToArray();
        if (missing.Length != 0 || extra.Length != 0 || actual.Length != expected.Length)
            throw new InvalidDataException($"{context} shape drifted; missing=[{string.Join(',', missing)}], extra=[{string.Join(',', extra)}].");
    }

    private static void Equal(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"{label} drifted: {actual} != {expected}.");
    }

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
    private static bool IsGitHead(string value) => value.Length == 40 && value.All(Uri.IsHexDigit);
    private static bool IsValidationException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException
            or JsonException or FormatException or ArgumentException or OverflowException;
}
