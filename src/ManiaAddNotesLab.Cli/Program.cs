using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using ManiaAddNotesLab.Core;

return Run(args);

static int Run(string[] args)
{
    try
    {
        if (args.Length == 0 || args.Contains("--help"))
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        var input = Path.GetFullPath(args[0]);
        if (!File.Exists(input)) throw new FileNotFoundException("Input beatmap was not found.", input);
        var parsed = ParseOptions(args[1..]);
        var seed = parsed.Seed ?? RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        var chart = OsuBeatmap.Parse(File.ReadAllText(input));
        var result = new AddNotesEngine().Apply(chart, new AddNotesOptions
        {
            Chance = parsed.Chance,
            StartMs = parsed.StartMs,
            EndMs = parsed.EndMs,
            LnWindowBeats = parsed.LnWindowBeats,
            SourceAffinityMultiplier = parsed.SourceAffinity,
            VerticalDensityMode = parsed.VerticalDensityMode,
            VerticalDensityScaleMode = parsed.VerticalDensityScaleMode,
            ContextualDensityNormalizationEnabled = parsed.ContextualDensity,
            ContextualDensityScaleMode = parsed.ContextualDensityScaleMode,
            BurstDensityRatioThreshold = parsed.BurstRatioThreshold,
            UseLocalLaneGap = parsed.UseLocalLaneGap,
            FallbackMinimumLaneGapBeats = parsed.FallbackLaneGapBeats,
            MapRelativeSnapEnabled = parsed.MapRelativeSnap,
            InteriorLnOpportunitiesEnabled = parsed.InteriorLnOpportunities,
            MaxInteriorOpportunitiesPerSource = parsed.MaxInteriorOpportunities,
            InteriorMinimumSourceBeats = parsed.MinimumInteriorSourceBeats,
            InteriorMinimumSupportedAnchors = parsed.MinimumInteriorAnchors,
            InteriorEligibilityMode = parsed.InteriorEligibilityMode,
            InteriorContextMode = parsed.InteriorContextMode,
            ArticulationEnabled = parsed.Articulation,
            ArticulationMaxNonHeldColumns = parsed.ArticulationMaxNonHeldColumns,
            RetriggerGapWindowBeats = parsed.RetriggerWindowBeats,
            RetriggerGapMinimumSupport = parsed.RetriggerMinimumSupport,
            Trace = parsed.Trace,
            DiagnosticsEnabled = parsed.DiagnosticsOutput is not null
        }, new SeededRandom(seed));

        var requested = parsed.Output is null
            ? Path.Combine(Path.GetDirectoryName(input)!, $"{Path.GetFileNameWithoutExtension(input)} [ADD {(parsed.Chance * 100).ToString("0.##", CultureInfo.InvariantCulture)}].osu")
            : Path.GetFullPath(parsed.Output);
        var output = UniquePath(requested);
        var writeStarted = Stopwatch.GetTimestamp();
        File.WriteAllText(output, OsuBeatmap.Write(result.ModifiedChart, parsed.Chance), new UTF8Encoding(false));
        result.Statistics.WriterMs = Stopwatch.GetElapsedTime(writeStarted).TotalMilliseconds;

        if (parsed.ProfileOutput is not null)
        {
            var profileOutput = Path.GetFullPath(parsed.ProfileOutput);
            Directory.CreateDirectory(Path.GetDirectoryName(profileOutput)!);
            File.WriteAllText(profileOutput, MapperEvidenceProfileJson.Serialize(result.EvidenceProfile),
                new UTF8Encoding(false));
            Console.WriteLine($"Evidence profile: {profileOutput}");
        }
        if (parsed.DiagnosticsOutput is not null)
        {
            var diagnosticsOutput = Path.GetFullPath(parsed.DiagnosticsOutput);
            Directory.CreateDirectory(Path.GetDirectoryName(diagnosticsOutput)!);
            var diagnostic = result.DecisionDiagnostics
                ?? throw new InvalidOperationException("Decision diagnostics were not built.");
            var json = DecisionDiagnosticsJson.SerializeMeasured(diagnostic, parsed.DiagnosticsDetail);
            result.Statistics.DetailedExportBytes = Encoding.UTF8.GetByteCount(json);
            File.WriteAllText(diagnosticsOutput, json, new UTF8Encoding(false));
            Console.WriteLine($"Decision diagnostics: {diagnosticsOutput}");
        }

        Console.WriteLine($"Seed: {seed}");
        Console.WriteLine($"Output: {output}");
        PrintStatistics(result.Statistics);
        if (result.Trace is not null) Console.WriteLine($"\nTRACE\n{result.Trace}");
        if (parsed.Report is not null) WriteReport(Path.GetFullPath(parsed.Report), input, output, seed, parsed, result.Statistics);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}

static CliOptions ParseOptions(string[] args)
{
    var result = new CliOptions();
    for (var i = 0; i < args.Length; i++)
    {
        string Value() => ++i < args.Length ? args[i] : throw new ArgumentException($"Missing value for {args[i - 1]}.");
        switch (args[i])
        {
            case "--chance": result.Chance = double.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--seed": result.Seed = int.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--start-ms": result.StartMs = int.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--end-ms": result.EndMs = int.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--ln-window-beats": result.LnWindowBeats = double.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--source-affinity": result.SourceAffinity = double.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--vertical-density": result.VerticalDensityMode = Value().ToLowerInvariant() switch
                { "legacy" => VerticalDensityMode.OccupiedColumnsLegacy, "heads" => VerticalDensityMode.SimultaneousHeads,
                    var v => throw new ArgumentException($"Unknown vertical density mode: {v}") }; break;
            case "--vertical-density-scale": result.VerticalDensityScaleMode = Value().ToLowerInvariant() switch
                { "absolute" or "legacy" => VerticalDensityScaleMode.AbsoluteLegacy,
                    "relative" => VerticalDensityScaleMode.KeymodeRelative,
                    var v => throw new ArgumentException($"Unknown vertical density scale: {v}") }; break;
            case "--context-density": result.ContextualDensity = OnOff(Value()); break;
            case "--context-density-scale": result.ContextualDensityScaleMode = Value().ToLowerInvariant() switch
                { "absolute" or "legacy" => ContextualDensityScaleMode.AbsoluteHeadCountLegacy,
                    "relative" => ContextualDensityScaleMode.KeymodeRelative,
                    var v => throw new ArgumentException($"Unknown context density scale: {v}") }; break;
            case "--burst-ratio": result.BurstRatioThreshold = double.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--local-gap": result.UseLocalLaneGap = OnOff(Value()); break;
            case "--fallback-gap-beats": result.FallbackLaneGapBeats = double.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--map-relative-snap": result.MapRelativeSnap = OnOff(Value()); break;
            case "--interior-ln": result.InteriorLnOpportunities = OnOff(Value()); break;
            case "--max-interior": result.MaxInteriorOpportunities = int.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--min-interior-source-beats": result.MinimumInteriorSourceBeats = double.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--min-interior-anchors": result.MinimumInteriorAnchors = int.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--interior-eligibility": result.InteriorEligibilityMode = Value().ToLowerInvariant() switch
                { "legacy" => InteriorEligibilityMode.Legacy, "anchors" => InteriorEligibilityMode.AnchorSupported,
                    var v => throw new ArgumentException($"Unknown interior eligibility mode: {v}") }; break;
            case "--interior-context": result.InteriorContextMode = Value().ToLowerInvariant() switch
                { "legacy" => InteriorContextMode.LegacyVirtualSource, "original" => InteriorContextMode.OriginalOnly,
                    var v => throw new ArgumentException($"Unknown interior context mode: {v}") }; break;
            case "--articulation": result.Articulation = OnOff(Value()); break;
            case "--articulation-max-nonheld": result.ArticulationMaxNonHeldColumns = int.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--retrigger-window-beats": result.RetriggerWindowBeats = double.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--retrigger-support": result.RetriggerMinimumSupport = int.Parse(Value(), CultureInfo.InvariantCulture); break;
            case "--output": result.Output = Value(); break;
            case "--report": result.Report = Value(); break;
            case "--profile-output": result.ProfileOutput = Value(); break;
            case "--diagnostics-output": result.DiagnosticsOutput = Value(); break;
            case "--diagnostics-detail": result.DiagnosticsDetail = Value().ToLowerInvariant() switch
                { "summary" => DiagnosticDetailMode.Summary, "relevant" => DiagnosticDetailMode.Relevant,
                    "all" => DiagnosticDetailMode.All, var v => throw new ArgumentException($"Unknown diagnostics detail: {v}") }; break;
            case "--trace": result.Trace = true; break;
            default: throw new ArgumentException($"Unknown option: {args[i]}");
        }
    }
    if (result.Chance is < 0 or > 1) throw new ArgumentOutOfRangeException("--chance", "Chance must be from 0 to 1.");
    if (result.LnWindowBeats <= 0) throw new ArgumentOutOfRangeException("--ln-window-beats", "Window must be positive.");
    if (result.BurstRatioThreshold <= 1) throw new ArgumentOutOfRangeException("--burst-ratio", "Ratio must be greater than 1.");
    if (result.FallbackLaneGapBeats is < 0 or > 1) throw new ArgumentOutOfRangeException("--fallback-gap-beats");
    if (result.MaxInteriorOpportunities is < 0 or > 2) throw new ArgumentOutOfRangeException("--max-interior");
    if (result.MinimumInteriorAnchors < 1) throw new ArgumentOutOfRangeException("--min-interior-anchors");
    if (result.MinimumInteriorSourceBeats <= 0) throw new ArgumentOutOfRangeException("--min-interior-source-beats");
    if (result.ArticulationMaxNonHeldColumns < 0) throw new ArgumentOutOfRangeException("--articulation-max-nonheld");
    if (result.RetriggerWindowBeats <= 0) throw new ArgumentOutOfRangeException("--retrigger-window-beats");
    if (result.RetriggerMinimumSupport < 1) throw new ArgumentOutOfRangeException("--retrigger-support");
    return result;

    static bool OnOff(string value) => value.ToLowerInvariant() switch
    {
        "on" or "true" or "1" => true,
        "off" or "false" or "0" => false,
        _ => throw new ArgumentException($"Expected on/off, got '{value}'.")
    };
}

static string UniquePath(string requested)
{
    var directory = Path.GetDirectoryName(requested)!;
    Directory.CreateDirectory(directory);
    if (!File.Exists(requested)) return requested;
    var stem = Path.GetFileNameWithoutExtension(requested);
    var extension = Path.GetExtension(requested);
    for (var n = 2; ; n++)
    {
        var candidate = Path.Combine(directory, $"{stem} {n}{extension}");
        if (!File.Exists(candidate)) return candidate;
    }
}

static void PrintStatistics(AddNotesStatistics s)
{
    Console.WriteLine($"Input objects: {s.InputObjects}");
    Console.WriteLine($"Output objects: {s.OutputObjects}");
    Console.WriteLine($"Original taps / LN: {s.OriginalTaps} / {s.OriginalLongNotes}");
    Console.WriteLine($"Added taps / LN: {s.AddedTaps} / {s.AddedLongNotes}");
    Console.WriteLine($"Total opportunities: {s.TotalOpportunities}");
    Console.WriteLine($"Successful probability rolls: {s.SuccessfulProbabilityRolls}");
    Console.WriteLine($"Placed objects: {s.PlacedObjects}");
    Console.WriteLine($"Failed placements: {s.FailedPlacements}");
    Console.WriteLine($"LN candidate retries: {s.LnCandidateRetries}");
    Console.WriteLine($"LN skips due to geometry: {s.LnSkipsDueToGeometry}");
    Console.WriteLine($"Density-adjusted opportunities: {s.DensityAdjustedOpportunities}");
    Console.WriteLine($"Context-density adjusted: {s.ContextualDensityAdjustedOpportunities}");
    Console.WriteLine($"Opportunities (base / LN interior): {s.BaseHeadOpportunities} / {s.InteriorLnOpportunities}");
    Console.WriteLine($"Successful rolls (base / LN interior): {s.SuccessfulBaseRolls} / {s.SuccessfulInteriorRolls}");
    Console.WriteLine($"Added LN (heads / LN interior): {s.AddedLongNotesFromHeadOpportunities} / {s.AddedLongNotesFromInteriorOpportunities}");
    Console.WriteLine($"Base LN (opportunities / rolls / placed): {s.BaseLnOpportunityCount} / {s.BaseLnSuccessfulRolls} / {s.BaseLnPlaced}");
    Console.WriteLine($"Interior rejects (short / context / anchors / legacy length): {s.InteriorRejectedTooShort} / {s.InteriorRejectedInsufficientLnContext} / {s.InteriorRejectedNoSupportedAnchors} / {s.InteriorRejectedRelativeLengthLegacy}");
    Console.WriteLine($"Interior candidates (short / medium / long / impossible): {s.InteriorCandidatesShort} / {s.InteriorCandidatesMedium} / {s.InteriorCandidatesLong} / {s.InteriorCandidatesImpossible}");
    Console.WriteLine($"Vertical means (simultaneous heads / held LN): {s.SimultaneousHeadColumnsMean:F3} / {s.HeldLnColumnsMean:F3}");
    Console.WriteLine($"Normalized means (head / held / non-held / legal-lane): {s.SimultaneousHeadRatioMean:P2} / {s.HeldLnRatioMean:P2} / {s.NonHeldRatioMean:P2} / {s.LegalLaneRatioMean:P2}");
    Console.WriteLine($"Rice (opportunities / rolls / placed): {s.TapOpportunities} / {s.TapSuccessfulRolls} / {s.TapPlaced}");
    Console.WriteLine($"Rice placed (burst / sustained / normal): {s.TapPlacedBurst} / {s.TapPlacedSustained} / {s.TapPlacedNormal}");
    Console.WriteLine($"Rice chance (effective / chord / contextual): {s.TapEffectiveChanceMean:P2} / {s.MeanChordFactorForTap:P2} / {s.MeanContextualFactorForTap:P2}");
    Console.WriteLine($"Articulation (blocked / eligible / placed): {s.InteriorBlockedByLnSaturation} / {s.ArticulationEligible} / {s.ArticulationPlaced}");
    Console.WriteLine($"Articulation rejects (anchor / saturation / retrigger / left / right / cap / geometry): {s.RejectedAnchorNotHead} / {s.RejectedNotSaturated} / {s.RejectedNoRetriggerGap} / {s.RejectedLeftTooShort} / {s.RejectedRightTooShort} / {s.RejectedParentCap} / {s.RejectedGeometry}");
    Console.WriteLine($"Added interactions (heads / releases / total): {s.AddedHeads} / {s.AddedReleases} / {s.AddedInteractions}");
    Console.WriteLine($"Interior mean effective chance: {s.InteriorMeanEffectiveChance:P2}");
    Console.WriteLine($"Mean effective chance: {s.MeanEffectiveChance:P2}");
    Console.WriteLine($"LN ratios (original / added / result): {s.OriginalLnRatio:P2} / {s.AddedLnRatio:P2} / {s.ResultLnRatio:P2}");
    Console.WriteLine($"Performance ms (context / candidates / geometry / writer): {s.ContextBuildMs:F3} / {s.CandidateBuildMs:F3} / {s.GeometryMs:F3} / {s.WriterMs:F3}");
    Console.WriteLine($"Structural counters (checks / examined / beat conversions): {s.GeometryChecks} / {s.GeometryObjectsExamined} / {s.BeatConversions}");
    Console.WriteLine($"Evidence profile (observations / relations / estimated bytes / build ms): {s.ProfileObservationCount} / {s.ProfileRelationCount} / {s.ProfileEstimatedSizeBytes} / {s.ProfileBuildMs:F3}");
    if (s.DiagnosticCandidateCount > 0)
        Console.WriteLine($"Shadow diagnostics (candidates / lane checks / witnesses / certificate ms / failures ms / export bytes): {s.DiagnosticCandidateCount} / {s.CandidateLaneDiagnosticCount} / {s.WitnessReferenceCount} / {s.CertificateBuildMs:F3} / {s.FailureDiagnosticMs:F3} / {s.DetailedExportBytes}");
}

static void WriteReport(string path, string input, string output, int seed, CliOptions options, AddNotesStatistics s)
{
    var exists = File.Exists(path);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var writer = new StreamWriter(path, append: true, new UTF8Encoding(false));
    if (!exists) writer.WriteLine("input,output,seed,chance,start_ms,end_ms,ln_window_beats,vertical_density,interior_eligibility,interior_context,contextual_density,burst_ratio,local_gap,fallback_gap_beats,map_relative_snap,interior_ln,max_interior,min_interior_source_beats,input_objects,output_objects,original_taps,original_ln,added_taps,added_ln,base_opportunities,interior_opportunities,base_ln_opportunities,successful_base_rolls,successful_interior_rolls,base_ln_successful_rolls,added_ln_from_heads,added_ln_from_interior,base_ln_placed,opportunities,successful_rolls,placed,failed_placements,ln_candidate_retries,ln_geometry_skips,density_adjusted_opportunities,contextual_density_adjusted,mean_effective_chance,interior_mean_effective_chance,simultaneous_head_columns_mean,held_ln_columns_mean,interior_rejected_too_short,interior_rejected_context,interior_rejected_anchors,interior_rejected_legacy_length,interior_candidates_short,interior_candidates_medium,interior_candidates_long,interior_candidates_impossible,ln_shape_rejected_gap,ln_shape_rejected_overlap,geometry_checks,geometry_objects_examined,beat_conversions,ln_candidates_built,ln_shape_no_legal_lane,context_build_ms,candidate_build_ms,geometry_ms,writer_ms,original_ln_ratio,added_ln_ratio,result_ln_ratio,vertical_density_scale,context_density_scale,articulation,articulation_max_nonheld,retrigger_window_beats,retrigger_support,head_ratio_mean,held_ratio_mean,nonheld_columns_mean,nonheld_ratio_mean,legal_lane_ratio_mean,tap_opportunities,tap_successful_rolls,tap_placed,tap_placed_burst,tap_placed_sustained,tap_placed_normal,tap_effective_chance_mean,tap_chord_factor_mean,tap_contextual_factor_mean,interior_no_free_lane,interior_blocked_saturation,articulation_eligible,articulation_placed,rejected_anchor_not_head,rejected_not_saturated,rejected_no_retrigger_gap,rejected_left_too_short,rejected_right_too_short,rejected_parent_cap,rejected_geometry,rejected_already_modified,added_heads,added_releases,added_interactions,pass1_ms,articulation_pass_ms,behavior_policy_version,evidence_profile_version,profile_observations,profile_relations,profile_estimated_bytes,decision_diagnostic_version,diagnostic_candidates,candidate_lane_diagnostics,witness_references,certificate_build_ms,failure_diagnostic_ms,detailed_export_bytes");
    static string Q(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    writer.WriteLine(string.Join(',', Q(input), Q(output), seed, F(options.Chance), options.StartMs, options.EndMs,
        F(options.LnWindowBeats), options.VerticalDensityMode, options.InteriorEligibilityMode, options.InteriorContextMode,
        options.ContextualDensity, F(options.BurstRatioThreshold), options.UseLocalLaneGap,
        F(options.FallbackLaneGapBeats), options.MapRelativeSnap, options.InteriorLnOpportunities, options.MaxInteriorOpportunities,
        F(options.MinimumInteriorSourceBeats),
        s.InputObjects, s.OutputObjects, s.OriginalTaps, s.OriginalLongNotes, s.AddedTaps, s.AddedLongNotes,
        s.BaseHeadOpportunities, s.InteriorLnOpportunities, s.BaseLnOpportunityCount, s.SuccessfulBaseRolls, s.SuccessfulInteriorRolls, s.BaseLnSuccessfulRolls,
        s.AddedLongNotesFromHeadOpportunities, s.AddedLongNotesFromInteriorOpportunities, s.BaseLnPlaced,
        s.TotalOpportunities,
        s.SuccessfulProbabilityRolls, s.PlacedObjects, s.FailedPlacements, s.LnCandidateRetries, s.LnSkipsDueToGeometry,
        s.DensityAdjustedOpportunities, s.ContextualDensityAdjustedOpportunities, F(s.MeanEffectiveChance), F(s.InteriorMeanEffectiveChance),
        F(s.SimultaneousHeadColumnsMean), F(s.HeldLnColumnsMean), s.InteriorRejectedTooShort,
        s.InteriorRejectedInsufficientLnContext, s.InteriorRejectedNoSupportedAnchors, s.InteriorRejectedRelativeLengthLegacy,
        s.InteriorCandidatesShort, s.InteriorCandidatesMedium, s.InteriorCandidatesLong, s.InteriorCandidatesImpossible,
        s.LnShapeRejectedGap, s.LnShapeRejectedOverlap,
        s.GeometryChecks, s.GeometryObjectsExamined, s.BeatConversions, s.LnCandidatesBuilt, s.LnCandidatesImpossible,
        F(s.ContextBuildMs), F(s.CandidateBuildMs), F(s.GeometryMs), F(s.WriterMs),
        F(s.OriginalLnRatio), F(s.AddedLnRatio), F(s.ResultLnRatio), options.VerticalDensityScaleMode,
        options.ContextualDensityScaleMode, options.Articulation, options.ArticulationMaxNonHeldColumns,
        F(options.RetriggerWindowBeats), options.RetriggerMinimumSupport, F(s.SimultaneousHeadRatioMean),
        F(s.HeldLnRatioMean), F(s.NonHeldColumnsMean), F(s.NonHeldRatioMean), F(s.LegalLaneRatioMean),
        s.TapOpportunities, s.TapSuccessfulRolls, s.TapPlaced, s.TapPlacedBurst, s.TapPlacedSustained,
        s.TapPlacedNormal, F(s.TapEffectiveChanceMean), F(s.MeanChordFactorForTap),
        F(s.MeanContextualFactorForTap), s.InteriorNoFreeLane, s.InteriorBlockedByLnSaturation,
        s.ArticulationEligible, s.ArticulationPlaced, s.RejectedAnchorNotHead, s.RejectedNotSaturated,
        s.RejectedNoRetriggerGap, s.RejectedLeftTooShort, s.RejectedRightTooShort, s.RejectedParentCap,
        s.RejectedGeometry, s.RejectedAlreadyModified, s.AddedHeads, s.AddedReleases, s.AddedInteractions,
        F(s.Pass1Ms), F(s.ArticulationPassMs), MapperEvidenceProfileBuilder.BehaviorPolicyVersion,
        MapperEvidenceProfileBuilder.EvidenceProfileVersion, s.ProfileObservationCount,
        s.ProfileRelationCount, s.ProfileEstimatedSizeBytes, DecisionDiagnosticVersions.Current,
        s.DiagnosticCandidateCount, s.CandidateLaneDiagnosticCount, s.WitnessReferenceCount,
        F(s.CertificateBuildMs), F(s.FailureDiagnosticMs), s.DetailedExportBytes));
    Console.WriteLine($"Report: {path}");
    static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}

static void PrintUsage() => Console.WriteLine("""
ManiaAddNotesLab <input.osu> [options]
  --chance <0..1>          Probability per original object (default 0.30)
  --seed <int>             Reproducible seed; generated and printed when omitted
  --start-ms <int>         First eligible head timestamp (inclusive)
  --end-ms <int>           Last eligible head timestamp (inclusive)
  --ln-window-beats <n>    Local LN context radius (default 4)
  --source-affinity <n>    Legacy base-source affinity (default 1.25)
  --vertical-density legacy|heads (default heads)
  --vertical-density-scale absolute|relative (default relative)
  --context-density on|off Contextual burst normalization (default on)
  --context-density-scale absolute|relative (default relative)
  --burst-ratio <n>        Micro/context density threshold (default 1.5)
  --local-gap on|off       Learn supported lane gap from originals (default on)
  --fallback-gap-beats <n> Fallback lane gap (default 0.125)
  --map-relative-snap on|off Preserve map timing vocabulary (default on)
  --interior-ln on|off     LN-interior opportunities (default off)
  --max-interior <0..2>    Interior opportunities per eligible LN (default 2)
  --min-interior-source-beats <n> Minimum parent LN duration (default 3)
  --min-interior-anchors <n> Original anchor support required (default 2)
  --interior-eligibility legacy|anchors (default anchors)
  --interior-context legacy|original (default original-only)
  --articulation on|off    Split saturated parent LNs using original retrigger evidence (default off)
  --articulation-max-nonheld <n> Saturation threshold (default 1)
  --retrigger-window-beats <n> Local retrigger evidence radius (default 4)
  --retrigger-support <n>  Required repeated original gaps (default 2)
  --output <path>          Desired output path; never overwrites
  --trace                  Print per-opportunity diagnostic trace
  --report <csv>           Append one metrics row to a CSV file
  --profile-output <json>  Export immutable mapper evidence diagnostics (shadow only)
  --diagnostics-output <json> Export decision diagnostics, including C1 shadow weights
  --diagnostics-detail summary|relevant|all (default relevant; deterministic explicit filter)
""");

file sealed class CliOptions
{
    public double Chance { get; set; } = 0.30;
    public int? Seed { get; set; }
    public int? StartMs { get; set; }
    public int? EndMs { get; set; }
    public double LnWindowBeats { get; set; } = 4;
    public double SourceAffinity { get; set; } = 1.25;
    public VerticalDensityMode VerticalDensityMode { get; set; } = VerticalDensityMode.SimultaneousHeads;
    public VerticalDensityScaleMode VerticalDensityScaleMode { get; set; } = VerticalDensityScaleMode.KeymodeRelative;
    public bool ContextualDensity { get; set; } = true;
    public ContextualDensityScaleMode ContextualDensityScaleMode { get; set; } = ContextualDensityScaleMode.KeymodeRelative;
    public double BurstRatioThreshold { get; set; } = 1.5;
    public bool UseLocalLaneGap { get; set; } = true;
    public double FallbackLaneGapBeats { get; set; } = .125;
    public bool MapRelativeSnap { get; set; } = true;
    public bool InteriorLnOpportunities { get; set; }
    public int MaxInteriorOpportunities { get; set; } = 2;
    public double MinimumInteriorSourceBeats { get; set; } = 3;
    public int MinimumInteriorAnchors { get; set; } = 2;
    public InteriorEligibilityMode InteriorEligibilityMode { get; set; } = InteriorEligibilityMode.AnchorSupported;
    public InteriorContextMode InteriorContextMode { get; set; } = InteriorContextMode.OriginalOnly;
    public bool Articulation { get; set; }
    public int ArticulationMaxNonHeldColumns { get; set; } = 1;
    public double RetriggerWindowBeats { get; set; } = 4;
    public int RetriggerMinimumSupport { get; set; } = 2;
    public string? Output { get; set; }
    public string? Report { get; set; }
    public string? ProfileOutput { get; set; }
    public string? DiagnosticsOutput { get; set; }
    public DiagnosticDetailMode DiagnosticsDetail { get; set; } = DiagnosticDetailMode.Relevant;
    public bool Trace { get; set; }
}
