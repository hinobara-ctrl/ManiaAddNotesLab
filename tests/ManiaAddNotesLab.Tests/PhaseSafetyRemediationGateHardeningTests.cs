using System.Collections.Immutable;
using System.Text;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseSafetyRemediationGateHardeningTests
{
    [Fact]
    public void HardenedImplementationFingerprintChangesWhenTreatmentDefinitionChanges()
    {
        var root = FindRepositoryRoot();
        var paths = new[]
        {
            "src/ManiaAddNotesLab.Core/AddNotesEngine.cs",
            "src/ManiaAddNotesLab.Core/BeatTimeline.cs",
            "src/ManiaAddNotesLab.Core/ChartAnalysis.cs",
            "src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs",
            "src/ManiaAddNotesLab.Core/Model.cs",
            "src/ManiaAddNotesLab.Core/OsuBeatmap.cs",
            "src/ManiaAddNotesLab.Core/SafetyRemediationGateResearch.cs"
        };
        var files = paths.Select(path => new NamedImplementationContent(path,
            File.ReadAllBytes(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))))).ToArray();
        var baseline = ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(files);
        var gate = files.Single(x => x.Name.EndsWith("SafetyRemediationGateResearch.cs", StringComparison.Ordinal));
        var text = Encoding.UTF8.GetString(gate.Content);
        Assert.Contains("public TimedManiaObject Project", text);
        var mutated = files.Select(x => x == gate
            ? new NamedImplementationContent(x.Name, Encoding.UTF8.GetBytes(text.Replace(
                "var start = Beat(value.Object.StartTime);",
                "var start = Beat(value.Object.StartTime + 1);", StringComparison.Ordinal)))
            : x);
        Assert.NotEqual(baseline, ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot(mutated));
    }

    [Fact]
    public void LegalLaneSetDifferenceWithoutCommitOrStateDifferenceDoesNotOpenLineage()
    {
        var same = State("SAME", 0);
        var steps = SafetyRemediationGateHardeningResearch.Classify("pair",
        [
            new("O-0", 0, same, same, same, same, "COMMIT-X", "COMMIT-X", true)
        ]);
        var step = Assert.Single(steps);
        Assert.False(step.DirectGovernedDivergence);
        Assert.Null(step.OpenedLineage);
        Assert.Null(step.ActiveLineageAfter);
    }

    [Fact]
    public void DifferentLegalLaneSetsWithSameSelectedLaneAreNotARejectedControlCommit()
    {
        var decision = Candidate([1, 2, 3], [1, 2], selectedLane: 1);

        Assert.True(decision.GeometrySetsDiffer);
        Assert.True(decision.LegacyAcceptsSelectedLane);
        Assert.True(decision.CanonicalAcceptsSelectedLane);
        Assert.False(SafetyRemediationGateHardeningResearch
            .CanonicalAuthorityRejectedSelectedCommit([decision]));
    }

    [Fact]
    public void SelectionDifferenceWithoutRejectionDoesNotOpenGovernedLineage()
    {
        var equal = State("EQUAL", 0);
        var left = State("LEFT", 1);
        var right = State("RIGHT", 1);
        var selectedControl = Candidate([1, 2, 3], [1, 2], selectedLane: 1);
        var canonicalRejected = SafetyRemediationGateHardeningResearch
            .CanonicalAuthorityRejectedSelectedCommit([selectedControl]);

        var step = Assert.Single(SafetyRemediationGateHardeningResearch.Classify("pair",
        [
            new("O-0", 0, equal, equal, left, right, "COMMIT-L1", "COMMIT-L2",
                canonicalRejected)
        ]));

        Assert.False(step.DirectGovernedDivergence);
        Assert.Null(step.OpenedLineage);
        Assert.True(step.UnexplainedStateDivergence);
    }

    [Fact]
    public void RejectionOfTheActuallySelectedControlLaneOpensGovernedLineage()
    {
        var equal = State("EQUAL", 0);
        var left = State("LEFT", 1);
        var right = State("RIGHT", 1);
        var selectedControl = Candidate([1, 2, 3], [1, 2], selectedLane: 3);
        var canonicalRejected = SafetyRemediationGateHardeningResearch
            .CanonicalAuthorityRejectedSelectedCommit([selectedControl]);

        var step = Assert.Single(SafetyRemediationGateHardeningResearch.Classify("pair",
        [
            new("O-0", 0, equal, equal, left, right, "COMMIT-L3", "COMMIT-L2",
                canonicalRejected)
        ]));

        Assert.True(canonicalRejected);
        Assert.True(step.DirectGovernedDivergence);
        Assert.NotNull(step.OpenedLineage);
        Assert.False(step.UnexplainedStateDivergence);
    }

    [Fact]
    public void UnexplainedDivergenceInFinalSuccessorIsDetectedWithoutANextOpportunity()
    {
        var equal = State("EQUAL", 0);
        var left = State("LEFT", 1);
        var right = State("RIGHT", 1);

        var step = Assert.Single(SafetyRemediationGateHardeningResearch.Classify("pair",
        [
            new("FINAL", 0, equal, equal, left, right, null, null, false)
        ]));

        Assert.True(step.BeforeEqual);
        Assert.False(step.AfterEqual);
        Assert.False(step.DirectGovernedDivergence);
        Assert.True(step.UnexplainedStateDivergence);
    }

    [Fact]
    public void CanonicalRejectWithoutSuccessorStateDifferenceDoesNotContaminateLineage()
    {
        var equal = State("EQUAL", 0);
        var next = State("NEXT", 1);

        var step = Assert.Single(SafetyRemediationGateHardeningResearch.Classify("pair",
        [
            new("O-0", 0, equal, equal, next, next, "REJECTED", null, true)
        ]));

        Assert.False(step.DirectGovernedDivergence);
        Assert.Null(step.ActiveLineageAfter);
        Assert.False(step.UnexplainedStateDivergence);
    }

    [Fact]
    public void FullReconvergenceClearsOldAncestryAndLaterDivergenceGetsNewLineage()
    {
        var equal0 = State("E0", 0);
        var left1 = State("L1", 1);
        var right1 = State("R1", 1);
        var equal2 = State("E2", 2);
        var left3 = State("L3", 3);
        var right3 = State("R3", 3);
        var steps = SafetyRemediationGateHardeningResearch.Classify("pair",
        [
            new("O-0", 0, equal0, equal0, left1, right1, "A", null, true),
            new("O-1", 1, left1, right1, equal2, equal2, null, null, false),
            new("O-2", 2, equal2, equal2, equal2, equal2, null, null, false),
            new("O-3", 3, equal2, equal2, left3, right3, "B", null, true)
        ]);

        Assert.NotNull(steps[0].OpenedLineage);
        Assert.True(steps[1].ReconvergedAfter);
        Assert.Null(steps[2].ActiveLineageBefore);
        Assert.Null(steps[2].ActiveLineageAfter);
        Assert.NotNull(steps[3].OpenedLineage);
        Assert.NotEqual(steps[0].OpenedLineage, steps[3].OpenedLineage);
    }

    [Fact]
    public void CanonicalRejectCommittedToTreatmentFailsTheCommitInvariant()
    {
        Assert.False(SafetyRemediationGateHardeningResearch.CanonicalRejectWasNotCommitted("REJECTED", "REJECTED"));
        Assert.True(SafetyRemediationGateHardeningResearch.CanonicalRejectWasNotCommitted("REJECTED", null));
        Assert.True(SafetyRemediationGateHardeningResearch.CanonicalRejectWasNotCommitted("REJECTED", "OTHER"));
    }

    [Fact]
    public void EqualHashesWithIncompleteParentsAreNotCausalEquality()
    {
        var complete = State("SAME", 0);
        var incomplete = complete with { CompleteForCausalLineage = false };
        var difference = SafetyRemediationForensicAttributionResearch.Compare(incomplete, incomplete);
        var step = Assert.Single(SafetyRemediationGateHardeningResearch.Classify("pair",
        [
            new("O-0", 0, incomplete, incomplete, incomplete, incomplete, null, null, false)
        ]));

        Assert.Equal(ForensicStateDifferenceKind.EqualHashIncomplete, difference.Kind);
        Assert.False(step.BeforeEqual);
        Assert.True(step.UnexplainedStateDivergence);
        Assert.Null(step.OpenedLineage);
    }

    [Fact]
    public void AlreadyDifferentParentsDoNotLetALaterCanonicalRejectClaimTheirOrigin()
    {
        var left = State("LEFT", 0);
        var right = State("RIGHT", 0);
        var leftAfter = State("LEFT-AFTER", 1);
        var rightAfter = State("RIGHT-AFTER", 1);
        var step = Assert.Single(SafetyRemediationGateHardeningResearch.Classify("pair",
        [
            new("O-0", 0, left, right, leftAfter, rightAfter, "CONTROL", null, true)
        ]));

        Assert.False(step.BeforeEqual);
        Assert.False(step.DirectGovernedDivergence);
        Assert.True(step.UnexplainedStateDivergence);
    }

    [Fact]
    public void ContinuousUnattributedDifferencesAreOneDiagnosticEpisodeNotCausalLineage()
    {
        var left0 = State("L0", 0);
        var right0 = State("R0", 0);
        var left1 = State("L1", 1);
        var right1 = State("R1", 1);
        var left2 = State("L2", 2);
        var right2 = State("R2", 2);
        var steps = SafetyRemediationGateHardeningResearch.Classify("pair",
        [
            new("O-0", 0, left0, right0, left1, right1, null, null, false),
            new("O-1", 1, left1, right1, left2, right2, null, null, false)
        ]);

        var episode = Assert.Single(SafetyRemediationForensicAttributionResearch.GroupEpisodes(steps));
        Assert.Equal(2, episode.ObservationCount);
        Assert.All(steps, x => Assert.Null(x.ActiveLineageAfter));
    }

    [Fact]
    public void CachedAdjacentStatesAreExactlyEquivalentToSimpleUncachedTrace()
    {
        var e0 = State("E0", 0);
        var left1 = State("L1", 1);
        var right1 = State("R1", 1);
        var left2 = State("L2", 2);
        var right2 = State("R2", 2);
        var e3 = State("E3", 3);
        var left4 = State("L4", 4);
        var right4 = State("R4", 4);

        SafetyRemediationGatePairedOpportunity[] cached =
        [
            new("O-0", 0, e0, e0, left1, right1, "COMMIT-A", null, true),
            new("O-1", 1, left1, right1, left2, right2, null, null, false),
            new("O-2", 2, left2, right2, e3, e3, "COMMIT-C", "COMMIT-C", false),
            new("O-3", 3, e3, e3, left4, right4, "COMMIT-B", null, true)
        ];
        var uncached = cached.Select(x => new SafetyRemediationGatePairedOpportunity(
            x.OpportunityKey, x.OpportunityOrder,
            RecomputeWithoutCache(x.ControlBefore), RecomputeWithoutCache(x.TreatmentBefore),
            RecomputeWithoutCache(x.ControlAfter), RecomputeWithoutCache(x.TreatmentAfter),
            x.ControlCommitIdentity, x.TreatmentCommitIdentity,
            x.CanonicalAuthorityChangedCommit)).ToArray();

        Assert.Equal(cached, uncached);
        var cachedLineage = SafetyRemediationGateHardeningResearch.Classify("pair", cached);
        var uncachedLineage = SafetyRemediationGateHardeningResearch.Classify("pair", uncached);
        Assert.Equal(cachedLineage.Length, uncachedLineage.Length);
        for (var index = 0; index < cachedLineage.Length; index++)
            Assert.Equal(cachedLineage[index], uncachedLineage[index]);
        Assert.True(cachedLineage[0].DirectGovernedDivergence);
        Assert.Null(cachedLineage[1].OpenedLineage);
        Assert.True(cachedLineage[2].ReconvergedAfter);
        Assert.True(cachedLineage[3].DirectGovernedDivergence);
        Assert.NotEqual(cachedLineage[0].OpenedLineage, cachedLineage[3].OpenedLineage);
        Assert.True(SafetyRemediationGateHardeningResearch.CanonicalRejectWasNotCommitted(
            cached[0].ControlCommitIdentity!, cached[0].TreatmentCommitIdentity));
    }

    [Fact]
    public void IncrementalDiagnosticFingerprintIsExactForEqualTraceAndSensitiveToStateAndCommit()
    {
        var before = State("BEFORE", 0);
        var after = State("AFTER", 1);
        var candidate = new SafetyRemediationCandidateGeometryDecision("O-0", 0, 0,
            ManiaObjectType.LongNote, 100, 200, .2m, .4m, .2m, .4m,
            [0, 1], [1], "GEOMETRY", 3, 1, true);
        var trace = new SafetyRemediationGateRuntimeDiagnostics("schema", "policy", "contract", true, 0,
            [candidate], [new("O-0", 0, before, after, "COMMIT-A")], [], 1, string.Empty,
            1, string.Empty);
        var structurallyEqual = new SafetyRemediationGateRuntimeDiagnostics("schema", "policy",
            "contract", true, 0, [candidate with { }],
            [new("O-0", 0, RecomputeWithoutCache(before), RecomputeWithoutCache(after), "COMMIT-A")],
            [], 1, string.Empty, 1, string.Empty);
        var changedCommit = trace with
        {
            OpportunityStates = [new("O-0", 0, before, after, "COMMIT-B")]
        };
        var changedState = trace with
        {
            OpportunityStates = [new("O-0", 0, before, State("DIFFERENT", 1), "COMMIT-A")]
        };

        var fingerprint = SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(trace);
        Assert.Equal(fingerprint,
            SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(structurallyEqual));
        Assert.NotEqual(fingerprint,
            SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(changedCommit));
        Assert.NotEqual(fingerprint,
            SafetyRemediationGateHardeningResearch.DiagnosticsFingerprint(changedState));
    }

    [Fact]
    public void CompactControlTracePreservesExactLineageClassificationAndCommits()
    {
        var e0 = State("E0", 0);
        var left1 = State("L1", 1);
        var right1 = State("R1", 1);
        var e2 = State("E2", 2);
        var left3 = State("L3", 3);
        var right3 = State("R3", 3);
        SafetyRemediationGateOpportunityState[] control =
        [
            new("O-0", 0, e0, left1, "COMMIT-A"),
            new("O-1", 1, left1, e2, "COMMIT-C"),
            new("O-2", 2, e2, left3, "COMMIT-B")
        ];
        SafetyRemediationGateOpportunityState[] treatment =
        [
            new("O-0", 0, e0, right1, null),
            new("O-1", 1, right1, e2, "COMMIT-C"),
            new("O-2", 2, e2, right3, null)
        ];
        var full = control.Zip(treatment, (left, right) => new SafetyRemediationGatePairedOpportunity(
            left.OpportunityKey, left.OpportunityOrder, left.StateBefore, right.StateBefore,
            left.StateAfter, right.StateAfter, left.CommittedObjectIdentity,
            right.CommittedObjectIdentity, left.OpportunityKey != "O-1")).ToArray();
        var compact = SafetyRemediationGateHardeningResearch.Compact(control);
        var compactTreatment = SafetyRemediationGateHardeningResearch.Compact(treatment);
        var expanded = compact.Zip(compactTreatment, (left, right) => new SafetyRemediationGatePairedOpportunity(
            left.OpportunityKey, left.OpportunityOrder,
            SafetyRemediationGateHardeningResearch.LineageState(left.StateBeforeHash,
                left.StateBeforeComplete, left.OpportunityOrder),
            SafetyRemediationGateHardeningResearch.LineageState(right.StateBeforeHash,
                right.StateBeforeComplete, right.OpportunityOrder),
            SafetyRemediationGateHardeningResearch.LineageState(left.StateAfterHash,
                left.StateAfterComplete, left.OpportunityOrder + 1),
            SafetyRemediationGateHardeningResearch.LineageState(right.StateAfterHash,
                right.StateAfterComplete, right.OpportunityOrder + 1),
            left.CommittedObjectIdentity, right.CommittedObjectIdentity,
            left.OpportunityKey != "O-1")).ToArray();

        Assert.Equal(control.Select(x => x.CommittedObjectIdentity),
            compact.Select(x => x.CommittedObjectIdentity));
        var expected = SafetyRemediationGateHardeningResearch.Classify("pair", full);
        var actual = SafetyRemediationGateHardeningResearch.Classify("pair", expanded);
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
        Assert.True(actual[0].DirectGovernedDivergence);
        Assert.True(actual[1].ReconvergedAfter);
        Assert.True(actual[2].DirectGovernedDivergence);
    }

    [Fact]
    public void IncrementalRngTranscriptHashMatchesSimpleBufferedReferenceExactly()
    {
        var random = new SeededRandom(1234);
        var reference = new StringBuilder();
        using var incremental = new SafetyRemediationGateTranscriptHash();
        for (var index = 0; index < 1000; index++)
        {
            if (index % 3 == 0)
            {
                var value = random.Next(2 + index % 17);
                reference.Append("I:").Append(2 + index % 17).Append(':').Append(value).Append('\n');
                incremental.AppendInteger(2 + index % 17, value);
            }
            else
            {
                var value = random.NextDouble();
                reference.Append("D:").Append(value.ToString("R",
                    System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
                incremental.AppendDouble(value);
            }
        }
        var expected = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(reference.ToString())));
        Assert.Equal(expected, incremental.CurrentHash);
        Assert.Equal(expected, incremental.CurrentHash);
    }

    [Fact]
    public void SameHistoricalTapCommitLosesViolationWhenConflictingObjectDisappears()
    {
        var tap = ManiaObject.Tap(0, 1000, synthetic: true, sequence: 20);
        var blocker = ManiaObject.Ln(0, 500, 1000, synthetic: true, sequence: 19,
            origin: AddedObjectOrigin.HeadOpportunity);
        var control = GeometrySafetyAttributionResearch.AuditSnapshot("chart", 4,
        [
            GeometrySafetyAttributionResearch.Object("chart", GeometrySnapshotRole.Control,
                blocker, 1m, 2m, 0),
            GeometrySafetyAttributionResearch.Object("chart", GeometrySnapshotRole.Control,
                tap, 2m, 2m, 1)
        ]);
        var treatment = GeometrySafetyAttributionResearch.AuditSnapshot("chart", 4,
        [
            GeometrySafetyAttributionResearch.Object("chart", GeometrySnapshotRole.Treatment,
                tap, 2m, 2m, 0)
        ]);

        Assert.Contains(control.HardViolations,
            x => x.Violation == GeometryHardViolationKind.TapOnHeldLongNote);
        Assert.DoesNotContain(treatment.HardViolations,
            x => x.Violation == GeometryHardViolationKind.TapOnHeldLongNote);
    }

    [Fact]
    public void SameHistoricalTapCommitLosesViolationWhenPriorConflictingGeometryChangesLane()
    {
        var tap = ManiaObject.Tap(0, 1000, synthetic: true, sequence: 20);
        var controlBlocker = ManiaObject.Ln(0, 500, 1000, synthetic: true, sequence: 19,
            origin: AddedObjectOrigin.HeadOpportunity);
        var treatmentBlocker = controlBlocker with { Lane = 1 };
        GeometrySnapshotAudit Audit(GeometrySnapshotRole role, ManiaObject blocker) =>
            GeometrySafetyAttributionResearch.AuditSnapshot("chart", 4,
            [
                GeometrySafetyAttributionResearch.Object("chart", role, blocker, 1m, 2m, 0),
                GeometrySafetyAttributionResearch.Object("chart", role, tap, 2m, 2m, 1)
            ]);

        var control = Audit(GeometrySnapshotRole.Control, controlBlocker);
        var treatment = Audit(GeometrySnapshotRole.Treatment, treatmentBlocker);

        Assert.Contains(control.HardViolations,
            x => x.Violation == GeometryHardViolationKind.TapOnHeldLongNote);
        Assert.DoesNotContain(treatment.HardViolations,
            x => x.Violation == GeometryHardViolationKind.TapOnHeldLongNote);
        Assert.Equal(tap, tap with { });
    }

    private static SafetyRemediationGateSufficientState State(string identity, int cursor)
    {
        var materialized = new GenerationStateIdentity(identity, identity, cursor, cursor, cursor,
            "EMPTY", true);
        return SafetyRemediationGateHardeningResearch.State(materialized, "LATENT-" + identity);
    }

    private static SafetyRemediationCandidateGeometryDecision Candidate(
        IEnumerable<int> legacyLanes, IEnumerable<int> canonicalLanes, int? selectedLane) => new(
        "O-0", 0, 0, ManiaObjectType.Tap, 100, null, 1m, null, 1m, null,
        legacyLanes.ToImmutableArray(), canonicalLanes.ToImmutableArray(), "GEOMETRY", 0,
        selectedLane, false);

    private static SafetyRemediationGateSufficientState RecomputeWithoutCache(
        SafetyRemediationGateSufficientState value) =>
        SafetyRemediationGateHardeningResearch.State(new GenerationStateIdentity(
            "UNUSED-GEOMETRY", value.MaterializedGenerationStateHash, value.RootRngPosition,
            value.StageRngPosition, value.OpportunityCursor, value.PendingArticulationHash,
            value.CompleteForCausalLineage), value.LatentCommittedGeometryHash);

    private static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), source })
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ManiaAddNotesLab.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
