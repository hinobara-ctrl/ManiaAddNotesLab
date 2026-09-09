# Phase D1 — ChordCompletion Resulting-State A/B

## 1–4. Closure

**Status: COMPLETE — OUTCOME C. `behaviorChange=true`. `DefaultBehaviorChanged=false`.** The authorized experiment executed, hit a frozen hard-abort condition and is invalid for behavioral interpretation. Runtime/default behavior remains `legacy-experimental.1`; `d1-resulting-state-ab.1` is parked and not promoted.

## 5–8. Frozen identities

- Actual initial HEAD: `3d4ac28ec798116913a22b4c998932674e3d370b` (`Complete D1.GATE behavioral experiment gate`), confirmed by `HEAD`, `origin/main` and reflog. The frozen pre-run certificate incorrectly recorded `77638edad9b41c8d6e3a361ce68c296ed12c90bc`, which is the D1.0 baseline embedded in the D1.GATE contract rather than D1's repository entry HEAD. The certificate is intentionally left unchanged after human outputs; this preregistration identity defect independently reinforces Outcome C.
- D1.GATE contract hash, verified before implementation: `D574631B3E713AC3D08C159B605A742D50C907B1BB4589D7FCADCFA8027A7824`.
- Pre-run certificate: `PHASE_D1_PRE_RUN_DESIGN.md`, created before any C11 treatment output.
- Run manifest hash: `4C87F8B98B5B22B81CD903F26E3EF861B608893B50B46DBC20AF13158E8A4F00`.

## 9–14. Frozen corpus and matrix

C11 remained 11 SHA-256-deduplicated charts, 11 independent families and 4K/7K/10K. No community chart, sample, generated `[ADD …]` output or duplicate entered the matrix. Seeds were the explicit deterministic replication set 1–20, not a population sample. AddChance was 0.50, range full-chart and every remaining option is serialized in the manifest. The frozen matrix contained 220 exact control/treatment pairs.

Control was `legacy-experimental.1`; treatment was `d1-resulting-state-ab.1`. Both arms used identical chart, seed, range and options.

## 15–21. Implementation and causal contract

The treatment builds one immutable original-only ReducedOnly index per chart. `AddNotesEngine.Apply` calls the pure gate after chance, candidate shape/lane selection and legal geometry, but before `added.Add` and `geometry.Insert`. Candidate identity is exact lane plus TapHead/LongNoteHead; releases and held tails are excluded.

Only the transition from one accepted pass-1 synthetic completion member to a prospective set of two is queried. JointUnique and JointAmongAlternatives admit; MarginalOnly, NoComparable and NotMarginallySupported abstain. k=0→1, k≥2→3+ and articulation remain legacy. No fallback, uniqueness requirement, frequency, score, confidence, cap or reroll was added.

The gate records exact base state, both completion members, completion set, evidence state, donor occurrence IDs, OpportunityKey, RNG position and final mutation. Gate RNG is asserted zero. Treatment-disabled tests preserve exact legacy bytes and RNG transcript.

## 22. Synthetic gate

The implementation gate passed 15/15 focused test cases and the complete pre-run suite passed 599/599. It covered joint unique/alternative, cross-donor marginal-only, no comparable, not marginal, original-only immutability, k scope, no reroll, zero gate RNG, selected-range evidence, Tap/LN identity, 1K–18K, disabled rollback and pre-divergence transcript identity.

## 23. Human A/B status

**HUMAN A/B: RUN, THEN HARD-ABORTED.** The runner produced the frozen 220 pairs and a deterministic repeat before its post-repeat safety gate threw `D1 hard abort condition observed`. Because the gate was evaluated only after the matrix, all 220+220 pairs exist locally; this is a runner-order limitation, not permission to interpret or publish the behavioral counts.

## 24–36. Behavioral metrics withheld

`D1EligibleDecisions`, admissions, abstentions, JointUnique/Alternative, chart/family/keymode/pair-type summaries, rice/LN strata and first-divergence distributions are **NOT PUBLISHED**. The runner intentionally wrote public aggregates only after every hard condition passed. Reconstructing them post-abort would violate the frozen rule against finishing or rescuing an invalid experiment after observing outputs.

Direct-versus-downstream and later k≥3 instrumentation exists in local run state, but no attributable public claim is made. No quality, naturalness, preference or difficulty conclusion is possible.

## 37–42. Safety failure, determinism and rollback

Read-only inspection of the already-produced files isolated the abort to the safety counter: it counted raw inclusive same-lane interval intersections across the entire output. It found 200 intersections in 39/220 control outputs and 171 in 40/220 treatment outputs. Thus the metric included pre-existing/control structure and could not establish `AttributableD1OverlapFailures`. A smaller raw treatment count is not safety evidence and cannot be converted post hoc into success.

Both repeated output sets were nevertheless byte-identical: 0/220 control mismatches and 0/220 treatment mismatches. This rules out nondeterminism but does not repair the frozen safety attribution failure. Source files were opened read-only and output stayed below `.artifacts/d1/`.

Rollback is exact: normal CLI/Web/default paths never select D1; treatment OFF is tested byte/RNG-identical to legacy. Default remains `legacy-experimental.1`.

## 43–46. Non-degeneracy and outcome evaluation

Non-degeneracy and cross-family effect criteria are **NOT EVALUATED**, because safety attribution failed first. Outcome A and B require a valid, attributable experiment. The hard-abort/contract-interpretability failure and the incorrect initial-HEAD identity in the frozen certificate therefore require **OUTCOME C**. No threshold, metric repair, rerun, alternate view, certificate rewrite or policy adjustment is allowed inside D1.

D1 does not prove that joint admission is good, marginal-only is bad, treatment is natural, or the raw overlap delta is an improvement. It proves only that this concrete frozen execution could not pass its preregistered safety audit.

## 47–49. Recommendation and unchanged branches

Park D1 behavioral work and return to roadmap review. A future attempt would require a separately authorized, pre-registered experiment whose safety denominator distinguishes source/control pre-existing intersections from treatment-introduced failures; this report does not authorize that work.

Default remains `legacy-experimental.1`. F2 remains `CONTINUE_CONDITIONALLY`, F2.ACQ remains `BLOCKED`, E/E.1 remain COMPLETE/B with exact recurrence parked, C2 remains DEFERRED and MapperSupport remains unauthorized.

## 50–52. Files and artifacts

Implementation files add the immutable D1 index, experiment-only engine overload, causal diagnostics, runner and focused tests. Public files contain the pre-run certificate, frozen manifest, this report, abort summary, raw safety audit and deterministic-repeat audit. The 880 generated `.osu` files remain ignored under `.artifacts/d1/{first,repeat}/{control,treatment}`. No large output is tracked.

## 53–54. Final verification

Final verification: `dotnet restore ManiaAddNotesLab.sln` PASS (only NU1900 vulnerability-feed warning from unavailable NuGet index); full Release solution build PASS with an isolated ignored output; `dotnet test` PASS 599/599; DocConsistency PASS; D1.GATE contract hash PASS; frozen manifest hash PASS; `git diff --check` PASS. The ordinary output had previously been locked by the already-running Web executable, so validation deliberately used isolated build output without stopping the user's interface. Source corpus identity remained the frozen C11 fingerprint and generated details remained ignored/untracked.

## 55–56. Explicit boundary

**NO DEFAULT PROMOTION. NO MID-RUN TUNING. NO RERUN AFTER ABORT. NO COMMUNITY CORPUS. NO PLAYTESTING. NO COMMIT. NO PUSH. NO TAG. NO RELEASE.**
