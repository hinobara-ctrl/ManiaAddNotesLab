using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;

internal sealed record Lane0IntegrationRuntimeIdentities(
    string Phase1ContractSha256, string Phase2ContractSha256,
    string IntegrationPreregistrationSha256, string IntegrationExecutionContractSha256,
    string ManifestSha256, string ScientificImplementationSha256,
    string Phase2HarnessSha256, string Phase2PackageVerifierSha256,
    string AdapterSha256, string RunnerSha256, string LauncherSha256,
    string SemanticVerifierSha256, string FrozenDependenciesSha256);

internal sealed record Lane0IntegrationBindingFields(
    string SchemaVersion, string ApprovedPublicHead,
    Lane0IntegrationRuntimeIdentities Identities,
    bool ExplicitHumanAuthorization, int AuthorizedExecutionCount);

internal sealed record Lane0IntegrationVerifiedBinding(
    ImmutableArray<byte> CanonicalBytes, string CanonicalSha256,
    Lane0IntegrationBindingFields Fields);

internal sealed record Lane0IntegrationLaunchRequest(
    string ExpectedSourcePublicHead,
    Lane0IntegrationRuntimeIdentities ExpectedIdentities,
    Lane0IntegrationVerifiedBinding? Binding,
    string SourceRoot, string ExecutionRoot,
    string CorpusRootToken);

internal sealed record Lane0IntegrationOfficialLaunchRequest(
    string ExpectedSourcePublicHead,
    Lane0IntegrationRuntimeIdentities ExpectedIdentities,
    string SourceRoot, string ExecutionRoot,
    string CorpusRootToken);

internal sealed record Lane0IntegrationPreReceiptLease(
    Lane0IntegrationLaunchRequest Request, string CanonicalSourceRoot,
    string CanonicalExecutionRoot, string CanonicalBindingSha256,
    Lane0IntegrationRuntimeIdentities ObservedIdentities,
    Lane0IntegrationPreparedRuntime PreparedRuntime,
    ImmutableArray<string> CompletedAuthoritySteps);

internal sealed record Lane0IntegrationPostReceiptLease(
    Lane0IntegrationPreReceiptLease PreReceipt, string CanonicalCorpusRoot,
    ImmutableArray<string> CompletedAuthoritySteps);

internal interface ILane0IntegrationAuthorityStore
{
    bool ReceiptExists { get; }
    bool OutputOrStagingExists { get; }
    void CreateDurableReceipt(byte[] bytes);
}

internal sealed record Lane0IntegrationCheckoutEvidence(
    string Head, bool Detached, bool TrackedClean, bool UsedHardlinks,
    bool ReusedSourceBuildOutputs);

internal interface ILane0IntegrationCheckoutMaterializer
{
    Lane0IntegrationCheckoutEvidence CreateDetachedCheckout(
        string canonicalSourceRoot, string canonicalExecutionRoot, string exactHead);
}

internal interface ILane0IntegrationAuthorityObserver
{
    string ObserveHead(string canonicalRepositoryRoot);
    bool ObserveTrackedClean(string canonicalRepositoryRoot);
    Lane0IntegrationRuntimeIdentities ObserveRuntimeIdentities(string canonicalRepositoryRoot);
}

/// <summary>
/// Authority gate for a future isolated run. CorpusRoot is not interpreted or passed to an
/// external collaborator until after CreateDurableReceipt succeeds.
/// </summary>
internal sealed class Lane0CorrectiveSuccessorIsolatedLauncher
{
    private readonly ILane0IntegrationCheckoutMaterializer checkoutMaterializer;
    private readonly ILane0IntegrationAuthorityObserver observer;
    private readonly ILane0IntegrationIsolatedScientificRuntime scientificRuntime;

    internal Lane0CorrectiveSuccessorIsolatedLauncher(
        ILane0IntegrationCheckoutMaterializer checkoutMaterializer,
        ILane0IntegrationAuthorityObserver observer,
        ILane0IntegrationIsolatedScientificRuntime scientificRuntime)
    {
        this.checkoutMaterializer = checkoutMaterializer;
        this.observer = observer;
        this.scientificRuntime = scientificRuntime;
    }

    internal static Lane0CorrectiveSuccessorIsolatedLauncher CreateOfficial() =>
        new(new GitDetachedCheckoutMaterializer(), new GitAndFileIntegrationAuthorityObserver(),
            new Lane0ExecutionRootScientificRuntime());

