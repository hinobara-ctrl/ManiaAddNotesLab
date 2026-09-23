using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class SafetyRemediationGateRunner
{
    private const string ManifestHash = "AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445";
    private static readonly string[] SnapshotFiles =
    [
        "src/ManiaAddNotesLab.Core/AddNotesEngine.cs",
        "src/ManiaAddNotesLab.Core/BeatTimeline.cs",
        "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
        "src/ManiaAddNotesLab.Core/Model.cs"
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

    public static string WriteContract(string path)
    {
        RequireFrozenImplementationSnapshot(Directory.GetCurrentDirectory());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, SafetyRemediationGateContractResearch.SerializeArtifact(), new UTF8Encoding(false));
        return SafetyRemediationGateContractResearch.ComputeHash(SafetyRemediationGateContractResearch.Create());
    }

    public static string Run(string corpusRoot, string publicDirectory, string artifactDirectory)
    {
        RequireFrozenImplementationSnapshot(Directory.GetCurrentDirectory());
        Directory.CreateDirectory(publicDirectory);
        Directory.CreateDirectory(artifactDirectory);
        var contract = SafetyRemediationGateContractResearch.Create();
        if (contract.ImplementationSnapshotSha256 == "IMPLEMENTATION_SNAPSHOT_PENDING")
            throw new InvalidOperationException("Freeze the implementation snapshot before executing the gate.");
        var contractHash = SafetyRemediationGateContractResearch.ComputeHash(contract);
        var known = ReadKnown(Path.Combine(publicDirectory, "safety_causal_cases.json"));
        var manifestIds = ReadManifestIds(Path.Combine(publicDirectory, "g1_gate_runtime_manifest.json"));
        var charts = C11CorpusDiscovery.Discover(corpusRoot).UniqueHumanCharts
            .Where(x => manifestIds.Contains(x.Sha256)).OrderBy(x => x.Sha256, StringComparer.Ordinal).ToArray();
        if (charts.Length != 11) throw new InvalidOperationException("Frozen C11 did not resolve to 11 charts.");

        var rows = new List<RunRow>();
        var caseRows = new List<KnownCaseRow>();
        foreach (var descriptor in charts)
        {
            Console.WriteLine($"SAFETY.REMEDIATION.GATE primary chart={descriptor.Sha256}");
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            for (var seed = 1; seed <= 20; seed++)
                EvaluatePair("primary", "control", descriptor.Sha256, seed, chart, null,
                    verifyDefault: true, known, rows, caseRows);
        }
        foreach (var (chartId, seed) in SecondaryRuns)
        {
            Console.WriteLine($"SAFETY.REMEDIATION.GATE secondary chart={chartId} seed={seed}");
            var descriptor = charts.Single(x => x.Sha256 == chartId);
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var g1 = G1GateRuntimeConfiguration.Frozen(G1GateEvidenceIndex.Build(chart), ManifestHash);
            EvaluatePair("secondary-g1-compatibility", "treatment", chartId, seed, chart, g1,
                verifyDefault: false, known, rows, caseRows);
        }

        WriteRuns(Path.Combine(publicDirectory, "safety_remediation_gate_runs.csv"), rows);
        WriteCases(Path.Combine(publicDirectory, "safety_remediation_gate_known_cases.csv"), caseRows);
        var treatmentViolations = rows.Sum(x => x.TreatmentIntroducedHardViolations);
        var unexpected = caseRows.Count(x => x.ExplanationClass == "C_UNEXPECTED");
        var knownCovered = caseRows.Count(x => x.FrozenKnown);
        var extraCovered = caseRows.Count(x => !x.FrozenKnown);
        var summary = new
        {
            schemaVersion = "safety-remediation-gate-runtime.1",
            phase = "SAFETY.REMEDIATION.GATE",
            repositoryEntryHead = contract.RepositoryEntryHead,
            contractSha256 = contractHash,
            implementationSnapshotSha256 = contract.ImplementationSnapshotSha256,
            corpus = new { charts = 11, primaryPairs = rows.Count(x => x.Stratum == "primary"),
                secondaryPairs = rows.Count(x => x.Stratum != "primary"), totalPairs = rows.Count,
                seeds = "1-20 primary; frozen 4 secondary" },
            runtime = new
            {
                controlAddedObjects = rows.Sum(x => x.ControlAddedObjects),
                treatmentAddedObjects = rows.Sum(x => x.TreatmentAddedObjects),
                addedObjectDelta = rows.Sum(x => x.TreatmentAddedObjects - x.ControlAddedObjects),
                controlHardViolations = rows.Sum(x => x.ControlIntroducedHardViolations),
                treatmentHardViolations = treatmentViolations,
                treatmentOnlyHardViolations = rows.Sum(x => x.TreatmentOnlyHardViolations),
                controlOnlyHardViolations = rows.Sum(x => x.ControlOnlyHardViolations),
                pairsWithOutputDifference = rows.Count(x => x.ControlOutputSha256 != x.TreatmentOutputSha256),
                pairsWithRngPositionDifference = rows.Count(x => x.ControlRngCalls != x.TreatmentRngCalls),
                pairsWithFullStateReconvergence = rows.Count(x => x.FirstFullStateReconvergenceOpportunity is not null),
                gateRngCalls = rows.Sum(x => x.ControlGateRngCalls + x.TreatmentGateRngCalls)
            },
            frozenCases = new
            {
                expectedKnown = 209, expectedAdditional = 6, observedKnown = knownCovered,
                observedAdditional = extraCovered,
                reachedAndRejected = caseRows.Count(x => x.ExplanationClass == "A_REACHED_CANONICAL_REJECT"),
                causallyUnreachable = caseRows.Count(x => x.ExplanationClass == "B_EARLIER_GOVERNED_DIVERGENCE"),
                unexpected
            },
            validation = new
            {
                defaultEquivalenceFailures = rows.Count(x => !x.DefaultReferenceExact),
                deterministicRerunFailures = rows.Count(x => !x.TreatmentDeterministic),
                serializationReparseFailures = rows.Count(x => !x.ControlReparseExact || !x.TreatmentReparseExact),
                g1EvidenceHashFailures = rows.Count(x => !x.G1EvidenceInvariant),
                contractFrozenBeforeFullRun = true,
                hardValiditySemanticsChanged = false,
                latentG1IdentityChanged = false,
                productDefaultChanged = false
            },
            outcome = treatmentViolations == 0 && unexpected == 0 && knownCovered == 209 && extraCovered == 6
                && rows.All(x => x.DefaultReferenceExact && x.TreatmentDeterministic
                    && x.ControlReparseExact && x.TreatmentReparseExact && x.G1EvidenceInvariant
                    && x.ControlGateRngCalls == 0 && x.TreatmentGateRngCalls == 0)
                    ? "REMEDIATION_RUNTIME_CERTIFIED" : "NEEDS_REVIEW",
            boundaries = new { g1Promotion = "NOT_AUTHORIZED", g1Utility = "NOT_AUTHORIZED",
                defaults = "legacy-experimental.1", cliWebExposure = false, g2 = "NOT_AUTHORIZED",
                h = "NOT_AUTHORIZED", successor = "NOT_AUTHORIZED" }
        };
        WriteJson(Path.Combine(publicDirectory, "safety_remediation_gate_summary.json"), summary);
        WriteJson(Path.Combine(artifactDirectory, "safety_remediation_gate_summary.json"), summary);
        return summary.outcome;
    }

    private static void RequireFrozenImplementationSnapshot(string repositoryRoot)
    {
        var actual = ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(SnapshotFiles.Select(path =>
            new NamedImplementationContent(path, File.ReadAllBytes(Path.Combine(repositoryRoot,
                path.Replace('/', Path.DirectorySeparatorChar))))));
        if (!string.Equals(actual, SafetyRemediationGateContractResearch.ImplementationSnapshotSha256,
                StringComparison.Ordinal))
            throw new InvalidOperationException($"Implementation snapshot mismatch: expected " +
                $"{SafetyRemediationGateContractResearch.ImplementationSnapshotSha256}, actual {actual}.");
    }

    private static void EvaluatePair(string stratum, string frozenArm, string chartId, int seed,
        ManiaChart chart, G1GateRuntimeConfiguration? g1, bool verifyDefault, HashSet<KnownKey> known,
        List<RunRow> rows, List<KnownCaseRow> caseRows)
    {
        var options = InteriorRelationMembershipResearch.CurrentOptions() with
            { Chance = .50, ArticulationEnabled = false, Trace = false, DiagnosticsEnabled = false };
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var control = Execute(chart, options, profile, seed, g1, false);
        var treatment = Execute(chart, options, profile, seed, g1, true);
        var repeat = Execute(chart, options, profile, seed, g1, true);
        var defaultExact = true;
        if (verifyDefault)
        {
            var random = new TranscriptRandom(seed);
            var reference = new AddNotesEngine().Apply(chart, options, random, profile);
            defaultExact = OsuBeatmap.Write(reference.ModifiedChart, options.Chance) == control.Serialized
                && random.Hash == control.RngHash && random.CallCount == control.RngCalls;
        }
        var treatmentDeterministic = treatment.Serialized == repeat.Serialized
            && treatment.RngHash == repeat.RngHash && treatment.RngCalls == repeat.RngCalls
            && JsonSerializer.Serialize(treatment.Diagnostics, Json) == JsonSerializer.Serialize(repeat.Diagnostics, Json);
        var controlViolations = control.IntroducedViolationSignatures.ToHashSet(StringComparer.Ordinal);
        var treatmentViolations = treatment.IntroducedViolationSignatures.ToHashSet(StringComparer.Ordinal);
        var firstGoverned = control.Diagnostics.CandidateDecisions.FirstOrDefault(x => x.GeometrySetsDiffer)?.OpportunityKey;
        var reconvergence = FirstReconvergence(control.Diagnostics, treatment.Diagnostics, firstGoverned);
        var g1Invariant = control.G1EvidenceHashBefore == control.G1EvidenceHashAfter
            && treatment.G1EvidenceHashBefore == treatment.G1EvidenceHashAfter
            && control.G1EvidenceHashBefore == treatment.G1EvidenceHashBefore;
        rows.Add(new(stratum, chartId, seed, control.Added, treatment.Added,
            control.AddedTaps, treatment.AddedTaps, control.AddedLns, treatment.AddedLns,
            control.OutputHash, treatment.OutputHash, controlViolations.Count, treatmentViolations.Count,
            treatmentViolations.Except(controlViolations).Count(), controlViolations.Except(treatmentViolations).Count(),
            control.Diagnostics.GeometrySetDifferences, treatment.Diagnostics.GeometrySetDifferences,
            control.Diagnostics.SelectedLegacyAcceptCanonicalReject,
            control.RngCalls, treatment.RngCalls, control.RngHash, treatment.RngHash,
            firstGoverned, reconvergence, control.ReparseExact, treatment.ReparseExact,
            defaultExact, treatmentDeterministic, g1Invariant,
            control.Diagnostics.GateRngCalls, treatment.Diagnostics.GateRngCalls));

        foreach (var decision in control.Diagnostics.CandidateDecisions.Where(x =>
                     x.SelectedLane is not null && x.LegacyAcceptsSelectedLane && !x.CanonicalAcceptsSelectedLane))
        {
            var key = new KnownKey(chartId, seed, frozenArm, decision.SelectedLane!.Value, decision.StartTime);
            var isKnown = known.Contains(key);
            var treatmentMatch = treatment.Diagnostics.CandidateDecisions.FirstOrDefault(x =>
                x.OpportunityKey == decision.OpportunityKey && x.ObjectType == decision.ObjectType
                && x.StartTime == decision.StartTime && x.EndTime == decision.EndTime);
            string classification;
            string evidence;
            if (treatmentMatch is not null && !treatmentMatch.CanonicalLegalLanes.Contains(decision.SelectedLane.Value))
            {
                classification = "A_REACHED_CANONICAL_REJECT";
                evidence = "Equivalent opportunity/candidate reached; frozen legacy lane absent from canonical legal lanes.";
            }
            else if (firstGoverned is not null && OpportunityOrder(firstGoverned) < OpportunityOrder(decision.OpportunityKey))
            {
                classification = "B_EARLIER_GOVERNED_DIVERGENCE";
                evidence = $"Earlier governed divergence {firstGoverned}; later frozen proposal is not required to remain reachable.";
            }
            else
            {
                classification = "C_UNEXPECTED";
                evidence = "No equivalent canonical rejection or earlier governed ancestry was established.";
            }
            caseRows.Add(new(stratum, chartId, seed, frozenArm, decision.OpportunityKey,
                decision.SelectedLane.Value, decision.StartTime, decision.EndTime, isKnown,
                classification, evidence, firstGoverned));
        }
    }

    private static Execution Execute(ManiaChart chart, AddNotesOptions options, MapperEvidenceProfile profile,
        int seed, G1GateRuntimeConfiguration? g1, bool treatment)
    {
        var random = new TranscriptRandom(seed);
        var result = new AddNotesEngine().Apply(chart, options, random, profile, null, null, g1, null,
            SafetyRemediationGateRuntimeConfiguration.Frozen(treatment));
        var serialized = OsuBeatmap.Write(result.ModifiedChart, options.Chance);
        var reparsed = OsuBeatmap.Parse(serialized);
        var reparseExact = result.ModifiedChart.AllObjects.Select(Materialized).Order(StringComparer.Ordinal)
            .SequenceEqual(reparsed.OriginalObjects.Select(Materialized).Order(StringComparer.Ordinal),
                StringComparer.Ordinal);
        var sourceSignatures = Audit(chart, chart).ToHashSet(StringComparer.Ordinal);
        var finalSignatures = Audit(chart, reparsed).ToHashSet(StringComparer.Ordinal);
        var introduced = finalSignatures.Except(sourceSignatures).Order(StringComparer.Ordinal).ToImmutableArray();
        return new(serialized, Hash(serialized), random.CallCount, random.Hash,
            result.ModifiedChart.AddedObjects.Count,
            result.ModifiedChart.AddedObjects.Count(x => x.Type == ManiaObjectType.Tap),
            result.ModifiedChart.AddedObjects.Count(x => x.Type == ManiaObjectType.LongNote),
            reparseExact, introduced,
            result.SafetyRemediationGateDiagnostics!,
            result.G1GateDiagnostics?.EvidenceIndexHashBefore,
            result.G1GateDiagnostics?.EvidenceIndexHashAfter);
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

    private static string? FirstReconvergence(SafetyRemediationGateRuntimeDiagnostics control,
        SafetyRemediationGateRuntimeDiagnostics treatment, string? first)
    {
        if (first is null) return null;
        var left = control.CandidateDecisions.GroupBy(x => x.OpportunityKey).ToDictionary(x => x.Key, x => x.First());
        var right = treatment.CandidateDecisions.GroupBy(x => x.OpportunityKey).ToDictionary(x => x.Key, x => x.First());
        foreach (var key in left.Keys.Intersect(right.Keys).OrderBy(OpportunityOrder))
        {
            if (OpportunityOrder(key) <= OpportunityOrder(first)) continue;
            if (left[key].MaterializedGeometryStateHash == right[key].MaterializedGeometryStateHash
                && left[key].RngPositionBeforeDecision == right[key].RngPositionBeforeDecision) return key;
        }
        return null;
    }

    private static int OpportunityOrder(string value)
    {
        var parts = value.Split('-');
        return parts.Length > 1 && int.TryParse(parts[1], out var order) ? order : int.MaxValue;
    }

    private static HashSet<KnownKey> ReadKnown(string path) =>
        (JsonSerializer.Deserialize<SafetyCausalCase[]>(File.ReadAllText(path), Json) ?? [])
        .Select(x => new KnownKey(x.ChartId, x.Seed, x.Arm, x.Lane, x.TapTime)).ToHashSet();

    private static HashSet<string> ReadManifestIds(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("manifest").GetProperty("charts").EnumerateArray()
            .Select(x => x.GetProperty("chartId").GetString()!).ToHashSet(StringComparer.Ordinal);
    }

    private static void WriteRuns(string path, IEnumerable<RunRow> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine("stratum,chart_id,seed,control_added,treatment_added,control_taps,treatment_taps,control_lns,treatment_lns,control_output_sha256,treatment_output_sha256,control_hard_violations,treatment_hard_violations,treatment_only_violations,control_only_violations,control_geometry_set_differences,treatment_geometry_set_differences,frozen_selected_lane_rejects,control_rng_calls,treatment_rng_calls,control_rng_sha256,treatment_rng_sha256,first_governed_divergence,first_full_state_reconvergence,control_reparse_exact,treatment_reparse_exact,default_reference_exact,treatment_deterministic,g1_evidence_invariant,control_gate_rng_calls,treatment_gate_rng_calls");
        foreach (var x in rows) writer.WriteLine(string.Join(',', x.Stratum, x.ChartId, x.Seed,
            x.ControlAddedObjects, x.TreatmentAddedObjects, x.ControlAddedTaps, x.TreatmentAddedTaps,
            x.ControlAddedLns, x.TreatmentAddedLns, x.ControlOutputSha256, x.TreatmentOutputSha256,
            x.ControlIntroducedHardViolations, x.TreatmentIntroducedHardViolations,
            x.TreatmentOnlyHardViolations, x.ControlOnlyHardViolations,
            x.ControlGeometrySetDifferences, x.TreatmentGeometrySetDifferences, x.FrozenSelectedLaneRejects,
            x.ControlRngCalls, x.TreatmentRngCalls, x.ControlRngSha256, x.TreatmentRngSha256,
            Csv(x.FirstGovernedDivergenceOpportunity ?? ""), Csv(x.FirstFullStateReconvergenceOpportunity ?? ""),
            B(x.ControlReparseExact), B(x.TreatmentReparseExact), B(x.DefaultReferenceExact),
            B(x.TreatmentDeterministic), B(x.G1EvidenceInvariant), x.ControlGateRngCalls, x.TreatmentGateRngCalls));
    }

    private static void WriteCases(string path, IEnumerable<KnownCaseRow> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine("stratum,chart_id,seed,frozen_arm,opportunity_key,lane,start_time,end_time,frozen_known,explanation_class,evidence,first_governed_divergence");
        foreach (var x in rows.OrderBy(x => x.Stratum).ThenBy(x => x.ChartId).ThenBy(x => x.Seed)
                     .ThenBy(x => OpportunityOrder(x.OpportunityKey)))
            writer.WriteLine(string.Join(',', x.Stratum, x.ChartId, x.Seed, x.FrozenArm,
                Csv(x.OpportunityKey), x.Lane, x.StartTime, x.EndTime?.ToString() ?? "",
                B(x.FrozenKnown), x.ExplanationClass, Csv(x.Evidence), Csv(x.FirstGovernedDivergence ?? "")));
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Json) + Environment.NewLine, new UTF8Encoding(false));
    }
    private static StreamWriter Writer(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new StreamWriter(path, false, new UTF8Encoding(false));
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Materialized(ManiaObject value) =>
        $"{value.Lane}|{value.StartTime}|{value.EndTime}|{value.Type}";
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string B(bool value) => value ? "true" : "false";

    private sealed record Execution(string Serialized, string OutputHash, long RngCalls, string RngHash,
        int Added, int AddedTaps, int AddedLns, bool ReparseExact,
        ImmutableArray<string> IntroducedViolationSignatures,
        SafetyRemediationGateRuntimeDiagnostics Diagnostics,
        string? G1EvidenceHashBefore, string? G1EvidenceHashAfter);
    private sealed record KnownKey(string Chart, int Seed, string Arm, int Lane, int StartTime);
    private sealed record KnownCaseRow(string Stratum, string ChartId, int Seed, string FrozenArm,
        string OpportunityKey, int Lane, int StartTime, int? EndTime, bool FrozenKnown,
        string ExplanationClass, string Evidence, string? FirstGovernedDivergence);
    private sealed record RunRow(string Stratum, string ChartId, int Seed,
        int ControlAddedObjects, int TreatmentAddedObjects, int ControlAddedTaps, int TreatmentAddedTaps,
        int ControlAddedLns, int TreatmentAddedLns, string ControlOutputSha256, string TreatmentOutputSha256,
        int ControlIntroducedHardViolations, int TreatmentIntroducedHardViolations,
        int TreatmentOnlyHardViolations, int ControlOnlyHardViolations,
        int ControlGeometrySetDifferences, int TreatmentGeometrySetDifferences, int FrozenSelectedLaneRejects,
        long ControlRngCalls, long TreatmentRngCalls, string ControlRngSha256, string TreatmentRngSha256,
        string? FirstGovernedDivergenceOpportunity, string? FirstFullStateReconvergenceOpportunity,
        bool ControlReparseExact, bool TreatmentReparseExact, bool DefaultReferenceExact,
        bool TreatmentDeterministic, bool G1EvidenceInvariant, int ControlGateRngCalls, int TreatmentGateRngCalls);

    private sealed class TranscriptRandom(int seed) : IRandomPositionSource
    {
        private readonly SeededRandom inner = new(seed);
        private readonly StringBuilder transcript = new();
        public long CallCount { get; private set; }
        public string Hash => SafetyRemediationGateRunner.Hash(transcript.ToString());
        public double NextDouble()
        {
            var value = inner.NextDouble(); CallCount++; transcript.Append("D:")
                .Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            return value;
        }
        public int Next(int maximum)
        {
            var value = inner.Next(maximum); CallCount++; transcript.Append("I:").Append(maximum).Append(':').Append(value).Append('\n');
            return value;
        }
    }
}
