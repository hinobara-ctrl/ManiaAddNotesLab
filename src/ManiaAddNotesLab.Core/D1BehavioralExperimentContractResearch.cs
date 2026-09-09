using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManiaAddNotesLab.Core;

public enum FutureD1EvidenceState
{
    OutsideScope,
    HardInvalidExcluded,
    NoComparableCompositionContext,
    MarginalOnly,
    NotMarginallySupported,
    JointObservedUnique,
    JointObservedAmongAlternatives
}

public enum FutureD1ExperimentalDisposition
{
    OutsideExperimentScope,
    HardInvalidExcluded,
    ExperimentAbstainNoComparable,
    ExperimentAbstainMarginalOnly,
    ExperimentAbstainNotObserved,
    ExperimentAdmittable
}

public sealed record FutureD1ProposalSnapshot(
    bool HardValidityKnown,
    bool HardValidityPassed,
    int AlreadyAddedCompletionMemberCount,
    CompletionMemberIdentity ProspectiveCandidate,
    FutureD1EvidenceState EvidenceState)
{
    public int ProspectiveCompletionSetCardinality => AlreadyAddedCompletionMemberCount + 1;
}

public sealed record FutureD1DispositionResult(
    FutureD1EvidenceState EvidenceState,
    FutureD1ExperimentalDisposition Disposition,
    int ProspectiveCompletionSetCardinality,
    int GateRngCalls,
    bool RetryPermitted);

public sealed record D1GateRngContract(
    int GateRngCalls,
    bool IdenticalTranscriptBeforeFirstBehavioralDivergence,
    bool DownstreamDivergenceAllowedAfterRejection,
    ImmutableArray<string> RequiredInstrumentation);

public sealed record D1GateInsertionPoint(
    string Method,
    string SemanticAnchor,
    string BeforeOperation,
    string AfterOperation,
    bool CandidateIdentityAvailable,
    bool CurrentOriginalBaseStateAvailable,
    bool AlreadyAddedSetAvailable,
    bool HardValidityKnown,
    bool OutputNotYetMutated,
    int GateRngCalls,
    string RngAlreadyConsumed,
    string DiagnosticCaveat);

public sealed record D1GateRollbackPolicy(
    string DisabledBehavior,
    bool MigrationRequired,
    bool PersistentLearnedState,
    bool ChartRewriteRequired);

public sealed record D1BehavioralExperimentContract(
    string SchemaVersion,
    string GatePhase,
    string GateDecision,
    string BaselineCommit,
    string ControlPolicyVersion,
    string ProposedTreatmentPolicyVersion,
    bool BehaviorChangeCurrent,
    bool FutureBehaviorChange,
    string FutureD1Authorization,
    string BehavioralQuestion,
    string SingleHypothesis,
    string EvidenceView,
    int ScopeCardinality,
    string EligibilityRule,
    ImmutableArray<string> EligibleOpportunityKinds,
    ImmutableArray<string> ExcludedOpportunityKinds,
    ImmutableArray<string> EvidenceStates,
    ImmutableArray<string> AdmissionStates,
    ImmutableArray<string> AbstentionStates,
    ImmutableArray<string> OutsideScopeStates,
    string HardInvalidHandling,
    string NoRerollRule,
    string OriginalEvidenceRule,
    string CurrentStateRule,
    string CandidateIdentity,
    string SelectedRangeSemantics,
    string AddChanceSemantics,
    string SeedProtocol,
    D1GateRngContract RngContract,
    D1GateInsertionPoint InsertionPoint,
    ImmutableArray<string> Denominators,
    string PrimaryDenominator,
    ImmutableArray<string> DirectMetrics,
    ImmutableArray<string> OutputMetrics,
    ImmutableArray<string> Strata,
    ImmutableArray<string> NonDegeneracyCriteria,
    ImmutableArray<string> StoppingCriteria,
    ImmutableArray<string> HoldCriteria,
    ImmutableArray<string> FutureD1Outcomes,
    ImmutableArray<string> ProhibitedCoupledChanges,
    D1GateRollbackPolicy RollbackPolicy);

