using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class C12CorpusRunner
{
    public static void Run(string corpusRoot, string publicOutputDirectory, string localDetailJson)
    {
        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        var rows = new List<EvaluatedChart>();
        foreach (var descriptor in discovery.UniqueHumanCharts)
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var c11 = LnWitnessAgreementResearch.Evaluate(chart, HeldOutExclusionMode.LeaveOneObjectOut);
            var comparison = new PriorTargetComparison(c11.ChartFingerprint,
                c11.Targets.Where(x => x.PairedOutcome == PairedWeightOutcome.Worsened)
                    .Select(x => x.TargetObservationId).ToImmutableHashSet());
            var result = ExactHeadRelationResearch.Evaluate(chart, comparison);
            rows.Add(new EvaluatedChart(Public(descriptor), result, c11.ElapsedMilliseconds));
            Console.WriteLine($"{descriptor.RelativePath}: groups={result.ExactHeadGroups.Length}, " +
                $"relations={result.RelationCount}, targets={result.HeldOutTargets.Length}, " +
                $"comparable={result.HeldOutTargets.Count(x => x.State != HeldOutRelationState.NoSameHeadEvidence)}, " +
                $"supported={result.HeldOutTargets.Count(x => x.ExactTargetEndpointSupported)}");
        }

        Directory.CreateDirectory(publicOutputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(localDetailJson)!);
        WriteChartSummary(Path.Combine(publicOutputDirectory, "c1_2_chart_summary.csv"), rows);
        WriteFamilySummary(Path.Combine(publicOutputDirectory, "c1_2_family_summary.csv"), rows);
        WriteGlobalSummary(Path.Combine(publicOutputDirectory, "c1_2_global_summary.csv"), rows);
        var safeDiscovery = discovery with
        {
            UniqueHumanCharts = discovery.UniqueHumanCharts.Select(Public).ToImmutableArray()
        };
        File.WriteAllText(localDetailJson,
            JsonSerializer.Serialize(new DetailArtifact(safeDiscovery, rows), JsonOptions()),
            new UTF8Encoding(false));
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
        foreach (var group in rows.GroupBy(x => x.Chart.FamilyKey))
        {
            var keyCounts = group.Select(x => x.Chart.KeyCount).Distinct().ToArray();
            WriteRow(writer, group.Key, "", "ALL", keyCounts.Length == 1 ? keyCounts[0] : 0,
                group.ToArray());
        }
    }

    private static void WriteGlobalSummary(string path, IReadOnlyList<EvaluatedChart> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine(Header() + ",macro_family_comparable_rate,macro_family_supported_rate,macro_family_unique_supported_rate,macro_family_competing_supported_rate");
        WriteRow(writer, "GLOBAL", "", "ALL", 0, rows,
            Macro(rows, x => x.State != HeldOutRelationState.NoSameHeadEvidence),
            Macro(rows, x => x.ExactTargetEndpointSupported),
            Macro(rows, x => x.ExactTargetEndpointSupported && x.State == HeldOutRelationState.SingleEndpointRelation),
            Macro(rows, x => x.ExactTargetEndpointSupported && x.State == HeldOutRelationState.CompetingEndpointRelations));
    }

    private static void WriteRow(StreamWriter writer, string family, string relative, string version, int keys,
        IReadOnlyList<EvaluatedChart> rows, params double?[] extra)
    {
        var results = rows.Select(x => x.Result).ToArray();
        var groups = results.SelectMany(x => x.ExactHeadGroups).ToArray();
        var targets = results.SelectMany(x => x.HeldOutTargets).ToArray();
        var comparable = targets.Count(x => x.State != HeldOutRelationState.NoSameHeadEvidence);
        var supported = targets.Count(x => x.ExactTargetEndpointSupported);
        var unique = targets.Count(x => x.ExactTargetEndpointSupported && x.State == HeldOutRelationState.SingleEndpointRelation);
        var competing = targets.Count(x => x.ExactTargetEndpointSupported && x.State == HeldOutRelationState.CompetingEndpointRelations);
        var twins = targets.Where(x => x.HasExactStructuralTwin).ToArray();
        var worsened = targets.Where(x => x.WasWorsenedByC11UniqueWitness).ToArray();
        var fields = new List<string>
        {
            Csv(family), Csv(relative), Csv(version), keys.ToString(CultureInfo.InvariantCulture),
            results.Length.ToString(CultureInfo.InvariantCulture),
            results.Sum(x => x.OriginalObjectCount).ToString(CultureInfo.InvariantCulture),
            targets.Length.ToString(CultureInfo.InvariantCulture), groups.Length.ToString(CultureInfo.InvariantCulture),
            groups.Count(x => x.ReleaseStructure == LnReleaseStructure.OneLongNote).ToString(CultureInfo.InvariantCulture),
            groups.Count(x => x.ReleaseStructure == LnReleaseStructure.MultipleLongNotesSameRelease).ToString(CultureInfo.InvariantCulture),
            groups.Count(x => x.ReleaseStructure == LnReleaseStructure.MultipleLongNotesDifferentReleases).ToString(CultureInfo.InvariantCulture),
            groups.Count(x => x.HasMixedObjectTypes).ToString(CultureInfo.InvariantCulture),
            groups.Count(x => x.LongNoteMemberCount > 1).ToString(CultureInfo.InvariantCulture),
            groups.Count(x => x.DistinctEndpointCount == 1).ToString(CultureInfo.InvariantCulture),
            groups.Count(x => x.DistinctEndpointCount > 1).ToString(CultureInfo.InvariantCulture),
            groups.Sum(x => x.EndpointRelations.Length).ToString(CultureInfo.InvariantCulture),
            groups.Sum(x => x.DistinctWitnessCount).ToString(CultureInfo.InvariantCulture),
            groups.Sum(x => x.EndpointRelations.Count(r => r.IndependentWitnessCount > 1)).ToString(CultureInfo.InvariantCulture),
            groups.Sum(x => x.EndpointRelations.Sum(r => Math.Max(0, r.IndependentWitnessCount - 1))).ToString(CultureInfo.InvariantCulture),
            comparable.ToString(CultureInfo.InvariantCulture), supported.ToString(CultureInfo.InvariantCulture),
            unique.ToString(CultureInfo.InvariantCulture), competing.ToString(CultureInfo.InvariantCulture),
            targets.Count(x => x.State == HeldOutRelationState.NoSameHeadEvidence).ToString(CultureInfo.InvariantCulture),
            twins.Length.ToString(CultureInfo.InvariantCulture), twins.Count(x => x.ExactTargetEndpointSupported).ToString(CultureInfo.InvariantCulture),
            worsened.Length.ToString(CultureInfo.InvariantCulture), worsened.Count(x => x.ExactTargetEndpointSupported).ToString(CultureInfo.InvariantCulture),
            F(Rate(comparable, targets.Length)), F(Rate(supported, targets.Length)),
            F(Rate(supported, comparable)), F(results.Sum(x => x.ElapsedMilliseconds)),
            F(rows.Sum(x => x.C11ElapsedMilliseconds))
        };
        fields.AddRange(extra.Select(F));
        writer.WriteLine(string.Join(',', fields));
    }

    private static double? Macro(IReadOnlyList<EvaluatedChart> rows,
        Func<HeldOutExactHeadRelationResult, bool> predicate)
    {
        var families = rows.GroupBy(x => x.Chart.FamilyKey)
            .Select(group => group.SelectMany(x => x.Result.HeldOutTargets).ToArray())
            .Where(x => x.Length > 0).ToArray();
        return families.Length == 0 ? null : families.Average(x => (double)x.Count(predicate) / x.Length);
    }

    private static string Header() => "family_key,relative_path,version,key_count,charts,objects,ln_targets,exact_head_groups,one_ln_groups,multiple_ln_same_release_groups,multiple_ln_different_release_groups,mixed_object_type_groups,multi_ln_groups,one_endpoint_groups,competing_endpoint_groups,relations,independent_relation_witnesses,repeated_relations,repeated_relation_witnesses,comparable_targets,target_endpoint_supported,uniquely_supported,supported_among_competing,no_comparable,structural_twins,structural_twins_supported,c11_unique_worsened,c11_unique_worsened_supported,micro_comparable_rate,micro_supported_all_rate,micro_supported_comparable_rate,relation_model_elapsed_ms,c11_comparison_elapsed_ms";
    private static C11CorpusChartDescriptor Public(C11CorpusChartDescriptor source) => source with { RuntimePath = "" };
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static double? Rate(int numerator, int denominator) => denominator == 0 ? null : (double)numerator / denominator;
    private static string F(double? value) => value?.ToString("0.############", CultureInfo.InvariantCulture) ?? "";
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private sealed record EvaluatedChart(C11CorpusChartDescriptor Chart, ExactHeadRelationResearchResult Result,
        double C11ElapsedMilliseconds);
    private sealed record DetailArtifact(C11CorpusDiscoveryResult Discovery, IReadOnlyList<EvaluatedChart> Evaluations);
}
