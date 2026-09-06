using System.Collections.Immutable;

namespace ManiaAddNotesLab.Core;

public sealed record HeldOutLnTargetResult(
    OriginalObservationId TargetObservationId,
    int StartTime,
    int EndTime,
    bool HasCandidateVocabulary,
    bool TrueTargetCandidatePresent,
    int? LegacyRank,
    int? DeduplicatedRank,
    bool DuplicateWitnessAffected,
    bool RankingChanged,
    bool TopCandidateChanged,
    bool HasStructuralTwinDonor,
    int CandidateCount,
    int WeightChangedCandidateCount);

public sealed record HeldOutLnReconstructionResult(
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    int OriginalLnCount,
    int TargetsEvaluated,
    int TargetsWithCandidateVocabulary,
    int TrueTargetCandidatePresent,
    int LegacyTrueTargetRankOne,
    int DeduplicatedTrueTargetRankOne,
    double? LegacyMeanRank,
    double? DeduplicatedMeanRank,
    double? LegacyMedianRank,
    double? DeduplicatedMedianRank,
    int DuplicateWitnessAffectedTargets,
    int NoDuplicateTargets,
    int RankingsChanged,
    int TopCandidatesChanged,
    int TargetsWithStructuralTwinDonor,
    int CandidatesEvaluated,
    int CandidatesWithDuplicateWitness,
    int CandidatesWeightChanged,
    int EvidencePathCount,
    int IndependentWitnessCount,
    int DuplicateEvidencePaths,
    ImmutableArray<HeldOutLnTargetResult> Targets);

/// <summary>
/// Research-only leave-one-observation-out reconstruction. The target cannot donate evidence and source affinity is
/// deliberately disabled because it reads the held-out target's own duration/release.
/// </summary>
public static class LnWitnessDeduplicationResearch
{
    public const string DeduplicatedRule = "DistinctObservationDistanceSumThenCandidateAffinity";

