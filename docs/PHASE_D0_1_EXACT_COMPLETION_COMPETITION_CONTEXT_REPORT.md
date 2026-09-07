# Phase D0.1 — Exact Completion Competition Context / Shadow

Fecha de cierre: 2026-09-06  
Estado: **COMPLETE — OUTCOME A: EXACT CONTEXT HAS RECURRENT DISCRIMINATING POWER, WITH A COVERAGE BOUNDARY**  
Efecto conductual: **ninguno**. `BehaviorPolicyVersion` continúa en `legacy-experimental.1`.

## Pregunta y población

D0 demostró que una relation exacta `ReducedState → Completion(lane,type)` reconstruye el target en 37.080 de 40.360 trials, pero 36.387 targets quedan soportados entre varias completions. D0.1 pregunta si contexto original exacto puede separar esas alternativas, sin score, probabilidad, similarity, mirror, translation, patrón nombrado, ventana temporal ni cambio de generación.

Se conservan dos denominadores:

- población completa: **40.360 trials**;
- pregunta primaria: **36.387 `TargetAmongCompetingCompletions` de D0**.

Los 693 `UniqueExactCompletion`, 2.516 comparables sin target y 764 sin reduced-state comparable permanecen en el análisis secundario. Ningún resultado se rebautiza como `LocalMismatch`.

## Modelo exacto

`ExactCompletionContext` conserva por grupo:

- reduced state exacto;
- `HeldBeforeHeadLanes`, sólo LNs iniciadas estrictamente antes del timestamp;
- grupo original inmediatamente anterior y posterior, sin saltos ni ventanas;
- transición anterior/posterior con delta exacto en beats y su materialización en ms;
- IDs del grupo actual y de los vecinos, timestamps, beats y provenance;
- ocupación held ante el vecino posterior recalculada como si el grupo actual completo no existiera.

Las ocho vistas son consultas paralelas, no una escalera de backoff:

1. `ReducedOnly`;
2. `ReducedHeld`;
3. `ReducedPrevious`;
4. `ReducedPreviousTransition`;
5. `ReducedNext`;
6. `ReducedNextTransition`;
7. `ReducedPrevNext`;
8. `ReducedHeldPrevNext`.

`ReducedPrevNext` contiene ambas transiciones exactas. Los gaps usan `decimal` canónico sin redondeo; los milisegundos se guardan como provenance descriptiva y nunca participan en la identidad. Los átomos ausentes se representan explícitamente como `<ABSENT>`.

## Prevención de leakage

Para cada target se excluye el grupo completo de todos los donors, no sólo el member ocultado. El contexto target y el contexto donor se construyen con la misma función. Una LN iniciada en el grupo actual puede estar activa en el siguiente timestamp, pero su ID se retira antes de materializar `NextHeldBefore...`; por tanto la completion no reaparece indirectamente como ocupación futura.

Auditoría corpus:

| Riesgo | Violaciones |
|---|---:|
| Grupo target presente entre donors | 0 |
| LN target/donor filtrada presente en held futuro | 0 |
| Held tail codificada como head actual | 0 |
| Anidamiento declarado de donor sets roto | 0 |
| Diferencia beat decimal ↔ ms materializado | 0 |

La ocupación held futura saneada se conserva para auditoría, pero no integra ninguna de las ocho firmas de matching. Esto evita que una señal no solicitada vuelva las vistas más específicas.

## Estados descriptivos

Cada target/vista registra `BaselineCompletionCount`, `ContextualCompletionCount`, soporte exacto del target y witnesses independientes por grupo donor. Los outcomes son:

- `NoComparableExactContext`;
- `ComparableTargetUnsupported`;
- `TargetSupportedStillCompeting`;
- `TargetResolvedUnique`;
- `UniqueWrongCompletion`.

Una completion exige lane y tipo exactos. La misma lane con `TapHead`/`LongNoteHead` diferente queda separada y se informa con `LaneSupportedButTypeDifferent`. Los witness counts son conteos descriptivos, no frecuencia probabilística ni autoridad.

## Resultado primario: 36.387 competencias D0

| Vista | Sin contexto comparable | Target no soportado | Soportado y compitiendo | Target resuelto único | Único incorrecto | Resolución / 36.387 |
|---|---:|---:|---:|---:|---:|---:|
| ReducedOnly | 0 | 0 | 36.387 | 0 | 0 | 0,00% |
| ReducedHeld | 2.651 | 1.291 | 27.911 | 2.936 | 1.598 | 8,07% |
| ReducedPrevious | 9.097 | 2.396 | 15.062 | 6.303 | 3.529 | 17,32% |
| ReducedPreviousTransition | 17.562 | 1.162 | 8.319 | 6.075 | 3.269 | 16,70% |
| ReducedNext | 9.049 | 2.541 | 15.085 | 6.264 | 3.448 | 17,21% |
| ReducedNextTransition | 17.249 | 1.353 | 8.507 | 5.921 | 3.357 | 16,27% |
| ReducedPrevNext | 29.166 | 69 | 768 | 5.292 | 1.092 | 14,54% |
| ReducedHeldPrevNext | 29.647 | 36 | 674 | 5.148 | 882 | 14,15% |

