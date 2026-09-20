# Phase SAFETY.CAUSAL — Downstream Hard-Validity Causality Forensics

**Status: COMPLETE — OUTCOME A / CAUSE FOUND / NO REMEDIATION / NO PROMOTION.**  
**Scientific classification:** `LEGACY_GENERAL_CAUSE_FOUND` + `ORACLE_OR_SEMANTIC_MISMATCH_FOUND`.  
**Behavior boundary:** `behaviorChange=false`, `defaultBehaviorChange=false`, `generationSemanticsChange=false`.

## Question and frozen boundary

This phase explains why runtime placement accepted the nine G1.GATE treatment-only `TapOnHeldLongNote` relations that canonical HardValidity later rejected, and whether the same mechanism explains the 200 hard-validity conditions in legacy control. The machine contract was frozen before certification at SHA-256 `300E879BCB479F77A704BFFB89BF304D9D04ECC556067F921C49C45E4E8FCB84`, with repository entry HEAD `f54c3be692fe4f9bfeffe2dc892297e61cf327c1`.

The phase adds research-only inspection, replay, tests and artifacts. It does not change G1 membership, candidate construction, lane selection, AddChance, density, eligibility, articulation, serialization, CLI/Web defaults or output behavior. No remediation is implemented.

## Result

The complete frozen C11 replay reproduced exactly 9/9 treatment-only violations in four runs and 200/200 legacy control conditions. Every one is the same exact causal family:

1. A generated LN is committed with an integer `EndTime` and a slightly smaller latent decimal `EndBeat`.
2. A later generated tap is evaluated in that lane at a decimal beat microscopically greater than the latent LN end, so runtime placement accepts it.
3. The LN end and tap head both materialize to the same integer millisecond.
4. Canonical HardValidity reconstructs both beats from those integer milliseconds and therefore sees equality; its inclusive `previous.EndBeat >= tap.StartBeat` rule reports `TapOnHeldLongNote`.

The operators do **not** differ. Runtime and oracle both treat equality as occupied. The disagreement is representation: runtime geometry observes the retained pre-materialization decimal, while the final oracle observes canonical millisecond-derived beats.

All 209 conditions were accepted by the real `AddNotesEngine.PlaceTap → LaneGeometryIndex.FindLegalTapLanes → CanPlaceTap` path. In all 209, `internalConflict=false`, `materializedConflict=true`, `placementAccepted=true`, `endpointOperatorDiffers=false`, `geometrySnapshotWasStale=false` and `serializationChangedCoordinates=false`.

## Earliest semantic divergence

The earliest divergence is candidate materialization in `AddNotesEngine.BuildReleaseCandidates`. `AddDuration` converts `intendedEndBeat` to `rawTime`; `Add` then retains `intendedEndBeat` whenever `endTime == rawTime`. Equality of the rounded millisecond does not imply equality of the original decimal beat. The mutable `LaneGeometryIndex` stores that latent decimal alongside the already-materialized `ManiaObject.EndTime`.

The writer is not the cause. For every violating pair, the in-memory `ManiaObject.EndTime` already equals the tap `StartTime`; write/reparse preserves lane, type, start and end. There is no stale cache: replay of the immediately preceding lane neighbors shows the blocking synthetic LN is the exact previous object consulted.

## Nine treatment-only cases

| Chart | Seed | Tap time | Lane | Introducing mutation | Immediate G1 divergence | Full reconvergence before mutation |
|---|---:|---:|---:|---|---|---|
| `02D9…E3EB` | 9 | 257700 | 0 | `MUT-00006492-DA9C91220AE9BCEF31E0` | `DIV-AA18CDEAB9F1EBEE5A452BE6` | no |
| `02D9…E3EB` | 11 | 163268 | 2 | `MUT-00003822-CC99007D99D22963EFCE` | `DIV-C65C335AAD8DBBBCA669A6DC` | no |
| `02D9…E3EB` | 11 | 166421 | 2 | `MUT-00003926-FC170273B84CB46C9AB3` | same | no |
| `02D9…E3EB` | 11 | 168296 | 6 | `MUT-00003985-CB1665D21CA392E2D329` | same | no |
| `02D9…E3EB` | 17 | 197189 | 4 | `MUT-00004588-315651C8D435FD853633` | `DIV-085E822B4965F23D850AEEC7` | no |
| `02D9…E3EB` | 17 | 200427 | 5 | `MUT-00004692-60B5AFDE31CA1D7621C2` | same | no |
| `02D9…E3EB` | 17 | 204433 | 1 | `MUT-00004818-8661C7AC634CA6E6E988` | same | no |
| `02D9…E3EB` | 17 | 221819 | 2 | `MUT-00005391-025820A4F24EB3408680` | same | no |
| `2065…C788` | 11 | 266968 | 3 | `MUT-00006726-0CB20C176C0A2140843B` | `DIV-26132A17713DC6BE99AE07EE` | no |

