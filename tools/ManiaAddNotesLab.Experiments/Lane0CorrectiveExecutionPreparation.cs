using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;

internal sealed record Lane0CorrectiveExecutionPublicationBinding(
    string SchemaVersion,
    string ApprovedPublishedHead,
    string ExecutionPreparationContractSha256,
    string PreregistrationCanonicalSha256,
    string EvaluatorHardeningCanonicalSha256,
    string ExpectedC11ManifestSha256,
    bool ExplicitHumanAuthorization,
    int AuthorizedExecutionCount);

internal sealed record Lane0CorrectivePublishedState(
    string CurrentHead,
    string ExecutionPreparationContractSha256,
    Lane0FrozenSnapshot HistoricalSnapshot,
    Lane0HistoricalCaseRule HistoricalCaseRule);

internal sealed record Lane0CorrectiveExecutionRequest(
    string RepositoryRoot,
    string BindingPath,
    string PreparationContractPath,
    string HardeningContractPath,
    string ManifestPath,
    string CorpusRoot,
    string OutputDirectory);

internal sealed record Lane0CorrectiveExecutionPreparationResult(
    string Outcome,
    string Reason,
    bool CorpusAccessPerformed,
    bool CorrectiveEvaluationExecuted);

internal interface ILane0CorrectiveCorpusAdapter
{
    int AccessCalls { get; }
    ImmutableArray<Lane0CorrectiveChartInput> Load();
}

/// <summary>
/// Explicit-root C11 adapter. The execution gate is responsible for ensuring that Load is never called
/// before publication identity and human authorization have passed.
/// </summary>
internal sealed class Lane0FrozenCorrectiveCorpusAdapter : ILane0CorrectiveCorpusAdapter
{
    private readonly string _manifestPath;
    private readonly string _corpusRoot;
    public int AccessCalls { get; private set; }

    public Lane0FrozenCorrectiveCorpusAdapter(string manifestPath, string corpusRoot)
    {
        _manifestPath = Path.GetFullPath(manifestPath);
        _corpusRoot = Path.GetFullPath(corpusRoot);
    }

