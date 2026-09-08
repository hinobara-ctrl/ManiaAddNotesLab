# Phase F2.2 — Quantization Inference Feasibility / Research Design

**Estado: COMPLETE — OUTCOME B — RESEARCH/SHADOW ONLY.**  
**Behavior change:** false.  
**Research schema:** `phase-f2-2-quantization-feasibility.1`.  
**Forward serialization model:** `osu-integer-ms-away-from-zero.1`.

F2.2 no implementa un quantizer. Determina qué parte de una inferencia temporal puede formalizarse, qué assumptions exige y por qué el archivo no autoriza escogerlas.

## 1. Estado inicial

- Commit: `5c37b93b730e54d4a9f98af8dd595689657d30e6`; branch `main`; working tree limpio.
- `dotnet restore`, build Release, DocConsistency y `git diff --check`: PASS.
- Suite inicial: 378 passed, 0 failed, 0 skipped.
- NuGet emitió `NU1900` al no poder consultar vulnerabilidades por red restringida; los paquetes ya restaurados, build y tests no fallaron.
- Estado canónico inicial: F2.1 COMPLETE/C, F2.2 NEXT; `legacy-experimental.1`, `phase-a.1`, `phase-c1-2-shadow.1`.

## 2. Hardening pre-F2.2

La revisión confirmó un bug auditable: `TargetEndpointLeakageCount` se construía con el literal `0`. No medía donors. Se reemplazó por `TargetEndpointLeakageViolations`, una colección calculada para cada target/identity/holdout/scope; el count ahora es su longitud. Cada violation conserva IDs de target, donor, scope y observations excluidas que reaparecieron.

El positive control pasa deliberadamente un donor sin filtrar y obtiene leakage 1 con provenance. El holdout correcto `TransitionEndpointGroup` considera sus donors reales y obtiene 0. El fixture de releases compartidos registra dos exclusions adicionales y dos targets afectados. El schema F2.1 de código sube a `phase-f2-1-exact-gap-timing-shadow.2`; el report histórico F2.1 permanece intacto como snapshot `.1`.

`SyntheticTeachingCount = 0` no pretende auditar leakage: es un invariante estructural. F2.1 construye el perfil desde `OriginalObjects`, su test compara serialización con/sin `AddedObjects`, y F2.2 no aprende de charts. No se añadió un contador redundante.

Gate posterior al hardening: build, 380 tests, DocConsistency y diff check PASS. Leakage real correcto = 0; por ello F2.2 pudo continuar.

## 3. Reproducción F2.1

El runner F2.1 fue ejecutado antes y después del hardening. Los cinco CSV públicos coinciden byte a byte con los publicados y dos reruns hardened produjeron hashes idénticos. Permanecen demostrados:

- false split FileExact para 1/2 beat a 499 ms/beat (250/249 ms);
- false split SegmentTraversal en ese caso;
- false split de traversal ante redline redundante;
- false merge del control `ExactMilliseconds` entre BPM distintos;
- semántica release endpoint-aware;
- cero dependencia de generation y artifacts deterministas.

## 4. Demostración fuerte de no-identificabilidad

Con una única redline visible `time=0, beatLength=499`:

| Historia | Coordenada latente | Serialización |
|---|---:|---:|
| World A | `0.5` beat | 250 ms |
| World B | `250/499` beat | 250 ms |

Las coordenadas latentes difieren, pero redlines, timestamp serializado e inputs disponibles al algoritmo son idénticos. El certificado marca `LatentHistoriesDiffer=true`, `ObservableInputsAreIdentical=true` y `DemonstratesNonIdentifiability=true`. No existe inverso determinista único desde el `.osu` solamente.

## 5. Modelo de serialización

`QuantizationSerializationForwardModel` reutiliza la ruta real `BeatTimeline`: ordena redlines positivas, integra beats con `decimal`, selecciona el segmento por beat y calcula:

```text
time = segment.time + (latentBeat - segment.startBeat) * segment.beatLength
timestamp = Round(time, 0, MidpointRounding.AwayFromZero)
```