    internal const string BindingSchema =
        "lane-0-corrective-successor-integration-publication-binding.1";
    internal const string ReceiptSchema =
        "lane-0-corrective-successor-integration-attempt.1";
    internal const string HistoricalImplementationBaseline =
        "94bba4825e1a3fafbcb44940b63bbd3e9b8b221c";

    internal static readonly ImmutableArray<string> AuthorityOrder =
    [
        "SOURCE_PUBLIC_HEAD_CHECKS", "CONTRACT_IDENTITIES", "RUNTIME_IDENTITIES",
        "CANONICAL_BINDING_PRESENCE", "CANONICAL_BINDING_BYTE_AND_HASH_VERIFICATION",
        "EXPLICIT_AUTHORIZATION_FIELD", "AUTHORIZED_EXECUTION_COUNT_ONE",
        "SOURCE_TRACKED_CLEANLINESS", "SOURCE_EXECUTION_RUNTIME_HEAD_EQUALITY",
        "SOURCE_EXECUTION_ROOT_ISOLATION", "DURABLE_RECEIPT_ABSENCE",
        "OUTPUT_AND_STAGING_ABSENCE", "DURABLE_RECEIPT_CREATION",
        "CORPUSROOT_INTERPRETATION", "CORPUS_SOURCE_EXECUTION_ROOT_ISOLATION",
        "CORPUS_ADMISSION", "SCIENTIFIC_PASS_1", "SCIENTIFIC_PASS_2",
        "BYTE_DETERMINISM", "DEEP_SEMANTIC_VERIFICATION",
        "FINAL_PUBLISHABLE_OR_NON_PUBLISHABLE_CLASSIFICATION"
    ];

    internal Lane0IntegrationPreReceiptLease Acquire(
        Lane0IntegrationLaunchRequest request, ILane0IntegrationAuthorityStore store)
    {
        var binding = request.Binding ?? throw new InvalidDataException("Synthetic binding seam is absent.");
        return AcquireCore(request, binding, store);
    }

