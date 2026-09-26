# SAFETY.REMEDIATION.GATE Validation Hardening / Recertification Addendum

**RECERTIFICATION NEEDS_REVIEW — 188 direct canonical rejects + 22 causally proven unreachable + 5 unresolved — NO PROMOTION.**

This addendum supersedes only the original report's strong claim that all 27 historical `B_EARLIER_GOVERNED_DIVERGENCE` cases were causally proven. The original report and artifacts remain preserved as the historical result. The treatment still produced zero HardValidity violations in the frozen matrix, but the strengthened proof obligation certifies only 22 of those 27 downstream cases. The other five remain `C_UNRESOLVED`; they are not silently reclassified and no extra investigation was added to force a pass.

## Entry identity and frozen execution

- Repository entry HEAD: `667fd1d469ed707c5fc28232672327c7e320d4a0`.
- Original gate contract: `9415E4710E44163D26BF56123C3166EF9F52C43AA376E53BA7D2E8305E3293D8`.
- Original implementation snapshot: `93DD2E5E2D7FC6C49FB33B34C7CCDCC58870FA6BE835892FF7903A09D4ADCBE7`.
- Hardened implementation snapshot: `B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835`.
- Certification harness snapshot: `81F2FA4BF462E169CCE8230C70DC792BF927E2BF57A93D4BDC571AD8F542DF72`.
- Hardening contract: `7E68F1CDC0E2F783794B229939B1E0C28764D1C3646999CFC1F85DABC30B9539`.
- C11 manifest: `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`.
- Runtime memory ceiling: `DOTNET_GCHeapHardLimit=0x400000000` (16 GiB).

The final run used the already frozen C11 corpus copy in `.artifacts/f2-1-corpus`: 12 physical `.osu` locations and 11 unique contents. Its 11 unique SHA-256 values matched `docs/e_chart_summary.csv` exactly, with zero missing and zero extra identities. This input substitution avoided scanning the user's entire Songs library; it did not change any chart bytes, implementation, harness, contract, denominator or classification rule.

An earlier attempt against the complete Songs tree stopped before the first pair with `Out of memory`. `C11CorpusDiscovery` had retained every parsed `.osu` before filtering to the frozen 11. That failed attempt wrote no public result artifacts. Windows recorded `RADAR_PRE_LEAK_64`; the 16 GiB limit prevented system-wide memory exhaustion. The final frozen-corpus run completed with empty stderr.

## Snapshot boundary: roles A–E

The implementation inventory makes the executable claim boundary explicit:

| Role | Meaning | Snapshot treatment |
|---|---|---|
| A — Behavioral authority | Engine insertion, canonical authority selection and collision predicates | Included in the hardened implementation snapshot |
| B — Shared runtime dependency | Timing projection, source analysis, durable model and osu serialization | Included in the hardened implementation snapshot |
| C — Instrumentation only | Generation state, independent HardValidity oracle and causal trace model | Excluded from behavioral identity; included where applicable in the harness snapshot |
| D — Certification only | Runner and tests | Frozen in the certification harness snapshot |
| E — Documentation only | Historical reports and prose | Non-executable and excluded |

The complete path-by-path classification and justification is machine-readable in `docs/safety_remediation_gate_implementation_inventory.csv`. This closes the original four-file undercoverage without pretending that diagnostics or prose define runtime behavior.

## Strong causal definition

The sufficient state is the materialized `GenerationState`, committed latent geometry, RNG positions, opportunity cursor, pending articulation identity and frozen opportunity sequence. Hashes are diagnostic accelerators; equality is defined by the complete represented state, not by temporal proximity.

A governed lineage opens only when:

1. control and treatment enter the opportunity with equal sufficient parent states;
2. their commits differ;
3. the control commit is rejected by the canonical playable-geometry authority; and
4. the differing successor states are recorded with a stable lineage identity.

Lineage remains active only while the full sufficient states stay different. Exact equality is reconvergence and permanently closes that ancestry. A later divergence must open a new lineage. Geometry-set difference without a differing governed commit does not open lineage. A canonical rejection is also checked against the invariant that the rejected object cannot appear in the treatment commit.

For a historical downstream case to be `B_CAUSALLY_PROVEN_UNREACHABLE`, it must have an active canonical-governed lineage, different parent states, no intervening reconvergence and absence of the exact historical lane/commit in treatment. Merely occurring after an earlier divergence is insufficient.

## Equivalence and bad controls

Before relying on compact/streaming diagnostics, the tests compare them with the simple materialized implementation. They require exact equality for states, candidate decisions, lineage, fingerprints, commits, rejections, divergences and reconvergences. Incremental RNG transcript hashing is compared with the reference `StringBuilder + SHA-256` transcript.

Bad controls prove that:

- mutating a treatment-defining implementation file changes the implementation snapshot;
- geometry difference alone cannot open causal lineage;
- full reconvergence clears ancestry and a later divergence receives a new lineage;
- canonical rejection cannot coexist with the rejected treatment commit;
- cached and uncached state construction are exact;
- compact and full trace classification are exact;
- diagnostics fingerprints are exact and sensitive to state changes; and
- streaming end-to-end execution preserves output, RNG, candidates, states, fingerprints and lineage.

