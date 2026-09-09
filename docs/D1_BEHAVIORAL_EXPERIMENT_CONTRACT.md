# Frozen future D1 behavioral experiment contract

Status: **PREREGISTERED BY D1.GATE — NOT AUTHORIZED TO RUN**.  
Machine schema: `phase-d1-gate-behavioral-experiment-contract.1`.  
Frozen content hash: `D574631B3E713AC3D08C159B605A742D50C907B1BB4589D7FCADCFA8027A7824`.  
Control: `legacy-experimental.1`.  
Proposed treatment: `d1-resulting-state-ab.1`.

## Single hypothesis

For exactly one otherwise legacy-valid proposal that would change the exact added completion set at a timestamp from cardinality one to cardinality two, require an exact `ReducedOnly` same-occurrence joint witness for that proposed pair. A joint witness makes the proposal `ExperimentAdmittable`; its absence makes the treatment abstain. This is an experimental intervention, not mapper truth.

No other behavior changes: chance, proposal construction, shape/lane selection, density, gap, LN weights, articulation, opportunity order and k≥3 behavior remain legacy.

## Causal unit and state

The unit is one `D1EligibleDecision`, not an object, timestamp or chart. It exists only after:

1. the existing opportunity and Bernoulli path succeeds;
2. legacy has selected one exact lane and Tap/LN-head type;
3. existing hard geometry has declared the proposal legal;
4. exactly one pass-1 synthetic completion head is already accepted at that timestamp;
5. the prospective added set therefore has cardinality two.

The query is exact:

```text
OriginalBaseState at timestamp
    + AlreadyAddedCompletionMember
    + ProspectiveCandidate
    → ProspectiveCompletionSet(k=2)
    → ReducedOnly original-evidence lookup
```

`OriginalBaseState`, `AlreadyAddedCompletionMembers`, `ProspectiveCandidate`, `ProspectiveCompletionSet`, `ProspectiveResultingState` and the immutable `OriginalEvidenceIndex` remain separate. Synthetic heads describe current state but never teach evidence.

Candidate identity is exact `lane + TapHead|LongNoteHead`. An LN release and a held tail are not completion members.

## Dispositions

| Evidence state | Future experimental disposition |
|---|---|
| `JointObservedUnique` | `ExperimentAdmittable` |
| `JointObservedAmongAlternatives` | `ExperimentAdmittable` |
| `MarginalOnly` | `ExperimentAbstainMarginalOnly` |
| `NoComparableCompositionContext` | `ExperimentAbstainNoComparable` |
| `NotMarginallySupported` | `ExperimentAbstainNotObserved` |
| hard-invalid | excluded before `D1EligibleDecisions` |
| k=0→1, k≥2→3+, articulation | `OutsideExperimentScope` |

One donor and twenty donors have identical existence semantics. Uniqueness, frequency, majority, score and confidence do not alter disposition. There is one view and no fallback.

## Real engine insertion point

In `AddNotesEngine.Apply`, the future check belongs after `PlaceTap` or `PlaceLongNote` returns one non-null `TimedManiaObject`, but before `added.Add(placed.Object)` and `geometry.Insert(placed)`. At that point candidate lane/type, original analysis, accepted-set state and hard validity are available; output/current geometry have not yet been mutated.

Legacy has already consumed the chance and proposal RNG. Tap placement has selected a legal lane. LN placement has selected one legal shape and lane. The D1 check itself consumes zero RNG.

Current diagnostics mark a proposal `Placed` inside `PlaceTap`/`PlaceLongNote`; a future authorized implementation must add a separate direct D1 disposition or revise that label atomically before mutation. D1.GATE adds no hook.

## No reroll and RNG

One legacy proposal produces one D1 disposition. Abstention moves to the next already scheduled `OpportunityKey`; it must not reroll lane, shape, chance or candidate for the same opportunity and must not compensate elsewhere.

