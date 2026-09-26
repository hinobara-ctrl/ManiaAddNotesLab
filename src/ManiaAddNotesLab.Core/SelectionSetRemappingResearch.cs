using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace ManiaAddNotesLab.Core;

public enum SelectionRandomCallKind { Double, Integer }

public sealed record SelectionRandomCall(
    SelectionRandomCallKind Kind, int? Maximum, double? DoubleValue, int? IntegerValue);

public sealed record SelectionReplayResult(
    ImmutableArray<int> OrderedLanes,
    int Seed,
    int PrefixCallCount,
    string PrefixSha256,
    int SelectionMaximum,
    int SelectedIndex,
    int SelectedLane,
    int SelectionRngCalls,
    int FinalCallCount);

public enum SelectionSetRemappingDisposition
{
    ESelectionSetRemap,
    AbstainParentsNotEquivalentComplete,
    AbstainOpportunityMismatch,
    AbstainConfigurationMismatch,
    AbstainNoCanonicalSetIntervention,
    AbstainControlLaneRejected,
    AbstainSelectionUnchanged,
    AbstainRngStateOrConsumptionMismatch,
    AbstainConcurrentIntervention,
    AbstainSuccessorsNotMateriallyDifferentComplete
}

public sealed record SelectionSetRemappingEvidence(
    string ControlOpportunityKey,
    string TreatmentOpportunityKey,
    SafetyRemediationGateSufficientState ControlParent,
    SafetyRemediationGateSufficientState TreatmentParent,
    SafetyRemediationGateSufficientState ControlSuccessor,
    SafetyRemediationGateSufficientState TreatmentSuccessor,
    string ControlConfigurationIdentity,
    string TreatmentConfigurationIdentity,
    SelectionReplayResult LegacySelection,
    SelectionReplayResult CanonicalSelection,
    bool NoConcurrentSelectionIntervention);

public sealed record SelectionSetRemappingAssessment(
    SelectionSetRemappingDisposition Disposition,
    ImmutableArray<string> FailedCriteria)
{
    public bool Demonstrated => Disposition == SelectionSetRemappingDisposition.ESelectionSetRemap;
}

public enum OpportunitySequenceDiagnostic
{
    Exact,
    TreatmentMissingOpportunity,
    TreatmentAdditionalOpportunity,
    OpportunityOrderSwapped,
    OpportunityKeyMismatch,
    OpportunityOrderMismatch
}

public sealed record OpportunitySequenceComparison(
    OpportunitySequenceDiagnostic Diagnostic,
    int? MismatchIndex,
    string? ControlOpportunityKey,
    string? TreatmentOpportunityKey,
    int? ControlOpportunityOrder,
    int? TreatmentOpportunityOrder)
{
    public bool Equivalent => Diagnostic == OpportunitySequenceDiagnostic.Exact;

    public void RequireEquivalent()
    {
        if (Equivalent) return;
        throw new InvalidDataException($"Control/treatment opportunity sequence mismatch: {Diagnostic}"
            + (MismatchIndex.HasValue ? $" at index {MismatchIndex.Value}" : string.Empty) + ".");
    }
}

public sealed record ValidatedOpportunityPairing(
    OpportunitySequenceComparison SequenceComparison,
    ImmutableArray<SafetyRemediationGatePairedOpportunity> Pairs);

