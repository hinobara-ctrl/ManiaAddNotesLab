using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ManiaAddNotesLab.Core;

public enum SafetyRemediationImplementationFileRole
{
    BehavioralAuthority,
    SharedRuntimeDependency,
    InstrumentationOnly,
    CertificationOnly,
    DocumentationOnly
}

public sealed record SafetyRemediationImplementationFile(
    string Path,
    SafetyRemediationImplementationFileRole Role,
    bool IncludedInImplementationSnapshot,
    string Justification);

public sealed record SafetyRemediationGateSufficientState(
    string Hash,
    string MaterializedGenerationStateHash,
    string LatentCommittedGeometryHash,
    long RootRngPosition,
    long StageRngPosition,
    int OpportunityCursor,
    string PendingArticulationHash,
    bool CompleteForCausalLineage);

public sealed record SafetyRemediationGatePairedOpportunity(
    string OpportunityKey,
    int OpportunityOrder,
    SafetyRemediationGateSufficientState ControlBefore,
    SafetyRemediationGateSufficientState TreatmentBefore,
    SafetyRemediationGateSufficientState ControlAfter,
    SafetyRemediationGateSufficientState TreatmentAfter,
    string? ControlCommitIdentity,
    string? TreatmentCommitIdentity,
    bool CanonicalAuthorityChangedCommit);

public sealed record SafetyRemediationGateLineageStep(
    string OpportunityKey,
    int OpportunityOrder,
    bool BeforeEqual,
    bool AfterEqual,
    bool CommitDifference,
    bool DirectGovernedDivergence,
    string? ActiveLineageBefore,
    string? OpenedLineage,
    string? ActiveLineageAfter,
    bool ReconvergedBefore,
    bool ReconvergedAfter,
    bool UnexplainedStateDivergence);

public sealed record SafetyRemediationGateCompactOpportunityState(
    string OpportunityKey,
    int OpportunityOrder,
    string StateBeforeHash,
    bool StateBeforeComplete,
    string StateAfterHash,
    bool StateAfterComplete,
    string? CommittedObjectIdentity);

public sealed class SafetyRemediationGateOpportunityFingerprint : IDisposable
{
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool completed;

    public void Add(SafetyRemediationGateCompactOpportunityState item)
    {
        if (completed) throw new InvalidOperationException("Opportunity fingerprint is already complete.");
        Append(item.OpportunityOrder.ToString(CultureInfo.InvariantCulture));
        Append(item.StateBeforeHash); Append(item.StateBeforeComplete ? "1" : "0");
        Append(item.StateAfterHash); Append(item.StateAfterComplete ? "1" : "0");
        Append(item.CommittedObjectIdentity);
    }

