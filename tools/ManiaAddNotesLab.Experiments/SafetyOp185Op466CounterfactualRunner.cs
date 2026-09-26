using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

public sealed record SingleRngInterventionPlan(long PositionBeforeCall, int ExpectedMaximum,
    int ExpectedAuthenticValue, int DeliveredValue);

public sealed record SingleRngInterventionEvent(long PositionBeforeCall, int Maximum,
    int AuthenticValue, int DeliveredValue);

public sealed class SingleRngInterventionRandom(int seed, SingleRngInterventionPlan? plan) : IRandomPositionSource
{
    private readonly SeededRandom inner = new(seed);
    private readonly SafetyRemediationGateTranscriptHash transcript = new();
    private readonly ImmutableArray<SelectionRandomCall>.Builder authenticCalls =
        ImmutableArray.CreateBuilder<SelectionRandomCall>();
    private readonly ImmutableArray<SingleRngInterventionEvent>.Builder interventions =
        ImmutableArray.CreateBuilder<SingleRngInterventionEvent>();

    public long CallCount => authenticCalls.Count;
    public string AuthenticTranscriptSha256 => transcript.CurrentHash;
    public ImmutableArray<SelectionRandomCall> AuthenticCalls => authenticCalls.ToImmutable();
    public ImmutableArray<SingleRngInterventionEvent> Interventions => interventions.ToImmutable();

    public double NextDouble()
    {
        var before = CallCount;
        if (plan is not null && before == plan.PositionBeforeCall)
            throw new InvalidDataException("Preregistered intervention call was Double instead of Integer.");
        var value = inner.NextDouble();
        transcript.AppendDouble(value);
        authenticCalls.Add(new(SelectionRandomCallKind.Double, null, value, null));
        return value;
    }

    public int Next(int maxExclusive)
    {
        var before = CallCount;
        var authentic = inner.Next(maxExclusive);
        transcript.AppendInteger(maxExclusive, authentic);
        authenticCalls.Add(new(SelectionRandomCallKind.Integer, maxExclusive, null, authentic));
        if (plan is null || before != plan.PositionBeforeCall) return authentic;
        if (interventions.Count != 0)
            throw new InvalidDataException("Preregistered intervention was invoked more than once.");
        if (maxExclusive != plan.ExpectedMaximum || authentic != plan.ExpectedAuthenticValue)
            throw new InvalidDataException("Preregistered RNG call identity drifted.");
        if (plan.DeliveredValue < 0 || plan.DeliveredValue >= maxExclusive)
            throw new InvalidDataException("Preregistered delivered RNG index is outside Next(maximum).");
        interventions.Add(new(before, maxExclusive, authentic, plan.DeliveredValue));
        return plan.DeliveredValue;
    }
}

public enum CounterfactualGuardFailure
{
    None,
    ParentStateMismatch,
    RngPrefixMismatch,
    OpportunityKeyMismatch,
    InterventionCountMismatch,
    InterventionLaneIllegal,
    RngCallOmitted,
    AdditionalRngCall,
    SharedMutableState,
    InstrumentationNotNeutral
}

public sealed record CounterfactualGuardObservation(bool ParentsEqualComplete, bool RngPrefixExact,
    string OpportunityKey, int InterventionCount, bool InterventionLaneLegal,
    int ControlRngCallsAtOpportunity, int TreatmentRngCallsAtOpportunity,
    bool IndependentMutableState, bool? InstrumentedNoChangeExact);

public static class CounterfactualTrajectoryGuardResearch
{
    public const string ExpectedOpportunity = "OP-00000185-BaseHead-S185-T16399-ANA";

    public static CounterfactualGuardFailure Validate(CounterfactualGuardObservation value)
    {
        if (!value.ParentsEqualComplete) return CounterfactualGuardFailure.ParentStateMismatch;
        if (!value.RngPrefixExact) return CounterfactualGuardFailure.RngPrefixMismatch;
        if (value.OpportunityKey != ExpectedOpportunity) return CounterfactualGuardFailure.OpportunityKeyMismatch;
        if (value.InterventionCount != 1) return CounterfactualGuardFailure.InterventionCountMismatch;
        if (!value.InterventionLaneLegal) return CounterfactualGuardFailure.InterventionLaneIllegal;
        if (value.ControlRngCallsAtOpportunity == 0 || value.TreatmentRngCallsAtOpportunity == 0)
            return CounterfactualGuardFailure.RngCallOmitted;
        if (value.ControlRngCallsAtOpportunity != 1 || value.TreatmentRngCallsAtOpportunity != 1)
            return CounterfactualGuardFailure.AdditionalRngCall;
        if (!value.IndependentMutableState) return CounterfactualGuardFailure.SharedMutableState;
        if (value.InstrumentedNoChangeExact == false) return CounterfactualGuardFailure.InstrumentationNotNeutral;
        return CounterfactualGuardFailure.None;
    }

