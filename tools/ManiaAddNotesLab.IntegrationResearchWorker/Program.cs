using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ManiaAddNotesLab.Core;

return MainWorker(args);

static int MainWorker(string[] args)
{
    try
    {
        var rootIndex = Array.IndexOf(args, "--execution-root");
        if (rootIndex < 0 || rootIndex + 1 >= args.Length)
            throw new InvalidDataException("ExecutionRoot is required.");
        var executionRoot = Path.GetFullPath(args[rootIndex + 1]);
        var provenance = ObserveProvenance(executionRoot);
        if (args.Length >= 2 && args[0] == "--provenance-only")
        {
            File.WriteAllBytes(args[1], JsonSerializer.SerializeToUtf8Bytes(provenance, JsonOptions()));
            return 0;
        }
        if (args[0] == "--official-run") return RunOfficial(executionRoot, provenance);
        if (args[0] == "--synthetic-run")
            return RunSynthetic(executionRoot, provenance);
        throw new InvalidDataException("Unknown worker mode.");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        return 1;
    }
}

static int RunOfficial(string executionRoot, Lane0IntegrationRuntimeProvenance initialProvenance)
{
    var attestation = JsonSerializer.Deserialize<Lane0IntegrationOfficialLaunchAttestation>(
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "official-launch-attestation.json")),
        JsonOptions()) ?? throw new InvalidDataException("Official launch attestation is absent.");
    var actualAttestationHash = Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(attestation with { CanonicalSha256 = "" }, JsonOptions())));
    if (attestation.CanonicalSha256 != actualAttestationHash || attestation.SchemaVersion !=
        "lane-0-integration-official-launch-attestation.1")
        throw new InvalidDataException("Official launch attestation identity drifted.");
    var source = Path.GetFullPath(attestation.CanonicalSourceRoot);
    if (Path.GetFullPath(attestation.CanonicalExecutionRoot) != executionRoot
        || !Lane0CorrectiveSuccessorIsolatedLauncher.RootsAreLexicallyIsolated(source, executionRoot))
        throw new InvalidDataException("Official roots are not canonically isolated.");
    if (!attestation.Detached || !attestation.TrackedClean || attestation.UsedHardlinks
        || attestation.ReusedSourceBuildOutputs)
        throw new InvalidDataException("Official launch does not attest detached pristine no-hardlink isolation.");
    if (initialProvenance.RuntimeHead != attestation.ExpectedHead
        || !Lane0ExecutionRootScientificRuntime.BinaryClosuresEqual(
            initialProvenance.BinaryClosure, attestation.BinaryClosure))
        throw new InvalidDataException("Official launch runtime provenance drifted.");
    if (CaptureGit(executionRoot, "symbolic-ref", "-q", "HEAD").ExitCode == 0
        || !string.IsNullOrWhiteSpace(CaptureGit(executionRoot, "status", "--porcelain",
            "--untracked-files=no").Output))
        throw new InvalidDataException("ExecutionRoot is not detached and tracked-clean.");

    var observer = new GitAndFileIntegrationAuthorityObserver();
    if (observer.ObserveHead(source) != attestation.ExpectedHead
        || !observer.ObserveTrackedClean(source)
        || observer.ObserveRuntimeIdentities(source) != attestation.ExpectedIdentities)
        throw new InvalidDataException("Official worker rejected live source authority.");
    var paths = Lane0IntegrationOfficialLayout.Derive(source, executionRoot);
    var binding = new Lane0IntegrationCanonicalBindingLoader().Load(paths.CanonicalBindingPath);
    if (binding.Fields.SchemaVersion != Lane0CorrectiveSuccessorIsolatedLauncher.BindingSchema
        || binding.Fields.ApprovedPublicHead != attestation.ExpectedHead
        || binding.Fields.Identities != attestation.ExpectedIdentities
        || !binding.Fields.ExplicitHumanAuthorization
        || binding.Fields.AuthorizedExecutionCount != 1)
        throw new InvalidDataException("Official worker rejected canonical binding semantics.");
    var store = new Lane0IntegrationFileAuthorityStore(paths.DurableReceiptPath,
        paths.FinalArtifactsRoot, paths.StagingRoot);
    if (store.ReceiptExists || store.OutputOrStagingExists || File.Exists(AuthorityStatePath()))
        throw new InvalidDataException("Official one-shot authority is already consumed.");

    // The claim is durable before the receipt. Failure and exceptions consume the attempt, while
    // binding the exact project-owned closure independently of host process memory.
    WriteAuthorityState(new(source, attestation.ExpectedHead, binding.CanonicalSha256,
        attestation.ExpectedIdentities, initialProvenance.BinaryClosure,
        "SCIENCE_ATTEMPT_CLAIMED"));
    store.CreateDurableReceipt(Lane0CorrectiveSuccessorIsolatedLauncher.BuildReceipt(
        attestation.ExpectedHead, attestation.ExpectedIdentities, binding.CanonicalSha256));
    Console.WriteLine("RECEIPT_CREATED");
    Console.Out.Flush();

    var corpusLine = Console.ReadLine()
        ?? throw new InvalidDataException("Post-receipt CorpusRoot token was not delivered.");
    var corpusToken = JsonSerializer.Deserialize<string>(corpusLine, JsonOptions())
        ?? throw new InvalidDataException("Post-receipt CorpusRoot token is null.");
    var corpus = Path.GetFullPath(corpusToken);
    if (!Lane0CorrectiveSuccessorIsolatedLauncher.RootsAreLexicallyIsolated(source, corpus)
        || !Lane0CorrectiveSuccessorIsolatedLauncher.RootsAreLexicallyIsolated(executionRoot, corpus))
        throw new InvalidDataException("CorpusRoot is not isolated from source and execution roots.");

    var beforeScience = ObserveProvenance(executionRoot);
    var state = ReadAuthorityState();
    Lane0ExecutionRootScientificRuntime.ValidateDurableAuthorityClosure(
        state, beforeScience.BinaryClosure, attestation.ExpectedIdentities);
    Lane0IntegrationDurableReceiptVerifier.VerifyCanonicalReceipt(source, executionRoot,
        state.ExpectedHead, state.CanonicalBindingSha256, state.ExpectedIdentities);

    var manifest = FrozenC11ManifestResearch.Load(Path.Combine(executionRoot, "docs",
        "g1_gate_runtime_manifest.json"));
    var admitted = Lane0CorrectiveSuccessorFrozenC11Adapter.CreateOfficial(manifest, executionRoot)
        .Admit(manifest, corpus);
    var ids = attestation.ExpectedIdentities;
    var context = new Lane0SuccessorEvaluationContext(attestation.ExpectedHead,
        ids.Phase1ContractSha256, ids.Phase2ContractSha256, ids.ManifestSha256,
        ids.ScientificImplementationSha256, ids.Phase2HarnessSha256,
        ids.Phase2PackageVerifierSha256, ids.FrozenDependenciesSha256,
        Lane0CorrectiveSuccessorReferenceValidator.FrozenReferences);
    var evaluator = new Lane0CorrectiveSuccessorEvaluationRunner();
    var finalizer = new Lane0IntegrationPackageFinalizer();
    var firstRaw = evaluator.Evaluate(admitted.ScientificCharts, context);
    var secondRaw = evaluator.Evaluate(admitted.ScientificCharts, context);
    var first = firstRaw with { Package = finalizer.Finalize(firstRaw.Package, ids, firstRaw.DefaultChanged) };
    var second = secondRaw with { Package = finalizer.Finalize(secondRaw.Package, ids, secondRaw.DefaultChanged) };
    if (!Lane0CorrectiveSuccessorPackage.ByteIdentical(first.Package, second.Package)
        || first.ScientificOutcome != second.ScientificOutcome)
        throw new InvalidDataException("Official scientific passes are not deterministic.");
    new Lane0CorrectiveSuccessorDeepSemanticPackageVerifier().Verify(first.Package,
        new(ids, attestation.ExpectedHead, first.ScientificOutcome, admitted, first.DefaultChanged));
    Directory.CreateDirectory(paths.FinalArtifactsRoot);
    WritePackage(Path.Combine(paths.FinalArtifactsRoot, "pass-1"), first.Package);
    WritePackage(Path.Combine(paths.FinalArtifactsRoot, "pass-2"), second.Package);
    if (!Lane0ExecutionRootScientificRuntime.BinaryClosuresEqual(
        ObserveProvenance(executionRoot).BinaryClosure, state.BinaryClosure))
        throw new InvalidDataException("Durable authority binary closure drifted after science.");
    Directory.CreateDirectory(paths.StagingRoot);
    var result = new Lane0IntegrationResearchResult("PUBLISHABLE", first.ScientificOutcome,
        true, 2, 2, Lane0CorrectiveSuccessorIsolatedLauncher.AuthorityOrder,
        "Unified official authority-to-science capability verified.");
    File.WriteAllBytes(Path.Combine(paths.StagingRoot, "response.json"),
        JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions()));
    return 0;
}