Focused result: **18 passed, 0 failed, 0 skipped**. Full repository result: **756 passed, 0 failed, 0 skipped**.

## Frozen matrix result

| Measure | Result |
|---|---:|
| Primary C11 pairs | 220 / 220 |
| Secondary frozen G1 compatibility pairs | 4 / 4 |
| Total paired runs | 224 / 224 |
| Historical cases | 215 / 215 |
| Frozen known cases | 209 |
| Additional cases | 6 |
| `A_DIRECT_CANONICAL_REJECT` | 188 |
| `B_CAUSALLY_PROVEN_UNREACHABLE` | 22 |
| `C_UNRESOLVED` | 5 |
| Control HardValidity conditions | 215 |
| Treatment HardValidity conditions | 0 |
| Treatment-only conditions | 0 |
| Unexplained state divergences | 0 |
| Pairs with full reconvergence | 0 |
| Gate RNG calls | 0 |
| Default-equivalence failures | 0 |
| Determinism failures | 0 |
| Reparse failures | 0 |
| G1 evidence failures | 0 |

The runtime safety observation remains strong: the treatment produced zero measured hard-validity conditions. The recertification outcome is nevertheless `NEEDS_REVIEW`, because the requested scientific claim includes individual strong causal accounting for all 215 historical cases and five cases do not meet that proof obligation.

## Individually certified B cases

The following 22 cases meet the complete strong definition. Full state hashes, lineage IDs, lane-selection evidence and invariant fields are retained in the CSV artifacts.

| Chart | Seed | Historical opportunity | Lane | Time | Governed divergence |
|---|---:|---|---:|---:|---|
| `02D9D178` | 1 | `OP-00003589-BaseHead-S3589-T190200-ANA` | 3 | 190200 | `OP-00000132-BaseHead-S132-T14632-ANA` |
| `02D9D178` | 2 | `OP-00004470-BaseHead-S4470-T222416-ANA` | 1 | 222416 | `OP-00000785-BaseHead-S785-T55967-ANA` |
| `02D9D178` | 8 | `OP-00004283-BaseHead-S4283-T215683-ANA` | 6 | 215683 | `OP-00003072-BaseHead-S3072-T163268-ANA` |
| `02D9D178` | 12 | `OP-00000135-BaseHead-S135-T14632-ANA` | 2 | 14632 | `OP-00000126-BaseHead-S126-T13950-ANA` |
| `02D9D178` | 14 | `OP-00004283-BaseHead-S4283-T215683-ANA` | 6 | 215683 | `OP-00001451-BaseHead-S1451-T81194-ANA` |
| `02D9D178` | 15 | `OP-00003850-BaseHead-S3850-T200427-ANA` | 5 | 200427 | `OP-00000674-BaseHead-S674-T51961-ANA` |
| `02D9D178` | 16 | `OP-00003953-BaseHead-S3953-T204262-ANA` | 0 | 204262 | `OP-00001379-BaseHead-S1379-T78637-ANA` |
| `02D9D178` | 16 | `OP-00004447-BaseHead-S4447-T221564-ANA` | 1 | 221564 | `OP-00001379-BaseHead-S1379-T78637-ANA` |
| `20651C9B` | 3 | `OP-00001936-BaseHead-S1936-T110572-ANA` | 5 | 110572 | `OP-00000370-BaseHead-S370-T29933-ANA` |
| `20651C9B` | 3 | `OP-00006160-BaseHead-S6160-T296527-ANA` | 1 | 296527 | `OP-00000370-BaseHead-S370-T29933-ANA` |
| `20651C9B` | 3 | `OP-00006359-BaseHead-S6359-T304468-ANA` | 0 | 304468 | `OP-00000370-BaseHead-S370-T29933-ANA` |
| `20651C9B` | 4 | `OP-00000466-BaseHead-S466-T35009-ANA` | 2 | 35009 | `OP-00000185-BaseHead-S185-T16399-ANA` |
| `20651C9B` | 5 | `OP-00005210-BaseHead-S5210-T265027-ANA` | 0 | 265027 | `OP-00000370-BaseHead-S370-T29933-ANA` |
| `20651C9B` | 5 | `OP-00005297-BaseHead-S5297-T267939-ANA` | 5 | 267939 | `OP-00000370-BaseHead-S370-T29933-ANA` |
| `20651C9B` | 5 | `OP-00005307-BaseHead-S5307-T268380-ANA` | 5 | 268380 | `OP-00000370-BaseHead-S370-T29933-ANA` |
| `20651C9B` | 5 | `OP-00006161-BaseHead-S6161-T296527-ANA` | 4 | 296527 | `OP-00000370-BaseHead-S370-T29933-ANA` |
| `20651C9B` | 6 | `OP-00005756-BaseHead-S5756-T282806-ANA` | 1 | 282806 | `OP-00000370-BaseHead-S370-T29933-ANA` |
| `20651C9B` | 7 | `OP-00000277-BaseHead-S277-T23730-ANA` | 3 | 23730 | `OP-00000011-BaseHead-S11-T2527-ANA` |
| `20651C9B` | 7 | `OP-00005266-BaseHead-S5266-T266791-ANA` | 0 | 266791 | `OP-00000011-BaseHead-S11-T2527-ANA` |
| `20651C9B` | 13 | `OP-00005915-BaseHead-S5915-T287968-ANA` | 0 | 287968 | `OP-00001936-BaseHead-S1936-T110572-ANA` |
| `20651C9B` | 17 | `OP-00006162-BaseHead-S6162-T296527-ANA` | 4 | 296527 | `OP-00000466-BaseHead-S466-T35009-ANA` |
| `20651C9B` | 18 | `OP-00005869-BaseHead-S5869-T286512-ANA` | 0 | 286512 | `OP-00000363-BaseHead-S363-T29369-ANA` |

