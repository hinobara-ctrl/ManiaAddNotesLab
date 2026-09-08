using System.Reflection;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseF21ExactGapTimingIdentityTests
{
    [Fact]
    public void FileExactBaselinePreservesDistinctDecimals()
    {
        var result = Evaluate(4, [new(0, 500)], Tap(0, 0), Tap(0, 249), Tap(0, 499));
        Assert.Equal(new[] { .498m, .5m }, result.Occurrences.Select(x => x.FileExactGapBeats));
        Assert.Equal(2, result.Occurrences.Select(x => x.FileExactIdentity).Distinct().Count());
    }

    [Fact]
    public void FileExactHeadGroupModeReproducesF2EvidenceAndBackoff()
    {
        var chart = Chart(4, [new TimingPoint(0, 500)],
            Tap(0, 0), Tap(0, 250), Tap(0, 500),
            Tap(1, 0), Tap(1, 375), Tap(2, 1000), Tap(2, 1250));
        var f2 = TypedGapBackoffResearch.Evaluate(chart);
        var f21 = ExactGapTimingIdentityResearch.Evaluate(chart);
        foreach (var expected in f2.Resolutions)
        {
            var actual = f21.Assessments.Single(x =>
                x.TargetPreviousObservationId == expected.TargetPreviousObservationId
                && x.TargetNextObservationId == expected.TargetNextObservationId
                && x.IdentityKind == GapTimingIdentityKind.FileExactDecimal
                && x.HoldoutKind == GapTimingHoldoutKind.F2HeadGroup);
            Assert.Equal(expected.LocalEvidence.EvidenceState, actual.LocalState);
            Assert.Equal(expected.GlobalEvidence?.EvidenceState, actual.GlobalState);
            Assert.Equal(expected.FinalAction, actual.HypotheticalAction);
        }
    }

    [Fact]
    public void EqualityIsReflexiveSymmetricAndTransitive()
    {
        var value = Evaluate(4, [new(0, 500)], Tap(0, 0), Tap(0, 250)).Occurrences.Single()
            .ExactSegmentTraversalIdentity;
        var sameA = value with { };
        var sameB = value with { };
        Assert.True(ExactGapTimingIdentityResearch.Equivalent(value, value));
        Assert.Equal(ExactGapTimingIdentityResearch.Equivalent(value, sameA),
            ExactGapTimingIdentityResearch.Equivalent(sameA, value));
        Assert.True(ExactGapTimingIdentityResearch.Equivalent(value, sameA)
            && ExactGapTimingIdentityResearch.Equivalent(sameA, sameB)
            && ExactGapTimingIdentityResearch.Equivalent(value, sameB));
    }

    [Fact]
    public void MembershipDoesNotDependOnOriginalEnumerationOrder()
    {
        var objects = new[] { Tap(0, 0), Tap(0, 250), Tap(0, 500), Tap(1, 0), Tap(1, 250) };
        var left = Evaluate(4, [new(0, 500)], objects);
        var right = Evaluate(4, [new(0, 500)], objects.Reverse().ToArray());
        Assert.Equal(IdentityMultiset(left), IdentityMultiset(right));
    }

    [Fact]
    public void IntegralMillisecondNominalHalfBeatRepeatsExactly()
    {
        var result = Evaluate(4, [new(0, 500)], Tap(0, 0), Tap(0, 250), Tap(0, 500));
        Assert.Single(result.Occurrences.Select(x => x.ExactSegmentTraversalIdentity).Distinct());
    }

    [Fact]
    public void NonIntegralSerializationCanSplitOneKnownNominalRelation()
    {
        // Synthetic ground truth: each source interval was nominally 1/2 beat at 499 ms/beat.
        // Integer serialization produces alternating 250/249 ms and the file contains no nominal label.
        var result = Evaluate(4, [new(0, 499)], Tap(0, 0), Tap(0, 250), Tap(0, 499), Tap(0, 749));
        Assert.Equal(2, result.Occurrences.Select(x => x.FileExactIdentity).Distinct().Count());
        Assert.Equal(2, result.Occurrences.Select(x => x.ExactSegmentTraversalIdentity).Distinct().Count());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void SameResearchImplementationHandlesEverySupportedKeymode(int keys)
    {
        var result = Evaluate(keys, [new(0, 500)], Tap(0, 0), Tap(0, 250));
        Assert.Equal(keys, result.KeyCount);
        Assert.Single(result.Occurrences);
    }

    [Fact]
    public void SeveralLanesRetainTheSameExactRelation()
    {
        var result = Evaluate(4, [new(0, 500)],
            Tap(0, 0), Tap(0, 250), Tap(1, 1000), Tap(1, 1250));
        Assert.Single(result.Occurrences.Select(x => x.FileExactIdentity).Distinct());
        Assert.Single(result.Occurrences.Select(x => x.ExactSegmentTraversalIdentity).Distinct());
    }

    [Fact]
    public void NearbyIntentionalIntervalsDoNotMerge()
    {
        var result = Evaluate(4, [new(0, 500)],
            Tap(0, 0), Tap(0, 249), Tap(1, 1000), Tap(1, 1250));
        Assert.Equal(2, result.Occurrences.Select(x => x.FileExactIdentity).Distinct().Count());
        Assert.Equal(2, result.Occurrences.Select(x => x.ExactSegmentTraversalIdentity).Distinct().Count());
    }

    [Fact]
    public void EqualMillisecondsAtDifferentBpmDoNotMergeDifferentBeatRelations()
    {
        var result = Evaluate(4, [new(0, 500), new(1000, 250)],
            Tap(0, 0), Tap(0, 250), Tap(1, 1000), Tap(1, 1250));
        Assert.Equal(new[] { .5m, 1m }, result.Occurrences.Select(x => x.FileExactGapBeats));
        Assert.Equal(2, result.Occurrences.Select(x => x.ExactSegmentTraversalIdentity).Distinct().Count());
    }

    [Theory]
    [InlineData(ManiaObjectType.Tap, ManiaObjectType.Tap, TypedGapTransitionKind.TapHeadToTapHead)]
    [InlineData(ManiaObjectType.Tap, ManiaObjectType.LongNote, TypedGapTransitionKind.TapHeadToLongNoteHead)]
    [InlineData(ManiaObjectType.LongNote, ManiaObjectType.Tap, TypedGapTransitionKind.LongNoteReleaseToTapHead)]
    [InlineData(ManiaObjectType.LongNote, ManiaObjectType.LongNote, TypedGapTransitionKind.LongNoteReleaseToLongNoteHead)]
    public void TransitionAndEndpointKindsRemainExact(ManiaObjectType previousType,
        ManiaObjectType nextType, TypedGapTransitionKind expected)
    {
        var previous = previousType == ManiaObjectType.Tap ? Tap(0, 0) : Ln(0, 0, 500);
        var next = nextType == ManiaObjectType.Tap ? Tap(0, 750) : Ln(0, 750, 1000);
        var occurrence = Evaluate(4, [new(0, 500)], previous, next).Occurrences.Single();
        Assert.Equal(expected, occurrence.TransitionKind);
        Assert.Equal(previousType == ManiaObjectType.Tap ? TypedGapEndpointType.TapHead
            : TypedGapEndpointType.LongNoteRelease, occurrence.SourceEndpoint.Type);
        Assert.Equal(nextType == ManiaObjectType.Tap ? TypedGapEndpointType.TapHead
            : TypedGapEndpointType.LongNoteHead, occurrence.DestinationEndpoint.Type);
    }

    [Fact]
    public void SameSegmentTraversalIsExact()
    {
        var occurrence = Evaluate(4, [new(0, 500)], Tap(0, 250), Tap(0, 750)).Occurrences.Single();
        Assert.True(occurrence.SameTimingSegment);
        var part = Assert.Single(occurrence.SegmentTraversal);
        Assert.Equal(500m, part.BeatLength);
        Assert.Equal(1m, part.BeatSpan);
    }

    [Fact]
    public void CrossBpmTraversalRetainsEveryExactPart()
    {
        var occurrence = Evaluate(4, [new(0, 500), new(1000, 250)],
            Tap(0, 750), Tap(0, 1125)).Occurrences.Single();
        Assert.False(occurrence.SameTimingSegment);
        Assert.Equal(1, occurrence.TimingPointBoundariesCrossed);
        Assert.Equal(new[] { new ExactTimingTraversalPart(500, .5m),
            new ExactTimingTraversalPart(250, .5m) }, occurrence.SegmentTraversal);
        Assert.Equal(1m, occurrence.FileExactGapBeats);
    }

    [Fact]
    public void LongNoteHeadAndReleaseCanCrossBpmWithoutConflation()
    {
        var occurrence = Evaluate(4, [new(0, 500), new(1000, 250)],
            Ln(0, 750, 1125), Tap(0, 1375)).Occurrences.Single();
        Assert.True(occurrence.PreviousLongNoteCrossesTimingSegment);
        Assert.Equal(TypedGapEndpointType.LongNoteRelease, occurrence.SourceEndpoint.Type);
        Assert.Equal(1m, occurrence.FileExactGapBeats);
    }

    [Fact]
    public void ReleaseToNextAfterBpmUsesReleaseSegment()
    {
        var occurrence = Evaluate(4, [new(0, 500), new(1000, 250)],
            Ln(0, 0, 1250), Tap(0, 1500)).Occurrences.Single();
        Assert.Equal(1, occurrence.SourceEndpoint.TimingSegmentIndex);
        Assert.True(occurrence.SameTimingSegment);
        Assert.Equal(1m, occurrence.FileExactGapBeats);
    }

    [Fact]
    public void InheritedTimingPointDoesNotAffectBeatIdentity()
    {
        const string withInherited = """
osu file format v14
[General]
Mode:3
[Difficulty]
CircleSize:4
[TimingPoints]
0,500,4,2,0,100,1,0
250,-50,4,2,0,100,0,0
[HitObjects]
64,192,0,1,0,0:0:0:0:
64,192,250,1,0,0:0:0:0:
""";
        var chart = OsuBeatmap.Parse(withInherited);
        Assert.Single(chart.TimingPoints);
        Assert.Equal(.5m, ExactGapTimingIdentityResearch.Evaluate(chart)
            .Occurrences.Single().FileExactGapBeats);
    }

    [Fact]
    public void EndpointAwareHoldoutExcludesOtherLongNotesSharingRelease()
    {
        var result = Evaluate(4, [new(0, 500)],
            Ln(0, 0, 1000), Tap(0, 1250), Ln(1, 500, 1000), Tap(1, 1500));
        var target = result.Occurrences.Single(x => x.Lane == 0);
        Assert.True(target.SourceReleaseEventShared);
        Assert.Contains(new OriginalObservationId(2), target.TransitionEndpointGroupExclusion);
        Assert.DoesNotContain(new OriginalObservationId(2), target.F2HeadGroupExclusion);
        var oldAssessment = Assessment(result, target, GapTimingHoldoutKind.F2HeadGroup);
        var endpointAssessment = Assessment(result, target, GapTimingHoldoutKind.TransitionEndpointGroup);
        Assert.Equal(ComparableEvidenceState.LocalMismatch, oldAssessment.GlobalState);
        Assert.Equal(ComparableEvidenceState.NoComparableContext, endpointAssessment.GlobalState);
        Assert.Equal(0, result.TargetEndpointLeakageCount);
        Assert.Empty(result.TargetEndpointLeakageViolations);
    }

    [Fact]
    public void EndpointLeakageAuditDetectsAnIntentionallyUnfilteredDonorWithProvenance()
    {
        var result = Evaluate(4, [new(0, 500)],
            Ln(0, 0, 1000), Tap(0, 1250), Ln(1, 500, 1000), Tap(1, 1500));
        var target = result.Occurrences.Single(x => x.Lane == 0);
        var invalidDonor = result.Occurrences.Single(x => x.Lane == 1);

        var violations = ExactGapTimingIdentityResearch.AuditEndpointLeakage(target,
            GapTimingIdentityKind.FileExactDecimal, GapTimingHoldoutKind.TransitionEndpointGroup,
            TypedGapEvidenceScope.GlobalChart, [invalidDonor]);

        var violation = Assert.Single(violations);
        Assert.Equal(invalidDonor.PreviousObservationId, violation.DonorPreviousObservationId);
        Assert.Equal(invalidDonor.NextObservationId, violation.DonorNextObservationId);
        Assert.Contains(new OriginalObservationId(2), violation.LeakedObservationIds);
        Assert.Equal(TypedGapEvidenceScope.GlobalChart, violation.Scope);
    }

    [Fact]
    public void EndpointLeakageAuditAcceptsCorrectlyFilteredDonors()
    {
        var result = Evaluate(4, [new(0, 500)],
            Ln(0, 0, 1000), Tap(0, 1250), Ln(1, 500, 1000), Tap(1, 1500));
        var target = result.Occurrences.Single(x => x.Lane == 0);
        var eligible = result.Occurrences.Where(donor =>
            !target.TransitionEndpointGroupExclusion.Contains(donor.PreviousObservationId)
            && !target.TransitionEndpointGroupExclusion.Contains(donor.NextObservationId));

        Assert.Empty(ExactGapTimingIdentityResearch.AuditEndpointLeakage(target,
            GapTimingIdentityKind.FileExactDecimal, GapTimingHoldoutKind.TransitionEndpointGroup,
            TypedGapEvidenceScope.GlobalChart, eligible));
    }

    [Fact]
    public void WholeHeadGroupStillExcludesEverySimultaneousHead()
    {
        var result = Evaluate(4, [new(0, 500)],
            Tap(0, 0), Tap(1, 0), Tap(0, 250), Tap(1, 500));
        var target = result.Occurrences.Single(x => x.Lane == 0);
        Assert.Contains(new OriginalObservationId(1), target.F2HeadGroupExclusion);
        Assert.Contains(new OriginalObservationId(1), target.TransitionEndpointGroupExclusion);
    }

    [Fact]
    public void AddedObjectsNeverTeachTimingIdentity()
    {
        var chart = Chart(4, [new(0, 500)], Tap(0, 0), Tap(0, 250));
        var withSynthetic = new ManiaChart
        {
            KeyCount = chart.KeyCount, Lines = chart.Lines, OriginalObjects = chart.OriginalObjects,
            TimingPoints = chart.TimingPoints, AddedObjects = [ManiaObject.Tap(0, 500, true)]
        };
        Assert.Equal(ExactGapTimingIdentityResearchJson.Serialize(ExactGapTimingIdentityResearch.Evaluate(chart)),
            ExactGapTimingIdentityResearchJson.Serialize(ExactGapTimingIdentityResearch.Evaluate(withSynthetic)));
    }

    [Fact]
    public void SeparateChartsHaveSeparateFingerprints()
    {
        var left = Evaluate(4, [new(0, 500)], Tap(0, 0), Tap(0, 250));
        var right = Evaluate(4, [new(0, 500)], Tap(0, 1000), Tap(0, 1250));
        Assert.NotEqual(left.ChartFingerprint, right.ChartFingerprint);
        Assert.All(left.Occurrences, x => Assert.Equal(left.ChartFingerprint, x.ChartFingerprint));
        Assert.All(right.Occurrences, x => Assert.Equal(right.ChartFingerprint, x.ChartFingerprint));
    }

    [Fact]
    public void ResearchIsDeterministicAndOwnsNoRandomParameter()
    {
        var chart = Chart(4, [new(0, 500)], Tap(0, 0), Tap(0, 250), Tap(0, 500));
        Assert.Equal(ExactGapTimingIdentityResearchJson.Serialize(ExactGapTimingIdentityResearch.Evaluate(chart)),
            ExactGapTimingIdentityResearchJson.Serialize(ExactGapTimingIdentityResearch.Evaluate(chart)));
        Assert.DoesNotContain(typeof(ExactGapTimingIdentityResearch).GetMethods(
                BindingFlags.Public | BindingFlags.Static).SelectMany(x => x.GetParameters()),
            x => typeof(IRandomSource).IsAssignableFrom(x.ParameterType));
    }

    [Fact]
    public void SerializationRetainsProvenanceAndContainsNoAuthorityPolicy()
    {
        var result = Evaluate(4, [new(0, 500)], Ln(0, 0, 500), Tap(0, 750));
        var json = ExactGapTimingIdentityResearchJson.Serialize(result);
        Assert.Contains("fileExactGapBeats", json);
        Assert.Contains("exactSegmentTraversalIdentity", json);
        Assert.Contains("previousObservationId", json);
        Assert.Contains("chartFingerprint", json);
        foreach (var forbidden in new[] { "nearest", "epsilon", "confidence", "probability",
                     "mapperSupport", "frequencyWeight", "bestIdentity" })
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
    }

    private static GapTimingEvidenceAssessment Assessment(ExactGapTimingIdentityResearchResult result,
        GapTimingIdentityOccurrence target, GapTimingHoldoutKind holdout) => result.Assessments.Single(x =>
            x.TargetPreviousObservationId == target.PreviousObservationId
            && x.TargetNextObservationId == target.NextObservationId
            && x.IdentityKind == GapTimingIdentityKind.FileExactDecimal && x.HoldoutKind == holdout);

    private static string[] IdentityMultiset(ExactGapTimingIdentityResearchResult result) => result.Occurrences
        .Select(x => $"{x.Lane}|{x.TransitionKind}|{x.FileExactIdentity.CanonicalValue}|" +
            x.ExactSegmentTraversalIdentity.CanonicalValue).Order(StringComparer.Ordinal).ToArray();

    private static ExactGapTimingIdentityResearchResult Evaluate(int keys,
        IReadOnlyList<TimingPoint> timings, params ManiaObject[] objects) =>
        ExactGapTimingIdentityResearch.Evaluate(Chart(keys, timings, objects));

    private static ManiaChart Chart(int keys, IReadOnlyList<TimingPoint> timings,
        params ManiaObject[] objects) => new()
        {
            KeyCount = keys,
            Lines = [],
            OriginalObjects = objects.Select((x, index) => x with { Sequence = index }).ToArray(),
            TimingPoints = timings
        };

    private static ManiaObject Tap(int lane, int time) => ManiaObject.Tap(lane, time);
    private static ManiaObject Ln(int lane, int start, int end) => ManiaObject.Ln(lane, start, end);
}
