using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public static class DecisionDiagnosticVersions
{
    public const string Current = "phase-c1-2-shadow.1";
}

public readonly record struct OpportunityDiagnosticKey(string Value)
{
    public override string ToString() => Value;
}

public readonly record struct DiagnosticCandidateKey(string Value)
{
    public override string ToString() => Value;
}

public enum DiagnosticCandidateKind { Tap, LongNote, Articulation }
public enum BlockingSourceKind { OriginalObservation, AddedObject, ArticulationReplacement, HardSpacingBoundary, Other }
public enum HardValidityFailureCause
{
    NoShapeEvidence,
    NoLegalLane,
    BlockedByOriginalHold,
    BlockedByOriginalHead,
    BlockedByOriginalTap,
    BlockedBySyntheticTap,
    BlockedBySyntheticLn,
    BlockedByArticulationReplacement,
    BlockedBySpacingBefore,
    BlockedBySpacingAfter,
    InvalidDuration,
    InvalidAfterMillisecondMaterialization,
    OutsideLane,
    OtherHardValidity
}

public enum InteriorEndRelation { NotApplicable, Contained, Crossing, EqualEnd, Invalid }
public enum OriginalHoldBlockClassification { NotApplicable, PureOriginalHoldBlock, MixedBlock, NoOriginalHoldBlock }
public enum LegacyDecisionOutcome { Placed, CandidateNotSelected, NoLegalLane, NoShapeEvidence, ArticulationPlaced, ArticulationSkipped }
public enum DiagnosticDetailMode { Summary, Relevant, All }

public sealed record BlockingSource(
    BlockingSourceKind Kind,
    OriginalObservationId? ObservationId,
    string? SyntheticDiagnosticId,
    int Lane,
    ManiaObjectType? ObjectType,
    int? StartTime,
    int? EndTime);

public sealed record PlacementFailure(
    HardValidityFailureCause Cause,
    ImmutableArray<BlockingSource> Blockers);

public sealed record LaneValidityEvaluation(
    int Lane,
    bool IsValid,
    ImmutableArray<PlacementFailure> Failures);

public sealed record HardValidityResult(
    bool IsValid,
    ImmutableArray<HardValidityFailureCause> FailureReasons,
    ImmutableArray<LaneValidityEvaluation> LaneEvaluations);

public sealed record CandidateEvidencePath(
    OriginalObservationId ObservationId,
    string EvidenceTag,
    EvidenceScope Scope);

public sealed record ObservationAuthorityContribution(
    OriginalObservationId ObservationId,
    double DistanceWeight,
    double LegacyPathContributionBeforeAffinity,
    double DeduplicatedAuthorityBeforeAffinity,
    ImmutableArray<string> EvidenceLabels);

public sealed record RetriggerEvidenceDiagnostic(
    decimal GapBeats,
    RetriggerTransitionType TransitionType,
    EvidenceScope Scope,
    bool SameLane,
    ImmutableArray<OriginalObservationId> WitnessIds,
    int TransitionWitnessCount);

public sealed record RiceStateDiagnostic(
    int KeyCount,
    int OriginalSimultaneousHeads,
    int OriginalHeldColumns,
    int CurrentSimultaneousHeadsBefore,
    int CurrentOccupiedColumnsBefore,
    int CurrentLegalLanesBefore,
    int? DiagnosticLaneSelected,
    int CurrentSimultaneousHeadsAfter);

public sealed record DecisionDiagnostic(
    OpportunityDiagnosticKey OpportunityKey,
    DiagnosticCandidateKey CandidateKey,
    DiagnosticCandidateKind CandidateKind,
    OpportunityKind OpportunityKind,
    int StableLegacyOpportunityOrder,
    int StableLegacyCandidateOrder,
    int StartTime,
    int? EndTime,
    decimal StartBeat,
    decimal? EndBeat,
    double? LegacyWeight,
    LegacyDecisionOutcome LegacyOutcome,
    int? LegacySelectedLane,
    ImmutableArray<CandidateEvidencePath> LegacyEvidencePaths,
    int IndependentWitnessCount,
    int DuplicateEvidencePaths,
    SupportCertificate Certificate,
    HardValidityResult HardValidity,
    OriginalObservationId? ParentObservationId,
    InteriorAnchorKind InteriorAnchorProvenance,
    bool? StartInsideParent,
    InteriorEndRelation InteriorEndRelation,
    RiceStateDiagnostic? RiceState,
    double? C1ShadowWeight = null,
    ImmutableArray<ObservationAuthorityContribution>? PerObservationContributions = null,
    string ActiveAggregationRule = "NotApplicable",
    string C1ShadowAggregationRule = "NotApplicable");

