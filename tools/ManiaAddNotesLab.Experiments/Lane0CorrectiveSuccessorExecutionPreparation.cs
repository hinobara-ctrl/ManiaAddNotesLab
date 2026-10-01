using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ManiaAddNotesLab.Core;

internal sealed record Lane0SuccessorExecutionContractState(
    string Phase1ContractSha256,
    string Phase2ContractSha256,
    string ManifestSha256,
    string ScientificImplementationSha256,
    string HarnessSha256,
    string PackageVerifierSha256,
    string FrozenDependenciesSha256,
    string ApprovedPublicHead);

internal sealed record Lane0SuccessorObservedIdentities(
    string Phase1ContractSha256,
    string Phase2ContractSha256,
    string ScientificImplementationSha256,
    string HarnessSha256,
    string PackageVerifierSha256,
    string FrozenDependenciesSha256);

internal sealed record Lane0SuccessorBindingFields(
    string SchemaVersion,
    string ApprovedPublicHead,
    string Phase1ContractSha256,
    string Phase2ContractSha256,
    string ScientificImplementationSha256,
    string HarnessSha256,
    string PackageVerifierSha256,
    string FrozenDependenciesSha256,
    bool ExplicitHumanAuthorization,
    int AuthorizedExecutionCount);

internal sealed record Lane0SuccessorVerifiedBinding(
    ImmutableArray<byte> CanonicalBytes,
    string CanonicalSha256,
    Lane0SuccessorBindingFields Fields);

internal sealed record Lane0SuccessorExecutionRequest(
    Lane0SuccessorExecutionContractState Contract,
    Lane0SuccessorObservedIdentities Observed,
    VerifiedFrozenC11Manifest Manifest,
    ImmutableArray<Lane0CorrectiveSuccessorReference> References,
    Lane0SuccessorVerifiedBinding? Binding,
    string SourceRoot,
    string ExecutionRoot,
    string CorpusRoot,
    string SourceHead,
    string ExecutionHead,
    string RuntimeHead,
    bool SourceTrackedClean);

internal sealed record Lane0SuccessorPreparationResult(
    string PreparationOutcome,
    string? ScientificOutcome,
    string Reason,
    int CorpusAccessCalls,
    bool ReceiptCreated,
    int ScientificPassesStarted,
    int ScientificPassesCompleted);

internal sealed record Lane0SuccessorEvaluationContext(
    string ApprovedPublicHead,
    string Phase1ContractSha256,
    string Phase2ContractSha256,
    string ManifestSha256,
    string ScientificImplementationSha256,
    string HarnessSha256,
    string PackageVerifierSha256,
    string FrozenDependenciesSha256,
    ImmutableArray<Lane0CorrectiveSuccessorReference> References);

internal sealed record Lane0SuccessorEvaluationPass(
    string ScientificOutcome,
    ImmutableSortedDictionary<string, byte[]> Package,
    ImmutableArray<string> IntegrityFailures,
    ImmutableArray<string> BlockedReasons,
    int RngCalls,
    bool InputsUnchanged,
    bool BehaviorChanged,
    bool DefaultChanged);

internal interface ILane0SuccessorCorpusAdapter
{
    int AccessCalls { get; }
    ImmutableArray<Lane0CorrectiveChartInput> Load(VerifiedFrozenC11Manifest manifest);
}

internal interface ILane0SuccessorScientificEvaluator
{
    Lane0SuccessorEvaluationPass Evaluate(
        ImmutableArray<Lane0CorrectiveChartInput> inputs,
        Lane0SuccessorEvaluationContext context);
}

