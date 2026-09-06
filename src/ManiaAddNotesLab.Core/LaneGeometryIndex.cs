using System.Collections.Immutable;

namespace ManiaAddNotesLab.Core;

public sealed class LaneGeometryIndex
{
    private readonly List<TimedManiaObject>[] _lanes;

    public LaneGeometryIndex(int keyCount, IReadOnlyList<TimedManiaObject> originals)
    {
        _lanes = Enumerable.Range(0, keyCount).Select(_ => new List<TimedManiaObject>()).ToArray();
        foreach (var item in originals) _lanes[item.Object.Lane].Add(item);
        foreach (var lane in _lanes) lane.Sort(Compare);
    }

    public int KeyCount => _lanes.Length;

    public int CountOccupiedColumns(decimal beat, AddNotesStatistics statistics)
    {
        var count = 0;
        for (var lane = 0; lane < _lanes.Length; lane++)
            if (!CanPlaceTap(lane, beat, statistics)) count++;
        return count;
    }

    public int CountSimultaneousHeadColumns(decimal beat)
    {
        var count = 0;
        foreach (var lane in _lanes)
        {
            var index = LowerBound(lane, beat);
            if (index < lane.Count && lane[index].StartBeat == beat) count++;
        }
        return count;
    }

    public int CountHeldLnColumns(decimal beat)
    {
        var count = 0;
        foreach (var lane in _lanes)
        {
            var index = LowerBound(lane, beat);
            var heldBefore = index > 0 && lane[index - 1].Object.Type == ManiaObjectType.LongNote
                && lane[index - 1].EndBeat >= beat;
            var heldAtHead = index < lane.Count && lane[index].StartBeat == beat
                && lane[index].Object.Type == ManiaObjectType.LongNote;
            if (heldBefore || heldAtHead) count++;
        }
        return count;
    }

    public List<int> FindLegalTapLanes(decimal beat, AddNotesStatistics statistics)
    {
        var result = new List<int>(_lanes.Length);
        for (var lane = 0; lane < _lanes.Length; lane++)
            if (CanPlaceTap(lane, beat, statistics)) result.Add(lane);
        return result;
    }

    public HardValidityResult ExplainTapPlacement(decimal beat, MapperEvidenceProfile profile) =>
        ExplainTapPlacement(beat, new DiagnosticProvenanceIndex(profile));

    internal HardValidityResult ExplainTapPlacement(decimal beat, DiagnosticProvenanceIndex provenance)
    {
        var lanes = new List<LaneValidityEvaluation>(_lanes.Length);
        for (var laneIndex = 0; laneIndex < _lanes.Length; laneIndex++)
        {
            var failures = new List<PlacementFailure>();
            var lane = _lanes[laneIndex];
            var index = LowerBound(lane, beat);
            if (index > 0 && lane[index - 1].EndBeat >= beat)
                failures.Add(FailureForBlocker(lane[index - 1], false, false, beat, provenance));
            if (index < lane.Count && lane[index].StartBeat == beat)
                failures.Add(FailureForBlocker(lane[index], false, false, beat, provenance));
            lanes.Add(new LaneValidityEvaluation(laneIndex, failures.Count == 0, failures.ToImmutableArray()));
        }
        var valid = lanes.Any(x => x.IsValid);
        return new HardValidityResult(valid,
            valid ? [] : [HardValidityFailureCause.NoLegalLane], lanes.ToImmutableArray());
    }

    public List<int> FindLegalLnLanes(decimal startBeat, decimal endBeat, decimal gapBeat,
        AddNotesStatistics statistics, out bool rejectedByOverlap, out bool rejectedByGap)
    {
        var result = new List<int>(_lanes.Length);
        rejectedByOverlap = false;
        rejectedByGap = false;
        for (var lane = 0; lane < _lanes.Length; lane++)
        {
            statistics.LnGeometryChecks++;
            var rejection = GetLnRejection(lane, startBeat, endBeat, gapBeat, statistics);
            if (rejection == LnRejection.None) result.Add(lane);
            else if (rejection == LnRejection.Overlap) rejectedByOverlap = true;
            else rejectedByGap = true;
        }
        return result;
    }