The per-case CSV/JSON records the full opportunity, source/anchor, candidate, blocker, pre/post GeometryState and GenerationState, RNG positions, both beat representations, placement result, oracle result and reconvergence fields. Opportunity numeric IDs are not execution order: exact event sequence proves every introducing tap occurred after its G1 ancestor. None of the four treatment traces fully reconverged before its violation.

## Control classification and same-mechanism test

The 200 control conditions occur in 39 runs across the same two chart families. Source-state audit proves zero are source-pre-existing. Mutation replay maps every condition to a later generated tap and an earlier generated blocking LN: `LegacyDownstream=200`, `LegacyDirect=0`, `SourcePreExisting=0`, articulation/transformation/serialization-derived=0 and unattributable=0.

All 200 reproduce the same latent-decimal/materialized-millisecond disagreement as the nine treatment-only cases. Therefore G1 suppression is not the root mechanism. G1 changes GenerationState and exposes nine additional path-dependent instances of an already-existing legacy-general semantic mismatch. This refines, but does not rewrite, the historical G1.GATE result.

## State, ancestry and instrumentation audit

`GenerationState` includes current geometry, RNG positions, opportunity cursor and pending articulation identity; no additional behaviorally mutable cache was found in this path. Downstream requires exact paired opportunity alignment, a governed direct divergence, unequal complete parent state, strict later execution order and no prior full-state reconvergence. Geometry-only or RNG-only equality is not full reconvergence.

The first certification run exposed four missing provenance transitions in two treatment traces: G1 abstentions used the post-candidate gate snapshot as the decision `StateBefore`, omitting roll/placement work since `provenanceBefore`. The research recorder was corrected to use the full-opportunity pre-state, matching all other decisions; G1 diagnostics retain the exact gate snapshot separately. Replayed outputs, findings and RNG transcripts stayed identical, and all eight control/treatment traces then passed validation. This was an instrumentation repair, not generation remediation.

The recorder consumes zero RNG. Instrumented and uninstrumented runs are byte-identical with identical RNG transcripts. Repeated artifact projection is deterministic.

## Minimal counterexample and adversarial controls

The minimal deterministic fixture contains one LN with materialized end `500 ms` and latent end `0.99999999999999999999999999 beats`, followed by a tap at beat `1` / `500 ms`. Real `LaneGeometryIndex` accepts it because the latent end is smaller; real HardValidity reports `TapOnHeldLongNote` after millisecond normalization.

Dedicated bad controls cover: temporal-after mistaken for downstream; ancestry retained after full reconvergence; incomplete hidden state treated as complete; deliberately different endpoint representation; source-pre-existing mislabeled legacy-introduced; later mutation outside cleared ancestry mislabeled G1 downstream; output-only attribution; RNG-consuming instrumentation; stale geometry; and serialization-only evidence mislabeled runtime mutation. The real runtime surfaces are used where available.

| Required bad control | Result | Evidence surface |
|---|---|---|
| 1. temporal-after called downstream | PASS | pair comparator + cleared lineage |
| 2. ancestry retained after full reconvergence | PASS | pair comparator reconvergence set |
| 3. hidden/incomplete state called reconverged | PASS | incomplete `GenerationState` forces abstention |
| 4. different endpoint semantics ignored | PASS | real `LaneGeometryIndex` and HardValidity fixture |
| 5. source-pre-existing called legacy-introduced | PASS | safety delta before/after sets |
| 6. mutation outside cleared G1 ancestry called downstream | PASS | post-reconvergence unrelated divergence |
| 7. output-only mutation attribution | PASS | missing provenance remains `Unattributable` |
| 8. instrumentation mutates generation/RNG | PASS | transcript/call-count detector and real OFF/ON G1 replay |
| 9. stale GeometryState justifies legality | PASS | mutable index before/after insertion |
| 10. serialization-only difference called runtime mutation | PASS | serialization-stage attribution remains distinct |

