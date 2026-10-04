using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;

internal sealed record Lane0IntegrationRuntimeProvenance(
    string RuntimeHead, string AssemblyPath, string AssemblySha256,
    string BaseDirectory, string ExecutionRoot, string WorkerMarker);

internal sealed record Lane0IntegrationPreparedRuntime(
    string ExecutionRoot, string WorkerAssemblyPath, string WorkerAssemblySha256,
    Lane0IntegrationRuntimeProvenance Provenance);

internal sealed record Lane0IntegrationScientificRuntimeResult(
    Lane0SuccessorEvaluationPass First, Lane0SuccessorEvaluationPass Second,
    Lane0IntegrationRuntimeProvenance Provenance);

internal interface ILane0IntegrationIsolatedScientificRuntime
{
    Lane0IntegrationPreparedRuntime Prepare(string canonicalExecutionRoot, string expectedHead);
    Lane0IntegrationScientificRuntimeResult Execute(
        Lane0IntegrationPreparedRuntime prepared,
        ImmutableArray<Lane0CorrectiveChartInput> inputs,
        Lane0SuccessorEvaluationContext context);
}

internal sealed record Lane0IntegrationWorkerChart(
    string ChartId, int KeyCount, ImmutableArray<string> Lines,
    ImmutableArray<ManiaObject> OriginalObjects, ImmutableArray<TimingPoint> TimingPoints);

internal sealed record Lane0IntegrationWorkerRequest(
    ImmutableArray<Lane0IntegrationWorkerChart> Charts,
    Lane0SuccessorEvaluationContext Context);

internal sealed record Lane0IntegrationWorkerPassMetadata(
    string ScientificOutcome, ImmutableArray<string> IntegrityFailures,
    ImmutableArray<string> BlockedReasons, int RngCalls, bool InputsUnchanged,
    bool BehaviorChanged, bool DefaultChanged);

internal sealed record Lane0IntegrationWorkerResponse(
    Lane0IntegrationWorkerPassMetadata First, Lane0IntegrationWorkerPassMetadata Second,
    Lane0IntegrationRuntimeProvenance Provenance);

/// <summary>
/// Builds the research worker from the detached checkout and executes that exact assembly twice.
/// The host validates both pre-receipt provenance and the post-receipt response independently.
/// </summary>
internal sealed class Lane0ExecutionRootScientificRuntime : ILane0IntegrationIsolatedScientificRuntime
{
    internal const string DefaultProjectPath =
        "tools/ManiaAddNotesLab.IntegrationResearchWorker/ManiaAddNotesLab.IntegrationResearchWorker.csproj";
    internal const string DefaultAssemblyName = "ManiaAddNotesLab.IntegrationResearchWorker.dll";
    internal const string ExpectedWorkerMarker = "LANE0_EXECUTION_ROOT_WORKER_V1";

    private readonly string projectRelativePath;
    private readonly string assemblyName;

    internal Lane0ExecutionRootScientificRuntime(
        string projectRelativePath = DefaultProjectPath,
        string assemblyName = DefaultAssemblyName)
    {
        this.projectRelativePath = projectRelativePath;
        this.assemblyName = assemblyName;
    }

    public Lane0IntegrationPreparedRuntime Prepare(string canonicalExecutionRoot, string expectedHead)
    {
        var project = UnderRoot(canonicalExecutionRoot, projectRelativePath);
        if (!File.Exists(project))
            throw new InvalidDataException("Isolated research worker project is absent from ExecutionRoot.");
        var build = Path.Combine(canonicalExecutionRoot, ".artifacts", "integration-worker");
        Directory.CreateDirectory(build);
        Run("dotnet", ["build", project, "-c", "Release", "-o", build, "--nologo"],
            canonicalExecutionRoot);
        var assembly = UnderRoot(build, assemblyName);
        if (!File.Exists(assembly))
            throw new InvalidDataException("ExecutionRoot build did not produce the research worker assembly.");
        var hash = Sha256File(assembly);
        var provenancePath = Path.Combine(build, "pre-receipt-provenance.json");
        Run("dotnet", [assembly, "--provenance-only", provenancePath,
            "--execution-root", canonicalExecutionRoot], canonicalExecutionRoot);
        var provenance = ReadProvenance(provenancePath);
        ValidateProvenance(provenance, canonicalExecutionRoot, expectedHead, assembly, hash);
        return new(canonicalExecutionRoot, assembly, hash, provenance);
    }