    public static HeldOutLnReconstructionResult Evaluate(ManiaChart chart, double windowBeats = 4)
    {
        ArgumentNullException.ThrowIfNull(chart);
        if (windowBeats <= 0) throw new ArgumentOutOfRangeException(nameof(windowBeats));
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var timeline = new BeatTimeline(chart.TimingPoints);
        var engine = new AddNotesEngine();
        var longNotes = profile.Observations.Where(x => x.Type == ManiaObjectType.LongNote).ToArray();
        var results = ImmutableArray.CreateBuilder<HeldOutLnTargetResult>(longNotes.Length);
        var candidatesEvaluated = 0;
        var duplicateCandidates = 0;
        var weightChangedCandidates = 0;
        var evidencePaths = 0;
        var independentWitnesses = 0;
        var duplicatePaths = 0;

        foreach (var target in longNotes)
        {
            var targetEndTime = target.EndTime
                ?? throw new InvalidOperationException("A long-note observation must have an end time.");
            var donors = longNotes.Where(x => x.Id != target.Id
                    && decimal.Abs(x.StartBeat - target.StartBeat) <= (decimal)windowBeats)
                .Select(x => new LnObservation(chart.OriginalObjects[x.Id.Value], x.StartBeat, x.EndBeat))
                .ToArray();
            if (donors.Length == 0)
            {
                results.Add(new HeldOutLnTargetResult(target.Id, target.StartTime, targetEndTime,
                    false, false, null, null, false, false, false, false, 0, 0));
                continue;
            }

            var context = new LocalLnContext(target.StartBeat, donors,
                donors.Min(x => x.ReleaseBeat - x.HeadBeat));
            var candidates = engine.BuildReleaseCandidates(chart.OriginalObjects[target.Id.Value], context,
                timeline, windowBeats, applySourceAffinity: false).ToArray();
            candidatesEvaluated += candidates.Length;
            duplicateCandidates += candidates.Count(x => x.Evidence.DuplicatePathCount > 0);
            weightChangedCandidates += candidates.Count(x => x.Evidence.LegacyWeight !=
                x.Evidence.DeduplicatedObservationWeight);
            evidencePaths += candidates.Sum(x => x.Evidence.EvidencePathCount);
            independentWitnesses += candidates.Sum(x => x.Evidence.IndependentWitnessCount);
            duplicatePaths += candidates.Sum(x => x.Evidence.DuplicatePathCount);

            foreach (var candidate in candidates.Where(x => x.Evidence.DuplicatePathCount == 0))
                if (candidate.Evidence.LegacyWeight != candidate.Evidence.DeduplicatedObservationWeight)
                    throw new InvalidOperationException("A no-duplicate candidate changed weight in C1 shadow.");

            var legacy = candidates.OrderByDescending(x => x.Evidence.LegacyWeight)
                .ThenBy(x => x.EndTime).ToArray();
            var deduplicated = candidates.OrderByDescending(x => x.Evidence.DeduplicatedObservationWeight)
                .ThenBy(x => x.EndTime).ToArray();
            var legacyRank = Rank(legacy, targetEndTime);
            var deduplicatedRank = Rank(deduplicated, targetEndTime);
            var rankingChanged = !legacy.Select(x => x.EndTime).SequenceEqual(deduplicated.Select(x => x.EndTime));
            var topChanged = legacy.Length > 0 && deduplicated.Length > 0
                && legacy[0].EndTime != deduplicated[0].EndTime;
            var structuralTwin = donors.Any(x => x.Object.StartTime == target.StartTime
                && x.Object.EndTime == target.EndTime);
            results.Add(new HeldOutLnTargetResult(target.Id, target.StartTime, targetEndTime,
                candidates.Length > 0, legacyRank is not null, legacyRank, deduplicatedRank,
                candidates.Any(x => x.Evidence.DuplicatePathCount > 0), rankingChanged, topChanged,
                structuralTwin, candidates.Length,
                candidates.Count(x => x.Evidence.LegacyWeight != x.Evidence.DeduplicatedObservationWeight)));
        }

        var final = results.ToImmutable();
        var present = final.Where(x => x.TrueTargetCandidatePresent).ToArray();
        return new HeldOutLnReconstructionResult(profile.ChartFingerprint, chart.KeyCount,
            chart.OriginalObjects.Count, longNotes.Length, final.Length,
            final.Count(x => x.HasCandidateVocabulary), present.Length,
            present.Count(x => x.LegacyRank == 1), present.Count(x => x.DeduplicatedRank == 1),
            Mean(present.Select(x => x.LegacyRank)), Mean(present.Select(x => x.DeduplicatedRank)),
            Median(present.Select(x => x.LegacyRank)), Median(present.Select(x => x.DeduplicatedRank)),
            final.Count(x => x.DuplicateWitnessAffected), final.Count(x => x.HasCandidateVocabulary
                && !x.DuplicateWitnessAffected), final.Count(x => x.RankingChanged),
            final.Count(x => x.TopCandidateChanged), final.Count(x => x.HasStructuralTwinDonor),
            candidatesEvaluated, duplicateCandidates, weightChangedCandidates, evidencePaths,
            independentWitnesses, duplicatePaths, final);
    }

    private static int? Rank(IReadOnlyList<ReleaseCandidate> candidates, int trueEndTime)
    {
        for (var i = 0; i < candidates.Count; i++)
            if (candidates[i].EndTime == trueEndTime) return i + 1;
        return null;
    }

    private static double? Mean(IEnumerable<int?> source)
    {
        var values = source.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return values.Length == 0 ? null : values.Average();
    }

    private static double? Median(IEnumerable<int?> source)
    {
        var values = source.Where(x => x.HasValue).Select(x => x!.Value).Order().ToArray();
        if (values.Length == 0) return null;
        var middle = values.Length / 2;
        return values.Length % 2 == 1 ? values[middle] : (values[middle - 1] + values[middle]) / 2d;
    }
}