    internal Lane0IntegrationPreReceiptLease AcquireOfficial(Lane0IntegrationOfficialLaunchRequest official)
    {
        var completed = ImmutableArray.CreateBuilder<string>();
        var source = Path.GetFullPath(official.SourceRoot);
        var execution = Path.GetFullPath(official.ExecutionRoot);
        var paths = Lane0IntegrationOfficialLayout.Derive(source, execution);
        var observedHead = observer.ObserveHead(source);
        Require(observedHead == official.ExpectedSourcePublicHead,
            "Source public HEAD does not equal the expected audited integration HEAD.");
        Require(official.ExpectedSourcePublicHead != HistoricalImplementationBaseline,
            "The implementation baseline is not eligible as an execution binding target.");
        completed.Add(AuthorityOrder[0]);
        Require(official.ExpectedIdentities.IntegrationPreregistrationSha256 ==
            "7C0A86BF3C6647CA7092C4D3390EB97EA6E11D7E5429ECBA6F499D55C5417A02",
            "Integration preregistration identity drifted.");
        completed.Add(AuthorityOrder[1]);
        var observedIdentities = observer.ObserveRuntimeIdentities(source);
        Require(official.ExpectedIdentities == observedIdentities,
            "Observed runtime identities do not equal the frozen integration identities.");
        completed.Add(AuthorityOrder[2]);
        completed.Add(AuthorityOrder[3]);
        // Host loading is only an early fail-closed preflight. The ExecutionRoot-built worker
        // independently reloads these fixed bytes and is the process that creates authority.
        var binding = new Lane0IntegrationCanonicalBindingLoader().Load(paths.CanonicalBindingPath);
        ValidateBinding(binding, official.ExpectedSourcePublicHead, official.ExpectedIdentities);
        completed.Add(AuthorityOrder[4]);
        Require(binding.Fields.ExplicitHumanAuthorization, "Explicit authorization is false.");
        completed.Add(AuthorityOrder[5]);
        Require(binding.Fields.AuthorizedExecutionCount == 1,
            "Authorized execution count must equal one.");
        completed.Add(AuthorityOrder[6]);
        Require(observer.ObserveTrackedClean(source), "Source tracked tree is dirty.");
        completed.Add(AuthorityOrder[7]);
        Require(Path.IsPathFullyQualified(official.ExecutionRoot), "ExecutionRoot must be absolute.");
        Require(RootsAreLexicallyIsolated(source, execution),
            "SourceRoot and ExecutionRoot must be lexically disjoint and nonnested.");
        Require(!Directory.Exists(execution) && !File.Exists(execution),
            "ExecutionRoot must initially be nonexistent.");
        var checkout = checkoutMaterializer.CreateDetachedCheckout(source, execution,
            official.ExpectedSourcePublicHead);
        Require(checkout.Head == official.ExpectedSourcePublicHead && checkout.Detached
            && checkout.TrackedClean && !checkout.UsedHardlinks && !checkout.ReusedSourceBuildOutputs,
            "Isolated checkout evidence does not prove exact detached pristine no-hardlink materialization.");
        var prepared = scientificRuntime.Prepare(execution, official.ExpectedSourcePublicHead);
        Require(prepared.Provenance.RuntimeHead == observedHead,
            "Binding/source/isolated/authority-runtime HEAD equality failed.");
        completed.Add(AuthorityOrder[8]);
        completed.Add(AuthorityOrder[9]);
        var store = new Lane0IntegrationFileAuthorityStore(paths.DurableReceiptPath,
            paths.FinalArtifactsRoot, paths.StagingRoot);
        Require(!store.ReceiptExists, "Durable receipt already exists.");
        completed.Add(AuthorityOrder[10]);
        Require(!store.OutputOrStagingExists, "Output or staging already exists.");
        completed.Add(AuthorityOrder[11]);
        var authority = scientificRuntime.CreateOfficialAuthorityReceipt(prepared, source,
            official.ExpectedSourcePublicHead, official.ExpectedIdentities);
        Require(authority.CanonicalBindingSha256 == binding.CanonicalSha256,
            "Authority worker binding identity disagrees with canonical preflight bytes.");
        completed.Add(AuthorityOrder[12]);
        var request = new Lane0IntegrationLaunchRequest(official.ExpectedSourcePublicHead,
            official.ExpectedIdentities, null, official.SourceRoot, official.ExecutionRoot,
            official.CorpusRootToken);
        return new(request, source, execution, authority.CanonicalBindingSha256,
            authority.ObservedIdentities, prepared, completed.ToImmutable());
    }

    private Lane0IntegrationPreReceiptLease AcquireCore(
        Lane0IntegrationLaunchRequest request, Lane0IntegrationVerifiedBinding binding,
        ILane0IntegrationAuthorityStore store)
    {
        var completed = ImmutableArray.CreateBuilder<string>();
        var source = Path.GetFullPath(request.SourceRoot);
        var observedSourceHead = observer.ObserveHead(source);
        Require(observedSourceHead == request.ExpectedSourcePublicHead,
            "Source public HEAD does not equal the expected audited integration HEAD.");
        Require(request.ExpectedSourcePublicHead != HistoricalImplementationBaseline,
            "The implementation baseline is not eligible as an execution binding target.");
        completed.Add(AuthorityOrder[0]);
        Require(request.ExpectedIdentities.IntegrationPreregistrationSha256 ==
            "7C0A86BF3C6647CA7092C4D3390EB97EA6E11D7E5429ECBA6F499D55C5417A02",
            "Integration preregistration identity drifted.");
        completed.Add(AuthorityOrder[1]);
        var observedIdentities = observer.ObserveRuntimeIdentities(source);
        Require(request.ExpectedIdentities == observedIdentities,
            "Observed runtime identities do not equal the frozen integration identities.");
        completed.Add(AuthorityOrder[2]);
        completed.Add(AuthorityOrder[3]);
        var bindingHash = ValidateBinding(binding, request.ExpectedSourcePublicHead,
            request.ExpectedIdentities);
        completed.Add(AuthorityOrder[4]);
        Require(binding.Fields.ExplicitHumanAuthorization, "Explicit authorization is false.");
        completed.Add(AuthorityOrder[5]);
        Require(binding.Fields.AuthorizedExecutionCount == 1,
            "Authorized execution count must equal one.");
        completed.Add(AuthorityOrder[6]);
        Require(observer.ObserveTrackedClean(source), "Source tracked tree is dirty.");
        completed.Add(AuthorityOrder[7]);
        Require(Path.IsPathFullyQualified(request.ExecutionRoot), "ExecutionRoot must be absolute.");
        var execution = Path.GetFullPath(request.ExecutionRoot);
        Require(RootsAreLexicallyIsolated(source, execution),
            "SourceRoot and ExecutionRoot must be lexically disjoint and nonnested.");
        Require(!Directory.Exists(execution) && !File.Exists(execution),
            "ExecutionRoot must initially be nonexistent.");
        var checkout = checkoutMaterializer.CreateDetachedCheckout(source, execution,
            request.ExpectedSourcePublicHead);
        Require(checkout.Head == request.ExpectedSourcePublicHead && checkout.Detached
            && checkout.TrackedClean && !checkout.UsedHardlinks
            && !checkout.ReusedSourceBuildOutputs,
            "Isolated checkout evidence does not prove exact detached pristine no-hardlink materialization.");
        var preparedRuntime = scientificRuntime.Prepare(execution, request.ExpectedSourcePublicHead);
        Require(preparedRuntime.Provenance.RuntimeHead == observedSourceHead,
            "Binding/source/isolated/runtime HEAD equality failed.");
        completed.Add(AuthorityOrder[8]);
        completed.Add(AuthorityOrder[9]);
        Require(!store.ReceiptExists, "Durable receipt already exists.");
        completed.Add(AuthorityOrder[10]);
        Require(!store.OutputOrStagingExists, "Output or staging already exists.");
        completed.Add(AuthorityOrder[11]);

        store.CreateDurableReceipt(BuildReceipt(request, bindingHash));
        completed.Add(AuthorityOrder[12]);
        return new(request, source, execution, bindingHash, observedIdentities,
            preparedRuntime, completed.ToImmutable());
    }

