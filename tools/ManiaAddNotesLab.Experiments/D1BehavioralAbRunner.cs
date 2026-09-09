using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class D1BehavioralAbRunner
{
    private const string ManifestSchema = "phase-d1-behavioral-ab-manifest.1";
    private static readonly JsonSerializerOptions CanonicalJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static readonly JsonSerializerOptions ArtifactJson = new(CanonicalJson) { WriteIndented = true };

    public static string Prepare(string corpusRoot, string manifestPath)
    {
        var discovery = FrozenCorpus(corpusRoot);
        var charts = discovery.UniqueHumanCharts.Select(x => new D1ManifestChart(x.Sha256, x.FamilyKey,
                x.KeyCount, x.ObjectCount, x.TapCount, x.LongNoteCount))
            .OrderBy(x => x.ChartId, StringComparer.Ordinal).ToImmutableArray();
        var corpusHash = Hash(JsonSerializer.SerializeToUtf8Bytes(charts, CanonicalJson));
        var manifest = new D1BehavioralAbManifest(ManifestSchema,
            D1BehavioralExperimentContractResearch.FrozenContractContentHash, corpusHash, charts,
            Enumerable.Range(1, 20).ToImmutableArray(), FrozenOptions(),
            "FullChart; range filters opportunity source heads only; evidence remains chart-global original-only",
            MapperEvidenceProfileBuilder.BehaviorPolicyVersion, D1BehavioralPolicyVersions.Treatment,
            charts.Length * 20, "Explicit deterministic replication set; not a random population sample",
            true, false, "D1EligibleDecisions");
        var hash = ManifestHash(manifest);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new D1ManifestArtifact(manifest, hash,
            "UTF-8 System.Text.Json camelCase; no indentation for hash; fixed record order; charts sorted by chartId; seeds ascending; no timestamp or runtime path"), ArtifactJson), new UTF8Encoding(false));
        return hash;
    }

    public static void Run(string corpusRoot, string manifestPath, string publicOutputDirectory,
        string localOutputDirectory)
    {
        var artifact = JsonSerializer.Deserialize<D1ManifestArtifact>(File.ReadAllText(manifestPath), CanonicalJson)
            ?? throw new InvalidDataException("D1 run manifest is empty.");
        if (artifact.Manifest.ContractContentHash !=
            D1BehavioralExperimentContractResearch.FrozenContractContentHash)
            throw new InvalidOperationException("D1.GATE contract hash mismatch; human A/B NOT RUN.");
        if (artifact.ContentHash != ManifestHash(artifact.Manifest))
            throw new InvalidOperationException("D1 run manifest hash mismatch; human A/B NOT RUN.");
        var discovery = FrozenCorpus(corpusRoot);
        VerifyCorpus(artifact.Manifest, discovery);
        var synthetic = SyntheticGate();
        if (synthetic.Any(x => !x.Passed))
            throw new InvalidOperationException("D1 synthetic gate failed; human A/B NOT RUN.");

        var first = RunOnce(discovery, artifact, Path.Combine(localOutputDirectory, "first"));
        var second = RunOnce(discovery, artifact, Path.Combine(localOutputDirectory, "repeat"));
        var firstSemantic = SerializeSemantic(first);
        var secondSemantic = SerializeSemantic(second);
        var deterministic = firstSemantic == secondSemantic
            && first.All(x => second.Single(y => y.ChartId == x.ChartId && y.Seed == x.Seed)
                .ControlOutputHash == x.ControlOutputHash
                && second.Single(y => y.ChartId == x.ChartId && y.Seed == x.Seed)
                    .TreatmentOutputHash == x.TreatmentOutputHash);
        if (!deterministic) throw new InvalidOperationException("D1 deterministic repeat failed.");
        if (first.Any(x => !x.ContractSafe))
            throw new InvalidOperationException("D1 hard abort condition observed.");

        Directory.CreateDirectory(publicOutputDirectory);
        WriteSynthetic(Path.Combine(publicOutputDirectory, "d1_synthetic_gate_summary.csv"), synthetic);
        WriteRuns(Path.Combine(publicOutputDirectory, "d1_run_summary.csv"), first);
        WriteDirect(Path.Combine(publicOutputDirectory, "d1_direct_decision_summary.csv"), first);
        WriteScope(Path.Combine(publicOutputDirectory, "d1_chart_summary.csv"), first, x => x.ChartId);
        WriteScope(Path.Combine(publicOutputDirectory, "d1_family_summary.csv"), first, x => x.FamilyKey);
        WriteScope(Path.Combine(publicOutputDirectory, "d1_keymode_summary.csv"), first,
            x => x.KeyCount.ToString(CultureInfo.InvariantCulture));
        WritePairTypes(Path.Combine(publicOutputDirectory, "d1_pair_type_summary.csv"), first);
        WriteRng(Path.Combine(publicOutputDirectory, "d1_rng_attribution_summary.csv"), first);
        WriteDownstream(Path.Combine(publicOutputDirectory, "d1_downstream_effect_summary.csv"), first);
        WriteK3(Path.Combine(publicOutputDirectory, "d1_k3_downstream_summary.csv"), first);
        WriteDeltas(Path.Combine(publicOutputDirectory, "d1_output_delta_summary.csv"), first);
        WriteSafety(Path.Combine(publicOutputDirectory, "d1_safety_summary.csv"), first);
        WriteDeterminism(Path.Combine(publicOutputDirectory, "d1_determinism_summary.csv"), artifact.ContentHash,
            firstSemantic, secondSemantic, deterministic, first.Count);
        File.WriteAllText(Path.Combine(localOutputDirectory, "d1_detail.json"),
            JsonSerializer.Serialize(first.Select(x => x.Detail), ArtifactJson), new UTF8Encoding(false));
        Console.WriteLine($"D1 A/B: pairs={first.Count} eligible={first.Sum(x => x.Eligible)} " +
            $"admit={first.Sum(x => x.Admissions)} abstain={first.Sum(x => x.Rejections)} " +
            $"manifest={artifact.ContentHash}");
    }

    public static string ManifestHash(D1BehavioralAbManifest manifest) =>
        Hash(JsonSerializer.SerializeToUtf8Bytes(manifest, CanonicalJson));

    private static List<D1RunRow> RunOnce(C11CorpusDiscoveryResult discovery, D1ManifestArtifact artifact,
        string outputRoot)
    {
        var rows = new List<D1RunRow>();
        foreach (var descriptor in discovery.UniqueHumanCharts.OrderBy(x => x.Sha256, StringComparer.Ordinal))
        {
            var sourceBytes = File.ReadAllBytes(descriptor.RuntimePath);
            var sourceHash = Hash(sourceBytes);
            var chart = OsuBeatmap.Parse(Encoding.UTF8.GetString(sourceBytes));
            var profile = MapperEvidenceProfileBuilder.Build(chart);
            var index = D1OriginalEvidenceIndex.Build(chart);
            foreach (var seed in artifact.Manifest.Seeds)
            {
                var controlRandom = new RecordingRandom(seed);
                var treatmentRandom = new RecordingRandom(seed);
                var control = new AddNotesEngine().Apply(chart, artifact.Manifest.Options, controlRandom, profile);
                var treatment = new AddNotesEngine().Apply(chart, artifact.Manifest.Options, treatmentRandom, profile,
                    D1BehavioralExperimentConfiguration.Frozen(index, artifact.ContentHash));
                var controlText = OsuBeatmap.Write(control.ModifiedChart, artifact.Manifest.Options.Chance);
                var treatmentText = OsuBeatmap.Write(treatment.ModifiedChart, artifact.Manifest.Options.Chance);
                var controlHash = Hash(Encoding.UTF8.GetBytes(controlText));
                var treatmentHash = Hash(Encoding.UTF8.GetBytes(treatmentText));
                var diagnostics = treatment.D1BehavioralDiagnostics
                    ?? throw new InvalidOperationException("Treatment produced no D1 diagnostics.");
                var firstReject = diagnostics.DirectDecisions.FirstOrDefault(x => !x.OutputMutationPerformed);
                var firstRngDifference = FirstDifference(controlRandom.Transcript, treatmentRandom.Transcript);
                var prefixMatch = firstReject is null
                    ? controlRandom.Transcript.SequenceEqual(treatmentRandom.Transcript)
                    : controlRandom.Transcript.Take((int)firstReject.RngPositionBeforeGate)
                        .SequenceEqual(treatmentRandom.Transcript.Take((int)firstReject.RngPositionBeforeGate));
                var outputDifference = SymmetricDifference(control.ModifiedChart.AddedObjects,
                    treatment.ModifiedChart.AddedObjects);
                var directDifference = diagnostics.DirectDecisions.Count(x => !x.OutputMutationPerformed);
                var downstream = Math.Max(0, outputDifference - directDifference);
                var sourceUnchanged = Hash(File.ReadAllBytes(descriptor.RuntimePath)) == sourceHash;
                var controlReparse = Reparse(controlText);
                var treatmentReparse = Reparse(treatmentText);
                var overlapFailures = CountOverlaps(treatment.ModifiedChart.AllObjects);
                var detail = new D1RunDetail(descriptor.Sha256, artifact.ContentHash, seed,
                    diagnostics.EvidenceIndexHashBefore, diagnostics.EvidenceIndexHashAfter,
                    diagnostics.EvidenceIndexBuildOperations, diagnostics.DirectDecisions);
                var row = new D1RunRow(descriptor.Sha256, descriptor.FamilyKey, descriptor.KeyCount, seed,
                    chart.OriginalObjects.Count, control.Statistics.TotalOpportunities,
                    diagnostics.DirectDecisions.Length, diagnostics.DirectGateAdmissions,
                    diagnostics.DirectGateRejections,
                    diagnostics.DirectDecisions.Count(x => x.EvidenceState == FutureD1EvidenceState.JointObservedUnique),
                    diagnostics.DirectDecisions.Count(x => x.EvidenceState == FutureD1EvidenceState.JointObservedAmongAlternatives),
                    diagnostics.DirectDecisions.Count(x => x.EvidenceState == FutureD1EvidenceState.MarginalOnly),
                    diagnostics.DirectDecisions.Count(x => x.EvidenceState == FutureD1EvidenceState.NoComparableCompositionContext),
                    diagnostics.DirectDecisions.Count(x => x.EvidenceState == FutureD1EvidenceState.NotMarginallySupported),
                    control.Statistics.PlacedObjects, treatment.Statistics.PlacedObjects,
                    control.Statistics.AddedLongNotes, treatment.Statistics.AddedLongNotes,
                    controlRandom.CallCount, treatmentRandom.CallCount, diagnostics.GateRngCalls,
                    firstReject?.OpportunityKey ?? "NoDivergence", firstReject?.RngPositionBeforeGate,
                    firstRngDifference, firstRngDifference is null || firstReject is not null
                        && firstRngDifference >= firstReject.RngPositionBeforeGate,
                    prefixMatch, directDifference, downstream, outputDifference,
                    CountK3(control.ModifiedChart.AddedObjects), CountK3(treatment.ModifiedChart.AddedObjects),
                    diagnostics.LaterK3PlusAfterDirectD1Count, controlHash, treatmentHash,
                    controlReparse, treatmentReparse, overlapFailures, sourceUnchanged,
                    diagnostics.EvidenceIndexHashBefore == diagnostics.EvidenceIndexHashAfter, detail);
                rows.Add(row);
                WriteOutput(outputRoot, "control", descriptor.Sha256, seed, controlText);
                WriteOutput(outputRoot, "treatment", descriptor.Sha256, seed, treatmentText);
            }
        }
        return rows;
    }

    private static ImmutableArray<D1SyntheticGateRow> SyntheticGate()
    {
        var joint = D1BehavioralExperimentContractResearch.Evaluate(new(true, true, 1,
            new(2, OriginalHeadMemberType.TapHead), FutureD1EvidenceState.JointObservedUnique));
        var alternative = D1BehavioralExperimentContractResearch.Evaluate(new(true, true, 1,
            new(2, OriginalHeadMemberType.TapHead), FutureD1EvidenceState.JointObservedAmongAlternatives));
        var marginal = D1BehavioralExperimentContractResearch.Evaluate(new(true, true, 1,
            new(2, OriginalHeadMemberType.TapHead), FutureD1EvidenceState.MarginalOnly));
        return [
            new("contract_hash", D1BehavioralExperimentContractResearch.ComputeContentHash(
                D1BehavioralExperimentContractResearch.CreateFrozenContract()) ==
                D1BehavioralExperimentContractResearch.FrozenContractContentHash, "frozen"),
            new("joint_unique_admit", joint.Disposition == FutureD1ExperimentalDisposition.ExperimentAdmittable, joint.Disposition.ToString()),
            new("joint_alternative_admit", alternative.Disposition == FutureD1ExperimentalDisposition.ExperimentAdmittable, alternative.Disposition.ToString()),
            new("marginal_abstain", marginal.Disposition == FutureD1ExperimentalDisposition.ExperimentAbstainMarginalOnly, marginal.Disposition.ToString()),
            new("gate_rng_zero", joint.GateRngCalls == 0 && marginal.GateRngCalls == 0, "0"),
            new("no_retry", !marginal.RetryPermitted, "false"),
            new("default_legacy", MapperEvidenceProfileBuilder.BehaviorPolicyVersion == "legacy-experimental.1", MapperEvidenceProfileBuilder.BehaviorPolicyVersion),
            new("treatment_version", D1BehavioralPolicyVersions.Treatment == "d1-resulting-state-ab.1", D1BehavioralPolicyVersions.Treatment)
        ];
    }

    private static C11CorpusDiscoveryResult FrozenCorpus(string root)
    {
        var discovery = C11CorpusDiscovery.Discover(root);
        if (discovery.UniqueHumanCharts.Length != 11
            || discovery.UniqueHumanCharts.Select(x => x.FamilyKey).Distinct(StringComparer.Ordinal).Count() != 11
            || !discovery.UniqueHumanCharts.Select(x => x.KeyCount).Distinct().Order().SequenceEqual([4, 7, 10]))
            throw new InvalidOperationException("Frozen C11 identity must be 11 charts / 11 families / 4K,7K,10K.");
        return discovery;
    }

    private static void VerifyCorpus(D1BehavioralAbManifest manifest, C11CorpusDiscoveryResult discovery)
    {
        var actual = discovery.UniqueHumanCharts.Select(x => new D1ManifestChart(x.Sha256, x.FamilyKey,
                x.KeyCount, x.ObjectCount, x.TapCount, x.LongNoteCount))
            .OrderBy(x => x.ChartId, StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(manifest.Charts))
            throw new InvalidOperationException("Frozen C11 charts differ from the run manifest.");
        if (Hash(JsonSerializer.SerializeToUtf8Bytes(actual, CanonicalJson)) != manifest.CorpusContentHash)
            throw new InvalidOperationException("Frozen C11 corpus hash differs from the run manifest.");
    }

    private static AddNotesOptions FrozenOptions() => new()
    {
        Chance = .50,
        ContextualDensityNormalizationEnabled = true,
        ContextualDensityScaleMode = ContextualDensityScaleMode.KeymodeRelative,
        VerticalDensityMode = VerticalDensityMode.SimultaneousHeads,
        VerticalDensityScaleMode = VerticalDensityScaleMode.KeymodeRelative,
        InteriorLnOpportunitiesEnabled = true,
        MaxInteriorOpportunitiesPerSource = 2,
        InteriorMinimumSourceBeats = 3,
        InteriorMinimumContextLnCount = 3,
        InteriorMinimumSupportedAnchors = 2,
        ArticulationEnabled = true,
        ArticulationMaxNonHeldColumns = 1,
        RetriggerGapMinimumSupport = 2,
        RetriggerGapWindowBeats = 4,
        Trace = false,
        DiagnosticsEnabled = false
    };

    private static string SerializeSemantic(IEnumerable<D1RunRow> rows) => JsonSerializer.Serialize(rows
        .Select(x => x with { Detail = null }).OrderBy(x => x.ChartId, StringComparer.Ordinal).ThenBy(x => x.Seed), CanonicalJson);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static bool Reparse(string text) { try { _ = OsuBeatmap.Parse(text); return true; } catch { return false; } }
    private static long? FirstDifference(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var count = Math.Min(left.Count, right.Count);
        for (var i = 0; i < count; i++) if (left[i] != right[i]) return i;
        return left.Count == right.Count ? null : count;
    }
    private static int SymmetricDifference(IEnumerable<ManiaObject> left, IEnumerable<ManiaObject> right)
    {
        static string Key(ManiaObject x) => $"{x.Lane}:{x.StartTime}:{x.EndTime}:{x.Type}";
        var a = left.Select(Key).ToHashSet(StringComparer.Ordinal);
        var b = right.Select(Key).ToHashSet(StringComparer.Ordinal);
        return a.Except(b).Count() + b.Except(a).Count();
    }
    private static int CountK3(IEnumerable<ManiaObject> objects) => objects.GroupBy(x => x.StartTime).Count(x => x.Count() >= 3);
    private static int CountOverlaps(IEnumerable<ManiaObject> objects)
    {
        var failures = 0;
        foreach (var lane in objects.GroupBy(x => x.Lane))
        {
            var ordered = lane.OrderBy(x => x.StartTime).ThenBy(x => x.EndTime ?? x.StartTime).ToArray();
            for (var i = 0; i < ordered.Length; i++)
            for (var j = i + 1; j < ordered.Length && ordered[j].StartTime <= (ordered[i].EndTime ?? ordered[i].StartTime); j++)
                failures++;
        }
        return failures;
    }
    private static void WriteOutput(string root, string arm, string chart, int seed, string text)
    {
        var directory = Path.Combine(root, arm);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{chart}-seed-{seed:D3}.osu"), text, new UTF8Encoding(false));
    }
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static void WriteSynthetic(string path, IEnumerable<D1SyntheticGateRow> rows)
    { using var w = Writer(path); w.WriteLine("case,passed,measured"); foreach (var x in rows) w.WriteLine($"{Csv(x.Case)},{x.Passed.ToString().ToLowerInvariant()},{Csv(x.Measured)}"); }
    private static void WriteRuns(string path, IEnumerable<D1RunRow> rows)
    { using var w = Writer(path); w.WriteLine("chart_id,family,keymode,seed,original_objects,legacy_opportunities,d1_eligible,admissions,rejections,control_added,treatment_added,control_ln,treatment_ln,first_divergence,control_hash,treatment_hash"); foreach (var x in rows) w.WriteLine(string.Join(',', x.ChartId, Csv(x.FamilyKey), x.KeyCount, x.Seed, x.OriginalObjects, x.LegacyOpportunities, x.Eligible, x.Admissions, x.Rejections, x.ControlAdded, x.TreatmentAdded, x.ControlLn, x.TreatmentLn, Csv(x.FirstDivergence), x.ControlOutputHash, x.TreatmentOutputHash)); }
    private static void WriteDirect(string path, IEnumerable<D1RunRow> rows)
    { using var w = Writer(path); w.WriteLine("evidence_state,decisions"); foreach (var g in rows.SelectMany(x => x.Detail!.Decisions).GroupBy(x => x.EvidenceState).OrderBy(x => x.Key)) w.WriteLine($"{g.Key},{g.Count()}"); }
    private static void WriteScope(string path, IEnumerable<D1RunRow> rows, Func<D1RunRow,string> key)
    { using var w = Writer(path); w.WriteLine("scope,runs,zero_eligible_runs,eligible,admissions,rejections,direct_changed,control_added,treatment_added"); foreach (var g in rows.GroupBy(key).OrderBy(x => x.Key, StringComparer.Ordinal)) w.WriteLine(string.Join(',', Csv(g.Key), g.Count(), g.Count(x => x.Eligible == 0), g.Sum(x => x.Eligible), g.Sum(x => x.Admissions), g.Sum(x => x.Rejections), g.Sum(x => x.DirectChanged), g.Sum(x => x.ControlAdded), g.Sum(x => x.TreatmentAdded))); }
    private static void WritePairTypes(string path, IEnumerable<D1RunRow> rows)
    { using var w = Writer(path); w.WriteLine("pair_type,eligible,admitted,abstained,direct_changed"); foreach (var g in rows.SelectMany(x => x.Detail!.Decisions).GroupBy(x => x.PairType).OrderBy(x => x.Key)) w.WriteLine($"{g.Key},{g.Count()},{g.Count(x => x.OutputMutationPerformed)},{g.Count(x => !x.OutputMutationPerformed)},{g.Count(x => !x.OutputMutationPerformed)}"); }
    private static void WriteRng(string path, IReadOnlyList<D1RunRow> rows)
    { using var w = Writer(path); w.WriteLine("runs,gate_rng_calls,pre_divergence_mismatch_runs,rng_difference_before_behavior_runs,runs_with_downstream_rng_difference"); w.WriteLine($"{rows.Count},{rows.Sum(x => x.GateRngCalls)},{rows.Count(x => !x.PreDivergenceRngMatch)},{rows.Count(x => !x.RngDifferenceAfterBehavior)},{rows.Count(x => x.FirstRngDifference.HasValue)}"); }
    private static void WriteDownstream(string path, IReadOnlyList<D1RunRow> rows)
    { using var w = Writer(path); w.WriteLine("direct_changed_opportunities,downstream_changed_objects,total_output_symmetric_difference,runs_with_direct_divergence,no_divergence_runs"); w.WriteLine($"{rows.Sum(x => x.DirectChanged)},{rows.Sum(x => x.DownstreamChanged)},{rows.Sum(x => x.OutputSymmetricDifference)},{rows.Count(x => x.DirectChanged > 0)},{rows.Count(x => x.DirectChanged == 0)}"); }
    private static void WriteK3(string path, IReadOnlyList<D1RunRow> rows)
    { using var w = Writer(path); w.WriteLine("control_k3_timestamps,treatment_k3_timestamps,directly_affected_later_k3"); w.WriteLine($"{rows.Sum(x => x.ControlK3)},{rows.Sum(x => x.TreatmentK3)},{rows.Sum(x => x.LaterK3AfterDirect)}"); }
    private static void WriteDeltas(string path, IReadOnlyList<D1RunRow> rows)
    { using var w = Writer(path); w.WriteLine("metric,control,treatment,delta"); w.WriteLine($"added_objects,{rows.Sum(x => x.ControlAdded)},{rows.Sum(x => x.TreatmentAdded)},{rows.Sum(x => x.TreatmentAdded - x.ControlAdded)}"); w.WriteLine($"added_ln,{rows.Sum(x => x.ControlLn)},{rows.Sum(x => x.TreatmentLn)},{rows.Sum(x => x.TreatmentLn - x.ControlLn)}"); }
    private static void WriteSafety(string path, IReadOnlyList<D1RunRow> rows)
    { using var w = Writer(path); w.WriteLine("runs,control_reparse_failures,treatment_reparse_failures,overlap_failures,source_changed,evidence_index_changed,contract_safe"); w.WriteLine($"{rows.Count},{rows.Count(x => !x.ControlReparse)},{rows.Count(x => !x.TreatmentReparse)},{rows.Sum(x => x.OverlapFailures)},{rows.Count(x => !x.SourceUnchanged)},{rows.Count(x => !x.IndexUnchanged)},{rows.All(x => x.ContractSafe).ToString().ToLowerInvariant()}"); }
    private static void WriteDeterminism(string path, string manifestHash, string first, string second, bool pass, int pairs)
    { using var w = Writer(path); w.WriteLine("manifest_hash,paired_runs,first_semantic_hash,repeat_semantic_hash,byte_identical"); w.WriteLine($"{manifestHash},{pairs},{Hash(Encoding.UTF8.GetBytes(first))},{Hash(Encoding.UTF8.GetBytes(second))},{pass.ToString().ToLowerInvariant()}"); }

    private sealed class RecordingRandom(int seed) : IRandomPositionSource
    {
        private readonly SeededRandom inner = new(seed);
        public List<string> Transcript { get; } = [];
        public long CallCount => Transcript.Count;
        public double NextDouble() { var value = inner.NextDouble(); Transcript.Add($"D:{value:R}"); return value; }
        public int Next(int maximum) { var value = inner.Next(maximum); Transcript.Add($"I:{maximum}:{value}"); return value; }
    }
}

