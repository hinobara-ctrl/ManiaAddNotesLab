# Phase D1 — Pre-run frozen behavioral A/B design

**Status: FROZEN BEFORE THE FIRST C11 TREATMENT OUTPUT.** This certificate authorizes execution only when the synthetic/build/control gates below pass. It does not authorize default promotion, tuning, community data or playtesting.

## Immutable identities

- Initial repository HEAD: `77638edad9b41c8d6e3a361ce68c296ed12c90bc` (`Complete D1.0 resulting-state composition feasibility research`).
- D1.GATE contract: `D574631B3E713AC3D08C159B605A742D50C907B1BB4589D7FCADCFA8027A7824`.
- Run manifest: `4C87F8B98B5B22B81CD903F26E3EF861B608893B50B46DBC20AF13158E8A4F00`.
- Frozen C11 corpus: `DDB14C468FB657B24D2A63BC77382526B8D10C3BA8748133F3E4F6068BD43887`.
- Uncommitted implementation source snapshot: `AB8E92F03E10128E78CF7E354CA1D99F01D8D68327A2843EEE55B55FDEC37395`, calculated from the ordered SHA-256 identities of `AddNotesEngine.cs`, `Model.cs`, both D1 Core files, the D1 runner and the D1 behavioral test file. No commit is created by this phase.
- Control: `legacy-experimental.1`.
- Treatment: `d1-resulting-state-ab.1`, experiment-only and never default.

## Frozen corpus

Exactly 11 SHA-256-deduplicated historical C11 charts, 11 independent families and keymodes 4K/7K/10K. Generated `[ADD …]` files, the duplicate ORGT location, samples and later community charts are excluded. Runtime paths are deliberately absent from semantic identity.

| Chart SHA-256 | Family | K | Objects | Tap | LN |
|---|---|---:|---:|---:|---:|
| `02D9D178…B8E3EB` | NOWISEE / KO INU / RUKA | 7 | 5,400 | 2,400 | 3,000 |
| `0338ADEB…C8349` | BUTAOTOME / HAKANAKI MONO NINGEN / YUEAST 2018 | 4 | 1,933 | 363 | 1,570 |
| `20651C9B…2C788` | KIKUO / KARA KARA KARA NO KARA / XNETT | 7 | 10,142 | 6,184 | 3,958 |
| `322F9954…F906B` | ORGT / DESTINY / BAIO | 7 | 1,653 | 1,357 | 296 |
| `B1F84719…C42249` | -45 / MIDORIGO QUEEN BEE / ITZBENJA616 | 7 | 4,897 | 4,876 | 21 |
| `BC0401B4…89AD95` | MORO / HOLY BITCH / ITZBENJA616 | 7 | 8,284 | 8,284 | 0 |
| `BEF3BB94…5677D4` | AKATSUKI RECORDS / MIZUIRO RAINDROP / [GB]V1DO | 4 | 2,241 | 2,241 | 0 |
| `DAC0347B…2F5AF` | A-ONE / SIDE BY SIDE / KURISU MAKISE | 10 | 4,001 | 4,001 | 0 |
| `DD5D885E…A3C5C20` | NMK&WATHUE / CELESTIAL AXES / WONKI | 7 | 3,098 | 3,029 | 69 |
| `E73B098A…0AAE0` | NJK RECORD FEAT. 3L / SPRING OF DREAMS / ITZBENJA616 | 7 | 4,608 | 1,021 | 3,587 |
| `F0E7AD21…91BE7E` | SAMFREE FT. SF-A2 MIKI / MIKIMIKI ROMANTIC NIGHT / NANANANA | 7 | 4,579 | 4,526 | 53 |

Full identities are in `d1_behavioral_ab_run_manifest.json`.

## Seed protocol and paired matrix

