using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;

internal sealed record Lane0IntegrationRuntimeProvenance(
    string RuntimeHead, string AssemblyPath, string AssemblySha256,
    string BaseDirectory, string ExecutionRoot, string WorkerMarker,
    Lane0IntegrationBinaryClosure BinaryClosure);

internal sealed record Lane0IntegrationBinaryIdentity(
    string BinaryName, string FullCanonicalPath, string Sha256);

internal sealed record Lane0IntegrationBinaryClosure(
    ImmutableArray<Lane0IntegrationBinaryIdentity> Binaries, string AggregateSha256);

internal sealed record Lane0IntegrationPreparedRuntime(
    string ExecutionRoot, string WorkerAssemblyPath, string WorkerAssemblySha256,
    Lane0IntegrationBinaryClosure BinaryClosure, Lane0IntegrationRuntimeProvenance Provenance);

internal sealed record Lane0IntegrationWorkerAuthorityRequest(
    string CanonicalSourceRoot, string ExpectedHead,
    Lane0IntegrationRuntimeIdentities ExpectedIdentities);

internal sealed record Lane0IntegrationWorkerAuthorityResponse(
    string CanonicalBindingSha256, Lane0IntegrationRuntimeIdentities ObservedIdentities,
    Lane0IntegrationRuntimeProvenance Provenance);

internal sealed record Lane0IntegrationScientificRuntimeResult(
    Lane0SuccessorEvaluationPass First, Lane0SuccessorEvaluationPass Second,
    Lane0IntegrationRuntimeProvenance Provenance,
    string FirstFinalArtifactRoot, string SecondFinalArtifactRoot);

internal interface ILane0IntegrationIsolatedScientificRuntime
{
    Lane0IntegrationPreparedRuntime Prepare(string canonicalExecutionRoot, string expectedHead);
    Lane0IntegrationResearchResult RunOfficial(
        Lane0IntegrationPreparedRuntime prepared, string canonicalSourceRoot,
        string expectedHead, Lane0IntegrationRuntimeIdentities expectedIdentities,
        Lane0IntegrationOfficialReceiptCoordinator receiptCoordinator, string corpusRootToken);
    Lane0IntegrationScientificRuntimeResult ExecuteSyntheticFixture(
        Lane0IntegrationPreparedRuntime prepared,
        ImmutableArray<Lane0CorrectiveChartInput> inputs,
        Lane0SuccessorEvaluationContext context,
        Lane0IntegrationRuntimeIdentities identities);
}

internal sealed record Lane0IntegrationWorkerChart(
    string ChartId, int KeyCount, ImmutableArray<string> Lines,
    ImmutableArray<ManiaObject> OriginalObjects, ImmutableArray<TimingPoint> TimingPoints);