internal sealed record D1ManifestChart(string ChartId, string FamilyId, int KeyCount, int ObjectCount,
    int TapCount, int LongNoteCount);
internal sealed record D1BehavioralAbManifest(string SchemaVersion, string ContractContentHash,
    string CorpusContentHash, ImmutableArray<D1ManifestChart> Charts, ImmutableArray<int> Seeds,
    AddNotesOptions Options, string RangeSemantics, string ControlPolicyVersion, string TreatmentPolicyVersion,
    int ExpectedPairedRunCount, string CreationSemantics, bool BehaviorChange, bool DefaultPromotion,
    string PrimaryDenominator);
internal sealed record D1ManifestArtifact(D1BehavioralAbManifest Manifest, string ContentHash,
    string Canonicalization);
internal sealed record D1SyntheticGateRow(string Case, bool Passed, string Measured);
internal sealed record D1RunDetail(string ChartFingerprint, string RunManifestHash, int Seed,
    string EvidenceIndexHashBefore, string EvidenceIndexHashAfter, long EvidenceIndexBuildOperations,
    ImmutableArray<D1DirectDecision> Decisions);
internal sealed record D1RunRow(string ChartId, string FamilyKey, int KeyCount, int Seed,
    int OriginalObjects, int LegacyOpportunities, int Eligible, int Admissions, int Rejections,
    int JointUnique, int JointAlternative, int MarginalOnly, int NoComparable, int NotObserved,
    int ControlAdded, int TreatmentAdded, int ControlLn, int TreatmentLn, long ControlRngCalls,
    long TreatmentRngCalls, int GateRngCalls, string FirstDivergence, long? RngAtFirstDivergence,
    long? FirstRngDifference, bool RngDifferenceAfterBehavior, bool PreDivergenceRngMatch,
    int DirectChanged, int DownstreamChanged, int OutputSymmetricDifference, int ControlK3,
    int TreatmentK3, int LaterK3AfterDirect, string ControlOutputHash, string TreatmentOutputHash,
    bool ControlReparse, bool TreatmentReparse, int OverlapFailures, bool SourceUnchanged,
    bool IndexUnchanged, D1RunDetail? Detail)
{
    public bool ContractSafe => GateRngCalls == 0 && PreDivergenceRngMatch && RngDifferenceAfterBehavior
        && ControlReparse && TreatmentReparse && OverlapFailures == 0 && SourceUnchanged && IndexUnchanged;
}