public sealed record ArticulationIntentDiagnostic(
    OpportunityDiagnosticKey OpportunityKey,
    OriginalObservationId ParentObservationId,
    int AnchorTime,
    bool LegacySaturationGate,
    int NonHeldColumns,
    int HeldLnColumns,
    double HeldRatio,
    int NormalCandidatesAttempted,
    OriginalHoldBlockClassification OriginalHoldBlockClassification,
    bool BlockedByOriginalHolds,
    bool MixedFailures,
    string LegacyDecision,
    int LegacyAggregatedRetriggerEvidenceCount,
    ImmutableArray<RetriggerEvidenceDiagnostic> RetriggerEvidence);

public sealed record DiagnosticMetric(
    string Name,
    long Numerator,
    string DenominatorName,
    long Denominator,
    double? Ratio);

public sealed record FailureCauseDiagnosticCount(
    HardValidityFailureCause Cause,
    long Count,
    string DenominatorName,
    long Denominator,
    double? Ratio);

public sealed record BlockerSourceDiagnosticCount(
    BlockingSourceKind SourceKind,
    long Count,
    string DenominatorName,
    long Denominator,
    double? Ratio);

public sealed record DecisionDiagnosticSummary(
    int DiagnosticCandidateCount,
    int CandidateLaneDiagnosticCount,
    int WitnessReferenceCount,
    int LnCandidatesEvaluated,
    int LnCandidateEvidencePaths,
    int IndependentLnWitnesses,
    int DuplicateEvidencePaths,
    int LnCandidatesWithDuplicateWitness,
    int LnCandidatesWeightChangedByC1Shadow,
    double LnCandidateLegacyWeightSum,
    double LnCandidateC1ShadowWeightSum,
    int DuplicateEvidencePathsRemovedFromAuthority,
    int LanesBlockedByOriginal,
    int LanesBlockedBySynthetic,
    int LanesBlockedBySpacing,
    int MixedBlockCandidates,
    int InteriorContained,
    int InteriorCrossing,
    int InteriorEqualEnd,
    int LegacySaturationEligible,
    int ActuallyOriginalHoldBlocked,
    ImmutableArray<FailureCauseDiagnosticCount> FailureCauses,
    ImmutableArray<BlockerSourceDiagnosticCount> BlockerSources,
    ImmutableArray<DiagnosticMetric> Metrics);

public sealed record DecisionDiagnostics(
    string DecisionDiagnosticVersion,
    string BehaviorPolicyVersion,
    string EvidenceProfileVersion,
    string ChartFingerprint,
    ImmutableArray<DecisionDiagnostic> Decisions,
    ImmutableArray<ArticulationIntentDiagnostic> ArticulationIntents,
    DecisionDiagnosticSummary Summary,
    double CertificateBuildMs,
    double FailureDiagnosticMs,
    long DetailedExportBytes,
    DiagnosticDetailMode DetailedDecisionFilter,
    int TotalDecisionCount,
    int ExportedDecisionCount);