`Serialize(Q) == T` es la única compatibilidad. Se probaron beat lengths integrales/no integrales, midpoint, offset, boundary, redline redundante, cambio real de BPM y releases LN. Los timing points inherited negativos/SV no entran al beat timeline. No hay jitter, epsilon ni nearest.

## 6. Taxonomía epistémica

| Nivel | Contenido permitido | Ejemplo |
|---|---|---|
| KNOWN FROM FILE | timestamp, redlines positivas, tipo de endpoint/transition | `T=250 ms`, `beatLength=499` |
| KNOWN FROM SYNTHETIC TRUTH | coordenada latente porque el fixture la creó | World A = `0.5` |
| ASSUMED BY MODEL | dominio finito, candidate coordinates, regla/versiones | domain `external-expanded-v1` |
| INFERRED UNDER MODEL | conjunto exactamente compatible y cardinalidad | `{half, exact_250_over_499}` |
| UNKNOWN | intención real, snap del editor, vocabulario autorizado | cuál candidate quiso el mapper |

Una salida nunca rebautiza `UniqueUnderModel` como `ObservedHalfBeat` o `TrueSnap`.

## 7. Frameworks considerados

| Framework | Resultado |
|---|---|
| Familia finita explícita | Auditable/falsable como condición research; aceptada sólo condicionalmente. |
| Todos los racionales | Infinito; no enumerable sin cap externo. Rechazado. |
| Denominadores “observados” | Circular: exige cuantizar para descubrir el vocabulario de cuantización. Rechazado. |
| Relaciones exactas del chart | Auditables, pero describen el archivo serializado; no restauran labels latentes. Insuficiente. |
| Metadata/editor del `.osu` | No conserva coordenadas snap latentes. No disponible. |
| Ground truth externo etiquetado | Puede falsar accuracy futura; no autoriza por sí mismo un dominio productivo. Recurso futuro. |

Bayes, ML, probabilidad, score, confidence, frequency authority y heurísticas de snap quedaron fuera por diseño.

## 8. Problema del dominio latente

El forward path es exacto si recibe una coordenada. La pregunta no resuelta es quién autoriza esas coordenadas. `QuantizationHypothesisDomain` exige ID, source, justification y candidates; no posee constructor/default de denominadores oculto. `ObservedDenominatorsCircular` queda marcado en el assumption certificate y no puede presentarse como mapper-derived.

Una constante como `max denominator = 192` sólo trasladaría el problema: sería parte de la hipótesis externa. El proyecto no dispone hoy de una derivación chart-local no circular para ese dominio.

## 9. Ground truth requerido

Disponible: latent truth sintética controlada antes de integer-ms serialization. Falta ground truth humano independiente. Un `.osu` humano sin labels sólo permite medir cardinalidad/model applicability bajo assumptions, no accuracy de intención.

Recursos futuros técnicamente válidos: fixtures desde beat coordinates conocidas, pares editor/export, formatos fuente que preserven snaps, examples mapper-authored controlados o annotation manual. Deben separar dataset de validación y domain design; ninguno se presume disponible.

## 10. Modelo de compatibility set

La entrada es `ObservedEndpoint + ExplicitDomain + ForwardModel`. La salida incluye observation, assumption certificate y todas las hypotheses cuyo timestamp predicho coincide exactamente:

```text
0 candidates → UnsupportedUnderModel
1 candidate  → UniqueUnderModel
N > 1        → AmbiguousUnderModel
not eligible → InferenceNotApplicable
```

No existe winner. El certificate expone domain ID/source/circularity, serialization model version y timing map hash.

## 11. Experimentos sintéticos

La matriz produjo `UniqueCorrect`, `AmbiguousContainsTruth`, `AmbiguousExcludesTruth`, `UniqueIncorrect` en tests y `UnsupportedUnderModel`. También conserva el false split F2.1 y el false merge observable por collision. Los cuatro transition kinds y fixtures 1K/4K/7K/10K/18K usan una implementación única.

La falsabilidad es real: un domain misspecified puede devolver ambiguity que excluye la verdad o uniqueness incorrecta. Más uniqueness no equivale a mejor modelo.

## 12. Collision analysis

El collision explícito tiene class size 2 a 499 ms/beat y timestamp 250 ms. Un control integral 500 ms/beat mantiene quarter y half separados. Las collisions son límites de identificabilidad, no score. El CSV conserva condition, BPM, position, candidate IDs, class size y observable.