static int RunSynthetic(string executionRoot,
    Lane0IntegrationRuntimeProvenance provenance)
{
    var authorityRoot = Path.Combine(executionRoot, ".artifacts",
        "synthetic-integration-authority", "SYNTHETIC.NONOFFICIAL.ONE_SHOT");
    Directory.CreateDirectory(authorityRoot);
    using (var receipt = new FileStream(Path.Combine(authorityRoot, "receipt.json"),
        FileMode.CreateNew, FileAccess.Write, FileShare.None))
    {
        JsonSerializer.Serialize(receipt, new { schemaVersion = "synthetic-nonofficial-attempt.1" },
            JsonOptions());
        receipt.Flush(true);
    }
    Console.WriteLine("SYNTHETIC_RECEIPT_CREATED");
    Console.Out.Flush();
    var requestLine = Console.ReadLine()
        ?? throw new InvalidDataException("Post-receipt synthetic request was not delivered.");
    var request = JsonSerializer.Deserialize<Lane0IntegrationWorkerRequest>(
        requestLine, JsonOptions())
        ?? throw new InvalidDataException("Synthetic worker request is absent.");
    var inputs = request.Charts.Select(x => new Lane0CorrectiveChartInput(x.ChartId, new ManiaChart
    {
        KeyCount = x.KeyCount, Lines = x.Lines, OriginalObjects = x.OriginalObjects,
        TimingPoints = x.TimingPoints
    })).ToImmutableArray();
    var evaluator = new Lane0CorrectiveSuccessorEvaluationRunner();
    var finalizer = new Lane0IntegrationPackageFinalizer();
    var firstRaw = evaluator.Evaluate(inputs, request.Context);
    var secondRaw = evaluator.Evaluate(inputs, request.Context);
    var first = firstRaw with { Package = finalizer.Finalize(firstRaw.Package, request.Identities,
        firstRaw.DefaultChanged) };
    var second = secondRaw with { Package = finalizer.Finalize(secondRaw.Package, request.Identities,
        secondRaw.DefaultChanged) };
    var root = Path.Combine(executionRoot, ".artifacts", "synthetic-integration-exchange");
    var staging = Path.Combine(root, "staging");
    var final = Path.Combine(root, "final-artifacts");
    WritePackage(Path.Combine(final, "pass-1"), first.Package);
    WritePackage(Path.Combine(final, "pass-2"), second.Package);
    Directory.CreateDirectory(staging);
    File.WriteAllBytes(Path.Combine(staging, "response.json"), JsonSerializer.SerializeToUtf8Bytes(
        new Lane0IntegrationWorkerResponse(Metadata(first), Metadata(second), provenance), JsonOptions()));
    return 0;
}

