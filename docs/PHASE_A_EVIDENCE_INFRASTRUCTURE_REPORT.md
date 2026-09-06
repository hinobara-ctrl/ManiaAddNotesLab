# Phase A — Evidence Infrastructure / Shadow Mode

Fecha: 2026-09-06.  
Resultado: **ROADMAP GREEN / PHASE A COMPLETE**.  
Behavior policy: `legacy-experimental.1`.  
Evidence profile: `phase-a.1`.

## Documents reviewed

- `FUTURE_MAPPER_DERIVED_ALGORITHM_PLAN.md`: visión y objetivo zero-config.
- `MAPPER_DERIVED_PROPOSALS_REVIEW.md`: revisión crítica, testigos, relations, composition y riesgos.
- `MAPPER_DERIVED_INTEGRATION_BLUEPRINT.md`: arquitectura previa.
- `DESIGN.md`, `EXPERIMENTS.md`.
- `KEYMODE_INVARIANCE_REPORT.md`, `RICE_BEHAVIOR_REPORT.md`.
- `LN_INTERIOR_CORRECTION_REPORT.md`, `LN_ARTICULATION_REPORT.md`.
- Código real de Model, ChartAnalysis, AddNotesEngine, LaneGeometryIndex, BeatTimeline, parser/writer, CLI, Web, tests y herramienta de experimentos.

Los documentos de visión y revisión fueron copiados a `docs/` sin fusionarlos, porque representan niveles distintos de la propuesta.

## Current implementation verified

- Parser 1K–18K.
- `OriginalObjects` congelados como fuente de análisis.
- Geometría frozen/current separada.
- Timing `decimal`, offsets, divisiones raras y BPM variable.
- Densidad de heads separada de held tails.
- Lane gap local con fallback actual.
- Interiores original-only disponibles detrás de opción.
- Articulación en segunda pasada, una por parent, detrás de opción.
- RNG derivado para preservar pass 1.
- 121 tests verdes antes de Phase A.

## Conflicts found between vision/review/current code

1. La visión habla de eliminar el fallback retrigger; el código actual ya no posee fallback numérico de retrigger. Sí conserva ventana, soporte mínimo, pooling y cutoff.
2. El blueprint proponía un perfil, pero no existía en Core antes de esta fase.
3. La invariancia moderna es portable, pero todavía usa incrementos relativos a 7K como referencia matemática.
4. La lane se selecciona uniformemente entre lanes legales; es una decisión estilística no numérica.
5. “LN interior” garantiza que el start nace dentro de la parent, pero el pipeline no impone universalmente que su release termine dentro de ella. El futuro debe distinguir `contained` y `crossing`.
6. La articulación actual se dispara por `NonHeldColumns`, no por blockers originales demostrados.
7. `ArticulationReplacement` representa exactamente dos segmentos; múltiples articulaciones requieren otro modelo.
8. Duration y release pueden sumar dos votos desde el mismo original; todavía no existe deduplicación por witness.
9. El lane gap agrega lanes/tipos, elige el menor soportado y aplica una separación simétrica.
10. Los rechazos geométricos actuales son agregados; todavía no identifican blockers ni causa original/sintética por candidate.

Ninguno fue “corregido” conductualmente en Phase A.

## Roadmap created

Path: `docs/MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md`.

El roadmap contiene las 23 secciones obligatorias, separa Evidence Layer de Generation Layer y divide la transición en fases A–K con goal, archivos, cambio conductual, estructuras, métricas, tests, artifacts, rollback y aceptación.

## Roadmap Gate

1. Distingue visión de implementación actual: **PASS**.
2. No contradice invariantes actuales: **PASS**.
3. Define evidence vs hard validity: **PASS**.
4. Preserva OriginalObjects-only: **PASS**.
5. Distingue value/relation/composition: **PASS**.
6. Distingue no evidence de local mismatch: **PASS**.
7. Incluye resulting-state validation: **PASS**.
8. Evita un MapperSupport prematuro: **PASS**.
9. Mantiene AddChance budget como hipótesis futura: **PASS**.
10. Evita decidir segmentación prematuramente: **PASS**.
11. Tiene fases pequeñas y reversibles: **PASS**.
12. Phase A puede implementarse sin cambiar output: **PASS**.
13. Tiene tests objetivos: **PASS**.
14. Tiene rollback claro: **PASS**.
15. No introduce reglas especiales por keymode: **PASS**.