    public Lane0IntegrationScientificRuntimeResult Execute(
        Lane0IntegrationPreparedRuntime prepared,
        ImmutableArray<Lane0CorrectiveChartInput> inputs,
        Lane0SuccessorEvaluationContext context)
    {
        Require(Sha256File(prepared.WorkerAssemblyPath) == prepared.WorkerAssemblySha256,
            "Prepared worker assembly changed after the receipt boundary.");
        var exchange = Path.Combine(prepared.ExecutionRoot, ".artifacts", "integration-exchange");
        Directory.CreateDirectory(exchange);
        var requestPath = Path.Combine(exchange, "request.json");
        var responsePath = Path.Combine(exchange, "response.json");
        var firstPath = Path.Combine(exchange, "pass-1");
        var secondPath = Path.Combine(exchange, "pass-2");
        var charts = inputs.Select(x => new Lane0IntegrationWorkerChart(x.ChartId, x.Chart.KeyCount,
            x.Chart.Lines.ToImmutableArray(), x.Chart.OriginalObjects.ToImmutableArray(),
            x.Chart.TimingPoints.ToImmutableArray())).ToImmutableArray();
        File.WriteAllBytes(requestPath, JsonSerializer.SerializeToUtf8Bytes(
            new Lane0IntegrationWorkerRequest(charts, context), JsonOptions));
        Run("dotnet", [prepared.WorkerAssemblyPath, "--execute", requestPath, responsePath,
            firstPath, secondPath, "--execution-root", prepared.ExecutionRoot], prepared.ExecutionRoot);
        var response = JsonSerializer.Deserialize<Lane0IntegrationWorkerResponse>(
            File.ReadAllBytes(responsePath), JsonOptions)
            ?? throw new InvalidDataException("Research worker returned no response.");
        ValidateProvenance(response.Provenance, prepared.ExecutionRoot,
            prepared.Provenance.RuntimeHead, prepared.WorkerAssemblyPath,
            prepared.WorkerAssemblySha256);
        return new(ToPass(response.First, ReadPackage(firstPath)),
            ToPass(response.Second, ReadPackage(secondPath)), response.Provenance);
    }

    private static Lane0SuccessorEvaluationPass ToPass(Lane0IntegrationWorkerPassMetadata value,
        ImmutableSortedDictionary<string, byte[]> package) => new(value.ScientificOutcome, package,
        value.IntegrityFailures, value.BlockedReasons, value.RngCalls, value.InputsUnchanged,
        value.BehaviorChanged, value.DefaultChanged);

    private static ImmutableSortedDictionary<string, byte[]> ReadPackage(string root)
    {
        var builder = ImmutableSortedDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var name in Lane0CorrectiveSuccessorPackage.ArtifactNames)
        {
            var path = UnderRoot(root, name);
            if (!File.Exists(path)) throw new InvalidDataException($"Worker package lacks {name}.");
            builder.Add(name, File.ReadAllBytes(path));
        }
        Require(Directory.EnumerateFiles(root).Select(Path.GetFileName)
            .OrderBy(x => x, StringComparer.Ordinal)
            .SequenceEqual(Lane0CorrectiveSuccessorPackage.ArtifactNames),
            "Worker package contains unexpected files.");
        return builder.ToImmutable();
    }

    private static Lane0IntegrationRuntimeProvenance ReadProvenance(string path) =>
        JsonSerializer.Deserialize<Lane0IntegrationRuntimeProvenance>(File.ReadAllBytes(path), JsonOptions)
        ?? throw new InvalidDataException("Research worker provenance is absent.");

    private static void ValidateProvenance(Lane0IntegrationRuntimeProvenance value,
        string executionRoot, string expectedHead, string expectedAssembly, string expectedHash)
    {
        var independentlyObservedHead = Capture("git", ["-C", executionRoot, "rev-parse", "HEAD"],
            executionRoot).Trim();
        Require(value.RuntimeHead == expectedHead && independentlyObservedHead == expectedHead,
            "Runtime provenance HEAD does not equal the independently observed ExecutionRoot HEAD.");
        Require(Path.GetFullPath(value.ExecutionRoot) == Path.GetFullPath(executionRoot),
            "Runtime provenance ExecutionRoot drifted.");
        Require(Path.GetFullPath(value.AssemblyPath) == Path.GetFullPath(expectedAssembly)
            && IsUnder(executionRoot, value.AssemblyPath)
            && IsUnder(executionRoot, value.BaseDirectory),
            "Runtime assembly/base directory did not originate from ExecutionRoot.");
        Require(value.AssemblySha256 == expectedHash && Sha256File(expectedAssembly) == expectedHash,
            "Runtime assembly identity drifted.");
        Require(value.WorkerMarker == ExpectedWorkerMarker, "Runtime worker marker drifted.");
        var clean = Capture("git", ["-C", executionRoot, "status", "--porcelain",
            "--untracked-files=no"], executionRoot);
        Require(string.IsNullOrWhiteSpace(clean), "ExecutionRoot tracked tree became dirty.");
    }

    private static string UnderRoot(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        Require(IsUnder(root, full), "Research worker path escaped ExecutionRoot.");
        return full;
    }

    private static bool IsUnder(string root, string path)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var canonicalRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var canonicalPath = Path.GetFullPath(path);
        return canonicalPath.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, comparison);
    }

    private static string Sha256File(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static void Run(string file, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var result = Start(file, arguments, workingDirectory);
        if (result.ExitCode != 0)
            throw new InvalidDataException($"ExecutionRoot command failed: {result.Error}{result.Output}");
    }

    private static string Capture(string file, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var result = Start(file, arguments, workingDirectory);
        if (result.ExitCode != 0)
            throw new InvalidDataException($"ExecutionRoot inspection failed: {result.Error}");
        return result.Output;
    }

    private static (int ExitCode, string Output, string Error) Start(
        string file, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var info = new ProcessStartInfo(file)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Could not start {file}.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