    public static string ClassifyTarget(bool controlExactCommitted, bool treatmentExactCommitted,
        bool treatmentSimilarNotExact) => !controlExactCommitted && treatmentExactCommitted
            ? "RECOVERED_EXACT" : treatmentSimilarNotExact ? "SIMILAR_NOT_EXACT"
            : !controlExactCommitted && !treatmentExactCommitted ? "ABSENT_BOTH"
            : "UNEXPECTED_BASELINE";

    public static void RequireFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
            if (!File.Exists(path)) throw new FileNotFoundException("Frozen prerequisite is missing.", path);
    }
}

internal static class SafetyOp185Op466CounterfactualRunner
{
    private const string MemoryLimit = "0x400000000";
    private const string ChartId = "20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788";
    private const int Seed = 4;
    private const string Op185 = "OP-00000185-BaseHead-S185-T16399-ANA";
    private const string Op370 = "OP-00000370-BaseHead-S370-T29933-ANA";
    private const string Op466 = "OP-00000466-BaseHead-S466-T35009-ANA";
    private const string ExpectedTargetCommit =
        "2|35009||Tap|466|HeadOpportunity|74.49995000000002979998|74.49995000000002979998";
    private const string ExpectedImplementation =
        "B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835";
    private const string ExpectedCorpus =
        "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    private const string ExpectedCanonicalOutput =
        "462307AE599D390CCC56595CCBE9ABAB02D36EF35AC4E00A69D270963438E999";
    private const string ExpectedCanonicalRng =
        "8F5589B0045254ECCAE68DFD3E01037D8C5771CA6BD7081132B173C2B6762DED";
    private const string ExpectedCanonicalDiagnostics =
        "26804F0A5753C75738C7CC14286CDA090C847292242649CFBBD3CCE534D74875";

    private static readonly SingleRngInterventionPlan PassThrough = new(260, 4, 2, 2);
    private static readonly SingleRngInterventionPlan Intervention = new(260, 4, 2, 3);
    private static readonly string[] ImplementationFiles =
    [
        "src/ManiaAddNotesLab.Core/AddNotesEngine.cs", "src/ManiaAddNotesLab.Core/BeatTimeline.cs",
        "src/ManiaAddNotesLab.Core/ChartAnalysis.cs", "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
        "src/ManiaAddNotesLab.Core/Model.cs", "src/ManiaAddNotesLab.Core/OsuBeatmap.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateResearch.cs"
    ];
    private static readonly string[] HarnessFiles =
    [
        "src/ManiaAddNotesLab.Core/GenerationProvenanceResearch.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateHardeningResearch.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationForensicAttributionResearch.cs",
        "src/ManiaAddNotesLab.Core/SelectionSetRemappingResearch.cs",
        "tests/ManiaAddNotesLab.Tests/SafetyOp185Op466CounterfactualTests.cs",
        "tools/ManiaAddNotesLab.Experiments/Program.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetyOp185Op466CounterfactualRunner.cs"
    ];
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions Canonical = new(Json) { WriteIndented = false };

