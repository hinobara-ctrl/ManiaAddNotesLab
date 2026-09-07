using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManiaAddNotesLab.Core;

if (args.Length == 4 && args[0] == "d0-1-corpus")
{
    D01CorpusRunner.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]),
        Path.GetFullPath(args[3]));
    return;
}

if (args.Length == 4 && args[0] == "d0-corpus")
{
    D0CorpusRunner.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]),
        Path.GetFullPath(args[3]));
    return;
}

if (args.Length == 4 && args[0] == "c1-2-corpus")
{
    C12CorpusRunner.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]),
        Path.GetFullPath(args[3]));
    return;
}

if (args.Length == 4 && args[0] == "c1-1-corpus")
{
    C11CorpusRunner.Run(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]),
        Path.GetFullPath(args[3]));
    return;
}

if (args.Length >= 3 && args[0] == "c1-performance")
{
    var chartPath = Path.GetFullPath(args[1]);
    var artifact = Path.GetFullPath(args[2]);
    var runs = args.Length >= 4 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 3;
    var chart = OsuBeatmap.Parse(File.ReadAllText(chartPath));
    var profile = MapperEvidenceProfileBuilder.Build(chart);
    var options = Options(.5);
    _ = new AddNotesEngine().Apply(chart, options with { DiagnosticsEnabled = false }, new SeededRandom(0), profile);
    _ = new AddNotesEngine().Apply(chart, options with { DiagnosticsEnabled = true }, new SeededRandom(0), profile);
    var off = new List<AddNotesStatistics>();
    var on = new List<AddNotesStatistics>();
    foreach (var seed in Enumerable.Range(1, runs))
    {
        if ((seed & 1) == 0)
        {
            on.Add(new AddNotesEngine().Apply(chart, options with { DiagnosticsEnabled = true },
                new SeededRandom(seed), profile).Statistics);
            off.Add(new AddNotesEngine().Apply(chart, options with { DiagnosticsEnabled = false },
                new SeededRandom(seed), profile).Statistics);
        }
        else
        {
            off.Add(new AddNotesEngine().Apply(chart, options with { DiagnosticsEnabled = false },
                new SeededRandom(seed), profile).Statistics);
            on.Add(new AddNotesEngine().Apply(chart, options with { DiagnosticsEnabled = true },
                new SeededRandom(seed), profile).Statistics);
        }
    }
    var result = new C1PerformanceResult(chartPath, runs,
        off.Average(x => x.CandidateBuildMs), on.Average(x => x.CandidateBuildMs),
        off.Average(x => x.Pass1Ms), on.Average(x => x.Pass1Ms),
        off.Average(x => x.CertificateBuildMs), on.Average(x => x.CertificateBuildMs),
        off.Average(x => x.FailureDiagnosticMs), on.Average(x => x.FailureDiagnosticMs));
    Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);
    File.WriteAllText(artifact, JsonSerializer.Serialize(result, new JsonSerializerOptions
        { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), new UTF8Encoding(false));
    Console.WriteLine(JsonSerializer.Serialize(result));
    return;
}

if (args.Length >= 3 && args[0] == "c1-held-out")
{
    var artifact = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(artifact)!);
    var results = args[2..].Select(path =>
    {
        var fullPath = Path.GetFullPath(path);
        var chart = OsuBeatmap.Parse(File.ReadAllText(fullPath));
        return new C1ChartResearchResult(fullPath, LnWitnessDeduplicationResearch.Evaluate(chart));
    }).ToArray();
    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    File.WriteAllText(artifact, JsonSerializer.Serialize(results, jsonOptions), new UTF8Encoding(false));
    foreach (var item in results)
        Console.WriteLine($"{Path.GetFileName(item.ChartPath)}: K={item.Result.KeyCount} " +
            $"targets={item.Result.TargetsEvaluated} vocabulary={item.Result.TargetsWithCandidateVocabulary} " +
            $"present={item.Result.TrueTargetCandidatePresent} legacyTop={item.Result.LegacyTrueTargetRankOne} " +
            $"c1Top={item.Result.DeduplicatedTrueTargetRankOne} duplicateTargets={item.Result.DuplicateWitnessAffectedTargets} " +
            $"rankChanged={item.Result.RankingsChanged} topChanged={item.Result.TopCandidatesChanged}");
    Console.WriteLine($"Wrote C1 held-out reconstruction to {artifact}");
    return;
}

