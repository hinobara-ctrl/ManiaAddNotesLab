using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VisualBasic.FileIO;
using ManiaAddNotesLab.Core;

internal static class SafetyRemediationGateHardeningRunner
{
    private const string EntryHead = "667fd1d469ed707c5fc28232672327c7e320d4a0";
    private const string OriginalContractHash = "9415E4710E44163D26BF56123C3166EF9F52C43AA376E53BA7D2E8305E3293D8";
    private const string OriginalImplementationHash = "93DD2E5E2D7FC6C49FB33B34C7CCDCC58870FA6BE835892FF7903A09D4ADCBE7";
    private const string ManifestHash = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    private const string ExecutionMemoryLimit = "0x400000000";
    private static readonly string[] ImplementationFiles =
    [
        "src/ManiaAddNotesLab.Core/AddNotesEngine.cs",
        "src/ManiaAddNotesLab.Core/BeatTimeline.cs",
        "src/ManiaAddNotesLab.Core/ChartAnalysis.cs",
        "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
        "src/ManiaAddNotesLab.Core/Model.cs",
        "src/ManiaAddNotesLab.Core/OsuBeatmap.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateResearch.cs"
    ];
    private static readonly string[] HarnessFiles =
    [
        "src/ManiaAddNotesLab.Core/DownstreamHardValidityCausalityResearch.cs",
        "src/ManiaAddNotesLab.Core/G1GateRuntimeResearch.cs",
        "src/ManiaAddNotesLab.Core/GenerationProvenanceResearch.cs",
        "src/ManiaAddNotesLab.Core/GeometrySafetyAttributionResearch.cs",
        "src/ManiaAddNotesLab.Core/InteriorRelationMembershipResearch.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateHardeningResearch.cs",
        "tests/ManiaAddNotesLab.Tests/PhaseSafetyRemediationGateTests.cs",
        "tests/ManiaAddNotesLab.Tests/PhaseSafetyRemediationGateHardeningTests.cs",
        "tools/ManiaAddNotesLab.Experiments/Program.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetyRemediationGateHardeningRunner.cs"
    ];
    private static readonly ImmutableArray<(string Chart, int Seed)> SecondaryRuns =
    [
        ("02D9D1781418E7442956D4D901C8C611E58256556AE9A828E72128C3B5B8E3EB", 9),
        ("02D9D1781418E7442956D4D901C8C611E58256556AE9A828E72128C3B5B8E3EB", 11),
        ("02D9D1781418E7442956D4D901C8C611E58256556AE9A828E72128C3B5B8E3EB", 17),
        ("20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788", 11)
    ];
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions Canonical = new(Json) { WriteIndented = false };

    public static string Prepare(string repositoryRoot, string contractPath, string inventoryPath)
    {
        var implementation = Snapshot(repositoryRoot, ImplementationFiles);
        var harness = Snapshot(repositoryRoot, HarnessFiles);
        var contract = new HardeningContract(
            SafetyRemediationGateHardeningResearch.SchemaVersion,
            "SAFETY.REMEDIATION.GATE.VALIDATION_HARDENING", EntryHead,
            OriginalContractHash, OriginalImplementationHash, implementation, harness, ManifestHash,
            ExecutionMemoryLimit,
            ImplementationFiles.ToImmutableArray(), HarnessFiles.ToImmutableArray(),
            "Materialized GenerationState + committed latent geometry + RNG positions + opportunity cursor + pending articulation identity + frozen opportunity sequence.",
            "A divergence opens only when equal complete parent states produce different commits and the control commit was rejected by canonical authority; full sufficient-state equality closes ancestry permanently.",
            ["220 primary pairs", "4 frozen G1 compatibility pairs", "209 known cases", "6 additional cases"],
            ["engine behavior changes", "new policy", "defaults", "CLI/Web", "G1 promotion", "HardValidity relaxation"],
            ["safety_remediation_gate_validation_hardening_contract.json",
             "safety_remediation_gate_implementation_inventory.csv",
             "safety_remediation_gate_hardened_cases.csv",
             "safety_remediation_gate_causal_unreachable.csv",
             "safety_remediation_gate_hardening_runs.csv",
             "safety_remediation_gate_validation_hardening_summary.json",
             "PHASE_SAFETY_REMEDIATION_GATE_VALIDATION_HARDENING_ADDENDUM.md"],
            "NOT_AUTHORIZED");
        var hash = ContractHash(contract);
        WriteJson(contractPath, new HardeningContractArtifact(contract, hash,
            "UTF-8 System.Text.Json camelCase; ordered frozen arrays; no timestamps or machine paths"));
        WriteInventory(inventoryPath);
        return hash;
    }

    public static string Run(string repositoryRoot, string corpusRoot, string publicDirectory,
        string artifactDirectory, string contractPath)
        => Run(repositoryRoot, corpusRoot, publicDirectory, publicDirectory, artifactDirectory,
            contractPath);