    public static string Prepare(string repositoryRoot, string corpusRoot, string manifestPath,
        string contractPath, string outputDirectory)
    {
        RequireMemoryLimit();
        RequireHistoricalDependencies(repositoryRoot);
        var implementation = Snapshot(repositoryRoot, ImplementationFiles);
        if (implementation != ExpectedImplementation)
            throw new InvalidDataException("Behavioral implementation differs from the frozen identity.");
        var harness = Snapshot(repositoryRoot, HarnessFiles);
        var manifest = FrozenC11ManifestResearch.Load(manifestPath);
        if (manifest.CanonicalSha256 != ExpectedCorpus)
            throw new InvalidDataException("C11 identity drifted.");
        var descriptor = C11CorpusDiscovery.ResolveFrozen(corpusRoot, manifest.Charts).Charts
            .Single(x => x.Sha256 == ChartId);
        var chartText = File.ReadAllText(descriptor.RuntimePath);
        Directory.CreateDirectory(outputDirectory);

        var reference = Execute(chartText, null);
        var repeat = Execute(chartText, null);
        var passThrough = Execute(chartText, PassThrough);
        var exactRepeat = Equivalent(reference, repeat);
        var instrumentationNeutral = Equivalent(reference, passThrough);
        var identity = InspectPrerequisites(reference);
        if (!exactRepeat || !instrumentationNeutral || !identity.Valid)
            throw new InvalidDataException("Preflight canonical controls did not pass; contract was not frozen.");
        var noSharedMutableState = !ReferenceEquals(reference.ChartInstance, repeat.ChartInstance)
            && !ReferenceEquals(reference.ProfileInstance, repeat.ProfileInstance)
            && !ReferenceEquals(reference.ChartInstance, passThrough.ChartInstance)
            && !ReferenceEquals(reference.ProfileInstance, passThrough.ProfileInstance);
        if (!noSharedMutableState) throw new InvalidDataException("Experimental arms share mutable setup state.");

        var preflight = new PreflightControls(exactRepeat, instrumentationNeutral, noSharedMutableState,
            reference.OutputSha256, reference.RngCalls, reference.AuthenticRngSha256,
            reference.DiagnosticsSha256!, identity, passThrough.Random.Interventions);
        var preflightPath = Path.Combine(outputDirectory, "preflight_controls.json");
        WriteJson(preflightPath, preflight);
        var historical = HistoricalInputs(repositoryRoot);
        var contract = new CounterfactualContract(
            "safety-op185-op466-counterfactual-contract.1", GitHead(repositoryRoot), implementation,
            harness, manifest.CanonicalSha256, HistoricalIdentity(historical), FileHash(preflightPath),
            ChartId, Seed, "canonical remediation treatment in both arms", Op185, 16399,
            [0, 1, 2, 3], 2, 3, 260, 4, 261,
            "complete SafetyRemediationGateSufficientState: materialized, latent, root/stage RNG, opportunity cursor, pending articulation, opportunity sequence",
            "parents complete and byte/value equal before OP-185; same configuration, key, authentic RNG prefix and independent mutable setup",
            "two normal canonical runs and instrumented pass-through are exact in output, commits, RNG, diagnostics and G1-null identity",
            "OP-466 recovery requires exact OpportunityKey, provenance S466, lane 2, start 35009 and commit identity " + ExpectedTargetCommit,
            ["RECOVERED_EXACT", "ABSENT_BOTH", "SIMILAR_NOT_EXACT", "UNEXPECTED_BASELINE", "INVALID", "BLOCKED"],
            ["baseline drift", "pre-OP185 inequality", "RNG call drift", "illegal lane 3", "more than one intervention", "missing frozen dependency", "resource failure"],
            historical, MemoryLimit);
        var contractHash = Hash(JsonSerializer.Serialize(contract, Canonical));
        var artifact = new CounterfactualContractArtifact(contract, contractHash,
            "UTF-8 System.Text.Json camelCase; ordered arrays; no timestamps or machine paths");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        WriteJson(contractPath, artifact);
        WriteJson(Path.Combine(outputDirectory, "frozen_contract.json"), artifact);
        WriteJson(Path.Combine(outputDirectory, "identity_inventory.json"), new
        {
            repositoryEntryHead = contract.RepositoryEntryHead, implementation, harness,
            corpus = manifest.CanonicalSha256, historicalArtifacts = contract.HistoricalArtifactsSha256,
            preflight = contract.PreflightControlsSha256, contract = contractHash
        });
        WriteJson(Path.Combine(outputDirectory, "case_manifest.json"), new
        {
            chartId = ChartId, seed = Seed, control = "CANONICAL_NORMAL",
            treatment = "CANONICAL_SINGLE_INTERVENTION", opportunity = Op185,
            normalLane = 2, interventionLane = 3, rngPositionBefore = 260, rngPositionAfter = 261,
            target = new { opportunity = Op466, lane = 2, startTime = 35009, commit = ExpectedTargetCommit }
        });
        WriteHashes(outputDirectory);
        return contractHash;
    }

