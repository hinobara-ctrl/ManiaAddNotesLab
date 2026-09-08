# Phase E.1 — Exact Recurrence Failure Stratification / Shadow

## 1. Status

**COMPLETE.**

## 2. Outcome

**OUTCOME B.** The failure population is usefully stratified, but exact observable refinements do not yield a defensible successor resolver.

## 3. Behavior change

`behaviorChange=false`. E.1 is research-only, shadow-only and diagnostic-only. `legacy-experimental.1` remains the active generation policy.

## 4. Initial state

Baseline commit `9a4eaafe837916af77df913104d25da3210e2966`, branch `main`, clean tree, 487 passed / 0 failed / 0 skipped. E was COMPLETE/B with Recurrence B and Segmentation B; E.1 was NEXT/NOT_AUTHORIZED. F2 remained `CONTINUE_CONDITIONALLY` and F2.ACQ BLOCKED.

## 5. Phase E reproduction

Phase E was rerun to `.artifacts/e1/baseline/` before E.1 human analysis. All eleven public E CSVs matched their committed SHA-256 hashes. The reproduced totals were 50,836 original objects, 28,312 event groups, 28,290 exact-neighbor eligible, 12,066 comparable, 4,143 reconstructed, 6,472 ambiguous, 1,451 mismatch and 16,224 no-context; block recurrence remained 10,980 links/10,764 non-contiguous, and segmentation remained 288/89 boundaries.

## 6. Exact-block relation semantics

For each exact block identity, Phase E sorts occurrences by start index and emits consecutive links only: `A1→A2`, `A2→A3`, …, not every pair. The human corpus contains 3,890 recurrent identities and 14,870 occurrences, producing 10,980 consecutive links; an all-pairs materialization would contain 82,981 pairs. Four synthetic occurrences correctly produce three links rather than six.

## 7. Research question

Why does the unchanged `exact-neighbor-recurrence.1` produce Reconstructed, Ambiguous, Mismatch or NoContext, and which exact already-observable properties distinguish those populations without introducing a new inference identity?

## 8. Frozen taxonomy

The taxonomy and four counterfactual configurations were frozen in `PHASE_E_1_PRE_HUMAN_DESIGN.md`. NoContext has mutually exclusive primary reasons: edge-ineligible, no exact pair elsewhere, only excluded occurrences, or other exact absence. The overlapping property matrix measures exact spacing, held state, type/head/lane composition, releases, donor multiplicity and hygiene. None is labeled causal.

## 9. Synthetic fixtures

The gate covers reconstructed, ambiguous, mismatch, self-only no-context, sanitized target-touching occurrence, equal/divergent spacing, held/type divergence, unequal donor multiplicity, four-occurrence link semantics, edge targets, counterfactual transition taxonomy, positive/correct leakage, fast rice, LN/release/BPM and 1K/4K/7K/10K/18K.

## 10. Synthetic gate

All 20 published gate rows passed before E.1 human diagnostics. The focused suite adds exact permutation/rerun, redundant redline, real BPM change, original-only and generation/RNG regression coverage. Synthetic truth validates the diagnostic machinery, not human style.

## 11. Human corpus

**RUN.** The gate passed and the frozen C11 corpus remained exactly 11 charts, 11 families and 4K/7K/10K. Fixtures were not counted as human evidence.

## 12. Reconstructed control

There are 4,143 reconstructed targets. They have exactly one semantic center identity by definition, represented by 12,054 raw donor occurrences (2.91 per target). Serialized spacing differs among donors for 2,558/4,143 (61.74%); exact file-derived beat spacing differs for 2,875/4,143 (69.39%). This high control prevalence limits spacing as a failure explanation. Type, head count, lane and held divergence are zero because a reconstructed center has the exact target token.

## 13. Ambiguous control

