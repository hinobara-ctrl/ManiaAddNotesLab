# Phase D1.0 — Resulting-State Composition Feasibility / Shadow

**Status:** COMPLETE  
**Outcome:** A  
**behaviorChange:** false  
**Human corpus:** RUN, only after the synthetic/D0/corpus gates passed

The two-stage task entered at commit `f1d834bc08edb1ead2854fda5261c69e45e9bfc7`, with E.1 `COMPLETE/B`, exact recurrence `PARKED`, F2 `CONTINUE_CONDITIONALLY`, F2.ACQ `BLOCKED`, C2 `DEFERRED`, and D1 semantically contradictory across living documentation. Stage A resolved the contradiction and was committed/pushed as `30d430397f6f36de88ca18b864f2bb59c9e18eb1`; that clean, remotely verified commit is the frozen D1.0 baseline.

## 1. Decision

**COMPLETE — OUTCOME A.** The exact original chart can represent and audit a jointly observed two-member completion separately from members that are only supported marginally. This is a feasibility result for research representation; it is **not** a selection rule and does **not** authorize behavioral Phase D1.

The frozen question was answered without inspecting generation outcomes: given one original simultaneous head group, remove exactly two members, reconstruct its exact reduced state, and ask whether the order-independent target `CompletionSet` was observed together in another single original occurrence. The answer is distinct from asking whether each member appeared somewhere among different donors.

## 2. Frozen design and sequence

The pre-human certificate is [PHASE_D1_0_PRE_HUMAN_DESIGN.md](PHASE_D1_0_PRE_HUMAN_DESIGN.md). It was written before human results and freezes:

- baseline commit `30d430397f6f36de88ca18b864f2bb59c9e18eb1`;
- research schema `phase-d1-0-resulting-state-composition-shadow.1`;
- D0 leave-one-member-out as the `k=1` semantic control;
- exhaustive unordered pair holdout as the primary `k=2` experiment;
- `k>=3` as deferred rather than silently extrapolated;
- canonical lane-plus-head-type identities and order-independent completion sets;
- whole-source-group holdout, target-observation exclusion and symmetric future-held hygiene;
- the eight D0.1 context views as parallel observations, never as a ladder;
- hard invalidity before evidence classification;
- the target and hypothetical taxonomies, leakage gates and Outcomes A/B/C.

Execution order was enforced by the runner: synthetic gate, D0 semantic reproduction, frozen C11 identity, and only then the human D1.0 corpus. A failed prerequisite aborts before human results are produced.

## 3. Corpus and denominators

The corpus remained exactly C11: **11 human charts, 11 independent families, 4K/7K/10K, 50,836 original objects**. No generated object teaches the model. D0 reproduction retained **14,463 chord groups and 40,360 k=1 trials**.

D1.0 enumerated **42,048 exhaustive pair-holdout targets** and performed **2,909,679 pair operations**. These are occurrence-level targets, not independent charts; therefore micro totals describe workload and prevalence, while the 11-family macro view is the independence check. No confidence interval is claimed from pseudo-replicating occurrences.

## 4. Exact representation

`CompletionMemberIdentity` is exact `(lane, TapHead|LongNoteHead)`. `CompletionSetIdentity` rejects duplicates, sorts canonically and compares independently of construction order. `ResultingHeadStateIdentity` is the exact union of the reduced state and candidate completion set. Tap and LN heads are never collapsed; held tails remain context and never become heads.

An `ObservedJointCompletionSet` is valid only when all target members occur together in the **same eligible original source occurrence** under the selected exact view. Intersecting two member donor lists cannot create a joint witness. Provenance retains chart fingerprint, source group, observation IDs and view-local audit fields.

## 5. Target reconstruction

For the broad `ReducedOnly` view:

| Measure | Count | Rate |
|---|---:|---:|
| Pair targets | 42,048 | 100% |
| Comparable exact context | 41,474 | 98.635% |
| Target set jointly observed | 37,112 | 88.261% of all; 89.483% of comparable |
| Joint unique | 484 | 1.167% of comparable |
| Joint among alternatives | 36,628 | 88.316% of comparable |
| Target absent despite comparable context | 4,362 | 10.518% of comparable |
| No comparable context | 574 | 1.365% of all |

Family-macro coverage was **98.746%** and family-macro joint support among comparable targets was **89.981%**. Joint target support occurred in all 11 families and every represented keymode. The representation is therefore not supported by a single large chart.

## 6. Marginal support is not joint support

In `ReducedOnly`, both target members were individually supported for 40,025 targets. For **2,913** of those targets, the complete pair was never observed together in a valid donor occurrence. This target-level marginal-only gap is **6.928% of all targets** and **7.278% of targets with both members marginally supported**. It appears in 10 of 11 families; the family-macro rate is **6.368%**.

The exhaustive hypothetical audit makes the distinction larger and more falsifiable. Of 1,387,156 marginal pair candidates, 103,198 were structurally invalid and were removed before consulting style evidence. Among 1,283,958 eligible pairs:

