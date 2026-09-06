# Phase C1.2 — Exact-Head Relation Modeling / Shadow

Fecha de cierre: 2026-09-06  
Estado: **COMPLETE — OUTCOME A: RELATION MODEL EXPLAINS AGREEMENT**  
Efecto conductual: **ninguno**. `BehaviorPolicyVersion` continúa en `legacy-experimental.1`.

## Baseline

Antes de C1.2 se corrigió el drift entre README, status, roadmaps, audit y arquitectura; se creó `docs/PROJECT_STATE.json`, el protocolo permanente de cierre y `tools/DocConsistency`. El checker pasó antes de tocar el modelo.

Baseline técnico: restore PASS; 188/188 tests PASS. El build normal encontró la DLL de Web bloqueada por la interfaz abierta, sin error de código. El mismo solution completo compiló PASS en `--artifacts-path .artifacts/c1-2-baseline`, sin detener la Web. La advertencia `NU1900` corresponde únicamente a la consulta ambiental de vulnerabilidades NuGet.

Versiones de partida: behavior `legacy-experimental.1`, evidence profile `phase-a.1`, diagnostics `phase-c1-shadow.1`.

## Documentation synchronization

Las fuentes vivas ahora reflejan C1.1 multi-family y el principio **WITNESS IDENTITY ≠ RELATION COUNT**. `ObservationId` deduplica identidad de muestra; claims y relations conservan provenance propia. C1 quedó HOLD/reframed, C1.1 COMPLETE/Outcome B, C1.2 fue la única fase iniciada, C2 quedó deferred y D0 no fue autorizado.

Los reports A/B/C1/C1.1 no se reescribieron. Son evidencia histórica; `PROJECT_STATE` sólo los resume.

## Relation model

Se eligió una primitive pequeña `SimultaneousOriginalEventGroup`, porque un exact head puede contener taps y LNs. C1.2 especializa dentro de ella únicamente `ExactHeadEndpointRelation`; no crea una taxonomía de “LN patterns” ni intenta generalizar a composición compatible.

```text
SimultaneousOriginalEventGroup H
  Members: O17, O31, ...
  Endpoint R1 → witnesses O17, O31
  Endpoint R2 → witnesses O42
```

Cada member conserva lane, tipo, head/release en ms y beats, duración y exact timing provenance. Cada endpoint conserva sus witness IDs y claims. `DistinctWitnessCount` cuenta IDs; `DistinctEndpointCount` cuenta relaciones `H→R`. No existen weight, bonus, probability, confidence ni `MapperSupport`.

`ResearchSchemaVersion=phase-c1-2-research.1`. `EvidenceProfileVersion` permanece `phase-a.1` porque el perfil persistente no cambió. `DecisionDiagnosticVersion` avanza a `phase-c1-2-shadow.1` porque `SupportCertificate` incorpora arrays descriptivos de observed values/relations.

## Provenance

Para una LN O17:

- `ObservedValue`: Duration D y ExactRelease R;
- `ObservedRelation`: ExactHead(H) y HeadToRelease(H,R);
- independent witness: O17, una sola vez.

Dos labels no producen dos samples. Dos relaciones tampoco producen dos samples. A la vez, deduplicar O17 no elimina ninguna claim o relation real. El orden de grupos, endpoints, IDs y claims es determinista.

## Exact-head groups

La unidad reportada es todo timestamp/beat exacto que contiene al menos una LN. En 11 charts:

| Métrica | Count | Denominador |
|---|---:|---:|
| Exact-head groups | 7.206 | grupos con ≥1 LN |
| One-LN groups | 3.481 | 7.206 groups |
| Multiple-LN, same release | 994 | 7.206 groups |
| Multiple-LN, different releases | 2.731 | 7.206 groups |
| Mixed tap/LN groups | 2.537 | 7.206 groups |
| Groups with multiple LN | 3.725 | 7.206 groups |
| Groups with one endpoint | 4.475 | 7.206 groups |
| Groups with competing endpoints | 2.731 | 7.206 groups |
| Distinct `H→R` relations | 10.426 | 7.206 groups |
| Independent relation witnesses | 12.554 | 10.426 relations |