The audit found no justified population seed model. Earlier technical runners use transparent consecutive deterministic sets: 1–20 for cross-key matrices and 1–100 for focused Spring replications. D1 freezes **seeds 1 through 20 inclusive** as a moderate deterministic replication set across all 11 charts. They are not random samples, not independent human evidence and support no p-values or confidence intervals.

Matrix: 11 charts × 20 seeds × one frozen configuration = **220 paired runs**, each producing exact CONTROL and TREATMENT arms from the same chart, seed and options.

## Frozen generation configuration

- AddChance: `0.50`, the canonical intensity used by the prior focused LN experiments.
- Range: full chart (`StartMs=null`, `EndMs=null`). Range would filter opportunity sources only; evidence always remains full-chart original-only.
- Density: grace 2, decay 0.65, simultaneous-head/keymode-relative, contextual normalization/keymode-relative, windows 1/4 beats, threshold 1.5, factors 0.45/0.60/0.80.
- LN: window 4, affinity 1.25, distance decay 0.20, minimum weight 0.20, local gap window 4/support 2/fallback 0.125, map-relative snap.
- Interior: enabled, maximum 2, minimum source 3 beats, ratio 1.5, absolute long 8, context 3, anchors 2, `AnchorSupported`, `OriginalOnly`.
- Articulation: enabled, max non-held 1, max per parent 1, retrigger window 4/support 2. Articulation remains outside D1 and uses unchanged legacy behavior.
- Trace and legacy diagnostics: disabled in the corpus run; D1 causal detail is mandatory and separate.

The manifest serializes every `AddNotesOptions` field; this prose is explanatory, not an alternate configuration.

## Frozen intervention

Primary denominator: `D1EligibleDecisions`. One decision exists only after chance, exact lane/LN shape selection and hard validity, when exactly one pass-1 synthetic head already exists at that timestamp and the prospective set has cardinality two. `ReducedOnly` exact same-occurrence evidence maps joint unique/among-alternatives to admission and marginal-only/no-comparable/not-observed to abstention. The gate consumes zero RNG and never rerolls. k=0→1, k≥2→3+ and articulation remain legacy.

Direct metrics, downstream metrics, k≥3 metrics, chart/family/keymode/pair-type strata, rice/LN sentinels, safety, output hashes and first-divergence RNG attribution are exactly those in the D1.GATE contract. Public aggregates must retain zero-eligibility charts/runs.

## Gates before human execution

The human A/B may run only after: contract and manifest hashes match; this certificate exists; C11 identity matches; synthetic behavioral tests pass; baseline restore/build/full tests pass; disabled treatment reproduces exact legacy bytes/RNG; gate RNG is zero; and default policy remains legacy. Any failure means `HUMAN A/B = NOT RUN`.

## Frozen outcomes and stopping

- **Outcome A:** exact deterministic contract execution, safety/rollback pass, nonzero admissions and abstentions, attributable effects across multiple families for cross-family claims, no collapse or hidden rule, and downstream/k≥3 remain interpretable. Allows only technical replication or separately authorized playtesting.
- **Outcome B:** technically valid and attributable but restricted, abstention-dominant, narrow, strongly reducing, or materially obscured by downstream/k≥3 interaction. No tuning or promotion.
- **Outcome C:** degenerate, unsafe, nondeterministic, unattributable or contract-violating. Roll back and park/redesign.

Immediate abort conditions remain exactly those frozen by D1.GATE: evidence contamination, cross-chart/cross-occurrence fabrication, gate RNG, out-of-scope query, reroll, control drift, source mutation, parse/overlap failure, determinism failure, manifest/contract mismatch, missing provenance or any view other than `ReducedOnly`.

## Prohibited interpretation

This experiment measures causal structural behavior, not naturalness, fun, preference, difficulty appropriateness, mapper approval or final policy quality. Counts are workload/prevalence, not independent observations. Marginal-only admission being zero is tautological and cannot determine Outcome A. No seed, option, view, scope, taxonomy, metric or stopping rule may change after the first treatment output.