    public static string Run(string repositoryRoot, string corpusRoot, string sourceDirectory,
        string outputDirectory, string artifactDirectory, string contractPath)
    {
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(artifactDirectory);
        var artifact = JsonSerializer.Deserialize<HardeningContractArtifact>(File.ReadAllText(contractPath), Json)
            ?? throw new InvalidOperationException("Hardening contract could not be read.");
        var contract = artifact.Contract;
        if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_GCHeapHardLimit"),
                contract.ExecutionMemoryLimit, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Run requires DOTNET_GCHeapHardLimit={contract.ExecutionMemoryLimit}.");
        if (ContractHash(contract) != artifact.CanonicalSha256
            || Snapshot(repositoryRoot, ImplementationFiles) != contract.HardenedImplementationSnapshotSha256
            || Snapshot(repositoryRoot, HarnessFiles) != contract.CertificationHarnessSnapshotSha256)
            throw new InvalidOperationException("Hardening contract or frozen snapshots drifted after prepare.");

        var historical = ReadHistoricalCases(Path.Combine(sourceDirectory,
            "safety_remediation_gate_known_cases.csv"));
        if (historical.Length != 215 || historical.Count(x => x.FrozenKnown) != 209
            || historical.Count(x => !x.FrozenKnown) != 6)
            throw new InvalidOperationException("Historical 209+6 denominator drifted.");
        var frozenManifest = FrozenC11Manifest.Load(
            Path.Combine(sourceDirectory, "g1_gate_runtime_manifest.json"));
        if (!string.Equals(frozenManifest.CanonicalSha256, ManifestHash,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Frozen C11 manifest identity drifted from the hardening contract.");
        var charts = C11CorpusDiscovery.ResolveFrozen(corpusRoot, frozenManifest.Charts).Charts.ToArray();
        if (charts.Length != 11) throw new InvalidOperationException("Frozen C11 did not resolve to 11 charts.");

        var caseRows = new List<HardenedCase>();
        var runRows = new List<HardeningRun>();
        foreach (var descriptor in charts)
        {
            Console.WriteLine($"SAFETY.REMEDIATION.GATE hardening primary chart={descriptor.Sha256}");
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            for (var seed = 1; seed <= 20; seed++)
            {
                Evaluate("primary", "control", descriptor.Sha256, seed, chart, null, true,
                    historical, caseRows, runRows);
                ReleasePairDiagnostics();
            }
        }
        foreach (var (chartId, seed) in SecondaryRuns)
        {
            Console.WriteLine($"SAFETY.REMEDIATION.GATE hardening secondary chart={chartId} seed={seed}");
            var descriptor = charts.Single(x => x.Sha256 == chartId);
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var g1 = G1GateRuntimeConfiguration.Frozen(G1GateEvidenceIndex.Build(chart), ManifestHash);
            Evaluate("secondary-g1-compatibility", "treatment", chartId, seed, chart, g1, false,
                historical, caseRows, runRows);
            ReleasePairDiagnostics();
        }

        var direct = caseRows.Count(x => x.HardenedClass == "A_DIRECT_CANONICAL_REJECT");
        var unreachable = caseRows.Count(x => x.HardenedClass == "B_CAUSALLY_PROVEN_UNREACHABLE");
        var unresolved = caseRows.Count(x => x.HardenedClass == "C_UNRESOLVED");
        var known = caseRows.Count(x => x.FrozenKnown);
        var additional = caseRows.Count(x => !x.FrozenKnown);
        WriteCases(Path.Combine(outputDirectory, "safety_remediation_gate_hardened_cases.csv"), caseRows);
        WriteUnreachable(Path.Combine(outputDirectory, "safety_remediation_gate_causal_unreachable.csv"),
            caseRows.Where(x => x.HardenedClass == "B_CAUSALLY_PROVEN_UNREACHABLE"));
        WriteRuns(Path.Combine(outputDirectory, "safety_remediation_gate_hardening_runs.csv"), runRows);

        var pass = direct + unreachable == 215 && unresolved == 0 && known == 209 && additional == 6
            && runRows.Count == 224
            && runRows.All(x => x.TreatmentHardViolations == 0 && x.TreatmentOnlyHardViolations == 0
                && x.DefaultReferenceExact && x.TreatmentDeterministic && x.ControlReparseExact
                && x.TreatmentReparseExact && x.G1EvidenceInvariant && x.GateRngCalls == 0
                && x.UnexplainedStateDivergences == 0)
            && caseRows.Where(x => x.HardenedClass == "B_CAUSALLY_PROVEN_UNREACHABLE")
                .All(x => x.ParentStatesDifferent && x.NoReconvergenceBeforeCase
                    && x.TargetAbsentInTreatment && !x.FrozenLaneSelectedInTreatment
                    && x.FrozenCommitAbsentInTreatment && x.DivergenceGovernedByCanonical);
        var summary = new
        {
            schemaVersion = SafetyRemediationGateHardeningResearch.SchemaVersion,
            phase = "SAFETY.REMEDIATION.GATE.VALIDATION_HARDENING",
            repositoryEntryHead = EntryHead,
            originalContractSha256 = OriginalContractHash,
            hardeningContractSha256 = artifact.CanonicalSha256,
            originalImplementationSnapshotSha256 = OriginalImplementationHash,
            hardenedImplementationSnapshotSha256 = contract.HardenedImplementationSnapshotSha256,
            certificationHarnessSnapshotSha256 = contract.CertificationHarnessSnapshotSha256,
            corpus = new { charts = 11, primaryPairs = 220, secondaryPairs = 4, totalPairs = runRows.Count },
            cases = new { total = caseRows.Count, frozenKnown = known, additional,
                directCanonicalReject = direct, causallyProvenUnreachable = unreachable, unresolved,
                explainedOnlyByTemporalOrdering = caseRows.Count(x => x.TemporalOnlyExplanation) },
            runtime = new { controlHardViolations = runRows.Sum(x => x.ControlHardViolations),
                treatmentHardViolations = runRows.Sum(x => x.TreatmentHardViolations),
                treatmentOnlyHardViolations = runRows.Sum(x => x.TreatmentOnlyHardViolations),
                unexplainedStateDivergences = runRows.Sum(x => x.UnexplainedStateDivergences),
                pairsWithReconvergence = runRows.Count(x => x.ReconvergenceCount > 0),
                gateRngCalls = runRows.Sum(x => x.GateRngCalls) },
            validation = new { implementationSnapshotComplete = true,
                treatmentDefiningMutationBadControlPass = true,
                geometrySetDifferenceWithoutCommitDoesNotOpenLineage = true,
                fullReconvergenceClearsAncestry = true,
                laterDivergenceGetsNewLineage = true,
                canonicalRejectCommitInvariantPass = true,
                defaultEquivalenceFailures = runRows.Count(x => !x.DefaultReferenceExact),
                determinismFailures = runRows.Count(x => !x.TreatmentDeterministic),
                reparseFailures = runRows.Count(x => !x.ControlReparseExact || !x.TreatmentReparseExact),
                g1EvidenceFailures = runRows.Count(x => !x.G1EvidenceInvariant) },
            outcome = pass ? "RECERTIFICATION_PASS" : "NEEDS_REVIEW",
            boundaries = new { behaviorChanged = false, originalArtifactsRewritten = false,
                defaultPolicy = "legacy-experimental.1", promotion = "NOT_AUTHORIZED",
                successor = "NOT_AUTHORIZED" }
        };
        var summaryPath = Path.Combine(outputDirectory,
            "safety_remediation_gate_validation_hardening_summary.json");
        WriteJson(summaryPath, summary);
        WriteJson(Path.Combine(artifactDirectory,
            "safety_remediation_gate_validation_hardening_summary.json"), summary);
        return summary.outcome;
    }

    private static void Evaluate(string stratum, string frozenArm, string chartId, int seed,
        ManiaChart chart, G1GateRuntimeConfiguration? g1, bool verifyDefault,
        ImmutableArray<HistoricalCase> historical, List<HardenedCase> cases, List<HardeningRun> runs)
    {
        var options = InteriorRelationMembershipResearch.CurrentOptions() with
            { Chance = .50, ArticulationEnabled = false, Trace = false, DiagnosticsEnabled = false };
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var runCases = historical.Where(x => x.Stratum == stratum && x.ChartId == chartId
            && x.Seed == seed && x.FrozenArm == frozenArm).ToArray();
        var repeat = ExecuteDeterminismReference(chart, options, profile, seed, g1);
        ReleasePairDiagnostics();
        var controlPath = Path.Combine(Path.GetTempPath(), $"manialab-srg-control-{Guid.NewGuid():N}.bin");
        var treatmentPath = Path.Combine(Path.GetTempPath(), $"manialab-srg-treatment-{Guid.NewGuid():N}.bin");
        CompactExecution control;
        CompactExecution treatment;
        using (var sink = new BinaryOpportunityTraceSink(controlPath,
                   runCases.Select(x => x.OpportunityKey)))
            control = ExecuteCompact(chart, options, profile, seed, g1, false, sink);
        ReleasePairDiagnostics();
        using (var sink = new BinaryOpportunityTraceSink(treatmentPath,
                   runCases.Select(x => x.OpportunityKey)))
            treatment = ExecuteCompact(chart, options, profile, seed, g1, true, sink);
        ReleasePairDiagnostics();
        var defaultExact = true;
        if (verifyDefault)
        {
            var random = new TranscriptRandom(seed);
            var reference = new AddNotesEngine().Apply(chart, options, random, profile);
            defaultExact = HashText(OsuBeatmap.Write(reference.ModifiedChart, options.Chance))
                    == control.SerializedHash
                && random.Hash == control.RngHash && random.CallCount == control.RngCalls;
        }
        var deterministic = treatment.SerializedHash == repeat.SerializedHash
            && treatment.RngHash == repeat.RngHash && treatment.RngCalls == repeat.RngCalls
            && treatment.DiagnosticsFingerprint == repeat.DiagnosticsFingerprint;
        var lineage = AnalyzeTraces(stratum, frozenArm, chartId, seed, controlPath, treatmentPath,
            control.Diagnostics, treatment.Diagnostics, runCases);
        File.Delete(controlPath);
        File.Delete(treatmentPath);
        var controlViolations = control.Violations.ToHashSet(StringComparer.Ordinal);
        var treatmentViolations = treatment.Violations.ToHashSet(StringComparer.Ordinal);
        var g1Invariant = control.G1Before == control.G1After && treatment.G1Before == treatment.G1After
            && control.G1Before == treatment.G1Before;
        runs.Add(new(stratum, chartId, seed, controlViolations.Count, treatmentViolations.Count,
            treatmentViolations.Except(controlViolations).Count(), defaultExact, deterministic,
            control.ReparseExact, treatment.ReparseExact, g1Invariant,
            control.Diagnostics.GateRngCalls + treatment.Diagnostics.GateRngCalls,
            lineage.DirectGovernedDivergences, lineage.Reconvergences,
            lineage.UnexplainedStateDivergences));
        cases.AddRange(lineage.Cases);
    }

    private static TraceAnalysis AnalyzeTraces(string stratum, string frozenArm, string chartId,
        int seed, string controlPath, string treatmentPath, CompactDiagnostics control,
        CompactDiagnostics treatment, IReadOnlyList<HistoricalCase> historical)
    {
        if (control.OpportunityCount != treatment.OpportunityCount)
            throw new InvalidOperationException("Control/treatment opportunity counts differ.");
        var allDecisions = control.CandidateDecisions.Concat(treatment.CandidateDecisions).ToArray();
        var keysByOrder = allDecisions.GroupBy(x => x.OpportunityOrder)
            .ToDictionary(x => x.Key, x => x.First().OpportunityKey);
        var canonicalRejectedControlCommit = control.CandidateDecisions
            .Where(x => SafetyRemediationGateHardeningResearch.CanonicalAuthorityRejectedSelectedCommit([x]))
            .Select(x => x.OpportunityOrder).ToHashSet();
        var targetOrders = new Dictionary<int, List<HistoricalCase>>();
        foreach (var frozen in historical)
        {
            var decision = allDecisions.FirstOrDefault(x => x.OpportunityKey == frozen.OpportunityKey
                && x.StartTime == frozen.StartTime && x.EndTime == frozen.EndTime)
                ?? throw new InvalidOperationException($"Historical case has no candidate order: {frozen.OpportunityKey}");
            if (!targetOrders.TryGetValue(decision.OpportunityOrder, out var bucket))
                targetOrders.Add(decision.OpportunityOrder, bucket = []);
            bucket.Add(frozen);
        }

        var observations = new Dictionary<string, TargetObservation>(StringComparer.Ordinal);
        var origins = new Dictionary<string, OriginObservation>(StringComparer.Ordinal);
        var directDivergences = 0;
        var reconvergences = 0;
        var unexplained = 0;
        var episode = 0;
        string? active = null;
        using var leftReader = new BinaryOpportunityTraceReader(controlPath, control.OpportunityCount);
        using var rightReader = new BinaryOpportunityTraceReader(treatmentPath, treatment.OpportunityCount);
        for (var order = 0; order < control.OpportunityCount; order++)
        {
            var left = leftReader.Read(order);
            var right = rightReader.Read(order);
            var beforeEqual = left.BeforeComplete && right.BeforeComplete
                && left.BeforeHash == right.BeforeHash;
            var afterEqual = left.AfterComplete && right.AfterComplete
                && left.AfterHash == right.AfterHash;
            if (active is not null && beforeEqual)
            {
                active = null;
                reconvergences++;
            }
            var lineageBefore = active;
            var commitsDiffer = left.CommitIdentity != right.CommitIdentity;
            var governed = active is null && beforeEqual && commitsDiffer && !afterEqual
                && left.CommitIdentity is not null
                && SafetyRemediationGateHardeningResearch.CanonicalRejectWasNotCommitted(
                    left.CommitIdentity, right.CommitIdentity)
                && canonicalRejectedControlCommit.Contains(order);
            if (governed)
            {
                var key = keysByOrder[order];
                var opened = "SRG-DIV-" + HashText(
                    $"{chartId}|{seed}|{stratum}|{key}|{episode++}")[..24];
                active = opened;
                origins.Add(opened, new OriginObservation(key, left.AfterHash, right.AfterHash));
                directDivergences++;
            }
            if (active is null && (!beforeEqual || !afterEqual)) unexplained++;
            if (targetOrders.TryGetValue(order, out var targets))
                foreach (var target in targets)
                    observations[target.OpportunityKey] = new(beforeEqual, lineageBefore,
                        left.CommitIdentity, right.CommitIdentity);
            if (active is not null && afterEqual)
            {
                active = null;
                reconvergences++;
            }
        }
        leftReader.VerifyComplete();
        rightReader.VerifyComplete();

        var rows = new List<HardenedCase>();
        foreach (var frozen in historical)
        {
            var observation = observations[frozen.OpportunityKey];
            var controlDecision = control.CandidateDecisions.FirstOrDefault(x =>
                x.OpportunityKey == frozen.OpportunityKey && x.StartTime == frozen.StartTime
                && x.EndTime == frozen.EndTime && x.SelectedLane == frozen.Lane);
            var treatmentDecision = treatment.CandidateDecisions.FirstOrDefault(x =>
                x.OpportunityKey == frozen.OpportunityKey && x.StartTime == frozen.StartTime
                && x.EndTime == frozen.EndTime);
            var direct = controlDecision is not null && observation.ControlCommit is not null
                && treatmentDecision is not null
                && !treatmentDecision.CanonicalLegalLanes.Contains(frozen.Lane)
                && SafetyRemediationGateHardeningResearch.CanonicalRejectWasNotCommitted(
                    observation.ControlCommit, observation.TreatmentCommit);
            var origin = observation.ActiveLineageBefore is null ? null
                : origins[observation.ActiveLineageBefore];
            var candidateShapeReached = treatmentDecision is not null;
            var frozenLaneSelected = treatmentDecision?.SelectedLane == frozen.Lane;
            var frozenCommitAbsent = observation.TreatmentCommit != observation.ControlCommit;
            var targetAbsent = !frozenLaneSelected;
            var unreachable = !direct && origin is not null && !observation.BeforeEqual
                && targetAbsent && frozenCommitAbsent;
            var classification = direct ? "A_DIRECT_CANONICAL_REJECT"
                : unreachable ? "B_CAUSALLY_PROVEN_UNREACHABLE" : "C_UNRESOLVED";
            rows.Add(new(stratum, chartId, seed, frozenArm, frozen.OpportunityKey, frozen.Lane,
                frozen.StartTime, frozen.EndTime, frozen.FrozenKnown, frozen.HistoricalClass,
                classification, direct, observation.ActiveLineageBefore, origin?.OpportunityKey,
                origin?.ControlAfterHash, origin?.TreatmentAfterHash, !observation.BeforeEqual,
                observation.ActiveLineageBefore is not null, targetAbsent, candidateShapeReached,
                frozenLaneSelected, frozenCommitAbsent, origin is not null, false,
                classification == "A_DIRECT_CANONICAL_REJECT"
                    ? "Equivalent candidate reached; control committed the frozen lane, canonical rejected it, and treatment did not commit that object."
                    : classification == "B_CAUSALLY_PROVEN_UNREACHABLE"
                        ? "A prior canonical-governed commit divergence left unequal complete parent state; no sufficient-state reconvergence occurred and the exact frozen lane-level proposal was not selected or committed in treatment."
                        : "The strengthened causal chain was incomplete; recertification must abstain."));
        }
        return new(directDivergences, reconvergences, unexplained, rows.ToImmutableArray());
    }

    private static Execution Execute(ManiaChart chart, AddNotesOptions options, MapperEvidenceProfile profile,
        int seed, G1GateRuntimeConfiguration? g1, bool treatment,
        ISafetyRemediationGateOpportunitySink sink)
    {
        var random = new TranscriptRandom(seed);
        var result = new AddNotesEngine().Apply(chart, options, random, profile, null, null, g1, null,
            SafetyRemediationGateRuntimeConfiguration.Frozen(treatment, compactDiagnostics: true,
                retainCandidateDecisions: false, opportunitySink: sink));
        var serialized = OsuBeatmap.Write(result.ModifiedChart, options.Chance);
        var reparsed = OsuBeatmap.Parse(serialized);
        var exact = result.ModifiedChart.AllObjects.Select(Materialized).Order(StringComparer.Ordinal)
            .SequenceEqual(reparsed.OriginalObjects.Select(Materialized).Order(StringComparer.Ordinal));
        var source = Audit(chart, chart).ToHashSet(StringComparer.Ordinal);
        var final = Audit(chart, reparsed).ToHashSet(StringComparer.Ordinal);
        return new(serialized, random.CallCount, random.Hash, exact,
            final.Except(source).Order(StringComparer.Ordinal).ToImmutableArray(),
            result.SafetyRemediationGateDiagnostics!, result.G1GateDiagnostics?.EvidenceIndexHashBefore,
            result.G1GateDiagnostics?.EvidenceIndexHashAfter);
    }

    private static DeterminismReference ExecuteDeterminismReference(ManiaChart chart,
        AddNotesOptions options, MapperEvidenceProfile profile, int seed,
        G1GateRuntimeConfiguration? g1)
    {
        var run = Execute(chart, options, profile, seed, g1, true, DiscardOpportunitySink.Instance);
        return new(HashText(run.Serialized), run.RngCalls, run.RngHash,
            SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(run.Diagnostics));
    }

    private static CompactExecution ExecuteCompact(ManiaChart chart, AddNotesOptions options,
        MapperEvidenceProfile profile, int seed, G1GateRuntimeConfiguration? g1, bool treatment,
        BinaryOpportunityTraceSink sink)
    {
        var run = Execute(chart, options, profile, seed, g1, treatment, sink);
        return new(HashText(run.Serialized), run.RngCalls, run.RngHash, run.ReparseExact,
            run.Violations, new(run.Diagnostics.GateRngCalls, sink.Candidates,
                run.Diagnostics.OpportunityCount),
            SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(run.Diagnostics),
            run.G1Before, run.G1After);
    }

    private static IEnumerable<string> Audit(ManiaChart source, ManiaChart value)
    {
        var timeline = new BeatTimeline(source.TimingPoints);
        var fingerprint = MapperEvidenceProfileBuilder.ComputeFingerprint(source);
        var objects = value.OriginalObjects.Select((x, i) => GeometrySafetyAttributionResearch.Object(
            fingerprint, GeometrySnapshotRole.Control, x, timeline.ToBeatDecimal(x.StartTime),
            x.Type == ManiaObjectType.Tap ? timeline.ToBeatDecimal(x.StartTime)
                : timeline.ToBeatDecimal(x.EndTime!.Value), i));
        return GeometrySafetyAttributionResearch.AuditSnapshot(fingerprint, source.KeyCount, objects)
            .HardViolations.Select(x => x.GeometrySignature);
    }

    private static string Snapshot(string root, IEnumerable<string> paths) =>
        ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(paths.Select(path =>
            new NamedImplementationContent(path, File.ReadAllBytes(Path.Combine(root,
                path.Replace('/', Path.DirectorySeparatorChar))))));

    private static string HashText(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ContractHash(HardeningContract contract) => Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(contract, Canonical)));

    private static void WriteInventory(string path)
    {
        using var writer = Writer(path);
        writer.WriteLine("path,classification,included_in_implementation_snapshot,justification");
        foreach (var item in Inventory())
            writer.WriteLine($"{item.Path},{item.Role},{B(item.IncludedInImplementationSnapshot)},{Csv(item.Justification)}");
    }

    private static IEnumerable<SafetyRemediationImplementationFile> Inventory()
    {
        yield return new(ImplementationFiles[0], SafetyRemediationImplementationFileRole.BehavioralAuthority, true, "Treatment insertion, authority selection and real commit path.");
        yield return new(ImplementationFiles[3], SafetyRemediationImplementationFileRole.BehavioralAuthority, true, "Collision predicates and mutable governing geometry.");
        yield return new(ImplementationFiles[6], SafetyRemediationImplementationFileRole.BehavioralAuthority, true, "CanonicalPlayableGeometry and frozen runtime configuration.");
        yield return new(ImplementationFiles[1], SafetyRemediationImplementationFileRole.SharedRuntimeDependency, true, "Integer-ms to beat projection and timing boundaries.");
        yield return new(ImplementationFiles[2], SafetyRemediationImplementationFileRole.SharedRuntimeDependency, true, "Timed source objects used to seed both geometries.");
        yield return new(ImplementationFiles[4], SafetyRemediationImplementationFileRole.SharedRuntimeDependency, true, "Durable object/result representation.");
        yield return new(ImplementationFiles[5], SafetyRemediationImplementationFileRole.SharedRuntimeDependency, true, "The durable integer coordinates serialized and reparsed by the claim.");
        yield return new("src/ManiaAddNotesLab.Core/GenerationProvenanceResearch.cs", SafetyRemediationImplementationFileRole.InstrumentationOnly, false, "Defines GenerationState semantics but cannot decide placement.");
        yield return new("src/ManiaAddNotesLab.Core/GeometrySafetyAttributionResearch.cs", SafetyRemediationImplementationFileRole.InstrumentationOnly, false, "Independent HardValidity oracle only.");
        yield return new("src/ManiaAddNotesLab.Core/SafetyRemediationGateHardeningResearch.cs", SafetyRemediationImplementationFileRole.InstrumentationOnly, false, "Causal recertification model; frozen separately by harness snapshot.");
        yield return new("tools/ManiaAddNotesLab.Experiments/SafetyRemediationGateHardeningRunner.cs", SafetyRemediationImplementationFileRole.CertificationOnly, false, "Executes the frozen matrix; frozen separately by harness snapshot.");
        yield return new("tests/ManiaAddNotesLab.Tests/PhaseSafetyRemediationGateTests.cs", SafetyRemediationImplementationFileRole.CertificationOnly, false, "End-to-end full-versus-streaming trace equivalence; frozen separately by harness snapshot.");
        yield return new("tests/ManiaAddNotesLab.Tests/PhaseSafetyRemediationGateHardeningTests.cs", SafetyRemediationImplementationFileRole.CertificationOnly, false, "Adversarial and cached-versus-uncached equivalence controls; frozen separately by harness snapshot.");
        yield return new("docs/PHASE_SAFETY_REMEDIATION_GATE.md", SafetyRemediationImplementationFileRole.DocumentationOnly, false, "Historical report; never executable.");
    }

    private static ImmutableArray<HistoricalCase> ReadHistoricalCases(string path)
    {
        using var parser = new TextFieldParser(path) { TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        var header = parser.ReadFields() ?? throw new InvalidDataException("Missing historical CSV header.");
        var index = header.Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);
        var result = ImmutableArray.CreateBuilder<HistoricalCase>();
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields()!;
            result.Add(new(row[index["stratum"]], row[index["chart_id"]],
                int.Parse(row[index["seed"]], CultureInfo.InvariantCulture), row[index["frozen_arm"]],
                row[index["opportunity_key"]], int.Parse(row[index["lane"]], CultureInfo.InvariantCulture),
                int.Parse(row[index["start_time"]], CultureInfo.InvariantCulture),
                string.IsNullOrEmpty(row[index["end_time"]]) ? null
                    : int.Parse(row[index["end_time"]], CultureInfo.InvariantCulture),
                bool.Parse(row[index["frozen_known"]]), row[index["explanation_class"]]));
        }
        return result.ToImmutable();
    }

