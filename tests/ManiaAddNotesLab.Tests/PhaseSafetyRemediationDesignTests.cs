using System.Text.Json;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseSafetyRemediationDesignTests
{
    [Fact]
    public void MinimalLatentEndpointCounterexampleIsRejectedByCanonicalPlayableShadow()
    {
        var timeline = Timeline();
        var state = new[] { new TimedManiaObject(ManiaObject.Ln(0, 0, 500), 0m,
            .99999999999999999999999999m) };
        var tap = new TimedManiaObject(ManiaObject.Tap(0, 500, true), 1m, 1m);

        var result = SafetyRemediationDesignResearch.EvaluateCommittedMutation("fixture", 1,
            timeline, state, tap);

        Assert.True(result.LegacyAccepted);
        Assert.False(result.CanonicalAccepted);
        Assert.Equal(GeometryHardViolationKind.None, result.LegacyLatentResult);
        Assert.Equal(GeometryHardViolationKind.TapOnHeldLongNote, result.CanonicalPlayableResult);
        Assert.True(result.StateUnchanged);
    }

    [Fact]
    public void ExactEndpointIsOccupiedUnderBothRepresentations()
    {
        var timeline = Timeline();
        var state = new[] { new TimedManiaObject(ManiaObject.Ln(0, 0, 500), 0m, 1m) };
        var tap = new TimedManiaObject(ManiaObject.Tap(0, 500, true), 1m, 1m);

        var result = SafetyRemediationDesignResearch.EvaluateCommittedMutation("fixture", 1,
            timeline, state, tap);

        Assert.False(result.LegacyAccepted);
        Assert.False(result.CanonicalAccepted);
        Assert.Equal(GeometryHardViolationKind.TapOnHeldLongNote, result.LegacyLatentResult);
        Assert.Equal(GeometryHardViolationKind.TapOnHeldLongNote, result.CanonicalPlayableResult);
    }

    [Fact]
    public void CanonicalPlayableCoordinateIsIdempotentAcrossTimingBoundariesAndNegativeOffsets()
    {
        var timeline = new BeatTimeline([new TimingPoint(0, 500), new TimingPoint(1000, 333.333m)]);
        foreach (var time in new[] { -1001, -1, 0, 1, 499, 500, 999, 1000, 1001, 1667, 2500 })
        {
            var first = SafetyRemediationDesignResearch.Canonicalize(timeline, time);
            var second = SafetyRemediationDesignResearch.Canonicalize(timeline, first.CanonicalBeat);
            var third = SafetyRemediationDesignResearch.Canonicalize(timeline, second.CanonicalBeat);
            Assert.Equal(time, first.Milliseconds);
            Assert.Equal(first, second);
            Assert.Equal(second, third);
        }
    }

    [Fact]
    public void MaterializationUsesAwayFromZeroAtPositiveAndNegativeMidpoints()
    {
        var timeline = Timeline();
        Assert.Equal(1, SafetyRemediationDesignResearch.Canonicalize(timeline, .001m).Milliseconds);
        Assert.Equal(-1, SafetyRemediationDesignResearch.Canonicalize(timeline, -.001m).Milliseconds);
    }

    [Fact]
    public void LongNoteBoundaryEqualityRemainsLegalUnderStrictOverlapSemantics()
    {
        var timeline = Timeline();
        var state = new[] { new TimedManiaObject(ManiaObject.Ln(0, 0, 500), 0m,
            .99999999999999999999999999m) };
        var next = new TimedManiaObject(ManiaObject.Ln(0, 500, 1000, true), 1m, 2m);

        var result = SafetyRemediationDesignResearch.EvaluateCommittedMutation("fixture", 1,
            timeline, state, next);

        Assert.True(result.LegacyAccepted);
        Assert.True(result.CanonicalAccepted);
        Assert.Equal(GeometryHardViolationKind.None, result.CanonicalPlayableResult);
    }

    [Fact]
    public void PlayableProjectionDoesNotOverwriteLatentOrG1IdentityValues()
    {
        var timeline = Timeline();
        var timed = new TimedManiaObject(ManiaObject.Ln(0, 0, 500), 0m,
            .99999999999999999999999999m);
        var identity = new InteriorRelationCandidateIdentity("candidate", "chart", new(1), 0,
            250, .5m, InteriorAnchorKind.Head, 500, timed.EndBeat, InteriorEndRelation.Contained,
            "query", "result", true);

        var canonical = SafetyRemediationDesignResearch.CanonicalizePlayableObject(timeline, timed);

        Assert.Equal(.99999999999999999999999999m, timed.EndBeat);
        Assert.Equal(1m, canonical.EndBeat);
        Assert.Equal(.99999999999999999999999999m, identity.CandidateEndBeat);
        Assert.Equal("query", identity.QuerySignature);
        Assert.Equal("result", identity.ResultSignature);
    }

    [Fact]
    public void ShadowEvaluatorHasNoRandomDependencyAndDoesNotMutateInputs()
    {
        var method = typeof(SafetyRemediationDesignResearch).GetMethod(
            nameof(SafetyRemediationDesignResearch.EvaluateCommittedMutation))!;
        Assert.DoesNotContain(method.GetParameters(), x => typeof(IRandomSource).IsAssignableFrom(x.ParameterType));
        var timeline = Timeline();
        var state = new List<TimedManiaObject>
        {
            new(ManiaObject.Ln(0, 0, 500), 0m, .99999999999999999999999999m)
        };
        var before = state.ToArray();
        _ = SafetyRemediationDesignResearch.EvaluateCommittedMutation("fixture", 1, timeline, state,
            new TimedManiaObject(ManiaObject.Tap(0, 500, true), 1m, 1m));
        Assert.Equal(before, state);
    }

    [Fact]
    public void PlacementObserverIsOutputAndRngTranscriptExact()
    {
        var chart = new ManiaChart
        {
            KeyCount = 7,
            Lines = [],
            OriginalObjects =
            [
                ManiaObject.Ln(0, 0, 2000),
                ManiaObject.Tap(1, 500),
                ManiaObject.Ln(2, 1000, 2500),
                ManiaObject.Tap(3, 1500)
            ],
            TimingPoints = [new TimingPoint(0, 500)]
        };
        var options = InteriorRelationMembershipResearch.CurrentOptions() with
        {
            Chance = 1,
            ArticulationEnabled = false,
            ContextualDensityNormalizationEnabled = false
        };
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var leftRandom = new TranscriptRandom(61);
        var rightRandom = new TranscriptRandom(61);
        var observer = new SafetyRemediationPlacementObserverResearch();

        var left = new AddNotesEngine().Apply(chart, options, leftRandom, profile);
        var right = new AddNotesEngine().Apply(chart, options, rightRandom, profile,
            null, null, null, observer);

        Assert.Equal(OsuBeatmap.Write(left.ModifiedChart, options.Chance),
            OsuBeatmap.Write(right.ModifiedChart, options.Chance));
        Assert.Equal(left.ModifiedChart.AddedObjects, right.ModifiedChart.AddedObjects);
        Assert.Equal(leftRandom.Transcript, rightRandom.Transcript);
        Assert.Equal(right.ModifiedChart.AddedObjects, observer.Build().Select(x => x.Object));
        Assert.Equal(0, observer.RngCalls);
    }

    [Fact]
    public void FrozenContractAndDesignTablesAreDeterministicAndKeepBehaviorUnauthorized()
    {
        Assert.Equal(SafetyRemediationDesignContractResearch.SerializeArtifact(),
            SafetyRemediationDesignContractResearch.SerializeArtifact());
        var contract = SafetyRemediationDesignContractResearch.Create();
        Assert.False(contract.BehaviorChange);
        Assert.False(contract.GenerationSemanticsChange);
        Assert.False(contract.DefaultBehaviorChange);
        Assert.False(contract.G1SemanticsChange);
        Assert.False(contract.RemediationImplemented);
        Assert.Equal("NOT_AUTHORIZED", contract.NextPhaseAuthorization);
        Assert.Single(SafetyRemediationDesignResearch.CandidateMatrix(), x => x.Preferred
            && x.Candidate == SafetyRemediationCandidateFamily.SharedCanonicalPlayableGeometry);
        Assert.Equal(JsonSerializer.Serialize(SafetyRemediationDesignResearch.CandidateMatrix()),
            JsonSerializer.Serialize(SafetyRemediationDesignResearch.CandidateMatrix()));
    }

    [Fact]
    public void BadControlChangingInclusiveEqualityIsAbsentFromEveryViableCandidate()
    {
        var exact = SafetyRemediationDesignResearch.EvaluateCommittedMutation("fixture", 1, Timeline(),
            [new TimedManiaObject(ManiaObject.Ln(0, 0, 500), 0m, 1m)],
            new TimedManiaObject(ManiaObject.Tap(0, 500, true), 1m, 1m));
        Assert.Equal(GeometryHardViolationKind.TapOnHeldLongNote, exact.CanonicalPlayableResult);
        Assert.All(SafetyRemediationDesignResearch.CandidateMatrix().Where(x => x.Viable),
            x => Assert.Equal(SafetyRemediationEvidenceGrade.Proven, x.HardValidityConsistency));
    }

    [Fact]
    public void RepresentationInventorySeparatesEvidenceFromCollisionAuthority()
    {
        var rows = SafetyRemediationDesignResearch.RepresentationInventory();
        Assert.Contains(rows, x => x.Stage == "Candidate intent" && x.EvidenceAuthority
            && !x.CollisionAuthorityCurrent && x.G1IdentityDependency);
        Assert.Contains(rows, x => x.Stage == "HardValidity" && x.CollisionAuthorityCurrent
            && !x.EvidenceAuthority);
        Assert.DoesNotContain(rows, x => x.DesignAuthority.Contains("synthetic style", StringComparison.Ordinal));
    }

    private static BeatTimeline Timeline() => new([new TimingPoint(0, 500)]);

    private sealed class TranscriptRandom(int seed) : IRandomPositionSource
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
