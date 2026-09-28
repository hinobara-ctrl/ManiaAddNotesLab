# LANE.0 — Corrective evaluator hardening

Date: 2026-09-28
Published parent: `d005534fc9b27827ebd3951368c3eb5eb714ee87`
State: **READY FOR MANUAL PUBLICATION AND AUDIT / EXECUTION BLOCKED**

## Scope

This successor closes four audit findings in the published in-memory corrective evaluator: optional scientific prerequisites, incorrect `BLOCKED` treatment of supplied historical contradictions, insufficient external RICE-snapshot enforcement, and output/test-execution hygiene. It does not authorize or access C11 and does not add a corpus loader, CLI route, publication binding or behavioral change.

The published design contract, report and verifier remain immutable historical evidence. Their contract, implementation and harness identities are reproduced by the successor verifier from Git blobs at `d005534`, not from the deliberately changed working tree.

## Mandatory scientific inputs

The evaluator still accepts `Lane0CorrectiveEvaluationOptions`, but classification is now guarded by two mandatory scientific inputs:

- `RequiredHistoricalSnapshot`;
- `RequiredHistoricalG1Cases`.

The canonical snapshot and eleven-case rule are represented as immutable internal values. If publication/authorization prerequisites are incomplete, or either scientific input is absent, the result contains a blocking reason and cannot become `FEASIBILITY_DEMONSTRATED` or `LIMITED_PARK`.

Validation of reconstructed historical evidence begins only after all prerequisite inputs are present. This makes the distinction exact:

- missing prerequisite: `BLOCKED`;
- supplied snapshot/rule contradicting the frozen definition: `INVALID`;
- supplied canonical facts but reconstructed counts, chart distribution or keymode disagree: `INVALID`;
- missing/duplicate historical-corrected identity or other structural failure: `INVALID`.

`INVALID` retains precedence over `BLOCKED`.

## RICE external sentinel

The local non-interference check remains `historical RICE == corrected RICE`. It is now complemented by a mandatory external check: reconstructed historical RICE must reproduce `RequiredHistoricalSnapshot.Rice`, and that supplied snapshot must equal the canonical frozen snapshot. No alternative corrected RICE algorithm was introduced.

## Eleven-case rule

The rule remains total 11, keymode 7 and distribution 9+2 over the two frozen chart hashes. Missing rule means `BLOCKED`; a supplied incompatible rule or incompatible reconstruction means `INVALID`. Exact pairing remains `OccurrenceId` only. No real occurrence identity was reconstructed or invented during this hardening.

## Output-directory hygiene

The deterministic writer now accepts only:

- a nonexistent output directory; or
- an existing completely empty directory.

Before writing, it serializes the result and checks the destination. Any existing file, `sha256sums.txt`, unrelated file or subdirectory causes an exception before an artifact is created. It never deletes, cleans, merges or overwrites prior output. UTF-8 without BOM, LF ordering and self-exclusion of `sha256sums.txt` remain unchanged.

## Test-execution identity

The corrective test project remains registered in `ManiaAddNotesLab.sln`. The successor freezes the normalized solution hash separately and its verifier requires exactly one occurrence of:

`tests\ManiaAddNotesLab.CorrectiveEvaluator.Tests\ManiaAddNotesLab.CorrectiveEvaluator.Tests.csproj`

Validation executes both the complete solution and the corrective project directly. This prevents an apparently healthy solution run from silently omitting the evaluator tests.

The new hardening suite adds 21 executed cases covering prerequisite combinations, final classifier outcomes, snapshot/rule contradictions, missing corrected identity, both RICE controls, nonexistent/empty/nonempty destinations, arbitrary files, old artifacts, existing checksums, subdirectories, no partial output, classifier precedence and semantic-output environment hygiene. The existing determinism, zero-RNG and fingerprint tests remain active.

## Successor identities

- Contract canonical SHA-256: `A61F0933A49534857096D3A568E9C07F37805F9468CC94001546767073CD2E18`
- Evaluator implementation identity: `20D0AC6BB0AF197ACC6BE1B5E71D1DF65BD2CDC0FCAB0E49FD6F036D03A0D8F4`
- Evaluator harness identity: `724837E42F2308DA3159BAE00F3339656C03EE936B9E350F416124A7E92AA173`
- Normalized solution SHA-256: `BFEB1689414A2FB712170A967B9BA6DAD70BFE656D307A25821A976D81942A37`

The independent successor verifier also reproduces from `d005534`:

- parent design contract: `074B84F57D0ED3870C744ABCD1F00E2BA9B9C587F917A9823C76F46D06DF01EB`;
- parent evaluator implementation: `9CA35582CCB70B5D85434373709D511A3B896BCDF1353137F8FE6724DBA53128`;
- parent evaluator harness: `F00289DF36021C0211F3EE888A1EFF0D332390F1219EFC3391EEF487D9CC63D0`.

## Boundaries and stop

No manifest was resolved, no corpus directory was enumerated, no `.osu` file was opened, no corrected C11 count was produced and no real transition matrix or eleven-ID list exists. `Program.cs`, historical runner, engine, geometry, evidence research and policy/default files remain unchanged.

Global LANE.0 certification remains suspended. The next permitted step is manual publication followed by an independent audit of the new SHA. Binding, adapter, manifest verification and the single authorized C11 evaluation require a separate future instruction and explicit human authorization.