internal sealed record Lane0IntegrationWorkerRequest(
    ImmutableArray<Lane0IntegrationWorkerChart> Charts,
    Lane0SuccessorEvaluationContext Context,
    Lane0IntegrationRuntimeIdentities Identities);

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

    internal static readonly ImmutableArray<string> DefaultProjectAssemblyNames =
    [
        "ManiaAddNotesLab.Core.dll",
        "ManiaAddNotesLab.Experiments.dll",
        "ManiaAddNotesLab.IntegrationResearchWorker.dll"
    ];

    private readonly string projectRelativePath;
    private readonly string assemblyName;
    private readonly ImmutableArray<string> projectAssemblyNames;

    internal Lane0ExecutionRootScientificRuntime(
        string projectRelativePath = DefaultProjectPath,
        string assemblyName = DefaultAssemblyName,
        IEnumerable<string>? projectAssemblyNames = null)
    {
        this.projectRelativePath = projectRelativePath;
        this.assemblyName = assemblyName;
        this.projectAssemblyNames = (projectAssemblyNames ?? DefaultProjectAssemblyNames)
            .OrderBy(x => x, StringComparer.Ordinal).ToImmutableArray();
        Require(this.projectAssemblyNames.Contains(assemblyName, StringComparer.Ordinal),
            "Project binary closure must include the worker entry assembly.");
    }

    public Lane0IntegrationPreparedRuntime Prepare(string canonicalExecutionRoot, string expectedHead)
    {
        var project = UnderRoot(canonicalExecutionRoot, projectRelativePath);
        if (!File.Exists(project))
            throw new InvalidDataException("Isolated research worker project is absent from ExecutionRoot.");
        var build = Path.Combine(canonicalExecutionRoot, ".artifacts", "integration-worker");
        Directory.CreateDirectory(build);
        Run("dotnet", ["build", project, "-c", "Release", "-o", build, "--nologo",
                "--ignore-failed-sources", "-p:NuGetAudit=false", "-nodeReuse:false",
                "-p:UseSharedCompilation=false"],
            canonicalExecutionRoot);
        var assembly = UnderRoot(build, assemblyName);
        if (!File.Exists(assembly))
            throw new InvalidDataException("ExecutionRoot build did not produce the research worker assembly.");
        var hash = Sha256File(assembly);
        var closure = ObserveBinaryClosure(build, projectAssemblyNames);
        var provenancePath = Path.Combine(build, "pre-receipt-provenance.json");
        Run("dotnet", [assembly, "--provenance-only", provenancePath,
            "--execution-root", canonicalExecutionRoot], canonicalExecutionRoot);
        var provenance = ReadProvenance(provenancePath);
        ValidateProvenance(provenance, canonicalExecutionRoot, expectedHead, assembly, hash, closure);
        return new(canonicalExecutionRoot, assembly, hash, closure, provenance);
    }

    public Lane0IntegrationScientificRuntimeResult ExecuteSyntheticFixture(
        Lane0IntegrationPreparedRuntime prepared,
        ImmutableArray<Lane0CorrectiveChartInput> inputs,
        Lane0SuccessorEvaluationContext context,
        Lane0IntegrationRuntimeIdentities identities) =>
        ExecuteSyntheticCore(prepared, inputs, context, identities);

    private Lane0IntegrationScientificRuntimeResult ExecuteSyntheticCore(
        Lane0IntegrationPreparedRuntime prepared,
        ImmutableArray<Lane0CorrectiveChartInput> inputs,
        Lane0SuccessorEvaluationContext context,
        Lane0IntegrationRuntimeIdentities identities)
    {
        ValidatePreparedBinaryClosure(prepared, projectAssemblyNames,
            "Prepared project binary closure changed before synthetic science.");
        var staging = UnderRoot(prepared.ExecutionRoot,
            ".artifacts/synthetic-integration-exchange/staging");
        var final = UnderRoot(prepared.ExecutionRoot,
            ".artifacts/synthetic-integration-exchange/final-artifacts");
        var responsePath = Path.Combine(staging, "response.json");
        var firstPath = Path.Combine(final, "pass-1");
        var secondPath = Path.Combine(final, "pass-2");
        var charts = inputs.Select(x => new Lane0IntegrationWorkerChart(x.ChartId, x.Chart.KeyCount,
            x.Chart.Lines.ToImmutableArray(), x.Chart.OriginalObjects.ToImmutableArray(),
            x.Chart.TimingPoints.ToImmutableArray())).ToImmutableArray();
        var request = new Lane0IntegrationWorkerRequest(charts, context, identities);
        var process = StartSynthetic(prepared.WorkerAssemblyPath, prepared.ExecutionRoot, request);
        if (process.ExitCode != 0)
            throw new InvalidDataException(
                $"Synthetic ExecutionRoot command failed: {process.Error}{process.Output}");
        var response = JsonSerializer.Deserialize<Lane0IntegrationWorkerResponse>(
            File.ReadAllBytes(responsePath), JsonOptions)
            ?? throw new InvalidDataException("Research worker returned no response.");
        ValidateProvenance(response.Provenance, prepared.ExecutionRoot,
            prepared.Provenance.RuntimeHead, prepared.WorkerAssemblyPath,
            prepared.WorkerAssemblySha256, prepared.BinaryClosure);
        ValidatePreparedBinaryClosure(prepared, projectAssemblyNames,
            "Prepared project binary closure changed after synthetic science.");
        return new(ToPass(response.First, ReadPackage(firstPath)),
            ToPass(response.Second, ReadPackage(secondPath)), response.Provenance,
            firstPath, secondPath);
    }

    public Lane0IntegrationResearchResult RunOfficial(
        Lane0IntegrationPreparedRuntime prepared, string canonicalSourceRoot,
        string expectedHead, Lane0IntegrationRuntimeIdentities expectedIdentities,
        Lane0IntegrationOfficialReceiptCoordinator receiptCoordinator, string corpusRootToken)
    {
        ValidatePreparedBinaryClosure(prepared, projectAssemblyNames,
            "Prepared project binary closure changed before official run.");
        var buildRoot = Path.GetDirectoryName(prepared.WorkerAssemblyPath)
            ?? throw new InvalidDataException("Worker build directory is absent.");
        var attestationPath = UnderRoot(buildRoot, "official-launch-attestation.json");
        var responsePath = UnderRoot(Lane0IntegrationOfficialLayout.StagingRoot(prepared.ExecutionRoot),
            "response.json");
        WriteOfficialLaunchAttestation(attestationPath, prepared, canonicalSourceRoot,
            expectedHead, expectedIdentities);

        var result = StartOfficial(prepared.WorkerAssemblyPath, prepared.ExecutionRoot,
            receiptCoordinator, corpusRootToken);
        if (result.ExitCode != 0)
            throw new InvalidDataException($"Official ExecutionRoot command failed: {result.Error}{result.Output}");
        var response = JsonSerializer.Deserialize<Lane0IntegrationResearchResult>(
            File.ReadAllBytes(responsePath), JsonOptions)
            ?? throw new InvalidDataException("Official worker returned no response.");
        ValidatePreparedBinaryClosure(prepared, projectAssemblyNames,
            "Prepared project binary closure changed after official run.");
        return response;
    }

    private static (int ExitCode, string Output, string Error) StartOfficial(
        string assembly, string executionRoot,
        Lane0IntegrationOfficialReceiptCoordinator receiptCoordinator,
        string corpusRootToken)
    {
        using var session = Lane0IntegrationOfficialWorkerSession.Start(
            assembly, executionRoot);
        try
        {
            receiptCoordinator.CreateCanonicalDurableReceipt();
        }
        catch
        {
            session.Terminate();
            throw;
        }
        session.VerifyCreatedReceipt();
        return session.Complete(corpusRootToken);
    }

    internal static string OfficialAttestationPath(Lane0IntegrationPreparedRuntime prepared) =>
        UnderRoot(Path.GetDirectoryName(prepared.WorkerAssemblyPath)
            ?? throw new InvalidDataException("Worker build directory is absent."),
            "official-launch-attestation.json");

    internal static void WriteOfficialLaunchAttestation(
        string attestationPath, Lane0IntegrationPreparedRuntime prepared,
        string canonicalSourceRoot, string expectedHead,
        Lane0IntegrationRuntimeIdentities expectedIdentities)
    {
        var unsigned = new Lane0IntegrationOfficialLaunchAttestation(
            "lane-0-integration-official-launch-attestation.1",
            Path.GetFullPath(canonicalSourceRoot), Path.GetFullPath(prepared.ExecutionRoot),
            expectedHead, expectedIdentities, prepared.BinaryClosure,
            true, true, false, false, "");
        var canonicalHash = Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(unsigned with { CanonicalSha256 = "" }, JsonOptions)));
        File.WriteAllBytes(attestationPath, JsonSerializer.SerializeToUtf8Bytes(
            unsigned with { CanonicalSha256 = canonicalHash }, JsonOptions));
    }

    private static (int ExitCode, string Output, string Error) StartSynthetic(
        string assembly, string executionRoot, Lane0IntegrationWorkerRequest request)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = executionRoot, UseShellExecute = false,
            RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        };
        info.ArgumentList.Add(assembly); info.ArgumentList.Add("--synthetic-run");
        info.ArgumentList.Add("--execution-root"); info.ArgumentList.Add(executionRoot);
        info.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start synthetic worker.");
        var receiptBoundary = process.StandardOutput.ReadLine();
        if (receiptBoundary == "SYNTHETIC_RECEIPT_CREATED")
        {
            process.StandardInput.WriteLine(JsonSerializer.Serialize(request, JsonOptions));
            process.StandardInput.Close();
        }
        var output = receiptBoundary is null ? string.Empty : receiptBoundary + "\n";
        output += process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error);
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
        string executionRoot, string expectedHead, string expectedAssembly, string expectedHash,
        Lane0IntegrationBinaryClosure expectedClosure)
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
        Require(BinaryClosuresEqual(value.BinaryClosure, expectedClosure),
            "Runtime project binary closure drifted.");
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

    internal static Lane0IntegrationBinaryClosure ObserveBinaryClosure(
        string buildRoot, IEnumerable<string> expectedAssemblyNames)
    {
        var canonicalBuildRoot = Path.GetFullPath(buildRoot);
        var names = expectedAssemblyNames.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Require(names.Length > 0 && names.Distinct(StringComparer.Ordinal).Count() == names.Length
            && names.All(x => Path.GetFileName(x) == x && x.EndsWith(".dll", StringComparison.Ordinal)),
            "Project binary closure names are invalid or duplicated.");
        var expected = names.ToHashSet(StringComparer.Ordinal);
        var unexpectedProjectAssemblies = Directory.EnumerateFiles(canonicalBuildRoot, "ManiaAddNotesLab.*.dll")
            .Select(Path.GetFileName).Where(x => x is not null && !expected.Contains(x))
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Require(unexpectedProjectAssemblies.Length == 0,
            "Unexpected project-owned assembly appeared in the runtime closure.");
        var binaries = names.Select(name =>
        {
            var path = Path.GetFullPath(Path.Combine(canonicalBuildRoot, name));
            Require(IsUnder(canonicalBuildRoot, path) && File.Exists(path),
                $"Expected project binary is missing or escaped its build root: {name}.");
            return new Lane0IntegrationBinaryIdentity(name, path, Sha256File(path));
        }).ToImmutableArray();
        var rows = binaries.Select(x => $"{x.BinaryName}|{x.FullCanonicalPath}|{x.Sha256}");
        var aggregate = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', rows))));
        return new(binaries, aggregate);
    }

    internal static bool BinaryClosuresEqual(
        Lane0IntegrationBinaryClosure left, Lane0IntegrationBinaryClosure right) =>
        left.AggregateSha256 == right.AggregateSha256
        && left.Binaries.SequenceEqual(right.Binaries);

    internal static void ValidateDurableAuthorityClosure(
        Lane0IntegrationWorkerAuthorityState state,
        Lane0IntegrationBinaryClosure observed,
        Lane0IntegrationRuntimeIdentities expectedIdentities)
    {
        Require(state.BinaryClosure is not null,
            "Durable authority state has no project binary closure binding.");
        Require(state.ExecutionState == "RECEIPT_VERIFIED_ATTEMPT_CONSUMED",
            "Durable authority state does not prove post-receipt attempt consumption.");
        Require(state.ExpectedIdentities == expectedIdentities,
            "Durable authority state component identities drifted.");
        Require(BinaryClosuresEqual(state.BinaryClosure!, observed),
            "Durable authority project binary closure drifted.");
    }

    internal static string AuthorityStatePath(string workerAssemblyPath) => Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(workerAssemblyPath))
            ?? throw new InvalidDataException("Worker build directory is absent."),
        "official-authority-state.json");

    private static Lane0IntegrationWorkerAuthorityState LoadAuthorityState(string workerAssemblyPath)
    {
        var path = AuthorityStatePath(workerAssemblyPath);
        if (!File.Exists(path))
            throw new InvalidDataException(
                "Official authority state is absent; science cannot create staging or bypass receipt creation.");
        return JsonSerializer.Deserialize<Lane0IntegrationWorkerAuthorityState>(
            File.ReadAllBytes(path), JsonOptions)
            ?? throw new InvalidDataException("Official authority state is malformed.");
    }

    internal static void ValidatePreparedBinaryClosure(
        Lane0IntegrationPreparedRuntime prepared, IEnumerable<string> projectAssemblyNames,
        string message)
    {
        var current = ObserveBinaryClosure(
            Path.GetDirectoryName(prepared.WorkerAssemblyPath)
                ?? throw new InvalidDataException("Worker build directory is absent."),
            projectAssemblyNames);
        Require(BinaryClosuresEqual(current, prepared.BinaryClosure), message);
    }

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
        // Persistent build/compilation servers can inherit redirected pipe handles and keep an
        // otherwise completed isolated build from reaching an observable process boundary.
        info.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Could not start {file}.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(outputTask, errorTask);
        return (process.ExitCode, outputTask.Result, errorTask.Result);
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

