# Phase D0 — Chord Completion Reconstruction / Shadow

Fecha de cierre: 2026-09-06  
Estado: **COMPLETE — OUTCOME A: EXACT CHORD-COMPLETION RELATIONS ARE RECONSTRUCTIBLE AND EXPLANATORY**  
Efecto conductual: **ninguno**. `BehaviorPolicyVersion` continúa en `legacy-experimental.1`.

## Baseline y alcance

El cierre documental C1.2 estaba publicado y el árbol limpio antes de comenzar. `DocConsistency`, restore y 204/204 tests pasaron. La solution completa compiló mediante un artifacts path aislado porque la interfaz Web mantenía su DLL normal abierta; no hubo error de código.

D0 investigó una sola hipótesis: si relaciones exactas entre estados de heads simultáneos del mismo chart pueden reconstruir un miembro oculto sin taxonomy externa, tolerancia, mirror, translation, lane equivalence, score ni cambio conductual. No se implementaron D1, C2, density replacement, weights, chance, planner, `MapperSupport` ni acumulación de additions.

## Representación

`ExactHeadRelationResearch.BuildAllGroups` expone la primitive C1.2 `SimultaneousOriginalEventGroup` también para grupos tap-only. C1.2 continúa filtrando su vista LN como antes. D0 selecciona exclusivamente grupos con dos o más heads originales exactos.

```text
OriginalHeadState
  KeyCount
  TapHeadLanes
  LongNoteHeadLanes
  Member ObservationIds
  HeldBeforeHeadLanes  (contexto separado)

Reduced exact head state
  → CompletionMember(lane exacta, TapHead | LongNoteHead)
  → observed full head state
```

La relation key contiene `KeyCount`, lanes tap reducidas, lanes LN-head reducidas, completion lane y completion type. No contiene timestamp absoluto, por lo que una estructura puede repetirse en otro momento del mismo chart. Timestamp/beat exactos y todos los IDs se conservan en provenance. Cada occurrence completa es un relation witness; enumerar members, labels o serializaciones no multiplica samples. Dos grupos originales diferentes que demuestran la misma relation aportan dos witnesses independientes.

Research schema: `phase-d0-research.1`. `EvidenceProfileVersion` permanece `phase-a.1` y `DecisionDiagnosticVersion` permanece `phase-c1-2-shadow.1`: D0 usa estructuras research separadas y no modifica export diagnostics.

## Held-before safety

`HeldBeforeHeadLanes` incluye sólo LNs originales con head estrictamente anterior al timestamp/beat del chord y release igual o posterior. Una LN que comienza exactamente en el chord es `LongNoteHead`, nunca held context. Las held lanes no entran en `TapHeadLanes`, `LongNoteHeadLanes` ni completion members.

Fixtures específicos prueban ambos límites. Held tails tratados como heads: **0**.

La reconstrucción primaria compara exact head state ignorando held occupancy, mientras que una sensibilidad separada exige además contexto held exacto. D0 no decide que contextos held diferentes sean equivalentes ni elige una policy de generación.

## Leave-one-head-group-out

Para cada chord target se excluye el grupo completo de todos los donors y luego se evalúa cada member oculto por separado. Los donors pertenecen siempre a otro exact-head group del mismo chart. Cada trial queda en uno de cuatro estados excluyentes:

- `NoComparableReducedState`;
- `ComparableButTargetUnsupported`;
- `UniqueExactCompletion`;
- `TargetAmongCompetingCompletions`.

Success primario exige lane y head type exactos. `LaneSupportedButTypeDifferent` se reporta separadamente y nunca cuenta como success. D0 reconstruye un solo member por trial y no combina additions.

## Corpus

Discovery read-only reutilizó exactamente la política C1.1/C1.2: búsqueda recursiva, exclusión de nombres `[ADD …]`, deduplicación binaria SHA-256 y cero escrituras al source. Se evaluaron también charts sin LN, porque D0 usa taps y LN heads.

| Métrica | Resultado |
|---|---:|
| Families | 11 |
| Charts humanos únicos | 11 |
| Keymodes humanos | 4K, 7K, 10K |
| Original objects | 50.836 |
| Exact head groups | 24.939 |
| Chord groups (≥2 heads) | 14.463 |
| Completion trials | 40.360 |

No existe validación humana 1K/18K en esta muestra. Fixtures automatizados cubren 1K/4K/7K/10K/18K con una implementación única.

## Chord groups y estados observados

| Métrica | Count | Denominador |
|---|---:|---:|
| Tap-only chord groups | 9.242 | 14.463 groups |
| LN-head-only chord groups | 2.684 | 14.463 groups |
| Mixed Tap/LN-head groups | 2.537 | 14.463 groups |
| Groups with held-before context | 5.311 | 14.463 groups |
| Distinct reduced states, sum per chart | 2.634 | chart-local states |
| Distinct completion relations, sum per chart | 7.669 | chart-local relations |
| Independent relation witnesses | 40.360 | relation occurrences |
| Distinct observed full states, sum per chart | 2.292 | chart-local states |

Chord groups por head count: 2→6.912, 3→4.381, 4→2.555, 5→540, 6→52 y 7→23. No se crearon bins; son counts naturales de los estados observados.

## Held-out reconstruction

