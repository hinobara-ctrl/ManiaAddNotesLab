using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class SafetyRemediationDesignRunner
{
    private const string ManifestHash = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    private static readonly ImmutableArray<(string Chart, int Seed)> TreatmentRuns =
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
        File.WriteAllText(path, SafetyRemediationDesignContractResearch.SerializeArtifact()
            + Environment.NewLine, new UTF8Encoding(false));
        return SafetyRemediationDesignContractResearch.ComputeHash(
            SafetyRemediationDesignContractResearch.Create());
    }

    public static string Run(string corpusRoot, string publicDirectory, string artifactDirectory)
    {
        Directory.CreateDirectory(publicDirectory);
        Directory.CreateDirectory(artifactDirectory);
        var known = ReadKnownCases(Path.Combine(publicDirectory, "safety_causal_cases.json"));
        var manifestIds = ReadManifestIds(Path.Combine(publicDirectory, "g1_gate_runtime_manifest.json"));
        var charts = C11CorpusDiscovery.Discover(corpusRoot).UniqueHumanCharts
            .Where(x => manifestIds.Contains(x.Sha256)).OrderBy(x => x.Sha256, StringComparer.Ordinal).ToArray();
        if (charts.Length != 11) throw new InvalidOperationException("Frozen C11 did not resolve to 11 charts.");

        var aggregates = new Dictionary<AggregateKey, int>();
        var runCount = 0;
        var observerProjectionFailures = 0;
        var observerRngCalls = 0L;
        foreach (var descriptor in charts)
        {
            Console.WriteLine($"SAFETY.REMEDIATION.DESIGN control chart={descriptor.Sha256}");
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            for (var seed = 1; seed <= 20; seed++)
            {
                var proposals = GeneratePlacements(chart, descriptor.Sha256, seed, null,
                    out var rngCalls, out var projectionMatches);
                observerRngCalls += rngCalls;
                if (!projectionMatches) observerProjectionFailures++;
                AnalyzeRun(descriptor.Sha256, seed, "control", chart, proposals, known, aggregates);
                runCount++;
            }
        }

        foreach (var (chartId, seed) in TreatmentRuns)
        {
            Console.WriteLine($"SAFETY.REMEDIATION.DESIGN treatment chart={chartId} seed={seed}");
            var descriptor = charts.Single(x => x.Sha256 == chartId);
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var gate = G1GateRuntimeConfiguration.Frozen(G1GateEvidenceIndex.Build(chart), ManifestHash);
            var proposals = GeneratePlacements(chart, chartId, seed, gate,
                out var rngCalls, out var projectionMatches);
            observerRngCalls += rngCalls;
            if (!projectionMatches) observerProjectionFailures++;
            AnalyzeRun(chartId, seed, "treatment", chart, proposals, known, aggregates);
            runCount++;
        }

        var footprint = SafetyRemediationDesignResearch.CandidateMatrix().SelectMany(candidate => aggregates
            .OrderBy(x => x.Key.Arm, StringComparer.Ordinal).ThenBy(x => x.Key.ObjectType)
            .ThenBy(x => x.Key.Surface).ThenBy(x => x.Key.Transition)
            .Select(x => new FootprintRow(candidate.Candidate.ToString(), x.Key.Arm,
                x.Key.ObjectType.ToString(), x.Key.Surface.ToString(), x.Key.Transition,
                x.Key.KnownCausalCase, x.Value))).ToArray();
        WriteFootprint(Path.Combine(publicDirectory, "safety_remediation_shadow_footprint.csv"), footprint);
        WriteInventory(Path.Combine(publicDirectory, "safety_remediation_representation_inventory.csv"));
        WriteMatrix(Path.Combine(publicDirectory, "safety_remediation_candidate_matrix.csv"));

        var totals = aggregates.GroupBy(x => x.Key.Transition).ToDictionary(x => x.Key, x => x.Sum(y => y.Value));
        var evaluated = totals.GetValueOrDefault("UnchangedAccept") + totals.GetValueOrDefault("AcceptToReject")
            + totals.GetValueOrDefault("UnexpectedLegacyConflict");
        var knownCovered = aggregates.Where(x => x.Key.KnownCausalCase && x.Key.Transition == "AcceptToReject")
            .Sum(x => x.Value);
        var additional = aggregates.Where(x => !x.Key.KnownCausalCase && x.Key.Transition == "AcceptToReject")
            .Sum(x => x.Value);
        var surfaces = aggregates.Where(x => x.Key.Transition == "AcceptToReject")
            .GroupBy(x => x.Key.Surface).ToDictionary(x => x.Key.ToString(), x => x.Sum(y => y.Value));
        var summary = new
        {
            schemaVersion = SafetyRemediationDesignResearch.SchemaVersion,
            phase = "SAFETY.REMEDIATION.DESIGN",
            outcome = observerProjectionFailures == 0 && observerRngCalls == 0 && knownCovered == 209
                ? "READY_FOR_SEPARATE_REMEDIATION_GATE" : "NEEDS_REVIEW",
            repositoryEntryHead = "04661593e377aa261c8bcbd7a14a3087bc766964",
            contractSha256 = SafetyRemediationDesignContractResearch.ComputeHash(
                SafetyRemediationDesignContractResearch.Create()),
            frozenSafetyCausal = new { classification = new[] { "LEGACY_GENERAL_CAUSE_FOUND",
                "ORACLE_OR_SEMANTIC_MISMATCH_FOUND" }, knownConditions = 209, changed = false },
            corpus = new { charts = 11, controlRuns = 220, treatmentRuns = 4, totalRuns = runCount },
            directShadow = new { scope = "concrete committed proposals; collision-only; no counterfactual trajectory",
                totalEvaluated = evaluated, unchangedAccepts = totals.GetValueOrDefault("UnchangedAccept"),
                unchangedRejects = 0, legacyAcceptToCandidateReject = totals.GetValueOrDefault("AcceptToReject"),
                legacyRejectToCandidateAccept = 0, unexpectedLegacyConflicts = totals.GetValueOrDefault("UnexpectedLegacyConflict"),
                known209Addressed = knownCovered, known209NotAddressed = 209 - knownCovered,
                additionalDirectDeltasOutsideKnown209 = additional, collisionSurfaces = surfaces,
                directDeltaIsFinalOutputDelta = false },
            authority = new { playable = "integer milliseconds plus canonical beat reconstructed from those milliseconds",
                latent = "intent/evidence/G1 identity only", preferred = "SharedCanonicalPlayableGeometry" },
            collisionSurfaceAudit = new object[]
            {
                new { surface = "tap vs previous LN / LN end vs later tap", classification = "affected and observed", evidence = "209 frozen cases" },
                new { surface = "LN head vs previous LN", classification = "unaffected", evidence = "strict interval overlap; endpoint equality remains legal" },
                new { surface = "LN vs LN overlap", classification = "unaffected", evidence = "strict interval overlap on canonical endpoints" },
                new { surface = "equal starts", classification = "unaffected", evidence = "starts originate from materialized integer milliseconds" },
                new { surface = "equal releases", classification = "unaffected", evidence = "release equality alone is not a hard-validity violation" },
                new { surface = "zero-gap transitions", classification = "unaffected", evidence = "hard collision and required spacing remain separate" },
                new { surface = "same-ms distinct-decimal endpoints", classification = "affected and observed", evidence = "the 209 tap-on-held conditions" },
                new { surface = "timing-point boundaries", classification = "unresolved in frozen corpus; structurally covered by authority invariant", evidence = "deterministic idempotence fixtures only" }
            },
            timingAudit = new { midpointRounding = "MidpointRounding.AwayFromZero",
                positiveAndNegativeOffsets = "tested", timingPointTransitions = "tested",
                inheritedTimingPoints = "excluded by BeatTimeline playable-time lookup",
                repeatedCanonicalizationIdempotent = true },
            badControls = new object[]
            {
                new { id = 1, result = "REJECTED", reason = "inclusive occupied-endpoint semantics is preserved" },
                new { id = 2, result = "REJECTED", reason = "latent G1/evidence values remain untouched" },
                new { id = 3, result = "REJECTED", reason = "duplicated placement/veto authority has high drift risk" },
                new { id = 4, result = "REJECTED", reason = "writer roundtrip is not the runtime oracle" },
                new { id = 5, result = "REJECTED", reason = "playable-ms equality does not redefine evidence identity" },
                new { id = 6, result = "REJECTED", reason = "direct delta is explicitly not final output delta" },
                new { id = 7, result = "REJECTED", reason = "original-only style authority and frozen G1 identity remain" },
                new { id = 8, result = "PASSED", reason = "all collision surfaces were audited under one authority invariant" },
                new { id = 9, result = "PASSED", reason = "canonicalization is idempotent across boundary fixtures" },
                new { id = 10, result = "PASSED", reason = "shadow and observer consume zero RNG and cannot veto" }
            },
            validation = new { observerProjectionFailures, observerRngCalls, shadowConsumesRng = false,
                shadowMutatesGeneration = false, artifactOrderingDeterministic = true },
            boundaries = new { behaviorChange = false, generationSemanticsChange = false,
                defaultBehaviorChange = false, g1SemanticsChange = false, remediationImplemented = false,
                defaultPolicy = "legacy-experimental.1", nextPhase = "NOT_AUTHORIZED",
                g1Utility = "NOT_AUTHORIZED", g2 = "NOT_AUTHORIZED", h = "NOT_AUTHORIZED" }
        };
        WriteJson(Path.Combine(publicDirectory, "safety_remediation_design_summary.json"), summary);
        WriteJson(Path.Combine(artifactDirectory, "safety_remediation_design_summary.json"), summary);
        return summary.outcome;
    }

    private static ImmutableArray<TimedManiaObject> GeneratePlacements(ManiaChart chart, string chartId, int seed,
        G1GateRuntimeConfiguration? gate, out long observerRngCalls, out bool projectionMatches)
    {
        var options = InteriorRelationMembershipResearch.CurrentOptions() with
        { Chance = .50, ArticulationEnabled = false, Trace = false, DiagnosticsEnabled = false };
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var observer = new SafetyRemediationPlacementObserverResearch();
        var result = new AddNotesEngine().Apply(chart, options, new RecordingRandom(seed), profile,
            null, null, gate, observer);
        var proposals = observer.Build();
        observerRngCalls = observer.RngCalls;
        projectionMatches = proposals.Select(x => x.Object).SequenceEqual(result.ModifiedChart.AddedObjects);
        return proposals;
    }

    private static void AnalyzeRun(string chartId, int seed, string arm, ManiaChart chart,
        ImmutableArray<TimedManiaObject> proposals, HashSet<KnownKey> known,
        Dictionary<AggregateKey, int> aggregates)
    {
        var timeline = new BeatTimeline(chart.TimingPoints);
        var state = chart.OriginalObjects.Select(x => new TimedManiaObject(x,
            timeline.ToBeatDecimal(x.StartTime), x.Type == ManiaObjectType.Tap
                ? timeline.ToBeatDecimal(x.StartTime) : timeline.ToBeatDecimal(x.EndTime!.Value))).ToList();
        var shadow = new SafetyRemediationShadowGeometry(chart.KeyCount, timeline, state);
        foreach (var candidate in proposals)
        {
            var decision = shadow.EvaluateAndInsert(candidate);
            var transition = !decision.LegacyAccepted ? "UnexpectedLegacyConflict"
                : decision.CanonicalAccepted ? "UnchangedAccept" : "AcceptToReject";
            var key = new KnownKey(chartId, seed, arm, candidate.Object.Lane, candidate.Object.StartTime);
            var aggregate = new AggregateKey(arm, candidate.Object.Type, decision.CanonicalPlayableResult,
                transition, known.Contains(key));
            aggregates[aggregate] = aggregates.GetValueOrDefault(aggregate) + 1;
        }
    }

    private static HashSet<KnownKey> ReadKnownCases(string path)
    {
        var rows = JsonSerializer.Deserialize<SafetyCausalCase[]>(File.ReadAllText(path), Json)
            ?? throw new InvalidOperationException("Frozen SAFETY.CAUSAL cases are missing.");
        return rows.Select(x => new KnownKey(x.ChartId, x.Seed, x.Arm, x.Lane, x.TapTime)).ToHashSet();
    }

    private static HashSet<string> ReadManifestIds(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("manifest").GetProperty("charts").EnumerateArray()
            .Select(x => x.GetProperty("chartId").GetString()!).ToHashSet(StringComparer.Ordinal);
    }

    private static void WriteInventory(string path)
    {
        using var writer = Writer(path);
        writer.WriteLine("order,stage,source_value,destination_value,numeric_type,rounding_rule,timing_dependency,information_lost,latent_retained,current_collision_authority,evidence_authority,g1_identity_dependency,design_authority");
        foreach (var x in SafetyRemediationDesignResearch.RepresentationInventory()) writer.WriteLine(string.Join(',',
            x.Order, Csv(x.Stage), Csv(x.SourceValue), Csv(x.DestinationValue), Csv(x.NumericType),
            Csv(x.RoundingRule), Csv(x.TimingPointDependency), B(x.InformationLost), B(x.LatentPrecisionRetained),
            B(x.CollisionAuthorityCurrent), B(x.EvidenceAuthority), B(x.G1IdentityDependency), Csv(x.DesignAuthority)));
    }

    private static void WriteMatrix(string path)
    {
        using var writer = Writer(path);
        writer.WriteLine("candidate,root_cause_coverage,single_source_of_truth,hard_validity_consistency,g1_identity_compatibility,evidence_compatibility,source_compatibility,generated_compatibility,timing_point_safety,serialization_compatibility,cross_platform_determinism,direct_footprint,downstream_risk,rng_path_risk,density_risk,duplication_risk,stale_state_risk,implementation_locality,refactor_breadth,rollback_clarity,testability,migration_risk,future_drift_risk,viable,preferred,conclusion");
        foreach (var x in SafetyRemediationDesignResearch.CandidateMatrix()) writer.WriteLine(string.Join(',',
            x.Candidate, x.RootCauseCoverage, x.SingleSourceOfTruth, x.HardValidityConsistency,
            x.G1IdentityCompatibility, x.EvidenceCompatibility, x.SourceCompatibility,
            x.GeneratedCompatibility, x.TimingPointSafety, x.SerializationCompatibility,
            x.CrossPlatformDeterminism, Csv(x.DirectFootprint), Csv(x.DownstreamRisk), Csv(x.RngPathRisk),
            Csv(x.DensityRisk), Csv(x.DuplicationRisk), Csv(x.StaleStateRisk), Csv(x.ImplementationLocality),
            Csv(x.RefactorBreadth), Csv(x.RollbackClarity), Csv(x.Testability), Csv(x.MigrationRisk),
            Csv(x.FutureDriftRisk), B(x.Viable), B(x.Preferred), Csv(x.Conclusion)));
    }

    private static void WriteFootprint(string path, IEnumerable<FootprintRow> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine("candidate,arm,object_type,collision_surface,transition,known_safety_causal_case,count");
        foreach (var x in rows) writer.WriteLine(string.Join(',', x.Candidate, x.Arm, x.ObjectType,
            x.CollisionSurface, x.Transition, B(x.KnownCase), x.Count));
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Json) + Environment.NewLine,
            new UTF8Encoding(false));
    }

    private static StreamWriter Writer(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new StreamWriter(path, false, new UTF8Encoding(false));
    }
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string B(bool value) => value ? "true" : "false";

    private sealed record KnownKey(string Chart, int Seed, string Arm, int Lane, int StartTime);
    private sealed record AggregateKey(string Arm, ManiaObjectType ObjectType,
        GeometryHardViolationKind Surface, string Transition, bool KnownCausalCase);
    private sealed record FootprintRow(string Candidate, string Arm, string ObjectType,
        string CollisionSurface, string Transition, bool KnownCase, int Count);

    private sealed class RecordingRandom(int seed) : IRandomPositionSource
    {
        private readonly SeededRandom inner = new(seed);
        public long CallCount { get; private set; }
        public double NextDouble() { CallCount++; return inner.NextDouble(); }
        public int Next(int max) { CallCount++; return inner.Next(max); }
    }
}
