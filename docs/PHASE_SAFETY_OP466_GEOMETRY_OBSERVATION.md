# SAFETY.REMEDIATION — OP-466 canonical geometry observation

## Scientific outcome

The preregistered passive query returned `LANE_2_ILLEGAL` in both exact parent states:

| Arm | Parent state | Lane 2 at 35009 ms / beat 74.49995000000002979998 |
| --- | --- | --- |
| canonical A | normal canonical trajectory | `LANE_2_ILLEGAL` |
| canonical B | the original single OP-185 remap, then normal canonical | `LANE_2_ILLEGAL` |

In both arms, the immediately preceding lane-2 object is the same committed synthetic LN: it starts at 34783 ms (beat 73.998983333333362932926666667) and ends at exactly 35009 ms (beat 74.49995000000002979998). The frozen canonical rule rejects the hypothetical tap because `previous.EndBeat >= beat`; equality is intentionally inclusive. The next lane-2 object is an original LN at 35460–35572 ms and is not the rejecting boundary. The complete legal-lane set at that beat is `{1, 3}` in each arm.

This is an additional hypothetical geometric barrier, not the observed cause of the historical abstention. OP-466 actually stopped earlier at the probability roll: the authentic value was `0.7305439518441185` against effective chance `0.5`. No candidate or lane selection occurred. Consequently this phase does not answer whether the exact historical target would have been selected or committed after a successful roll, and it does not reclassify the historical case. OP-466 remains `C_UNRESOLVED`; the global state remains `NEEDS_REVIEW / NO PROMOTION`.

## Entry state and scope

- Entry branch: `main`.
- Entry HEAD: `e6a0fbe748c32b6ad39b823e031e8ccce9f963cf`.
- Remote verification was attempted with `git ls-remote origin refs/heads/main`, but GitHub was unavailable over port 443. Remote synchronization is therefore not claimed.
- Historical behavioral fingerprint: `B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835`.
- Frozen C11 manifest: `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`.
- Chart: `20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788`; seed 4 only.
- No Songs discovery, other chart, other seed, legacy rerun, 224-pair matrix, forced OP-466, product behavior change, commit, push or publication was performed.
- The three personal untracked documents named in the task were not read, edited or included.
- All experiment executions used `DOTNET_GCHeapHardLimit=0x400000000` and `DOTNET_GCConserveMemory=9`, sequentially.

The historical inputs from `safety_op185_op466_counterfactual`, `safety_op466_candidate_admission` and `safety_op466_jules_handoff` were treated as read-only. Their individual hashes are frozen in the new contract, including the Jules ZIP.

### Reproducing the historical v3 environment

The v3 contract intentionally binds `repositoryEntryHead` to `e6a0fbe748c32b6ad39b823e031e8ccce9f963cf`, implementation identity `B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835`, harness identity `0165ACDD45F8122F553E797D16436D22133F88BA23EF70843B5AF9FCC8761B1B`, and C11 identity `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`. The experiment sources and evidence were published later in closure commit `be08f3f2f0191a5c972ad16449a7199dd07f2e3f`; therefore a clean checkout of that later commit is suitable for inspecting and hashing the published evidence, but it is not by itself the exact v3 entry environment and must fail the frozen entry-HEAD guard by design.

An exact replay must use an isolated worktree at the frozen entry HEAD, overlay the exact published experiment files whose combined identities match the frozen implementation and harness hashes, supply only the explicit C11 directory verified against its frozen manifest, and preserve every historical input at the hashes listed in the contract. Run sequentially with `DOTNET_GCHeapHardLimit=0x400000000` and `DOTNET_GCConserveMemory=9`. The contract, entry-HEAD check, canonicalization rule and identity checks must not be edited or relaxed to make a later checkout pass; doing so would create a different experiment. If the historical overlay cannot be reconstructed exactly, the honest result is `BLOCKED`, while artifact verification remains possible independently.

## Technical viability and inspected authority

The observation was viable without adding a product hook. The audit covered `AddNotesEngine`, `LaneGeometryIndex`, `CanonicalPlayableGeometry`, `SafetyRemediationGateSufficientState`, `SeededRandom`, `IRandomSource`, `SafetyOp185Op466CounterfactualRunner` and `SafetyOp466CandidateAdmissionRunner`.