    /// <summary>Diagnostic-only explanation. It reads current geometry and never changes legacy acceptance.</summary>
    public HardValidityResult ExplainLnPlacement(decimal startBeat, decimal endBeat, decimal gapBeat,
        int startTime, int endTime, MapperEvidenceProfile profile) => ExplainLnPlacement(startBeat, endBeat,
            gapBeat, startTime, endTime, new DiagnosticProvenanceIndex(profile));

    internal HardValidityResult ExplainLnPlacement(decimal startBeat, decimal endBeat, decimal gapBeat,
        int startTime, int endTime, DiagnosticProvenanceIndex provenance)
    {
        var global = new List<HardValidityFailureCause>();
        if (endBeat <= startBeat) global.Add(HardValidityFailureCause.InvalidDuration);
        if (endTime <= startTime) global.Add(HardValidityFailureCause.InvalidAfterMillisecondMaterialization);
        var lanes = new List<LaneValidityEvaluation>(_lanes.Length);
        for (var laneIndex = 0; laneIndex < _lanes.Length; laneIndex++)
            lanes.Add(ExplainLnLane(laneIndex, startBeat, endBeat, gapBeat, provenance));
        var valid = global.Count == 0 && lanes.Any(x => x.IsValid);
        if (!valid && lanes.All(x => !x.IsValid)) global.Add(HardValidityFailureCause.NoLegalLane);
        return new HardValidityResult(valid, global.Distinct().ToImmutableArray(), lanes.ToImmutableArray());
    }

    public LaneValidityEvaluation ExplainLnLane(int laneIndex, decimal startBeat, decimal endBeat,
        decimal gapBeat, MapperEvidenceProfile profile) => ExplainLnLane(laneIndex, startBeat, endBeat, gapBeat,
            new DiagnosticProvenanceIndex(profile));

    internal LaneValidityEvaluation ExplainLnLane(int laneIndex, decimal startBeat, decimal endBeat,
        decimal gapBeat, DiagnosticProvenanceIndex provenance)
    {
        if (laneIndex < 0 || laneIndex >= _lanes.Length)
            return new LaneValidityEvaluation(laneIndex, false,
                [new PlacementFailure(HardValidityFailureCause.OutsideLane, [])]);
        var failures = new List<PlacementFailure>();
        var lane = _lanes[laneIndex];
        var index = LowerBound(lane, startBeat);
        if (index > 0)
        {
            var previous = lane[index - 1];
            if (previous.EndBeat > startBeat)
                failures.Add(FailureForBlocker(previous, false, false, startBeat, provenance));
            else if (previous.EndBeat + gapBeat > startBeat)
                failures.Add(FailureForBlocker(previous, true, true, startBeat, provenance));
        }
        if (index < lane.Count)
        {
            var next = lane[index];
            if (endBeat > next.StartBeat)
                failures.Add(FailureForBlocker(next, false, false, startBeat, provenance));
            else if (endBeat + gapBeat > next.StartBeat)
                failures.Add(FailureForBlocker(next, true, false, startBeat, provenance));
        }
        return new LaneValidityEvaluation(laneIndex, failures.Count == 0, failures.ToImmutableArray());
    }

    public void Insert(TimedManiaObject item)
    {
        var lane = _lanes[item.Object.Lane];
        lane.Insert(LowerBound(lane, item.StartBeat), item);
    }

    public bool CanReplaceWithArticulation(TimedManiaObject parent, decimal releaseBeat, decimal repressBeat,
        AddNotesStatistics statistics)
    {
        if (!(parent.StartBeat < releaseBeat && releaseBeat < repressBeat && repressBeat < parent.EndBeat))
            return false;
        return SegmentClear(parent.Object.Lane, parent.StartBeat, releaseBeat, parent, statistics)
            && SegmentClear(parent.Object.Lane, repressBeat, parent.EndBeat, parent, statistics);
    }

    private bool SegmentClear(int laneIndex, decimal startBeat, decimal endBeat, TimedManiaObject ignored,
        AddNotesStatistics statistics)
    {
        var lane = _lanes[laneIndex];
        var index = LowerBound(lane, startBeat);
        for (var i = Math.Max(0, index - 1); i < Math.Min(lane.Count, index + 3); i++)
        {
            statistics.GeometryChecks++;
            statistics.GeometryObjectsExamined++;
            var item = lane[i];
            if (ReferenceEquals(item, ignored) || item.Object == ignored.Object) continue;
            if (item.StartBeat <= endBeat && item.EndBeat >= startBeat) return false;
        }
        return true;
    }