There are 6,472 ambiguous targets, with 170,443 raw donor occurrences and 41,789 semantic centers (26.34 and 6.46 per target). Unequal multiplicity never votes. Spacing divergence is 94.98% serialized and 95.75% file-derived beat; type/head divergence is 92.44%. Ambiguity is a genuine multi-center compatibility state, not weak support or error.

## 14. Mismatch analysis

All 1,451 mismatches have one semantic donor center distinct from the target and exact lane-occupancy disagreement; this is definitional, not causal. Type/head-count difference appears in 935 (64.44%), serialized-spacing disagreement in 1,033 (71.19%), and file-derived beat-spacing disagreement in 1,100 (75.81%). Multiple diagnostic differences overlap in 1,300 (89.59%). Every property appears across all 11 families except held divergence, which is zero.

## 15. NoContext analysis

The mutually exclusive breakdown sums exactly to 16,224. `NoExactNeighborPairElsewhere` accounts for 16,145 (99.513%); only 79 (0.487%) have other apparent occurrences that are all removed by whole-observation hygiene. Edge targets are excluded from the eligible human denominator and `OtherExactAbsence` is zero. Therefore the dominant issue is literal exact absence, not sanitizer overreach.

## 16. Donor multiplicity

Multiplicity is descriptive only. Reconstructed has 12,054 occurrences for 4,143 semantic centers; Mismatch has 1,720 occurrences for 1,451 centers; Ambiguous has 170,443 occurrences for 41,789 centers. A 10-vs-1 synthetic donor split remains Ambiguous. No rank, vote, probability or confidence is computed.

## 17. Spacing stratification

Spacing is the only non-definitional observable with broad mismatch presence, but its separation from controls is modest: 71.19% vs 61.74% for serialized spacing and 75.81% vs 69.39% for exact file-derived beat spacing. Ambiguous is higher than both. Coordinates are exact file-derived facts, never snap or nominal musical intervals.

## 18. Held-state stratification

Held-state divergence is zero in all four outcomes. The baseline neighbor tokens already preserve exact external held state and center held state is part of the exact center token; consequently the proposed held refinement changes no disposition. This is a useful negative result, not permission to redesign identity.

## 19. Tap/LN/release stratification

Mismatch is dominated by ordinary tap-head targets: 1,305/1,451 are tap heads without held-before or release-involved external context. NoContext is more LN-associated: 8,746/16,224 (53.91%) have release-involved external context and 8,403/16,224 (51.79%) have target held-before, versus 40.57% and 42.99% in Reconstructed. This is association with exact absence, not an LN cause or articulation rule.

## 20. Chart/family/keymode analysis

All four dispositions and spacing/type findings are published by chart and family. Mismatch occurs in every chart (14–356) and all 4K/7K/10K strata. NoContext ranges from 61 to 5,002, demonstrating strong chart variance; nevertheless its exact-absence primary reason appears in all 11 families. No keymode branch was introduced.

## 21. Counterfactual audit

Four predeclared diagnostics were evaluated after unchanged donor hygiene: baseline, +exact spacing, +exact held, and +both. Held produces no changes. Exact spacing preserves 2,427 reconstructed and destroys comparability for 1,716; among Ambiguous it yields 594 Reconstructed, preserves 2,966, yields 808 Mismatch and destroys 2,104; among Mismatch it preserves 386 and yields 1,065 NoContext.

## 22. Coverage destruction

No Mismatch becomes Reconstructed under an exact filtering refinement: filtering cannot introduce a target center absent from the baseline donor set. The apparent mismatch reduction of 1,065 is entirely `Mismatch→NoContext`. Moreover 1,716 valid Reconstructed controls also become NoContext. Therefore fewer mismatches would be a coverage artifact, not an improvement.

## 23. Rice sentinel

Fast 31 ms alternating exact recurrence passes the same identity and multiplicity rules. E.1 introduces no rice/jack/trill class, temporal threshold, hand model or behavior. Human spacing remains reported as exact values rather than a density label.

