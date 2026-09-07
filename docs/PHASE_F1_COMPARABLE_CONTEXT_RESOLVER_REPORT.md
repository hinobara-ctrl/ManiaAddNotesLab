# Phase F1 — Comparable-context Resolver / Shadow + Validation

**Estado: COMPLETE — OUTCOME A.**  
**Behavior change:** no.  
**Research schema:** `phase-f1-research.1`.  
**Corpus:** 11 charts humanos / 11 familias / 4K, 7K y 10K, leído sin modificar desde `C:\Users\benja\OneDrive\Escritorio\tests`.

## Pregunta e hipótesis predefinidas

F1 pregunta si la evidencia exacta D0.2 permite reconstruir, para una candidate completion concreta, uno de cuatro estados epistemológicos auditables sin elegir completion, vista o política de backoff. El diseño, las 25 condiciones del gate y los Outcomes A/B/C se fijaron antes del corpus; una tasa alta de abstención o ambiguity no podía redefinirlos después.

El universo candidate-centric es la unión exacta de completions observadas por D0.2 más la completion target held-out. `ReducedOnly` conserva baseline/vocabulario, pero no cuenta como contexto local: el resolver usa las siete vistas que añaden al menos un átomo exacto.

## Semántica implementada

- `NoComparableContext`: ninguna vista contextual posee donors comparables. Es abstención, no failure ni mismatch.
- `LocalMismatch`: existe contexto comparable, pero ninguna vista soporta exactamente la candidate. No afirma que otra completion sea correcta.
- `AmbiguousEvidence`: existe soporte exacto, pero coexiste un mismatch, un conflicto unique que involucra la candidate o un acuerdo marginal sin joint comparable/soporte joint.
- `ObservedSupport`: existe soporte contextual exacto y ninguna de las contradicciones anteriores.

`HasObservedSupport` y `HasLocalMismatch` conservan los hechos view-local debajo del estado combinado. Cada candidate, incluida una alternativa no-target, recibe su propio certificado; ninguno gana sobre otro. Las vistas son `Supporting`, `Contradicting` o `Abstaining`. Los dependency edges sólo describen nesting y nunca se suman como votos.

Cada resolución conserva lane/type de target y candidate, target group/observation, fingerprint chart-local, coverage y firma exactas, grupos donor, observation IDs y occurrence provenance con beat/contexto original.

## Marginal no significa joint

Dos assessments separados representan `PreviousTransition + NextTransition → ReducedPrevNext` y `ReducedHeld + ReducedPrevNext → ReducedHeldPrevNext`. Cada uno produce `NoMarginalAgreement`, `ObservedJointContext`, `MarginalWithoutJointComparable` o `MarginalContradictedByJoint`. La intersección marginal jamás fabrica una occurrence conjunta.

## Hardening D0.2 exigido antes de interpretar F1

La revisión encontró y corrigió dos problemas nominales/auditables:

1. `SpecificityStability.TargetPreserved` no significaba “target preservado”: esa rama sólo era alcanzable cuando el target seguía sin soporte. Se renombró a `TargetStillUnsupported`, se expuso el clasificador y se probaron sus seis ramas.
2. El audit de nesting comprobaba tres edges aunque el grafo prometía nueve. `ExactContextViewDependencies` centraliza ahora los nueve y el audit verifica por donor+completion exactos que cada child sea subconjunto de su parent.

Este hardening eleva únicamente el schema research D0.2 a `phase-d0-2-research.2`; el report histórico D0.2 no se reescribe. Las versiones productivas permanecen `legacy-experimental.1`, `phase-a.1` y `phase-c1-2-shadow.1`.

## Implementación y frontera

`ComparableContextResolverResearch` envuelve D0.2 y emite resoluciones descriptivas deterministas. No acepta RNG y no contiene score, probabilidad, confianza, ranking, peso, threshold, similarity, tolerance, mirror, translation, majority vote ni ladder/backoff. No está referenciado por `AddNotesEngine`, CLI o Web productivos; el runner vive en `tools/ManiaAddNotesLab.Experiments`.

El detalle se serializa por streaming. El JSON local mide 4.599.989.935 bytes y permanece ignorado en `.artifacts/f1/f1_detail.json`. Once CSV públicos conservan agregados ordenados y versionables. Una repetición completa confirmó SHA-256 idéntico para todos los CSV; los tiempos variables se mantienen sólo en el log y en este informe, fuera de la evidencia serializada.

## Resultados

F1 resolvió 249.202 candidates sobre los mismos 40.360 target trials D0:

| Estado target | Resoluciones | Micro |
|---|---:|---:|
| `ObservedSupport` | 17.942 | 44,45% |
| `LocalMismatch` | 4.021 | 9,96% |
| `NoComparableContext` | 3.266 | 8,09% |
| `AmbiguousEvidence` | 15.131 | 37,49% |