    internal Lane0IntegrationScientificRuntimeResult ExecuteScience(
        Lane0IntegrationPreReceiptLease lease,
        ImmutableArray<Lane0CorrectiveChartInput> inputs,
        Lane0SuccessorEvaluationContext context) =>
        scientificRuntime.Execute(lease.PreparedRuntime, inputs, context,
            lease.ObservedIdentities);

    internal Lane0IntegrationPostReceiptLease InterpretCorpusRoot(Lane0IntegrationPreReceiptLease lease)
    {
        var completed = lease.CompletedAuthoritySteps.ToBuilder();
        var corpus = Path.GetFullPath(lease.Request.CorpusRootToken);
        completed.Add(AuthorityOrder[13]);
        Require(RootsAreLexicallyIsolated(lease.CanonicalSourceRoot, corpus)
            && RootsAreLexicallyIsolated(lease.CanonicalExecutionRoot, corpus),
            "CorpusRoot must be lexically disjoint from source and execution roots.");
        completed.Add(AuthorityOrder[14]);
        return new(lease, corpus, completed.ToImmutable());
    }

    internal static Lane0IntegrationVerifiedBinding CreateSyntheticVerifiedBinding(
        Lane0IntegrationBindingFields fields)
    {
        var bytes = CanonicalBindingBytes(fields);
        return new(bytes.ToImmutableArray(), Convert.ToHexString(SHA256.HashData(bytes)), fields);
    }

    internal static bool RootsAreLexicallyIsolated(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
        var b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
        if (string.Equals(a, b, comparison)) return false;
        return !IsAncestor(a, b, comparison) && !IsAncestor(b, a, comparison);
    }