    private bool CanPlaceTap(int laneIndex, decimal beat, AddNotesStatistics statistics)
    {
        statistics.GeometryChecks++;
        var lane = _lanes[laneIndex];
        var index = LowerBound(lane, beat);
        if (index > 0)
        {
            statistics.GeometryObjectsExamined++;
            if (lane[index - 1].EndBeat >= beat) return false;
        }
        if (index < lane.Count)
        {
            statistics.GeometryObjectsExamined++;
            if (lane[index].StartBeat == beat) return false;
        }
        return true;
    }

    private static PlacementFailure FailureForBlocker(TimedManiaObject item, bool spacing, bool before,
        decimal placementStartBeat, DiagnosticProvenanceIndex provenance)
    {
        var sourceKind = item.Object.Origin == AddedObjectOrigin.ArticulationReplacement
            ? BlockingSourceKind.ArticulationReplacement
            : item.Object.IsSynthetic ? BlockingSourceKind.AddedObject : BlockingSourceKind.OriginalObservation;
        var observation = sourceKind == BlockingSourceKind.OriginalObservation
            ? provenance.Find(item.Object)?.Id
            : null;
        var syntheticId = sourceKind == BlockingSourceKind.OriginalObservation ? null
            : $"S-{item.Object.Origin}-{item.Object.Sequence}-{item.Object.Lane}-{item.Object.StartTime}-{item.Object.EndTime?.ToString() ?? "TAP"}";
        var blocker = new BlockingSource(sourceKind, observation, syntheticId, item.Object.Lane,
            item.Object.Type, item.Object.StartTime, item.Object.EndTime);
        HardValidityFailureCause cause;
        if (spacing) cause = before ? HardValidityFailureCause.BlockedBySpacingBefore
            : HardValidityFailureCause.BlockedBySpacingAfter;
        else if (sourceKind == BlockingSourceKind.ArticulationReplacement)
            cause = HardValidityFailureCause.BlockedByArticulationReplacement;
        else if (item.Object.IsSynthetic)
            cause = item.Object.Type == ManiaObjectType.Tap
                ? HardValidityFailureCause.BlockedBySyntheticTap : HardValidityFailureCause.BlockedBySyntheticLn;
        else if (item.Object.Type == ManiaObjectType.Tap)
            cause = HardValidityFailureCause.BlockedByOriginalTap;
        else if (item.StartBeat < placementStartBeat)
            cause = HardValidityFailureCause.BlockedByOriginalHold;
        else cause = HardValidityFailureCause.BlockedByOriginalHead;
        return new PlacementFailure(cause, [blocker]);
    }

    private LnRejection GetLnRejection(int laneIndex, decimal startBeat, decimal endBeat, decimal gapBeat,
        AddNotesStatistics statistics)
    {
        statistics.GeometryChecks++;
        var lane = _lanes[laneIndex];
        var index = LowerBound(lane, startBeat);
        if (index > 0)
        {
            statistics.GeometryObjectsExamined++;
            if (lane[index - 1].EndBeat > startBeat) return LnRejection.Overlap;
            if (lane[index - 1].EndBeat + gapBeat > startBeat) return LnRejection.Gap;
        }
        if (index < lane.Count)
        {
            statistics.GeometryObjectsExamined++;
            if (endBeat > lane[index].StartBeat) return LnRejection.Overlap;
            if (endBeat + gapBeat > lane[index].StartBeat) return LnRejection.Gap;
        }
        return LnRejection.None;
    }

    private static int LowerBound(List<TimedManiaObject> lane, decimal startBeat)
    {
        var lo = 0;
        var hi = lane.Count;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (lane[mid].StartBeat < startBeat) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    private static int Compare(TimedManiaObject left, TimedManiaObject right)
    {
        var start = left.StartBeat.CompareTo(right.StartBeat);
        return start != 0 ? start : left.EndBeat.CompareTo(right.EndBeat);
    }

    private enum LnRejection { None, Overlap, Gap }
}