public static class DecisionDiagnosticsJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(DecisionDiagnostics diagnostics, DiagnosticDetailMode detailMode = DiagnosticDetailMode.All)
    {
        var decisions = detailMode switch
        {
            DiagnosticDetailMode.Summary => [],
            DiagnosticDetailMode.Relevant => diagnostics.Decisions.Where(x =>
                x.LegacyOutcome == LegacyDecisionOutcome.Placed
                || x.OpportunityKind == OpportunityKind.LnInterior
                || x.LegacyOutcome == LegacyDecisionOutcome.NoShapeEvidence).ToImmutableArray(),
            _ => diagnostics.Decisions
        };
        var export = diagnostics with
        {
            Decisions = decisions,
            DetailedDecisionFilter = detailMode,
            TotalDecisionCount = diagnostics.Summary.DiagnosticCandidateCount,
            ExportedDecisionCount = decisions.Length
        };
        return JsonSerializer.Serialize(export, Options);
    }

    public static string SerializeMeasured(
        DecisionDiagnostics diagnostics,
        DiagnosticDetailMode detailMode = DiagnosticDetailMode.All)
    {
        var measured = diagnostics;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var json = Serialize(measured, detailMode);
            var bytes = Encoding.UTF8.GetByteCount(json);
            if (measured.DetailedExportBytes == bytes) return json;
            measured = measured with { DetailedExportBytes = bytes };
        }

        throw new InvalidOperationException("Decision diagnostic export size did not converge.");
    }
}

public static class DecisionDiagnosticQueries
{
    public static ImmutableArray<RetriggerEvidenceDiagnostic> BuildRetriggerEvidence(
        MapperEvidenceProfile profile, int parentLane, decimal anchorBeat, AddNotesOptions options)
    {
        var radius = (decimal)options.RetriggerGapWindowBeats;
        var local = profile.RetriggerObservations.Where(x =>
        {
            var head = profile.Observations[x.NextHeadId.Value];
            return decimal.Abs(head.StartBeat - anchorBeat) <= radius && x.GapBeats > 0 && x.GapBeats <= 1;
        }).ToArray();
        var sameLane = local.Where(x => x.Lane == parentLane).GroupBy(x => new
            { Gap = decimal.Round(x.GapBeats, 6, MidpointRounding.AwayFromZero), x.TransitionType })
            .Where(x => x.Count() >= options.RetriggerGapMinimumSupport).ToArray();
        var selected = sameLane.Length > 0 ? sameLane : local.Where(x => x.Lane != parentLane)
            .GroupBy(x => new { Gap = decimal.Round(x.GapBeats, 6, MidpointRounding.AwayFromZero), x.TransitionType })
            .Where(x => x.Count() >= options.RetriggerGapMinimumSupport).ToArray();
        return selected.OrderByDescending(x => x.Count()).ThenBy(x => x.Key.Gap).ThenBy(x => x.Key.TransitionType)
            .Select(group => new RetriggerEvidenceDiagnostic(group.Key.Gap, group.Key.TransitionType,
                EvidenceScope.Local, sameLane.Length > 0,
                group.SelectMany(x => new[] { x.LongNoteId, x.NextHeadId }).Distinct()
                    .OrderBy(x => x.Value).ToImmutableArray(), group.Count())).ToImmutableArray();
    }
}

internal sealed class DecisionDiagnosticsBuilder
{
    public const string Version = DecisionDiagnosticVersions.Current;
    private readonly MapperEvidenceProfile _profile;
    private readonly List<DecisionDiagnostic> _decisions = [];
    private readonly List<ArticulationIntentDiagnostic> _articulation = [];
    private long _certificateTicks;
    private long _failureTicks;

    public DecisionDiagnosticsBuilder(MapperEvidenceProfile profile)
    {
        _profile = profile;
        Provenance = new DiagnosticProvenanceIndex(profile);
    }

    internal DiagnosticProvenanceIndex Provenance { get; }

    public OpportunityDiagnosticKey OpportunityKey(OpportunityKind kind, int order, ManiaObject source,
        ManiaObject? parent, int? anchorTime)
    {
        var sourceId = FindObservation(parent ?? source)?.Id.ToString() ?? "VIRTUAL";
        return new OpportunityDiagnosticKey(
            $"{_profile.BehaviorPolicyVersion}/OP-{order:D8}-{kind}-{sourceId}-T{source.StartTime}-A{anchorTime?.ToString() ?? "NA"}");
    }

    public DiagnosticCandidateKey CandidateKey(OpportunityDiagnosticKey opportunity, DiagnosticCandidateKind kind,
        int order, int startTime, int? endTime) => new(
        $"{opportunity.Value}/C-{order:D4}-{kind}-S{startTime}-E{endTime?.ToString() ?? "TAP"}");