Las categorías usan sólo estructura discreta natural; no hay buckets temporales ni tolerancias.

## Same-release relations

Hay 1.643 relaciones exactas `H→R` con más de un witness independiente. Tras contar una vez la relación, contienen 2.128 witnesses repetidos adicionales. Esto representa agreement observado, no cuatro paths ni una regla de autoridad.

Las 994 groups multiple-LN/same-release son grupos puros de esta forma. Una group con endpoints competidores también puede contener repetición dentro de uno de sus endpoints; por eso el conteo de relaciones repetidas se mantiene separado de la categoría del grupo.

## Competing releases

2.731 groups demuestran más de un endpoint exacto. Cada endpoint conserva su count y IDs, por ejemplo `R1: O17`, `R2: O31`, con sample size dos. No se convierte `1/2` en “50% del mapper” ni en confidence. Esta estructura queda disponible para investigación composicional posterior, pero C1.2 no la resuelve.

## Held-out relation reconstruction

Para cada una de las 12.554 LNs se retiró sólo la target como donor. Los demás objetos de su exact-head group permanecieron. El resultado no consulta window, distance, candidate weight, geometry, lane selection ni RNG.

| Resultado | Count | Denominador |
|---|---:|---:|
| Same-head comparable context | 9.073 | 12.554 targets (72,27%) |
| No same-head relation | 3.481 | 12.554 targets (27,73%) |
| Exact target endpoint supported | 3.771 | 12.554 targets (30,04%) |
| Exact target endpoint supported | 3.771 | 9.073 comparable (41,56%) |
| Uniquely supported | 2.359 | 12.554 targets (18,79%) |
| Supported among competing relations | 1.412 | 12.554 targets (11,25%) |

`NoSameHeadEvidence`, `SingleEndpointRelation` y `CompetingEndpointRelations` son estados descriptivos. `DistinctAlternativeEndpoints` y `ExactTargetEndpointWitnessCount` se guardan por target. No existe score.

## Comparison to C1.1 population

C1.1 contó 14.828 convergencias candidate/path y mostró que 100% eran `SameHeadAgreement`. C1.2 no intenta igualar ese conteo de rutas: representa directamente los eventos originales `H→R` que causan el fenómeno.

La comparación causal relevante es completa:

- exact structural twins: **3.771/3.771** tienen su endpoint soportado por otro witness exact-head;
- targets worsened by UniqueWitness: **3.103/3.103** tienen su endpoint soportado;
- LOO-head-group C1.1 retiraba precisamente estos witnesses y hacía desaparecer agreement y toda diferencia de weights.

Así, el modelo explica la población donde deduplicar paths borraba señal sin afirmar que cada relation deba añadir autoridad. Los 5.302 targets con contexto exact-head pero endpoint no reconstruido muestran que compartir head no garantiza compartir release; quedan explícitamente como relación competidora/no soportada, no como fallo oculto.

## Per-family results

Las tres familias sin LNs se ejecutaron pero no entran como ceros artificiales en macro LN. Las ocho familias con LN muestran:

| Familia | K | Targets | Groups | Comparable | Supported | Unique | Competing-supported |
|---|---:|---:|---:|---:|---:|---:|---:|
| Midorigo Queen Bee | 7 | 21 | 20 | 2 | 2 | 2 | 0 |
| Hakanaki Mono Ningen | 4 | 1.570 | 902 | 1.201 | 148 | 38 | 110 |
| Kara Kara Kara no Kara | 7 | 3.958 | 2.239 | 2.876 | 1.238 | 745 | 493 |
| Spring of Dreams | 7 | 3.587 | 1.977 | 2.726 | 1.548 | 1.137 | 411 |
| Celestial Axes | 7 | 69 | 56 | 18 | 16 | 16 | 0 |
| Ko Inu | 7 | 3.000 | 1.746 | 2.092 | 673 | 275 | 398 |
| DESTINY | 7 | 296 | 230 | 124 | 118 | 118 | 0 |
| MikiMiki Romantic Night | 7 | 53 | 36 | 34 | 28 | 28 | 0 |

## Micro/macro

