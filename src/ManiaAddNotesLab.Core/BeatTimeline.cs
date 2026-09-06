namespace ManiaAddNotesLab.Core;

public sealed class BeatTimeline
{
    private static readonly int[] SupportedSnapDivisors = [1, 2, 3, 4, 6, 8, 12, 16];
    private readonly Segment[] _segments;
    public long ConversionCount { get; private set; }

    public BeatTimeline(IEnumerable<TimingPoint> timingPoints)
    {
        var points = timingPoints.Where(p => p.BeatLength > 0).OrderBy(p => p.Time).ToArray();
        if (points.Length == 0) throw new InvalidDataException("The beatmap has no uninherited timing point.");

        _segments = new Segment[points.Length];
        var accumulatedBeat = 0m;
        for (var i = 0; i < points.Length; i++)
        {
            if (i > 0)
                accumulatedBeat += (points[i].Time - points[i - 1].Time) / points[i - 1].BeatLength;
            _segments[i] = new Segment(points[i].Time, points[i].BeatLength, accumulatedBeat);
        }
    }

    public decimal ToBeatDecimal(int time)
    {
        ConversionCount++;
        var segment = _segments.LastOrDefault(s => s.Time <= time) ?? _segments[0];
        return segment.StartBeat + (time - segment.Time) / segment.BeatLength;
    }

    public int ToTimeMilliseconds(decimal beat)
    {
        ConversionCount++;
        var segment = _segments.LastOrDefault(s => s.StartBeat <= beat) ?? _segments[0];
        var time = segment.Time + (beat - segment.StartBeat) * segment.BeatLength;
        return decimal.ToInt32(decimal.Round(time, 0, MidpointRounding.AwayFromZero));
    }

    public double ToBeat(double time) => (double)ToBeatDecimal((int)Math.Round(time, MidpointRounding.AwayFromZero));
    public double ToTime(double beat) => ToTimeMilliseconds((decimal)beat);

    public int SnapToSupportedDivision(double time)
    {
        ConversionCount++;
        var decimalTime = (decimal)time;
        var segment = _segments.LastOrDefault(s => s.Time <= decimalTime) ?? _segments[0];
        var localBeat = (decimalTime - segment.Time) / segment.BeatLength;
        var bestTime = decimalTime;
        var bestDistance = decimal.MaxValue;
        foreach (var divisor in SupportedSnapDivisors)
        {
            var snappedBeat = decimal.Round(localBeat * divisor, 0, MidpointRounding.AwayFromZero) / divisor;
            var candidate = segment.Time + snappedBeat * segment.BeatLength;
            var distance = decimal.Abs(candidate - decimalTime);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTime = candidate;
            }
        }
        return decimal.ToInt32(decimal.Round(bestTime, 0, MidpointRounding.AwayFromZero));
    }

    public bool IsOnSupportedDivision(double time, double toleranceMs = 1.1) =>
        Math.Abs(SnapToSupportedDivision(time) - time) <= toleranceMs;

    private sealed record Segment(decimal Time, decimal BeatLength, decimal StartBeat);
}
