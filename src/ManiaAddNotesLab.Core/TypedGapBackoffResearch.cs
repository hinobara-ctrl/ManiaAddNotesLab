using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum TypedGapEndpointType { TapHead, LongNoteHead, LongNoteRelease }
public enum TypedGapTransitionKind
{
    TapHeadToTapHead,
    TapHeadToLongNoteHead,
    LongNoteReleaseToTapHead,
    LongNoteReleaseToLongNoteHead
}
public enum TypedGapEvidenceScope { LocalLane, GlobalChart }
public enum TypedGapBackoffAction
{
    AdmitLocalSupport,
    AdmitGlobalSupport,
    SkipLocalMismatch,
    SkipLocalAmbiguity,
    SkipGlobalMismatch,
    SkipGlobalAmbiguity,
    SkipNoComparableContext
}

public readonly record struct TypedTransitionGapIdentity(
    TypedGapTransitionKind TransitionKind,
    decimal ExactGapBeats);

public sealed record TypedGapDonorReference(
    OriginalObservationId PreviousObservationId,
    OriginalObservationId NextObservationId,
    int Lane,
    TypedGapEndpointType SourceEndpointType,
    TypedGapEndpointType DestinationEndpointType,
    int SourceEndpointTime,
    decimal SourceEndpointBeat,
    int DestinationHeadTime,
    decimal DestinationHeadBeat,
    decimal ExactGapBeats);

public readonly record struct TypedGapDonorKey(
    OriginalObservationId PreviousObservationId,
    OriginalObservationId NextObservationId);

public sealed record TypedGapScopeEvidence(
    TypedGapEvidenceScope Scope,
    ComparableEvidenceState EvidenceState,
    ImmutableArray<TypedGapDonorKey> SupportingDonors,
    ImmutableArray<TypedGapDonorKey> ContradictingDonors,
    ImmutableArray<decimal> ObservedExactGapVocabulary);

public sealed record TypedGapBackoffStep(
    TypedGapEvidenceScope Scope,
    ComparableEvidenceState EvidenceState,
    bool BackoffPermitted,
    TypedGapBackoffAction? TerminalAction);

public sealed record TypedGapResolution(
    string ChartFingerprint,
    OriginalObservationId TargetPreviousObservationId,
    OriginalObservationId TargetNextObservationId,
    ImmutableArray<OriginalObservationId> ExcludedWholeGroupObservationIds,
    int TargetLane,
    TypedTransitionGapIdentity Candidate,
    TypedGapScopeEvidence LocalEvidence,
    TypedGapScopeEvidence? GlobalEvidence,
    ImmutableArray<TypedGapBackoffStep> BackoffTrace,
    TypedGapBackoffAction FinalAction);

public sealed record TypedGapBackoffResearchResult(
    string ResearchSchemaVersion,
    string ChartFingerprint,
    int KeyCount,
    int OriginalObjectCount,
    ImmutableArray<TypedGapDonorReference> DonorOccurrences,
    ImmutableArray<TypedGapResolution> Resolutions,
    int TargetGroupLeakageCount,
    int SyntheticTeachingCount,
    int HeldTailEncodedAsHeadCount)
{
    public int CandidateCount => Resolutions.Length;
}

/// <summary>
/// F2 shadow model. It resolves exact same-lane transition-gap candidates using original observations only.
/// It does not choose a gap, lane, head type, or generation candidate.
/// </summary>
public static class TypedGapBackoffResearch
{
    public const string ResearchSchemaVersion = "phase-f2-typed-gap-shadow.1";

    public static TypedGapBackoffResearchResult Evaluate(ManiaChart chart) =>
        Evaluate(MapperEvidenceProfileBuilder.Build(chart));

