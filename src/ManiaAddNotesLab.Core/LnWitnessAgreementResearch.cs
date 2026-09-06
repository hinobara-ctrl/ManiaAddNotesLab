using System.Collections.Immutable;
using System.Diagnostics;

namespace ManiaAddNotesLab.Core;

public enum HeldOutExclusionMode { LeaveOneObjectOut, LeaveOneHeadGroupOut }
public enum WitnessAgreementKind { SameHeadAgreement, MaterializationCoincidence, OtherConvergence }
public enum TargetAgreementClass
{
    NoDuplicateWitness,
    SameHeadAgreement,
    MaterializationCoincidence,
    OtherConvergence,
    MultipleCauses
}
public enum PairedWeightOutcome { Improved, Worsened, Tie, NotEvaluable }

public sealed record WitnessAgreement(
    OriginalObservationId ObservationId,
    int CandidateEndTime,
    ImmutableArray<LnEvidenceLabel> Labels,
    WitnessAgreementKind AgreementKind,
    int SourceHeadTime,
    int DonorHeadTime,
    decimal SourceHeadBeat,
    decimal DonorHeadBeat,
    decimal DurationEndBeat,
    decimal ExactReleaseBeat,
    bool BeatSpaceExactEquality,
    bool DecimalEquality,
    bool CandidateEndpointEquality,
    bool MillisecondEquality,
    bool EqualityOnlyThroughTolerance,
    int DurationRawEndTime,
    int ReleaseRawEndTime,
    int MaterializedEndTime,
    int SnapAdjustmentMilliseconds);

public sealed record C11TargetValidation(
    OriginalObservationId TargetObservationId,
    int StartTime,
    int EndTime,
    bool HasCandidateVocabulary,
    bool TrueTargetCandidatePresent,
    int? LegacyShapeRank,
    int? UniqueWitnessShapeRank,
    bool HasLegalCandidateVocabulary,
    bool TrueTargetLegalCandidatePresent,
    int? LegacyGeometryRank,
    int? UniqueWitnessGeometryRank,
    double? LegacyTargetWeight,
    double? UniqueWitnessTargetWeight,
    double LegacyTotalLegalCandidateWeight,
    double UniqueWitnessTotalLegalCandidateWeight,
    double? LegacyTargetNormalizedWeight,
    double? UniqueWitnessTargetNormalizedWeight,
    double? DeltaNormalizedWeight,
    double? LogProbabilityRatio,
    PairedWeightOutcome PairedOutcome,
    bool OriginalEndpointHasAnyLegalLane,
    bool OriginalEndpointIsLegalInTargetLane,
    bool HasExactStructuralTwin,
    TargetAgreementClass TargetAgreementClass,
    int VocabularySameHeadAgreements,
    int VocabularyMaterializationCoincidences,
    int VocabularyOtherConvergences,
    int TargetSameHeadAgreements,
    int TargetMaterializationCoincidences,
    int TargetOtherConvergences,
    int CandidateCount,
    int LegalCandidateCount,
    int CandidatesWithDuplicateWitness,
    int EvidencePathCount,
    int IndependentWitnessCount,
    int DuplicateEvidencePathCount);

public sealed record C11HeldOutValidationResult(
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    int OriginalLnCount,
    HeldOutExclusionMode ExclusionMode,
    int TargetsEvaluated,
    int CandidatesEvaluated,
    int LegalCandidatesEvaluated,
    int CandidatesWithDuplicateWitness,
    int EvidencePathCount,
    int IndependentWitnessCount,
    int DuplicateEvidencePathCount,
    int SameHeadAgreementCount,
    int MaterializationCoincidenceCount,
    int OtherConvergenceCount,
    double ElapsedMilliseconds,
    ImmutableArray<WitnessAgreement> Agreements,
    ImmutableArray<C11TargetValidation> Targets);