Final: **ROADMAP GREEN — PHASE A IMPLEMENTATION AUTHORIZED**.

## Phase A

Status: **AUTHORIZED AND IMPLEMENTED**.

### Implemented

- Copia de trabajo futura separada del proyecto anterior y de su backup.
- `OriginalObservationId` estable dentro del fingerprint del chart.
- `MapperEvidenceProfile` inmutable y original-only.
- Observaciones por valor; ninguna referencia mutable a `ManiaObject` se almacena.
- Chord observations y held occupancy original.
- LN durations y exact releases con witness IDs.
- Same-lane transitions con tipo y gap sin cutoff de extracción.
- Retrigger observations diferenciando release→tap y release→LN head.
- Interior anchor observations con provenance head/release.
- Timing vocabulary y beats exactos.
- Fingerprint SHA-256 canónico.
- `EvidenceProfileVersion=phase-a.1`.
- `BehaviorPolicyVersion=legacy-experimental.1`.
- Skeletons extensibles `TransformationWitness` y `SupportCertificate` sin confidence/score.
- Construcción en sombra dentro de `AddNotesEngine`.
- Overload para reutilizar un perfil preparado, con validación de fingerprint y versión.
- Web construye una vez por batch, guarda `mapper-profile.json` y reutiliza el perfil.
- CLI exporta con `--profile-output`.
- CLI/Web CSV registran versiones y conteos observacionales.
- Métricas `ProfileBuildMs`, observation/relation count y estimación estructural de tamaño.
- `BEHAVIOR_DECISION_AUDIT.md`.
- 15 tests nuevos.

### Not implemented intentionally

- Ningún profile datum altera oportunidades, chance, candidates, lane o articulación.
- No se eliminó `DensityDecay` ni `DensityGraceColumns`.
- No se eliminó el lane gap fallback.
- No se cambió source affinity ni distance weighting.
- No se deduplicaron todavía los votos del selector LN.
- No se implementó backoff local/section/global.
- No se definieron sections ni segmentación adaptativa.
- No se implementó `ChordCompletionModel`.
- No se implementó resulting-state style validation.
- No se implementó MapperSupport ni confidence.
- No cambió la semántica Bernoulli de AddChance.
- No se implementó `ParentArticulationPlan` ni múltiples articulaciones.
- No se introdujeron roles de lane ni pattern classifiers.

## Behavior Decision Audit

Total decisions: **82**.

- User intent: **3**.
- Hard invariant: **14**.
- Mapper-derived candidates: **50**.
- Implementation-only: **4**.
- Open design: **11**.

El total es un inventario inicial, no una afirmación de exhaustividad permanente. Debe actualizarse con cada policy version.

## MapperEvidenceProfile

### Observation model

Cada objeto en `OriginalObjects` recibe un ID ordinal estable para ese contenido canónico y conserva:

- source sequence;
- lane y tipo;
- head/release exactos en ms;
- head/release en beats `decimal`;
- duración derivada.

El ID se interpreta junto con `ChartFingerprint`; reordenar el contenido original produce otro fingerprint.

### Relations and provenance

- Chords: IDs miembros y estado de ocupación.
- LN duration/release: IDs de todos los originales que observan el valor.
- Same-lane: IDs before/after, tipo y gap.
- Retrigger: LN y siguiente head, tipo y gap.
- Interior anchor: parent ID y witness IDs de heads/releases.

Los skeletons de witness/certificate permiten deduplicación futura, pero no autorizan candidates en Phase A.

### Fingerprint/version

El fingerprint incluye `KeyCount`, timing points positivos y objetos originales canónicos. No incluye lines metadata, seed, ADD, rango, added objects ni replacements.

### Real-chart diagnostic

Chart: Spring of Dreams, 7K, mapa original empleado en las iteraciones previas.