public static class SelectionSetOpportunityPairingResearch
{
    public static OpportunitySequenceComparison Compare(
        IReadOnlyList<SafetyRemediationGateOpportunityState> control,
        IReadOnlyList<SafetyRemediationGateOpportunityState> treatment)
    {
        var shared = Math.Min(control.Count, treatment.Count);
        for (var index = 0; index < shared; index++)
        {
            var left = control[index];
            var right = treatment[index];
            if (left.OpportunityKey == right.OpportunityKey
                && left.OpportunityOrder == right.OpportunityOrder) continue;

            var controlIdentities = control.Select(Identity).Order(StringComparer.Ordinal).ToArray();
            var treatmentIdentities = treatment.Select(Identity).Order(StringComparer.Ordinal).ToArray();
            var swapped = control.Count == treatment.Count
                && controlIdentities.SequenceEqual(treatmentIdentities, StringComparer.Ordinal);
            var diagnostic = swapped ? OpportunitySequenceDiagnostic.OpportunityOrderSwapped
                : left.OpportunityKey != right.OpportunityKey
                    ? OpportunitySequenceDiagnostic.OpportunityKeyMismatch
                    : OpportunitySequenceDiagnostic.OpportunityOrderMismatch;
            return new(diagnostic, index, left.OpportunityKey, right.OpportunityKey,
                left.OpportunityOrder, right.OpportunityOrder);
        }

        if (control.Count > treatment.Count)
        {
            var missing = control[shared];
            return new(OpportunitySequenceDiagnostic.TreatmentMissingOpportunity, shared,
                missing.OpportunityKey, null, missing.OpportunityOrder, null);
        }
        if (treatment.Count > control.Count)
        {
            var additional = treatment[shared];
            return new(OpportunitySequenceDiagnostic.TreatmentAdditionalOpportunity, shared,
                null, additional.OpportunityKey, null, additional.OpportunityOrder);
        }
        return new(OpportunitySequenceDiagnostic.Exact, null, null, null, null, null);
    }

    public static ValidatedOpportunityPairing Pair(
        IReadOnlyList<SafetyRemediationGateOpportunityState> control,
        IReadOnlyList<SafetyRemediationGateOpportunityState> treatment,
        IReadOnlySet<int> canonicalAuthorityRejectedOrders)
    {
        var comparison = Compare(control, treatment);
        comparison.RequireEquivalent();
        var pairs = control.Zip(treatment).Select(x =>
            new SafetyRemediationGatePairedOpportunity(x.First.OpportunityKey,
                x.First.OpportunityOrder, x.First.StateBefore, x.Second.StateBefore,
                x.First.StateAfter, x.Second.StateAfter, x.First.CommittedObjectIdentity,
                x.Second.CommittedObjectIdentity,
                canonicalAuthorityRejectedOrders.Contains(x.First.OpportunityOrder)))
            .ToImmutableArray();
        return new(comparison, pairs);
    }

    private static string Identity(SafetyRemediationGateOpportunityState value) =>
        $"{value.OpportunityOrder.ToString(System.Globalization.CultureInfo.InvariantCulture)}\0{value.OpportunityKey}";
}

public static class SelectionSetRemappingResearch
{
    public const string ExperimentalClass = "E_SELECTION_SET_REMAP";

    public static SelectionReplayResult Replay(int seed,
        IReadOnlyList<SelectionRandomCall> prefix, IEnumerable<int> orderedLanes)
    {
        var lanes = orderedLanes.ToImmutableArray();
        if (lanes.IsDefaultOrEmpty || lanes.Distinct().Count() != lanes.Length)
            throw new ArgumentException("Selection replay requires a non-empty ordered set of unique lanes.",
                nameof(orderedLanes));
        var random = new SeededRandom(seed);
        foreach (var call in prefix)
        {
            if (call.Kind == SelectionRandomCallKind.Double)
            {
                if (!call.DoubleValue.HasValue || call.Maximum.HasValue || call.IntegerValue.HasValue
                    || BitConverter.DoubleToInt64Bits(random.NextDouble())
                    != BitConverter.DoubleToInt64Bits(call.DoubleValue.Value))
                    throw new InvalidDataException("RNG double prefix cannot be reproduced exactly.");
            }
            else
            {
                if (!call.Maximum.HasValue || !call.IntegerValue.HasValue || call.DoubleValue.HasValue
                    || random.Next(call.Maximum.Value) != call.IntegerValue.Value)
                    throw new InvalidDataException("RNG integer prefix cannot be reproduced exactly.");
            }
        }
        var index = random.Next(lanes.Length);
        return new(lanes, seed, prefix.Count, PrefixHash(prefix), lanes.Length, index,
            lanes[index], 1, prefix.Count + 1);
    }