    public static string Run(string repositoryRoot, string corpusRoot, string manifestPath,
        string contractPath, string outputDirectory)
    {
        RequireMemoryLimit();
        var artifact = JsonSerializer.Deserialize<CounterfactualContractArtifact>(
            File.ReadAllText(contractPath), Json) ?? throw new InvalidDataException("Contract cannot be decoded.");
        var contract = artifact.Contract;
        if (Hash(JsonSerializer.Serialize(contract, Canonical)) != artifact.CanonicalSha256
            || contract.RepositoryEntryHead != GitHead(repositoryRoot)
            || contract.ImplementationSnapshotSha256 != Snapshot(repositoryRoot, ImplementationFiles)
            || contract.HarnessSnapshotSha256 != Snapshot(repositoryRoot, HarnessFiles))
            throw new InvalidDataException("Frozen contract, implementation or harness identity drifted.");
        var historical = HistoricalInputs(repositoryRoot);
        if (HistoricalIdentity(historical) != contract.HistoricalArtifactsSha256)
            throw new InvalidDataException("Historical artifact identity drifted.");
        if (FileHash(Path.Combine(outputDirectory, "preflight_controls.json"))
            != contract.PreflightControlsSha256)
            throw new InvalidDataException("Preflight controls drifted after freeze.");
        var manifest = FrozenC11ManifestResearch.Load(manifestPath);
        if (manifest.CanonicalSha256 != contract.CorpusManifestSha256)
            throw new InvalidDataException("C11 identity drifted after freeze.");
        var descriptor = C11CorpusDiscovery.ResolveFrozen(corpusRoot, manifest.Charts).Charts
            .Single(x => x.Sha256 == ChartId);
        var chartText = File.ReadAllText(descriptor.RuntimePath);

        var control = Execute(chartText, null);
        if (control.OutputSha256 != ExpectedCanonicalOutput
            || control.AuthenticRngSha256 != ExpectedCanonicalRng
            || control.DiagnosticsSha256 != ExpectedCanonicalDiagnostics)
            return WriteInvalid(outputDirectory, artifact.CanonicalSha256, "UNEXPECTED_BASELINE",
                "Official canonical arm did not reproduce frozen historical identities.");
        var treatment = Execute(chartText, Intervention);
        var assessment = Assess(control, treatment);
        if (!assessment.Valid)
            return WriteInvalid(outputDirectory, artifact.CanonicalSha256, "INVALID", assessment.Failure!);

        var control185 = Opportunity(control, Op185); var treatment185 = Opportunity(treatment, Op185);
        var control370 = Inspect(control, Op370); var treatment370 = Inspect(treatment, Op370);
        var control466 = Inspect(control, Op466); var treatment466 = Inspect(treatment, Op466);
        var target = TargetResult(control, treatment, control466, treatment466);
        var outcome = target.Outcome;
        var firstRngStateDifference = PairByKey(control, treatment)
            .FirstOrDefault(x => x.Control.StateBefore.RootRngPosition != x.Treatment.StateBefore.RootRngPosition
                || x.Control.StateAfter.RootRngPosition != x.Treatment.StateAfter.RootRngPosition);

        WriteJson(Path.Combine(outputDirectory, "pre_op185_snapshot.json"), new
        {
            control = control185.StateBefore, treatment = treatment185.StateBefore,
            equal = control185.StateBefore == treatment185.StateBefore,
            controlConfiguration = control.ConfigurationSha256,
            treatmentConfiguration = treatment.ConfigurationSha256,
            authenticPrefixSha256 = SelectionSetRemappingResearch.PrefixHash(
                control.Random.AuthenticCalls.Take(260)), prefixCalls = 260,
            independentChartInstances = !ReferenceEquals(control.ChartInstance, treatment.ChartInstance),
            independentProfiles = !ReferenceEquals(control.ProfileInstance, treatment.ProfileInstance)
        });
        WriteJson(Path.Combine(outputDirectory, "single_intervention.json"), new
        {
            plan = Intervention, events = treatment.Random.Interventions,
            eventCount = treatment.Random.Interventions.Length,
            authenticControlCall = control.Random.AuthenticCalls[260],
            authenticTreatmentCall = treatment.Random.AuthenticCalls[260],
            deliveredControlIndex = 2, deliveredTreatmentIndex = 3,
            noAdditionalIntervention = treatment.Random.Interventions.Length == 1
        });
        WriteJson(Path.Combine(outputDirectory, "op185_comparison.json"), new
        {
            opportunity = Op185, controlBefore = control185.StateBefore,
            treatmentBefore = treatment185.StateBefore, controlAfter = control185.StateAfter,
            treatmentAfter = treatment185.StateAfter,
            controlCandidate = Candidate(control, Op185), treatmentCandidate = Candidate(treatment, Op185),
            controlCommit = control185.CommittedObjectIdentity,
            treatmentCommit = treatment185.CommittedObjectIdentity
        });
        WriteJson(Path.Combine(outputDirectory, "op370_comparison.json"), new
        {
            control = control370, treatment = treatment370,
            comparableOpportunity = control370.Reached && treatment370.Reached,
            rngPositionsEqual = control370.ParentRngPosition == treatment370.ParentRngPosition
        });
        WriteJson(Path.Combine(outputDirectory, "op466_result.json"), target);
        WriteTrajectory(Path.Combine(outputDirectory, "downstream_trajectory.csv"), control, treatment);
        WriteJson(Path.Combine(outputDirectory, "rng_transcripts.json"), new
        {
            control = new { control.RngCalls, control.AuthenticRngSha256, calls = control.Random.AuthenticCalls },
            treatment = new { treatment.RngCalls, treatment.AuthenticRngSha256,
                calls = treatment.Random.AuthenticCalls, interventions = treatment.Random.Interventions },
            prefixThroughSelectionExact = control.Random.AuthenticCalls.Take(261)
                .SequenceEqual(treatment.Random.AuthenticCalls.Take(261)),
            firstStateDifferenceOpportunity = firstRngStateDifference.Control is null
                ? null : firstRngStateDifference.Control.OpportunityKey
        });
        WriteJson(Path.Combine(outputDirectory, "summary.json"), new
        {
            schemaVersion = "safety-op185-op466-counterfactual-summary.1",
            contractSha256 = artifact.CanonicalSha256, outcome,
            scientificInterpretation = target.Interpretation,
            interventionCount = treatment.Random.Interventions.Length,
            initialParentsEqualComplete = assessment.InitialParentsEqualComplete,
            authenticRngThroughInterventionExact = assessment.AuthenticRngThroughInterventionExact,
            controlSelectedLane = 2, treatmentSelectedLane = 3,
            op370 = new { control = control370.Disposition, treatment = treatment370.Disposition },
            op466 = target, firstRngStateDifferenceOpportunity = firstRngStateDifference.Control is null
                ? null : firstRngStateDifference.Control.OpportunityKey,
            controlOutputSha256 = control.OutputSha256, treatmentOutputSha256 = treatment.OutputSha256,
            controlRngSha256 = control.AuthenticRngSha256,
            treatmentRngSha256 = treatment.AuthenticRngSha256,
            controlDiagnosticsSha256 = control.DiagnosticsSha256,
            treatmentDiagnosticsSha256 = treatment.DiagnosticsSha256,
            behaviorImplementationChanged = false, fullMatrixExecuted = false,
            historicalClassificationChanged = false, globalStatus = "NEEDS_REVIEW",
            promotion = "NOT_AUTHORIZED"
        });
        WriteHashes(outputDirectory);
        return outcome;
    }

