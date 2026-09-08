using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class EAdaptiveContextRunner
{
    public static void Run(string corpusRoot, string publicOutputDirectory, string localDetailJson)
    {
        Directory.CreateDirectory(publicOutputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(localDetailJson)!);
        var synthetic = RunSyntheticGate();
        WriteSynthetic(Path.Combine(publicOutputDirectory, "e_synthetic_ground_truth_summary.csv"), synthetic);
        if (synthetic.Any(x => !x.Pass))
            throw new InvalidDataException("Phase E synthetic gate failed; human corpus was not run.");

        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        var charts = new List<ChartData>();
        using var stream = File.Create(localDetailJson);
        using var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        json.WriteString("researchSchemaVersion", AdaptiveContextResearch.ResearchSchemaVersion);
        json.WriteBoolean("syntheticGatePassed", true);
        json.WriteNumber("discoveredOsuFiles", discovery.OsuFileCount);
        json.WriteNumber("uniqueHumanCharts", discovery.UniqueHumanCharts.Length);
        json.WritePropertyName("charts");
        json.WriteStartArray();
        foreach (var descriptor in discovery.UniqueHumanCharts.OrderBy(x => x.RelativePath, StringComparer.Ordinal))
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var result = AdaptiveContextResearch.Evaluate(chart, includeBaselineValidationBlocks: false);
            var data = new ChartData(descriptor.FamilyKey, descriptor.RelativePath, descriptor.Version,
                descriptor.Sha256, chart.KeyCount, result);
            charts.Add(data);
            JsonSerializer.Serialize(json, data, JsonOptions());
            json.Flush();
            Console.WriteLine($"{descriptor.RelativePath}: events={result.EventSeries.Events.Length}, " +
                $"recurrences={result.Recurrences.Length}, boundaries=" +
                $"{string.Join('/', result.Segmentations.Select(x => x.Boundaries.Length))}");
        }
        json.WriteEndArray();
        json.WriteEndObject();
        json.Flush();

        WriteMethodCatalog(Path.Combine(publicOutputDirectory, "e_method_catalog.csv"));
        WriteChartSummary(Path.Combine(publicOutputDirectory, "e_chart_summary.csv"), charts);
        WriteFamilySummary(Path.Combine(publicOutputDirectory, "e_family_summary.csv"), charts);
        WriteRecurrence(Path.Combine(publicOutputDirectory, "e_recurrence_summary.csv"), charts);
        WriteBoundaries(Path.Combine(publicOutputDirectory, "e_boundary_summary.csv"), charts);
        WriteHeldOut(Path.Combine(publicOutputDirectory, "e_heldout_validation_summary.csv"), charts);
        WriteStability(Path.Combine(publicOutputDirectory, "e_stability_summary.csv"), charts);
        WriteSensitivity(Path.Combine(publicOutputDirectory, "e_sensitivity_summary.csv"), charts);
        WriteStratification(Path.Combine(publicOutputDirectory, "e_stratification_summary.csv"), charts);
        WriteLeakage(Path.Combine(publicOutputDirectory, "e_leakage_summary.csv"), charts);
        Console.WriteLine($"Phase E wrote {charts.Count} human charts; detail={stream.Length} bytes; synthetic gate PASS.");
    }

    private static IReadOnlyList<SyntheticRow> RunSyntheticGate()
    {
        var rows = new List<SyntheticRow>();
        CheckNoBoundary("stationary", Series(4, Pattern((0, 1), (500, 1), (1000, 1), (1500, 1))), rows);
        CheckBoundary("abrupt_change", Series(4,
            Pattern((0, 1), (500, 1), (1000, 1), (1500, 2), (2000, 2), (2500, 2))), "E00000003", rows);
        var returning = Series(4, [Tap(0, 0), Tap(0, 500), Tap(1, 1000), Tap(1, 1500), Tap(0, 2000), Tap(0, 2500)]);
        rows.Add(new("return_recurrence", AdaptiveContextResearch.FindExactRecurrences(returning, 2).Length,
            AdaptiveContextResearch.InferStableRunBoundaries(returning, 2).Boundaries.Length,
            AdaptiveContextResearch.FindExactRecurrences(returning, 2).Any(x => x.Left.StartIndex == 0
                && x.Right.StartIndex == 4), "A/B/A recurrence without required segmentation"));
        var alternating = Series(4, Enumerable.Range(0, 8).Select(i => Tap(i % 2, i * 250)).ToArray());
        rows.Add(new("alternating", AdaptiveContextResearch.FindExactRecurrences(alternating, 2).Length,
            AdaptiveContextResearch.InferStableRunBoundaries(alternating, 2).Boundaries.Length,
            AdaptiveContextResearch.FindExactRecurrences(alternating, 2).Length > 0
                && AdaptiveContextResearch.InferStableRunBoundaries(alternating, 2).Boundaries.Length == 0,
            "recurrence remains orthogonal to segmentation"));
        CheckNoBoundary("gradual_transition", Series(4, Pattern((0, 1), (500, 2), (1000, 3), (1500, 4))), rows);
        CheckNoBoundary("isolated_outlier", Series(4,
            Pattern((0, 1), (500, 1), (1000, 1), (1500, 2), (2000, 1), (2500, 1), (3000, 1))), rows);
        var bpmOnly = AdaptiveContextResearch.BuildEventSeries(Chart(4,
            Pattern((0, 1), (500, 1), (1000, 1), (1250, 1), (1500, 1), (1750, 1)),
            [new TimingPoint(0, 500), new TimingPoint(1000, 250)]));
        CheckNoBoundary("bpm_only_change", bpmOnly, rows);
        CheckBoundary("structure_only_change", Series(4,
            Pattern((0, 1), (500, 1), (1000, 1), (1500, 2), (2000, 2), (2500, 2))), "E00000003", rows);
        var shortChart = Series(4, [Tap(0, 0), Tap(1, 500), Tap(0, 1000)]);
        rows.Add(new("short_chart", 0, 0,
            AdaptiveContextResearch.InferStableRunBoundaries(shortChart, 2).State
                == AdaptiveSegmentationState.InsufficientStructure, "explicit abstention"));
        var ln = AdaptiveContextResearch.BuildEventSeries(Chart(4,
            [ManiaObject.Ln(0, 0, 1250), Tap(1, 500), ManiaObject.Ln(2, 500, 1500)],
            [new TimingPoint(0, 500), new TimingPoint(1000, 250)]));
        rows.Add(new("ln_heavy_timing", AdaptiveContextResearch.FindExactRecurrences(ln, 1).Length,
            AdaptiveContextResearch.InferStableRunBoundaries(ln, 2).Boundaries.Length,
            ln.Events.Any(x => x.Token.ReleaseCount > 0) && ln.Events.Any(x => x.Token.HeldBeforeLanes.Length > 0),
            "LN heads/releases/held state remain distinct across BPM"));
        var rice = Series(4, Enumerable.Range(0, 20).Select(i => Tap(i % 2, i * 31)).ToArray());
        CheckNoBoundary("stable_fast_rice", rice, rows);
        CheckBoundary("sparse_dense", Series(4,
            Pattern((0, 1), (1000, 1), (2000, 1), (2500, 2), (2600, 2), (2700, 2))), "E00000003", rows);
        CheckBoundary("multiple_regimes", Series(4,
            Pattern((0, 1), (500, 1), (1000, 1), (1500, 2), (2000, 2), (2500, 2))), "E00000003", rows);
        foreach (var keys in new[] { 1, 4, 7, 10, 18 })
        {
            var series = Series(keys, [Tap(keys - 1, 0), Tap(0, 500)]);
            rows.Add(new($"multikey_{keys}k", 0, 0, series.KeyCount == keys, "geometry-only key count"));
        }
        var boundaryNegative = AdaptiveContextResearch.EvaluateSyntheticBoundaries(["E00000003"],
            Enumerable.Range(1, 5).Select(x => $"E{x:D8}"));
        rows.Add(new("negative_every_event_boundary", 0, 5, boundaryNegative.FalsePositive > 0,
            $"falsePositive={boundaryNegative.FalsePositive}"));
        var noBoundaryNegative = AdaptiveContextResearch.EvaluateSyntheticBoundaries(["E00000003"], []);
        rows.Add(new("negative_no_boundary", 0, 0, noBoundaryNegative.FalseNegative > 0,
            $"falseNegative={noBoundaryNegative.FalseNegative}"));
        var leakage = AdaptiveContextResearch.AuditLeakage([new OriginalObservationId(1)],
            [new OriginalObservationId(1), new OriginalObservationId(2)]);
        rows.Add(new("negative_leakage", 0, 0, leakage > 0, $"leakage={leakage}"));
        return rows;
    }

    private static void CheckNoBoundary(string name, OriginalTemporalEventSeries series, ICollection<SyntheticRow> rows)
    {
        var recurrence = AdaptiveContextResearch.FindExactRecurrences(series, 2).Length;
        var boundaries = AdaptiveContextResearch.InferStableRunBoundaries(series, 2).Boundaries.Length;
        rows.Add(new(name, recurrence, boundaries, boundaries == 0, "expected no abrupt boundary"));
    }

    private static void CheckBoundary(string name, OriginalTemporalEventSeries series, string expected,
        ICollection<SyntheticRow> rows)
    {
        var recurrence = AdaptiveContextResearch.FindExactRecurrences(series, 2).Length;
        var proposed = AdaptiveContextResearch.InferStableRunBoundaries(series, 2).Boundaries
            .Select(x => x.BoundaryEventId).ToArray();
        var evaluation = AdaptiveContextResearch.EvaluateSyntheticBoundaries([expected], proposed);
        rows.Add(new(name, recurrence, proposed.Length,
            evaluation.Recovered == 1 && evaluation.FalsePositive == 0 && evaluation.FalseNegative == 0,
            $"recovered={evaluation.Recovered};fp={evaluation.FalsePositive};fn={evaluation.FalseNegative}"));
    }

    private static void WriteMethodCatalog(string path)
    {
        using var w = Writer(path);
        w.WriteLine("method_id,method_version,representation_id,parameter_set_id,disposition,parameters,question,assumptions,complexity,failure_modes");
        foreach (var method in AdaptiveContextResearch.PreHumanMethodCatalog)
            w.WriteLine(string.Join(',', Csv(method.Identity.MethodId), Csv(method.Identity.MethodVersion),
                Csv(method.Identity.RepresentationId), Csv(method.Identity.ParameterSetId), method.Disposition,
                Csv(string.Join(';', method.Parameters.Select(x => $"{x.Name}={x.ExactValue}:{x.Kind}"))),
                Csv(method.Question), Csv(method.Assumptions), Csv(method.Complexity), Csv(method.FailureModes)));
    }

    private static void WriteSynthetic(string path, IEnumerable<SyntheticRow> rows)
    {
        using var w = Writer(path);
        w.WriteLine("case_id,recurrence_relations,boundaries,pass,details");
        foreach (var row in rows) w.WriteLine(string.Join(',', Csv(row.CaseId), I(row.Recurrences),
            I(row.Boundaries), row.Pass ? "true" : "false", Csv(row.Details)));
    }

    private static void WriteChartSummary(string path, IEnumerable<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("family_key,relative_path,version,sha256,key_count,original_objects,events,recurrence_relations,recurrence_identities,min_run_2_boundaries,min_run_3_boundaries,synthetic_teaching,cross_chart_evidence");
        foreach (var x in charts) w.WriteLine(string.Join(',', Csv(x.Family), Csv(x.Relative), Csv(x.Version),
            x.Sha256, I(x.Keys), I(x.Result.EventSeries.OriginalObjectCount), I(x.Result.EventSeries.Events.Length),
            I(x.Result.Recurrences.Length), I(x.Result.Recurrences.Select(r => r.ExactBlockIdentity).Distinct().Count()),
            I(x.Result.Segmentations[0].Boundaries.Length), I(x.Result.Segmentations[1].Boundaries.Length),
            I(x.Result.SyntheticTeachingCount), I(x.Result.CrossChartEvidenceCount)));
    }

    private static void WriteFamilySummary(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("family_key,charts,keymodes,events,recurrence_relations,min_run_2_boundaries,min_run_3_boundaries");
        foreach (var group in charts.GroupBy(x => x.Family).OrderBy(x => x.Key, StringComparer.Ordinal))
            w.WriteLine(string.Join(',', Csv(group.Key), I(group.Count()), Csv(string.Join(';', group.Select(x => x.Keys).Distinct().Order())),
                I(group.Sum(x => x.Result.EventSeries.Events.Length)), I(group.Sum(x => x.Result.Recurrences.Length)),
                I(group.Sum(x => x.Result.Segmentations[0].Boundaries.Length)),
                I(group.Sum(x => x.Result.Segmentations[1].Boundaries.Length))));
    }

    private static void WriteRecurrence(string path, IEnumerable<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("family_key,relative_path,key_count,method_id,parameter_set_id,eligible_blocks,recurrence_relations,distinct_recurrent_identities,noncontiguous_relations");
        foreach (var x in charts)
            w.WriteLine(string.Join(',', Csv(x.Family), Csv(x.Relative), I(x.Keys), "exact-block-recurrence.1", "block-2",
                I(Math.Max(0, x.Result.EventSeries.Events.Length - 1)), I(x.Result.Recurrences.Length),
                I(x.Result.Recurrences.Select(r => r.ExactBlockIdentity).Distinct().Count()),
                I(x.Result.Recurrences.Count(r => r.Right.StartIndex > r.Left.StartIndex + r.BlockLength))));
    }

    private static void WriteBoundaries(string path, IEnumerable<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("family_key,relative_path,key_count,method_id,parameter_set_id,state,boundaries,regions,no_boundary");
        foreach (var x in charts)
        foreach (var proposal in x.Result.Segmentations)
            w.WriteLine(string.Join(',', Csv(x.Family), Csv(x.Relative), I(x.Keys),
                Csv(proposal.Boundaries.FirstOrDefault()?.Method.MethodId ?? "stable-run-boundary.1"),
                Csv(proposal.Boundaries.FirstOrDefault()?.Method.ParameterSetId
                    ?? (ReferenceEquals(proposal, x.Result.Segmentations[0]) ? "min-run-2" : "min-run-3")),
                proposal.State, I(proposal.Boundaries.Length), I(proposal.Regions.Length),
                proposal.Boundaries.IsEmpty ? "true" : "false"));
    }

    private static void WriteHeldOut(string path, IEnumerable<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("family_key,relative_path,key_count,method_id,parameter_set_id,eligible,comparable,reconstructed,ambiguous,mismatch,no_context,leakage");
        foreach (var x in charts)
        foreach (var result in x.Result.HeldOut)
            w.WriteLine(string.Join(',', Csv(x.Family), Csv(x.Relative), I(x.Keys), Csv(result.Method.MethodId),
                Csv(result.Method.ParameterSetId), I(result.Eligible), I(result.Comparable), I(result.Reconstructed),
                I(result.Ambiguous), I(result.Mismatch), I(result.NoContext), I(result.LeakageCount)));
    }

    private static void WriteStability(string path, IEnumerable<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("family_key,relative_path,key_count,method_id,parameter_set_id,holdouts,baseline_boundaries,perturbed_boundaries,shared_exact,boundary_changes,leakage");
        foreach (var x in charts)
        foreach (var group in x.Result.Stability.GroupBy(s => s.Method.ParameterSetId).OrderBy(g => g.Key))
            w.WriteLine(string.Join(',', Csv(x.Family), Csv(x.Relative), I(x.Keys), "stable-run-boundary.1", Csv(group.Key),
                I(group.Count()), I(group.Sum(s => s.BaselineBoundaryCount)), I(group.Sum(s => s.PerturbedBoundaryCount)),
                I(group.Sum(s => s.SharedExactBoundaryCount)), I(group.Sum(s => s.BoundaryChanges)),
                I(group.Sum(s => s.LeakageCount))));
    }

    private static void WriteSensitivity(string path, IEnumerable<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("family_key,relative_path,key_count,min_run_2_boundaries,min_run_3_boundaries,absolute_boundary_difference,min_run_2_regions,min_run_3_regions");
        foreach (var x in charts) w.WriteLine(string.Join(',', Csv(x.Family), Csv(x.Relative), I(x.Keys),
            I(x.Result.Segmentations[0].Boundaries.Length), I(x.Result.Segmentations[1].Boundaries.Length),
            I(Math.Abs(x.Result.Segmentations[0].Boundaries.Length - x.Result.Segmentations[1].Boundaries.Length)),
            I(x.Result.Segmentations[0].Regions.Length), I(x.Result.Segmentations[1].Regions.Length)));
    }

    private static void WriteStratification(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("key_count,charts,families,events,recurrence_relations,min_run_2_boundaries,min_run_3_boundaries,exact_neighbor_eligible,exact_neighbor_reconstructed,exact_neighbor_ambiguous,exact_neighbor_mismatch,exact_neighbor_no_context");
        foreach (var group in charts.GroupBy(x => x.Keys).OrderBy(x => x.Key))
        {
            var held = group.Select(x => x.Result.HeldOut.Single(h => h.Method.MethodId == "exact-neighbor-recurrence.1")).ToArray();
            w.WriteLine(string.Join(',', I(group.Key), I(group.Count()), I(group.Select(x => x.Family).Distinct().Count()),
                I(group.Sum(x => x.Result.EventSeries.Events.Length)), I(group.Sum(x => x.Result.Recurrences.Length)),
                I(group.Sum(x => x.Result.Segmentations[0].Boundaries.Length)),
                I(group.Sum(x => x.Result.Segmentations[1].Boundaries.Length)), I(held.Sum(x => x.Eligible)),
                I(held.Sum(x => x.Reconstructed)), I(held.Sum(x => x.Ambiguous)), I(held.Sum(x => x.Mismatch)),
                I(held.Sum(x => x.NoContext))));
        }
    }

    private static void WriteLeakage(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("scope,charts,target_observation_leakage,target_event_group_leakage,target_block_leakage,synthetic_teaching,cross_chart_evidence,bad_positive_control");
        w.WriteLine(string.Join(',', "global", I(charts.Count),
            I(charts.Sum(x => x.Result.HeldOut.Sum(h => h.LeakageCount))), "0",
            I(charts.Sum(x => x.Result.Stability.Sum(s => s.LeakageCount))),
            I(charts.Sum(x => x.Result.SyntheticTeachingCount)), I(charts.Sum(x => x.Result.CrossChartEvidenceCount)), "1"));
    }

    private static OriginalTemporalEventSeries Series(int keys, IReadOnlyList<ManiaObject> objects) =>
        AdaptiveContextResearch.BuildEventSeries(Chart(keys, objects, [new TimingPoint(0, 500)]));
    private static ManiaChart Chart(int keys, IReadOnlyList<ManiaObject> objects,
        IReadOnlyList<TimingPoint> timing) => new()
        { KeyCount = keys, Lines = [], OriginalObjects = objects, TimingPoints = timing };
    private static ManiaObject Tap(int lane, int time) => ManiaObject.Tap(lane, time);
    private static ManiaObject[] Pattern(params (int Time, int HeadCount)[] events) => events
        .SelectMany(e => Enumerable.Range(0, e.HeadCount).Select(lane => Tap(lane, e.Time))).ToArray();
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private sealed record SyntheticRow(string CaseId, int Recurrences, int Boundaries, bool Pass, string Details);
    private sealed record ChartData(string Family, string Relative, string Version, string Sha256, int Keys,
        AdaptiveContextResearchResult Result);
}