## 13. Sensibilidad al vocabulario

Con el mismo observable 250 ms y el mismo mapa de 499 ms:

- `external-narrow-v1 = {half}` → `UniqueUnderModel`;
- `external-expanded-v1 = {half, exact_250_over_499}` → `AmbiguousUnderModel`.

La “unicidad” cambia sólo por el domain asumido. La salida expone ambos IDs y no oculta la dependencia.

## 14. Timing changes

El forward model pasa same-segment, timing offset, redline redundante y cambio real 500→250 ms/beat. El hash de timing map distingue el mapa con boundary redundante aunque el timestamp forward sea igual. La serialización de beat 3 bajo el cambio real es 1250 ms. SV inherited no inventa beat evidence.

## 15. Endpoint y release

Tap→Tap, Tap→LN, Release→Tap y Release→LN permanecen campos descriptivos. Para holdout futuro, Tap source usa HeadEventGroup; LN source usa ReleaseEventGroup; destination usa HeadEventGroup; la exclusión es su unión. El positive control incorrecto obtiene leakage y el contrato correcto obtiene cero. LN head/release no se colapsan.

## 16. Rice sentinel

El stream latente `0|0.125|0.25|0.375` a 500 ms/beat serializa `0|63|125|188`. `0.124` y `0.125` permanecen distintos bajo truth (`62` vs `63`). Se incluyeron repeated, jack-like y alternating fixtures sin `JackPenalty`, `TrillClassifier`, `RicePatternClassifier`, lane-role authority ni nueva policy rice.

## 17. Corpus humano

**NOT RUN, justificadamente.** La synthetic gate ya responde la pregunta de formalizabilidad y demuestra domain sensitivity. El corpus humano no contiene latent labels, por lo que no puede escoger denominadores ni medir inferred-intent accuracy. Ejecutarlo sólo produciría applicability/cardinality condicional y correría el riesgo de usar coverage para rescatar una assumption no autorizada.

## 18. Gate F2.2 predefinido

El gate siguiente fue fijado antes de interpretar artifacts; no se retiraron condiciones:

| # | Condición | Estado |
|---:|---|---|
| 1 | baseline repo green | PASS |
| 2 | reproducción F2.1 | PASS |
| 3 | endpoint leakage medido | PASS |
| 4 | endpoint leakage cero | PASS |
| 5 | observed vs inferred separado | PASS |
| 6 | forward serialization explícita | PASS |
| 7 | serialization determinista | PASS |
| 8 | compatibility exacta | PASS |
| 9 | sin epsilon | PASS |
| 10 | sin nearest | PASS |
| 11 | sin round-based identity | PASS |
| 12 | sin frequency authority | PASS |
| 13 | sin score | PASS |
| 14 | sin probability | PASS |
| 15 | sin confidence | PASS |
| 16 | sin ML | PASS |
| 17 | latent domain explícito | PASS |
| 18 | assumptions explícitas | PASS |
| 19 | sin vocabulary oculta | PASS |
| 20 | sin derivación circular activa | PASS |
| 21 | synthetic latent truth | PASS |
| 22 | collision/non-identifiability | PASS |
| 23 | ambiguity medible | PASS |
| 24 | false merge medible | PASS |
| 25 | false split medible | PASS |
| 26 | unique inference medible | PASS |
| 27 | múltiples hypotheses preservadas | PASS |
| 28 | abstention válida | PASS |
| 29 | timing changes | PASS |
| 30 | LN release semantics | PASS |
| 31 | endpoint-aware holdout | PASS |
| 32 | original-only | PASS |
| 33 | synthetic teaching cero | PASS estructural |
| 34 | cross-chart cero | PASS estructural |
| 35 | artifacts deterministas | PASS |
| 36 | sin generation dependency | PASS |
| 37 | legacy byte-exact | PASS por regresión |
| 38 | legacy RNG exacto | PASS por regresión |
| 39 | multikey | PASS |
| 40 | rice timing sentinel | PASS |
| 41 | sin branches estilísticas por keymode | PASS |
| 42 | docs consistentes | PASS |
| 43 | root guard | PASS |
| 44 | suite completa | PASS |

