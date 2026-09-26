using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class SelectionSetRemappingResearchTests
{
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
}