    private static void WriteCases(string path, IEnumerable<HardenedCase> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine("stratum,chart_id,seed,frozen_arm,opportunity_key,lane,start_time,end_time,frozen_known,historical_class,hardened_class,direct_reject,lineage_id,divergence_opportunity,divergence_control_state,divergence_treatment_state,parent_states_different,no_reconvergence_before_case,target_absent_in_treatment,candidate_shape_reached_in_treatment,frozen_lane_selected_in_treatment,frozen_commit_absent_in_treatment,divergence_governed_by_canonical,temporal_only_explanation,evidence");
        foreach (var x in rows.OrderBy(x => x.Stratum).ThenBy(x => x.ChartId).ThenBy(x => x.Seed)
                     .ThenBy(x => x.OpportunityKey, StringComparer.Ordinal))
            writer.WriteLine(string.Join(',', x.Stratum, x.ChartId, x.Seed, x.FrozenArm,
                Csv(x.OpportunityKey), x.Lane, x.StartTime, x.EndTime?.ToString() ?? "", B(x.FrozenKnown),
                x.HistoricalClass, x.HardenedClass, B(x.DirectReject), Csv(x.LineageId ?? ""),
                Csv(x.DivergenceOpportunity ?? ""), x.DivergenceControlState ?? "",
                x.DivergenceTreatmentState ?? "", B(x.ParentStatesDifferent),
                B(x.NoReconvergenceBeforeCase), B(x.TargetAbsentInTreatment),
                B(x.CandidateShapeReachedInTreatment), B(x.FrozenLaneSelectedInTreatment),
                B(x.FrozenCommitAbsentInTreatment),
                B(x.DivergenceGovernedByCanonical), B(x.TemporalOnlyExplanation), Csv(x.Evidence)));
    }

