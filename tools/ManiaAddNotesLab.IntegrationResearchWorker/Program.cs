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
        if (rootIndex < 0 || rootIndex + 1 >= args.Length) throw new InvalidDataException("ExecutionRoot is required.");
        var executionRoot = Path.GetFullPath(args[rootIndex + 1]);
        var provenance = ObserveProvenance(executionRoot);
        if (args.Length >= 2 && args[0] == "--provenance-only")
        {
            File.WriteAllBytes(args[1], JsonSerializer.SerializeToUtf8Bytes(provenance, JsonOptions()));
            return 0;
        }
        if (args.Length >= 3 && args[0] == "--authorize")
        {
            var authorityRequest = JsonSerializer.Deserialize<Lane0IntegrationWorkerAuthorityRequest>(
                File.ReadAllBytes(args[1]), JsonOptions())
                ?? throw new InvalidDataException("Authority request is absent.");
            if (provenance.RuntimeHead != authorityRequest.ExpectedHead)
                throw new InvalidDataException("Authority-runtime HEAD drifted.");
            var source = Path.GetFullPath(authorityRequest.CanonicalSourceRoot);
            var observer = new GitAndFileIntegrationAuthorityObserver();
            if (observer.ObserveHead(source) != authorityRequest.ExpectedHead
                || !observer.ObserveTrackedClean(source))
                throw new InvalidDataException("Authority worker rejected SourceRoot Git state.");
            var observed = observer.ObserveRuntimeIdentities(source);
            if (observed != authorityRequest.ExpectedIdentities)
                throw new InvalidDataException("Authority worker rejected live component identities.");
            var paths = Lane0IntegrationOfficialLayout.Derive(source, executionRoot);
            var binding = new Lane0IntegrationCanonicalBindingLoader().Load(paths.CanonicalBindingPath);
            if (binding.Fields.SchemaVersion != Lane0CorrectiveSuccessorIsolatedLauncher.BindingSchema
                || binding.Fields.ApprovedPublicHead != authorityRequest.ExpectedHead
                || binding.Fields.Identities != observed
                || !binding.Fields.ExplicitHumanAuthorization
                || binding.Fields.AuthorizedExecutionCount != 1)
                throw new InvalidDataException("Authority worker rejected canonical binding semantics.");
            var store = new Lane0IntegrationFileAuthorityStore(paths.DurableReceiptPath,
                paths.FinalArtifactsRoot, paths.StagingRoot);
            if (store.ReceiptExists || store.OutputOrStagingExists)
                throw new InvalidDataException("Authority worker rejected consumed or nonempty authority.");
            var launch = new Lane0IntegrationLaunchRequest(authorityRequest.ExpectedHead, observed, null,
                source, executionRoot, "OPAQUE_NOT_INTERPRETED_BY_AUTHORITY_WORKER");
            store.CreateDurableReceipt(Lane0CorrectiveSuccessorIsolatedLauncher.BuildReceipt(
                launch, binding.CanonicalSha256));
            var authorityResponse = new Lane0IntegrationWorkerAuthorityResponse(
                binding.CanonicalSha256, observed, provenance);
            File.WriteAllBytes(args[2], JsonSerializer.SerializeToUtf8Bytes(authorityResponse, JsonOptions()));
            return 0;
        }
        if (args.Length < 5 || args[0] != "--execute") throw new InvalidDataException("Unknown worker mode.");
        var request = JsonSerializer.Deserialize<Lane0IntegrationWorkerRequest>(
            File.ReadAllBytes(args[1]), JsonOptions()) ?? throw new InvalidDataException("Worker request is absent.");
        var inputs = request.Charts.Select(x => new Lane0CorrectiveChartInput(x.ChartId, new ManiaChart
        {
            KeyCount = x.KeyCount, Lines = x.Lines, OriginalObjects = x.OriginalObjects,
            TimingPoints = x.TimingPoints
        })).ToImmutableArray();
        var evaluator = new Lane0CorrectiveSuccessorEvaluationRunner();
        var finalizer = new Lane0IntegrationPackageFinalizer();
        var firstRaw = evaluator.Evaluate(inputs, request.Context);
        var secondRaw = evaluator.Evaluate(inputs, request.Context);
        var first = firstRaw with
        {
            Package = finalizer.Finalize(firstRaw.Package, request.Identities, firstRaw.DefaultChanged)
        };
        var second = secondRaw with
        {
            Package = finalizer.Finalize(secondRaw.Package, request.Identities, secondRaw.DefaultChanged)
        };
        WritePackage(args[3], first.Package);
        WritePackage(args[4], second.Package);
        var response = new Lane0IntegrationWorkerResponse(Metadata(first), Metadata(second), provenance);
        File.WriteAllBytes(args[2], JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions()));
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception);
        return 1;
    }
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
    var assembly = Path.GetFullPath(Environment.ProcessPath
        ?? throw new InvalidDataException("Worker process path is unavailable."));
    // Under `dotnet worker.dll`, ProcessPath is dotnet; the scientific assembly is this assembly.
    assembly = Path.GetFullPath(System.Reflection.Assembly.GetEntryAssembly()?.Location
        ?? throw new InvalidDataException("Worker entry assembly is unavailable."));
    return new(CaptureGitHead(executionRoot), assembly,
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly))),
        Path.GetFullPath(AppContext.BaseDirectory), executionRoot,
        Lane0ExecutionRootScientificRuntime.ExpectedWorkerMarker);
}

static string CaptureGitHead(string root)
{
    var info = new ProcessStartInfo("git")
    {
        UseShellExecute = false, RedirectStandardOutput = true,
        RedirectStandardError = true, CreateNoWindow = true
    };
    info.ArgumentList.Add("-C"); info.ArgumentList.Add(root);
    info.ArgumentList.Add("rev-parse"); info.ArgumentList.Add("HEAD");
    using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start git.");
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidDataException($"Runtime Git observation failed: {error}");
    return output.Trim();
}

static JsonSerializerOptions JsonOptions() => new()
{ PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false };
