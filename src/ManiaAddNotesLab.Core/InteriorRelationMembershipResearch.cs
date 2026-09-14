using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ManiaAddNotesLab.Core;

public enum InteriorRelationMembershipState
{
    CandidateObservedUnique,
    CandidateObservedAmongAlternatives,
    CandidateNotObserved,
    NoObservedRelation,
    UnresolvableExactIdentity
}

public enum InteriorRelationSupportProvenance
{
    SameParentOnly,
    OtherParentOnly,
    Both,
    None
}

public sealed record InteriorRelationVocabularyEntry(
    InteriorRelationOccurrence Occurrence,
    bool IsSynthetic = false);

public sealed record InteriorRelationCandidateIdentity(
    string CandidateId,
    string ChartFingerprint,
    OriginalObservationId ParentLongNoteId,
    int ParentLane,
    int AnchorTime,
    decimal AnchorBeat,
    InteriorAnchorKind AnchorKind,
    int CandidateEndTime,
    decimal CandidateEndBeat,
    InteriorEndRelation EndRelation,
    string QuerySignature,
    string? ResultSignature,
    bool ExactlyResolvable);

public sealed record InteriorRelationMembershipEvaluation(
    InteriorRelationCandidateIdentity Candidate,
    InteriorRelationMembershipState State,
    InteriorRelationSupportProvenance SupportProvenance,
    ImmutableArray<string> ObservedResultSignatures,
    ImmutableArray<string> MatchingOccurrenceIds,
    int SameParentSupportCount,
    int OtherParentSupportCount,
    int IgnoredSyntheticEvidenceCount,
    int IgnoredCrossChartEvidenceCount,
    int RngCalls)
{
    public bool HypotheticalAdmit => State is InteriorRelationMembershipState.CandidateObservedUnique
        or InteriorRelationMembershipState.CandidateObservedAmongAlternatives;
}

public sealed record InteriorRelationMembershipCorpusResult(
    string SchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    int CurrentInteriorOpportunityCount,
    ImmutableArray<InteriorRelationMembershipEvaluation> Candidates,
    int ResearchRngCalls);

public sealed record InteriorRelationAdmissionTrace(
    string ProposedCandidateId,
    string EvaluatedCandidateId,
    string? ReplacementCandidateId,
    bool UsedFuzzyIdentity,
    bool JoinedMarginalFragments,
    bool FrequencyWinnerIntroduced,
    bool ArticulationIntentCreated,
    int RngPositionBefore,
    int RngPositionAfter);

public sealed record InteriorRelationAdmissionInvariantAudit(
    int CandidateSubstitutionViolations,
    int FuzzyIdentityViolations,
    int MarginalJoinViolations,
    int FrequencyAuthorityViolations,
    int ArticulationFallbackViolations,
    int RngConsumptionViolations)
{
    public int Total => CandidateSubstitutionViolations + FuzzyIdentityViolations
        + MarginalJoinViolations + FrequencyAuthorityViolations + ArticulationFallbackViolations
        + RngConsumptionViolations;
}

/// <summary>
/// G1.DESIGN shadow-only exact membership model. It never chooses a relation and has no engine callsite.
/// </summary>
public static class InteriorRelationMembershipResearch
{
    public const string SchemaVersion = "g1-design-interior-relation-membership-shadow.1";

    public static InteriorRelationMembershipEvaluation Evaluate(
        InteriorRelationCandidateIdentity candidate,
        IEnumerable<InteriorRelationVocabularyEntry> vocabulary)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(vocabulary);
        var entries = vocabulary.ToArray();
        var ignoredSynthetic = entries.Count(x => x.IsSynthetic);
        var ignoredCrossChart = entries.Count(x => !x.IsSynthetic && !string.Equals(
            x.Occurrence.ChartFingerprint, candidate.ChartFingerprint, StringComparison.Ordinal));
        var eligible = entries.Where(x => !x.IsSynthetic && string.Equals(x.Occurrence.ChartFingerprint,
            candidate.ChartFingerprint, StringComparison.Ordinal)).Select(x => x.Occurrence).ToArray();