Positive tests cover the minimal runtime mismatch, exact endpoint negative, G1 abstain trace continuity/non-interference, frozen contract determinism, all four treatment runs, all nine unique mutation IDs, exact treatment/control counts, state/reconvergence fields, every discovered causal family, and public artifact classification.

## Candidate remediations — documented, not implemented

| Candidate | Mechanism addressed | Semantic impact and risks | Required validation |
|---|---|---|---|
| Canonicalize candidate beats from materialized milliseconds before geometry insertion | Removes dual representation | Changes placement availability, RNG path after altered accepts/rejects, density and output; compatible with frozen G1 membership only if proposal identity semantics are reviewed | full legacy/G1 A/B, RNG/path deltas, all endpoint fixtures, human review |
| Canonical pre-commit HardValidity check | Prevents invalid commit even if builders diverge | Adds a new veto; changes output/path and may change later RNG-independent geometry decisions | complete replay, rejected-candidate provenance, density/path review |
| Unify geometry and oracle around a shared endpoint value object | Prevents future semantic drift | Broad refactor; serialization/timing/redline risks; behavior likely changes where latent and materialized values differ | equivalence suite across timing points, all keymodes, serialization roundtrip, corpus diff |
| Preserve latent values only as evidence, never as collision authority | Separates inference precision from playable geometry | Candidate scoring may remain stable but collision decisions change | candidate identity stability plus full behavioral certification |

Changing `>=` to `>` is not an evidenced fix: both authorities already agree on inclusive equality, and making the oracle permissive would redefine safety merely to hide the mismatch.

## Reproduction

```powershell
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- safety-causal-contract docs/safety_causal_downstream_hard_validity_contract.json
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- safety-causal-run src/ManiaAddNotesLab.Web/batch-results docs .artifacts/safety_causal
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- safety-causal-validate-traces .artifacts/g1_gate_forensic
```

The full runner regenerates the treatment CSV/JSON, control classification CSV/JSON, combined case JSON, placement-vs-oracle CSV, family summary and phase summary. It is intentionally expensive because it replays all 220 controls and instruments every affected run.

## Validation record

- Entry branch/identity: `main`; `HEAD == origin/main == f54c3be692fe4f9bfeffe2dc892297e61cf327c1`.
- Baseline before edits: restore PASS with the known offline NuGet `NU1900` warning; build PASS; **710/710 tests**; DocConsistency PASS; `git diff --check` PASS.
- Frozen contract: `300E879BCB479F77A704BFFB89BF304D9D04ECC556067F921C49C45E4E8FCB84`.
- Full forensic certification: Outcome A; 220 controls; 39 affected control runs instrumented; 4 treatment pairs; 209 case rows; 0 non-interference failures; 0 trace-validation failures; recorder RNG calls 0; deterministic repeat true.
- Critical trace replay: all eight control/treatment traces valid after the instrumentation boundary correction.
- Final verification: restore PASS with only `NU1900`; Release build PASS; **723/723 tests**, 0 failed, 0 skipped; DocConsistency PASS; public artifact republication byte-identical; `git diff --check` PASS.
- Repository hygiene: the pre-existing untracked `docs/ASTRA_ROADMAP_V2_PROPOSAL.md` was not read, edited or included. No staging, commit, push, tag or release was performed.

## Closure and authority

SAFETY.CAUSAL closes the authorized forensic question with Outcome A. It does not repair the cause and does not make G1.GATE safe or useful. G1.GATE remains `COMPLETE — NEEDS_REVIEW / NO PROMOTION`; default remains `legacy-experimental.1`; G1 behavioral utility, G2, H and every successor remain `NOT_AUTHORIZED`. A separate human-authorized remediation design would be required before any behavior change.
