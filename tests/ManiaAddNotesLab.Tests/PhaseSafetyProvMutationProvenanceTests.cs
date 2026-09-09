using System.Collections.Immutable;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseSafetyProvMutationProvenanceTests
{
    private const string ChartFingerprint = "SAFETY-PROV-SYNTHETIC";

    [Fact]
    public void EngineProvenanceOnOffIsByteObjectRngDecisionAndSerializationExact()
    {
        var chart = ArticulationFixture(7, 0);
        var options = ArticulationOptions() with { DiagnosticsEnabled = true };
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var offRandom = new PositionedTranscriptRandom(71);
        var onRandom = new PositionedTranscriptRandom(71);
        var recorder = Recorder(profile.ChartFingerprint, 71, options);

        var off = new AddNotesEngine().Apply(chart, options, offRandom, profile, null);
        var on = new AddNotesEngine().Apply(chart, options, onRandom, profile, null, recorder);

        Assert.Equal(OsuBeatmap.Write(off.ModifiedChart, options.Chance),
            OsuBeatmap.Write(on.ModifiedChart, options.Chance));
        Assert.Equal(off.ModifiedChart.AddedObjects, on.ModifiedChart.AddedObjects);
        Assert.Equal(off.ModifiedChart.ArticulationReplacements, on.ModifiedChart.ArticulationReplacements);
        Assert.Equal(offRandom.Transcript, onRandom.Transcript);
        Assert.Equal(DecisionIdentity(off), DecisionIdentity(on));
        Assert.Equal(ArticulationDecisionIdentity(off), ArticulationDecisionIdentity(on));
        Assert.Equal(off.ModifiedChart.AllObjects, on.ModifiedChart.AllObjects);
        Assert.NotEmpty(recorder.Build().Decisions);
        Assert.NotEmpty(recorder.Build().Mutations);
        var pass1Keys = recorder.Build().Decisions
            .Where(x => x.Stage != GenerationProvenanceStage.Articulation)
            .Select(x => x.OpportunityKey!).ToArray();
        var expectedRun = GenerationProvenanceIdentityResearch.WithOpportunitySequence(
            GenerationProvenanceIdentityResearch.Create(profile.ChartFingerprint, 71, options,
                "legacy-experimental.1", []), pass1Keys);
        Assert.Equal(expectedRun.OpportunitySequenceHash, recorder.Build().RunIdentity.OpportunitySequenceHash);
        Assert.True(GenerationProvenanceTraceValidatorResearch.Validate(recorder.Build()).IsValid);
    }

    [Fact]
    public void TraceIsByteDeterministicAndRecorderUsesZeroRng()
    {
        var chart = ArticulationFixture(7, 0);
        var options = ArticulationOptions();
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        string Run()
        {
            var recorder = Recorder(profile.ChartFingerprint, 19, options);
            _ = new AddNotesEngine().Apply(chart, options, new PositionedTranscriptRandom(19), profile, null,
                recorder);
            Assert.Equal(0, recorder.RecorderRngCalls);
            return recorder.Serialize();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void EngineTracePreservesDecisionMutationContinuityAndArticulationBoundary()
    {
        var chart = ArticulationFixture(7, 0);
        var options = ArticulationOptions();
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var recorder = Recorder(profile.ChartFingerprint, 7, options);
        var result = new AddNotesEngine().Apply(chart, options, new PositionedTranscriptRandom(7), profile, null,
            recorder);
        var trace = recorder.Build();
        Assert.True(GenerationProvenanceTraceValidatorResearch.Validate(trace).IsValid);
        Assert.Contains(trace.Mutations, x => x.Stage is GenerationProvenanceStage.Pass1BaseOpportunity
            or GenerationProvenanceStage.InteriorOpportunity);
        if (result.Statistics.ArticulationPlaced > 0)
            Assert.Contains(trace.Mutations, x => x.Stage == GenerationProvenanceStage.Articulation
                && x.MutationKind == GenerationMutationKind.ArticulationReplace
                && x.ParentObjectIdentity is not null);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void EngineRecorderCoversSupportedKeymodesWithoutBehaviorDrift(int keys)
    {
        var chart = SimpleChart(keys);
        var options = new AddNotesOptions { Chance = 1, ContextualDensityNormalizationEnabled = false,
            DensityDecayPerColumn = 1 };
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var leftRng = new PositionedTranscriptRandom(31);
        var rightRng = new PositionedTranscriptRandom(31);
        var left = new AddNotesEngine().Apply(chart, options, leftRng, profile);
        var recorder = Recorder(profile.ChartFingerprint, 31, options);
        var right = new AddNotesEngine().Apply(chart, options, rightRng, profile, null, recorder);
        Assert.Equal(OsuBeatmap.Write(left.ModifiedChart, 1), OsuBeatmap.Write(right.ModifiedChart, 1));
        Assert.Equal(leftRng.Transcript, rightRng.Transcript);
        Assert.All(recorder.Build().Mutations, x => Assert.InRange(x.Lane, 0, keys - 1));
    }

    [Fact]
    public void SourceObjectsRemainUnchangedWithRecorder()
    {
        var chart = ArticulationFixture(7, 0);
        var options = ArticulationOptions();
        var before = chart.OriginalObjects.ToArray();
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        _ = new AddNotesEngine().Apply(chart, options, new PositionedTranscriptRandom(11), profile, null,
            Recorder(profile.ChartFingerprint, 11, options));
        Assert.Equal(before, chart.OriginalObjects);
    }

    [Fact]
    public void MutationDecisionAndDivergenceIdsAreDeterministicAndPathFree()
    {
        var options = new AddNotesOptions();
        var runA = GenerationProvenanceIdentityResearch.Create(ChartFingerprint, 9, options,
            "synthetic-policy", ["OP-1", "OP-2"]);
        var runB = GenerationProvenanceIdentityResearch.Create(ChartFingerprint, 9, options,
            "synthetic-policy", ["OP-1", "OP-2"]);
        Assert.Equal(runA, runB);
        var pairA = ProvenancePairComparatorResearch.Compare("PAIR", DivergentControl(), DivergentTreatment());
        var pairB = ProvenancePairComparatorResearch.Compare("PAIR", DivergentControl(), DivergentTreatment());
        Assert.Equal(pairA.DirectDivergenceId, pairB.DirectDivergenceId);
        Assert.DoesNotContain("\\", runA.SemanticRunId);
        Assert.DoesNotContain(":\\", runA.SemanticRunId);
    }

    [Fact]
    public void StateHashesAreOrderStableAndSeparateGeometryFromGenerationState()
    {
        var recorder = Recorder();
        var a = ManiaObject.Tap(0, 0);
        var b = ManiaObject.Ln(1, 500, 1000);
        var left = recorder.State([a, b], [], [], 2, 2, 0);
        var reordered = recorder.State([b, a], [], [], 2, 2, 0);
        var advancedRng = recorder.State([a, b], [], [], 3, 3, 0);
        Assert.Equal(left, reordered);
        Assert.Equal(left.GeometryStateHash, advancedRng.GeometryStateHash);
        Assert.NotEqual(left.GenerationStateHash, advancedRng.GenerationStateHash);
    }

    [Fact]
    public void PairComparatorProvesDirectAndDownstreamFromExactParentState()
    {
        var result = ProvenancePairComparatorResearch.Compare("PAIR", DivergentControl(), DivergentTreatment());
        Assert.True(result.ExactMatching);
        Assert.Equal("OP-DIRECT", result.FirstDivergenceOpportunityKey);
        Assert.NotNull(result.DirectDivergenceId);
        Assert.Contains("OP-DOWNSTREAM", result.DownstreamAffectedOpportunityKeys);
    }

    [Fact]
    public void TemporalAfterIsNotDownstreamAfterFullReconvergence()
    {
        var control = DivergentControl().Concat([Pair("OP-RECONVERGED", "same", "G2", "G3")]).ToArray();
        var treatment = DivergentTreatment().Concat([Pair("OP-RECONVERGED", "same", "G2", "G3")]).ToArray();
        var result = ProvenancePairComparatorResearch.Compare("PAIR", control, treatment);
        Assert.Contains("OP-RECONVERGED", result.ReconvergenceOpportunityKeys);
        Assert.Contains("OP-RECONVERGED", result.TemporalAfterButNotDownstreamOpportunityKeys);
        Assert.DoesNotContain("OP-RECONVERGED", result.DownstreamAffectedOpportunityKeys);
    }

    [Fact]
    public void GeometryReconvergenceWithoutRngReconvergenceRemainsDownstream()
    {
        var c = DivergentControl().ToArray();
        var t = DivergentTreatment().ToArray();
        c[1] = Pair("OP-DOWNSTREAM", "later", "GEOM|RNG-C", "GEOM|RNG-C", "GEOM");
        t[1] = Pair("OP-DOWNSTREAM", "later", "GEOM|RNG-T", "GEOM|RNG-T", "GEOM");
        var result = ProvenancePairComparatorResearch.Compare("PAIR", c, t);
        Assert.True(result.ExactMatching);
        Assert.Contains("OP-DOWNSTREAM", result.DownstreamAffectedOpportunityKeys);
    }

    [Fact]
    public void IncompleteGenerationStateForcesComparatorToAbstain()
    {
        var c = DivergentControl().ToArray();
        var t = DivergentTreatment().ToArray();
        c[1] = Pair("OP-DOWNSTREAM", "later", "A", "A2", "GEOM", complete: false);
        t[1] = Pair("OP-DOWNSTREAM", "later", "B", "B2", "GEOM", complete: false);
        var result = ProvenancePairComparatorResearch.Compare("PAIR", c, t);
        Assert.False(result.ExactMatching);
        Assert.Contains("abstains", result.Explanation);
    }

    [Fact]
    public void OpportunityMismatchForbidsFuzzyPairing()
    {
        var result = ProvenancePairComparatorResearch.Compare("PAIR",
            [Pair("OP-A", "same", "S", "S")], [Pair("OP-B", "same", "S", "S")]);
        Assert.False(result.ExactMatching);
        Assert.Contains("fuzzy", result.Explanation);
    }

    [Fact]
    public void DifferenceWithoutExperimentalGovernanceIsNotCalledDirect()
    {
        var result = ProvenancePairComparatorResearch.Compare("PAIR",
            [Pair("OP", "legacy-a", "S0", "A")], [Pair("OP", "legacy-b", "S0", "B")]);
        Assert.False(result.ExactMatching);
        Assert.Null(result.DirectDivergenceId);
        Assert.Contains("not marked", result.Explanation);
    }

    [Theory]
    [InlineData(GenerationCausalOrigin.Legacy, GenerationProvenanceStage.Pass1BaseOpportunity,
        GeometrySafetyAttribution.LegacyIntroducedViolation)]
    [InlineData(GenerationCausalOrigin.TreatmentDirect, GenerationProvenanceStage.Pass1BaseOpportunity,
        GeometrySafetyAttribution.TreatmentDirectIntroducedViolation)]
    [InlineData(GenerationCausalOrigin.TreatmentDownstream, GenerationProvenanceStage.InteriorOpportunity,
        GeometrySafetyAttribution.TreatmentDownstreamIntroducedViolation)]
    [InlineData(GenerationCausalOrigin.Legacy, GenerationProvenanceStage.Articulation,
        GeometrySafetyAttribution.ArticulationIntroducedViolation)]
    public void SafetyDeltaUsesProvenMutationCause(GenerationCausalOrigin origin,
        GenerationProvenanceStage stage, GeometrySafetyAttribution expected)
    {
        var recorder = Recorder();
        var held = SafetyLn("HELD", 0, 3, 0, 1500);
        var candidate = SafetyTap("CANDIDATE", 2, 1000);
        var delta = recorder.EvaluateSafetyDelta(4, [held], [held, candidate], candidate,
            "MUT-1", origin, stage);
        Assert.Equal(expected, delta.CandidateFinding.Attribution);
        Assert.NotEmpty(delta.IntroducedViolationIds);
    }

    [Fact]
    public void MissingMutationProvenanceIsAlwaysUnattributable()
    {
        var recorder = Recorder();
        var existing = SafetyTap("A", 1, 500);
        var candidate = SafetyTap("B", 1, 500);
        var delta = recorder.EvaluateSafetyDelta(4, [existing], [existing, candidate], candidate,
            "MISSING", GenerationCausalOrigin.Legacy, GenerationProvenanceStage.Pass1BaseOpportunity,
            provenanceAvailable: false);
        Assert.Equal(GeometrySafetyAttribution.Unattributable, delta.CandidateFinding.Attribution);
    }

    [Fact]
    public void PreExistingViolationIsNotIntroducedByLaterValidMutation()
    {
        var recorder = Recorder();
        var a = SafetyTap("A", 1, 500);
        var b = SafetyTap("B", 1, 500);
        var candidate = SafetyTap("C", 2, 1000, 1);
        var delta = recorder.EvaluateSafetyDelta(4, [a, b], [a, b, candidate], candidate,
            "MUT", GenerationCausalOrigin.Legacy, GenerationProvenanceStage.Pass1BaseOpportunity);
        Assert.Empty(delta.IntroducedViolationIds);
        Assert.Equal(GeometrySafetyAttribution.NoViolation, delta.CandidateFinding.Attribution);
    }

    [Fact]
    public void SerializationViolationAndValidNegativeRemainDistinct()
    {
        var recorder = Recorder();
        var invalid = new GeometrySafetyObject("BAD", ManiaObject.Ln(0, 500, 500), 1, 1.01m);
        var bad = recorder.EvaluateSafetyDelta(4, [], [invalid], invalid, "SER-BAD",
            GenerationCausalOrigin.TreatmentDirect, GenerationProvenanceStage.Serialization);
        Assert.Equal(GeometrySafetyAttribution.SerializationIntroducedViolation,
            bad.CandidateFinding.Attribution);
        var valid = new GeometrySafetyObject("GOOD", ManiaObject.Ln(0, 500, 501), 1, 1.01m);
        var good = recorder.EvaluateSafetyDelta(4, [], [valid], valid, "SER-GOOD",
            GenerationCausalOrigin.TreatmentDirect, GenerationProvenanceStage.Serialization);
        Assert.Equal(GeometrySafetyAttribution.NoViolation, good.CandidateFinding.Attribution);
    }

    [Fact]
    public void ExactSerializationLinkSurvivesWriteAndReparseWithoutChangingFile()
    {
        var chart = SimpleChart(4);
        var result = new AddNotesEngine().Apply(chart, new AddNotesOptions { Chance = 1,
            ContextualDensityNormalizationEnabled = false, DensityDecayPerColumn = 1 }, new SeededRandom(3));
        var serialized = OsuBeatmap.Write(result.ModifiedChart, 1);
        var links = GenerationSerializationProvenanceResearch.LinkExact(result.ModifiedChart, serialized);
        Assert.All(links, x =>
        {
            Assert.True(x.ExactReparseIdentity);
            Assert.Equal(GeometrySafetyAttribution.NoViolation, x.Attribution);
        });
        Assert.Equal(serialized, OsuBeatmap.Write(result.ModifiedChart, 1));
    }

    [Fact]
    public void AmbiguousSerializationIdentityAbstainsInsteadOfFuzzyMatching()
    {
        var synthetic = ManiaObject.Tap(0, 0, true, 0) with { Origin = AddedObjectOrigin.HeadOpportunity };
        var chart = Chart(1, [ManiaObject.Tap(0, 0), synthetic]);
        var links = GenerationSerializationProvenanceResearch.LinkExact(chart, OsuBeatmap.Write(chart, 1));
        Assert.Equal(GeometrySafetyAttribution.Unattributable, Assert.Single(links).Attribution);
    }

    [Fact]
    public void DecisionMayAbstainWithoutCreatingMutation()
    {
        var recorder = Recorder();
        var state = recorder.State([], [], [], 0, 0, 0);
        recorder.ObserveDecision(GenerationProvenanceStage.Pass1BaseOpportunity, "OP",
            GenerationDecisionDisposition.ExperimentalAbstain, "CANDIDATE", state,
            state with { OpportunityCursor = 1, GenerationStateHash = "NEXT" }, "fake-treatment");
        var trace = recorder.Build();
        Assert.Single(trace.Decisions);
        Assert.Empty(trace.Mutations);
    }

    [Fact]
    public void SameTimestampMutationsRemainDistinctBySequenceAndOpportunity()
    {
        var recorder = Recorder();
        var s0 = recorder.State([], [], [], 0, 0, 0);
        var a = ManiaObject.Tap(0, 500, true);
        var s1 = recorder.State([], [a], [], 0, 0, 1);
        recorder.ObserveMutation(GenerationProvenanceStage.Pass1BaseOpportunity,
            GenerationMutationKind.InsertTap, "OP-A", a, 1, 1, s0, s1);
        var b = ManiaObject.Tap(1, 500, true);
        var s2 = recorder.State([], [a, b], [], 0, 0, 2);
        recorder.ObserveMutation(GenerationProvenanceStage.Pass1BaseOpportunity,
            GenerationMutationKind.InsertTap, "OP-B", b, 1, 1, s1, s2);
        Assert.NotEqual(recorder.Build().Mutations[0].MutationId, recorder.Build().Mutations[1].MutationId);
    }

    [Fact]
    public void TraceValidatorRejectsWrongBeforeGapFakeDownstreamAndRandomId()
    {
        var recorder = Recorder();
        var s0 = recorder.State([], [], [], 0, 0, 0);
        recorder.ObserveDecision(GenerationProvenanceStage.Pass1BaseOpportunity, "OP-A",
            GenerationDecisionDisposition.ProbabilityAbstain, null, s0,
            s0 with { GenerationStateHash = "AFTER-A" });
        recorder.ObserveMutation(GenerationProvenanceStage.Pass1BaseOpportunity,
            GenerationMutationKind.InsertTap, "OP-B", ManiaObject.Tap(0, 500, true), 1, 1,
            s0 with { GenerationStateHash = "WRONG-BEFORE" }, s0 with { GenerationStateHash = "AFTER-B" },
            GenerationCausalOrigin.TreatmentDownstream);
        var trace = recorder.Build();
        trace = trace with { Mutations = [trace.Mutations[0] with { MutationId = "RANDOM-ID" }] };
        var validation = GenerationProvenanceTraceValidatorResearch.Validate(trace);
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, x => x.Contains("continuity"));
        Assert.Contains(validation.Errors, x => x.Contains("ancestor"));
        Assert.Contains(validation.Errors, x => x.Contains("ID"));
    }

    [Fact]
    public void ContractIsStableShadowOnlyAndEndsAtRoadmapReview()
    {
        var contract = SafetyProvContractResearch.Create();
        Assert.False(contract.BehaviorChange);
        Assert.Equal("C", contract.D1HistoricalOutcome);
        Assert.Equal("C", contract.D1SafetyHistoricalOutcome);
        Assert.Equal("legacy-experimental.1", contract.DefaultPolicy);
        Assert.Equal("ROADMAP_REVIEW", contract.NextRecommendedAction);
        Assert.Equal(SafetyProvContractResearch.ComputeHash(contract),
            SafetyProvContractResearch.ComputeHash(SafetyProvContractResearch.Create()));
        Assert.Equal(SafetyProvContractResearch.SerializeArtifact(),
            SafetyProvContractResearch.SerializeArtifact());
    }

    [Fact]
    public void ExistingBaselineIdentityRejectsHeadOriginContractAndSnapshotMismatch()
    {
        var identity = new ExperimentRepositoryIdentity("HEAD", "main", "HEAD", false,
            "CONTRACT", "MANIFEST", "SNAPSHOT");
        Assert.True(ExperimentBaselineIdentityResearch.CertificateMatches(identity, identity));
        Assert.False(ExperimentBaselineIdentityResearch.CertificateMatches(identity,
            identity with { RepositoryEntryHead = "OTHER" }));
        Assert.False(ExperimentBaselineIdentityResearch.CertificateMatches(identity,
            identity with { OriginMainHead = "OTHER" }));
        Assert.False(ExperimentBaselineIdentityResearch.CertificateMatches(identity,
            identity with { SemanticContractHash = "OTHER" }));
        Assert.False(ExperimentBaselineIdentityResearch.CertificateMatches(identity,
            identity with { ImplementationSnapshotHash = "OTHER" }));
        Assert.False(ExperimentBaselineIdentityResearch.CertificateMatches(identity,
            identity with { Branch = "other" }));
    }

    private static GenerationProvenanceRecorderResearch Recorder(string chart = ChartFingerprint, int seed = 1,
        AddNotesOptions? options = null) => new(GenerationProvenanceIdentityResearch.Create(chart, seed,
            options ?? new AddNotesOptions(), "legacy-experimental.1", ["ENGINE-ORDERED-OPPORTUNITIES"]));

    private static string DecisionIdentity(AddNotesResult result) => string.Join('\n',
        result.DecisionDiagnostics!.Decisions.Select(x =>
            $"{x.OpportunityKey}|{x.CandidateKey}|{x.LegacyOutcome}|{x.LegacySelectedLane}"));

    private static string ArticulationDecisionIdentity(AddNotesResult result) => string.Join('\n',
        result.DecisionDiagnostics!.ArticulationIntents.Select(x =>
            $"{x.OpportunityKey}|{x.ParentObservationId}|{x.AnchorTime}|{x.LegacyDecision}"));

    private static GeometrySafetyObject SafetyTap(string id, decimal beat, int time, int lane = 0) =>
        new(id, ManiaObject.Tap(lane, time), beat, beat);
    private static GeometrySafetyObject SafetyLn(string id, decimal start, decimal end,
        int startTime, int endTime, int lane = 0) =>
        new(id, ManiaObject.Ln(lane, startTime, endTime), start, end);

    private static ProvenancePairEvent[] DivergentControl() =>
    [
        Pair("OP-DIRECT", "insert", "S0", "CONTROL-S1"),
        Pair("OP-DOWNSTREAM", "later", "CONTROL-S1", "CONTROL-S2")
    ];

    private static ProvenancePairEvent[] DivergentTreatment() =>
    [
        Pair("OP-DIRECT", "abstain", "S0", "TREATMENT-S1",
            disposition: GenerationDecisionDisposition.ExperimentalAbstain, direct: true),
        Pair("OP-DOWNSTREAM", "later", "TREATMENT-S1", "TREATMENT-S2")
    ];

    private static ProvenancePairEvent Pair(string opportunity, string decision, string before, string after,
        string? geometry = null, bool complete = true,
        GenerationDecisionDisposition disposition = GenerationDecisionDisposition.Place,
        bool direct = false) => new(opportunity, decision, disposition,
        State(before, geometry ?? before, complete), State(after, geometry ?? after, complete), direct);

    private static GenerationStateIdentity State(string generation, string geometry, bool complete) =>
        new(geometry, generation, complete ? 0 : null, complete ? 0 : null, 0, "PENDING", complete);

    private static AddNotesOptions ArticulationOptions() => new()
    {
        Chance = 1,
        ContextualDensityNormalizationEnabled = false,
        DensityDecayPerColumn = 1,
        InteriorLnOpportunitiesEnabled = true,
        MaxInteriorOpportunitiesPerSource = 2,
        InteriorMinimumSourceBeats = 3,
        InteriorMinimumContextLnCount = 3,
        InteriorMinimumSupportedAnchors = 2,
        ArticulationEnabled = true,
        ArticulationMaxNonHeldColumns = 1,
        RetriggerGapMinimumSupport = 2,
        RetriggerGapWindowBeats = 4
    };

    private static ManiaChart ArticulationFixture(int keys, int nonHeldAtAnchor)
    {
        const decimal beatLength = 500, parentEndBeat = 8, headBeat = 4, gap = .25m;
        static int T(decimal beat) => (int)decimal.Round(beat * beatLength, 0, MidpointRounding.AwayFromZero);
        var objects = new List<ManiaObject>
        {
            ManiaObject.Ln(0, T(0), T(parentEndBeat)),
            ManiaObject.Ln(1, T(0), T(headBeat - gap)),
            ManiaObject.Ln(1, T(headBeat), T(parentEndBeat))
        };
        var heldOtherLanes = Math.Max(0, keys - nonHeldAtAnchor - 2);
        for (var lane = 2; lane < keys; lane++)
        {
            objects.Add(ManiaObject.Ln(lane, T(-2), T(-gap)));
            objects.Add(ManiaObject.Tap(lane, T(0)));
            objects.Add(lane - 2 < heldOtherLanes
                ? ManiaObject.Ln(lane, T(headBeat), T(parentEndBeat))
                : ManiaObject.Tap(lane, T(headBeat + .2m)));
        }
        return Chart(keys, objects);
    }

    private static ManiaChart SimpleChart(int keys)
    {
        var objects = new List<ManiaObject> { ManiaObject.Tap(0, 0) };
        if (keys > 1) objects.Add(ManiaObject.Ln(1, 500, 1000));
        return Chart(keys, objects);
    }

    private static ManiaChart Chart(int keys, IEnumerable<ManiaObject> values)
    {
        var objects = values.Select((item, index) => item with
        {
            Sequence = index,
            RawLine = Raw(item, keys)
        }).ToArray();
        return new ManiaChart
        {
            KeyCount = keys,
            Lines = ["osu file format v14", "[General]", "Mode:3", "[Metadata]", "Version:Test",
                "BeatmapID:0", "[Difficulty]", $"CircleSize:{keys}", "[TimingPoints]",
                "0,500,4,2,0,100,1,0", "[HitObjects]"],
            OriginalObjects = objects.Where(x => !x.IsSynthetic).ToArray(),
            AddedObjects = objects.Where(x => x.IsSynthetic).ToArray(),
            TimingPoints = [new TimingPoint(0, 500)]
        };
    }

    private static string Raw(ManiaObject item, int keys)
    {
        var x = (int)Math.Floor((item.Lane + .5) * 512 / keys);
        return item.Type == ManiaObjectType.Tap
            ? $"{x},192,{item.StartTime},1,0,0:0:0:0:"
            : $"{x},192,{item.StartTime},128,0,{item.EndTime}:0:0:0:0:";
    }

    private sealed class PositionedTranscriptRandom : IDerivableRandomSource, IRandomPositionSource
    {
        private readonly IRandomSource _inner;
        private readonly List<string> _transcript;
        private readonly string _domain;

        public PositionedTranscriptRandom(int seed) : this(new SeededRandom(seed), [], "root") { }
        private PositionedTranscriptRandom(IRandomSource inner, List<string> transcript, string domain)
            => (_inner, _transcript, _domain) = (inner, transcript, domain);
        public IReadOnlyList<string> Transcript => _transcript;
        public long CallCount => _transcript.Count;
        public double NextDouble()
        {
            var value = _inner.NextDouble();
            _transcript.Add($"{_domain}:D:{value:R}");
            return value;
        }
        public int Next(int maximum)
        {
            var value = _inner.Next(maximum);
            _transcript.Add($"{_domain}:I:{maximum}:{value}");
            return value;
        }
        public IRandomSource Derive(int salt)
        {
            _transcript.Add($"{_domain}:R:{salt}");
            var derived = _inner is IDerivableRandomSource d ? d.Derive(salt) : new SeededRandom(salt);
            return new PositionedTranscriptRandom(derived, _transcript, $"derived-{salt}");
        }
    }
}