        var observed = eligible.Where(x => x.QuerySignature == candidate.QuerySignature)
            .OrderBy(x => x.OccurrenceId, StringComparer.Ordinal).ToArray();
        var results = observed.Select(x => x.RelationSignature).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToImmutableArray();
        var matching = candidate.ExactlyResolvable && candidate.ResultSignature is not null
            ? observed.Where(x => x.RelationSignature == candidate.ResultSignature).ToArray()
            : [];
        var sameParent = matching.Count(x => x.ParentLongNoteId == candidate.ParentLongNoteId);
        var otherParent = matching.Length - sameParent;
        var provenance = sameParent > 0 && otherParent > 0 ? InteriorRelationSupportProvenance.Both
            : sameParent > 0 ? InteriorRelationSupportProvenance.SameParentOnly
            : otherParent > 0 ? InteriorRelationSupportProvenance.OtherParentOnly
            : InteriorRelationSupportProvenance.None;
        var state = !candidate.ExactlyResolvable || candidate.ResultSignature is null
            ? InteriorRelationMembershipState.UnresolvableExactIdentity
            : results.Length == 0 ? InteriorRelationMembershipState.NoObservedRelation
            : matching.Length == 0 ? InteriorRelationMembershipState.CandidateNotObserved
            : results.Length == 1 ? InteriorRelationMembershipState.CandidateObservedUnique
            : InteriorRelationMembershipState.CandidateObservedAmongAlternatives;