    public OriginalObservation? FindObservation(ManiaObject source) => Provenance.Find(source);

    public int Record(DecisionDiagnostic diagnostic)
    {
        _decisions.Add(diagnostic);
        return _decisions.Count - 1;
    }

    public void MarkSelected(int index, int lane, LegacyDecisionOutcome outcome, RiceStateDiagnostic? riceState = null)
    {
        _decisions[index] = _decisions[index] with
        {
            LegacySelectedLane = lane,
            LegacyOutcome = outcome,
            RiceState = riceState ?? _decisions[index].RiceState
        };
    }

    public SupportCertificate Certificate(DiagnosticCandidateKey key, ImmutableArray<CandidateEvidencePath> paths,
        HardValidityResult validity, bool evaluatedEvidence, int? sourceHeadTime = null,
        decimal? sourceHeadBeat = null)
    {
        var started = Stopwatch.GetTimestamp();
        var witnesses = paths.GroupBy(x => x.ObservationId).OrderBy(x => x.Key.Value)
            .Select(group => new TransformationWitness($"W-{group.Key}", EvidenceClaimLevel.ObservedValue,
                [group.Key], group.Select(x => x.EvidenceTag).Distinct().Order().ToImmutableArray()))
            .ToImmutableArray();
        var observedValues = paths.GroupBy(x => new { x.ObservationId, x.EvidenceTag })
            .OrderBy(x => x.Key.ObservationId.Value).ThenBy(x => x.Key.EvidenceTag)
            .Select(group =>
            {
                var observation = _profile.Observations[group.Key.ObservationId.Value];
                var value = group.Key.EvidenceTag == "Duration"
                    ? observation.DurationBeats.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : observation.EndTime?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
                return new CertificateObservedValue(group.Key.ObservationId, group.Key.EvidenceTag, value);
            }).ToImmutableArray();
        var sameHead = sourceHeadTime is null || sourceHeadBeat is null ? []
            : paths.Select(x => x.ObservationId).Distinct().OrderBy(x => x.Value)
                .Select(id => _profile.Observations[id.Value])
                .Where(x => x.StartTime == sourceHeadTime && x.StartBeat == sourceHeadBeat).ToArray();
        var observedRelations = sameHead.Length == 0 ? []
            : ImmutableArray.Create(new CertificateObservedRelation("ExactHead", sourceHeadTime!.Value,
                    sourceHeadBeat!.Value, null, null, sameHead.Select(x => x.Id).ToImmutableArray(), ["ExactHead"]))
                .AddRange(sameHead.GroupBy(x => new { x.EndTime, x.EndBeat })
                    .OrderBy(x => x.Key.EndTime).ThenBy(x => x.Key.EndBeat)
                    .Select(group => new CertificateObservedRelation("HeadToRelease", sourceHeadTime.Value,
                        sourceHeadBeat.Value, group.Key.EndTime, group.Key.EndBeat,
                        group.Select(x => x.Id).ToImmutableArray(),
                        group.SelectMany(observation => paths.Where(x => x.ObservationId == observation.Id)
                                .Select(x => x.EvidenceTag)).Distinct().Order().ToImmutableArray())));
        var claimLevel = observedRelations.Length > 0 ? EvidenceClaimLevel.ObservedRelation
            : evaluatedEvidence ? EvidenceClaimLevel.ObservedValue : EvidenceClaimLevel.Unknown;
        var certificate = new SupportCertificate(key, claimLevel,
            EvidenceScope.Local, evaluatedEvidence && witnesses.Length > 0
                ? EvidenceResolutionState.ObservedSupport : EvidenceResolutionState.NotEvaluated,
            witnesses,
            evaluatedEvidence && witnesses.Length > 0 ? ["LegacyShapeRoutePresent"] : [],
            evaluatedEvidence ? [] : ["ComparableContext", "CompatibleComposition"],
            [], witnesses.Length, validity, observedValues, observedRelations);
        _certificateTicks += Stopwatch.GetTimestamp() - started;
        return certificate;
    }

