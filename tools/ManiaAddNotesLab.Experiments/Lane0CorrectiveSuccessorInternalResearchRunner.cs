using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ManiaAddNotesLab.Core;

internal sealed record Lane0IntegrationResearchRequest(
    Lane0IntegrationLaunchRequest Launch,
    VerifiedFrozenC11Manifest Manifest,
    ImmutableArray<Lane0CorrectiveSuccessorReference> References);

internal sealed record Lane0IntegrationResearchResult(
    string Classification, string? ScientificOutcome, bool ReceiptConsumed,
    int ScientificPassesStarted, int ScientificPassesCompleted,
    ImmutableArray<string> CompletedAuthoritySteps, string Reason);

internal interface ILane0IntegrationPackageFinalizer
{
    ImmutableSortedDictionary<string, byte[]> Finalize(
        ImmutableSortedDictionary<string, byte[]> phase2Package,
        Lane0IntegrationRuntimeIdentities identities, bool defaultChanged);
}

internal sealed class Lane0IntegrationPackageFinalizer : ILane0IntegrationPackageFinalizer
{
    public ImmutableSortedDictionary<string, byte[]> Finalize(
        ImmutableSortedDictionary<string, byte[]> phase2Package,
        Lane0IntegrationRuntimeIdentities identities, bool defaultChanged)
    {
        if (!phase2Package.ContainsKey("execution_identity.json")
            || !phase2Package.ContainsKey("sha256sums.txt"))
            throw new InvalidDataException("Phase 2 package cannot receive integration identity fields.");
        var identity = JsonNode.Parse(phase2Package["execution_identity.json"])?.AsObject()
            ?? throw new InvalidDataException("Phase 2 execution identity is malformed.");
        identity["integrationPreregistrationSha256"] = identities.IntegrationPreregistrationSha256;
        identity["integrationExecutionContractSha256"] = identities.IntegrationExecutionContractSha256;
        identity["adapterSha256"] = identities.AdapterSha256;
        identity["runnerSha256"] = identities.RunnerSha256;
        identity["launcherSha256"] = identities.LauncherSha256;
        identity["semanticVerifierSha256"] = identities.SemanticVerifierSha256;
        identity["defaultChanged"] = defaultChanged;
        var identityBytes = Encoding.UTF8.GetBytes(identity.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
        var semantic = phase2Package.Remove("sha256sums.txt")
            .SetItem("execution_identity.json", identityBytes);
        var checksums = Encoding.UTF8.GetBytes(string.Join('\n', semantic.Select(x =>
            $"{Convert.ToHexString(SHA256.HashData(x.Value))}  {x.Key}")) + "\n");
        return semantic.SetItem("sha256sums.txt", checksums);
    }
}

/// <summary>
/// Internal research-only orchestrator. It has no Program, CLI, Web or AddNotesEngine call site.
/// Frozen Phase 2 science is injected and invoked twice over one admitted immutable corpus.
/// </summary>
internal sealed class Lane0CorrectiveSuccessorInternalResearchRunner
{
    private readonly Lane0CorrectiveSuccessorIsolatedLauncher launcher;
    private readonly ILane0IntegrationFrozenCorpusAdapter adapter;
    private readonly ILane0SuccessorScientificEvaluator science;
    private readonly ILane0IntegrationDeepSemanticVerifier semanticVerifier;
    private readonly ILane0IntegrationPackageFinalizer packageFinalizer;

    internal Lane0CorrectiveSuccessorInternalResearchRunner(
        Lane0CorrectiveSuccessorIsolatedLauncher launcher,
        ILane0IntegrationFrozenCorpusAdapter adapter,
        ILane0SuccessorScientificEvaluator science,
        ILane0IntegrationDeepSemanticVerifier semanticVerifier,
        ILane0IntegrationPackageFinalizer packageFinalizer)
    {
        this.launcher = launcher;
        this.adapter = adapter;
        this.science = science;
        this.semanticVerifier = semanticVerifier;
        this.packageFinalizer = packageFinalizer;
    }

