using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class F2CorpusRunner
{
    public static void Run(string corpusRoot, string outputDirectory, string detailPath)
    {
        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(detailPath)!);
        var charts = new List<ChartData>();
        using var stream = File.Create(detailPath);
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("researchSchemaVersion", TypedGapBackoffResearch.ResearchSchemaVersion);
        writer.WritePropertyName("charts");
        writer.WriteStartArray();
        foreach (var descriptor in discovery.UniqueHumanCharts)
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var result = TypedGapBackoffResearch.Evaluate(chart);
            writer.WriteStartObject();
            writer.WriteString("familyKey", descriptor.FamilyKey);
            writer.WriteString("relativePath", descriptor.RelativePath);
            writer.WriteString("version", descriptor.Version);
            writer.WritePropertyName("result");
            JsonSerializer.Serialize(writer, result, JsonOptions());
            writer.WriteEndObject();
            writer.Flush();
            charts.Add(new ChartData(descriptor.FamilyKey, descriptor.RelativePath, descriptor.Version,
                chart.KeyCount, result));
            Console.WriteLine($"{descriptor.RelativePath}: typed-gap candidates={result.CandidateCount}");
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        WriteSummary(Path.Combine(outputDirectory, "f2_chart_summary.csv"), charts.Select(x =>
            new ScopeData(x.Family, x.Relative, x.Version, x.Keys, [x])));
        WriteSummary(Path.Combine(outputDirectory, "f2_family_summary.csv"), charts.GroupBy(x => x.Family)
            .OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => new ScopeData(x.Key, "", "ALL", 0, x.ToArray())));
        WriteSummary(Path.Combine(outputDirectory, "f2_global_summary.csv"),
            [new ScopeData("GLOBAL", "", "ALL", 0, charts)]);
        WriteStates(Path.Combine(outputDirectory, "f2_evidence_state_summary.csv"), charts);
        WriteTransitions(Path.Combine(outputDirectory, "f2_transition_summary.csv"), charts);
        WriteGaps(Path.Combine(outputDirectory, "f2_gap_summary.csv"), charts);
        WriteBackoff(Path.Combine(outputDirectory, "f2_backoff_summary.csv"), charts);
        WriteSkips(Path.Combine(outputDirectory, "f2_skip_summary.csv"), charts);
        WriteStrata(Path.Combine(outputDirectory, "f2_stratification_summary.csv"), charts);
        Console.WriteLine($"Wrote F2 shadow summaries for {charts.Count} charts; detail={stream.Length} bytes.");
    }

    private static void WriteSummary(string path, IEnumerable<ScopeData> scopes)
    {
        using var w = Writer(path);
        w.WriteLine("family_key,relative_path,version,key_count,charts,typed_gap_candidates,local_support,local_mismatch,local_no_context,local_ambiguous,backoff_attempts,admit_local,admit_global,final_skip,supporting_donor_references,contradicting_donor_references,target_group_leakage,synthetic_teaching,held_tail_as_head");
        foreach (var s in scopes)
        {
            var resolutions = s.Charts.SelectMany(x => x.Result.Resolutions).ToArray();
            int State(ComparableEvidenceState state) => resolutions.Count(x => x.LocalEvidence.EvidenceState == state);
            int Action(TypedGapBackoffAction action) => resolutions.Count(x => x.FinalAction == action);
            w.WriteLine(string.Join(',', Csv(s.Family), Csv(s.Relative), Csv(s.Version), I(s.Keys), I(s.Charts.Count),
                I(resolutions.Length), I(State(ComparableEvidenceState.ObservedSupport)),
                I(State(ComparableEvidenceState.LocalMismatch)), I(State(ComparableEvidenceState.NoComparableContext)),
                I(State(ComparableEvidenceState.AmbiguousEvidence)),
                I(resolutions.Count(x => x.BackoffTrace.Length == 2)), I(Action(TypedGapBackoffAction.AdmitLocalSupport)),
                I(Action(TypedGapBackoffAction.AdmitGlobalSupport)), I(resolutions.Count(x => IsSkip(x.FinalAction))),
                I(resolutions.Sum(x => x.LocalEvidence.SupportingDonors.Length + (x.GlobalEvidence?.SupportingDonors.Length ?? 0))),
                I(resolutions.Sum(x => x.LocalEvidence.ContradictingDonors.Length + (x.GlobalEvidence?.ContradictingDonors.Length ?? 0))),
                I(s.Charts.Sum(x => x.Result.TargetGroupLeakageCount)),
                I(s.Charts.Sum(x => x.Result.SyntheticTeachingCount)),
                I(s.Charts.Sum(x => x.Result.HeldTailEncodedAsHeadCount))));
        }
    }

    private static void WriteStates(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("scope,family_key,evidence_scope,evidence_state,resolutions");
        foreach (var scope in FamilyAndGlobal(charts))
        foreach (var item in scope.Charts.SelectMany(x => x.Result.Resolutions)
                     .SelectMany(x => x.GlobalEvidence is null ? [x.LocalEvidence]
                         : new[] { x.LocalEvidence, x.GlobalEvidence })
                     .GroupBy(x => (x.Scope, x.EvidenceState)).OrderBy(x => x.Key.Scope).ThenBy(x => x.Key.EvidenceState))
            w.WriteLine(string.Join(',', scope.Scope, Csv(scope.Family), item.Key.Scope,
                item.Key.EvidenceState, I(item.Count())));
    }

    private static void WriteTransitions(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("scope,family_key,transition_kind,local_state,final_action,resolutions");
        foreach (var scope in FamilyAndGlobal(charts))
        foreach (var item in scope.Charts.SelectMany(x => x.Result.Resolutions)
                     .GroupBy(x => (x.Candidate.TransitionKind, x.LocalEvidence.EvidenceState, x.FinalAction))
                     .OrderBy(x => x.Key.TransitionKind).ThenBy(x => x.Key.EvidenceState).ThenBy(x => x.Key.FinalAction))
            w.WriteLine(string.Join(',', scope.Scope, Csv(scope.Family), item.Key.TransitionKind,
                item.Key.EvidenceState, item.Key.FinalAction, I(item.Count())));
    }

    private static void WriteGaps(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("transition_kind,exact_gap_beats,local_state,final_action,resolutions");
        foreach (var item in charts.SelectMany(x => x.Result.Resolutions)
                     .GroupBy(x => (x.Candidate.TransitionKind, x.Candidate.ExactGapBeats,
                         x.LocalEvidence.EvidenceState, x.FinalAction))
                     .OrderBy(x => x.Key.TransitionKind).ThenBy(x => x.Key.ExactGapBeats)
                     .ThenBy(x => x.Key.EvidenceState).ThenBy(x => x.Key.FinalAction))
            w.WriteLine(string.Join(',', item.Key.TransitionKind, D(item.Key.ExactGapBeats),
                item.Key.EvidenceState, item.Key.FinalAction, I(item.Count())));
    }

    private static void WriteBackoff(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("scope,family_key,global_state,final_action,resolutions");
        foreach (var scope in FamilyAndGlobal(charts))
        foreach (var item in scope.Charts.SelectMany(x => x.Result.Resolutions)
                     .Where(x => x.BackoffTrace.Length == 2)
                     .GroupBy(x => (x.GlobalEvidence!.EvidenceState, x.FinalAction))
                     .OrderBy(x => x.Key.EvidenceState).ThenBy(x => x.Key.FinalAction))
            w.WriteLine(string.Join(',', scope.Scope, Csv(scope.Family), item.Key.EvidenceState,
                item.Key.FinalAction, I(item.Count())));
    }

    private static void WriteSkips(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("scope,family_key,skip_reason,resolutions");
        foreach (var scope in FamilyAndGlobal(charts))
        foreach (var item in scope.Charts.SelectMany(x => x.Result.Resolutions).Where(x => IsSkip(x.FinalAction))
                     .GroupBy(x => x.FinalAction).OrderBy(x => x.Key))
            w.WriteLine(string.Join(',', scope.Scope, Csv(scope.Family), item.Key, I(item.Count())));
    }

    private static void WriteStrata(string path, IReadOnlyList<ChartData> charts)
    {
        using var w = Writer(path);
        w.WriteLine("key_count,transition_kind,local_state,global_state,final_action,resolutions");
        foreach (var item in charts.SelectMany(chart => chart.Result.Resolutions.Select(x => (chart.Keys, Value: x)))
                     .GroupBy(x => (x.Keys, x.Value.Candidate.TransitionKind,
                         LocalState: x.Value.LocalEvidence.EvidenceState,
                         GlobalState: x.Value.GlobalEvidence?.EvidenceState, x.Value.FinalAction))
                     .OrderBy(x => x.Key.Keys).ThenBy(x => x.Key.TransitionKind).ThenBy(x => x.Key.LocalState)
                     .ThenBy(x => x.Key.GlobalState).ThenBy(x => x.Key.FinalAction))
            w.WriteLine(string.Join(',', I(item.Key.Keys), item.Key.TransitionKind, item.Key.LocalState,
                item.Key.GlobalState, item.Key.FinalAction, I(item.Count())));
    }

    private static IEnumerable<(string Scope, string Family, IReadOnlyList<ChartData> Charts)> FamilyAndGlobal(
        IReadOnlyList<ChartData> charts)
    {
        foreach (var family in charts.GroupBy(x => x.Family).OrderBy(x => x.Key, StringComparer.Ordinal))
            yield return ("family", family.Key, family.ToArray());
        yield return ("global", "GLOBAL", charts);
    }

    private static bool IsSkip(TypedGapBackoffAction action) => action.ToString().StartsWith("Skip", StringComparison.Ordinal);
    private sealed record ChartData(string Family, string Relative, string Version, int Keys,
        TypedGapBackoffResearchResult Result);
    private sealed record ScopeData(string Family, string Relative, string Version, int Keys,
        IReadOnlyList<ChartData> Charts);
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
