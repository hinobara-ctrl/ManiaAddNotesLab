# Phase D1.GATE — Resulting-State Behavioral Experiment Gate / Shadow

## 1–3. Status, decision and behavior

**Status: COMPLETE. Gate decision: READY. `behaviorChange=false`.** READY means one future experiment is sufficiently specified for separate review; it does not authorize implementation or execution of D1.

## 4–5. Initial commit and baseline

Initial and published baseline: `77638edad9b41c8d6e3a361ce68c296ed12c90bc` (`Complete D1.0 resulting-state composition feasibility research`), branch `main`, clean worktree, local `origin/main` equal. Baseline: restore PASS; build PASS with zero errors; **550 passed / 0 failed / 0 skipped**; DocConsistency PASS; `git diff --check` PASS. The only warning was NU1900 from the unavailable NuGet vulnerability endpoint.

## 6. D1.0 facts reproduced

No new human search ran. The validation runner read the frozen public artifacts and reproduced: 42,048 pair targets; 2,913 target marginal-only pairs in `ReducedOnly`; 374,520 hypothetical marginal-only eligible pairs; zero target-group and cross-occurrence leakage; zero order violations. D1.0 remains `COMPLETE/A`.

## 7–11. Behavioral question, hypothesis, control and treatment

Question: can one bounded second-addition intervention isolate exact joint-composition evidence with attributable effects and exact rollback?

Single future hypothesis: after legacy has produced one otherwise valid exact proposal, when that proposal would move the pass-1 added completion set from cardinality one to two, `ReducedOnly` same-occurrence joint evidence makes it experimentally admittable; otherwise treatment abstains without reroll.

Control is exact `legacy-experimental.1`, with the same input, seed, range, AddChance and all options. Proposed treatment identifier is `d1-resulting-state-ab.1`; it is only preregistered and is not the compiled active policy.

## 12–13. Evidence view decision

The frozen view is **ReducedOnly**. It directly tests the D1.0 marginal-versus-joint distinction and has the primary representation/coverage. Adding held, previous, next or transition context would introduce a second hypothesis about context authority. There is no fallback or search across views.

## 14–16. k=2, k≥3 and absence of a hidden cap

The only governed transition is pass-1 added-set k=1→2. k=0→1 and k≥2→3+ remain legacy and outside scope. Articulation replacement is also outside scope.

The engine has no explicit added-head cap per timestamp. It creates one base opportunity per original head, precomputes the opportunity list, orders by time/kind/order, and inserts accepted objects into current geometry. Optional interior opportunities can share timestamps. Maximum accumulation is `min(free lanes, successful legal opportunities at that timestamp)`; three additions are structurally possible, for example with three original heads in 7K and four initially free lanes.

No k≥3 human prevalence was inspected because it was unnecessary for contract coherence and could become post-hoc style tuning. Later k≥3 effects are downstream metrics. If they dominate interpretation, future D1 must return Outcome B; it may not add a cap of two.

## 17–19. Exact insertion, candidate timing and hard validity

The clean semantic insertion point is in `AddNotesEngine.Apply`: after `PlaceTap`/`PlaceLongNote` returns a non-null `TimedManiaObject`, before `added.Add(placed.Object)` and `geometry.Insert(placed)`. The baseline implementation places that boundary immediately before current lines 131–132.

At this point chance has succeeded; tap lane or LN shape/lane has been selected; exact `lane + TapHead|LongNoteHead` identity exists; original analysis, accepted pass-1 state and immutable evidence are available; hard geometry has passed; output and geometry have not been mutated. Existing hard-invalid proposals never enter the D1 denominator.

Existing diagnostics label proposals inside `PlaceTap`/`PlaceLongNote`; a future authorized implementation must record D1 disposition separately or revise the label atomically before mutation. This is an instrumentation task, not a blocker or a hook added by D1.GATE.

## 20–21. RNG and no reroll

The gate consumes **zero RNG**. The chance roll and legacy proposal RNG occur before it: tap uses one legal-lane selection; LN uses weighted shape selection then legal-lane selection. Control/treatment transcripts must match through the first behavioral divergence. After abstention changes geometry, downstream RNG may diverge and must be labeled downstream.

One `OpportunityKey` produces one proposal and one disposition. D1 abstention cannot retry chance, lane, shape or another candidate for that opportunity. A later pre-existing opportunity is a separate causal unit, not a reroll.

## 22–23. Original evidence and current state

The evidence index is frozen exclusively from `OriginalObjects`; accepted synthetics never become donors, witnesses, frequency or context. Synthetics may only describe `AlreadyAddedCompletionMembers` in current state.

The contract separates `OriginalBaseState`, `AlreadyAddedCompletionMembers`, `ProspectiveCandidate`, `ProspectiveCompletionSet`, `ProspectiveResultingState` and `OriginalEvidenceIndex`. The evidence lookup uses original base state as the exact `ReducedOnly` reduced state and the two-member added set as the queried completion set.

## 24–25. Evidence and admission taxonomies

Evidence states: `OutsideScope`, `HardInvalidExcluded`, `NoComparableCompositionContext`, `MarginalOnly`, `NotMarginallySupported`, `JointObservedUnique`, `JointObservedAmongAlternatives`.

Both joint states map to `ExperimentAdmittable`; uniqueness is not required. The three non-joint comparable/absence states map to explicit experimental abstentions. Hard invalid is excluded and k outside scope remains legacy. None means approved, forbidden, natural, correct or mapper truth.

## 26–28. Denominators and attribution metrics

