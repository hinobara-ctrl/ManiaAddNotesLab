# Phase D1.SAFETY — Attributable Geometry Safety Semantics / Shadow

## 1–7. Closure and preserved state

**Status: COMPLETE — OUTCOME C. `behaviorChange=false`.** Initial HEAD, branch and verified `origin/main` were `988c987b0f51f0e5b71e70263d9a8a0a4992f733` / `main` / the same hash. The entry worktree was clean. Baseline restore, Release build, 599/599 tests, DocConsistency and diff check passed; NU1900 remained the known unavailable vulnerability-feed warning.

D1 remains permanently **COMPLETE — OUTCOME C — PARKED**, with `behaviorChange=true` historically and `DefaultBehaviorChanged=false`. `legacy-experimental.1` remains default; `d1-resulting-state-ab.1` remains experiment-only and parked. D1.SAFETY did not change normal generation.

## 8–15. Problem and real engine semantics

The old D1 audit counted raw inclusive same-lane interval intersections over final output. That mathematical relation is not itself an attributable hard-validity failure.

- Tap: a same-lane equal head is illegal. A prior LN blocks a tap while `EndBeat >= TapBeat`, including equality at release.
- LN: duration must be positive. LN/LN overlap uses strict crossing; boundary equality is not overlap, while the candidate-specific required gap is evaluated separately.
- Articulation: each proposed segment is checked while explicitly ignoring its parent; it is not equivalent to ordinary independent insertion.
- Serialization: beat-space validity and millisecond validity are distinct. A positive beat duration can materialize invalidly, and final serialized coordinates can conceal the candidate beat used by placement.

Accordingly the frozen hierarchy is `RawGeometryRelation != HardValidityViolation != AttributableIntroducedViolation`.

## 16–25. Taxonomy and causal attribution

The contract defines `NoViolation`, source/control-pre-existing conditions, legacy/direct/downstream/articulation/serialization introduced violations, resolution relative to control and `Unattributable`. Source is parsed original state, control is legacy output and treatment is a separately authorized experimental output.

An introduced label requires absence in the causal parent, a real engine hard violation and mutation provenance identifying policy, stage, opportunity and causal unit. Direct means the governed mutation itself; downstream means a later mutation after an earlier treatment divergence. Conditions present in source or control cannot silently be charged to treatment. Missing provenance is `Unattributable`. A raw condition absent in treatment may be recorded as resolved relative to control, never automatically as a safety improvement.

## 26–37. Oracle, synthetic validation and contract

`GeometrySafetyAttributionResearch` is a generic, pure, read-only, deterministic component. It has no RNG or mapper-evidence dependency, uses stable semantic IDs and bounded lane-local scanning, and separates snapshot relations from provenance-bearing mutation evaluation. It does not connect to AddNotesEngine, CLI, Web or defaults.

The synthetic matrix covered legal and illegal tap/LN boundaries, containment/crossing, required gap, articulation parent exclusion, serialization collision/non-collision, source/control conditions, direct/downstream/legacy categories, missing-provenance bad control, same/different lane, order invariance, deterministic IDs, read-only state and 1K/4K/7K/10K/18K. Focused result: 26/26. Full pre-forensic suite: 625/625. Contract serialization repeated byte-identically and the oracle consumes zero RNG.

Pre-forensic certificate: `PHASE_D1_SAFETY_PRE_FORENSIC_DESIGN.md`. Contract: `geometry-safety-attribution-shadow.1`, frozen hash `C4C1273AB4029D8AAF68EFA25AB64159B56D524AF6B8B26D74054A57A2096133`, verified against the machine artifact.

## 38–48. Historical forensic attempt and hard abort

Old D1 outputs were available and opened read-only; D1 was not rerun. The safety-only scan reproduced the old raw totals: control 200 and treatment 171. It did not inspect or publish eligibility, admissions, abstentions, family effects, pair types or any other withheld behavioral metric.

The attempt then violated the frozen causal rule: it labeled final-output conditions absent from source as `LegacyIntroduced` without mutation-level provenance. Snapshot difference proves state difference, not the causal mutation or whether the condition arose directly, downstream, through articulation or during serialization. The correct answer at that boundary was `Unattributable`.

This is an explicit hard-abort condition. The invalid forensic aggregate was withdrawn; no treatment-direct, treatment-downstream, legacy, serialization or resolved counts are published. Raw 200/171 is retained only as reproduction of the known non-attributable metric. It cannot rescue or reinterpret D1.

## 49–53. Baseline identity hardening, limitations and Outcome C

`ExperimentBaselineIdentityResearch` separates `RepositoryEntryHead` from `ImplementationSnapshotHash`, canonicalizes a named source-file set, records branch/origin/dirty state/contract/manifest and detects certificate mismatch. The pre-forensic capture correctly recorded HEAD `988c987…`, unlike D1's historical handwritten baseline defect.

The unresolved boundary is fundamental for old final artifacts: exact direct/downstream/articulation/serialization attribution requires mutation provenance captured while generation runs. Inferring it afterward would be heuristic. Because the forensic implementation actually crossed that boundary instead of abstaining, the phase meets the frozen **Outcome C** condition: causal attribution was guessed rather than proven.

Recommendation: park D1.SAFETY and return to roadmap review. A future separately authorized shadow design would need mutation-level pre/post geometry and serialization provenance from its inception. This task does not authorize that work or any behavioral experiment.

## 54–60. Artifacts, verification and boundaries

Public artifacts retained: pre-forensic certificate, machine contract, semantic matrix, synthetic summary, contract/synthetic determinism summary, baseline identity summary, abort summary and this report. No forensic attribution CSV is retained. Repeat safety detail under `.artifacts/d1_safety/` remains ignored; old D1 outputs remain untouched.

Living docs and `PROJECT_STATE.json` record D1.SAFETY COMPLETE/C/PARKED while preserving D1 COMPLETE/C/PARKED. Files changed are limited to the pure oracle/contract/baseline identity helpers, research runner, synthetic tests, closure guards and documentation. No normal policy callsite was added.

Final `dotnet restore ManiaAddNotesLab.sln`, normal Release solution build, `dotnet test ManiaAddNotesLab.sln -c Release`, DocConsistency and `git diff --check` all PASS. Tests: 625 passed / 0 failed / 0 skipped. NU1900 remains only the known vulnerability-feed availability warning. Normal generation byte/RNG regression passes, CLI/Web contain no safety-oracle or treatment selector, and the active default remains `legacy-experimental.1`.

The worktree is intentionally dirty for human review; no file is staged. Large ignored details are not tracked. No commit, push, tag or release was performed.

**NO D1 RERUN. NO WITHHELD METRICS. NO D1 REPAIR. NO DEFAULT CHANGE. NO COMMUNITY DATA. NO PLAYTESTING. NO COMMIT. NO PUSH. NO TAG. NO RELEASE.**
