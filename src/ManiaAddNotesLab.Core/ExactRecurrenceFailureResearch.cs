using System.Collections.Immutable;

namespace ManiaAddNotesLab.Core;

public enum ExactNoContextReason
{
    NotApplicable,
    EdgeIneligible,
    NoExactNeighborPairElsewhere,
    OnlyExcludedOccurrences,
    OtherExactAbsence
}

public enum CounterfactualTransition
{
    PreservedReconstructed,
    PreservedAmbiguous,
    PreservedMismatch,
    PreservedNoContext,
    BecameReconstructed,
    BecameAmbiguous,
    BecameMismatch,
    BecameNoContext
}

public sealed record DonorCenterMultiplicity(string CenterTokenIdentity, int OccurrenceCount);

public sealed record CounterfactualRefinementAudit(
    string RefinementId,
    AdaptiveReconstructionState BaselineDisposition,
    AdaptiveReconstructionState RefinedDisposition,
    CounterfactualTransition Transition,
    int RawDonorOccurrenceCount,
    int DistinctCenterTokenCount,
    bool TargetTokenPreserved);

public sealed record RecurrenceIdentityLinkAudit(
    string ExactBlockIdentity,
    int OccurrenceCount,
    int EmittedConsecutiveRelationCount,
    int AllPairsCount);

public sealed record ExactRecurrenceFailureDiagnostic(
    string ResearchSchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    string TargetEventId,
    int TargetIndex,
    AdaptiveReconstructionState Disposition,
    ExactNoContextReason PrimaryNoContextReason,
    string? PreviousEventId,
    string? NextEventId,
    string TargetTokenIdentity,
    string? PreviousTokenIdentity,
    string? NextTokenIdentity,
    int TargetHeadCount,
    int TargetTapHeadCount,
    int TargetLongNoteHeadCount,
    int TargetReleaseCount,
    int PreviousHeadCount,
    int NextHeadCount,
    int? PreviousSerializedSpacingMilliseconds,
    int? NextSerializedSpacingMilliseconds,
    decimal? PreviousExactFileDerivedBeatSpacing,
    decimal? NextExactFileDerivedBeatSpacing,
    int PreHoldoutCandidateCount,
    int ExcludedCandidateCount,
    int PostHoldoutDonorCount,
    int DistinctCenterTokenCount,
    ImmutableArray<DonorCenterMultiplicity> CenterMultiplicities,
    bool HasOtherPreHoldoutOccurrence,
    bool HasSelfOccurrence,
    bool HasEndpointOrGroupCollision,
    bool HasSerializedSpacingDivergence,
    bool HasExactFileDerivedBeatSpacingDivergence,
    bool HasHeldStateDivergence,
    bool HasTypeCompositionDivergence,
    bool HasHeadCountDivergence,
    bool HasLaneOccupancyDivergence,
    bool HasMixedTypeDivergence,
    bool ReleaseInvolvedExternalContext,
    bool TargetReleaseInvolved,
    bool TargetHeldBefore,
    ImmutableArray<OriginalObservationId> TargetObservationIds,
    ImmutableArray<OriginalObservationId> ExcludedObservationIds,
    ImmutableArray<string> ExcludedCandidateEventIds,
    ImmutableArray<string> DonorEventIds,
    ImmutableArray<OriginalObservationId> DonorObservationIds,
    int LeakageCount,
    ImmutableArray<CounterfactualRefinementAudit> Counterfactuals);

public sealed record ExactRecurrenceFailureResearchResult(
    string ResearchSchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    int SyntheticTeachingCount,
    int CrossChartEvidenceCount,
    ImmutableArray<ExactRecurrenceFailureDiagnostic> Diagnostics,
    ImmutableArray<RecurrenceIdentityLinkAudit> BlockRelationSemantics);

/// <summary>
/// E.1 diagnostic-only projection over the unchanged Phase E exact-neighbor semantics.
/// Observable differences are reported as associations and never alter identity, voting or generation.
/// </summary>
public static class ExactRecurrenceFailureResearch
{
    public const string ResearchSchemaVersion = "phase-e-1-exact-recurrence-failure-shadow.1";

    public static ExactRecurrenceFailureResearchResult Evaluate(ManiaChart chart)
    {
        var series = AdaptiveContextResearch.BuildEventSeries(chart);
        return Evaluate(series);
    }

