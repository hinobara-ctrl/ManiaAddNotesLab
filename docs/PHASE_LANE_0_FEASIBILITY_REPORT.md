# LANE.0 — Original-only spatial relation feasibility report

## Closure

**COMPLETE — FEASIBILITY_DEMONSTRATED / RESEARCH ONLY / NO BEHAVIOR AUTHORITY.**

Both preregistered exact families satisfy the frozen criterion: independent joint support occurs in at least two charts and two keymodes, supported occurrences reach the current operational universe, integrity failures are zero, RNG use is zero and the repeated pure evaluation is byte-identical. This establishes representational feasibility only. It does not select a lane, estimate human utility, authorize a probability, promote G1 or open `LANE.DESIGN` automatically.

## Identity and preregistration

- Approved entry HEAD: `be08f3f2f0191a5c972ad16449a7199dd07f2e3f` on local `main`.
- Remote synchronization was attempted during preflight but GitHub was unavailable over port 443; remote equality is not claimed. The user confirmed this published HEAD and explicitly authorized LANE.0.
- Final contract: `docs/lane_0_feasibility_contract.json`.
- Canonical contract SHA-256: `62B2F4F67C34E8F57893A3A025D567000E1BB12B6517C13D97FE9EEC3FE0A3C2`.
- Frozen C11 manifest: `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`.
- Corpus: 11 unique contents, 12 explicit locations and one exact duplicate under `.artifacts/f2-1-corpus`; every individual SHA and manifest field passed.
- Heap cap: `DOTNET_GCHeapHardLimit=0x400000000`; execution sequential with `DOTNET_GCConserveMemory=9`.

Two preregistration attempts were rejected before resolving or evaluating any C11 chart because the contract round-trip hash was unstable. They produced no corpus observation or scientific result. The final canonicalization hashes a stable, unindented semantic JSON round-trip and has a dedicated fixture. Only contract `62B2…A3C2` governed the official evaluation.

## Primary question and frozen outcome rule

The primary question asked whether both exact complete original-only families recur under strict chart-local holdout in at least two charts and two keymodes, including at least one independently supported operational occurrence per family, with zero integrity failures, RNG or behavior change.

The outcome rule was frozen before C11:

- `FEASIBILITY_DEMONSTRATED`: both families satisfy every criterion.
- `LIMITED_PARK`: valid instrument/integrity but at least one family misses a criterion.
- `BLOCKED`: required data or identity cannot be reconstructed.
- `INVALID`: preregistration, identity, controls, determinism or no-interference fails.

No threshold or identity changed after the official result was observed.

## Representation and universes

`RICE_HEAD_COMPLETION` reuses D0 whole-group holdout: the query is the exact reduced simultaneous head state and the complete result is target lane plus head type. `G1_INTERIOR_SPATIAL` reuses the complete parent/anchor/witness occurrence: the query is the exact temporal G1 query plus parent lane; the complete result joins temporal relation, witness lane, exact witness-parent delta and same-lane flag from one occurrence.

Mirror, translation, proportional normalization and cross-keymode delta equivalence were not used.

| Universe | Rice | G1 interior |
|---|---:|---:|
| A — complete structural original occurrences | 40,360 | 16,881 |
| B — strict holdout trials | 40,360 | 16,881 |
| B — any comparable independent context | 39,596 | 9,540 |
| B — independent joint support | 37,080 | 3,373 |
| C — current operational occurrences | 40,360 | 288 |
| C — independently supported operational occurrences | 37,080 | 11 |

The universes retain independent denominators. Rice A and C have equal counts because every original head has a base opportunity before chance/density; that equality does not predict a successful roll or placement. G1 C is the subset whose anchors pass the current eligibility/ranking/cap pipeline, not a placement count.

## Reconstruction, alternatives and abstention

| State | Rice | G1 interior |
|---|---:|---:|
| Joint support unique | 693 | 192 |
| Joint support among alternatives | 36,387 | 3,181 |
| Contradiction in comparable context | 2,516 | 6,167 |
| No comparable context / abstention | 764 | 7,341 |
| Temporal and spatial marginals present, joint absent | 758 | 1,436 |

Alternatives dominate supported rice and G1 trials. Therefore the result demonstrates that complete spatial relations can be represented and recovered, not that evidence usually determines one lane. The 758 and 1,436 marginal-only cases are direct controls against joining temporal evidence from one occurrence with spatial evidence from another.

