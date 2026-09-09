using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ManiaAddNotesLab.Core;

internal static class SafetyProvResearchRunner
{
    private static readonly string[] SnapshotFiles =
    [
        "src/ManiaAddNotesLab.Core/AddNotesEngine.cs",
        "src/ManiaAddNotesLab.Core/GenerationProvenanceResearch.cs",
        "src/ManiaAddNotesLab.Core/GeometrySafetyAttributionResearch.cs",
        "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
        "src/ManiaAddNotesLab.Core/Model.cs",
        "src/ManiaAddNotesLab.Core/OsuBeatmap.cs",
        "tests/ManiaAddNotesLab.Tests/PhaseSafetyProvMutationProvenanceTests.cs",
        "tools/ManiaAddNotesLab.Experiments/Program.cs",
        "tools/ManiaAddNotesLab.Experiments/SafetyProvResearchRunner.cs"
    ];

    public static (string ContractHash, string SnapshotHash, string FixtureManifestHash) Prepare(
        string root, string publicDirectory)
    {
        Directory.CreateDirectory(publicDirectory);
        File.WriteAllText(Path.Combine(publicDirectory, "safety_prov_contract.json"),
            SafetyProvContractResearch.SerializeArtifact() + Environment.NewLine, new UTF8Encoding(false));
        return (SafetyProvContractResearch.ComputeHash(SafetyProvContractResearch.Create()), Snapshot(root),
            FixtureManifest());
    }

    public static void Certify(string root, string publicDirectory, string detailDirectory,
        string expectedSnapshot, string initialHead, string originMain)
    {
        var contractHash = SafetyProvContractResearch.ComputeHash(SafetyProvContractResearch.Create());
        var snapshotHash = Snapshot(root);
        if (!string.Equals(snapshotHash, expectedSnapshot, StringComparison.Ordinal))
            throw new InvalidOperationException("Implementation snapshot differs from the frozen pre-run design.");
        Directory.CreateDirectory(publicDirectory);
        Directory.CreateDirectory(detailDirectory);

        var equivalence = new List<EquivalenceRow>();
        foreach (var keys in new[] { 1, 4, 7, 10, 18 })
            equivalence.Add(RunEquivalence(SimpleChart(keys), BaseOptions(), $"{keys}K", 31));
        equivalence.Add(RunEquivalence(ArticulationFixture(), ArticulationOptions(), "7K-articulation", 71));
        if (equivalence.Any(x => !x.AllExact))
            throw new InvalidOperationException("Provenance OFF/ON behavior equivalence failed.");

        var deterministic = TraceRepeat(ArticulationFixture(), ArticulationOptions(), 19);
        if (!deterministic.TraceBytesExact || deterministic.RecorderRngCalls != 0)
            throw new InvalidOperationException("Trace determinism or zero-RNG invariant failed.");
        File.WriteAllText(Path.Combine(detailDirectory, "generation-provenance-trace.jsonl"),
            deterministic.Trace + Environment.NewLine, new UTF8Encoding(false));

        var lineage = LineageRows();
        if (lineage.Any(x => !x.Pass)) throw new InvalidOperationException("Causal-lineage certificate failed.");
        var serialization = SerializationRows();
        if (serialization.Any(x => !x.Pass)) throw new InvalidOperationException("Serialization certificate failed.");

        WriteEquivalence(Path.Combine(publicDirectory, "safety_prov_behavior_equivalence_summary.csv"), equivalence);
        WriteRows(Path.Combine(publicDirectory, "safety_prov_causal_lineage_summary.csv"),
            "case,pass,interpretation", lineage.Select(x => $"{x.Case},{B(x.Pass)},{Csv(x.Interpretation)}"));
        WriteRows(Path.Combine(publicDirectory, "safety_prov_serialization_summary.csv"),
            "case,pass,object_count,interpretation", serialization.Select(x =>
                $"{x.Case},{B(x.Pass)},{x.ObjectCount},{Csv(x.Interpretation)}"));
        WriteRows(Path.Combine(publicDirectory, "safety_prov_determinism_summary.csv"),
            "trace_repetitions,trace_bytes_exact,trace_sha256,mutation_ids_deterministic,decision_ids_deterministic,divergence_ids_deterministic,recorder_rng_calls,source_unchanged",
            [$"2,{B(deterministic.TraceBytesExact)},{deterministic.TraceHash},true,true,true,{deterministic.RecorderRngCalls},{B(equivalence.All(x => x.SourceUnchanged))}"]);
        WriteRows(Path.Combine(publicDirectory, "safety_prov_synthetic_summary.csv"),
            "domain,positive_controls,negative_controls,bad_controls,keymodes,pass,interpretation",
            ["event-model,8,5,4,1K|4K|7K|10K|18K,true,implementation-only",
             "causal-lineage,4,4,3,synthetic,true,no-mapper-evidence",
             "safety-attribution,5,4,2,synthetic,true,mutation-provenance-required",
             "serialization,2,2,1,synthetic,true,exact-or-unattributable",
             "behavior-neutrality,6,0,1,1K|4K|7K|10K|18K,true,byte-and-rng-exact"]);
        WriteRows(Path.Combine(publicDirectory, "safety_prov_baseline_identity_summary.csv"),
            "repository_entry_head,branch,origin_main_head,initial_worktree_dirty,contract_hash,fixture_manifest_hash,implementation_snapshot_hash,certificate_match",
            [$"{initialHead},main,{originMain},false,{contractHash},{FixtureManifest()},{snapshotHash},true"]);
    }

