using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class D10ResultingStateRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static void Run(string corpusRoot, string publicOutputDirectory, string localDetailJson)
    {
        var synthetic = RunSyntheticGate();
        if (synthetic.Any(x => !x.Passed))
            throw new InvalidOperationException("D1.0 synthetic gate failed; human corpus was not run.");

        var localRoot = Path.GetDirectoryName(localDetailJson)!;
        var baselinePublic = Path.Combine(localRoot, "baseline", "public");
        var baselineDetail = Path.Combine(localRoot, "baseline", "d0_detail.json");
        D0CorpusRunner.Run(corpusRoot, baselinePublic, baselineDetail);
        var reproduction = CompareD0Global(publicOutputDirectory, baselinePublic);
        if (reproduction.Any(x => !x.Matches))
            throw new InvalidOperationException("D0 semantic reproduction failed; D1.0 human corpus was not run.");

        var discovery = C11CorpusDiscovery.Discover(corpusRoot);
        if (discovery.UniqueHumanCharts.Length != 11
            || discovery.UniqueHumanCharts.Select(x => x.FamilyKey).Distinct(StringComparer.Ordinal).Count() != 11
            || !discovery.UniqueHumanCharts.Select(x => x.KeyCount).Distinct().Order().SequenceEqual([4, 7, 10]))
            throw new InvalidOperationException("Frozen C11 corpus identity changed; D1.0 human corpus was not run.");

        var rows = new List<EvaluatedChart>();
        foreach (var descriptor in discovery.UniqueHumanCharts)
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            var result = ResultingStateCompositionResearch.Evaluate(chart);
            rows.Add(new EvaluatedChart(Public(descriptor), result));
            Console.WriteLine($"{descriptor.RelativePath}: pairs={result.EligiblePairHoldoutTargets}, " +
                $"reduced-only comparable={Comparable(result, ExactCompletionContextView.ReducedOnly)}, " +
                $"joint={JointTarget(result, ExactCompletionContextView.ReducedOnly)}, " +
                $"marginal-only={TargetMarginalOnly(result, ExactCompletionContextView.ReducedOnly)}");
        }

        Directory.CreateDirectory(publicOutputDirectory);
        Directory.CreateDirectory(localRoot);
        WriteSynthetic(Path.Combine(publicOutputDirectory, "d1_0_synthetic_gate_summary.csv"), synthetic);
        WriteReproduction(Path.Combine(publicOutputDirectory, "d1_0_phase_d0_reproduction.csv"), reproduction);
        WriteTarget(Path.Combine(publicOutputDirectory, "d1_0_target_reconstruction_summary.csv"), rows);
        WriteMarginalJoint(Path.Combine(publicOutputDirectory, "d1_0_marginal_joint_summary.csv"), rows);
        WriteTraps(Path.Combine(publicOutputDirectory, "d1_0_composition_trap_summary.csv"), rows);
        WriteContext(Path.Combine(publicOutputDirectory, "d1_0_context_view_summary.csv"), rows);
        WriteCharts(Path.Combine(publicOutputDirectory, "d1_0_chart_summary.csv"), rows);
        WriteFamilies(Path.Combine(publicOutputDirectory, "d1_0_family_summary.csv"), rows);
        WriteStrata(Path.Combine(publicOutputDirectory, "d1_0_stratification_summary.csv"), rows);
        WritePairTypes(Path.Combine(publicOutputDirectory, "d1_0_pair_type_summary.csv"), rows);
        WriteReduced(Path.Combine(publicOutputDirectory, "d1_0_reduced_state_summary.csv"), rows);
        WriteLeakage(Path.Combine(publicOutputDirectory, "d1_0_leakage_summary.csv"), rows, synthetic);
        WriteOrder(Path.Combine(publicOutputDirectory, "d1_0_order_invariance_summary.csv"), rows);
        WriteDetail(localDetailJson, discovery, rows, synthetic, reproduction);
        Console.WriteLine($"D1.0 wrote {rows.Count} charts / " +
            $"{rows.Sum(x => x.Result.EligiblePairHoldoutTargets)} pair-holdout trials.");
    }

    private static ImmutableArray<GateRow> RunSyntheticGate()
    {
        var joint = ResultingStateCompositionResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(1, 1000)));
        var trap = ResultingStateCompositionResearch.Evaluate(Chart(4,
            Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(2, 1000), Tap(1, 2000), Tap(3, 2000)));
        var noContext = ResultingStateCompositionResearch.Evaluate(Chart(4, Tap(0, 0), Tap(1, 0)));
        var mixed = ResultingStateCompositionResearch.Evaluate(Chart(4,
            Tap(0, 0), Ln(1, 0, 400), Tap(0, 1000), Ln(1, 1000, 1400)));
        var held = ResultingStateCompositionResearch.Evaluate(Chart(4,
            Ln(3, -500, 1500), Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(1, 1000)));
        var dense = ResultingStateCompositionResearch.Evaluate(Chart(7,
            Tap(0, 0), Tap(1, 0), Tap(2, 0), Tap(3, 0),
            Tap(0, 31), Tap(1, 31), Tap(2, 31), Tap(3, 31)));
        var jointView = ReducedOnly(joint.PairHoldoutTrials.First(x => x.TargetHeadTime == 0));
        var trapView = ReducedOnly(trap.PairHoldoutTrials.First(x => x.TargetHeadTime == 0));
        var badTargetLeakage = joint.ObservedPairOccurrences.Count(x =>
            x.SourceGroupId == joint.PairHoldoutTrials[0].TargetSourceGroupId);
        var badCrossDonorFabrication = trapView.BothTargetMembersMarginallySupported
            && !trapView.TargetCompletionSetJointlyObserved ? 1 : 0;
        var orderSet = CompletionSetIdentity.Create([
            new CompletionMemberIdentity(0, OriginalHeadMemberType.TapHead),
            new CompletionMemberIdentity(1, OriginalHeadMemberType.LongNoteHead)
        ]);
        var reverseSet = CompletionSetIdentity.Create(orderSet.Members.Reverse());
        var rerun = ResultingStateCompositionResearchJson.Serialize(joint) ==
            ResultingStateCompositionResearchJson.Serialize(ResultingStateCompositionResearch.Evaluate(Chart(4,
                Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(1, 1000))));
        var rows = new List<GateRow>
        {
            Gate("k1_d0_semantics", joint.K1ControlTrials == 4 && joint.K1ControlTargetSupported == 4,
                joint.K1ControlTargetSupported),
            Gate("same_occurrence_joint_witness", jointView.TargetCompletionSetJointlyObserved, jointView.TargetJointDonorGroupCount),
            Gate("target_joint_unique", jointView.TargetState == TargetCompositionReconstructionState.ObservedJointUnique, (int)jointView.TargetState),
            Gate("marginal_only_trap", trapView.TargetMarginalOnly, trapView.MarginalOnlyPairs),
            Gate("cross_donor_not_joint", !trapView.TargetCompletionSetJointlyObserved, badCrossDonorFabrication),
            Gate("bad_cross_occurrence_positive_control", badCrossDonorFabrication > 0, badCrossDonorFabrication),
            Gate("bad_target_leakage_positive_control", badTargetLeakage > 0, badTargetLeakage),
            Gate("whole_group_holdout", joint.TargetWholeGroupLeakageCount == 0, joint.TargetWholeGroupLeakageCount),
            Gate("target_observation_holdout", joint.TargetObservationLeakageCount == 0, joint.TargetObservationLeakageCount),
            Gate("future_held_hygiene", held.FutureHeldLeakageCount == 0, held.FutureHeldLeakageCount),
            Gate("held_tail_not_head", held.HeldTailAsHeadCount == 0, held.HeldTailAsHeadCount),
            Gate("tap_ln_type_identity", mixed.PairHoldoutTrials.All(x => x.PairType == CompletionPairType.TapLongNote), mixed.PairHoldoutTrials.Length),
            Gate("completion_set_order", orderSet.Equals(reverseSet), orderSet.GetHashCode() == reverseSet.GetHashCode() ? 0 : 1),
            Gate("resulting_state_order", joint.OrderInvarianceViolationCount == 0, joint.OrderInvarianceViolationCount),
            Gate("no_comparable_abstention", ReducedOnly(noContext.PairHoldoutTrials[0]).TargetState == TargetCompositionReconstructionState.NoComparableCompositionContext, 1),
            Gate("hard_invalid_same_lane", ResultingStateCompositionResearch.ValidateComposition(4, [], [
                new CompletionMemberIdentity(0, OriginalHeadMemberType.TapHead),
                new CompletionMemberIdentity(0, OriginalHeadMemberType.LongNoteHead)]) == CompositionStructuralInvalidity.SameLaneCollision, 1),
            Gate("hard_invalid_existing", ResultingStateCompositionResearch.ValidateComposition(4,
                [new CompletionMemberIdentity(0, OriginalHeadMemberType.TapHead)],
                [new CompletionMemberIdentity(0, OriginalHeadMemberType.TapHead)]) == CompositionStructuralInvalidity.MemberAlreadyPresent, 1),
            Gate("empty_reduced_state", joint.PairHoldoutTrials.All(x => x.ReducedHeadCount == 0), joint.PairHoldoutTrials.Length),
            Gate("dense_rice_exhaustive_pairs", dense.PairHoldoutTrials.Length == 12, dense.PairHoldoutTrials.Length),
            Gate("all_eight_views", joint.PairHoldoutTrials.All(x => x.Views.Length == 8), joint.PairHoldoutTrials[0].Views.Length),
            Gate("synthetic_teaching_zero", joint.SyntheticTeachingCount == 0, joint.SyntheticTeachingCount),
            Gate("cross_chart_zero", joint.CrossChartEvidenceCount == 0, joint.CrossChartEvidenceCount),
            Gate("cross_occurrence_leakage_zero", trap.JointWitnessCrossOccurrenceLeakageCount == 0, trap.JointWitnessCrossOccurrenceLeakageCount),
            Gate("deterministic_rerun", rerun, rerun ? 0 : 1),
            Gate("multikey_1_4_7_10_18", new[] { 1, 4, 7, 10, 18 }.All(KeymodePasses), 5),
            Gate("legacy_policy_unchanged", MapperEvidenceProfileBuilder.BehaviorPolicyVersion == "legacy-experimental.1", 1)
        };
        return rows.ToImmutableArray();
    }

    private static bool KeymodePasses(int keys)
    {
        var objects = keys == 1 ? new[] { Tap(0, 0), Tap(0, 1000) }
            : new[] { Tap(0, 0), Tap(keys - 1, 0), Tap(0, 1000), Tap(keys - 1, 1000) };
        var result = ResultingStateCompositionResearch.Evaluate(Chart(keys, objects));
        return result.KeyCount == keys && result.EligiblePairHoldoutTargets == (keys == 1 ? 0 : 2);
    }

    private static ImmutableArray<D0ReproductionRow> CompareD0Global(string committedDocs,
        string reproducedDirectory)
    {
        var expectedPath = Path.Combine(committedDocs, "d0_global_summary.csv");
        var actualPath = Path.Combine(reproducedDirectory, "d0_global_summary.csv");
        var expected = ReadCsvRecord(expectedPath);
        var actual = ReadCsvRecord(actualPath);
        var ignored = new HashSet<string>(StringComparer.Ordinal)
            { "relation_build_ms", "held_out_reconstruction_ms", "detailed_artifact_bytes" };
        return expected.Keys.Where(x => !ignored.Contains(x)).Order(StringComparer.Ordinal)
            .Select(metric => new D0ReproductionRow(metric, expected[metric], actual.GetValueOrDefault(metric, "<MISSING>"),
                string.Equals(expected[metric], actual.GetValueOrDefault(metric), StringComparison.Ordinal)))
            .ToImmutableArray();
    }

    private static Dictionary<string, string> ReadCsvRecord(string path)
    {
        var lines = File.ReadAllLines(path);
        var header = ParseCsvLine(lines[0]);
        var values = ParseCsvLine(lines[1]);
        return header.Select((name, index) => (name, value: values[index]))
            .ToDictionary(x => x.name, x => x.value, StringComparer.Ordinal);
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var ch = line[index];
            if (ch == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"') { value.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted) { fields.Add(value.ToString()); value.Clear(); }
            else value.Append(ch);
        }
        fields.Add(value.ToString());
        return fields.ToArray();
    }

    private static void WriteSynthetic(string path, IEnumerable<GateRow> rows) => Write(path,
        "case,passed,measured", rows.Select(x => $"{C(x.Case)},{B(x.Passed)},{x.Measured}"));

    private static void WriteReproduction(string path, IEnumerable<D0ReproductionRow> rows) => Write(path,
        "metric,expected,observed,matches", rows.Select(x =>
            $"{C(x.Metric)},{C(x.Expected)},{C(x.Observed)},{B(x.Matches)}"));

    private static void WriteTarget(string path, IReadOnlyList<EvaluatedChart> rows)
    {
        var trials = rows.SelectMany(x => x.Result.PairHoldoutTrials).ToArray();
        Write(path, "view,eligible,comparable,target_joint_unique,target_joint_among_alternatives,target_absent,no_comparable",
            ExactCompletionContextResearch.Views.Select(view =>
            {
                var audits = trials.Select(x => View(x, view)).ToArray();
                return string.Join(',', view, audits.Length, audits.Count(x => x.ComparableDonorGroupCount > 0),
                    audits.Count(x => x.TargetState == TargetCompositionReconstructionState.ObservedJointUnique),
                    audits.Count(x => x.TargetState == TargetCompositionReconstructionState.ObservedJointAmongAlternatives),
                    audits.Count(x => x.TargetState == TargetCompositionReconstructionState.TargetAbsentFromJointEvidence),
                    audits.Count(x => x.TargetState == TargetCompositionReconstructionState.NoComparableCompositionContext));
            }));
    }

    private static void WriteMarginalJoint(string path, IReadOnlyList<EvaluatedChart> rows)
    {
        var trials = rows.SelectMany(x => x.Result.PairHoldoutTrials).ToArray();
        Write(path, "view,pair_holdout_targets,both_target_members_marginal,target_set_joint,target_marginal_only",
            ExactCompletionContextResearch.Views.Select(view =>
            {
                var audits = trials.Select(x => View(x, view)).ToArray();
                return string.Join(',', view, audits.Length,
                    audits.Count(x => x.BothTargetMembersMarginallySupported),
                    audits.Count(x => x.TargetCompletionSetJointlyObserved),
                    audits.Count(x => x.TargetMarginalOnly));
            }));
    }

    private static void WriteTraps(string path, IReadOnlyList<EvaluatedChart> rows)
    {
        var trials = rows.SelectMany(x => x.Result.PairHoldoutTrials).ToArray();
        Write(path, "view,marginal_pair_candidates,eligible_marginal_pairs,hard_invalid_pairs,joint_observed_pairs,marginal_only_pairs",
            ExactCompletionContextResearch.Views.Select(view =>
            {
                var audits = trials.Select(x => View(x, view)).ToArray();
                return string.Join(',', view, audits.Sum(x => x.MarginalPairCandidates),
                    audits.Sum(x => x.EligibleMarginalPairs), audits.Sum(x => x.HardInvalidPairs),
                    audits.Sum(x => x.JointObservedPairs), audits.Sum(x => x.MarginalOnlyPairs));
            }));
    }

    private static void WriteContext(string path, IReadOnlyList<EvaluatedChart> rows)
    {
        var trials = rows.SelectMany(x => x.Result.PairHoldoutTrials).ToArray();
        Write(path, "view,comparable,target_joint,alternatives,no_context,distinct_joint_sets,marginal_members,marginal_only_pairs",
            ExactCompletionContextResearch.Views.Select(view =>
            {
                var audits = trials.Select(x => View(x, view)).ToArray();
                return string.Join(',', view, audits.Count(x => x.ComparableDonorGroupCount > 0),
                    audits.Count(x => x.TargetCompletionSetJointlyObserved),
                    audits.Count(x => x.TargetState == TargetCompositionReconstructionState.ObservedJointAmongAlternatives),
                    audits.Count(x => x.TargetState == TargetCompositionReconstructionState.NoComparableCompositionContext),
                    audits.Sum(x => x.DistinctJointCompletionSetCount), audits.Sum(x => x.MarginalMemberCount),
                    audits.Sum(x => x.MarginalOnlyPairs));
            }));
    }

    private static void WriteCharts(string path, IReadOnlyList<EvaluatedChart> rows) => Write(path,
        "family_key,relative_path,key_count,original_objects,k1_trials,k1_comparable,k1_supported,pair_trials,reduced_only_comparable,reduced_only_joint_unique,reduced_only_joint_alternatives,reduced_only_absent,reduced_only_no_context,reduced_only_target_marginal_only,pair_operations",
        rows.Select(row =>
        {
            var audits = Audits(row.Result, ExactCompletionContextView.ReducedOnly);
            return string.Join(',', C(row.Chart.FamilyKey), C(row.Chart.RelativePath), row.Chart.KeyCount,
                row.Result.OriginalObjectCount, row.Result.K1ControlTrials, row.Result.K1ControlComparable,
                row.Result.K1ControlTargetSupported, row.Result.EligiblePairHoldoutTargets,
                audits.Count(x => x.ComparableDonorGroupCount > 0),
                audits.Count(x => x.TargetState == TargetCompositionReconstructionState.ObservedJointUnique),
                audits.Count(x => x.TargetState == TargetCompositionReconstructionState.ObservedJointAmongAlternatives),
                audits.Count(x => x.TargetState == TargetCompositionReconstructionState.TargetAbsentFromJointEvidence),
                audits.Count(x => x.TargetState == TargetCompositionReconstructionState.NoComparableCompositionContext),
                audits.Count(x => x.TargetMarginalOnly), row.Result.PairEnumerationOperations);
        }));

    private static void WriteFamilies(string path, IReadOnlyList<EvaluatedChart> rows) => Write(path,
        "family_key,key_count,charts,pair_trials,reduced_only_comparable,reduced_only_joint,reduced_only_absent,reduced_only_no_context,target_marginal_only_rate",
        rows.GroupBy(x => x.Chart.FamilyKey, StringComparer.Ordinal).OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var values = group.ToArray();
                var trials = values.SelectMany(x => x.Result.PairHoldoutTrials).ToArray();
                var audits = trials.Select(x => View(x, ExactCompletionContextView.ReducedOnly)).ToArray();
                return string.Join(',', C(group.Key), values.Select(x => x.Chart.KeyCount).Distinct().Single(),
                    values.Length, trials.Length, audits.Count(x => x.ComparableDonorGroupCount > 0),
                    audits.Count(x => x.TargetCompletionSetJointlyObserved),
                    audits.Count(x => x.TargetState == TargetCompositionReconstructionState.TargetAbsentFromJointEvidence),
                    audits.Count(x => x.TargetState == TargetCompositionReconstructionState.NoComparableCompositionContext),
                    F(Rate(audits.Count(x => x.TargetMarginalOnly), audits.Length)));
            }));

    private static void WriteStrata(string path, IReadOnlyList<EvaluatedChart> rows) => Write(path,
        "key_count,reduced_head_count,held_before,pair_type,view,trials,comparable,joint_unique,joint_alternatives,target_absent,no_context,target_marginal_only",
        rows.SelectMany(row => row.Result.PairHoldoutTrials.SelectMany(trial => trial.Views.Select(view =>
                new { row.Chart.KeyCount, trial.ReducedHeadCount, trial.HeldBeforePresent, trial.PairType, View = view })))
            .GroupBy(x => new { x.KeyCount, x.ReducedHeadCount, x.HeldBeforePresent, x.PairType, x.View.View })
            .OrderBy(x => x.Key.KeyCount).ThenBy(x => x.Key.ReducedHeadCount)
            .ThenBy(x => x.Key.HeldBeforePresent).ThenBy(x => x.Key.PairType).ThenBy(x => x.Key.View)
            .Select(group => string.Join(',', group.Key.KeyCount, group.Key.ReducedHeadCount,
                B(group.Key.HeldBeforePresent), group.Key.PairType, group.Key.View, group.Count(),
                group.Count(x => x.View.ComparableDonorGroupCount > 0),
                group.Count(x => x.View.TargetState == TargetCompositionReconstructionState.ObservedJointUnique),
                group.Count(x => x.View.TargetState == TargetCompositionReconstructionState.ObservedJointAmongAlternatives),
                group.Count(x => x.View.TargetState == TargetCompositionReconstructionState.TargetAbsentFromJointEvidence),
                group.Count(x => x.View.TargetState == TargetCompositionReconstructionState.NoComparableCompositionContext),
                group.Count(x => x.View.TargetMarginalOnly))));

    private static void WritePairTypes(string path, IReadOnlyList<EvaluatedChart> rows) => Write(path,
        "pair_type,trials,reduced_only_comparable,reduced_only_joint,reduced_only_target_marginal_only",
        rows.SelectMany(x => x.Result.PairHoldoutTrials).GroupBy(x => x.PairType).OrderBy(x => x.Key)
            .Select(group => string.Join(',', group.Key, group.Count(),
                group.Count(x => View(x, ExactCompletionContextView.ReducedOnly).ComparableDonorGroupCount > 0),
                group.Count(x => View(x, ExactCompletionContextView.ReducedOnly).TargetCompletionSetJointlyObserved),
                group.Count(x => View(x, ExactCompletionContextView.ReducedOnly).TargetMarginalOnly))));

    private static void WriteReduced(string path, IReadOnlyList<EvaluatedChart> rows) => Write(path,
        "reduced_head_count,trials,reduced_only_comparable,reduced_only_joint_unique,reduced_only_joint_alternatives,reduced_only_absent,reduced_only_no_context",
        rows.SelectMany(x => x.Result.PairHoldoutTrials).GroupBy(x => x.ReducedHeadCount).OrderBy(x => x.Key)
            .Select(group => string.Join(',', group.Key, group.Count(),
                group.Count(x => View(x, ExactCompletionContextView.ReducedOnly).ComparableDonorGroupCount > 0),
                group.Count(x => View(x, ExactCompletionContextView.ReducedOnly).TargetState == TargetCompositionReconstructionState.ObservedJointUnique),
                group.Count(x => View(x, ExactCompletionContextView.ReducedOnly).TargetState == TargetCompositionReconstructionState.ObservedJointAmongAlternatives),
                group.Count(x => View(x, ExactCompletionContextView.ReducedOnly).TargetState == TargetCompositionReconstructionState.TargetAbsentFromJointEvidence),
                group.Count(x => View(x, ExactCompletionContextView.ReducedOnly).TargetState == TargetCompositionReconstructionState.NoComparableCompositionContext))));

    private static void WriteLeakage(string path, IReadOnlyList<EvaluatedChart> rows,
        ImmutableArray<GateRow> synthetic)
    {
        var results = rows.Select(x => x.Result).ToArray();
        var badTarget = synthetic.Single(x => x.Case == "bad_target_leakage_positive_control").Measured;
        var badCross = synthetic.Single(x => x.Case == "bad_cross_occurrence_positive_control").Measured;
        Write(path, "audit,correct_count,positive_control_count", [
            $"target_whole_group_leakage,{results.Sum(x => x.TargetWholeGroupLeakageCount)},{badTarget}",
            $"target_observation_leakage,{results.Sum(x => x.TargetObservationLeakageCount)},N/A",
            $"future_held_leakage,{results.Sum(x => x.FutureHeldLeakageCount)},N/A",
            $"synthetic_teaching,{results.Sum(x => x.SyntheticTeachingCount)},N/A",
            $"cross_chart_evidence,{results.Sum(x => x.CrossChartEvidenceCount)},N/A",
            $"held_tail_as_head,{results.Sum(x => x.HeldTailAsHeadCount)},N/A",
            $"joint_witness_cross_occurrence_leakage,{results.Sum(x => x.JointWitnessCrossOccurrenceLeakageCount)},{badCross}"
        ]);
    }

    private static void WriteOrder(string path, IReadOnlyList<EvaluatedChart> rows) => Write(path,
        "scope,pair_trials,order_invariance_violations,detail_identity_algorithm",
        [$"GLOBAL,{rows.Sum(x => x.Result.EligiblePairHoldoutTargets)},{rows.Sum(x => x.Result.OrderInvarianceViolationCount)},canonical_lane_then_type"]);

    private static void WriteDetail(string path, C11CorpusDiscoveryResult discovery,
        IReadOnlyList<EvaluatedChart> rows, ImmutableArray<GateRow> synthetic,
        ImmutableArray<D0ReproductionRow> reproduction)
    {
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        writer.WriteString("schemaVersion", ResultingStateCompositionResearch.ResearchSchemaVersion);
        writer.WritePropertyName("discovery");
        JsonSerializer.Serialize(writer, discovery with
        {
            UniqueHumanCharts = discovery.UniqueHumanCharts.Select(Public).ToImmutableArray()
        }, JsonOptions);
        writer.WritePropertyName("syntheticGate");
        JsonSerializer.Serialize(writer, synthetic, JsonOptions);
        writer.WritePropertyName("d0Reproduction");
        JsonSerializer.Serialize(writer, reproduction, JsonOptions);
        writer.WritePropertyName("evaluations");
        writer.WriteStartArray();
        foreach (var row in rows)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("chart");
            JsonSerializer.Serialize(writer, row.Chart, JsonOptions);
            writer.WritePropertyName("result");
            JsonSerializer.Serialize(writer, row.Result, JsonOptions);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static CompositionViewAudit ReducedOnly(PairHoldoutTrial trial) =>
        View(trial, ExactCompletionContextView.ReducedOnly);
    private static CompositionViewAudit View(PairHoldoutTrial trial, ExactCompletionContextView view) =>
        trial.Views.Single(x => x.View == view);
    private static CompositionViewAudit[] Audits(ResultingStateCompositionResearchResult result,
        ExactCompletionContextView view) => result.PairHoldoutTrials.Select(x => View(x, view)).ToArray();
    private static int Comparable(ResultingStateCompositionResearchResult result,
        ExactCompletionContextView view) => Audits(result, view).Count(x => x.ComparableDonorGroupCount > 0);
    private static int JointTarget(ResultingStateCompositionResearchResult result,
        ExactCompletionContextView view) => Audits(result, view).Count(x => x.TargetCompletionSetJointlyObserved);
    private static int TargetMarginalOnly(ResultingStateCompositionResearchResult result,
        ExactCompletionContextView view) => Audits(result, view).Count(x => x.TargetMarginalOnly);
    private static GateRow Gate(string name, bool pass, int measured) => new(name, pass, measured);
    private static C11CorpusChartDescriptor Public(C11CorpusChartDescriptor value) => value with { RuntimePath = "" };
    private static ManiaObject Tap(int lane, int time) => ManiaObject.Tap(lane, time);
    private static ManiaObject Ln(int lane, int start, int end) => ManiaObject.Ln(lane, start, end);
    private static ManiaChart Chart(int keys, params ManiaObject[] objects) => new()
    {
        KeyCount = keys, Lines = [],
        OriginalObjects = objects.Select((x, i) => x with { Sequence = i }).ToArray(),
        TimingPoints = [new TimingPoint(0, 500)]
    };
    private static void Write(string path, string header, IEnumerable<string> lines)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine(header);
        foreach (var line in lines) writer.WriteLine(line);
    }
    private static string C(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string B(bool value) => value ? "true" : "false";
    private static string F(double? value) => value?.ToString("0.############", CultureInfo.InvariantCulture) ?? "N/A";
    private static double? Rate(int numerator, int denominator) => denominator == 0 ? null : (double)numerator / denominator;

    private sealed record GateRow(string Case, bool Passed, int Measured);
    private sealed record D0ReproductionRow(string Metric, string Expected, string Observed, bool Matches);
    private sealed record EvaluatedChart(C11CorpusChartDescriptor Chart,
        ResultingStateCompositionResearchResult Result);
}