public sealed record D1BehavioralExperimentContractArtifact(
    D1BehavioralExperimentContract Contract,
    string ContractContentHash,
    string Canonicalization);

/// <summary>
/// D1.GATE design-only contract. This type is not referenced by AddNotesEngine, CLI, Web, options, or defaults.
/// It evaluates hypothetical future dispositions and consumes no RNG.
/// </summary>
public static class D1BehavioralExperimentContractResearch
{
    public const string SchemaVersion = "phase-d1-gate-behavioral-experiment-contract.1";
    public const string BaselineCommit = "77638edad9b41c8d6e3a361ce68c296ed12c90bc";
    public const string ControlPolicyVersion = "legacy-experimental.1";
    public const string ProposedTreatmentPolicyVersion = "d1-resulting-state-ab.1";
    public const string FrozenContractContentHash = "D574631B3E713AC3D08C159B605A742D50C907B1BB4589D7FCADCFA8027A7824";

    public static D1BehavioralExperimentContract CreateFrozenContract() => Canonicalize(new(
        SchemaVersion,
        "D1.GATE",
        "READY",
        BaselineCommit,
        ControlPolicyVersion,
        ProposedTreatmentPolicyVersion,
        false,
        true,
        "NOT_AUTHORIZED",
        "Can one bounded second-addition admission experiment isolate exact same-occurrence joint evidence?",
        "For one otherwise legacy-valid proposal that would change the added set from cardinality 1 to 2, require an exact ReducedOnly same-occurrence joint witness; otherwise abstain without reroll.",
        "ReducedOnly",
        2,
        "Hard validity passed; exactly one previously accepted pass-1 completion member exists at the timestamp; the already-selected legacy proposal would make the exact added CompletionSet cardinality two.",
        ["BaseHead", "LnInterior"],
        ["ArticulationReplacement", "AddedSetCardinality0To1", "AddedSetCardinality2OrMoreTo3OrMore"],
        ["HardInvalidExcluded", "JointObservedAmongAlternatives", "JointObservedUnique", "MarginalOnly", "NoComparableCompositionContext", "NotMarginallySupported", "OutsideScope"],
        ["JointObservedAmongAlternatives", "JointObservedUnique"],
        ["MarginalOnly", "NoComparableCompositionContext", "NotMarginallySupported"],
        ["AddedSetCardinality0To1", "AddedSetCardinality2OrMoreTo3OrMore", "ArticulationReplacement"],
        "Existing geometry and format validity run first. Hard-invalid proposals are excluded from D1EligibleDecisions and are never D1 abstentions.",
        "One legacy proposal produces one disposition. Abstention advances to the next pre-existing opportunity with no lane, shape, candidate, chance, or RNG retry caused by D1.",
        "The immutable index is built only from OriginalObjects. Synthetic objects may never become donors, witnesses, frequency, context, or support.",
        "OriginalBaseState, AlreadyAddedCompletionMembers, ProspectiveCandidate, ProspectiveCompletionSet, ProspectiveResultingState, and OriginalEvidenceIndex remain separate. Accepted synthetic heads describe current state only.",
        "lane + TapHead|LongNoteHead; releases and held tails are not completion members",
        "The inclusive range filters opportunity source heads only. Evidence remains full-chart original-only and may come from outside the selected range.",
        "The existing Bernoulli/chance path runs before D1 eligibility. D1 neither changes chance nor compensates for abstentions elsewhere.",
        "No population seed claim is frozen. Future D1 must declare an explicit deterministic paired seed list before running; fixture seeds are implementation coverage only.",
        new(0, true, true,
            ["DirectD1Decision", "DownstreamRngDivergence", "FirstBehavioralDivergence", "RngCallsBeforeGate", "RngCallsByGate"]),
        EngineInsertionAudit(),
        ["AllLegacyOpportunities", "OtherwiseLegalCandidateProposals", "D1EligibleDecisions", "TimestampsWithD1EligibleDecision", "ChartsWithD1EligibleDecision", "IndependentFamiliesAffected"],
        "D1EligibleDecisions",
        ["D1EligibleDecisions", "DirectGateAdmissions", "DirectGateRejections", "JointAlternativeAdmissions", "JointObservedAdmissions", "JointUniqueAdmissions", "MarginalOnlyAbstentions", "NoComparableAbstentions", "NotObservedAbstentions"],
        ["AddedHeadCount", "AddedHeadsPerTimestampDistribution", "AddedLNCount", "AddedObjectCount", "AffectedTimestamps", "DownstreamOutputDifferences", "FinalOutputHash", "HardValidityFailures", "OriginalObjectCount", "OverlapFailures", "ReparseSuccess", "TimestampsDirectlyChangedByD1"],
        ["FamilyMacro", "Keymode4K", "Keymode7K", "Keymode10K", "LongNoteLongNote", "Micro", "PerChart", "RiceDenseTapSentinel", "Synthetic1K", "Synthetic18K", "TapLongNote", "TapTap"],
        ["AtLeastOneDirectAdmission", "AtLeastOneDirectAbstention", "MoreThanOneIndependentFamilyAffectedForCrossFamilyClaim", "NoAffectedChartLosesEveryAddition", "ReportChartsWithNoEligibleDecisionSeparately"],
        ["AddedObjectsEnterEvidence", "ControlDiffersFromLegacy", "CrossChartEvidence", "DeterministicRerunFailure", "DirectDecisionLacksExactProvenance", "GateConsumesRng", "GateRunsOutsideK2Scope", "HardInvariantFailure", "JointWitnessUsesMultipleOccurrences", "NoOverlapFailure", "NonD1OptionOrDefaultChanged", "PolicyVersionMismatch", "SourceOverwrite", "TreatmentChangesBehaviorWhenDisabled", "WriterOrReparseFailure"],
        ["AdmissionRequiresFrequencyOrScore", "CandidateUnavailableBeforeMutation", "DirectAndDownstreamEffectsCannotBeSeparated", "EvidenceRequiresMultipleViews", "GateRequiresExtraRng", "HardValidityCannotBeSeparated", "K3MakesK2Uninterpretable", "NoCleanInsertionPoint", "RerollRequired", "RollbackNotByteExact", "SyntheticStateWouldTeachEvidence"],
        ["A: exact preregistration, deterministic and safe, nonzero admissions and abstentions across multiple families, attributable effects, noncollapsed output, exact rollback; continue technical evaluation/playtesting only", "B: valid and interpretable but restricted, abstention-heavy, k3/downstream dominated, or one narrow research dependency remains; no promotion", "C: degenerate, unsafe, unattributable, or unable to isolate joint evidence; rollback and park/redesign"],
        ["AddChanceSemantics", "Articulation", "CandidateSelection", "Chance", "ContextWindows", "Density", "FrequencyThreshold", "Gap", "K3Handling", "LaneSelection", "LnWeighting", "OpportunityOrdering", "Rng", "ScoreOrConfidence", "ShapeSelection", "ViewFallback"],
        new(ControlPolicyVersion, false, false, false)));

