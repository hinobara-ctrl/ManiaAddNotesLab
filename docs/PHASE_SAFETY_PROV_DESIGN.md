# SAFETY.PROV — frozen pre-certification design

Status at freeze: `ACTIVE / RESEARCH / SHADOW`, `behaviorChange=false`.

This certificate was frozen before the SAFETY.PROV certification runner was executed. Unit and implementation-fixture tests used while constructing the instrumentation are synthetic implementation evidence, not mapper or behavioral evidence.

## Baseline identity

- Repository entry HEAD: `dc8a557d22d0f2a07668541cfcbb912afbf1704b`
- Branch: `main`
- `origin/main`: `dc8a557d22d0f2a07668541cfcbb912afbf1704b`
- Initial worktree: clean
- Baseline: restore PASS; Release build PASS; 625/625 tests PASS; DocConsistency PASS; `git diff --check` PASS.
- SafetyProvContractHash: `3562A0A7746F0E6BFFD80E93B51E5D9ADAA6E646C607E9B8994E36F1AE264B8B`
- ImplementationSnapshotHash: `56E44EC366F088AD133C4EBC90D62A9EF76BED858EF1A3E56038F670B2FEEEC9`
- FixtureManifestHash: `3A1DF8814BCDBDF05970435BC9FC0B21B52349F89DA63979490A279D568792AD`

Snapshot files, in canonical ordinal order, are `AddNotesEngine.cs`, `GenerationProvenanceResearch.cs`, `GeometrySafetyAttributionResearch.cs`, `LaneGeometryIndex.cs`, `Model.cs`, `OsuBeatmap.cs`, the SAFETY.PROV test file, and the experiment program/runner. File contents, not timestamps or paths outside the repository, define the snapshot.

## Frozen instrumentation boundary

Normal overloads, CLI, Web, defaults and generation policies remain unchanged. A research-only overload accepts an append-only recorder. The observer has no RNG, returns no decisions, and receives copied semantic values. Its output may differ; generation output may not.

The audited state-changing boundaries are:

1. Pass-1 tap insertion: candidate selection, then the composite `added.Add + geometry.Insert` mutation.
2. Pass-1 LN insertion: candidate/lane selection, then the same composite insertion.
3. An interior placement failure may append an articulation intent. This changes hidden future-generation state but not chart geometry, and is represented by a decision state transition with no physical mutation.
4. Articulation is an atomic domain replacement: one identified original parent becomes two identified segments. The production geometry index is not rewritten during this pass; the final `ManiaChart` projection applies the replacement.
5. `OsuBeatmap.Write` materializes millisecond objects outside `AddNotesEngine`; a separate read-only linker follows generated semantic tuple → serialized tuple → exact reparsed tuple.

No other safety-relevant chart mutation was found. Statistics and performance counters do not feed generation decisions. Original analysis, evidence profile, timing points, options and precomputed opportunity order are immutable run inputs.

## Event and identity model

`GenerationDecisionEvent` permits zero, one or multiple later physical mutations. `GenerationMutationEvent` records the exact physical/domain mutation. Both have a monotonic `SequenceIndex`; beat/time never substitutes for causal order. Existing `OpportunityKey` semantics are reused for pass 1, while articulation uses its exact parent sequence because it has no natural pass-1 opportunity.

Decision, mutation and divergence IDs are SHA-256-derived from semantic run identity, sequence and exact opportunity. No UUID, timestamp, thread, memory address or machine path is allowed. Generated-object identity is derived from mutation identity plus the complete semantic object tuple.

## State model and hidden-state audit

`GeometryStateHash` canonically hashes sorted materialized object tuples: lane, serialized start/end, kind, original sequence, origin and synthetic flag. It excludes metadata unrelated to safety.

`GenerationStateHash` additionally commits to root RNG position, current stage RNG position, opportunity cursor, pending articulation-intent set and immutable opportunity-sequence hash. Exact causal lineage is permitted only when both RNG positions are available. Geometry equality alone is not full generation-state reconvergence.

StateBefore/StateAfter must link successive events in a stage. Articulation stage transition changes RNG domain but not chart geometry and is explicit in stage identity. Any unrecorded state-changing operation is a hard failure.

## Causal lineage model

Paired comparison is separate from the single-run recorder. Pairing requires identical ordered OpportunityKeys; fuzzy or list-position matching is forbidden. The first exact decision/state delta produces `ExperimentalDivergenceId` and is direct. A later event is downstream only while its complete parent generation state remains unequal and descends from that exact divergence. Merely occurring later is insufficient. Exact full-state reconvergence clears ancestry; geometry reconvergence with unequal RNG does not.

Missing mutation provenance, incomplete hidden state, ambiguous pairing or ambiguous reparse mapping produces `Unattributable`, never a confidence score or guessed cause. Stage and causal origin are independent: an articulation-stage event may be legacy or treatment-downstream.

## Safety and serialization integration

Mutation deltas reuse `GeometrySafetyAttributionResearch`: hard violations are audited before/after by semantic signature, and candidate attribution uses explicit mutation provenance. `Introduced = After - Before`; `Resolved = Before - After`. Existing conditions remain pre-existing relative to the mutation.

Serialization linkage is one-to-one only on the exact lane/start/end/type tuple. An invalid millisecond duration with exact mutation provenance is `SerializationIntroducedViolation`. Ambiguous/reparse-failed cases abstain. No metadata is written into `.osu` files.

## Synthetic certification matrix

The frozen matrix covers legacy valid/invalid insertion; direct divergence and direct violation; experimental abstention; proven downstream; temporal-after negative; reconvergence; RNG non-reconvergence; incomplete hidden state; articulation and parent relation; serialization valid/invalid/ambiguous; missing provenance; pre-existing conditions; continuity; deterministic IDs/traces; exact opportunity matching; same-timestamp events; zero recorder RNG; read-only/source unchanged; and 1K/4K/7K/10K/18K engine fixtures.

Bad controls must reject wrong StateBefore, sequence gaps/duplicates, downstream without ancestor, invalid/random IDs, missing provenance with forced causality, non-zero recorder RNG, fuzzy matching and baseline/certificate mismatch.

## Behavior-neutrality acceptance

For provenance OFF versus ON, the following must be exact: output bytes, added objects/order, articulation replacements, RNG transcript, opportunity sequence, candidate sequence, placement/skip decisions, final geometry and serialization. Two identical ON runs must have identical trace bytes. Source objects must be unchanged. Any drift is Outcome C and STOP.

## Outcomes and hard aborts

- A: exact mutation/decision provenance, direct/downstream ancestry, reconvergence, articulation and serialization boundary are technically viable with zero behavior effect.
- B: neutral and useful, but exactly one important causal frontier remains; name the smallest prerequisite and park.
- C: exactness requires behavior/RNG drift, heuristic ancestry, nondeterministic identity, unsafe production coupling or incomplete mutation coverage.

Hard aborts include behavior/default/D1 drift, any recorder RNG, changed opportunities/candidates, guessed downstream, causal labeling without provenance, source mutation, fuzzy serialization identity, unstable contract/snapshot/trace, community data, D1/D1.SAFETY reinterpretation, or failed DocConsistency.

Forbidden interpretations: this phase does not establish D1 safety, authorize or rerun D1, publish withheld D1 metrics, use C11/community evidence, change mapper evidence, or authorize a behavioral phase. D1 and D1.SAFETY remain COMPLETE/C/PARKED; `legacy-experimental.1` remains default. Regardless of outcome, the only recommendation after closure is `ROADMAP_REVIEW`.