## Results by chart and keymode

`Sup` is independent joint support; `OpSup` is its intersection with universe C.

| Chart ID | K | Rice A | Rice Sup | G1 A | G1 Sup | G1 C | G1 OpSup |
|---|---:|---:|---:|---:|---:|---:|---:|
| `02D9D178…` | 7 | 4,599 | 3,603 | 4,220 | 702 | 84 | 0 |
| `0338ADEB…` | 4 | 1,505 | 1,489 | 1,099 | 824 | 16 | 0 |
| `20651C9B…` | 7 | 7,918 | 6,990 | 7,207 | 744 | 75 | 9 |
| `322F9954…` | 7 | 1,302 | 1,113 | 37 | 3 | 0 | 0 |
| `B1F84719…` | 7 | 4,218 | 4,176 | 0 | 0 | 0 | 0 |
| `BC0401B4…` | 7 | 7,020 | 7,002 | 0 | 0 | 0 | 0 |
| `BEF3BB94…` | 4 | 921 | 921 | 0 | 0 | 0 | 0 |
| `DAC0347B…` | 10 | 2,583 | 2,447 | 0 | 0 | 0 | 0 |
| `DD5D885E…` | 7 | 2,660 | 2,562 | 61 | 0 | 8 | 0 |
| `E73B098A…` | 7 | 3,607 | 2,886 | 4,245 | 1,100 | 101 | 2 |
| `F0E7AD21…` | 7 | 4,027 | 3,891 | 12 | 0 | 4 | 0 |

Rice support spans 11 charts and keymodes 4/7/10. G1 support spans five charts and keymodes 4/7. G1 operational support is much narrower: 11 occurrences in two 7K charts. This is still sufficient for the preregistered feasibility criterion, but it is a major limitation for any later utility claim.

## Leakage, joint identity and bad controls

The official run reports zero integrity failures and zero legacy G1 leakage counts. Whole-group holdout prevents a rice target or sibling from teaching itself. G1 excludes the target occurrence, complete anchor event and complete parent; donors used constructively by the target cannot count as independent support. Cross-chart evidence and synthetic objects are never eligible.

Five focused synthetic tests ran before the final freeze. They deliberately induced and detected every preregistered control: target, target-group, parent, release endpoint, future-held, same-event, synthetic teaching, cross-chart, artificial composition, duplicated observation identity and incomplete/mispaired identity. A separate test proves that matching temporal and spatial marginals in different donors remains contradiction rather than joint support. Another verifies that contract identity survives pretty-JSON round-trip.

## Determinism and non-interference

- Official outcome: `FEASIBILITY_DEMONSTRATED`.
- Supported charts/keymodes: rice 11/3; G1 5/2.
- Supported operational targets: rice 37,080; G1 11.
- Research RNG calls: 0.
- Behavior changed: false.
- Corpus/chart fingerprint mutation: none.
- Repeat: byte-identical.
- Semantic result SHA-256: `1C59A127D3DCD564C179EA6103D411251338F02459FCF4677B74CD075F40A8DB`.

No 224-pair matrix, behavioral A/B, new gate, product candidate, Songs scan, third-party download or `.osu` publication occurred. `AddNotesEngine`, lane filters, RNG selection, AddChance, weights, caps, eligibility, HardValidity and defaults were not changed.

## Artifacts

Under `.artifacts/lane_0/`:

- `frozen_contract.json`
- `scientific_summary.json`
- `chart_results.json`
- `integrity.json`
- `sha256sums.txt`

Only aggregate identities and counts are published; no third-party chart content is copied into the report.

## Limits and next scientific question

C11 is a historical development corpus, not independent external validation. Exact absolute identities sacrifice coverage and do not establish mirror/translation equivalence. G1 operational support is confined to 11 occurrences and two 7K charts, despite structural support in 4K and 7K. Alternatives are common, so no frequency, winner, score or confidence follows.

The justified next question, if separately authorized, is whether a preregistered descriptive comparison can distinguish complete supported alternatives using original-only context without introducing cross-keymode normalization or a winner. That would be a new phase. `LANE.DESIGN`, `LANE.GATE` and behavioral integration remain **NOT_AUTHORIZED**.
