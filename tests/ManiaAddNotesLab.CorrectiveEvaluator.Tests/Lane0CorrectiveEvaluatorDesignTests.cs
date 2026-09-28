using System.Collections.Immutable;
using System.Security.Cryptography;
using ManiaAddNotesLab.Core;
using Xunit;

public sealed class Lane0CorrectiveEvaluatorDesignTests
{
    [Fact]
    public void BranchesPairByExactOccurrenceIdentity()
    {
        var historical = ImmutableArray.Create(Occurrence("b", 2), Occurrence("a", 1));
        var corrected = ImmutableArray.Create(Corrected(Occurrence("a", 1)), Corrected(Occurrence("b", 2)));
        var result = Lane0CorrectiveEvaluationRunner.CompareG1Branches(historical, corrected);
        Assert.Empty(result.IntegrityFailures);
        Assert.Equal(["a", "b"], result.Transitions.Select(x => x.OccurrenceId));
    }

    [Fact]
    public void OnlyAnchorAndFutureHeldIdentitiesMayDiffer()
    {
        var oldValue = Occurrence("target", 1);
        var newValue = oldValue with { AnchorTime = 100, FutureHeldObservationIds = [9, 10] };
        Assert.Empty(Lane0CorrectiveEvaluationRunner.CompareG1Branches([oldValue], [newValue]).IntegrityFailures);
    }