/// <summary>
/// Fail-closed future execution gate. There is deliberately no CLI/Web call site. Tests provide
/// synthetic authority roots and adapters; canonical successor authority paths remain absent.
/// </summary>
internal static class Lane0CorrectiveSuccessorExecutionPreparation
{
    internal const string PhaseId = "LANE.0.CORRECTIVE_SUCCESSOR.EXECUTION_PREPARATION";
    internal const string ContractSchema = "lane-0-corrective-successor-execution-preparation.1";
    internal const string BindingSchema = "lane-0-corrective-successor-publication-binding.1";
    internal const string ReceiptSchema = "lane-0-corrective-successor-attempt.1";
    internal const string Phase1ContractSha256 =
        "66B0D27535B8EEE5B466DDAB1209DD0C548B982C24913E1993E611D57E1BE505";
    internal const string ManifestSha256 =
        "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    internal const string CanonicalBindingRelativePath =
        "docs/lane_0_corrective_successor_publication_binding.json";
    internal const string CanonicalReceiptRelativePath =
        ".artifacts/lane_0_corrective_successor.attempt.json";
    internal const string CanonicalOutputRelativePath =
        ".artifacts/lane_0_corrective_successor";

    internal static Lane0SuccessorPreparationResult ValidateStatic(
        Lane0SuccessorExecutionContractState contract,
        Lane0SuccessorObservedIdentities observed,
        VerifiedFrozenC11Manifest manifest,
        ImmutableArray<Lane0CorrectiveSuccessorReference> references)
    {
        if (contract.Phase1ContractSha256 != Phase1ContractSha256
            || observed.Phase1ContractSha256 != contract.Phase1ContractSha256)
            return Invalid("Phase 1 successor contract identity drifted.");
        if (observed.Phase2ContractSha256 != contract.Phase2ContractSha256)
            return Invalid("Phase 2 execution contract identity drifted.");
        if (contract.ManifestSha256 != ManifestSha256
            || manifest.CanonicalSha256 != contract.ManifestSha256)
            return Invalid("Verified manifest identity drifted.");
        if (observed.ScientificImplementationSha256 != contract.ScientificImplementationSha256)
            return Invalid("Successor scientific implementation identity drifted.");
        if (observed.HarnessSha256 != contract.HarnessSha256)
            return Invalid("Successor execution harness identity drifted.");
        if (observed.PackageVerifierSha256 != contract.PackageVerifierSha256)
            return Invalid("Successor package verifier identity drifted.");
        if (observed.FrozenDependenciesSha256 != contract.FrozenDependenciesSha256)
            return Invalid("Successor frozen dependency identity drifted.");

        var referencesResult = Lane0CorrectiveSuccessorReferenceValidator.Validate(
            manifest, references, Lane0CorrectiveSuccessorReferenceValidator.ExpectedHistoricalOperationalCount);
        return referencesResult.IsValid
            ? new("READY", null, "Verified manifest and successor references are coupled and valid.",
                0, false, 0, 0)
            : Invalid(string.Join(" ", referencesResult.Errors));
    }

    internal static Lane0SuccessorVerifiedBinding CreateVerifiedBinding(
        Lane0SuccessorBindingFields fields)
    {
        var bytes = CanonicalBindingBytes(fields);
        return new(bytes.ToImmutableArray(), Convert.ToHexString(SHA256.HashData(bytes)), fields);
    }