/// <summary>
/// One already-running pre-receipt worker session. It can verify a receipt created by the sealed
/// coordinator, but deliberately has no receipt creation API and cannot attach after startup.
/// </summary>
internal sealed class Lane0IntegrationOfficialWorkerSession : IDisposable
{
    private readonly Process process;
    private string output;
    private bool completed;

    private Lane0IntegrationOfficialWorkerSession(Process process, string boundary)
    {
        this.process = process;
        output = boundary + "\n";
    }

    internal static Lane0IntegrationOfficialWorkerSession Start(
        string assembly, string executionRoot)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = executionRoot, UseShellExecute = false,
            RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        };
        info.ArgumentList.Add(assembly); info.ArgumentList.Add("--official-run");
        info.ArgumentList.Add("--execution-root"); info.ArgumentList.Add(executionRoot);
        info.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        var process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start official worker.");
        var boundary = process.StandardOutput.ReadLine();
        if (boundary != "PRE_RECEIPT_READY")
        {
            process.StandardInput.Close();
            var remaining = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            process.Dispose();
            throw new InvalidDataException(
                $"Official worker failed before PRE_RECEIPT_READY: {boundary}{remaining}{error}");
        }
        return new(process, boundary);
    }

    internal void VerifyCreatedReceipt()
    {
        Require(!completed, "Official worker session is already complete.");
        process.StandardInput.WriteLine("RECEIPT_CREATED");
        process.StandardInput.Flush();
        var boundary = process.StandardOutput.ReadLine();
        output += boundary is null ? string.Empty : boundary + "\n";
        Require(boundary == "RECEIPT_VERIFIED",
            "Official worker did not independently verify the canonical receipt.");
    }

    internal (int ExitCode, string Output, string Error) Complete(string corpusRootToken)
    {
        Require(!completed, "Official worker session is already complete.");
        process.StandardInput.WriteLine(JsonSerializer.Serialize(corpusRootToken));
        process.StandardInput.Close();
        output += process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        completed = true;
        return (process.ExitCode, output, error);
    }

    internal void Terminate()
    {
        if (completed) return;
        try { process.StandardInput.Close(); } catch (ObjectDisposedException) { }
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        process.WaitForExit();
        completed = true;
    }

    public void Dispose()
    {
        Terminate();
        process.Dispose();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
