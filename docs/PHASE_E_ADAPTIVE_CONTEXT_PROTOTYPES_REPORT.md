# Phase E — Adaptive Context Prototypes / Shadow

## 1. Status

**COMPLETE — OUTCOME B.** Component outcomes: **Recurrence B**, **Segmentation B**. `behaviorChange=false`. Everything in this phase is research-only, shadow-only and diagnostic-only.

## 2. Initial state

Baseline commit `028fe72a240be72f66124efc30f0744ac7efcdcc`, branch `main`, with 455 passed, 0 failed and 0 skipped. The canonical state was F2.3 COMPLETE/B, F2 `CONTINUE_CONDITIONALLY`, F2.ACQ BLOCKED, and E NEXT/NOT_AUTHORIZED. Generation remained `legacy-experimental.1`.

## 3. Research question

Can exact structure in the original chart demonstrate useful non-contiguous recurrence or stable contiguous context proposals without replacing the legacy ±4-beat convention with another arbitrary segmenter?

## 4. Frozen corpus

The historical C11 snapshot was frozen before human results: 11 charts, 11 families, keymodes 4K/7K/10K and 50,836 original objects. No sample, Web output or newly discovered file was admitted. The evaluated event series contains 28,312 exact temporal event groups.

## 5. Event-series representation

`OriginalTemporalEventSeries` is built only from `OriginalObjects` and existing `BeatTimeline`/evidence primitives. Each ordered event records its serialized time, exact file-derived beat, simultaneous Tap/LN heads, LN releases, held-before lanes, exact serialized spacing, stable group/observation IDs, structural token and provenance. LN head, release and held tail remain distinct.

## 6. Observed vs derived taxonomy

- File facts: timestamps, object IDs, lanes, object type, timing points and simultaneous groups.
- Exact file-derived facts: `BeatTimeline` coordinates, exact serialized spacing, releases, held-before state and categorical structural tokens.
- Research-derived claims: recurrence relations, boundary candidates, regions, reconstruction disposition and stability measurements.

A boundary is explicitly inferred: it is neither an observed fact nor mapper-authored section truth. No beat coordinate is treated as latent snap intent.

## 7. Candidate methods considered

Implemented methods are `global-chart.1`, `legacy-fixed-window.1/beats-4`, `exact-neighbor-recurrence.1`, `exact-block-recurrence.1/block-2`, and `stable-run-boundary.1` with `min-run-2` and `min-run-3`. Every method/configuration has deterministic method, version, representation and parameter-set identity.

## 8. Methods rejected before experiment

Fuzzy self-similarity was rejected because it required an unjustified distance and threshold and risked quadratic work. PELT/CROPS was deferred because its objective and penalty lacked mapper-derived justification. A hybrid recurrence/segmentation method was deferred so neither component could conceal failure of the other.

## 9. Baselines

`global-chart.1` is the parameter-free no-segmentation baseline. `legacy-fixed-window.1/beats-4` is only the historical ±4-beat external-convention reference; it is not truth and receives no authority.

## 10. Parameter audit

Block length 2 and stable-run minima 2/3 are explicit `RESEARCH_HYPOTHESIS` values. Four beats is `EXTERNAL_CONVENTION`. There is no hidden winner, tuned threshold, similarity epsilon, quantization grid or keymode style branch. Both stable-run configurations are published separately.

## 11. Synthetic ground truth

Twenty-one predefined cases cover stationary, abrupt change, A/B/A return, alternating recurrence, gradual change, isolated outlier, BPM-only change, structure-only change, short chart, LN-heavy timing, stable fast rice, sparse→dense, multiple regimes, 1K/4K/7K/10K/18K, and deliberately bad methods.

## 12. Negative controls

The evaluators detect four false positives from a boundary-at-every-event method, one false negative from a no-boundary method and one leakage violation from a deliberately contaminated evaluator. Thus the tests can fail rather than merely restating their inputs.

## 13. Synthetic gate

