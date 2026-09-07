using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class D01CorpusRunner
{
    public static void Run(string corpusRoot, string publicOutputDirectory, string localDetailJson)
    {
        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        var rows = new List<EvaluatedChart>();
        foreach (var descriptor in discovery.UniqueHumanCharts)
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var result = ExactCompletionContextResearch.Evaluate(chart);
            var detailBytes = Encoding.UTF8.GetByteCount(ExactCompletionContextResearchJson.Serialize(result));
            rows.Add(new EvaluatedChart(Public(descriptor), result, detailBytes));
            Console.WriteLine($"{descriptor.RelativePath}: trials={result.CompletionTrials}, " +
                $"primary={result.BaselineCompetingTrials}, context={result.ContextBuildMilliseconds:0.##}ms, " +
                $"match={result.ExactMatchMilliseconds:0.##}ms");
        }

        Directory.CreateDirectory(publicOutputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(localDetailJson)!);
        WriteScope(Path.Combine(publicOutputDirectory, "d0_1_chart_summary.csv"), rows, Scope.Chart);
        WriteScope(Path.Combine(publicOutputDirectory, "d0_1_family_summary.csv"), rows, Scope.Family);
        WriteScope(Path.Combine(publicOutputDirectory, "d0_1_global_summary.csv"), rows, Scope.Global);
        WriteContextViews(Path.Combine(publicOutputDirectory, "d0_1_context_view_summary.csv"), rows);
        var safeDiscovery = discovery with
        {
            UniqueHumanCharts = discovery.UniqueHumanCharts.Select(Public).ToImmutableArray()
        };
        File.WriteAllText(localDetailJson,
            JsonSerializer.Serialize(new DetailArtifact(safeDiscovery, rows), JsonOptions()),
            new UTF8Encoding(false));
        Console.WriteLine($"Wrote D0.1 summaries for {rows.Count} charts, " +
            $"{rows.Select(x => x.Chart.FamilyKey).Distinct().Count()} families and " +
            $"{ExactCompletionContextResearch.Views.Length} parallel views.");
    }

    private static void WriteScope(string path, IReadOnlyList<EvaluatedChart> rows, Scope scope)
    {
        using var writer = Writer(path);
        writer.WriteLine(ScopeHeader());
        var groups = scope switch
        {
            Scope.Chart => rows.Select(x => new[] { x }),
            Scope.Family => rows.GroupBy(x => x.Chart.FamilyKey).OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => x.ToArray()),
            Scope.Global => new[] { rows.ToArray() },
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };
        var families = rows.GroupBy(x => x.Chart.FamilyKey).Select(x => x.ToArray()).ToArray();
        foreach (var group in groups)
        foreach (var view in ExactCompletionContextResearch.Views)
        {
            var first = group[0];
            var family = scope == Scope.Global ? "GLOBAL" : first.Chart.FamilyKey;
            var relative = scope == Scope.Chart ? first.Chart.RelativePath : "";
            var version = scope == Scope.Chart ? first.Chart.Version : "ALL";
            var keyCounts = group.Select(x => x.Chart.KeyCount).Distinct().ToArray();
            var keys = keyCounts.Length == 1 ? keyCounts[0] : 0;
            var evaluations = Evaluations(group, view).ToArray();
            var primary = Primary(group, view).ToArray();
            var macroResolved = scope == Scope.Global ? Macro(families, view,
                x => x.Outcome == ExactCompletionContextOutcome.TargetResolvedUnique, true) : null;
            var macroComparable = scope == Scope.Global ? Macro(families, view,
                x => x.Outcome != ExactCompletionContextOutcome.NoComparableExactContext, false) : null;
            var fields = new[]
            {
                Csv(family), Csv(relative), Csv(version), I(keys), I(group.Length), view.ToString(),
                I(evaluations.Length), I(primary.Length),
                I(primary.Sum(x => x.BaselineCompletionCount)),
                I(primary.Sum(x => x.ContextualCompletionCount)),
                F(primary.Length == 0 ? null : primary.Average(x => x.BaselineCompletionCount)),
                F(primary.Length == 0 ? null : primary.Average(x => x.ContextualCompletionCount)),
                I(Count(evaluations, ExactCompletionContextOutcome.NoComparableExactContext)),
                I(Count(evaluations, ExactCompletionContextOutcome.ComparableTargetUnsupported)),
                I(Count(evaluations, ExactCompletionContextOutcome.TargetSupportedStillCompeting)),
                I(Count(evaluations, ExactCompletionContextOutcome.TargetResolvedUnique)),
                I(Count(evaluations, ExactCompletionContextOutcome.UniqueWrongCompletion)),
                I(primary.Count(x => x.Outcome == ExactCompletionContextOutcome.NoComparableExactContext)),
                I(primary.Count(x => x.Outcome == ExactCompletionContextOutcome.ComparableTargetUnsupported)),
                I(primary.Count(x => x.Outcome == ExactCompletionContextOutcome.TargetSupportedStillCompeting)),
                I(primary.Count(x => x.Outcome == ExactCompletionContextOutcome.TargetResolvedUnique)),
                I(primary.Count(x => x.Outcome == ExactCompletionContextOutcome.UniqueWrongCompletion)),
                I(evaluations.Count(x => x.ExactTargetSupported)),
                I(evaluations.Count(x => x.LaneSupportedButTypeDifferent)),
                I(evaluations.Sum(x => x.ContextualIndependentWitnessCount)),
                I(group.Sum(x => x.Result.TargetGroupLeakageCount)),
                I(group.Sum(x => x.Result.FutureHeldLeakageCount)),
                I(group.Sum(x => x.Result.HeldTailEncodedAsHeadCount)),
                I(group.Sum(x => x.Result.DonorNestingViolationCount)),
                I(group.Sum(x => x.Result.BeatMillisecondMaterializationDifferences)),
                F(group.Sum(x => x.Result.ContextBuildMilliseconds)),
                F(group.Sum(x => x.Result.ExactMatchMilliseconds)),
                group.Sum(x => x.DetailArtifactBytes).ToString(CultureInfo.InvariantCulture),
                F(Rate(evaluations.Count(x => x.Outcome != ExactCompletionContextOutcome.NoComparableExactContext),
                    evaluations.Length)),
                F(Rate(evaluations.Count(x => x.ExactTargetSupported), evaluations.Length)),
                F(Rate(primary.Count(x => x.Outcome == ExactCompletionContextOutcome.TargetResolvedUnique),
                    primary.Length)),
                F(macroComparable), F(macroResolved)
            };
            writer.WriteLine(string.Join(',', fields));
        }
    }

    private static void WriteContextViews(string path, IReadOnlyList<EvaluatedChart> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine("view,target_type,original_chord_head_count,reduced_visible_member_count,held_before,baseline_completion_count,trials,baseline_competing_trials,no_comparable_exact_context,comparable_target_unsupported,target_supported_still_competing,target_resolved_unique,unique_wrong_completion,contextual_witness_references,micro_coverage_rate,micro_target_supported_rate,primary_resolution_rate");
        var joined = rows.SelectMany(row => row.Result.Trials.SelectMany(trial => trial.Views.Select(view =>
            new { Trial = trial, View = view }))).GroupBy(x => new
            {
                x.View.View, x.Trial.TargetType, x.Trial.OriginalChordHeadCount,
                x.Trial.ReducedVisibleMemberCount, x.Trial.HeldBeforePresent, x.Trial.BaselineCompletionCount
            }).OrderBy(x => x.Key.View).ThenBy(x => x.Key.TargetType)
            .ThenBy(x => x.Key.OriginalChordHeadCount).ThenBy(x => x.Key.ReducedVisibleMemberCount)
            .ThenBy(x => x.Key.HeldBeforePresent).ThenBy(x => x.Key.BaselineCompletionCount);
        foreach (var group in joined)
        {
            var items = group.ToArray();
            var evaluations = items.Select(x => x.View).ToArray();
            var primary = items.Where(x => x.Trial.BaselineState ==
                ChordCompletionReconstructionState.TargetAmongCompetingCompletions).Select(x => x.View).ToArray();
            writer.WriteLine(string.Join(',', group.Key.View, group.Key.TargetType,
                I(group.Key.OriginalChordHeadCount), I(group.Key.ReducedVisibleMemberCount),
                group.Key.HeldBeforePresent ? "present" : "absent", I(group.Key.BaselineCompletionCount),
                I(items.Length), I(primary.Length),
                I(Count(evaluations, ExactCompletionContextOutcome.NoComparableExactContext)),
                I(Count(evaluations, ExactCompletionContextOutcome.ComparableTargetUnsupported)),
                I(Count(evaluations, ExactCompletionContextOutcome.TargetSupportedStillCompeting)),
                I(Count(evaluations, ExactCompletionContextOutcome.TargetResolvedUnique)),
                I(Count(evaluations, ExactCompletionContextOutcome.UniqueWrongCompletion)),
                I(evaluations.Sum(x => x.ContextualIndependentWitnessCount)),
                F(Rate(evaluations.Count(x => x.Outcome != ExactCompletionContextOutcome.NoComparableExactContext),
                    evaluations.Length)), F(Rate(evaluations.Count(x => x.ExactTargetSupported), evaluations.Length)),
                F(Rate(primary.Count(x => x.Outcome == ExactCompletionContextOutcome.TargetResolvedUnique),
                    primary.Length))));
        }
    }

    private static IEnumerable<ExactContextViewEvaluation> Evaluations(IEnumerable<EvaluatedChart> rows,
        ExactCompletionContextView view) => rows.SelectMany(x => x.Result.Trials)
        .Select(x => x.Views.Single(v => v.View == view));
    private static IEnumerable<ExactContextViewEvaluation> Primary(IEnumerable<EvaluatedChart> rows,
        ExactCompletionContextView view) => rows.SelectMany(x => x.Result.Trials)
        .Where(x => x.BaselineState == ChordCompletionReconstructionState.TargetAmongCompetingCompletions)
        .Select(x => x.Views.Single(v => v.View == view));
    private static int Count(IEnumerable<ExactContextViewEvaluation> rows, ExactCompletionContextOutcome outcome) =>
        rows.Count(x => x.Outcome == outcome);
    private static double? Macro(IEnumerable<EvaluatedChart[]> families, ExactCompletionContextView view,
        Func<ExactContextViewEvaluation, bool> numerator, bool primaryOnly)
    {
        var values = families.Select(family =>
        {
            var trials = family.SelectMany(x => x.Result.Trials)
                .Where(x => !primaryOnly || x.BaselineState ==
                    ChordCompletionReconstructionState.TargetAmongCompetingCompletions).ToArray();
            var evaluations = trials.Select(x => x.Views.Single(v => v.View == view)).ToArray();
            return Rate(evaluations.Count(numerator), evaluations.Length);
        }).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return values.Length == 0 ? null : values.Average();
    }

    private static string ScopeHeader() => "family_key,relative_path,version,key_count,charts,view,total_trials,baseline_competing_trials,primary_baseline_alternatives_sum,primary_contextual_alternatives_sum,primary_baseline_alternatives_mean,primary_contextual_alternatives_mean,no_comparable_exact_context,comparable_target_unsupported,target_supported_still_competing,target_resolved_unique,unique_wrong_completion,primary_no_comparable_exact_context,primary_comparable_target_unsupported,primary_target_supported_still_competing,primary_target_resolved_unique,primary_unique_wrong_completion,exact_target_supported,lane_supported_but_type_different,contextual_witness_references,target_group_leakage,future_held_leakage,held_tail_encoded_as_head,donor_nesting_violations,beat_ms_materialization_differences,context_build_ms,exact_match_ms,detailed_artifact_bytes,micro_coverage_rate,micro_target_supported_rate,primary_resolution_rate,macro_family_coverage_rate,macro_family_primary_resolution_rate";
    private static C11CorpusChartDescriptor Public(C11CorpusChartDescriptor source) => source with { RuntimePath = "" };
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static double? Rate(int numerator, int denominator) => denominator == 0 ? null : (double)numerator / denominator;
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string F(double? value) => value?.ToString("0.############", CultureInfo.InvariantCulture) ?? "N/A";
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private enum Scope { Chart, Family, Global }
    private sealed record EvaluatedChart(C11CorpusChartDescriptor Chart,
        ExactCompletionContextResearchResult Result, int DetailArtifactBytes);
    private sealed record DetailArtifact(C11CorpusDiscoveryResult Discovery,
        IReadOnlyList<EvaluatedChart> Evaluations);
}