static Lane0IntegrationWorkerPassMetadata Metadata(Lane0SuccessorEvaluationPass pass) =>
    new(pass.ScientificOutcome, pass.IntegrityFailures, pass.BlockedReasons, pass.RngCalls,
        pass.InputsUnchanged, pass.BehaviorChanged, pass.DefaultChanged);

static void WritePackage(string root, ImmutableSortedDictionary<string, byte[]> package)
{
    Directory.CreateDirectory(root);
    foreach (var pair in package) File.WriteAllBytes(Path.Combine(root, pair.Key), pair.Value);
}

static Lane0IntegrationRuntimeProvenance ObserveProvenance(string executionRoot)
{
    var assembly = Path.GetFullPath(System.Reflection.Assembly.GetEntryAssembly()?.Location
        ?? throw new InvalidDataException("Worker entry assembly is unavailable."));
    var closure = Lane0ExecutionRootScientificRuntime.ObserveBinaryClosure(
        AppContext.BaseDirectory, Lane0ExecutionRootScientificRuntime.DefaultProjectAssemblyNames);
    return new(CaptureGit(executionRoot, "rev-parse", "HEAD").Output.Trim(), assembly,
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),
        Path.GetFullPath(AppContext.BaseDirectory), executionRoot,
        Lane0ExecutionRootScientificRuntime.ExpectedWorkerMarker, closure);
}

static string AuthorityStatePath() => Lane0ExecutionRootScientificRuntime.AuthorityStatePath(
    System.Reflection.Assembly.GetEntryAssembly()?.Location
        ?? throw new InvalidDataException("Worker entry assembly is unavailable."));

static void WriteAuthorityState(Lane0IntegrationWorkerAuthorityState state)
{
    using var stream = new FileStream(AuthorityStatePath(), FileMode.CreateNew,
        FileAccess.Write, FileShare.None);
    JsonSerializer.Serialize(stream, state, JsonOptions());
    stream.Flush(true);
}

static Lane0IntegrationWorkerAuthorityState ReadAuthorityState() =>
    JsonSerializer.Deserialize<Lane0IntegrationWorkerAuthorityState>(
        File.ReadAllBytes(AuthorityStatePath()), JsonOptions())
    ?? throw new InvalidDataException("Official authority state is malformed.");

static (int ExitCode, string Output, string Error) CaptureGit(string root, params string[] arguments)
{
    var info = new ProcessStartInfo("git")
    {
        UseShellExecute = false, RedirectStandardOutput = true,
        RedirectStandardError = true, CreateNoWindow = true
    };
    info.ArgumentList.Add("-C"); info.ArgumentList.Add(root);
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start git.");
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    return (process.ExitCode, output, error);
}

static JsonSerializerOptions JsonOptions() => new()
{ PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false };
