# SAFETY.REMEDIATION.GATE — Final pre-recertification guards

## Scope

This addendum records the two pre-recertification guard corrections only. It does not alter engine behavior, causal classification, defaults, G1, HardValidity, remediation, or any historical report. The 224-pair matrix was not executed.

Repository entry state: `main` and `origin/main` at `6fb5ee6aa2a1f7cc7a11034db3f41978c42489e6`.

## DOCCONSISTENCY

`DocConsistency` now structurally validates both the focused follow-up contract and its unresolved-case result. The guard verifies:

- canonical contract content SHA-256 `645DAD6346130FBF0D4753053A35A40890A33FEE8F7F928768B8DDE9F4701083`;
- frozen implementation, historical harness, repository-entry and C11 corpus identities;
- immutable historical counts `188 / 22 / 5` and outcome `NEEDS_REVIEW`;
- five exact chart/seed/opportunity/target identities;
- five exact blocking LN identities, geometry, commit identity/order and governed lineage;
- the sole additive classification `D_CONFLICTING_OBJECT_ABSENT`;
- focused execution and corpus counts, non-execution of the full matrix, and unchanged defaults/HardValidity;
- exact object shapes, rejecting missing and unexpected properties.

Adversarial tests mutate every scalar leaf and every semantically relevant empty array independently: 12 contract bad controls and 147 result bad controls. Additional shape controls remove a required property and add an unexpected property. Every bad control must make the guard fail.

## Frozen C11 manifest

`FrozenC11Manifest.Load` now delegates to a shared verifier that reconstructs the exact historical canonical payload and recomputes its SHA-256. The reconstruction deliberately preserves the original serializer's inferred `KeyCount` member name, while reading the published camel-case artifact. The recomputed value must equal both the manifest's declared field and trusted historical identity:

`AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`

Tests cover the intact manifest, changed content with the old declaration, changed content with a correspondingly changed declaration, and missing/unexpected manifest properties. Existing synthetic temporary-corpus tests cover missing and unexpected `.osu` files, exact duplicates and metadata drift. No `Songs` directory is discovered or read by these tests.

## Harness identity

The historical follow-up harness identity `31FC70DE3C856D4E4EB9046520169C2A9E5899AE6E82777C390D366A3EE6E44F` remains frozen inside the historical contract and result. It is not rewritten.

The final pre-recertification guard harness has its own identity over the ordinal-sorted path/SHA-256 rows listed below, using `ExperimentBaselineIdentityResearch.ComputeImplementationSnapshot` semantics:

`407E6072870BE85F2C83A8DF8118E3F2A4919C251EE126764A5EEDDA5BB9D7DA`

Identity inputs:

- `src/ManiaAddNotesLab.Core/FrozenC11ManifestResearch.cs`
- `tools/ManiaAddNotesLab.Experiments/FrozenC11CorpusRunner.cs`
- `tools/DocConsistency/Program.cs`
- `tools/DocConsistency/SafetyRemediationFollowupGuard.cs`
- `tests/ManiaAddNotesLab.Tests/ManiaAddNotesLab.Tests.csproj`
- `tests/ManiaAddNotesLab.Tests/FrozenC11ManifestGuardTests.cs`
- `tests/ManiaAddNotesLab.Tests/SafetyRemediationFollowupGuardTests.cs`
- `tests/ManiaAddNotesLab.Tests/PhaseC11WitnessAgreementTests.cs`

## Verification

All commands ran with one build/test worker. The complete test run also inherited
`DOTNET_GCHeapHardLimit=0x400000000` (16 GiB).

- `dotnet build ManiaAddNotesLab.sln --no-restore -m:1`: PASS, 0 errors; one `NU1900` warning because the offline environment could not query NuGet vulnerability metadata.
- `dotnet test tests/ManiaAddNotesLab.Tests/ManiaAddNotesLab.Tests.csproj --no-build --no-restore -m:1 -- RunConfiguration.MaxCpuCount=1`: PASS, 774/774.
- focused manifest, follow-up and synthetic frozen-corpus tests: PASS, 11/11.
- `dotnet run --project tools/DocConsistency --no-build -- --check`: `DOCUMENTATION CONSISTENCY: PASS`.
- bad controls: PASS, all 12 contract leaf mutations, all 147 follow-up leaf/empty-array mutations, both structural document mutations, both manifest hash attacks, both manifest-shape mutations, and synthetic missing/unexpected `.osu` cases were rejected as intended.
