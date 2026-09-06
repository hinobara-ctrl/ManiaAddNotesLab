using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using ManiaAddNotesLab.Core;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = ResolveWebRoot()
});
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
builder.WebHost.UseUrls("http://127.0.0.1:5178");
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

var jobs = new ConcurrentDictionary<Guid, BatchJob>();
var resultsRoot = Path.Combine(app.Environment.ContentRootPath, "batch-results");
Directory.CreateDirectory(resultsRoot);

app.MapPost("/api/jobs", async (HttpRequest request) =>
{
    if (!request.HasFormContentType) return Results.BadRequest(new { error = "Selecciona un archivo .osu." });
    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("chart");
    if (file is null || file.Length == 0) return Results.BadRequest(new { error = "El archivo .osu está vacío o no fue enviado." });
    if (!Path.GetExtension(file.FileName).Equals(".osu", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Solo se aceptan archivos .osu." });

    BatchRequest options;
    try { options = BatchRequest.From(form); }
    catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }

    var id = Guid.NewGuid();
    var safeStem = SafeStem(Path.GetFileNameWithoutExtension(file.FileName));
    var directory = Path.Combine(resultsRoot, $"{DateTime.Now:yyyyMMdd-HHmmss}-{id:N}");
    Directory.CreateDirectory(directory);
    var inputPath = Path.Combine(directory, safeStem + ".osu");
    await using (var target = File.Create(inputPath)) await file.CopyToAsync(target);

    var job = new BatchJob(id, Path.GetFileName(file.FileName), directory, options);
    jobs[id] = job;
    _ = Task.Run(() => RunBatch(job, inputPath, safeStem));
    return Results.Accepted($"/api/jobs/{id}", new { id });
}).DisableAntiforgery();

app.MapGet("/api/jobs/{id:guid}", (Guid id) =>
    jobs.TryGetValue(id, out var job) ? Results.Ok(job.Snapshot()) : Results.NotFound());

app.MapPost("/api/jobs/{id:guid}/cancel", (Guid id) =>
{
    if (!jobs.TryGetValue(id, out var job)) return Results.NotFound();
    job.Cancellation.Cancel();
    return Results.Accepted();
});

app.MapGet("/api/jobs/{id:guid}/download", (Guid id) =>
{
    if (!jobs.TryGetValue(id, out var job) || job.Status is not ("completed" or "cancelled")) return Results.NotFound();
    var zipPath = Path.Combine(job.Directory, "ManiaAddNotesLab-results.zip");
    if (!File.Exists(zipPath))
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var path in Directory.EnumerateFiles(job.Directory).Where(p => !p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            archive.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Fastest);
    }
    return Results.File(zipPath, "application/zip", $"ManiaAddNotesLab-{id:N}.zip");
});

try
{
    app.Run();
}
catch (IOException ex) when (ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("ManiaAddNotesLab ya está abierto en http://127.0.0.1:5178/. Usa esa pestaña; no hace falta iniciar otra instancia.");
}

static string ResolveWebRoot()
{
    var current = Directory.GetCurrentDirectory();
    var candidates = new[]
    {
        Path.Combine(current, "wwwroot"),
        Path.Combine(current, "src", "ManiaAddNotesLab.Web", "wwwroot"),
        Path.Combine(AppContext.BaseDirectory, "wwwroot")
    };
    return candidates.FirstOrDefault(Directory.Exists) ?? candidates[^1];
}

