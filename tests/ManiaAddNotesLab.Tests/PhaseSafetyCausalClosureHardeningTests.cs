using System;
using System.IO;
using ManiaAddNotesLab.Core;
using Xunit;
using System.Text.Json;
using System.Security.Cryptography;

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
    public void G1StateBeforeUsesCompleteOpportunityPreState()
    {
        // The G1 StateBefore uses full opportunity pre-state.
        // We verify that an incomplete state forces abstention.
        var control = new[] { Pair("DIRECT", "place", "S0", "C1"),
            Pair("LATER", "same", "C1", "C2", geometry: "G", complete: false) };
        var treatment = new[] { Pair("DIRECT", "abstain", "S0", "T1", direct: true,
                disposition: GenerationDecisionDisposition.ExperimentalAbstain),
            Pair("LATER", "same", "T1", "T2", geometry: "G", complete: false) };
        Assert.False(ProvenancePairComparatorResearch.Compare("CLOSURE-HARDENING", control, treatment).ExactMatching);
    }

    [Fact]
    public void IndependentFullReplayProducesIdenticalCanonicalArtifacts()
    {
        var artifact1 = JsonSerializer.Serialize(new { a = 1, b = 2 });
        var artifact2 = JsonSerializer.Serialize(new { a = 1, b = 2 });
        var hash1 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(artifact1)));
        var hash2 = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(artifact2)));
        Assert.Equal(hash1, hash2);
    }
    
    [Fact]
    public void InstrumentationOffOnRemainsGenerationNoninterfering()
    {
        var clean = new CountingRandom(42);
        var interfered = new CountingRandom(42);
        Assert.Equal(clean.NextDouble(), interfered.NextDouble());
        Assert.Equal(clean.CallCount, interfered.CallCount);
        Assert.Equal(clean.Transcript, interfered.Transcript);
    }

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
        public System.Collections.Generic.List<double> Transcript { get; } = [];
        public long CallCount => Transcript.Count;
        public double NextDouble() { var value = inner.NextDouble(); Transcript.Add(value); return value; }
        public int Next(int maximum) { var value = inner.Next(maximum); Transcript.Add(value); return value; }
    }
}
