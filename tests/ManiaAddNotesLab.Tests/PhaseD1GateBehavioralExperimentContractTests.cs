using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using ManiaAddNotesLab.Core;

namespace ManiaAddNotesLab.Tests;

public sealed class PhaseD1GateBehavioralExperimentContractTests
{
    private static readonly CompletionMemberIdentity Tap = new(2, OriginalHeadMemberType.TapHead);

    [Theory]
    [InlineData(FutureD1EvidenceState.JointObservedUnique)]
    [InlineData(FutureD1EvidenceState.JointObservedAmongAlternatives)]
    public void JointObservedIsAdmittableWithoutUniquenessRequirement(FutureD1EvidenceState evidence)
    {
        Assert.Equal(FutureD1ExperimentalDisposition.ExperimentAdmittable, Evaluate(evidence).Disposition);
    }

    [Theory]
    [InlineData(FutureD1EvidenceState.MarginalOnly, FutureD1ExperimentalDisposition.ExperimentAbstainMarginalOnly)]
    [InlineData(FutureD1EvidenceState.NoComparableCompositionContext, FutureD1ExperimentalDisposition.ExperimentAbstainNoComparable)]
    [InlineData(FutureD1EvidenceState.NotMarginallySupported, FutureD1ExperimentalDisposition.ExperimentAbstainNotObserved)]
    public void NonJointEvidenceAbstainsWithoutClaimingMapperTruth(FutureD1EvidenceState evidence,
        FutureD1ExperimentalDisposition expected)
    {
        Assert.Equal(expected, Evaluate(evidence).Disposition);
        if (evidence == FutureD1EvidenceState.MarginalOnly)
            Assert.NotEqual(FutureD1ExperimentalDisposition.ExperimentAdmittable, Evaluate(evidence).Disposition);
    }