    public static ExactRecurrenceFailureResearchResult Evaluate(OriginalTemporalEventSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        var candidateIndex = BuildCandidateIndex(series.Events);
        var diagnostics = Enumerable.Range(1, Math.Max(0, series.Events.Length - 2))
            .Select(index => DiagnoseTarget(series, index, candidateIndex)).ToImmutableArray();
        return new(ResearchSchemaVersion, series.ChartFingerprint, series.KeyCount,
            series.SyntheticTeachingCount, series.CrossChartEvidenceCount, diagnostics,
            AuditBlockRelationSemantics(series, 2));
    }

    public static ExactRecurrenceFailureDiagnostic DiagnoseTarget(
        OriginalTemporalEventSeries series, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (targetIndex < 0 || targetIndex >= series.Events.Length)
            throw new ArgumentOutOfRangeException(nameof(targetIndex));
        if (targetIndex == 0 || targetIndex == series.Events.Length - 1)
            return Edge(series, targetIndex);
        return DiagnoseTarget(series, targetIndex, BuildCandidateIndex(series.Events));
    }

    private static ExactRecurrenceFailureDiagnostic DiagnoseTarget(OriginalTemporalEventSeries series,
        int targetIndex, IReadOnlyDictionary<(string Previous, string Next), CandidateOccurrence[]> candidateIndex)
    {

        var events = series.Events;
        var target = events[targetIndex];
        var previous = events[targetIndex - 1];
        var next = events[targetIndex + 1];
        var excluded = target.AllObservationIds;
        var candidates = candidateIndex.GetValueOrDefault((previous.Token.Identity, next.Token.Identity), []);
        var valid = candidates.Where(x => !x.Provenance.Any(excluded.Contains)).ToArray();
        var centerMultiplicities = valid.GroupBy(x => x.Center.Token.Identity, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new DonorCenterMultiplicity(x.Key, x.Count())).ToImmutableArray();
        var centers = centerMultiplicities.Select(x => x.CenterTokenIdentity).ToImmutableArray();
        var disposition = Classify(target.Token.Identity, centers);
        var otherCandidates = candidates.Where(x => x.Index != targetIndex).ToArray();
        var excludedCandidates = candidates.Where(x => x.Provenance.Any(excluded.Contains)).ToArray();
        var comparison = valid.Length > 0 ? valid : otherCandidates;
        var donorObservations = valid.SelectMany(x => x.Provenance).Distinct().OrderBy(x => x.Value)
            .ToImmutableArray();
        var leakage = AdaptiveContextResearch.AuditLeakage(excluded, donorObservations);
        var noContextReason = disposition != AdaptiveReconstructionState.NoContext
            ? ExactNoContextReason.NotApplicable
            : otherCandidates.Length == 0
                ? ExactNoContextReason.NoExactNeighborPairElsewhere
                : valid.Length == 0
                    ? ExactNoContextReason.OnlyExcludedOccurrences
                    : ExactNoContextReason.OtherExactAbsence;
        var targetSpacing = Spacing(events, targetIndex);
        var exactSpacingDivergence = comparison.Any(x => Spacing(events, x.Index) != targetSpacing);
        var beatSpacingDivergence = comparison.Any(x => BeatSpacing(events, x.Index) != BeatSpacing(events, targetIndex));
        var heldDivergence = comparison.Any(x => !x.Center.Token.HeldBeforeLanes.SequenceEqual(
            target.Token.HeldBeforeLanes));
        var typeDivergence = comparison.Any(x => TypeIdentity(x.Center.Token) != TypeIdentity(target.Token));
        var headCountDivergence = comparison.Any(x => x.Center.Token.HeadCount != target.Token.HeadCount);
        var laneDivergence = comparison.Any(x => LaneIdentity(x.Center.Token) != LaneIdentity(target.Token));
        var mixedDivergence = comparison.Any(x => x.Center.Token.MixedHeadTypes != target.Token.MixedHeadTypes);
        var refinements = ImmutableArray.Create(
            Refine("neighbor-token-only", disposition, valid, target.Token.Identity, _ => true),
            Refine("plus-exact-spacing", disposition, valid, target.Token.Identity,
                donor => Spacing(events, donor.Index) == targetSpacing
                    && BeatSpacing(events, donor.Index) == BeatSpacing(events, targetIndex)),
            Refine("plus-exact-held-state", disposition, valid, target.Token.Identity,
                donor => donor.Center.Token.HeldBeforeLanes.SequenceEqual(target.Token.HeldBeforeLanes)),
            Refine("plus-exact-spacing-held", disposition, valid, target.Token.Identity,
                donor => Spacing(events, donor.Index) == targetSpacing
                    && BeatSpacing(events, donor.Index) == BeatSpacing(events, targetIndex)
                    && donor.Center.Token.HeldBeforeLanes.SequenceEqual(target.Token.HeldBeforeLanes)));

        return new(ResearchSchemaVersion, series.ChartFingerprint, series.KeyCount, target.EventId, targetIndex,
            disposition, noContextReason, previous.EventId, next.EventId, target.Token.Identity,
            previous.Token.Identity, next.Token.Identity, target.Token.HeadCount, target.Token.TapHeadLanes.Length,
            target.Token.LongNoteHeadLanes.Length, target.Token.ReleaseCount, previous.Token.HeadCount,
            next.Token.HeadCount, targetSpacing.PreviousMilliseconds, targetSpacing.NextMilliseconds,
            BeatSpacing(events, targetIndex).PreviousBeats, BeatSpacing(events, targetIndex).NextBeats,
            candidates.Length, excludedCandidates.Length, valid.Length, centers.Length, centerMultiplicities,
            otherCandidates.Length > 0, candidates.Any(x => x.Index == targetIndex),
            excludedCandidates.Any(x => x.Index != targetIndex), exactSpacingDivergence, beatSpacingDivergence,
            heldDivergence, typeDivergence, headCountDivergence, laneDivergence, mixedDivergence,
            previous.Token.ReleaseCount > 0 || next.Token.ReleaseCount > 0,
            target.Token.ReleaseCount > 0, !target.Token.HeldBeforeLanes.IsEmpty,
            target.AllObservationIds, excluded,
            excludedCandidates.Select(x => x.Center.EventId).Distinct().Order(StringComparer.Ordinal).ToImmutableArray(),
            valid.Select(x => x.Center.EventId).Order(StringComparer.Ordinal).ToImmutableArray(), donorObservations,
            leakage, refinements);
    }