El mayor número absoluto de resoluciones primarias aparece con el vecino anterior exacto: 6.303. El vecino siguiente produce 6.264, una señal casi simétrica. Añadir gap exacto reduce competencia, pero también elimina comparabilidad: por eso `ReducedPreviousTransition` resuelve un porcentaje menor del denominador total aunque sea más selectivo dentro de los casos que conserva.

La vista bidireccional ilustra el límite con claridad. `ReducedPrevNext` sólo conserva contexto comparable para 7.221 de 36.387 competencias, pero resuelve correctamente 5.292 de ellas. Es fuerte cuando existe recurrencia exacta y silenciosa en el resto. `ReducedHeldPrevNext` reduce aún más la cobertura a 6.740 comparables y resuelve 5.148.

## Reducción de alternatives competidoras

Las 36.387 competencias contienen 236.827 referencias a completions baseline: media 6,509 por trial. Al aplicar cada vista, sin imputar alternatives cuando no existe contexto:

| Vista | Alternatives contextuales | Media / 36.387 |
|---|---:|---:|
| ReducedOnly | 236.827 | 6,509 |
| ReducedHeld | 140.528 | 3,862 |
| ReducedPrevious | 58.899 | 1,619 |
| ReducedPreviousTransition | 32.888 | 0,904 |
| ReducedNext | 60.584 | 1,665 |
| ReducedNextTransition | 34.126 | 0,938 |
| ReducedPrevNext | 8.093 | 0,222 |
| ReducedHeldPrevNext | 7.463 | 0,205 |

La reducción no se interpreta sola como mejora: una media baja mezcla resolución real con `NoComparableExactContext`. Por eso siempre se publica junto a coverage y los cinco outcomes.

## Cobertura y resolución no son lo mismo

| Vista | Cobertura micro / 40.360 | Target soportado / 40.360 | Resolución primaria / 36.387 | Macro familias: cobertura | Macro familias: resolución primaria |
|---|---:|---:|---:|---:|---:|
| ReducedOnly | 98,11% | 91,87% | 0,00% | 98,20% | 0,00% |
| ReducedHeld | 87,36% | 77,83% | 8,07% | 89,51% | 8,33% |
| ReducedPrevious | 69,47% | 53,73% | 17,32% | 72,06% | 18,72% |
| ReducedPreviousTransition | 47,57% | 36,24% | 16,70% | 52,92% | 19,95% |
| ReducedNext | 69,78% | 53,88% | 17,21% | 72,36% | 18,62% |
| ReducedNextTransition | 48,37% | 36,31% | 16,27% | 53,24% | 19,70% |
| ReducedPrevNext | 18,27% | 15,34% | 14,54% | 22,03% | 18,07% |
| ReducedHeldPrevNext | 17,05% | 14,75% | 14,15% | 20,70% | 17,70% |

No se elige una “mejor vista” mediante un umbral manual. `ReducedPrevious` y `ReducedNext` ofrecen la mejor resolución absoluta con cobertura intermedia; las transiciones y el contexto bidireccional describen subconjuntos más puros pero mucho más pequeños. Esta frontera es un resultado, no un fallo que autorice backoff implícito.

## Población completa y tipo de target

En los 40.360 trials, `ReducedOnly` reproduce D0: 764 sin comparable, 2.516 targets no soportados —1.779 competidores y 737 únicos incorrectos—, 36.387 soportados con competencia y 693 únicos.

Hay 29.791 targets TapHead y 10.569 LongNoteHead. En la población completa:

- `ReducedPrevious` resuelve 5.231 TapHeads y 1.393 LongNoteHeads;
- `ReducedNext` resuelve 5.239 TapHeads y 1.422 LongNoteHeads;
- `ReducedPrevNext` resuelve 4.169 TapHeads y 1.256 LongNoteHeads;
- `ReducedHeldPrevNext` resuelve 4.138 TapHeads y 1.143 LongNoteHeads.

Los CSV estratifican además por tamaño de chord original, miembros visibles, presencia/ausencia de held y cantidad exacta de alternativas baseline. No se aplican weights de familia.

## Recurrencia multi-family

Las ocho vistas contextuales producen `TargetResolvedUnique` en las 11 familias. En `ReducedPrevNext`, por ejemplo, las resoluciones primarias por familia van desde 87 hasta 1.218; no dependen de un único chart ni de una única familia LN-heavy. La macro resolución familiar sigue la señal micro, por lo que Outcome A no proviene sólo del tamaño de los charts mayores.

## Timing exacto

Se registraron ambos deltas, beats `decimal` y milisegundos originales, para previous/next. No se fusionaron valores cercanos ni se aplicó tolerance. En este corpus hubo **0** transiciones donde el beat exacto materializado por `BeatTimeline` difiriera del timestamp observado. Esto describe el corpus actual; el schema conserva ambos valores para detectar una diferencia futura.

## Corpus y artifacts

