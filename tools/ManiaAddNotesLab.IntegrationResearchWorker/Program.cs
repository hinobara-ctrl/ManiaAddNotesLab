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
        if (args.Length < 5 || args[0] != "--execute") throw new InvalidDataException("Unknown worker mode.");
        var request = JsonSerializer.Deserialize<Lane0IntegrationWorkerRequest>(
            File.ReadAllBytes(args[1]), JsonOptions()) ?? throw new InvalidDataException("Worker request is absent.");
        var inputs = request.Charts.Select(x => new Lane0CorrectiveChartInput(x.ChartId, new ManiaChart
        {
            KeyCount = x.KeyCount, Lines = x.Lines, OriginalObjects = x.OriginalObjects,
            TimingPoints = x.TimingPoints
        })).ToImmutableArray();
        var evaluator = new Lane0CorrectiveSuccessorEvaluationRunner();
        var first = evaluator.Evaluate(inputs, request.Context);
        var second = evaluator.Evaluate(inputs, request.Context);
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
