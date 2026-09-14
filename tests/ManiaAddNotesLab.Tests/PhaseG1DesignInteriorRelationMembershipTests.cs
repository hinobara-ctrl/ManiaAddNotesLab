using System.Collections.Immutable;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseG1DesignInteriorRelationMembershipTests
{
    [Fact]
    public void ExactCandidateIsObservedUnique()
    {
        var candidate = Candidate("R:A");
        var result = Evaluate(candidate, Entry(Occurrence(2, "R:A")));

        Assert.Equal(InteriorRelationMembershipState.CandidateObservedUnique, result.State);
        Assert.True(result.HypotheticalAdmit);
    }

    [Fact]
    public void ExactCandidateAmongAlternativesRemainsMemberUnderFrequencySkew()
    {
        var candidate = Candidate("R:B");
        var evidence = Enumerable.Repeat(Entry(Occurrence(2, "R:A")), 20)
            .Append(Entry(Occurrence(3, "R:B")));
        var result = InteriorRelationMembershipResearch.Evaluate(candidate, evidence);

        Assert.Equal(InteriorRelationMembershipState.CandidateObservedAmongAlternatives, result.State);
        Assert.True(result.HypotheticalAdmit);
        Assert.Equal(new[] { "R:A", "R:B" }, result.ObservedResultSignatures.ToArray());
    }

    [Fact]
    public void CandidateAbsentFromAlternativeSetAbstainsWithoutReplacement()
    {
        var result = Evaluate(Candidate("R:C"), Entry(Occurrence(2, "R:A")), Entry(Occurrence(3, "R:B")));

        Assert.Equal(InteriorRelationMembershipState.CandidateNotObserved, result.State);
        Assert.False(result.HypotheticalAdmit);
    }

    [Fact]
    public void MissingQueryAndUnresolvableIdentityAreSeparateAbstentions()
    {
        var noRelation = Evaluate(Candidate("R:A"));
        var unresolved = Evaluate(Candidate(null, resolvable: false), Entry(Occurrence(2, "R:A")));

        Assert.Equal(InteriorRelationMembershipState.NoObservedRelation, noRelation.State);
        Assert.Equal(InteriorRelationMembershipState.UnresolvableExactIdentity, unresolved.State);
        Assert.False(noRelation.HypotheticalAdmit);
        Assert.False(unresolved.HypotheticalAdmit);
    }

    [Theory]
    [InlineData(InteriorEndRelation.Contained, InteriorRelationClass.Contained)]
    [InlineData(InteriorEndRelation.EqualEnd, InteriorRelationClass.EqualEnd)]
    [InlineData(InteriorEndRelation.Crossing, InteriorRelationClass.Crossing)]
    public void ContainedEqualEndAndCrossingUseExactG10Identity(InteriorEndRelation endRelation,
        InteriorRelationClass relationClass)
    {
        var parent = Parent();
        var anchor = Anchor();
        var endBeat = endRelation switch { InteriorEndRelation.Contained => 5m,
            InteriorEndRelation.EqualEnd => 8m, _ => 9m };
        var release = new ReleaseCandidate(endBeat, (int)(endBeat * 500), 1, Evidence());
        var candidate = InteriorRelationMembershipResearch.Candidate(Chart, 7, parent, anchor, release);
        var occurrence = Occurrence(2, candidate.ResultSignature!, relationClass: relationClass) with
        { QuerySignature = candidate.QuerySignature };
        var result = Evaluate(candidate, Entry(occurrence));

        Assert.Equal(endRelation, candidate.EndRelation);
        Assert.Equal(InteriorRelationMembershipState.CandidateObservedUnique, result.State);
    }

    [Fact]
    public void MarginalFragmentsNeverCreateExactMembership()
    {
        var candidate = Candidate("R:joint");
        var durationOnly = Occurrence(2, "R:duration-only") with
        { DurationFromAnchorBeats = 2m, OffsetFromParentEndBeats = -2m };
        var endpointOnly = Occurrence(3, "R:endpoint-only") with
        { DurationFromAnchorBeats = 3m, OffsetFromParentEndBeats = -1m };
        var result = Evaluate(candidate, Entry(durationOnly), Entry(endpointOnly));

        Assert.Equal(InteriorRelationMembershipState.CandidateNotObserved, result.State);
        Assert.False(result.HypotheticalAdmit);
    }

    [Fact]
    public void SyntheticAndCrossChartEvidenceAreIgnoredRatherThanAuthority()
    {
        var candidate = Candidate("R:A");
        var result = Evaluate(candidate,
            new(Occurrence(2, "R:A"), IsSynthetic: true),
            Entry(Occurrence(3, "R:A", chart: "other-chart")));

        Assert.Equal(InteriorRelationMembershipState.NoObservedRelation, result.State);
        Assert.Equal(1, result.IgnoredSyntheticEvidenceCount);
        Assert.Equal(1, result.IgnoredCrossChartEvidenceCount);
    }

    [Fact]
    public void SupportProvenanceSeparatesSameOtherAndBothParents()
    {
        var candidate = Candidate("R:A");
        var same = Evaluate(candidate, Entry(Occurrence(1, "R:A")));
        var other = Evaluate(candidate, Entry(Occurrence(2, "R:A")));
        var both = Evaluate(candidate, Entry(Occurrence(1, "R:A")), Entry(Occurrence(2, "R:A")));

        Assert.Equal(InteriorRelationSupportProvenance.SameParentOnly, same.SupportProvenance);
        Assert.Equal(InteriorRelationSupportProvenance.OtherParentOnly, other.SupportProvenance);
        Assert.Equal(InteriorRelationSupportProvenance.Both, both.SupportProvenance);
    }

    [Fact]
    public void FullChartMembershipAllowsFutureOriginalEvidence()
    {
        var candidate = Candidate("R:A") with { AnchorTime = 1000 };
        var future = Occurrence(2, "R:A") with { AnchorTime = 9000 };
        var result = Evaluate(candidate, Entry(future));

        Assert.Equal(InteriorRelationMembershipState.CandidateObservedUnique, result.State);
        Assert.True(result.HypotheticalAdmit);
    }

    [Fact]
    public void EvaluatorHasNoSelectorWinnerOrArticulationFieldsAndUsesZeroRng()
    {
        var result = Evaluate(Candidate("R:A"), Entry(Occurrence(2, "R:A")));
        var names = typeof(InteriorRelationMembershipEvaluation).GetProperties()
            .Select(x => x.Name).ToArray();

        Assert.DoesNotContain(names, x => x.Contains("Select", StringComparison.OrdinalIgnoreCase)
            || x.Contains("Winner", StringComparison.OrdinalIgnoreCase)
            || x.Contains("Preferred", StringComparison.OrdinalIgnoreCase)
            || x.Contains("Articulation", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, result.RngCalls);
    }

    [Fact]
    public void CandidateSemanticIdAndEvaluationOrderingAreDeterministic()
    {
        var first = ExactCandidate();
        var second = ExactCandidate();
        var evidence = new[] { Entry(Occurrence(3, "R:B")), Entry(Occurrence(2, "R:A")) };

        Assert.Equal(first.CandidateId, second.CandidateId);
        var firstEvaluation = InteriorRelationMembershipResearch.Evaluate(first, evidence);
        var secondEvaluation = InteriorRelationMembershipResearch.Evaluate(second, evidence);
        Assert.Equal(firstEvaluation.State, secondEvaluation.State);
        Assert.Equal(firstEvaluation.ObservedResultSignatures.ToArray(),
            secondEvaluation.ObservedResultSignatures.ToArray());
        Assert.Equal(firstEvaluation.MatchingOccurrenceIds.ToArray(),
            secondEvaluation.MatchingOccurrenceIds.ToArray());
    }

    [Fact]
    public void AdmissionInvariantAuditorDetectsEveryForbiddenControl()
    {
        var clean = Trace();
        Assert.Equal(0, InteriorRelationMembershipResearch.AuditTrace(clean).Total);
        Assert.True(InteriorRelationMembershipResearch.AuditTrace(clean with
            { ReplacementCandidateId = "replacement" }).CandidateSubstitutionViolations > 0);
        Assert.True(InteriorRelationMembershipResearch.AuditTrace(clean with
            { EvaluatedCandidateId = "different" }).CandidateSubstitutionViolations > 0);
        Assert.True(InteriorRelationMembershipResearch.AuditTrace(clean with
            { UsedFuzzyIdentity = true }).FuzzyIdentityViolations > 0);
        Assert.True(InteriorRelationMembershipResearch.AuditTrace(clean with
            { JoinedMarginalFragments = true }).MarginalJoinViolations > 0);
        Assert.True(InteriorRelationMembershipResearch.AuditTrace(clean with
            { FrequencyWinnerIntroduced = true }).FrequencyAuthorityViolations > 0);
        Assert.True(InteriorRelationMembershipResearch.AuditTrace(clean with
            { ArticulationIntentCreated = true }).ArticulationFallbackViolations > 0);
        Assert.True(InteriorRelationMembershipResearch.AuditTrace(clean with
            { RngPositionAfter = 11 }).RngConsumptionViolations > 0);
    }

    private static InteriorRelationMembershipEvaluation Evaluate(InteriorRelationCandidateIdentity candidate,
        params InteriorRelationVocabularyEntry[] entries) =>
        InteriorRelationMembershipResearch.Evaluate(candidate, entries);

    private static InteriorRelationCandidateIdentity Candidate(string? result, bool resolvable = true) =>
        new("candidate", Chart, new OriginalObservationId(1), 0, 1000, 2m,
            InteriorAnchorKind.Head, 2000, 4m, InteriorEndRelation.Contained,
            Query, result, resolvable);

    private static InteriorRelationCandidateIdentity ExactCandidate()
    {
        var parent = Parent();
        return InteriorRelationMembershipResearch.Candidate(Chart, 7, parent, Anchor(),
            new ReleaseCandidate(5m, 2500, 1, Evidence()));
    }

    private static OriginalObservation Parent() => new(new OriginalObservationId(1), 1, 0,
        ManiaObjectType.LongNote, 0, 4000, 0m, 8m);
    private static InteriorStructuralAnchor Anchor() => new("anchor", new OriginalObservationId(1),
        1000, 2m, InteriorAnchorKind.Head, [], [], []);
    private static CandidateEvidence Evidence() => new(1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 0);
    private static InteriorRelationVocabularyEntry Entry(InteriorRelationOccurrence occurrence) => new(occurrence);
    private static InteriorRelationOccurrence Occurrence(int parent, string result,
        string chart = Chart, InteriorRelationClass relationClass = InteriorRelationClass.Contained) =>
        new($"occurrence-{parent}-{result}", chart, new OriginalObservationId(parent),
            new OriginalObservationId(parent + 10), 0, 1, 0, 4000, 500, 2000,
            0m, 8m, 1m, 4m, 3m, -4m, InteriorAnchorKind.Head, relationClass,
            false, true, true, Query, result);
    private static InteriorRelationAdmissionTrace Trace() =>
        new("candidate", "candidate", null, false, false, false, false, 10, 10);

    private const string Chart = "chart";
    private const string Query = "query";
}