    private static Execution Execute(string chartText, SingleRngInterventionPlan? plan)
    {
        var chart = OsuBeatmap.Parse(chartText);
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var options = InteriorRelationMembershipResearch.CurrentOptions() with
            { Chance = .50, ArticulationEnabled = false, Trace = false, DiagnosticsEnabled = false };
        var random = new SingleRngInterventionRandom(Seed, plan);
        var gate = SafetyRemediationGateRuntimeConfiguration.Frozen(true,
            compactDiagnostics: false, retainCandidateDecisions: true);
        var result = new AddNotesEngine().Apply(chart, options, random, profile, null, null,
            null, null, gate);
        var diagnostics = result.SafetyRemediationGateDiagnostics
            ?? throw new InvalidDataException("Canonical diagnostics were not captured.");
        return new(chart, profile, Hash(OsuBeatmap.Write(result.ModifiedChart, options.Chance)),
            random.CallCount, random.AuthenticTranscriptSha256, random, diagnostics,
            SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(diagnostics),
            Hash(JsonSerializer.Serialize(options, Canonical) + "|CANONICAL|NO_G1"),
            result.ModifiedChart.AddedObjects.ToImmutableArray());
    }

    private static PrerequisiteIdentity InspectPrerequisites(Execution execution)
    {
        var opportunity = Opportunity(execution, Op185);
        var candidate = Candidate(execution, Op185);
        var valid = opportunity.StateBefore.CompleteForCausalLineage
            && opportunity.StateAfter.CompleteForCausalLineage
            && candidate.CanonicalLegalLanes.SequenceEqual([0, 1, 2, 3])
            && candidate.SelectedLane == 2 && candidate.RngPositionBeforeDecision == 260
            && opportunity.StateAfter.RootRngPosition == 261
            && candidate.CanonicalLegalLanes.Contains(3)
            && execution.Random.AuthenticCalls[260].Maximum == 4
            && execution.Random.AuthenticCalls[260].IntegerValue == 2
            && execution.OutputSha256 == ExpectedCanonicalOutput
            && execution.AuthenticRngSha256 == ExpectedCanonicalRng
            && execution.DiagnosticsSha256 == ExpectedCanonicalDiagnostics;
        return new(valid, opportunity.StateBefore, opportunity.StateAfter,
            candidate.CanonicalLegalLanes, candidate.SelectedLane,
            candidate.RngPositionBeforeDecision, execution.Random.AuthenticCalls[260]);
    }