`AddNotesEngine` already offers an experiment-only `SafetyRemediationPlacementObserverResearch`. It receives every committed `TimedManiaObject` before insertion, including its durable times and latent beats, and has no decision or RNG API. Existing remediation diagnostics retain every opportunity state and committed-object identity. Pairing those two complete streams allows the harness to reconstruct the exact prefix from all original objects plus all synthetic commits before opportunity order 468.

The engine calls the observer immediately before adding the object to the normal collections and completing its diagnostics. Between `BeginOpportunity` and the OP-466 probability roll, the relevant code performs reads and statistics only; no geometry mutation occurs. Therefore the reconstructed state is equivalent to the required instant immediately before the roll. No engine duplication or product modification was used.

The harness projects each original and committed object through `CanonicalPlayableGeometry`, preserving `StartTime`, `EndTime`, `StartBeat`, latent `EndBeat`, type, origin, sequence and commit order, and inserts it into an isolated canonical `LaneGeometryIndex`. It validates both the raw materialized geometry accumulator and the exact composite historical generation-state hash. The passive query compares `InspectTapLane` with `FindLegalTapLanes` on that verified clone.

## Preregistration and invalidated pre-query attempts

The official v3 contract was frozen before the official lane query:

- schema: `safety-op466-geometry-observation-contract.3`;
- canonical contract SHA-256: `07B5678CD73B40583A5C79321846FB8B6D856EF99B75C4B68AF488ED3AF86928`;
- repository, implementation, harness, corpus, historical inputs, A/B definition, target opportunity, complete-state definition, validity rules, lane-query semantics, non-interference rules, outcomes and stop conditions are embedded in the contract;
- official files: `docs/safety_op466_geometry_observation_contract_v3.json` and `.artifacts/safety_op466_geometry_observation/v3/frozen_contract.json`.

Two earlier executions were invalidated before any lane-2 query and are retained rather than overwritten:

1. v1 found that its snapshot error did not distinguish raw and composite hashes. It produced no scientific result and originally surfaced as the Windows managed-exception dialog `0xe0434352` because the command boundary did not catch the exception.
2. v2 added a clean command-boundary failure and distinguished the hash types, then demonstrated that it had compared the raw `LaneGeometryIndex.MaterializedStateHash` with `SafetyRemediationGateSufficientState.MaterializedGenerationStateHash`. The latter is a composite over geometry, RNG positions, cursor, pending articulation and opportunity-sequence hash. It also stopped before the lane query.

Their contracts and invalid-run records remain under `.artifacts/safety_op466_geometry_observation/` and `v2/`, with corresponding documentation copies. v3 computes the historical composite with the exact SHA-256 UTF-8 rule. Focused fixtures passed before v3 was frozen.

## Exact A/B reproduction and snapshot validation

Both arms used independently parsed charts, profiles, observers, RNG instances, materialized indices and latent state. The only B intervention was the preregistered OP-185 delivery at RNG position 260: authentic `Next(4)=2`, delivered value 3 once. No later intervention occurred.

| Check | A | B |
| --- | --- | --- |
| Output SHA-256 | `462307AE599D390CCC56595CCBE9ABAB02D36EF35AC4E00A69D270963438E999` | `5902D77132A36F1DBF8286FA41731FD6F62403BA4CEA8A2EFC3B5C2F4986671B` |
| RNG calls | 13730 | 13730 |
| RNG transcript | `8F5589B0045254ECCAE68DFD3E01037D8C5771CA6BD7081132B173C2B6762DED` | same |
| Diagnostics | `26804F0A5753C75738C7CC14286CDA090C847292242649CFBBD3CCE534D74875` | `A82C7CD93DC6B38ACCF211AD7AEB198A76492ABCE285F2AE188965DD463E0478` |
| Full-run commits | 2839 | 2839 |
| Observer RNG calls | 0 | 0 |
| Pre-OP-466 commits | 144 | 144 |
| Complete snapshot objects | 10286 | 10286 |
| Parent RNG position | 659 | 659 |

The opportunity is exactly `OP-00000466-BaseHead-S466-T35009-ANA`, order 468. Both arms preserve effective chance `0.5`, consume the same authentic roll, and reproduce `ProbabilityAbstain`.