    internal static byte[] CanonicalBindingBytes(Lane0IntegrationBindingFields fields) =>
        [.. JsonSerializer.SerializeToUtf8Bytes(fields, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        }), (byte)'\n'];

    internal static byte[] BuildReceipt(Lane0IntegrationLaunchRequest request, string bindingHash) =>
        [.. JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = ReceiptSchema,
            approvedPublicHead = request.ExpectedSourcePublicHead,
            canonicalBindingSha256 = bindingHash,
            integrationPreregistrationSha256 = request.ExpectedIdentities.IntegrationPreregistrationSha256,
            integrationExecutionContractSha256 = request.ExpectedIdentities.IntegrationExecutionContractSha256,
            attemptCount = 1
        }, new JsonSerializerOptions { WriteIndented = true }), (byte)'\n'];

    private static string ValidateBinding(Lane0IntegrationVerifiedBinding binding,
        string expectedHead, Lane0IntegrationRuntimeIdentities expectedIdentities)
    {
        var canonical = CanonicalBindingBytes(binding.Fields);
        Require(binding.CanonicalBytes.AsSpan().SequenceEqual(canonical),
            "Canonical binding bytes drifted.");
        var bindingHash = Convert.ToHexString(SHA256.HashData(canonical));
        Require(binding.CanonicalSha256 == bindingHash, "Canonical binding hash drifted.");
        Require(binding.Fields.SchemaVersion == BindingSchema, "Canonical binding schema drifted.");
        Require(binding.Fields.ApprovedPublicHead == expectedHead, "Binding HEAD drifted.");
        Require(binding.Fields.Identities == expectedIdentities,
            "Binding runtime identities drifted.");
        return bindingHash;
    }

    private static bool IsAncestor(string parent, string child, StringComparison comparison) =>
        child.StartsWith(parent + Path.DirectorySeparatorChar, comparison)
        || child.StartsWith(parent + Path.AltDirectorySeparatorChar, comparison);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}

internal sealed class GitAndFileIntegrationAuthorityObserver : ILane0IntegrationAuthorityObserver
{
    private static readonly string[] ScientificFiles =
    [
        "src/ManiaAddNotesLab.Core/Lane0CorrectiveSuccessorReferenceValidator.cs",
        "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorEvaluator.cs"
    ];
    private static readonly string[] HarnessFiles =
        ["tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorExecutionPreparation.cs"];
    private static readonly string[] PackageFiles =
        ["tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorPackage.cs"];
    private static readonly string[] DependencyFiles =
    [
        "src/ManiaAddNotesLab.Core/ChordCompletionResearch.cs",
        "src/ManiaAddNotesLab.Core/FrozenC11ManifestResearch.cs",
        "src/ManiaAddNotesLab.Core/InteriorRelationFeasibilityResearch.cs",
        "src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs",
        "src/ManiaAddNotesLab.Core/Model.cs",
        "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveEvaluationRunner.cs",
        "tools/ManiaAddNotesLab.Experiments/Lane0FeasibilityRunner.cs"
    ];
    private static readonly string[] AdapterFiles =
        ["tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorFrozenC11Adapter.cs"];
    private static readonly string[] RunnerFiles =
        ["tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorInternalResearchRunner.cs"];
    private static readonly string[] LauncherFiles =
    [
        "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorPrebindingAuthority.cs",
        "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorIsolatedLauncher.cs",
        "tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorIsolatedRuntime.cs",
        "tools/ManiaAddNotesLab.Experiments/ManiaAddNotesLab.Experiments.csproj",
        "tools/ManiaAddNotesLab.IntegrationResearchWorker/ManiaAddNotesLab.IntegrationResearchWorker.csproj",
        "tools/ManiaAddNotesLab.IntegrationResearchWorker/Program.cs"
    ];
    private static readonly string[] VerifierFiles =
        ["tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorDeepSemanticPackageVerifier.cs"];

    public string ObserveHead(string root) => Capture("git", ["-C", root, "rev-parse", "HEAD"]).Trim();

    public bool ObserveTrackedClean(string root) => string.IsNullOrWhiteSpace(
        Capture("git", ["-C", root, "status", "--porcelain", "--untracked-files=no"]));

    public Lane0IntegrationRuntimeIdentities ObserveRuntimeIdentities(string root) => new(
        CanonicalContract(root, "docs/lane_0_corrective_successor_preregistration_contract.json"),
        CanonicalContract(root, "docs/lane_0_corrective_successor_execution_preparation_contract.json"),
        CanonicalContract(root, "docs/lane_0_corrective_successor_integration_preregistration_contract.json"),
        CanonicalContract(root, "docs/lane_0_corrective_successor_integration_execution_contract.json"),
        FrozenC11ManifestResearch.Load(Path.Combine(root, "docs", "g1_gate_runtime_manifest.json"))
            .CanonicalSha256,
        NormalizedTextTreeIdentity(root, ScientificFiles), NormalizedTextTreeIdentity(root, HarnessFiles),
        NormalizedTextTreeIdentity(root, PackageFiles), NormalizedTextTreeIdentity(root, AdapterFiles),
        NormalizedTextTreeIdentity(root, RunnerFiles), NormalizedTextTreeIdentity(root, LauncherFiles),
        NormalizedTextTreeIdentity(root, VerifierFiles), NormalizedTextTreeIdentity(root, DependencyFiles));