    private static OfficialAssessment Assess(Execution control, Execution treatment)
    {
        var left = Opportunity(control, Op185); var right = Opportunity(treatment, Op185);
        var leftCandidate = Candidate(control, Op185); var rightCandidate = Candidate(treatment, Op185);
        var parents = left.StateBefore.CompleteForCausalLineage
            && right.StateBefore.CompleteForCausalLineage && left.StateBefore == right.StateBefore;
        var prefix = control.Random.AuthenticCalls.Take(261)
            .SequenceEqual(treatment.Random.AuthenticCalls.Take(261));
        var guard = CounterfactualTrajectoryGuardResearch.Validate(new(parents, prefix,
            left.OpportunityKey, treatment.Random.Interventions.Length,
            rightCandidate.CanonicalLegalLanes.Contains(3),
            checked((int)(left.StateAfter.RootRngPosition - leftCandidate.RngPositionBeforeDecision)),
            checked((int)(right.StateAfter.RootRngPosition - rightCandidate.RngPositionBeforeDecision)),
            !ReferenceEquals(control.ChartInstance, treatment.ChartInstance)
                && !ReferenceEquals(control.ProfileInstance, treatment.ProfileInstance), null));
        string? failure = guard == CounterfactualGuardFailure.None ? null : $"Guard failed: {guard}.";
        if (failure is null && control.ConfigurationSha256 != treatment.ConfigurationSha256)
            failure = "Control/treatment configurations differ.";
        else if (failure is null && (!leftCandidate.CanonicalLegalLanes.SequenceEqual([0, 1, 2, 3])
            || !rightCandidate.CanonicalLegalLanes.SequenceEqual([0, 1, 2, 3])))
            failure = "Runtime canonical lane set at OP-185 differs from [0,1,2,3].";
        else if (failure is null && (leftCandidate.SelectedLane != 2 || rightCandidate.SelectedLane != 3))
            failure = "OP-185 did not select lane 2 versus lane 3.";
        else if (failure is null && (left.StateAfter.RootRngPosition != 261 || right.StateAfter.RootRngPosition != 261))
            failure = "OP-185 did not end at RNG position 261 in both arms.";
        else if (failure is null && (!left.StateAfter.CompleteForCausalLineage || !right.StateAfter.CompleteForCausalLineage))
            failure = "OP-185 successor state is incomplete.";
        return new(failure is null, failure, parents, prefix);
    }

    private static TargetAssessment TargetResult(Execution control, Execution treatment,
        OpportunityInspection controlTarget, OpportunityInspection treatmentTarget)
    {
        var controlExact = controlTarget.CommitIdentity == ExpectedTargetCommit;
        var treatmentExact = treatmentTarget.CommitIdentity == ExpectedTargetCommit;
        var similar = treatment.AddedObjects.Any(x => x.Lane == 2 && x.StartTime == 35009)
            && !treatmentExact;
        var outcome = CounterfactualTrajectoryGuardResearch.ClassifyTarget(controlExact,
            treatmentExact, similar);
        var interpretation = outcome switch
        {
            "RECOVERED_EXACT" => "The single OP-185 selection intervention recovered the exact OP-466 target under the frozen canonical policy; this does not establish exclusive historical causality.",
            "SIMILAR_NOT_EXACT" => "Treatment produced a similar object but not the exact historical target.",
            "ABSENT_BOTH" => "Changing only OP-185 from lane 2 to lane 3 was insufficient to recover exact OP-466.",
            _ => "The canonical control unexpectedly committed exact OP-466; comparison is invalid."
        };
        return new(outcome, interpretation, controlTarget, treatmentTarget,
            controlTarget.Reached, treatmentTarget.Reached,
            controlTarget.Candidate is not null, treatmentTarget.Candidate is not null,
            controlTarget.Candidate?.SelectedLane == 2, treatmentTarget.Candidate?.SelectedLane == 2,
            controlTarget.Candidate?.CanonicalLegalLanes.Contains(2) ?? false,
            treatmentTarget.Candidate?.CanonicalLegalLanes.Contains(2) ?? false,
            controlExact, treatmentExact, similar, ExpectedTargetCommit);
    }

    private static OpportunityInspection Inspect(Execution execution, string key)
    {
        var state = execution.Diagnostics.OpportunityStates.SingleOrDefault(x => x.OpportunityKey == key);
        var candidates = execution.Diagnostics.CandidateDecisions.Where(x => x.OpportunityKey == key).ToArray();
        var candidate = candidates.SingleOrDefault(x => x.CandidateOrder == 0);
        var disposition = state is null ? "NOT_REACHED" : candidate is null ? "REACHED_NO_CANDIDATE"
            : state.CommittedObjectIdentity is null ? "CANDIDATE_NOT_COMMITTED" : "COMMITTED";
        return new(state is not null, disposition, key, state?.OpportunityOrder,
            state?.StateBefore, state?.StateAfter, state?.StateBefore.RootRngPosition,
            state?.CommittedObjectIdentity, candidate, candidates.Length);
    }

    private static SafetyRemediationGateOpportunityState Opportunity(Execution execution, string key) =>
        execution.Diagnostics.OpportunityStates.Single(x => x.OpportunityKey == key);
    private static SafetyRemediationCandidateGeometryDecision Candidate(Execution execution, string key) =>
        execution.Diagnostics.CandidateDecisions.Single(x => x.OpportunityKey == key && x.CandidateOrder == 0);

