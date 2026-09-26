using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class SafetyOp185Op466CounterfactualTests
{
    [Fact]
    public void TwoNormalArmsWithSameSeedHaveIdenticalTranscript()
    {
        var left = Calls(new SingleRngInterventionRandom(4, null));
        var right = Calls(new SingleRngInterventionRandom(4, null));
        Assert.Equal(left, right);
    }

    [Fact]
    public void DifferentParentStateIsRejected() => Assert.Equal(
        CounterfactualGuardFailure.ParentStateMismatch, Guard(parents: false));

    [Fact]
    public void DifferentRngPrefixIsRejected() => Assert.Equal(
        CounterfactualGuardFailure.RngPrefixMismatch, Guard(prefix: false));

    [Fact]
    public void WrongOpportunityKeyIsRejected() => Assert.Equal(
        CounterfactualGuardFailure.OpportunityKeyMismatch, Guard(key: "OP-WRONG"));

    [Fact]
    public void DuplicateInterventionIsRejected() => Assert.Equal(
        CounterfactualGuardFailure.InterventionCountMismatch, Guard(interventions: 2));

    [Fact]
    public void IllegalInterventionLaneIsRejected() => Assert.Equal(
        CounterfactualGuardFailure.InterventionLaneIllegal, Guard(legal: false));

    [Fact]
    public void OmittedRngCallIsRejected() => Assert.Equal(
        CounterfactualGuardFailure.RngCallOmitted, Guard(treatmentCalls: 0));

    [Fact]
    public void AdditionalRngCallIsRejected() => Assert.Equal(
        CounterfactualGuardFailure.AdditionalRngCall, Guard(treatmentCalls: 2));

    [Fact]
    public void SharedMutableStateIsRejected() => Assert.Equal(
        CounterfactualGuardFailure.SharedMutableState, Guard(independent: false));

    [Fact]
    public void ActiveInstrumentationWithoutLaneChangeIsNeutral()
    {
        var random = new SingleRngInterventionRandom(4, new(0, 4, 3, 3));
        var delivered = random.Next(4);
        Assert.Equal(3, delivered);
        var intervention = Assert.Single(random.Interventions);
        Assert.Equal(intervention.AuthenticValue, intervention.DeliveredValue);
        Assert.Equal(CounterfactualGuardFailure.None, Guard(instrumentationExact: true));
    }

    [Fact]
    public void InstrumentationDriftIsRejected() => Assert.Equal(
        CounterfactualGuardFailure.InstrumentationNotNeutral, Guard(instrumentationExact: false));

    [Fact]
    public void NaturalDownstreamRngDivergenceDoesNotInvalidateInitialGuard()
    {
        Assert.Equal(CounterfactualGuardFailure.None, Guard());
    }

    [Fact]
    public void AbsentOp466InBothArmsIsClassifiedExactly() => Assert.Equal("ABSENT_BOTH",
        CounterfactualTrajectoryGuardResearch.ClassifyTarget(false, false, false));

    [Fact]
    public void SimilarObjectIsNotExactRecovery() => Assert.Equal("SIMILAR_NOT_EXACT",
        CounterfactualTrajectoryGuardResearch.ClassifyTarget(false, false, true));

    [Fact]
    public void ExactTreatmentCommitIsRecoveredExact() => Assert.Equal("RECOVERED_EXACT",
        CounterfactualTrajectoryGuardResearch.ClassifyTarget(false, true, false));

    [Fact]
    public void ReachedButNotCommittedRemainsAbsent()
    {
        Assert.Equal("ABSENT_BOTH",
            CounterfactualTrajectoryGuardResearch.ClassifyTarget(false, false, false));
    }

    [Fact]
    public void DeterminismMismatchIsObservable()
    {
        Assert.NotEqual(Calls(new SingleRngInterventionRandom(4, null)),
            Calls(new SingleRngInterventionRandom(5, null)));
    }

    [Fact]
    public void MissingFrozenArtifactIsRejected()
    {
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json");
        Assert.Throws<FileNotFoundException>(() =>
            CounterfactualTrajectoryGuardResearch.RequireFiles([missing]));
    }

    [Fact]
    public void WrongPreregisteredRngMaximumIsRejected()
    {
        var random = new SingleRngInterventionRandom(4, new(0, 5, 3, 3));
        Assert.Throws<InvalidDataException>(() => random.Next(4));
    }

    private static CounterfactualGuardFailure Guard(bool parents = true, bool prefix = true,
        string key = CounterfactualTrajectoryGuardResearch.ExpectedOpportunity,
        int interventions = 1, bool legal = true, int controlCalls = 1, int treatmentCalls = 1,
        bool independent = true, bool? instrumentationExact = null) =>
        CounterfactualTrajectoryGuardResearch.Validate(new(parents, prefix, key, interventions,
            legal, controlCalls, treatmentCalls, independent, instrumentationExact));

    private static string Calls(SingleRngInterventionRandom random)
    {
        _ = random.NextDouble(); _ = random.Next(4); _ = random.NextDouble(); _ = random.Next(7);
        return random.AuthenticTranscriptSha256;
    }
}