- 909,438 (70.831%) were jointly observed;
- **374,520 (29.169%) were marginal-only**.

Thus a future policy that composes individually supported members would admit a material class of resulting states for which the exact original chart supplies no same-occurrence witness. D1.0 does not say those states are musically wrong; it proves that marginal evidence cannot honestly be labeled joint evidence.

## 7. Context views

All eight frozen views were evaluated in parallel:

| View | Comparable | Target joint | Target marginal-only | No context |
|---|---:|---:|---:|---:|
| ReducedOnly | 41,474 | 37,112 | 2,913 | 574 |
| ReducedHeld | 38,382 | 31,986 | 1,972 | 3,666 |
| ReducedPrevious | 31,741 | 21,684 | 3,280 | 10,307 |
| ReducedPreviousTransition | 22,727 | 14,729 | 1,907 | 19,321 |
| ReducedNext | 31,828 | 21,759 | 3,257 | 10,220 |
| ReducedNextTransition | 22,837 | 14,624 | 2,081 | 19,211 |
| ReducedPrevNext | 8,452 | 6,242 | 339 | 33,596 |
| ReducedHeldPrevNext | 7,900 | 6,069 | 301 | 34,148 |

Adding exact context increases discrimination but sharply reduces coverage. For example, unique target sets rise from 484 in `ReducedOnly` to 4,911 in `ReducedPrevNext`, while comparable coverage falls to 8,452 targets. This is evidence of a coverage/discrimination frontier, not permission to rank views, back off between them or assign confidence.

## 8. Natural strata

The phenomenon survives distinct member types:

| Pair type | Trials | Comparable | Joint | Target marginal-only |
|---|---:|---:|---:|---:|
| Tap+Tap | 29,102 | 28,838 | 27,379 | 983 |
| Tap+LN | 5,532 | 5,332 | 3,424 | 1,277 |
| LN+LN | 7,414 | 7,304 | 6,309 | 653 |

Mixed Tap+LN targets show the largest marginal-only count relative to their population, which confirms why exact head type must remain part of identity. Reduced-state sizes from zero through five members were present. Empty reduced states were handled explicitly; denser reduced states increasingly abstained, rather than being coerced into a fallback.

## 9. Hard invalidity and abstention

Structural invalidity is evaluated before evidence. It includes duplicate lanes inside a candidate set, collision with the reduced state, invalid lane/type identity and non-constructible resulting state. The hypothetical population contained **103,198 hard-invalid pairs (7.440%)**; these are not counted as marginal evidence failures.

The evidence taxonomy remains four-way: `JointObserved`, `MarginalOnly`, `NotMarginallySupported`, and `HardInvalid`. Target reconstruction separately records `JointUnique`, `JointAmongAlternatives`, `TargetAbsent`, and `NoComparableContext`. No-context is a valid abstention, not a negative mapper claim.

## 10. Leakage and invariants

Measured correct-path counts were all zero:

- target whole-group leakage;
- target observation leakage;
- future-held leakage;
- synthetic teaching;
- cross-chart evidence;
- held-tail-as-head contamination;
- joint witnesses assembled across different occurrences.

Two deliberately invalid positive controls fired exactly once each: target-group leakage and cross-occurrence joint-witness construction. The gate therefore demonstrates both rejection and test sensitivity. Canonical construction produced **0 order-invariance violations across 42,048 trials**.

## 11. D0 and legacy regression

The runner reproduced every frozen stable D0 aggregate before D1.0: 11 charts, 14,463 groups, 40,360 trials, 39,596 comparable trials, 37,080 exact supported completions and all published strata/rates. Every row in `d1_0_phase_d0_reproduction.csv` matched.

The new research code does not enter generation, candidate selection, scoring, probabilities, RNG, serialization or the active Web/CLI policy. `BehaviorPolicyVersion` remains `legacy-experimental.1`, evidence remains `phase-a.1`, diagnostics remain `phase-c1-2-shadow.1`, and `behaviorChange=false`. Regression tests verify unchanged generated output and RNG transcript when the research path is not invoked.

## 12. Determinism and artifacts

Two complete executions produced byte-identical public artifacts and detail: **`MISMATCH_COUNT=0`**. The 378,151,348-byte local detail hash is:

`B259CA3E480A15CD1D03D09035F1CF3102DA01F7F2BB5EA611B1516AFEEF9316`

Public versionable artifacts:

- `d1_0_phase_d0_reproduction.csv`
- `d1_0_synthetic_gate_summary.csv`
- `d1_0_target_reconstruction_summary.csv`
- `d1_0_marginal_joint_summary.csv`
- `d1_0_composition_trap_summary.csv`
- `d1_0_context_view_summary.csv`
- `d1_0_chart_summary.csv`
- `d1_0_family_summary.csv`
- `d1_0_stratification_summary.csv`
- `d1_0_pair_type_summary.csv`
- `d1_0_reduced_state_summary.csv`
- `d1_0_leakage_summary.csv`
- `d1_0_order_invariance_summary.csv`

