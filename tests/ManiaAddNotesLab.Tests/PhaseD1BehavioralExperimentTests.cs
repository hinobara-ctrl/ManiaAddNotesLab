using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseD1BehavioralExperimentTests
{
    private const string ManifestHash = "SYNTHETIC-MANIFEST";

    [Fact]
    public void JointUniqueProposalIsAdmittedAtExactEngineBoundary()
    {
        var chart = Chart(4, Tap(0, 0), Tap(1, 0),
            Tap(0, 1000), Tap(1, 1000), Tap(2, 1000), Tap(3, 1000));
        var result = Treatment(chart, 1);
        var decision = At(result, 0);
        Assert.Equal(FutureD1EvidenceState.JointObservedUnique, decision.EvidenceState);
        Assert.Equal(FutureD1ExperimentalDisposition.ExperimentAdmittable, decision.Disposition);
        Assert.True(decision.OutputMutationPerformed);
        Assert.Equal(2, result.ModifiedChart.AddedObjects.Count(x => x.StartTime == 0));
    }

    [Fact]
    public void JointAmongAlternativesIsAdmittedWithoutFrequencyOrSelection()
    {
        var chart = Chart(5, Tap(0, 0), Tap(1, 0),
            Tap(0, 1000), Tap(1, 1000), Tap(2, 1000), Tap(3, 1000),
            Tap(0, 2000), Tap(1, 2000), Tap(2, 2000), Tap(4, 2000));
        var decision = At(Treatment(chart, 1), 0);
        Assert.Equal(FutureD1EvidenceState.JointObservedAmongAlternatives, decision.EvidenceState);
        Assert.Equal(FutureD1ExperimentalDisposition.ExperimentAdmittable, decision.Disposition);
        Assert.NotEmpty(decision.JointDonorOccurrenceIds);
    }

    [Fact]
    public void CrossDonorMarginalsDoNotFabricateJointEvidenceAndDoNotReroll()
    {
        var chart = Chart(6, Tap(0, 0), Tap(1, 0),
            Tap(0, 1000), Tap(1, 1000), Tap(2, 1000), Tap(4, 1000),
            Tap(0, 2000), Tap(1, 2000), Tap(3, 2000), Tap(5, 2000));
        var random = new RecordingRandom(1);
        var result = Treatment(chart, random);
        var decision = At(result, 0);
        Assert.Equal(FutureD1EvidenceState.MarginalOnly, decision.EvidenceState);
        Assert.Equal(FutureD1ExperimentalDisposition.ExperimentAbstainMarginalOnly, decision.Disposition);
        Assert.False(decision.OutputMutationPerformed);
        Assert.Empty(decision.JointDonorOccurrenceIds);
        Assert.Single(result.ModifiedChart.AddedObjects, x => x.StartTime == 0);
        Assert.Equal(decision.RngPositionBeforeGate, random.Transcript.Take((int)decision.RngPositionBeforeGate).Count());
    }

    [Fact]
    public void NoComparableAndNotMarginalRemainDistinctAbstentions()
    {
        var noComparable = Chart(4, Tap(0, 0), Tap(1, 0));
        Assert.Equal(FutureD1EvidenceState.NoComparableCompositionContext,
            At(Treatment(noComparable, 1), 0).EvidenceState);

        var notMarginal = Chart(5, Tap(0, 0), Tap(1, 0),
            Tap(0, 1000), Tap(1, 1000), Tap(2, 1000), Tap(4, 1000));
        var decision = At(Treatment(notMarginal, 1), 0);
        Assert.Equal(FutureD1EvidenceState.NotMarginallySupported, decision.EvidenceState);
        Assert.Equal(FutureD1ExperimentalDisposition.ExperimentAbstainNotObserved, decision.Disposition);
    }

    [Fact]
    public void K0To1AndK2To3RemainLegacyAndOnlyK1To2IsQueried()
    {
        var chart = Chart(7, Tap(0, 0), Tap(1, 0), Tap(2, 0),
            Tap(0, 1000), Tap(1, 1000), Tap(2, 1000), Tap(3, 1000), Tap(4, 1000));
        var result = Treatment(chart, 1);
        Assert.Equal(3, result.ModifiedChart.AddedObjects.Count(x => x.StartTime == 0));
        Assert.Single(result.D1BehavioralDiagnostics!.DirectDecisions.Where(x => x.Timestamp == 0));
        Assert.True(result.D1BehavioralDiagnostics.EvidenceQueryCount >= 1);
        Assert.True(result.D1BehavioralDiagnostics.LaterK3PlusAfterDirectD1Count > 0);
    }

    [Fact]
    public void EvidenceOutsideSelectedRangeRemainsQueryable()
    {
        var chart = Chart(4, Tap(0, 0), Tap(1, 0),
            Tap(0, 1000), Tap(1, 1000), Tap(2, 1000), Tap(3, 1000));
        var result = Treatment(chart, new RecordingRandom(1), TestOptions() with
            { StartMs = 0, EndMs = 0 });
        Assert.Equal(FutureD1EvidenceState.JointObservedUnique, At(result, 0).EvidenceState);
        Assert.Equal(2, result.ModifiedChart.AddedObjects.Count);
    }

    [Fact]
    public void GateConsumesZeroRngAndIndexRemainsImmutable()
    {
        var chart = Chart(4, Tap(0, 0), Tap(1, 0),
            Tap(0, 1000), Tap(1, 1000), Tap(2, 1000), Tap(3, 1000));
        var index = D1OriginalEvidenceIndex.Build(chart);
        var before = index.ContentHash;
        var result = Treatment(chart, new RecordingRandom(3), TestOptions(), index);
        Assert.Equal(0, result.D1BehavioralDiagnostics!.GateRngCalls);
        Assert.Equal(before, index.ContentHash);
        Assert.Equal(result.D1BehavioralDiagnostics.EvidenceIndexHashBefore,
            result.D1BehavioralDiagnostics.EvidenceIndexHashAfter);
    }

    [Fact]
    public void DisabledTreatmentIsByteExactLegacyWithIdenticalRngTranscript()
    {
        var chart = Chart(4, Tap(0, 0), Tap(1, 0), Tap(0, 1000), Tap(1, 1000));
        var left = new RecordingRandom(17);
        var right = new RecordingRandom(17);
        var legacy = new AddNotesEngine().Apply(chart, TestOptions(), left);
        var disabled = new AddNotesEngine().Apply(chart, TestOptions(), right,
            MapperEvidenceProfileBuilder.Build(chart), null);
        Assert.Equal(OsuBeatmap.Write(legacy.ModifiedChart, 1), OsuBeatmap.Write(disabled.ModifiedChart, 1));
        Assert.Equal(left.Transcript, right.Transcript);
        Assert.Null(disabled.D1BehavioralDiagnostics);
    }

    [Fact]
    public void ControlAndTreatmentRngMatchThroughFirstBehavioralDivergence()
    {
        var chart = Chart(6, Tap(0, 0), Tap(1, 0),
            Tap(0, 1000), Tap(1, 1000), Tap(2, 1000), Tap(4, 1000),
            Tap(0, 2000), Tap(1, 2000), Tap(3, 2000), Tap(5, 2000));
        var controlRandom = new RecordingRandom(19);
        var treatmentRandom = new RecordingRandom(19);
        _ = new AddNotesEngine().Apply(chart, TestOptions(), controlRandom);
        var treatment = Treatment(chart, treatmentRandom);
        var first = treatment.D1BehavioralDiagnostics!.DirectDecisions
            .First(x => !x.OutputMutationPerformed);
        Assert.Equal(controlRandom.Transcript.Take((int)first.RngPositionBeforeGate),
            treatmentRandom.Transcript.Take((int)first.RngPositionBeforeGate));
        Assert.Equal(0, first.GateRngCalls);
    }

    [Fact]
    public void ContractHashMismatchStopsBeforeGeneration()
    {
        var chart = Chart(4, Tap(0, 0), Tap(1, 0));
        var config = new D1BehavioralExperimentConfiguration(D1OriginalEvidenceIndex.Build(chart),
            "WRONG", ManifestHash);
        Assert.Throws<InvalidOperationException>(() => new AddNotesEngine().Apply(chart,
            TestOptions(), new RecordingRandom(1), null, config));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void OriginalOnlyIndexIsDeterministicAcrossSupportedKeymodes(int keys)
    {
        var objects = keys == 1 ? new[] { Tap(0, 0) }
            : new[] { Tap(0, 0), Tap(keys - 1, 0) };
        var chart = Chart(keys, objects);
        Assert.Equal(D1OriginalEvidenceIndex.Build(chart).ContentHash,
            D1OriginalEvidenceIndex.Build(chart).ContentHash);
    }

    private static D1DirectDecision At(AddNotesResult result, int time) =>
        result.D1BehavioralDiagnostics!.DirectDecisions.First(x => x.Timestamp == time);

    private static AddNotesResult Treatment(ManiaChart chart, int seed) =>
        Treatment(chart, new RecordingRandom(seed));

    private static AddNotesResult Treatment(ManiaChart chart, RecordingRandom random,
        AddNotesOptions? options = null, D1OriginalEvidenceIndex? index = null)
    {
        index ??= D1OriginalEvidenceIndex.Build(chart);
        return new AddNotesEngine().Apply(chart, options ?? TestOptions(), random,
            MapperEvidenceProfileBuilder.Build(chart),
            D1BehavioralExperimentConfiguration.Frozen(index, ManifestHash));
    }

    private static AddNotesOptions TestOptions() => new()
    {
        Chance = 1,
        DensityGraceColumns = 18,
        DensityDecayPerColumn = 1,
        ContextualDensityNormalizationEnabled = false
    };

    private static ManiaChart Chart(int keys, params ManiaObject[] objects) => new()
    {
        KeyCount = keys,
        Lines = ["osu file format v14", "", "[General]", "Mode:3", "", "[Metadata]",
            "Title:D1 synthetic", "Artist:Lab", "Creator:Tests", "Version:Gate", "", "[Difficulty]",
            $"CircleSize:{keys}", "", "[TimingPoints]", "0,500,4,2,1,100,1,0", "", "[HitObjects]"],
        OriginalObjects = objects.Select((x, sequence) => x with { Sequence = sequence }).ToArray(),
        TimingPoints = [new TimingPoint(0, 500)]
    };

    private static ManiaObject Tap(int lane, int time) => ManiaObject.Tap(lane, time);

    private sealed class RecordingRandom(int seed) : IRandomPositionSource
    {
        private readonly SeededRandom inner = new(seed);
        public List<string> Transcript { get; } = [];
        public long CallCount => Transcript.Count;
        public double NextDouble()
        {
            var value = inner.NextDouble();
            Transcript.Add($"D:{value:R}");
            return value;
        }
        public int Next(int maximum)
        {
            var value = inner.Next(maximum);
            Transcript.Add($"I:{maximum}:{value}");
            return value;
        }
    }
}