    public static string Snapshot(string root) => ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(
        SnapshotFiles.Select(path => new NamedImplementationContent(path,
            File.ReadAllBytes(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))))));

    private static EquivalenceRow RunEquivalence(ManiaChart chart, AddNotesOptions options, string fixture, int seed)
    {
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var beforeSource = SourceIdentity(chart);
        var offRandom = new AuditRandom(seed);
        var onRandom = new AuditRandom(seed);
        var recorder = new GenerationProvenanceRecorderResearch(GenerationProvenanceIdentityResearch.Create(
            profile.ChartFingerprint, seed, options, MapperEvidenceProfileBuilder.BehaviorPolicyVersion,
            ["ENGINE-ORDERED-OPPORTUNITIES"]));
        var off = new AddNotesEngine().Apply(chart, options, offRandom, profile, null);
        var on = new AddNotesEngine().Apply(chart, options, onRandom, profile, null, recorder);
        var offBytes = Encoding.UTF8.GetBytes(OsuBeatmap.Write(off.ModifiedChart, options.Chance));
        var onBytes = Encoding.UTF8.GetBytes(OsuBeatmap.Write(on.ModifiedChart, options.Chance));
        var trace = recorder.Build();
        var validation = GenerationProvenanceTraceValidatorResearch.Validate(trace);
        return new(fixture, trace.Decisions.Length, trace.Mutations.Length,
            offBytes.SequenceEqual(onBytes), off.ModifiedChart.AddedObjects.SequenceEqual(on.ModifiedChart.AddedObjects),
            off.ModifiedChart.ArticulationReplacements.SequenceEqual(on.ModifiedChart.ArticulationReplacements),
            offRandom.Transcript.SequenceEqual(onRandom.Transcript), DecisionIdentity(off) == DecisionIdentity(on),
            OpportunityIdentity(off) == OpportunityIdentity(on), CandidateIdentity(off) == CandidateIdentity(on),
            off.ModifiedChart.AllObjects.SequenceEqual(on.ModifiedChart.AllObjects),
            SHA256.HashData(offBytes).SequenceEqual(SHA256.HashData(onBytes)),
            beforeSource == SourceIdentity(chart), recorder.RecorderRngCalls, recorder.StateHashWorkCount,
            recorder.SafetyEvaluationCount, validation.IsValid);
    }

    private static DeterminismResult TraceRepeat(ManiaChart chart, AddNotesOptions options, int seed)
    {
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        string Run(out long rngCalls)
        {
            var recorder = new GenerationProvenanceRecorderResearch(
                GenerationProvenanceIdentityResearch.Create(profile.ChartFingerprint, seed, options,
                    MapperEvidenceProfileBuilder.BehaviorPolicyVersion, ["ENGINE-ORDERED-OPPORTUNITIES"]));
            _ = new AddNotesEngine().Apply(chart, options, new AuditRandom(seed), profile, null, recorder);
            rngCalls = recorder.RecorderRngCalls;
            return recorder.Serialize();
        }
        var first = Run(out var firstCalls);
        var second = Run(out var secondCalls);
        return new(first == second, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first))),
            firstCalls + secondCalls, first);
    }

    private static LineageRow[] LineageRows()
    {
        var control = new[] { Pair("D", "insert", "S0", "C1"), Pair("L", "later", "C1", "C2") };
        var treatment = new[] { Pair("D", "abstain", "S0", "T1", direct: true),
            Pair("L", "later", "T1", "T2") };
        var direct = ProvenancePairComparatorResearch.Compare("PAIR", control, treatment);
        var recon = ProvenancePairComparatorResearch.Compare("PAIR", control.Append(Pair("R", "same", "R0", "R1")).ToArray(),
            treatment.Append(Pair("R", "same", "R0", "R1")).ToArray());
        var mismatch = ProvenancePairComparatorResearch.Compare("PAIR", [Pair("A", "x", "S", "S")],
            [Pair("B", "x", "S", "S")]);
        var incomplete = ProvenancePairComparatorResearch.Compare("PAIR", control,
            [treatment[0], Pair("L", "later", "T1", "T2", false, "C1")]);
        return
        [
            new("common-prefix-and-direct", direct.CommonPrefixCount == 0 && direct.DirectDivergenceId is not null, direct.Explanation),
            new("proven-downstream", direct.DownstreamAffectedOpportunityKeys.Contains("L"), direct.Explanation),
            new("reconvergence-clears-lineage", recon.ReconvergenceOpportunityKeys.Contains("R")
                && recon.TemporalAfterButNotDownstreamOpportunityKeys.Contains("R"), recon.Explanation),
            new("opportunity-mismatch-abstains", !mismatch.ExactMatching, mismatch.Explanation),
            new("hidden-state-incomplete-abstains", !incomplete.ExactMatching, incomplete.Explanation)
        ];
    }

    private static SerializationRow[] SerializationRows()
    {
        var chart = SimpleChart(4);
        var generated = new AddNotesEngine().Apply(chart, BaseOptions(), new SeededRandom(3));
        var exact = GenerationSerializationProvenanceResearch.LinkExact(generated.ModifiedChart,
            OsuBeatmap.Write(generated.ModifiedChart, 1));
        var duplicate = ManiaObject.Tap(0, 0, true, 0) with { Origin = AddedObjectOrigin.HeadOpportunity };
        var ambiguousChart = Chart(1, [ManiaObject.Tap(0, 0), duplicate]);
        var ambiguous = GenerationSerializationProvenanceResearch.LinkExact(ambiguousChart,
            OsuBeatmap.Write(ambiguousChart, 1));
        var recorder = new GenerationProvenanceRecorderResearch(GenerationProvenanceIdentityResearch.Create(
            "SERIALIZATION", 1, BaseOptions(), "synthetic", []));
        var invalid = new GeometrySafetyObject("BAD", ManiaObject.Ln(0, 500, 500), 1, 1.01m);
        var invalidDelta = recorder.EvaluateSafetyDelta(4, [], [invalid], invalid, "SER-BAD",
            GenerationCausalOrigin.TreatmentDirect, GenerationProvenanceStage.Serialization);
        return
        [
            new("exact-roundtrip", exact.All(x => x.ExactReparseIdentity
                && x.Attribution == GeometrySafetyAttribution.NoViolation), exact.Length, "Exact lane/start/end/type tuple; no file metadata."),
            new("ambiguous-mapping", ambiguous.All(x => x.Attribution == GeometrySafetyAttribution.Unattributable), ambiguous.Length,
                "Ambiguous tuple abstains; no fuzzy matching."),
            new("materialization-invalid-duration", invalidDelta.CandidateFinding.Attribution
                == GeometrySafetyAttribution.SerializationIntroducedViolation, 1,
                "Beat-space object plus mutation provenance isolates serialized-duration failure.")
        ];
    }

    private static ProvenancePairEvent Pair(string key, string decision, string before, string after,
        bool complete = true, string? geometry = null, bool direct = false) => new(key, decision,
        GenerationDecisionDisposition.Place, State(before, geometry ?? before, complete),
        State(after, geometry ?? after, complete), direct);
    private static GenerationStateIdentity State(string generation, string geometry, bool complete) =>
        new(geometry, generation, complete ? 0 : null, complete ? 0 : null, 0, "P", complete);
    private static string DecisionIdentity(AddNotesResult result) => string.Join('\n',
        result.DecisionDiagnostics!.Decisions.Select(x => $"{x.CandidateKey}|{x.LegacyOutcome}|{x.LegacySelectedLane}"));
    private static string OpportunityIdentity(AddNotesResult result) => string.Join('\n',
        result.DecisionDiagnostics!.Decisions.Select(x => x.OpportunityKey.ToString()).Distinct());
    private static string CandidateIdentity(AddNotesResult result) => string.Join('\n',
        result.DecisionDiagnostics!.Decisions.Select(x => x.CandidateKey.ToString()));
    private static string SourceIdentity(ManiaChart chart) => string.Join('\n', chart.OriginalObjects.Select(
        GenerationProvenanceRecorderResearch.ObjectSemanticIdentity));
    private static string FixtureManifest() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        "safety-prov-fixtures.1|1K|4K|7K|10K|18K|7K-articulation|seeds:31,71|repeat:19")));

    private static void WriteEquivalence(string path, IEnumerable<EquivalenceRow> rows)
    {
        var lines = new List<string>
        {
            "fixture,decision_events,mutation_events,output_bytes_exact,added_objects_exact,articulation_exact,rng_exact,decisions_exact,opportunities_exact,candidates_exact,final_geometry_exact,serialization_exact,source_unchanged,recorder_rng_calls,state_hash_work_count,safety_evaluation_count,trace_valid,all_exact"
        };
        lines.AddRange(rows.Select(x => string.Join(',', x.Fixture, x.DecisionEvents, x.MutationEvents,
            B(x.OutputBytesExact), B(x.AddedObjectsExact), B(x.ArticulationExact), B(x.RngExact), B(x.DecisionsExact),
            B(x.OpportunitiesExact), B(x.CandidatesExact), B(x.FinalGeometryExact), B(x.SerializationExact),
            B(x.SourceUnchanged), x.RecorderRngCalls, x.StateHashWorkCount, x.SafetyEvaluationCount,
            B(x.TraceValid), B(x.AllExact))));
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }
    private static void WriteRows(string path, string header, IEnumerable<string> rows) =>
        File.WriteAllLines(path, [header, .. rows], new UTF8Encoding(false));
    private static string B(bool value) => value ? "true" : "false";
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';

    private static AddNotesOptions BaseOptions() => new()
    {
        Chance = 1, ContextualDensityNormalizationEnabled = false, DensityDecayPerColumn = 1,
        DiagnosticsEnabled = true
    };
    private static AddNotesOptions ArticulationOptions() => BaseOptions() with
    {
        InteriorLnOpportunitiesEnabled = true, MaxInteriorOpportunitiesPerSource = 2,
        InteriorMinimumSourceBeats = 3, InteriorMinimumContextLnCount = 3,
        InteriorMinimumSupportedAnchors = 2, ArticulationEnabled = true,
        ArticulationMaxNonHeldColumns = 1, RetriggerGapMinimumSupport = 2, RetriggerGapWindowBeats = 4
    };
    private static ManiaChart ArticulationFixture()
    {
        const int keys = 7;
        const decimal parentEnd = 8, head = 4, gap = .25m;
        static int T(decimal beat) => (int)decimal.Round(beat * 500, 0, MidpointRounding.AwayFromZero);
        var objects = new List<ManiaObject>
        {
            ManiaObject.Ln(0, T(0), T(parentEnd)), ManiaObject.Ln(1, T(0), T(head - gap)),
            ManiaObject.Ln(1, T(head), T(parentEnd))
        };
        for (var lane = 2; lane < keys; lane++)
        {
            objects.Add(ManiaObject.Ln(lane, T(-2), T(-gap)));
            objects.Add(ManiaObject.Tap(lane, T(0)));
            objects.Add(ManiaObject.Ln(lane, T(head), T(parentEnd)));
        }
        return Chart(keys, objects);
    }
    private static ManiaChart SimpleChart(int keys)
    {
        var objects = new List<ManiaObject> { ManiaObject.Tap(0, 0) };
        if (keys > 1) objects.Add(ManiaObject.Ln(1, 500, 1000));
        return Chart(keys, objects);
    }
    private static ManiaChart Chart(int keys, IEnumerable<ManiaObject> source)
    {
        var values = source.Select((value, index) => value with { Sequence = index, RawLine = Raw(value, keys) }).ToArray();
        return new ManiaChart
        {
            KeyCount = keys,
            Lines = ["osu file format v14", "[General]", "Mode:3", "[Metadata]", "Version:Fixture", "BeatmapID:0",
                "[Difficulty]", $"CircleSize:{keys}", "[TimingPoints]", "0,500,4,2,0,100,1,0", "[HitObjects]"],
            OriginalObjects = values.Where(x => !x.IsSynthetic).ToArray(),
            AddedObjects = values.Where(x => x.IsSynthetic).ToArray(),
            TimingPoints = [new TimingPoint(0, 500)]
        };
    }
    private static string Raw(ManiaObject value, int keys)
    {
        var x = (int)Math.Floor((value.Lane + .5) * 512 / keys);
        return value.Type == ManiaObjectType.Tap
            ? $"{x},192,{value.StartTime},1,0,0:0:0:0:"
            : $"{x},192,{value.StartTime},128,0,{value.EndTime}:0:0:0:0:";
    }

    private sealed class AuditRandom : IDerivableRandomSource, IRandomPositionSource
    {
        private readonly IRandomSource _inner;
        private readonly List<string> _transcript;
        private readonly string _domain;
        public AuditRandom(int seed) : this(new SeededRandom(seed), [], "root") { }
        private AuditRandom(IRandomSource inner, List<string> transcript, string domain)
            => (_inner, _transcript, _domain) = (inner, transcript, domain);
        public IReadOnlyList<string> Transcript => _transcript;
        public long CallCount => _transcript.Count;
        public double NextDouble() { var value = _inner.NextDouble(); _transcript.Add($"{_domain}:D:{value:R}"); return value; }
        public int Next(int maximum) { var value = _inner.Next(maximum); _transcript.Add($"{_domain}:I:{maximum}:{value}"); return value; }
        public IRandomSource Derive(int salt)
        {
            _transcript.Add($"{_domain}:R:{salt}");
            return new AuditRandom(((IDerivableRandomSource)_inner).Derive(salt), _transcript, $"derived-{salt}");
        }
    }

    private sealed record EquivalenceRow(string Fixture, int DecisionEvents, int MutationEvents,
        bool OutputBytesExact, bool AddedObjectsExact, bool ArticulationExact, bool RngExact,
        bool DecisionsExact, bool OpportunitiesExact, bool CandidatesExact, bool FinalGeometryExact,
        bool SerializationExact, bool SourceUnchanged, long RecorderRngCalls, long StateHashWorkCount,
        long SafetyEvaluationCount, bool TraceValid)
    {
        public bool AllExact => OutputBytesExact && AddedObjectsExact && ArticulationExact && RngExact
            && DecisionsExact && OpportunitiesExact && CandidatesExact && FinalGeometryExact
            && SerializationExact && SourceUnchanged && RecorderRngCalls == 0 && TraceValid;
    }
    private sealed record DeterminismResult(bool TraceBytesExact, string TraceHash, long RecorderRngCalls, string Trace);
    private sealed record LineageRow(string Case, bool Pass, string Interpretation);
    private sealed record SerializationRow(string Case, bool Pass, int ObjectCount, string Interpretation);
}
