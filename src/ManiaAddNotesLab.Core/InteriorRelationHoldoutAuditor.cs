using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ManiaAddNotesLab.Core;

/// <summary>
/// A donor presented to the post-construction G1.0 auditor. Synthetic provenance is explicit because
/// synthetic research fixtures must be auditable even though the real census is original-only.
/// </summary>
public sealed record InteriorRelationHoldoutDonor(
    InteriorRelationOccurrence Occurrence,
    bool IsSynthetic = false);

public sealed record InteriorRelationHoldoutAudit(
    int TargetLeakageCount,
    int ParentLeakageCount,
    int ReleaseLeakageCount,
    int FutureLeakageCount,
    int SameEventLeakageCount,
    int SyntheticLeakageCount,
    int CrossChartLeakageCount)
{
    public int Total => TargetLeakageCount + ParentLeakageCount + ReleaseLeakageCount
        + FutureLeakageCount + SameEventLeakageCount + SyntheticLeakageCount + CrossChartLeakageCount;
}

public sealed record InteriorRelationHoldoutAssessment(
    int ComparableDonorCount,
    int DistinctRelationCount,
    bool ExactJointSupported,
    bool DurationMarginalSupported,
    bool EndpointMarginalSupported,
    bool MarginalOnly,
    InteriorAlternativeState AlternativeState,
    ImmutableArray<string> DonorOccurrenceIds);

/// <summary>
/// Pure, deterministic verification of an already accepted G1.0 donor set. This deliberately does not
/// participate in donor construction, so a future construction regression can reach and fail this layer.
/// </summary>
public static class InteriorRelationHoldoutAuditor
{
    public static InteriorRelationHoldoutAudit Audit(InteriorRelationOccurrence target,
        InteriorHoldoutKind holdoutKind, IEnumerable<InteriorRelationHoldoutDonor> acceptedDonors)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(acceptedDonors);

        var targetLeakage = 0;
        var parentLeakage = 0;
        var releaseLeakage = 0;
        var futureLeakage = 0;
        var sameEventLeakage = 0;
        var syntheticLeakage = 0;
        var crossChartLeakage = 0;

        foreach (var accepted in acceptedDonors)
        {
            var donor = accepted.Occurrence;
            var sameChart = string.Equals(donor.ChartFingerprint, target.ChartFingerprint,
                StringComparison.Ordinal);

            if (!sameChart) crossChartLeakage++;
            if (accepted.IsSynthetic) syntheticLeakage++;

            // Observation IDs are chart-local identities, so event comparisons are meaningful only
            // after chart identity has independently passed.
            if (!sameChart) continue;
            if (donor.WitnessLongNoteId == target.WitnessLongNoteId) targetLeakage++;
            if (holdoutKind == InteriorHoldoutKind.ParentOccurrence
                && donor.ParentLongNoteId == target.ParentLongNoteId)
                parentLeakage++;
            if (donor.WitnessLongNoteId == target.ParentLongNoteId) releaseLeakage++;
            if (donor.AnchorTime >= target.AnchorTime) futureLeakage++;
            if (donor.ParentLongNoteId == target.WitnessLongNoteId) sameEventLeakage++;
        }

        return new InteriorRelationHoldoutAudit(targetLeakage, parentLeakage, releaseLeakage,
            futureLeakage, sameEventLeakage, syntheticLeakage, crossChartLeakage);
    }

    public static InteriorRelationHoldoutAssessment Assess(InteriorRelationOccurrence target,
        IEnumerable<InteriorRelationHoldoutDonor> acceptedDonors)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(acceptedDonors);
        var donors = acceptedDonors.Select(x => x.Occurrence).ToArray();
        var distinct = donors.Select(x => x.RelationSignature).Distinct(StringComparer.Ordinal).Count();
        var joint = donors.Any(x => x.RelationSignature == target.RelationSignature);
        var duration = donors.Any(x => x.DurationFromAnchorBeats == target.DurationFromAnchorBeats);
        var endpoint = donors.Any(x => x.RelationClass == target.RelationClass
            && x.OffsetFromParentEndBeats == target.OffsetFromParentEndBeats);
        var state = joint ? distinct == 1 ? InteriorAlternativeState.ObservedUnique
                : InteriorAlternativeState.ObservedAmongAlternatives
            : donors.Length == 0 ? InteriorAlternativeState.NoObservedRelation
            : InteriorAlternativeState.ConflictingForSpecificClaim;
        return new InteriorRelationHoldoutAssessment(donors.Length, distinct, joint, duration, endpoint,
            !joint && duration && endpoint, state,
            donors.Select(x => x.OccurrenceId).Order(StringComparer.Ordinal).ToImmutableArray());
    }

    public static string ComputeCanonicalSemanticId(InteriorRelationOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        var semantic = $"{InteriorRelationFeasibilityResearch.SchemaVersion}|{occurrence.ChartFingerprint}|" +
            $"{occurrence.ParentLongNoteId}|{occurrence.AnchorTime}|{D(occurrence.AnchorBeat)}|" +
            $"{occurrence.WitnessLongNoteId}|{occurrence.RelationSignature}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(semantic))).ToLowerInvariant();
    }

    public static bool HasCanonicalSemanticId(InteriorRelationOccurrence occurrence) =>
        string.Equals(occurrence.OccurrenceId, ComputeCanonicalSemanticId(occurrence),
            StringComparison.Ordinal);

    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