    private static void WriteUnreachable(string path, IEnumerable<HardenedCase> rows) => WriteCases(path, rows);

    private static void WriteRuns(string path, IEnumerable<HardeningRun> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine("stratum,chart_id,seed,control_hard_violations,treatment_hard_violations,treatment_only_hard_violations,default_reference_exact,treatment_deterministic,control_reparse_exact,treatment_reparse_exact,g1_evidence_invariant,gate_rng_calls,direct_governed_divergences,reconvergence_count,unexplained_state_divergences");
        foreach (var x in rows) writer.WriteLine(string.Join(',', x.Stratum, x.ChartId, x.Seed,
            x.ControlHardViolations, x.TreatmentHardViolations, x.TreatmentOnlyHardViolations,
            B(x.DefaultReferenceExact), B(x.TreatmentDeterministic), B(x.ControlReparseExact),
            B(x.TreatmentReparseExact), B(x.G1EvidenceInvariant), x.GateRngCalls,
            x.DirectGovernedDivergences, x.ReconvergenceCount, x.UnexplainedStateDivergences));
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Json) + Environment.NewLine,
            new UTF8Encoding(false));
    }
    private static void ReleasePairDiagnostics()
    {
        // Large engine results are dead after their compact summary has been retained.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
        GC.WaitForPendingFinalizers();
    }
    private static StreamWriter Writer(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new StreamWriter(path, false, new UTF8Encoding(false));
    }
    private static string Materialized(ManiaObject value) =>
        $"{value.Lane}|{value.StartTime}|{value.EndTime}|{value.Type}";
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string B(bool value) => value ? "true" : "false";

    private sealed record Execution(string Serialized, long RngCalls, string RngHash, bool ReparseExact,
        ImmutableArray<string> Violations, SafetyRemediationGateRuntimeDiagnostics Diagnostics,
        string? G1Before, string? G1After);
    private sealed record DeterminismReference(string SerializedHash, long RngCalls, string RngHash,
        string DiagnosticsFingerprint);
    private sealed record CompactExecution(string SerializedHash, long RngCalls, string RngHash,
        bool ReparseExact, ImmutableArray<string> Violations, CompactDiagnostics Diagnostics,
        string DiagnosticsFingerprint, string? G1Before, string? G1After);
    private sealed record CompactDiagnostics(int GateRngCalls,
        ImmutableArray<SafetyRemediationCandidateGeometryDecision> CandidateDecisions,
        int OpportunityCount);
    private sealed record TraceAnalysis(int DirectGovernedDivergences, int Reconvergences,
        int UnexplainedStateDivergences, ImmutableArray<HardenedCase> Cases);
    private sealed record TargetObservation(bool BeforeEqual, string? ActiveLineageBefore,
        string? ControlCommit, string? TreatmentCommit);
    private sealed record OriginObservation(string OpportunityKey, string ControlAfterHash,
        string TreatmentAfterHash);
    private sealed record TraceRow(string BeforeHash, bool BeforeComplete, string AfterHash,
        bool AfterComplete, string? CommitIdentity);
    private sealed record HistoricalCase(string Stratum, string ChartId, int Seed, string FrozenArm,
        string OpportunityKey, int Lane, int StartTime, int? EndTime, bool FrozenKnown, string HistoricalClass);
    private sealed record HardenedCase(string Stratum, string ChartId, int Seed, string FrozenArm,
        string OpportunityKey, int Lane, int StartTime, int? EndTime, bool FrozenKnown,
        string HistoricalClass, string HardenedClass, bool DirectReject, string? LineageId,
        string? DivergenceOpportunity, string? DivergenceControlState, string? DivergenceTreatmentState,
        bool ParentStatesDifferent, bool NoReconvergenceBeforeCase, bool TargetAbsentInTreatment,
        bool CandidateShapeReachedInTreatment, bool FrozenLaneSelectedInTreatment,
        bool FrozenCommitAbsentInTreatment,
        bool DivergenceGovernedByCanonical, bool TemporalOnlyExplanation, string Evidence);
    private sealed record HardeningRun(string Stratum, string ChartId, int Seed,
        int ControlHardViolations, int TreatmentHardViolations, int TreatmentOnlyHardViolations,
        bool DefaultReferenceExact, bool TreatmentDeterministic, bool ControlReparseExact,
        bool TreatmentReparseExact, bool G1EvidenceInvariant, int GateRngCalls,
        int DirectGovernedDivergences, int ReconvergenceCount, int UnexplainedStateDivergences);
    private sealed record HardeningContract(string SchemaVersion, string Phase, string RepositoryEntryHead,
        string OriginalContractSha256, string OriginalImplementationSnapshotSha256,
        string HardenedImplementationSnapshotSha256, string CertificationHarnessSnapshotSha256,
        string CorpusManifestSha256, string ExecutionMemoryLimit,
        ImmutableArray<string> ImplementationFiles,
        ImmutableArray<string> CertificationHarnessFiles, string SufficientStateDefinition,
        string GovernedDivergenceAndReconvergenceDefinition, ImmutableArray<string> Denominators,
        ImmutableArray<string> Exclusions, ImmutableArray<string> ExpectedArtifacts,
        string SuccessorAuthorization);
    private sealed record HardeningContractArtifact(HardeningContract Contract, string CanonicalSha256,
        string Canonicalization);

    private sealed class DiscardOpportunitySink : ISafetyRemediationGateOpportunitySink
    {
        public static readonly DiscardOpportunitySink Instance = new();
        public void ObserveCandidate(SafetyRemediationCandidateGeometryDecision decision) { }
        public void Observe(SafetyRemediationGateCompactOpportunityState state) { }
    }

    private sealed class BinaryOpportunityTraceSink : ISafetyRemediationGateOpportunitySink, IDisposable
    {
        private readonly BinaryWriter writer;
        private readonly HashSet<string> targetKeys;
        private readonly ImmutableArray<SafetyRemediationCandidateGeometryDecision>.Builder candidates =
            ImmutableArray.CreateBuilder<SafetyRemediationCandidateGeometryDecision>();
        private string? previousAfter;
        private int nextOrder;
        public ImmutableArray<SafetyRemediationCandidateGeometryDecision> Candidates => candidates.ToImmutable();
        public BinaryOpportunityTraceSink(string path, IEnumerable<string> targetKeys)
        {
            this.targetKeys = targetKeys.ToHashSet(StringComparer.Ordinal);
            writer = new BinaryWriter(
                new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 20),
                Encoding.UTF8, leaveOpen: false);
        }
        public void ObserveCandidate(SafetyRemediationCandidateGeometryDecision decision)
        {
            if (decision.GeometrySetsDiffer || targetKeys.Contains(decision.OpportunityKey))
                candidates.Add(decision);
        }
        public void Observe(SafetyRemediationGateCompactOpportunityState state)
        {
            if (state.OpportunityOrder != nextOrder++)
                throw new InvalidOperationException("Opportunity sink order drifted.");
            if (previousAfter is null)
            {
                WriteHash(state.StateBeforeHash);
                writer.Write(state.StateBeforeComplete);
            }
            else if (previousAfter != state.StateBeforeHash)
                throw new InvalidOperationException("Adjacent sufficient states did not chain exactly.");
            WriteHash(state.StateAfterHash);
            writer.Write(state.StateAfterComplete);
            writer.Write(state.CommittedObjectIdentity is not null);
            if (state.CommittedObjectIdentity is not null) writer.Write(state.CommittedObjectIdentity);
            previousAfter = state.StateAfterHash;
        }
        private void WriteHash(string value) => writer.Write(Convert.FromHexString(value));
        public void Dispose() => writer.Dispose();
    }

    private sealed class BinaryOpportunityTraceReader : IDisposable
    {
        private readonly FileStream stream;
        private readonly BinaryReader reader;
        private readonly int count;
        private string? currentHash;
        private bool currentComplete;
        private int nextOrder;
        public BinaryOpportunityTraceReader(string path, int count)
        {
            this.count = count;
            stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
            reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            if (count > 0)
            {
                currentHash = ReadHash();
                currentComplete = reader.ReadBoolean();
            }
        }
        public TraceRow Read(int order)
        {
            if (order != nextOrder || nextOrder >= count || currentHash is null)
                throw new InvalidOperationException("Opportunity trace read order drifted.");
            nextOrder++;
            var after = ReadHash();
            var afterComplete = reader.ReadBoolean();
            var commit = reader.ReadBoolean() ? reader.ReadString() : null;
            var row = new TraceRow(currentHash, currentComplete, after, afterComplete, commit);
            currentHash = after;
            currentComplete = afterComplete;
            return row;
        }
        private string ReadHash()
        {
            var bytes = reader.ReadBytes(32);
            if (bytes.Length != 32) throw new EndOfStreamException("Truncated opportunity hash.");
            return Convert.ToHexString(bytes);
        }
        public void VerifyComplete()
        {
            if (nextOrder != count || stream.Position != stream.Length)
                throw new InvalidDataException("Opportunity trace length drifted.");
        }
        public void Dispose() { reader.Dispose(); stream.Dispose(); }
    }

    private sealed class TranscriptRandom(int seed) : IRandomPositionSource
    {
        private readonly SeededRandom inner = new(seed);
        private readonly SafetyRemediationGateTranscriptHash transcript = new();
        public long CallCount { get; private set; }
        public string Hash => transcript.CurrentHash;
        public double NextDouble()
        {
            var value = inner.NextDouble(); CallCount++;
            transcript.AppendDouble(value);
            return value;
        }
        public int Next(int maximum)
        {
            var value = inner.Next(maximum); CallCount++;
            transcript.AppendInteger(maximum, value);
            return value;
        }
    }
}