El gate valida infraestructura científica, no justificación del domain.

## 19. Outcome

**OUTCOME B.** La inferencia es técnicamente formalizable y falsable bajo una familia finita proporcionada explícitamente. La serialización es exacta; ambiguity y abstention sobreviven. Sin embargo, el proyecto no tiene una justificación mapper-derived para escoger el dominio y carece de latent labels humanos para validar accuracy. Outcome A no aplica porque la dependencia central continúa sin resolver; Outcome C no aplica porque la hipótesis condicional sí es auditable.

No se autoriza quantizer, selector, behavior ni A/B.

## 20. Limitaciones

- `decimal` representa las coordenadas que el caller declara; no prueba intención original.
- Un timing map hash identifica assumptions visibles, no restaura editor state.
- Synthetic truth prueba lógica y falsabilidad, no naturalidad humana.
- Dominios finitos pueden crear uniqueness artificial.
- No existe dataset etiquetado humano ni vocabulary autorizado.
- El model no resuelve la coordenada latente del endpoint source; enumerar historias completas requerirá otro diseño explícito.

## 21. Próxima recomendación

Recomendar **F2.3 — Quantization Hypothesis Domain and Labeled Ground Truth Acquisition / Research Design**, `NEXT / NOT AUTHORIZED YET`. Debe especificar/adquirir ground truth independiente y comparar dominios declarados sin seleccionar uno por coverage. Sólo después podría decidirse si merece una fase experimental de quantizer. Esta recomendación no abre C2, D1, E, MapperSupport ni generation.

## 22. Archivos cambiados

- Core: hardening `ExactGapTimingIdentityResearch`; nuevo `QuantizationInferenceFeasibilityResearch`.
- Tests: positive/negative leakage, forward model, collisions, compatibility, truth, timing, transitions, multikey, rice y determinismo.
- Tooling: runner `f2-2-synthetic`.
- Evidence: diez CSV `f2_2_*`.
- Docs: este report y living state/index/roadmap/architecture/design/experiments/audit.

## 23. Artifacts

Versionables y no vacíos:

- `f2_2_model_summary.csv`;
- `f2_2_forward_serialization_summary.csv`;
- `f2_2_collision_summary.csv`;
- `f2_2_hypothesis_domain_summary.csv`;
- `f2_2_compatibility_summary.csv`;
- `f2_2_synthetic_ground_truth_summary.csv`;
- `f2_2_ambiguity_summary.csv`;
- `f2_2_timing_change_summary.csv`;
- `f2_2_endpoint_holdout_summary.csv`;
- `f2_2_rice_sentinel_summary.csv`.

Detalle local: `.artifacts/f2-2/f2_2_detail.json`, confirmado ignored mediante `git check-ignore`.

## 24. Determinismo

El runner no usa RNG, fechas, timings de máquina ni rutas absolutas. Dos ejecuciones independientes generaron hashes SHA-256 idénticos para los diez CSV y el detail JSON. La permutation de candidates produce la misma salida ordenada.

## 25. Regresión

La suite completa conserva snapshots byte-exact, transcript RNG, output válido, no-overlap y comportamiento Web/CLI existente. Ningún archivo de `AddNotesEngine` ni opción productiva fue modificado. Los tests finales reportan 415 passed, 0 failed, 0 skipped.

## 26. Verificación final

```powershell
git status --short
git diff --check
dotnet build -c Release
dotnet test -c Release
dotnet run --project tools/DocConsistency -- --check
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- f2-2-synthetic docs .artifacts/f2-2/f2_2_detail.json
git check-ignore .artifacts/f2-2/f2_2_detail.json
```

Todos pasan; `git status` muestra únicamente los cambios F2.2 sin commit.

## 27. Confirmación de fronteras

- `BehaviorPolicyVersion = legacy-experimental.1`.
- Generation behavior unchanged.
- No quantization behavior exists.
- No F2 A/B exists.
- C2 remains DEFERRED.
- D1 remains NOT AUTHORIZED.
- Phase E remains unopened.
- No commit, push, tag or release was performed.

**DO NOT HIDE INFERENCE AS OBSERVATION.**
