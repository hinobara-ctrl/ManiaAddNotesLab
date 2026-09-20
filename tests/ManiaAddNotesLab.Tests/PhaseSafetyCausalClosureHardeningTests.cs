using ManiaAddNotesLab.Core;
using Xunit;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseSafetyCausalClosureHardeningTests
{
    [Fact]
    public void FullReconvergenceClearsOldCausalLineage()
    {
        // This is primarily tested by BadControlTemporalAfterReconvergenceNeverReinheritsOldDivergence
        // We add an explicit test mapping to the roadmap terminology here.
        var control = new[]
        {
            Pair("DIVERGENCE", "place", "S0", "C1"),
            Pair("RECONVERGED", "same", "R", "R2"),
            Pair("AFTER", "left-after", "U1", "U2")
        };
        var treatment = new[]
        {
            Pair("DIVERGENCE", "abstain", "S0", "T1", direct: true,
                disposition: GenerationDecisionDisposition.ExperimentalAbstain),
            Pair("RECONVERGED", "same", "R", "R2"),
            Pair("AFTER", "right-after", "V1", "V2")
        };
        var result = ProvenancePairComparatorResearch.Compare("CLOSURE-HARDENING", control, treatment);
        Assert.Contains("RECONVERGED", result.ReconvergenceOpportunityKeys);
        Assert.DoesNotContain("AFTER", result.DownstreamAffectedOpportunityKeys);
    }

    [Fact]
    public void G1AbstainRecorderUsesCompleteOpportunityPreStateRatherThanPostCandidateGateState()
    {
        for (var seed = 1; seed <= 64; seed++)
        {
            var run = RunObservedTreatment(seed);
            var abstain = run.Trace.Decisions.FirstOrDefault(x =>
                x.Disposition == GenerationDecisionDisposition.ExperimentalAbstain);
            if (abstain is null) continue;
            var gate = run.Result.G1GateDiagnostics!.DirectDecisions.Single(x =>
                x.OpportunityKey == abstain.OpportunityKey);
            var ordered = run.Trace.Decisions.Cast<object>().Concat(run.Trace.Mutations)
                .Select(x => x switch
                {
                    GenerationDecisionEvent decision => (decision.SequenceIndex, decision.StateAfter),
                    GenerationMutationEvent mutation => (mutation.SequenceIndex, mutation.StateAfter),
                    _ => throw new InvalidOperationException()
                }).OrderBy(x => x.SequenceIndex).ToArray();
            var prior = ordered.Single(x => x.SequenceIndex == abstain.SequenceIndex - 1).StateAfter;

            Assert.True(abstain.StateBefore.CompleteForCausalLineage);
            Assert.Equal(prior.GenerationStateHash, abstain.StateBefore.GenerationStateHash);
            Assert.Equal(prior.GeometryStateHash, abstain.StateBefore.GeometryStateHash);
            Assert.NotEqual(gate.PreGateGenerationStateHash, abstain.StateBefore.GenerationStateHash);
            Assert.True(abstain.RngPositionBefore < abstain.RngPositionAfter);
            Assert.Equal(abstain.StateBefore.OpportunityCursor + 1, abstain.StateAfter.OpportunityCursor);
            return;
        }
        Assert.Fail("The real G1 treatment path never produced an abstention.");
    }

    [Fact]
    public void CanonicalReplayArtifactComparisonUsesRunnerArtifactSetAndDetectsDrift()
    {
        var root = Path.Combine(Path.GetTempPath(), "safety-causal-replay-" + Guid.NewGuid().ToString("N"));
        var first = Path.Combine(root, "runA");
        var second = Path.Combine(root, "runB");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        try
        {
            foreach (var relative in SafetyCausalReplayArtifactResearch.CanonicalArtifactFileNames)
            {
                File.WriteAllText(Path.Combine(first, relative), relative + "\ncanonical\n");
                File.WriteAllText(Path.Combine(second, relative), relative + "\ncanonical\n");
            }

            var equal = SafetyCausalReplayArtifactResearch.CompareDirectories(first, second);
            Assert.True(equal.Equivalent);
            Assert.Empty(equal.MissingArtifacts);
            Assert.Empty(equal.DifferentArtifacts);

            var changed = SafetyCausalReplayArtifactResearch.CanonicalArtifactFileNames[0];
            File.AppendAllText(Path.Combine(second, changed), "drift");
            var unequal = SafetyCausalReplayArtifactResearch.CompareDirectories(first, second);
            Assert.False(unequal.Equivalent);
            Assert.Equal(changed, Assert.Single(unequal.DifferentArtifacts));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProvenanceInstrumentationOffOnIsNoninterferingOnRealG1GenerationPath()
    {
        for (var seed = 1; seed <= 64; seed++)
        {
            var chart = Fixture();
            var options = Options();
            var profile = MapperEvidenceProfileBuilder.Build(chart);
            var configuration = G1GateRuntimeConfiguration.Frozen(G1GateEvidenceIndex.Build(chart), "closure");
            var cleanRng = new CountingRandom(seed);
            var observedRng = new CountingRandom(seed);
            var clean = new AddNotesEngine().Apply(chart, options, cleanRng, profile, null, null, configuration);
            var recorder = Recorder(profile, seed, options);
            var observed = new AddNotesEngine().Apply(chart, options, observedRng, profile, null, recorder,
                configuration);
            if (!observed.G1GateDiagnostics!.DirectDecisions.Any(x => !x.Admitted)) continue;

            Assert.Equal(OsuBeatmap.Write(clean.ModifiedChart, options.Chance),
                OsuBeatmap.Write(observed.ModifiedChart, options.Chance));
            Assert.Equal(clean.ModifiedChart.AddedObjects, observed.ModifiedChart.AddedObjects);
            Assert.Equal(cleanRng.CallCount, observedRng.CallCount);
            Assert.Equal(cleanRng.Transcript, observedRng.Transcript);
            Assert.Equal(0, recorder.RecorderRngCalls);
            var validation = GenerationProvenanceTraceValidatorResearch.Validate(recorder.Build());
            Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
            return;
        }
        Assert.Fail("The real G1 treatment path never produced an abstention.");
    }

    private static (AddNotesResult Result, GenerationProvenanceTrace Trace) RunObservedTreatment(int seed)
    {
        var chart = Fixture();
        var options = Options();
        var profile = MapperEvidenceProfileBuilder.Build(chart);
        var recorder = Recorder(profile, seed, options);
        var result = new AddNotesEngine().Apply(chart, options, new CountingRandom(seed), profile, null, recorder,
            G1GateRuntimeConfiguration.Frozen(G1GateEvidenceIndex.Build(chart), "closure"));
        return (result, recorder.Build());
    }

    private static GenerationProvenanceRecorderResearch Recorder(MapperEvidenceProfile profile, int seed,
        AddNotesOptions options) => new(GenerationProvenanceIdentityResearch.Create(profile.ChartFingerprint,
            seed, options, G1GatePolicyVersions.Treatment, []));

    private static AddNotesOptions Options() => InteriorRelationMembershipResearch.CurrentOptions() with
    {
        Chance = 1,
        ArticulationEnabled = false,
        ContextualDensityNormalizationEnabled = false,
        StartMs = 1500,
        EndMs = 1500,
        Trace = true
    };

    private static ManiaChart Fixture() => new()
    {
        KeyCount = 7,
        Lines = [],
        OriginalObjects = new[]
        {
            ManiaObject.Ln(0, 0, 4000),
            ManiaObject.Ln(1, 500, 1500),
            ManiaObject.Ln(2, 1000, 2500),
            ManiaObject.Ln(3, 1500, 3000)
        }.Select((value, index) => value with { Sequence = index }).ToArray(),
        TimingPoints = [new TimingPoint(0, 500)]
    };

    private static ProvenancePairEvent Pair(string opportunity, string decision, string before, string after,
        string? geometry = null, bool complete = true, bool direct = false,
        GenerationDecisionDisposition disposition = GenerationDecisionDisposition.Place) => new(
            opportunity, decision, disposition, State(before, geometry ?? before, complete),
            State(after, geometry ?? after, complete), direct);

    private static GenerationStateIdentity State(string generation, string geometry, bool complete) => new(
        geometry, generation, complete ? 0 : null, complete ? 0 : null, 0, "PENDING", complete);

    private sealed class CountingRandom(int seed) : IRandomPositionSource
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