Snapshot identities:

| Identity | A | B |
| --- | --- | --- |
| Raw materialized geometry | `099352630977A567D8B25CC410D7F45E986BD5E1E5AF392C8FD69481050C8F6E` | `D563ABC24CDD09F46563391C5C9E73D5A9AF774289E22F5AC5593243C35F20C5` |
| Composite materialized generation state | `4567EC31F2655CF21F2EFD8C84AA1D2488DF9424EDA3C82AFCB1ADB924828B5C` | `C30CD699FB56765CA9C3D579F92B437B69AE6CC71E30D82A2E5726124ED8FBDB` |
| Latent state | `2581D7B31BFF43228337769B0A3CED8DE2488C259C0CD751C5F4AE464E613868` | `7ED12FCF44F64341FA5EB707AE3C0F53A982FD6F6BA628D811550B0DBA6DD160` |
| Opportunity sequence | `A5809E4C8EE8788A2BEB695DCE511751034B687009496F658EEA8DB63D48753F` | same |
| Serialized complete objects | `DBA8FF5A0C31C3602F65AAEF25A4DB64CAA59EDC60332911AF1654104795B641` | `EBA1F33CF65E5E3FEF2F7638BA2B9C0C38C473BFF90CB0C4F2240619151DEF3C` |

The complete snapshots are intentionally retained only in `.artifacts/`. They include every original object and pre-target synthetic commit; no LN `EndBeat` is reconstructed from `EndTime`.

## Passive query and no interference

For each arm, `InspectTapLane(2, beat)` and membership in `FindLegalTapLanes(beat)` agreed. Before and after the query:

- the raw geometry hash is identical;
- RNG position remains 13730 (zero calls added);
- commit count remains 144;
- no object is constructed, inserted or altered;
- effective chance and the historical trajectory are untouched.

Although A and B have different global geometry fingerprints, their local lane-2 predecessor and successor at OP-466 are identical. The inclusive endpoint collision with the preceding synthetic LN explains both `LANE_2_ILLEGAL` outcomes.

## Tests and adversarial controls

The focused tests cover legal and illegal lanes, endpoint equality, preservation of LN `EndTime` and latent `EndBeat`, complete population and fingerprint rejection, and absence of geometry/RNG/commit mutation. The synthetic fixtures are instrument checks only and are not substituted for A/B evidence.

Final validation passed with **829/829 tests** (the documented 826 baseline plus the three focused instrument tests), zero build errors, `DOCUMENTATION CONSISTENCY: PASS` and clean `git diff --check`. Restore emitted only `NU1900` because the NuGet vulnerability feed was unavailable; all projects were already restored. The official run rechecked the implementation fingerprint as `B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835`, and no implementation file changed afterward. The required commands were run sequentially with the 16 GiB heap limit:

```text
dotnet restore ManiaAddNotesLab.sln --disable-parallel
dotnet build ManiaAddNotesLab.sln -c Release -m:1
dotnet test ManiaAddNotesLab.sln -c Release -m:1
dotnet run --project tools/DocConsistency -c Release -- --check
git diff --check
```

## Evidence inventory

The official evidence is under `.artifacts/safety_op466_geometry_observation/v3/`:

- `frozen_contract.json`
- `source_inventory.json`
- `preflight_controls.json`
- `arm_identities.json`
- `no_interference.json`
- `snapshot_A.json` and `snapshot_B.json`
- `lane2_query_A.json` and `lane2_query_B.json`
- `scientific_summary.json`
- `sha256sums.txt`

The behavioral engine and classifier were not changed. Source changes are limited to the experiment runner and command routing, its three focused tests/project link, the versioned contracts, artifacts and this report. The task made no Git commit and staged no file.

## Limits and justified next step

The result establishes only question B: lane 2 is geometrically illegal in both exact parent states. Question A remains answered by the prior admission study: the actual stop was the probability roll. Question C—what exact lane/candidate would be selected and committed after a successful roll—is not answered here.

No automatic promotion or historical reclassification follows. If question C is scientifically necessary, it requires a separately preregistered counterfactual that crosses the probability boundary while preserving and accounting for subsequent RNG semantics. That is a new intervention and should not be inferred from this passive observation.
