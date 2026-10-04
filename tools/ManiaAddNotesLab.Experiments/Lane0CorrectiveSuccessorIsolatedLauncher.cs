using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

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
    Lane0IntegrationRuntimeIdentities ObservedIdentities,
    Lane0IntegrationVerifiedBinding? Binding,
    string SourceHead, string IsolatedHead, string RuntimeHead,
    bool SourceTrackedClean, string SourceRoot, string ExecutionRoot,
    string CorpusRootToken);

internal sealed record Lane0IntegrationPreReceiptLease(
    Lane0IntegrationLaunchRequest Request, string CanonicalSourceRoot,
    string CanonicalExecutionRoot, string CanonicalBindingSha256,
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

/// <summary>
/// Authority gate for a future isolated run. CorpusRoot is not interpreted or passed to an
/// external collaborator until after CreateDurableReceipt succeeds.
/// </summary>
internal sealed class Lane0CorrectiveSuccessorIsolatedLauncher
{
    private readonly ILane0IntegrationCheckoutMaterializer checkoutMaterializer;

    internal Lane0CorrectiveSuccessorIsolatedLauncher(
        ILane0IntegrationCheckoutMaterializer checkoutMaterializer) =>
        this.checkoutMaterializer = checkoutMaterializer;

    internal static Lane0CorrectiveSuccessorIsolatedLauncher CreateOfficial() =>
        new(new GitDetachedCheckoutMaterializer());

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
        var completed = ImmutableArray.CreateBuilder<string>();
        Require(request.SourceHead == request.ExpectedSourcePublicHead,
            "Source public HEAD does not equal the expected audited integration HEAD.");
        Require(request.ExpectedSourcePublicHead != HistoricalImplementationBaseline,
            "The implementation baseline is not eligible as an execution binding target.");
        completed.Add(AuthorityOrder[0]);
        Require(request.ExpectedIdentities.IntegrationPreregistrationSha256 ==
            "7C0A86BF3C6647CA7092C4D3390EB97EA6E11D7E5429ECBA6F499D55C5417A02",
            "Integration preregistration identity drifted.");
        completed.Add(AuthorityOrder[1]);
        Require(request.ExpectedIdentities == request.ObservedIdentities,
            "Observed runtime identities do not equal the frozen integration identities.");
        completed.Add(AuthorityOrder[2]);
        var binding = request.Binding ?? throw new InvalidDataException("Canonical binding is absent.");
        completed.Add(AuthorityOrder[3]);
        var canonical = CanonicalBindingBytes(binding.Fields);
        Require(binding.CanonicalBytes.AsSpan().SequenceEqual(canonical),
            "Canonical binding bytes drifted.");
        var bindingHash = Convert.ToHexString(SHA256.HashData(canonical));
        Require(binding.CanonicalSha256 == bindingHash, "Canonical binding hash drifted.");
        Require(binding.Fields.SchemaVersion == BindingSchema, "Canonical binding schema drifted.");
        Require(binding.Fields.ApprovedPublicHead == request.ExpectedSourcePublicHead,
            "Binding HEAD drifted.");
        Require(binding.Fields.Identities == request.ExpectedIdentities,
            "Binding runtime identities drifted.");
        completed.Add(AuthorityOrder[4]);
        Require(binding.Fields.ExplicitHumanAuthorization, "Explicit authorization is false.");
        completed.Add(AuthorityOrder[5]);
        Require(binding.Fields.AuthorizedExecutionCount == 1,
            "Authorized execution count must equal one.");
        completed.Add(AuthorityOrder[6]);
        Require(request.SourceTrackedClean, "Source tracked tree is dirty.");
        completed.Add(AuthorityOrder[7]);
        Require(request.SourceHead == request.IsolatedHead && request.SourceHead == request.RuntimeHead,
            "Binding/source/isolated/runtime HEAD equality failed.");
        completed.Add(AuthorityOrder[8]);

        var source = Path.GetFullPath(request.SourceRoot);
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
        completed.Add(AuthorityOrder[9]);
        Require(!store.ReceiptExists, "Durable receipt already exists.");
        completed.Add(AuthorityOrder[10]);
        Require(!store.OutputOrStagingExists, "Output or staging already exists.");
        completed.Add(AuthorityOrder[11]);

        store.CreateDurableReceipt(BuildReceipt(request, bindingHash));
        completed.Add(AuthorityOrder[12]);
        return new(request, source, execution, bindingHash, completed.ToImmutable());
    }

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

    private static byte[] BuildReceipt(Lane0IntegrationLaunchRequest request, string bindingHash) =>
        [.. JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = ReceiptSchema,
            approvedPublicHead = request.ExpectedSourcePublicHead,
            canonicalBindingSha256 = bindingHash,
            integrationPreregistrationSha256 = request.ExpectedIdentities.IntegrationPreregistrationSha256,
            integrationExecutionContractSha256 = request.ExpectedIdentities.IntegrationExecutionContractSha256,
            attemptCount = 1
        }, new JsonSerializerOptions { WriteIndented = true }), (byte)'\n'];

    private static bool IsAncestor(string parent, string child, StringComparison comparison) =>
        child.StartsWith(parent + Path.DirectorySeparatorChar, comparison)
        || child.StartsWith(parent + Path.AltDirectorySeparatorChar, comparison);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
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
