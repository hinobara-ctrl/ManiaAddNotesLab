# LANE.0 corrective successor — integration audit remediation

Date: 2026-10-04  
Phase: `LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION_AUDIT_REMEDIATION`  
Status: `COMPLETE`  
Outcome: `READY_FOR_INDEPENDENT_REMEDIATION_AUDIT`  
Authority: `AUTHORIZED_FOR_INTEGRATION_AUDIT_REMEDIATION_IMPLEMENTATION_ONLY`

This remediation closes R1–R4 without changing product behavior, RNG, defaults, HardValidity, Phase 2 science, or the historical contracts. The historical execution contract remains immutable at `8763BC90212AF44947C6BAF3BD7FAADD1905289F0F8A1C3480199F771D3CAA0A`. Its former implementation identity is now historical evidence; an explicit live comparison rejects the remediated tree, while the new contract guards the live successor identities.

## R1 — independently observed authority: CLOSED

Authority-critical state is observed rather than accepted from caller claims: SourceRoot Git HEAD and tracked cleanliness; isolated HEAD, detached state and tracked cleanliness; live component identities; child assembly path/hash/base directory; and child-reported runtime HEAD. Contradictory source HEAD, runtime HEAD, dirty-tree and supplied-identity controls fail before receipt/science. `Lane0IntegrationLaunchRequest` no longer exposes caller-supplied observed fields.

## R2 — exact explicit path membership: CLOSED

Additional authorization used: `AUTHORIZED_FOR_FROZEN_C11_EXPLICIT_PATH_INVENTORY_READ_ONLY`.

- Frozen corpus contextual root: `.artifacts/f2-1-corpus`
- C11 access: **YES — READ_ONLY_EXPLICIT_PATH_INVENTORY_ONLY**
- Exactly 12 `.osu` locations were enumerated and read once for membership/SHA verification.
- Songs discovery: NO.
- Unrelated `.osu` reads: NO.
- C11 scientific evaluation: NO.
- C11 successor science execution: NO.
- C11 behavior experiment: NO.
- C11 RNG execution: NO.

The public authority artifact is `docs/lane_0_corrective_successor_frozen_c11_explicit_path_authority.json`, canonical identity `DA86B97DA2E0309BE4C5FC035E54397846EA5B2DBA6989BDEDE4424939EDF884`. Production loads this exact immutable authority; it no longer synthesizes `admitted/<SHA>.osu` membership. Missing, extra, renamed/substituted, wrong-content, duplicate, family, keymode, object-count, traversal and absolute-path controls fail closed. Rehashing a modified authority does not bypass the separately trusted identity.

Exact 12-location authority (`locationId | relativePath | SHA-256 | family | keys | objects | role`):

```text
LOCATION_00 | 20260906-131826-08255ad9b6104a4bac32bcbc4a445ed5/BUTAOTOME - Hakanaki Mono Ningen (YuEast 2018) [fake].osu | 0338ADEB4CDBB086D7819736292CB932608EA53DAD840AD3B8BBEAA605AC8349 | BUTAOTOME / HAKANAKI MONO NINGEN / YUEAST 2018 | 4 | 1933 | UNIQUE
LOCATION_01 | 20260906-131904-f5a5b697431649b9bf70ce8289507356/A-One - Side by Side (Kurisu Makise) [Lunatic].osu | DAC0347BBDC54E15A080316356E2E9A0CC2002927FF4B031D5F995C6C832F5AF | A-ONE / SIDE BY SIDE / KURISU MAKISE | 10 | 4001 | UNIQUE
LOCATION_02 | 20260906-131925-932f92eec34b42a0bbe1ef0b2ede4d5c/-45 - Midorigo Queen Bee (ItzBenja616) [Sooth].osu | B1F847192EBB07C44024AA8A4BA570FCCF04EAA03F92D5FC4DBA1EAF40C42249 | -45 / MIDORIGO QUEEN BEE / ITZBENJA616 | 7 | 4897 | UNIQUE
LOCATION_03 | 20260906-131946-3dda05e77fb04c549bcf02b2d695642f/Kikuo - Kara Kara Kara no Kara (xNett) [Karanival!].osu | 20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788 | KIKUO / KARA KARA KARA NO KARA / XNETT | 7 | 10142 | UNIQUE
LOCATION_04 | 20260906-132052-e0a90265f66f4602a029ae018f756893/nmk&wathue - Celestial Axes (Wonki) [Insane].osu | DD5D885E5A72729DF913D1010A32D145596DB465F94271AE54E5ECDD7A3C5C20 | NMK&WATHUE / CELESTIAL AXES / WONKI | 7 | 3098 | UNIQUE
LOCATION_05 | 20260906-132115-f5b43f7ecd4c4b3c85e2a5b98daa23ae/Samfree ft. SF-A2 Miki - MikiMiki Romantic Night (Nananana) [Miki Dance].osu | F0E7AD21B536EC13F9EE2F8C09913100A35BA2DFE1338C86F78452AA4391BE7E | SAMFREE FT. SF-A2 MIKI / MIKIMIKI ROMANTIC NIGHT / NANANANA | 7 | 4579 | UNIQUE
LOCATION_06 | 20260906-132131-824fc52bd41e4a9186d385ad3f715838/Akatsuki Records - Mizuiro Raindrop ([GB]V1do) [Lunat1c (Cut ver.)].osu | BEF3BB94655D3F4749FA91C3C535FD5CFB8387AB19B951DDA3D3877B705677D4 | AKATSUKI RECORDS / MIZUIRO RAINDROP / [GB]V1DO | 4 | 2241 | UNIQUE
LOCATION_07 | 20260906-132157-f3ed30353b874ea1af9e136183fe1c12/orgT - DESTINY (Baio) [Insane].osu | 322F995448A73713DCBECE3D140F4D516EF687DBC43CC140CFF14948566F906B | ORGT / DESTINY / BAIO | 7 | 1653 | DUPLICATE_PRIMARY
LOCATION_08 | 20260906-132325-64eca68cb6f6401a8fbd4034d5a921ea/orgT - DESTINY (Baio) [Insane].osu | 322F995448A73713DCBECE3D140F4D516EF687DBC43CC140CFF14948566F906B | ORGT / DESTINY / BAIO | 7 | 1653 | DUPLICATE_SECONDARY
LOCATION_09 | 20260906-132536-c81deb37e7374b3fbe1da9cbf9a63eec/nowisee - Ko Inu (ruka) [aphotic].osu | 02D9D1781418E7442956D4D901C8C611E58256556AE9A828E72128C3B5B8E3EB | NOWISEE / KO INU / RUKA | 7 | 5400 | UNIQUE
LOCATION_10 | 20260906-132704-cb69a956fc164cf9a7c21fe1b49dbb13/NJK Record feat. 3L - Spring of Dreams (ItzBenja616) [KNH - Lvl 81 (No SV)] CUSTOM.osu | E73B098AA0CB6FA3BECE45E9551EF225412F5E01F6398184093A40B944F0AAE0 | NJK RECORD FEAT. 3L / SPRING OF DREAMS / ITZBENJA616 | 7 | 4608 | UNIQUE
LOCATION_11 | 20260906-132812-c9792f6e6e054cca9f0a2e5a2d504b45/moro - Holy Bitch (ItzBenja616) [Selfishness].osu | BC0401B460251A3CD37EBB8FB6BCDE3D75B619F692C7E9373BF8FC84D089AD95 | MORO / HOLY BITCH / ITZBENJA616 | 7 | 8284 | UNIQUE
```