| Metric | Result |
|---|---:|
| Original objects / observations | 4,608 |
| Relations | 13,975 |
| Distinct head timestamps / chord observations | 2,356 |
| Distinct LN durations | 67 |
| Distinct exact releases | 2,020 |
| Same-lane transitions | 4,601 |
| Retrigger observations | 3,586 |
| Interior anchor observations | 3,432 |
| Fingerprint | `a7c5e56b3de4feb996ee1339d27e11a651919d138c47b7bde70dd081a10ae424` |

No se presenta “evidence coverage” ni “confidence”: Phase A todavía no define transformaciones comparables ni sus denominadores.

## Behavioral regression

Se guardaron outputs pre/post para:

- rice 4K;
- LN durations;
- interior/articulation configuration;
- 7K;
- 18K;
- timing vocabulary;
- local gap.

Resultado de las siete comparaciones:

- Output equality: **PASS** — SHA-256 del `.osu` idéntico en cada caso.
- Decision equality: **PASS** — todas las columnas CSV previas coinciden, excluyendo paths y timers.
- RNG equality: **PASS** — transcript de `NextDouble`, `Next(max)` y derivación cubierto por test.
- Existing metrics equality: **PASS** — métricas deterministas previas idénticas; solo se agregaron observacionales.

La matriz histórica guardada no coincidía con una regeneración del código actual por cambios anteriores a Phase A. Para evitar una conclusión falsa, se regeneró desde una copia separada del código pre-Phase-A. Comparación controlada:

- `cross-key-matrix.csv` pre-code vs post-code: **PASS, exact**.
- `vertical-density-matrix.csv` pre-code vs post-code: **PASS, exact**.

Artifacts: directorio hermano `phase-a-baseline/` fuera del árbol de source.

## Tests

- `dotnet restore`: **PASS**.
- `dotnet build -c Release`: **PASS**, 0 errores.
- `dotnet test -c Release`: **PASS**.
- Passed: **136**.
- Failed: **0**.
- Skipped: **0**.
- Warning: `NU1900` al consultar datos de vulnerabilidad de NuGet porque `api.nuget.org` no fue accesible desde el entorno. Restore utilizó los paquetes disponibles y completó.

Cobertura nueva:

- perfil determinista;
- independencia de seed, chance y rango;
- added/replacements excluidos;
- no alias mutable;
- provenance de todos los vocabularios iniciales;
- 1/5, 1/10 y offset exactos;
- 1K/4K/7K/10K/18K;
- JSON requerido sin confidence;
- perfil preparado validado contra fingerprint;
- output/RNG/métricas sin cambios.

## Performance

Medición observacional en Spring of Dreams:

- `ProfileBuildMs`: **306.748 ms** en la ejecución medida.
- Estimación estructural determinista: **1,222,396 bytes**.
- JSON pretty-printed exportado: **6,361,377 bytes**.
- Observations: **4,608**.
- Relations: **13,975**.

La estimación estructural no pretende ser working-set real del proceso. El JSON es mayor por nombres de campos, indentación e IDs serializados.

Para pruebas masivas Web construye el perfil una sola vez por chart y lo comparte de forma inmutable entre runs. Cada uso preparado recalcula solo el fingerprint para impedir que se aplique un perfil de otro chart. CLI construye una vez por ejecución.

No se observó regresión conductual. La extracción actual es suficiente para Phase A, pero `BuildChords` y relaciones interiores deben perfilarse otra vez antes de ampliar el modelo.

## Remaining design questions

1. Definición de contextos comparables y semántica exacta de `LocalMismatch`.
2. Denominadores de opportunities/transformations para cobertura.
3. Representación de structural diversity y stability de singletons.
4. Qué relaciones mínimas autorizan composición, especialmente LN interior.
5. Candidate identity estable sin congelar prematuramente el futuro planner.
6. Deducción y evaluación del estado resultante de chords.
7. Segmentación por recurrencia, change points o combinación.
8. Equivalencia y roles de lanes.
9. Unidad de un eventual presupuesto ADD y contrato con articulation.
10. Representación atómica de múltiples articulaciones.
11. Si y cómo un certificate explicativo debe producir posteriormente un ranking.

Estas preguntas quedan deliberadamente abiertas. Resolverlas corresponde a Phase B o fases de investigación posteriores, no a Phase A.