/// <summary>
/// C1.1 research only. It separates independent witness identity from descriptive agreement between evidence labels.
/// It does not consume RNG and does not expose a behavioral policy.
/// </summary>
public static class LnWitnessAgreementResearch
{
    public static C11HeldOutValidationResult Evaluate(ManiaChart chart, HeldOutExclusionMode exclusionMode,
        double windowBeats = 4)
    {
        ArgumentNullException.ThrowIfNull(chart);
        if (windowBeats <= 0) throw new ArgumentOutOfRangeException(nameof(windowBeats));
        var started = Stopwatch.GetTimestamp();
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var timeline = new BeatTimeline(chart.TimingPoints);
        var engine = new AddNotesEngine();
        var longNotes = profile.Observations.Where(x => x.Type == ManiaObjectType.LongNote).ToArray();
        var objectIds = new Dictionary<ManiaObject, OriginalObservationId>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < chart.OriginalObjects.Count; i++)
            objectIds[chart.OriginalObjects[i]] = new OriginalObservationId(i);
        var targets = ImmutableArray.CreateBuilder<C11TargetValidation>(longNotes.Length);
        var candidatesEvaluated = 0;
        var legalCandidatesEvaluated = 0;
        var candidatesWithDuplicates = 0;
        var evidencePaths = 0;
        var independentWitnesses = 0;
        var duplicatePaths = 0;
        var sameHeadAgreements = 0;
        var materializationCoincidences = 0;
        var otherConvergences = 0;
        var allAgreements = ImmutableArray.CreateBuilder<WitnessAgreement>();

