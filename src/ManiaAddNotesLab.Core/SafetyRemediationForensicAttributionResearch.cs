using System.Collections.Immutable;

namespace ManiaAddNotesLab.Core;

public enum ForensicStateDifferenceKind
{
    None,
    MaterialComplete,
    EqualHashIncomplete,
    DifferentHashIncomplete
}

public sealed record ForensicStateDifference(
    ForensicStateDifferenceKind Kind,
    ImmutableArray<string> DifferentFields);

public sealed record UnattributedDifferenceEpisode(
    int FirstOpportunityOrder,
    string FirstOpportunityKey,
    int LastOpportunityOrder,
    string LastOpportunityKey,
    int ObservationCount);

public static class SafetyRemediationForensicAttributionResearch
{
    public static ForensicStateDifference Compare(
        SafetyRemediationGateSufficientState control,
        SafetyRemediationGateSufficientState treatment)
    {
        var fields = ImmutableArray.CreateBuilder<string>();
        if (control.MaterializedGenerationStateHash != treatment.MaterializedGenerationStateHash)
            fields.Add(nameof(control.MaterializedGenerationStateHash));
        if (control.LatentCommittedGeometryHash != treatment.LatentCommittedGeometryHash)
            fields.Add(nameof(control.LatentCommittedGeometryHash));
        if (control.RootRngPosition != treatment.RootRngPosition) fields.Add(nameof(control.RootRngPosition));
        if (control.StageRngPosition != treatment.StageRngPosition) fields.Add(nameof(control.StageRngPosition));
        if (control.OpportunityCursor != treatment.OpportunityCursor) fields.Add(nameof(control.OpportunityCursor));
        if (control.PendingArticulationHash != treatment.PendingArticulationHash)
            fields.Add(nameof(control.PendingArticulationHash));
        if (control.CompleteForCausalLineage != treatment.CompleteForCausalLineage)
            fields.Add(nameof(control.CompleteForCausalLineage));
        var complete = control.CompleteForCausalLineage && treatment.CompleteForCausalLineage;
        var hashEqual = control.Hash == treatment.Hash;
        var kind = complete
            ? hashEqual ? ForensicStateDifferenceKind.None : ForensicStateDifferenceKind.MaterialComplete
            : hashEqual ? ForensicStateDifferenceKind.EqualHashIncomplete
                : ForensicStateDifferenceKind.DifferentHashIncomplete;
        return new(kind, fields.ToImmutable());
    }

    public static ImmutableArray<UnattributedDifferenceEpisode> GroupEpisodes(
        IReadOnlyList<SafetyRemediationGateLineageStep> steps)
    {
        var result = ImmutableArray.CreateBuilder<UnattributedDifferenceEpisode>();
        var index = 0;
        while (index < steps.Count)
        {
            if (!steps[index].UnexplainedStateDivergence) { index++; continue; }
            var first = index;
            while (index + 1 < steps.Count && steps[index + 1].UnexplainedStateDivergence) index++;
            result.Add(new(steps[first].OpportunityOrder, steps[first].OpportunityKey,
                steps[index].OpportunityOrder, steps[index].OpportunityKey, index - first + 1));
            index++;
        }
        return result.ToImmutable();
    }
}