All synthetic cases passed before the human runner was allowed to execute. The complete 53-condition gate also covers research/generation isolation, original-only/chart-local evidence, provenance, method identity, holdout, determinism, sensitivity, multikey and legacy regression. Synthetic success validates machinery only; it is not human style evidence.

## 14. Recurrence analysis

Exact two-event blocks produced 10,980 recurrence relations across all 11 families; 10,764 are non-contiguous and represent 3,890 distinct recurrent identities among 28,301 eligible blocks. This establishes real chart-local recurrence without requiring a section partition. Exact identity is intentionally narrow and is not rescued with fuzzy matching.

## 15. Segmentation analysis

The stable-run proposal produced 288 inferred boundaries with minimum run 2 and 89 with minimum run 3. It abstained on short/stationary structures and did not use BPM changes as boundaries. The 199-boundary difference between the two explicit hypotheses is too large to call the partition robust.

## 16. Recurrence vs segmentation

The implementation and artifacts keep recurrence relations independent from contiguous boundary/region proposals. A/B/A and alternating controls show recurrence without requiring segmentation; structure-only change demonstrates a boundary without needing distant motif identity.

## 17. Held-out reconstruction design

For each target, every event belonging to its original observation IDs is excluded before donor/context construction. The exact-neighbor task predicts the categorical target token from exact previous/next donor context. Boundary stability separately rebuilds proposals after whole-event/block exclusions. Target and donor IDs are retained in detailed validation records.

## 18. Leakage audit

Measured target-observation, event-group and target-block leakage are all zero for the three reconstruction methods and both segmentation configurations. Synthetic teaching and cross-chart evidence are zero. The intentionally bad positive control reports one violation.

## 19. Boundary stability

Across 187 holdouts per configuration, `min-run-2` accumulated 4,895 baseline boundary references, 4,902 perturbed references, 4,893 exact shared references and 11 changes. `min-run-3` accumulated 1,513/1,513 references, 1,511 shared and four changes. This is strong local holdout stability, but not evidence of section accuracy.

## 20. Sensitivity

Changing only the declared minimum stable run from 2 to 3 reduced chart-level boundary proposals from 288 to 89; every chart records its own configuration output. The method is stable under individual exclusions yet highly sensitive to this research hypothesis. Those are compatible findings and must not be averaged into a winner score.

## 21. Human corpus

**RUN.** It was permitted only after the predefined synthetic gate passed. The runner processed exactly the frozen 11-chart C11 corpus.

## 22. Human results

| Method/config | Eligible | Comparable | Reconstructed | Ambiguous | Mismatch | No context |
|---|---:|---:|---:|---:|---:|---:|
| Global / none | 28,312 | 28,312 | 0 | 28,312 | 0 | 0 |
| Fixed ±4 beats | 28,312 | 28,312 | 1 | 28,307 | 4 | 0 |
| Exact neighbor recurrence | 28,290 | 12,066 | 4,143 | 6,472 | 1,451 | 16,224 |

Exact recurrence reconstructs substantially more targets than either baseline and does so in all 11 families. It also introduces 1,451 mismatches versus four for the fixed reference, with this increase present systematically across families, and abstains for 16,224 targets. Therefore it fails the frozen promotion criterion requiring no systematic mismatch increase.

## 23. Chart/family/keymode stratification

All 11 charts and all 11 families are eligible. By keymode: 4K has 3,566 events, 2,743 recurrence relations and 1,435 exact reconstructions; 7K has 22,304 events, 6,654 relations and 1,981 reconstructions; 10K has 2,442 events, 1,583 relations and 727 reconstructions. The finding is not carried by a single chart, family or keymode.

## 24. Rice sentinel

Stable fast streams and alternating/jack-like exact recurrence do not create forced boundaries in the synthetic controls. Isolated bursts/outliers likewise do not automatically become regimes. No rice, jack, trill or hand classifier was introduced.

## 25. LN and timing-change sentinel

Tap-only, LN-heavy, full/mixed LN, release, held-state and LN-across-BPM fixtures preserve endpoint semantics. Redundant and real BPM changes do not themselves create structural boundaries; a structural change at constant BPM remains detectable.

