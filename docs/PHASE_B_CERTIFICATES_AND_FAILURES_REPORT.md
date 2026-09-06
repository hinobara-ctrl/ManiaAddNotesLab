# Phase B — Certificates and explicit failure causes / Shadow Mode

Fecha: 2026-09-06.  
Resultado: **PHASE B COMPLETE — READY TO DESIGN PHASE C1**.  
Behavior policy: `legacy-experimental.1` (sin cambio).  
Evidence profile: `phase-a.1` (sin cambio).  
Decision diagnostics: `phase-b.1`.

Phase B explica decisiones de la policy legacy; no decide qué debería hacer una policy futura. No se implementaron C1, C2 ni cambios conductuales posteriores.

## Baseline verified

Antes de tocar código: 136/136 tests PASS; perfil original-only e independiente de Seed/AddChance/SelectedRange; added/replacements excluidos; determinismo y contrato pass 1 OFF/ON intactos. Se guardaron el snapshot `phase-b-baseline/pre-code/ManiaAddNotesLab`, cinco outputs y el CSV pre-Phase-B.

`EvidenceProfileVersion` permanece `phase-a.1`: no cambió extracción ni schema serializado del perfil. `BehaviorPolicyVersion` permanece `legacy-experimental.1`: no cambió generación. Diagnostics posee versión independiente `phase-b.1`.

## Documentation refinements

### Hard validity vs policy contract

- `HARD_VALIDITY_INVARIANT`: lane válida, duración positiva, no overlap, tiempos/serialización válidos y source no sobrescrito.
- `CURRENT_POLICY_CONTRACT`: original-only, no recursividad, segunda pasada, pass 1 OFF/ON idéntico y contratos revisables de `legacy-experimental.1`.

Una policy contract solo cambia con nueva `BehaviorPolicyVersion`; un invariant de validez nunca puede ser compensado por evidencia.

### Roadmap phase split

El roadmap queda dividido en A, B, C1, C2, D0, D1, E, F1, F2, G1, G2, H, I, J y K. Esto mantiene una hipótesis conductual principal por A/B.

### Definition of Done adjustment

La policy mapper-derived final exige `Active manually sourced style decisions = 0`. Documentar o versionar un magic number no basta. Una decisión estilística abierta debe resolverse o quedar inactiva en el default.

## Phase B pre-gate

Los diez gates fueron PASS: baseline verde, perfil original-only, selección intacta, diagnostics sin RNG, provenance paralela, identidad provisional, HardValidity independiente, output idéntico, métricas legacy idénticas y rollback por desconexión.

**PHASE B GREEN — SHADOW IMPLEMENTATION AUTHORIZED**

## Diagnostic architecture

```text
Legacy candidate + CurrentGeometry
        ├── bool/weight/order/RNG legacy → decisión vigente
        └── shadow opt-in
              ├── DiagnosticCandidateKey
              ├── SupportCertificate
              ├── HardValidityResult por lane
              ├── blocker provenance
              └── decision-diagnostics.json
```

Con `DiagnosticsEnabled=false` no existe collector. Con diagnostics activos, legacy calcula primero lanes y mantiene autoridad. La explicación se calcula sin counters legacy ni RNG; si las listas discrepan, se lanza una excepción. El perfil sigue frozen/original-only; blockers sintéticos describen CurrentGeometry, no estilo del mapper.

## DiagnosticCandidateKey

Combina `BehaviorPolicyVersion`, orden/tipo de oportunidad, source/parent `ObservationId`, start/end y orden candidate. Es **policy-local, diagnostic-only, versionada y provisional**. No reemplaza equality, sort ni merge legacy, y no promete estabilidad entre policies. `ObservationId` sigue identificando originales.

## SupportCertificate

Campos reales: key, claim level, scope, witnesses con IDs/tags, requisitos, generalizaciones, independent witness count y HardValidity separado. LN legacy produce `ObservedValue/ObservedSupport`. Tap/rice declara `Unknown/NotEvaluated` porque ChordCompletion y ComparableContext no existen. No hay confidence, MapperSupport, LocalMismatch inventado ni CompatibleComposition fingida.

## Failure taxonomy

Se distinguen `NoShapeEvidence`, `NoLegalLane`, blocker original hold/head/tap, synthetic tap/LN, replacement, spacing before/after, duración inválida, colapso a ms, outside lane y other. Cada candidate conserva todas las lanes; múltiples causas pueden coexistir, por lo que sus conteos no forman una partición.

## Blocker provenance

Cada blocker conserva source kind, lane, tipo, tiempos y `ObservationId` original o ID sintético diagnóstico. Un ID sintético nunca es evidence witness. Replacements están soportados por schema/tests aunque el pass 2 actual no genere oportunidades posteriores.

## LN witness diagnostics

Cada candidate LN conserva `LegacyWeight`, paths duration/release, IDs únicos, independent witnesses y duplicate paths. Fixture: una LN por duration + exact release = 2 labels, 1 ID, 1 witness, weight legacy 2.5. Dos LNs distintas = 2 witnesses, weight 5.0. C1 aún no modifica weights.

## Retrigger diagnostics

La consulta shadow conserva gap, `ReleaseToTap`/`ReleaseToLongNoteHead`, witness IDs, count, scope y same/cross-lane. Fixture 8:2 reporta A=8/B=2 mientras legacy mantiene soporte agregado. C2 queda sin implementar.

## Rice state diagnostics

Para taps considerados se guardan K, heads/held originales, heads/ocupación/lanes actuales antes, lane seleccionada y heads después. No se llama `SupportedChordState`: D0 no existe. Un roll fallido no construye candidate legacy y no figura como candidate evaluado.

