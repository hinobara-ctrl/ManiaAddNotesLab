using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class SafetySelectionSetRemappingRunner
{
    private const string MemoryLimit = "0x400000000";
    private const string ExpectedImplementation = "B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835";
    private const string ExpectedCorpus = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    private const string Chart02 = "02D9D1781418E7442956D4D901C8C611E58256556AE9A828E72128C3B5B8E3EB";
    private const string Chart206 = "20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788";
    private const string Op370 = "OP-00000370-BaseHead-S370-T29933-ANA";
    private const string Op466 = "OP-00000466-BaseHead-S466-T35009-ANA";
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
        "tests/ManiaAddNotesLab.Tests/SelectionSetRemappingResearchTests.cs",
        "tools/ManiaAddNotesLab.Experiments/Program.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetySelectionSetRemappingRunner.cs"
    ];
    private static readonly ImmutableArray<RunIdentity> Runs =
    [
        new("primary", Chart206, 4, "OP-00000185-BaseHead-S185-T16399-ANA", 10007),
        new("primary", Chart02, 5, "OP-00000659-BaseHead-S659-T51279-ANA", 4823),
        new("primary", Chart02, 20, "OP-00000183-BaseHead-S183-T18552-ANA", 5305),
        new("primary", Chart206, 10, "OP-00000048-BaseHead-S48-T6475-ANA", 10144),
        new("primary", Chart206, 11, "OP-00000055-BaseHead-S55-T6926-ANA", 10137),
        new("primary", Chart206, 14, "OP-00000051-BaseHead-S51-T6700-ANA", 10141),
        new("primary", Chart206, 19, "OP-00000051-BaseHead-S51-T6700-ANA", 10141),
        new("secondary-g1-compatibility", Chart206, 11,
            "OP-00000055-BaseHead-S55-T6926-ANA", 10137)
    ];
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions Canonical = new(Json) { WriteIndented = false };

    public static string Prepare(string repositoryRoot, string manifestPath, string contractPath,
        string artifactDirectory)
    {
        var implementation = Snapshot(repositoryRoot, ImplementationFiles);
        if (implementation != ExpectedImplementation)
            throw new InvalidDataException("Behavioral implementation differs from the frozen remediation identity.");
        var harness = Snapshot(repositoryRoot, HarnessFiles);
        var manifest = FrozenC11ManifestResearch.Load(manifestPath);
        if (manifest.CanonicalSha256 != ExpectedCorpus)
            throw new InvalidDataException("C11 corpus manifest identity drifted.");
        var historical = HistoricalInputs(repositoryRoot);
        var contract = new Contract("safety-selection-set-remapping-contract.2", GitHead(repositoryRoot),
            implementation, harness, manifest.CanonicalSha256, MemoryLimit,
            Runs, "lanes[rng.Next(lanes.Count)] over an exact replay of SeededRandom prefix",
            "E_SELECTION_SET_REMAP requires equal complete parents, same opportunity/configuration, isolated ordered-set intervention, control lane still canonical, authentic different selection with identical RNG prefix/one-call consumption, and materially different complete successors.",
            "ABSTAIN on any failed E criterion; later interventions remain separate and persistence is not target attribution.",
            ["A actual legacy set", "B actual canonical set",
             "C explicit equal-cardinality fixture", "D explicit reordered fixture",
             "E-control explicit different-set/same-selection fixture"],
            ["synthetic controls pass", "priority-only OP-185 local E, no-interference and memory pass",
             "eight reference/repeat comparisons pass", "historical counts remain exact"],
            ["identity drift", "RNG prefix mismatch", "non-interference failure", "G1 drift",
             "missing C11/artifact", "memory/resource failure"], historical);
        var hash = Hash(JsonSerializer.Serialize(contract, Canonical));
        var artifact = new ContractArtifact(contract, hash,
            "UTF-8 System.Text.Json camelCase; ordered arrays; no timestamps or machine paths");
        Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!);
        Directory.CreateDirectory(artifactDirectory);
        WriteJson(contractPath, artifact);
        WriteJson(Path.Combine(artifactDirectory, "frozen_contract.json"), artifact);
        WriteJson(Path.Combine(artifactDirectory, "focused_case_manifest.json"), new
        {
            schemaVersion = "safety-selection-set-remapping-cases.1", runs = Runs
        });
        return hash;
    }

    public static string Run(string repositoryRoot, string corpusRoot, string manifestPath,
        string contractPath, string outputDirectory, bool priorityOnly = false)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_GCHeapHardLimit"), MemoryLimit,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Remapping gate requires DOTNET_GCHeapHardLimit={MemoryLimit}.");
        var artifact = JsonSerializer.Deserialize<ContractArtifact>(File.ReadAllText(contractPath), Json)
            ?? throw new InvalidDataException("Frozen remapping contract cannot be decoded.");
        if (Hash(JsonSerializer.Serialize(artifact.Contract, Canonical)) != artifact.CanonicalSha256
            || artifact.Contract.RepositoryEntryHead != GitHead(repositoryRoot)
            || artifact.Contract.ImplementationSnapshotSha256 != Snapshot(repositoryRoot, ImplementationFiles)
            || artifact.Contract.HarnessSnapshotSha256 != Snapshot(repositoryRoot, HarnessFiles))
            throw new InvalidDataException("Frozen contract, implementation or harness identity drifted.");
        foreach (var input in artifact.Contract.HistoricalInputs)
            if (FileHash(Path.Combine(repositoryRoot, input.Path.Replace('/', Path.DirectorySeparatorChar)))
                != input.Sha256) throw new InvalidDataException($"Historical input drifted: {input.Path}");
        var manifest = FrozenC11Manifest.Load(manifestPath);
        if (manifest.CanonicalSha256 != artifact.Contract.CorpusManifestSha256)
            throw new InvalidDataException("Corpus manifest drifted after freeze.");
        var corpus = C11CorpusDiscovery.ResolveFrozen(corpusRoot, manifest.Charts);
        if (corpus.Charts.Length != 11) throw new InvalidDataException("C11 did not resolve to 11 charts.");
        Directory.CreateDirectory(outputDirectory);
        var results = new List<RunResult>();
        var population = priorityOnly ? Runs.Take(1).ToImmutableArray() : Runs;
        foreach (var identity in population)
        {
            Console.WriteLine($"SELECTION-REMAP {identity.Stratum} chart={identity.ChartId} seed={identity.Seed}");
            var descriptor = corpus.Charts.Single(x => x.Sha256 == identity.ChartId);
            results.Add(Evaluate(identity, OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath))));
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
            GC.WaitForPendingFinalizers();
        }
        var controls = SyntheticControls();
        WriteDecisionCsv(Path.Combine(outputDirectory, "original_decisions.csv"), results);
        WriteCounterfactualCsv(Path.Combine(outputDirectory, "counterfactuals.csv"), results, controls);
        WriteNoInterference(Path.Combine(outputDirectory, "no_interference.csv"), results);
        WriteResults(Path.Combine(outputDirectory, "experimental_e_results.csv"), results);
        WriteJson(Path.Combine(outputDirectory, "selector_evidence.json"), new
        {
            schemaVersion = "safety-selection-set-remapping-evidence.1",
            contractSha256 = artifact.CanonicalSha256, results, syntheticControls = controls
        });
        var priority = results.Single(x => x.Identity.ChartId == Chart206 && x.Identity.Seed == 4
            && x.Identity.Stratum == "primary");
        WriteJson(Path.Combine(outputDirectory, "op185_analysis.json"), priority);
        WriteJson(Path.Combine(outputDirectory, "op370_op466_downstream.json"), priority.Downstream);
        var demonstrated = results.Count(x => x.Assessment.Demonstrated);
        var allSafe = results.All(x => x.ReferenceExact && x.TreatmentRepeatExact
            && x.RngPrefixExact && x.G1IdentityInvariant
            && x.UnattributedObservations == x.Identity.HistoricalUnattributedObservations);
        var outcome = priorityOnly && demonstrated == 1 && allSafe ? "PRIORITY_PASS"
            : demonstrated == 8 && allSafe ? "E_DEMONSTRATED"
            : demonstrated > 0 && allSafe ? "PARTIAL" : "NEEDS_REVIEW";
        WriteJson(Path.Combine(outputDirectory, "summary.json"), new
        {
            schemaVersion = "safety-selection-set-remapping-summary.1",
            contractSha256 = artifact.CanonicalSha256,
            artifact.Contract.RepositoryEntryHead,
            artifact.Contract.ImplementationSnapshotSha256,
            artifact.Contract.HarnessSnapshotSha256,
            artifact.Contract.CorpusManifestSha256,
            focusedRuns = results.Count, priorityOnly, eDemonstrated = demonstrated,
            abstained = results.Count - demonstrated,
            historicalObservations = results.Sum(x => x.UnattributedObservations),
            diagnosticEpisodes = results.Sum(x => x.EpisodeCount),
            fullMatrixExecuted = false, behaviorChanged = false,
            downstreamTargetAttributed = false,
            downstreamConclusion = priority.Downstream!.Conclusion,
            outcome, promotion = "NOT_AUTHORIZED"
        });
        WriteHashes(outputDirectory);
        return outcome;
    }

    private static RunResult Evaluate(RunIdentity identity, ManiaChart chart)
    {
        var options = InteriorRelationMembershipResearch.CurrentOptions() with
            { Chance = .50, ArticulationEnabled = false, Trace = false, DiagnosticsEnabled = false };
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var g1 = identity.Stratum == "secondary-g1-compatibility"
            ? G1GateRuntimeConfiguration.Frozen(G1GateEvidenceIndex.Build(chart), ExpectedCorpus) : null;
        var reference = Execute(chart, options, profile, identity.Seed, g1, null);
        var control = Execute(chart, options, profile, identity.Seed, g1, false);
        var treatment = Execute(chart, options, profile, identity.Seed, g1, true);
        var repeat = Execute(chart, options, profile, identity.Seed, g1, true);
        var referenceExact = Equivalent(reference, control, false);
        var repeatExact = Equivalent(treatment, repeat, true);
        if (!referenceExact || !repeatExact)
            throw new InvalidDataException($"Non-interference failed for {identity}.");
        var pairs = Pair(control.Diagnostics!, treatment.Diagnostics!);
        var lineage = SafetyRemediationGateHardeningResearch.Classify(
            $"{identity.Stratum}|{identity.ChartId}|{identity.Seed}", pairs);
        var first = pairs.First(x =>
            SafetyRemediationForensicAttributionResearch.Compare(x.ControlBefore,
                x.TreatmentBefore).Kind != ForensicStateDifferenceKind.None
            || SafetyRemediationForensicAttributionResearch.Compare(x.ControlAfter,
                x.TreatmentAfter).Kind != ForensicStateDifferenceKind.None);
        if (first.OpportunityKey != identity.ExpectedFirstOpportunity)
            throw new InvalidDataException("First divergence drifted from preregistered historical evidence.");
        var controlCandidate = control.Diagnostics!.CandidateDecisions.Single(x =>
            x.OpportunityOrder == first.OpportunityOrder && x.CandidateOrder == 0);
        var treatmentCandidate = treatment.Diagnostics!.CandidateDecisions.Single(x =>
            x.OpportunityOrder == first.OpportunityOrder && x.CandidateOrder == 0);
        var prefixLength = checked((int)controlCandidate.RngPositionBeforeDecision);
        var controlPrefix = control.Calls.Take(prefixLength).ToImmutableArray();
        var treatmentPrefix = treatment.Calls.Take(prefixLength).ToImmutableArray();
        var prefixExact = controlPrefix.SequenceEqual(treatmentPrefix)
            && controlCandidate.RngPositionBeforeDecision == treatmentCandidate.RngPositionBeforeDecision;
        if (!prefixExact) throw new InvalidDataException("Control/treatment RNG prefix differs before first selection.");
        VerifyHistoricalSelectionCall(control, controlCandidate, controlCandidate.LegacyLegalLanes);
        VerifyHistoricalSelectionCall(treatment, treatmentCandidate, treatmentCandidate.CanonicalLegalLanes);
        var legacyReplay = SelectionSetRemappingResearch.Replay(identity.Seed, controlPrefix,
            controlCandidate.LegacyLegalLanes);
        var canonicalReplay = SelectionSetRemappingResearch.Replay(identity.Seed, controlPrefix,
            controlCandidate.CanonicalLegalLanes);
        if (legacyReplay.SelectedLane != controlCandidate.SelectedLane
            || canonicalReplay.SelectedLane != treatmentCandidate.SelectedLane)
            throw new InvalidDataException("Authentic selector replay did not reproduce historical selection.");
        var configuration = Hash(JsonSerializer.Serialize(options, Canonical)
            + "|" + identity.Stratum + "|" + (g1?.ContractContentHash ?? "NO_G1"));
        var noConcurrent = control.Diagnostics.CandidateDecisions.Count(x =>
                x.OpportunityOrder == first.OpportunityOrder) == 1
            && treatment.Diagnostics.CandidateDecisions.Count(x =>
                x.OpportunityOrder == first.OpportunityOrder) == 1
            && (treatment.G1?.DirectDecisions.All(x => x.OpportunityKey != first.OpportunityKey) ?? true);
        var evidence = new SelectionSetRemappingEvidence(first.OpportunityKey, first.OpportunityKey,
            first.ControlBefore, first.TreatmentBefore, first.ControlAfter, first.TreatmentAfter,
            configuration, configuration, legacyReplay, canonicalReplay, noConcurrent);
        var assessment = SelectionSetRemappingResearch.Assess(evidence);
        var episodes = SafetyRemediationForensicAttributionResearch.GroupEpisodes(lineage);
        var downstream = identity.ChartId == Chart206 && identity.Seed == 4
            && identity.Stratum == "primary" ? AnalyzeDownstream(pairs, control, treatment, lineage) : null;
        var g1Invariant = (control.G1 is null && treatment.G1 is null)
            || (control.G1!.EvidenceIndexHashBefore == control.G1.EvidenceIndexHashAfter
                && treatment.G1!.EvidenceIndexHashBefore == treatment.G1.EvidenceIndexHashAfter
                && control.G1.EvidenceIndexHashBefore == treatment.G1.EvidenceIndexHashBefore);
        return new(identity, first.OpportunityOrder, first.ControlBefore, first.TreatmentBefore,
            first.ControlAfter, first.TreatmentAfter, controlCandidate.LegacyLegalLanes,
            controlCandidate.CanonicalLegalLanes, controlCandidate.SelectedLane,
            treatmentCandidate.SelectedLane, legacyReplay, canonicalReplay, assessment,
            lineage.Count(x => x.UnexplainedStateDivergence), episodes.Length,
            lineage.Count(x => x.ReconvergedBefore || x.ReconvergedAfter), referenceExact,
            repeatExact, prefixExact, g1Invariant, control.OutputSha256, treatment.OutputSha256,
            control.RngHash, treatment.RngHash, control.DiagnosticsFingerprint,
            treatment.DiagnosticsFingerprint, downstream);
    }

    private static DownstreamAnalysis AnalyzeDownstream(
        IReadOnlyList<SafetyRemediationGatePairedOpportunity> pairs, Execution control,
        Execution treatment, IReadOnlyList<SafetyRemediationGateLineageStep> lineage)
    {
        var first = pairs.Single(x => x.OpportunityKey.Contains("OP-00000185-", StringComparison.Ordinal));
        var op370 = pairs.Single(x => x.OpportunityKey == Op370);
        var op466 = pairs.Single(x => x.OpportunityKey == Op466);
        var between = pairs.Where(x => x.OpportunityOrder > first.OpportunityOrder
            && x.OpportunityOrder <= op466.OpportunityOrder).ToArray();
        var firstRngDifference = between.FirstOrDefault(x =>
            x.ControlBefore.RootRngPosition != x.TreatmentBefore.RootRngPosition
            || x.ControlAfter.RootRngPosition != x.TreatmentAfter.RootRngPosition);
        var canonicalInterventions = between.Count(x => x.CanonicalAuthorityChangedCommit);
        var commitDifferences = between.Count(x => x.ControlCommitIdentity != x.TreatmentCommitIdentity);
        var stateReconvergences = lineage.Where(x => x.OpportunityOrder > first.OpportunityOrder
            && x.OpportunityOrder <= op466.OpportunityOrder)
            .Count(x => x.ReconvergedBefore || x.ReconvergedAfter);
        return new(Inspect(op370, control, treatment, lineage),
            Inspect(op466, control, treatment, lineage), firstRngDifference?.OpportunityKey,
            canonicalInterventions, commitDifferences, stateReconvergences,
            pairs.Select(x => x.OpportunityKey).SequenceEqual(pairs.Select(x => x.OpportunityKey)),
            "E demonstrates the first divergence only. Later canonical interventions and RNG drift are concurrent; no isolated counterfactual trajectory proves OP-466 absence.");
    }

    private static OpportunityInspection Inspect(SafetyRemediationGatePairedOpportunity item,
        Execution control, Execution treatment, IReadOnlyList<SafetyRemediationGateLineageStep> lineage)
    {
        var step = lineage.Single(x => x.OpportunityOrder == item.OpportunityOrder);
        return new(item.OpportunityKey, item.OpportunityOrder, item.ControlBefore, item.TreatmentBefore,
            item.ControlAfter, item.TreatmentAfter, item.ControlCommitIdentity,
            item.TreatmentCommitIdentity, step.BeforeEqual, step.AfterEqual,
            step.DirectGovernedDivergence, step.ActiveLineageBefore,
            control.Diagnostics!.CandidateDecisions.Where(x => x.OpportunityOrder == item.OpportunityOrder)
                .ToImmutableArray(),
            treatment.Diagnostics!.CandidateDecisions.Where(x => x.OpportunityOrder == item.OpportunityOrder)
                .ToImmutableArray());
    }

    private static void VerifyHistoricalSelectionCall(Execution execution,
        SafetyRemediationCandidateGeometryDecision candidate, ImmutableArray<int> lanes)
    {
        var position = checked((int)candidate.RngPositionBeforeDecision);
        var call = execution.Calls[position];
        if (call.Kind != SelectionRandomCallKind.Integer || call.Maximum != lanes.Length
            || !call.IntegerValue.HasValue || lanes[call.IntegerValue.Value] != candidate.SelectedLane)
            throw new InvalidDataException("Recorded selection call does not match candidate decision.");
    }

    private static ImmutableArray<SafetyRemediationGatePairedOpportunity> Pair(
        SafetyRemediationGateRuntimeDiagnostics control,
        SafetyRemediationGateRuntimeDiagnostics treatment)
    {
        if (control.OpportunityStates.Length != treatment.OpportunityStates.Length)
            throw new InvalidDataException("Opportunity count mismatch.");
        var rejected = control.CandidateDecisions.Where(x =>
                SafetyRemediationGateHardeningResearch.CanonicalAuthorityRejectedSelectedCommit([x]))
            .Select(x => x.OpportunityOrder).ToHashSet();
        return control.OpportunityStates.Zip(treatment.OpportunityStates).Select(x =>
        {
            if (x.First.OpportunityOrder != x.Second.OpportunityOrder
                || x.First.OpportunityKey != x.Second.OpportunityKey)
                throw new InvalidDataException("Opportunity identity mismatch.");
            return new SafetyRemediationGatePairedOpportunity(x.First.OpportunityKey,
                x.First.OpportunityOrder, x.First.StateBefore, x.Second.StateBefore,
                x.First.StateAfter, x.Second.StateAfter, x.First.CommittedObjectIdentity,
                x.Second.CommittedObjectIdentity, rejected.Contains(x.First.OpportunityOrder));
        }).ToImmutableArray();
    }

    private static Execution Execute(ManiaChart chart, AddNotesOptions options,
        MapperEvidenceProfile profile, int seed, G1GateRuntimeConfiguration? g1, bool? treatment)
    {
        var random = new RecordingRandom(seed);
        var gate = treatment.HasValue ? SafetyRemediationGateRuntimeConfiguration.Frozen(
            treatment.Value, compactDiagnostics: false, retainCandidateDecisions: true) : null;
        var result = new AddNotesEngine().Apply(chart, options, random, profile, null, null, g1, null, gate);
        var output = Hash(OsuBeatmap.Write(result.ModifiedChart, options.Chance));
        return new(output, random.CallCount, random.Hash, random.Calls,
            result.SafetyRemediationGateDiagnostics, result.G1GateDiagnostics,
            result.SafetyRemediationGateDiagnostics is null ? null
                : SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(
                    result.SafetyRemediationGateDiagnostics));
    }

    private static bool Equivalent(Execution left, Execution right, bool diagnostics) =>
        left.OutputSha256 == right.OutputSha256 && left.RngCalls == right.RngCalls
        && left.RngHash == right.RngHash && left.Calls.SequenceEqual(right.Calls)
        && (!diagnostics || left.DiagnosticsFingerprint == right.DiagnosticsFingerprint);

    private static ImmutableArray<SyntheticControl> SyntheticControls()
    {
        SyntheticControl Control(string name, int[] left, int[] right, Func<int, bool> predicate)
        {
            var seed = Enumerable.Range(0, 10000).First(candidate =>
            {
                var a = SelectionSetRemappingResearch.Replay(candidate, [], left);
                var b = SelectionSetRemappingResearch.Replay(candidate, [], right);
                return predicate(a.SelectedLane * 100 + b.SelectedLane);
            });
            var a = SelectionSetRemappingResearch.Replay(seed, [], left);
            var b = SelectionSetRemappingResearch.Replay(seed, [], right);
            return new(name, seed, a, b);
        }
        return
        [
            Control("cardinality_remap", [0,1,2,3,6], [0,1,2,3], x => x / 100 != x % 100),
            Control("equal_cardinality_same_order", [0,1,2], [0,1,2], x => x / 100 == x % 100),
            Control("order_only", [0,1,2,3], [3,2,1,0], x => x / 100 != x % 100),
            Control("different_set_same_selection", [0,1,2], [0,1], x => x / 100 == x % 100)
        ];
    }

    private static ImmutableArray<HistoricalInput> HistoricalInputs(string root)
    {
        string[] paths =
        [
            ".artifacts/safety_remediation_gate_final_recertification/frozen/official-matrix/safety_remediation_gate_hardening_runs.csv",
            ".artifacts/safety_remediation_gate_final_recertification/frozen/official-matrix/safety_remediation_gate_hardened_cases.csv",
            ".artifacts/safety_remediation_gate_final_recertification/smoke/followup_result.json",
            ".artifacts/safety_remediation_gate_forensic_attribution/forensic_attribution_runs.json",
            ".artifacts/safety_remediation_gate_forensic_attribution/forensic_offline_consolidation_summary.json"
        ];
        return paths.Select(x => new HistoricalInput(x, FileHash(Path.Combine(root,
            x.Replace('/', Path.DirectorySeparatorChar))))).ToImmutableArray();
    }

    private static void WriteDecisionCsv(string path, IEnumerable<RunResult> rows)
    {
        using var w = Writer(path);
        w.WriteLine("stratum,chart_id,seed,opportunity_key,order,legacy_lanes,canonical_lanes,control_selected,treatment_selected,prefix_calls,prefix_sha256,legacy_index,canonical_index");
        foreach (var x in rows) w.WriteLine(string.Join(',', Csv(x.Identity.Stratum), x.Identity.ChartId,
            x.Identity.Seed, Csv(x.Identity.ExpectedFirstOpportunity), x.FirstOpportunityOrder,
            Csv(string.Join(';', x.LegacyLanes)), Csv(string.Join(';', x.CanonicalLanes)),
            x.ControlSelectedLane, x.TreatmentSelectedLane, x.LegacyReplay.PrefixCallCount,
            x.LegacyReplay.PrefixSha256, x.LegacyReplay.SelectedIndex, x.CanonicalReplay.SelectedIndex));
    }

    private static void WriteCounterfactualCsv(string path, IEnumerable<RunResult> rows,
        IEnumerable<SyntheticControl> controls)
    {
        using var w = Writer(path);
        w.WriteLine("scope,name,seed,left_lanes,right_lanes,left_max,left_index,left_lane,right_max,right_index,right_lane,prefix_sha256,left_calls,right_calls,result");
        foreach (var x in rows) WriteCounterfactual(w, "official", x.Identity.Stratum + "|" +
            x.Identity.ChartId + "|" + x.Identity.Seed, x.Identity.Seed, x.LegacyReplay,
            x.CanonicalReplay, x.Assessment.Disposition.ToString());
        foreach (var x in controls) WriteCounterfactual(w, "synthetic", x.Name, x.Seed,
            x.Left, x.Right, x.Left.SelectedLane == x.Right.SelectedLane ? "SAME" : "REMAP");
    }

    private static void WriteCounterfactual(StreamWriter w, string scope, string name, int seed,
        SelectionReplayResult left, SelectionReplayResult right, string result) => w.WriteLine(
        string.Join(',', scope, Csv(name), seed, Csv(string.Join(';', left.OrderedLanes)),
            Csv(string.Join(';', right.OrderedLanes)), left.SelectionMaximum, left.SelectedIndex,
            left.SelectedLane, right.SelectionMaximum, right.SelectedIndex, right.SelectedLane,
            left.PrefixSha256, left.SelectionRngCalls, right.SelectionRngCalls, result));

    private static void WriteNoInterference(string path, IEnumerable<RunResult> rows)
    {
        using var w = Writer(path);
        w.WriteLine("stratum,chart_id,seed,reference_exact,treatment_repeat_exact,rng_prefix_exact,g1_identity_invariant,control_output_sha256,treatment_output_sha256,control_rng_sha256,treatment_rng_sha256,control_diagnostics_sha256,treatment_diagnostics_sha256");
        foreach (var x in rows) w.WriteLine(string.Join(',', x.Identity.Stratum, x.Identity.ChartId,
            x.Identity.Seed, B(x.ReferenceExact), B(x.TreatmentRepeatExact), B(x.RngPrefixExact),
            B(x.G1IdentityInvariant), x.ControlOutputSha256, x.TreatmentOutputSha256,
            x.ControlRngSha256, x.TreatmentRngSha256, x.ControlDiagnosticsSha256,
            x.TreatmentDiagnosticsSha256));
    }

    private static void WriteResults(string path, IEnumerable<RunResult> rows)
    {
        using var w = Writer(path);
        w.WriteLine("stratum,chart_id,seed,opportunity_key,disposition,demonstrated,unattributed_observations,episodes,reconvergences,failed_criteria");
        foreach (var x in rows) w.WriteLine(string.Join(',', x.Identity.Stratum, x.Identity.ChartId,
            x.Identity.Seed, Csv(x.Identity.ExpectedFirstOpportunity), x.Assessment.Disposition,
            B(x.Assessment.Demonstrated), x.UnattributedObservations, x.EpisodeCount,
            x.ReconvergenceCount, Csv(string.Join(';', x.Assessment.FailedCriteria))));
    }

    private static void WriteHashes(string directory)
    {
        var rows = Directory.EnumerateFiles(directory).Where(x => Path.GetFileName(x) != "sha256sums.txt")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(x =>
                $"{FileHash(x)}  {Path.GetFileName(x)}");
        File.WriteAllLines(Path.Combine(directory, "sha256sums.txt"), rows, new UTF8Encoding(false));
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
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static void WriteJson<T>(string path, T value) => File.WriteAllText(path,
        JsonSerializer.Serialize(value, Json) + Environment.NewLine, new UTF8Encoding(false));
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string B(bool value) => value ? "true" : "false";

    private sealed record RunIdentity(string Stratum, string ChartId, int Seed,
        string ExpectedFirstOpportunity, int HistoricalUnattributedObservations);
    private sealed record HistoricalInput(string Path, string Sha256);
    private sealed record Contract(string SchemaVersion, string RepositoryEntryHead,
        string ImplementationSnapshotSha256, string HarnessSnapshotSha256,
        string CorpusManifestSha256, string ExecutionMemoryLimit, ImmutableArray<RunIdentity> Population,
        string AuthenticSelector, string ExperimentalCausalDefinition, string AbstentionRule,
        ImmutableArray<string> Counterfactuals, ImmutableArray<string> SuccessCriteria,
        ImmutableArray<string> StopConditions, ImmutableArray<HistoricalInput> HistoricalInputs);
    private sealed record ContractArtifact(Contract Contract, string CanonicalSha256, string Canonicalization);
    private sealed record Execution(string OutputSha256, long RngCalls, string RngHash,
        ImmutableArray<SelectionRandomCall> Calls, SafetyRemediationGateRuntimeDiagnostics? Diagnostics,
        G1GateRuntimeDiagnostics? G1, string? DiagnosticsFingerprint);
    private sealed record SyntheticControl(string Name, int Seed, SelectionReplayResult Left,
        SelectionReplayResult Right);
    private sealed record OpportunityInspection(string OpportunityKey, int OpportunityOrder,
        SafetyRemediationGateSufficientState ControlBefore,
        SafetyRemediationGateSufficientState TreatmentBefore,
        SafetyRemediationGateSufficientState ControlAfter,
        SafetyRemediationGateSufficientState TreatmentAfter,
        string? ControlCommit, string? TreatmentCommit, bool BeforeEqual, bool AfterEqual,
        bool DirectGovernedDivergence, string? ActiveLineageBefore,
        ImmutableArray<SafetyRemediationCandidateGeometryDecision> ControlCandidates,
        ImmutableArray<SafetyRemediationCandidateGeometryDecision> TreatmentCandidates);
    private sealed record DownstreamAnalysis(OpportunityInspection Op370, OpportunityInspection Op466,
        string? FirstRngDifferenceOpportunity, int CanonicalInterventionsThroughTarget,
        int CommitDifferencesThroughTarget, int ReconvergencesThroughTarget,
        bool OpportunitySequenceIdentity, string Conclusion);
    private sealed record RunResult(RunIdentity Identity, int FirstOpportunityOrder,
        SafetyRemediationGateSufficientState ControlParent,
        SafetyRemediationGateSufficientState TreatmentParent,
        SafetyRemediationGateSufficientState ControlSuccessor,
        SafetyRemediationGateSufficientState TreatmentSuccessor,
        ImmutableArray<int> LegacyLanes, ImmutableArray<int> CanonicalLanes,
        int? ControlSelectedLane, int? TreatmentSelectedLane,
        SelectionReplayResult LegacyReplay, SelectionReplayResult CanonicalReplay,
        SelectionSetRemappingAssessment Assessment, int UnattributedObservations,
        int EpisodeCount, int ReconvergenceCount, bool ReferenceExact,
        bool TreatmentRepeatExact, bool RngPrefixExact, bool G1IdentityInvariant,
        string ControlOutputSha256, string TreatmentOutputSha256,
        string ControlRngSha256, string TreatmentRngSha256,
        string? ControlDiagnosticsSha256, string? TreatmentDiagnosticsSha256,
        DownstreamAnalysis? Downstream);

    private sealed class RecordingRandom(int seed) : IRandomPositionSource
    {
        private readonly SeededRandom inner = new(seed);
        private readonly SafetyRemediationGateTranscriptHash transcript = new();
        private readonly ImmutableArray<SelectionRandomCall>.Builder calls = ImmutableArray.CreateBuilder<SelectionRandomCall>();
        public long CallCount => calls.Count;
        public string Hash => transcript.CurrentHash;
        public ImmutableArray<SelectionRandomCall> Calls => calls.ToImmutable();
        public double NextDouble()
        {
            var value = inner.NextDouble(); transcript.AppendDouble(value);
            calls.Add(new(SelectionRandomCallKind.Double, null, value, null)); return value;
        }
        public int Next(int maximum)
        {
            var value = inner.Next(maximum); transcript.AppendInteger(maximum, value);
            calls.Add(new(SelectionRandomCallKind.Integer, maximum, null, value)); return value;
        }
    }
}