static void RunBatch(BatchJob job, string inputPath, string safeStem)
{
    try
    {
        job.SetStatus("running");
        var inputText = File.ReadAllText(inputPath);
        var chart = OsuBeatmap.Parse(inputText);
        var evidenceProfile = MapperEvidenceProfileBuilder.Build(chart);
        File.WriteAllText(Path.Combine(job.Directory, "mapper-profile.json"),
            MapperEvidenceProfileJson.Serialize(evidenceProfile), new UTF8Encoding(false));
        var chances = BuildSweep(job.Options.ChanceStart, job.Options.ChanceEnd, job.Options.ChanceStep);
        job.SetTotal(chances.Count * job.Options.RunsPerChance);
        var rows = new List<RunRow>(job.TotalRuns);
        string? firstTrace = null;
        string? firstDiagnostics = null;
        var runIndex = 0;

        for (var chanceIndex = 0; chanceIndex < chances.Count; chanceIndex++)
        {
            var chance = chances[chanceIndex];
            for (var repeat = 0; repeat < job.Options.RunsPerChance; repeat++)
            {
                job.Cancellation.Token.ThrowIfCancellationRequested();
                var seed = unchecked(job.Options.BaseSeed + runIndex);
                var traceThisRun = job.Options.TraceFirst && runIndex == 0;
                var diagnosticsThisRun = job.Options.DiagnosticsFirst && runIndex == 0;
                var result = new AddNotesEngine().Apply(chart, new AddNotesOptions
                {
                    Chance = chance,
                    StartMs = job.Options.StartMs,
                    EndMs = job.Options.EndMs,
                    LnWindowBeats = job.Options.LnWindowBeats,
                    SourceAffinityMultiplier = job.Options.SourceAffinity,
                    DistanceDecayPerBeat = job.Options.DistanceDecay,
                    MinimumDistanceWeight = job.Options.MinimumDistanceWeight,
                    DensityGraceColumns = job.Options.DensityGraceColumns,
                    DensityDecayPerColumn = job.Options.DensityDecayPerColumn,
                    VerticalDensityMode = job.Options.VerticalDensityMode,
                    VerticalDensityScaleMode = job.Options.VerticalDensityScaleMode,
                    ContextualDensityNormalizationEnabled = job.Options.ContextualDensity,
                    ContextualDensityScaleMode = job.Options.ContextualDensityScaleMode,
                    BurstDensityRatioThreshold = job.Options.BurstRatioThreshold,
                    BurstSpanOneBeatFactor = job.Options.BurstFactor1,
                    BurstSpanTwoBeatFactor = job.Options.BurstFactor2,
                    BurstSpanThreeBeatFactor = job.Options.BurstFactor3,
                    UseLocalLaneGap = job.Options.UseLocalLaneGap,
                    LaneGapWindowBeats = job.Options.LaneGapWindowBeats,
                    LaneGapMinimumSupport = job.Options.LaneGapMinimumSupport,
                    FallbackMinimumLaneGapBeats = job.Options.FallbackLaneGapBeats,
                    MapRelativeSnapEnabled = job.Options.MapRelativeSnap,
                    InteriorLnOpportunitiesEnabled = job.Options.InteriorLnOpportunities,
                    MaxInteriorOpportunitiesPerSource = job.Options.MaxInteriorOpportunities,
                    InteriorMinimumSourceBeats = job.Options.MinimumInteriorSourceBeats,
                    InteriorMinimumSupportedAnchors = job.Options.MinimumInteriorAnchors,
                    InteriorEligibilityMode = job.Options.InteriorEligibilityMode,
                    InteriorContextMode = job.Options.InteriorContextMode,
                    ArticulationEnabled = job.Options.Articulation,
                    ArticulationMaxNonHeldColumns = job.Options.ArticulationMaxNonHeldColumns,
                    RetriggerGapWindowBeats = job.Options.RetriggerWindowBeats,
                    RetriggerGapMinimumSupport = job.Options.RetriggerMinimumSupport,
                    Trace = traceThisRun,
                    DiagnosticsEnabled = diagnosticsThisRun
                }, new SeededRandom(seed), evidenceProfile);

                if (traceThisRun) firstTrace = result.Trace;
                if (diagnosticsThisRun)
                {
                    var diagnostic = result.DecisionDiagnostics
                        ?? throw new InvalidOperationException("Decision diagnostics were not built.");
                    firstDiagnostics = DecisionDiagnosticsJson.SerializeMeasured(
                        diagnostic, DiagnosticDetailMode.Relevant);
                    result.Statistics.DetailedExportBytes = Encoding.UTF8.GetByteCount(firstDiagnostics);
                }
                if (job.Options.WriteCharts)
                {
                    var chanceLabel = (chance * 100).ToString("0.##", CultureInfo.InvariantCulture).Replace('.', '_');
                    var output = Path.Combine(job.Directory, $"{safeStem} [ADD {chanceLabel} seed {seed} run {repeat + 1}].osu");
                    var writeStarted = Stopwatch.GetTimestamp();
                    File.WriteAllText(output, OsuBeatmap.Write(result.ModifiedChart, chance), new UTF8Encoding(false));
                    result.Statistics.WriterMs = Stopwatch.GetElapsedTime(writeStarted).TotalMilliseconds;
                }

                rows.Add(RunRow.From(chance, seed, result.Statistics));
                runIndex++;
                job.SetProgress(runIndex);
            }
        }

        var reportPath = Path.Combine(job.Directory, "runs.csv");
        WriteCsv(reportPath, rows, job.Options, evidenceProfile);
        if (firstTrace is not null)
            File.WriteAllText(Path.Combine(job.Directory, "first-run-trace.txt"), firstTrace, new UTF8Encoding(false));
        if (firstDiagnostics is not null)
            File.WriteAllText(Path.Combine(job.Directory, "decision-diagnostics.json"), firstDiagnostics,
                new UTF8Encoding(false));
        job.Complete(rows, firstTrace, Path.GetFileName(reportPath));
    }
    catch (OperationCanceledException)
    {
        job.SetStatus("cancelled");
    }
    catch (Exception ex)
    {
        job.Fail(ex.Message);
    }
}

