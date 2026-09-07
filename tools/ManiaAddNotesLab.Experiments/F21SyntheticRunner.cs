using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class F21SyntheticRunner
{
    public static void Run(string outputDirectory, string detailPath)
    {
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(detailPath)!);
        var cases = BuildCases();
        WriteModels(Path.Combine(outputDirectory, "f2_1_identity_model_summary.csv"), cases);
        WriteSynthetic(Path.Combine(outputDirectory, "f2_1_synthetic_validation_summary.csv"), cases);
        WriteNegative(Path.Combine(outputDirectory, "f2_1_negative_control_summary.csv"), cases);
        WriteTiming(Path.Combine(outputDirectory, "f2_1_timing_segment_summary.csv"), cases);
        var endpoint = EndpointCase();
        WriteEndpoint(Path.Combine(outputDirectory, "f2_1_endpoint_holdout_summary.csv"), endpoint);
        var detail = new Detail(ExactGapTimingIdentityResearch.ResearchSchemaVersion, cases, endpoint);
        File.WriteAllText(detailPath, JsonSerializer.Serialize(detail, JsonOptions()), new UTF8Encoding(false));
        Console.WriteLine($"Wrote F2.1 synthetic artifacts: cases={cases.Length}, detail={new FileInfo(detailPath).Length} bytes.");
    }

    private static SyntheticCase[] BuildCases()
    {
        var integral = Evaluate("integral_half_beat", true, false,
            Chart([new(0, 500)], Tap(0, 0), Tap(0, 250), Tap(0, 500)), 0, 1);
        var alternating = Evaluate("alternating_serialized_half_beat", true, false,
            Chart([new(0, 499)], Tap(0, 0), Tap(0, 250), Tap(0, 499)), 0, 1);
        var nearby = Evaluate("nearby_intentionally_distinct", false, true,
            Chart([new(0, 500)], Tap(0, 0), Tap(0, 249), Tap(1, 1000), Tap(1, 1250)), 0, 1);
        var sameMillisecondsDifferentBpm = Evaluate("same_ms_different_beat_relation", false, true,
            Chart([new(0, 500), new(1000, 250)],
                Tap(0, 0), Tap(0, 250), Tap(1, 1000), Tap(1, 1250)), 0, 1);
        var differentKind = Evaluate("different_transition_kind", false, true,
            Chart([new(0, 500)], Tap(0, 0), Tap(0, 250), Ln(1, 1000, 1500), Tap(1, 1750)), 0, 1);
        var redundantBoundaryResult = ExactGapTimingIdentityResearch.Evaluate(Chart(
            [new(0, 500), new(1000, 500)], Tap(0, 250), Tap(0, 750), Tap(1, 750), Tap(1, 1250)));
        var redundant = Compare("redundant_redline_same_relation", true, false,
            redundantBoundaryResult.Occurrences.Single(x => x.Lane == 0),
            redundantBoundaryResult.Occurrences.Single(x => x.Lane == 1));
        var crossBpmResult = ExactGapTimingIdentityResearch.Evaluate(Chart(
            [new(0, 500), new(1000, 250)], Tap(0, 750), Tap(0, 1125)));
        var crossBpm = DescribeSingle("cross_bpm_exact_path", crossBpmResult.Occurrences.Single());
        var lnCrossResult = ExactGapTimingIdentityResearch.Evaluate(Chart(
            [new(0, 500), new(1000, 250)], Ln(0, 750, 1125), Tap(0, 1375)));
        var lnCross = DescribeSingle("ln_head_release_cross_bpm", lnCrossResult.Occurrences.Single());
        return [integral, alternating, nearby, sameMillisecondsDifferentBpm, differentKind,
            redundant, crossBpm, lnCross];
    }

    private static SyntheticCase Evaluate(string name, bool expectedEquivalent, bool negativeControl,
        ManiaChart chart, int left, int right)
    {
        var occurrences = ExactGapTimingIdentityResearch.Evaluate(chart).Occurrences;
        return Compare(name, expectedEquivalent, negativeControl, occurrences[left], occurrences[right]);
    }

    private static SyntheticCase Compare(string name, bool expectedEquivalent, bool negativeControl,
        GapTimingIdentityOccurrence left, GapTimingIdentityOccurrence right)
    {
        var fileEqual = ExactGapTimingIdentityResearch.Equivalent(left.FileExactIdentity, right.FileExactIdentity);
        var traversalEqual = ExactGapTimingIdentityResearch.Equivalent(left.ExactSegmentTraversalIdentity,
            right.ExactSegmentTraversalIdentity);
        return new SyntheticCase(name, expectedEquivalent, negativeControl, left.TransitionKind, right.TransitionKind,
            left.FileExactGapBeats, right.FileExactGapBeats, fileEqual, traversalEqual,
            expectedEquivalent && !fileEqual, !expectedEquivalent && fileEqual,
            expectedEquivalent && !traversalEqual, !expectedEquivalent && traversalEqual,
            left.SameTimingSegment, right.SameTimingSegment,
            left.TimingPointBoundariesCrossed, right.TimingPointBoundariesCrossed,
            left.PreviousLongNoteCrossesTimingSegment || right.PreviousLongNoteCrossesTimingSegment);
    }

    private static SyntheticCase DescribeSingle(string name, GapTimingIdentityOccurrence value) =>
        new(name, true, false, value.TransitionKind, value.TransitionKind,
            value.FileExactGapBeats, value.FileExactGapBeats, true, true,
            false, false, false, false, value.SameTimingSegment, value.SameTimingSegment,
            value.TimingPointBoundariesCrossed, value.TimingPointBoundariesCrossed,
            value.PreviousLongNoteCrossesTimingSegment);

    private static EndpointCaseData EndpointCase()
    {
        var result = ExactGapTimingIdentityResearch.Evaluate(Chart([new(0, 500)],
            Ln(0, 0, 1000), Tap(0, 1250), Ln(1, 500, 1000), Tap(1, 1500)));
        var target = result.Occurrences.Single(x => x.Lane == 0);
        GapTimingEvidenceAssessment A(GapTimingHoldoutKind holdout) => result.Assessments.Single(x =>
            x.TargetPreviousObservationId == target.PreviousObservationId
            && x.TargetNextObservationId == target.NextObservationId
            && x.IdentityKind == GapTimingIdentityKind.FileExactDecimal && x.HoldoutKind == holdout);
        return new EndpointCaseData(result.SharedReleaseEndpointTargets,
            result.EndpointHoldoutAdditionalExcludedReferences, result.TargetEndpointLeakageCount,
            target.F2HeadGroupExclusion.Length, target.TransitionEndpointGroupExclusion.Length,
            A(GapTimingHoldoutKind.F2HeadGroup), A(GapTimingHoldoutKind.TransitionEndpointGroup));
    }

    private static void WriteModels(string path, IReadOnlyList<SyntheticCase> cases)
    {
        using var w = Writer(path);
        w.WriteLine("identity_model,implemented,exact_file_derivation,equivalence_valid,alternating_nominal_relation,negative_false_merges,general_enough,disposition");
        var alternating = cases.Single(x => x.Name == "alternating_serialized_half_beat");
        w.WriteLine($"FileExactDecimal,true,true,true,{Pass(!alternating.FileExactFalseSplit)},{I(cases.Count(x => x.FileExactFalseMerge))},false,baseline_only");
        w.WriteLine($"ExactSegmentTraversal,true,true,true,{Pass(!alternating.TraversalFalseSplit)},{I(cases.Count(x => x.TraversalFalseMerge))},false,rejected_as_musical_identity");
        w.WriteLine("ExactMillisecondInterval,false,true,true,not_applicable,1,false,rejected_theoretically_false_merge_across_bpm");
        w.WriteLine("ExactRationalDeltaMsOverBeatLength,false,true,true,fail,0,false,equivalent_to_file_exact_same_segment");
        w.WriteLine("EndpointPhaseRelation,false,true,true,not_applicable,0,false,rejected_position_dependent");
        w.WriteLine("NearestSubdivision,false,false,not_applicable,not_applicable,unknown,false,prohibited_requires_quantization_policy");
        w.WriteLine("PairwiseEpsilon,false,false,false,not_applicable,unknown,false,prohibited_non_transitive");
        w.WriteLine("FrequencyChosenClass,false,false,not_applicable,not_applicable,unknown,false,prohibited_frequency_authority");
    }

    private static void WriteSynthetic(string path, IReadOnlyList<SyntheticCase> cases)
    {
        using var w = Writer(path);
        w.WriteLine("scenario,expected_equivalent,negative_control,left_transition,right_transition,left_file_exact_gap,right_file_exact_gap,file_exact_equal,segment_traversal_equal,file_exact_false_split,file_exact_false_merge,traversal_false_split,traversal_false_merge");
        foreach (var x in cases.OrderBy(x => x.Name, StringComparer.Ordinal))
            w.WriteLine(string.Join(',', x.Name, B(x.ExpectedEquivalent), B(x.NegativeControl),
                x.LeftTransition, x.RightTransition, D(x.LeftFileExactGap), D(x.RightFileExactGap),
                B(x.FileExactEqual), B(x.TraversalEqual), B(x.FileExactFalseSplit),
                B(x.FileExactFalseMerge), B(x.TraversalFalseSplit), B(x.TraversalFalseMerge)));
    }

    private static void WriteNegative(string path, IReadOnlyList<SyntheticCase> cases)
    {
        using var w = Writer(path);
        w.WriteLine("scenario,file_exact_false_merge,traversal_false_merge,exact_ms_false_merge_risk");
        foreach (var x in cases.Where(x => x.NegativeControl).OrderBy(x => x.Name, StringComparer.Ordinal))
            w.WriteLine(string.Join(',', x.Name, B(x.FileExactFalseMerge), B(x.TraversalFalseMerge),
                B(x.Name == "same_ms_different_beat_relation")));
    }

    private static void WriteTiming(string path, IReadOnlyList<SyntheticCase> cases)
    {
        using var w = Writer(path);
        w.WriteLine("scenario,left_same_segment,right_same_segment,left_boundaries_crossed,right_boundaries_crossed,previous_ln_crosses_segment");
        foreach (var x in cases.OrderBy(x => x.Name, StringComparer.Ordinal))
            w.WriteLine(string.Join(',', x.Name, B(x.LeftSameSegment), B(x.RightSameSegment),
                I(x.LeftBoundariesCrossed), I(x.RightBoundariesCrossed), B(x.PreviousLnCrossesSegment)));
    }

    private static void WriteEndpoint(string path, EndpointCaseData x)
    {
        using var w = Writer(path);
        w.WriteLine("fixture,holdout,excluded_observations,local_state,global_state,local_donors,global_donors,hypothetical_action,shared_release_targets,additional_endpoint_exclusions,endpoint_leakage");
        Row("shared_release_different_next", GapTimingHoldoutKind.F2HeadGroup, x.F2Excluded, x.F2Assessment);
        Row("shared_release_different_next", GapTimingHoldoutKind.TransitionEndpointGroup,
            x.EndpointExcluded, x.EndpointAssessment);
        void Row(string fixture, GapTimingHoldoutKind holdout, int excluded,
            GapTimingEvidenceAssessment assessment) => w.WriteLine(string.Join(',', fixture, holdout,
                I(excluded), assessment.LocalState, assessment.GlobalState, I(assessment.LocalComparableDonors),
                I(assessment.GlobalComparableDonors), assessment.HypotheticalAction,
                I(x.SharedReleaseTargets), I(x.AdditionalEndpointExclusions), I(x.EndpointLeakage)));
    }

    private static ManiaChart Chart(IReadOnlyList<TimingPoint> timings, params ManiaObject[] objects) => new()
    {
        KeyCount = 4,
        Lines = [],
        OriginalObjects = objects.Select((x, index) => x with { Sequence = index }).ToArray(),
        TimingPoints = timings
    };
    private static ManiaObject Tap(int lane, int time) => ManiaObject.Tap(lane, time);
    private static ManiaObject Ln(int lane, int start, int end) => ManiaObject.Ln(lane, start, end);
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string B(bool value) => value ? "true" : "false";
    private static string Pass(bool value) => value ? "pass" : "fail";
    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private sealed record Detail(string ResearchSchemaVersion, IReadOnlyList<SyntheticCase> SyntheticCases,
        EndpointCaseData EndpointHoldout);
    private sealed record EndpointCaseData(int SharedReleaseTargets, int AdditionalEndpointExclusions,
        int EndpointLeakage, int F2Excluded, int EndpointExcluded,
        GapTimingEvidenceAssessment F2Assessment, GapTimingEvidenceAssessment EndpointAssessment);
    private sealed record SyntheticCase(string Name, bool ExpectedEquivalent, bool NegativeControl,
        TypedGapTransitionKind LeftTransition, TypedGapTransitionKind RightTransition,
        decimal LeftFileExactGap, decimal RightFileExactGap, bool FileExactEqual, bool TraversalEqual,
        bool FileExactFalseSplit, bool FileExactFalseMerge, bool TraversalFalseSplit,
        bool TraversalFalseMerge, bool LeftSameSegment, bool RightSameSegment,
        int LeftBoundariesCrossed, int RightBoundariesCrossed, bool PreviousLnCrossesSegment);
}