Micro sobre 12.554 targets: comparable 72,27%; supported 30,04% del total y 41,56% de comparables. Macro, promediando primero las ocho familias con LN: comparable 54,57%; supported 28,96%; uniquely supported 23,44%; supported-among-competing 5,52%. Se publican counts y denominadores; las ratios con denominador cero son N/A.

Artifacts versionados:

- `c1_2_chart_summary.csv`;
- `c1_2_family_summary.csv`;
- `c1_2_global_summary.csv`.

El JSON detallado (~30,7 MB) permanece local en `phase-c1-2-baseline/corpus/detail.json` y no contiene rutas personales absolutas.

## Corpus

Discovery read-only idéntico a C1.1: 1.212 `.osu` válidos, 1.200 outputs `[ADD …]` excluidos, 12 ubicaciones humanas, un duplicado SHA-256, 11 charts/familias únicos, ocho con LN, 4K/7K/10K y 12.554 LN targets. El corpus no cambió. Ningún chart, family o keymode dona evidencia a otro.

## Cross-key

El corpus humano aporta LNs en 4K y 7K; 10K está presente sin LN. Fixtures 1K/4K/7K/10K/18K verifican la misma implementación sin branches por K. No se afirma validación humana LN para 1K/10K/18K.

## Performance

La construcción/evaluación pura C1.2 acumuló 921,37 ms para los 11 charts. La recomputación C1.1 LOO-object usada sólo para enlazar la población worsened acumuló 50.063,93 ms y domina el runner. El modelo no se ejecuta en generation ni en diagnostics normales; el coste de corpus es research-only.

## Regression

- Behavior policy: `legacy-experimental.1`, sin cambio.
- Evidence profile: `phase-a.1`, sin cambio.
- Candidate set/order/active weights: legacy intactos.
- Output, RNG, lane selection y articulación: cubiertos por toda la suite y cinco hashes históricos byte-exact.
- C1.2 research no recibe ni consume `IRandomSource`.
- SupportCertificate sólo añade explanation; diagnostics ON/OFF conserva output y transcript.

## Tests

204 passed, 0 failed, 0 skipped. Los 16 casos nuevos cubren determinismo, exact timing provenance, same-release, competing releases, witness dedup, múltiples claims, held-out exclusion, ausencia de cross-chart evidence, 1K–18K, serialización, certificate relations, ausencia de score/confidence/MapperSupport, ausencia de RNG, behavior ON/OFF y state blocks.

El checker documental es un comando offline independiente y retorna non-zero ante report/master faltante, estado inválido, fase inexistente, contradicción de versiones o frases obsoletas.

## Decision gate

| # | Criterio | Resultado |
|---:|---|---|
| 1 | Representación determinista | PASS |
| 2 | Provenance completa | PASS |
| 3 | Witness identity deduplicada | PASS |
| 4 | Multiple claims preservadas | PASS |
| 5 | Same H/same R vs different R | PASS |
| 6 | Sin magic weights | PASS |
| 7 | Sin confidence | PASS |
| 8 | Sin MapperSupport | PASS |
| 9 | Sin behavior change | PASS |
| 10 | Corpus multi-family | PASS |
| 11 | Explica población agreement C1.1 | PASS: twins 3.771/3.771; worsened 3.103/3.103 |
| 12 | Multi-key sin branches | PASS |
| 13 | Legacy output exacto | PASS |
| 14 | RNG exacto | PASS |
| 15 | Candidate weights activos exactos | PASS |
| 16 | Lane selection exacta | PASS |
| 17 | Articulation exacta | PASS |
| 18 | Tests completos verdes | PASS |

**OUTCOME A — RELATION MODEL EXPLAINS AGREEMENT.**

Esto valida la representación, no un peso. La evidencia permite decir que repeated `H→R` explica por qué UniqueWitness perdía información en C1.1. No permite decir cuánto debería influir en generación.

## Next recommendation

Posible siguiente fase: **D0 — ChordCompletion Reconstruction / Shadow**, para estudiar otra familia de relaciones simultáneas y composición sin intervenir generation. D0 queda recomendado, no implementado ni autorizado por este cierre. C2 permanece **DEFERRED**. No se implementó C1.3 porque no fue necesario para explicar la población C1.1.