var outputRoot = args.Length > 0 ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../ab-results"));
Directory.CreateDirectory(outputRoot);

var matrix = new List<MatrixRow>();
foreach (var keys in Enumerable.Range(4, 7))
foreach (var chance in new[] { .10, .30, .50 })
foreach (var scenario in new[] { (Name: "Full", NonHeld: 0), (Name: "NearFull", NonHeld: 1),
             (Name: "Moderate", NonHeld: 2) })
{
    var chart = Fixture(keys, scenario.NonHeld);
    var evidenceProfile = MapperEvidenceProfileBuilder.Build(chart);
    var runs = Enumerable.Range(1, 20).Select(seed =>
        new AddNotesEngine().Apply(chart, Options(chance), new SeededRandom(seed), evidenceProfile).Statistics)
        .ToArray();
    matrix.Add(MatrixRow.From(keys, chance, scenario.Name, runs));
}

WriteCsv(Path.Combine(outputRoot, "cross-key-matrix.csv"), matrix);
WriteVerticalMatrix(Path.Combine(outputRoot, "vertical-density-matrix.csv"));
Console.WriteLine($"Wrote {matrix.Count} aggregate rows to {outputRoot}");

static AddNotesOptions Options(double chance) => new()
{
    Chance = chance,
    ContextualDensityNormalizationEnabled = true,
    ContextualDensityScaleMode = ContextualDensityScaleMode.KeymodeRelative,
    VerticalDensityMode = VerticalDensityMode.SimultaneousHeads,
    VerticalDensityScaleMode = VerticalDensityScaleMode.KeymodeRelative,
    InteriorLnOpportunitiesEnabled = true,
    MaxInteriorOpportunitiesPerSource = 2,
    InteriorMinimumSourceBeats = 3,
    InteriorMinimumContextLnCount = 3,
    InteriorMinimumSupportedAnchors = 2,
    ArticulationEnabled = true,
    ArticulationMaxNonHeldColumns = 1,
    RetriggerGapMinimumSupport = 2,
    RetriggerGapWindowBeats = 4
};

static ManiaChart Fixture(int keys, int nonHeldAtAnchor)
{
    const int beatLength = 500;
    const decimal endBeat = 8;
    const decimal headBeat = 4;
    const decimal gap = .25m;
    static int T(decimal beat) => (int)decimal.Round(beat * beatLength, 0,
        MidpointRounding.AwayFromZero);
    var objects = new List<ManiaObject>
    {
        ManiaObject.Ln(0, T(0), T(endBeat)),
        ManiaObject.Ln(1, T(0), T(headBeat - gap)),
        ManiaObject.Ln(1, T(headBeat), T(endBeat))
    };
    var heldOtherLanes = Math.Max(0, keys - nonHeldAtAnchor - 2);
    for (var lane = 2; lane < keys; lane++)
    {
        objects.Add(ManiaObject.Ln(lane, T(-2), T(-gap)));
        objects.Add(ManiaObject.Tap(lane, T(0)));
        objects.Add(lane - 2 < heldOtherLanes
            ? ManiaObject.Ln(lane, T(headBeat), T(endBeat))
            : ManiaObject.Tap(lane, T(headBeat + .2m)));
    }
    return new ManiaChart
    {
        KeyCount = keys,
        Lines = [],
        OriginalObjects = objects.Select((item, index) => item with { Sequence = index }).ToArray(),
        TimingPoints = [new TimingPoint(0, beatLength)]
    };
}