## Interior diagnostics

Cada candidate interior conserva parent ID, provenance del anchor, start estricto dentro de parent y end `Contained`, `Crossing`, `EqualEnd` o `Invalid`. La clasificación no cambia eligibility.

## Articulation causal diagnostics

Cada intent saturado conserva gate legacy, held/non-held, candidates normales, retrigger evidence y resultado. Se clasifica `PureOriginalHoldBlock`, `MixedBlock` o `NoOriginalHoldBlock`. `BlockedByOriginalHolds=true` requiere una lane que quedaría libre retirando únicamente blockers de original hold; no basta encontrar un hold cualquiera. Esto no reemplaza el gate legacy.

## Export

CLI: `--diagnostics-output` y `--diagnostics-detail summary|relevant|all`. `relevant` (default) exporta placed + interior + no-shape; summary cubre todos. No hay sampling ni RNG. Web puede exportar la primera seed dentro del ZIP. Trace conjunto separa `[LEGACY DECISION]`, `[SHADOW EVIDENCE]`, `[SHADOW HARD VALIDITY]` y `[SHADOW RESULT]`.

Toda ratio incluye numerador, nombre/valor de denominador y ratio nullable; denominador cero es N/A.

## Real chart results

Spring of Dreams, 7K; ADD 50, seed 100, interior ON, articulation ON, defaults modernos. Observación, no ajuste de policy.

| Métrica | Resultado |
|---|---:|
| Diagnostic candidates / lane evaluations | 28,862 / 202,034 |
| LN candidates / evidence paths | 28,563 / 110,533 |
| Independent witnesses / duplicate paths | 107,900 / 2,633 |
| Lanes con blocker original / synthetic / spacing | 190,077 / 12,679 / 51,329 |
| Mixed candidates | 26,089 |
| Interior contained / crossing / equal-end | 189 / 344 / 33 |
| Legacy saturation eligible / actually original-hold blocked | 10 / 10 |
| Articulation classifications | 10 MixedBlock |

| Causa | Count | Denominador |
|---|---:|---:|
| NoLegalLane | 25,102 | 28,862 candidates |
| BlockedByOriginalHold | 39,565 | 202,034 lanes |
| BlockedByOriginalHead | 153,948 | 202,034 lanes |
| BlockedByOriginalTap | 14,831 | 202,034 lanes |
| BlockedBySyntheticTap / Ln | 323 / 6,173 | 202,034 lanes |
| BlockedBySpacingBefore / After | 45,952 / 8,130 | 202,034 lanes |

Blocker references: 256,243 originales y 12,679 added sobre 268,922. Los ratios por causa pueden superponerse. Retrigger observó gaps 0.12495, 0.2499, 0.4182, 0.4998, 0.7497, 0.75225 y 0.9996 separados por tipo/scope.

Artifact `phase-b-baseline/real/decision-diagnostics.json`: filtro relevant, 1,560 de 28,862 details, summary global completo.

## Behavioral regression

- Output equality: **PASS** — cinco SHA-256 pre/post idénticos.
- Decision equality: **PASS** — 101 columnas deterministas comunes × 5 rows, 0 diferencias.
- RNG equality: **PASS** — `NextDouble`, `Next(max)` y `Derive`, incluida articulation.
- Candidate equality: **PASS** — trace pre-code/post con secuencia exacta.
- Weights equality: **PASS** — weights exactos.
- Lane selection y articulation behavior: **PASS**.
- Spring ON/OFF: **PASS**, SHA-256 `8347A6DE885930C643814344D93D0A74A8094088B44E3BB41D3B28D93D10030D`.

## Tests

157 passed, 0 failed, 0 skipped. Cubren ON/OFF, RNG, keys, provenance, blockers, spacing mixto, legacy==diagnostic, witnesses, retrigger 8:2/tipos, interiores, articulation causal, tamaño UTF-8 exacto del export, JSON sin confidence/MapperSupport y 1K/4K/7K/10K/18K. Build Release PASS.

## Performance

Spring Release: Diagnostics OFF `Pass1Ms=268.944`, `ArticulationPassMs=8.744`; ON `1769.889` y `46.820`. Dentro de ON: certificates 244.895 ms, failures 251.370 ms, relevant JSON 24,993,073 bytes.

El prototipo `all` produjo ~455 MB. Se añadió filtro explícito e índice de provenance, reduciendo pass 1 ON observado de ~8 s a ~1.77 s sin quitar blockers. OFF no construye diagnostics.

## Open questions

1. Candidate semantic identity futura sigue abierta.
2. ComparableContext, LocalMismatch, CompatibleComposition y generalization siguen NotEvaluated.
3. C1 debe deduplicar authority sin confundir labels/testigos.
4. C2 debe decidir condición por tipo y same/cross-lane.
5. Los 10 intents reales fueron MixedBlock; G2 deberá definir causalidad conductual.
6. El volumen/mixed blockers requiere más charts, no ajuste sobre Spring.

## Phase C recommendation

Diseñar primero **C1 — LN witness deduplication A/B** como única hipótesis. Usar este baseline para comparar labels, witnesses, weights y outputs. No mezclar con C2. Ninguna de ambas fue implementada.

## Phase B acceptance gate

Los 20 criterios pasan: output/RNG/order/weights/lanes/articulation idénticos; legalidad concordante; provenance y causas distinguibles; duplicates, retrigger frequency, interior relations y causal articulation observables; sin confidence, MapperSupport ni ComparableContext prematuro; multi-key/profile/docs/rollback verdes.

**PHASE B COMPLETE — READY TO DESIGN PHASE C1**
