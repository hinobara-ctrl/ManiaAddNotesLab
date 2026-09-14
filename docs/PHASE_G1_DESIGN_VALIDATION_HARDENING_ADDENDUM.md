# G1.DESIGN Validation Hardening / Recertification Addendum

**RECERTIFICATION PASS — G1.DESIGN COMPLETE / READY / SHADOW — behaviorChange=false.**

**READY denotes design readiness only. It is not behavioral promotion or authorization.** `G1.GATE` remains `NEXT CANDIDATE / NOT_AUTHORIZED`.

## Entry and frozen dependency

`RepositoryEntryHead` and `origin/main` were both `964831978da269ccc2ada56b6fed7fe6a895ac2c`. Baseline was 698 passed, 0 failed, 0 skipped. The original contract remained byte-semantically unchanged at canonical SHA-256 `15A16B6EFBF779CFF2C42A8C9A0DD46E025019252BEFA968E831233DC802AA68`. C11 fingerprint remained `878585D604E807B7E46AB429990C7DA6DB396AB0B7E930DAC2B631C8DE7ECD77`.

Final hardening implementation snapshot: `B0F6872A052F0A2A286F2AF317EC7CF2BE73AE2EC73082EFEF5116C94BF9443C`.

## Findings and corrections

The previous phrase **direct-effect potential** overclaimed what the shadow census measured. The 133 hypothetical ADMIT values are now described as **candidate-universe membership potential**: exact membership results over 4,226 release shapes enumerated from the real candidate builder. This phase did not execute mutable geometry or `FindLegalLnLanes`, select a seed-level proposal, or observe a post-geometry placement. The values are not output counts, behavioral effects, utility, or promotion evidence. The frozen contract retains its historical wording; the scientific membership question and mapping did not change.

The 87/66 query counts refer only to distinct chart-local exact queries reached by the current candidate-builder universe. The complete reached denominator is 247: 94 with no observed result, 87 with one and 66 with multiple. They are not every G1.0 query in C11.

## Synthetic source normalization

G1.DESIGN previously let G1.0 normalize internally but rebuilt its own profile, context and object lookup from the unnormalized `source.OriginalObjects`. The hardening creates one research-only normalized chart using `IsSynthetic == false && Origin == None`, then derives census, profile, context, identity and vocabulary from that same population.

An actual synthetic LN was injected into `source.OriginalObjects` for each of all 11 C11 charts. Clean and contaminated semantic projections produced the same hash: `5BFB4BD5DCEB14D915A428754F7B4ACF1AB891BEFC88F0E0F61BCBAFF3A21D2B`. The injected object could not alter profile/context, shift observation alignment, teach vocabulary or create membership support. Normal parsed C11 aggregates did not change.

## Construction evidence versus membership evidence

Exact attribution is available without heuristic identity. `BuildReleaseCandidatesForResearch` preserves each original `ManiaObject` route and its exact materialized endpoint. The normalized chart maps that same object reference to `OriginalObservationId`; routes are associated with a candidate only by exact `MaterializedEndTime`. Membership witnesses remain G1.0 `WitnessLongNoteId` values.

Among the 133 memberships, 107 had membership support only from observations also used to construct the shape, 15 had independent-only support, and 11 had both construction-overlap and independent support. None were unattributable. This provenance is descriptive and does not admit, reject, rank, weight or select.

## RNG certification

The earlier literal `RngCalls=0` fields are schema-compatible diagnostic metadata, not proof. The recertification basis is structural: the membership public API accepts no `IRandomSource`, the DESIGN enumeration calls the candidate builder without weighted selection, and repeated C11 evaluation produced the same semantic candidate hash. A future runtime gate must independently certify its own RNG position.

## Adversarial controls

Real-evaluator controls actually inject synthetic authority, cross-chart authority, A≫B frequency skew, marginal fragments and an exact mismatch. The real evaluator ignored or rejected every forbidden input while preserving the minority exact member among alternatives.

Candidate replacement, articulation routing and RNG-position divergence have no productive G1 runtime surface yet. Their injected trace checks are therefore classified only as **design-contract adversarial controls**, not certification of future runtime behavior. G1.GATE must later exercise any authorized runtime path itself.

## Opportunity distribution

Across the 298 current opportunities, 185 had zero admitted shapes and 113 had at least one. All 298 had at least one enumerated candidate. There were 185 all-abstain opportunities, 113 mixed admit/abstain and zero all-admit. Admitted-shape counts per opportunity were: 0→185, 1→94, 2→18, 3→1. These are descriptive candidate-universe counts; no weighted selection or seed simulation was performed.

## Aggregate reproduction

Every historical core aggregate reproduced exactly:

- opportunities 298; shapes 4,226; representable 4,226;
- Unique 76; AmongAlternatives 57; NotObserved 2,907; NoRelation 1,186; Unresolvable 0;
- hypothetical ADMIT 133; ABSTAIN 4,093;
- SameParentOnly 91; OtherParentOnly 15; Both 27; None 0;
- Contained 1,877; Crossing 2,221; EqualEnd 128;
- semantic candidate hash `19E86D5C8C358B07CBE2E890EC6655229DB6AF9CC1670866E085E2B9A4FCB24E`.

## Tests and scope

The original G1.DESIGN file has 12 test methods and 14 materialized xUnit cases; its assertions jointly cover the 20 requested semantic invariants rather than mapping one test to each item. Hardening adds five test methods/cases for contaminated source normalization, exact construction provenance, explicit unattributable state, opportunity aggregation and structural RNG purity. Final full-suite result: **703 passed, 0 failed, 0 skipped**.

There is no productive membership callsite, toggle, option, A/B, treatment, reroll, eligibility/lane/ranking change, articulation behavior, G2/H behavior, AddChance change or default change. `BehaviorPolicyVersion` remains `legacy-experimental.1`.

## Decision

**RECERTIFICATION PASS.** The narrow `identity → membership` claim survives hardening. Reconstruction still does not imply augmentation, and membership still does not imply preference, safety or utility. G1.DESIGN remains `COMPLETE — READY / SHADOW`; G1.0 remains `COMPLETE — OUTCOME A / SHADOW — RECERTIFIED`.

**G1.GATE remains NOT_AUTHORIZED. Do not continue without a separate human authorization and prompt.**
