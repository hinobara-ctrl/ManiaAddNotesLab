using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseG10InteriorRelationFeasibilityTests
{
    [Fact]
    public void CompleteOccurrencesClassifyContainedEqualEndAndCrossingExactly()
    {
        var result = Evaluate(Chart(
            Ln(0, 0, 5000),
            Ln(1, 1000, 2000),
            Ln(2, 1500, 5000),
            Ln(3, 2000, 6000)));

        Assert.Contains(result.CompleteRelations, x => x.RelationClass == InteriorRelationClass.Contained);
        Assert.Contains(result.CompleteRelations, x => x.RelationClass == InteriorRelationClass.EqualEnd);
        Assert.Contains(result.CompleteRelations, x => x.RelationClass == InteriorRelationClass.Crossing);
        Assert.All(result.CompleteRelations, x => Assert.StartsWith(result.ChartFingerprint, x.ChartFingerprint));
    }

    [Fact]
    public void AnchorKindsPreserveHeadReleaseAndHeadPlusRelease()
    {
        var result = Evaluate(Chart(
            Ln(0, 0, 5000),
            Tap(1, 1000),
            Ln(2, 250, 1500),
            Ln(3, 500, 2000),
            Tap(1, 2000)));

        Assert.Contains(result.StructuralAnchors, x => x.AnchorTime == 1000 && x.AnchorKind == InteriorAnchorKind.Head);
        Assert.Contains(result.StructuralAnchors, x => x.AnchorTime == 1500 && x.AnchorKind == InteriorAnchorKind.Release);
        Assert.Contains(result.StructuralAnchors, x => x.AnchorTime == 2000
            && x.AnchorKind == (InteriorAnchorKind.Head | InteriorAnchorKind.Release));
    }

    [Fact]
    public void SameAndDifferentLaneAreDescriptiveAndGeometryIsSeparate()
    {
        var result = Evaluate(Chart(Ln(0, 0, 5000), Ln(0, 1000, 2000), Ln(2, 1500, 2500)));

        Assert.Contains(result.CompleteRelations, x => x.SameLane && !x.GeometryValidOnOriginalLaneAfterWitnessHoldout);
        Assert.Contains(result.CompleteRelations, x => !x.SameLane && x.GeometryValidOnOriginalLaneAfterWitnessHoldout);
    }

    [Fact]
    public void CompleteWitnessNeverCombinesMarginalPiecesFromDifferentObjects()
    {
        var result = Evaluate(Chart(Ln(0, 0, 5000), Tap(1, 1000), Ln(2, 250, 2000)));
        var anchor = result.StructuralAnchors.Single(x => x.ParentLongNoteId.Value == 0 && x.AnchorTime == 1000);

        Assert.Empty(anchor.CompleteRelationOccurrenceIds);
        Assert.True(result.MarginalOnlyAnchorCount > 0);
    }

    [Fact]
    public void HoldoutExcludesTargetReleaseFutureSameEventAndCrossChartByConstruction()
    {
        var result = Evaluate(RepeatedRelations(includeAlternative: false));
        var target = result.Holdouts.Where(x => x.HoldoutKind == InteriorHoldoutKind.TargetObservation)
            .OrderByDescending(x => result.CompleteRelations.Single(y => y.OccurrenceId == x.OccurrenceId).AnchorTime)
            .First();

        Assert.True(target.ExactJointSupported);
        Assert.Equal(0, target.TargetLeakageCount + target.ReleaseLeakageCount + target.FutureLeakageCount
            + target.SameEventLeakageCount + target.SyntheticLeakageCount + target.CrossChartLeakageCount);
        Assert.All(target.DonorOccurrenceIds, id => Assert.NotEqual(target.OccurrenceId, id));
    }

    [Fact]
    public void ParentHoldoutIsStrongerAndKeepsParentOnlyAsQueryContext()
    {
        var result = Evaluate(RepeatedRelations(includeAlternative: false));
        var targetOccurrence = result.CompleteRelations.OrderByDescending(x => x.AnchorTime).First();
        var target = result.Holdouts.Single(x => x.OccurrenceId == targetOccurrence.OccurrenceId
            && x.HoldoutKind == InteriorHoldoutKind.TargetObservation);
        var parent = result.Holdouts.Single(x => x.OccurrenceId == targetOccurrence.OccurrenceId
            && x.HoldoutKind == InteriorHoldoutKind.ParentOccurrence);

        Assert.True(parent.ComparableDonorCount <= target.ComparableDonorCount);
        Assert.Equal(0, parent.ParentLeakageCount);
    }

    [Fact]
    public void AlternativesAreRepresentedWithoutSelectingAWinner()
    {
        var result = Evaluate(RepeatedRelations(includeAlternative: true));

        Assert.Contains(result.Holdouts, x => x.ExactJointSupported
            && x.AlternativeState == InteriorAlternativeState.ObservedAmongAlternatives);
    }

    [Fact]
    public void SyntheticObjectsAreIgnoredAndCannotTeachStyle()
    {
        var chart = RepeatedRelations(includeAlternative: false);
        var synthetic = Ln(4, 8500, 8800) with { IsSynthetic = true, Origin = AddedObjectOrigin.LnInteriorOpportunity };
        chart = chart.WithObjects([.. chart.OriginalObjects, synthetic]);
        var result = Evaluate(chart);

        Assert.Equal(1, result.IgnoredSyntheticObjectCount);
        Assert.DoesNotContain(result.CompleteRelations, x => x.AnchorTime == 8500);
    }

    [Fact]
    public void SourceContextAnchorSupportAndCapAreAuditedSeparately()
    {
        var chart = Chart(
            Ln(0, 0, 6000), Ln(1, 1000, 2000), Ln(2, 2000, 3000), Ln(3, 3000, 4000),
            Ln(4, -500, 500), Ln(5, 500, 2500), Ln(6, 2500, 4500));
        var result = Evaluate(chart);
        var parent = result.CurrentGateAudit.Where(x => x.ParentLongNoteId.Value == 0).ToArray();

        Assert.True(parent.Length >= 3);
        Assert.Equal(2, parent.Count(x => x.CurrentOpportunity));
        Assert.Contains(parent, x => x.FirstExclusionGate == InteriorLegacyGate.Cap);
        Assert.Equal(0, result.CurrentInteriorOpportunityMirrorMismatchCount);
    }

    [Fact]
    public void StructuralRelationCanBeExcludedBeforeCurrentGenerationGates()
    {
        var result = Evaluate(Chart(Ln(0, 0, 1000), Ln(1, 500, 900)));

        Assert.NotEmpty(result.CompleteRelations);
        Assert.Contains(result.CurrentGateAudit, x => x.FirstExclusionGate == InteriorLegacyGate.SourceLength);
    }

    [Fact]
    public void OrderingAndSemanticIdsAreDeterministicAndResearchUsesZeroRng()
    {
        var chart = RepeatedRelations(includeAlternative: true);
        var first = Evaluate(chart);
        var second = Evaluate(chart);

        Assert.Equal(first.CompleteRelations.Select(x => x.OccurrenceId),
            second.CompleteRelations.Select(x => x.OccurrenceId));
        Assert.Equal(first.CurrentGateAudit.Select(x => (x.AnchorId, x.FirstExclusionGate, x.RankPosition)),
            second.CurrentGateAudit.Select(x => (x.AnchorId, x.FirstExclusionGate, x.RankPosition)));
        Assert.Equal(first.Holdouts.Select(x => (x.OccurrenceId, x.HoldoutKind, x.AlternativeState)),
            second.Holdouts.Select(x => (x.OccurrenceId, x.HoldoutKind, x.AlternativeState)));
        Assert.Equal(0, first.ResearchRngCalls);
    }

    [Fact]
    public void IndependentAuditorDetectsInjectedTargetWitness()
    {
        var (target, _) = HoldoutFixture();
        var audit = InteriorRelationHoldoutAuditor.Audit(target, InteriorHoldoutKind.TargetObservation,
            [new(target)]);

        Assert.True(audit.TargetLeakageCount > 0);
    }

    [Fact]
    public void IndependentAuditorDetectsInjectedSameParentForParentOccurrence()
    {
        var (target, donor) = HoldoutFixture();
        var bad = donor with { ParentLongNoteId = target.ParentLongNoteId };
        var audit = InteriorRelationHoldoutAuditor.Audit(target, InteriorHoldoutKind.ParentOccurrence,
            [new(bad)]);

        Assert.True(audit.ParentLeakageCount > 0);
    }

    [Fact]
    public void IndependentAuditorDetectsInjectedTargetReleaseDonor()
    {
        var (target, donor) = HoldoutFixture();
        var bad = donor with { WitnessLongNoteId = target.ParentLongNoteId };
        var audit = InteriorRelationHoldoutAuditor.Audit(target, InteriorHoldoutKind.TargetObservation,
            [new(bad)]);

        Assert.True(audit.ReleaseLeakageCount > 0);
    }

    [Fact]
    public void IndependentAuditorDetectsInjectedFutureDonor()
    {
        var (target, donor) = HoldoutFixture();
        var bad = donor with { AnchorTime = target.AnchorTime };
        var audit = InteriorRelationHoldoutAuditor.Audit(target, InteriorHoldoutKind.TargetObservation,
            [new(bad)]);

        Assert.True(audit.FutureLeakageCount > 0);
    }

    [Fact]
    public void IndependentAuditorDetectsInjectedSameEventDonor()
    {
        var (target, donor) = HoldoutFixture();
        var bad = donor with { ParentLongNoteId = target.WitnessLongNoteId };
        var audit = InteriorRelationHoldoutAuditor.Audit(target, InteriorHoldoutKind.TargetObservation,
            [new(bad)]);

        Assert.True(audit.SameEventLeakageCount > 0);
    }

    [Fact]
    public void IndependentAuditorDetectsInjectedSyntheticDonor()
    {
        var (target, donor) = HoldoutFixture();
        var audit = InteriorRelationHoldoutAuditor.Audit(target, InteriorHoldoutKind.TargetObservation,
            [new(donor, IsSynthetic: true)]);

        Assert.True(audit.SyntheticLeakageCount > 0);
    }

    [Fact]
    public void IndependentAuditorDetectsInjectedCrossChartDonor()
    {
        var (target, donor) = HoldoutFixture();
        var bad = donor with { ChartFingerprint = "deliberately-different-chart" };
        var audit = InteriorRelationHoldoutAuditor.Audit(target, InteriorHoldoutKind.TargetObservation,
            [new(bad)]);

        Assert.True(audit.CrossChartLeakageCount > 0);
    }

    [Fact]
    public void IndependentAuditorAcceptsNormalConstructedDonorsAndRealPipelineMeasuresZero()
    {
        var result = Evaluate(RepeatedRelations(includeAlternative: true));
        var target = result.CompleteRelations.OrderByDescending(x => x.AnchorTime).First();
        var holdout = result.Holdouts.Single(x => x.OccurrenceId == target.OccurrenceId
            && x.HoldoutKind == InteriorHoldoutKind.TargetObservation);
        var byId = result.CompleteRelations.ToDictionary(x => x.OccurrenceId);
        var accepted = holdout.DonorOccurrenceIds.Select(x => new InteriorRelationHoldoutDonor(byId[x]));
        var audit = InteriorRelationHoldoutAuditor.Audit(target, holdout.HoldoutKind, accepted);

        Assert.Equal(0, audit.Total);
        Assert.Equal(0, holdout.TargetLeakageCount + holdout.ParentLeakageCount
            + holdout.ReleaseLeakageCount + holdout.FutureLeakageCount + holdout.SameEventLeakageCount
            + holdout.SyntheticLeakageCount + holdout.CrossChartLeakageCount);
    }

    [Fact]
    public void TamperedSemanticIdIsDetectedWithoutRandomness()
    {
        var (occurrence, _) = HoldoutFixture();

        Assert.True(InteriorRelationHoldoutAuditor.HasCanonicalSemanticId(occurrence));
        Assert.False(InteriorRelationHoldoutAuditor.HasCanonicalSemanticId(
            occurrence with { OccurrenceId = new string('0', 64) }));
    }

    [Fact]
    public void SkewedFrequencyRemainsAlternativesWithoutWinnerAuthority()
    {
        var result = Evaluate(RepeatedRelations(includeAlternative: true));
        var alternatives = result.CompleteRelations.GroupBy(x => x.RelationSignature)
            .Where(x => x.Any()).Take(2).Select(x => x.First()).ToArray();
        Assert.Equal(2, alternatives.Length);
        var target = alternatives[0];
        var skewed = Enumerable.Repeat(new InteriorRelationHoldoutDonor(alternatives[0]), 8)
            .Append(new InteriorRelationHoldoutDonor(alternatives[1]));

        var assessment = InteriorRelationHoldoutAuditor.Assess(target, skewed);

        Assert.True(assessment.ExactJointSupported);
        Assert.Equal(2, assessment.DistinctRelationCount);
        Assert.Equal(InteriorAlternativeState.ObservedAmongAlternatives, assessment.AlternativeState);
    }

    [Fact]
    public void MarginalPiecesFromDifferentDonorsDoNotBecomeJointSupport()
    {
        var (target, donor) = HoldoutFixture();
        var durationOnly = donor with
        {
            RelationSignature = "duration-only-not-joint",
            DurationFromAnchorBeats = target.DurationFromAnchorBeats,
            RelationClass = target.RelationClass == InteriorRelationClass.Contained
                ? InteriorRelationClass.Crossing : InteriorRelationClass.Contained,
            OffsetFromParentEndBeats = target.OffsetFromParentEndBeats + 1
        };
        var endpointOnly = donor with
        {
            RelationSignature = "endpoint-only-not-joint",
            DurationFromAnchorBeats = target.DurationFromAnchorBeats + 1,
            RelationClass = target.RelationClass,
            OffsetFromParentEndBeats = target.OffsetFromParentEndBeats
        };

        var assessment = InteriorRelationHoldoutAuditor.Assess(target,
            [new(durationOnly), new(endpointOnly)]);

        Assert.False(assessment.ExactJointSupported);
        Assert.True(assessment.DurationMarginalSupported);
        Assert.True(assessment.EndpointMarginalSupported);
        Assert.True(assessment.MarginalOnly);
    }

    [Fact]
    public void AnchorSupportAttritionIsIsolatedAfterEarlierGatesPass()
    {
        var result = Evaluate(Chart(
            Ln(0, 0, 4000), Ln(1, 1000, 4500),
            Ln(2, -1500, 0), Ln(3, -1000, 0), Ln(4, -500, 0)));
        var parentId = result.CompleteRelations.Single(x => x.ParentStartTime == 0).ParentLongNoteId;
        var audit = result.CurrentGateAudit.Single(x => x.ParentLongNoteId == parentId);

        Assert.True(audit.CompleteRelationCount > 0);
        Assert.True(audit.SourceLengthPassed);
        Assert.True(audit.SourceContextPassed);
        Assert.True(audit.RelativeOrAbsoluteLengthPassed);
        Assert.False(audit.AnchorSupportPassed);
        Assert.Equal(InteriorLegacyGate.AnchorSupport, audit.FirstExclusionGate);
        Assert.False(audit.CurrentOpportunity);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void StructuralCensusSupportsAllEngineKeymodes(int keys)
    {
        var lane = Math.Min(1, keys - 1);
        var result = Evaluate(Chart(keys, Ln(0, 0, 5000), Ln(lane, 1000, 2000)));
        Assert.Single(result.CompleteRelations);
        Assert.Equal(keys, result.KeyCount);
    }

    private static InteriorRelationFeasibilityResult Evaluate(ManiaChart chart) =>
        InteriorRelationFeasibilityResearch.Evaluate(chart);

    private static ManiaChart RepeatedRelations(bool includeAlternative)
    {
        var objects = new List<ManiaObject>
        {
            Ln(0, 0, 4000), Ln(1, 1000, 2000),
            Ln(2, 5000, 9000), Ln(3, 6000, 7000),
            Ln(4, 10000, 14000), Ln(5, 11000, 12000)
        };
        if (includeAlternative) objects.Insert(4, Ln(6, 6000, 7500));
        return Chart(objects.ToArray());
    }

    private static (InteriorRelationOccurrence Target, InteriorRelationOccurrence Donor) HoldoutFixture()
    {
        var result = Evaluate(RepeatedRelations(includeAlternative: true));
        var target = result.CompleteRelations.OrderByDescending(x => x.AnchorTime).First();
        var holdout = result.Holdouts.Single(x => x.OccurrenceId == target.OccurrenceId
            && x.HoldoutKind == InteriorHoldoutKind.TargetObservation);
        var donor = result.CompleteRelations.Single(x => x.OccurrenceId == holdout.DonorOccurrenceIds[0]);
        return (target, donor);
    }

    private static ManiaChart Chart(params ManiaObject[] objects) => Chart(7, objects);

    private static ManiaChart Chart(int keys, params ManiaObject[] objects) => new()
    {
        KeyCount = keys,
        Lines = [],
        OriginalObjects = objects.Select((x, i) => x with { Sequence = i }).ToArray(),
        TimingPoints = [new TimingPoint(0, 500)]
    };

    private static ManiaObject Ln(int lane, int start, int end) => ManiaObject.Ln(lane, start, end);
    private static ManiaObject Tap(int lane, int time) => ManiaObject.Tap(lane, time);
}

file static class G10ChartExtensions
{
    public static ManiaChart WithObjects(this ManiaChart chart, IReadOnlyList<ManiaObject> objects) => new()
    {
        KeyCount = chart.KeyCount,
        Lines = chart.Lines,
        OriginalObjects = objects,
        TimingPoints = chart.TimingPoints
    };
}