## 24. LN and timing sentinels

LN head, release and held tail remain distinct. LN crossing a BPM change uses existing `BeatTimeline`; redundant same-BPM redlines preserve diagnostics, and real BPM changes only alter exact file-derived coordinates. BPM change is not treated as a failure cause.

## 25. Leakage

Across 28,290 human targets, target-observation leakage is zero, synthetic teaching is zero and cross-chart evidence is zero. The deliberately bad positive control reports one violation. Pre-holdout candidates are diagnostic only; the baseline disposition always uses post-sanitization donors.

## 26. Determinism

Two complete runner executions produced byte-identical thirteen public CSVs and byte-identical `.artifacts/e1/e1_detail.json`. Stable ordering excludes RNG, current time, machine paths and unstable hashes from identities.

## 27. Limitations

The human corpus contains only 11 families and no human 1K/18K. Exact identity cannot diagnose musically similar but non-identical contexts. Type/lane differences partly restate why exact tokens differ. The property matrix is observational; no human failure-cause labels exist. Counterfactual filtering measures compatibility and coverage, not preference.

## 28. What E.1 does not prove

E.1 does not prove spacing or LN state should enter a resolver; does not select features; does not recover mapper intention; does not justify fuzzy similarity, quantization, backoff, weighting or confidence; does not reopen segmentation; and does not change generation, F2, D1, C2 or MapperSupport.

## 29. Final outcome

**OUTCOME B.** E.1 explains NoContext strongly as exact absence and demonstrates that sanitizer loss is negligible. Mismatch has broad spacing/type associations, but spacing also occurs frequently in controls and exact refinement mostly destroys coverage; held state supplies no additional distinction. The explanation is useful but partial and does not justify E.2.

## 30. Next recommendation

**PARK exact recurrence refinement.** Do not add fuzzy fallback or further feature search. Recommend returning to roadmap review with **D1 — Resulting-State Chords / Shadow** as the next research candidate, `NEXT / NOT_AUTHORIZED`; this is a recommendation only and E.1 supplies no D1 authority.

## 31. Files changed

Added a standalone E.1 research projection, tests, runner, frozen pre-human certificate, this report and aggregate artifacts. Updated only the experiment entry point, state guard expectations and living documentation. No engine, production option, Web/CLI behavior, F2 resolver or segmentation code changed.

## 32. Artifacts

Public artifacts: `e1_phase_e_reproduction.csv`, `e1_synthetic_gate_summary.csv`, `e1_relation_semantics_summary.csv`, `e1_chart_summary.csv`, `e1_family_summary.csv`, `e1_disposition_property_summary.csv`, `e1_no_context_breakdown.csv`, `e1_mismatch_breakdown.csv`, `e1_donor_multiplicity_summary.csv`, `e1_counterfactual_refinement_summary.csv`, `e1_exact_spacing_summary.csv`, `e1_ln_stratification_summary.csv` and `e1_leakage_summary.csv`. Full provenance is local in `.artifacts/e1/e1_detail.json` (179,004,515 bytes) and ignored.

## 33. Regression

E.1 tests cover unchanged Phase E disposition semantics, original-only invariance, chart-local evidence, leakage, relation-link semantics, multiplicity without voting, exact counterfactual transitions, timing/LN/rice/multikey sentinels, deterministic permutation/rerun, and byte/RNG-identical legacy generation.

## 34. Final test count

The final unfiltered Release run passed **512**, failed **0** and skipped **0** tests. This is the observed E.1 result, not an inferred baseline total.

## 35. Verification commands

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project tools/DocConsistency -- --check
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- phase-e-1-recurrence-failures .artifacts/f2-1-corpus docs .artifacts/e1/e1_detail.json
git check-ignore -v .artifacts/e1/e1_detail.json
git ls-files .artifacts/e1/e1_detail.json
git diff --check
git status --short
```

No commit, push, tag or release belongs to this closure.