        return new InteriorRelationMembershipEvaluation(candidate, state, provenance, results,
            matching.Select(x => x.OccurrenceId).Order(StringComparer.Ordinal).ToImmutableArray(),
            sameParent, otherParent, ignoredSynthetic, ignoredCrossChart, 0);
    }

    public static InteriorRelationMembershipCorpusResult EvaluateCurrentCandidateUniverse(ManiaChart source,
        AddNotesOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        options ??= CurrentOptions();
        var census = InteriorRelationFeasibilityResearch.Evaluate(source, options);
        var profile = MapperEvidenceProfileBuilder.Build(source);
        var timeline = new BeatTimeline(source.TimingPoints);
        var engine = new AddNotesEngine();
        var observations = profile.Observations.ToDictionary(x => x.Id);
        var objects = profile.Observations.ToDictionary(x => x.Id,
            x => source.OriginalObjects[x.SourceSequence]);
        var structural = census.StructuralAnchors.ToDictionary(x => x.AnchorId, StringComparer.Ordinal);
        var vocabulary = census.CompleteRelations.Select(x => new InteriorRelationVocabularyEntry(x)).ToArray();
        var evaluations = new List<InteriorRelationMembershipEvaluation>();

        foreach (var gate in census.CurrentGateAudit.Where(x => x.CurrentOpportunity)
            .OrderBy(x => x.ParentLongNoteId.Value).ThenBy(x => x.AnchorTime))
        {
            var anchor = structural[gate.AnchorId];
            var parent = observations[gate.ParentLongNoteId];
            var parentObject = objects[gate.ParentLongNoteId];
            var context = engine.BuildOriginalInteriorLnContext(parentObject, anchor.AnchorTime,
                source.OriginalObjects, timeline, options.LnWindowBeats,
                options.InteriorMinimumContextLnCount);
            var virtualSource = ManiaObject.Ln(parent.Lane, anchor.AnchorTime, parent.EndTime!.Value,
                sequence: parent.SourceSequence, origin: AddedObjectOrigin.LnInteriorOpportunity);
            var build = engine.BuildReleaseCandidatesForResearch(virtualSource, context, timeline,
                options.LnWindowBeats, options.SourceAffinityMultiplier, options.DistanceDecayPerBeat,
                options.MinimumDistanceWeight, applySourceAffinity: false,
                mapRelativeSnapEnabled: options.MapRelativeSnapEnabled);
            foreach (var release in build.Candidates.OrderBy(x => x.EndTime))
            {
                var candidate = Candidate(profile.ChartFingerprint, source.KeyCount, parent, anchor, release);
                evaluations.Add(Evaluate(candidate, vocabulary));
            }
        }

        return new InteriorRelationMembershipCorpusResult(SchemaVersion, profile.ChartFingerprint,
            source.KeyCount, census.CurrentInteriorOpportunityCount,
            evaluations.OrderBy(x => x.Candidate.CandidateId, StringComparer.Ordinal).ToImmutableArray(), 0);
    }

    public static InteriorRelationCandidateIdentity Candidate(string chartFingerprint, int keyCount,
        OriginalObservation parent, InteriorStructuralAnchor anchor, ReleaseCandidate release)
    {
        var relation = release.EndBeat <= anchor.AnchorBeat ? InteriorEndRelation.Invalid
            : release.EndBeat < parent.EndBeat ? InteriorEndRelation.Contained
            : release.EndBeat == parent.EndBeat ? InteriorEndRelation.EqualEnd
            : InteriorEndRelation.Crossing;
        var resolvable = release.EndTime > anchor.AnchorTime
            && relation != InteriorEndRelation.Invalid
            && relation != InteriorEndRelation.NotApplicable;
        var query = InteriorRelationFeasibilityResearch.CanonicalQuerySignature(keyCount,
            parent.DurationBeats, anchor.AnchorBeat - parent.StartBeat, anchor.AnchorKind);
        var result = resolvable ? InteriorRelationFeasibilityResearch.CanonicalRelationSignature(
            ToRelationClass(relation), release.EndBeat - anchor.AnchorBeat,
            release.EndBeat - parent.EndBeat) : null;
        var semantic = $"{SchemaVersion}|{chartFingerprint}|{parent.Id}|{anchor.AnchorTime}|" +
            $"{D(anchor.AnchorBeat)}|{anchor.AnchorKind}|{release.EndTime}|{D(release.EndBeat)}|{query}|{result}";
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(semantic))).ToLowerInvariant();
        return new InteriorRelationCandidateIdentity(id, chartFingerprint, parent.Id, parent.Lane,
            anchor.AnchorTime, anchor.AnchorBeat, anchor.AnchorKind, release.EndTime, release.EndBeat,
            relation, query, result, resolvable);
    }

    public static InteriorRelationAdmissionInvariantAudit AuditTrace(InteriorRelationAdmissionTrace trace) =>
        new(trace.ProposedCandidateId == trace.EvaluatedCandidateId && trace.ReplacementCandidateId is null ? 0 : 1,
            trace.UsedFuzzyIdentity ? 1 : 0, trace.JoinedMarginalFragments ? 1 : 0,
            trace.FrequencyWinnerIntroduced ? 1 : 0, trace.ArticulationIntentCreated ? 1 : 0,
            trace.RngPositionBefore == trace.RngPositionAfter ? 0 : 1);

    public static AddNotesOptions CurrentOptions() => new()
    {
        Chance = 0,
        InteriorLnOpportunitiesEnabled = true,
        MaxInteriorOpportunitiesPerSource = 2,
        InteriorMinimumSourceBeats = 3,
        InteriorLengthRatio = 1.5,
        InteriorAbsoluteLongBeats = 8,
        InteriorMinimumContextLnCount = 3,
        InteriorMinimumSupportedAnchors = 2,
        LnWindowBeats = 4,
        InteriorEligibilityMode = InteriorEligibilityMode.AnchorSupported,
        InteriorContextMode = InteriorContextMode.OriginalOnly
    };

    private static InteriorRelationClass ToRelationClass(InteriorEndRelation value) => value switch
    {
        InteriorEndRelation.Contained => InteriorRelationClass.Contained,
        InteriorEndRelation.EqualEnd => InteriorRelationClass.EqualEnd,
        InteriorEndRelation.Crossing => InteriorRelationClass.Crossing,
        _ => InteriorRelationClass.Invalid
    };

    private static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