Verified invariants: 12 physical locations, 11 unique content identities, one two-location duplicate (`322F…906B`), 50,836 unique original objects, keymodes 4/7/10.

## R3 — deep semantic verification: CLOSED

| Artifact | Independent semantic cross-check |
|---|---|
| `chart_family_keymode_results.csv` | exact header; unique rows; chart/family/keymode; every historical and corrected numeric aggregate; exact equality with `scientific_summary.json` |
| `g1_historical_operational_cases.csv` | exact header/count; row identity and uniqueness; all values; booleans; exact 9+2 distribution; equality with summary |
| `g1_state_transitions.csv` | exact header/count; row identity and uniqueness; every state/support/operational/transition value; equality with summary |
| admitted/scientific corpus | `unique(LocationEvidence.ChartId) == ScientificCharts.ChartId`; keymode/object metadata equality; exact duplicate relationship |

Adversarial tests change each meaningful CSV field, recompute `sha256sums.txt`, and still obtain rejection. Checksums remain byte-integrity evidence, never semantic authority.

## R4 — actual isolated ExecutionRoot runtime: CLOSED

Provenance chain: authorized public SHA → real no-hardlink clone → detached exact checkout → Release build under `ExecutionRoot/.artifacts/integration-worker` → child `dotnet` process executing that assembly → child reports Git HEAD, assembly path/hash, base directory, execution root and worker marker → authority process independently re-observes Git HEAD/cleanliness and rehashes the assembly → only then may post-receipt synthetic/future-authorized science run.

The host no longer calls `science.Evaluate` for the real route. Merely changing `WorkingDirectory` cannot satisfy the assembly-origin checks. Synthetic low-fake tests use a temporary Git repository, detached checkout, real build and real child provenance. Real C11 was not supplied to this runtime.

## Frozen identities

- Remediation contract: `F2FD07BC0D120A7B2CA7350BA582141C4EDC3277AD06C6D9E92139244F8A39EB`
- Explicit path authority: `DA86B97DA2E0309BE4C5FC035E54397846EA5B2DBA6989BDEDE4424939EDF884`
- Adapter: `032F9660D8C0012C990983CE307CDA9D20B1C49116BEAA7F3C5E3DB1EBD875B0`
- Runner: `DAF26670B68F6621303DA6F3D49FD0E8B3DCDB61489E1AB0A33523267602F784`
- Launcher/runtime/worker: `498FF1C8CF83C54F5EF31F1D2C127CA688F36500C9CD25C34EE7E53DB6979DE9`
- Deep semantic verifier: `67969A60704553AA6CFB134F8B7F67236757EA6A2CF51C9CD7C4EF3C8FA5A4DB`

## Validation and stop boundary

Focused implementation R1–R4: 59/59 PASS. Focused contract/guard suite: 18/18 PASS. Full SAFE suites: 933/933 + 166/166 = 1,099/1,099 PASS, 0 failed, 0 skipped. DocConsistency and `git diff --check`: PASS. Historical old-route live comparison: expected rejection. Rehashed semantic bad controls: expected rejection.

binding created: NO  
real receipt created: NO  
real successor execution: NO  
behaviorChange: false  
rngChange: false  
defaultChange: false  
productExposure: false  
nextRequiredAction: `HUMAN_REVIEW_REQUIRED`

Final verdict: **INTEGRATION AUDIT REMEDIATION READY FOR HUMAN PUBLICATION**.

Post-publication required action: `INDEPENDENT_REMEDIATION_AUDIT`.
