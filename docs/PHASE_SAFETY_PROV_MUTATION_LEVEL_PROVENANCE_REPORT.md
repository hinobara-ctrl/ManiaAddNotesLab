# SAFETY.PROV — Mutation-Level Generation Provenance / Shadow

## 1–8. Closure and baseline

Status: **COMPLETE — OUTCOME A / RESEARCH SHADOW**. `behaviorChange=false`.

The repository entered at `dc8a557d22d0f2a07668541cfcbb912afbf1704b` on `main`; `origin/main` was the same commit and the worktree was clean. Baseline restore/build passed, 625/625 tests passed, DocConsistency passed and `git diff --check` passed. The active/default behavior policy remained `legacy-experimental.1`.

D1 remains historical **COMPLETE/C/PARKED**, `behaviorChange=true`, no promotion. D1.SAFETY remains historical **COMPLETE/C/PARKED**, `behaviorChange=false`. Neither phase was rerun, repaired or reinterpreted. No withheld D1 metrics or historical outputs were read.

## 9–11. Problem, mutation boundary and mutable-state audit

Final snapshot difference could not supply the causal provenance D1.SAFETY needed. SAFETY.PROV therefore records exact causal history during generation.

The audited mutation boundaries are:

1. A pass-1 tap or LN is selected, then `added.Add` and `geometry.Insert` form one domain insertion mutation.
2. Failed saturated interior placement can append an articulation intent. It is a decision/no-geometry state transition because it affects the later articulation pass.
3. Articulation selects a parent and atomically projects parent removal plus two replacement segments into final chart state.
4. `OsuBeatmap.Write` performs serialization outside the engine. A separate read-only layer links generated semantics to millisecond and reparsed representations.

Mutable decision-relevant state is geometry/added objects, articulation replacements, pending articulation intents, RNG state and opportunity cursor. Options, timing map, original analysis, mapper profile and precomputed opportunities are immutable run inputs. Statistics/timers do not drive decisions. The production geometry index is intentionally not rewritten during articulation; `ManiaChart.AllObjects` performs the final replacement projection.

## 12–19. DecisionEvent, MutationEvent, IDs and continuity

`GenerationDecisionEvent` represents placement, probability/no-legal abstention, experimental abstention, articulation skip/replace and materialization. A decision may produce zero, one or multiple physical mutations. `GenerationMutationEvent` records the physical/domain delta with stage, kind, exact candidate/parent, lane, beat interval, policy, state identities, RNG positions, divergence/ancestor IDs and violation IDs.

IDs are deterministic SHA-256 derivations from semantic run identity, event sequence, exact opportunity and kind. `SequenceIndex` preserves execution order and distinguishes same-timestamp events. No UUID, timestamp, thread, address or machine path enters identity.

Frozen stages are `Pass1BaseOpportunity`, `InteriorOpportunity`, `Articulation` and `Serialization`. Mutation kinds are `InsertTap`, `InsertLongNote`, `ArticulationReplace` and `SerializationMaterialization`. Existing pass-1 opportunity identity is reused; articulation uses exact parent sequence because it has no natural pass-1 opportunity.

The validator requires contiguous ordered IDs and same-stage StateAfter→StateBefore continuity. The real-engine trace passed. Stage transition changes RNG domain but not chart state and remains explicit rather than being hidden as a fake mutation.

## 20–28. State, RNG and opportunity provenance

`GeometryStateHash` canonically hashes materialized lane/start/end/kind/sequence/origin/synthetic tuples in ordinal order. `GenerationStateHash` additionally commits to root and stage RNG positions, opportunity cursor, pending articulation set and immutable opportunity-sequence hash. This separates geometry equivalence from complete generation-state equivalence.

The recorder reads `IRandomPositionSource.CallCount` when available; it never calls RNG. Exact causal lineage abstains when a required RNG position or hidden-state component is unavailable. Engine certification used positioned transcript RNGs, so the lineage state was complete.

## 29–35. Causal origin and safety integration

Stage and causal origin are independent. Legacy is proven only by a recorded legacy mutation, never by “output absent from source”. Treatment-direct is the exact paired decision that first changes state. Treatment-downstream requires a later unequal complete parent state descending from that divergence. Temporal order alone is insufficient.

Synthetic paired runs proved common prefix, exact first divergence, direct ID and downstream ancestry. A negative control proved that a later event after full reconvergence is not downstream. Geometry reconvergence with unequal RNG remains generation-state divergence. Incomplete hidden state, OpportunityKey mismatch or missing mutation provenance abstains as `Unattributable`.

The recorder integrates read-only with `GeometrySafetyAttributionResearch`. Before/after hard-violation sets use semantic signatures; introduced is `After - Before`, resolved is `Before - After`. Candidate evaluation uses explicit mutation provenance and preserves D1.SAFETY tap/LN/gap/parent semantics. Synthetic cases proved legacy, treatment-direct, treatment-downstream, articulation, serialization and unattributable outcomes. A violation already present before a later valid mutation was not re-attributed.

## 36–39. Neutral architecture, trace and work

