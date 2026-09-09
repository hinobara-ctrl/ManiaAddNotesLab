using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace ManiaAddNotesLab.Core;

public static class D1BehavioralPolicyVersions
{
    public const string Treatment = "d1-resulting-state-ab.1";
}

public interface IRandomPositionSource : IRandomSource
{
    long CallCount { get; }
}

public sealed record D1JointEvidenceOccurrence(
    string SourceGroupId,
    int HeadTime,
    string ReducedStateSignature,
    CompletionSetIdentity CompletionSet,
    ImmutableArray<OriginalObservationId> CompletionObservationIds);

public sealed record D1EvidenceQueryResult(
    string OriginalBaseState,
    CompletionSetIdentity ProspectiveCompletionSet,
    FutureD1EvidenceState EvidenceState,
    int ComparableOccurrenceCount,
    int DistinctJointCompletionSetCount,
    ImmutableArray<string> JointDonorOccurrenceIds);

/// <summary>Immutable, original-only ReducedOnly lookup built once per chart for the frozen D1 experiment.</summary>
public sealed class D1OriginalEvidenceIndex
{
    private readonly ImmutableDictionary<int, ImmutableArray<CompletionMemberIdentity>> baseMembersByTime;
    private readonly ImmutableDictionary<string, ImmutableArray<D1JointEvidenceOccurrence>> occurrencesByReducedState;

    private D1OriginalEvidenceIndex(string chartFingerprint, int keyCount, int originalObjectCount,
        ImmutableDictionary<int, ImmutableArray<CompletionMemberIdentity>> baseMembersByTime,
        ImmutableDictionary<string, ImmutableArray<D1JointEvidenceOccurrence>> occurrencesByReducedState,
        string contentHash, long buildOperations, double buildMilliseconds)
    {
        ChartFingerprint = chartFingerprint;
        KeyCount = keyCount;
        OriginalObjectCount = originalObjectCount;
        this.baseMembersByTime = baseMembersByTime;
        this.occurrencesByReducedState = occurrencesByReducedState;
        ContentHash = contentHash;
        BuildOperations = buildOperations;
        BuildMilliseconds = buildMilliseconds;
    }

    public string ChartFingerprint { get; }
    public int KeyCount { get; }
    public int OriginalObjectCount { get; }
    public string ContentHash { get; }
    public long BuildOperations { get; }
    public double BuildMilliseconds { get; }
    public int ReducedStateCount => occurrencesByReducedState.Count;
    public int JointOccurrenceCount => occurrencesByReducedState.Sum(x => x.Value.Length);

