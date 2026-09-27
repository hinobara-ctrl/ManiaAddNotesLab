using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class SafetyOp466GeometryObservationTests
{
    [Fact]
    public void Passive_query_detects_legal_and_illegal_lane_without_mutation_or_rng()
    {
        var original = new TimedManiaObject(ManiaObject.Ln(2, 100, 300), 1m, 3m);
        var geometry = new LaneGeometryIndex(4, [original]);
        var rng = new PositionRandom();
        var illegal = SafetyOp466GeometryObservationResearch.Query(geometry, 2, 2m, rng, 0);
        var legal = SafetyOp466GeometryObservationResearch.Query(geometry, 1, 2m, rng, 0);
        Assert.False(illegal.IsLegal); Assert.True(legal.IsLegal);
        Assert.True(illegal.NonMutating); Assert.True(legal.NonMutating); Assert.Equal(0, rng.CallCount);
    }

    [Fact]
    public void Snapshot_rejects_incomplete_population_and_bad_fingerprint()
    {
        Assert.Throws<InvalidDataException>(() => SafetyOp466GeometryObservationResearch.ValidateCompleteSnapshot(
            [], 1, 0, "A", "A", "B", "B"));
        var tap = new GeometryObservationObject(0, "original", null, null, 0, 100, null,
            ManiaObjectType.Tap, 0, AddedObjectOrigin.None, 1m, 1m, 1m, 1m);
        Assert.Throws<InvalidDataException>(() => SafetyOp466GeometryObservationResearch.ValidateCompleteSnapshot(
            [tap], 1, 0, "A", "X", "B", "B"));
    }

    [Fact]
    public void Snapshot_rejects_missing_ln_end_semantics_and_accepts_complete_values()
    {
        var bad = new GeometryObservationObject(0, "original", null, null, 0, 100, null,
            ManiaObjectType.LongNote, 0, AddedObjectOrigin.None, 1m, 1m, 1m, 1m);
        Assert.Throws<InvalidDataException>(() => SafetyOp466GeometryObservationResearch.ValidateCompleteSnapshot(
            [bad], 1, 0, "A", "A", "B", "B"));
        var good = bad with { EndTime = 300, LatentEndBeat = 3m, CanonicalEndBeat = 3m };
        SafetyOp466GeometryObservationResearch.ValidateCompleteSnapshot([good], 1, 0, "A", "A", "B", "B");
    }

    private sealed class PositionRandom : IRandomPositionSource
    {
        public long CallCount { get; private set; }
        public double NextDouble() { CallCount++; return .25; }
        public int Next(int maxExclusive) { CallCount++; return 0; }
    }
}