        foreach (var target in longNotes)
        {
            var targetObject = chart.OriginalObjects[target.Id.Value];
            var targetEndTime = target.EndTime
                ?? throw new InvalidOperationException("A long-note observation must have an end time.");
            var donors = longNotes.Where(x => x.Id != target.Id
                    && decimal.Abs(x.StartBeat - target.StartBeat) <= (decimal)windowBeats
                    && (exclusionMode != HeldOutExclusionMode.LeaveOneHeadGroupOut
                        || x.StartTime != target.StartTime))
                .Select(x => new LnObservation(chart.OriginalObjects[x.Id.Value], x.StartBeat, x.EndBeat))
                .ToArray();
            var hasTwin = longNotes.Any(x => x.Id != target.Id && x.StartTime == target.StartTime
                && x.EndTime == target.EndTime);
            if (donors.Length == 0)
            {
                targets.Add(EmptyTarget(target, targetEndTime, hasTwin));
                continue;
            }

            var context = new LocalLnContext(target.StartBeat, donors,
                donors.Min(x => x.ReleaseBeat - x.HeadBeat));
            var build = engine.BuildReleaseCandidatesForResearch(targetObject, context, timeline, windowBeats,
                applySourceAffinity: false);
            var candidates = build.Candidates.ToArray();
            var agreements = ClassifyAgreements(build, target.StartTime, target.StartBeat,
                value => objectIds[value]);
            allAgreements.AddRange(agreements);
            var same = agreements.Count(x => x.AgreementKind == WitnessAgreementKind.SameHeadAgreement);
            var materialized = agreements.Count(x => x.AgreementKind == WitnessAgreementKind.MaterializationCoincidence);
            var other = agreements.Count(x => x.AgreementKind == WitnessAgreementKind.OtherConvergence);
            sameHeadAgreements += same;
            materializationCoincidences += materialized;
            otherConvergences += other;
            candidatesEvaluated += candidates.Length;
            candidatesWithDuplicates += candidates.Count(x => x.Evidence.DuplicatePathCount > 0);
            evidencePaths += candidates.Sum(x => x.Evidence.EvidencePathCount);
            independentWitnesses += candidates.Sum(x => x.Evidence.IndependentWitnessCount);
            duplicatePaths += candidates.Sum(x => x.Evidence.DuplicatePathCount);

            var legacyShape = Rank(candidates, targetEndTime, uniqueWitness: false);
            var uniqueShape = Rank(candidates, targetEndTime, uniqueWitness: true);
            var evaluationObjects = chart.OriginalObjects.Where((_, index) => index != target.Id.Value).ToArray();
            var evaluationChart = new ManiaChart
            {
                KeyCount = chart.KeyCount,
                Lines = chart.Lines,
                OriginalObjects = evaluationObjects,
                TimingPoints = chart.TimingPoints
            };
            var analysis = new OriginalChartAnalysis(evaluationChart, timeline);
            var geometry = new LaneGeometryIndex(chart.KeyCount, analysis.Objects);
            var gap = new LocalLaneGapAnalyzer(analysis).Resolve(target.StartBeat, new AddNotesOptions());
            var legal = candidates.Select(candidate =>
            {
                var lanes = geometry.FindLegalLnLanes(target.StartBeat, candidate.EndBeat,
                    (decimal)gap.GapBeats, new AddNotesStatistics(), out _, out _).ToImmutableArray();
                return new LegalCandidate(candidate, lanes);
            }).Where(x => x.Lanes.Length > 0).ToArray();
            legalCandidatesEvaluated += legal.Length;

            var targetLegal = legal.FirstOrDefault(x => x.Candidate.EndTime == targetEndTime);
            var legacyTargetWeight = targetLegal?.Candidate.Evidence.LegacyWeight;
            var uniqueTargetWeight = targetLegal?.Candidate.Evidence.DeduplicatedObservationWeight;
            var legacyTotal = legal.Sum(x => x.Candidate.Evidence.LegacyWeight);
            var uniqueTotal = legal.Sum(x => x.Candidate.Evidence.DeduplicatedObservationWeight);
            double? legacyNormalized = legacyTargetWeight is null || legacyTotal == 0
                ? null : legacyTargetWeight.Value / legacyTotal;
            double? uniqueNormalized = uniqueTargetWeight is null || uniqueTotal == 0
                ? null : uniqueTargetWeight.Value / uniqueTotal;
            double? delta = legacyNormalized is null || uniqueNormalized is null
                ? null : uniqueNormalized.Value - legacyNormalized.Value;
            var paired = delta is null ? PairedWeightOutcome.NotEvaluable
                : delta.Value > 0 ? PairedWeightOutcome.Improved
                : delta.Value < 0 ? PairedWeightOutcome.Worsened : PairedWeightOutcome.Tie;
            double? logRatio = legacyNormalized is > 0 && uniqueNormalized is > 0
                ? Math.Log(uniqueNormalized.Value / legacyNormalized.Value) : null;
            var targetAgreements = agreements.Where(x => x.CandidateEndTime == targetEndTime).ToArray();
            var targetKinds = targetAgreements.Select(x => x.AgreementKind).Distinct().ToArray();

            targets.Add(new C11TargetValidation(target.Id, target.StartTime, targetEndTime,
                candidates.Length > 0, candidates.Any(x => x.EndTime == targetEndTime),
                legacyShape, uniqueShape, legal.Length > 0, targetLegal is not null,
                Rank(legal.Select(x => x.Candidate), targetEndTime, false),
                Rank(legal.Select(x => x.Candidate), targetEndTime, true),
                legacyTargetWeight, uniqueTargetWeight, legacyTotal, uniqueTotal,
                legacyNormalized, uniqueNormalized, delta, logRatio, paired,
                targetLegal is not null, targetLegal?.Lanes.Contains(target.Lane) == true, hasTwin,
                ClassifyTarget(targetKinds), same, materialized, other,
                targetAgreements.Count(x => x.AgreementKind == WitnessAgreementKind.SameHeadAgreement),
                targetAgreements.Count(x => x.AgreementKind == WitnessAgreementKind.MaterializationCoincidence),
                targetAgreements.Count(x => x.AgreementKind == WitnessAgreementKind.OtherConvergence),
                candidates.Length, legal.Length, candidates.Count(x => x.Evidence.DuplicatePathCount > 0),
                candidates.Sum(x => x.Evidence.EvidencePathCount),
                candidates.Sum(x => x.Evidence.IndependentWitnessCount),
                candidates.Sum(x => x.Evidence.DuplicatePathCount)));
        }

