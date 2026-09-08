using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

internal static class F23SyntheticRunner
{
    public static void Run(string outputDirectory, string detailPath)
    {
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(detailPath)!);
        var validations = BuildValidations();
        WriteDomainCatalog(Path.Combine(outputDirectory, "f2_3_domain_catalog.csv"));
        WriteDomainHashes(Path.Combine(outputDirectory, "f2_3_domain_hash_summary.csv"));
        WriteTruthSources(Path.Combine(outputDirectory, "f2_3_ground_truth_source_summary.csv"));
        WriteTruthSchema(Path.Combine(outputDirectory, "f2_3_ground_truth_schema_summary.csv"));
        WriteCircularity(Path.Combine(outputDirectory, "f2_3_circularity_summary.csv"));
        WriteValidation(Path.Combine(outputDirectory, "f2_3_domain_validation_summary.csv"), validations);
        WriteAcquisition(Path.Combine(outputDirectory, "f2_3_acquisition_feasibility_summary.csv"));
        WriteTransitions(Path.Combine(outputDirectory, "f2_3_transition_coverage_summary.csv"));
        WriteRice(Path.Combine(outputDirectory, "f2_3_rice_sentinel_summary.csv"));
        WriteDecision(Path.Combine(outputDirectory, "f2_3_branch_decision_summary.csv"));
        var detail = new Detail(QuantizationDomainGroundTruthResearch.ResearchSchemaVersion,
            QuantizationInferenceFeasibilityResearch.ResearchSchemaVersion,
            QuantizationHypothesisDomain.CanonicalizationVersion, validations,
            "CONTINUE CONDITIONALLY",
            "Obtain an authorized pre-serialization mapper-authored transition package with source and destination coordinates, timing map and exported osu, split from domain design.");
        File.WriteAllText(detailPath, JsonSerializer.Serialize(detail, JsonOptions()), new UTF8Encoding(false));
        Console.WriteLine($"Wrote F2.3 research artifacts: validations={validations.Length}, detail={new FileInfo(detailPath).Length} bytes.");
    }

    private static TransitionDomainValidationResult[] BuildValidations()
    {
        var integralTruth = Truth("unique-correct", .5m, 1m, 500);
        var fractionalTruth = Truth("fractional", .5m, 1m, 499);
        return
        [
            Validate(Domain("unique-correct", .5m, 1m), integralTruth),
            Validate(Domain("unique-incorrect", 250m / 499m, 498.6m / 499m),
                fractionalTruth with { TruthId = "unique-incorrect" }),
            Validate(Domain("ambiguous-contains", .5m, 250m / 499m, 1m, 498.6m / 499m),
                fractionalTruth with { TruthId = "ambiguous-contains" }),
            Validate(Domain("ambiguous-excludes", 249.6m / 499m, 250.4m / 499m,
                    498.6m / 499m, 499.4m / 499m),
                fractionalTruth with { TruthId = "ambiguous-excludes" }),
            Validate(Domain("unsupported", .25m), integralTruth with { TruthId = "unsupported" })
        ];
    }

    private static void WriteDomainCatalog(string path)
    {
        using var w = Writer(path);
        w.WriteLine("proposal,source_kind,availability_now,circularity,ground_truth_relationship,mapper_derived_justified,intended_use,disposition");
        w.WriteLine("fixed_external_rhythm_vocabulary,ExternalConvention,available_as_declared_assumption,NonCircular,None,false,MethodDemonstration,research_condition_only");
        w.WriteLine("editor_snap_metadata,EditorMetadata,not_present_in_osu,Unknown,IndependentValidationPlanned,false,ValidationCandidate,unavailable_now");
        w.WriteLine("pre_serialization_editor_source,EditorMetadata,requires_external_source_pair,NonCircular,IndependentValidationPlanned,false,ValidationCandidate,conditional_acquisition_route");
        w.WriteLine("mapper_provided_annotation,MapperAnnotated,requires_author_participation,NonCircular,IndependentValidationPlanned,false,ValidationCandidate,conditional_acquisition_route");
        w.WriteLine("chart_derived_denominators,ChartDerived,computable_only_after_quantization,Circular,SameSourceAsDomain,false,ObservationOnly,rejected");
        w.WriteLine("exact_file_relations,FileDerived,available,NonCircular,None,false,ObservationOnly,observed_fact_not_latent_domain");
        w.WriteLine("programmatic_latent_domain,SyntheticGroundTruth,available,NonCircular,SameSourceAsDomain,false,SyntheticControl,logic_falsification_only");
        w.WriteLine("manual_finite_research_domain,ManualResearchDomain,available,NonCircular,None,false,MethodDemonstration,research_condition_only");
        w.WriteLine("unknown_domain,Unknown,unknown,Unknown,Unknown,false,ObservationOnly,not_promotable");
    }

    private static void WriteDomainHashes(string path)
    {
        using var w = Writer(path);
        w.WriteLine("scenario,left_hash,right_hash,hash_equal,semantic_candidate_count,expected");
        var ordered = DomainLabels("ordered", ("half", .5m), ("quarter", .25m));
        var permuted = DomainLabels("permuted", ("quarter", .25m), ("half", .5m));
        var changed = DomainLabels("ordered", ("half", .5m), ("eighth", .125m));
        var alias = DomainLabels("alias", ("two_quarters", .5m), ("quarter", .25m));
        var duplicates = DomainLabels("duplicates", ("half", .5m), ("two_quarters", .5m));
        Row("permutation", ordered, permuted, true);
        Row("candidate_change_same_id", ordered, changed, false);
        Row("alias_metadata_change", ordered, alias, true);
        Row("semantic_alias_dedup", duplicates, duplicates, true);
        void Row(string scenario, QuantizationHypothesisDomain left, QuantizationHypothesisDomain right,
            bool expected) => w.WriteLine(string.Join(',', scenario, left.ContentHash, right.ContentHash,
                B(left.ContentHash == right.ContentHash), I(left.Candidates.Length), B(expected)));
    }

    private static void WriteTruthSources(string path)
    {
        using var w = Writer(path);
        w.WriteLine("level,source_kind,availability_now,independent_from_osu,latent_strength,can_validate_human_intent,limitation");
        w.WriteLine("NoLabel,UnlabeledHumanOsu,true,false,none,false,observable_only");
        w.WriteLine("RetrospectiveAnnotation,RetrospectiveHumanAnnotation,possible,false,interpretive,false,annotation_after_information_loss");
        w.WriteLine("AuthorConfirmed,MapperAuthorAnnotation,requires_mapper,true,author_statement,true,may_lack_exact_pre_export_coordinates");
        w.WriteLine("PreSerializationLatent,EditorSourceExportPair,requires_external_pair,true,strong,true,source_format_or_capture_not_currently_available");
        w.WriteLine("ControlledGeneration,ProgrammaticSynthetic,true,true,complete_synthetic,false,does_not_establish_human_mapper_vocabulary");
    }

    private static void WriteTruthSchema(string path)
    {
        using var w = Writer(path);
        w.WriteLine("field,epistemic_role,required,release_specific,purpose");
        foreach (var row in new[]
        {
            "source_latent_beat,latent_truth,true,false,source_coordinate_before_serialization",
            "destination_latent_beat,latent_truth,true,false,destination_coordinate_before_serialization",
            "latent_gap_beats,derived_truth,true,false,destination_minus_source",
            "source_serialized_timestamp,observed_pair,true,false,forward_validation",
            "destination_serialized_timestamp,observed_pair,true,false,forward_validation",
            "transition_kind,relation_identity,true,false,preserve_four_typed_transitions",
            "source_endpoint_type,endpoint_semantics,true,false,head_or_release",
            "destination_endpoint_type,endpoint_semantics,true,false,tap_head_or_ln_head",
            "source_ln_head_latent_beat,latent_truth,conditional,true,separate_head_from_release",
            "source_ln_head_serialized_timestamp,observed_pair,conditional,true,separate_head_from_release",
            "timing_map,serialization_input,true,false,multi_redline_path",
            "timing_map_version,assumption_certificate,true,false,detect_map_change",
            "truth_source_kind,provenance,true,false,truth_authority_level",
            "truth_level,provenance,true,false,no_probabilistic_confidence",
            "provenance,provenance,true,false,reproducible_origin"
        }) w.WriteLine(row);
    }

    private static void WriteCircularity(string path)
    {
        using var w = Writer(path);
        w.WriteLine("proposal,status,reason,promotion_allowed");
        w.WriteLine("external_declared_coordinates,NonCircular,candidates_exist_before_evaluation,false");
        w.WriteLine("programmatic_pre_serialization_coordinates,NonCircular,latent_state_created_before_export,false");
        w.WriteLine("mapper_pre_export_annotation,NonCircular,label_recorded_before_integer_ms_export,conditional");
        w.WriteLine("denominators_inferred_from_chart_notes,Circular,requires_solving_quantization_to_construct_domain,false");
        w.WriteLine("exact_file_timestamp_relations,NonCircular,observed_but_not_latent_intent,false");
        w.WriteLine("unknown_editor_metadata,Unknown,not_present_in_current_osu_inputs,false");
    }

    private static void WriteValidation(string path, IReadOnlyList<TransitionDomainValidationResult> rows)
    {
        using var w = Writer(path);
        w.WriteLine("truth_id,domain_id,domain_hash,truth_source,independence,state,source_compatible,destination_compatible,winner_selected");
        foreach (var x in rows.OrderBy(x => x.TruthId, StringComparer.Ordinal))
            w.WriteLine(string.Join(',', x.TruthId, x.DomainId, x.DomainContentHash, x.TruthSourceKind,
                x.Independence, x.State, I(x.SourceCompatibility.CompatibleHypotheses.Length),
                I(x.DestinationCompatibility.CompatibleHypotheses.Length), "false"));
    }

    private static void WriteAcquisition(string path)
    {
        using var w = Writer(path);
        w.WriteLine("route,availability_now,effort,independent_from_osu,truth_strength,transition_coverage,timing_change_coverage,ln_coverage,keymode_coverage,mapper_diversity,annotation_bias,reproducibility,legal_privacy,disposition");
        w.WriteLine("historical_unlabeled_osu,available,low,no,NoLabel,observable_only,observable_only,observable_only,4K|7K|10K,11_families,not_a_label,high,no_new_issue,reject_as_truth");
        w.WriteLine("retrospective_annotation,not_collected,medium,partial,RetrospectiveAnnotation,possible,possible,possible,configurable,requires_recruitment,high,medium,consent_required,insufficient_alone");
        w.WriteLine("mapper_author_confirmation,not_collected,medium,yes,AuthorConfirmed,all_four_required,required,required,planned_multi_key,requires_mapper,recall_risk,medium,consent_required,conditional_support");
        w.WriteLine("pre_export_editor_pair,not_available,medium_high,yes,PreSerializationLatent,all_four_required,required,required,planned_multi_key,requires_mapper,low_if_captured_before_export,high,consent_and_source_license,preferred_conditional_route");
        w.WriteLine("programmatic_synthetic_pilot,available,low,yes,ControlledGeneration,all_four,complete,complete,1K|4K|7K|10K|18K,none,none,high,no_issue,implemented_logic_control");
        w.WriteLine("manual_domain_generated_labels,available,low,no,domain_self_label,configurable,configurable,configurable,configurable,none,total,high,no_issue,rejected_circular_validation");
    }

    private static void WriteTransitions(string path)
    {
        using var w = Writer(path);
        w.WriteLine("fixture,transition_kind,keymode,timing_condition,source_endpoint,destination_endpoint,source_latent,destination_latent,latent_gap,source_ms,destination_ms");
        var fixtures = new[]
        {
            Truth("tap_tap_integral", .5m, 1m, 500, TypedGapTransitionKind.TapHeadToTapHead, 1),
            Truth("tap_ln_non_integral", .5m, 1m, 499, TypedGapTransitionKind.TapHeadToLongNoteHead, 4),
            Truth("release_tap", .5m, 1m, 500, TypedGapTransitionKind.LongNoteReleaseToTapHead, 7, 0m),
            Truth("release_ln", .5m, 1m, 499, TypedGapTransitionKind.LongNoteReleaseToLongNoteHead, 10, 0m),
            Truth("release_ln_18k", .5m, 1m, 500, TypedGapTransitionKind.LongNoteReleaseToLongNoteHead, 18, 0m),
            TruthMap("redundant_redline", 1.5m, 2.5m, TypedGapTransitionKind.TapHeadToTapHead, 4,
                null, [new TimingPoint(0, 500), new TimingPoint(1000, 500)]),
            TruthMap("real_bpm_change", 1.5m, 3m, TypedGapTransitionKind.TapHeadToTapHead, 7,
                null, [new TimingPoint(0, 500), new TimingPoint(1000, 250)]),
            TruthMap("ln_crossing_bpm", 2.5m, 3m, TypedGapTransitionKind.LongNoteReleaseToTapHead, 7,
                1.5m, [new TimingPoint(0, 500), new TimingPoint(1000, 250)])
        };
        foreach (var x in fixtures)
            w.WriteLine(string.Join(',', x.TruthId, x.TransitionKind, $"{x.KeyCount}K",
                x.TimingMap.Length == 1 ? "single_redline" : x.TruthId, x.SourceEndpointType,
                x.DestinationEndpointType, D(x.SourceLatentBeat), D(x.DestinationLatentBeat),
                D(x.LatentGapBeats), I(x.SourceSerializedTimestamp), I(x.DestinationSerializedTimestamp)));
    }

    private static void WriteRice(string path)
    {
        using var w = Writer(path);
        w.WriteLine("scenario,latent_source,latent_destination,latent_gap,source_ms,destination_ms,distinguishable,pattern_classifier");
        foreach (var (name, from, to) in new[]
                 { ("fast_repeated", 0m, .125m), ("jack_like", .125m, .25m),
                     ("alternating", .25m, .375m), ("close_left", 0m, .124m),
                     ("close_right", 0m, .125m) })
        {
            var truth = Truth(name, from, to, 500);
            w.WriteLine(string.Join(',', name, D(from), D(to), D(to - from),
                I(truth.SourceSerializedTimestamp), I(truth.DestinationSerializedTimestamp), "true", "none"));
        }
    }

    private static void WriteDecision(string path)
    {
        using var w = Writer(path);
        w.WriteLine("outcome,f2_branch_decision,domain_status,truth_status,missing_dependency,next_action,behavior_authorized");
        w.WriteLine("B,CONTINUE_CONDITIONALLY,explicit_domains_methodologically_valid_but_not_mapper_authorized,synthetic_available_human_independent_missing,authorized_pre_serialization_mapper_authored_transition_package,acquire_and_freeze_design_vs_validation_sets_before_any_quantizer_phase,false");
    }

    private static TransitionDomainValidationResult Validate(QuantizationHypothesisDomain domain,
        LabeledTransitionTruth truth) => QuantizationDomainGroundTruthResearch.Evaluate(domain, truth,
        DomainTruthIndependenceStatus.DomainConstructedFromTruth);
    private static QuantizationHypothesisDomain Domain(string id, params decimal[] beats) => new(id,
        QuantizationHypothesisDomainSource.ExplicitExternalFiniteVocabulary,
        beats.Select((x, i) => new LatentTimingCandidate($"candidate_{i}", x)),
        "Explicit manual domain for a synthetic research condition.");
    private static QuantizationHypothesisDomain DomainLabels(string id,
        params (string Label, decimal Beat)[] values) => new(id,
        QuantizationHypothesisDomainSource.ExplicitExternalFiniteVocabulary,
        values.Select(x => new LatentTimingCandidate(x.Label, x.Beat)),
        "Explicit manual domain for canonical hash research.");
    private static LabeledTransitionTruth Truth(string id, decimal source, decimal destination,
        decimal beatLength, TypedGapTransitionKind kind = TypedGapTransitionKind.TapHeadToTapHead,
        int keys = 4, decimal? lnHead = null) => TruthMap(id, source, destination, kind, keys, lnHead,
        [new TimingPoint(0, beatLength)]);
    private static LabeledTransitionTruth TruthMap(string id, decimal source, decimal destination,
        TypedGapTransitionKind kind, int keys, decimal? lnHead, TimingPoint[] map) =>
        QuantizationDomainGroundTruthResearch.CreateControlledTruth(id, keys, kind, source, destination,
            map, QuantizationTruthSourceKind.ProgrammaticSynthetic,
            QuantizationGroundTruthLevel.ControlledGeneration,
            "Programmatic pre-serialization fixture; not a human label.", lnHead);
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
    private sealed record Detail(string ResearchSchemaVersion, string HardenedF22SchemaVersion,
        string DomainCanonicalizationVersion, IReadOnlyList<TransitionDomainValidationResult> Validations,
        string BranchDecision, string MissingDependency);
}
