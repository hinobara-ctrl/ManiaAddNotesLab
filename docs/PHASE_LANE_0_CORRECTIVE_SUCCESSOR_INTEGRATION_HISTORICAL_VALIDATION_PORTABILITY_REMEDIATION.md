# LANE.0 corrective successor — historical validation portability remediation

## Boundary

- Phase: `LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_HISTORICAL_VALIDATION_PORTABILITY_REMEDIATION`
- Baseline: `9eaf82e8aa933ab730af107042884c591e597576`
- Parent remediation contract: `27BAFFB4B92E5B30A073E60673D8F2FEDB109FF70A69A23C5783D8D589693FDD`
- Authority: `AUTHORIZED_FOR_HISTORICAL_VALIDATION_PORTABILITY_REMEDIATION_IMPLEMENTATION_ONLY`
- Status: **COMPLETE**
- Outcome: **READY_FOR_FINAL_INDEPENDENT_PREBINDING_REAUDIT**

This phase changes current tests, validation guards and living documentation only. It does not change historical contracts, the historical validator, authority/runtime code, science, defaults, RNG or product behavior.

## Independent audit finding

The independent audit stopped with `FINAL_PREBINDING_REAUDIT_BLOCKED` after one current regression test failed on the auditor checkout. The only blocker was `HV-P1`: `HISTORICAL_EXACT_BYTE_REUSED_DEPENDENCY_ASSERTION_NOT_PORTABLE_ACROSS_CHECKOUTS`.

The failed assertion was `Assert.True(readiness.Checks.ReusedDependencies)` in `HistoricalV2RouteRejectsChangedInstrumentIdentity`. On this baseline checkout the pre-change build and both suites passed (`210 + 977 = 1187`), so the auditor failure did not reproduce locally. That difference is itself consistent with the historical byte-sensitive rule.

## Root cause and preserved historical meaning

The immutable historical contract states:

> file hashes use exact working-tree bytes, therefore CRLF/LF changes identity

The historical validator hashes reused dependencies with exact file bytes. It therefore remains correct for `ReusedDependencies` to vary between later checkouts that hold semantically identical text with different physical line endings. Its value remains authoritative within an execution of that historical validator, but `ReusedDependencies == true` is not a portable invariant for an arbitrary evolved checkout.

Neither `docs/lane_0_future_held_hardening_contract.json` nor `Lane0FeasibilityRunner.ValidateCorrectiveReadiness`, `TreeIdentity`, `FileHash`, `CanonicalCorrectiveHardeningContractHash` or `ClassifyCorrectiveReadiness` was modified. The exact-byte historical semantics remain unchanged.

## Test intent before and after

Before remediation, the current regression test combined two concerns: it proved that the evolved instrument is rejected, but also required the checkout-dependent reused-dependency diagnostic to be true.

After remediation, `HistoricalV2RouteRejectsChangedInstrumentIdentity` proves only stable facts:

- outcome is `INVALID`;
- the historical contract remains canonical;
- the current implementation identity differs from the frozen historical implementation;
- the current harness identity differs from the frozen historical harness.

It no longer requires `ReusedDependencies == true`. Separate fail-closed evidence remains explicit: `CorrectiveReadinessUsesVerifiedConditionsAndPrecedence` and `HistoricalV2ExactByteDependencyDriftRemainsFailClosed` prove that `ReusedDependencies=false` still yields `INVALID`. A focused declaration test and the new DocConsistency guard preserve the exact wording `file hashes use exact working-tree bytes` and `CRLF/LF changes identity`.

## Preserved audit closures and identities

- AP-R1 remains CLOSED: `CLOSED_SOURCE_ROOT_ONE_SHOT_ATOMICITY`.
- AP-R2 remains CLOSED: `CLOSED_NO_HARDLINK_PROVENANCE_OWNERSHIP`.
- The legacy injectable authority-store seam remains `NOT_A_FINDING`.
- Final-artifact timing remains `NON_BLOCKING_PRESERVED`.
- The stale runtime comment remains `DOCUMENTATION_ONLY_PRESERVED_TO_AVOID_RUNTIME_IDENTITY_CHURN`; it was intentionally not changed.
- Adapter: `032F9660D8C0012C990983CE307CDA9D20B1C49116BEAA7F3C5E3DB1EBD875B0`.
- Runner: `32C600B809F0B021C3806FDBC2BE58656C6DA27B5CE053E4E625AC9A175D72BF`.
- Authority/runtime/worker: `805F70466B503FEBBB14511E595A12E3AB42A583CED1A1D04193BF3DE04474DE`.
- Deep semantic verifier: `67969A60704553AA6CFB134F8B7F67236757EA6A2CF51C9CD7C4EF3C8FA5A4DB`.

The historical `27BAFFB4...` contract remains internally valid and all four live component identities remain exact.

## Validation

- Focused portability, fail-closed, 27BA, atomicity and authority guards: **45 passed / 0 failed / 0 skipped**.
- CorrectiveEvaluator suite: **210 passed / 0 failed / 0 skipped**.
- Main suite: **992 passed / 0 failed / 0 skipped**.
- Combined full suites: **1202 passed / 0 failed / 0 skipped**.
- Solution build: **PASS**, zero warnings and zero errors.
- DocConsistency: **PASS**.
- `git diff --check`: **PASS**.

## Authorization boundary

- No C11 or Songs content was accessed.
- No binding, real receipt or execution occurred.
- No C11 authorization, binding authorization, receipt authorization, execution authorization or product-promotion authorization exists.
- No behavior, RNG, default or product exposure change occurred.

Next required action: `FINAL_INDEPENDENT_PREBINDING_REAUDIT`.