La lectura macro por familia es 48,03% support, 8,66% mismatch, 6,91% no-context y 36,39% ambiguous. Support y ambiguous aparecen en 11/11 familias, mismatch en 10/11 y no-context en 9/11. Esto muestra que las categorías no dependen de una sola familia, aunque su prevalencia sí varía.

Las 208.842 candidates no-target quedan separadas: 19.755 support (9,46%), 70.649 mismatch (33,83%), 9.557 no-context (4,58%) y 108.881 ambiguous (52,14%). Sus certificados describen alternativas; no son votos ni selecciones.

Por keymode, el support target fue 64,92% en 4K, 41,16% en 7K y 70,27% en 10K. El corpus no está balanceado —35.351/40.360 targets son 7K—, de modo que son descripciones, no comparaciones causales. TapHead obtuvo 47,94% support y 5,09% no-context; LongNoteHead, 34,63% y 16,56%. Esto localiza una frontera de cobertura más dura para LN sin inventar una explicación conductual.

Las 22 coverage signatures D0.2 permanecen observables. En target hubo 11.939 `SupportAndLocalMismatch`, 2.482 conflictos unique que involucran la candidate, 3.232 acuerdos marginales sin joint comparable y 442 contradichos por el joint. Las causas pueden coexistir y no son una partición sumable.

En Previous/Next, 6.193 target candidates tuvieron joint observado, 3.075 acuerdo marginal sin joint comparable y 438 contradicho por el joint. En Held/PrevNext fueron 5.955, 157 y 4. No apareció ningún donor compartido entre support y contradiction; sí 119.185 resoluciones con ambos lados sostenidos por donors disjoint. Esto describe provenance, no independencia estadística.

Las vistas unique aportaron 39.948 referencias candidate-view al target y 18.709 a candidates no-target; no son resoluciones independientes ni ganadores. Los supporting dependency edges sumaron 53.576 referencias para target y 45.959 para no-target, conservadas por parent/child para impedir que esa multiplicidad se interprete como fuerza.

## Lectura micro/macro e invariantes

Micro confirma la separación de support, mismatch, abstención y conflicto. Macro revela heterogeneidad: support target va de 19,07% a 70,27% entre familias; ambiguous de 17,70% a 51,11%; mismatch de 0% a 28,66% y no-context de 0% a 24,92%. Outcome A significa que los estados son reconstruibles sin ocultar límites, no que exista una regla universal.

Sobre las 11 familias quedaron exactamente en cero target-group leakage, future-held leakage, held tails codificadas como heads y violaciones de nesting en los nueve edges. Los índices/fingerprints son chart-local. El corpus conservó exclusión `[ADD …]` y deduplicación SHA-256; no se copiaron ni modificaron sources.

## Pruebas y gate

Se añadieron 34 casos F1 y 7 casos/ramas de hardening D0.2: cuatro estados; todos/algunos/ningún soporte; múltiples candidates sin ganador; conflictos target-vs-wrong y wrong-vs-wrong; dependencies no-vote; donors shared/disjoint; ambos assessments joint; provenance; whole-group holdout; saneamiento LN; held-tail; identidad exacta lane/type/beat; separación cross-chart; determinismo; cero RNG y fixtures 1K/4K/7K/10K/18K.

El cierre completo reporta **319 passed / 0 failed / 0 skipped**. Output `.osu`, transcript RNG, active weights, lane selection y articulation permanecen idénticos. Las 25 condiciones predefinidas pasan: semántica/provenance exactas, invariantes cero, nesting completo, marginal/joint separados, conflictos sin ganador, ausencia de autoridad/generalización, frontera productiva intacta, versiones estables, corpus micro/macro, artifacts deterministas y validación integral verde.

La última corrida completa empleó 57,34 s de tiempo acumulado por chart, de los cuales 9,37 s correspondieron a construir las resoluciones F1; es una observación de rendimiento de esta máquina, no parte de la identidad del resultado.

## Decisión

**OUTCOME A.** Los cuatro estados son reconstruibles, auditables y estables bajo el gate. Esto valida un lenguaje de evidencia comparable; no valida selection policy, no autoriza sumar vistas y no convierte support en authority.

La siguiente fase recomendada es **F2 — Typed gaps and evidence backoff A/B**, sólo como `NEXT / NOT AUTHORIZED YET`. Abrirla exige autorización separada, nueva versión conductual y diseño que mantenga explícitos mismatch, no-context y SKIP. D1 sigue **NOT AUTHORIZED** y C2 **DEFERRED**.

## Artefactos

- `f1_chart_summary.csv`, `f1_family_summary.csv`, `f1_global_summary.csv`.
- `f1_evidence_state_summary.csv`, `f1_coverage_summary.csv`, `f1_conflict_summary.csv`.
- `f1_joint_context_summary.csv`, `f1_stratification_summary.csv`, `f1_view_evidence_summary.csv`.
- `f1_unique_support_summary.csv`, `f1_dependency_summary.csv`.

```powershell
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- f1-corpus <corpus-read-only> docs <detail-json-local>
```