    public static D1OriginalEvidenceIndex Build(ManiaChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        var started = Stopwatch.GetTimestamp();
        var fingerprint = MapperEvidenceProfileBuilder.Build(chart).ChartFingerprint;
        var baseMembers = chart.OriginalObjects.GroupBy(x => x.StartTime)
            .ToImmutableDictionary(group => group.Key, group => group
                .Select(D1BehavioralExperimentContractResearch.CandidateIdentity)
                .OrderBy(x => x.Lane).ThenBy(x => x.HeadType).ToImmutableArray());
        var research = ChordCompletionResearch.Evaluate(chart);
        var occurrences = ImmutableArray.CreateBuilder<D1JointEvidenceOccurrence>();
        long operations = 0;
        foreach (var group in research.ChordGroups)
        for (var left = 0; left < group.Members.Length - 1; left++)
        for (var right = left + 1; right < group.Members.Length; right++)
        {
            operations++;
            var completion = CompletionSetIdentity.Create([
                Identity(group.Members[left]), Identity(group.Members[right])]);
            var reduced = group.Members.Where((_, index) => index != left && index != right).ToArray();
            occurrences.Add(new D1JointEvidenceOccurrence(group.SourceGroupId, group.HeadTime,
                HeadSignature(chart.KeyCount, reduced.Select(Identity)), completion,
                [group.Members[left].ObservationId, group.Members[right].ObservationId]));
        }

        var byReduced = occurrences.GroupBy(x => x.ReducedStateSignature, StringComparer.Ordinal)
            .ToImmutableDictionary(group => group.Key, group => group
                .OrderBy(x => x.SourceGroupId, StringComparer.Ordinal)
                .ThenBy(x => x.CompletionSet.Signature, StringComparer.Ordinal).ToImmutableArray(),
                StringComparer.Ordinal);
        var hashText = new StringBuilder()
            .Append("chart=").Append(fingerprint).Append("\nkeys=").Append(chart.KeyCount)
            .Append("\nobjects=").Append(chart.OriginalObjects.Count).Append('\n');
        foreach (var occurrence in byReduced.OrderBy(x => x.Key, StringComparer.Ordinal).SelectMany(x => x.Value))
            hashText.Append(occurrence.ReducedStateSignature).Append('|')
                .Append(occurrence.CompletionSet.Signature).Append('|')
                .Append(occurrence.SourceGroupId).Append('|')
                .AppendJoin(',', occurrence.CompletionObservationIds.OrderBy(x => x.Value))
                .Append('\n');
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashText.ToString())));
        return new D1OriginalEvidenceIndex(fingerprint, chart.KeyCount, chart.OriginalObjects.Count,
            baseMembers, byReduced, contentHash, operations,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    public D1EvidenceQueryResult Query(int headTime, CompletionSetIdentity prospectiveCompletionSet)
    {
        var baseMembers = baseMembersByTime.GetValueOrDefault(headTime, []);
        var reducedState = HeadSignature(KeyCount, baseMembers);
        var comparable = occurrencesByReducedState.GetValueOrDefault(reducedState, []);
        if (comparable.Length == 0)
            return Result(FutureD1EvidenceState.NoComparableCompositionContext, [], 0);

        var jointSets = comparable.Select(x => x.CompletionSet).Distinct()
            .OrderBy(x => x.Signature, StringComparer.Ordinal).ToArray();
        var donors = comparable.Where(x => x.CompletionSet.Equals(prospectiveCompletionSet))
            .Select(x => x.SourceGroupId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .ToImmutableArray();
        if (donors.Length > 0)
            return Result(jointSets.Length == 1 ? FutureD1EvidenceState.JointObservedUnique
                : FutureD1EvidenceState.JointObservedAmongAlternatives, donors, jointSets.Length);
        var marginal = jointSets.SelectMany(x => x.Members).ToHashSet();
        return Result(prospectiveCompletionSet.Members.All(marginal.Contains)
            ? FutureD1EvidenceState.MarginalOnly
            : FutureD1EvidenceState.NotMarginallySupported, [], jointSets.Length);

        D1EvidenceQueryResult Result(FutureD1EvidenceState state, ImmutableArray<string> donors,
            int distinctSets) => new(reducedState, prospectiveCompletionSet, state, comparable.Length,
                distinctSets, donors);
    }

    public string OriginalBaseStateAt(int headTime) => HeadSignature(KeyCount,
        baseMembersByTime.GetValueOrDefault(headTime, []));

    private static CompletionMemberIdentity Identity(OriginalHeadMember member) =>
        new(member.Lane, member.HeadType);

    private static string HeadSignature(int keyCount, IEnumerable<CompletionMemberIdentity> members) =>
        ChordCompletionResearch.HeadSignature(keyCount,
            members.Where(x => x.HeadType == OriginalHeadMemberType.TapHead).Select(x => x.Lane),
            members.Where(x => x.HeadType == OriginalHeadMemberType.LongNoteHead).Select(x => x.Lane));
}

public sealed record D1BehavioralExperimentConfiguration(
    D1OriginalEvidenceIndex EvidenceIndex,
    string ContractContentHash,
    string RunManifestContentHash)
{
    public static D1BehavioralExperimentConfiguration Frozen(D1OriginalEvidenceIndex evidenceIndex,
        string runManifestContentHash) => new(evidenceIndex,
            D1BehavioralExperimentContractResearch.FrozenContractContentHash, runManifestContentHash);
}

public sealed record D1DirectDecision(
    string OpportunityKey,
    OpportunityKind OpportunityKind,
    int StableOpportunityOrder,
    int Timestamp,
    string OriginalBaseState,
    CompletionMemberIdentity AlreadyAddedCompletionMember,
    CompletionMemberIdentity ProspectiveCandidate,
    CompletionSetIdentity ProspectiveCompletionSet,
    CompletionPairType PairType,
    FutureD1EvidenceState EvidenceState,
    FutureD1ExperimentalDisposition Disposition,
    ImmutableArray<string> JointDonorOccurrenceIds,
    string ControlAction,
    string TreatmentAction,
    long RngPositionBeforeGate,
    int GateRngCalls,
    bool OutputMutationPerformed);

public sealed record D1BehavioralRunDiagnostics(
    string SchemaVersion,
    string BehaviorPolicyVersion,
    string ContractContentHash,
    string RunManifestContentHash,
    string ChartFingerprint,
    string EvidenceIndexHashBefore,
    string EvidenceIndexHashAfter,
    long EvidenceIndexBuildOperations,
    double EvidenceIndexBuildMilliseconds,
    int EvidenceQueryCount,
    int GateRngCalls,
    int LaterK3PlusAfterDirectD1Count,
    ImmutableArray<D1DirectDecision> DirectDecisions)
{
    public int DirectGateAdmissions => DirectDecisions.Count(x =>
        x.Disposition == FutureD1ExperimentalDisposition.ExperimentAdmittable);
    public int DirectGateRejections => DirectDecisions.Length - DirectGateAdmissions;
}

internal sealed class D1BehavioralRunDiagnosticsBuilder(D1BehavioralExperimentConfiguration configuration)
{
    private readonly List<D1DirectDecision> decisions = [];
    public string IndexHashBefore { get; } = configuration.EvidenceIndex.ContentHash;

    public void Add(D1DirectDecision decision) => decisions.Add(decision);

    public D1BehavioralRunDiagnostics Build(IReadOnlyList<ManiaObject> added)
    {
        var directTimes = decisions.Select(x => x.Timestamp).Distinct().ToHashSet();
        var laterK3 = directTimes.Count(time => added.Count(x => x.StartTime == time) >= 3);
        return new D1BehavioralRunDiagnostics("phase-d1-behavioral-ab.1",
            D1BehavioralPolicyVersions.Treatment, configuration.ContractContentHash,
            configuration.RunManifestContentHash, configuration.EvidenceIndex.ChartFingerprint,
            IndexHashBefore, configuration.EvidenceIndex.ContentHash,
            configuration.EvidenceIndex.BuildOperations, configuration.EvidenceIndex.BuildMilliseconds,
            decisions.Count, decisions.Sum(x => x.GateRngCalls), laterK3, decisions.ToImmutableArray());
    }
}
