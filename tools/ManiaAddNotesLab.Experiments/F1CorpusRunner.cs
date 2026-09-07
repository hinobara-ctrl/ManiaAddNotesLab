using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class F1CorpusRunner
{
    public static void Run(string corpusRoot, string publicOutputDirectory, string localDetailJson)
    {
        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        var charts = new List<ChartAggregate>();
        Directory.CreateDirectory(publicOutputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(localDetailJson)!);
        using var stream = File.Create(localDetailJson);
        using var detail = new Utf8JsonWriter(stream);
        detail.WriteStartObject();
        detail.WriteString("researchSchemaVersion", ComparableContextResolverResearch.ResearchSchemaVersion);
        detail.WritePropertyName("charts");
        detail.WriteStartArray();
        foreach (var descriptor in discovery.UniqueHumanCharts)
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var started = Stopwatch.GetTimestamp();
            var result = ComparableContextResolverResearch.Evaluate(chart);
            var totalMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            detail.WriteStartObject();
            detail.WriteString("familyKey", descriptor.FamilyKey);
            detail.WriteString("relativePath", descriptor.RelativePath);
            detail.WriteString("version", descriptor.Version);
            detail.WritePropertyName("result");
            JsonSerializer.Serialize(detail, result, JsonOptions());
            detail.WriteEndObject();
            detail.Flush();
            var aggregate = ChartAggregate.From(Public(descriptor), result);
            charts.Add(aggregate);
            Console.WriteLine($"{descriptor.RelativePath}: trials={aggregate.Trials}, " +
                $"candidates={aggregate.Candidates}, target={aggregate.TargetCandidates}, " +
                $"resolver={result.ResolutionBuildMilliseconds:0.##}ms total={totalMs:0.##}ms");
        }
        detail.WriteEndArray();
        detail.WriteEndObject();
        detail.Flush();

        WriteScopes(Path.Combine(publicOutputDirectory, "f1_chart_summary.csv"),
            charts.Select(x => (x.Chart.FamilyKey, x.Chart.RelativePath, x.Chart.Version, x.Chart.KeyCount, x)));
        WriteScopes(Path.Combine(publicOutputDirectory, "f1_family_summary.csv"),
            charts.GroupBy(x => x.Chart.FamilyKey).OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => (x.Key, "", "ALL", 0, ChartAggregate.Combine(x))));
        WriteScopes(Path.Combine(publicOutputDirectory, "f1_global_summary.csv"),
            [("GLOBAL", "", "ALL", 0, ChartAggregate.Combine(charts))]);
        WriteState(Path.Combine(publicOutputDirectory, "f1_evidence_state_summary.csv"), charts);
        WriteCoverage(Path.Combine(publicOutputDirectory, "f1_coverage_summary.csv"), charts);
        WriteConflicts(Path.Combine(publicOutputDirectory, "f1_conflict_summary.csv"), charts);
        WriteJoints(Path.Combine(publicOutputDirectory, "f1_joint_context_summary.csv"), charts);
        WriteStrata(Path.Combine(publicOutputDirectory, "f1_stratification_summary.csv"), charts);
        WriteViews(Path.Combine(publicOutputDirectory, "f1_view_evidence_summary.csv"), charts);
        WriteUniqueSupport(Path.Combine(publicOutputDirectory, "f1_unique_support_summary.csv"), charts);
        WriteDependencies(Path.Combine(publicOutputDirectory, "f1_dependency_summary.csv"), charts);
        Console.WriteLine($"Wrote F1 summaries for {charts.Count} charts, " +
            $"{charts.Select(x => x.Chart.FamilyKey).Distinct().Count()} families; detail={stream.Length} bytes.");
    }

    private static void WriteScopes(string path,
        IEnumerable<(string Family, string Relative, string Version, int Keys, ChartAggregate Data)> scopes)
    {
        using var writer = Writer(path);
        writer.WriteLine("family_key,relative_path,version,key_count,charts,trials,candidate_resolutions,target_candidate_resolutions,target_observed_support,target_local_mismatch,target_no_comparable_context,target_ambiguous_evidence,non_target_observed_support,non_target_local_mismatch,non_target_no_comparable_context,non_target_ambiguous_evidence,target_unique_conflicts,target_vs_wrong_conflicts,shared_support_contradiction_donors,disjoint_support_contradiction_resolutions,supporting_donor_references,contradicting_donor_references,target_group_leakage,future_held_leakage,held_tail_encoded_as_head,donor_nesting_violations");
        foreach (var scope in scopes)
        {
            var x = scope.Data;
            writer.WriteLine(string.Join(',', Csv(scope.Family), Csv(scope.Relative), Csv(scope.Version), I(scope.Keys),
                I(x.ChartCount), I(x.Trials), I(x.Candidates), I(x.TargetCandidates),
                I(x.State(true, ComparableEvidenceState.ObservedSupport)),
                I(x.State(true, ComparableEvidenceState.LocalMismatch)),
                I(x.State(true, ComparableEvidenceState.NoComparableContext)),
                I(x.State(true, ComparableEvidenceState.AmbiguousEvidence)),
                I(x.State(false, ComparableEvidenceState.ObservedSupport)),
                I(x.State(false, ComparableEvidenceState.LocalMismatch)),
                I(x.State(false, ComparableEvidenceState.NoComparableContext)),
                I(x.State(false, ComparableEvidenceState.AmbiguousEvidence)),
                I(x.TargetConflicts), I(x.TargetWrongConflicts), I(x.SharedDonors), I(x.DisjointResolutions),
                I(x.SupportDonorReferences), I(x.ContradictionDonorReferences),
                I(x.TargetLeakage), I(x.FutureLeakage), I(x.HeldAsHead), I(x.NestingViolations)));
        }
    }

    private static void WriteState(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("scope,family_key,is_target_candidate,evidence_state,resolutions");
        foreach (var group in FamilyAndGlobal(charts, x => x.States))
        foreach (var item in group.Data)
            writer.WriteLine(string.Join(',', group.Scope, Csv(group.Family), B(item.Key.IsTarget),
                item.Key.State, I(item.Value)));
    }

    private static void WriteCoverage(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("is_target_candidate,coverage_signature,evidence_state,resolutions");
        foreach (var item in Combine(charts.Select(x => x.Coverage)))
            writer.WriteLine(string.Join(',', B(item.Key.IsTarget), item.Key.Coverage, item.Key.State, I(item.Value)));
    }

    private static void WriteConflicts(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("is_target_candidate,ambiguity_cause,resolutions");
        foreach (var item in Combine(charts.Select(x => x.Ambiguities)))
            writer.WriteLine(string.Join(',', B(item.Key.IsTarget), item.Key.Cause, I(item.Value)));
    }

    private static void WriteJoints(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("scope,family_key,is_target_candidate,joint_kind,joint_evidence_state,resolutions");
        foreach (var group in FamilyAndGlobal(charts, x => x.Joints))
        foreach (var item in group.Data)
            writer.WriteLine(string.Join(',', group.Scope, Csv(group.Family), B(item.Key.IsTarget),
                item.Key.Kind, item.Key.State, I(item.Value)));
    }

    private static void WriteStrata(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("is_target_candidate,target_type,original_chord_size,reduced_visible_count,held_before,baseline_completion_count,comparable_view_count,coverage_signature,evidence_state,resolutions");
        foreach (var item in Combine(charts.Select(x => x.Strata)))
        {
            var k = item.Key;
            writer.WriteLine(string.Join(',', B(k.IsTarget), k.TargetType, I(k.ChordSize), I(k.ReducedCount),
                k.Held ? "present" : "absent", I(k.BaselineCount), I(k.ComparableCount), k.Coverage,
                k.State, I(item.Value)));
        }
    }

    private static void WriteViews(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("is_target_candidate,view,disposition,resolutions");
        foreach (var item in Combine(charts.Select(x => x.Views)))
            writer.WriteLine(string.Join(',', B(item.Key.IsTarget), item.Key.View, item.Key.Disposition, I(item.Value)));
    }

    private static void WriteUniqueSupport(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("scope,family_key,is_target_candidate,view,resolutions");
        foreach (var group in FamilyAndGlobal(charts, x => x.UniqueSupportingViews))
        foreach (var item in group.Data)
            writer.WriteLine(string.Join(',', group.Scope, Csv(group.Family), B(item.Key.IsTarget),
                item.Key.View, I(item.Value)));
    }

    private static void WriteDependencies(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("scope,family_key,is_target_candidate,parent_view,child_view,resolutions");
        foreach (var group in FamilyAndGlobal(charts, x => x.Dependencies))
        foreach (var item in group.Data)
            writer.WriteLine(string.Join(',', group.Scope, Csv(group.Family), B(item.Key.IsTarget),
                item.Key.Parent, item.Key.Child, I(item.Value)));
    }

    private static IEnumerable<(string Scope, string Family, SortedDictionary<TKey, int> Data)>
        FamilyAndGlobal<TKey>(IReadOnlyList<ChartAggregate> charts,
            Func<ChartAggregate, Dictionary<TKey, int>> select) where TKey : notnull, IComparable<TKey>
    {
        foreach (var family in charts.GroupBy(x => x.Chart.FamilyKey).OrderBy(x => x.Key, StringComparer.Ordinal))
            yield return ("family", family.Key, Combine(family.Select(select)));
        yield return ("global", "GLOBAL", Combine(charts.Select(select)));
    }

    private static SortedDictionary<TKey, int> Combine<TKey>(IEnumerable<Dictionary<TKey, int>> sources)
        where TKey : notnull
    {
        var result = new SortedDictionary<TKey, int>();
        foreach (var source in sources)
        foreach (var item in source)
            result[item.Key] = result.GetValueOrDefault(item.Key) + item.Value;
        return result;
    }

    private sealed class ChartAggregate
    {
        public required C11CorpusChartDescriptor Chart { get; init; }
        public int ChartCount, Trials, Candidates, TargetCandidates, TargetConflicts, TargetWrongConflicts,
            SharedDonors, DisjointResolutions, SupportDonorReferences, ContradictionDonorReferences,
            TargetLeakage, FutureLeakage, HeldAsHead, NestingViolations;
        public Dictionary<StateKey, int> States { get; } = [];
        public Dictionary<CoverageKey, int> Coverage { get; } = [];
        public Dictionary<AmbiguityKey, int> Ambiguities { get; } = [];
        public Dictionary<JointKey, int> Joints { get; } = [];
        public Dictionary<StrataKey, int> Strata { get; } = [];
        public Dictionary<ViewKey, int> Views { get; } = [];
        public Dictionary<UniqueViewKey, int> UniqueSupportingViews { get; } = [];
        public Dictionary<DependencyKey, int> Dependencies { get; } = [];

        public int State(bool target, ComparableEvidenceState state) => States.GetValueOrDefault(new(target, state));

        public static ChartAggregate From(C11CorpusChartDescriptor chart,
            ComparableContextResolverResearchResult result)
        {
            var x = new ChartAggregate { Chart = chart, ChartCount = 1, Trials = result.TargetTrials,
                Candidates = result.CandidateResolutions, TargetCandidates = result.TargetCandidateResolutions,
                TargetLeakage = result.TargetGroupLeakageCount, FutureLeakage = result.FutureHeldLeakageCount,
                HeldAsHead = result.HeldTailEncodedAsHeadCount, NestingViolations = result.DonorNestingViolationCount };
            foreach (var resolution in result.Resolutions)
            {
                Add(x.States, new StateKey(resolution.IsTargetCandidate, resolution.EvidenceState));
                Add(x.Coverage, new CoverageKey(resolution.IsTargetCandidate,
                    resolution.CoverageSignature, resolution.EvidenceState));
                foreach (var ambiguity in resolution.Ambiguities)
                    Add(x.Ambiguities, new AmbiguityKey(resolution.IsTargetCandidate, ambiguity));
                foreach (var joint in resolution.JointEvidence)
                    Add(x.Joints, new JointKey(resolution.IsTargetCandidate, joint.Kind, joint.State));
                foreach (var view in resolution.ViewEvidence)
                    Add(x.Views, new ViewKey(resolution.IsTargetCandidate, view.ViewId, view.Disposition));
                foreach (var view in resolution.UniqueSupportingViews)
                    Add(x.UniqueSupportingViews, new UniqueViewKey(resolution.IsTargetCandidate, view));
                foreach (var edge in resolution.SupportingDependencies)
                    Add(x.Dependencies, new DependencyKey(resolution.IsTargetCandidate, edge.Parent, edge.Child));
                Add(x.Strata, new StrataKey(resolution.IsTargetCandidate, resolution.TargetCompletion.HeadType,
                    resolution.OriginalChordHeadCount, resolution.ReducedVisibleMemberCount,
                    resolution.HeldBeforePresent, resolution.BaselineCompletionCount,
                    resolution.ComparableViews.Length, resolution.CoverageSignature, resolution.EvidenceState));
                if (resolution.IsTargetCandidate && resolution.ConflictRecord is not null) x.TargetConflicts++;
                if (resolution.IsTargetCandidate && resolution.ConflictRecord?.IncludesTargetAndWrong == true)
                    x.TargetWrongConflicts++;
                x.SharedDonors += resolution.SharedSupportingAndContradictingDonorGroupIds.Length;
                if (resolution.SupportingDonorGroupIds.Length > 0 && resolution.ContradictingDonorGroupIds.Length > 0
                    && resolution.SharedSupportingAndContradictingDonorGroupIds.Length == 0) x.DisjointResolutions++;
                x.SupportDonorReferences += resolution.SupportingDonorGroupIds.Length;
                x.ContradictionDonorReferences += resolution.ContradictingDonorGroupIds.Length;
            }
            return x;
        }

        public static ChartAggregate Combine(IEnumerable<ChartAggregate> source)
        {
            var items = source.ToArray();
            var result = new ChartAggregate { Chart = items[0].Chart };
            foreach (var x in items)
            {
                result.ChartCount += x.ChartCount; result.Trials += x.Trials; result.Candidates += x.Candidates;
                result.TargetCandidates += x.TargetCandidates; result.TargetConflicts += x.TargetConflicts;
                result.TargetWrongConflicts += x.TargetWrongConflicts; result.SharedDonors += x.SharedDonors;
                result.DisjointResolutions += x.DisjointResolutions;
                result.SupportDonorReferences += x.SupportDonorReferences;
                result.ContradictionDonorReferences += x.ContradictionDonorReferences;
                result.TargetLeakage += x.TargetLeakage; result.FutureLeakage += x.FutureLeakage;
                result.HeldAsHead += x.HeldAsHead; result.NestingViolations += x.NestingViolations;
                Merge(result.States, x.States); Merge(result.Coverage, x.Coverage);
                Merge(result.Ambiguities, x.Ambiguities); Merge(result.Joints, x.Joints);
                Merge(result.Strata, x.Strata); Merge(result.Views, x.Views);
                Merge(result.UniqueSupportingViews, x.UniqueSupportingViews);
                Merge(result.Dependencies, x.Dependencies);
            }
            return result;
        }
        private static void Add<TKey>(Dictionary<TKey, int> target, TKey key) where TKey : notnull =>
            target[key] = target.GetValueOrDefault(key) + 1;
        private static void Merge<TKey>(Dictionary<TKey, int> target, Dictionary<TKey, int> source)
            where TKey : notnull
        { foreach (var item in source) target[item.Key] = target.GetValueOrDefault(item.Key) + item.Value; }
    }

    private sealed record StateKey(bool IsTarget, ComparableEvidenceState State) : IComparable<StateKey>
    { public int CompareTo(StateKey? x) => x is null ? 1 : ToString().CompareTo(x.ToString()); }
    private sealed record CoverageKey(bool IsTarget, string Coverage, ComparableEvidenceState State)
        : IComparable<CoverageKey>
    { public int CompareTo(CoverageKey? x) => x is null ? 1 : string.CompareOrdinal(ToString(), x.ToString()); }
    private sealed record AmbiguityKey(bool IsTarget, ComparableEvidenceAmbiguity Cause) : IComparable<AmbiguityKey>
    { public int CompareTo(AmbiguityKey? x) => x is null ? 1 : ToString().CompareTo(x.ToString()); }
    private sealed record JointKey(bool IsTarget, ObservedJointContextKind Kind, CandidateJointEvidenceState State)
        : IComparable<JointKey>
    { public int CompareTo(JointKey? x) => x is null ? 1 : ToString().CompareTo(x.ToString()); }
    private sealed record ViewKey(bool IsTarget, ExactCompletionContextView View, CandidateViewDisposition Disposition)
        : IComparable<ViewKey>
    { public int CompareTo(ViewKey? x) => x is null ? 1 : ToString().CompareTo(x.ToString()); }
    private sealed record UniqueViewKey(bool IsTarget, ExactCompletionContextView View)
        : IComparable<UniqueViewKey>
    { public int CompareTo(UniqueViewKey? x) => x is null ? 1 : ToString().CompareTo(x.ToString()); }
    private sealed record DependencyKey(bool IsTarget, ExactCompletionContextView Parent,
        ExactCompletionContextView Child) : IComparable<DependencyKey>
    { public int CompareTo(DependencyKey? x) => x is null ? 1 : ToString().CompareTo(x.ToString()); }
    private sealed record StrataKey(bool IsTarget, OriginalHeadMemberType TargetType, int ChordSize,
        int ReducedCount, bool Held, int BaselineCount, int ComparableCount, string Coverage,
        ComparableEvidenceState State) : IComparable<StrataKey>
    { public int CompareTo(StrataKey? x) => x is null ? 1 : string.CompareOrdinal(ToString(), x.ToString()); }

    private static C11CorpusChartDescriptor Public(C11CorpusChartDescriptor x) => x with { RuntimePath = "" };
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string B(bool value) => value ? "yes" : "no";
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