    internal Lane0IntegrationResearchResult ExecuteSyntheticOrFutureAuthorized(
        Lane0IntegrationResearchRequest request, ILane0IntegrationAuthorityStore authorityStore)
    {
        Lane0IntegrationPreReceiptLease preReceipt;
        try
        {
            preReceipt = launcher.Acquire(request.Launch, authorityStore);
        }
        catch (Exception exception)
        {
            return new("NON_PUBLISHABLE", null, authorityStore.ReceiptExists, 0, 0,
                ImmutableArray<string>.Empty, exception.Message);
        }

        var steps = preReceipt.CompletedAuthoritySteps.ToBuilder();
        var started = 0;
        var completed = 0;
        try
        {
            var postReceipt = launcher.InterpretCorpusRoot(preReceipt);
            steps = postReceipt.CompletedAuthoritySteps.ToBuilder();
            var admitted = adapter.Admit(request.Manifest, postReceipt.CanonicalCorpusRoot);
            steps.Add(Lane0CorrectiveSuccessorIsolatedLauncher.AuthorityOrder[15]);
            var context = new Lane0SuccessorEvaluationContext(
                request.Launch.ExpectedSourcePublicHead,
                request.Launch.ExpectedIdentities.Phase1ContractSha256,
                request.Launch.ExpectedIdentities.Phase2ContractSha256,
                request.Launch.ExpectedIdentities.ManifestSha256,
                request.Launch.ExpectedIdentities.ScientificImplementationSha256,
                request.Launch.ExpectedIdentities.Phase2HarnessSha256,
                request.Launch.ExpectedIdentities.Phase2PackageVerifierSha256,
                request.Launch.ExpectedIdentities.FrozenDependenciesSha256,
                request.References);

            started++;
            var first = science.Evaluate(admitted.ScientificCharts.ToImmutableArray(), context);
            completed++;
            steps.Add(Lane0CorrectiveSuccessorIsolatedLauncher.AuthorityOrder[16]);
            started++;
            var second = science.Evaluate(admitted.ScientificCharts.ToImmutableArray(), context);
            completed++;
            steps.Add(Lane0CorrectiveSuccessorIsolatedLauncher.AuthorityOrder[17]);

            var firstPackage = packageFinalizer.Finalize(first.Package,
                request.Launch.ExpectedIdentities, first.DefaultChanged);
            var secondPackage = packageFinalizer.Finalize(second.Package,
                request.Launch.ExpectedIdentities, second.DefaultChanged);

            Require(firstPackage.Keys.SequenceEqual(secondPackage.Keys)
                && firstPackage.All(x => x.Value.AsSpan().SequenceEqual(secondPackage[x.Key])),
                "Independent scientific packages are not byte-identical.");
            Require(first.ScientificOutcome == second.ScientificOutcome,
                "Independent scientific outcomes disagree.");
            steps.Add(Lane0CorrectiveSuccessorIsolatedLauncher.AuthorityOrder[18]);
            Require(first.RngCalls == 0 && second.RngCalls == 0, "Successor science consumed RNG.");
            Require(first.InputsUnchanged && second.InputsUnchanged,
                "Successor science mutated admitted inputs.");
            Require(!first.BehaviorChanged && !second.BehaviorChanged
                && !first.DefaultChanged && !second.DefaultChanged,
                "Successor science changed behavior or defaults.");
            Require(first.ScientificOutcome is "FEASIBILITY_DEMONSTRATED" or "LIMITED_PARK",
                "Successor science did not produce a publishable classification.");

            semanticVerifier.Verify(firstPackage, new(
                request.Launch.ExpectedIdentities, request.Launch.ExpectedSourcePublicHead,
                first.ScientificOutcome, admitted, first.DefaultChanged));
            steps.Add(Lane0CorrectiveSuccessorIsolatedLauncher.AuthorityOrder[19]);
            steps.Add(Lane0CorrectiveSuccessorIsolatedLauncher.AuthorityOrder[20]);
            return new("PUBLISHABLE", first.ScientificOutcome, true, started, completed,
                steps.ToImmutable(), "Two independent passes and deep semantics verified.");
        }
        catch (Exception exception)
        {
            return new("NON_PUBLISHABLE", null, true, started, completed,
                steps.ToImmutable(), exception.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
