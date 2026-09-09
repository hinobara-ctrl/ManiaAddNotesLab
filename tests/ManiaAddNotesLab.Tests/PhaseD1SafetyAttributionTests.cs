using System.Text;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseD1SafetyAttributionTests
{
    private const string Chart = "SYNTHETIC-CHART";

    [Fact]
    public void RawBoundaryRelationIsNotAutomaticallyAHardViolation()
    {
        var audit = Audit(4, Ln("a", 0, 2, 0, 1000), Ln("b", 2, 4, 1000, 2000));
        Assert.Equal(RawGeometryRelationKind.TouchingBoundary, Assert.Single(audit.RawRelations).Kind);
        Assert.Empty(audit.HardViolations);
    }

    [Fact]
    public void TapAtLongNoteReleaseIsIllegalButLnBoundaryEqualityIsNotOverlap()
    {
        var tap = Audit(4, Ln("ln", 0, 2, 0, 1000), Tap("tap", 2, 1000));
        Assert.Equal(GeometryHardViolationKind.TapOnHeldLongNote, Assert.Single(tap.HardViolations).Violation);
        var ln = Audit(4, Ln("left", 0, 2, 0, 1000), Ln("right", 2, 3, 1000, 1500));
        Assert.Empty(ln.HardViolations);
    }

    [Fact]
    public void SameLaneTapHeadsConflictAndDifferentLanesDoNot()
    {
        Assert.Equal(GeometryHardViolationKind.DuplicateHead,
            Assert.Single(Audit(4, Tap("a", 1, 500), Tap("b", 1, 500)).HardViolations).Violation);
        Assert.Empty(Audit(4, Tap("a", 1, 500, 0), Tap("b", 1, 500, 1)).RawRelations);
    }

    [Fact]
    public void StrictLnContainmentAndCrossingAreViolations()
    {
        Assert.Equal(GeometryHardViolationKind.LongNoteOverlap,
            Assert.Single(Audit(4, Ln("a", 0, 4, 0, 2000), Ln("b", 1, 2, 500, 1000)).HardViolations).Violation);
        Assert.Equal(GeometryHardViolationKind.LongNoteOverlap,
            Assert.Single(Audit(4, Ln("a", 0, 3, 0, 1500), Ln("b", 2, 4, 1000, 2000)).HardViolations).Violation);
    }

    [Fact]
    public void InvalidBeatAndSerializedDurationsRemainSeparate()
    {
        var beat = Audit(4, Ln("a", 1, 1, 500, 600));
        Assert.Equal(GeometryHardViolationKind.InvalidBeatDuration, Assert.Single(beat.HardViolations).Violation);
        var serialized = Audit(4, Ln("b", 1, 2, 500, 500));
        Assert.Equal(GeometryHardViolationKind.InvalidSerializedDuration,
            Assert.Single(serialized.HardViolations).Violation);
    }

    [Fact]
    public void RequiredGapIsMutationSpecificAndSeparateFromRawOverlap()
    {
        var before = new[] { Ln("a", 0, 2, 0, 1000) };
        var candidate = new GeometryMutation(Ln("b", 2.1m, 3, 1050, 1500), .25m,
            Provenance("legacy", GeometryMutationCause.Legacy));
        var finding = GeometrySafetyAttributionResearch.EvaluateMutation(Chart, 4, before, candidate);
        Assert.Equal(GeometryHardViolationKind.RequiredSpacing, finding.Violation);
        Assert.Equal(GeometrySafetyAttribution.LegacyIntroducedViolation, finding.Attribution);
    }

    [Theory]
    [InlineData(GeometryMutationCause.Legacy, GeometrySafetyAttribution.LegacyIntroducedViolation)]
    [InlineData(GeometryMutationCause.TreatmentDirect, GeometrySafetyAttribution.TreatmentDirectIntroducedViolation)]
    [InlineData(GeometryMutationCause.TreatmentDownstream, GeometrySafetyAttribution.TreatmentDownstreamIntroducedViolation)]
    [InlineData(GeometryMutationCause.Articulation, GeometrySafetyAttribution.ArticulationIntroducedViolation)]
    public void TrueViolationUsesExplicitMutationCause(GeometryMutationCause cause,
        GeometrySafetyAttribution expected)
    {
        var finding = GeometrySafetyAttributionResearch.EvaluateMutation(Chart, 4,
            [Ln("held", 0, 3, 0, 1500)], new GeometryMutation(Tap("candidate", 2, 1000), 0,
                Provenance("mutation", cause)));
        Assert.Equal(expected, finding.Attribution);
    }

    [Fact]
    public void MissingProvenanceIsUnattributableBadControl()
    {
        var finding = GeometrySafetyAttributionResearch.EvaluateMutation(Chart, 4,
            [Tap("existing", 1, 500)], new GeometryMutation(Tap("candidate", 1, 500), 0, null));
        Assert.Equal(GeometrySafetyAttribution.Unattributable, finding.Attribution);
        Assert.Equal(GeometryHardViolationKind.DuplicateHead, finding.Violation);
    }

    [Fact]
    public void SerializationCollisionReceivesSerializationAttribution()
    {
        var finding = GeometrySafetyAttributionResearch.EvaluateMutation(Chart, 4, [],
            new GeometryMutation(Ln("candidate", 1, 1.01m, 500, 500), 0,
                Provenance("serialized", GeometryMutationCause.TreatmentDirect)));
        Assert.Equal(GeometrySafetyAttribution.SerializationIntroducedViolation, finding.Attribution);
    }

    [Fact]
    public void ValidSerializationDoesNotCreateViolation()
    {
        var finding = GeometrySafetyAttributionResearch.EvaluateMutation(Chart, 4, [],
            new GeometryMutation(Ln("candidate", 1, 1.01m, 500, 501), 0,
                Provenance("serialized", GeometryMutationCause.TreatmentDirect)));
        Assert.Equal(GeometrySafetyAttribution.NoViolation, finding.Attribution);
    }

    [Fact]
    public void ArticulationCanIgnoreOnlyItsExplicitParent()
    {
        var parent = Ln("parent", 0, 4, 0, 2000);
        var segment = Ln("segment", 0, 1.5m, 0, 750);
        var allowed = GeometrySafetyAttributionResearch.EvaluateMutation(Chart, 4, [parent],
            new GeometryMutation(segment, 0, Provenance("art", GeometryMutationCause.Articulation, parent.Id)));
        Assert.Equal(GeometrySafetyAttribution.NoViolation, allowed.Attribution);
        var blocked = GeometrySafetyAttributionResearch.EvaluateMutation(Chart, 4,
            [parent, Tap("other", 1, 500)],
            new GeometryMutation(segment, 0, Provenance("art", GeometryMutationCause.Articulation, parent.Id)));
        Assert.Equal(GeometrySafetyAttribution.ArticulationIntroducedViolation, blocked.Attribution);
    }

    [Fact]
    public void ComparativeAuditDoesNotChargeSourceOrControlConditionsToTreatment()
    {
        var source = new[] { Ln("s-ln", 0, 2, 0, 1000), Tap("s-tap", 2, 1000) };
        var control = source.Append(Tap("control", 4, 2000)).ToArray();
        var treatment = source.Append(Tap("treatment", 5, 2500)).ToArray();
        var audit = GeometrySafetyAttributionResearch.Compare(Chart, 4, source, control, treatment);
        Assert.All(audit.AttributedTreatment,
            x => Assert.Equal(GeometrySafetyAttribution.PreExistingOriginalCondition, x.Attribution));
    }

    [Fact]
    public void ComparativeAuditLabelsControlPresentConditionSeparately()
    {
        var source = Array.Empty<GeometrySafetyObject>();
        var control = new[] { Tap("a", 1, 500), Tap("b", 1, 500) };
        var treatment = new[] { Tap("c", 1, 500), Tap("d", 1, 500) };
        var audit = GeometrySafetyAttributionResearch.Compare(Chart, 4, source, control, treatment);
        Assert.Equal(GeometrySafetyAttribution.PreExistingControlCondition,
            Assert.Single(audit.AttributedTreatment).Attribution);
    }

    [Fact]
    public void ResolvedRawRelationIsDescriptiveOnly()
    {
        var control = new[] { Ln("a", 0, 2, 0, 1000), Ln("b", 2, 3, 1000, 1500) };
        var treatment = new[] { Ln("a2", 0, 1.5m, 0, 750), Ln("b2", 2, 3, 1000, 1500) };
        var finding = Assert.Single(GeometrySafetyAttributionResearch.Compare(Chart, 4, [], control, treatment)
            .ResolvedRelativeToControl);
        Assert.Equal(GeometryHardViolationKind.None, finding.Violation);
        Assert.Equal(GeometrySafetyAttribution.ResolvedRelativeToControl, finding.Attribution);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void KeymodeBoundariesAreSupported(int keys)
    {
        var valid = GeometrySafetyAttributionResearch.EvaluateMutation(Chart, keys, [],
            new GeometryMutation(Tap("valid", 1, 500, keys - 1), 0,
                Provenance("valid", GeometryMutationCause.Legacy)));
        Assert.Equal(GeometrySafetyAttribution.NoViolation, valid.Attribution);
        var invalid = GeometrySafetyAttributionResearch.EvaluateMutation(Chart, keys, [],
            new GeometryMutation(Tap("invalid", 1, 500, keys), 0,
                Provenance("invalid", GeometryMutationCause.Legacy)));
        Assert.Equal(GeometryHardViolationKind.OutsideLane, invalid.Violation);
    }

    [Fact]
    public void AuditIsOrderInvariantDeterministicReadOnlyAndZeroRngByConstruction()
    {
        var objects = new[] { Tap("tap", 2, 1000), Ln("ln", 0, 2, 0, 1000) };
        var before = GeometrySafetyAttributionResearch.SemanticStateHash(objects);
        var first = Audit(4, objects);
        var second = Audit(4, objects.Reverse().ToArray());
        Assert.Equal(first.RawRelations.ToArray(), second.RawRelations.ToArray());
        Assert.Equal(first.HardViolations.ToArray(), second.HardViolations.ToArray());
        Assert.Equal(first.WorkCount, second.WorkCount);
        Assert.Equal(before, GeometrySafetyAttributionResearch.SemanticStateHash(objects));
        Assert.DoesNotContain(typeof(GeometrySafetyAttributionResearch).GetMethods(),
            x => x.GetParameters().Any(p => typeof(IRandomSource).IsAssignableFrom(p.ParameterType)));
    }

    [Fact]
    public void ContractIsDeterministicShadowOnlyAndKeepsD1Parked()
    {
        var contract = D1SafetyAttributionContractResearch.Create();
        Assert.False(contract.BehaviorChange);
        Assert.Equal("C", contract.D1HistoricalOutcome);
        Assert.True(contract.D1RemainsParked);
        Assert.Equal(D1SafetyAttributionContractResearch.ComputeHash(contract),
            D1SafetyAttributionContractResearch.ComputeHash(D1SafetyAttributionContractResearch.Create()));
        Assert.Equal(D1SafetyAttributionContractResearch.SerializeArtifact(),
            D1SafetyAttributionContractResearch.SerializeArtifact());
    }

    [Fact]
    public void BaselineIdentitySeparatesEntryHeadFromImplementationSnapshotAndDetectsMismatch()
    {
        var snapshot = ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot([
            new("b.cs", Encoding.UTF8.GetBytes("B")), new("a.cs", Encoding.UTF8.GetBytes("A"))]);
        Assert.Equal(snapshot, ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot([
            new("a.cs", Encoding.UTF8.GetBytes("A")), new("b.cs", Encoding.UTF8.GetBytes("B"))]));
        var captured = new ExperimentRepositoryIdentity("HEAD", "main", "ORIGIN", true,
            "CONTRACT", "MANIFEST", snapshot);
        Assert.True(ExperimentBaselineIdentityResearch.CertificateMatches(captured, captured));
        Assert.False(ExperimentBaselineIdentityResearch.CertificateMatches(captured,
            captured with { RepositoryEntryHead = "OLD-BASELINE" }));
    }

    [Fact]
    public void NormalGenerationRemainsByteAndRngExactBecauseOracleIsDisconnected()
    {
        var chart = OsuBeatmap.Parse(SampleBeatmap());
        var leftRng = new CountingRandom(17);
        var rightRng = new CountingRandom(17);
        var left = new AddNotesEngine().Apply(chart, new AddNotesOptions { Chance = 1 }, leftRng);
        _ = GeometrySafetyAttributionResearch.AuditSnapshot("read-only", chart.KeyCount,
            chart.OriginalObjects.Select((x, i) => GeometrySafetyAttributionResearch.Object("read-only",
                GeometrySnapshotRole.Source, x, x.StartTime / 500m, (x.EndTime ?? x.StartTime) / 500m, i)));
        var right = new AddNotesEngine().Apply(chart, new AddNotesOptions { Chance = 1 }, rightRng);
        Assert.Equal(OsuBeatmap.Write(left.ModifiedChart, 1), OsuBeatmap.Write(right.ModifiedChart, 1));
        Assert.Equal(leftRng.Calls, rightRng.Calls);
        Assert.Equal("legacy-experimental.1", MapperEvidenceProfileBuilder.BehaviorPolicyVersion);
    }

    private static GeometrySnapshotAudit Audit(int keys, params GeometrySafetyObject[] values) =>
        GeometrySafetyAttributionResearch.AuditSnapshot(Chart, keys, values);

    private static GeometrySafetyObject Tap(string id, decimal beat, int time, int lane = 0) =>
        new(id, ManiaObject.Tap(lane, time), beat, beat);
    private static GeometrySafetyObject Ln(string id, decimal startBeat, decimal endBeat,
        int startTime, int endTime, int lane = 0) =>
        new(id, ManiaObject.Ln(lane, startTime, endTime), startBeat, endBeat);
    private static GeometryMutationProvenance Provenance(string id, GeometryMutationCause cause,
        string? ignored = null) => new(id, cause, "synthetic-policy", "synthetic", "OP", 0, null, ignored);

    private static string SampleBeatmap() => """
        osu file format v14
        [General]
        Mode:3
        [Difficulty]
        CircleSize:4
        [TimingPoints]
        0,500,4,2,1,60,1,0
        [HitObjects]
        64,192,0,1,0,0:0:0:0:
        192,192,500,128,0,1000:0:0:0:0:
        320,192,1000,1,0,0:0:0:0:
        """;

    private sealed class CountingRandom(int seed) : IRandomSource
    {
        private readonly SeededRandom inner = new(seed);
        public int Calls { get; private set; }
        public double NextDouble() { Calls++; return inner.NextDouble(); }
        public int Next(int maximum) { Calls++; return inner.Next(maximum); }
    }
}