    private static IEnumerable<(SafetyRemediationGateOpportunityState Control,
        SafetyRemediationGateOpportunityState Treatment)> PairByKey(Execution control, Execution treatment)
    {
        var treatmentByKey = treatment.Diagnostics.OpportunityStates.ToDictionary(x => x.OpportunityKey);
        return control.Diagnostics.OpportunityStates.Where(x => treatmentByKey.ContainsKey(x.OpportunityKey))
            .Select(x => (x, treatmentByKey[x.OpportunityKey]));
    }

    private static bool Equivalent(Execution left, Execution right) =>
        left.OutputSha256 == right.OutputSha256 && left.RngCalls == right.RngCalls
        && left.AuthenticRngSha256 == right.AuthenticRngSha256
        && left.Random.AuthenticCalls.SequenceEqual(right.Random.AuthenticCalls)
        && left.DiagnosticsSha256 == right.DiagnosticsSha256;

    private static void WriteTrajectory(string path, Execution control, Execution treatment)
    {
        var right = treatment.Diagnostics.OpportunityStates.ToDictionary(x => x.OpportunityKey);
        var controlCandidates = control.Diagnostics.CandidateDecisions.GroupBy(x => x.OpportunityKey)
            .ToDictionary(x => x.Key, x => x.FirstOrDefault(y => y.CandidateOrder == 0));
        var treatmentCandidates = treatment.Diagnostics.CandidateDecisions.GroupBy(x => x.OpportunityKey)
            .ToDictionary(x => x.Key, x => x.FirstOrDefault(y => y.CandidateOrder == 0));
        using var writer = Writer(path);
        writer.WriteLine("opportunity_key,order,control_before_rng,treatment_before_rng,control_after_rng,treatment_after_rng,control_commit,treatment_commit,control_selected,treatment_selected");
        foreach (var left in control.Diagnostics.OpportunityStates.Where(x => x.OpportunityOrder >= 185
                     && x.OpportunityOrder <= 468))
        {
            right.TryGetValue(left.OpportunityKey, out var r);
            controlCandidates.TryGetValue(left.OpportunityKey, out var lc);
            treatmentCandidates.TryGetValue(left.OpportunityKey, out var rc);
            writer.WriteLine(string.Join(',', Csv(left.OpportunityKey), left.OpportunityOrder,
                left.StateBefore.RootRngPosition, r?.StateBefore.RootRngPosition,
                left.StateAfter.RootRngPosition, r?.StateAfter.RootRngPosition,
                Csv(left.CommittedObjectIdentity ?? string.Empty), Csv(r?.CommittedObjectIdentity ?? string.Empty),
                lc?.SelectedLane, rc?.SelectedLane));
        }
    }

    private static string WriteInvalid(string outputDirectory, string contractHash, string outcome, string failure)
    {
        WriteJson(Path.Combine(outputDirectory, "summary.json"), new
        {
            schemaVersion = "safety-op185-op466-counterfactual-summary.1", contractSha256 = contractHash,
            outcome, failure, interventionExecuted = outcome != "UNEXPECTED_BASELINE",
            historicalClassificationChanged = false, globalStatus = "NEEDS_REVIEW",
            promotion = "NOT_AUTHORIZED", fullMatrixExecuted = false
        });
        WriteHashes(outputDirectory); return outcome;
    }

    private static ImmutableArray<HistoricalInput> HistoricalInputs(string root)
    {
        string[] paths =
        [
            ".artifacts/safety_remediation_gate_final_recertification/frozen/official-matrix/safety_remediation_gate_hardening_runs.csv",
            ".artifacts/safety_remediation_gate_final_recertification/frozen/official-matrix/safety_remediation_gate_hardened_cases.csv",
            ".artifacts/safety_remediation_gate_forensic_attribution/forensic_attribution_runs.json",
            ".artifacts/safety_remediation_gate_forensic_attribution/forensic_offline_consolidation_summary.json",
            ".artifacts/safety_selection_set_remapping/op185_analysis.json",
            ".artifacts/safety_selection_set_remapping/op370_op466_downstream.json",
            ".artifacts/safety_selection_set_remapping/original_decisions.csv",
            ".artifacts/safety_selection_set_remapping/summary.json",
            ".artifacts/safety_selection_set_remapping/sha256sums.txt"
        ];
        return paths.Select(path => new HistoricalInput(path, FileHash(Path.Combine(root,
            path.Replace('/', Path.DirectorySeparatorChar))))).ToImmutableArray();
    }

