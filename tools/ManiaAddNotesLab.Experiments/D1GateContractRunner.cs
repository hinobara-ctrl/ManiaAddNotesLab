using System.Globalization;
using System.Text;
using ManiaAddNotesLab.Core;

internal static class D1GateContractRunner
{
    public static void Run(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var contract = D1BehavioralExperimentContractResearch.CreateFrozenContract();
        Write(Path.Combine(outputDirectory, "d1_gate_behavioral_experiment_contract.json"),
            D1BehavioralExperimentContractResearch.SerializeArtifact(contract) + Environment.NewLine);
        WriteValidation(Path.Combine(outputDirectory, "d1_gate_contract_validation_summary.csv"), contract);
        WriteInsertionAudit(Path.Combine(outputDirectory, "d1_gate_engine_insertion_audit.csv"));
        WriteScopeAudit(Path.Combine(outputDirectory, "d1_gate_scope_audit.csv"));
        WriteD10Reproduction(Path.Combine(outputDirectory, "d1_gate_d1_0_reproduction.csv"), outputDirectory);
        Console.WriteLine($"D1.GATE contract {D1BehavioralExperimentContractResearch.ComputeContentHash(contract)}");
    }

    private static void WriteValidation(string path, D1BehavioralExperimentContract contract)
    {
        var tap = new CompletionMemberIdentity(2, OriginalHeadMemberType.TapHead);
        var rows = new List<(string Case, bool Passed, string Measured)>
        {
            ("joint_unique_admittable", Disposition(FutureD1EvidenceState.JointObservedUnique) ==
                FutureD1ExperimentalDisposition.ExperimentAdmittable, "ExperimentAdmittable"),
            ("joint_alternative_admittable", Disposition(FutureD1EvidenceState.JointObservedAmongAlternatives) ==
                FutureD1ExperimentalDisposition.ExperimentAdmittable, "ExperimentAdmittable"),
            ("marginal_only_abstains", Disposition(FutureD1EvidenceState.MarginalOnly) ==
                FutureD1ExperimentalDisposition.ExperimentAbstainMarginalOnly, "ExperimentAbstainMarginalOnly"),
            ("no_comparable_abstains", Disposition(FutureD1EvidenceState.NoComparableCompositionContext) ==
                FutureD1ExperimentalDisposition.ExperimentAbstainNoComparable, "ExperimentAbstainNoComparable"),
            ("not_observed_abstains", Disposition(FutureD1EvidenceState.NotMarginallySupported) ==
                FutureD1ExperimentalDisposition.ExperimentAbstainNotObserved, "ExperimentAbstainNotObserved"),
            ("hard_invalid_excluded", D1BehavioralExperimentContractResearch.Evaluate(new(false, false, 1, tap,
                FutureD1EvidenceState.JointObservedUnique)).Disposition ==
                FutureD1ExperimentalDisposition.HardInvalidExcluded, "HardInvalidExcluded"),
            ("k1_outside_scope", D1BehavioralExperimentContractResearch.Evaluate(new(true, true, 0, tap,
                FutureD1EvidenceState.JointObservedUnique)).Disposition ==
                FutureD1ExperimentalDisposition.OutsideExperimentScope, "OutsideExperimentScope"),
            ("k3_outside_scope", D1BehavioralExperimentContractResearch.Evaluate(new(true, true, 2, tap,
                FutureD1EvidenceState.JointObservedUnique)).Disposition ==
                FutureD1ExperimentalDisposition.OutsideExperimentScope, "OutsideExperimentScope"),
            ("gate_rng_zero", DispositionResult(FutureD1EvidenceState.JointObservedUnique).GateRngCalls == 0, "0"),
            ("no_retry", !DispositionResult(FutureD1EvidenceState.MarginalOnly).RetryPermitted, "false"),
            ("donor_frequency_non_authoritative", !contract.AdmissionStates.Any(x => x.Contains("Count", StringComparison.Ordinal)), "no threshold"),
            ("reduced_only", contract.EvidenceView == "ReducedOnly", contract.EvidenceView),
            ("no_fallback_view", contract.ProhibitedCoupledChanges.Contains("ViewFallback"), "ViewFallback prohibited"),
            ("control_legacy", contract.ControlPolicyVersion == "legacy-experimental.1", contract.ControlPolicyVersion),
            ("treatment_inactive", contract.ProposedTreatmentPolicyVersion != contract.ControlPolicyVersion
                && !contract.BehaviorChangeCurrent && contract.FutureD1Authorization == "NOT_AUTHORIZED",
                contract.ProposedTreatmentPolicyVersion),
            ("rollback_legacy", contract.RollbackPolicy.DisabledBehavior == contract.ControlPolicyVersion,
                contract.RollbackPolicy.DisabledBehavior),
            ("contract_hash_frozen", D1BehavioralExperimentContractResearch.ComputeContentHash(contract) ==
                D1BehavioralExperimentContractResearch.FrozenContractContentHash,
                D1BehavioralExperimentContractResearch.ComputeContentHash(contract))
        };
        Write(path, "case,passed,measured\n" + string.Join('\n', rows.Select(x =>
            $"{Q(x.Case)},{x.Passed.ToString().ToLowerInvariant()},{Q(x.Measured)}")) + "\n");
        if (rows.Any(x => !x.Passed)) throw new InvalidOperationException("D1.GATE contract validation failed.");

        FutureD1ExperimentalDisposition Disposition(FutureD1EvidenceState state) => DispositionResult(state).Disposition;
        FutureD1DispositionResult DispositionResult(FutureD1EvidenceState state) =>
            D1BehavioralExperimentContractResearch.Evaluate(new(true, true, 1, tap, state));
    }

