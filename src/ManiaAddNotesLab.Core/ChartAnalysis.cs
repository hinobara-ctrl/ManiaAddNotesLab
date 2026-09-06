namespace ManiaAddNotesLab.Core;

public sealed class OriginalChartAnalysis
{
    public OriginalChartAnalysis(ManiaChart chart, BeatTimeline timeline)
    {
        var timed = new TimedManiaObject[chart.OriginalObjects.Count];
        for (var i = 0; i < timed.Length; i++)
        {
            var obj = chart.OriginalObjects[i];
            var start = timeline.ToBeatDecimal(obj.StartTime);
            var end = obj.Type == ManiaObjectType.LongNote ? timeline.ToBeatDecimal(obj.EndTime!.Value) : start;
            timed[i] = new TimedManiaObject(obj, start, end);
        }
        Objects = timed;
        Heads = timed.OrderBy(o => o.StartBeat).ThenBy(o => o.Object.Sequence).ToArray();
        LongNotes = timed.Where(o => o.Object.Type == ManiaObjectType.LongNote)
            .OrderBy(o => o.StartBeat).ThenBy(o => o.Object.Sequence).ToArray();
        ReleaseAnchors = LongNotes.Select(o => new OriginalAnchor(o.Object.EndTime!.Value, o.EndBeat))
            .OrderBy(o => o.Beat).ThenBy(o => o.Time).ToArray();
        ByLane = Enumerable.Range(0, chart.KeyCount)
            .Select(lane => (IReadOnlyList<TimedManiaObject>)timed.Where(o => o.Object.Lane == lane)
                .OrderBy(o => o.StartBeat).ThenBy(o => o.EndBeat).ToArray()).ToArray();
        HeadTimes = Heads.Select(o => o.Object.StartTime).ToHashSet();
    }

    public IReadOnlyList<TimedManiaObject> Objects { get; }
    public IReadOnlyList<TimedManiaObject> Heads { get; }
    public IReadOnlyList<TimedManiaObject> LongNotes { get; }
    public IReadOnlyList<OriginalAnchor> ReleaseAnchors { get; }
    public IReadOnlyList<TimedManiaObject>[] ByLane { get; }
    public IReadOnlySet<int> HeadTimes { get; }

    public IReadOnlyList<TimedManiaObject> LongNotesNear(decimal beat, decimal radius) =>
        Slice(LongNotes, beat - radius, beat + radius);

    public IReadOnlyList<TimedManiaObject> ObjectsBetween(decimal startExclusive, decimal endExclusive)
    {
        var first = LowerBound(Heads, startExclusive);
        var result = new List<TimedManiaObject>();
        for (var i = first; i < Heads.Count && Heads[i].StartBeat < endExclusive; i++)
            if (Heads[i].StartBeat > startExclusive) result.Add(Heads[i]);
        return result;
    }

    public IReadOnlyList<OriginalAnchor> ReleasesBetween(decimal startExclusive, decimal endExclusive)
    {
        var first = LowerBound(ReleaseAnchors, startExclusive);
        var result = new List<OriginalAnchor>();
        for (var i = first; i < ReleaseAnchors.Count && ReleaseAnchors[i].Beat < endExclusive; i++)
            if (ReleaseAnchors[i].Beat > startExclusive) result.Add(ReleaseAnchors[i]);
        return result;
    }

    private static IReadOnlyList<TimedManiaObject> Slice(IReadOnlyList<TimedManiaObject> source, decimal min, decimal max)
    {
        var first = LowerBound(source, min);
        var result = new List<TimedManiaObject>();
        for (var i = first; i < source.Count && source[i].StartBeat <= max; i++) result.Add(source[i]);
        return result;
    }

