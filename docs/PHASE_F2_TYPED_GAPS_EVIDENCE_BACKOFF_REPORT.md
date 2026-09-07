# Phase F2 — Typed Gaps and Evidence Backoff A/B

**Estado: COMPLETE — OUTCOME B — SHADOW ONLY.**  
**Behavior change:** no.  
**Research schema:** `phase-f2-typed-gap-shadow.1`.

El diseño, gate y outcomes de las secciones siguientes se fijaron antes del corpus. El resultado no autorizó la etapa conductual.

## Frontera

F2 estudia una sola sustitución: un gap candidate exacto se admite por evidencia original o se omite; sólo `NoComparableContext` permite ampliar de lane-local a chart-global. No existe scope Section validado. F2 v1 usa `LocalLane → GlobalChart → SKIP`.

F2 no selecciona lane, head type, chord, duración ni uno entre varios gaps; no usa frequency como weight; no implementa C2, D1, E, MapperSupport ni J. Legacy permanece default y `legacy-experimental.1` conserva identidad. Una policy conductual sólo puede existir opt-in después de que shadow pase.

## Identidad exacta predefinida

`TypedTransitionGap` describe una transición same-lane entre endpoints originales adyacentes:

- `TapHeadToTapHead`;
- `TapHeadToLongNoteHead`;
- `LongNoteReleaseToTapHead`;
- `LongNoteReleaseToLongNoteHead`.

Una LN previa aporta su release, no su head, como origen del spacing. TapHead, LongNoteHead y LongNoteRelease son identidades distintas. El gap es `next.StartBeat - previous.EndBeat` como `decimal` exacto; ms son provenance. No hay round-to-6, epsilon de matching ni tolerancia.

Cada target retira los grupos head completos que contienen sus observaciones previous y next. Los donors restantes deben coincidir exactamente en transition kind y scope. Todo proviene de `MapperEvidenceProfile.Observations`/`SameLaneTransitions`, que son original-only y chart-local.

## Evidence semantics

Para una candidate `{transition kind, exact gap}` y un scope:

- `NoComparableContext`: no hay donor de ese transition kind.
- `LocalMismatch`: hay donors, pero ninguno contiene el gap exacto.
- `AmbiguousEvidence`: el gap exacto aparece y también aparecen gaps distintos.
- `ObservedSupport`: todos los donors comparables contienen el gap exacto.

Los counts y vocabularios son descripción, no authority. Múltiples gaps nunca se rankean.

## Backoff predefinido

```text
Local ObservedSupport  → ADMIT
Local LocalMismatch    → SKIP; no backoff
Local Ambiguous        → SKIP; no backoff
Local NoContext        → consultar Global
Global ObservedSupport → ADMIT
Global cualquier otro  → SKIP
```

Backoff significa ampliar scope porque el scope local no podía evaluar; nunca buscar otro scope después de un “no”.

## Auditoría legacy de gaps

| Decisión | Código actual | Clasificación | Riesgo estilístico |
|---|---|---|---|
| Ventana lane-gap ±4 beats | `LocalLaneGapAnalyzer`, `LaneGapWindowBeats` | `MAPPER_DERIVED_CANDIDATE` | Define locality sin segmentación validada. |
| Pooling de todas las lanes | `LocalLaneGapAnalyzer.Resolve` | `MAPPER_DERIVED_CANDIDATE` | Borra roles de lane. |
| Sólo gaps `0 < gap <= 1` | `LocalLaneGapAnalyzer.Resolve` | `MAPPER_DERIVED_CANDIDATE` | Recorta vocabulario por constante estilística. |
| `decimal.Round(gap, 6)` | `LocalLaneGapAnalyzer.Resolve` | `IMPLEMENTATION_ONLY` en legacy; inválido para identity F2 | Fusiona beats exactos distintos. |
| Soporte mínimo 2 | `LaneGapMinimumSupport` | `MAPPER_DERIVED_CANDIDATE` | Frequency se vuelve validity. |
| Elegir el gap soportado más pequeño | `OrderBy(...).FirstOrDefault()` | `MAPPER_DERIVED_CANDIDATE` | Selector estilístico no derivado. |
| Fallback fijo 0,125 | `FallbackMinimumLaneGapBeats` | `MAPPER_DERIVED_CANDIDATE` | `No evidence → default inventado`. |
| Aplicar un único minimum a ambos lados de LN | `FindLegalLnLanes` | `CURRENT_POLICY_CONTRACT` | Colapsa transition kinds. |
| Retrigger ±4 / soporte 2 / límite 1 beat | `LocalRetriggerGapAnalyzer` | `MAPPER_DERIVED_CANDIDATE` | Ventana, threshold y recorte no derivados. |
| Same-lane primero; luego pooling cross-lane | `LocalRetriggerGapAnalyzer.Resolve` | `MAPPER_DERIVED_CANDIDATE` | Ladder implícito sin mismatch explícito. |
| Orden retrigger por count y luego gap | `LocalRetriggerGapAnalyzer.Supported` | `MAPPER_DERIVED_CANDIDATE` | Frequency y smallest actúan como preference. |
| Peso articulation por evidence count/release/same-lane | `ResolveArticulations` | `CURRENT_POLICY_CONTRACT` | Selección weighted legacy; F2 no la modifica. |
| Contexto LN ±4 y pooling de shapes | `BuildOriginalLnContext` | `MAPPER_DERIVED_CANDIDATE` | Pertenece a shape vocabulary, no al gap F2. |
| Conversión beat↔ms de `BeatTimeline` | timeline/parser | `FORMAT_OR_INVARIANT` | Necesaria para output; ms no forman identity F2. |