    private static void WriteInsertionAudit(string path)
    {
        var value = D1BehavioralExperimentContractResearch.EngineInsertionAudit();
        var rows = new[]
        {
            ("method", value.Method), ("semantic_anchor", value.SemanticAnchor),
            ("before_operation", value.BeforeOperation), ("after_operation", value.AfterOperation),
            ("candidate_identity_available", B(value.CandidateIdentityAvailable)),
            ("current_original_base_state_available", B(value.CurrentOriginalBaseStateAvailable)),
            ("already_added_set_available", B(value.AlreadyAddedSetAvailable)),
            ("hard_validity_known", B(value.HardValidityKnown)),
            ("output_not_yet_mutated", B(value.OutputNotYetMutated)),
            ("gate_rng_calls", value.GateRngCalls.ToString(CultureInfo.InvariantCulture)),
            ("rng_already_consumed", value.RngAlreadyConsumed), ("diagnostic_caveat", value.DiagnosticCaveat)
        };
        Write(path, "audit,value\n" + string.Join('\n', rows.Select(x => $"{Q(x.Item1)},{Q(x.Item2)}")) + "\n");
    }

    private static void WriteScopeAudit(string path)
    {
        var rows = new[]
        {
            ("base_opportunities", "one per original head; simultaneous original heads create sequential proposals"),
            ("interior_opportunities", "zero to two per eligible original LN; anchors can share timestamps with other opportunities"),
            ("synthetic_recursion", "none; opportunity list is frozen before placement"),
            ("current_geometry", "original objects plus previously accepted pass-1 objects"),
            ("maximum_added_heads_at_timestamp", "min(free lanes under current geometry, otherwise successful legal opportunities at the timestamp)"),
            ("legacy_timestamp_cap", "none beyond physical lanes and available opportunities"),
            ("k3_structurally_possible", "true; e.g. three simultaneous original heads in 7K provide three base opportunities and four initially free lanes"),
            ("human_k3_frequency_audited", "false; not required for contract coherence and prohibited as opportunistic style tuning"),
            ("d1_v1_transition", "already-added cardinality 1 to prospective cardinality 2 only"),
            ("k0_to_k1", "legacy unchanged"),
            ("k2_to_k3_or_higher", "legacy unchanged and outside D1 v1 scope"),
            ("hidden_cap", "none"),
            ("later_opportunity_after_abstention", "allowed only as the next pre-existing OpportunityKey; never a D1-triggered reroll"),
            ("interpretability", "direct gate decisions remain attributable; later geometry/output/RNG changes are downstream consequences"),
            ("future_hold_signal", "if k3 interactions dominate the paired A/B, classify future D1 Outcome B; do not retrofit a cap")
        };
        Write(path, "audit,value\n" + string.Join('\n', rows.Select(x => $"{Q(x.Item1)},{Q(x.Item2)}")) + "\n");
    }

    private static void WriteD10Reproduction(string path, string docsDirectory)
    {
        var checks = new[]
        {
            ("pair_targets", "d1_0_target_reconstruction_summary.csv", "ReducedOnly", "eligible", "42048"),
            ("target_marginal_only", "d1_0_marginal_joint_summary.csv", "ReducedOnly", "target_marginal_only", "2913"),
            ("hypothetical_marginal_only", "d1_0_composition_trap_summary.csv", "ReducedOnly", "marginal_only_pairs", "374520"),
            ("target_whole_group_leakage", "d1_0_leakage_summary.csv", "target_whole_group_leakage", "correct_count", "0"),
            ("joint_cross_occurrence_leakage", "d1_0_leakage_summary.csv", "joint_witness_cross_occurrence_leakage", "correct_count", "0"),
            ("order_invariance_violations", "d1_0_order_invariance_summary.csv", "GLOBAL", "order_invariance_violations", "0")
        };
        var output = new StringBuilder("metric,expected,observed,matches\n");
        foreach (var check in checks)
        {
            var csv = ReadCsv(Path.Combine(docsDirectory, check.Item2));
            var row = csv.Rows.Single(x => x[0] == check.Item3);
            var index = Array.IndexOf(csv.Header, check.Item4);
            var observed = row[index];
            output.Append(Q(check.Item1)).Append(',').Append(Q(check.Item5)).Append(',').Append(Q(observed))
                .Append(',').AppendLine(B(observed == check.Item5));
        }
        Write(path, output.ToString());
    }

    private static (string[] Header, List<string[]> Rows) ReadCsv(string path)
    {
        var lines = File.ReadAllLines(path);
        return (lines[0].Split(','), lines.Skip(1).Where(x => x.Length > 0).Select(x => x.Split(',')).ToList());
    }

    private static void Write(string path, string value) => File.WriteAllText(path, value, new UTF8Encoding(false));
    private static string Q(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private static string B(bool value) => value.ToString().ToLowerInvariant();
}