    public static D1GateInsertionPoint EngineInsertionAudit() => new(
        "AddNotesEngine.Apply",
        "after PlaceTap/PlaceLongNote returns a non-null TimedManiaObject and before added.Add/geometry.Insert",
        "added.Add(placed.Object), geometry.Insert(placed), placement statistics, and output mutation",
        "chance roll, exact tap lane or LN shape/lane selection, and existing legal-geometry validation",
        true, true, true, true, true, 0,
        "The chance roll and proposal-specific lane/shape RNG have already been consumed by legacy.",
        "Legacy diagnostics currently label the proposal Placed inside PlaceTap/PlaceLongNote; future D1 must record a separate direct disposition or revise that label atomically before mutation.");

    public static CompletionMemberIdentity CandidateIdentity(ManiaObject candidate) => new(candidate.Lane,
        candidate.Type == ManiaObjectType.Tap ? OriginalHeadMemberType.TapHead : OriginalHeadMemberType.LongNoteHead);

    public static FutureD1DispositionResult Evaluate(FutureD1ProposalSnapshot proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (!proposal.HardValidityKnown || !proposal.HardValidityPassed)
            return Result(FutureD1EvidenceState.HardInvalidExcluded,
                FutureD1ExperimentalDisposition.HardInvalidExcluded, proposal.ProspectiveCompletionSetCardinality);
        if (proposal.AlreadyAddedCompletionMemberCount != 1)
            return Result(FutureD1EvidenceState.OutsideScope,
                FutureD1ExperimentalDisposition.OutsideExperimentScope, proposal.ProspectiveCompletionSetCardinality);
        return proposal.EvidenceState switch
        {
            FutureD1EvidenceState.JointObservedUnique or
                FutureD1EvidenceState.JointObservedAmongAlternatives =>
                Result(proposal.EvidenceState, FutureD1ExperimentalDisposition.ExperimentAdmittable, 2),
            FutureD1EvidenceState.MarginalOnly =>
                Result(proposal.EvidenceState, FutureD1ExperimentalDisposition.ExperimentAbstainMarginalOnly, 2),
            FutureD1EvidenceState.NoComparableCompositionContext =>
                Result(proposal.EvidenceState, FutureD1ExperimentalDisposition.ExperimentAbstainNoComparable, 2),
            FutureD1EvidenceState.NotMarginallySupported =>
                Result(proposal.EvidenceState, FutureD1ExperimentalDisposition.ExperimentAbstainNotObserved, 2),
            _ => throw new ArgumentException("An eligible k=2 proposal requires an exact prospective evidence state.",
                nameof(proposal))
        };
    }

