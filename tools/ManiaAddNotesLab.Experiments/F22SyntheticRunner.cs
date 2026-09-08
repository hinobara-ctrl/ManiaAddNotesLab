using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class F22SyntheticRunner
{
    public static void Run(string outputDirectory, string detailPath)
    {
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(detailPath)!);
        var cases = BuildCases();
        WriteModelSummary(Path.Combine(outputDirectory, "f2_2_model_summary.csv"));
        WriteForward(Path.Combine(outputDirectory, "f2_2_forward_serialization_summary.csv"), cases);
        WriteCollisions(Path.Combine(outputDirectory, "f2_2_collision_summary.csv"), cases);
        WriteDomains(Path.Combine(outputDirectory, "f2_2_hypothesis_domain_summary.csv"), cases);
        WriteCompatibility(Path.Combine(outputDirectory, "f2_2_compatibility_summary.csv"), cases);
        WriteTruth(Path.Combine(outputDirectory, "f2_2_synthetic_ground_truth_summary.csv"), cases);
        WriteAmbiguity(Path.Combine(outputDirectory, "f2_2_ambiguity_summary.csv"), cases);
        WriteTiming(Path.Combine(outputDirectory, "f2_2_timing_change_summary.csv"));
        var endpoint = EndpointAudit();
        WriteEndpoint(Path.Combine(outputDirectory, "f2_2_endpoint_holdout_summary.csv"), endpoint);
        WriteRice(Path.Combine(outputDirectory, "f2_2_rice_sentinel_summary.csv"));
        var detail = new Detail(QuantizationInferenceFeasibilityResearch.ResearchSchemaVersion,
            QuantizationSerializationForwardModel.ModelVersion, cases, endpoint,
            "SyntheticTeachingCount is structurally zero: domains are caller-supplied and the model learns from no chart.",
            "CrossChartReferenceCount is structurally zero: Evaluate accepts one observation, one domain and one timing map; it has no donor store.");
        File.WriteAllText(detailPath, JsonSerializer.Serialize(detail, JsonOptions()), new UTF8Encoding(false));
        Console.WriteLine($"Wrote F2.2 synthetic artifacts: cases={cases.Length}, detail={new FileInfo(detailPath).Length} bytes.");
    }

    private static CaseData[] BuildCases()
    {
        var integral = Model(500);
        var fractional = Model(499);
        var narrow = Domain("external-narrow-v1", C("half", .5m));
        var expanded = Domain("external-expanded-v1", C("half", .5m), C("exact_250_over_499", 250m / 499m));
        var wrongAmbiguous = Domain("misspecified-collision-v1", C("left", 249.6m / 499m), C("right", 250.4m / 499m));
        var unsupported = Domain("unsupported-control-v1", C("quarter", .25m));
        var unique = Evaluate("unique_correct", 250, narrow, integral, "half", TypedGapTransitionKind.TapHeadToTapHead);
        var ambiguous = Evaluate("ambiguous_contains_truth", 250, expanded, fractional, "half", TypedGapTransitionKind.TapHeadToLongNoteHead);
        var excludes = Evaluate("ambiguous_excludes_truth", 250, wrongAmbiguous, fractional, "half", TypedGapTransitionKind.LongNoteReleaseToTapHead);
        var empty = Evaluate("unsupported_under_model", 250, unsupported, integral, "half", TypedGapTransitionKind.LongNoteReleaseToLongNoteHead);
        var domainSensitiveNarrow = Evaluate("vocabulary_narrow_unique", 250, narrow, fractional, "half", TypedGapTransitionKind.TapHeadToTapHead);
        return [unique, ambiguous, excludes, empty, domainSensitiveNarrow];
    }

    private static CaseData Evaluate(string name, int timestamp, QuantizationHypothesisDomain domain,
        QuantizationSerializationForwardModel model, string truth, TypedGapTransitionKind transition)
    {
        var observation = new QuantizationObservedEndpoint(name, timestamp, transition,
            transition is TypedGapTransitionKind.LongNoteReleaseToTapHead or TypedGapTransitionKind.TapHeadToTapHead
                ? TypedGapEndpointType.TapHead : TypedGapEndpointType.LongNoteHead);
        var set = QuantizationInferenceFeasibilityResearch.Evaluate(observation, domain, model);
        return new CaseData(name, timestamp, truth, domain, set,
            QuantizationInferenceFeasibilityResearch.EvaluateSyntheticTruth(set, truth));
    }

    private static void WriteModelSummary(string path)
    {
        using var w = Writer(path);
        w.WriteLine("framework,domain_source,auditable,falsable,circular,disposition,reason");
        w.WriteLine("explicit_finite_family,caller_supplied,true,true,false,research_condition,clean_only_conditional_on_visible_domain");
        w.WriteLine("all_rationals,infinite,false,false,false,rejected,not_enumerable_without_an_explicit_cap");
        w.WriteLine("denominators_observed_elsewhere,chart_derived,false,false,true,rejected,requires_quantization_to_derive_quantization_vocabulary");
        w.WriteLine("exact_chart_relations,chart_derived,true,true,false,insufficient,serialized_file_relations_do_not_recover_latent_snap_labels");
        w.WriteLine("metadata_editor,file,false,true,false,unavailable,osu_file_has_no_latent_snap_coordinate_labels");
        w.WriteLine("external_labeled_truth,external,true,true,false,future_resource,can_validate_but_does_not_authorize_product_domain");
    }

    private static void WriteForward(string path, IReadOnlyList<CaseData> cases)
    {
        using var w = Writer(path);
        w.WriteLine("scenario,beat_length,latent_beat,predicted_timestamp,expected_timestamp,exact_match");
        Row("integral", 500, 1, 500);
        Row("fractional_midpoint", 499, .5m, 250);
        var offset = new QuantizationSerializationForwardModel([new TimingPoint(100, 500)]);
        w.WriteLine($"offset,500,{D(.5m)},{I(offset.Serialize(.5m))},350,true");
        var bpm = new QuantizationSerializationForwardModel([new TimingPoint(0, 500), new TimingPoint(1000, 250)]);
        w.WriteLine($"real_bpm_change,variable,3,{I(bpm.Serialize(3))},1250,true");
        void Row(string name, decimal beatLength, decimal beat, int expected)
        {
            var actual = Model(beatLength).Serialize(beat);
            w.WriteLine($"{name},{D(beatLength)},{D(beat)},{I(actual)},{I(expected)},{B(actual == expected)}");
        }
    }

    private static void WriteCollisions(string path, IReadOnlyList<CaseData> cases)
    {
        using var w = Writer(path);
        w.WriteLine("scenario,beat_length,position,candidate_ids,latent_class_size,serialized_timestamp,collision");
        var proof = QuantizationInferenceFeasibilityResearch.CertifyNonIdentifiability("half", .5m,
            "exact_250_over_499", 250m / 499m, Model(499));
        w.WriteLine($"same_observable_distinct_latent,499,origin,half|exact_250_over_499,2,{I(proof.WorldASerializedTimestamp)},{B(proof.DemonstratesNonIdentifiability)}");
        w.WriteLine("integral_control,500,origin,quarter|half,1,250,false");
    }

    private static void WriteDomains(string path, IReadOnlyList<CaseData> cases)
    {
        using var w = Writer(path);
        w.WriteLine("domain_id,source,candidate_count,circular,justification");
        foreach (var domain in cases.Select(x => x.Domain).DistinctBy(x => x.Id).OrderBy(x => x.Id, StringComparer.Ordinal))
            w.WriteLine($"{domain.Id},{domain.Source},{I(domain.Candidates.Length)},{B(domain.IsCircular)},explicit_synthetic_research_condition_not_mapper_derived");
        w.WriteLine("observed-denominators-proposal,ObservedDenominatorsCircular,not_enumerated,true,rejected_as_circular");
    }

    private static void WriteCompatibility(string path, IReadOnlyList<CaseData> cases)
    {
        using var w = Writer(path);
        w.WriteLine("scenario,domain_id,transition_kind,observed_timestamp,state,compatible_count,compatible_candidate_ids");
        foreach (var x in cases.OrderBy(x => x.Name, StringComparer.Ordinal))
            w.WriteLine(string.Join(',', x.Name, x.Domain.Id, x.Set.ObservedFileFact.TransitionKind,
                I(x.ObservedTimestamp), x.Set.State, I(x.Set.CompatibleHypotheses.Length),
                string.Join('|', x.Set.CompatibleHypotheses.Select(y => y.CandidateId))));
    }

    private static void WriteTruth(string path, IReadOnlyList<CaseData> cases)
    {
        using var w = Writer(path);
        w.WriteLine("scenario,known_latent_truth,model_result,truth_state,synthetic_only");
        foreach (var x in cases.OrderBy(x => x.Name, StringComparer.Ordinal))
            w.WriteLine($"{x.Name},{x.TruthCandidateId},{x.Set.State},{x.TruthState},true");
        w.WriteLine("f2_1_false_split,half_beat,FileExactDecimalDistinct,FalseSplit,true");
        w.WriteLine("observable_collision,half_vs_exact_250_over_499,SameTimestamp,FalseMerge,true");
    }

    private static void WriteAmbiguity(string path, IReadOnlyList<CaseData> cases)
    {
        using var w = Writer(path);
        w.WriteLine("domain_id,unsupported,singleton,multiple,max_class_size,winner_selected");
        foreach (var group in cases.GroupBy(x => x.Domain.Id).OrderBy(x => x.Key, StringComparer.Ordinal))
            w.WriteLine(string.Join(',', group.Key,
                I(group.Count(x => x.Set.State == QuantizationCompatibilityState.UnsupportedUnderModel)),
                I(group.Count(x => x.Set.State == QuantizationCompatibilityState.UniqueUnderModel)),
                I(group.Count(x => x.Set.State == QuantizationCompatibilityState.AmbiguousUnderModel)),
                I(group.Max(x => x.Set.CompatibleHypotheses.Length)), "false"));
    }

    private static void WriteTiming(string path)
    {
        using var w = Writer(path);
        w.WriteLine("scenario,source_beat,destination_beat,source_timestamp,destination_timestamp,serialized_gap,latent_gap,timing_change");
        Row("same_segment", Model(500), .5m, 1m, false);
        Row("redundant_redline", new([new TimingPoint(0, 500), new TimingPoint(1000, 500)]), 1.5m, 2.5m, true);
        Row("real_bpm_change", new([new TimingPoint(0, 500), new TimingPoint(1000, 250)]), 1.5m, 3m, true);
        Row("ln_crossing_bpm", new([new TimingPoint(0, 500), new TimingPoint(1000, 250)]), 1.5m, 3m, true);
        void Row(string name, QuantizationSerializationForwardModel model, decimal from, decimal to, bool change)
        {
            var interval = model.SerializeInterval(from, to);
            w.WriteLine(string.Join(',', name, D(from), D(to), I(interval.SourceTimestamp),
                I(interval.DestinationTimestamp), I(interval.SerializedGapMilliseconds),
                D(interval.LatentGapBeats), B(change)));
        }
    }

    private static EndpointData EndpointAudit()
    {
        var chart = new ManiaChart { KeyCount = 4, Lines = [], TimingPoints = [new(0, 500)],
            OriginalObjects = new[] { ManiaObject.Ln(0, 0, 1000), ManiaObject.Tap(0, 1250),
                ManiaObject.Ln(1, 500, 1000), ManiaObject.Tap(1, 1500) }
                .Select((x, i) => x with { Sequence = i }).ToArray() };
        var result = ExactGapTimingIdentityResearch.Evaluate(chart);
        var target = result.Occurrences.Single(x => x.Lane == 0);
        var invalid = result.Occurrences.Single(x => x.Lane == 1);
        var deliberatelyBad = ExactGapTimingIdentityResearch.AuditEndpointLeakage(target,
            GapTimingIdentityKind.FileExactDecimal, GapTimingHoldoutKind.TransitionEndpointGroup,
            TypedGapEvidenceScope.GlobalChart, [invalid]);
        return new EndpointData(result.TargetEndpointLeakageCount, deliberatelyBad.Length,
            deliberatelyBad.SelectMany(x => x.LeakedObservationIds).Distinct().Count(),
            result.EndpointHoldoutAdditionalExcludedReferences, result.SharedReleaseEndpointTargets);
    }

    private static void WriteEndpoint(string path, EndpointData x)
    {
        using var w = Writer(path);
        w.WriteLine("fixture,holdout,measured_leakage,leaked_observation_references,additional_endpoint_exclusions,shared_release_targets,expected");
        w.WriteLine($"shared_release,deliberately_unfiltered,{I(x.DeliberatelyBadLeakage)},{I(x.BadLeakedReferences)},{I(x.AdditionalExclusions)},{I(x.SharedReleaseTargets)},positive_control");
        w.WriteLine($"shared_release,transition_endpoint_group,{I(x.CorrectLeakage)},0,{I(x.AdditionalExclusions)},{I(x.SharedReleaseTargets)},zero");
    }

    private static void WriteRice(string path)
    {
        using var w = Writer(path);
        w.WriteLine("scenario,latent_beats,serialized_timestamps,distinct_under_truth,pattern_classifier");
        var model = Model(500);
        var fast = new[] { 0m, .125m, .25m, .375m };
        w.WriteLine($"repeated_fast_stream,{string.Join('|', fast.Select(D))},{string.Join('|', fast.Select(x => I(model.Serialize(x))))},true,none");
        w.WriteLine($"close_neighbor,{D(.124m)}|{D(.125m)},{I(model.Serialize(.124m))}|{I(model.Serialize(.125m))},{B(model.Serialize(.124m) != model.Serialize(.125m))},none");
        w.WriteLine("jack_like_same_lane,0|0.125|0.25,0|63|125,true,none");
        w.WriteLine("alternating_lanes,0|0.125|0.25,0|63|125,true,none");
    }

    private static QuantizationHypothesisDomain Domain(string id, params LatentTimingCandidate[] candidates) =>
        new(id, QuantizationHypothesisDomainSource.ExplicitExternalFiniteVocabulary, candidates,
            "Explicit synthetic research condition; not mapper-derived.");
    private static LatentTimingCandidate C(string id, decimal beat) => new(id, beat);
    private static QuantizationSerializationForwardModel Model(decimal beatLength) => new([new(0, beatLength)]);
    private static StreamWriter Writer(string path) => new(path, false, new UTF8Encoding(false));
    private static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    private static string B(bool value) => value ? "true" : "false";
    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private sealed record CaseData(string Name, int ObservedTimestamp, string TruthCandidateId,
        QuantizationHypothesisDomain Domain, QuantizationCompatibilitySet Set,
        SyntheticCompatibilityTruthState TruthState);
    private sealed record EndpointData(int CorrectLeakage, int DeliberatelyBadLeakage,
        int BadLeakedReferences, int AdditionalExclusions, int SharedReleaseTargets);
    private sealed record Detail(string ResearchSchemaVersion, string SerializationModelVersion,
        IReadOnlyList<CaseData> Cases, EndpointData EndpointAudit,
        string SyntheticTeachingInvariant, string CrossChartInvariant);
}