    public void AddFailureTicks(long ticks) => _failureTicks += ticks;

    public void AddArticulation(ArticulationIntentDiagnostic diagnostic) => _articulation.Add(diagnostic);

    public (int CandidateCount, OriginalHoldBlockClassification Classification, bool BlockedByOriginal,
        bool Mixed) ClassifyNormalFailures(OpportunityDiagnosticKey opportunity)
    {
        var candidates = _decisions.Where(x => x.OpportunityKey == opportunity
            && x.CandidateKind == DiagnosticCandidateKind.LongNote).ToArray();
        var lanes = candidates.SelectMany(x => x.HardValidity.LaneEvaluations).Where(x => !x.IsValid).ToArray();
        var causalOriginal = lanes.Any(x => x.Failures.Length > 0
            && x.Failures.All(f => f.Cause == HardValidityFailureCause.BlockedByOriginalHold));
        var hasOther = lanes.SelectMany(x => x.Failures)
            .Any(x => x.Cause != HardValidityFailureCause.BlockedByOriginalHold);
        var classification = !causalOriginal ? OriginalHoldBlockClassification.NoOriginalHoldBlock
            : hasOther ? OriginalHoldBlockClassification.MixedBlock
            : OriginalHoldBlockClassification.PureOriginalHoldBlock;
        return (candidates.Length, classification, causalOriginal, causalOriginal && hasOther);
    }

    public DecisionDiagnostics Build()
    {
        var decisions = _decisions.ToImmutableArray();
        var articulation = _articulation.ToImmutableArray();
        var ln = decisions.Where(x => x.CandidateKind == DiagnosticCandidateKind.LongNote).ToArray();
        var laneEvaluations = decisions.SelectMany(x => x.HardValidity.LaneEvaluations).ToArray();
        var originalBlocked = laneEvaluations.Count(x => x.Failures.Any(f => f.Blockers.Any(b => b.Kind == BlockingSourceKind.OriginalObservation)));
        var syntheticBlocked = laneEvaluations.Count(x => x.Failures.Any(f => f.Blockers.Any(b => b.Kind is BlockingSourceKind.AddedObject or BlockingSourceKind.ArticulationReplacement)));
        var spacingBlocked = laneEvaluations.Count(x => x.Failures.Any(f => f.Cause is HardValidityFailureCause.BlockedBySpacingBefore or HardValidityFailureCause.BlockedBySpacingAfter));
        var mixed = decisions.Count(x => DistinctFailureFamilies(x.HardValidity) > 1);
        var paths = ln.Sum(x => x.LegacyEvidencePaths.Length);
        var independent = ln.Sum(x => x.IndependentWitnessCount);
        var failures = laneEvaluations.SelectMany(x => x.Failures).ToArray();
        var blockers = failures.SelectMany(x => x.Blockers).ToArray();
        var laneFailureCounts = failures.GroupBy(x => x.Cause).OrderBy(x => x.Key)
            .Select(x => new FailureCauseDiagnosticCount(x.Key, x.LongCount(), "CandidateLaneEvaluations",
                laneEvaluations.Length, laneEvaluations.Length == 0 ? null : x.LongCount() / (double)laneEvaluations.Length))
            .ToImmutableArray();
        var globalFailureCounts = decisions.SelectMany(x => x.HardValidity.FailureReasons).GroupBy(x => x)
            .OrderBy(x => x.Key).Select(x => new FailureCauseDiagnosticCount(x.Key, x.LongCount(),
                "DiagnosticCandidates", decisions.Length,
                decisions.Length == 0 ? null : x.LongCount() / (double)decisions.Length)).ToImmutableArray();
        var failureCounts = globalFailureCounts.AddRange(laneFailureCounts);
        var blockerCounts = blockers.GroupBy(x => x.Kind).OrderBy(x => x.Key)
            .Select(x => new BlockerSourceDiagnosticCount(x.Key, x.LongCount(), "BlockerReferences",
                blockers.LongLength, blockers.Length == 0 ? null : x.LongCount() / (double)blockers.Length))
            .ToImmutableArray();
        var summary = new DecisionDiagnosticSummary(decisions.Length, laneEvaluations.Length,
            decisions.Sum(x => x.Certificate.Witnesses.Sum(w => w.ObservationIds.Length)), ln.Length, paths,
            independent, ln.Sum(x => x.DuplicateEvidencePaths),
            ln.Count(x => x.DuplicateEvidencePaths > 0),
            ln.Count(x => x.LegacyWeight != x.C1ShadowWeight),
            ln.Sum(x => x.LegacyWeight ?? 0), ln.Sum(x => x.C1ShadowWeight ?? 0),
            ln.Sum(x => x.DuplicateEvidencePaths), originalBlocked, syntheticBlocked, spacingBlocked,
            mixed, ln.Count(x => x.InteriorEndRelation == InteriorEndRelation.Contained),
            ln.Count(x => x.InteriorEndRelation == InteriorEndRelation.Crossing),
            ln.Count(x => x.InteriorEndRelation == InteriorEndRelation.EqualEnd),
            articulation.Count(x => x.LegacySaturationGate),
            articulation.Count(x => x.BlockedByOriginalHolds),
            failureCounts, blockerCounts,
            [
                Metric("CandidatesBlockedByOriginalHold", decisions.Count(HasOriginalHold), "LnCandidatesEvaluated", ln.Length),
                Metric("LanesBlockedBySynthetic", syntheticBlocked, "CandidateLaneEvaluations", laneEvaluations.Length),
                Metric("DuplicateEvidencePaths", ln.Sum(x => x.DuplicateEvidencePaths), "LnCandidateEvidencePaths", paths),
                Metric("ActuallyOriginalHoldBlockedArticulationIntents", articulation.Count(x => x.BlockedByOriginalHolds), "LegacySaturationEligible", articulation.Count(x => x.LegacySaturationGate))
            ]);
        return new DecisionDiagnostics(Version, _profile.BehaviorPolicyVersion, _profile.EvidenceProfileVersion,
            _profile.ChartFingerprint, decisions, articulation, summary,
            TicksToMs(_certificateTicks), TicksToMs(_failureTicks), 0, DiagnosticDetailMode.All,
            decisions.Length, decisions.Length);
    }