    public static TypedGapBackoffResearchResult Evaluate(MapperEvidenceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var observations = profile.Observations.ToDictionary(x => x.Id);
        var groupByObservation = profile.Observations
            .GroupBy(x => (x.StartTime, x.StartBeat))
            .SelectMany(group => group.Select(item => (item.Id,
                Members: group.Select(x => x.Id).OrderBy(x => x.Value).ToImmutableArray())))
            .ToDictionary(x => x.Id, x => x.Members);
        var transitions = profile.SameLaneTransitions.Where(x => x.GapBeats >= 0)
            .OrderBy(x => x.PreviousId.Value).ThenBy(x => x.NextId.Value)
            .Select(x => new TransitionEntry(x, Kind(observations[x.PreviousId].Type,
                observations[x.NextId].Type), Donor(x, observations))).ToArray();
        var byKind = transitions.GroupBy(x => x.Kind).ToDictionary(x => x.Key, x => x.ToArray());
        var byLaneKind = transitions.GroupBy(x => (x.Transition.Lane, x.Kind))
            .ToDictionary(x => x.Key, x => x.ToArray());
        var resolutions = transitions.Select(target => Resolve(target, profile.ChartFingerprint,
            byKind, byLaneKind, groupByObservation)).ToImmutableArray();
        var leakage = resolutions.Sum(resolution => resolution.LocalEvidence.SupportingDonors
                .Concat(resolution.LocalEvidence.ContradictingDonors)
                .Concat(resolution.GlobalEvidence?.SupportingDonors ?? [])
                .Concat(resolution.GlobalEvidence?.ContradictingDonors ?? [])
                .Count(donor => resolution.ExcludedWholeGroupObservationIds.Contains(donor.PreviousObservationId)
                    || resolution.ExcludedWholeGroupObservationIds.Contains(donor.NextObservationId)));
        var heldAsHead = resolutions.Count(x =>
            (x.Candidate.TransitionKind is TypedGapTransitionKind.LongNoteReleaseToTapHead or
                TypedGapTransitionKind.LongNoteReleaseToLongNoteHead)
            && observations[x.TargetPreviousObservationId].Type != ManiaObjectType.LongNote);
        return new TypedGapBackoffResearchResult(ResearchSchemaVersion, profile.ChartFingerprint,
            profile.KeyCount, profile.OriginalObjectCount, transitions.Select(x => x.Donor).ToImmutableArray(),
            resolutions, leakage, 0, heldAsHead);
    }

    public static ComparableEvidenceState ClassifyEvidence(int comparableDonors, int supportingDonors)
    {
        if (comparableDonors == 0) return ComparableEvidenceState.NoComparableContext;
        if (supportingDonors == 0) return ComparableEvidenceState.LocalMismatch;
        return supportingDonors == comparableDonors
            ? ComparableEvidenceState.ObservedSupport
            : ComparableEvidenceState.AmbiguousEvidence;
    }

    public static (ImmutableArray<TypedGapBackoffStep> Trace, TypedGapBackoffAction Action) ResolveBackoff(
        ComparableEvidenceState local, ComparableEvidenceState global)
    {
        if (local == ComparableEvidenceState.ObservedSupport)
            return ([new(TypedGapEvidenceScope.LocalLane, local, false,
                TypedGapBackoffAction.AdmitLocalSupport)], TypedGapBackoffAction.AdmitLocalSupport);
        if (local == ComparableEvidenceState.LocalMismatch)
            return ([new(TypedGapEvidenceScope.LocalLane, local, false,
                TypedGapBackoffAction.SkipLocalMismatch)], TypedGapBackoffAction.SkipLocalMismatch);
        if (local == ComparableEvidenceState.AmbiguousEvidence)
            return ([new(TypedGapEvidenceScope.LocalLane, local, false,
                TypedGapBackoffAction.SkipLocalAmbiguity)], TypedGapBackoffAction.SkipLocalAmbiguity);

        var terminal = global switch
        {
            ComparableEvidenceState.ObservedSupport => TypedGapBackoffAction.AdmitGlobalSupport,
            ComparableEvidenceState.LocalMismatch => TypedGapBackoffAction.SkipGlobalMismatch,
            ComparableEvidenceState.AmbiguousEvidence => TypedGapBackoffAction.SkipGlobalAmbiguity,
            _ => TypedGapBackoffAction.SkipNoComparableContext
        };
        return ([new(TypedGapEvidenceScope.LocalLane, local, true, null),
            new(TypedGapEvidenceScope.GlobalChart, global, false, terminal)], terminal);
    }

