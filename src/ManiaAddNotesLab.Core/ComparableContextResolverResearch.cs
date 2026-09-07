using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum ComparableEvidenceState
{
    ObservedSupport,
    LocalMismatch,
    NoComparableContext,
    AmbiguousEvidence
}

public enum CandidateViewDisposition { Supporting, Contradicting, Abstaining }

public enum ComparableEvidenceAmbiguity
{
    SupportAndLocalMismatch,
    CandidateInUniqueCompletionConflict,
    MarginalAgreementWithoutJointComparable,
    MarginalAgreementContradictedByJoint
}

public enum CandidateJointEvidenceState
{
    NoMarginalAgreement,
    ObservedJointContext,
    MarginalWithoutJointComparable,
    MarginalContradictedByJoint
}

public sealed record DonorOccurrenceReference(
    string DonorGroupId,
    OriginalObservationId CompletionObservationId,
    ImmutableArray<OriginalObservationId> FullGroupObservationIds,
    int HeadTime,
    decimal HeadBeat,
    string ContextSourceGroupId);

public sealed record CandidateCompletionEvidence(
    ExactCompletionIdentity Completion,
    ImmutableArray<DonorOccurrenceReference> Donors);

public sealed record CandidateContextViewEvidence(
    ExactCompletionContextView ViewId,
    CandidateViewDisposition Disposition,
    string ExactContextSignature,
    int ObservedCompletionCount,
    ImmutableArray<CandidateCompletionEvidence> RelevantCompletionEvidence);

public sealed record SupportingDependencyEdge(
    ExactCompletionContextView Parent,
    ExactCompletionContextView Child);

public sealed record CandidateJointContextEvidence(
    ObservedJointContextKind Kind,
    CandidateJointEvidenceState State,
    ExactCompletionContextView LeftMarginal,
    ExactCompletionContextView RightMarginal,
    ExactCompletionContextView JointView,
    ImmutableArray<string> LeftMarginalDonorGroupIds,
    ImmutableArray<string> RightMarginalDonorGroupIds,
    ImmutableArray<string> JointDonorGroupIds);

public sealed record ComparableContextResolution(
    string ChartFingerprint,
    string TargetSourceGroupId,
    OriginalObservationId TargetObservationId,
    ExactCompletionIdentity TargetCompletion,
    ExactCompletionIdentity CandidateCompletion,
    bool IsTargetCandidate,
    ComparableEvidenceState EvidenceState,
    bool HasObservedSupport,
    bool HasLocalMismatch,
    string CoverageSignature,
    ImmutableArray<ExactCompletionContextView> ComparableViews,
    ImmutableArray<ExactCompletionContextView> SupportingViews,
    ImmutableArray<ExactCompletionContextView> ContradictingViews,
    ImmutableArray<ExactCompletionContextView> AbstainingViews,
    ImmutableArray<ExactCompletionContextView> UniqueSupportingViews,
    ImmutableArray<string> SupportingDonorGroupIds,
    ImmutableArray<string> ContradictingDonorGroupIds,
    ImmutableArray<string> SharedSupportingAndContradictingDonorGroupIds,
    ImmutableArray<OriginalObservationId> SupportingObservationIds,
    ImmutableArray<OriginalObservationId> ContradictingObservationIds,
    ImmutableArray<ComparableEvidenceAmbiguity> Ambiguities,
    ImmutableArray<SupportingDependencyEdge> SupportingDependencies,
    ImmutableArray<CandidateJointContextEvidence> JointEvidence,
    ImmutableArray<CandidateContextViewEvidence> ViewEvidence,
    ViewConflictRecord? ConflictRecord,
    int OriginalChordHeadCount,
    int ReducedVisibleMemberCount,
    bool HeldBeforePresent,
    int BaselineCompletionCount);

