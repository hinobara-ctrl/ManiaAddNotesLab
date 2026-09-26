using System.Text.Json;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseSafetyRemediationGateTests
{
    [Fact]
    public void CanonicalPlayableProjectionClosesSameMillisecondLatentEndpointPermission()
    {
        var timeline = Timeline();
        var projector = new CanonicalPlayableGeometry(timeline);
        var latent = new TimedManiaObject(ManiaObject.Ln(0, 0, 500), 0m,
            .99999999999999999999999999m);
        var tapBeat = projector.Beat(500);

        var legacy = new LaneGeometryIndex(1, [latent]);
        var canonical = new LaneGeometryIndex(1, [projector.Project(latent)]);

        Assert.True(legacy.InspectTapLane(0, tapBeat).IsLegal);
        Assert.False(canonical.InspectTapLane(0, tapBeat).IsLegal);
        Assert.Equal(500, projector.Project(latent).Object.EndTime);
        Assert.Equal(1m, projector.Project(latent).EndBeat);
        Assert.Equal(.99999999999999999999999999m, latent.EndBeat);
    }

    [Fact]
    public void ExactAndAdjacentBoundariesPreserveInclusiveTapAndStrictLnSemantics()
    {
        var timeline = Timeline();
        var projector = new CanonicalPlayableGeometry(timeline);
        var exact = new LaneGeometryIndex(1,
            [projector.Project(new TimedManiaObject(ManiaObject.Ln(0, 0, 500), 0m, 1m))]);
        Assert.True(exact.InspectTapLane(0, projector.Beat(499)).IsLegal == false);
        Assert.False(exact.InspectTapLane(0, projector.Beat(500)).IsLegal);
        Assert.True(exact.InspectTapLane(0, projector.Beat(501)).IsLegal);
        Assert.True(exact.InspectLnLane(0, projector.Beat(500), projector.Beat(1000)).IsLegal);
        Assert.False(exact.InspectLnLane(0, projector.Beat(499), projector.Beat(1000)).IsLegal);
    }

    [Fact]
    public void CanonicalLnBoundariesCoverFollowingTapPreviousLnEqualStartsReleasesAndZeroGap()
    {
        var projector = new CanonicalPlayableGeometry(Timeline());
        TimedManiaObject P(ManiaObject value) => projector.Project(new(value,
            projector.Beat(value.StartTime), projector.Beat(value.EndTime ?? value.StartTime)));

        var followingTap = new LaneGeometryIndex(1, [P(ManiaObject.Tap(0, 1000))]);
        Assert.True(followingTap.InspectLnLane(0, projector.Beat(500), projector.Beat(1000)).IsLegal);
        Assert.False(followingTap.InspectLnLane(0, projector.Beat(500), projector.Beat(1001)).IsLegal);

        var previousLn = new LaneGeometryIndex(1, [P(ManiaObject.Ln(0, 0, 500))]);
        Assert.True(previousLn.InspectLnLane(0, projector.Beat(500), projector.Beat(1000)).IsLegal);
        Assert.False(previousLn.InspectLnLane(0, projector.Beat(499), projector.Beat(1000)).IsLegal);

        var equalStart = new LaneGeometryIndex(1, [P(ManiaObject.Ln(0, 500, 1000))]);
        Assert.False(equalStart.InspectLnLane(0, projector.Beat(500), projector.Beat(750)).IsLegal);
        var equalRelease = new LaneGeometryIndex(1, [P(ManiaObject.Ln(0, 0, 1000))]);
        Assert.False(equalRelease.InspectLnLane(0, projector.Beat(500), projector.Beat(1000)).IsLegal);
    }

    [Fact]
    public void CanonicalProjectionIsIdempotentAcrossNegativeMidpointsAndRedlineBoundary()
    {
        var timeline = new BeatTimeline([new TimingPoint(0, 500), new TimingPoint(1000, 333.333m)]);
        var projector = new CanonicalPlayableGeometry(timeline);
        foreach (var time in new[] { -1001, -1, 0, 1, 499, 500, 999, 1000, 1001, 1667 })
        {
            var value = new TimedManiaObject(ManiaObject.Ln(0, time, time + 1), 123.456m, 789.012m);
            Assert.Equal(projector.Project(value), projector.Project(projector.Project(value)));
        }
        Assert.Equal(1, timeline.ToTimeMilliseconds(.001m));
        Assert.Equal(-1, timeline.ToTimeMilliseconds(-.001m));
    }

    [Fact]
    public void RemediationOffIsByteObjectAndRngExactToNormalEngine()
    {
        var chart = Fixture();
        var options = Options();
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var normalRng = new TranscriptRandom(41);
        var gateRng = new TranscriptRandom(41);
        var normal = new AddNotesEngine().Apply(chart, options, normalRng, profile);
        var observed = new AddNotesEngine().Apply(chart, options, gateRng, profile,
            null, null, null, null, SafetyRemediationGateRuntimeConfiguration.Frozen(false));

        Assert.Equal(OsuBeatmap.Write(normal.ModifiedChart, options.Chance),
            OsuBeatmap.Write(observed.ModifiedChart, options.Chance));
        Assert.Equal(normal.ModifiedChart.AddedObjects, observed.ModifiedChart.AddedObjects);
        Assert.Equal(normalRng.Transcript, gateRng.Transcript);
        Assert.Equal(0, observed.SafetyRemediationGateDiagnostics!.GateRngCalls);
    }

    [Fact]
    public void RemediationTreatmentIsDeterministicAndSerializationReparsesExactly()
    {
        string Run(out AddNotesResult result, out IReadOnlyList<string> transcript)
        {
            var chart = Fixture();
            var random = new TranscriptRandom(73);
            result = new AddNotesEngine().Apply(chart, Options(), random,
                MapperEvidenceProfileBuilder.Build(chart), null, null, null, null,
                SafetyRemediationGateRuntimeConfiguration.Frozen(true));
            transcript = random.Transcript;
            var serialized = OsuBeatmap.Write(result.ModifiedChart, Options().Chance);
            var reparsed = OsuBeatmap.Parse(serialized);
            Assert.Equal(result.ModifiedChart.AllObjects.Select(Materialized).Order(StringComparer.Ordinal),
                reparsed.OriginalObjects.Select(Materialized).Order(StringComparer.Ordinal));
            return serialized;
        }

        var first = Run(out var a, out var firstTranscript);
        var second = Run(out var b, out var secondTranscript);
        Assert.Equal(first, second);
        Assert.Equal(a.ModifiedChart.AddedObjects, b.ModifiedChart.AddedObjects);
        Assert.Equal(firstTranscript, secondTranscript);
        Assert.Equal(0, a.SafetyRemediationGateDiagnostics!.GateRngCalls);
    }

    [Fact]
    public void TreatmentObserverAndMutationProvenanceAreNonInterfering()
    {
        var chart = Fixture();
        var options = Options();
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var cleanRandom = new TranscriptRandom(89);
        var observedRandom = new TranscriptRandom(89);
        var gate = SafetyRemediationGateRuntimeConfiguration.Frozen(true);
        var clean = new AddNotesEngine().Apply(chart, options, cleanRandom, profile,
            null, null, null, null, gate);
        var recorder = new GenerationProvenanceRecorderResearch(
            GenerationProvenanceIdentityResearch.Create(profile.ChartFingerprint, 89, options,
                SafetyRemediationGatePolicyVersions.Treatment, []));
        var observer = new SafetyRemediationPlacementObserverResearch();
        var observed = new AddNotesEngine().Apply(chart, options, observedRandom, profile,
            null, recorder, null, observer, gate);

        Assert.Equal(OsuBeatmap.Write(clean.ModifiedChart, options.Chance),
            OsuBeatmap.Write(observed.ModifiedChart, options.Chance));
        Assert.Equal(clean.ModifiedChart.AddedObjects, observed.ModifiedChart.AddedObjects);
        Assert.Equal(cleanRandom.Transcript, observedRandom.Transcript);
        Assert.Equal(observed.ModifiedChart.AddedObjects, observer.Build().Select(x => x.Object));
        Assert.Equal(0, observer.RngCalls);
        Assert.Equal(0, recorder.RecorderRngCalls);
        var validation = GenerationProvenanceTraceValidatorResearch.Validate(recorder.Build());
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
    }

    [Fact]
    public void CanonicalProjectionDoesNotMutateFrozenG1CandidateIdentity()
    {
        var latent = .99999999999999999999999999m;
        var identity = new InteriorRelationCandidateIdentity("candidate", "chart", new(1), 0,
            250, .5m, InteriorAnchorKind.Head, 500, latent, InteriorEndRelation.Contained,
            "query", "result", true);
        var projected = new CanonicalPlayableGeometry(Timeline()).Project(
            new TimedManiaObject(ManiaObject.Ln(0, 250, 500, true), .5m, latent));

        Assert.Equal(1m, projected.EndBeat);
        Assert.Equal(latent, identity.CandidateEndBeat);
        Assert.Equal("query", identity.QuerySignature);
        Assert.Equal("result", identity.ResultSignature);
    }

    [Fact]
    public void GateContractIsStableAndKeepsSuccessorsUnauthorized()
    {
        var contract = SafetyRemediationGateContractResearch.Create();
        Assert.Equal("53f1475cb14186b2a6a4eef5a9c71d8aa8777d6f", contract.RepositoryEntryHead);
        Assert.Equal("NOT_AUTHORIZED", contract.SuccessorAuthorization);
        Assert.False(contract.DefaultBehaviorChanged);
        Assert.Equal(SafetyRemediationGateContractResearch.ComputeHash(contract),
            SafetyRemediationGateContractResearch.ComputeHash(SafetyRemediationGateContractResearch.Create()));
        Assert.Equal(SafetyRemediationGateContractResearch.SerializeArtifact(),
            SafetyRemediationGateContractResearch.SerializeArtifact());
    }

    [Fact]
    public void StreamingCompactDiagnosticsAreExactToFullDiagnosticsEndToEnd()
    {
        var chart = Fixture();
        var options = Options();
        var profile = MapperEvidenceProfileBuilder.Build(chart);

        (AddNotesResult Result, TranscriptRandom Random) Run(bool treatment, bool compact,
            ISafetyRemediationGateOpportunitySink? sink = null)
        {
            var random = new TranscriptRandom(97);
            var result = new AddNotesEngine().Apply(chart, options, random, profile,
                null, null, null, null,
                SafetyRemediationGateRuntimeConfiguration.Frozen(treatment, compact,
                    retainCandidateDecisions: sink is null, opportunitySink: sink));
            return (result, random);
        }

        var controlFull = Run(false, false);
        var controlSink = new RecordingOpportunitySink();
        var controlCompact = Run(false, true, controlSink);
        var treatmentFull = Run(true, false);
        var treatmentSink = new RecordingOpportunitySink();
        var treatmentCompact = Run(true, true, treatmentSink);
        foreach (var pair in new[]
                 {
                     (Full: controlFull, Compact: controlCompact, Rows: controlSink.Rows,
                         Decisions: controlSink.Candidates),
                     (Full: treatmentFull, Compact: treatmentCompact, Rows: treatmentSink.Rows,
                         Decisions: treatmentSink.Candidates)
                 })
        {
            Assert.Equal(OsuBeatmap.Write(pair.Full.Result.ModifiedChart, options.Chance),
                OsuBeatmap.Write(pair.Compact.Result.ModifiedChart, options.Chance));
            Assert.Equal(pair.Full.Random.Transcript, pair.Compact.Random.Transcript);
            var full = pair.Full.Result.SafetyRemediationGateDiagnostics!;
            var compact = pair.Compact.Result.SafetyRemediationGateDiagnostics!;
            Assert.Equal(JsonSerializer.Serialize(full.CandidateDecisions),
                JsonSerializer.Serialize(pair.Decisions));
            Assert.True(SafetyRemediationGateHardeningResearch.Compact(full.OpportunityStates)
                .SequenceEqual(pair.Rows));
            Assert.Equal(SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(full),
                SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(compact));
            Assert.Empty(compact.OpportunityStates);
            Assert.Empty(compact.CompactOpportunityStates);
        }

        static SafetyRemediationGatePairedOpportunity[] Pair(
            SafetyRemediationGateRuntimeDiagnostics control,
            SafetyRemediationGateRuntimeDiagnostics treatment)
        {
            var left = control.CompactOpportunityStates.IsDefaultOrEmpty
                ? SafetyRemediationGateHardeningResearch.Compact(control.OpportunityStates)
                : control.CompactOpportunityStates;
            var right = treatment.CompactOpportunityStates.IsDefaultOrEmpty
                ? SafetyRemediationGateHardeningResearch.Compact(treatment.OpportunityStates)
                : treatment.CompactOpportunityStates;
            return left.Zip(right, (a, b) => new SafetyRemediationGatePairedOpportunity(
                a.OpportunityKey, a.OpportunityOrder,
                SafetyRemediationGateHardeningResearch.LineageState(a.StateBeforeHash,
                    a.StateBeforeComplete, a.OpportunityOrder),
                SafetyRemediationGateHardeningResearch.LineageState(b.StateBeforeHash,
                    b.StateBeforeComplete, b.OpportunityOrder),
                SafetyRemediationGateHardeningResearch.LineageState(a.StateAfterHash,
                    a.StateAfterComplete, a.OpportunityOrder + 1),
                SafetyRemediationGateHardeningResearch.LineageState(b.StateAfterHash,
                    b.StateAfterComplete, b.OpportunityOrder + 1),
                a.CommittedObjectIdentity, b.CommittedObjectIdentity,
                a.CommittedObjectIdentity != b.CommittedObjectIdentity
                    && control.CandidateDecisions.Any(x => x.OpportunityKey == a.OpportunityKey
                        && x.GeometrySetsDiffer))).ToArray();
        }

        var fullLineage = SafetyRemediationGateHardeningResearch.Classify("pair",
            Pair(controlFull.Result.SafetyRemediationGateDiagnostics!,
                treatmentFull.Result.SafetyRemediationGateDiagnostics!));
        var streamedControl = controlCompact.Result.SafetyRemediationGateDiagnostics! with
            { CompactOpportunityStates = [.. controlSink.Rows],
                CandidateDecisions = [.. controlSink.Candidates] };
        var streamedTreatment = treatmentCompact.Result.SafetyRemediationGateDiagnostics! with
            { CompactOpportunityStates = [.. treatmentSink.Rows],
                CandidateDecisions = [.. treatmentSink.Candidates] };
        var compactLineage = SafetyRemediationGateHardeningResearch.Classify("pair",
            Pair(streamedControl, streamedTreatment));
        Assert.Equal(fullLineage.Length, compactLineage.Length);
        for (var index = 0; index < fullLineage.Length; index++)
            Assert.Equal(fullLineage[index], compactLineage[index]);
    }

    private static string Materialized(ManiaObject value) =>
        $"{value.Lane}|{value.StartTime}|{value.EndTime}|{value.Type}";

    private static BeatTimeline Timeline() => new([new TimingPoint(0, 500)]);

    private static AddNotesOptions Options() => InteriorRelationMembershipResearch.CurrentOptions() with
    {
        Chance = 1,
        ArticulationEnabled = false,
        ContextualDensityNormalizationEnabled = false
    };

    private static ManiaChart Fixture() => new()
    {
        KeyCount = 7,
        Lines = ["osu file format v14", "", "[General]", "Mode:3", "", "[Metadata]",
            "Version:Gate", "BeatmapID:1", "", "[Difficulty]", "CircleSize:7", "",
            "[TimingPoints]", "0,500,4,2,0,100,1,0", "1000,333.333,4,2,0,100,1,0", "", "[HitObjects]"],
        OriginalObjects =
        [
            ManiaObject.Ln(0, -500, 1000, sequence: 0) with { RawLine = "36,192,-500,128,0,1000:0:0:0:0:" },
            ManiaObject.Tap(1, 0, sequence: 1) with { RawLine = "109,192,0,1,0,0:0:0:0:" },
            ManiaObject.Ln(2, 500, 2000, sequence: 2) with { RawLine = "182,192,500,128,0,2000:0:0:0:0:" },
            ManiaObject.Tap(3, 1000, sequence: 3) with { RawLine = "256,192,1000,1,0,0:0:0:0:" },
            ManiaObject.Ln(4, 1500, 3000, sequence: 4) with { RawLine = "329,192,1500,128,0,3000:0:0:0:0:" }
        ],
        TimingPoints = [new TimingPoint(0, 500), new TimingPoint(1000, 333.333m)]
    };

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

    private sealed class RecordingOpportunitySink : ISafetyRemediationGateOpportunitySink
    {
        public List<SafetyRemediationGateCompactOpportunityState> Rows { get; } = [];
        public List<SafetyRemediationCandidateGeometryDecision> Candidates { get; } = [];
        public void ObserveCandidate(SafetyRemediationCandidateGeometryDecision decision) =>
            Candidates.Add(decision);
        public void Observe(SafetyRemediationGateCompactOpportunityState state) => Rows.Add(state);
    }
}