    private static TypedGapResolution Resolve(TransitionEntry target, string fingerprint,
        IReadOnlyDictionary<TypedGapTransitionKind, TransitionEntry[]> byKind,
        IReadOnlyDictionary<(int Lane, TypedGapTransitionKind Kind), TransitionEntry[]> byLaneKind,
        IReadOnlyDictionary<OriginalObservationId, ImmutableArray<OriginalObservationId>> groups)
    {
        var transition = target.Transition;
        var candidate = new TypedTransitionGapIdentity(target.Kind, transition.GapBeats);
        var excluded = groups[transition.PreviousId].Concat(groups[transition.NextId]).Distinct()
            .OrderBy(x => x.Value).ToImmutableArray();
        bool Eligible(TransitionEntry x) => !excluded.Contains(x.Transition.PreviousId)
            && !excluded.Contains(x.Transition.NextId);
        var local = Scope(TypedGapEvidenceScope.LocalLane, candidate,
            byLaneKind.GetValueOrDefault((transition.Lane, target.Kind), []).Where(Eligible));
        var global = local.EvidenceState == ComparableEvidenceState.NoComparableContext
            ? Scope(TypedGapEvidenceScope.GlobalChart, candidate, byKind[target.Kind].Where(Eligible))
            : null;
        var backoff = ResolveBackoff(local.EvidenceState,
            global?.EvidenceState ?? ComparableEvidenceState.NoComparableContext);
        return new TypedGapResolution(fingerprint, transition.PreviousId, transition.NextId, excluded,
            transition.Lane, candidate, local, global, backoff.Trace, backoff.Action);
    }

    private static TypedGapScopeEvidence Scope(TypedGapEvidenceScope scope,
        TypedTransitionGapIdentity candidate, IEnumerable<TransitionEntry> source)
    {
        var donors = source.OrderBy(x => x.Transition.PreviousId.Value)
            .ThenBy(x => x.Transition.NextId.Value).ToArray();
        var supporting = donors.Where(x => x.Transition.GapBeats == candidate.ExactGapBeats)
            .Select(x => Key(x.Transition)).ToImmutableArray();
        var contradicting = donors.Where(x => x.Transition.GapBeats != candidate.ExactGapBeats)
            .Select(x => Key(x.Transition)).ToImmutableArray();
        return new TypedGapScopeEvidence(scope, ClassifyEvidence(donors.Length, supporting.Length),
            supporting, contradicting, donors.Select(x => x.Transition.GapBeats).Distinct().Order().ToImmutableArray());
    }

    private static TypedGapDonorKey Key(SameLaneTransitionObservation x) => new(x.PreviousId, x.NextId);

    private static TypedGapDonorReference Donor(SameLaneTransitionObservation transition,
        IReadOnlyDictionary<OriginalObservationId, OriginalObservation> observations)
    {
        var previous = observations[transition.PreviousId];
        var next = observations[transition.NextId];
        return new TypedGapDonorReference(previous.Id, next.Id, transition.Lane,
            previous.Type == ManiaObjectType.Tap ? TypedGapEndpointType.TapHead : TypedGapEndpointType.LongNoteRelease,
            next.Type == ManiaObjectType.Tap ? TypedGapEndpointType.TapHead : TypedGapEndpointType.LongNoteHead,
            previous.Type == ManiaObjectType.Tap ? previous.StartTime : previous.EndTime!.Value,
            previous.EndBeat, next.StartTime, next.StartBeat, transition.GapBeats);
    }

    private static TypedGapTransitionKind Kind(ManiaObjectType previous, ManiaObjectType next) =>
        (previous, next) switch
        {
            (ManiaObjectType.Tap, ManiaObjectType.Tap) => TypedGapTransitionKind.TapHeadToTapHead,
            (ManiaObjectType.Tap, ManiaObjectType.LongNote) => TypedGapTransitionKind.TapHeadToLongNoteHead,
            (ManiaObjectType.LongNote, ManiaObjectType.Tap) => TypedGapTransitionKind.LongNoteReleaseToTapHead,
            _ => TypedGapTransitionKind.LongNoteReleaseToLongNoteHead
        };

    private sealed record TransitionEntry(SameLaneTransitionObservation Transition,
        TypedGapTransitionKind Kind, TypedGapDonorReference Donor);
}

public static class TypedGapBackoffResearchJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    public static string Serialize(TypedGapBackoffResearchResult result) => JsonSerializer.Serialize(result, Options);
}