        return new C11HeldOutValidationResult(profile.ChartFingerprint, chart.KeyCount,
            chart.OriginalObjects.Count, longNotes.Length, exclusionMode, targets.Count,
            candidatesEvaluated, legalCandidatesEvaluated, candidatesWithDuplicates, evidencePaths,
            independentWitnesses, duplicatePaths, sameHeadAgreements, materializationCoincidences,
            otherConvergences, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            allAgreements.ToImmutable(), targets.ToImmutable());
    }

    public static ImmutableArray<WitnessAgreement> ClassifyAgreements(ReleaseCandidateResearchBuild build,
        int sourceHeadTime, decimal sourceHeadBeat,
        Func<ManiaObject, OriginalObservationId>? observationId = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        observationId ??= value => new OriginalObservationId(value.Sequence);
        var result = ImmutableArray.CreateBuilder<WitnessAgreement>();
        foreach (var group in build.Routes.GroupBy(x => new { x.Observation, x.MaterializedEndTime }))
        {
            var duration = group.SingleOrDefault(x => x.Label == LnEvidenceLabel.Duration);
            var release = group.SingleOrDefault(x => x.Label == LnEvidenceLabel.ExactRelease);
            if (duration is null || release is null) continue;
            var donorHeadBeat = duration.ObservationHeadBeat;
            var sameHead = duration.Observation.StartTime == sourceHeadTime && donorHeadBeat == sourceHeadBeat;
            var beatExact = duration.IntendedEndBeat == release.IntendedEndBeat;
            var millisecondExact = duration.RawEndTime == release.RawEndTime;
            var kind = sameHead ? WitnessAgreementKind.SameHeadAgreement
                : !beatExact && (millisecondExact || duration.SnapAdjustmentMilliseconds > 0
                    || release.SnapAdjustmentMilliseconds > 0)
                    ? WitnessAgreementKind.MaterializationCoincidence
                    : WitnessAgreementKind.OtherConvergence;
            result.Add(new WitnessAgreement(observationId(duration.Observation), group.Key.MaterializedEndTime,
                [LnEvidenceLabel.Duration, LnEvidenceLabel.ExactRelease], kind, sourceHeadTime,
                duration.Observation.StartTime, sourceHeadBeat, donorHeadBeat, duration.IntendedEndBeat,
                release.IntendedEndBeat, beatExact, beatExact, true, millisecondExact, false,
                duration.RawEndTime, release.RawEndTime, group.Key.MaterializedEndTime,
                duration.SnapAdjustmentMilliseconds + release.SnapAdjustmentMilliseconds));
        }
        return result.ToImmutable();
    }

    private static C11TargetValidation EmptyTarget(OriginalObservation target, int endTime, bool hasTwin) =>
        new(target.Id, target.StartTime, endTime, false, false, null, null, false, false, null, null,
            null, null, 0, 0, null, null, null, null, PairedWeightOutcome.NotEvaluable,
            false, false, hasTwin, TargetAgreementClass.NoDuplicateWitness,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static int? Rank(IEnumerable<ReleaseCandidate> candidates, int trueEndTime, bool uniqueWitness)
    {
        var ordered = candidates.OrderByDescending(x => uniqueWitness
                ? x.Evidence.DeduplicatedObservationWeight : x.Evidence.LegacyWeight)
            .ThenBy(x => x.EndTime).ToArray();
        for (var i = 0; i < ordered.Length; i++)
            if (ordered[i].EndTime == trueEndTime) return i + 1;
        return null;
    }

    private static TargetAgreementClass ClassifyTarget(IReadOnlyCollection<WitnessAgreementKind> kinds) =>
        kinds.Count == 0 ? TargetAgreementClass.NoDuplicateWitness
        : kinds.Count > 1 ? TargetAgreementClass.MultipleCauses
        : kinds.Single() switch
        {
            WitnessAgreementKind.SameHeadAgreement => TargetAgreementClass.SameHeadAgreement,
            WitnessAgreementKind.MaterializationCoincidence => TargetAgreementClass.MaterializationCoincidence,
            _ => TargetAgreementClass.OtherConvergence
        };

    private sealed record LegalCandidate(ReleaseCandidate Candidate, ImmutableArray<int> Lanes);
}