static List<double> BuildSweep(double start, double end, double step)
{
    var values = new List<double>();
    for (var value = start; value <= end + 1e-9; value += step)
        values.Add(Math.Round(value, 6));
    return values;
}

static void WriteCsv(string path, IReadOnlyList<RunRow> rows, BatchRequest options,
    MapperEvidenceProfile evidenceProfile)
{
    using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
    writer.WriteLine("chance,seed,ln_window_beats,source_affinity,distance_decay,minimum_distance_weight,density_grace_columns,density_decay_per_column,vertical_density_mode,interior_eligibility_mode,interior_context_mode,contextual_density,burst_ratio_threshold,burst_factor_1,burst_factor_2,burst_factor_3,use_local_lane_gap,lane_gap_window_beats,lane_gap_minimum_support,fallback_lane_gap_beats,map_relative_snap,interior_ln_opportunities,max_interior_opportunities,min_interior_source_beats,min_interior_anchors,input_objects,output_objects,original_taps,original_ln,added_taps,added_ln,base_opportunities,interior_opportunities,base_ln_opportunities,successful_base_rolls,successful_interior_rolls,base_ln_successful_rolls,added_ln_from_heads,added_ln_from_interior,base_ln_placed,opportunities,successful_rolls,placed,failed_placements,ln_candidate_retries,ln_geometry_skips,density_adjusted_opportunities,contextual_density_adjusted,mean_effective_chance,interior_mean_effective_chance,simultaneous_head_columns_mean,held_ln_columns_mean,interior_rejected_too_short,interior_rejected_context,interior_rejected_anchors,interior_rejected_legacy_length,interior_candidates_short,interior_candidates_medium,interior_candidates_long,interior_candidates_impossible,ln_shape_rejected_gap,ln_shape_rejected_overlap,geometry_checks,geometry_objects_examined,beat_conversions,ln_candidates_built,ln_shape_no_legal_lane,context_build_ms,candidate_build_ms,geometry_ms,writer_ms,original_ln_ratio,added_ln_ratio,result_ln_ratio,vertical_density_scale,context_density_scale,articulation,articulation_max_nonheld,retrigger_window_beats,retrigger_support,head_ratio_mean,held_ratio_mean,nonheld_columns_mean,nonheld_ratio_mean,legal_lane_ratio_mean,tap_opportunities,tap_successful_rolls,tap_placed,tap_placed_burst,tap_placed_sustained,tap_placed_normal,tap_effective_chance_mean,tap_chord_factor_mean,tap_contextual_factor_mean,interior_no_free_lane,interior_blocked_saturation,articulation_eligible,articulation_placed,rejected_anchor_not_head,rejected_not_saturated,rejected_no_retrigger_gap,rejected_left_too_short,rejected_right_too_short,rejected_parent_cap,rejected_geometry,rejected_already_modified,added_heads,added_releases,added_interactions,pass1_ms,articulation_pass_ms,behavior_policy_version,evidence_profile_version,profile_observations,profile_relations,profile_estimated_bytes");
    foreach (var r in rows)
        writer.WriteLine(string.Join(',', F(r.Chance), r.Seed, F(options.LnWindowBeats), F(options.SourceAffinity),
            F(options.DistanceDecay), F(options.MinimumDistanceWeight), options.DensityGraceColumns,
            F(options.DensityDecayPerColumn), options.VerticalDensityMode, options.InteriorEligibilityMode,
            options.InteriorContextMode, options.ContextualDensity, F(options.BurstRatioThreshold), F(options.BurstFactor1),
            F(options.BurstFactor2), F(options.BurstFactor3), options.UseLocalLaneGap, F(options.LaneGapWindowBeats),
            options.LaneGapMinimumSupport, F(options.FallbackLaneGapBeats), options.MapRelativeSnap,
            options.InteriorLnOpportunities, options.MaxInteriorOpportunities, F(options.MinimumInteriorSourceBeats), options.MinimumInteriorAnchors,
            r.InputObjects, r.OutputObjects, r.OriginalTaps, r.OriginalLn,
            r.AddedTaps, r.AddedLn, r.BaseOpportunities, r.InteriorOpportunities, r.BaseLnOpportunities, r.SuccessfulBaseRolls,
            r.SuccessfulInteriorRolls, r.BaseLnSuccessfulRolls, r.AddedLnFromHeads, r.AddedLnFromInterior, r.BaseLnPlaced,
            r.Opportunities, r.SuccessfulRolls,
            r.Placed, r.FailedPlacements, r.LnCandidateRetries, r.LnGeometrySkips, r.DensityAdjustedOpportunities,
            r.ContextualDensityAdjusted, F(r.MeanEffectiveChance), F(r.InteriorMeanEffectiveChance),
            F(r.SimultaneousHeadColumnsMean), F(r.HeldLnColumnsMean), r.InteriorRejectedTooShort,
            r.InteriorRejectedContext, r.InteriorRejectedAnchors, r.InteriorRejectedLegacyLength,
            r.InteriorCandidatesShort, r.InteriorCandidatesMedium, r.InteriorCandidatesLong,
            r.InteriorCandidatesImpossible, r.LnShapeRejectedGap, r.LnShapeRejectedOverlap,
            r.GeometryChecks, r.GeometryObjectsExamined,
            r.BeatConversions, r.LnCandidatesBuilt, r.LnCandidatesImpossible, F(r.ContextBuildMs), F(r.CandidateBuildMs),
            F(r.GeometryMs), F(r.WriterMs),
            F(r.OriginalLnRatio), F(r.AddedLnRatio), F(r.ResultLnRatio), options.VerticalDensityScaleMode,
            options.ContextualDensityScaleMode, options.Articulation, options.ArticulationMaxNonHeldColumns,
            F(options.RetriggerWindowBeats), options.RetriggerMinimumSupport, F(r.SimultaneousHeadRatioMean),
            F(r.HeldLnRatioMean), F(r.NonHeldColumnsMean), F(r.NonHeldRatioMean), F(r.LegalLaneRatioMean),
            r.TapOpportunities, r.TapSuccessfulRolls, r.TapPlaced, r.TapPlacedBurst, r.TapPlacedSustained,
            r.TapPlacedNormal, F(r.TapEffectiveChanceMean), F(r.TapChordFactorMean), F(r.TapContextualFactorMean),
            r.InteriorNoFreeLane, r.InteriorBlockedSaturation, r.ArticulationEligible, r.ArticulationPlaced,
            r.RejectedAnchorNotHead, r.RejectedNotSaturated, r.RejectedNoRetriggerGap, r.RejectedLeftTooShort,
            r.RejectedRightTooShort, r.RejectedParentCap, r.RejectedGeometry, r.RejectedAlreadyModified,
            r.AddedHeads, r.AddedReleases, r.AddedInteractions, F(r.Pass1Ms), F(r.ArticulationPassMs),
            evidenceProfile.BehaviorPolicyVersion, evidenceProfile.EvidenceProfileVersion,
            evidenceProfile.ObservationCount, evidenceProfile.RelationCount, evidenceProfile.EstimatedSizeBytes));
    static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}