Full target/donor provenance remains local at `.artifacts/d1-0/d1_0_detail.json` and is intentionally ignored because of its size.

## 13. Outcome evaluation

Outcome A was frozen as requiring an exact and auditable representation, nontrivial recurrence across the human corpus, a material marginal-versus-joint distinction, zero leakage/order violations, and a falsifiable future behavioral question. Every condition passed.

Outcome B does not apply because joint composition is representable and recurrent. Outcome C does not apply because the result does not collapse to marginal support and the experiment is not confounded by leakage or nondeterminism.

## 14. What this result does not mean

D1.0 does not establish that jointly observed pairs should be accepted, that marginal-only pairs should be rejected, that one context view outranks another, or that frequency should become weight. It defines no threshold, score, fallback, confidence, probability or selector. It does not evaluate player quality or naturalness of generated output.

Most importantly, **D1 remains `FUTURE / NOT_AUTHORIZED`**. Connecting this evidence to accumulated current state would be behavioral work and requires a separately frozen A/B contract, policy version, admission metrics and rollback.

## 15. Recommendation

The next actionable research candidate is **D1.GATE — Resulting-State Behavioral Experiment Gate / Shadow**, still `NOT_AUTHORIZED`. Its sole purpose would be to pre-register a future behavioral experiment: exact admission/abstention semantics, measurement denominators, rollback and stopping criteria. It must not run generation A/B or change selection.

Only after that separate gate is reviewed and explicitly authorized could the historical **D1 — ChordCompletion Resulting-State A/B** be considered. D1.0 itself grants no such authorization.

## 16. Synthetic fixtures and sentinels

The synthetic gate contains 26 independently named checks. They cover k=1 equivalence, a valid same-occurrence joint witness, joint uniqueness, a marginal-only trap, cross-donor fabrication, deliberately bad leakage controls, whole-group and target-observation holdout, future-held hygiene, Tap/LN identity, canonical sets/states, no-context abstention, hard invalidity, empty reduced state, a dense-rice 12-pair enumeration, all eight views, original-only/cross-chart/cross-occurrence hygiene, rerun determinism, 1K/4K/7K/10K/18K implementation support and legacy policy invariance. All 26 passed before the human corpus ran.

The dense-rice fixture is a complexity and identity sentinel only; it creates no rice policy. LN heads are completion members, while LN releases and held tails remain distinct context endpoints and never become chord heads.

## 17. Limitations

- The human corpus contains 11 families and only 4K/7K/10K; 1K/18K are implementation fixtures, not human style evidence.
- Occurrences within a chart are not statistically independent; family macro is reported, but no population-wide causal claim is made.
- Exact identity abstains when recurrence is absent and may miss musically equivalent but non-identical states.
- `k=2` is the minimal nontrivial composition order. `k>=3` was deliberately not run because exhaustive expansion and detail size were not justified before results.
- The local detail is about 378 MB, so future tooling should stream or summarize rather than materialize additional orders.
- This phase measures original evidence structure, not playability, perceived naturalness or quality of generated maps.

## 18. Phase and branch state after closure

- D1.0: `COMPLETE — OUTCOME A`, `behaviorChange=false`.
- D1.GATE: `NEXT / NOT_AUTHORIZED`, research-only proposal.
- D1 behavioral: `FUTURE / NOT_AUTHORIZED`, `behaviorChange=true`.
- F2: `CONTINUE_CONDITIONALLY`.
- F2.ACQ: `BLOCKED / CONDITIONAL_ON_EXTERNAL_DATA`.
- E and E.1: `COMPLETE/B`; exact recurrence remains `PARKED`.
- C2: `DEFERRED`; MapperSupport remains `NOT_AUTHORIZED`.

## 19. Files changed

Research implementation is isolated in `src/ManiaAddNotesLab.Core/ResultingStateCompositionResearch.cs`. The opt-in runner is `tools/ManiaAddNotesLab.Experiments/D10ResultingStateRunner.cs`, registered only as the explicit `phase-d1-0-resulting-state` command. Focused tests live in `tests/ManiaAddNotesLab.Tests/PhaseD10ResultingStateCompositionTests.cs`.

Closure also updates the living README, status, roadmap, architecture, documentation index, design/experiments/audit, technical roadmap, canonical project state, state guards and their current-state assertion. No generation engine, Web surface, CLI generation path, production option or behavior version was changed.

## 20. Final verification

Final closure requires and records these commands:

```text
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project tools/DocConsistency -- --check
git diff --check
dotnet run --project tools/ManiaAddNotesLab.Experiments -- phase-d1-0-resulting-state <C11> docs .artifacts/d1-0/d1_0_detail.json
git check-ignore -v .artifacts/d1-0/d1_0_detail.json
git ls-files .artifacts/d1-0/d1_0_detail.json
```

The final suite contains **550 passed, 0 failed, 0 skipped**. D1.0 remains an unstaged local review set: no D1.0 commit, push, tag or release was made.
