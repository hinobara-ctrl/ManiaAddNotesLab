using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class C11CorpusRunner
{
    public static void Run(string corpusRoot, string publicOutputDirectory, string localDetailJson)
    {
        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        var evaluated = new List<EvaluatedChart>();
        foreach (var descriptor in discovery.UniqueHumanCharts)
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            foreach (var mode in Enum.GetValues<HeldOutExclusionMode>())
            {
                var result = LnWitnessAgreementResearch.Evaluate(chart, mode);
                evaluated.Add(new EvaluatedChart(Public(descriptor), result));
                Console.WriteLine($"{descriptor.RelativePath}: {mode}, targets={result.TargetsEvaluated}, " +
                    $"legal={result.Targets.Count(x => x.TrueTargetLegalCandidatePresent)}, " +
                    $"agreements={result.Agreements.Length}");
            }
        }

        Directory.CreateDirectory(publicOutputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(localDetailJson)!);
        WriteInventory(Path.Combine(publicOutputDirectory, "MAP_FAMILY_VALIDATION_INVENTORY.md"), discovery,
            evaluated);
        WriteChartSummary(Path.Combine(publicOutputDirectory, "c1_1_chart_summary.csv"), evaluated);
        WriteFamilySummary(Path.Combine(publicOutputDirectory, "c1_1_family_summary.csv"), evaluated);
        WriteAgreementSummary(Path.Combine(publicOutputDirectory, "c1_1_agreement_summary.csv"), evaluated);
        File.WriteAllText(localDetailJson, JsonSerializer.Serialize(new DetailArtifact(discovery with
        {
            UniqueHumanCharts = discovery.UniqueHumanCharts.Select(Public).ToImmutableArray()
        }, evaluated), JsonOptions()), new UTF8Encoding(false));
    }

    private static C11CorpusChartDescriptor Public(C11CorpusChartDescriptor source) =>
        source with { RuntimePath = "" };

    private static void WriteInventory(string path, C11CorpusDiscoveryResult result,
        IReadOnlyList<EvaluatedChart> evaluated)
    {
        var text = new StringBuilder()
            .AppendLine("# Phase C1.1 — Inventario de familias de validación")
            .AppendLine()
            .AppendLine("Este inventario se genera de forma reproducible y de solo lectura. Los archivos con `[ADD …]` son salidas sintéticas y se excluyen de la validación humana. Los duplicados binarios se colapsan por SHA-256.")
            .AppendLine()
            .AppendLine($"- Archivos `.osu`: {result.OsuFileCount}")
            .AppendLine($"- Archivos mania válidos: {result.ValidManiaFileCount}")
            .AppendLine($"- Archivos inválidos/no mania: {result.InvalidFileCount}")
            .AppendLine($"- Salidas sintéticas excluidas: {result.GeneratedOutputCount}")
            .AppendLine($"- Ubicaciones originales humanas: {result.HumanOriginalLocationCount}")
            .AppendLine($"- Ubicaciones duplicadas exactas: {result.ExactDuplicateLocationCount}")
            .AppendLine($"- Charts humanos únicos: {result.UniqueHumanCharts.Length}")
            .AppendLine($"- Familias: {result.UniqueHumanCharts.Select(x => x.FamilyKey).Distinct().Count()}")
            .AppendLine()
            .AppendLine("Una familia se define por `Artist + Title + Creator` normalizados; la dificultad (`Version`) se conserva como chart separado. En este corpus cada familia tiene una sola dificultad única.")
            .AppendLine()
            .AppendLine("| Familia | Dificultad | K | Objetos | Taps | LN | Timing | BPM | Dup. paths | Candidates afectados | Copias | Ruta relativa |")
            .AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach (var chart in result.UniqueHumanCharts)
        {
            var loo = evaluated.Single(x => x.Chart.Sha256 == chart.Sha256 &&
                x.Result.ExclusionMode == HeldOutExclusionMode.LeaveOneObjectOut).Result;
            var duplicateRate = loo.EvidencePathCount == 0 ? 0 :
                (double)loo.DuplicateEvidencePathCount / loo.EvidencePathCount;
            text.AppendLine($"| {Cell(chart.FamilyKey)} | {Cell(chart.Version)} | {chart.KeyCount} | " +
                $"{chart.ObjectCount} | {chart.TapCount} | {chart.LongNoteCount} | {chart.TimingPointCount} | " +
                $"{F(chart.MinimumBpm)}–{F(chart.MaximumBpm)} | {F(duplicateRate)} | " +
                $"{loo.CandidatesWithDuplicateWitness} | {chart.DuplicateRelativePaths.Length} | " +
                $"{Cell(chart.RelativePath)} |");
        }
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static void WriteChartSummary(string path, IReadOnlyList<EvaluatedChart> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine(SummaryHeader());
        foreach (var row in rows) WriteSummary(writer, row.Chart.FamilyKey, row.Chart.RelativePath,
            row.Chart.Version, row.Chart.KeyCount, row.Result.ExclusionMode, [row.Result]);
    }

    private static void WriteFamilySummary(string path, IReadOnlyList<EvaluatedChart> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine(SummaryHeader());
        foreach (var group in rows.GroupBy(x => new { x.Chart.FamilyKey, x.Result.ExclusionMode }))
        {
            var keyCounts = group.Select(x => x.Chart.KeyCount).Distinct().ToArray();
            WriteSummary(writer, group.Key.FamilyKey, "", "ALL",
                keyCounts.Length == 1 ? keyCounts[0] : 0, group.Key.ExclusionMode,
                group.Select(x => x.Result).ToArray());
        }
    }

    private static void WriteAgreementSummary(string path, IReadOnlyList<EvaluatedChart> rows)
    {
        using var writer = Writer(path);
        writer.WriteLine("scope,mode,charts,families,targets,micro_target_present_rate,macro_chart_target_present_rate,micro_legal_any_lane_rate,macro_chart_legal_any_lane_rate,agreements,same_head,materialization,other,beat_exact,millisecond_exact,tolerance_only,mean_delta_micro,mean_delta_macro_chart");
        foreach (var mode in Enum.GetValues<HeldOutExclusionMode>())
        {
            var selected = rows.Where(x => x.Result.ExclusionMode == mode).ToArray();
            WriteAgreementScope(writer, "global", mode, selected);
            WriteAgreementScope(writer, "structural_twin", mode, selected, x => x.HasExactStructuralTwin);
            WriteAgreementScope(writer, "no_structural_twin", mode, selected, x => !x.HasExactStructuralTwin);
            WriteAgreementScope(writer, "same_head_target", mode, selected,
                x => x.TargetAgreementClass == TargetAgreementClass.SameHeadAgreement);
            WriteAgreementScope(writer, "no_duplicate_target", mode, selected,
                x => x.TargetAgreementClass == TargetAgreementClass.NoDuplicateWitness);
        }
    }

    private static void WriteSummary(StreamWriter writer, string family, string relative, string version,
        int keys, HeldOutExclusionMode mode, IReadOnlyList<C11HeldOutValidationResult> results)
    {
        var targets = results.SelectMany(x => x.Targets).ToArray();
        var deltas = targets.Where(x => x.DeltaNormalizedWeight.HasValue).Select(x => x.DeltaNormalizedWeight!.Value).ToArray();
        var logs = targets.Where(x => x.LogProbabilityRatio.HasValue).Select(x => x.LogProbabilityRatio!.Value).ToArray();
        var legacyNorm = targets.Where(x => x.LegacyTargetNormalizedWeight.HasValue).Select(x => x.LegacyTargetNormalizedWeight!.Value).ToArray();
        var uniqueNorm = targets.Where(x => x.UniqueWitnessTargetNormalizedWeight.HasValue).Select(x => x.UniqueWitnessTargetNormalizedWeight!.Value).ToArray();
        var legacyRanks = targets.Where(x => x.LegacyGeometryRank.HasValue).Select(x => x.LegacyGeometryRank!.Value).ToArray();
        var uniqueRanks = targets.Where(x => x.UniqueWitnessGeometryRank.HasValue).Select(x => x.UniqueWitnessGeometryRank!.Value).ToArray();
        writer.WriteLine(string.Join(',', Csv(family), Csv(relative), Csv(version), keys, mode,
            targets.Length, targets.Count(x => x.TrueTargetCandidatePresent),
            targets.Count(x => x.OriginalEndpointHasAnyLegalLane),
            targets.Count(x => x.OriginalEndpointIsLegalInTargetLane), targets.Count(x => x.HasExactStructuralTwin),
            targets.Count(x => x.TargetAgreementClass == TargetAgreementClass.NoDuplicateWitness),
            targets.Count(x => x.TargetAgreementClass == TargetAgreementClass.SameHeadAgreement),
            targets.Count(x => x.TargetAgreementClass == TargetAgreementClass.MaterializationCoincidence),
            targets.Count(x => x.TargetAgreementClass == TargetAgreementClass.OtherConvergence),
            targets.Count(x => x.TargetAgreementClass == TargetAgreementClass.MultipleCauses),
            targets.Count(x => x.PairedOutcome == PairedWeightOutcome.Improved),
            targets.Count(x => x.PairedOutcome == PairedWeightOutcome.Worsened),
            targets.Count(x => x.PairedOutcome == PairedWeightOutcome.Tie),
            targets.Count(x => x.PairedOutcome == PairedWeightOutcome.NotEvaluable),
            F(deltas.Length == 0 ? null : deltas.Average()), F(logs.Length == 0 ? null : logs.Average()),
            targets.Count(x => x.LegacyGeometryRank == 1), targets.Count(x => x.UniqueWitnessGeometryRank == 1),
            F(legacyRanks.Length == 0 ? null : legacyRanks.Average()),
            F(uniqueRanks.Length == 0 ? null : uniqueRanks.Average()),
            F(legacyNorm.Length == 0 ? null : legacyNorm.Average()),
            F(uniqueNorm.Length == 0 ? null : uniqueNorm.Average()),
            results.Sum(x => x.Agreements.Length), results.Sum(x => x.EvidencePathCount),
            results.Sum(x => x.IndependentWitnessCount), results.Sum(x => x.DuplicateEvidencePathCount),
            F(results.Sum(x => x.ElapsedMilliseconds))));
    }

    private static void WriteAgreementScope(StreamWriter writer, string scope,
        HeldOutExclusionMode mode, IReadOnlyList<EvaluatedChart> rows,
        Func<C11TargetValidation, bool>? targetFilter = null)
    {
        targetFilter ??= _ => true;
        var targets = rows.SelectMany(x => x.Result.Targets).Where(targetFilter).ToArray();
        var isGlobal = scope == "global";
        var agreements = isGlobal ? rows.SelectMany(x => x.Result.Agreements).ToArray() : [];
        var agreementCount = isGlobal ? agreements.Length : targets.Sum(x =>
            x.TargetSameHeadAgreements + x.TargetMaterializationCoincidences + x.TargetOtherConvergences);
        var sameHeadCount = isGlobal ? agreements.Count(x => x.AgreementKind == WitnessAgreementKind.SameHeadAgreement)
            : targets.Sum(x => x.TargetSameHeadAgreements);
        var materializationCount = isGlobal ? agreements.Count(x => x.AgreementKind == WitnessAgreementKind.MaterializationCoincidence)
            : targets.Sum(x => x.TargetMaterializationCoincidences);
        var otherCount = isGlobal ? agreements.Count(x => x.AgreementKind == WitnessAgreementKind.OtherConvergence)
            : targets.Sum(x => x.TargetOtherConvergences);
        var targetDeltas = targets.Where(x => x.DeltaNormalizedWeight.HasValue).Select(x => x.DeltaNormalizedWeight!.Value).ToArray();
        var applicableRows = rows.Where(x => x.Result.Targets.Any(targetFilter)).ToArray();
        var chartDelta = applicableRows.Select(x => x.Result.Targets.Where(targetFilter)
            .Where(t => t.DeltaNormalizedWeight.HasValue)
            .Select(t => t.DeltaNormalizedWeight!.Value).ToArray()).Where(x => x.Length > 0)
            .Select(x => x.Average()).ToArray();
        writer.WriteLine(string.Join(',', scope, mode, applicableRows.Length,
            applicableRows.Select(x => x.Chart.FamilyKey).Distinct().Count(),
            targets.Length, F(Rate(targets.Count(x => x.TrueTargetCandidatePresent), targets.Length)),
            F(applicableRows.Length == 0 ? null : applicableRows.Average(x =>
                Rate(x.Result.Targets.Count(t => targetFilter(t) && t.TrueTargetCandidatePresent),
                    x.Result.Targets.Count(targetFilter)))),
            F(Rate(targets.Count(x => x.OriginalEndpointHasAnyLegalLane), targets.Length)),
            F(applicableRows.Length == 0 ? null : applicableRows.Average(x =>
                Rate(x.Result.Targets.Count(t => targetFilter(t) && t.OriginalEndpointHasAnyLegalLane),
                    x.Result.Targets.Count(targetFilter)))),
            agreementCount, sameHeadCount, materializationCount, otherCount,
            isGlobal ? agreements.Count(x => x.BeatSpaceExactEquality).ToString(CultureInfo.InvariantCulture) : "",
            isGlobal ? agreements.Count(x => x.MillisecondEquality).ToString(CultureInfo.InvariantCulture) : "",
            isGlobal ? agreements.Count(x => x.EqualityOnlyThroughTolerance).ToString(CultureInfo.InvariantCulture) : "",
            F(targetDeltas.Length == 0 ? null : targetDeltas.Average()),
            F(chartDelta.Length == 0 ? null : chartDelta.Average())));
    }

    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static string SummaryHeader() => "family_key,relative_path,version,key_count,mode,targets,target_present,legal_target_any_lane,legal_target_original_lane,structural_twins,no_duplicate,same_head,materialization,other,multiple,improved,worsened,tie,not_evaluable,mean_delta,mean_log_ratio,legacy_top1,unique_top1,legacy_mean_rank,unique_mean_rank,legacy_mean_normalized_weight,unique_mean_normalized_weight,agreements,evidence_paths,independent_witnesses,duplicate_paths,elapsed_ms";
    private static double Rate(int numerator, int denominator) => denominator == 0 ? 0 : (double)numerator / denominator;
    private static string F(double? value) => value?.ToString("0.############", CultureInfo.InvariantCulture) ?? "";
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string Cell(string value) => value.Replace("|", "\\|");
    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private sealed record EvaluatedChart(C11CorpusChartDescriptor Chart, C11HeldOutValidationResult Result);
    private sealed record DetailArtifact(C11CorpusDiscoveryResult Discovery, IReadOnlyList<EvaluatedChart> Evaluations);
}