    public static ImmutableArray<RecurrenceIdentityLinkAudit> AuditBlockRelationSemantics(
        OriginalTemporalEventSeries series, int blockLength)
    {
        if (blockLength < 1) throw new ArgumentOutOfRangeException(nameof(blockLength));
        if (series.Events.Length < blockLength) return [];
        var occurrenceCounts = Enumerable.Range(0, series.Events.Length - blockLength + 1)
            .Select(start => string.Join("=>", series.Events.Skip(start).Take(blockLength)
                .Select(x => x.Token.Identity)))
            .GroupBy(x => x, StringComparer.Ordinal).Where(x => x.Count() > 1)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        var emitted = AdaptiveContextResearch.FindExactRecurrences(series, blockLength)
            .GroupBy(x => x.ExactBlockIdentity, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        return occurrenceCounts.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new RecurrenceIdentityLinkAudit(x.Key, x.Value, emitted.GetValueOrDefault(x.Key),
                checked(x.Value * (x.Value - 1) / 2))).ToImmutableArray();
    }

    public static CounterfactualTransition DescribeTransition(
        AdaptiveReconstructionState baseline, AdaptiveReconstructionState refined) =>
        baseline == refined ? baseline switch
        {
            AdaptiveReconstructionState.Reconstructed => CounterfactualTransition.PreservedReconstructed,
            AdaptiveReconstructionState.Ambiguous => CounterfactualTransition.PreservedAmbiguous,
            AdaptiveReconstructionState.Mismatch => CounterfactualTransition.PreservedMismatch,
            _ => CounterfactualTransition.PreservedNoContext
        } : refined switch
        {
            AdaptiveReconstructionState.Reconstructed => CounterfactualTransition.BecameReconstructed,
            AdaptiveReconstructionState.Ambiguous => CounterfactualTransition.BecameAmbiguous,
            AdaptiveReconstructionState.Mismatch => CounterfactualTransition.BecameMismatch,
            _ => CounterfactualTransition.BecameNoContext
        };

