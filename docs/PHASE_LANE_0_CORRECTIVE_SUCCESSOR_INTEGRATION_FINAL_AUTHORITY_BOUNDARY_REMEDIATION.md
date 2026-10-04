# LANE.0 Corrective Successor — final authority-boundary remediation

## Closure

- Baseline: `1443cf17eb50938c8f3e1ebd18a9d521ac9e409b`
- Prior independent verdict: `PREBINDING_REAUDIT_REQUIRES_REMEDIATION_BEFORE_BINDING`
- Phase: `LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_FINAL_AUTHORITY_BOUNDARY_REMEDIATION`
- Authority: `AUTHORIZED_FOR_FINAL_AUTHORITY_BOUNDARY_REMEDIATION_IMPLEMENTATION_ONLY`
- Contract: `B4489D605EFF3E0AB2DF2768CAE2530DBA071E7AC4EF4009AEE7383E764AEFF0`
- Status: `COMPLETE`
- Outcome: `READY_FOR_FINAL_INDEPENDENT_PREBINDING_REAUDIT`

This outcome is not `READY_FOR_BINDING`, does not authorize binding, receipt, execution or C11,
and is not an audit-clean claim.

## Unified official capability

The production worker now exposes one official entry surface: `--official-run`. The historical
split production surfaces `--authorize` and `--execute` no longer exist. The same child process,
built from the detached ExecutionRoot, independently observes source and checkout Git state,
reloads canonical binding bytes, validates current component identities and the exact
project-owned binary closure, durably claims the attempt, creates the receipt, admits the corpus,
runs both passes, finalizes packages, verifies deep semantics and writes its derived response.

`CorpusRoot` is not present in process arguments or the pre-receipt attestation. The host sends its
opaque token through standard input only after the child has durably created the receipt and
emitted `RECEIPT_CREATED`. The worker then performs the first interpretation, three-root isolation
and exact DA86 adapter admission. This preserves the frozen 21-step order and prevents a 13→17
jump.

Official outputs are derived internally and fixed at:

- `ExecutionRoot/.artifacts/integration-exchange/staging`
- `ExecutionRoot/.artifacts/integration-exchange/final-artifacts/pass-1`
- `ExecutionRoot/.artifacts/integration-exchange/final-artifacts/pass-2`

No official API accepts charts, response paths, pass paths, a staging root or a final root.

## Finding dispositions

- **FB-R1 — CLOSED.** `official-authority-state.json` is created with `FileMode.CreateNew` before
  the receipt and binds `SCIENCE_ATTEMPT_CLAIMED`. Success, invalid admission, scientific failure
  and exception consume the sole attempt. A second invocation fails before science; there is no
  automatic retry, rearm or delete-to-retry API.
- **FB-R2 — CLOSED.** Official science receives only the immutable chart array returned inside the
  worker by `Lane0CorrectiveSuccessorFrozenC11Adapter`. A caller cannot provide `request.Charts` to
  `--official-run`.
- **FB-R3 — CLOSED.** Every official exchange and final path is derived from ExecutionRoot. The
  command shape accepts no output destination.
- **FB-R4 — CLOSED.** The receipt-creating worker verifies the fixed canonical attestation, exact
  HEAD, detached state, tracked cleanliness, root isolation, canonical binding, live identities,
  no prior receipt/output/state and isolated-build provenance before authority creation.
- **FB-R5 — CLOSED.** Exact deterministic Core/Experiments/Worker binary rows and aggregate are
  bound into durable authority state and compared at launch, immediately before science, inside
  the scientific worker and after science. Missing or changed closure fails closed.

## Synthetic separation and E2E seam

The explicit nonofficial surface is `ExecuteSyntheticFixture` → `--synthetic-run`. It alone accepts
synthetic charts and writes only under
`ExecutionRoot/.artifacts/synthetic-integration-exchange`. It cannot create or consume the official
receipt namespace. The low-fake E2E uses a disposable Git repository, a real detached no-hardlink
checkout, an ExecutionRoot-built child, real provenance, deterministic two-pass finalization and
semantic verification. The exact seam is synthetic chart injection after the modeled receipt
boundary because DA86 cannot be satisfied without the real C11 corpus. It is not a real C11 E2E.

## Historical and phase boundaries

The `41F47787…` contract remains internally valid and immutable. Its explicit live comparison now
rejects the evolved runner/authority identities, as expected. The new current guard validates this
contract and the live components.

- historical C11 explicit inventory: `YES_READ_ONLY_EXPLICIT_PATH_INVENTORY_ONLY`
- successor C11 science: `NO`
- C11 accessed in this phase: NO
- real binding created: NO
- real receipt created: NO
- real successor execution: NO
- behavior/default/RNG/product change: NO

The only next action is `FINAL_INDEPENDENT_PREBINDING_REAUDIT`.