Control and treatment RNG transcripts must match until the first behavioral divergence. After an abstention changes geometry, downstream lane availability and branching may legitimately change RNG consumption. Instrument `FirstBehavioralDivergence`, `DirectD1Decision`, `RngCallsBeforeGate`, `RngCallsByGate=0` and `DownstreamRngDivergence`; never mislabel downstream differences as direct rejections.

## Bounded k scope

D1.0 validated k=2, not k≥3. The engine freezes opportunities before placement, but has no added-head cap per timestamp: simultaneous original heads create sequential base opportunities, and optional interior opportunities may share timestamps. The physical maximum is bounded by free lanes and otherwise successful legal opportunities. For example, three original heads in 7K yield three base opportunities with four initially free lanes, so three additions are structurally possible.

Future D1 v1 governs only k=1→2. k=0→1 and k≥2→3+ remain legacy. This is explicitly a **second-addition admission experiment**, not a full resulting-state policy. No cap of two is introduced. A later distinct opportunity after an abstention is not a reroll; it receives its own identity and decision. If k≥3 downstream interactions dominate interpretation, future D1 receives Outcome B rather than retrofitting a cap.

## Range, AddChance and pairing

The inclusive selected range filters opportunity source heads, not evidence. A proposal in range may query original-only evidence outside the range. AddChance remains the existing Bernoulli input and is not increased to compensate for abstentions.

Every A/B pair must use the same chart, seed, range, AddChance and all other options. Control is byte-exact `legacy-experimental.1`; treatment is the proposed version. No population seed protocol is currently justified, so a future D1 authorization must freeze an explicit deterministic paired seed list before running and label fixture seeds as implementation coverage only.

## Denominators and metrics

Keep separate: all legacy opportunities, otherwise legal proposals, `D1EligibleDecisions` (primary), timestamps with eligibility, charts with eligibility and independent families affected.

Direct metrics: eligible decisions; joint admissions split unique/alternatives; marginal-only, no-comparable and not-observed abstentions; direct admissions/rejections; first divergence reason.

Downstream/output metrics: downstream output differences, object/head/LN counts, affected timestamps, added-heads-per-timestamp distribution, output hash, reparse, hard-validity/overlap failures. Report micro, per-chart, family macro, 4K/7K/10K, Tap+Tap/Tap+LN/LN+LN, dense-rice sentinel and synthetic 1K/18K. These are structural effects, not quality measurements.

## Non-degeneracy, stopping and outcome

Zero marginal-only admissions is tautological and cannot pass D1. Future treatment must produce at least one direct admission and abstention, must not erase every addition from every affected chart, and must affect more than one family before a cross-family viability claim. Charts without eligibility remain explicit. No arbitrary retention percentage is frozen.

Abort immediately on evidence contamination, cross-chart or cross-occurrence witness fabrication, RNG use by gate, evaluation outside k=2, hard-invariant or deterministic failure, control/version drift, source overwrite, reparse/overlap failure, or untraceable direct decisions. Do not tune view, scope, seeds or semantics after observing output.

Future outcomes:

- **A:** exact execution, safety, determinism, nonzero admissions/abstentions across multiple families, attributable effects, noncollapsed generation and byte-exact rollback; proceed only to technical evaluation/playtesting.
- **B:** technically valid but restricted, abstention-heavy, k≥3/downstream dominated or still dependent on one narrow research question; no promotion.
- **C:** degenerate, unsafe, unattributable or unable to isolate joint evidence; rollback and park/redesign.

A technical Outcome A does not establish naturalness, preference, fun or appropriate difficulty. Human playtesting remains separate.

## Rollback and authorization

Treatment OFF equals exact `legacy-experimental.1`; no migration, rewrite, cache or persistent learned state exists. The D1 path must be removable without altering control bytes.

This contract does not authorize implementation or execution. D1 stays `NOT_AUTHORIZED` until a separate explicit human instruction.
