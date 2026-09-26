using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class SelectionSetRemappingResearchTests
{
    [Fact]
    public void IdenticalOpportunitySequencesAreExact()
    {
        var control = Opportunities(("OP-A", 0, "C-A"), ("OP-B", 1, "C-B"));
        var comparison = SelectionSetOpportunityPairingResearch.Compare(control, control.ToArray());
        Assert.True(comparison.Equivalent);
        Assert.Equal(OpportunitySequenceDiagnostic.Exact, comparison.Diagnostic);
    }

    [Fact]
    public void MissingTreatmentOpportunityIsDiagnosedBeforePairing()
    {
        var control = Opportunities(("OP-A", 0, "C-A"), ("OP-B", 1, "C-B"));
        var treatment = Opportunities(("OP-A", 0, "T-A"));
        var comparison = SelectionSetOpportunityPairingResearch.Compare(control, treatment);
        Assert.Equal(OpportunitySequenceDiagnostic.TreatmentMissingOpportunity, comparison.Diagnostic);
        Assert.Equal(1, comparison.MismatchIndex);
        Assert.Throws<InvalidDataException>(() =>
            SelectionSetOpportunityPairingResearch.Pair(control, treatment, new HashSet<int>()));
    }

    [Fact]
    public void AdditionalTreatmentOpportunityIsDiagnosedBeforePairing()
    {
        var control = Opportunities(("OP-A", 0, "C-A"));
        var treatment = Opportunities(("OP-A", 0, "T-A"), ("OP-B", 1, "T-B"));
        var comparison = SelectionSetOpportunityPairingResearch.Compare(control, treatment);
        Assert.Equal(OpportunitySequenceDiagnostic.TreatmentAdditionalOpportunity, comparison.Diagnostic);
        Assert.Equal(1, comparison.MismatchIndex);
    }

    [Fact]
    public void SwappedTreatmentOpportunitiesAreDiagnosed()
    {
        var control = Opportunities(("OP-A", 0, "C-A"), ("OP-B", 1, "C-B"));
        var treatment = Opportunities(("OP-B", 1, "T-B"), ("OP-A", 0, "T-A"));
        Assert.Equal(OpportunitySequenceDiagnostic.OpportunityOrderSwapped,
            SelectionSetOpportunityPairingResearch.Compare(control, treatment).Diagnostic);
    }

    [Fact]
    public void SameLengthWithDifferentOpportunityKeysIsDiagnosed()
    {
        var control = Opportunities(("OP-A", 0, "C-A"), ("OP-B", 1, "C-B"));
        var treatment = Opportunities(("OP-A", 0, "T-A"), ("OP-X", 1, "T-X"));
        var comparison = SelectionSetOpportunityPairingResearch.Compare(control, treatment);
        Assert.Equal(OpportunitySequenceDiagnostic.OpportunityKeyMismatch, comparison.Diagnostic);
        Assert.Equal(1, comparison.MismatchIndex);
    }

    [Fact]
    public void EqualSequenceWithDifferentCommitsRemainsExact()
    {
        var control = Opportunities(("OP-A", 0, "CONTROL"));
        var treatment = Opportunities(("OP-A", 0, "TREATMENT"));
        Assert.Equal(OpportunitySequenceDiagnostic.Exact,
            SelectionSetOpportunityPairingResearch.Compare(control, treatment).Diagnostic);
    }

    [Fact]
    public void CorrectlyValidatedPairingPreservesBothCommitsAndGuarantee()
    {
        var control = Opportunities(("OP-A", 0, "CONTROL"));
        var treatment = Opportunities(("OP-A", 0, "TREATMENT"));
        var result = SelectionSetOpportunityPairingResearch.Pair(control, treatment,
            new HashSet<int> { 0 });
        var pair = Assert.Single(result.Pairs);
        Assert.True(result.SequenceComparison.Equivalent);
        Assert.Equal("CONTROL", pair.ControlCommitIdentity);
        Assert.Equal("TREATMENT", pair.TreatmentCommitIdentity);
        Assert.True(pair.CanonicalAuthorityChangedCommit);
    }

    [Fact]
    public void DifferentSetsCanKeepTheSameSelectionAndMustAbstain()
    {
        var evidence = Evidence([0, 1, 2], [0, 1], sameSelection: true);
        var result = SelectionSetRemappingResearch.Assess(evidence);
        Assert.False(result.Demonstrated);
        Assert.Contains("selection_unchanged", result.FailedCriteria);
    }

    [Fact]
    public void DifferentSetsCanRemapWhileControlLaneRemainsCanonical()
    {
        var result = SelectionSetRemappingResearch.Assess(Evidence([0, 1, 2, 3, 6], [0, 1, 2, 3]));
        Assert.True(result.Demonstrated);
        Assert.Equal(SelectionSetRemappingResearch.ExperimentalClass, "E_SELECTION_SET_REMAP");
    }

    [Fact]
    public void CanonicallyRejectedControlLaneIsNotE()
    {
        var evidence = Evidence([0, 1, 2], [0, 1]) with
        {
            CanonicalSelection = ReplaySelecting([0, 1], lane => lane != 2),
            LegacySelection = ReplaySelecting([0, 1, 2], lane => lane == 2)
        };
        var result = SelectionSetRemappingResearch.Assess(evidence);
        Assert.False(result.Demonstrated);
        Assert.Contains("control_lane_rejected", result.FailedCriteria);
    }

    [Fact]
    public void CardinalityCanChangeWithoutChangingSelection()
    {
        var result = SelectionSetRemappingResearch.Assess(Evidence([0, 1, 2, 3], [0, 1, 2], true));
        Assert.Contains("selection_unchanged", result.FailedCriteria);
    }

    [Fact]
    public void OrderAloneCanRemapAtEqualCardinality()
    {
        var evidence = Evidence([0, 1, 2, 3], [3, 2, 1, 0]);
        Assert.Equal(evidence.LegacySelection.SelectionMaximum, evidence.CanonicalSelection.SelectionMaximum);
        Assert.True(SelectionSetRemappingResearch.Assess(evidence).Demonstrated);
    }

    [Fact]
    public void EqualCardinalityDifferentContentCanRemap()
    {
        var evidence = Evidence([0, 1, 2], [1, 0, 3]);
        Assert.Equal(evidence.LegacySelection.SelectionMaximum, evidence.CanonicalSelection.SelectionMaximum);
        Assert.True(SelectionSetRemappingResearch.Assess(evidence).Demonstrated);
    }

    [Fact]
    public void EmptyCanonicalSetCannotInvokeTheAuthenticSelector()
    {
        Assert.Throws<ArgumentException>(() => SelectionSetRemappingResearch.Replay(1, [], []));
    }

    [Fact]
    public void SameRngConsumptionCanProduceDifferentSelection()
    {
        var evidence = Evidence([0, 1, 2, 3, 6], [0, 1, 2, 3]);
        Assert.NotEqual(evidence.LegacySelection.SelectedLane, evidence.CanonicalSelection.SelectedLane);
        Assert.Equal(evidence.LegacySelection.PrefixSha256, evidence.CanonicalSelection.PrefixSha256);
        Assert.Equal(evidence.LegacySelection.FinalCallCount, evidence.CanonicalSelection.FinalCallCount);
    }

    [Fact]
    public void DifferentRngConsumptionMustAbstain()
    {
        var evidence = Evidence([0, 1, 2, 3, 6], [0, 1, 2, 3]);
        evidence = evidence with
        {
            CanonicalSelection = evidence.CanonicalSelection with
                { FinalCallCount = evidence.CanonicalSelection.FinalCallCount + 1 }
        };
        Assert.Contains("rng_state_or_consumption_mismatch",
            SelectionSetRemappingResearch.Assess(evidence).FailedCriteria);
    }

    [Fact]
    public void IncompleteOrUnequalParentsMustAbstain()
    {
        var evidence = Evidence([0, 1, 2, 3, 6], [0, 1, 2, 3]);
        evidence = evidence with
        {
            TreatmentParent = State("OTHER", 0),
            ControlParent = evidence.ControlParent with { CompleteForCausalLineage = false }
        };
        Assert.Contains("parents_not_equivalent_complete",
            SelectionSetRemappingResearch.Assess(evidence).FailedCriteria);
    }

    [Fact]
    public void MateriallyEqualSuccessorsMustAbstain()
    {
        var evidence = Evidence([0, 1, 2, 3, 6], [0, 1, 2, 3]);
        evidence = evidence with { TreatmentSuccessor = evidence.ControlSuccessor };
        Assert.Contains("successors_not_materially_different_complete",
            SelectionSetRemappingResearch.Assess(evidence).FailedCriteria);
    }

    [Fact]
    public void ExperimentalEAndLaterDirectAStaySeparate()
    {
        var evidence = Evidence([0, 1, 2, 3, 6], [0, 1, 2, 3]);
        var e = SelectionSetRemappingResearch.Assess(evidence);
        var later = Assert.Single(SafetyRemediationGateHardeningResearch.Classify("pair",
        [
            new("LATER", 1, State("LEFT", 1), State("RIGHT", 1), State("L2", 2),
                State("R2", 2), "REJECTED", null, true)
        ]));
        Assert.True(e.Demonstrated);
        Assert.False(later.DirectGovernedDivergence);
        Assert.True(later.UnexplainedStateDivergence);
    }

    [Fact]
    public void ExperimentalECanBeFollowedByCompleteReconvergence()
    {
        var equal = State("EQUAL", 0); var left = State("LEFT", 1); var right = State("RIGHT", 1);
        var reconverged = State("RECONVERGED", 2);
        var steps = SafetyRemediationGateHardeningResearch.Classify("pair",
        [
            new("E", 0, equal, equal, left, right, "L", "R", false),
            new("R", 1, left, right, reconverged, reconverged, null, null, false)
        ]);
        Assert.True(steps[0].UnexplainedStateDivergence);
        Assert.True(steps[1].AfterEqual);
    }

    [Fact]
    public void LaterIndependentCauseDoesNotBecomePartOfEAutomatically()
    {
        var evidence = Evidence([0, 1, 2, 3, 6], [0, 1, 2, 3]);
        var concurrent = evidence with { NoConcurrentSelectionIntervention = false };
        Assert.True(SelectionSetRemappingResearch.Assess(evidence).Demonstrated);
        Assert.Contains("concurrent_intervention",
            SelectionSetRemappingResearch.Assess(concurrent).FailedCriteria);
    }

    [Fact]
    public void SameChartSeedAcrossDifferentExperimentalConfigurationsMustNotBeMerged()
    {
        var evidence = Evidence([0, 1, 2, 3, 6], [0, 1, 2, 3]) with
            { TreatmentConfigurationIdentity = "secondary-g1" };
        Assert.Contains("configuration_mismatch",
            SelectionSetRemappingResearch.Assess(evidence).FailedCriteria);
    }

    private static SelectionSetRemappingEvidence Evidence(int[] legacy, int[] canonical,
        bool sameSelection = false)
    {
        var seed = Enumerable.Range(0, 10000).First(candidate =>
        {
            var left = SelectionSetRemappingResearch.Replay(candidate, [], legacy);
            var right = SelectionSetRemappingResearch.Replay(candidate, [], canonical);
            return canonical.Contains(left.SelectedLane)
                && (sameSelection ? left.SelectedLane == right.SelectedLane
                    : left.SelectedLane != right.SelectedLane);
        });
        var parent = State("PARENT", 0);
        return new("OP", "OP", parent, parent, State("LEFT", 1), State("RIGHT", 1),
            "same-comparable-configuration", "same-comparable-configuration",
            SelectionSetRemappingResearch.Replay(seed, [], legacy),
            SelectionSetRemappingResearch.Replay(seed, [], canonical), true);
    }

    private static SelectionReplayResult ReplaySelecting(int[] lanes, Func<int, bool> predicate)
    {
        var seed = Enumerable.Range(0, 10000).First(x =>
            predicate(SelectionSetRemappingResearch.Replay(x, [], lanes).SelectedLane));
        return SelectionSetRemappingResearch.Replay(seed, [], lanes);
    }

    private static SafetyRemediationGateSufficientState State(string value, int cursor) =>
        new(value, value + "-M", value + "-L", cursor, cursor, cursor, value + "-P", true);

    private static SafetyRemediationGateOpportunityState[] Opportunities(
        params (string Key, int Order, string Commit)[] values) => values.Select(x =>
            new SafetyRemediationGateOpportunityState(x.Key, x.Order,
                State($"{x.Key}-BEFORE", x.Order), State($"{x.Key}-AFTER", x.Order + 1), x.Commit))
            .ToArray();
}
