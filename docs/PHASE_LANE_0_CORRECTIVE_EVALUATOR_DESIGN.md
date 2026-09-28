# LANE.0 — Corrective evaluator core design

Date: 2026-09-28
Preparation baseline: `3c89e0b42d4e26509381f40218e3d5304fb2c5ac`
State: **EVALUATOR CORE IMPLEMENTED / SYNTHETICALLY VALIDATED / EXECUTION BLOCKED**

## Scope and authority

This addendum implements only the deterministic, research-only, in-memory core preregistered after the LANE.0 future-held correction. It does not load a corpus, discover files, expose a CLI route, create an authorization binding, or grant human authorization. C11 was not resolved, enumerated, opened or evaluated. No `.osu` input was read.

The published preparation baseline and all historical identities remain dependencies, not editable inputs. The parent preregistration canonical identity is `6392C579B87EC318C1DE381201D797B004650AB961398E3E2AB23F7A49A83D33`; instrument v3 remains `221D5133D8058FEBEAEA0A13F83058219D900261B033282EE389A82BA20FAAF7` with implementation `2F938F18D98C6299214C080195633A68B68C688CABF49DC0F4DEFFCA9C1897D9` and harness `1EFC2B824FAA6F0798C3FFA679FCA073939D88B2C751612C06BB75C240E96309`. The preparation harness remains `AC1DD7469AC2876B1A31226DDB311CDF74F5D64ABEACC6C442C461F5F87964E3`.

## Architecture

`Lane0CorrectiveEvaluationRunner` accepts already-loaded `ManiaChart` objects and options. For each chart it:

1. records the immutable mapper-evidence fingerprint;
2. rebuilds the frozen RICE occurrence representation from `ChordCompletionResearch.Evaluate`;
3. evaluates `InteriorRelationFeasibilityResearch` once;
4. derives historical G1 with `reproduceHistoricalDefect: true` and `requireTemporalIntegrity: false`;
5. derives corrected G1 from the same census with `reproduceHistoricalDefect: false` and `requireTemporalIntegrity: true`;
6. pairs occurrences strictly by ordinal `OccurrenceId`;
7. rejects all structural drift except `AnchorTime` and `FutureHeldObservationIds`;
8. derives operational universe C from `CurrentGateAudit.CurrentOpportunity` before probability, proposal or placement;
9. calculates per-occurrence transitions, per-chart/family summaries and global family criteria;
10. validates the historical snapshot, RICE sentinel, eleven-case rule, zero RNG and input fingerprint;
11. serializes semantic artifacts deterministically and, only when explicitly asked, writes them to a caller-supplied output directory.

No adapter exists between this core and C11. No call site was added to the experiment CLI.

## Frozen research questions

- **RQ1 — Global recertification:** whether both RICE and corrected G1 still pass the frozen feasibility criterion.
- **RQ2 — G1 defect magnitude:** unchanged and changed states/support over the historical structural universe, without post-observation thresholds.
- **RQ3 — Difference causes:** temporal, future-held identity, overlap, other barriers and loss of context with a valid donor partition.
- **RQ4 — Eleven historical G1 cases:** exact deterministic reconstruction, distribution 9+2 over the two frozen chart hashes, 7K only, and exact-ID pairing.
- **RQ5 — G1 generality:** corrected independent joint support in at least two charts and two keymodes, computed from A/B rather than the eleven C cases.
- **RQ6 — Operational universe C:** operational and operational-supported counts kept separate from proposals and placements.
- **RQ7 — RICE sentinel:** exact reproduction of the historical RICE snapshot.
- **RQ8 — G1 transition matrix:** every historical state to corrected state transition, without normative interpretation.
- **RQ9 — Reproducibility:** byte-identical outputs over identical loaded inputs, zero RNG and unchanged chart fingerprints.

## Historical guards

The future authorized invocation must first reproduce the complete frozen snapshot in the successor contract. Any mismatch is `INVALID` before scientific classification. RICE historical and corrected summaries must also be exactly equal.

