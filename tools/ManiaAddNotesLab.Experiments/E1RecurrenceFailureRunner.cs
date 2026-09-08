using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class E1RecurrenceFailureRunner
{
    public static void Run(string corpusRoot, string publicOutputDirectory, string localDetailJson)
    {
        Directory.CreateDirectory(publicOutputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(localDetailJson)!);
        var synthetic = SyntheticGate();
        WriteSynthetic(Path.Combine(publicOutputDirectory, "e1_synthetic_gate_summary.csv"), synthetic);
        if (synthetic.Any(x => !x.Pass))
            throw new InvalidDataException("E.1 synthetic gate failed; human corpus was not run.");

        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        var phaseE = discovery.UniqueHumanCharts.OrderBy(x => x.RelativePath, StringComparer.Ordinal)
            .Select(x => AdaptiveContextResearch.Evaluate(OsuBeatmap.Parse(File.ReadAllText(x.RuntimePath)), false))
            .ToArray();
        var reproduction = ReproducePhaseE(discovery, phaseE);
        WriteReproduction(Path.Combine(publicOutputDirectory, "e1_phase_e_reproduction.csv"), reproduction);
        if (reproduction.Any(x => !x.Match))
            throw new InvalidDataException("E.1 Phase E reproduction failed; human diagnostics were not run.");

        var charts = new List<ChartData>();
        using var stream = File.Create(localDetailJson);
        using var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        json.WriteString("researchSchemaVersion", ExactRecurrenceFailureResearch.ResearchSchemaVersion);
        json.WriteBoolean("phaseEReproduced", true);
        json.WriteBoolean("syntheticGatePassed", true);
        json.WriteNumber("uniqueHumanCharts", discovery.UniqueHumanCharts.Length);
        json.WritePropertyName("charts");
        json.WriteStartArray();
        foreach (var descriptor in discovery.UniqueHumanCharts.OrderBy(x => x.RelativePath, StringComparer.Ordinal))
        {
            var result = ExactRecurrenceFailureResearch.Evaluate(
                OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath)));
            var data = new ChartData(descriptor.FamilyKey, descriptor.RelativePath, descriptor.Version,
                descriptor.Sha256, descriptor.KeyCount, result);
            charts.Add(data);
            JsonSerializer.Serialize(json, data, JsonOptions());
            json.Flush();
            Console.WriteLine($"{descriptor.RelativePath}: targets={result.Diagnostics.Length}, " +
                $"mismatch={result.Diagnostics.Count(x => x.Disposition == AdaptiveReconstructionState.Mismatch)}, " +
                $"no-context={result.Diagnostics.Count(x => x.Disposition == AdaptiveReconstructionState.NoContext)}");
        }
        json.WriteEndArray();
        json.WriteEndObject();
        json.Flush();

        WriteRelationSemantics(Path.Combine(publicOutputDirectory, "e1_relation_semantics_summary.csv"), charts);
        WriteChartSummary(Path.Combine(publicOutputDirectory, "e1_chart_summary.csv"), charts);
        WriteFamilySummary(Path.Combine(publicOutputDirectory, "e1_family_summary.csv"), charts);
        WriteProperties(Path.Combine(publicOutputDirectory, "e1_disposition_property_summary.csv"), charts);
        WriteNoContext(Path.Combine(publicOutputDirectory, "e1_no_context_breakdown.csv"), charts);
        WriteMismatch(Path.Combine(publicOutputDirectory, "e1_mismatch_breakdown.csv"), charts);
        WriteDonors(Path.Combine(publicOutputDirectory, "e1_donor_multiplicity_summary.csv"), charts);
        WriteCounterfactuals(Path.Combine(publicOutputDirectory, "e1_counterfactual_refinement_summary.csv"), charts);
        WriteSpacing(Path.Combine(publicOutputDirectory, "e1_exact_spacing_summary.csv"), charts);
        WriteLn(Path.Combine(publicOutputDirectory, "e1_ln_stratification_summary.csv"), charts);
        WriteLeakage(Path.Combine(publicOutputDirectory, "e1_leakage_summary.csv"), charts);
        Console.WriteLine($"E.1 wrote {charts.Count} human charts; detail={stream.Length} bytes; gates PASS.");
    }

    private static IReadOnlyList<GateRow> SyntheticGate()
    {
        var rows = new List<GateRow>();
        Check("reconstructed", D(Series("P", "X", "N", "Q", "P", "X", "N"), 1),
            x => x.Disposition == AdaptiveReconstructionState.Reconstructed, rows);
        Check("ambiguous", D(Series("P", "T", "N", "Q", "P", "A", "N", "R", "P", "B", "N"), 1),
            x => x.Disposition == AdaptiveReconstructionState.Ambiguous, rows);
        Check("mismatch", D(Series("P", "X", "N", "Q", "P", "Y", "N"), 1),
            x => x.Disposition == AdaptiveReconstructionState.Mismatch, rows);
        Check("no_context_self_only", D(Series("P", "X", "N", "Q"), 1),
            x => x.PrimaryNoContextReason == ExactNoContextReason.NoExactNeighborPairElsewhere, rows);
        var touching = Series("P", "X", "N", "Q", "P", "Y", "N");
        touching = touching with { Events = touching.Events.SetItem(4, touching.Events[4] with
            { HeadObservationIds = touching.Events[1].HeadObservationIds }) };
        Check("sanitized_target_touching", D(touching, 1),
            x => x.PrimaryNoContextReason == ExactNoContextReason.OnlyExcludedOccurrences
                && x.HasEndpointOrGroupCollision && x.LeakageCount == 0, rows);
        var spacing = Series([0, 100, 200, 300, 500, 700, 900], "P", "X", "N", "Q", "P", "Y", "N");
        Check("spacing_divergent_mismatch", D(spacing, 1),
            x => x.HasSerializedSpacingDivergence && x.Counterfactuals
                .Single(r => r.RefinementId == "plus-exact-spacing").RefinedDisposition
                == AdaptiveReconstructionState.NoContext, rows);
        Check("spacing_same_mismatch", D(Series("P", "X", "N", "Q", "P", "Y", "N"), 1),
            x => !x.HasSerializedSpacingDivergence, rows);
        var held = Series("P", "X", "N", "Q", "P", "Y", "N");
        held = held with { Events = held.Events.SetItem(5, Token(held.Events[5], "Y", [], [1], [2])) };
        Check("held_and_type_divergent", D(held, 1),
            x => x.HasHeldStateDivergence && x.HasTypeCompositionDivergence, rows);
        var repeated = new List<string> { "P", "T", "N", "S0" };
        for (var i = 0; i < 10; i++) repeated.AddRange(["P", "A", "N", $"S{i + 1}"]);
        repeated.AddRange(["P", "B", "N"]);
        var multi = D(Series(repeated.ToArray()), 1);
        rows.Add(new("multiplicity_no_vote", multi.Disposition == AdaptiveReconstructionState.Ambiguous
            && multi.DistinctCenterTokenCount == 2, $"centers={multi.DistinctCenterTokenCount};donors={multi.PostHoldoutDonorCount}"));
        var links = ExactRecurrenceFailureResearch.AuditBlockRelationSemantics(Series("A", "A", "A", "A"), 1).Single();
        rows.Add(new("four_occurrence_link_semantics", links.OccurrenceCount == 4
            && links.EmittedConsecutiveRelationCount == 3 && links.AllPairsCount == 6,
            $"occurrences={links.OccurrenceCount};links={links.EmittedConsecutiveRelationCount};allPairs={links.AllPairsCount}"));
        rows.Add(new("edge_ineligible", ExactRecurrenceFailureResearch.DiagnoseTarget(Series("A", "B", "C"), 0)
            .PrimaryNoContextReason == ExactNoContextReason.EdgeIneligible, "edge excluded from eligible population"));
        rows.Add(new("counterfactual_transition_distinction",
            ExactRecurrenceFailureResearch.DescribeTransition(AdaptiveReconstructionState.Mismatch,
                AdaptiveReconstructionState.Reconstructed) == CounterfactualTransition.BecameReconstructed
            && ExactRecurrenceFailureResearch.DescribeTransition(AdaptiveReconstructionState.Mismatch,
                AdaptiveReconstructionState.NoContext) == CounterfactualTransition.BecameNoContext,
            "support and coverage destruction remain distinct"));
        rows.Add(new("leakage_positive_and_correct",
            AdaptiveContextResearch.AuditLeakage([new OriginalObservationId(1)], [new OriginalObservationId(1)]) > 0
            && D(touching, 1).LeakageCount == 0, "bad>0;correct=0"));
        var rice = Series(Enumerable.Range(0, 20).Select(i => i * 31).ToArray(),
            Enumerable.Range(0, 20).Select(i => i % 2 == 0 ? "A" : "B").ToArray());
        rows.Add(new("rice_fast_exact", ExactRecurrenceFailureResearch.Evaluate(rice).Diagnostics
            .Any(x => x.Disposition == AdaptiveReconstructionState.Reconstructed), "31ms exact recurrence"));
        var lnChart = new ManiaChart { KeyCount = 4, Lines = [],
            OriginalObjects = [ManiaObject.Ln(0, 0, 1000), ManiaObject.Tap(1, 500), ManiaObject.Tap(2, 1500)],
            TimingPoints = [new TimingPoint(0, 500), new TimingPoint(1000, 250)] };
        rows.Add(new("ln_release_timing", ExactRecurrenceFailureResearch.Evaluate(lnChart).Diagnostics
            .Any(x => x.TargetHeldBefore || x.ReleaseInvolvedExternalContext || x.TargetReleaseInvolved),
            "head/release/held remain distinct across BPM"));
        foreach (var keys in new[] { 1, 4, 7, 10, 18 })
        {
            var chart = new ManiaChart { KeyCount = keys, Lines = [],
                OriginalObjects = [ManiaObject.Tap(0, 0), ManiaObject.Tap(0, 100), ManiaObject.Tap(0, 200)],
                TimingPoints = [new TimingPoint(0, 500)] };
            var result = ExactRecurrenceFailureResearch.Evaluate(chart);
            rows.Add(new($"multikey_{keys}k", result.KeyCount == keys && result.CrossChartEvidenceCount == 0,
                "keymode is stratification only"));
        }
        return rows;
    }

    private static IReadOnlyList<ReproductionRow> ReproducePhaseE(C11CorpusDiscoveryResult discovery,
        IReadOnlyList<AdaptiveContextResearchResult> results)
    {
        var held = results.SelectMany(x => x.HeldOut)
            .Where(x => x.Method.MethodId == "exact-neighbor-recurrence.1").ToArray();
        return
        [
            M("charts", discovery.UniqueHumanCharts.Length, 11),
            M("families", discovery.UniqueHumanCharts.Select(x => x.FamilyKey).Distinct().Count(), 11),
            M("original_objects", results.Sum(x => x.EventSeries.OriginalObjectCount), 50836),
            M("event_groups", results.Sum(x => x.EventSeries.Events.Length), 28312),
            M("eligible", held.Sum(x => x.Eligible), 28290),
            M("comparable", held.Sum(x => x.Comparable), 12066),
            M("reconstructed", held.Sum(x => x.Reconstructed), 4143),
            M("ambiguous", held.Sum(x => x.Ambiguous), 6472),
            M("mismatch", held.Sum(x => x.Mismatch), 1451),
            M("no_context", held.Sum(x => x.NoContext), 16224),
            M("block_relation_links", results.Sum(x => x.Recurrences.Length), 10980),
            M("non_contiguous_links", results.Sum(x => x.Recurrences.Count(r =>
                r.Right.StartIndex > r.Left.StartIndex + r.BlockLength)), 10764),
            M("min_run_2_boundaries", results.Sum(x => x.Segmentations[0].Boundaries.Length), 288),
            M("min_run_3_boundaries", results.Sum(x => x.Segmentations[1].Boundaries.Length), 89)
        ];
    }

    private static ReproductionRow M(string metric, int observed, int expected) => new(metric, expected, observed, observed == expected);
    private static void WriteReproduction(string path, IEnumerable<ReproductionRow> rows)
    { using var w = Writer(path); w.WriteLine("metric,expected,observed,match"); foreach (var x in rows) w.WriteLine($"{x.Metric},{I(x.Expected)},{I(x.Observed)},{B(x.Match)}"); }
    private static void WriteSynthetic(string path, IEnumerable<GateRow> rows)
    { using var w = Writer(path); w.WriteLine("case_id,pass,details"); foreach (var x in rows) w.WriteLine($"{Csv(x.CaseId)},{B(x.Pass)},{Csv(x.Details)}"); }

    private static void WriteRelationSemantics(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path); w.WriteLine("family_key,relative_path,key_count,recurrent_identities,occurrences,emitted_consecutive_links,all_possible_pairs");
        foreach (var x in charts) w.WriteLine(string.Join(',', Csv(x.Family), Csv(x.Relative), I(x.Keys),
            I(x.Result.BlockRelationSemantics.Length), I(x.Result.BlockRelationSemantics.Sum(a => a.OccurrenceCount)),
            I(x.Result.BlockRelationSemantics.Sum(a => a.EmittedConsecutiveRelationCount)), I(x.Result.BlockRelationSemantics.Sum(a => a.AllPairsCount))));
    }

    private static void WriteChartSummary(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path); PopulationHeader(w);
        foreach (var x in charts) WritePopulation(w, x.Family, x.Relative, x.Keys, x.Result.Diagnostics);
    }
    private static void WriteFamilySummary(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path); PopulationHeader(w);
        foreach (var g in charts.GroupBy(x => x.Family).OrderBy(x => x.Key, StringComparer.Ordinal))
            WritePopulation(w, g.Key, "", g.Select(x => x.Keys).Distinct().Single(), g.SelectMany(x => x.Result.Diagnostics).ToArray());
    }
    private static void PopulationHeader(StreamWriter w) => w.WriteLine("family_key,relative_path,key_count,eligible,reconstructed,ambiguous,mismatch,no_context,no_pair_elsewhere,only_excluded,spacing_divergence,held_divergence,type_divergence,leakage");
    private static void WritePopulation(StreamWriter w, string family, string relative, int keys,
        IReadOnlyCollection<ExactRecurrenceFailureDiagnostic> d) => w.WriteLine(string.Join(',', Csv(family), Csv(relative), I(keys), I(d.Count),
        I(d.Count(x => x.Disposition == AdaptiveReconstructionState.Reconstructed)), I(d.Count(x => x.Disposition == AdaptiveReconstructionState.Ambiguous)),
        I(d.Count(x => x.Disposition == AdaptiveReconstructionState.Mismatch)), I(d.Count(x => x.Disposition == AdaptiveReconstructionState.NoContext)),
        I(d.Count(x => x.PrimaryNoContextReason == ExactNoContextReason.NoExactNeighborPairElsewhere)),
        I(d.Count(x => x.PrimaryNoContextReason == ExactNoContextReason.OnlyExcludedOccurrences)),
        I(d.Count(x => x.HasSerializedSpacingDivergence || x.HasExactFileDerivedBeatSpacingDivergence)),
        I(d.Count(x => x.HasHeldStateDivergence)), I(d.Count(x => x.HasTypeCompositionDivergence)), I(d.Sum(x => x.LeakageCount))));

    private static void WriteProperties(string path, IReadOnlyList<ChartData> charts)
    {
        var properties = new (string, Func<ExactRecurrenceFailureDiagnostic, bool>)[]
        {
            ("serialized_spacing_divergence", x => x.HasSerializedSpacingDivergence), ("exact_file_beat_spacing_divergence", x => x.HasExactFileDerivedBeatSpacingDivergence),
            ("held_state_divergence", x => x.HasHeldStateDivergence), ("type_composition_divergence", x => x.HasTypeCompositionDivergence),
            ("head_count_divergence", x => x.HasHeadCountDivergence), ("lane_occupancy_divergence", x => x.HasLaneOccupancyDivergence),
            ("mixed_type_divergence", x => x.HasMixedTypeDivergence), ("release_involved_external_context", x => x.ReleaseInvolvedExternalContext),
            ("target_release_involved", x => x.TargetReleaseInvolved), ("target_held_before", x => x.TargetHeldBefore),
            ("endpoint_or_group_collision", x => x.HasEndpointOrGroupCollision), ("other_pre_holdout_occurrence", x => x.HasOtherPreHoldoutOccurrence)
        };
        using var w = Writer(path); w.WriteLine("disposition,property,denominator,count,fraction,charts_present,families_present,keymodes_present");
        var flat = Flat(charts);
        foreach (var state in Enum.GetValues<AdaptiveReconstructionState>()) foreach (var p in properties)
        {
            var population = flat.Where(x => x.Diagnostic.Disposition == state).ToArray(); var selected = population.Where(x => p.Item2(x.Diagnostic)).ToArray();
            w.WriteLine(string.Join(',', state, Csv(p.Item1), I(population.Length), I(selected.Length), Ratio(selected.Length, population.Length),
                I(selected.Select(x => x.Chart.Relative).Distinct().Count()), I(selected.Select(x => x.Chart.Family).Distinct().Count()),
                Csv(string.Join(';', selected.Select(x => x.Chart.Keys).Distinct().Order()))));
        }
    }

    private static void WriteNoContext(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path); w.WriteLine("primary_reason,mutually_exclusive,denominator,count,fraction,charts_present,families_present,keymodes_present");
        var pop = Flat(charts).Where(x => x.Diagnostic.Disposition == AdaptiveReconstructionState.NoContext).ToArray();
        foreach (var reason in Enum.GetValues<ExactNoContextReason>().Where(x => x != ExactNoContextReason.NotApplicable)) WriteStratum(w, reason.ToString(), true, pop, x => x.Diagnostic.PrimaryNoContextReason == reason);
    }
    private static void WriteMismatch(string path, IReadOnlyList<ChartData> charts)
    {
        var flags = new (string, Func<ExactRecurrenceFailureDiagnostic, bool>)[]
        {
            ("serialized_spacing_disagreement", x => x.HasSerializedSpacingDivergence), ("exact_file_beat_spacing_disagreement", x => x.HasExactFileDerivedBeatSpacingDivergence),
            ("held_disagreement", x => x.HasHeldStateDivergence), ("type_disagreement", x => x.HasTypeCompositionDivergence),
            ("head_count_disagreement", x => x.HasHeadCountDivergence), ("lane_occupancy_disagreement", x => x.HasLaneOccupancyDivergence),
            ("multiple_diagnostic_differences", x => Differences(x) > 1), ("none_of_measured_differences", x => Differences(x) == 0)
        };
        using var w = Writer(path); w.WriteLine("diagnostic_property,categories_overlap,denominator,count,fraction,charts_present,families_present,keymodes_present");
        var pop = Flat(charts).Where(x => x.Diagnostic.Disposition == AdaptiveReconstructionState.Mismatch).ToArray();
        foreach (var flag in flags) WriteStratum(w, flag.Item1, true, pop, x => flag.Item2(x.Diagnostic));
    }
    private static void WriteStratum(StreamWriter w, string name, bool overlap,
        (ChartData Chart, ExactRecurrenceFailureDiagnostic Diagnostic)[] pop,
        Func<(ChartData Chart, ExactRecurrenceFailureDiagnostic Diagnostic), bool> test)
    {
        var selected = pop.Where(test).ToArray(); w.WriteLine(string.Join(',', Csv(name), B(overlap), I(pop.Length), I(selected.Length), Ratio(selected.Length, pop.Length),
            I(selected.Select(x => x.Chart.Relative).Distinct().Count()), I(selected.Select(x => x.Chart.Family).Distinct().Count()),
            Csv(string.Join(';', selected.Select(x => x.Chart.Keys).Distinct().Order()))));
    }

    private static void WriteDonors(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path); w.WriteLine("disposition,raw_donor_occurrences,distinct_center_tokens,occurrences_per_center,count,charts_present,families_present,keymodes_present");
        foreach (var g in Flat(charts).GroupBy(x => new { x.Diagnostic.Disposition, Raw = x.Diagnostic.PostHoldoutDonorCount,
                     Centers = x.Diagnostic.DistinctCenterTokenCount, Distribution = string.Join(';', x.Diagnostic.CenterMultiplicities.Select(c => c.OccurrenceCount).Order()) })
                 .OrderBy(x => x.Key.Disposition).ThenBy(x => x.Key.Raw).ThenBy(x => x.Key.Centers))
            w.WriteLine(string.Join(',', g.Key.Disposition, I(g.Key.Raw), I(g.Key.Centers), Csv(g.Key.Distribution), I(g.Count()),
                I(g.Select(x => x.Chart.Relative).Distinct().Count()), I(g.Select(x => x.Chart.Family).Distinct().Count()), Csv(string.Join(';', g.Select(x => x.Chart.Keys).Distinct().Order()))));
    }
    private static void WriteCounterfactuals(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path); w.WriteLine("baseline_disposition,refinement_id,refined_disposition,transition,count,charts_present,families_present,keymodes_present");
        var rows = Flat(charts).SelectMany(x => x.Diagnostic.Counterfactuals.Select(a => (x.Chart, Audit: a)));
        foreach (var g in rows.GroupBy(x => new { x.Audit.BaselineDisposition, x.Audit.RefinementId, x.Audit.RefinedDisposition, x.Audit.Transition })
                     .OrderBy(x => x.Key.BaselineDisposition).ThenBy(x => x.Key.RefinementId, StringComparer.Ordinal).ThenBy(x => x.Key.RefinedDisposition))
            w.WriteLine(string.Join(',', g.Key.BaselineDisposition, Csv(g.Key.RefinementId), g.Key.RefinedDisposition, g.Key.Transition, I(g.Count()),
                I(g.Select(x => x.Chart.Relative).Distinct().Count()), I(g.Select(x => x.Chart.Family).Distinct().Count()), Csv(string.Join(';', g.Select(x => x.Chart.Keys).Distinct().Order()))));
    }
    private static void WriteSpacing(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path); w.WriteLine("disposition,previous_ms,next_ms,previous_exact_file_beat,next_exact_file_beat,has_spacing_divergence,count,charts_present,families_present,keymodes_present");
        foreach (var g in Flat(charts).GroupBy(x => new { x.Diagnostic.Disposition, x.Diagnostic.PreviousSerializedSpacingMilliseconds,
                     x.Diagnostic.NextSerializedSpacingMilliseconds, x.Diagnostic.PreviousExactFileDerivedBeatSpacing,
                     x.Diagnostic.NextExactFileDerivedBeatSpacing, Divergence = x.Diagnostic.HasSerializedSpacingDivergence || x.Diagnostic.HasExactFileDerivedBeatSpacingDivergence })
                 .OrderBy(x => x.Key.Disposition).ThenBy(x => x.Key.PreviousSerializedSpacingMilliseconds).ThenBy(x => x.Key.NextSerializedSpacingMilliseconds))
            w.WriteLine(string.Join(',', g.Key.Disposition, N(g.Key.PreviousSerializedSpacingMilliseconds), N(g.Key.NextSerializedSpacingMilliseconds),
                N(g.Key.PreviousExactFileDerivedBeatSpacing), N(g.Key.NextExactFileDerivedBeatSpacing), B(g.Key.Divergence), I(g.Count()),
                I(g.Select(x => x.Chart.Relative).Distinct().Count()), I(g.Select(x => x.Chart.Family).Distinct().Count()), Csv(string.Join(';', g.Select(x => x.Chart.Keys).Distinct().Order()))));
    }
    private static void WriteLn(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path); w.WriteLine("disposition,target_composition,target_held_before,release_involved_external_context,count,charts_present,families_present,keymodes_present");
        foreach (var g in Flat(charts).GroupBy(x => new { x.Diagnostic.Disposition, Composition = Composition(x.Diagnostic),
                     x.Diagnostic.TargetHeldBefore, x.Diagnostic.ReleaseInvolvedExternalContext })
                 .OrderBy(x => x.Key.Disposition).ThenBy(x => x.Key.Composition, StringComparer.Ordinal))
            w.WriteLine(string.Join(',', g.Key.Disposition, Csv(g.Key.Composition), B(g.Key.TargetHeldBefore), B(g.Key.ReleaseInvolvedExternalContext), I(g.Count()),
                I(g.Select(x => x.Chart.Relative).Distinct().Count()), I(g.Select(x => x.Chart.Family).Distinct().Count()), Csv(string.Join(';', g.Select(x => x.Chart.Keys).Distinct().Order()))));
    }
    private static void WriteLeakage(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path); var all = charts.SelectMany(x => x.Result.Diagnostics).ToArray();
        var bad = AdaptiveContextResearch.AuditLeakage([new OriginalObservationId(1)], [new OriginalObservationId(1)]);
        w.WriteLine("scope,charts,targets,target_observation_leakage,synthetic_teaching,cross_chart_evidence,bad_positive_control");
        w.WriteLine(string.Join(',', "global", I(charts.Count), I(all.Length), I(all.Sum(x => x.LeakageCount)), I(charts.Sum(x => x.Result.SyntheticTeachingCount)),
            I(charts.Sum(x => x.Result.CrossChartEvidenceCount)), I(bad)));
    }

    private static (ChartData Chart, ExactRecurrenceFailureDiagnostic Diagnostic)[] Flat(IEnumerable<ChartData> charts) =>
        charts.SelectMany(chart => chart.Result.Diagnostics.Select(d => (chart, d))).ToArray();
    private static int Differences(ExactRecurrenceFailureDiagnostic x) => new[] { x.HasSerializedSpacingDivergence || x.HasExactFileDerivedBeatSpacingDivergence,
        x.HasHeldStateDivergence, x.HasTypeCompositionDivergence, x.HasHeadCountDivergence, x.HasLaneOccupancyDivergence, x.HasMixedTypeDivergence }.Count(v => v);
    private static string Composition(ExactRecurrenceFailureDiagnostic x) => x.TargetTapHeadCount > 0 && x.TargetLongNoteHeadCount > 0 ? "mixed-tap-ln-head"
        : x.TargetLongNoteHeadCount > 0 ? "ln-head" : x.TargetTapHeadCount > 0 ? "tap-head" : x.TargetReleaseCount > 0 ? "release-only" : "no-head-no-release";
    private static ExactRecurrenceFailureDiagnostic D(OriginalTemporalEventSeries s, int i) => ExactRecurrenceFailureResearch.DiagnoseTarget(s, i);
    private static void Check(string id, ExactRecurrenceFailureDiagnostic d, Func<ExactRecurrenceFailureDiagnostic, bool> test, ICollection<GateRow> rows) =>
        rows.Add(new(id, test(d), $"disposition={d.Disposition};reason={d.PrimaryNoContextReason}"));
    private static OriginalTemporalEventSeries Series(params string[] ids) => Series(Enumerable.Range(0, ids.Length).Select(i => i * 100).ToArray(), ids);
    private static OriginalTemporalEventSeries Series(int[] times, params string[] ids)
    {
        var events = ids.Select((id, i) => Event(id, i, times[i], i == 0 ? null : times[i - 1])).ToImmutableArray();
        return new(AdaptiveContextResearch.ResearchSchemaVersion, "synthetic-e1", 4, ids.Length, events, 0, 0);
    }
    private static OriginalTemporalEvent Event(string id, int i, int time, int? previous) => new($"E{i:D8}", "synthetic-e1", time, time / 500m,
        previous is null ? null : time - previous.Value, previous is null ? null : (time - previous.Value) / 500m,
        [new OriginalObservationId(i)], [], new(id, [i % 4], [], [], [], 1, 0, false));
    private static OriginalTemporalEvent Token(OriginalTemporalEvent source, string id, ImmutableArray<int> taps, ImmutableArray<int> lns, ImmutableArray<int> held) =>
        source with { Token = new(id, taps, lns, [], held, taps.Length + lns.Length, 0, !taps.IsEmpty && !lns.IsEmpty) };
    private static JsonSerializerOptions JsonOptions() => new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string B(bool value) => value ? "true" : "false";
    private static string Ratio(int value, int denominator) => denominator == 0 ? "0" : ((decimal)value / denominator).ToString("0.000000", CultureInfo.InvariantCulture);
    private static string N<T>(T? value) where T : struct => value is null ? ""
        : ((IFormattable)value.Value).ToString(null, CultureInfo.InvariantCulture);
    private sealed record GateRow(string CaseId, bool Pass, string Details);
    private sealed record ReproductionRow(string Metric, int Expected, int Observed, bool Match);
    private sealed record ChartData(string Family, string Relative, string Version, string Sha256, int Keys, ExactRecurrenceFailureResearchResult Result);
}