    internal static Lane0SuccessorPreparationResult ExecuteSynthetic(
        Lane0SuccessorExecutionRequest request,
        ILane0SuccessorCorpusAdapter adapter,
        ILane0SuccessorScientificEvaluator evaluator)
    {
        var preflight = ValidateStatic(request.Contract, request.Observed, request.Manifest,
            request.References);
        if (preflight.PreparationOutcome != "READY") return WithAccess(preflight, adapter);

        var binding = request.Binding;
        if (binding is null) return Blocked("Successor publication binding is absent.", adapter);
        var fields = binding.Fields;
        var verifiedBytes = CanonicalBindingBytes(fields);
        if (!binding.CanonicalBytes.AsSpan().SequenceEqual(verifiedBytes)
            || binding.CanonicalSha256 != Convert.ToHexString(SHA256.HashData(verifiedBytes)))
            return Invalid("Successor binding canonical byte identity drifted.", adapter);
        if (fields.SchemaVersion != BindingSchema)
            return Invalid("Successor publication binding schema drifted.", adapter);
        if (!fields.ExplicitHumanAuthorization)
            return Blocked("Explicit human authorization is false.", adapter);
        if (fields.AuthorizedExecutionCount != 1)
            return Invalid("Authorized execution count must equal one.", adapter);
        if (fields.Phase1ContractSha256 != request.Contract.Phase1ContractSha256
            || fields.Phase2ContractSha256 != request.Contract.Phase2ContractSha256
            || fields.ScientificImplementationSha256 != request.Contract.ScientificImplementationSha256
            || fields.HarnessSha256 != request.Contract.HarnessSha256
            || fields.PackageVerifierSha256 != request.Contract.PackageVerifierSha256
            || fields.FrozenDependenciesSha256 != request.Contract.FrozenDependenciesSha256)
            return Invalid("Successor binding identity drifted.", adapter);
        if (fields.ApprovedPublicHead != request.Contract.ApprovedPublicHead)
            return Invalid("Successor binding HEAD drifted.", adapter);
        if (!request.SourceTrackedClean)
            return Invalid("Authorization source tracked tree is dirty.", adapter);
        if (request.SourceHead != request.Contract.ApprovedPublicHead
            || request.ExecutionHead != request.Contract.ApprovedPublicHead
            || request.RuntimeHead != request.Contract.ApprovedPublicHead)
            return Invalid("Binding, source, isolated and runtime HEADs are not identical.", adapter);
        string source;
        string execution;
        try
        {
            source = Path.GetFullPath(request.SourceRoot);
            execution = Path.GetFullPath(request.ExecutionRoot);
            if (!RootsAreIsolated(source, execution))
                return Invalid("Source and execution roots must be disjoint and non-nested.", adapter);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
            or PathTooLongException)
        {
            return Invalid($"Source or execution root is invalid: {exception.Message}", adapter);
        }
        var receiptPath = Path.Combine(source, CanonicalReceiptRelativePath);
        var finalOutputPath = Path.Combine(execution, CanonicalOutputRelativePath);
        if (File.Exists(receiptPath) || Directory.Exists(receiptPath))
            return Blocked("Successor one-shot receipt already exists.", adapter);
        if (File.Exists(finalOutputPath) || Directory.Exists(finalOutputPath))
            return Blocked("Successor execution destination is not empty.", adapter);

        try
        {
            CreateReceipt(receiptPath, request.Contract, binding);
        }
        catch (IOException exception)
        {
            return Blocked($"Successor one-shot receipt could not be acquired: {exception.Message}", adapter);
        }

        string corpus;
        try
        {
            corpus = Path.GetFullPath(request.CorpusRoot);
            if (!RootsAreIsolated(source, corpus) || !RootsAreIsolated(execution, corpus))
                return InvalidAfterReceipt("Corpus root must be disjoint from source and execution roots.",
                    null, adapter, 0, 0);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
            or PathTooLongException)
        {
            return InvalidAfterReceipt($"Corpus root is invalid after receipt: {exception.Message}",
                null, adapter, 0, 0);
        }

        var started = 0;
        var completed = 0;
        try
        {
            var context = new Lane0SuccessorEvaluationContext(
                request.Contract.ApprovedPublicHead, request.Contract.Phase1ContractSha256,
                request.Contract.Phase2ContractSha256, request.Contract.ManifestSha256,
                request.Contract.ScientificImplementationSha256, request.Contract.HarnessSha256,
                request.Contract.PackageVerifierSha256, request.Contract.FrozenDependenciesSha256,
                request.References);
            var firstInputs = adapter.Load(request.Manifest);
            started++;
            var first = evaluator.Evaluate(firstInputs, context);
            completed++;
            var secondInputs = adapter.Load(request.Manifest);
            started++;
            var second = evaluator.Evaluate(secondInputs, context);
            completed++;
            Lane0CorrectiveSuccessorPackage.Verify(first.Package);
            Lane0CorrectiveSuccessorPackage.Verify(second.Package);
            if (!Lane0CorrectiveSuccessorPackage.ByteIdentical(first.Package, second.Package))
                return InvalidAfterReceipt("Independent successor packages are not byte-identical.",
                    null, adapter, started, completed);
            if (first.ScientificOutcome != second.ScientificOutcome)
                return InvalidAfterReceipt("Independent scientific outcomes differ.", null,
                    adapter, started, completed);
            if (first.RngCalls != 0 || second.RngCalls != 0)
                return InvalidAfterReceipt("Successor evaluation consumed RNG.", first.ScientificOutcome,
                    adapter, started, completed);
            if (!first.InputsUnchanged || !second.InputsUnchanged)
                return InvalidAfterReceipt("Successor evaluation mutated scientific inputs.",
                    first.ScientificOutcome, adapter, started, completed);
            if (first.BehaviorChanged || second.BehaviorChanged
                || first.DefaultChanged || second.DefaultChanged)
                return InvalidAfterReceipt("Successor evaluation changed behavior or defaults.",
                    first.ScientificOutcome, adapter, started, completed);
            if (first.ScientificOutcome == "INVALID")
                return InvalidAfterReceipt("Successor scientific evaluator returned INVALID.", "INVALID",
                    adapter, started, completed);
            if (first.ScientificOutcome == "BLOCKED")
                return InvalidAfterReceipt("Unexpected post-receipt scientific BLOCKED fails closed.",
                    "BLOCKED", adapter, started, completed);
            if (first.ScientificOutcome is not ("FEASIBILITY_DEMONSTRATED" or "LIMITED_PARK"))
                return InvalidAfterReceipt("Unknown successor scientific outcome.",
                    first.ScientificOutcome, adapter, started, completed);
            return new("PREPARATION_VALIDATED", first.ScientificOutcome,
                "Synthetic two-pass route completed after durable one-shot receipt.",
                adapter.AccessCalls, true, started, completed);
        }
        catch (Exception exception)
        {
            return InvalidAfterReceipt(exception.Message, null, adapter, started, completed);
        }
    }

