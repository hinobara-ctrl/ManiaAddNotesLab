using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseC12ExactHeadRelationTests
{
    [Fact]
    public void ExactHeadGroupsAreDeterministicAndKeepExactTimingProvenance()
    {
        var chart = Chart(4, ManiaObject.Ln(2, 0, 1000), ManiaObject.Tap(0, 0),
            ManiaObject.Ln(1, 0, 1500));

        var first = ExactHeadRelationResearch.Evaluate(chart);
        var second = ExactHeadRelationResearch.Evaluate(chart);

        Assert.Equal(first.ChartFingerprint, second.ChartFingerprint);
        Assert.Equal(ExactHeadRelationResearchJson.Serialize(first with { ElapsedMilliseconds = 0 }),
            ExactHeadRelationResearchJson.Serialize(second with { ElapsedMilliseconds = 0 }));
        var group = Assert.Single(first.ExactHeadGroups);
        Assert.Equal(0, group.HeadTime);
        Assert.Equal(0, group.HeadBeat);
        Assert.All(group.Members, x => Assert.Equal(group.HeadTime, x.Timing.HeadTime));
    }

    [Fact]
    public void SameReleaseCreatesOneRelationWithTwoIndependentWitnesses()
    {
        var result = ExactHeadRelationResearch.Evaluate(Chart(4,
            ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, 0, 1000)));
        var group = Assert.Single(result.ExactHeadGroups);
        var relation = Assert.Single(group.EndpointRelations);

        Assert.Equal(LnReleaseStructure.MultipleLongNotesSameRelease, group.ReleaseStructure);
        Assert.Equal(2, relation.IndependentWitnessCount);
        Assert.Equal(2, group.DistinctWitnessCount);
        Assert.Equal(new[] { 0, 1 }, relation.Witnesses.Select(x => x.ObservationId.Value));
    }

    [Fact]
    public void DifferentReleasesCreateCompetingRelationsWithoutProbabilities()
    {
        var result = ExactHeadRelationResearch.Evaluate(Chart(4,
            ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, 0, 1500)));
        var group = Assert.Single(result.ExactHeadGroups);

        Assert.Equal(LnReleaseStructure.MultipleLongNotesDifferentReleases, group.ReleaseStructure);
        Assert.Equal(2, group.DistinctEndpointCount);
        Assert.Equal(new[] { 1000, 1500 }, group.EndpointRelations.Select(x => x.EndpointTime));
        var json = ExactHeadRelationResearchJson.Serialize(result);
        Assert.DoesNotContain("weight", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confidence", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mapperSupport", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WitnessIdentityIsDeduplicatedWhileMultipleClaimsRemain()
    {
        var group = Assert.Single(ExactHeadRelationResearch.Evaluate(Chart(4,
            ManiaObject.Ln(0, 0, 1000))).ExactHeadGroups);
        var witness = Assert.Single(Assert.Single(group.EndpointRelations).Witnesses);

        Assert.Equal(1, group.DistinctWitnessCount);
        Assert.Contains(OriginalEventClaim.Duration, witness.Claims);
        Assert.Contains(OriginalEventClaim.ExactRelease, witness.Claims);
        Assert.Contains(OriginalEventClaim.ExactHead, witness.Claims);
        Assert.Contains(OriginalEventClaim.HeadToRelease, witness.Claims);
    }

    [Fact]
    public void HeldOutTargetCannotDonateButSimultaneousObjectsCanReconstructIt()
    {
        var result = ExactHeadRelationResearch.Evaluate(Chart(4,
            ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, 0, 1000), ManiaObject.Ln(2, 0, 1500)));
        var target = result.HeldOutTargets.Single(x => x.TargetObservationId.Value == 0);

        Assert.Equal(HeldOutRelationState.CompetingEndpointRelations, target.State);
        Assert.True(target.ExactTargetEndpointSupported);
        Assert.Equal(1, target.ExactTargetEndpointWitnessCount);
        Assert.Equal(1, target.DistinctAlternativeEndpoints);
        Assert.True(target.HasExactStructuralTwin);
    }

    [Fact]
    public void LoneTargetHasNoSameHeadEvidenceAfterExclusion()
    {
        var target = Assert.Single(ExactHeadRelationResearch.Evaluate(Chart(4,
            ManiaObject.Ln(0, 0, 1000))).HeldOutTargets);

        Assert.Equal(HeldOutRelationState.NoSameHeadEvidence, target.State);
        Assert.False(target.ExactTargetEndpointSupported);
        Assert.Equal(0, target.ExactTargetEndpointWitnessCount);
    }

    [Fact]
    public void SeparateChartsNeverDonateRelationsToEachOther()
    {
        var first = ExactHeadRelationResearch.Evaluate(Chart(4, ManiaObject.Ln(0, 0, 1000)));
        var second = ExactHeadRelationResearch.Evaluate(Chart(4, ManiaObject.Ln(0, 0, 1500)));

        Assert.Equal(new[] { 1000 }, first.ExactHeadGroups.Single().EndpointRelations.Select(x => x.EndpointTime));
        Assert.Equal(new[] { 1500 }, second.ExactHeadGroups.Single().EndpointRelations.Select(x => x.EndpointTime));
        Assert.All(first.HeldOutTargets.Concat(second.HeldOutTargets),
            x => Assert.Equal(HeldOutRelationState.NoSameHeadEvidence, x.State));
        var firstChart = Chart(4, ManiaObject.Ln(0, 0, 1000));
        var firstComparison = new PriorTargetComparison(
            MapperEvidenceProfileBuilder.Build(firstChart).ChartFingerprint, []);
        Assert.Throws<ArgumentException>(() => ExactHeadRelationResearch.Evaluate(
            Chart(4, ManiaObject.Ln(0, 0, 1500)), firstComparison));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void RelationModelHasNoKeyCountSpecificBranch(int keys)
    {
        var result = ExactHeadRelationResearch.Evaluate(Chart(keys,
            ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(0, 0, 1000)));

        Assert.Equal(keys, result.KeyCount);
        Assert.Equal(2, result.IndependentRelationWitnessCount);
        Assert.All(result.HeldOutTargets, x => Assert.True(x.ExactTargetEndpointSupported));
    }

    [Fact]
    public void RelationSerializationPreservesSchemaAndObservationIds()
    {
        var json = ExactHeadRelationResearchJson.Serialize(ExactHeadRelationResearch.Evaluate(Chart(4,
            ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, 0, 1000))));

        Assert.Contains("phase-c1-2-research.1", json);
        Assert.Contains("observationId", json);
        Assert.Contains("headToRelease", json);
        Assert.Contains("independentWitnessCount", json);
    }

    [Fact]
    public void SupportCertificateExposesValuesAndRelationsWithoutChangingWitnessCount()
    {
        var chart = Chart(4, ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, 0, 1000));
        var result = new AddNotesEngine().Apply(chart, new AddNotesOptions
        {
            Chance = 1,
            ContextualDensityNormalizationEnabled = false,
            DiagnosticsEnabled = true
        }, new SeededRandom(3));
        var certificate = result.DecisionDiagnostics!.Decisions
            .First(x => x.CandidateKind == DiagnosticCandidateKind.LongNote).Certificate;

        Assert.Equal(EvidenceClaimLevel.ObservedRelation, certificate.ClaimLevel);
        Assert.Equal(certificate.IndependentWitnessCount,
            certificate.Witnesses.SelectMany(x => x.ObservationIds).Distinct().Count());
        Assert.Contains(certificate.ObservedValues, x => x.ValueKind == "Duration");
        Assert.Contains(certificate.ObservedRelations, x => x.RelationKind == "ExactHead");
        Assert.Contains(certificate.ObservedRelations, x => x.RelationKind == "HeadToRelease");
    }

    [Fact]
    public void ResearchConsumesNoRandomAndDiagnosticsDoNotChangeBehavior()
    {
        var chart = Chart(4, ManiaObject.Ln(0, 0, 1000), ManiaObject.Ln(1, 0, 1000));
        _ = ExactHeadRelationResearch.Evaluate(chart);
        var options = new AddNotesOptions { Chance = .73, ContextualDensityNormalizationEnabled = false };
        var offRandom = new RecordingRandom(91);
        var onRandom = new RecordingRandom(91);
        var off = new AddNotesEngine().Apply(chart, options, offRandom);
        var on = new AddNotesEngine().Apply(chart, options with { DiagnosticsEnabled = true }, onRandom);

        Assert.Equal(OsuBeatmap.Write(off.ModifiedChart, options.Chance),
            OsuBeatmap.Write(on.ModifiedChart, options.Chance));
        Assert.Equal(offRandom.Transcript, onRandom.Transcript);
        Assert.Equal("legacy-experimental.1", on.DecisionDiagnostics!.BehaviorPolicyVersion);
        Assert.Equal("phase-a.1", on.DecisionDiagnostics.EvidenceProfileVersion);
        Assert.Equal("phase-c1-2-shadow.1", on.DecisionDiagnostics.DecisionDiagnosticVersion);
    }

    [Fact]
    public void ProjectStateAndMasterStateBlocksAreConsistent()
    {
        var root = FindRepositoryRoot();
        using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "PROJECT_STATE.json")));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var status = File.ReadAllText(Path.Combine(root, "PROJECT_STATUS.md"));

        var stateRoot = state.RootElement;
        Assert.Equal("legacy-experimental.1", stateRoot.GetProperty("behaviorPolicyVersion").GetString());
        Assert.False(stateRoot.GetProperty("behaviorChange").GetBoolean());
        Assert.Equal("E.1", stateRoot.GetProperty("currentPhase").GetString());
        Assert.Equal("D1.0", stateRoot.GetProperty("nextRecommendedPhase").GetString());
        Assert.Equal("D1", stateRoot.GetProperty("nextBehavioralPhase").GetString());
        var phases = stateRoot.GetProperty("phases").EnumerateArray().ToArray();
        Assert.Equal("BLOCKED", phases.Single(x => x.GetProperty("id").GetString() == "F2.ACQ")
            .GetProperty("status").GetString());
        Assert.Equal("NOT_AUTHORIZED", phases.Single(x => x.GetProperty("id").GetString() == "D1")
            .GetProperty("authorization").GetString());
        Assert.Equal("CONTINUE_CONDITIONALLY", stateRoot.GetProperty("researchBranches")[0]
            .GetProperty("decision").GetString());
        Assert.Contains("Current phase: E.1", readme);
        Assert.Contains("Current phase: E.1", status);
        Assert.Contains("Next actionable research candidate: D1.0", readme);
        Assert.Contains("Next actionable research candidate: D1.0", status);
        Assert.Contains("Next behavioral phase: D1 — ChordCompletion Resulting-State A/B", readme);
        Assert.Contains("Next behavioral phase: D1 — ChordCompletion Resulting-State A/B", status);
        Assert.Contains("Blocked prerequisite: F2.ACQ — BLOCKED", readme);
        Assert.Contains("Blocked prerequisite: F2.ACQ — BLOCKED", status);
        Assert.Contains("Tests: 518 passed / 0 failed / 0 skipped", readme);
        Assert.Contains("Tests: 518 passed / 0 failed / 0 skipped", status);
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), sourceFile })
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ManiaAddNotesLab.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException();
    }

    private static ManiaChart Chart(int keys, params ManiaObject[] objects) => new()
    {
        KeyCount = keys,
        Lines = [],
        OriginalObjects = objects.Select((x, i) => x with { Sequence = i }).ToArray(),
        TimingPoints = [new TimingPoint(0, 500)]
    };

    private sealed class RecordingRandom(int seed) : IRandomSource
    {
        private readonly SeededRandom _inner = new(seed);
        public List<string> Transcript { get; } = [];
        public double NextDouble() { var value = _inner.NextDouble(); Transcript.Add($"D:{value:R}"); return value; }
        public int Next(int maxExclusive) { var value = _inner.Next(maxExclusive); Transcript.Add($"I:{maxExclusive}:{value}"); return value; }
    }
}
