# SAFETY.REMEDIATION.GATE — Canonical Playable Geometry Runtime

## Closure

**COMPLETE — REMEDIATION_RUNTIME_CERTIFIED — EXPERIMENTAL ONLY — NO PROMOTION**

This phase implemented and certified the remediation authorized after `SAFETY.REMEDIATION.DESIGN`. It does not promote G1, change product defaults, expose a CLI/Web switch, relax HardValidity, or authorize a successor. Normal generation remains `legacy-experimental.1`.

The frozen runtime policy is `safety-remediation-canonical-playable.1`. It changes one authority in an explicitly supplied research configuration: collision queries use beats reconstructed from the integer millisecond coordinates that will be serialized and played. Latent decimal beats remain intact for candidate intent, research provenance, and frozen G1 identity.

## Entry identity and frozen contract

- Repository entry HEAD and live `origin/main`: `53f1475cb14186b2a6a4eef5a9c71d8aa8777d6f`.
- Design dependency: `EFB31F2BF5026BE7353ACB30C15768389D077CC7244B0C91D66F8B6B6BD002F8`.
- C11 manifest: `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`.
- Frozen implementation snapshot: `93DD2E5E2D7FC6C49FB33B34C7CCDCC58870FA6BE835892FF7903A09D4ADCBE7`.
- Canonical gate contract: `9415E4710E44163D26BF56123C3166EF9F52C43AA376E53BA7D2E8305E3293D8`.

The snapshot covers the four runtime files that define engine placement, timing materialization, geometry and result state. Both contract generation and the full runner recompute it and abort on drift.

## Runtime integration

`CanonicalPlayableGeometry` projects each object from durable integer `StartTime`/`EndTime` through the chart timing map. The engine keeps a canonical geometry mirror only when the research gate is supplied. Control retains legacy authority; treatment uses the canonical mirror for tap and LN lane legality. Both mirrors follow the objects actually committed by their own run.

Candidate construction, opportunity ordering, Bernoulli decisions, weighted shape selection, lane-selection algorithm and latent candidate values are unchanged. The gate consumes no RNG and does not reroll a canonical rejection. G1, when present in the secondary stratum, still evaluates its exact frozen latent identity; the remediation neither canonicalizes nor rewrites that evidence.

The materialized geometry identity is maintained incrementally from integer coordinates, object type, sequence and origin. This diagnostic state excludes latent beats by design. It makes downstream divergence and reconvergence auditable without changing placement.

## Frozen experiment

The primary stratum contains 11 C11 charts × seeds 1–20: **220 paired runs**, remediation OFF versus ON, with G1 and articulation OFF. Each pair also compares gate-OFF against the ordinary engine path and repeats treatment deterministically.

The secondary stratum contains the four frozen G1.GATE violation runs: chart `02D9…` seeds 9, 11 and 17, plus chart `20651…` seed 11. Both arms run the same frozen G1 treatment; only remediation differs. This stratum tests compatibility, not G1 utility.

Every output is independently serialized, reparsed, compared by durable object identity and audited with HardValidity reconstructed from integer coordinates. The runner records the first governed divergence, RNG positions, materialized-state identity and any later full-state reconvergence.

## Results

| Measure | Control | Treatment |
|---|---:|---:|
| Added objects across 224 pairs | 307,167 | 306,983 |
| Introduced hard-validity conditions | 215 | 0 |
| Treatment-only hard violations | — | 0 |
| Gate RNG calls | 0 | 0 |

The net object delta is -184. Outputs differ in 43 pairs; RNG position differs in 8, which is permitted only after a governed accept/reject divergence. Thirty-one pairs later reconverge in full materialized state plus RNG position. These are path-dependent runtime effects, not direct-shadow counts.

All **209 frozen known cases** and **6 additional design-shadow cases** were accounted for. Of the 215 cases, 188 reached the equivalent proposal and were rejected directly by canonical authority; 27 became unreachable after an earlier governed divergence. There were **0 unexpected cases**.

Treatment produced **0 hard-validity violations**. Default-path equivalence failures, deterministic rerun failures, serialization/reparse failures and G1 evidence-identity failures were all zero. The gate consumed zero RNG. Therefore the frozen result is `REMEDIATION_RUNTIME_CERTIFIED`.

## Interpretation and boundaries

This result certifies that canonical playable geometry removes the measured latent-versus-materialized contradiction under the frozen experiment. It also demonstrates that the earlier `209 + 6` shadow footprint maps cleanly into direct rejection or causal unreachability once the authority governs runtime.

It does not establish that fewer objects are stylistically better, validate G1 utility, or authorize the treatment as the product default. `legacy-experimental.1` remains the only normal CLI/Web behavior. The research overload is deliberately inaccessible from product surfaces.

The following remain **NOT AUTHORIZED**:

- G1 utility or promotion;
- product defaults or CLI/Web exposure;
- G2, H, articulation changes or general refactors;
- changes to AddChance, density, lane style, composition or candidate vocabulary;
- HardValidity or serializer semantic relaxation;
- any successor phase without explicit human authorization.

## Artifacts

- `docs/safety_remediation_gate_contract.json` — frozen machine contract.
- `docs/safety_remediation_gate_runs.csv` — 224 paired-run summaries.
- `docs/safety_remediation_gate_known_cases.csv` — all 215 frozen cases with direct/unreachable classification.
- `docs/safety_remediation_gate_summary.json` — canonical aggregate and certification outcome.
- `.artifacts/safety_remediation_gate/safety_remediation_gate_summary.json` — local regenerated copy.

## Verification

The focused gate suite covers millisecond aliasing, inclusive tap and strict LN boundaries, equal starts/releases, zero-gap LN→LN, projection idempotence across negative times/redlines, exact default equivalence, observer/provenance non-interference, deterministic treatment, serialize/reparse equality, frozen G1 identity and contract rejection on drift. Final repository validation records **747 passed, 0 failed, 0 skipped**, plus build, documentation consistency and diff checks.