| Resultado | Count | Denominador |
|---|---:|---:|
| Comparable trials | 39.596 | 40.360 (98,11%) |
| No comparable reduced state | 764 | 40.360 (1,89%) |
| Exact target completion supported | 37.080 | 40.360 (91,87%) |
| Exact target completion supported | 37.080 | 39.596 comparable (93,65%) |
| Unique exact completion | 693 | 40.360 (1,72%) |
| Target among competing completions | 36.387 | 40.360 (90,16%) |
| Comparable but target unsupported | 2.516 | 39.596 comparable (6,35%) |
| Lane supported but type different | 13.424 | 40.360, descriptivo |

La reconstruction exacta aparece en las 11 familias. El resultado no depende de una cifra umbral para ser Outcome A: estructuralmente, existen relations recurrentes chart-local en todas las familias y reconstruyen el target exacto en 37.080 trials. A la vez, la mayoría son completions competidoras; D0 explica que el mapper repitió la relation, no cuál alternativa debe elegirse.

## Estratificación natural

| Estrato | Trials | Exact supported | Rate |
|---|---:|---:|---:|
| Target TapHead | 29.791 | 27.968 | 93,88% |
| Target LongNoteHead | 10.569 | 9.112 | 86,21% |
| Held-before present | 13.863 | 11.611 | 83,76% |
| Held-before absent | 26.497 | 25.469 | 96,12% |
| Original chord size 2 | 13.824 | 13.722 | 99,26% |
| Original chord size 3 | 13.143 | 12.243 | 93,15% |
| Original chord size 4 | 10.220 | 8.844 | 86,54% |
| Original chord size 5 | 2.700 | 1.990 | 73,70% |
| Original chord size 6 | 312 | 162 | 51,92% |
| Original chord size 7 | 161 | 119 | 73,91% |

La sensibilidad `exact heads + exact held context` encontró contexto comparable en 35.259 trials y target exacto soportado en 31.414. Esta caída frente a heads-only es información descriptiva: no autoriza ignorar held context ni convertirlo en threshold de similitud.

## Micro y macro

Micro sobre todos los trials: comparable 98,11%; supported 91,87% del total y 93,65% de comparables. Macro, promediando primero cada familia/chart: comparable 98,20%; supported 92,50%; unique 2,25%; target-among-competing 90,25%. Cada familia tiene un chart único en este corpus; no se asignaron pesos de familia.

## Performance y artifacts

La construcción de relations acumuló 674,84 ms y la reconstrucción held-out 32.053,36 ms en 11 charts. Este coste pertenece al runner research; D0 no se ejecuta en generation normal. El JSON detallado local ocupa 127.405.008 bytes y permanece fuera del repositorio. Los payloads de results suman 106.393.317 bytes.

Artifacts versionados:

- `d0_chart_summary.csv`;
- `d0_family_summary.csv`;
- `d0_global_summary.csv`.

Los CSV usan `N/A` cuando el denominador es cero y no contienen rutas personales absolutas.

## Regression y tests

- Behavior policy: `legacy-experimental.1`, sin cambio.
- Evidence profile: `phase-a.1`, sin cambio.
- Diagnostic schema: `phase-c1-2-shadow.1`, sin cambio.
- Generation, RNG transcript, active candidate weights, lane selection y articulation: intactos.
- Cinco snapshots históricos: byte-exact PASS.
- D0 no recibe `IRandomSource` y no se integra a `AddNotesEngine`.
- Suite final: **220 passed, 0 failed, 0 skipped**.

Los 16 casos D0 cubren exact recurrence, competition, Tap/LN distinction, held-tail safety, same-timestamp LN safety, whole-group exclusion, independent donors, no cross-chart evidence, determinismo, ausencia de scores, RNG/output exactos, 1K vacío, 4K/7K/10K/18K y permutación total de lanes sin generalizar evidencia.

## Decision gate

| # | Criterio | Resultado |
|---:|---|---|
| 1 | Representation deterministic | PASS |
| 2 | Original-only | PASS |
| 3 | Target head-group excluded | PASS |
| 4 | No cross-chart evidence | PASS |
| 5 | Tap/LN heads distinguished | PASS |
| 6 | Held tails never teach heads | PASS |
| 7 | Exact lane identity only | PASS |
| 8 | No pattern taxonomy | PASS |
| 9 | No similarity threshold | PASS |
| 10 | Relation provenance complete | PASS |
| 11 | Independent witnesses deduplicated | PASS |
| 12 | Unique/competing distinguished | PASS |
| 13 | Denominators explicit | PASS |
| 14 | Multi-family corpus | PASS |
| 15 | Generic multi-key implementation | PASS |
| 16 | No score/confidence | PASS |
| 17 | No MapperSupport | PASS |
| 18 | No generation change | PASS |
| 19 | RNG exact | PASS |
| 20 | Active weights exact | PASS |
| 21 | Lane selection exact | PASS |
| 22 | Articulation exact | PASS |
| 23 | Legacy output exact | PASS |
| 24 | Tests complete green | PASS |
| 25 | Documentation consistency | PASS |

## Outcome y recomendación

**OUTCOME A — EXACT CHORD-COMPLETION RELATIONS ARE RECONSTRUCTIBLE AND EXPLANATORY.**

La primitive exacta recupera recurrence real dentro de múltiples charts/familias y explica un miembro oculto sin taxonomía ni similitud inventada. Esto valida una representación shadow, no una autoridad de generation.

La evidencia recomienda **D0.1 — Exact Completion Competition Context / Shadow** como siguiente investigación: estudiar descriptivamente qué contexto original exacto separa alternativas, porque 36.387 trials soportados permanecen entre completions competidoras y sólo 693 son unique. D1 no queda NEXT ni autorizado; un resulting-state planner sería prematuro. C2 continúa DEFERRED.