static void WriteCsv(string path, IReadOnlyList<MatrixRow> rows)
{
    using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
    writer.WriteLine("key_count,chance,scenario,runs,head_ratio,held_ratio,chord_factor,context_factor,tap_opportunities,tap_rolls,tap_placements,interior_opportunities,interior_rolls,free_lane_placements,articulation_eligible,articulation_placed,added_interactions,legal_lane_ratio,nonheld_columns,rejected_not_saturated");
    foreach (var r in rows)
        writer.WriteLine(string.Join(',', r.KeyCount, F(r.Chance), r.Scenario, r.Runs, F(r.HeadRatio),
            F(r.HeldRatio), F(r.ChordFactor), F(r.ContextFactor), F(r.TapOpportunities), F(r.TapRolls),
            F(r.TapPlacements), F(r.InteriorOpportunities), F(r.InteriorRolls), F(r.FreeLanePlacements),
            F(r.ArticulationEligible), F(r.ArticulationPlaced), F(r.AddedInteractions),
            F(r.LegalLaneRatio), F(r.NonHeldColumns), F(r.RejectedNotSaturated)));
}

static void WriteVerticalMatrix(string path)
{
    using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
    writer.WriteLine("key_count,occupancy_percent,columns,absolute_legacy_factor,keymode_relative_factor");
    var engine = new AddNotesEngine();
    foreach (var keys in Enumerable.Range(4, 7))
    foreach (var percent in new[] { 25, 50, 75, 100 })
    {
        var columns = Math.Clamp((int)Math.Round(keys * percent / 100.0,
            MidpointRounding.AwayFromZero), 0, keys);
        var legacy = engine.AnalyzeVerticalDensity(keys, columns, new AddNotesOptions
            { VerticalDensityScaleMode = VerticalDensityScaleMode.AbsoluteLegacy }).ChordFactor;
        var relative = engine.AnalyzeVerticalDensity(keys, columns, new AddNotesOptions
            { VerticalDensityScaleMode = VerticalDensityScaleMode.KeymodeRelative }).ChordFactor;
        writer.WriteLine(string.Join(',', keys, percent, columns, F(legacy), F(relative)));
    }
}

static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

sealed record MatrixRow(int KeyCount, double Chance, string Scenario, int Runs, double HeadRatio,
    double HeldRatio, double ChordFactor, double ContextFactor, double TapOpportunities, double TapRolls,
    double TapPlacements, double InteriorOpportunities, double InteriorRolls, double FreeLanePlacements,
    double ArticulationEligible, double ArticulationPlaced, double AddedInteractions,
    double LegalLaneRatio, double NonHeldColumns, double RejectedNotSaturated)
{
    public static MatrixRow From(int keys, double chance, string scenario, IReadOnlyList<AddNotesStatistics> rows)
    {
        double A(Func<AddNotesStatistics, double> selector) => rows.Average(selector);
        return new(keys, chance, scenario, rows.Count, A(x => x.SimultaneousHeadRatioMean),
            A(x => x.HeldLnRatioMean), A(x => x.MeanChordFactorForTap),
            A(x => x.MeanContextualFactorForTap), A(x => x.TapOpportunities),
            A(x => x.TapSuccessfulRolls), A(x => x.TapPlaced), A(x => x.InteriorLnOpportunities),
            A(x => x.SuccessfulInteriorRolls), A(x => x.InteriorFreeLanePlaced),
            A(x => x.ArticulationEligible), A(x => x.ArticulationPlaced), A(x => x.AddedInteractions),
            A(x => x.LegalLaneRatioMean), A(x => x.NonHeldColumnsMean), A(x => x.RejectedNotSaturated));
    }
}

sealed record C1ChartResearchResult(string ChartPath, HeldOutLnReconstructionResult Result);
sealed record C1PerformanceResult(string ChartPath, int Runs, double DiagnosticsOffCandidateBuildMs,
    double DiagnosticsOnCandidateBuildMs, double DiagnosticsOffPass1Ms, double DiagnosticsOnPass1Ms,
    double DiagnosticsOffCertificateMs, double DiagnosticsOnCertificateMs,
    double DiagnosticsOffFailureMs, double DiagnosticsOnFailureMs);