## Gate F2 predefinido

F2 sólo puede cerrar Outcome A si pasan estas 34 condiciones:

1. typed-gap identity exacta; 2. transition endpoint exacto; 3. beats `decimal` exactos; 4. original-only; 5. whole-group exclusion; 6. LN sanitation; 7. tail distinto de head; 8. provenance chart-local; 9. cross-chart cero; 10. candidate-centric; 11. sin winner automático; 12. sin frequency weighting; 13. sin C2; 14. sin D1; 15. sin Section inventada; 16. sin MapperSupport; 17. mismatch bloquea backoff; 18. ambiguity bloquea backoff; 19. sólo no-context permite backoff; 20. SKIP explícito; 21. shadow determinista; 22. artifacts deterministas; 23. legacy byte-exact; 24. RNG legacy exacto; 25. legacy default; 26. F2 opt-in; 27. rollback; 28. implementación 1K–18K; 29. sin branches estilísticas por keymode; 30. output válido; 31. cero overlaps nuevos; 32. docs consistentes; 33. root guard verde; 34. suite completa verde.

## Outcomes fijados

- **Outcome A:** identity/evidence/backoff exactos y auditables; shadow supera gate y una policy opt-in puede ejecutar support → backoff sólo por ausencia → SKIP sin selector inventado. A/B técnicamente válida, sin promoción.
- **Outcome B:** representación válida, pero conducta necesita otra fase, scope, resolución de múltiples candidates o cobertura adicional. F2 queda shadow-only.
- **Outcome C:** typed-gap evidence/backoff no es reconstruible de forma exacta/auditable bajo los invariantes.

Una cobertura baja no se oculta. La conducta sólo se implementará si puede actuar como admission de un gap candidate ya definido por el pipeline sin heredar `smallest`, `support >= 2` o fallback fijo.

## Estado inicial y PRE-F2 HARDENING GATE

El baseline se tomó en `d6d6042459bec7bfece7b2eace49ad28b86ee2eb`, rama `main`, working tree limpio. Build y restore pasaron, pero la suite quedó 318/319 y DocConsistency falló por el archivo raíz trackeado `tatus`. Su contenido era una captura accidental de `git diff --stat`; no pertenecía al producto. Se preparó su eliminación normal, sin allowlist ni reescritura de historia.

`RepositoryRootGuard` usaba sólo `git ls-files`. Ahora enumera `--cached --others --exclude-standard`, descarta paths eliminados del working tree y detecta nombres inesperados trackeados o untracked sin incluir `.artifacts/` u otros ignores legítimos. Los diagnostics ya no dicen únicamente “tracked”.

El test F1 Held/PrevNext ahora cubre explícitamente marginal agreement + joint comparable + candidate ausente y confirma `MarginalContradictedByJoint`; la rama productiva ya era correcta.

Después del hardening: build PASS, DocConsistency/root guard PASS, `git diff --check` PASS y 321/321 tests. Sólo entonces se abrió F2.

## Implementación shadow

`TypedGapBackoffResearch` consume el `MapperEvidenceProfile` original-only. Cada `SameLaneTransitionObservation` no negativa es target held-out. La provenance se normaliza: occurrences completas se guardan una vez y cada scope conserva claves `{PreviousObservationId, NextObservationId}`. Esto evita repetir records cuadráticamente sin perder trazabilidad.

La consulta local es estructural, no temporal: misma lane + mismo transition kind. Global conserva el transition kind y amplía al chart. No existe Section. Global se materializa únicamente cuando Local es `NoComparableContext`, de acuerdo con la semántica de backoff.

Un primer intento de corpus agotó memoria porque repetía donors globales completos en cada resolución. No produjo resultados parciales interpretables. La normalización anterior corrigió la representación y el corpus completo posterior pasó.

## Corpus shadow

Se leyeron, sin modificar, los mismos 11 charts/familias humanos 4K/7K/10K. Se reconstruyeron **50.762 typed-gap candidates** y **1.809 gaps `decimal` exactos**.