public sealed record ComparableContextResolverResearchResult(
    string ResearchSchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    int ExactHeadGroupCount,
    ImmutableArray<ExactCompletionOccurrence> DonorOccurrences,
    ImmutableArray<ComparableContextResolution> Resolutions,
    [property: JsonIgnore]
    double ResolutionBuildMilliseconds,
    int TargetGroupLeakageCount,
    int FutureHeldLeakageCount,
    int HeldTailEncodedAsHeadCount,
    int DonorNestingViolationCount)
{
    public int TargetTrials => Resolutions.Select(x => (x.TargetSourceGroupId, x.TargetObservationId)).Distinct().Count();
    public int CandidateResolutions => Resolutions.Length;
    public int TargetCandidateResolutions => Resolutions.Count(x => x.IsTargetCandidate);
}

/// <summary>
/// F1 candidate-centric research resolver. It describes exact evidence state and provenance, but never selects a
/// completion, orders candidates, performs backoff, consumes RNG, or participates in generation.
/// </summary>
public static class ComparableContextResolverResearch
{
    public const string ResearchSchemaVersion = "phase-f1-research.1";
    public static readonly ImmutableArray<ExactCompletionContextView> ContextViews =
        ExactCompletionContextResearch.Views.Where(x => x != ExactCompletionContextView.ReducedOnly)
            .ToImmutableArray();

    public static ComparableContextResolverResearchResult Evaluate(ManiaChart chart) =>
        Resolve(ExactContextAgreementResearch.Evaluate(chart));

