using System.Collections.Immutable;
using System.Reflection;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseF2TypedGapBackoffTests
{
    [Theory]
    [InlineData(ManiaObjectType.Tap, ManiaObjectType.Tap, TypedGapTransitionKind.TapHeadToTapHead)]
    [InlineData(ManiaObjectType.Tap, ManiaObjectType.LongNote, TypedGapTransitionKind.TapHeadToLongNoteHead)]
    [InlineData(ManiaObjectType.LongNote, ManiaObjectType.Tap, TypedGapTransitionKind.LongNoteReleaseToTapHead)]
    [InlineData(ManiaObjectType.LongNote, ManiaObjectType.LongNote, TypedGapTransitionKind.LongNoteReleaseToLongNoteHead)]
    public void TransitionTaxonomyUsesExactEndpoints(ManiaObjectType previous, ManiaObjectType next,
        TypedGapTransitionKind expected)
    {
        var result = TypedGapBackoffResearch.Evaluate(Profile(
            Row(0, previous, next, .25m), Row(0, previous, next, .25m)));
        Assert.All(result.Resolutions, x => Assert.Equal(expected, x.Candidate.TransitionKind));
        var donorKey = result.Resolutions[0].LocalEvidence.SupportingDonors.Single();
        var donor = result.DonorOccurrences.Single(x => x.PreviousObservationId == donorKey.PreviousObservationId
            && x.NextObservationId == donorKey.NextObservationId);
        Assert.Equal(previous == ManiaObjectType.Tap ? TypedGapEndpointType.TapHead
            : TypedGapEndpointType.LongNoteRelease, donor.SourceEndpointType);
        Assert.Equal(next == ManiaObjectType.Tap ? TypedGapEndpointType.TapHead
            : TypedGapEndpointType.LongNoteHead, donor.DestinationEndpointType);
    }

    [Theory]
    [InlineData(0, 0, ComparableEvidenceState.NoComparableContext)]
    [InlineData(2, 0, ComparableEvidenceState.LocalMismatch)]
    [InlineData(2, 2, ComparableEvidenceState.ObservedSupport)]
    [InlineData(2, 1, ComparableEvidenceState.AmbiguousEvidence)]
    public void EvidenceStatesAreExactAndExclusive(int comparable, int supporting,
        ComparableEvidenceState expected) =>
        Assert.Equal(expected, TypedGapBackoffResearch.ClassifyEvidence(comparable, supporting));

    [Fact]
    public void ExactGapDoesNotRoundToSixDecimals()
    {
        var result = TypedGapBackoffResearch.Evaluate(Profile(
            Row(0, ManiaObjectType.Tap, ManiaObjectType.Tap, .1234564m),
            Row(0, ManiaObjectType.Tap, ManiaObjectType.Tap, .1234565m)));
        Assert.All(result.Resolutions, x => Assert.Equal(ComparableEvidenceState.LocalMismatch,
            x.LocalEvidence.EvidenceState));
        Assert.Equal(2, result.Resolutions.Select(x => x.Candidate.ExactGapBeats).Distinct().Count());
    }

    [Fact]
    public void CandidateWithMatchingAndDifferentGapsIsAmbiguousWithoutRanking()
    {
        var result = TypedGapBackoffResearch.Evaluate(Profile(
            Row(0, ManiaObjectType.Tap, ManiaObjectType.Tap, .25m),
            Row(0, ManiaObjectType.Tap, ManiaObjectType.Tap, .25m),
            Row(0, ManiaObjectType.Tap, ManiaObjectType.Tap, .5m)));
        var target = result.Resolutions[0];
        Assert.Equal(ComparableEvidenceState.AmbiguousEvidence, target.LocalEvidence.EvidenceState);
        Assert.Equal(new[] { .25m, .5m }, target.LocalEvidence.ObservedExactGapVocabulary);
        Assert.Equal(TypedGapBackoffAction.SkipLocalAmbiguity, target.FinalAction);
        Assert.DoesNotContain(typeof(TypedGapResolution).GetProperties(), x =>
            x.Name.Contains("Rank", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(BackoffCases))]
    public void BackoffOccursOnlyForNoComparableContext(ComparableEvidenceState local,
        ComparableEvidenceState global, TypedGapBackoffAction expected, int steps)
    {
        var resolved = TypedGapBackoffResearch.ResolveBackoff(local, global);
        Assert.Equal(expected, resolved.Action);
        Assert.Equal(steps, resolved.Trace.Length);
        Assert.Equal(local == ComparableEvidenceState.NoComparableContext,
            resolved.Trace[0].BackoffPermitted);
    }

    public static TheoryData<ComparableEvidenceState, ComparableEvidenceState,
        TypedGapBackoffAction, int> BackoffCases => new()
    {
        { ComparableEvidenceState.ObservedSupport, ComparableEvidenceState.ObservedSupport,
            TypedGapBackoffAction.AdmitLocalSupport, 1 },
        { ComparableEvidenceState.LocalMismatch, ComparableEvidenceState.ObservedSupport,
            TypedGapBackoffAction.SkipLocalMismatch, 1 },
        { ComparableEvidenceState.AmbiguousEvidence, ComparableEvidenceState.ObservedSupport,
            TypedGapBackoffAction.SkipLocalAmbiguity, 1 },
        { ComparableEvidenceState.NoComparableContext, ComparableEvidenceState.ObservedSupport,
            TypedGapBackoffAction.AdmitGlobalSupport, 2 },
        { ComparableEvidenceState.NoComparableContext, ComparableEvidenceState.LocalMismatch,
            TypedGapBackoffAction.SkipGlobalMismatch, 2 },
        { ComparableEvidenceState.NoComparableContext, ComparableEvidenceState.AmbiguousEvidence,
            TypedGapBackoffAction.SkipGlobalAmbiguity, 2 },
        { ComparableEvidenceState.NoComparableContext, ComparableEvidenceState.NoComparableContext,
            TypedGapBackoffAction.SkipNoComparableContext, 2 }
    };

    [Fact]
    public void LaneLocalNoContextCanUseExactGlobalSupport()
    {
        var result = TypedGapBackoffResearch.Evaluate(Profile(
            Row(0, ManiaObjectType.Tap, ManiaObjectType.LongNote, .25m),
            Row(1, ManiaObjectType.Tap, ManiaObjectType.LongNote, .25m)));
        var target = result.Resolutions[0];
        Assert.Equal(ComparableEvidenceState.NoComparableContext, target.LocalEvidence.EvidenceState);
        Assert.Equal(ComparableEvidenceState.ObservedSupport, target.GlobalEvidence!.EvidenceState);
        Assert.Equal(TypedGapBackoffAction.AdmitGlobalSupport, target.FinalAction);
    }

    [Fact]
    public void WholeHeadGroupsAreExcludedFromDonors()
    {
        var rows = new[] { Row(0, ManiaObjectType.Tap, ManiaObjectType.Tap, .25m),
            Row(0, ManiaObjectType.Tap, ManiaObjectType.Tap, .25m) };
        var profile = Profile(rows);
        var observations = profile.Observations.ToArray();
        observations[2] = observations[2] with
            { StartTime = observations[0].StartTime, StartBeat = observations[0].StartBeat };
        profile = profile with { Observations = observations.ToImmutableArray() };
        var result = TypedGapBackoffResearch.Evaluate(profile);
        Assert.Equal(0, result.TargetGroupLeakageCount);
        Assert.Contains(observations[2].Id, result.Resolutions[0].ExcludedWholeGroupObservationIds);
        Assert.Empty(result.Resolutions[0].LocalEvidence.SupportingDonors);
    }

    [Fact]
    public void AddedObjectsNeverTeachTypedGaps()
    {
        var baseChart = Chart(4, ManiaObject.Tap(0, 0), ManiaObject.Tap(0, 125));
        var withSynthetic = new ManiaChart
        {
            KeyCount = baseChart.KeyCount, Lines = baseChart.Lines,
            OriginalObjects = baseChart.OriginalObjects, TimingPoints = baseChart.TimingPoints,
            AddedObjects = [ManiaObject.Tap(0, 250, true)]
        };
        var left = TypedGapBackoffResearchJson.Serialize(TypedGapBackoffResearch.Evaluate(baseChart));
        var right = TypedGapBackoffResearchJson.Serialize(TypedGapBackoffResearch.Evaluate(withSynthetic));
        Assert.Equal(left, right);
    }

    [Fact]
    public void SeparateChartsNeverTransferEvidence()
    {
        var left = TypedGapBackoffResearch.Evaluate(Chart(4,
            ManiaObject.Tap(0, 0), ManiaObject.Tap(0, 125)));
        var right = TypedGapBackoffResearch.Evaluate(Chart(4,
            ManiaObject.Tap(0, 1000), ManiaObject.Tap(0, 1125)));
        Assert.NotEqual(left.ChartFingerprint, right.ChartFingerprint);
        Assert.All(left.Resolutions.Concat(right.Resolutions), x =>
            Assert.Equal(ComparableEvidenceState.NoComparableContext, x.LocalEvidence.EvidenceState));
    }

    [Fact]
    public void ShadowIsDeterministicAndOwnsNoRandomParameter()
    {
        var profile = Profile(Row(0, ManiaObjectType.Tap, ManiaObjectType.Tap, .25m),
            Row(0, ManiaObjectType.Tap, ManiaObjectType.Tap, .25m));
        Assert.Equal(TypedGapBackoffResearchJson.Serialize(TypedGapBackoffResearch.Evaluate(profile)),
            TypedGapBackoffResearchJson.Serialize(TypedGapBackoffResearch.Evaluate(profile)));
        Assert.DoesNotContain(typeof(TypedGapBackoffResearch).GetMethods(
                BindingFlags.Public | BindingFlags.Static).SelectMany(x => x.GetParameters()),
            x => typeof(IRandomSource).IsAssignableFrom(x.ParameterType));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void SameImplementationHandlesAllSupportedKeymodes(int keys)
    {
        var result = TypedGapBackoffResearch.Evaluate(Chart(keys,
            ManiaObject.Tap(0, 0), ManiaObject.Tap(0, 125)));
        Assert.Equal(keys, result.KeyCount);
        Assert.Single(result.Resolutions);
    }

    [Fact]
    public void SerializedShadowContainsNoAuthorityOrFrequencyPolicy()
    {
        var json = TypedGapBackoffResearchJson.Serialize(TypedGapBackoffResearch.Evaluate(Profile(
            Row(0, ManiaObjectType.Tap, ManiaObjectType.Tap, .25m))));
        foreach (var forbidden in new[] { "score", "probability", "confidence", "likelihood",
                     "authority", "ranking", "weight", "majority", "mapperSupport", "section" })
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record GapRow(int Lane, ManiaObjectType Previous, ManiaObjectType Next, decimal Gap);
    private static GapRow Row(int lane, ManiaObjectType previous, ManiaObjectType next, decimal gap) =>
        new(lane, previous, next, gap);

    private static MapperEvidenceProfile Profile(params GapRow[] rows)
    {
        var observations = ImmutableArray.CreateBuilder<OriginalObservation>();
        var transitions = ImmutableArray.CreateBuilder<SameLaneTransitionObservation>();
        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var previousId = new OriginalObservationId(i * 2);
            var nextId = new OriginalObservationId(i * 2 + 1);
            var start = i * 10m;
            var previousEnd = start + (row.Previous == ManiaObjectType.LongNote ? 1 : 0);
            observations.Add(new OriginalObservation(previousId, i * 2, row.Lane, row.Previous,
                i * 10000, row.Previous == ManiaObjectType.LongNote ? i * 10000 + 1000 : null,
                start, previousEnd));
            observations.Add(new OriginalObservation(nextId, i * 2 + 1, row.Lane, row.Next,
                i * 10000 + 2000, row.Next == ManiaObjectType.LongNote ? i * 10000 + 3000 : null,
                previousEnd + row.Gap, previousEnd + row.Gap + (row.Next == ManiaObjectType.LongNote ? 1 : 0)));
            transitions.Add(new SameLaneTransitionObservation(previousId, nextId, row.Lane,
                OriginalTransitionType.TapToTap, row.Gap));
        }
        return new MapperEvidenceProfile("phase-a.1", "legacy-experimental.1", "chart", 4,
            observations.Count, observations.ToImmutable(), [], [], [], transitions.ToImmutable(), [], [],
            new TimingVocabularySummary([], [], []), 0);
    }

    private static ManiaChart Chart(int keys, params ManiaObject[] objects) => new()
    {
        KeyCount = keys,
        Lines = [],
        OriginalObjects = objects,
        TimingPoints = [new TimingPoint(0, 500)]
    };
}