| Estado Local | Candidates | Micro |
|---|---:|---:|
| `ObservedSupport` | 2 | 0,004% |
| `LocalMismatch` | 4.051 | 7,981% |
| `NoComparableContext` | 18 | 0,035% |
| `AmbiguousEvidence` | 46.691 | 91,980% |

La lectura macro por familia fue 0,006% support, 7,166% mismatch, 0,035% no-context y 92,793% ambiguous. Sólo una familia produjo los dos supports locales. Las 18 consultas globales terminaron en 13 mismatch y 5 ambiguous: **global support = 0**.

Por transición:

| Transition kind | Candidates |
|---|---:|
| `TapHeadToTapHead` | 34.507 |
| `TapHeadToLongNoteHead` | 3.719 |
| `LongNoteReleaseToTapHead` | 3.725 |
| `LongNoteReleaseToLongNoteHead` | 8.811 |

Por keymode hubo 4.166 candidates 4K, 42.605 7K y 3.991 10K. Los únicos dos supports fueron 7K. Fixtures 1K/18K validan implementación, no estilo humano.

La fragmentación exacta es material: gaps cercanos visualmente como `0.499800000000000049980`, `0.5002000000000005002000000000` y otras variantes permanecen identidades distintas. Esto proviene de la conversión exacta de ms enteros bajo timing points, no de nondeterminism. Fusionarlas mediante round/epsilon habría violado el gate y creado una regla no autorizada.

## Backoff y SKIP shadow

- backoff attempts: 18;
- backoff permitidos por mismatch: 0;
- backoff permitidos por ambiguity: 0;
- admit local: 2;
- admit global: 0;
- final SKIP hipotético: 50.760;
- fallback inventado: 0.

Los 4.051 mismatch y 46.691 ambiguous detienen localmente. Las 18 ausencias son las únicas que consultan Global. Ningún scope busca “hasta encontrar un sí”. Shared local/global donors no aplica: Global sólo se consulta cuando Local tiene cero donors.

## Integridad

Target whole-group leakage = 0; synthetic teaching = 0; held tail encoded as head = 0; cross-chart transfer = 0 por construcción/fingerprint. TapHead, LongNoteHead y LongNoteRelease permanecen separados. El detail local mide 2.265.416.170 bytes y está ignorado por `.artifacts/`. Una segunda corrida produjo hashes SHA-256 idénticos para los nueve CSV públicos; ninguno contiene timestamps ni timings variables.

## Shadow gate

Condiciones 1–24 y 28–34: **PASS en representación/shadow o regresión legacy**. Esto incluye identity/type/beat exactos, original-only, whole-group exclusion, tail correctness, provenance, chart isolation, candidate-centric, cero winner/frequency/C2/D1/Section/MapperSupport, backoff sólo por ausencia, SKIP explícito, determinismo, artifacts estables, snapshots/RNG legacy, multikey, output/overlap legacy y guards.

Condiciones 25–27 para una policy F2 —legacy default, opt-in y rollback conductual— quedan **NOT EXERCISED**, porque el behavioral gate no fue satisfecho. No se creó una versión conductual F2 ni una opción en CLI/Web. El default continúa `legacy-experimental.1`.

## Decisión del behavioral gate

**FAIL para conectar A/B.** La representación exacta es válida, pero 2/50.762 admits locales y 0 globales producirían un experimento degenerado que prácticamente equivale a “SKIP todo”. La coexistencia de 1.809 decimals exactos muestra además un límite de identidad temporal: multiple supported gaps no pueden resolverse sin investigar una relación exacta más adecuada. Implementar conducta aquí no demostraría reemplazo usable de una heuristic; sólo trasladaría el límite del shadow a generation.

No se generó `f2_ab_summary.csv` porque no hubo A/B. Crear un CSV vacío fingiría una comparación inexistente.

## Outcome

**OUTCOME B.** Typed transition gaps, evidence states y backoff conservador son reconstruibles y auditables, pero la policy conductual requiere investigación adicional sobre identidad temporal exacta. F2 queda cerrado shadow-only, sin `phase-f2-typed-gap-ab.1`.

La siguiente recomendación es **F2.1 — Exact Gap Timing Identity / Shadow Validation**, `NEXT / NOT AUTHORIZED YET`: estudiar si existe una representación timing-point-relative exacta que conserve intención musical sin round, epsilon ni frequency authority. No se autoriza automáticamente. D1 sigue NOT AUTHORIZED, C2 DEFERRED y Phase E/MapperSupport sin abrir.

## Artefactos

- `f2_chart_summary.csv`, `f2_family_summary.csv`, `f2_global_summary.csv`;
- `f2_transition_summary.csv`, `f2_gap_summary.csv`, `f2_evidence_state_summary.csv`;
- `f2_backoff_summary.csv`, `f2_skip_summary.csv`, `f2_stratification_summary.csv`;
- detalle local `.artifacts/f2/f2_detail.json`.

```powershell
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- f2-corpus <corpus-read-only> docs <detail-json-local>
```
