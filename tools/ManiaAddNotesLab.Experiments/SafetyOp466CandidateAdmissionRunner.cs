using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class SafetyOp466CandidateAdmissionRunner
{
    private const string MemoryLimit = "0x400000000";
    private const string ChartId = "20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788";
    private const int Seed = 4;
    private const string Op370 = "OP-00000370-BaseHead-S370-T29933-ANA";
    private const string Op466 = "OP-00000466-BaseHead-S466-T35009-ANA";
    private const string Implementation = "B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835";
    private const string Corpus = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    private const string LegacyOutput = "71D78994D7D655BF0E2CB5F423B775805BFD0463864DE6EB9BB7DE0415E93D94";
    private const string LegacyRng = "804CA1EF7A1F88A31E80FDDDCD2960C9E9FB3D4B47FC10195CA2D8432B80FB90";
    private const string LegacyDiagnostics = "9FBA7641102C6821EAA58A9270977141C1856D22B7ABB17C59B918F14752AA6B";
    private const string CanonicalOutput = "462307AE599D390CCC56595CCBE9ABAB02D36EF35AC4E00A69D270963438E999";
    private const string CanonicalRng = "8F5589B0045254ECCAE68DFD3E01037D8C5771CA6BD7081132B173C2B6762DED";
    private const string CanonicalDiagnostics = "26804F0A5753C75738C7CC14286CDA090C847292242649CFBBD3CCE534D74875";
    private const string TargetCommit =
        "2|35009||Tap|466|HeadOpportunity|74.49995000000002979998|74.49995000000002979998";

    private static readonly string[] ImplementationFiles =
    [
        "src/ManiaAddNotesLab.Core/AddNotesEngine.cs", "src/ManiaAddNotesLab.Core/BeatTimeline.cs",
        "src/ManiaAddNotesLab.Core/ChartAnalysis.cs", "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
        "src/ManiaAddNotesLab.Core/Model.cs", "src/ManiaAddNotesLab.Core/OsuBeatmap.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateResearch.cs"
    ];

    private static readonly string[] Sources =
    [
        "docs/PHASE_SAFETY_REMEDIATION_GATE.md",
        "docs/PHASE_SAFETY_REMEDIATION_GATE_FINAL_RECERTIFICATION.md",
        "docs/PHASE_SAFETY_REMEDIATION_GATE_FORENSIC_ATTRIBUTION.md",
        "docs/PHASE_SAFETY_SELECTION_SET_REMAPPING_DESIGN.md",
        "docs/PHASE_SAFETY_SELECTION_SET_REMAPPING_GATE.md",
        "docs/PHASE_SAFETY_SELECTION_SET_REMAPPING_HARNESS_HARDENING.md",
        "docs/PHASE_SAFETY_OP185_OP466_COUNTERFACTUAL_DESIGN.md",
        "docs/PHASE_SAFETY_OP185_OP466_COUNTERFACTUAL_GATE.md",
        "src/ManiaAddNotesLab.Core/AddNotesEngine.cs",
        "src/ManiaAddNotesLab.Core/Model.cs",
        "src/ManiaAddNotesLab.Core/ChartAnalysis.cs",
        "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
        "src/ManiaAddNotesLab.Core/D1BehavioralExperiment.cs",
        "src/ManiaAddNotesLab.Core/SafetyRemediationGateResearch.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetySelectionSetRemappingRunner.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetyOp185Op466CounterfactualRunner.cs",
        ".artifacts/safety_selection_set_remapping/op370_op466_downstream.json",
        ".artifacts/safety_selection_set_remapping/op185_analysis.json",
        ".artifacts/safety_selection_set_remapping/no_interference.csv",
        ".artifacts/safety_op185_op466_counterfactual/op466_result.json",
        ".artifacts/safety_op185_op466_counterfactual/rng_transcripts.json",
        ".artifacts/safety_op185_op466_counterfactual/summary.json"
    ];

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Run(string repositoryRoot, string corpusRoot, string manifestPath, string outputDirectory)
    {
        RequireMemoryLimit();
        var head = GitHead(repositoryRoot);
        var implementation = Snapshot(repositoryRoot, ImplementationFiles);
        if (implementation != Implementation)
            throw new InvalidDataException($"Behavioral implementation drifted: {implementation}.");
        var manifest = FrozenC11Manifest.Load(manifestPath);
        if (manifest.CanonicalSha256 != Corpus)
            throw new InvalidDataException("Frozen C11 corpus identity drifted.");
        VerifyHistoricalHashes(repositoryRoot);
        var corpus = C11CorpusDiscovery.ResolveFrozen(corpusRoot, manifest.Charts);
        var descriptor = corpus.Charts.Single(x => x.Sha256 == ChartId);
        var chartText = File.ReadAllText(descriptor.RuntimePath);
        var options = InteriorRelationMembershipResearch.CurrentOptions() with
            { Chance = .50, ArticulationEnabled = false, Trace = false, DiagnosticsEnabled = false };

        var legacy = Execute(chartText, options, false);
        var canonical = Execute(chartText, options, true);
        var historicalB = ReadHistoricalB(repositoryRoot);
        var inputs = CalculateAdmissionInputs(legacy.Chart, options);
        var legacy466 = Inspect(legacy, Op466, inputs);
        var canonical466 = Inspect(canonical, Op466, inputs);
        var b466 = historicalB with
        {
            EffectiveChance = inputs.EffectiveChance,
            ProbabilityPassed = historicalB.Sample < inputs.EffectiveChance
        };

        if (!legacy466.ProbabilityPassed || canonical466.ProbabilityPassed || b466.ProbabilityPassed)
            throw new InvalidDataException("Observed OP-466 admission decisions do not match frozen evidence.");
        if (legacy466.CommitIdentity != TargetCommit || legacy466.Candidate is null
            || canonical466.Candidate is not null || canonical466.CommitIdentity is not null)
            throw new InvalidDataException("OP-466 candidate/commit evidence drifted.");

        var divergences = RngConsumptionDivergences(legacy, canonical);
        var first = divergences.First();
        if (first.OpportunityKey != Op370 || first.DeltaBefore != 0 || first.DeltaAfter != 1)
            throw new InvalidDataException("First pertinent RNG-consumption divergence drifted.");
        var legacy370 = Inspect(legacy, Op370, CalculateAdmissionInputs(legacy.Chart, options, 370, 29933));
        var canonical370 = Inspect(canonical, Op370, CalculateAdmissionInputs(canonical.Chart, options, 370, 29933));
        if (!legacy370.ProbabilityPassed || !canonical370.ProbabilityPassed
            || legacy370.Candidate is null || canonical370.Candidate is null
            || legacy370.Candidate.SelectedLane != 4 || canonical370.Candidate.SelectedLane is not null)
            throw new InvalidDataException("OP-370 branch evidence drifted.");

        Directory.CreateDirectory(outputDirectory);
        WriteJson(Path.Combine(outputDirectory, "source_inventory.json"), new
        {
            schemaVersion = "safety-op466-source-inventory.1", repositoryHead = head,
            implementationSnapshotSha256 = implementation, corpusManifestSha256 = manifest.CanonicalSha256,
            chart = ChartId, seed = Seed, sources = Sources.Select(path => new
            {
                path, sha256 = FileHash(Path.Combine(repositoryRoot, Native(path)))
            })
        });
        WriteJson(Path.Combine(outputDirectory, "admission_decisions.json"), new
        {
            schemaVersion = "safety-op466-admission-decisions.1", inputs,
            legacy = legacy466, canonicalNormal = canonical466, canonicalOp185Intervened = b466,
            exactCondition = "effectiveChance > 0 && (effectiveChance >= 1 || rng.NextDouble() < effectiveChance)"
        });
        WriteJson(Path.Combine(outputDirectory, "lab_comparison.json"), new
        {
            schemaVersion = "safety-op466-lab-comparison.1", chart = ChartId, seed = Seed,
            comparableConfiguration = true, sameLogicalOpportunity = true,
            legacy = legacy466, canonicalNormal = canonical466, canonicalOp185Intervened = b466,
            canonicalArmsUseSameAuthenticTranscript = historicalB.TranscriptIdentity == CanonicalRng,
            canonicalArmsTakeSameAdmissionDecisionFromSameInputs =
                canonical466.RngPositionBefore == b466.RngPositionBefore
                && canonical466.Sample == b466.Sample
                && canonical466.EffectiveChance == b466.EffectiveChance
        });
        WriteJson(Path.Combine(outputDirectory, "first_rng_divergence.json"), new
        {
            schemaVersion = "safety-op466-first-rng-divergence.1", first,
            op370 = new { legacy = legacy370, canonical = canonical370 },
            sharedProbabilityCall = legacy.Calls[532],
            legacyAdditionalSelectionCall = legacy.Calls[533],
            canonicalCallAtPosition533BelongsToNextOpportunity = canonical.Calls[533],
            explanation = "Both arms consume the same probability Double at position 532. Legacy then has lane [4] and consumes Next(1) at 533 to commit lane 4; canonical has no legal lane and makes no selection call. This is the first offset, not the sole contributor to the +8 offset before OP-466."
        });
        WriteDivergences(Path.Combine(outputDirectory, "rng_consumption_divergences.csv"), divergences);
        WriteJson(Path.Combine(outputDirectory, "no_interference.json"), NoInterference(legacy, canonical));
        WriteJson(Path.Combine(outputDirectory, "op466_explanation.json"), new
        {
            schemaVersion = "safety-op466-explanation.1", outcome = "MECHANISM_IDENTIFIED",
            demonstrated = new[]
            {
                "OP-466 exists and is reached in L, A and B.",
                "The original-only density inputs and configuration give the same effective chance in all arms.",
                "Legacy consumes a different RNG sample, passes probability admission, builds a candidate, evaluates lanes, selects lane 2 and commits the historical target.",
                "Canonical A and B consume the identical authentic sample at position 659, fail probability admission and return before candidate construction or lane evaluation.",
                "The first consumption offset begins at OP-370; later differing per-opportunity consumption grows the offset to eight calls before OP-466."
            },
            hypotheses = new
            {
                h1 = "ACCEPTED for the immediate OP-466 admission decision: different samples are compared with the same threshold.",
                h2 = "REJECTED: no other pre-candidate condition differs at OP-466.",
                h3 = "NOT THE ADMISSION MECHANISM: materialized state differs, but density probability reads originals only; state would affect geometry only after admission.",
                h4 = "REJECTED: opportunity key, order, source and original-only context are identical.",
                h5 = "ACCEPTED upstream: multiple earlier branch/consumption differences accumulate; OP-370 is only the first.",
                h6 = "NOT OBSERVED."
            },
            limitation = "This identifies the concrete admission mechanism, not an exclusive historical cause for every upstream RNG offset and not a reclassification of the frozen sixth case.",
            historicalScientificStatus = "NEEDS_REVIEW", promotion = "NOT_AUTHORIZED"
        });
        WriteJson(Path.Combine(outputDirectory, "summary.json"), new
        {
            schemaVersion = "safety-op466-candidate-admission-summary.1", outcome = "MECHANISM_IDENTIFIED",
            repositoryHead = head, implementationSnapshotSha256 = implementation,
            harnessSnapshotSha256 = Snapshot(repositoryRoot,
                ["tools/ManiaAddNotesLab.Experiments/Program.cs",
                 "tools/ManiaAddNotesLab.Experiments/SafetyOp466CandidateAdmissionRunner.cs"]),
            corpusManifestSha256 = Corpus, chart = ChartId, seed = Seed,
            fullMatrixExecuted = false, canonicalBReexecuted = false, behaviorChanged = false,
            historicalClassificationChanged = false, globalStatus = "NEEDS_REVIEW", promotion = "NOT_AUTHORIZED"
        });
        WriteHashes(outputDirectory);
        return "MECHANISM_IDENTIFIED";
    }

    private static Execution Execute(string chartText, AddNotesOptions options, bool canonical)
    {
        var chart = OsuBeatmap.Parse(chartText);
        var random = new RecordingRandom(Seed);
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var gate = SafetyRemediationGateRuntimeConfiguration.Frozen(canonical,
            compactDiagnostics: false, retainCandidateDecisions: true);
        var result = new AddNotesEngine().Apply(chart, options, random, profile, null, null, null, null, gate);
        var diagnostics = result.SafetyRemediationGateDiagnostics
            ?? throw new InvalidDataException("Full remediation diagnostics were not captured.");
        return new(chart, Hash(OsuBeatmap.Write(result.ModifiedChart, options.Chance)), random.CallCount,
            random.Hash, random.Calls, diagnostics,
            SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(diagnostics));
    }

    private static AdmissionInputs CalculateAdmissionInputs(ManiaChart chart, AddNotesOptions options,
        int sequence = 466, int time = 35009)
    {
        var timeline = new BeatTimeline(chart.TimingPoints);
        var analysis = new OriginalChartAnalysis(chart, timeline);
        var source = analysis.Objects.Single(x => x.Object.Sequence == sequence && x.Object.StartTime == time);
        var geometry = new LaneGeometryIndex(chart.KeyCount, analysis.Objects);
        var density = new AddNotesEngine().AnalyzeDensity(chart, options, time);
        var stats = new AddNotesStatistics();
        var occupied = geometry.CountOccupiedColumns(source.StartBeat, stats);
        var simultaneous = geometry.CountSimultaneousHeadColumns(source.StartBeat);
        var held = geometry.CountHeldLnColumns(source.StartBeat);
        var columns = options.VerticalDensityMode == VerticalDensityMode.SimultaneousHeads
            ? simultaneous : occupied;
        var vertical = new AddNotesEngine().AnalyzeVerticalDensity(chart.KeyCount, columns, options);
        var contextual = options.ContextualDensityNormalizationEnabled ? density.ContextualFactor : 1d;
        var effective = Math.Min(options.Chance, options.Chance * vertical.ChordFactor * contextual);
        return new(sequence, time, source.StartBeat, chart.KeyCount, options.Chance, occupied, simultaneous,
            held, density, vertical, contextual, effective,
            "All inputs are derived from the immutable original chart; added geometry is not consulted by probability admission.");
    }

    private static AdmissionDecision Inspect(Execution execution, string key, AdmissionInputs inputs)
    {
        var state = execution.Diagnostics.OpportunityStates.Single(x => x.OpportunityKey == key);
        var call = execution.Calls[checked((int)state.StateBefore.RootRngPosition)];
        if (call.Kind != SelectionRandomCallKind.Double || call.DoubleValue is null)
            throw new InvalidDataException($"Expected probability Double at {key}.");
        var candidates = execution.Diagnostics.CandidateDecisions
            .Where(x => x.OpportunityOrder == state.OpportunityOrder).ToArray();
        var candidate = candidates.SingleOrDefault();
        var selection = candidate?.SelectedLane is null ? null
            : execution.Calls[checked((int)candidate.RngPositionBeforeDecision)];
        return new(key, state.OpportunityOrder, true, state.StateBefore, state.StateAfter,
            state.StateBefore.RootRngPosition, call.Kind.ToString(), null, call.DoubleValue.Value,
            inputs.EffectiveChance, call.DoubleValue.Value < inputs.EffectiveChance,
            candidate, selection, state.CommittedObjectIdentity, execution.RngHash);
    }

    private static HistoricalDecision ReadHistoricalB(string root)
    {
        var transcriptPath = Path.Combine(root, Native(
            ".artifacts/safety_op185_op466_counterfactual/rng_transcripts.json"));
        using var transcript = JsonDocument.Parse(File.ReadAllText(transcriptPath));
        var treatment = transcript.RootElement.GetProperty("treatment");
        var call = treatment.GetProperty("calls")[659];
        var resultPath = Path.Combine(root, Native(
            ".artifacts/safety_op185_op466_counterfactual/op466_result.json"));
        using var result = JsonDocument.Parse(File.ReadAllText(resultPath));
        var b = result.RootElement.GetProperty("treatment");
        return new(Op466, b.GetProperty("opportunityOrder").GetInt32(), true,
            b.GetProperty("parent").Deserialize<SafetyRemediationGateSufficientState>(Json)!,
            b.GetProperty("successor").Deserialize<SafetyRemediationGateSufficientState>(Json)!,
            b.GetProperty("parentRngPosition").GetInt64(), "Double", null,
            call.GetProperty("doubleValue").GetDouble(), 0, false, null, null, null,
            treatment.GetProperty("authenticRngSha256").GetString()!, "historical artifact; B not reexecuted");
    }

    private static ImmutableArray<RngDivergence> RngConsumptionDivergences(Execution legacy, Execution canonical)
    {
        var right = canonical.Diagnostics.OpportunityStates.ToDictionary(x => x.OpportunityKey);
        return legacy.Diagnostics.OpportunityStates.Where(x => x.OpportunityOrder <= 468)
            .Select(x => (L: x, C: right[x.OpportunityKey]))
            .Select(x => new RngDivergence(x.L.OpportunityKey, x.L.OpportunityOrder,
                x.L.StateBefore.RootRngPosition, x.L.StateAfter.RootRngPosition,
                x.C.StateBefore.RootRngPosition, x.C.StateAfter.RootRngPosition,
                x.L.StateBefore.RootRngPosition - x.C.StateBefore.RootRngPosition,
                x.L.StateAfter.RootRngPosition - x.C.StateAfter.RootRngPosition,
                x.L.StateAfter.RootRngPosition - x.L.StateBefore.RootRngPosition,
                x.C.StateAfter.RootRngPosition - x.C.StateBefore.RootRngPosition,
                x.L.CommittedObjectIdentity, x.C.CommittedObjectIdentity))
            .Where(x => x.LegacyCalls != x.CanonicalCalls).ToImmutableArray();
    }

    private static object NoInterference(Execution legacy, Execution canonical)
    {
        var values = new
        {
            legacy = new { legacy.OutputHash, legacy.CallCount, legacy.RngHash, legacy.DiagnosticsHash,
                outputExact = legacy.OutputHash == LegacyOutput, rngCountExact = legacy.CallCount == 13730,
                rngExact = legacy.RngHash == LegacyRng, diagnosticsExact = legacy.DiagnosticsHash == LegacyDiagnostics },
            canonical = new { canonical.OutputHash, canonical.CallCount, canonical.RngHash, canonical.DiagnosticsHash,
                outputExact = canonical.OutputHash == CanonicalOutput, rngCountExact = canonical.CallCount == 13730,
                rngExact = canonical.RngHash == CanonicalRng,
                diagnosticsExact = canonical.DiagnosticsHash == CanonicalDiagnostics },
            opportunityAndOrderExact = legacy.Diagnostics.OpportunityStates.Any(x => x.OpportunityKey == Op466 && x.OpportunityOrder == 468)
                && canonical.Diagnostics.OpportunityStates.Any(x => x.OpportunityKey == Op466 && x.OpportunityOrder == 468),
            g1 = "not applicable; frozen primary comparison has G1 disabled",
            observationalRecorderAddsRngCalls = false
        };
        if (!values.legacy.outputExact || !values.legacy.rngCountExact || !values.legacy.rngExact
            || !values.legacy.diagnosticsExact || !values.canonical.outputExact
            || !values.canonical.rngCountExact || !values.canonical.rngExact
            || !values.canonical.diagnosticsExact || !values.opportunityAndOrderExact)
            throw new InvalidDataException("Focused reproduction failed frozen no-interference identities.");
        return values;
    }

    private static void VerifyHistoricalHashes(string root)
    {
        foreach (var directory in new[]
                 { ".artifacts/safety_selection_set_remapping", ".artifacts/safety_op185_op466_counterfactual" })
        {
            var nativeDirectory = Path.Combine(root, Native(directory));
            var listed = File.ReadAllLines(Path.Combine(nativeDirectory, "sha256sums.txt"))
                .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Split("  ", 2))
                .ToDictionary(x => x[1], x => x[0], StringComparer.Ordinal);
            foreach (var source in Sources.Where(x => x.StartsWith(directory + "/", StringComparison.Ordinal)))
            {
                var name = Path.GetFileName(source);
                if (!listed.TryGetValue(name, out var expected) || FileHash(Path.Combine(root, Native(source))) != expected)
                    throw new InvalidDataException($"Historical artifact hash is not verified: {source}.");
            }
        }
    }

    private static void WriteDivergences(string path, IEnumerable<RngDivergence> values)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("opportunity_key,order,legacy_before,legacy_after,canonical_before,canonical_after,delta_before,delta_after,legacy_calls,canonical_calls,legacy_commit,canonical_commit");
        foreach (var x in values) writer.WriteLine(string.Join(',', Csv(x.OpportunityKey), x.OpportunityOrder,
            x.LegacyBefore, x.LegacyAfter, x.CanonicalBefore, x.CanonicalAfter, x.DeltaBefore, x.DeltaAfter,
            x.LegacyCalls, x.CanonicalCalls, Csv(x.LegacyCommit ?? ""), Csv(x.CanonicalCommit ?? "")));
    }

    private static void RequireMemoryLimit()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_GCHeapHardLimit"), MemoryLimit,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Forensic runner requires DOTNET_GCHeapHardLimit={MemoryLimit}.");
    }
    private static string GitHead(string root)
    {
        var head = File.ReadAllText(Path.Combine(root, ".git", "HEAD")).Trim();
        return head.StartsWith("ref: ", StringComparison.Ordinal)
            ? File.ReadAllText(Path.Combine(root, ".git", Native(head[5..]))).Trim() : head;
    }
    private static string Snapshot(string root, IEnumerable<string> paths) =>
        ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(paths.Select(path =>
            new NamedImplementationContent(path, File.ReadAllBytes(Path.Combine(root, Native(path))))));
    private static string Native(string path) => path.Replace('/', Path.DirectorySeparatorChar);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static void WriteJson<T>(string path, T value) => File.WriteAllText(path,
        JsonSerializer.Serialize(value, Json) + Environment.NewLine, new UTF8Encoding(false));
    private static void WriteHashes(string directory)
    {
        var rows = Directory.EnumerateFiles(directory).Where(x => Path.GetFileName(x) != "sha256sums.txt")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal).Select(x => $"{FileHash(x)}  {Path.GetFileName(x)}");
        File.WriteAllLines(Path.Combine(directory, "sha256sums.txt"), rows, new UTF8Encoding(false));
    }

    private sealed record AdmissionInputs(int SourceSequence, int Time, decimal Beat, int KeyCount,
        double BaseChance, int OccupiedColumns, int SimultaneousHeadColumns, int HeldLnColumns,
        DensitySnapshot Density, VerticalDensitySnapshot Vertical, double ContextualFactor,
        double EffectiveChance, string StateDependency);
    private sealed record AdmissionDecision(string OpportunityKey, int OpportunityOrder, bool Reached,
        SafetyRemediationGateSufficientState Parent, SafetyRemediationGateSufficientState Successor,
        long RngPositionBefore, string RngCallKind, int? Maximum, double Sample, double EffectiveChance,
        bool ProbabilityPassed, SafetyRemediationCandidateGeometryDecision? Candidate,
        SelectionRandomCall? SelectionCall, string? CommitIdentity, string TranscriptIdentity);
    private sealed record HistoricalDecision(string OpportunityKey, int OpportunityOrder, bool Reached,
        SafetyRemediationGateSufficientState Parent, SafetyRemediationGateSufficientState Successor,
        long RngPositionBefore, string RngCallKind, int? Maximum, double Sample, double EffectiveChance,
        bool ProbabilityPassed, SafetyRemediationCandidateGeometryDecision? Candidate,
        SelectionRandomCall? SelectionCall, string? CommitIdentity, string TranscriptIdentity, string Provenance);
    private sealed record RngDivergence(string OpportunityKey, int OpportunityOrder,
        long LegacyBefore, long LegacyAfter, long CanonicalBefore, long CanonicalAfter,
        long DeltaBefore, long DeltaAfter, long LegacyCalls, long CanonicalCalls,
        string? LegacyCommit, string? CanonicalCommit);
    private sealed record Execution(ManiaChart Chart, string OutputHash, long CallCount, string RngHash,
        ImmutableArray<SelectionRandomCall> Calls, SafetyRemediationGateRuntimeDiagnostics Diagnostics,
        string DiagnosticsHash);

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