    private static bool HasOriginalHold(DecisionDiagnostic decision) => decision.HardValidity.LaneEvaluations
        .Any(l => l.Failures.Any(f => f.Cause == HardValidityFailureCause.BlockedByOriginalHold));

    private static int DistinctFailureFamilies(HardValidityResult result) => result.LaneEvaluations
        .SelectMany(x => x.Failures).Select(x => Family(x.Cause)).Distinct().Count();

    private static string Family(HardValidityFailureCause cause) => cause switch
    {
        HardValidityFailureCause.BlockedByOriginalHold or HardValidityFailureCause.BlockedByOriginalHead
            or HardValidityFailureCause.BlockedByOriginalTap => "original",
        HardValidityFailureCause.BlockedBySyntheticTap or HardValidityFailureCause.BlockedBySyntheticLn
            or HardValidityFailureCause.BlockedByArticulationReplacement => "synthetic",
        HardValidityFailureCause.BlockedBySpacingBefore or HardValidityFailureCause.BlockedBySpacingAfter => "spacing",
        _ => "other"
    };

    private static DiagnosticMetric Metric(string name, long numerator, string denominatorName, long denominator) =>
        new(name, numerator, denominatorName, denominator, denominator == 0 ? null : (double)numerator / denominator);

    private static double TicksToMs(long ticks) => ticks * 1000d / Stopwatch.Frequency;
}

internal sealed class DiagnosticProvenanceIndex
{
    private readonly Dictionary<(int Sequence, int Lane, ManiaObjectType Type, int Start, int? End), OriginalObservation> _bySource;

    public DiagnosticProvenanceIndex(MapperEvidenceProfile profile) => _bySource = profile.Observations.ToDictionary(
        x => (x.SourceSequence, x.Lane, x.Type, x.StartTime, x.EndTime));

    public OriginalObservation? Find(ManiaObject source) => _bySource.GetValueOrDefault(
        (source.Sequence, source.Lane, source.Type, source.StartTime, source.EndTime));
}