    internal static string NormalizedTextTreeIdentity(string root, IEnumerable<string> files)
    {
        var rows = files.OrderBy(x => x, StringComparer.Ordinal).Select(relative =>
        {
            var text = File.ReadAllText(Path.Combine(root,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (text.Length > 0 && text[0] == '\uFEFF') text = text[1..];
            text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var hash = Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false).GetBytes(text)));
            return $"{relative}|{hash}";
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', rows))));
    }

    private static string CanonicalContract(string root, string relative)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            relative.Replace('/', Path.DirectorySeparatorChar))));
        var actual = CanonicalHash(document.RootElement.GetProperty("contract"));
        var declared = document.RootElement.GetProperty("canonicalSha256").GetString();
        if (actual != declared) throw new InvalidDataException($"Canonical contract drifted: {relative}.");
        return actual;
    }

    private static string CanonicalHash(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, element);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray(); break;
            case JsonValueKind.String: writer.WriteStringValue(element.GetString()); break;
            case JsonValueKind.Number: element.WriteTo(writer); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new InvalidDataException("Unsupported canonical JSON value.");
        }
    }

    private static string Capture(string file, IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {file}.");
        var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidDataException($"Authority observation failed: {error}");
        return output;
    }
}

internal sealed class GitDetachedCheckoutMaterializer : ILane0IntegrationCheckoutMaterializer
{
    public Lane0IntegrationCheckoutEvidence CreateDetachedCheckout(
        string canonicalSourceRoot, string canonicalExecutionRoot, string exactHead)
    {
        Run("git", ["clone", "--no-hardlinks", "--no-checkout", "--", canonicalSourceRoot,
            canonicalExecutionRoot]);
        Run("git", ["-C", canonicalExecutionRoot, "checkout", "--detach", exactHead]);
        var head = Capture("git", ["-C", canonicalExecutionRoot, "rev-parse", "HEAD"]).Trim();
        var status = Capture("git", ["-C", canonicalExecutionRoot, "status", "--porcelain",
            "--untracked-files=no"]);
        var detached = ExitCode("git", ["-C", canonicalExecutionRoot, "symbolic-ref", "-q", "HEAD"]) != 0;
        return new(head, detached, string.IsNullOrWhiteSpace(status), false, false);
    }

    private static void Run(string file, IReadOnlyList<string> arguments)
    {
        var result = Start(file, arguments, capture: true);
        if (result.ExitCode != 0)
            throw new InvalidDataException($"Isolated checkout command failed: {result.Error}");
    }

    private static string Capture(string file, IReadOnlyList<string> arguments)
    {
        var result = Start(file, arguments, capture: true);
        if (result.ExitCode != 0)
            throw new InvalidDataException($"Isolated checkout inspection failed: {result.Error}");
        return result.Output;
    }

    private static int ExitCode(string file, IReadOnlyList<string> arguments) =>
        Start(file, arguments, capture: true).ExitCode;

    private static (int ExitCode, string Output, string Error) Start(
        string file, IReadOnlyList<string> arguments, bool capture)
    {
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            RedirectStandardOutput = capture,
            RedirectStandardError = capture,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Could not start {file}.");
        var output = capture ? process.StandardOutput.ReadToEnd() : string.Empty;
        var error = capture ? process.StandardError.ReadToEnd() : string.Empty;
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }
}

internal sealed class Lane0IntegrationFileAuthorityStore : ILane0IntegrationAuthorityStore
{
    private readonly string receiptPath;
    private readonly string outputPath;
    private readonly string stagingPath;

    internal Lane0IntegrationFileAuthorityStore(string receiptPath, string outputPath, string stagingPath)
    {
        this.receiptPath = receiptPath;
        this.outputPath = outputPath;
        this.stagingPath = stagingPath;
    }

    public bool ReceiptExists => File.Exists(receiptPath) || Directory.Exists(receiptPath);
    public bool OutputOrStagingExists => File.Exists(outputPath) || Directory.Exists(outputPath)
        || File.Exists(stagingPath) || Directory.Exists(stagingPath);

    public void CreateDurableReceipt(byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(receiptPath))!);
        using var stream = new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(true);
    }
}
