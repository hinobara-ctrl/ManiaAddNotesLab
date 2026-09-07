using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class D02CorpusRunner
{
    public static void Run(string corpusRoot, string publicOutputDirectory, string localDetailJson)
    {
        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        var charts = new List<ChartAggregate>();
        Directory.CreateDirectory(publicOutputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(localDetailJson)!);
        using var detailStream = File.Create(localDetailJson);
        using var detail = new Utf8JsonWriter(detailStream, new JsonWriterOptions { Indented = false });
        detail.WriteStartObject();
        detail.WriteString("researchSchemaVersion", ExactContextAgreementResearch.ResearchSchemaVersion);
        detail.WritePropertyName("charts");
        detail.WriteStartArray();
        foreach (var descriptor in discovery.UniqueHumanCharts)
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var result = ExactContextAgreementResearch.Evaluate(chart);
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
            Console.WriteLine($"{descriptor.RelativePath}: trials={result.Trials}, " +
                $"primary={result.PrimaryCompetingTrials}, signatures={aggregate.Coverage.Count}, " +
                $"conflicts={result.ConflictRecords}");
        }
        detail.WriteEndArray();
        detail.WriteEndObject();
        detail.Flush();

        WriteScopes(Path.Combine(publicOutputDirectory, "d0_2_chart_summary.csv"),
            charts.Select(x => (x.Chart.FamilyKey, x.Chart.RelativePath, x.Chart.Version, x.Chart.KeyCount, x)));
        var families = charts.GroupBy(x => x.Chart.FamilyKey).OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => (x.Key, "", "ALL", 0, ChartAggregate.Combine(x))).ToArray();
        WriteScopes(Path.Combine(publicOutputDirectory, "d0_2_family_summary.csv"), families);
        WriteScopes(Path.Combine(publicOutputDirectory, "d0_2_global_summary.csv"),
            [("GLOBAL", "", "ALL", 0, ChartAggregate.Combine(charts))]);
        WritePairs(Path.Combine(publicOutputDirectory, "d0_2_view_pair_summary.csv"), charts);
        WriteJoints(Path.Combine(publicOutputDirectory, "d0_2_joint_context_summary.csv"), charts);
        WriteCoverage(Path.Combine(publicOutputDirectory, "d0_2_coverage_signature_summary.csv"), charts);
        WriteConflicts(Path.Combine(publicOutputDirectory, "d0_2_conflict_summary.csv"), charts);
        WriteStrata(Path.Combine(publicOutputDirectory, "d0_2_stratification_summary.csv"), charts);
        Console.WriteLine($"Wrote D0.2 summaries for {charts.Count} charts and " +
            $"{charts.Select(x => x.Chart.FamilyKey).Distinct().Count()} families. Detail bytes={detailStream.Length}.");
    }

    private static void WriteScopes(string path,
        IEnumerable<(string Family, string Relative, string Version, int Keys, ChartAggregate Data)> scopes)
    {
        using var writer = Writer(path);
        writer.WriteLine("family_key,relative_path,version,key_count,charts,trials,primary_competing_trials,view_observations,pairwise_records,conflict_records,target_vs_wrong_conflicts,donor_references,distinct_coverage_signatures,agreement_build_ms,pairwise_comparison_ms,joint_witness_comparison_ms,target_group_leakage,future_held_leakage,held_tail_encoded_as_head,donor_nesting_violations");
        foreach (var scope in scopes)
        {
            var x = scope.Data;
            writer.WriteLine(string.Join(',', Csv(scope.Family), Csv(scope.Relative), Csv(scope.Version), I(scope.Keys),
                I(x.ChartCount), I(x.Trials), I(x.Primary), I(x.ViewObservations), I(x.PairwiseRecords),
                I(x.Conflicts), I(x.TargetWrongConflicts), I(x.DonorReferences), I(x.Coverage.Count),
                F(x.AgreementMs), F(x.PairwiseMs), F(x.JointMs), I(x.TargetLeakage), I(x.FutureLeakage),
                I(x.HeldAsHead), I(x.NestingViolations)));
        }
    }

    private static void WritePairs(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("left_view,right_view,pair_kind,trials,left_comparable,right_comparable,both_comparable,left_only,right_only,neither,equal_completion_sets,overlap,disjoint,both_support_target,unique_same_target,unique_same_wrong,unique_conflict,donor_sets_identical,donor_sets_left_subset,donor_sets_right_subset,donor_sets_partial,donor_sets_disjoint,donor_empty_or_not_comparable");
        foreach (var item in CombineCounters(charts.Select(x => x.Pairs)))
        {
            var x = item.Value;
            writer.WriteLine(string.Join(',', item.Key.Left, item.Key.Right, item.Key.Kind, I(x.Trials),
                I(x.LeftComparable), I(x.RightComparable), I(x.BothComparable), I(x.LeftOnly), I(x.RightOnly),
                I(x.Neither), I(x.Equal), I(x.Overlap), I(x.Disjoint), I(x.BothTarget), I(x.UniqueTarget),
                I(x.UniqueWrong), I(x.UniqueConflict), I(x.DonorIdentical), I(x.DonorLeftSubset),
                I(x.DonorRightSubset), I(x.DonorPartial), I(x.DonorDisjoint), I(x.DonorEmpty)));
        }
    }

    private static void WriteJoints(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("scope,family_key,joint_kind,trials,left_comparable,right_comparable,joint_comparable,marginal_agreement_trials,marginal_agreement_completions,joint_witness_confirmed,marginal_no_joint_comparable,marginal_joint_unsupported,marginals_disagree_joint_supported,joint_unique_target,joint_unique_wrong");
        foreach (var group in charts.GroupBy(x => x.Chart.FamilyKey).OrderBy(x => x.Key, StringComparer.Ordinal)
                     .Select(x => (Scope: "family", Family: x.Key, Data: CombineCounters(x.Select(y => y.Joints))))
                     .Append((Scope: "global", Family: "GLOBAL", Data: CombineCounters(charts.Select(x => x.Joints)))))
        foreach (var item in group.Data)
        {
            var x = item.Value;
            writer.WriteLine(string.Join(',', group.Scope, Csv(group.Family), item.Key, I(x.Trials), I(x.LeftComparable), I(x.RightComparable),
                I(x.JointComparable), I(x.MarginalAgreementTrials), I(x.MarginalAgreementCompletions),
                I(x.Confirmed), I(x.NoJointComparable), I(x.JointUnsupported), I(x.DisagreeJointSupported),
                I(x.JointTarget), I(x.JointWrong)));
        }
    }

    private static void WriteCoverage(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("coverage_signature,trials,primary_competing_trials");
        foreach (var item in CombineCounters(charts.Select(x => x.Coverage)))
            writer.WriteLine(string.Join(',', item.Key, I(item.Value.Trials), I(item.Value.Primary)));
    }

    private static void WriteConflicts(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("unique_agreement,trials,target_vs_wrong_conflicts");
        foreach (var item in CombineCounters(charts.Select(x => x.UniqueStates)))
            writer.WriteLine(string.Join(',', item.Key, I(item.Value.Trials), I(item.Value.TargetWrong)));
    }

    private static void WriteStrata(string path, IReadOnlyList<ChartAggregate> charts)
    {
        using var writer = Writer(path);
        writer.WriteLine("target_type,original_chord_size,reduced_visible_count,held_before,baseline_completion_count,comparable_view_count,target_resolving_view_count,coverage_signature,trials,primary_competing_trials,unique_wrong_views");
        foreach (var item in CombineCounters(charts.Select(x => x.Strata)))
        {
            var k = item.Key;
            writer.WriteLine(string.Join(',', k.TargetType, I(k.ChordSize), I(k.ReducedCount),
                k.Held ? "present" : "absent", I(k.BaselineCount), I(k.ComparableCount), I(k.ResolvingCount),
                k.Coverage, I(item.Value.Trials), I(item.Value.Primary), I(item.Value.UniqueWrongViews)));
        }
    }

    private static SortedDictionary<TKey, TValue> CombineCounters<TKey, TValue>(
        IEnumerable<Dictionary<TKey, TValue>> sources) where TKey : notnull where TValue : ICounter<TValue>, new()
    {
        var result = new SortedDictionary<TKey, TValue>();
        foreach (var source in sources)
        foreach (var item in source)
        {
            if (!result.TryGetValue(item.Key, out var target)) result[item.Key] = target = new TValue();
            target.Add(item.Value);
        }
        return result;
    }

    private static C11CorpusChartDescriptor Public(C11CorpusChartDescriptor source) => source with { RuntimePath = "" };
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string F(double value) => value.ToString("0.############", CultureInfo.InvariantCulture);
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private interface ICounter<in T> { void Add(T other); }
    private sealed class PairCounter : ICounter<PairCounter>
    {
        public int Trials, LeftComparable, RightComparable, BothComparable, LeftOnly, RightOnly, Neither,
            Equal, Overlap, Disjoint, BothTarget, UniqueTarget, UniqueWrong, UniqueConflict,
            DonorIdentical, DonorLeftSubset, DonorRightSubset, DonorPartial, DonorDisjoint, DonorEmpty;
        public void Add(PairCounter x)
        {
            Trials += x.Trials; LeftComparable += x.LeftComparable; RightComparable += x.RightComparable;
            BothComparable += x.BothComparable; LeftOnly += x.LeftOnly; RightOnly += x.RightOnly;
            Neither += x.Neither; Equal += x.Equal; Overlap += x.Overlap; Disjoint += x.Disjoint;
            BothTarget += x.BothTarget; UniqueTarget += x.UniqueTarget; UniqueWrong += x.UniqueWrong;
            UniqueConflict += x.UniqueConflict; DonorIdentical += x.DonorIdentical;
            DonorLeftSubset += x.DonorLeftSubset; DonorRightSubset += x.DonorRightSubset;
            DonorPartial += x.DonorPartial; DonorDisjoint += x.DonorDisjoint; DonorEmpty += x.DonorEmpty;
        }
    }
    private sealed class JointCounter : ICounter<JointCounter>
    {
        public int Trials, LeftComparable, RightComparable, JointComparable, MarginalAgreementTrials,
            MarginalAgreementCompletions, Confirmed, NoJointComparable, JointUnsupported,
            DisagreeJointSupported, JointTarget, JointWrong;
        public void Add(JointCounter x)
        {
            Trials += x.Trials; LeftComparable += x.LeftComparable; RightComparable += x.RightComparable;
            JointComparable += x.JointComparable; MarginalAgreementTrials += x.MarginalAgreementTrials;
            MarginalAgreementCompletions += x.MarginalAgreementCompletions; Confirmed += x.Confirmed;
            NoJointComparable += x.NoJointComparable; JointUnsupported += x.JointUnsupported;
            DisagreeJointSupported += x.DisagreeJointSupported; JointTarget += x.JointTarget;
            JointWrong += x.JointWrong;
        }
    }
    private sealed class CoverageCounter : ICounter<CoverageCounter>
    {
        public int Trials, Primary;
        public void Add(CoverageCounter x) { Trials += x.Trials; Primary += x.Primary; }
    }
    private sealed class UniqueCounter : ICounter<UniqueCounter>
    {
        public int Trials, TargetWrong;
        public void Add(UniqueCounter x) { Trials += x.Trials; TargetWrong += x.TargetWrong; }
    }
    private sealed class StrataCounter : ICounter<StrataCounter>
    {
        public int Trials, Primary, UniqueWrongViews;
        public void Add(StrataCounter x)
        { Trials += x.Trials; Primary += x.Primary; UniqueWrongViews += x.UniqueWrongViews; }
    }

    private sealed class ChartAggregate
    {
        public required C11CorpusChartDescriptor Chart { get; init; }
        public int ChartCount, Trials, Primary, ViewObservations, PairwiseRecords, Conflicts,
            TargetWrongConflicts, DonorReferences, TargetLeakage, FutureLeakage, HeldAsHead, NestingViolations;
        public double AgreementMs, PairwiseMs, JointMs;
        public Dictionary<PairKey, PairCounter> Pairs { get; } = [];
        public Dictionary<ObservedJointContextKind, JointCounter> Joints { get; } = [];
        public Dictionary<string, CoverageCounter> Coverage { get; } = new(StringComparer.Ordinal);
        public Dictionary<UniqueViewAgreement, UniqueCounter> UniqueStates { get; } = [];
        public Dictionary<StrataKey, StrataCounter> Strata { get; } = [];

        public static ChartAggregate From(C11CorpusChartDescriptor chart, ExactContextAgreementResearchResult result)
        {
            var x = new ChartAggregate { Chart = chart, ChartCount = 1, Trials = result.Trials,
                Primary = result.PrimaryCompetingTrials, ViewObservations = result.ViewObservations,
                PairwiseRecords = result.PairwiseRecords, Conflicts = result.ConflictRecords,
                TargetWrongConflicts = result.Certificates.Count(c => c.Conflict?.IncludesTargetAndWrong == true),
                DonorReferences = result.DonorReferences, AgreementMs = result.AgreementBuildMilliseconds,
                PairwiseMs = result.PairwiseComparisonMilliseconds, JointMs = result.JointWitnessComparisonMilliseconds,
                TargetLeakage = result.TargetGroupLeakageCount, FutureLeakage = result.FutureHeldLeakageCount,
                HeldAsHead = result.HeldTailEncodedAsHeadCount, NestingViolations = result.DonorNestingViolationCount };
            foreach (var certificate in result.Certificates)
            {
                var primary = certificate.BaselineState == ChordCompletionReconstructionState.TargetAmongCompetingCompletions;
                Add(x.Coverage, certificate.CoverageSignature, new CoverageCounter { Trials = 1, Primary = primary ? 1 : 0 });
                Add(x.UniqueStates, certificate.UniqueAgreement, new UniqueCounter { Trials = 1,
                    TargetWrong = certificate.Conflict?.IncludesTargetAndWrong == true ? 1 : 0 });
                Add(x.Strata, new StrataKey(certificate.TargetType, certificate.OriginalChordHeadCount,
                    certificate.ReducedVisibleMemberCount, certificate.HeldBeforePresent,
                    certificate.BaselineCompletionCount, certificate.ComparableViewCount,
                    certificate.TargetResolvingViewCount, certificate.CoverageSignature),
                    new StrataCounter { Trials = 1, Primary = primary ? 1 : 0,
                        UniqueWrongViews = certificate.UniqueWrongViewCount });
                foreach (var pair in certificate.ViewPairs)
                {
                    var left = certificate.Views.Single(v => v.ViewId == pair.LeftView).Comparable;
                    var right = certificate.Views.Single(v => v.ViewId == pair.RightView).Comparable;
                    var leftUnique = certificate.Views.Single(v => v.ViewId == pair.LeftView).UniqueCompletion;
                    var rightUnique = certificate.Views.Single(v => v.ViewId == pair.RightView).UniqueCompletion;
                    var target = new ExactCompletionIdentity(certificate.TargetLane, certificate.TargetType);
                    var counter = new PairCounter { Trials = 1, LeftComparable = left ? 1 : 0,
                        RightComparable = right ? 1 : 0, BothComparable = left && right ? 1 : 0,
                        LeftOnly = left && !right ? 1 : 0, RightOnly = !left && right ? 1 : 0,
                        Neither = !left && !right ? 1 : 0,
                        Equal = pair.CompletionRelation == CompletionSetRelation.EqualCompletionSet ? 1 : 0,
                        Overlap = pair.CompletionRelation is CompletionSetRelation.LeftStrictSubset
                            or CompletionSetRelation.RightStrictSubset or CompletionSetRelation.PartialOverlap ? 1 : 0,
                        Disjoint = pair.CompletionRelation == CompletionSetRelation.Disjoint ? 1 : 0,
                        BothTarget = pair.TargetSupport == PairTargetSupport.TargetSupportedByBoth ? 1 : 0,
                        UniqueTarget = leftUnique == target && rightUnique == target ? 1 : 0,
                        UniqueWrong = leftUnique is not null && leftUnique == rightUnique && leftUnique != target ? 1 : 0,
                        UniqueConflict = leftUnique is not null && rightUnique is not null && leftUnique != rightUnique ? 1 : 0,
                        DonorIdentical = pair.DonorComparisons.Count(d => d.Relation == DonorSetRelation.IdenticalDonorSet),
                        DonorLeftSubset = pair.DonorComparisons.Count(d => d.Relation == DonorSetRelation.LeftStrictSubset),
                        DonorRightSubset = pair.DonorComparisons.Count(d => d.Relation == DonorSetRelation.RightStrictSubset),
                        DonorPartial = pair.DonorComparisons.Count(d => d.Relation == DonorSetRelation.PartialOverlap),
                        DonorDisjoint = pair.DonorComparisons.Count(d => d.Relation == DonorSetRelation.Disjoint),
                        DonorEmpty = pair.DonorComparisons.Count(d => d.Relation == DonorSetRelation.EmptyOrNotComparable) };
                    Add(x.Pairs, new PairKey(pair.LeftView, pair.RightView, pair.PairKind), counter);
                }
                foreach (var joint in certificate.JointContexts)
                {
                    Add(x.Joints, joint.Kind, new JointCounter { Trials = 1,
                        LeftComparable = joint.LeftComparable ? 1 : 0, RightComparable = joint.RightComparable ? 1 : 0,
                        JointComparable = joint.JointComparable ? 1 : 0,
                        MarginalAgreementTrials = joint.MarginalIntersection.Length > 0 ? 1 : 0,
                        MarginalAgreementCompletions = joint.MarginalIntersection.Length,
                        Confirmed = joint.CompletionObservations.Count(o => o.Support == JointCompletionSupport.MarginalAgreeAndJointSupported),
                        NoJointComparable = joint.CompletionObservations.Count(o => o.Support == JointCompletionSupport.MarginalAgreeButNoJointComparable),
                        JointUnsupported = joint.CompletionObservations.Count(o => o.Support == JointCompletionSupport.MarginalAgreeButJointDoesNotSupportCompletion),
                        DisagreeJointSupported = joint.CompletionObservations.Count(o => o.Support == JointCompletionSupport.MarginalsDisagreeButJointSupported),
                        JointTarget = joint.JointUniqueTarget ? 1 : 0, JointWrong = joint.JointUniqueWrong ? 1 : 0 });
                }
            }
            return x;
        }

        public static ChartAggregate Combine(IEnumerable<ChartAggregate> source)
        {
            var items = source.ToArray();
            var result = new ChartAggregate { Chart = items[0].Chart };
            foreach (var x in items)
            {
                result.ChartCount += x.ChartCount; result.Trials += x.Trials; result.Primary += x.Primary;
                result.ViewObservations += x.ViewObservations; result.PairwiseRecords += x.PairwiseRecords;
                result.Conflicts += x.Conflicts; result.TargetWrongConflicts += x.TargetWrongConflicts;
                result.DonorReferences += x.DonorReferences; result.AgreementMs += x.AgreementMs;
                result.PairwiseMs += x.PairwiseMs; result.JointMs += x.JointMs;
                result.TargetLeakage += x.TargetLeakage; result.FutureLeakage += x.FutureLeakage;
                result.HeldAsHead += x.HeldAsHead; result.NestingViolations += x.NestingViolations;
                Merge(result.Pairs, x.Pairs); Merge(result.Joints, x.Joints); Merge(result.Coverage, x.Coverage);
                Merge(result.UniqueStates, x.UniqueStates); Merge(result.Strata, x.Strata);
            }
            return result;
        }
        private static void Add<TKey, TValue>(Dictionary<TKey, TValue> target, TKey key, TValue value)
            where TKey : notnull where TValue : ICounter<TValue>, new()
        {
            if (!target.TryGetValue(key, out var current)) target[key] = current = new TValue();
            current.Add(value);
        }
        private static void Merge<TKey, TValue>(Dictionary<TKey, TValue> target, Dictionary<TKey, TValue> source)
            where TKey : notnull where TValue : ICounter<TValue>, new()
        { foreach (var item in source) Add(target, item.Key, item.Value); }
    }

    private sealed record PairKey(ExactCompletionContextView Left, ExactCompletionContextView Right, ViewPairKind Kind)
        : IComparable<PairKey>
    { public int CompareTo(PairKey? other) => other is null ? 1 : (Left, Right, Kind).CompareTo((other.Left, other.Right, other.Kind)); }
    private sealed record StrataKey(OriginalHeadMemberType TargetType, int ChordSize, int ReducedCount, bool Held,
        int BaselineCount, int ComparableCount, int ResolvingCount, string Coverage) : IComparable<StrataKey>
    {
        public int CompareTo(StrataKey? other) => other is null ? 1
            : string.CompareOrdinal(ToString(), other.ToString());
    }
}