    private static string HistoricalIdentity(IEnumerable<HistoricalInput> values) => Hash(string.Join('\n',
        values.OrderBy(x => x.Path, StringComparer.Ordinal).Select(x => $"{x.Path}:{x.Sha256}")));
    private static void RequireHistoricalDependencies(string root)
    {
        CounterfactualTrajectoryGuardResearch.RequireFiles(HistoricalInputs(root).Select(item =>
            Path.Combine(root, item.Path.Replace('/', Path.DirectorySeparatorChar))));
    }
    private static void RequireMemoryLimit()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_GCHeapHardLimit"), MemoryLimit,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Counterfactual requires DOTNET_GCHeapHardLimit={MemoryLimit}.");
    }
    private static string Snapshot(string root, IEnumerable<string> paths) =>
        ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(paths.Select(path =>
            new NamedImplementationContent(path, File.ReadAllBytes(Path.Combine(root,
                path.Replace('/', Path.DirectorySeparatorChar))))));
    private static string GitHead(string root)
    {
        var head = File.ReadAllText(Path.Combine(root, ".git", "HEAD")).Trim();
        return head.StartsWith("ref: ", StringComparison.Ordinal)
            ? File.ReadAllText(Path.Combine(root, ".git", head[5..].Replace('/', Path.DirectorySeparatorChar))).Trim()
            : head;
    }
    private static void WriteHashes(string directory)
    {
        var rows = Directory.EnumerateFiles(directory).Where(x => Path.GetFileName(x) != "sha256sums.txt")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(x =>
                $"{FileHash(x)}  {Path.GetFileName(x)}");
        File.WriteAllLines(Path.Combine(directory, "sha256sums.txt"), rows, new UTF8Encoding(false));
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static void WriteJson<T>(string path, T value) => File.WriteAllText(path,
        JsonSerializer.Serialize(value, Json) + Environment.NewLine, new UTF8Encoding(false));
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';

    private sealed record HistoricalInput(string Path, string Sha256);
    private sealed record CounterfactualContract(string SchemaVersion, string RepositoryEntryHead,
        string ImplementationSnapshotSha256, string HarnessSnapshotSha256, string CorpusManifestSha256,
        string HistoricalArtifactsSha256, string PreflightControlsSha256, string ChartId, int Seed,
        string SharedPolicy, string InterventionOpportunityKey, int InterventionTime, ImmutableArray<int> CanonicalLanes,
        int NormalLane, int InterventionLane, int RngPositionBefore, int RngMaximum, int RngPositionAfter,
        string SufficientStateDefinition, string InitialEqualityCriteria, string NonInterferenceCriteria,
        string ExactRecoveryDefinition, ImmutableArray<string> Outcomes, ImmutableArray<string> StopConditions,
        ImmutableArray<HistoricalInput> HistoricalInputs, string ExecutionMemoryLimit);
    private sealed record CounterfactualContractArtifact(CounterfactualContract Contract,
        string CanonicalSha256, string Canonicalization);
    private sealed record Execution(ManiaChart ChartInstance, MapperEvidenceProfile ProfileInstance,
        string OutputSha256, long RngCalls, string AuthenticRngSha256,
        SingleRngInterventionRandom Random, SafetyRemediationGateRuntimeDiagnostics Diagnostics,
        string? DiagnosticsSha256, string ConfigurationSha256, ImmutableArray<ManiaObject> AddedObjects);
    private sealed record PrerequisiteIdentity(bool Valid, SafetyRemediationGateSufficientState Parent,
        SafetyRemediationGateSufficientState Successor, ImmutableArray<int> CanonicalLanes,
        int? SelectedLane, long RngPositionBeforeDecision, SelectionRandomCall AuthenticSelectionCall);
    private sealed record PreflightControls(bool TwoNormalRunsExact, bool PassThroughInstrumentationExact,
        bool IndependentMutableSetup, string OutputSha256, long RngCalls, string RngSha256,
        string DiagnosticsSha256, PrerequisiteIdentity RuntimeIdentity,
        ImmutableArray<SingleRngInterventionEvent> PassThroughEvents);
    private sealed record OfficialAssessment(bool Valid, string? Failure,
        bool InitialParentsEqualComplete, bool AuthenticRngThroughInterventionExact);
    private sealed record OpportunityInspection(bool Reached, string Disposition, string OpportunityKey,
        int? OpportunityOrder, SafetyRemediationGateSufficientState? Parent,
        SafetyRemediationGateSufficientState? Successor, long? ParentRngPosition,
        string? CommitIdentity, SafetyRemediationCandidateGeometryDecision? Candidate, int CandidateCount);
    private sealed record TargetAssessment(string Outcome, string Interpretation,
        OpportunityInspection Control, OpportunityInspection Treatment,
        bool ControlReached, bool TreatmentReached, bool ControlCandidateProduced,
        bool TreatmentCandidateProduced, bool ControlExactProposed, bool TreatmentExactProposed,
        bool ControlGeometryPermitsLane2, bool TreatmentGeometryPermitsLane2,
        bool ControlExactCommitted, bool TreatmentExactCommitted, bool TreatmentSimilarNotExact,
        string ExpectedCommitIdentity);
}