Discovery read-only idéntico a C1.1–D0: 1.212 `.osu` válidos, 1.200 outputs `[ADD …]` excluidos, 12 ubicaciones humanas, un duplicado SHA-256 y 11 charts/familias humanos únicos en 4K/7K/10K. Se analizaron 50.836 objetos, 24.939 exact-head groups, 14.463 chord groups y 40.360 trials. Ningún chart o familia dona contexto a otro.

Artifacts versionados:

- `d0_1_global_summary.csv`;
- `d0_1_chart_summary.csv`;
- `d0_1_family_summary.csv`;
- `d0_1_context_view_summary.csv`.

El detalle completo permanece local en `.artifacts/d0-1/detail.json`: 568.710.119 bytes. La suma de resultados serializados chart por chart es 484.180.855 bytes; el artifact combinado incluye discovery, wrappers y serialización completa. No contiene rutas personales de runtime.

## Rendimiento

En los 11 charts, la construcción de contextos e índices acumuló 2.355,46 ms y el matching exacto acumuló 2.413,52 ms. La ejecución D0 baseline interna y la serialización del detalle se mantienen separadas de estas dos métricas. Nada de este coste entra a `AddNotesEngine`.

## Tests

Se añadieron 23 casos D0.1 que cubren los mínimos A–S:

- previous context que resuelve competencia, unique-wrong y contexto demasiado específico;
- held-before separador y held tails nunca convertidas en heads;
- previous/next state y gaps exactos;
- exclusión de grupo completo;
- ausencia de fuga target-LN y donor-LN hacia held futuro;
- anidamiento de donor sets;
- aislamiento cross-chart;
- ausencia de RNG y de cambios de generación;
- identidad exacta Tap/LN;
- implementación genérica 1K/4K/7K/10K/18K;
- metamorfismo por permutación de lanes.

La suite final contiene **243 passed, 0 failed, 0 skipped**. Los cinco snapshots históricos permanecen byte-exact. Spring ADD 50 seed 100 con interiores/articulación conserva SHA-256 `8347A6DE885930C643814344D93D0A74A8094088B44E3BB41D3B28D93D10030D`.

## Decision gate

| # | Criterio | Resultado |
|---:|---|---|
| 1 | Schema `phase-d0-1-research.1` separado | PASS |
| 2 | Behavior version intacta | PASS |
| 3 | Evidence version intacta | PASS |
| 4 | Diagnostic version intacta | PASS |
| 5 | Sólo original evidence | PASS |
| 6 | Sin RNG | PASS |
| 7 | Sin generación | PASS |
| 8 | Sin score/weight/probability/confidence | PASS |
| 9 | Sin similarity/mirror/translation/taxonomy | PASS |
| 10 | Ocho vistas paralelas | PASS |
| 11 | Previous inmediato | PASS |
| 12 | Next inmediato | PASS |
| 13 | Gap beat exacto | PASS |
| 14 | Provenance ms preservada | PASS |
| 15 | Sin rounding/tolerance/fusión | PASS |
| 16 | Whole-target-group exclusion | PASS |
| 17 | Held-before estricto | PASS |
| 18 | Target LN sin fuga futura | PASS |
| 19 | Donor LN sin fuga futura | PASS |
| 20 | Tap/LN completion exacta | PASS |
| 21 | Witness independiente por donor group | PASS |
| 22 | Sin transferencia cross-chart | PASS |
| 23 | Cinco outcomes completos | PASS |
| 24 | Baseline/contextual counts preservados | PASS |
| 25 | Denominador completo 40.360 | PASS |
| 26 | Denominador primario 36.387 | PASS |
| 27 | Estratos requeridos | PASS |
| 28 | Micro y macro separados | PASS |
| 29 | Recurrencia multi-family | PASS |
| 30 | Multi-key genérico | PASS |
| 31 | Anidamiento declarado sin violaciones | PASS |
| 32 | Rendimiento y artifact size medidos | PASS |
| 33 | Legacy generation/RNG/output sin cambio | PASS |

## Decisión

**OUTCOME A — EXACT CONTEXT HAS RECURRENT DISCRIMINATING POWER, WITH A COVERAGE BOUNDARY.**

La decisión se justifica porque múltiples familias contienen miles de context matches independientes que convierten competencia real en `TargetResolvedUnique`, sin leakage ni pérdida artificial del target. A la vez, la caída de cobertura es sustancial y queda explícitamente separada de resolución. Outcome A valida la representación y el poder discriminante descriptivo; no selecciona una vista, no crea autoridad y no autoriza generation.

## Próximo paso derivado

Recomendación: **D0.2 — Exact Context Coverage and View Agreement / Shadow**, para estudiar estabilidad y desacuerdo entre vistas sobre los mismos targets, conservar abstención y determinar si una composición explícita puede justificarse sin un ladder manual. D0.2 sería research-only y requeriría autorización propia.

**D1 no queda autorizado automáticamente.** `C2` permanece **DEFERRED** y C1 continúa HOLD para cambios de weights. No se modificó ninguna policy activa.
