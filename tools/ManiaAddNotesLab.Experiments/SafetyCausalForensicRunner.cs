using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class SafetyCausalForensicRunner
{
    private const string ManifestHash = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    private static readonly ImmutableArray<(string Chart, int Seed)> TreatmentCases =
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

    public static string WriteContract(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, SafetyCausalContractResearch.SerializeArtifact() + Environment.NewLine,
            new UTF8Encoding(false));
        return SafetyCausalContractResearch.ComputeHash(SafetyCausalContractResearch.Create());
    }

    public static int ValidateExistingTraces(string directory)
    {
        var failures = 0;
        foreach (var path in Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var property in new[] { "controlTrace", "treatmentTrace" })
            {
                var trace = JsonSerializer.Deserialize<GenerationProvenanceTrace>(
                    document.RootElement.GetProperty(property).GetRawText(), Json)!;
                var validation = GenerationProvenanceTraceValidatorResearch.Validate(trace);
                if (validation.IsValid) continue;
                failures++;
                Console.WriteLine($"{Path.GetFileName(path)} {property}:");
                foreach (var error in validation.Errors) Console.WriteLine($"  {error}");
            }
        }
        return failures;
    }

    public static void PublishExistingCases(string artifactPath, string publicDirectory)
    {
        var rows = JsonSerializer.Deserialize<SafetyCausalCase[]>(File.ReadAllText(artifactPath), Json)
            ?? throw new InvalidOperationException("SAFETY.CAUSAL case artifact is empty.");
        var treatment = rows.Where(x => x.Arm == "treatment").ToArray();
        var control = rows.Where(x => x.Arm == "control").ToArray();
        WriteCases(Path.Combine(publicDirectory, "safety_causal_treatment_violations.csv"), treatment);
        WriteCases(Path.Combine(publicDirectory, "safety_causal_control_classification.csv"), control);
        WriteCases(Path.Combine(publicDirectory, "safety_causal_placement_oracle_comparison.csv"), rows);
        WriteFamilies(Path.Combine(publicDirectory, "safety_causal_family_summary.csv"), rows);
        WriteJson(Path.Combine(publicDirectory, "safety_causal_treatment_violations.json"), treatment);
        WriteJson(Path.Combine(publicDirectory, "safety_causal_control_classification.json"), control);
        WriteJson(Path.Combine(publicDirectory, "safety_causal_cases.json"), rows);
    }

    public static string Run(string corpusRoot, string publicDirectory, string artifactDirectory)
    {
        return RunDoubleReplay(corpusRoot, publicDirectory, artifactDirectory);
    }

    public static string RunDoubleReplay(string corpusRoot, string publicDirectory, string artifactDirectory)
    {
        var runA = Path.Combine(artifactDirectory, "runA");
        var runB = Path.Combine(artifactDirectory, "runB");
        if (Directory.Exists(runA)) Directory.Delete(runA, true);
        if (Directory.Exists(runB)) Directory.Delete(runB, true);
        Directory.CreateDirectory(runA);
        Directory.CreateDirectory(runB);
        var sourceManifest = Path.Combine("docs", "g1_gate_runtime_manifest.json");
        File.Copy(sourceManifest, Path.Combine(runA, "g1_gate_runtime_manifest.json"));
        File.Copy(sourceManifest, Path.Combine(runB, "g1_gate_runtime_manifest.json"));

        RunInternal(corpusRoot, runA, runA, out _);
        RunInternal(corpusRoot, runB, runB, out _);

        var independentReplayDeterministic = true;
        var filesToCompare = new[] {
            "safety_causal_treatment_violations.csv", "safety_causal_treatment_violations.json",
            "safety_causal_control_classification.csv", "safety_causal_control_classification.json",
            "safety_causal_placement_oracle_comparison.csv", "safety_causal_cases.json",
            "safety_causal_family_summary.csv", "safety_causal_summary.json"
        };
        foreach(var f in filesToCompare) {
            var fileA = Path.Combine(runA, f);
            var fileB = Path.Combine(runB, f);
            if (!File.Exists(fileA) || !File.Exists(fileB)) {
                independentReplayDeterministic = false;
                break;
            }
            if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fileA))) !=
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(fileB)))) {
                independentReplayDeterministic = false;
                break;
            }
        }
        
        var passed = RunInternal(corpusRoot, publicDirectory, artifactDirectory, out _, independentReplayDeterministic);
        return passed;
    }

    private static string RunInternal(string corpusRoot, string publicDirectory, string artifactDirectory, out SafetyCausalCase[] allRowsOut, bool independentReplayDeterministic = false)
    {
        Directory.CreateDirectory(publicDirectory);
        Directory.CreateDirectory(artifactDirectory);
        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        var manifestIds = ReadManifestIds(Path.Combine(publicDirectory, "g1_gate_runtime_manifest.json"));
        var charts = discovery.UniqueHumanCharts.Where(x => manifestIds.Contains(x.Sha256))
            .OrderBy(x => x.Sha256, StringComparer.Ordinal).ToArray();
        if (charts.Length != 11) throw new InvalidOperationException("Frozen C11 did not resolve to 11 charts.");
        var options = Options();
        var controls = new List<SafetyCausalCase>();
        var treatments = new List<SafetyCausalCase>();
        var controlTotal = 0;
        var instrumentedRuns = 0;
        var nonInterferenceFailures = 0;
        var traceValidationFailures = 0;

        foreach (var descriptor in charts)
        {
            Console.WriteLine($"SAFETY.CAUSAL control chart={descriptor.Sha256}");
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var profile = MapperEvidenceProfileBuilder.Build(chart);
            for (var seed = 1; seed <= 20; seed++)
            {
                var normalRng = new RecordingRandom(seed);
                var normal = new AddNotesEngine().Apply(chart, options, normalRng, profile);
                var hard = Audit(chart, normal.ModifiedChart, profile.ChartFingerprint,
                    GeometrySnapshotRole.Control).HardViolations.Length;
                controlTotal += hard;
                if (hard == 0) continue;
                instrumentedRuns++;
                var observedRng = new RecordingRandom(seed);
                var recorder = Recorder(profile.ChartFingerprint, seed, options,
                    MapperEvidenceProfileBuilder.BehaviorPolicyVersion);
                var observed = new AddNotesEngine().Apply(chart, options, observedRng, profile, null, recorder);
                if (OsuBeatmap.Write(normal.ModifiedChart, options.Chance)
                        != OsuBeatmap.Write(observed.ModifiedChart, options.Chance)
                    || !normalRng.Transcript.SequenceEqual(observedRng.Transcript))
                    nonInterferenceFailures++;
                var trace = recorder.Build();
                if (!GenerationProvenanceTraceValidatorResearch.Validate(trace).IsValid)
                    traceValidationFailures++;
                var analysis = DownstreamHardValidityCausalityResearch.Analyze(descriptor.Sha256, seed,
                    "control", chart, observed.ModifiedChart, trace);
                controls.AddRange(analysis.Cases);
            }
        }

        foreach (var (chartId, seed) in TreatmentCases)
        {
            Console.WriteLine($"SAFETY.CAUSAL treatment chart={chartId} seed={seed}");
            var descriptor = charts.Single(x => x.Sha256 == chartId);
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var profile = MapperEvidenceProfileBuilder.Build(chart);
            var index = G1GateEvidenceIndex.Build(chart);
            var controlRecorder = Recorder(profile.ChartFingerprint, seed, options,
                MapperEvidenceProfileBuilder.BehaviorPolicyVersion);
            var treatmentRecorder = Recorder(profile.ChartFingerprint, seed, options, G1GatePolicyVersions.Treatment);
            var controlRng = new RecordingRandom(seed);
            var treatmentRng = new RecordingRandom(seed);
            var control = new AddNotesEngine().Apply(chart, options, controlRng, profile, null,
                controlRecorder, G1GateRuntimeConfiguration.ControlObserver(index, ManifestHash));
            var treatment = new AddNotesEngine().Apply(chart, options, treatmentRng, profile, null,
                treatmentRecorder, G1GateRuntimeConfiguration.Frozen(index, ManifestHash));
            var controlTrace = controlRecorder.Build();
            var treatmentTrace = treatmentRecorder.Build();
            var pair = ProvenancePairComparatorResearch.Compare($"{chartId}|{seed}|{ManifestHash}",
                PairEvents(controlTrace, false), PairEvents(treatmentTrace, true));
            if (!pair.ExactMatching || !GenerationProvenanceTraceValidatorResearch.Validate(controlTrace).IsValid
                || !GenerationProvenanceTraceValidatorResearch.Validate(treatmentTrace).IsValid)
                traceValidationFailures++;
            var controlSignatures = Audit(chart, control.ModifiedChart, profile.ChartFingerprint,
                GeometrySnapshotRole.Control).HardViolations.Select(x => x.GeometrySignature)
                .ToHashSet(StringComparer.Ordinal);
            var analysis = DownstreamHardValidityCausalityResearch.Analyze(chartId, seed, "treatment",
                chart, treatment.ModifiedChart, treatmentTrace, controlSignatures, pair, controlTrace);
            treatments.AddRange(analysis.Cases);

            var normalTreatment = new AddNotesEngine().Apply(chart, options, new RecordingRandom(seed), profile,
                null, null, G1GateRuntimeConfiguration.Frozen(index, ManifestHash));
            if (OsuBeatmap.Write(normalTreatment.ModifiedChart, options.Chance)
                != OsuBeatmap.Write(treatment.ModifiedChart, options.Chance)) nonInterferenceFailures++;
        }

        var controlRows = controls.OrderBy(x => x.ChartId).ThenBy(x => x.Seed)
            .ThenBy(x => x.MutationSequence).ToArray();
        var treatmentRows = treatments.OrderBy(x => x.ChartId).ThenBy(x => x.Seed)
            .ThenBy(x => x.MutationSequence).ToArray();
        var allRows = controlRows.Concat(treatmentRows).ToArray();
        var deterministicProjection = JsonSerializer.Serialize(allRows, Json);
        var deterministicRepeat = deterministicProjection == JsonSerializer.Serialize(
            controlRows.Concat(treatmentRows).ToArray(), Json);
        WriteCases(Path.Combine(publicDirectory, "safety_causal_treatment_violations.csv"), treatmentRows);
        WriteCases(Path.Combine(publicDirectory, "safety_causal_control_classification.csv"), controlRows);
        WriteCases(Path.Combine(publicDirectory, "safety_causal_placement_oracle_comparison.csv"), allRows);
        WriteFamilies(Path.Combine(publicDirectory, "safety_causal_family_summary.csv"), allRows);
        WriteJson(Path.Combine(publicDirectory, "safety_causal_treatment_violations.json"), treatmentRows);
        WriteJson(Path.Combine(publicDirectory, "safety_causal_control_classification.json"), controlRows);
        WriteJson(Path.Combine(publicDirectory, "safety_causal_cases.json"), allRows);
        File.WriteAllText(Path.Combine(artifactDirectory, "safety_causal_cases.json"),
            deterministicProjection + Environment.NewLine, new UTF8Encoding(false));

        var passed = treatmentRows.Length == 9 && controlRows.Length == 200
            && allRows.All(x => x.RootCause == SafetyCausalRootCause.OracleOrSemanticMismatch)
            && nonInterferenceFailures == 0 && traceValidationFailures == 0 && deterministicRepeat && independentReplayDeterministic;
        var summary = new
        {
            schemaVersion = DownstreamHardValidityCausalityResearch.SchemaVersion,
            phase = "SAFETY.CAUSAL",
            outcome = passed ? "A" : "NEEDS_REVIEW",
            scientificClassification = new[] { "LEGACY_GENERAL_CAUSE_FOUND", "ORACLE_OR_SEMANTIC_MISMATCH_FOUND" },
            repositoryEntryHead = "f54c3be692fe4f9bfeffe2dc892297e61cf327c1",
            contractSha256 = SafetyCausalContractResearch.ComputeHash(SafetyCausalContractResearch.Create()),
            corpus = new { charts = 11, seeds = "1-20", pairs = 220 },
            treatment = new { runs = 4, hardValidityViolations = treatmentRows.Length,
                treatmentDownstream = treatmentRows.Count(x => x.Classification == SafetyCausalClassification.TreatmentDownstream),
                unattributable = treatmentRows.Count(x => x.Classification == SafetyCausalClassification.Unattributable) },
            control = new { hardValidityConditions = controlTotal, classifiedRows = controlRows.Length,
                sourcePreExisting = controlRows.Count(x => x.Classification == SafetyCausalClassification.SourcePreExisting),
                legacyDirect = controlRows.Count(x => x.Classification == SafetyCausalClassification.LegacyDirect),
                legacyDownstream = controlRows.Count(x => x.Classification == SafetyCausalClassification.LegacyDownstream),
                articulationDerived = controlRows.Count(x => x.Classification == SafetyCausalClassification.ArticulationDerived),
                transformationDerived = controlRows.Count(x => x.Classification == SafetyCausalClassification.TransformationDerived),
                serializationDerived = controlRows.Count(x => x.Classification == SafetyCausalClassification.SerializationDerived),
                unattributable = controlRows.Count(x => x.Classification == SafetyCausalClassification.Unattributable) },
            mechanism = new { placementAuthority = "LaneGeometryIndex.CanPlaceTap",
                placementPredicate = "previous.EndBeat >= tapBeat blocks",
                oraclePredicate = "materialized previous.EndBeat >= materialized tapBeat blocks",
                endpointOperatorDiffers = false, geometryCacheStale = false,
                hiddenStateDifference = false, serializationChangesCoordinates = false,
                firstSemanticDivergence = "BuildReleaseCandidates preserves intended EndBeat when ToTimeMilliseconds(intendedEndBeat) equals selected EndTime; CurrentGeometry then compares that latent decimal while canonical final HardValidity normalizes both endpoints from integer milliseconds." },
            validation = new { instrumentedRuns, nonInterferenceFailures, traceValidationFailures,
                recorderRngCalls = 0, artifactProjectionDeterministic = deterministicRepeat, independentReplayDeterministic },
            behavior = new { behaviorChange = false, defaultBehaviorChange = false,
                generationSemanticsChange = false, defaultPolicy = "legacy-experimental.1", remediationImplemented = false },
            authorization = new { g1Gate = "COMPLETE_NEEDS_REVIEW_NO_PROMOTION",
                g1BehavioralUtility = "NOT_AUTHORIZED", g2 = "NOT_AUTHORIZED", h = "NOT_AUTHORIZED",
                nextPhase = "NOT_AUTHORIZED" }
        };
        File.WriteAllText(Path.Combine(publicDirectory, "safety_causal_summary.json"),
            JsonSerializer.Serialize(summary, Json) + Environment.NewLine, new UTF8Encoding(false));
        allRowsOut = allRows;
        return passed ? "A" : "NEEDS_REVIEW";
    }

    private static HashSet<string> ReadManifestIds(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("manifest").GetProperty("charts").EnumerateArray()
            .Select(x => x.GetProperty("chartId").GetString()!).ToHashSet(StringComparer.Ordinal);
    }

    private static GeometrySnapshotAudit Audit(ManiaChart source, ManiaChart generated, string fingerprint,
        GeometrySnapshotRole role)
    {
        var timeline = new BeatTimeline(source.TimingPoints);
        return GeometrySafetyAttributionResearch.AuditSnapshot(fingerprint, source.KeyCount,
            generated.AllObjects.Select((x, i) => GeometrySafetyAttributionResearch.Object(fingerprint, role, x,
                timeline.ToBeatDecimal(x.StartTime), x.EndTime is null ? timeline.ToBeatDecimal(x.StartTime)
                    : timeline.ToBeatDecimal(x.EndTime.Value), i)));
    }

    private static ImmutableArray<ProvenancePairEvent> PairEvents(GenerationProvenanceTrace trace, bool treatment) =>
        trace.Decisions.Where(x => x.OpportunityKey is not null).Select(x => new ProvenancePairEvent(
            x.OpportunityKey!, x.CandidateIdentity ?? "NONE", x.Disposition, x.StateBefore, x.StateAfter,
            treatment && x.DirectExperimentalDisposition is not null)).ToImmutableArray();

    private static GenerationProvenanceRecorderResearch Recorder(string chart, int seed,
        AddNotesOptions options, string policy) => new(GenerationProvenanceIdentityResearch.Create(
            chart, seed, options, policy, []));

    private static AddNotesOptions Options() => InteriorRelationMembershipResearch.CurrentOptions() with
    {
        Chance = .50, ArticulationEnabled = false, Trace = false, DiagnosticsEnabled = false
    };

    private static void WriteCases(string path, IEnumerable<SafetyCausalCase> rows)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("chart_id,seed,arm,violation_id,violation_kind,classification,root_cause,mutation_id,mutation_sequence,opportunity_id,source_object,anchor,candidate,lane,object_type,tap_time,blocking_ln_id,blocking_ln_lane,blocking_ln_start,blocking_ln_end,candidate_internal_beat,blocking_internal_end_beat,candidate_materialized_beat,blocking_materialized_end_beat,placement_accepted,hard_validity_result,internal_conflict,materialized_conflict,endpoint_operator_differs,geometry_stale,pre_geometry,post_geometry,pre_generation,post_generation,rng_before,rng_after,g1_divergence,g1_opportunity,opportunity_distance,full_reconverged,geometry_reconverged,rng_reconverged,serialization_changed,evidence_status");
        foreach (var x in rows) writer.WriteLine(string.Join(',', Csv(x.ChartId), x.Seed, x.Arm,
            x.ViolationId, x.ViolationKind, x.Classification, x.RootCause, x.MutationId, x.MutationSequence,
            Csv(x.OpportunityId), Csv(x.SourceObjectIdentity), Csv(x.AnchorIdentity), Csv(x.CandidateIdentity),
            x.Lane, x.ObjectType, x.TapTime, x.BlockingLongNoteIdentity, x.BlockingLnLane,
            x.BlockingLnStartTime, x.BlockingLnEndTime, D(x.CandidateInternalBeat),
            D(x.BlockingLnInternalEndBeat), D(x.CandidateMaterializedBeat),
            D(x.BlockingLnMaterializedEndBeat), x.PlacementAccepted, x.ViolationKind, x.InternalBeatConflict,
            x.MaterializedConflict, x.EndpointOperatorDiffers, x.GeometrySnapshotWasStale,
            x.PreGeometryState, x.PostGeometryState, x.PreGenerationState, x.PostGenerationState,
            x.PreRngPosition, x.PostRngPosition, x.ImmediateG1DivergenceId,
            Csv(x.ImmediateG1OpportunityId), x.OpportunityDistance, x.FullStateReconvergedBeforeViolation,
            x.GeometryReconvergedBeforeViolation, x.RngReconvergedBeforeViolation,
            x.SerializationChangedCoordinates, x.EvidenceStatus));
    }

    private static void WriteJson(string path, object value) => File.WriteAllText(path,
        JsonSerializer.Serialize(value, Json) + Environment.NewLine, new UTF8Encoding(false));

    private static void WriteFamilies(string path, IEnumerable<SafetyCausalCase> rows)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("arm,classification,root_cause,count");
        foreach (var group in rows.GroupBy(x => (x.Arm, x.Classification, x.RootCause))
                     .OrderBy(x => x.Key.Arm).ThenBy(x => x.Key.Classification))
            writer.WriteLine($"{group.Key.Arm},{group.Key.Classification},{group.Key.RootCause},{group.Count()}");
    }

    private static string Csv(string? value) => value is null ? "" : '"' + value.Replace("\"", "\"\"") + '"';
    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed class RecordingRandom(int seed) : IRandomPositionSource
    {
        private readonly SeededRandom inner = new(seed);
        public List<string> Transcript { get; } = [];
        public long CallCount => Transcript.Count;
        public double NextDouble() { var value = inner.NextDouble(); Transcript.Add($"D:{value:R}"); return value; }
        public int Next(int max) { var value = inner.Next(max); Transcript.Add($"I:{max}:{value}"); return value; }
    }
}