    internal static void CreateReceipt(string path, Lane0SuccessorExecutionContractState contract,
        Lane0SuccessorVerifiedBinding binding)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = ReceiptSchema,
            approvedPublicHead = contract.ApprovedPublicHead,
            bindingSha256 = binding.CanonicalSha256,
            phase1ContractSha256 = contract.Phase1ContractSha256,
            phase2ContractSha256 = contract.Phase2ContractSha256,
            attemptCount = 1
        }, new JsonSerializerOptions { WriteIndented = true });
        bytes = [.. bytes, (byte)'\n'];
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(true);
    }

    private static bool RootsAreIsolated(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var leftRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
        var rightRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
        if (string.Equals(leftRoot, rightRoot, comparison)) return false;
        return !IsAncestor(leftRoot, rightRoot, comparison)
            && !IsAncestor(rightRoot, leftRoot, comparison);
    }

    private static bool IsAncestor(string parent, string child, StringComparison comparison) =>
        child.StartsWith(parent + Path.DirectorySeparatorChar, comparison)
        || child.StartsWith(parent + Path.AltDirectorySeparatorChar, comparison);

    private static byte[] CanonicalBindingBytes(Lane0SuccessorBindingFields fields)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(fields, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        return [.. bytes, (byte)'\n'];
    }

    private static Lane0SuccessorPreparationResult Invalid(string reason) =>
        new("INVALID", null, reason, 0, false, 0, 0);
    private static Lane0SuccessorPreparationResult Invalid(string reason,
        ILane0SuccessorCorpusAdapter adapter) => new("INVALID", null, reason,
            adapter.AccessCalls, false, 0, 0);
    private static Lane0SuccessorPreparationResult Blocked(string reason,
        ILane0SuccessorCorpusAdapter adapter) => new("BLOCKED", null, reason,
            adapter.AccessCalls, false, 0, 0);
    private static Lane0SuccessorPreparationResult InvalidAfterReceipt(string reason,
        string? scientificOutcome, ILane0SuccessorCorpusAdapter adapter, int started, int completed) =>
        new("INVALID", scientificOutcome, reason, adapter.AccessCalls, true, started, completed);
    private static Lane0SuccessorPreparationResult WithAccess(Lane0SuccessorPreparationResult result,
        ILane0SuccessorCorpusAdapter adapter) => result with { CorpusAccessCalls = adapter.AccessCalls };
}