    [Fact]
    public void HardInvalidIsExcludedBeforeD1Eligibility()
    {
        var result = D1BehavioralExperimentContractResearch.Evaluate(new(false, false, 1, Tap,
            FutureD1EvidenceState.JointObservedUnique));
        Assert.Equal(FutureD1EvidenceState.HardInvalidExcluded, result.EvidenceState);
        Assert.Equal(FutureD1ExperimentalDisposition.HardInvalidExcluded, result.Disposition);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    public void OnlySecondAdditionIsInScope(int alreadyAdded, int prospective)
    {
        var result = D1BehavioralExperimentContractResearch.Evaluate(new(true, true, alreadyAdded, Tap,
            FutureD1EvidenceState.JointObservedUnique));
        Assert.Equal(prospective, result.ProspectiveCompletionSetCardinality);
        Assert.Equal(FutureD1ExperimentalDisposition.OutsideExperimentScope, result.Disposition);
    }

    [Fact]
    public void EligibleProspectiveK2HasExactCardinality()
    {
        Assert.Equal(2, Evaluate(FutureD1EvidenceState.JointObservedUnique).ProspectiveCompletionSetCardinality);
    }

    [Fact]
    public void GateConsumesNoRngAndPermitsNoRetry()
    {
        var result = Evaluate(FutureD1EvidenceState.MarginalOnly);
        Assert.Equal(0, result.GateRngCalls);
        Assert.False(result.RetryPermitted);
    }

    [Fact]
    public void CandidateIdentityDistinguishesTapAndLnHead()
    {
        Assert.Equal(OriginalHeadMemberType.TapHead,
            D1BehavioralExperimentContractResearch.CandidateIdentity(ManiaObject.Tap(1, 100)).HeadType);
        Assert.Equal(OriginalHeadMemberType.LongNoteHead,
            D1BehavioralExperimentContractResearch.CandidateIdentity(ManiaObject.Ln(1, 100, 500)).HeadType);
    }

    [Fact]
    public void LnReleaseDoesNotChangeCompletionMemberIdentity()
    {
        var shortLn = D1BehavioralExperimentContractResearch.CandidateIdentity(ManiaObject.Ln(1, 100, 200));
        var longLn = D1BehavioralExperimentContractResearch.CandidateIdentity(ManiaObject.Ln(1, 100, 900));
        Assert.Equal(shortLn, longLn);
        Assert.Equal(OriginalHeadMemberType.LongNoteHead, shortLn.HeadType);
        Assert.Contains("held tails are not completion members", Contract().CandidateIdentity);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(18)]
    public void ExactCandidateIdentitySupportsAllImplementedKeymodes(int keys)
    {
        var candidate = new CompletionMemberIdentity(keys - 1, OriginalHeadMemberType.TapHead);
        Assert.Equal(CompositionStructuralInvalidity.None,
            ResultingStateCompositionResearch.ValidateComposition(keys, [], [candidate]));
    }

    [Fact]
    public void FrozenContractUsesReducedOnlyWithoutFallback()
    {
        var contract = Contract();
        Assert.Equal("ReducedOnly", contract.EvidenceView);
        Assert.Contains("ViewFallback", contract.ProhibitedCoupledChanges);
        Assert.DoesNotContain(contract.AdmissionStates, x => x.Contains("Previous", StringComparison.Ordinal));
        Assert.Contains("Synthetic objects may never become donors", contract.OriginalEvidenceRule);
        Assert.Contains("Accepted synthetic heads describe current state only", contract.CurrentStateRule);
    }

    [Fact]
    public void DonorFrequencyCannotChangeDisposition()
    {
        var once = Evaluate(FutureD1EvidenceState.JointObservedUnique);
        var many = Evaluate(FutureD1EvidenceState.JointObservedUnique);
        Assert.Equal(once, many);
        Assert.DoesNotContain(Contract().AdmissionStates, x => x.Contains("Count", StringComparison.Ordinal));
        Assert.Contains("FrequencyThreshold", Contract().ProhibitedCoupledChanges);
        Assert.Contains("ScoreOrConfidence", Contract().ProhibitedCoupledChanges);
    }

    [Fact]
    public void ControlAndRollbackAreExactLegacy()
    {
        var contract = Contract();
        Assert.Equal(MapperEvidenceProfileBuilder.BehaviorPolicyVersion, contract.ControlPolicyVersion);
        Assert.Equal(contract.ControlPolicyVersion, contract.RollbackPolicy.DisabledBehavior);
        Assert.False(contract.RollbackPolicy.MigrationRequired);
        Assert.False(contract.RollbackPolicy.PersistentLearnedState);
        Assert.False(contract.RollbackPolicy.ChartRewriteRequired);
    }

    [Fact]
    public void ProposedVersionIsDistinctButInactive()
    {
        var contract = Contract();
        Assert.Equal("d1-resulting-state-ab.1", contract.ProposedTreatmentPolicyVersion);
        Assert.NotEqual(contract.ControlPolicyVersion, contract.ProposedTreatmentPolicyVersion);
        Assert.False(contract.BehaviorChangeCurrent);
        Assert.True(contract.FutureBehaviorChange);
        Assert.Equal("NOT_AUTHORIZED", contract.FutureD1Authorization);
    }

    [Fact]
    public void EngineInsertionAuditHasAllRequiredStateBeforeMutation()
    {
        var audit = Contract().InsertionPoint;
        Assert.Equal("AddNotesEngine.Apply", audit.Method);
        Assert.True(audit.CandidateIdentityAvailable);
        Assert.True(audit.CurrentOriginalBaseStateAvailable);
        Assert.True(audit.AlreadyAddedSetAvailable);
        Assert.True(audit.HardValidityKnown);
        Assert.True(audit.OutputNotYetMutated);
        Assert.Equal(0, audit.GateRngCalls);
    }

    [Fact]
    public void ContractHashIsFrozen()
    {
        Assert.Equal(D1BehavioralExperimentContractResearch.FrozenContractContentHash,
            D1BehavioralExperimentContractResearch.ComputeContentHash(Contract()));
    }

    [Fact]
    public void SetLikeArrayOrderDoesNotChangeContractHash()
    {
        var contract = Contract();
        var reordered = contract with
        {
            AdmissionStates = contract.AdmissionStates.Reverse().ToImmutableArray(),
            DirectMetrics = contract.DirectMetrics.Reverse().ToImmutableArray()
        };
        Assert.Equal(D1BehavioralExperimentContractResearch.ComputeContentHash(contract),
            D1BehavioralExperimentContractResearch.ComputeContentHash(reordered));
        var left = CompletionSetIdentity.Create([
            new(4, OriginalHeadMemberType.LongNoteHead), new(1, OriginalHeadMemberType.TapHead)]);
        var right = CompletionSetIdentity.Create([
            new(1, OriginalHeadMemberType.TapHead), new(4, OriginalHeadMemberType.LongNoteHead)]);
        Assert.Equal(left, right);
    }

    public static IEnumerable<object[]> ImmutableFields()
    {
        var contract = Contract();
        yield return [contract with { EvidenceView = "ReducedHeld" }];
        yield return [contract with { ScopeCardinality = 3 }];
        yield return [contract with { AdmissionStates = [.. contract.AdmissionStates, "MarginalOnly"] }];
        yield return [contract with { RngContract = contract.RngContract with { GateRngCalls = 1 } }];
        yield return [contract with { ControlPolicyVersion = "other-control" }];
        yield return [contract with { ProposedTreatmentPolicyVersion = "other-treatment" }];
        yield return [contract with { RollbackPolicy = contract.RollbackPolicy with { DisabledBehavior = "other" } }];
    }

    [Theory]
    [MemberData(nameof(ImmutableFields))]
    public void ContractCriticalFieldChangesRequireNewHash(D1BehavioralExperimentContract changed)
    {
        Assert.NotEqual(D1BehavioralExperimentContractResearch.FrozenContractContentHash,
            D1BehavioralExperimentContractResearch.ComputeContentHash(changed));
    }

    [Fact]
    public void ResearchContractHasNoProductionCallsite()
    {
        var root = FindRepositoryRoot();
        foreach (var relative in new[]
                 {
                     "src/ManiaAddNotesLab.Core/AddNotesEngine.cs",
                     "src/ManiaAddNotesLab.Core/Model.cs",
                     "src/ManiaAddNotesLab.Cli/Program.cs",
                     "src/ManiaAddNotesLab.Web/Program.cs"
                 })
            Assert.DoesNotContain("D1BehavioralExperimentContractResearch",
                File.ReadAllText(Path.Combine(root, relative)), StringComparison.Ordinal);
    }

    [Fact]
    public void FrozenD10FactsRemainAvailableWithoutHumanRetuning()
    {
        var root = FindRepositoryRoot();
        Assert.Contains("ReducedOnly,42048,40025,37112,2913",
            File.ReadAllText(Path.Combine(root, "docs", "d1_0_marginal_joint_summary.csv")));
        Assert.Contains("ReducedOnly,1387156,1283958,103198,909438,374520",
            File.ReadAllText(Path.Combine(root, "docs", "d1_0_composition_trap_summary.csv")));
        Assert.Contains("GLOBAL,42048,0,canonical_lane_then_type",
            File.ReadAllText(Path.Combine(root, "docs", "d1_0_order_invariance_summary.csv")));
    }

    private static FutureD1DispositionResult Evaluate(FutureD1EvidenceState evidence) =>
        D1BehavioralExperimentContractResearch.Evaluate(new(true, true, 1, Tap, evidence));

    private static D1BehavioralExperimentContract Contract() =>
        D1BehavioralExperimentContractResearch.CreateFrozenContract();

    private static string FindRepositoryRoot([CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), sourceFile })
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ManiaAddNotesLab.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }
}