    public static ComparableContextResolverResearchResult Resolve(ExactContextAgreementResearchResult source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var started = Stopwatch.GetTimestamp();
        var occurrenceIndex = source.DonorOccurrences.GroupBy(x => (
                x.Relation.ReducedStateSignature, x.Witness.SourceGroupId,
                x.Relation.CompletionLane, x.Relation.CompletionType))
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Witness.CompletionObservationId.Value).ToImmutableArray());
        var resolutions = ImmutableArray.CreateBuilder<ComparableContextResolution>();
        foreach (var certificate in source.Certificates)
        {
            var target = new ExactCompletionIdentity(certificate.TargetLane, certificate.TargetType);
            var candidates = certificate.Views.SelectMany(x => x.CompletionSet)
                .Select(x => x.Completion).Append(target).Distinct()
                .OrderBy(x => x.Lane).ThenBy(x => x.HeadType).ToArray();
            var reducedSignature = certificate.Views.Single(x =>
                x.ViewId == ExactCompletionContextView.ReducedOnly).ExactContextSignature;
            foreach (var candidate in candidates)
                resolutions.Add(ResolveCandidate(source.ChartFingerprint, certificate, candidate,
                    reducedSignature, occurrenceIndex));
        }
        return new ComparableContextResolverResearchResult(ResearchSchemaVersion, source.ChartFingerprint,
            source.KeyCount, source.OriginalObjectCount, source.ExactHeadGroupCount, source.DonorOccurrences,
            resolutions.ToImmutable(), Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            source.TargetGroupLeakageCount, source.FutureHeldLeakageCount, source.HeldTailEncodedAsHeadCount,
            source.DonorNestingViolationCount);
    }

    public static ComparableEvidenceState ClassifyEvidenceState(int comparableViewCount,
        bool hasObservedSupport, bool hasLocalMismatch,
        IEnumerable<ComparableEvidenceAmbiguity> ambiguities)
    {
        if (comparableViewCount == 0) return ComparableEvidenceState.NoComparableContext;
        if (!hasObservedSupport) return ComparableEvidenceState.LocalMismatch;
        return hasLocalMismatch || ambiguities.Any()
            ? ComparableEvidenceState.AmbiguousEvidence
            : ComparableEvidenceState.ObservedSupport;
    }

    private static ComparableContextResolution ResolveCandidate(string chartFingerprint,
        ContextAgreementCertificate certificate, ExactCompletionIdentity candidate, string reducedSignature,
        IReadOnlyDictionary<(string ReducedStateSignature, string SourceGroupId, int CompletionLane,
            OriginalHeadMemberType CompletionType), ImmutableArray<ExactCompletionOccurrence>> occurrenceIndex)
    {
        var viewEvidence = ContextViews.Select(viewId =>
        {
            var view = certificate.Views.Single(x => x.ViewId == viewId);
            var disposition = !view.Comparable ? CandidateViewDisposition.Abstaining
                : view.CompletionSet.Any(x => x.Completion == candidate) ? CandidateViewDisposition.Supporting
                : CandidateViewDisposition.Contradicting;
            var relevant = (!view.Comparable ? [] : disposition == CandidateViewDisposition.Supporting
                    ? view.CompletionSet.Where(x => x.Completion == candidate) : view.CompletionSet)
                .Select(completion => new CandidateCompletionEvidence(
                    completion.Completion, completion.DonorGroupIds.SelectMany(groupId =>
                        References(reducedSignature, groupId, completion.Completion, occurrenceIndex))
                    .DistinctBy(x => (x.DonorGroupId, x.CompletionObservationId))
                    .OrderBy(x => x.DonorGroupId, StringComparer.Ordinal)
                    .ThenBy(x => x.CompletionObservationId.Value).ToImmutableArray()))
                .ToImmutableArray();
            return new CandidateContextViewEvidence(viewId, disposition, view.ExactContextSignature,
                view.CompletionSet.Length, relevant);
        }).ToImmutableArray();

        var comparable = viewEvidence.Where(x => x.Disposition != CandidateViewDisposition.Abstaining).ToArray();
        var supporting = viewEvidence.Where(x => x.Disposition == CandidateViewDisposition.Supporting).ToArray();
        var contradicting = viewEvidence.Where(x => x.Disposition == CandidateViewDisposition.Contradicting).ToArray();
        var supportDonors = supporting.SelectMany(x => x.RelevantCompletionEvidence)
            .Where(x => x.Completion == candidate).SelectMany(x => x.Donors).ToArray();
        var contradictionDonors = contradicting.SelectMany(x => x.RelevantCompletionEvidence)
            .SelectMany(x => x.Donors).ToArray();
        var supportGroups = supportDonors.Select(x => x.DonorGroupId).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToImmutableArray();
        var contradictionGroups = contradictionDonors.Select(x => x.DonorGroupId).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToImmutableArray();

        var jointEvidence = certificate.JointContexts.Select(joint => CandidateJoint(candidate, joint,
            certificate.Views)).ToImmutableArray();
        var ambiguity = ImmutableArray.CreateBuilder<ComparableEvidenceAmbiguity>();
        if (supporting.Length > 0 && contradicting.Length > 0)
            ambiguity.Add(ComparableEvidenceAmbiguity.SupportAndLocalMismatch);
        if (certificate.Conflict?.Selections.Any(x => x.Completion == candidate) == true)
            ambiguity.Add(ComparableEvidenceAmbiguity.CandidateInUniqueCompletionConflict);
        if (jointEvidence.Any(x => x.State == CandidateJointEvidenceState.MarginalWithoutJointComparable))
            ambiguity.Add(ComparableEvidenceAmbiguity.MarginalAgreementWithoutJointComparable);
        if (jointEvidence.Any(x => x.State == CandidateJointEvidenceState.MarginalContradictedByJoint))
            ambiguity.Add(ComparableEvidenceAmbiguity.MarginalAgreementContradictedByJoint);
        var ambiguities = ambiguity.Distinct().Order().ToImmutableArray();
        var state = ClassifyEvidenceState(comparable.Length, supporting.Length > 0,
            contradicting.Length > 0, ambiguities);
        var supportingIds = supporting.Select(x => x.ViewId).ToHashSet();
        var dependencies = ExactContextViewDependencies.Graph.Where(x =>
                supportingIds.Contains(x.Parent) && supportingIds.Contains(x.Child))
            .Select(x => new SupportingDependencyEdge(x.Parent, x.Child)).ToImmutableArray();
        var target = new ExactCompletionIdentity(certificate.TargetLane, certificate.TargetType);
        return new ComparableContextResolution(chartFingerprint, certificate.TargetSourceGroupId,
            certificate.TargetObservationId, target, candidate, candidate == target, state,
            supporting.Length > 0, contradicting.Length > 0, certificate.CoverageSignature,
            comparable.Select(x => x.ViewId).ToImmutableArray(),
            supporting.Select(x => x.ViewId).ToImmutableArray(),
            contradicting.Select(x => x.ViewId).ToImmutableArray(),
            viewEvidence.Where(x => x.Disposition == CandidateViewDisposition.Abstaining)
                .Select(x => x.ViewId).ToImmutableArray(),
            supporting.Where(x => x.ObservedCompletionCount == 1).Select(x => x.ViewId).ToImmutableArray(),
            supportGroups, contradictionGroups,
            supportGroups.Intersect(contradictionGroups, StringComparer.Ordinal).Order(StringComparer.Ordinal)
                .ToImmutableArray(),
            supportDonors.SelectMany(x => x.FullGroupObservationIds.Append(x.CompletionObservationId))
                .Distinct().OrderBy(x => x.Value).ToImmutableArray(),
            contradictionDonors.SelectMany(x => x.FullGroupObservationIds.Append(x.CompletionObservationId))
                .Distinct().OrderBy(x => x.Value).ToImmutableArray(),
            ambiguities, dependencies, jointEvidence, viewEvidence, certificate.Conflict,
            certificate.OriginalChordHeadCount, certificate.ReducedVisibleMemberCount,
            certificate.HeldBeforePresent, certificate.BaselineCompletionCount);
    }

    private static CandidateJointContextEvidence CandidateJoint(ExactCompletionIdentity candidate,
        ObservedJointContextSupport joint, ImmutableArray<ContextViewObservation> views)
    {
        var left = views.Single(x => x.ViewId == joint.LeftMarginal);
        var right = views.Single(x => x.ViewId == joint.RightMarginal);
        var jointView = views.Single(x => x.ViewId == joint.JointView);
        var leftDonors = Donors(left, candidate);
        var rightDonors = Donors(right, candidate);
        var jointDonors = Donors(jointView, candidate);
        var marginalAgreement = left.Comparable && right.Comparable
            && leftDonors.Length > 0 && rightDonors.Length > 0;
        var state = !marginalAgreement ? CandidateJointEvidenceState.NoMarginalAgreement
            : !jointView.Comparable ? CandidateJointEvidenceState.MarginalWithoutJointComparable
            : jointDonors.Length > 0 ? CandidateJointEvidenceState.ObservedJointContext
            : CandidateJointEvidenceState.MarginalContradictedByJoint;
        return new CandidateJointContextEvidence(joint.Kind, state, joint.LeftMarginal,
            joint.RightMarginal, joint.JointView, leftDonors, rightDonors, jointDonors);
    }

    private static ImmutableArray<string> Donors(ContextViewObservation view, ExactCompletionIdentity candidate) =>
        view.CompletionSet.SingleOrDefault(x => x.Completion == candidate)?.DonorGroupIds ?? [];

    private static IEnumerable<DonorOccurrenceReference> References(string reducedSignature, string donorGroupId,
        ExactCompletionIdentity completion,
        IReadOnlyDictionary<(string ReducedStateSignature, string SourceGroupId, int CompletionLane,
            OriginalHeadMemberType CompletionType), ImmutableArray<ExactCompletionOccurrence>> index)
    {
        foreach (var occurrence in index.GetValueOrDefault((reducedSignature, donorGroupId,
                     completion.Lane, completion.HeadType), []))
            yield return new DonorOccurrenceReference(occurrence.Witness.SourceGroupId,
                occurrence.Witness.CompletionObservationId, occurrence.Witness.FullOriginalMemberObservationIds,
                occurrence.Witness.HeadTime, occurrence.Witness.HeadBeat, occurrence.Context.SourceGroupId);
    }
}

public static class ComparableContextResolverResearchJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    public static string Serialize(ComparableContextResolverResearchResult result) =>
        JsonSerializer.Serialize(result, Options);
}
