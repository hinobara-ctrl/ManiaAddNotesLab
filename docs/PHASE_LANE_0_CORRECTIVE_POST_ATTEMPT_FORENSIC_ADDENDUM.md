# LANE.0 — post-attempt forensic addendum

**Closure date:** 2026-10-01  
**Scope:** documentation-only forensic closure  
**Behavior change:** none  
**Scientific promotion:** none

## Status

The single official corrective C11 attempt authorized for public commit
`243c43a589512a12a69b9a4c0d68d1c4fd54a8ca` has been consumed.

- Attempt status: `AUTHORIZED_CORRECTIVE_ATTEMPT_CONSUMED`
- Scientific evaluator: `INVALID`
- Scientific result: `NOT PUBLISHABLE`
- Promotion: `NONE`
- Retry: `PROHIBITED`
- Successor: `NOT YET PREREGISTERED / NOT AUTHORIZED`
- Global LANE.0 certification: `SUSPENDED / PENDING_RECERTIFICATION`
- Historical LANE.0 feasibility result: retained as historical evidence only

The authorized publication binding had SHA-256
`5F2C82A217B465F7948D8362D4083460152733901378E279976127F973BB26DD`.
The durable attempt receipt has SHA-256
`7F6FA78F528CC3AE882B2F8BFB89019C18C4E356ABFAB7E3E2F6B719B9CDC387`.
The receipt proves consumption of the one-shot authority. The binding and receipt remain local
historical evidence and are not part of this documentation change.

No final or staging scientific package was published by the attempt.

## Forensic verdict

`FORENSIC SUFFICIENT CAUSE ESTABLISHED / EXCLUSIVE CAUSE NOT PROVABLE`

The public/runtime chart identity for Spring of Dreams is:

`E73B098AA0CB6FA3BECE45E9551EF225412F5E01F6398184093A40B944F0AAE0`

The corrective historical G1 reference was frozen as the distinct identity:

`E73B098A4D4C99D716D2C53B3EA3A4BFD1A75059881F30DB416631B5EDB972D4`

The first identity is present throughout public runtime and historical evidence, including
`docs/e_chart_summary.csv`, `docs/g1_gate_runtime_manifest.json`,
`docs/d1_behavioral_ab_run_manifest.json`, `docs/g1_gate_paired_runs.csv` and the safety
provenance artifacts. The second identity has no versioned chart provenance before its
introduction in commit `3c89e0b42d4e26509381f40218e3d5304fb2c5ac`, where it was encoded as a
corrective historical reference.

## SCI-01 — SCIENTIFIC_REFERENCE_IDENTITY_MISMATCH

The violated property is:

> Every chart ID in `historicalG1CaseRule.chartDistribution` must identify exactly one chart in
> the frozen C11 manifest and must agree with the corresponding public historical evidence,
> including family and keymode.

The runtime manifest supplies the real Spring chart using the first identity. The evaluator
reconstructs historical cases under their real input chart IDs, while the frozen corrective
rule expects two cases under the second identity. Consequently, the reconstructed distribution
cannot equal the frozen expected distribution and the evaluator adds:

`Historical operational G1 chart distribution mismatch.`

That integrity failure is sufficient to force `INVALID`. The external runner only reported the
generic non-publishable outcome and invalid packages were intentionally not published, so other
internal failures cannot be excluded. SCI-01 is therefore a proven sufficient cause, not a
proven exclusive cause.

SCI-01 is separate from execution-hardening findings F01–F09. Execution-hardening correctness
is not the same property as scientific frozen-input correctness. F03×F07 remains `CLOSED`: the
durable receipt was created before corpus access and now prevents reuse of the consumed
authority as designed.

## Control-flow conclusions

Reaching the observed generic evaluator outcome proves that the published identities, binding,
authorization root, frozen manifest and corpus inventory gates had already passed. It also
proves that both independent corpus loads and evaluations completed, the two six-artifact
packages were byte-identical in memory, research RNG calls were zero, inputs remained unchanged
and behavior remained unchanged.

It does not prove that SCI-01 was the only evaluator failure, nor does it produce a publishable
corrective scientific result.

The interactive PowerShell `elseif`/`else` errors that occurred while arming the binding are
classified `NON-CAUSAL`. The preceding `if` had already completed the `false` to `true`
replacement. Subsequent independent hash, JSON, HEAD, authorization and host-preflight checks
validated the exact armed binding before the official launcher ran.

## Historical evidence retained

This addendum does not rewrite or invalidate the historical aggregate observation:

- 9 supported operational G1 cases on `20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788`;
- 2 supported operational G1 cases on Spring of Dreams;
- 11 supported operational G1 cases in total.

The defect is the full chart identity attached later to the two Spring cases, not the historical
`9 + 2` counts. This closure also preserves unchanged:

- the frozen C11 manifest;
- all C11 content;
- RICE science and its historical counts;
- future-held science;
- all thresholds and criteria;
- execution hardening F03×F07;
- the consumed binding and receipt as historical evidence;
- all behavioral defaults.

The original corrective preregistration, design, hardening, preparation, template, evaluator
constant, tests and verifiers retain the erroneous value intentionally. They are historically
binding evidence of what was preregistered and executed, contain an invalid scientific frozen
reference, and are superseded for any successor use. They must not be silently rewritten.

## Test and verifier gap

The failure class was `SELF-CONSISTENT BUT EXTERNALLY WRONG FROZEN INPUT`.

The existing checks principally established:

`contract ↔ implementation ↔ tests`

They did not establish:

`historical reference IDs ⊆ frozen C11 manifest chart IDs`

A future successor must add adversarial validation that every chart ID in
`historicalG1CaseRule.chartDistribution` exists exactly in the frozen manifest and agrees with
its family and keymode. The validation must reject missing, substituted, duplicated or
otherwise non-member identities. This requirement is documented here but not implemented by
this closure.

## Successor governance

The consumed attempt cannot be retried, rearmed or repaired in place. A possible future
evaluation would be a separately governed successor experiment, not a retry. Before any access
or execution it would require:

1. a new remediation addendum or contract;
2. a new successor preregistration;
3. successor evaluator, tests and verifiers;
4. a new public commit identity;
5. a new independent audit;
6. new explicit human authorization;
7. a new one-shot authority.

That authority must use a new namespace and new versioned paths for its publication binding and
attempt receipt, tied unambiguously to the successor experiment ID. Final path names are not
defined here because no successor design has been frozen. The historical binding must not be
overwritten and `.artifacts/lane_0_corrective.attempt.json` must not be deleted, moved, reused or
treated as successor authority.

## Closure

`POST-ATTEMPT FORENSIC DOCUMENTED`  
`SCI-01 RECORDED`  
`OFFICIAL ATTEMPT CONSUMED`  
`NO RETRY`  
`SUCCESSOR NOT YET DESIGNED/AUTHORIZED`

This addendum authorizes no C11 access, experiment, successor, behavioral integration,
`LANE.DESIGN`, `G1×LANE` or `COMPOSITION` work.