    public ImmutableArray<Lane0CorrectiveChartInput> Load()
    {
        AccessCalls++;
        var manifest = FrozenC11ManifestResearch.Load(_manifestPath);
        if (!string.Equals(manifest.CanonicalSha256,
                Lane0CorrectiveExecutionPreparation.ExpectedManifestSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Frozen C11 manifest identity drifted.");
        var verification = C11CorpusDiscovery.ResolveFrozen(_corpusRoot, manifest.Charts);
        return BuildInputs(verification, path => File.ReadAllText(path, Encoding.UTF8));
    }

    internal static ImmutableArray<Lane0CorrectiveChartInput> BuildInputs(
        C11FrozenCorpusVerification verification, Func<string, string> readText)
    {
        ArgumentNullException.ThrowIfNull(verification);
        ArgumentNullException.ThrowIfNull(readText);
        ValidateInventory(verification);
        return verification.Charts.OrderBy(x => x.Sha256, StringComparer.Ordinal).Select(descriptor =>
        {
            var chart = OsuBeatmap.Parse(readText(descriptor.RuntimePath));
            if (chart.KeyCount != descriptor.KeyCount
                || chart.OriginalObjects.Count != descriptor.ObjectCount)
                throw new InvalidDataException($"Adapter parse drifted for {descriptor.Sha256}.");
            return new Lane0CorrectiveChartInput(descriptor.Sha256, chart);
        }).ToImmutableArray();
    }

    internal static void ValidateInventory(C11FrozenCorpusVerification verification)
    {
        if (!verification.IsExact)
            throw new InvalidDataException("Frozen corpus has missing, unexpected, invalid or mismatched content.");
        if (verification.OsuFileCount != 12 || verification.Charts.Length != 11)
            throw new InvalidDataException(
                $"Frozen corpus cardinality drifted: locations={verification.OsuFileCount}, unique={verification.Charts.Length}.");
        var duplicateLocations = verification.Duplicates.Sum(x => x.RelativePaths.Length - 1);
        if (verification.Duplicates.Length != 1 || duplicateLocations != 1)
            throw new InvalidDataException("Frozen corpus duplicate-location cardinality drifted.");
        if (verification.Charts.Select(x => x.FamilyKey).Distinct(StringComparer.Ordinal).Count() != 11)
            throw new InvalidDataException("Frozen corpus family cardinality drifted.");
        if (!verification.Charts.Select(x => x.KeyCount).Distinct().Order().SequenceEqual([4, 7, 10]))
            throw new InvalidDataException("Frozen corpus keymode inventory drifted.");
        if (verification.Charts.Select(x => x.Sha256).Distinct(StringComparer.Ordinal).Count() != 11)
            throw new InvalidDataException("Frozen corpus chart identity is not unique.");
    }
}

/// <summary>
/// Successor-only execution preparation. It has no CLI/Web/production call site and cannot access the corpus
/// until the canonical publication binding passes every pre-corpus guard.
/// </summary>
internal static class Lane0CorrectiveExecutionPreparation
{
    internal const string BindingSchema = "lane-0-corrective-evaluation-publication-binding.1";
    internal const string CanonicalBindingPath =
        "docs/lane_0_corrective_evaluation_publication_binding.json";
    internal const string BaselineHead = "9b065535248e7dd7581822af80cf6534b16922eb";
    internal const string PreregistrationSha256 =
        "6392C579B87EC318C1DE381201D797B004650AB961398E3E2AB23F7A49A83D33";
    internal const string HardeningSha256 =
        "A61F0933A49534857096D3A568E9C07F37805F9468CC94001546767073CD2E18";
    internal const string ExpectedManifestSha256 =
        "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    internal const string OfficialOutputPath = ".artifacts/lane_0_corrective";

    public static Lane0CorrectiveExecutionPreparationResult Execute(
        Lane0CorrectiveExecutionRequest request, ILane0CorrectiveCorpusAdapter? adapter = null)
    {
        Lane0CorrectivePublishedState state;
        try
        {
            state = LoadPublishedState(request.RepositoryRoot, request.PreparationContractPath,
                request.HardeningContractPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or JsonException or FormatException)
        {
            return new("INVALID", exception.Message, false, false);
        }

        var expectedBindingPath = Path.GetFullPath(Path.Combine(request.RepositoryRoot,
            CanonicalBindingPath));
        var expectedOutputPath = Path.GetFullPath(Path.Combine(request.RepositoryRoot,
            OfficialOutputPath));
        if (!string.Equals(Path.GetFullPath(request.BindingPath), expectedBindingPath,
                StringComparison.OrdinalIgnoreCase))
            return new("INVALID", "Only the canonical publication binding path is accepted.", false, false);
        if (!string.Equals(Path.GetFullPath(request.OutputDirectory), expectedOutputPath,
                StringComparison.OrdinalIgnoreCase))
            return new("INVALID", "Only the frozen corrective output path is accepted.", false, false);

        string? bindingJson = null;
        if (File.Exists(request.BindingPath))
        {
            try { bindingJson = File.ReadAllText(request.BindingPath, Encoding.UTF8); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            { return new("INVALID", exception.Message, false, false); }
        }

        adapter ??= new Lane0FrozenCorrectiveCorpusAdapter(request.ManifestPath, request.CorpusRoot);
        return ExecuteAfterPublishedIdentity(state, bindingJson, adapter, request.OutputDirectory);
    }

    internal static Lane0CorrectiveExecutionPreparationResult ExecuteAfterPublishedIdentity(
        Lane0CorrectivePublishedState state, string? bindingJson,
        ILane0CorrectiveCorpusAdapter adapter, string outputDirectory)
    {
        var authorization = ValidateBinding(state, bindingJson);
        if (authorization.Outcome != "AUTHORIZED")
            return new(authorization.Outcome, authorization.Reason, adapter.AccessCalls != 0, false);

        ImmutableArray<Lane0CorrectiveChartInput> firstInputs;
        ImmutableArray<Lane0CorrectiveChartInput> secondInputs;
        try
        {
            firstInputs = adapter.Load();
            secondInputs = adapter.Load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or JsonException or FormatException)
        {
            return new("INVALID", exception.Message, adapter.AccessCalls != 0, false);
        }

        var options = new Lane0CorrectiveEvaluationOptions(true,
            state.HistoricalSnapshot, state.HistoricalCaseRule);
        var first = Lane0CorrectiveEvaluationRunner.Evaluate(firstInputs, options);
        var second = Lane0CorrectiveEvaluationRunner.Evaluate(secondInputs, options);
        var firstArtifacts = Lane0CorrectiveEvaluationRunner.SerializeArtifacts(first);
        var secondArtifacts = Lane0CorrectiveEvaluationRunner.SerializeArtifacts(second);
        if (!ArtifactsEqual(firstArtifacts, secondArtifacts))
            return new("INVALID", "Independent corrective evaluations were not byte-identical.", true, true);
        if (first.ResearchRngCalls != 0 || second.ResearchRngCalls != 0
            || !first.InputsUnchanged || !second.InputsUnchanged
            || first.BehaviorChanged || second.BehaviorChanged)
            return new("INVALID", "Corrective evaluation violated non-interference.", true, true);
        if (first.Outcome is "INVALID" or "BLOCKED")
            return new(first.Outcome, "Corrective evaluator did not produce a writable scientific outcome.",
                true, true);

        try { Lane0CorrectiveEvaluationRunner.WriteArtifacts(first, outputDirectory); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException)
        { return new("INVALID", exception.Message, true, true); }
        return new(first.Outcome, "One authorized corrective evaluation completed.", true, true);
    }

    internal static Lane0CorrectiveExecutionPreparationResult ValidateBinding(
        Lane0CorrectivePublishedState state, string? bindingJson)
    {
        if (bindingJson is null)
            return new("BLOCKED", "Canonical publication binding is absent.", false, false);
        Lane0CorrectiveExecutionPublicationBinding binding;
        try { binding = ParseBinding(bindingJson); }
        catch (Exception exception) when (exception is JsonException or InvalidDataException
            or FormatException)
        { return new("INVALID", exception.Message, false, false); }

        if (!string.Equals(binding.ExecutionPreparationContractSha256,
                state.ExecutionPreparationContractSha256, StringComparison.Ordinal))
            return new("INVALID", "Execution-preparation contract identity drifted.", false, false);
        if (!string.Equals(binding.PreregistrationCanonicalSha256,
                PreregistrationSha256, StringComparison.Ordinal))
            return new("INVALID", "Preregistration identity drifted.", false, false);
        if (!string.Equals(binding.EvaluatorHardeningCanonicalSha256,
                HardeningSha256, StringComparison.Ordinal))
            return new("INVALID", "Evaluator hardening identity drifted.", false, false);
        if (!string.Equals(binding.ExpectedC11ManifestSha256,
                ExpectedManifestSha256, StringComparison.Ordinal))
            return new("INVALID", "Expected manifest identity drifted.", false, false);
        if (!IsGitHead(binding.ApprovedPublishedHead)
            || !string.Equals(binding.ApprovedPublishedHead, state.CurrentHead, StringComparison.OrdinalIgnoreCase))
            return new("INVALID", "Approved published HEAD does not match current HEAD.", false, false);
        if (!binding.ExplicitHumanAuthorization)
            return new("BLOCKED", "Explicit human authorization is false.", false, false);
        if (binding.AuthorizedExecutionCount != 1)
            return new("INVALID", "Authorized execution count must equal one.", false, false);
        return new("AUTHORIZED", "All pre-corpus authorization guards passed.", false, false);
    }

    internal static Lane0CorrectiveExecutionPublicationBinding ParseBinding(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        RequireShape(root, "publication binding", "schemaVersion", "approvedPublishedHead",
            "executionPreparationContractSha256", "preregistrationCanonicalSha256",
            "evaluatorHardeningCanonicalSha256", "expectedC11ManifestSha256",
            "explicitHumanAuthorization", "authorizedExecutionCount");
        var result = new Lane0CorrectiveExecutionPublicationBinding(
            RequiredString(root, "schemaVersion"), RequiredString(root, "approvedPublishedHead"),
            RequiredString(root, "executionPreparationContractSha256"),
            RequiredString(root, "preregistrationCanonicalSha256"),
            RequiredString(root, "evaluatorHardeningCanonicalSha256"),
            RequiredString(root, "expectedC11ManifestSha256"),
            root.GetProperty("explicitHumanAuthorization").GetBoolean(),
            root.GetProperty("authorizedExecutionCount").GetInt32());
        if (!string.Equals(result.SchemaVersion, BindingSchema, StringComparison.Ordinal))
            throw new InvalidDataException("Publication binding schema is invalid.");
        foreach (var hash in new[] { result.ExecutionPreparationContractSha256,
                     result.PreregistrationCanonicalSha256, result.EvaluatorHardeningCanonicalSha256,
                     result.ExpectedC11ManifestSha256 })
            if (!IsSha(hash)) throw new InvalidDataException("Publication binding contains malformed SHA-256.");
        return result;
    }

    internal static Lane0CorrectivePublishedState LoadPublishedState(string root,
        string preparationContractPath, string hardeningContractPath)
    {
        root = Path.GetFullPath(root);
        var head = ReadHead(root);
        using var prep = JsonDocument.Parse(File.ReadAllText(preparationContractPath, Encoding.UTF8));
        RequireShape(prep.RootElement, "execution-preparation contract artifact",
            "contract", "canonicalSha256", "canonicalization");
        var prepCanonical = CanonicalJsonHash(prep.RootElement.GetProperty("contract"));
        if (!string.Equals(prepCanonical, RequiredString(prep.RootElement, "canonicalSha256"),
                StringComparison.Ordinal))
            throw new InvalidDataException("Execution-preparation contract canonical hash drifted.");
        ValidatePreparationContract(root, prep.RootElement.GetProperty("contract"));

        using var hardening = JsonDocument.Parse(File.ReadAllText(hardeningContractPath, Encoding.UTF8));
        var hardeningCanonical = CanonicalJsonHash(hardening.RootElement.GetProperty("contract"));
        if (!string.Equals(hardeningCanonical, HardeningSha256, StringComparison.Ordinal)
            || !string.Equals(RequiredString(hardening.RootElement, "canonicalSha256"),
                HardeningSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Evaluator hardening contract identity drifted.");
        var contract = hardening.RootElement.GetProperty("contract");
        var snapshot = ParseSnapshot(contract.GetProperty("frozenHistoricalSnapshot"));
        var rule = ParseCaseRule(contract.GetProperty("frozenHistoricalG1CaseRule"));
        if (snapshot != Lane0CorrectiveEvaluationRunner.FrozenHistoricalSnapshot
            || !RuleEquals(rule, Lane0CorrectiveEvaluationRunner.FrozenHistoricalG1CaseRule))
            throw new InvalidDataException("Published scientific inputs do not reproduce evaluator constants.");
        return new(head, prepCanonical, snapshot, rule);
    }

    private static void ValidatePreparationContract(string root, JsonElement contract)
    {
        static void Equal(string actual, string expected, string label)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new InvalidDataException($"Execution-preparation {label} drifted.");
        }

        Equal(RequiredString(contract, "schemaVersion"),
            "lane-0-corrective-execution-preparation.1", "schema");
        Equal(RequiredString(contract, "baselinePublishedHead"), BaselineHead, "baseline");
        Equal(RequiredString(contract, "preregistrationCanonicalSha256"),
            PreregistrationSha256, "preregistration identity");
        Equal(RequiredString(contract, "evaluatorHardeningCanonicalSha256"),
            HardeningSha256, "hardening identity");
        Equal(RequiredString(contract, "evaluatorImplementationSha256"),
            "20D0AC6BB0AF197ACC6BE1B5E71D1DF65BD2CDC0FCAB0E49FD6F036D03A0D8F4",
            "evaluator implementation identity");
        Equal(RequiredString(contract, "evaluatorHarnessSha256"),
            "724837E42F2308DA3159BAE00F3339656C03EE936B9E350F416124A7E92AA173",
            "evaluator harness identity");
        Equal(RequiredString(contract, "solutionNormalizedSha256"),
            "BFEB1689414A2FB712170A967B9BA6DAD70BFE656D307A25821A976D81942A37",
            "solution identity");
        Equal(RequiredString(contract, "expectedC11ManifestSha256"),
            ExpectedManifestSha256, "manifest identity");
        Equal(RequiredString(contract, "bindingSchema"), BindingSchema, "binding schema");
        Equal(RequiredString(contract, "bindingCanonicalPath"), CanonicalBindingPath,
            "binding path");
        Equal(RequiredString(contract, "officialOutputPath"), OfficialOutputPath,
            "output path");
        if (contract.GetProperty("bindingPresent").GetBoolean()
            || contract.GetProperty("humanAuthorization").GetBoolean()
            || contract.GetProperty("corpusAccessPerformed").GetBoolean()
            || contract.GetProperty("correctiveEvaluationExecuted").GetBoolean()
            || contract.GetProperty("successorAuthorization").GetBoolean())
            throw new InvalidDataException("Execution-preparation safety state is not closed.");
        if (contract.GetProperty("authorizedExecutionCount").GetInt32() != 1)
            throw new InvalidDataException("Execution-preparation one-shot policy drifted.");

        var implementationFiles = ReadStringArray(contract, "executionPreparationImplementationFiles");
        var harnessFiles = ReadStringArray(contract, "successorHarnessFiles");
        Equal(TreeIdentity(root, implementationFiles),
            RequiredString(contract, "executionPreparationImplementationSha256"),
            "implementation tree identity");
        Equal(TreeIdentity(root, harnessFiles), RequiredString(contract, "successorHarnessSha256"),
            "harness tree identity");
        Equal(NormalizedFileHash(Path.Combine(root,
                RequiredString(contract, "solutionPath"))),
            RequiredString(contract, "solutionNormalizedSha256"), "solution working identity");

        var preregistrationPath = Path.Combine(root,
            RequiredString(contract, "preregistrationContractPath"));
        using var preregistration = JsonDocument.Parse(File.ReadAllText(preregistrationPath, Encoding.UTF8));
        Equal(CanonicalJsonHash(preregistration.RootElement.GetProperty("contract")),
            PreregistrationSha256, "preregistration working identity");
    }

    private static string[] ReadStringArray(JsonElement value, string property) =>
        value.GetProperty(property).EnumerateArray().Select(x => x.GetString()
            ?? throw new InvalidDataException($"{property} contains null.")).ToArray();

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

    private static Lane0FrozenSnapshot ParseSnapshot(JsonElement value) => new(
        ParseFamily(value.GetProperty("rice")), ParseFamily(value.GetProperty("g1")));

    private static Lane0FrozenFamilySnapshot ParseFamily(JsonElement value) => new(
        value.GetProperty("structural").GetInt32(), value.GetProperty("comparableContext").GetInt32(),
        value.GetProperty("jointSupport").GetInt32(), value.GetProperty("jointUnique").GetInt32(),
        value.GetProperty("jointAmongAlternatives").GetInt32(), value.GetProperty("contradiction").GetInt32(),
        value.GetProperty("noContext").GetInt32(), value.GetProperty("operational").GetInt32(),
        value.GetProperty("operationalSupported").GetInt32(), value.GetProperty("supportedCharts").GetInt32(),
        value.GetProperty("supportedKeymodes").GetInt32());

    private static Lane0HistoricalCaseRule ParseCaseRule(JsonElement value)
    {
        var distribution = value.GetProperty("chartDistribution").EnumerateObject()
            .ToImmutableSortedDictionary(x => x.Name, x => x.Value.GetInt32(), StringComparer.Ordinal);
        return new(value.GetProperty("total").GetInt32(), distribution,
            value.GetProperty("keymode").GetInt32());
    }

    private static bool RuleEquals(Lane0HistoricalCaseRule left, Lane0HistoricalCaseRule right) =>
        left.ExpectedCount == right.ExpectedCount && left.ExpectedKeymode == right.ExpectedKeymode
        && left.ExpectedChartDistribution.SequenceEqual(right.ExpectedChartDistribution);

    private static bool ArtifactsEqual(ImmutableSortedDictionary<string, byte[]> left,
        ImmutableSortedDictionary<string, byte[]> right) => left.Keys.SequenceEqual(right.Keys)
        && left.All(pair => pair.Value.AsSpan().SequenceEqual(right[pair.Key]));

    internal static string CanonicalJsonHash(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, value);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
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

    private static string ReadHead(string root)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("rev-parse"); start.ArgumentList.Add("HEAD");
        using var process = Process.Start(start) ?? throw new InvalidDataException("Cannot read Git HEAD.");
        var value = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit();
        if (process.ExitCode != 0 || !IsGitHead(value)) throw new InvalidDataException("Cannot read Git HEAD.");
        return value;
    }

    private static bool IsSha(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static bool IsGitHead(string value) => value.Length == 40 && value.All(Uri.IsHexDigit);

    private static string RequiredString(JsonElement value, string property) =>
        value.GetProperty(property).GetString()
        ?? throw new InvalidDataException($"{property} is null.");

    private static void RequireShape(JsonElement value, string context, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"{context} must be an object.");
        var actual = value.EnumerateObject().Select(x => x.Name).ToArray();
        var missing = expected.Except(actual, StringComparer.Ordinal).ToArray();
        var extra = actual.Except(expected, StringComparer.Ordinal).ToArray();
        if (missing.Length != 0 || extra.Length != 0 || actual.Length != expected.Length)
            throw new InvalidDataException($"{context} shape drifted; missing=[{string.Join(',', missing)}], extra=[{string.Join(',', extra)}].");
    }
}