static string SafeStem(string value)
{
    var invalid = Path.GetInvalidFileNameChars();
    var safe = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
    return string.IsNullOrWhiteSpace(safe) ? "chart" : safe;
}

sealed record BatchRequest(double ChanceStart, double ChanceEnd, double ChanceStep, int RunsPerChance,
    int BaseSeed, double LnWindowBeats, double SourceAffinity, double DistanceDecay, double MinimumDistanceWeight,
    int DensityGraceColumns, double DensityDecayPerColumn, bool ContextualDensity, double BurstRatioThreshold,
    double BurstFactor1, double BurstFactor2, double BurstFactor3, bool UseLocalLaneGap,
    double LaneGapWindowBeats, int LaneGapMinimumSupport, double FallbackLaneGapBeats,
    bool MapRelativeSnap, bool InteriorLnOpportunities, int MaxInteriorOpportunities, double MinimumInteriorSourceBeats,
    int MinimumInteriorAnchors,
    VerticalDensityMode VerticalDensityMode, VerticalDensityScaleMode VerticalDensityScaleMode,
    ContextualDensityScaleMode ContextualDensityScaleMode, InteriorEligibilityMode InteriorEligibilityMode,
    InteriorContextMode InteriorContextMode,
    bool Articulation, int ArticulationMaxNonHeldColumns, double RetriggerWindowBeats,
    int RetriggerMinimumSupport,
    int? StartMs, int? EndMs, bool WriteCharts, bool TraceFirst, bool DiagnosticsFirst)
{
    public static BatchRequest From(IFormCollection form)
    {
        double D(string name, double fallback) => double.TryParse(form[name], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        int I(string name, int fallback) => int.TryParse(form[name], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        int? NI(string name) => string.IsNullOrWhiteSpace(form[name]) ? null : I(name, 0);
        bool B(string name) => string.Equals(form[name], "true", StringComparison.OrdinalIgnoreCase);

        var request = new BatchRequest(D("chanceStart", .1), D("chanceEnd", .5), D("chanceStep", .1),
            I("runsPerChance", 20), I("baseSeed", 1), D("lnWindowBeats", 4), D("sourceAffinity", 1.25),
            D("distanceDecay", .2), D("minimumDistanceWeight", .2), I("densityGraceColumns", 2),
            D("densityDecayPerColumn", .65), B("contextualDensity"), D("burstRatioThreshold", 1.5),
            D("burstFactor1", .45), D("burstFactor2", .60), D("burstFactor3", .80), B("useLocalLaneGap"),
            D("laneGapWindowBeats", 4), I("laneGapMinimumSupport", 2), D("fallbackLaneGapBeats", .125),
            B("mapRelativeSnap"), B("interiorLnOpportunities"), I("maxInteriorOpportunities", 2),
            D("minimumInteriorSourceBeats", 3),
            I("minimumInteriorAnchors", 2),
            Enum.TryParse<VerticalDensityMode>(form["verticalDensityMode"], out var vertical) ? vertical : VerticalDensityMode.SimultaneousHeads,
            Enum.TryParse<VerticalDensityScaleMode>(form["verticalDensityScaleMode"], out var verticalScale) ? verticalScale : VerticalDensityScaleMode.KeymodeRelative,
            Enum.TryParse<ContextualDensityScaleMode>(form["contextualDensityScaleMode"], out var contextScale) ? contextScale : ContextualDensityScaleMode.KeymodeRelative,
            Enum.TryParse<InteriorEligibilityMode>(form["interiorEligibilityMode"], out var eligibility) ? eligibility : InteriorEligibilityMode.AnchorSupported,
            Enum.TryParse<InteriorContextMode>(form["interiorContextMode"], out var context) ? context : InteriorContextMode.OriginalOnly,
            B("articulation"), I("articulationMaxNonHeldColumns", 1), D("retriggerWindowBeats", 4),
            I("retriggerMinimumSupport", 2),
            NI("startMs"), NI("endMs"),
            B("writeCharts"), B("traceFirst"), B("diagnosticsFirst"));
        if (request.ChanceStart is < 0 or > 1 || request.ChanceEnd is < 0 or > 1 || request.ChanceStart > request.ChanceEnd)
            throw new ArgumentException("El rango de probabilidad debe estar entre 0 y 1 y en orden ascendente.");
        if (request.ChanceStep is <= 0 or > 1) throw new ArgumentException("El paso de probabilidad debe estar entre 0 y 1.");
        if (request.RunsPerChance is < 1 or > 10_000) throw new ArgumentException("Las seeds por valor deben estar entre 1 y 10.000.");
        if (request.LnWindowBeats is <= 0 or > 32) throw new ArgumentException("La ventana LN debe estar entre 0 y 32 beats.");
        if (request.SourceAffinity is < 0.5 or > 3) throw new ArgumentException("La afinidad source debe estar entre 0.5 y 3.");
        if (request.DistanceDecay is < 0 or > 1) throw new ArgumentException("La caída por beat debe estar entre 0 y 1.");
        if (request.MinimumDistanceWeight is <= 0 or > 1) throw new ArgumentException("El peso mínimo debe estar entre 0 y 1.");
        if (request.DensityGraceColumns is < 0 or > 18) throw new ArgumentException("La gracia de densidad debe estar entre 0 y 18 columnas.");
        if (request.DensityDecayPerColumn is <= 0 or > 1) throw new ArgumentException("La retención por columna debe estar entre 0 y 1.");
        if (request.BurstRatioThreshold <= 1) throw new ArgumentException("El umbral contextual debe ser mayor que 1.");
        if (request.BurstFactor1 is <= 0 or > 1 || request.BurstFactor2 is <= 0 or > 1 || request.BurstFactor3 is <= 0 or > 1)
            throw new ArgumentException("Los factores contextuales deben estar entre 0 y 1.");
        if (request.LaneGapWindowBeats <= 0 || request.LaneGapMinimumSupport < 1 || request.FallbackLaneGapBeats is < 0 or > 1)
            throw new ArgumentException("La configuración de separación local no es válida.");
        if (request.MaxInteriorOpportunities is < 0 or > 2) throw new ArgumentException("Las oportunidades interiores deben estar entre 0 y 2.");
        if (request.MinimumInteriorSourceBeats is <= 0 or > 32) throw new ArgumentException("La duración mínima interior debe estar entre 0 y 32 beats.");
        if (request.MinimumInteriorAnchors is < 1 or > 32) throw new ArgumentException("El soporte de anchors debe estar entre 1 y 32.");
        if (request.ArticulationMaxNonHeldColumns is < 0 or > 18) throw new ArgumentException("El umbral de saturación debe estar entre 0 y 18 columnas.");
        if (request.RetriggerWindowBeats is <= 0 or > 32 || request.RetriggerMinimumSupport is < 1 or > 32)
            throw new ArgumentException("La configuración de retrigger local no es válida.");
        if (request.StartMs is not null && request.EndMs is not null && request.StartMs > request.EndMs)
            throw new ArgumentException("El inicio del rango no puede superar al final.");
        var configurations = (int)Math.Floor((request.ChanceEnd - request.ChanceStart) / request.ChanceStep + 1e-9) + 1;
        if ((long)configurations * request.RunsPerChance > 20_000)
            throw new ArgumentException("El lote no puede superar 20.000 ejecuciones.");
        if (request.WriteCharts && (long)configurations * request.RunsPerChance > 2_000)
            throw new ArgumentException("La generación de charts está limitada a 2.000 archivos por lote.");
        return request;
    }
}

sealed class BatchJob(Guid id, string inputName, string directory, BatchRequest options)
{
    private readonly object _gate = new();
    private IReadOnlyList<AggregateRow> _aggregates = [];
    public Guid Id { get; } = id;
    public string InputName { get; } = inputName;
    public string Directory { get; } = directory;
    public BatchRequest Options { get; } = options;
    public CancellationTokenSource Cancellation { get; } = new();
    public string Status { get; private set; } = "queued";
    public int CompletedRuns { get; private set; }
    public int TotalRuns { get; private set; }
    public string? Error { get; private set; }
    public string? Trace { get; private set; }
    public string? Report { get; private set; }

    public void SetStatus(string status) { lock (_gate) Status = status; }
    public void SetTotal(int total) { lock (_gate) TotalRuns = total; }
    public void SetProgress(int completed) { lock (_gate) CompletedRuns = completed; }
    public void Fail(string error) { lock (_gate) { Error = error; Status = "failed"; } }
    public void Complete(IReadOnlyList<RunRow> rows, string? trace, string report)
    {
        lock (_gate)
        {
            _aggregates = rows.GroupBy(r => r.Chance).Select(AggregateRow.From).OrderBy(r => r.Chance).ToArray();
            Trace = trace;
            Report = report;
            Status = "completed";
        }
    }

    public object Snapshot()
    {
        lock (_gate) return new { Id, InputName, Status, CompletedRuns, TotalRuns, Error, Trace, Report, Aggregates = _aggregates };
    }
}

sealed record RunRow(double Chance, int Seed, int InputObjects, int OutputObjects, int OriginalTaps, int OriginalLn,
    int AddedTaps, int AddedLn, int BaseOpportunities, int InteriorOpportunities, int SuccessfulBaseRolls,
    int SuccessfulInteriorRolls, int AddedLnFromHeads, int AddedLnFromInterior, int Opportunities,
    int SuccessfulRolls, int Placed, int FailedPlacements, int LnCandidateRetries, int LnGeometrySkips,
    int DensityAdjustedOpportunities, int ContextualDensityAdjusted, double MeanEffectiveChance,
    long GeometryChecks, long GeometryObjectsExamined, long BeatConversions, int LnCandidatesBuilt,
    int LnCandidatesImpossible, double ContextBuildMs, double CandidateBuildMs, double GeometryMs, double WriterMs,
    double OriginalLnRatio, double AddedLnRatio, double ResultLnRatio,
    int BaseLnOpportunities, int BaseLnSuccessfulRolls, int BaseLnPlaced,
    int InteriorRejectedTooShort, int InteriorRejectedContext, int InteriorRejectedAnchors,
    int InteriorRejectedLegacyLength, int InteriorCandidatesShort, int InteriorCandidatesMedium,
    int InteriorCandidatesLong, int InteriorCandidatesImpossible, double InteriorMeanEffectiveChance,
    double SimultaneousHeadColumnsMean, double HeldLnColumnsMean, int LnShapeRejectedGap,
    int LnShapeRejectedOverlap, double SimultaneousHeadRatioMean, double HeldLnRatioMean,
    double NonHeldColumnsMean, double NonHeldRatioMean, double LegalLaneRatioMean,
    int TapOpportunities, int TapSuccessfulRolls, int TapPlaced, int TapPlacedBurst,
    int TapPlacedSustained, int TapPlacedNormal, double TapEffectiveChanceMean,
    double TapChordFactorMean, double TapContextualFactorMean, int InteriorNoFreeLane,
    int InteriorBlockedSaturation, int ArticulationEligible, int ArticulationPlaced,
    int RejectedAnchorNotHead, int RejectedNotSaturated, int RejectedNoRetriggerGap,
    int RejectedLeftTooShort, int RejectedRightTooShort, int RejectedParentCap,
    int RejectedGeometry, int RejectedAlreadyModified, int AddedHeads, int AddedReleases,
    int AddedInteractions, double Pass1Ms, double ArticulationPassMs)
{
    public static RunRow From(double chance, int seed, AddNotesStatistics s) => new(chance, seed, s.InputObjects,
        s.OutputObjects, s.OriginalTaps, s.OriginalLongNotes, s.AddedTaps, s.AddedLongNotes, s.BaseHeadOpportunities,
        s.InteriorLnOpportunities, s.SuccessfulBaseRolls, s.SuccessfulInteriorRolls,
        s.AddedLongNotesFromHeadOpportunities, s.AddedLongNotesFromInteriorOpportunities, s.TotalOpportunities,
        s.SuccessfulProbabilityRolls, s.PlacedObjects, s.FailedPlacements, s.LnCandidateRetries,
        s.LnSkipsDueToGeometry, s.DensityAdjustedOpportunities, s.ContextualDensityAdjustedOpportunities,
        s.MeanEffectiveChance, s.GeometryChecks, s.GeometryObjectsExamined, s.BeatConversions,
        s.LnCandidatesBuilt, s.LnCandidatesImpossible, s.ContextBuildMs, s.CandidateBuildMs, s.GeometryMs, s.WriterMs,
        s.OriginalLnRatio, s.AddedLnRatio, s.ResultLnRatio, s.BaseLnOpportunityCount, s.BaseLnSuccessfulRolls,
        s.BaseLnPlaced, s.InteriorRejectedTooShort, s.InteriorRejectedInsufficientLnContext,
        s.InteriorRejectedNoSupportedAnchors, s.InteriorRejectedRelativeLengthLegacy, s.InteriorCandidatesShort,
        s.InteriorCandidatesMedium, s.InteriorCandidatesLong, s.InteriorCandidatesImpossible,
        s.InteriorMeanEffectiveChance, s.SimultaneousHeadColumnsMean, s.HeldLnColumnsMean,
        s.LnShapeRejectedGap, s.LnShapeRejectedOverlap, s.SimultaneousHeadRatioMean,
        s.HeldLnRatioMean, s.NonHeldColumnsMean, s.NonHeldRatioMean, s.LegalLaneRatioMean,
        s.TapOpportunities, s.TapSuccessfulRolls, s.TapPlaced, s.TapPlacedBurst,
        s.TapPlacedSustained, s.TapPlacedNormal, s.TapEffectiveChanceMean,
        s.MeanChordFactorForTap, s.MeanContextualFactorForTap, s.InteriorNoFreeLane,
        s.InteriorBlockedByLnSaturation, s.ArticulationEligible, s.ArticulationPlaced,
        s.RejectedAnchorNotHead, s.RejectedNotSaturated, s.RejectedNoRetriggerGap,
        s.RejectedLeftTooShort, s.RejectedRightTooShort, s.RejectedParentCap,
        s.RejectedGeometry, s.RejectedAlreadyModified, s.AddedHeads, s.AddedReleases,
        s.AddedInteractions, s.Pass1Ms, s.ArticulationPassMs);
}

sealed record AggregateRow(double Chance, int Runs, double MeanPlaced, int MinPlaced, int MaxPlaced,
    double MeanAddedTaps, double MeanAddedLn, double MeanFailed, double MeanRetries,
    double MeanDensityAdjusted, double MeanEffectiveChance, double MeanResultLnRatio,
    double MeanInteriorOpportunities, double MeanInteriorPlaced, double MeanInteriorEffectiveChance,
    double MeanSimultaneousHeadColumns, double MeanHeldLnColumns, double MeanInteriorSuccessfulRolls,
    double MeanInteriorRejectedTooShort, double MeanInteriorRejectedContext, double MeanInteriorRejectedAnchors,
    double MeanInteriorRejectedLegacyLength, double MeanInteriorCandidatesShort,
    double MeanInteriorCandidatesMedium, double MeanInteriorCandidatesLong,
    double MeanInteriorCandidatesImpossible, double InteriorImpossibleRate,
    double MeanArticulationPlaced)
{
    public static AggregateRow From(IGrouping<double, RunRow> group) => new(group.Key, group.Count(),
        group.Average(r => r.Placed), group.Min(r => r.Placed), group.Max(r => r.Placed),
        group.Average(r => r.AddedTaps), group.Average(r => r.AddedLn), group.Average(r => r.FailedPlacements),
        group.Average(r => r.LnCandidateRetries), group.Average(r => r.DensityAdjustedOpportunities),
        group.Average(r => r.MeanEffectiveChance), group.Average(r => r.ResultLnRatio),
        group.Average(r => r.InteriorOpportunities), group.Average(r => r.AddedLnFromInterior),
        group.Average(r => r.InteriorMeanEffectiveChance), group.Average(r => r.SimultaneousHeadColumnsMean),
        group.Average(r => r.HeldLnColumnsMean), group.Average(r => r.SuccessfulInteriorRolls),
        group.Average(r => r.InteriorRejectedTooShort), group.Average(r => r.InteriorRejectedContext),
        group.Average(r => r.InteriorRejectedAnchors), group.Average(r => r.InteriorRejectedLegacyLength),
        group.Average(r => r.InteriorCandidatesShort), group.Average(r => r.InteriorCandidatesMedium),
        group.Average(r => r.InteriorCandidatesLong), group.Average(r => r.InteriorCandidatesImpossible),
        Ratio(group.Sum(r => r.InteriorCandidatesImpossible), group.Sum(r => r.InteriorCandidatesShort
            + r.InteriorCandidatesMedium + r.InteriorCandidatesLong)),
        group.Average(r => r.ArticulationPlaced));

    private static double Ratio(int part, int total) => total == 0 ? 0 : (double)part / total;
}