Keep separate: all opportunities; otherwise legal proposals; **D1EligibleDecisions** (primary); eligible timestamps; eligible charts; independent families affected.

Direct metrics: eligible decisions, joint admissions split unique/alternative, marginal-only/no-comparable/not-observed abstentions, direct admissions/rejections and first divergence reason.

Downstream metrics: output differences caused after an earlier direct decision, counts of objects/heads/LNs, affected timestamps, added-head distribution, output hash, reparse, overlap and other hard failures. They are not direct D1 rejections.

## 29–32. Non-degeneracy, strata and sentinels

Zero marginal-only admissions is guaranteed by definition and cannot pass D1. Future D1 must observe nonzero direct admissions and abstentions, preserve at least some additions on affected charts, report charts with zero eligibility, and affect more than one family before any cross-family viability claim. No retention percentage or p-value is invented.

Report micro, per-chart, family macro, 4K/7K/10K and Tap+Tap/Tap+LN/LN+LN. Synthetic 1K/18K cover implementation only. Dense Tap-heavy timestamps are a rice sentinel without rice/jack/trill/hand policy. LN head remains a completion member; release and held tail do not. `ReducedOnly` does not secretly consult LN held/release context.

## 33–34. Selected range and AddChance

Range keeps current semantics: it filters inclusive opportunity source heads, not original evidence. Evidence outside the range remains queryable. AddChance remains the same Bernoulli before proposal construction; treatment neither redefines intensity nor compensates elsewhere for abstentions.

## 35–36. Stopping and HOLD criteria

Immediate aborts include evidence contamination, cross-chart or multi-occurrence fabricated witnesses, gate RNG, out-of-scope evaluation, hard-invariant/determinism/reparse failure, control/version/default drift, source overwrite and untraceable direct decisions. No mid-run changes to view, k, seeds or taxonomy are allowed.

D1 must remain on HOLD if implementation reveals no clean pre-mutation point, extra RNG/reroll, inseparable hard validity, multiple required views, frequency/score authority, synthetic teaching, unattributable downstream effects, non-exact rollback or k≥3 interactions that make k=2 uninterpretable.

## 37–38. Rollback and future D1 outcomes

Treatment OFF is byte-exact `legacy-experimental.1`: no migration, chart rewrite, persistent state or evidence cache mutation. The path must be removable without altering legacy.

Future outcomes were frozen before A/B: A permits continued technical evaluation/playtesting only after exact, deterministic, safe, nondegenerate, multi-family and attributable behavior; B denotes valid but restricted/abstention-heavy/k≥3-downstream-dominated behavior with no promotion; C denotes degenerate, unsafe or unattributable behavior and requires rollback/park.

## 39. Playtesting boundary

Structural A/B measures causal behavior, safety and reproducibility. It cannot establish preference, naturalness, fun or difficulty appropriateness. Human playtesting remains a later independent requirement.

## 40. Synthetic contract validation

The pure evaluator covers joint unique/alternative admission; marginal/no-context/not-observed abstention; hard-invalid exclusion; k=1 and k=3 outside scope; zero RNG/no retry; frequency-independence; exact Tap/LN identity; release non-membership; 1K/4K/7K/10K/18K; ReducedOnly/no fallback; exact rollback; inactive treatment version; and no production callsites. All contract tests passed.

## 41. Machine-readable contract and hash

`d1_gate_behavioral_experiment_contract.json` is canonicalized as UTF-8 camelCase JSON without indentation for hashing, fixed property order and ordinal sorting for set-like arrays. It excludes timestamps and machine paths. Frozen SHA-256:

`D574631B3E713AC3D08C159B605A742D50C907B1BB4589D7FCADCFA8027A7824`

Changing evidence view, k scope, admission states, RNG, control/treatment versions or rollback changes the hash; mere set-array reordering does not.

## 42. Limitations

- No behavioral output or actual proposal prevalence was observed.
- Non-degeneracy remains a future empirical question.
- k≥3 remains unvalidated and deliberately outside the bounded intervention.
- No population seed design is justified; future authorization must freeze one before running.
- Exact evidence may abstain on musically related but non-identical states.
- Existing diagnostics require future attribution plumbing at the insertion boundary.
- D1.0 human independence remains 11 chart/family units, not 42,048 independent samples.

## 43–44. Authorization and recommendation

D1.GATE authorizes no active code path, toggle, default, policy, A/B or generation change. D1 is now the next **behavioral candidate** because the contract is READY, but remains unequivocally `NOT_AUTHORIZED`. The next action is human review and separate explicit authorization; there is no next research phase automatically opened.

## 45. Files changed

Added a pure research contract/evaluator, an opt-in design runner, contract tests, this report, the human-readable contract, machine JSON and five small audit artifacts. Living docs, canonical state and DocConsistency are updated only after this report. No `AddNotesEngine`, CLI, Web, production options/defaults or generation file changed.

## 46–47. Final tests and verification

Final verification: restore PASS; Release build PASS with zero errors; **584 passed / 0 failed / 0 skipped**; DocConsistency PASS; contract runner PASS with the frozen hash above; two artifact snapshots had identical SHA-256 values; `git diff --check` PASS; no staged files; no production callsite; active behavior remains `legacy-experimental.1`. The only warning was the already-known NU1900 vulnerability-endpoint lookup.

## 48. Explicit closure boundary

**NO D1 A/B RUN. NO BEHAVIOR CHANGE. NO COMMIT. NO PUSH. NO TAG. NO RELEASE.**