The eleven-case resolver selects historical occurrences that are both operational and independently supported, sorts exact IDs ordinally and validates total, chart distribution and keymode. A count, distribution or keymode mismatch adds a blocking reason; a missing corrected occurrence is an identity-set integrity failure. The core does not invent or freeze placeholder identities.

## Transition and operational schemas

`g1_state_transitions.csv` contains:

`chart_id,keymode,occurrence_id,group_id,historical_state,corrected_state,historical_joint_supported,corrected_joint_supported,historical_comparable_donors,corrected_comparable_donors,historical_matching_joint_donors,corrected_matching_joint_donors,temporal_exclusion,future_held_identity_exclusion,both_temporal_and_identity,unique_future_held_excluded,unique_excluded,historical_operational,corrected_operational,transition_kind`

The in-memory result also carries the deterministically sorted state-pair/count matrix. `chart_family_keymode_results.csv` keeps structural, holdout, context, all state populations, marginal-only, independent support, operational, operational-independent support and integrity counts for both branches. The scientific JSON includes family criteria and the four distinct universe-C quantities. No field represents a placement count.

The future private semantic set is:

- `scientific_summary.json`
- `chart_family_keymode_results.csv`
- `g1_historical_operational_cases.csv`
- `g1_state_transitions.csv`
- `integrity.json`
- `sha256sums.txt` (which excludes itself)

All text uses UTF-8 without BOM and LF, ordinal ordering and no timestamp, GUID, machine name or absolute path.

## Classification

Classification precedence is exact:

1. `INVALID` for identity, contract, structural equivalence, historical snapshot, integrity, deterministic-byte, RNG, non-interference or RICE-sentinel failure;
2. `BLOCKED` when a prerequisite or the exact eleven-case reconstruction is unavailable;
3. `FEASIBILITY_DEMONSTRATED` only for a valid, complete and authorized evaluation in which both families pass;
4. `LIMITED_PARK` for a valid, complete and authorized evaluation in which either family fails the frozen support criterion.

Each family must have independent joint support in at least two charts, at least two keymodes and at least one operational target. There is no new threshold and no behavioral authority.

## Synthetic validation and non-interference

A separate SDK test project was necessary because the existing test project file is part of the frozen preparation/v3 identities and therefore could not be edited. The new project links the immutable feasibility runner and the new evaluator without modifying either frozen test inventory.

The 32 passing cases cover exact-ID pairing, allowed and forbidden drift, missing/duplicate identities, prior/same-time/future donors, temporal/identity overlap, unrelated barriers, partition arithmetic, state transitions, unchanged support, eleven-case acceptance and bad controls, universe-C separation, RICE sentinel, all four classifier outcomes, zero RNG, fingerprint preservation, deterministic bytes, closed artifact inventory and explicit-directory checksum writing. Fixtures and IDs are synthetic.

## Frozen identities

- Successor contract canonical SHA-256: `074B84F57D0ED3870C744ABCD1F00E2BA9B9C587F917A9823C76F46D06DF01EB`
- Evaluator implementation identity: `9CA35582CCB70B5D85434373709D511A3B896BCDF1353137F8FE6724DBA53128`
- Evaluator harness identity: `F00289DF36021C0211F3EE888A1EFF0D332390F1219EFC3391EEF487D9CC63D0`

The identities use strict UTF-8, optional BOM removal, CRLF/CR-to-LF normalization, per-file SHA-256, ordinal `path|hash` inventory rows and SHA-256 of the LF-joined inventory.

## Limits and future authorization boundary

This work produces no corrected C11 counts, real transition matrix, real supported chart/keymode values or real eleven-ID list. Global LANE.0 certification remains suspended.

A separate future task, after manual publication and independent audit, must create a publication binding to the published evaluator SHA, obtain explicit human authorization, implement exactly one corpus adapter, verify the canonical manifest and all eleven content hashes, resolve the corpus, invoke this in-memory core exactly once officially, privately publish artifacts and close the scientific result. None of those steps is authorized or implemented here.

The evaluator must not be used for lane preference, scoring, selection, probability, geometry, LANE.DESIGN or behavioral promotion.