    public string Complete()
    {
        if (completed) throw new InvalidOperationException("Opportunity fingerprint is already complete.");
        completed = true;
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public static string Compute(IEnumerable<SafetyRemediationGateCompactOpportunityState> values)
    {
        using var fingerprint = new SafetyRemediationGateOpportunityFingerprint();
        foreach (var item in values) fingerprint.Add(item);
        return fingerprint.Complete();
    }

    private void Append(string? item)
    {
        if (item is null)
        {
            Span<byte> missing = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(missing, -1);
            hash.AppendData(missing);
            return;
        }
        var bytes = Encoding.UTF8.GetBytes(item);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    public void Dispose() => hash.Dispose();
}

public sealed class SafetyRemediationGateCandidateFingerprint : IDisposable
{
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool completed;
    public void Add(SafetyRemediationCandidateGeometryDecision item)
    {
        if (completed) throw new InvalidOperationException("Candidate fingerprint is already complete.");
        Append(item.OpportunityKey); Append(item.OpportunityOrder); Append(item.CandidateOrder);
        Append((int)item.ObjectType); Append(item.StartTime); Append(item.EndTime);
        Append(item.LatentStartBeat); Append(item.LatentEndBeat);
        Append(item.CanonicalStartBeat); Append(item.CanonicalEndBeat);
        Append(string.Join(',', item.LegacyLegalLanes)); Append(string.Join(',', item.CanonicalLegalLanes));
        Append(item.MaterializedGeometryStateHash); Append(item.RngPositionBeforeDecision);
        Append(item.SelectedLane); Append(item.TreatmentEnabled ? 1 : 0);
    }
    public string Complete()
    {
        if (completed) throw new InvalidOperationException("Candidate fingerprint is already complete.");
        completed = true;
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static string Compute(IEnumerable<SafetyRemediationCandidateGeometryDecision> values)
    {
        using var fingerprint = new SafetyRemediationGateCandidateFingerprint();
        foreach (var item in values) fingerprint.Add(item);
        return fingerprint.Complete();
    }
    private void Append<T>(T value) => Append(value is IFormattable formatted
        ? formatted.ToString(null, CultureInfo.InvariantCulture) : value?.ToString());
    private void Append(string? item)
    {
        if (item is null)
        {
            Span<byte> missing = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(missing, -1); hash.AppendData(missing); return;
        }
        var bytes = Encoding.UTF8.GetBytes(item);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        hash.AppendData(length); hash.AppendData(bytes);
    }
    public void Dispose() => hash.Dispose();
}

public sealed class SafetyRemediationGateTranscriptHash : IDisposable
{
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    public void AppendDouble(double value) => Append("D:" + value.ToString("R", CultureInfo.InvariantCulture) + "\n");
    public void AppendInteger(int maximum, int value) => Append("I:" +
        maximum.ToString(CultureInfo.InvariantCulture) + ":" +
        value.ToString(CultureInfo.InvariantCulture) + "\n");
    public string CurrentHash => Convert.ToHexString(hash.GetCurrentHash());
    private void Append(string value) => hash.AppendData(Encoding.UTF8.GetBytes(value));
    public void Dispose() => hash.Dispose();
}

public static class SafetyRemediationGateHardeningResearch
{
    public const string SchemaVersion = "safety-remediation-gate-hardening.1";

    public static SafetyRemediationGateSufficientState State(
        GenerationStateIdentity materializedState, string latentCommittedGeometryHash)
    {
        var complete = materializedState.CompleteForCausalLineage
            && materializedState.RootRngPosition.HasValue
            && materializedState.StageRngPosition.HasValue;
        var hash = GenerationProvenanceRecorderResearch.Hash(string.Join('|',
            materializedState.GenerationStateHash,
            latentCommittedGeometryHash,
            materializedState.RootRngPosition?.ToString() ?? "UNKNOWN",
            materializedState.StageRngPosition?.ToString() ?? "UNKNOWN",
            materializedState.OpportunityCursor,
            materializedState.PendingArticulationHash));
        return new(hash, materializedState.GenerationStateHash, latentCommittedGeometryHash,
            materializedState.RootRngPosition ?? -1, materializedState.StageRngPosition ?? -1,
            materializedState.OpportunityCursor, materializedState.PendingArticulationHash, complete);
    }

    public static ImmutableArray<SafetyRemediationGateLineageStep> Classify(
        string pairIdentity, IReadOnlyList<SafetyRemediationGatePairedOpportunity> opportunities)
    {
        var result = ImmutableArray.CreateBuilder<SafetyRemediationGateLineageStep>();
        string? active = null;
        var episode = 0;
        foreach (var item in opportunities.OrderBy(x => x.OpportunityOrder))
        {
            var beforeEqual = item.ControlBefore.CompleteForCausalLineage
                && item.TreatmentBefore.CompleteForCausalLineage
                && item.ControlBefore.Hash == item.TreatmentBefore.Hash;
            var afterEqual = item.ControlAfter.CompleteForCausalLineage
                && item.TreatmentAfter.CompleteForCausalLineage
                && item.ControlAfter.Hash == item.TreatmentAfter.Hash;
            var reconvergedBefore = active is not null && beforeEqual;
            if (reconvergedBefore) active = null;
            var lineageBefore = active;
            var commitsDiffer = item.ControlCommitIdentity != item.TreatmentCommitIdentity;
            var governed = active is null && beforeEqual && commitsDiffer && !afterEqual
                && item.ControlCommitIdentity is not null
                && CanonicalRejectWasNotCommitted(item.ControlCommitIdentity,
                    item.TreatmentCommitIdentity)
                && item.CanonicalAuthorityChangedCommit;
            string? opened = null;
            if (governed)
            {
                opened = "SRG-DIV-" + GenerationProvenanceRecorderResearch.Hash(
                    $"{pairIdentity}|{item.OpportunityKey}|{episode++}")[..24];
                active = opened;
            }
            // Inspect the successor of this opportunity immediately. Otherwise a first divergence in the
            // final opportunity would be invisible because there is no next StateBefore to expose it.
            var unexplained = active is null && (!beforeEqual || !afterEqual);
            var reconvergedAfter = active is not null && afterEqual;
            if (reconvergedAfter) active = null;
            result.Add(new(item.OpportunityKey, item.OpportunityOrder, beforeEqual, afterEqual,
                commitsDiffer, governed, lineageBefore, opened, active, reconvergedBefore,
                reconvergedAfter, unexplained));
        }
        return result.ToImmutable();
    }

    public static bool CanonicalRejectWasNotCommitted(string rejectedCommitIdentity,
        string? treatmentCommitIdentity) => treatmentCommitIdentity != rejectedCommitIdentity;

    public static bool CanonicalAuthorityRejectedSelectedCommit(
        IEnumerable<SafetyRemediationCandidateGeometryDecision> decisions) => decisions.Any(x =>
        x.SelectedLane is not null && x.LegacyAcceptsSelectedLane && !x.CanonicalAcceptsSelectedLane);

    public static ImmutableArray<SafetyRemediationGateCompactOpportunityState> Compact(
        IEnumerable<SafetyRemediationGateOpportunityState> values) => values.Select(x => new
            SafetyRemediationGateCompactOpportunityState(x.OpportunityKey, x.OpportunityOrder,
                x.StateBefore.Hash, x.StateBefore.CompleteForCausalLineage,
                x.StateAfter.Hash, x.StateAfter.CompleteForCausalLineage,
                x.CommittedObjectIdentity)).ToImmutableArray();

    public static SafetyRemediationGateSufficientState LineageState(
        string hash, bool complete, int cursor) => new(hash, string.Empty, string.Empty,
            -1, -1, cursor, string.Empty, complete);

    public static string DiagnosticsFingerprint(SafetyRemediationGateRuntimeDiagnostics value)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string? item)
        {
            if (item is null)
            {
                Span<byte> missing = stackalloc byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(missing, -1);
                hash.AppendData(missing);
                return;
            }
            var bytes = Encoding.UTF8.GetBytes(item);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
        Add(value.SchemaVersion); Add(value.BehaviorPolicyVersion); Add(value.ContractContentHash);
        Add(value.TreatmentEnabled ? "1" : "0");
        Add(value.GateRngCalls.ToString(CultureInfo.InvariantCulture));
        Add(value.CandidateDecisionCount.ToString(CultureInfo.InvariantCulture));
        Add(string.IsNullOrEmpty(value.CandidateTraceFingerprint)
            ? SafetyRemediationGateCandidateFingerprint.Compute(value.CandidateDecisions)
            : value.CandidateTraceFingerprint);
        Add(value.OpportunityCount.ToString(CultureInfo.InvariantCulture));
        var opportunityFingerprint = string.IsNullOrEmpty(value.OpportunityTraceFingerprint)
            ? SafetyRemediationGateOpportunityFingerprint.Compute(
                value.CompactOpportunityStates.IsDefaultOrEmpty
                    ? Compact(value.OpportunityStates) : value.CompactOpportunityStates)
            : value.OpportunityTraceFingerprint;
        Add(opportunityFingerprint);
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