## 26. Recurrence component outcome

**B — partial/restricted signal.** Exact recurrence is real, deterministic, non-contiguous, chart-local and useful across every family, but coverage is restricted and mismatch rises systematically against the simple baselines. It merits diagnosis, not promotion.

## 27. Segmentation component outcome

**B — signal with important sensitivity.** Exact stable-run boundaries are reproducible and stable under holdout, but their count changes from 288 to 89 under the only explicit parameter variation. Without human section labels there is no boundary-accuracy claim and no defensible configuration winner.

## 28. Global Phase E outcome

**OUTCOME B.** Observable recurrent structure and stable local boundary behavior exist, but exact-neighbor mismatch, large no-context abstention and segmentation sensitivity prevent promoting an adaptive context representation. Global/simple context remains safer for current behavior.

## 29. Limitations

The corpus has 11 families and only 4K/7K/10K human charts; 1K/18K are synthetic coverage only. Human section labels and mapper intent labels do not exist. Exact tokens favor literal recurrence and can miss musically related variants. Stability measures perturbation resistance, not semantic correctness. The fixed reference is deliberately weak and is not truth.

## 30. What E does not prove

E does not recover mapper sections, infer snap intent, prove naturalness, choose a context scope, assign confidence/authority, validate fuzzy similarity, repair F2 coverage or justify a new generation policy. Better reconstruction than a baseline is not behavioral authority.

## 31. Next recommendation

Recommend **E.1 — Exact Recurrence Failure Stratification / Shadow**, `NEXT / NOT_AUTHORIZED`. Its narrow question should explain the 1,451 mismatches and 16,224 no-context cases by exact observable strata before any new representation is proposed. It must remain research-only and must not add fuzzy similarity automatically.

## 32. F2 branch state

Unchanged: F2 remains `CONTINUE_CONDITIONALLY`, blocked by F2.ACQ. Phase E creates no quantizer and supplies no external mapper-authored acquisition package.

## 33. Other phase states

F2.ACQ remains BLOCKED/conditional on external data. D1 is not authorized, C2 remains deferred, and MapperSupport plus G1/G2/H/I/J/K remain unauthorized.

## 34. Files changed

Added a standalone adaptive-context research model, a Phase E experiment runner, focused tests, the frozen pre-human certificate, this report and aggregate CSVs. The experiment command was registered in the existing runner entry point. No generation, Web, CLI behavior, options or defaults were modified.

## 35. Artifacts

Public deterministic artifacts are `docs/e_method_catalog.csv`, `e_synthetic_ground_truth_summary.csv`, `e_chart_summary.csv`, `e_family_summary.csv`, `e_recurrence_summary.csv`, `e_boundary_summary.csv`, `e_heldout_validation_summary.csv`, `e_stability_summary.csv`, `e_sensitivity_summary.csv`, `e_stratification_summary.csv` and `e_leakage_summary.csv`. Detailed target/donor provenance is local at `.artifacts/e/e_detail.json` and ignored.

## 36. Determinism

Two complete reruns produced byte-identical public CSVs and detail JSON. Ordering, identities and hashes do not depend on enumeration order, machine paths, current time or RNG.

## 37. Regression

Phase E tests verify original-only invariance, exact rerun/permutation stability, zero generation/RNG dependencies, and byte-exact legacy generation plus RNG transcript. Existing parser/writer, collision, CLI/Web and default-policy coverage remain in the full suite.

## 38. Final test count

The final unfiltered Release run passed **487**, failed **0** and skipped **0** tests. This is the observed result after Phase E, not a reused baseline constant.

## 39. Final verification commands

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project tools/DocConsistency -- --check
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- phase-e-adaptive-context <frozen-corpus> docs .artifacts/e/e_detail.json
git check-ignore -v .artifacts/e/e_detail.json
git ls-files .artifacts/e/e_detail.json
git diff --check
git status --short
```

No commit, push, tag or release is part of Phase E closure.