    internal static int LowerBound(IReadOnlyList<TimedManiaObject> source, decimal beat)
    {
        var lo = 0;
        var hi = source.Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (source[mid].StartBeat < beat) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    private static int LowerBound(IReadOnlyList<OriginalAnchor> source, decimal beat)
    {
        var lo = 0;
        var hi = source.Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (source[mid].Beat < beat) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}

public sealed record OriginalAnchor(int Time, decimal Beat);

public sealed class HeadDensityAnalyzer(IReadOnlyList<TimedManiaObject> originalHeads, int keyCount)
{
    private readonly decimal[] _heads = originalHeads.Select(o => o.StartBeat).Order().ToArray();

    public DensitySnapshot Analyze(decimal beat, AddNotesOptions options)
    {
        var microWindow = (decimal)options.MicroDensityWindowBeats;
        var micro = DensityAt(beat, microWindow);
        var samples = new List<double>();
        var radius = Math.Max(1, (int)Math.Floor(options.ContextDensityWindowBeats));
        for (var step = 1; step <= radius; step++)
        {
            samples.Add(DensityAt(beat - step, microWindow));
            samples.Add(DensityAt(beat + step, microWindow));
        }
        samples.Sort();
        var context = samples.Count == 0 ? micro : samples[(samples.Count - 1) / 2]; // robust lower median
        var legacy = options.ContextualDensityScaleMode == ContextualDensityScaleMode.AbsoluteHeadCountLegacy;
        var baselineFloor = legacy ? 1.0 : keyCount / 7.0;
        var minimumMicro = legacy ? 2.0 : keyCount * (2.0 / 7.0);
        var ratio = micro / Math.Max(baselineFloor, context);
        var elevated = micro + 1e-9 >= minimumMicro && ratio >= options.BurstDensityRatioThreshold;
        if (!elevated) return new DensitySnapshot(micro, context, ratio, 0, 1);

        var thresholdDensity = Math.Max(baselineFloor, context) * options.BurstDensityRatioThreshold;
        var span = 1;
        var maxExtra = 3; // once four beats are proven, the factor is already 1.
        for (var step = 1; step <= maxExtra; step++)
        {
            if (DensityAt(beat - step, microWindow) + 1e-9 < thresholdDensity) break;
            span++;
        }
        for (var step = 1; step <= maxExtra && span < 4; step++)
        {
            if (DensityAt(beat + step, microWindow) + 1e-9 < thresholdDensity) break;
            span++;
        }
        var factor = span switch
        {
            <= 1 => options.BurstSpanOneBeatFactor,
            2 => options.BurstSpanTwoBeatFactor,
            3 => options.BurstSpanThreeBeatFactor,
            _ => 1.0
        };
        return new DensitySnapshot(micro, context, ratio, span, Math.Clamp(factor, 0.000001, 1));
    }

    private double DensityAt(decimal center, decimal window)
    {
        var half = window / 2;
        var count = LowerBound(center + half) - LowerBound(center - half);
        return count / (double)window;
    }

    private int LowerBound(decimal value)
    {
        var lo = 0;
        var hi = _heads.Length;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (_heads[mid] < value) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}

public sealed record RetriggerGapDecision(IReadOnlyList<decimal> SupportedGaps, bool UsedSameLane,
    int EvidenceCount);

public sealed class LocalRetriggerGapAnalyzer
{
    private readonly OriginalChartAnalysis _analysis;
    private readonly IReadOnlyList<RetriggerTransition>[] _byLane;

    public LocalRetriggerGapAnalyzer(OriginalChartAnalysis analysis)
    {
        _analysis = analysis;
        _byLane = analysis.ByLane.Select(BuildTransitions).ToArray();
    }

    public RetriggerGapDecision? Resolve(int lane, decimal headBeat, AddNotesOptions options)
    {
        var sameLane = Supported(_byLane[lane], headBeat, options);
        if (sameLane.Count > 0)
            return new RetriggerGapDecision(sameLane.Select(x => x.Gap).ToArray(), true, sameLane.Sum(x => x.Count));

        var crossLane = Supported(_byLane.Where((_, index) => index != lane).SelectMany(x => x), headBeat, options);
        return crossLane.Count == 0 ? null
            : new RetriggerGapDecision(crossLane.Select(x => x.Gap).ToArray(), false, crossLane.Sum(x => x.Count));
    }

    private static IReadOnlyList<RetriggerTransition> BuildTransitions(IReadOnlyList<TimedManiaObject> lane)
    {
        var result = new List<RetriggerTransition>();
        for (var i = 1; i < lane.Count; i++)
        {
            var previous = lane[i - 1];
            var next = lane[i];
            if (previous.Object.Type != ManiaObjectType.LongNote) continue;
            var gap = next.StartBeat - previous.EndBeat;
            if (gap > 0 && gap <= 1)
                result.Add(new RetriggerTransition(next.StartBeat, decimal.Round(gap, 6, MidpointRounding.AwayFromZero)));
        }
        return result;
    }

    private static IReadOnlyList<GapSupport> Supported(IEnumerable<RetriggerTransition> source, decimal headBeat,
        AddNotesOptions options)
    {
        var radius = (decimal)options.RetriggerGapWindowBeats;
        return source.Where(x => decimal.Abs(x.HeadBeat - headBeat) <= radius)
            .GroupBy(x => x.Gap).Select(g => new GapSupport(g.Key, g.Count()))
            .Where(x => x.Count >= options.RetriggerGapMinimumSupport)
            .OrderByDescending(x => x.Count).ThenBy(x => x.Gap).ToArray();
    }

    private sealed record RetriggerTransition(decimal HeadBeat, decimal Gap);
    private sealed record GapSupport(decimal Gap, int Count);
}

public sealed class LocalLaneGapAnalyzer(OriginalChartAnalysis analysis)
{
    public LaneGapDecision Resolve(decimal opportunityBeat, AddNotesOptions options)
    {
        if (!options.UseLocalLaneGap)
            return new LaneGapDecision(options.FallbackMinimumLaneGapBeats, false, 0);

        var min = opportunityBeat - (decimal)options.LaneGapWindowBeats;
        var max = opportunityBeat + (decimal)options.LaneGapWindowBeats;
        var gaps = new Dictionary<decimal, int>();
        foreach (var lane in analysis.ByLane)
        {
            var first = LowerBound(lane, min);
            for (var i = Math.Max(1, first); i < lane.Count; i++)
            {
                var previous = lane[i - 1];
                var next = lane[i];
                if (previous.EndBeat > max && next.StartBeat > max) break;
                if (next.StartBeat < min || previous.EndBeat > max) continue;
                var gap = next.StartBeat - previous.EndBeat;
                if (gap <= 0 || gap > 1.0m) continue;
                var key = decimal.Round(gap, 6, MidpointRounding.AwayFromZero);
                gaps[key] = gaps.GetValueOrDefault(key) + 1;
            }
        }
        var supported = gaps.Where(pair => pair.Value >= options.LaneGapMinimumSupport)
            .OrderBy(pair => pair.Key).FirstOrDefault();
        return supported.Value > 0
            ? new LaneGapDecision((double)supported.Key, true, supported.Value)
            : new LaneGapDecision(options.FallbackMinimumLaneGapBeats, false, gaps.Values.Sum());
    }

    private static int LowerBound(IReadOnlyList<TimedManiaObject> lane, decimal beat)
    {
        var lo = 0;
        var hi = lane.Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (lane[mid].StartBeat < beat) lo = mid + 1; else hi = mid;
        }
        return lo;
    }
}