    public static SelectionSetRemappingAssessment Assess(SelectionSetRemappingEvidence evidence)
    {
        var failures = ImmutableArray.CreateBuilder<string>();
        var parentsEquivalent = evidence.ControlParent.CompleteForCausalLineage
            && evidence.TreatmentParent.CompleteForCausalLineage
            && evidence.ControlParent.Hash == evidence.TreatmentParent.Hash;
        if (!parentsEquivalent) failures.Add("parents_not_equivalent_complete");
        if (evidence.ControlOpportunityKey != evidence.TreatmentOpportunityKey)
            failures.Add("opportunity_mismatch");
        if (evidence.ControlConfigurationIdentity != evidence.TreatmentConfigurationIdentity)
            failures.Add("configuration_mismatch");
        if (evidence.LegacySelection.OrderedLanes.SequenceEqual(
                evidence.CanonicalSelection.OrderedLanes))
            failures.Add("no_canonical_set_intervention");
        if (!evidence.CanonicalSelection.OrderedLanes.Contains(evidence.LegacySelection.SelectedLane))
            failures.Add("control_lane_rejected");
        if (evidence.LegacySelection.SelectedLane == evidence.CanonicalSelection.SelectedLane)
            failures.Add("selection_unchanged");
        if (evidence.LegacySelection.Seed != evidence.CanonicalSelection.Seed
            || evidence.LegacySelection.PrefixSha256 != evidence.CanonicalSelection.PrefixSha256
            || evidence.LegacySelection.PrefixCallCount != evidence.CanonicalSelection.PrefixCallCount
            || evidence.LegacySelection.SelectionRngCalls != 1
            || evidence.CanonicalSelection.SelectionRngCalls != 1
            || evidence.LegacySelection.FinalCallCount != evidence.CanonicalSelection.FinalCallCount)
            failures.Add("rng_state_or_consumption_mismatch");
        if (!evidence.NoConcurrentSelectionIntervention)
            failures.Add("concurrent_intervention");
        var successors = SafetyRemediationForensicAttributionResearch.Compare(
            evidence.ControlSuccessor, evidence.TreatmentSuccessor);
        if (successors.Kind != ForensicStateDifferenceKind.MaterialComplete)
            failures.Add("successors_not_materially_different_complete");
        if (failures.Count == 0) return new(SelectionSetRemappingDisposition.ESelectionSetRemap, []);
        var disposition = failures[0] switch
        {
            "parents_not_equivalent_complete" => SelectionSetRemappingDisposition.AbstainParentsNotEquivalentComplete,
            "opportunity_mismatch" => SelectionSetRemappingDisposition.AbstainOpportunityMismatch,
            "configuration_mismatch" => SelectionSetRemappingDisposition.AbstainConfigurationMismatch,
            "no_canonical_set_intervention" => SelectionSetRemappingDisposition.AbstainNoCanonicalSetIntervention,
            "control_lane_rejected" => SelectionSetRemappingDisposition.AbstainControlLaneRejected,
            "selection_unchanged" => SelectionSetRemappingDisposition.AbstainSelectionUnchanged,
            "rng_state_or_consumption_mismatch" => SelectionSetRemappingDisposition.AbstainRngStateOrConsumptionMismatch,
            "concurrent_intervention" => SelectionSetRemappingDisposition.AbstainConcurrentIntervention,
            _ => SelectionSetRemappingDisposition.AbstainSuccessorsNotMateriallyDifferentComplete
        };
        return new(disposition, failures.ToImmutable());
    }

    public static string PrefixHash(IEnumerable<SelectionRandomCall> calls)
    {
        var text = string.Join('\n', calls.Select(x => x.Kind == SelectionRandomCallKind.Double
            ? $"D:{BitConverter.DoubleToInt64Bits(x.DoubleValue!.Value)}"
            : $"I:{x.Maximum}:{x.IntegerValue}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
