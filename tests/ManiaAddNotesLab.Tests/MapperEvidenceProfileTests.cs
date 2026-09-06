using System.Collections;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class MapperEvidenceProfileTests
{
    [Fact]
    public void ProfileIsDeterministicAndObservationIdsAreStable()
    {
        var chart = Chart(4, ManiaObject.Tap(0, 7), ManiaObject.Ln(1, 107, 607),
            ManiaObject.Tap(2, 107));

        var first = MapperEvidenceProfileBuilder.Build(chart);
        var second = MapperEvidenceProfileBuilder.Build(chart);

        Assert.Equal(MapperEvidenceProfileJson.Serialize(first), MapperEvidenceProfileJson.Serialize(second));
        Assert.Equal(first.Observations.Select(x => x.Id), second.Observations.Select(x => x.Id));
        Assert.Equal(first.ObservationCount, first.Observations.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public void ProfileIsIndependentOfSeedChanceAndSelectedRange()
    {
        var chart = Chart(4, ManiaObject.Tap(0, 0), ManiaObject.Ln(1, 500, 1000),
            ManiaObject.Tap(2, 1500));
        var engine = new AddNotesEngine();
        var a = engine.Apply(chart, new AddNotesOptions { Chance = 0, StartMs = 0, EndMs = 0 },
            new SeededRandom(1));
        var b = engine.Apply(chart, new AddNotesOptions { Chance = 1, StartMs = 1500, EndMs = 1500 },
            new SeededRandom(999));

        Assert.Equal(MapperEvidenceProfileJson.Serialize(a.EvidenceProfile),
            MapperEvidenceProfileJson.Serialize(b.EvidenceProfile));
    }

    [Fact]
    public void AddedObjectsAndArticulationReplacementsCannotBecomeEvidence()
    {
        var parent = ManiaObject.Ln(0, 0, 1000) with { Sequence = 0, RawLine = "64,192,0,128,0,1000:0:0:0:0:" };
        var originalList = new List<ManiaObject> { parent };
        var baseChart = Chart(4, originalList.ToArray());
        var chart = new ManiaChart
        {
            KeyCount = baseChart.KeyCount,
            Lines = baseChart.Lines,
            OriginalObjects = baseChart.OriginalObjects,
            TimingPoints = baseChart.TimingPoints,
            AddedObjects = [ManiaObject.Tap(1, 500, true)],
            ArticulationReplacements = [new ArticulationReplacement(baseChart.OriginalObjects[0],
                ManiaObject.Ln(0, 0, 375, true), ManiaObject.Ln(0, 500, 1000, true), 375, 500)]
        };

        var profile = MapperEvidenceProfileBuilder.Build(chart);

        Assert.Single(profile.Observations);
        Assert.Equal(0, profile.Observations[0].Lane);
        Assert.Equal(0, profile.Observations[0].StartTime);
        Assert.Equal(1000, profile.Observations[0].EndTime);
        Assert.Equal(MapperEvidenceProfileBuilder.Build(baseChart).ChartFingerprint, profile.ChartFingerprint);
    }

    [Fact]
    public void ProfileDoesNotAliasMutableOriginalStorage()
    {
        var originals = new List<ManiaObject> { ManiaObject.Tap(0, 0) };
        var chart = ChartFromStorage(4, originals);
        var profile = MapperEvidenceProfileBuilder.Build(chart);

        originals[0] = ManiaObject.Ln(3, 100, 900);
        originals.Add(ManiaObject.Tap(2, 500));

        Assert.Single(profile.Observations);
        Assert.Equal(ManiaObjectType.Tap, profile.Observations[0].Type);
        Assert.Equal(0, profile.Observations[0].Lane);
        Assert.Equal(0, profile.Observations[0].StartTime);
    }

    [Fact]
    public void DurationReleaseChordTransitionRetriggerAndInteriorProvenanceAreExtracted()
    {
        var chart = Chart(4,
            ManiaObject.Ln(0, 0, 2000),
            ManiaObject.Ln(1, 0, 750),
            ManiaObject.Tap(1, 1000),
            ManiaObject.Tap(2, 1000));

        var profile = MapperEvidenceProfileBuilder.Build(chart);

        Assert.Contains(profile.ChordObservations, x => x.Time == 0 && x.SimultaneousHeadCount == 2);
        Assert.Contains(profile.LnDurations, x => x.DurationBeats == 4m && x.WitnessIds.Length == 1);
        Assert.Contains(profile.ExactReleases, x => x.ReleaseTime == 750 && x.WitnessIds.Length == 1);
        Assert.Contains(profile.SameLaneTransitions, x => x.TransitionType == OriginalTransitionType.LongNoteToTap
            && x.GapBeats == .5m);
        Assert.Contains(profile.RetriggerObservations, x => x.TransitionType == RetriggerTransitionType.ReleaseToTap
            && x.GapBeats == .5m);
        Assert.Contains(profile.InteriorAnchorObservations, x => x.ParentLongNoteId == new OriginalObservationId(0)
            && x.AnchorTime == 1000 && x.Kind.HasFlag(InteriorAnchorKind.Head)
            && x.HeadWitnessIds.Length == 2);
    }

    [Fact]
    public void OneFifthOneTenthAndOffsetTimingRemainExactObservations()
    {
        var chart = ChartWithTiming(4, [new TimingPoint(7, 500)],
            ManiaObject.Ln(0, 7, 107),
            ManiaObject.Ln(1, 207, 257));

        var profile = MapperEvidenceProfileBuilder.Build(chart);

        Assert.Contains(profile.Observations, x => x.StartBeat == 0m && x.EndBeat == .2m);
        Assert.Contains(profile.Observations, x => x.StartBeat == .4m && x.EndBeat == .5m);
        Assert.Contains(profile.LnDurations, x => x.DurationBeats == .2m);
        Assert.Contains(profile.LnDurations, x => x.DurationBeats == .1m);
        Assert.Contains(.4m, profile.TimingVocabulary.HeadBeatFractions);
        Assert.Contains(.5m, profile.TimingVocabulary.ReleaseBeatFractions);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void ProfileBuildsAcrossSupportedKeymodes(int keys)
    {
        var chart = Chart(keys, ManiaObject.Tap(0, 0), ManiaObject.Ln(keys - 1, 500, 1000));

        var profile = MapperEvidenceProfileBuilder.Build(chart);

        Assert.Equal(keys, profile.KeyCount);
        Assert.Equal(2, profile.ObservationCount);
        Assert.All(profile.ChordObservations, x => Assert.InRange(x.HeadOccupancyRatio, 0m, 1m));
        Assert.All(profile.ChordObservations, x => Assert.InRange(x.HeldOccupancyRatio, 0m, 1m));
    }

    [Fact]
    public void JsonExportContainsRequiredDiagnosticsAndNoConfidenceClaim()
    {
        var profile = MapperEvidenceProfileBuilder.Build(Chart(4,
            ManiaObject.Tap(0, 0), ManiaObject.Ln(1, 500, 1000)));

        var json = MapperEvidenceProfileJson.Serialize(profile);

        Assert.Contains("\"chartFingerprint\"", json);
        Assert.Contains("\"evidenceProfileVersion\": \"phase-a.1\"", json);
        Assert.Contains("\"timingVocabulary\"", json);
        Assert.Contains("\"sameLaneTransitions\"", json);
        Assert.Contains("\"retriggerObservations\"", json);
        Assert.Contains("\"interiorAnchorObservations\"", json);
        Assert.DoesNotContain("confidence", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildingAndIntegratingProfilePreservesOutputRngAndExistingDecisionMetrics()
    {
        var chart = Chart(7,
            ManiaObject.Tap(0, 0),
            ManiaObject.Tap(1, 500),
            ManiaObject.Ln(2, 1000, 2000),
            ManiaObject.Ln(3, 1500, 2500),
            ManiaObject.Tap(4, 3000));
        var options = new AddNotesOptions { Chance = .73, ContextualDensityNormalizationEnabled = false };

        var untouchedRandom = new TranscriptRandom(314159);
        var untouched = new AddNotesEngine().Apply(chart, options, untouchedRandom);

        var shadow = MapperEvidenceProfileBuilder.Build(chart);
        Assert.Equal(chart.OriginalObjects.Count, shadow.ObservationCount);
        var observedRandom = new TranscriptRandom(314159);
        var observed = new AddNotesEngine().Apply(chart, options, observedRandom, shadow);

        Assert.Equal(OsuBeatmap.Write(untouched.ModifiedChart, options.Chance),
            OsuBeatmap.Write(observed.ModifiedChart, options.Chance));
        Assert.Equal(untouchedRandom.Transcript, observedRandom.Transcript);
        Assert.Equal(DecisionMetrics(untouched.Statistics), DecisionMetrics(observed.Statistics));
    }

    [Fact]
    public void PreparedProfileFromAnotherChartIsRejected()
    {
        var chart = Chart(4, ManiaObject.Tap(0, 0));
        var other = Chart(4, ManiaObject.Tap(0, 500));
        var wrongProfile = MapperEvidenceProfileBuilder.Build(other);

        var error = Assert.Throws<ArgumentException>(() => new AddNotesEngine().Apply(chart,
            new AddNotesOptions { Chance = 0 }, new SeededRandom(1), wrongProfile));

        Assert.Contains("does not match", error.Message);
    }

    [Fact]
    public void WitnessSkeletonDeduplicatesObservationIdsByConstructionConvention()
    {
        var ids = new[] { new OriginalObservationId(2), new OriginalObservationId(1),
            new OriginalObservationId(2) }.Distinct().OrderBy(x => x.Value).ToImmutableArray();
        var witness = new TransformationWitness("W1", EvidenceClaimLevel.ObservedRelation, ids,
            new[] { "duration", "release" }.ToImmutableArray());

        Assert.Equal(2, witness.ObservationIds.Length);
        Assert.Equal([new OriginalObservationId(1), new OriginalObservationId(2)], witness.ObservationIds.ToArray());
        Assert.Equal(2, witness.EvidenceTags.Length);
    }

    private static IReadOnlyDictionary<string, string> DecisionMetrics(AddNotesStatistics statistics)
    {
        var excluded = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(AddNotesStatistics.ContextBuildMs), nameof(AddNotesStatistics.CandidateBuildMs),
            nameof(AddNotesStatistics.GeometryMs), nameof(AddNotesStatistics.WriterMs),
            nameof(AddNotesStatistics.Pass1Ms), nameof(AddNotesStatistics.ArticulationPassMs),
            nameof(AddNotesStatistics.ProfileBuildMs), nameof(AddNotesStatistics.ProfileEstimatedSizeBytes),
            nameof(AddNotesStatistics.ProfileObservationCount), nameof(AddNotesStatistics.ProfileRelationCount)
        };
        return typeof(AddNotesStatistics).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => !excluded.Contains(x.Name))
            .ToDictionary(x => x.Name, x => Format(x.GetValue(statistics)), StringComparer.Ordinal);

        static string Format(object? value)
        {
            if (value is IDictionary dictionary)
                return string.Join(";", dictionary.Keys.Cast<object>().OrderBy(x => x)
                    .Select(key => $"{key}={dictionary[key]}"));
            return value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture) ?? ""
                : value?.ToString() ?? "null";
        }
    }

    private static ManiaChart Chart(int keys, params ManiaObject[] objects) =>
        ChartWithTiming(keys, [new TimingPoint(0, 500)], objects);

    private static ManiaChart ChartWithTiming(int keys, IReadOnlyList<TimingPoint> timings,
        params ManiaObject[] objects)
    {
        var originals = objects.Select((item, index) => item with
        {
            Sequence = index,
            RawLine = Raw(item, keys)
        }).ToArray();
        return new ManiaChart
        {
            KeyCount = keys,
            Lines = Lines(keys),
            OriginalObjects = originals,
            TimingPoints = timings
        };
    }

    private static ManiaChart ChartFromStorage(int keys, List<ManiaObject> objects) => new()
    {
        KeyCount = keys,
        Lines = Lines(keys),
        OriginalObjects = objects,
        TimingPoints = [new TimingPoint(0, 500)]
    };

    private static IReadOnlyList<string> Lines(int keys) =>
        ["osu file format v14", "", "[General]", "Mode:3", "", "[Metadata]", "Version:Test",
            "BeatmapID:12", "", "[Difficulty]", $"CircleSize:{keys}", "", "[TimingPoints]",
            "0,500,4,2,0,100,1,0", "", "[HitObjects]"];

    private static string Raw(ManiaObject item, int keys)
    {
        var x = (int)Math.Floor((item.Lane + .5) * 512 / keys);
        return item.Type == ManiaObjectType.Tap
            ? $"{x},192,{item.StartTime},1,0,0:0:0:0:"
            : $"{x},192,{item.StartTime},128,0,{item.EndTime}:0:0:0:0:";
    }

    private sealed class TranscriptRandom : IDerivableRandomSource
    {
        private readonly IRandomSource _inner;
        private readonly List<string> _transcript;
        private readonly string _prefix;

        public TranscriptRandom(int seed) : this(new SeededRandom(seed), [], "root") { }

        private TranscriptRandom(IRandomSource inner, List<string> transcript, string prefix)
        {
            _inner = inner;
            _transcript = transcript;
            _prefix = prefix;
        }

        public IReadOnlyList<string> Transcript => _transcript;

        public double NextDouble()
        {
            var value = _inner.NextDouble();
            _transcript.Add($"{_prefix}:D:{value:R}");
            return value;
        }

        public int Next(int maxExclusive)
        {
            var value = _inner.Next(maxExclusive);
            _transcript.Add($"{_prefix}:I:{maxExclusive}:{value}");
            return value;
        }

        public IRandomSource Derive(int salt)
        {
            _transcript.Add($"{_prefix}:DERIVE:{salt}");
            var derived = _inner is IDerivableRandomSource source
                ? source.Derive(salt)
                : throw new InvalidOperationException("Test source must be derivable.");
            return new TranscriptRandom(derived, _transcript, $"derived-{salt}");
        }
    }
}