    private static ExactRecurrenceFailureDiagnostic Edge(OriginalTemporalEventSeries series, int targetIndex)
    {
        var target = series.Events[targetIndex];
        return new(ResearchSchemaVersion, series.ChartFingerprint, series.KeyCount, target.EventId, targetIndex,
            AdaptiveReconstructionState.NoContext, ExactNoContextReason.EdgeIneligible,
            targetIndex > 0 ? series.Events[targetIndex - 1].EventId : null,
            targetIndex + 1 < series.Events.Length ? series.Events[targetIndex + 1].EventId : null,
            target.Token.Identity, targetIndex > 0 ? series.Events[targetIndex - 1].Token.Identity : null,
            targetIndex + 1 < series.Events.Length ? series.Events[targetIndex + 1].Token.Identity : null,
            target.Token.HeadCount, target.Token.TapHeadLanes.Length, target.Token.LongNoteHeadLanes.Length,
            target.Token.ReleaseCount, targetIndex > 0 ? series.Events[targetIndex - 1].Token.HeadCount : 0,
            targetIndex + 1 < series.Events.Length ? series.Events[targetIndex + 1].Token.HeadCount : 0,
            target.PreviousSpacingMilliseconds,
            targetIndex + 1 < series.Events.Length ? series.Events[targetIndex + 1].PreviousSpacingMilliseconds : null,
            target.PreviousSpacingBeats,
            targetIndex + 1 < series.Events.Length ? series.Events[targetIndex + 1].PreviousSpacingBeats : null,
            0, 0, 0, 0, [], false, false, false, false, false, false, false, false, false, false,
            false, target.Token.ReleaseCount > 0, !target.Token.HeldBeforeLanes.IsEmpty,
            target.AllObservationIds, target.AllObservationIds, [], [], [], 0, []);
    }

    private static CounterfactualRefinementAudit Refine(string id, AdaptiveReconstructionState baseline,
        IEnumerable<CandidateOccurrence> donors, string targetToken, Func<CandidateOccurrence, bool> predicate)
    {
        var selected = donors.Where(predicate).ToArray();
        var centers = selected.Select(x => x.Center.Token.Identity).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToImmutableArray();
        var refined = Classify(targetToken, centers);
        return new(id, baseline, refined, DescribeTransition(baseline, refined), selected.Length, centers.Length,
            centers.Contains(targetToken, StringComparer.Ordinal));
    }

    private static AdaptiveReconstructionState Classify(string target, ImmutableArray<string> centers) =>
        centers.Length == 0 ? AdaptiveReconstructionState.NoContext
        : centers.Length > 1 ? AdaptiveReconstructionState.Ambiguous
        : centers[0] == target ? AdaptiveReconstructionState.Reconstructed
        : AdaptiveReconstructionState.Mismatch;

    private static CandidateOccurrence Candidate(ImmutableArray<OriginalTemporalEvent> events, int index) =>
        new(index, events[index], events.Skip(index - 1).Take(3).SelectMany(x => x.AllObservationIds)
            .Distinct().OrderBy(x => x.Value).ToImmutableArray());

    private static IReadOnlyDictionary<(string Previous, string Next), CandidateOccurrence[]> BuildCandidateIndex(
        ImmutableArray<OriginalTemporalEvent> events) => Enumerable.Range(1, Math.Max(0, events.Length - 2))
        .Select(index => Candidate(events, index))
        .GroupBy(x => (events[x.Index - 1].Token.Identity, events[x.Index + 1].Token.Identity))
        .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Index).ToArray());

    private static (int? PreviousMilliseconds, int? NextMilliseconds) Spacing(
        ImmutableArray<OriginalTemporalEvent> events, int index) =>
        (events[index].PreviousSpacingMilliseconds, events[index + 1].PreviousSpacingMilliseconds);

    private static (decimal? PreviousBeats, decimal? NextBeats) BeatSpacing(
        ImmutableArray<OriginalTemporalEvent> events, int index) =>
        (events[index].PreviousSpacingBeats, events[index + 1].PreviousSpacingBeats);

    private static string TypeIdentity(StructuralEventToken token) =>
        $"tap={token.TapHeadLanes.Length}|ln={token.LongNoteHeadLanes.Length}|release={token.ReleaseCount}|mixed={token.MixedHeadTypes}";

    private static string LaneIdentity(StructuralEventToken token) =>
        $"tap={string.Join(',', token.TapHeadLanes)}|ln={string.Join(',', token.LongNoteHeadLanes)}|release={string.Join(',', token.LongNoteReleaseLanes)}";

    private sealed record CandidateOccurrence(int Index, OriginalTemporalEvent Center,
        ImmutableArray<OriginalObservationId> Provenance);
}