The observer is append-only and returns no generation decisions. Normal overloads, CLI and Web remain unchanged; only the research overload accepts a recorder. No oracle result gates, rejects, admits, rerolls or changes candidates, lanes, LN shapes, chance, articulation or serialization.

Trace schema is `generation-provenance-shadow.1`, deterministic JSON without timestamps or paths. Large detail is stored at `.artifacts/safety_prov/generation-provenance-trace.jsonl` and ignored. Public files contain only contracts and small measured summaries.

The 7K articulation fixture emitted 21 decision events and 3 mutation events, performed 894 state-hash object-row operations and zero recorder RNG calls. No machine-time threshold was used. Safety-delta work is invoked only for research evaluation and never influences generation.

## 40–42. Contract, baseline identity and implementation snapshot

- SafetyProvContractHash: `3562A0A7746F0E6BFFD80E93B51E5D9ADAA6E646C607E9B8994E36F1AE264B8B`
- ImplementationSnapshotHash: `56E44EC366F088AD133C4EBC90D62A9EF76BED858EF1A3E56038F670B2FEEEC9`
- FixtureManifestHash: `3A1DF8814BCDBDF05970435BC9FC0B21B52349F89DA63979490A279D568792AD`

The certificate matched repository entry HEAD, branch, origin, clean initial state, contract and implementation snapshot. Negative tests reject head/origin/branch/contract/snapshot mismatch.

## 43–52. Synthetic matrix, bad controls and equivalence

The test matrix covers positive, negative and deliberately bad controls for event shape, no-mutation decisions, continuity, deterministic IDs, exact opportunity matching, direct/downstream/reconvergence, hidden RNG state, articulation, safety deltas, serialization and source immutability.

Bad controls correctly rejected a wrong StateBefore, sequence/identity corruption, downstream without an ancestor, fuzzy opportunity pairing, incomplete hidden state and missing provenance. Recorder RNG is structurally zero.

Deterministic engine fixtures covered 1K, 4K, 7K, 10K and 18K plus a saturated 7K articulation case. Across provenance OFF/ON, every fixture had exact:

- output bytes and final serialization;
- AddedObjects and order;
- articulation replacements;
- RNG transcript;
- opportunity and candidate sequences;
- placement/skip decisions;
- final geometry;
- source objects.

Two provenance-ON repetitions produced identical trace bytes. Trace SHA-256 was `2E13D042FA2CD95E72FAB8764FD36EA3527DA9BBD530BC9C3E7D48BBC7738226`.

## 53–56. Limitations, outcome and recommendation

The exact paired comparator is research infrastructure, not a production A/B framework. It requires exact OpportunityKey alignment and complete declared hidden state; otherwise it abstains. Exact reparse identity is available only for a one-to-one lane/start/end/type tuple. Ambiguous duplicate tuples or failed reparse remain `Unattributable`; no fuzzy matching is attempted. No mapper-style, population or human-safety claim is made from fixtures.

Outcome **A** is warranted because mutation/decision provenance is captured during real engine execution; behavior and RNG are exact OFF/ON; state continuity, deterministic IDs/traces, direct divergence, downstream ancestry, reconvergence, articulation and serialization boundaries are demonstrated; and all uncertainty paths abstain.

Outcome A means only: **mutation-level provenance infrastructure is technically viable**. It does not mean D1 is safe, should be rerun, or that another experiment is authorized.

**ROADMAP REVIEW REQUIRED.** SAFETY.PROV is the final authorized safety-infrastructure phase in this line. There is no automatic SAFETY.GATE, SAFETY.PROV.1, D1 rerun or next behavioral phase.

## 57–64. Preserved state, artifacts, validation and Git

Default remains `legacy-experimental.1`. D1 and D1.SAFETY remain unchanged COMPLETE/C/PARKED. F2 remains CONTINUE_CONDITIONALLY, F2.ACQ BLOCKED, E/E.1 COMPLETE/B/PARKED, C2 DEFERRED and MapperSupport NOT_AUTHORIZED. Community corpus was not touched.

Changed implementation consists of the generic provenance model/recorder/comparator/serialization linker, the observational engine overload and hooks, its synthetic tests, certification runner, DocConsistency guards and living documentation.

Public artifacts:

- `docs/safety_prov_contract.json`
- `docs/PHASE_SAFETY_PROV_DESIGN.md`
- `docs/safety_prov_synthetic_summary.csv`
- `docs/safety_prov_behavior_equivalence_summary.csv`
- `docs/safety_prov_causal_lineage_summary.csv`
- `docs/safety_prov_serialization_summary.csv`
- `docs/safety_prov_determinism_summary.csv`
- `docs/safety_prov_baseline_identity_summary.csv`
- this report.

Ignored detail: `.artifacts/safety_prov/generation-provenance-trace.jsonl`; `git check-ignore` confirms `.artifacts/`, and `git ls-files .artifacts/safety_prov` is empty.

Final regression: restore PASS; Release build PASS; **656 passed, 0 failed, 0 skipped**; DocConsistency PASS; `git diff --check` PASS. The worktree is intentionally dirty for human review. No `git add`, commit, push, tag or release was performed.
