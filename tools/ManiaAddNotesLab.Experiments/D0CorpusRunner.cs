using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class D0CorpusRunner
{
    public static void Run(string corpusRoot, string publicOutputDirectory, string localDetailJson)
    {
        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        var rows = new List<EvaluatedChart>();
        foreach (var descriptor in discovery.UniqueHumanCharts)
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var result = ChordCompletionResearch.Evaluate(chart);
            var detailBytes = Encoding.UTF8.GetByteCount(ChordCompletionResearchJson.Serialize(result));
            rows.Add(new EvaluatedChart(Public(descriptor), result, detailBytes));
            Console.WriteLine($"{descriptor.RelativePath}: groups={result.ChordGroups.Length}, " +
                $"trials={result.CompletionTrials}, comparable={Comparable(result)}, " +
                $"supported={Supported(result)}");
        }

        Directory.CreateDirectory(publicOutputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(localDetailJson)!);
        WriteChartSummary(Path.Combine(publicOutputDirectory, "d0_chart_summary.csv"), rows);
        WriteFamilySummary(Path.Combine(publicOutputDirectory, "d0_family_summary.csv"), rows);
        WriteGlobalSummary(Path.Combine(publicOutputDirectory, "d0_global_summary.csv"), rows);
        var safeDiscovery = discovery with
        {
            UniqueHumanCharts = discovery.UniqueHumanCharts.Select(Public).ToImmutableArray()
        };
        File.WriteAllText(localDetailJson,
            JsonSerializer.Serialize(new DetailArtifact(safeDiscovery, rows), JsonOptions()),
            new UTF8Encoding(false));
        Console.WriteLine($"Wrote D0 summaries for {rows.Count} charts and " +
            $"{rows.Select(x => x.Chart.FamilyKey).Distinct().Count()} families.");
    }

    private static void WriteChartSummary(string path, IReadOnlyList<EvaluatedChart> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine(Header());
        foreach (var row in rows)
            WriteRow(writer, row.Chart.FamilyKey, row.Chart.RelativePath, row.Chart.Version,
                row.Chart.KeyCount, [row]);
    }

    private static void WriteFamilySummary(string path, IReadOnlyList<EvaluatedChart> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine(Header());
        foreach (var group in rows.GroupBy(x => x.Chart.FamilyKey).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var keyCounts = group.Select(x => x.Chart.KeyCount).Distinct().ToArray();
            WriteRow(writer, group.Key, "", "ALL", keyCounts.Length == 1 ? keyCounts[0] : 0,
                group.ToArray());
        }
    }

    private static void WriteGlobalSummary(string path, IReadOnlyList<EvaluatedChart> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine(Header());
        var families = rows.GroupBy(x => x.Chart.FamilyKey).Select(x => x.ToArray()).ToArray();
        WriteRow(writer, "GLOBAL", "", "ALL", 0, rows,
            Macro(families, Comparable), Macro(families, Supported),
            Macro(families, Unique), Macro(families, Competing));
    }

    private static void WriteRow(StreamWriter writer, string family, string relative, string version,
        int keys, IReadOnlyList<EvaluatedChart> rows, params double?[] macro)
    {
        var results = rows.Select(x => x.Result).ToArray();
        var groups = results.SelectMany(x => x.ChordGroups).ToArray();
        var trials = results.SelectMany(x => x.Trials).ToArray();
        var comparable = trials.Count(x => x.State != ChordCompletionReconstructionState.NoComparableReducedState);
        var supported = trials.Count(x => x.ExactTargetCompletionSupported);
        var unique = trials.Count(x => x.State == ChordCompletionReconstructionState.UniqueExactCompletion);
        var competing = trials.Count(x => x.State == ChordCompletionReconstructionState.TargetAmongCompetingCompletions);
        var unsupported = trials.Count(x => x.State == ChordCompletionReconstructionState.ComparableButTargetUnsupported);
        var tapTrials = trials.Count(x => x.TargetType == OriginalHeadMemberType.TapHead);
        var lnTrials = trials.Length - tapTrials;
        var fields = new List<string>
        {
            Csv(family), Csv(relative), Csv(version), keys.ToString(CultureInfo.InvariantCulture),
            rows.Count.ToString(CultureInfo.InvariantCulture),
            results.Sum(x => x.OriginalObjectCount).ToString(CultureInfo.InvariantCulture),
            results.Sum(x => x.ExactHeadGroupCount).ToString(CultureInfo.InvariantCulture),
            groups.Length.ToString(CultureInfo.InvariantCulture), Csv(HeadCounts(groups)),
            groups.Count(x => x.Members.All(m => m.HeadType == OriginalHeadMemberType.TapHead)).ToString(CultureInfo.InvariantCulture),
            groups.Count(x => x.Members.All(m => m.HeadType == OriginalHeadMemberType.LongNoteHead)).ToString(CultureInfo.InvariantCulture),
            groups.Count(x => x.Members.Select(m => m.HeadType).Distinct().Count() > 1).ToString(CultureInfo.InvariantCulture),
            groups.Count(x => x.HeadState.HeldBeforeHeadLanes.Length > 0).ToString(CultureInfo.InvariantCulture),
            trials.Length.ToString(CultureInfo.InvariantCulture),
            results.Sum(x => x.DistinctReducedStates).ToString(CultureInfo.InvariantCulture),
            results.Sum(x => x.DistinctCompletionRelations).ToString(CultureInfo.InvariantCulture),
            results.Sum(x => x.IndependentRelationWitnesses).ToString(CultureInfo.InvariantCulture),
            results.Sum(x => x.WitnessReferenceCount).ToString(CultureInfo.InvariantCulture),
            results.Sum(x => x.DistinctObservedFullStates).ToString(CultureInfo.InvariantCulture),
            comparable.ToString(CultureInfo.InvariantCulture),
            trials.Count(x => x.State == ChordCompletionReconstructionState.NoComparableReducedState).ToString(CultureInfo.InvariantCulture),
            supported.ToString(CultureInfo.InvariantCulture), unique.ToString(CultureInfo.InvariantCulture),
            competing.ToString(CultureInfo.InvariantCulture), unsupported.ToString(CultureInfo.InvariantCulture),
            trials.Count(x => x.LaneSupportedButTypeDifferent).ToString(CultureInfo.InvariantCulture),
            trials.Count(x => x.ExactHeldContextComparable).ToString(CultureInfo.InvariantCulture),
            trials.Count(x => x.ExactHeldContextTargetSupported).ToString(CultureInfo.InvariantCulture),
            trials.Count(x => x.HeldBeforePresent).ToString(CultureInfo.InvariantCulture),
            trials.Count(x => x.HeldBeforePresent && x.ExactTargetCompletionSupported).ToString(CultureInfo.InvariantCulture),
            trials.Count(x => !x.HeldBeforePresent).ToString(CultureInfo.InvariantCulture),
            trials.Count(x => !x.HeldBeforePresent && x.ExactTargetCompletionSupported).ToString(CultureInfo.InvariantCulture),
            tapTrials.ToString(CultureInfo.InvariantCulture),
            trials.Count(x => x.TargetType == OriginalHeadMemberType.TapHead && x.ExactTargetCompletionSupported).ToString(CultureInfo.InvariantCulture),
            lnTrials.ToString(CultureInfo.InvariantCulture),
            trials.Count(x => x.TargetType == OriginalHeadMemberType.LongNoteHead && x.ExactTargetCompletionSupported).ToString(CultureInfo.InvariantCulture),
            Csv(TrialCounts(trials, x => x.OriginalChordHeadCount, _ => true)),
            Csv(TrialCounts(trials, x => x.OriginalChordHeadCount, x => x.ExactTargetCompletionSupported)),
            Csv(VisibleCounts(trials)),
            Csv(TrialCounts(trials, x => x.ReducedVisibleMemberCount, x => x.ExactTargetCompletionSupported)),
            F(results.Sum(x => x.RelationBuildMilliseconds)),
            F(results.Sum(x => x.HeldOutReconstructionMilliseconds)),
            rows.Sum(x => x.DetailArtifactBytes).ToString(CultureInfo.InvariantCulture),
            F(Rate(comparable, trials.Length)), F(Rate(supported, trials.Length)),
            F(Rate(supported, comparable)), F(Rate(unique, trials.Length)), F(Rate(competing, trials.Length)),
            F(Rate(unsupported, comparable))
        };
        fields.AddRange(Enumerable.Range(0, 4).Select(index => F(index < macro.Length ? macro[index] : null)));
        writer.WriteLine(string.Join(',', fields));
    }

    private static double? Macro(IEnumerable<EvaluatedChart[]> families,
        Func<IEnumerable<ChordCompletionResearchResult>, int> numerator)
    {
        var rates = families.Select(family =>
        {
            var results = family.Select(x => x.Result).ToArray();
            var denominator = results.Sum(x => x.CompletionTrials);
            return Rate(numerator(results), denominator);
        }).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return rates.Length == 0 ? null : rates.Average();
    }

    private static int Comparable(IEnumerable<ChordCompletionResearchResult> results) => results.Sum(Comparable);
    private static int Comparable(ChordCompletionResearchResult result) => result.Trials.Count(x =>
        x.State != ChordCompletionReconstructionState.NoComparableReducedState);
    private static int Supported(IEnumerable<ChordCompletionResearchResult> results) => results.Sum(Supported);
    private static int Supported(ChordCompletionResearchResult result) => result.Trials.Count(x =>
        x.ExactTargetCompletionSupported);
    private static int Unique(IEnumerable<ChordCompletionResearchResult> results) => results.Sum(result =>
        result.Trials.Count(x => x.State == ChordCompletionReconstructionState.UniqueExactCompletion));
    private static int Competing(IEnumerable<ChordCompletionResearchResult> results) => results.Sum(result =>
        result.Trials.Count(x => x.State == ChordCompletionReconstructionState.TargetAmongCompetingCompletions));
    private static string HeadCounts(IEnumerable<OriginalChordGroup> groups) => string.Join(';', groups
        .GroupBy(x => x.Members.Length).OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Count()}"));
    private static string VisibleCounts(IEnumerable<ChordCompletionReconstructionTrial> trials) => string.Join(';', trials
        .GroupBy(x => x.ReducedVisibleMemberCount).OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Count()}"));
    private static string TrialCounts(IEnumerable<ChordCompletionReconstructionTrial> trials,
        Func<ChordCompletionReconstructionTrial, int> key,
        Func<ChordCompletionReconstructionTrial, bool> predicate) => string.Join(';', trials.Where(predicate)
        .GroupBy(key).OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Count()}"));
    private static string Header() => "family_key,relative_path,version,key_count,charts,original_objects,exact_head_groups,chord_groups,chord_groups_by_head_count,tap_only_chord_groups,ln_head_only_chord_groups,mixed_tap_ln_chord_groups,groups_with_held_before,completion_trials,distinct_reduced_states,distinct_completion_relations,independent_relation_witnesses,witness_references,distinct_observed_full_states,comparable_trials,no_comparable_reduced_state,exact_target_completion_supported,unique_exact_completion,target_among_competing_completions,comparable_but_target_unsupported,lane_supported_but_type_different,exact_held_context_comparable,exact_held_context_target_supported,held_present_trials,held_present_supported,held_absent_trials,held_absent_supported,tap_target_trials,tap_target_supported,ln_head_target_trials,ln_head_target_supported,trials_by_original_chord_head_count,supported_by_original_chord_head_count,reduced_visible_member_counts,supported_by_reduced_visible_member_count,relation_build_ms,held_out_reconstruction_ms,detailed_artifact_bytes,micro_comparable_rate,micro_supported_all_rate,micro_supported_comparable_rate,micro_unique_rate,micro_competing_rate,micro_unsupported_comparable_rate,macro_family_comparable_rate,macro_family_supported_rate,macro_family_unique_rate,macro_family_competing_rate";
    private static C11CorpusChartDescriptor Public(C11CorpusChartDescriptor source) => source with { RuntimePath = "" };
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static double? Rate(int numerator, int denominator) => denominator == 0 ? null : (double)numerator / denominator;
    private static string F(double? value) => value?.ToString("0.############", CultureInfo.InvariantCulture) ?? "N/A";
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private sealed record EvaluatedChart(C11CorpusChartDescriptor Chart,
        ChordCompletionResearchResult Result, int DetailArtifactBytes);
    private sealed record DetailArtifact(C11CorpusDiscoveryResult Discovery,
        IReadOnlyList<EvaluatedChart> Evaluations);
}
