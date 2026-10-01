# LANE.0 corrective successor — Phase 1 preregistration

Status: **READY FOR HUMAN REVIEW / NO C11 AUTHORIZATION**.  
Schema: `lane-0-corrective-successor-preregistration.1`.  
Baseline: `45292af1ac58d8af4d24e53ab297a8b7099f0825`.

## Scientific boundary

This is a new successor experiment, not a retry or rearm of the consumed corrective attempt. It preserves the original feasibility question, RICE and G1 criteria, future-held and `AnchorTime` semantics, joint-support requirement, thresholds, zero-integrity rule, zero RNG, byte-identical repeat and `legacy-experimental.1` behavior. The only scientific change is `SCIENTIFIC_REFERENCE_REMEDIATION_ONLY`.

The consumed contract remains historical evidence and continues to show erroneous Spring identity B:

`E73B098A4D4C99D716D2C53B3EA3A4BFD1A75059881F30DB416631B5EDB972D4`

The successor freezes corrected public Spring identity A:

`E73B098AA0CB6FA3BECE45E9551EF225412F5E01F6398184093A40B944F0AAE0`

alongside:

`20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788`

The historical operational distribution remains exactly `9 + 2 = 11`, with both references 7K and associated with the exact versioned manifest families. No occurrence identities are inferred.

## Pre-C11 invariant

Before any future authority or corpus gate, every successor reference must identify exactly one chart in the versioned public manifest `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`. Chart identity, manifest family, keymode, uniqueness, individual distribution and total count are validated statically. The public manifest contains no historical supported-count field; the `9+2` counts therefore remain preregistered historical evidence rather than fabricated manifest metadata.

Bad controls reject unknown identities, historical B, missing or duplicate references, another valid-but-wrong C11 chart, family/keymode drift, `10+1`, `9+1`, extra distributions, total-count drift and manifest identity drift. All return `INVALID` before binding, receipt, corpus inspection, `.osu` discovery or parsing.

## Authority boundary

The successor reserves `docs/lane_0_corrective_successor_publication_binding.json` and `.artifacts/lane_0_corrective_successor.attempt.json`. Neither file exists or is authorized in Phase 1. The historical binding and receipt cannot authorize this successor and the successor namespace cannot mutate or rearm the consumed attempt.

Any future authority is one-shot: success and failure consume it; there is no automatic retry, rearm or receipt reuse. Publication of this preregistration is not execution authorization. A public commit, independent audit, separate execution-preparation prompt and explicit human authorization remain mandatory.

Phase 1 contains no C11 adapter, corpus root, `.osu` discovery, parser, launcher, official runner, binding or receipt. It produces no scientific result and cannot return `FEASIBILITY_DEMONSTRATED` or `LIMITED_PARK`.
