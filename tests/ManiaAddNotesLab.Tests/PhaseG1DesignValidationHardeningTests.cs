using System.Collections.Immutable;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseG1DesignValidationHardeningTests
{
    [Fact]
    public void SyntheticObjectInsideOriginalPopulationCannotChangeResearchInputsOrMembershipAggregate()
    {
        var original = Chart();
        var synthetic = ManiaObject.Ln(3, 1250, 3750, true, 999,
            AddedObjectOrigin.LnInteriorOpportunity);
        var contaminated = new ManiaChart
        {
            KeyCount = original.KeyCount,
            Lines = original.Lines,
            OriginalObjects = original.OriginalObjects.Take(2).Append(synthetic)
                .Concat(original.OriginalObjects.Skip(2)).ToArray(),
            TimingPoints = original.TimingPoints
        };

        var clean = InteriorRelationMembershipResearch.EvaluateCurrentCandidateUniverse(original);
        var hardened = InteriorRelationMembershipResearch.EvaluateCurrentCandidateUniverse(contaminated);

        Assert.Equal(1, hardened.IgnoredSyntheticObjectCount);
        Assert.Equal(clean.ChartFingerprint, hardened.ChartFingerprint);
        Assert.Equal(clean.CurrentOpportunityIds.ToArray(), hardened.CurrentOpportunityIds.ToArray());
        Assert.Equal(Project(clean), Project(hardened));
    }

    [Fact]
    public void ConstructionEvidenceProvenanceIsExactAndNeverChangesMembership()
    {
        var baseCandidate = Candidate() with
        {
            ConstructionEvidenceObservationIds = [new OriginalObservationId(12)]
        };
        var construction = Evaluate(baseCandidate, Occurrence(2));
        var independent = Evaluate(baseCandidate, Occurrence(3));
        var both = Evaluate(baseCandidate, Occurrence(2), Occurrence(3));

        Assert.Equal(InteriorRelationConstructionSupportProvenance.ConstructionEvidenceOnly,
            construction.ConstructionSupportProvenance);
        Assert.Equal(InteriorRelationConstructionSupportProvenance.IndependentEvidenceOnly,
            independent.ConstructionSupportProvenance);
        Assert.Equal(InteriorRelationConstructionSupportProvenance.ConstructionAndIndependent,
            both.ConstructionSupportProvenance);
        Assert.All(new[] { construction, independent, both }, x => Assert.True(x.HypotheticalAdmit));
    }

    [Fact]
    public void UnattributableConstructionRouteIsReportedRatherThanHeuristicallyMatched()
    {
        var candidate = Candidate() with { ConstructionEvidenceAttributable = false };
        var result = Evaluate(candidate, Occurrence(2));

        Assert.Equal(InteriorRelationMembershipState.CandidateObservedUnique, result.State);
        Assert.Equal(InteriorRelationConstructionSupportProvenance.Unattributable,
            result.ConstructionSupportProvenance);
    }

    [Fact]
    public void OpportunityAggregationIncludesZeroMixedAndAllAdmitWithoutChangingCandidates()
    {
        var candidates = ImmutableArray.Create(
            Evaluation("o1", true), Evaluation("o2", true), Evaluation("o2", false),
            Evaluation("o3", false));
        var corpus = new InteriorRelationMembershipCorpusResult("schema", "chart", 4, 4,
            ["o1", "o2", "o3", "o4"], candidates, 0, 0);

        var result = InteriorRelationMembershipResearch.SummarizeOpportunities(corpus);

        Assert.Equal(4, result.TotalOpportunities);
        Assert.Equal(2, result.ZeroAdmitted);
        Assert.Equal(2, result.AtLeastOneAdmitted);
        Assert.Equal(1, result.NoCandidateShapes);
        Assert.Equal(1, result.AllCandidatesAbstain);
        Assert.Equal(1, result.MixedAdmitAbstain);
        Assert.Equal(1, result.AllCandidatesAdmit);
        Assert.Equal(new Dictionary<int, int> { [0] = 2, [1] = 2 }, result.AdmittedShapeCountDistribution);
    }

    [Fact]
    public void MembershipResearchPublicApiIsStructurallyRngFreeAndRepeatable()
    {
        var publicMethods = typeof(InteriorRelationMembershipResearch).GetMethods()
            .Where(x => x.DeclaringType == typeof(InteriorRelationMembershipResearch)).ToArray();
        Assert.All(publicMethods.SelectMany(x => x.GetParameters()), parameter =>
            Assert.False(typeof(IRandomSource).IsAssignableFrom(parameter.ParameterType)));
        Assert.Equal("STRUCTURAL_NO_RNG_DEPENDENCY", InteriorRelationMembershipResearch.RngCertification);

        var first = Evaluate(Candidate(), Occurrence(2));
        var second = Evaluate(Candidate(), Occurrence(2));
        Assert.Equal(first.State, second.State);
        Assert.Equal(first.SupportProvenance, second.SupportProvenance);
        Assert.Equal(first.ConstructionSupportProvenance, second.ConstructionSupportProvenance);
        Assert.Equal(first.ObservedResultSignatures.ToArray(), second.ObservedResultSignatures.ToArray());
        Assert.Equal(first.MatchingOccurrenceIds.ToArray(), second.MatchingOccurrenceIds.ToArray());
    }

    private static string[] Project(InteriorRelationMembershipCorpusResult result) => result.Candidates
        .Select(x => $"{x.Candidate.CandidateId}|{x.State}|{x.SupportProvenance}|" +
            $"{x.ConstructionSupportProvenance}|{string.Join(';', x.ObservedResultSignatures)}")
        .ToArray();

    private static InteriorRelationMembershipEvaluation Evaluate(
        InteriorRelationCandidateIdentity candidate, params InteriorRelationOccurrence[] occurrences) =>
        InteriorRelationMembershipResearch.Evaluate(candidate,
            occurrences.Select(x => new InteriorRelationVocabularyEntry(x)));

    private static InteriorRelationMembershipEvaluation Evaluation(string opportunityId, bool admit)
    {
        var candidate = Candidate() with { OpportunityId = opportunityId };
        return Evaluate(candidate, admit ? [Occurrence(2)] : []);
    }

    private static InteriorRelationCandidateIdentity Candidate() => new("candidate", "chart",
        new OriginalObservationId(1), 0, 1000, 2m, InteriorAnchorKind.Head, 2000, 4m,
        InteriorEndRelation.Contained, "query", "result", true);

    private static InteriorRelationOccurrence Occurrence(int parent) => new($"occurrence-{parent}", "chart",
        new OriginalObservationId(parent), new OriginalObservationId(parent + 10), 0, 1, 0, 4000,
        1000, 2000, 0m, 8m, 2m, 4m, 2m, -4m, InteriorAnchorKind.Head,
        InteriorRelationClass.Contained, false, true, true, "query", "result");

    private static ManiaChart Chart()
    {
        var objects = new[]
        {
            ManiaObject.Ln(0, 0, 4000, sequence: 0),
            ManiaObject.Ln(1, 500, 3000, sequence: 1),
            ManiaObject.Ln(2, 1000, 2000, sequence: 2),
            ManiaObject.Ln(3, 1500, 2500, sequence: 3)
        };
        return new ManiaChart { KeyCount = 4, Lines = [], OriginalObjects = objects,
            TimingPoints = [new TimingPoint(0, 500)] };
    }
}