    [Fact]
    public void QueryDriftIsRejected()
    {
        var oldValue = Occurrence("target", 1);
        var result = Lane0CorrectiveEvaluationRunner.CompareG1Branches(
            [oldValue], [Corrected(oldValue) with { QuerySignature = "OTHER" }]);
        Assert.Contains(result.IntegrityFailures, x => x.Contains("structural drift", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingOccurrenceIsRejected()
    {
        var result = Lane0CorrectiveEvaluationRunner.CompareG1Branches(
            [Occurrence("a", 1), Occurrence("b", 2)], [Corrected(Occurrence("a", 1))]);
        Assert.Contains("Historical/corrected occurrence ID sets differ.", result.IntegrityFailures);
    }

    [Fact]
    public void DuplicateOccurrenceIsRejected()
    {
        var value = Occurrence("a", 1);
        var result = Lane0CorrectiveEvaluationRunner.CompareG1Branches(
            [value, value], [Corrected(value)]);
        Assert.Contains(result.IntegrityFailures, x => x.StartsWith("Duplicate historical", StringComparison.Ordinal));
    }

    [Fact]
    public void PriorDonorSurvivesCorrection()
    {
        var target = Occurrence("target", 1, joint: "J", anchor: 100);
        var donor = Occurrence("prior", 2, joint: "J", anchor: 90);
        var transition = CompareTarget(target, donor, futureIds: []);
        Assert.True(transition.CorrectedJointSupported);
        Assert.Equal("JointUnique", transition.CorrectedState);
    }

    [Fact]
    public void SameTimeDonorIsHistoricalSupportButCorrectedExclusion()
    {
        var transition = CompareTarget(
            Occurrence("target", 1, joint: "J", anchor: 100),
            Occurrence("same", 2, joint: "J", anchor: 100), [2]);
        Assert.True(transition.HistoricalJointSupported);
        Assert.False(transition.CorrectedJointSupported);
        Assert.Equal(2, transition.TemporalExclusion);
    }

    [Fact]
    public void FutureDonorIsHistoricalSupportButCorrectedExclusion()
    {
        var transition = CompareTarget(
            Occurrence("target", 1, joint: "J", anchor: 100),
            Occurrence("future", 2, joint: "J", anchor: 110), [2]);
        Assert.True(transition.HistoricalJointSupported);
        Assert.False(transition.CorrectedJointSupported);
        Assert.Equal(1, transition.BothTemporalAndIdentity);
    }

    [Fact]
    public void PriorIdentityCollisionIsStillExcluded()
    {
        var transition = CompareTarget(
            Occurrence("target", 1, joint: "J", anchor: 100),
            Occurrence("prior", 2, joint: "J", anchor: 90), [2]);
        Assert.False(transition.CorrectedJointSupported);
        Assert.Equal(1, transition.FutureHeldIdentityExclusion);
        Assert.Equal(1, transition.TemporalExclusion);
    }

    [Fact]
    public void TemporalAndIdentityUnionCountsDonorOnce()
    {
        var transition = CompareTarget(
            Occurrence("target", 1, joint: "J", anchor: 100),
            Occurrence("future", 2, joint: "J", anchor: 110), [2]);
        Assert.Equal(2, transition.TemporalExclusion);
        Assert.Equal(1, transition.FutureHeldIdentityExclusion);
        Assert.Equal(1, transition.BothTemporalAndIdentity);
        Assert.Equal(2, transition.UniqueFutureHeldExcluded);
    }

    [Fact]
    public void UnrelatedBarrierKeepsPartitionValid()
    {
        var target = Occurrence("target", 1, anchor: 100);
        var unrelated = Occurrence("other", 2, anchor: 90) with { QuerySignature = "OTHER" };
        var audit = Lane0SpatialResearch.Audit(target, [target, unrelated]);
        Assert.True(audit.PartitionValid);
        Assert.Equal(audit.DonorsConsidered, audit.Eligible.Length + audit.UniqueExcluded);
    }

    [Fact]
    public void CorrectedJointUniqueCanBecomeNoContext()
    {
        var transition = CompareTarget(Occurrence("target", 1, anchor: 100),
            Occurrence("future", 2, anchor: 110), [2]);
        Assert.Equal("JointUnique", transition.HistoricalState);
        Assert.Equal("NoContext", transition.CorrectedState);
    }

    [Fact]
    public void CorrectedAmongAlternativesCanBecomeContradiction()
    {
        var target = Occurrence("target", 1, joint: "J", anchor: 100);
        var futureMatch = Occurrence("future", 2, joint: "J", anchor: 110);
        var priorAlternative = Occurrence("prior", 3, joint: "ALT", anchor: 90);
        var historical = ImmutableArray.Create(Historical(target), Historical(futureMatch), Historical(priorAlternative));
        var corrected = ImmutableArray.Create(
            target with { FutureHeldObservationIds = [2] }, Corrected(futureMatch), Corrected(priorAlternative));
        var transition = Lane0CorrectiveEvaluationRunner.CompareG1Branches(historical, corrected)
            .Transitions.Single(x => x.OccurrenceId == "target");
        Assert.Equal("JointAmongAlternatives", transition.HistoricalState);
        Assert.Equal("Contradiction", transition.CorrectedState);
    }

    [Fact]
    public void UnaffectedSupportIsStableAcrossBranches()
    {
        var transition = CompareTarget(Occurrence("target", 1, anchor: 100),
            Occurrence("prior", 2, anchor: 90), []);
        Assert.Equal(transition.HistoricalState, transition.CorrectedState);
        Assert.Equal(transition.HistoricalJointSupported, transition.CorrectedJointSupported);
    }

    [Fact]
    public void HistoricalCaseResolverAcceptsExactEleven()
    {
        var transitions = ElevenHistoricalTransitions();
        var blocked = ImmutableArray.CreateBuilder<string>();
        var rule = new Lane0HistoricalCaseRule(11,
            ImmutableSortedDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal).Add("chart", 11), 7);
        Assert.Equal(11, Lane0CorrectiveEvaluationRunner.ResolveHistoricalCases(transitions, rule, blocked).Length);
        Assert.Empty(blocked);
    }

    [Fact]
    public void HistoricalCaseCountMismatchIsInvalidFailure()
    {
        var failures = ImmutableArray.CreateBuilder<string>();
        Lane0CorrectiveEvaluationRunner.ResolveHistoricalCases(ElevenHistoricalTransitions(),
            new Lane0HistoricalCaseRule(10, Distribution(11), 7), failures);
        Assert.Contains(failures, x => x.Contains("count mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void HistoricalCaseDistributionMismatchIsInvalidFailure()
    {
        var failures = ImmutableArray.CreateBuilder<string>();
        Lane0CorrectiveEvaluationRunner.ResolveHistoricalCases(ElevenHistoricalTransitions(),
            new Lane0HistoricalCaseRule(11, Distribution(10), 7), failures);
        Assert.Contains(failures, x => x.Contains("distribution mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void HistoricalCaseKeymodeMismatchIsInvalidFailure()
    {
        var failures = ImmutableArray.CreateBuilder<string>();
        Lane0CorrectiveEvaluationRunner.ResolveHistoricalCases(ElevenHistoricalTransitions(),
            new Lane0HistoricalCaseRule(11, Distribution(11), 4), failures);
        Assert.Contains(failures, x => x.Contains("keymode mismatch", StringComparison.Ordinal));
    }

    [Fact]
    public void OperationalUniverseIsIndependentFromSupport()
    {
        var historical = ImmutableArray.Create(Historical(Occurrence("target", 1)), Historical(Occurrence("donor", 2)));
        var corrected = historical.Select(Corrected).ToImmutableArray();
        var result = Lane0CorrectiveEvaluationRunner.CompareG1Branches(historical, corrected,
            new HashSet<string>(["target"], StringComparer.Ordinal),
            new HashSet<string>(["target"], StringComparer.Ordinal));
        Assert.True(result.Transitions.Single(x => x.OccurrenceId == "target").CorrectedOperational);
        Assert.False(result.Transitions.Single(x => x.OccurrenceId == "donor").CorrectedOperational);
    }

    [Fact]
    public void RiceSentinelAcceptsExactEquality()
    {
        var value = Population(1);
        Assert.True(Lane0CorrectiveEvaluationRunner.ValidateRiceSentinel(value, value));
    }

    [Fact]
    public void RiceSentinelRejectsAnyChangedCount()
    {
        Assert.False(Lane0CorrectiveEvaluationRunner.ValidateRiceSentinel(Population(1), Population(2)));
    }

    [Theory]
    [InlineData(true, false, true, true, "FEASIBILITY_DEMONSTRATED")]
    [InlineData(true, false, true, false, "LIMITED_PARK")]
    [InlineData(false, false, true, true, "INVALID")]
    [InlineData(true, true, true, true, "BLOCKED")]
    public void ClassifierHasFrozenPrecedence(bool valid, bool blocked, bool rice, bool g1, string expected) =>
        Assert.Equal(expected, Lane0CorrectiveEvaluationRunner.Classify(valid, blocked, rice, g1));

    [Fact]
    public void EmptyInMemoryEvaluationConsumesNoRngAndRemainsBlocked()
    {
        var result = Lane0CorrectiveEvaluationRunner.Evaluate([], new(false));
        Assert.Equal(0, result.ResearchRngCalls);
        Assert.Equal("BLOCKED", result.Outcome);
        Assert.False(result.BehaviorChanged);
        Assert.True(result.InputsUnchanged);
    }

    [Fact]
    public void ChartFingerprintIsUnchangedByEvaluation()
    {
        var chart = SyntheticChart();
        var before = MapperEvidenceProfileBuilder.ComputeFingerprint(chart);
        var result = Lane0CorrectiveEvaluationRunner.Evaluate([new("synthetic", chart)], new(false));
        Assert.Equal(before, MapperEvidenceProfileBuilder.ComputeFingerprint(chart));
        Assert.True(result.InputsUnchanged);
    }

    [Fact]
    public void SerializationIsByteDeterministic()
    {
        var result = Lane0CorrectiveEvaluationRunner.Evaluate([], new(false));
        var first = Lane0CorrectiveEvaluationRunner.SerializeArtifacts(result);
        var second = Lane0CorrectiveEvaluationRunner.SerializeArtifacts(result);
        Assert.Equal(first.Keys, second.Keys);
        foreach (var key in first.Keys) Assert.Equal(first[key], second[key]);
    }

    [Fact]
    public void ArtifactSetIsClosedAndNamed()
    {
        var artifacts = Lane0CorrectiveEvaluationRunner.SerializeArtifacts(
            Lane0CorrectiveEvaluationRunner.Evaluate([], new(false)));
        Assert.Equal(["chart_family_keymode_results.csv", "g1_historical_operational_cases.csv",
            "g1_state_transitions.csv", "integrity.json", "scientific_summary.json"], artifacts.Keys);
    }

    [Fact]
    public void ExplicitWriterProducesChecksumsForEveryArtifact()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"lane0-core-{Guid.NewGuid():N}");
        try
        {
            var result = Lane0CorrectiveEvaluationRunner.Evaluate([], new(false));
            Lane0CorrectiveEvaluationRunner.WriteArtifacts(result, directory);
            var lines = File.ReadAllLines(Path.Combine(directory, "sha256sums.txt"));
            Assert.Equal(5, lines.Length);
            Assert.All(lines, line => Assert.Matches("^[0-9A-F]{64}  ", line));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TransitionMatrixIsDeterministicallySorted()
    {
        var result = Lane0CorrectiveEvaluationRunner.Evaluate([], new(false));
        Assert.Equal(result.G1TransitionMatrix.OrderBy(x => x.HistoricalState, StringComparer.Ordinal)
            .ThenBy(x => x.CorrectedState, StringComparer.Ordinal), result.G1TransitionMatrix);
    }

    [Fact]
    public void SchemaVersionIsExplicit()
    {
        Assert.Equal("lane-0-corrective-evaluation-core.1", Lane0CorrectiveEvaluationRunner.SchemaVersion);
    }

    private static Lane0G1Transition CompareTarget(Lane0Occurrence target, Lane0Occurrence donor,
        ImmutableArray<int> futureIds)
    {
        var historical = ImmutableArray.Create(Historical(target), Historical(donor));
        var corrected = ImmutableArray.Create(target with { FutureHeldObservationIds = futureIds }, Corrected(donor));
        return Lane0CorrectiveEvaluationRunner.CompareG1Branches(historical, corrected)
            .Transitions.Single(x => x.OccurrenceId == target.OccurrenceId);
    }

    private static Lane0Occurrence Occurrence(string id, int observation, string joint = "J", int? anchor = 100) =>
        new(Lane0Family.G1InteriorSpatial, "chart", 7, id, $"group-{id}", $"event-{id}", $"parent-{id}",
            "Q", joint, $"T-{joint}", $"S-{joint}", [observation], [], [], AnchorTime: anchor);

    private static Lane0Occurrence Historical(Lane0Occurrence value) =>
        value with { AnchorTime = null, FutureHeldObservationIds = [] };

    private static Lane0Occurrence Corrected(Lane0Occurrence value) =>
        value with { AnchorTime = value.AnchorTime ?? 100, FutureHeldObservationIds = [] };

    private static ImmutableArray<Lane0G1Transition> ElevenHistoricalTransitions() =>
        Enumerable.Range(1, 11).Select(i => new Lane0G1Transition("chart", 7, $"case-{i:D2}",
            $"group-{i:D2}", "JointUnique", "NoContext", true, false, 10, 0, 1, 0,
            true, true, "JointUnique->NoContext", 11, 0, 1, 1, 1, 1, 1))
            .ToImmutableArray();

    private static ImmutableSortedDictionary<string, int> Distribution(int count) =>
        ImmutableSortedDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal).Add("chart", count);

    private static Lane0PopulationSummary Population(int structural) =>
        new(structural, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 0);

    private static ManiaChart SyntheticChart() => new()
    {
        KeyCount = 7,
        Lines = [],
        OriginalObjects =
        [
            ManiaObject.Ln(0, 0, 4000, sequence: 0),
            ManiaObject.Ln(1, 1000, 2000, sequence: 1),
            ManiaObject.Ln(0, 5000, 9000, sequence: 2),
            ManiaObject.Ln(1, 6000, 7000, sequence: 3)
        ],
        TimingPoints = [new TimingPoint(0, 500)]
    };
}