    public static string ComputeContentHash(D1BehavioralExperimentContract contract)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(Canonicalize(contract), CanonicalJsonOptions);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public static string SerializeArtifact(D1BehavioralExperimentContract contract)
    {
        var canonical = Canonicalize(contract);
        return JsonSerializer.Serialize(new D1BehavioralExperimentContractArtifact(canonical,
            ComputeContentHash(canonical),
            "UTF-8 System.Text.Json camelCase; no indentation for hash; record property order; all set-like arrays sorted ordinal; no timestamps or paths"), ArtifactJsonOptions);
    }

    public static D1BehavioralExperimentContract Canonicalize(D1BehavioralExperimentContract value) => value with
    {
        EligibleOpportunityKinds = Sort(value.EligibleOpportunityKinds),
        ExcludedOpportunityKinds = Sort(value.ExcludedOpportunityKinds),
        EvidenceStates = Sort(value.EvidenceStates),
        AdmissionStates = Sort(value.AdmissionStates),
        AbstentionStates = Sort(value.AbstentionStates),
        OutsideScopeStates = Sort(value.OutsideScopeStates),
        Denominators = Sort(value.Denominators),
        DirectMetrics = Sort(value.DirectMetrics),
        OutputMetrics = Sort(value.OutputMetrics),
        Strata = Sort(value.Strata),
        NonDegeneracyCriteria = Sort(value.NonDegeneracyCriteria),
        StoppingCriteria = Sort(value.StoppingCriteria),
        HoldCriteria = Sort(value.HoldCriteria),
        FutureD1Outcomes = Sort(value.FutureD1Outcomes),
        ProhibitedCoupledChanges = Sort(value.ProhibitedCoupledChanges),
        RngContract = value.RngContract with
        {
            RequiredInstrumentation = Sort(value.RngContract.RequiredInstrumentation)
        }
    };

    private static FutureD1DispositionResult Result(FutureD1EvidenceState evidence,
        FutureD1ExperimentalDisposition disposition, int cardinality) => new(evidence, disposition, cardinality, 0,
        false);

    private static ImmutableArray<string> Sort(IEnumerable<string> values) =>
        values.Order(StringComparer.Ordinal).ToImmutableArray();

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions ArtifactJsonOptions = new(CanonicalJsonOptions)
    {
        WriteIndented = true
    };
}
