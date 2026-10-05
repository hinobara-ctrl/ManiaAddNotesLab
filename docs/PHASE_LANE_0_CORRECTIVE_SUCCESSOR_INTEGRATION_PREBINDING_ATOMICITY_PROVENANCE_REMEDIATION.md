# LANE.0 Corrective Successor — pre-binding atomicity and provenance remediation

Phase: `LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_PREBINDING_ATOMICITY_PROVENANCE_REMEDIATION`  
Status: `COMPLETE`  
Outcome: `READY_FOR_FINAL_INDEPENDENT_PREBINDING_REAUDIT`

Contract identity: `27BAFFB4B92E5B30A073E60673D8F2FEDB109FF70A69A23C5783D8D589693FDD`

## Scope and baseline

The exact public baseline was `991dd5e8006d896d604a697a3d11c26d81a82a62`, with parent
`1443cf17eb50938c8f3e1ebd18a9d521ac9e409b`. Before modification, build and both SAFE suites passed:
199 CorrectiveEvaluator plus 965 main tests, 1164 total. The independently reported historical xUnit
failure did not reproduce: `HistoricalV2RouteRejectsChangedInstrumentIdentity` passed as a rejection
test. The independently reported aggregate discrepancy was also not reproduced; its exact rows
recomputed to the published B4489D-era aggregates.

The accepted independent verdict was
`FINAL_PREBINDING_REAUDIT_REQUIRES_REMEDIATION_BEFORE_BINDING`. This phase closes only AP-R1 and
AP-R2. Historical contracts, Phase 2, DA86, scientific semantics and product behavior remain frozen.

## AP-R1 — receipt is the first durable consumption

The production worker no longer creates the canonical receipt and writes no pre-receipt attempt claim.
It completes all preflight checks, rejects an already existing canonical receipt at startup, and emits
`PRE_RECEIPT_READY`. The sealed host/coordinator then creates the canonical receipt. The same already
running worker independently verifies the exact fixed-path receipt bytes, writes
`official-authority-state.json` as the post-receipt fact
`RECEIPT_VERIFIED_ATTEMPT_CONSUMED`, and emits `RECEIPT_VERIFIED`. Only then does the host send the
opaque CorpusRoot token.

Crash semantics are consequently source-root atomic:

- death before receipt creation leaves no consumption and a fresh ExecutionRoot may preflight;
- successful receipt creation consumes the attempt even if state persistence, worker, host, admission,
  science or final classification subsequently fails;
- deleting ExecutionRoot-local state or an alternate directory cannot rearm the fixed SourceAuthorizationRoot.

The frozen 21-step order is unchanged. Post-receipt state is evidence of the already irreversible step
13, not an extra consumption step. Final-artifact timing remains the previously classified
`NON_BLOCKING_PRESERVED` observation.

## AP-R2 — materializer-owned no-hardlink provenance

The canonical receipt creator is now the sealed production host/coordinator that directly receives the
result of fixed `GitDetachedCheckoutMaterializer` execution (`git clone --no-hardlinks --no-checkout`,
then detached checkout of exact HEAD). Immediately before receipt creation it rejects wrong HEAD,
attached state, tracked dirt, hardlink use, and reused source build outputs; re-observes source and
checkout Git state; re-observes the prepared three-assembly closure; and re-reads/revalidates the exact
canonical binding bytes and hash. Caller-authored attestation cannot substitute for this evidence.

The official runner remains parameterless and dependency-sealed. An injected test launcher cannot use
canonical official authority. Direct `--official-run` invocation may reach preflight but has no receipt
creation API and cannot continue or attach to an existing receipt.

## Provenance and opacity chain

The required closure equality chain is preserved across prepared build, pre-receipt worker, immediate
host observation, post-receipt authority state, pre-science and post-science observations. CorpusRoot is
not normalized, checked, enumerated, parsed, hashed or sent before the worker itself reports
`RECEIPT_VERIFIED`. A malformed or wrong receipt ends the session without transmitting CorpusRoot.

FB-R2, FB-R3 and FB-R5 remain closed: official charts still originate only in DA86 admission, official
output roots remain fixed below ExecutionRoot, and the project-owned binary closure remains the same
three assemblies. `--synthetic-run` remains explicitly nonofficial under
`SYNTHETIC.NONOFFICIAL.ONE_SHOT` and cannot access canonical receipt authority.

## Validation boundary

Synthetic temporary Git repositories, real no-hardlink detached checkouts, real child worker processes
and synthetic authority fixtures were used. No C11 or Songs content was accessed. No real publication
binding, canonical receipt or successor execution was created. No behavior, RNG, default or product exposure change
occurred.

Authorization remains `NO_C11_AUTHORIZATION`, `NO_BINDING_AUTHORIZATION`,
`NO_RECEIPT_AUTHORIZATION`, `NO_EXECUTION_AUTHORIZATION` and
`NO_PRODUCT_PROMOTION_AUTHORIZATION`. This result is not an audit-clean or binding authorization claim.

## Frozen current identities

- contract: `27BAFFB4B92E5B30A073E60673D8F2FEDB109FF70A69A23C5783D8D589693FDD`
- adapter: `032F9660D8C0012C990983CE307CDA9D20B1C49116BEAA7F3C5E3DB1EBD875B0`
- runner: `32C600B809F0B021C3806FDBC2BE58656C6DA27B5CE053E4E625AC9A175D72BF`
- authority/runtime/worker: `805F70466B503FEBBB14511E595A12E3AB42A583CED1A1D04193BF3DE04474DE`
- verifier: `67969A60704553AA6CFB134F8B7F67236757EA6A2CF51C9CD7C4EF3C8FA5A4DB`

Exact normalized rows:

```text
tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorFrozenC11Adapter.cs|44E629FD93D83FC0F9F6010B795258675455CA0C4899D464DADA6EDEAB0CC5E0
tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorInternalResearchRunner.cs|606211CBEC30B4FA929FAD24DD401013297568BE82F6025095B49AF13D1601AE
tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorIsolatedLauncher.cs|BF3D89A55F0148A0A15D64189F2D5A619E7D8B0FCC53711389DEEB9B983FC40A
tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorIsolatedRuntime.cs|4594E89FC244D3BC69EA3E36A85CABD72F38A900CC73CEE8C41D5D6424C7D21A
tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorPrebindingAuthority.cs|9E96B65573D16836279BDE47BC6C165876AB898A7BDFFC5575C45D571664B567
tools/ManiaAddNotesLab.Experiments/ManiaAddNotesLab.Experiments.csproj|8F0C8F639DD7A22658DFCB3E9F4995D865517FC42C5427B6C04A0F048D50D8BD
tools/ManiaAddNotesLab.IntegrationResearchWorker/ManiaAddNotesLab.IntegrationResearchWorker.csproj|CFE00E764EC99D311CFA0972E998C12008B1A001B5E364CDFE191959897B2C9F
tools/ManiaAddNotesLab.IntegrationResearchWorker/Program.cs|B15B231D9E400267F557E23EE807E2BB45B3421D12AC5580A8AF11359AB13B95
tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorDeepSemanticPackageVerifier.cs|3F5950D0C80232FEC4590E3FACCDB1F695FD422935CA6BCB9B1361DF031BB809
```

Final local SAFE validation: CorrectiveEvaluator `210/0/0`, main `977/0/0`, total
`1187/0/0`. Build and DocConsistency passed. These local results are not GitHub CI.