## Unresolved cases

All five cases have a valid earlier canonical-governed lineage, different parent states and no recorded reconvergence. They remain unresolved because treatment still reaches the historical candidate shape, selects the same historical lane and commits the same historical object. Therefore the exact target is not absent and the strong downstream-unreachability implication is not established.

| Chart | Seed | Historical opportunity | Lane | Time | Earlier divergence |
|---|---:|---|---:|---:|---|
| `02D9D178` | 8 | `OP-00004470-BaseHead-S4470-T222416-ANA` | 1 | 222416 | `OP-00003072-BaseHead-S3072-T163268-ANA` |
| `20651C9B` | 3 | `OP-00004664-BaseHead-S4664-T242439-ANA` | 6 | 242439 | `OP-00000370-BaseHead-S370-T29933-ANA` |
| `20651C9B` | 6 | `OP-00001515-BaseHead-S1515-T89821-ANA` | 6 | 89821 | `OP-00000370-BaseHead-S370-T29933-ANA` |
| `20651C9B` | 7 | `OP-00001936-BaseHead-S1936-T110572-ANA` | 5 | 110572 | `OP-00000011-BaseHead-S11-T2527-ANA` |
| `20651C9B` | 13 | `OP-00006173-BaseHead-S6173-T296968-ANA` | 0 | 296968 | `OP-00001936-BaseHead-S1936-T110572-ANA` |

This is an abstention about the strengthened causal explanation, not evidence that treatment introduced a HardValidity violation. The treatment-wide HardValidity count remains zero.

## Six additional G1-compatibility cases

All six additional design-shadow cases are independently certified as direct canonical rejects:

| Chart | Seed | Opportunity | Lane | Time | Hardened class |
|---|---:|---|---:|---:|---|
| `02D9D178` | 9 | `OP-00000673-BaseHead-S673-T51961-ANA` | 0 | 51961 | `A_DIRECT_CANONICAL_REJECT` |
| `02D9D178` | 11 | `OP-00004283-BaseHead-S4283-T215683-ANA` | 6 | 215683 | `A_DIRECT_CANONICAL_REJECT` |
| `02D9D178` | 17 | `OP-00000785-BaseHead-S785-T55967-ANA` | 5 | 55967 | `A_DIRECT_CANONICAL_REJECT` |
| `02D9D178` | 17 | `OP-00004283-BaseHead-S4283-T215683-ANA` | 6 | 215683 | `A_DIRECT_CANONICAL_REJECT` |
| `20651C9B` | 11 | `OP-00001792-BaseHead-S1792-T103016-ANA` | 2 | 103016 | `A_DIRECT_CANONICAL_REJECT` |
| `20651C9B` | 11 | `OP-00007967-BaseHead-S7967-T375292-ANA` | 0 | 375292 | `A_DIRECT_CANONICAL_REJECT` |

## Artifacts

- `docs/safety_remediation_gate_validation_hardening_contract.json` — frozen identities, denominators, exclusions and 16 GiB execution ceiling.
- `docs/safety_remediation_gate_implementation_inventory.csv` — A–E executable-claim boundary.
- `docs/safety_remediation_gate_hardening_runs.csv` — all 224 paired-run invariant results.
- `docs/safety_remediation_gate_hardened_cases.csv` — individual result for all 215 cases.
- `docs/safety_remediation_gate_causal_unreachable.csv` — complete evidence for the 22 certified B cases.
- `docs/safety_remediation_gate_validation_hardening_summary.json` — aggregate machine-readable result.
- `.artifacts/safety_remediation_gate_hardening/detached-run.stderr.log` — preserved OOM evidence from the full Songs scan.
- `.artifacts/safety_remediation_gate_hardening/detached-run-frozen.stdout.log` and `.stderr.log` — final run log; stderr is empty.

## Decision and authority boundary

**RECERTIFICATION NEEDS_REVIEW.** The frozen treatment still eliminates every measured HardValidity condition, preserves default equivalence, remains deterministic and consumes no gate RNG. However, the original `188 + 27 = 215` causal closure is not supported under the stronger exact-target definition. The defensible result is `188 + 22 + 5 unresolved`.

No remediation behavior, default, CLI/Web surface, G1 mapping or HardValidity rule changed. `legacy-experimental.1` remains the product policy. Promotion and every successor remain `NOT_AUTHORIZED`.
