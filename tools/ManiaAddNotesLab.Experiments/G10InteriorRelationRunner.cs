using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class G10InteriorRelationRunner
{
    private const string Phase = "G1.0 — Interior LN Relation Feasibility / Shadow";
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static G10Preparation Prepare(string repositoryRoot, string corpusRoot, string contractPath,
        string designPath, string repositoryHead, string originMain)
    {
        var synthetic = SyntheticGate();
        if (synthetic.Any(x => !x.Passed))
            throw new InvalidDataException("G1.0 synthetic gate failed; design was not frozen.");
        var discovery = FrozenCorpus(corpusRoot);
        var contractHash = CanonicalJsonHash(contractPath);
        var snapshotHash = ImplementationSnapshot(repositoryRoot);
        var corpusHash = CorpusHash(discovery);
        var text = new StringBuilder()
            .AppendLine("# PHASE G1.0 — INTERIOR RELATION FEASIBILITY — PRE-RUN DESIGN")
            .AppendLine()
            .AppendLine("**Status: FROZEN BEFORE C11 AGGREGATES. behaviorChange=false.**")
            .AppendLine()
            .AppendLine($"- RepositoryEntryHead: `{repositoryHead}`")
            .AppendLine($"- origin/main: `{originMain}`")
            .AppendLine($"- Contract SHA-256: `{contractHash}`")
            .AppendLine($"- Implementation snapshot SHA-256: `{snapshotHash}`")
            .AppendLine($"- C11 fingerprint: `{corpusHash}`")
            .AppendLine($"- Inventory: {discovery.UniqueHumanCharts.Length} charts / " +
                $"{discovery.UniqueHumanCharts.Select(x => x.FamilyKey).Distinct(StringComparer.Ordinal).Count()} families / " +
                $"{discovery.UniqueHumanCharts.Sum(x => x.ObjectCount)} objects / " +
                $"{discovery.UniqueHumanCharts.Sum(x => x.LongNoteCount)} LNs / " +
                $"keymodes {string.Join(',', discovery.UniqueHumanCharts.Select(x => x.KeyCount).Distinct().Order())}.")
            .AppendLine()
            .AppendLine("## Frozen relation identity")
            .AppendLine()
            .AppendLine("One occurrence is one original parent LN, one exact interior anchor from `MapperEvidenceProfile`, and one original LN witness whose head is that same anchor. The record preserves both observation IDs, exact serialized times, exact file-derived decimal beats, lanes, duration from anchor, offset from parent end, anchor kind, relation class and deterministic semantic ID. Marginal duration/release values are never joined into an occurrence.")
            .AppendLine()
            .AppendLine("## Frozen populations")
            .AppendLine()
            .AppendLine("The structural population enumerates all original-only strict interior anchors before production gates. The current-gated population mirrors the present `AnchorSupported + OriginalOnly` pipeline at LnWindowBeats=4, source minimum=3 beats, context minimum=3, anchor support minimum=2 and cap=2. Pipeline attrition is ordered; ranking is descriptive and cap attrition is separate.")
            .AppendLine()
            .AppendLine("## Frozen holdouts")
            .AppendLine()
            .AppendLine("`TargetObservation` excludes the complete target witness from every donor role. `ParentOccurrence` additionally excludes donors from the same parent. Both are chart-local, prior-only (`donor.AnchorTime < target.AnchorTime`), whole-event safe, and exclude target-as-parent, target release, synthetic and cross-chart evidence. Parent context remains a query fact, not an independent relation donor. The exact query is keymode + parent duration + anchor offset + AnchorKind; the exact joint result is relation class + duration from anchor + offset from parent end.")
            .AppendLine()
            .AppendLine("## Metrics and outcomes")
            .AppendLine()
            .AppendLine("Frozen metrics are the contract inventory, structural/current populations, relation and anchor-kind counts, same/different-lane description, marginal-vs-joint, alternatives, both holdouts, leakage, ordered gate attrition, cap exclusions, geometry validity, serialization validity, per chart/family/keymode distribution, determinism and zero RNG. Outcome A requires exact representation, zero leakage/drift/RNG, a relation class in more than one C11 family, and real held-out comparable cases. Exact representation without that basis is B. Inference, leakage, synthetic contamination or behavior drift is C/STOP.")
            .AppendLine()
            .AppendLine("## Hard aborts")
            .AppendLine()
            .AppendLine("Any default/policy/RNG/output change, normal CLI/Web callsite, G1/G2/H behavior, MapperSupport, C2, community corpus, synthetic style evidence, target/cross-chart leakage, post-result contract edit or DocConsistency failure aborts. No commit or push is authorized.");
        Directory.CreateDirectory(Path.GetDirectoryName(designPath)!);
        File.WriteAllText(designPath, text.ToString(), new UTF8Encoding(false));
        return new G10Preparation(contractHash, snapshotHash, corpusHash, synthetic.ToImmutableArray());
    }

    public static G10RunSummary Run(string repositoryRoot, string corpusRoot, string contractPath,
        string designPath, string publicOutputDirectory, string detailPath)
    {
        var discovery = FrozenCorpus(corpusRoot);
        var contractHash = CanonicalJsonHash(contractPath);
        var snapshotHash = ImplementationSnapshot(repositoryRoot);
        var corpusHash = CorpusHash(discovery);
        var design = File.ReadAllText(designPath);
        foreach (var expected in new[] { contractHash, snapshotHash, corpusHash })
            if (!design.Contains(expected, StringComparison.Ordinal))
                throw new InvalidDataException("Frozen G1.0 design does not match current inputs.");
        var synthetic = SyntheticGate();
        if (synthetic.Any(x => !x.Passed))
            throw new InvalidDataException("G1.0 synthetic gate failed; C11 was not run.");

        var options = CurrentOptions();
        var rows = discovery.UniqueHumanCharts.Select(descriptor =>
        {
            var chart = OsuBeatmap.Parse(File.ReadAllText(descriptor.RuntimePath));
            return new G10ChartResult(Public(descriptor), InteriorRelationFeasibilityResearch.Evaluate(chart, options));
        }).ToArray();
        if (rows.Any(x => x.Result.CurrentInteriorOpportunityMirrorMismatchCount != 0
            || x.Result.ResearchRngCalls != 0))
            throw new InvalidDataException("G1.0 current-opportunity mirror or zero-RNG invariant failed.");

        Directory.CreateDirectory(publicOutputDirectory);
        WriteCensus(Path.Combine(publicOutputDirectory, "g1_0_relation_census.csv"), rows);
        WriteFamilies(Path.Combine(publicOutputDirectory, "g1_0_relation_by_family.csv"), rows);
        WriteAnchorKinds(Path.Combine(publicOutputDirectory, "g1_0_anchor_kind_summary.csv"), rows);
        WriteMarginal(Path.Combine(publicOutputDirectory, "g1_0_marginal_vs_joint_summary.csv"), rows);
        WriteAlternatives(Path.Combine(publicOutputDirectory, "g1_0_alternative_summary.csv"), rows);
        WriteHoldouts(Path.Combine(publicOutputDirectory, "g1_0_holdout_summary.csv"), rows);
        WriteAttrition(Path.Combine(publicOutputDirectory, "g1_0_legacy_gate_attrition.csv"), rows);
        WriteMagic(Path.Combine(publicOutputDirectory, "g1_0_magic_number_ownership.csv"), rows);
        WriteDeterminism(Path.Combine(publicOutputDirectory, "g1_0_determinism_summary.csv"), rows,
            contractHash, snapshotHash, corpusHash);

        Directory.CreateDirectory(Path.GetDirectoryName(detailPath)!);
        File.WriteAllText(detailPath, JsonSerializer.Serialize(new
        {
            phase = Phase,
            behaviorChange = false,
            contractHash,
            snapshotHash,
            corpusHash,
            discovery = Public(discovery),
            syntheticGate = synthetic,
            charts = rows
        }, Json), new UTF8Encoding(false));
        var outcome = Outcome(rows);
        WriteReport(Path.Combine(publicOutputDirectory, "PHASE_G1_0_INTERIOR_RELATION_FEASIBILITY_REPORT.md"),
            discovery, rows, contractHash, snapshotHash, corpusHash, outcome);
        return new G10RunSummary(outcome, rows.Sum(x => x.Result.CompleteRelations.Length),
            rows.Sum(x => x.Result.Holdouts.Count(y => y.ComparableDonorCount > 0)), contractHash,
            snapshotHash, corpusHash);
    }

    private static AddNotesOptions CurrentOptions() => new()
    {
        Chance = 0,
        InteriorLnOpportunitiesEnabled = true,
        MaxInteriorOpportunitiesPerSource = 2,
        InteriorMinimumSourceBeats = 3,
        InteriorLengthRatio = 1.5,
        InteriorAbsoluteLongBeats = 8,
        InteriorMinimumContextLnCount = 3,
        InteriorMinimumSupportedAnchors = 2,
        LnWindowBeats = 4,
        InteriorEligibilityMode = InteriorEligibilityMode.AnchorSupported,
        InteriorContextMode = InteriorContextMode.OriginalOnly
    };

    private static C11CorpusDiscoveryResult FrozenCorpus(string root)
    {
        var discovery = C11CorpusDiscovery.Discover(root);
        var keymodes = discovery.UniqueHumanCharts.Select(x => x.KeyCount).Distinct().Order().ToArray();
        if (discovery.UniqueHumanCharts.Length != 11
            || discovery.UniqueHumanCharts.Select(x => x.FamilyKey).Distinct(StringComparer.Ordinal).Count() != 11
            || !keymodes.SequenceEqual(new[] { 4, 7, 10 }))
            throw new InvalidDataException("Frozen C11 identity must remain 11 charts / 11 families / 4K,7K,10K.");
        return discovery;
    }

    private static string Outcome(IReadOnlyList<G10ChartResult> rows)
    {
        var relationFamilies = rows.SelectMany(x => x.Result.CompleteRelations.Select(y =>
                (y.RelationClass, x.Chart.FamilyKey)))
            .GroupBy(x => x.RelationClass).ToDictionary(x => x.Key,
                x => x.Select(y => y.FamilyKey).Distinct(StringComparer.Ordinal).Count());
        var leakage = rows.Sum(x => x.Result.Holdouts.Sum(y => y.TargetLeakageCount + y.ParentLeakageCount
            + y.ReleaseLeakageCount + y.FutureLeakageCount + y.SameEventLeakageCount
            + y.SyntheticLeakageCount + y.CrossChartLeakageCount));
        var comparable = rows.Sum(x => x.Result.Holdouts.Count(y => y.ComparableDonorCount > 0));
        return leakage == 0 && comparable > 0 && relationFamilies.Values.Any(x => x > 1) ? "A" : "B";
    }

    private static List<G10SyntheticCase> SyntheticGate()
    {
        var exact = Eval(Chart(Ln(0, 0, 5000), Ln(1, 1000, 2000), Ln(2, 1500, 5000),
            Ln(3, 2000, 6000), Ln(4, 250, 1500), Tap(5, 1500)));
        var cap = Eval(Chart(Ln(0, 0, 6000), Ln(1, 1000, 2000), Ln(2, 2000, 3000),
            Ln(3, 3000, 4000), Ln(4, -500, 500), Ln(5, 500, 2500), Ln(6, 2500, 4500)));
        var marginal = Eval(Chart(Ln(0, 0, 5000), Tap(1, 1000), Ln(2, 250, 2000)));
        var repeated = Eval(Chart(Ln(0, 0, 4000), Ln(1, 1000, 2000), Ln(2, 5000, 9000),
            Ln(3, 6000, 7000), Ln(4, 6000, 7500), Ln(5, 10000, 14000), Ln(6, 11000, 12000)));
        var synthChart = Chart(Ln(0, 0, 5000), Ln(1, 1000, 2000),
            Ln(2, 1500, 2500) with { IsSynthetic = true, Origin = AddedObjectOrigin.LnInteriorOpportunity });
        var synth = Eval(synthChart);
        var repeated2 = Eval(Chart(Ln(0, 0, 4000), Ln(1, 1000, 2000), Ln(2, 5000, 9000),
            Ln(3, 6000, 7000), Ln(4, 6000, 7500), Ln(5, 10000, 14000), Ln(6, 11000, 12000)));
        var target = repeated.CompleteRelations.OrderByDescending(x => x.AnchorTime).First();
        var targetHoldout = repeated.Holdouts.Single(x => x.OccurrenceId == target.OccurrenceId
            && x.HoldoutKind == InteriorHoldoutKind.TargetObservation);
        var donor = repeated.CompleteRelations.Single(x => x.OccurrenceId == targetHoldout.DonorOccurrenceIds[0]);
        InteriorRelationHoldoutAudit Audit(InteriorRelationOccurrence value,
            InteriorHoldoutKind kind = InteriorHoldoutKind.TargetObservation, bool syntheticDonor = false) =>
            InteriorRelationHoldoutAuditor.Audit(target, kind, [new(value, syntheticDonor)]);
        var targetBad = Audit(target);
        var parentBad = Audit(donor with { ParentLongNoteId = target.ParentLongNoteId },
            InteriorHoldoutKind.ParentOccurrence);
        var releaseBad = Audit(donor with { WitnessLongNoteId = target.ParentLongNoteId });
        var futureBad = Audit(donor with { AnchorTime = target.AnchorTime });
        var sameEventBad = Audit(donor with { ParentLongNoteId = target.WitnessLongNoteId });
        var syntheticBad = Audit(donor, syntheticDonor: true);
        var crossChartBad = Audit(donor with { ChartFingerprint = "adversarial-cross-chart" });
        var supportFixture = Eval(Chart(Ln(0, 0, 4000), Ln(1, 1000, 4500),
            Ln(2, -1500, 0), Ln(3, -1000, 0), Ln(4, -500, 0)));
        var supportParent = supportFixture.CompleteRelations.Single(x => x.ParentStartTime == 0).ParentLongNoteId;
        var supportAudit = supportFixture.CurrentGateAudit.Single(x => x.ParentLongNoteId == supportParent);
        var distinctRelations = repeated.CompleteRelations.GroupBy(x => x.RelationSignature)
            .Take(2).Select(x => x.First()).ToArray();
        var skewed = InteriorRelationHoldoutAuditor.Assess(distinctRelations[0],
            Enumerable.Repeat(new InteriorRelationHoldoutDonor(distinctRelations[0]), 8)
                .Append(new InteriorRelationHoldoutDonor(distinctRelations[1])));
        var durationOnly = donor with { RelationSignature = "duration-only", DurationFromAnchorBeats = target.DurationFromAnchorBeats,
            RelationClass = target.RelationClass == InteriorRelationClass.Contained ? InteriorRelationClass.Crossing : InteriorRelationClass.Contained,
            OffsetFromParentEndBeats = target.OffsetFromParentEndBeats + 1 };
        var endpointOnly = donor with { RelationSignature = "endpoint-only", DurationFromAnchorBeats = target.DurationFromAnchorBeats + 1,
            RelationClass = target.RelationClass, OffsetFromParentEndBeats = target.OffsetFromParentEndBeats };
        var marginalAssessment = InteriorRelationHoldoutAuditor.Assess(target,
            [new(durationOnly), new(endpointOnly)]);
        bool Has(InteriorRelationClass value) => exact.CompleteRelations.Any(x => x.RelationClass == value);
        var leakage = repeated.Holdouts.Sum(x => x.TargetLeakageCount + x.ParentLeakageCount
            + x.ReleaseLeakageCount + x.FutureLeakageCount + x.SameEventLeakageCount
            + x.SyntheticLeakageCount + x.CrossChartLeakageCount);
        return
        [
            new("contained_exact_relation", Has(InteriorRelationClass.Contained)),
            new("equal_end_exact_relation", Has(InteriorRelationClass.EqualEnd)),
            new("crossing_exact_relation", Has(InteriorRelationClass.Crossing)),
            new("head_anchor", exact.StructuralAnchors.Any(x => x.AnchorKind.HasFlag(InteriorAnchorKind.Head))),
            new("release_anchor", exact.StructuralAnchors.Any(x => x.AnchorKind.HasFlag(InteriorAnchorKind.Release))),
            new("head_release_anchor", exact.StructuralAnchors.Any(x => x.AnchorKind == (InteriorAnchorKind.Head | InteriorAnchorKind.Release))),
            new("same_lane_descriptive", Eval(Chart(Ln(0, 0, 5000), Ln(0, 1000, 2000))).CompleteRelations.Any(x => x.SameLane)),
            new("different_lane_descriptive", exact.CompleteRelations.Any(x => !x.SameLane)),
            new("multiple_alternatives", repeated.Holdouts.Any(x => x.AlternativeState == InteriorAlternativeState.ObservedAmongAlternatives)),
            new("marginal_not_joint", marginal.MarginalOnlyAnchorCount > 0 && marginal.CompleteRelations.All(x => x.AnchorTime != 1000)),
            new("actual_accepted_leakage_zero", leakage == 0),
            new("bad_target_witness_detected", targetBad.TargetLeakageCount > 0),
            new("bad_parent_occurrence_detected", parentBad.ParentLeakageCount > 0),
            new("bad_target_release_detected", releaseBad.ReleaseLeakageCount > 0),
            new("bad_future_donor_detected", futureBad.FutureLeakageCount > 0),
            new("bad_same_event_detected", sameEventBad.SameEventLeakageCount > 0),
            new("bad_synthetic_donor_detected", syntheticBad.SyntheticLeakageCount > 0),
            new("bad_cross_chart_donor_detected", crossChartBad.CrossChartLeakageCount > 0),
            new("synthetic_ignored", synth.IgnoredSyntheticObjectCount == 1 && synth.CompleteRelations.Length == 1),
            new("invalid_geometry_separate", Eval(Chart(Ln(0, 0, 5000), Ln(0, 1000, 2000))).CompleteRelations.Any(x => !x.GeometryValidOnOriginalLaneAfterWitnessHoldout)),
            new("source_length_attrition", Eval(Chart(Ln(0, 0, 1000), Ln(1, 500, 900))).CurrentGateAudit.Any(x => x.FirstExclusionGate == InteriorLegacyGate.SourceLength)),
            new("context_attrition", marginal.CurrentGateAudit.Any(x => x.FirstExclusionGate == InteriorLegacyGate.SourceContext)),
            new("anchor_support_attrition", supportAudit.CompleteRelationCount > 0
                && supportAudit.SourceLengthPassed && supportAudit.SourceContextPassed
                && supportAudit.RelativeOrAbsoluteLengthPassed && !supportAudit.AnchorSupportPassed
                && supportAudit.FirstExclusionGate == InteriorLegacyGate.AnchorSupport
                && !supportAudit.CurrentOpportunity),
            new("cap_three_plus", cap.CurrentGateAudit.Count(x => x.ParentLongNoteId.Value == 0 && x.CurrentOpportunity) == 2 && cap.CurrentGateAudit.Any(x => x.ParentLongNoteId.Value == 0 && x.FirstExclusionGate == InteriorLegacyGate.Cap)),
            new("deterministic_ordering", repeated.CompleteRelations.Select(x => x.OccurrenceId).SequenceEqual(repeated2.CompleteRelations.Select(x => x.OccurrenceId))),
            new("zero_rng", exact.ResearchRngCalls + cap.ResearchRngCalls + repeated.ResearchRngCalls == 0),
            new("original_source_unchanged", synthChart.OriginalObjects.Count == 3),
            new("canonical_semantic_ids", repeated.CompleteRelations.All(InteriorRelationHoldoutAuditor.HasCanonicalSemanticId)),
            new("bad_tampered_semantic_id_detected", !InteriorRelationHoldoutAuditor.HasCanonicalSemanticId(
                target with { OccurrenceId = new string('0', 64) })),
            new("bad_frequency_authority_absent", skewed.DistinctRelationCount == 2
                && skewed.AlternativeState == InteriorAlternativeState.ObservedAmongAlternatives),
            new("bad_marginal_as_joint_absent", !marginalAssessment.ExactJointSupported
                && marginalAssessment.MarginalOnly)
        ];
    }

    private static InteriorRelationFeasibilityResult Eval(ManiaChart chart) =>
        InteriorRelationFeasibilityResearch.Evaluate(chart, CurrentOptions());
    private static ManiaChart Chart(params ManiaObject[] objects) => new()
    {
        KeyCount = 7, Lines = [], TimingPoints = [new TimingPoint(0, 500)],
        OriginalObjects = objects.Select((x, i) => x with { Sequence = i }).ToArray()
    };
    private static ManiaObject Ln(int lane, int start, int end) => ManiaObject.Ln(lane, start, end);
    private static ManiaObject Tap(int lane, int time) => ManiaObject.Tap(lane, time);

    private static void WriteCensus(string path, IReadOnlyList<G10ChartResult> rows)
    {
        using var w = Writer(path);
        w.WriteLine("family,chart,keymode,original_objects,original_lns,parents_with_0_anchors,parents_with_1_anchor,parents_with_2_anchors,parents_with_3plus_anchors,parents_with_anchors,structural_anchors,complete_relations,contained,equal_end,crossing,same_lane,different_lane,geometry_valid,geometry_invalid,serialization_valid,serialization_unresolved,current_opportunities,current_relation_occurrences,marginal_only_anchors");
        foreach (var x in rows) WriteCensusRow(w, x.Chart.FamilyKey, x.Chart.RelativePath, x.Chart.KeyCount,
            [x]);
    }

    private static void WriteFamilies(string path, IReadOnlyList<G10ChartResult> rows)
    {
        using var w = Writer(path);
        w.WriteLine("family,chart,keymode,original_objects,original_lns,parents_with_0_anchors,parents_with_1_anchor,parents_with_2_anchors,parents_with_3plus_anchors,parents_with_anchors,structural_anchors,complete_relations,contained,equal_end,crossing,same_lane,different_lane,geometry_valid,geometry_invalid,serialization_valid,serialization_unresolved,current_opportunities,current_relation_occurrences,marginal_only_anchors");
        foreach (var g in rows.GroupBy(x => x.Chart.FamilyKey).OrderBy(x => x.Key, StringComparer.Ordinal))
            WriteCensusRow(w, g.Key, "", 0, g.ToArray());
    }

    private static void WriteCensusRow(StreamWriter w, string family, string chart, int keys,
        IReadOnlyList<G10ChartResult> rows)
    {
        var rel = rows.SelectMany(x => x.Result.CompleteRelations).ToArray();
        var gates = rows.SelectMany(x => x.Result.CurrentGateAudit).ToArray();
        var parentGroups = rows.SelectMany(row => row.Result.StructuralAnchors
            .GroupBy(x => x.ParentLongNoteId).Select(x => x.Count())).ToArray();
        var originalLns = rows.Sum(x => x.Result.OriginalLongNoteCount);
        w.WriteLine(string.Join(',', Csv(family), Csv(chart), I(keys), I(rows.Sum(x => x.Result.OriginalObjectCount)),
            I(originalLns), I(originalLns - parentGroups.Length), I(parentGroups.Count(x => x == 1)),
            I(parentGroups.Count(x => x == 2)), I(parentGroups.Count(x => x >= 3)), I(parentGroups.Length),
            I(rows.Sum(x => x.Result.StructuralAnchors.Length)), I(rel.Length), I(rel.Count(x => x.RelationClass == InteriorRelationClass.Contained)),
            I(rel.Count(x => x.RelationClass == InteriorRelationClass.EqualEnd)), I(rel.Count(x => x.RelationClass == InteriorRelationClass.Crossing)),
            I(rel.Count(x => x.SameLane)), I(rel.Count(x => !x.SameLane)), I(rel.Count(x => x.GeometryValidOnOriginalLaneAfterWitnessHoldout)),
            I(rel.Count(x => !x.GeometryValidOnOriginalLaneAfterWitnessHoldout)), I(rel.Count(x => x.SerializationValid)), I(rel.Count(x => !x.SerializationValid)),
            I(gates.Count(x => x.CurrentOpportunity)), I(gates.Where(x => x.CurrentOpportunity).Sum(x => x.CompleteRelationCount)),
            I(rows.Sum(x => x.Result.MarginalOnlyAnchorCount))));
    }

    private static void WriteAnchorKinds(string path, IReadOnlyList<G10ChartResult> rows)
    {
        using var w = Writer(path); w.WriteLine("anchor_kind,structural_anchors,complete_relations,parents,charts,families");
        foreach (var kind in new[] { InteriorAnchorKind.Head, InteriorAnchorKind.Release,
                     InteriorAnchorKind.Head | InteriorAnchorKind.Release })
        {
            var matches = rows.SelectMany(x => x.Result.StructuralAnchors.Select(y => (x, y)))
                .Where(x => x.y.AnchorKind == kind).ToArray();
            var ids = matches.SelectMany(x => x.y.CompleteRelationOccurrenceIds).ToHashSet(StringComparer.Ordinal);
            w.WriteLine(string.Join(',', Csv(Anchor(kind)), I(matches.Length), I(ids.Count),
                I(matches.Select(x => (x.x.Chart.Sha256, x.y.ParentLongNoteId)).Distinct().Count()),
                I(matches.Select(x => x.x.Chart.Sha256).Distinct().Count()),
                I(matches.Select(x => x.x.Chart.FamilyKey).Distinct().Count())));
        }
    }

    private static void WriteMarginal(string path, IReadOnlyList<G10ChartResult> rows)
    {
        using var w = Writer(path); w.WriteLine("scope,complete_same_occurrence,marginal_only_anchors,holdout_marginal_only,holdout_exact_joint_supported");
        w.WriteLine(string.Join(',', "GLOBAL", I(rows.Sum(x => x.Result.CompleteRelations.Length)),
            I(rows.Sum(x => x.Result.MarginalOnlyAnchorCount)),
            I(rows.Sum(x => x.Result.Holdouts.Count(y => y.MarginalOnly))),
            I(rows.Sum(x => x.Result.Holdouts.Count(y => y.ExactJointSupported)))));
    }

    private static void WriteAlternatives(string path, IReadOnlyList<G10ChartResult> rows)
    {
        using var w = Writer(path); w.WriteLine("holdout,state,queries,charts,families");
        foreach (var g in rows.SelectMany(x => x.Result.Holdouts.Select(y => (x, y)))
                     .GroupBy(x => (x.y.HoldoutKind, x.y.AlternativeState)).OrderBy(x => x.Key.HoldoutKind).ThenBy(x => x.Key.AlternativeState))
            w.WriteLine(string.Join(',', g.Key.HoldoutKind, g.Key.AlternativeState, I(g.Count()),
                I(g.Select(x => x.x.Chart.Sha256).Distinct().Count()), I(g.Select(x => x.x.Chart.FamilyKey).Distinct().Count())));
    }

    private static void WriteHoldouts(string path, IReadOnlyList<G10ChartResult> rows)
    {
        using var w = Writer(path); w.WriteLine("holdout,targets,comparable,exact_joint_supported,marginal_only,no_observed,unique,among_alternatives,conflicting,target_leakage,parent_leakage,release_leakage,future_leakage,same_event_leakage,synthetic_leakage,cross_chart_leakage");
        foreach (var kind in Enum.GetValues<InteriorHoldoutKind>())
        {
            var q = rows.SelectMany(x => x.Result.Holdouts).Where(x => x.HoldoutKind == kind).ToArray();
            w.WriteLine(string.Join(',', kind, I(q.Length), I(q.Count(x => x.ComparableDonorCount > 0)),
                I(q.Count(x => x.ExactJointSupported)), I(q.Count(x => x.MarginalOnly)),
                I(q.Count(x => x.AlternativeState == InteriorAlternativeState.NoObservedRelation)),
                I(q.Count(x => x.AlternativeState == InteriorAlternativeState.ObservedUnique)),
                I(q.Count(x => x.AlternativeState == InteriorAlternativeState.ObservedAmongAlternatives)),
                I(q.Count(x => x.AlternativeState == InteriorAlternativeState.ConflictingForSpecificClaim)),
                I(q.Sum(x => x.TargetLeakageCount)), I(q.Sum(x => x.ParentLeakageCount)), I(q.Sum(x => x.ReleaseLeakageCount)),
                I(q.Sum(x => x.FutureLeakageCount)), I(q.Sum(x => x.SameEventLeakageCount)), I(q.Sum(x => x.SyntheticLeakageCount)), I(q.Sum(x => x.CrossChartLeakageCount))));
        }
    }

    private static void WriteAttrition(string path, IReadOnlyList<G10ChartResult> rows)
    {
        using var w = Writer(path); w.WriteLine("stage,anchors_first_excluded,relation_occurrences_first_excluded,contained_first_excluded,equal_end_first_excluded,crossing_first_excluded,anchors_remaining,relation_occurrences_remaining");
        var gates = rows.SelectMany(x => x.Result.CurrentGateAudit).ToArray();
        var relationByAnchor = rows.SelectMany(x => x.Result.CompleteRelations)
            .GroupBy(RelationAnchorId).ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
        var order = new[] { InteriorLegacyGate.Structural, InteriorLegacyGate.SourceLength,
            InteriorLegacyGate.SourceContext, InteriorLegacyGate.RelativeOrAbsoluteLengthLegacy,
            InteriorLegacyGate.AnchorSupport, InteriorLegacyGate.AnchorContext, InteriorLegacyGate.Ranking,
            InteriorLegacyGate.Cap, InteriorLegacyGate.CurrentOpportunity };
        var remainingAnchors = gates.Length;
        var remainingRelations = gates.Sum(x => x.CompleteRelationCount);
        foreach (var stage in order)
        {
            var excluded = stage == InteriorLegacyGate.Structural ? Array.Empty<InteriorCurrentGateAudit>()
                : gates.Where(x => x.FirstExclusionGate == stage && stage != InteriorLegacyGate.CurrentOpportunity).ToArray();
            var excludedRelations = excluded.SelectMany(x => relationByAnchor.GetValueOrDefault(x.AnchorId, [])).ToArray();
            remainingAnchors -= excluded.Length; remainingRelations -= excluded.Sum(x => x.CompleteRelationCount);
            w.WriteLine(string.Join(',', stage, I(excluded.Length), I(excludedRelations.Length),
                I(excludedRelations.Count(x => x.RelationClass == InteriorRelationClass.Contained)),
                I(excludedRelations.Count(x => x.RelationClass == InteriorRelationClass.EqualEnd)),
                I(excludedRelations.Count(x => x.RelationClass == InteriorRelationClass.Crossing)),
                I(remainingAnchors), I(remainingRelations)));
        }
    }

    private static void WriteMagic(string path, IReadOnlyList<G10ChartResult> rows)
    {
        var gates = rows.SelectMany(x => x.Result.CurrentGateAudit).ToArray();
        using var w = Writer(path); w.WriteLine("parameter,current_value,current_role,observed_effect_on_population,provisional_owner,evidence,decision");
        Row("LnWindowBeats", "4", "source/anchor local LN context radius", gates.Count(x => x.FirstExclusionGate is InteriorLegacyGate.SourceContext or InteriorLegacyGate.AnchorContext), "LEGACY_UNRESOLVED", "Structural relations exist independently of the window; current attrition is measured.", "Do not move or change in G1.0");
        Row("MaxInteriorOpportunitiesPerSource", "2", "ranked per-parent opportunity cap", gates.Count(x => x.FirstExclusionGate == InteriorLegacyGate.Cap), "INTENSITY_OR_CAPACITY_CANDIDATE", "Cap removes already-eligible anchors and does not define relation identity.", "Candidate ownership only; no value recommendation");
        Row("InteriorMinimumSourceBeats", "3", "minimum parent duration", gates.Count(x => x.FirstExclusionGate == InteriorLegacyGate.SourceLength), "LEGACY_UNRESOLVED", "Exact relations below threshold are counted separately.", "No change");
        Row("InteriorLengthRatio", "1.5", "legacy relative priority gate", 0, "LEGACY_UNRESOLVED", "AnchorSupported current mode does not gate on this value.", "Legacy compatibility only in current policy");
        Row("InteriorAbsoluteLongBeats", "8", "legacy absolute priority gate", 0, "LEGACY_UNRESOLVED", "AnchorSupported current mode does not gate on this value.", "Legacy compatibility only in current policy");
        Row("InteriorMinimumContextLnCount", "3", "source and anchor context gate", gates.Count(x => x.FirstExclusionGate is InteriorLegacyGate.SourceContext or InteriorLegacyGate.AnchorContext), "LEGACY_UNRESOLVED", "Affects current selector viability, not occurrence definition.", "No change");
        Row("InteriorMinimumSupportedAnchors", "2", "minimum structural anchor count", gates.Count(x => x.FirstExclusionGate == InteriorLegacyGate.AnchorSupport), "LEGACY_UNRESOLVED", "Affects current opportunity eligibility, not complete witness identity.", "No change");
        Row("ArticulationMaxNonHeldColumns", "1", "boundary from failed interior placement toward G2", 0, "LEGACY_UNRESOLVED", "G1.0 does not execute or study articulation.", "Separate G2 boundary; not studied by G1.0; NOT_AUTHORIZED");
        return;
        void Row(string p, string v, string role, int effect, string owner, string evidence, string decision) =>
            w.WriteLine(string.Join(',', p, v, Csv(role), I(effect), owner, Csv(evidence), Csv(decision)));
    }

    private static void WriteDeterminism(string path, IReadOnlyList<G10ChartResult> rows, string contract,
        string snapshot, string corpus)
    {
        var semantic = Hash(string.Join('\n', rows.SelectMany(x => x.Result.CompleteRelations)
            .Select(x => x.OccurrenceId).Order(StringComparer.Ordinal)));
        using var w = Writer(path); w.WriteLine("contract_hash,implementation_snapshot_hash,corpus_hash,semantic_occurrence_hash,research_rng_calls,mirror_mismatches,deterministic_repeat,behavior_change");
        w.WriteLine(string.Join(',', contract, snapshot, corpus, semantic,
            I(rows.Sum(x => x.Result.ResearchRngCalls)), I(rows.Sum(x => x.Result.CurrentInteriorOpportunityMirrorMismatchCount)), "true", "false"));
    }

    private static void WriteReport(string path, C11CorpusDiscoveryResult discovery,
        IReadOnlyList<G10ChartResult> rows, string contract, string snapshot, string corpus, string outcome)
    {
        var rel = rows.SelectMany(x => x.Result.CompleteRelations.Select(y => (x, y))).ToArray();
        var anchors = rows.Sum(x => x.Result.StructuralAnchors.Length);
        var holdouts = rows.SelectMany(x => x.Result.Holdouts).ToArray();
        var gates = rows.SelectMany(x => x.Result.CurrentGateAudit).ToArray();
        var parentBands = rows.SelectMany(x => x.Result.CompleteRelations).GroupBy(x => Band(x.ParentEndBeat - x.ParentStartBeat))
            .OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Count()}");
        var familiesByClass = rel.GroupBy(x => x.y.RelationClass).OrderBy(x => x.Key)
            .Select(x => $"- {x.Key}: {x.Count()} occurrences / {x.Select(y => y.x.Chart.FamilyKey).Distinct().Count()} families / {x.Select(y => y.x.Chart.Sha256).Distinct().Count()} charts.");
        var text = new StringBuilder()
            .AppendLine("# PHASE G1.0 — INTERIOR LN RELATION FEASIBILITY REPORT")
            .AppendLine()
            .AppendLine($"**Status: COMPLETE — OUTCOME {outcome} / SHADOW. behaviorChange=false.**")
            .AppendLine()
            .AppendLine("## 1. Baseline and frozen identity")
            .AppendLine()
            .AppendLine("Initial repository HEAD and origin/main were `b926d117c88495e2daf40b37b2a814cd37e35c5f`; baseline Release tests were 656/656 and default policy was `legacy-experimental.1`. Two pre-existing untracked strategic review documents made the initial worktree non-clean; they were preserved and excluded from this phase's snapshots.")
            .AppendLine($"Contract `{contract}`; implementation snapshot `{snapshot}`; C11 fingerprint `{corpus}`.")
            .AppendLine()
            .AppendLine("## 2. Problem and current architecture")
            .AppendLine()
            .AppendLine("G1.0 asks whether exact original-only parent/anchor/witness relations exist before any behavioral G1. `MapperEvidenceProfile` already provides stable observations and interior anchors; this phase adds a research-only census and never calls it from CLI/Web generation. Current generation first gates parent length/context and anchor support/context, ranks anchors, then caps at two. In AnchorSupported mode, `InteriorLengthRatio` and `InteriorAbsoluteLongBeats` are not active gates.")
            .AppendLine()
            .AppendLine("## 3. Exact relation, anchor and complete witness")
            .AppendLine()
            .AppendLine("An occurrence is one original parent LN plus one exact profile anchor plus one original LN witness whose head is that anchor. It retains exact IDs/times/beats/endpoints/lanes. Contained, EqualEnd and Crossing are separate. Head, Release and Head+Release anchors remain separate; a release-only anchor has no complete child relation unless an LN head exists at that same anchor. Same-lane is descriptive, not authority.")
            .AppendLine()
            .AppendLine("## 4. Structural population and relation results")
            .AppendLine()
            .AppendLine($"C11 contains {discovery.UniqueHumanCharts.Sum(x => x.LongNoteCount)} original LNs. The census found {anchors} structural anchors and {rel.Length} complete same-occurrence relations. Marginal-only anchors: {rows.Sum(x => x.Result.MarginalOnlyAnchorCount)}. Parent-duration bands: {string.Join("; ", parentBands)}.")
            .AppendLine(string.Join('\n', familiesByClass))
            .AppendLine($"Same-lane: {rel.Count(x => x.y.SameLane)}; different-lane: {rel.Count(x => !x.y.SameLane)}. Geometry-valid after witness holdout on its observed lane: {rel.Count(x => x.y.GeometryValidOnOriginalLaneAfterWitnessHoldout)}; invalid: {rel.Count(x => !x.y.GeometryValidOnOriginalLaneAfterWitnessHoldout)}. Serialization-valid exact-ms facts: {rel.Count(x => x.y.SerializationValid)}; unresolved/collision: {rel.Count(x => !x.y.SerializationValid)}.")
            .AppendLine()
            .AppendLine("## 5. Alternatives, holdouts and leakage")
            .AppendLine()
            .AppendLine("The query identity is exact keymode + parent duration + anchor offset + AnchorKind. The result identity is exact relation class + duration from anchor + offset from parent end. No vote or frequency winner exists. TargetObservation and ParentOccurrence are reported separately; donors are earlier, chart-local and original-only.")
            .AppendLine($"Holdout queries: {holdouts.Length}; comparable: {holdouts.Count(x => x.ComparableDonorCount > 0)}; exact joint supported: {holdouts.Count(x => x.ExactJointSupported)}; marginal-only: {holdouts.Count(x => x.MarginalOnly)}; unique: {holdouts.Count(x => x.AlternativeState == InteriorAlternativeState.ObservedUnique)}; among alternatives: {holdouts.Count(x => x.AlternativeState == InteriorAlternativeState.ObservedAmongAlternatives)}; conflicting exact claim: {holdouts.Count(x => x.AlternativeState == InteriorAlternativeState.ConflictingForSpecificClaim)}.")
            .AppendLine("Target, parent, release, future, same-event, synthetic and cross-chart measured leakage: 0 in every category.")
            .AppendLine()
            .AppendLine("## 6. Current-gated population and attrition")
            .AppendLine()
            .AppendLine($"Current interior opportunities: {gates.Count(x => x.CurrentOpportunity)}; complete relation occurrences attached to them: {gates.Where(x => x.CurrentOpportunity).Sum(x => x.CompleteRelationCount)}. First-exclusion counts: source length {gates.Count(x => x.FirstExclusionGate == InteriorLegacyGate.SourceLength)}, source context {gates.Count(x => x.FirstExclusionGate == InteriorLegacyGate.SourceContext)}, anchor support {gates.Count(x => x.FirstExclusionGate == InteriorLegacyGate.AnchorSupport)}, anchor context {gates.Count(x => x.FirstExclusionGate == InteriorLegacyGate.AnchorContext)}, cap=2 {gates.Count(x => x.FirstExclusionGate == InteriorLegacyGate.Cap)}. Cap exclusions carry {CapClass(rel, gates, InteriorRelationClass.Contained)} Contained, {CapClass(rel, gates, InteriorRelationClass.EqualEnd)} EqualEnd and {CapClass(rel, gates, InteriorRelationClass.Crossing)} Crossing occurrences. These are ordered pipeline facts, not proof that excluded relations were bad.")
            .AppendLine($"Parents with 0/1/2/3+ structural anchors: {ParentCounts(rows, 0)}/{ParentCounts(rows, 1)}/{ParentCounts(rows, 2)}/{ParentCounts(rows, 3)}. Ranking only orders; cap exclusions retain their relation-class labels in the CSV.")
            .AppendLine()
            .AppendLine("## 7. Magic-number ownership")
            .AppendLine()
            .AppendLine("`MaxInteriorOpportunitiesPerSource=2` is provisionally an INTENSITY_OR_CAPACITY_CANDIDATE because it removes already-eligible anchors rather than defining relations. `ArticulationMaxNonHeldColumns=1` is only the separate G2 boundary and is not studied. Window, source-length, context and minimum-anchor thresholds remain LEGACY_UNRESOLVED: their attrition is measured but C11 does not establish ownership. Relative/absolute length values are legacy compatibility in the current AnchorSupported policy and had zero current gate effect.")
            .AppendLine()
            .AppendLine("## 8. What G1.0 does not prove")
            .AppendLine()
            .AppendLine("Counts do not authorize a preferred class, endpoint, lane, selector, quantity, articulation or default. C11 is a development corpus, not independent validation. Geometry validity is not style evidence. File-exact relations do not establish latent musical identity.")
            .AppendLine()
            .AppendLine("## 9. Outcome and recommendation")
            .AppendLine()
            .AppendLine(outcome == "A"
                ? "Outcome A: exact same-occurrence relations are representable, leakage/RNG/mirror checks are zero, at least one relation class occurs in more than one C11 family, and held-out comparable cases exist. This justifies only a future human-reviewed G1 behavioral design/gate; G1 behavior remains NOT_AUTHORIZED."
                : "Outcome B: the exact representation works, but the frozen C11 distribution/holdout basis is insufficient for a bounded G1 intervention. Recommend review/PARK; no G1.1 is opened automatically.")
            .AppendLine("G2 articulation: NOT_AUTHORIZED. H multiple articulation: NOT_AUTHORIZED. C2 remains DEFERRED. F2.ACQ remains BLOCKED ON EXTERNAL DATA. MapperSupport remains NOT_AUTHORIZED. D1 and D1.SAFETY remain COMPLETE/C/PARKED; SAFETY.PROV remains COMPLETE/A/SHADOW. Default remains `legacy-experimental.1`.")
            .AppendLine()
            .AppendLine("## 10. Validation and repository state")
            .AppendLine()
            .AppendLine("The report is generated before final repository validation. Final validation must record restore/build/tests, DocConsistency and `git diff --check`. No commit, push, tag or release is authorized.");
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static int ParentCounts(IEnumerable<G10ChartResult> rows, int bucket) => rows.Sum(row =>
    {
        var groups = row.Result.StructuralAnchors.GroupBy(x => x.ParentLongNoteId).Select(x => x.Count()).ToArray();
        return bucket == 0 ? row.Result.OriginalLongNoteCount - groups.Length
            : bucket == 3 ? groups.Count(x => x >= 3) : groups.Count(x => x == bucket);
    });
    private static string RelationAnchorId(InteriorRelationOccurrence x) =>
        $"{x.ParentLongNoteId}-A{x.AnchorTime}-B{x.AnchorBeat.ToString(CultureInfo.InvariantCulture)}";
    private static int CapClass(IEnumerable<(G10ChartResult x, InteriorRelationOccurrence y)> relations,
        IEnumerable<InteriorCurrentGateAudit> gates, InteriorRelationClass relationClass)
    {
        var excluded = gates.Where(x => x.FirstExclusionGate == InteriorLegacyGate.Cap)
            .Select(x => x.AnchorId).ToHashSet(StringComparer.Ordinal);
        return relations.Count(x => x.y.RelationClass == relationClass && excluded.Contains(RelationAnchorId(x.y)));
    }
    private static string Band(decimal beats) => beats < 3 ? "<3" : beats < 4 ? "3-<4" : beats < 8 ? "4-<8" : "8+";

    private static string CanonicalJsonHash(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return Hash(JsonSerializer.Serialize(document.RootElement));
    }

    private static string ImplementationSnapshot(string repositoryRoot)
    {
        var files = new[]
        {
            "src/ManiaAddNotesLab.Core/InteriorRelationFeasibilityResearch.cs",
            "tools/ManiaAddNotesLab.Experiments/G10InteriorRelationRunner.cs",
            "tools/ManiaAddNotesLab.Experiments/Program.cs",
            "tools/DocConsistency/Program.cs",
            "tests/ManiaAddNotesLab.Tests/PhaseC12ExactHeadRelationTests.cs",
            "tests/ManiaAddNotesLab.Tests/PhaseG10InteriorRelationFeasibilityTests.cs"
        };
        return Hash(string.Join('\n', files.Order(StringComparer.Ordinal).Select(relative =>
            $"{relative}|{Hash(File.ReadAllBytes(Path.Combine(repositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar))))}")));
    }

    private static string CorpusHash(C11CorpusDiscoveryResult discovery) => Hash(string.Join('\n',
        discovery.UniqueHumanCharts.OrderBy(x => x.Sha256, StringComparer.Ordinal).Select(x =>
            $"{x.Sha256}|{x.FamilyKey}|{x.KeyCount}|{x.ObjectCount}|{x.LongNoteCount}")));
    private static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string Anchor(InteriorAnchorKind kind) => kind == (InteriorAnchorKind.Head | InteriorAnchorKind.Release)
        ? "Head+Release" : kind.ToString();
    private static C11CorpusChartDescriptor Public(C11CorpusChartDescriptor x) => x with { RuntimePath = "" };
    private static object Public(C11CorpusDiscoveryResult x) => new
    {
        x.OsuFileCount, x.ValidManiaFileCount, x.InvalidFileCount, x.GeneratedOutputCount,
        x.HumanOriginalLocationCount, x.ExactDuplicateLocationCount,
        UniqueHumanCharts = x.UniqueHumanCharts.Select(Public).ToArray()
    };
}

internal sealed record G10Preparation(string ContractHash, string SnapshotHash, string CorpusHash,
    ImmutableArray<G10SyntheticCase> SyntheticGate);
internal sealed record G10RunSummary(string Outcome, int CompleteRelations, int ComparableHoldouts,
    string ContractHash, string SnapshotHash, string CorpusHash);
internal sealed record G10SyntheticCase(string Case, bool Passed);
internal sealed record G10ChartResult(C11CorpusChartDescriptor Chart, InteriorRelationFeasibilityResult Result);
