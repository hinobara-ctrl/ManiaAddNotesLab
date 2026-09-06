# Mapper-Derived Implementation Roadmap

Estado: roadmap de transición aprobado para implementación incremental.  
Baseline revisado: copia local de `ManiaAddNotesLab`, 6 de septiembre de 2026.  
Phase A/B: **COMPLETE**. Phase C1 research: **HOLD — DEDUPLICATION RULE NOT JUSTIFIED**; generation continúa en `legacy-experimental.1`.

Este documento reconcilia la visión de `FUTURE_MAPPER_DERIVED_ALGORITHM_PLAN.md`, la revisión crítica `MAPPER_DERIVED_PROPOSALS_REVIEW.md`, el blueprint previo y el código real. La visión establece el destino; la revisión identifica peligros conceptuales; este roadmap define una secuencia implementable. Ninguno reemplaza a los otros.

Las etiquetas usadas son:

- **CURRENT:** comportamiento comprobado en el código actual.
- **EXPERIMENTAL NOW:** comportamiento implementado detrás de opciones A/B o todavía pendiente de playtesting.
- **FUTURE PROPOSED:** dirección diseñada, aún no implementada.
- **RESEARCH REQUIRED:** pregunta que necesita evidencia antes de escoger una política.

---

# 1. Final objective

**FUTURE PROPOSED.** La interfaz final debería necesitar únicamente chart, intensidad ADD, seed, rango y generación. Toda preferencia que describa el estilo debe provenir de evidencia original o de una generalización explícita, auditable y versionada basada en ella.

```text
User intent                    Original mapper evidence
AddChance / Seed / Range       values / relations / compositions
           │                              │
           └──────────┬───────────────────┘
                      ▼
             supported transformations
                      │
                      ▼
             hard-valid current result
```

Zero-config no significa cero algoritmos ni cero constantes. Permite:

- intención del usuario;
- invariantes de formato y seguridad;
- decisiones de implementación que demostradamente no cambian estilo.

No permite ocultar preferencias en thresholds, pooling, desempates, identidades, orden de resolución o modelos estadísticos no auditados.

---

# 2. Existing baseline

## CURRENT

- Parser/writer `.osu` independiente, con keymodes 1K–18K.
- `BeatTimeline` en `decimal`, BPM variables, offsets y conservación de releases originales.
- `OriginalChartAnalysis` con objetos originales temporizados, índices por lane, heads, LNs y releases.
- `LaneGeometryIndex` para overlap, spacing y ocupación.
- Separación entre geometría congelada para densidad y geometría mutable para colocaciones.
- Una oportunidad base por head original; los sintéticos no generan oportunidades recursivas.
- Densidad vertical moderna por heads simultáneos y escala relativa; modo legacy A/B.
- Densidad contextual por ventanas fijas; modo relativo y toggle A/B.
- Duraciones/releases LN locales ponderados y snapping relativo al mapa.
- Lane gap local con fallback fijo.
- Interiores LN original-only con anchors, mínimos y cap; inactivos por defecto.
- Articulación en segunda pasada con RNG derivado y una sustitución por parent; inactiva por defecto.
- `AddedHeads`, `AddedReleases` y `AddedInteractions`.
- CLI, Web, trace, CSV y herramienta de experimentos.
- 121 tests verdes antes de Phase A.

## EXPERIMENTAL NOW

- Naturalidad de densidad, interiores y articulación.
- Constantes de contexto, afinidad, soporte y caps.
- Generalización relativa que todavía usa 7K como escala de referencia matemática.
- Uniformidad de selección entre lanes legales.

## No debe rehacerse en Phase A

Parser, writer, timeline, geometría, orden del generador, RNG, selección, opciones existentes y segunda pasada. Phase A agrega observación al lado del motor.

---

# 3. Architectural principles

La arquitectura separa explícitamente dos capas.

## Evidence Layer

```text
OriginalObjects
  → OriginalEvidence observations
  → observed relations
  → TransformationWitness
  → MapperEvidenceProfile
  → SupportCertificate
```

- `OriginalEvidence` es inmutable y usa identidades estables, no referencias mutables.
- `AddedObjects` y `ArticulationReplacements` jamás se convierten en evidencia durante la misma run.
- Phase A describe; no autoriza transformaciones.

## Generation Layer

```text
opportunity
  → legal candidate set
  → evidence query
  → supported transformation
  → resulting-state validation
  → selection
  → output
```

- `CurrentGeometry` incluye originales y sintéticos ya colocados.
- Consultarla no equivale a aprender de sintéticos: cambia el estado evaluado, no el vocabulario que juzga.
- `HardValidity` domina siempre a `StyleEvidence`.
- `ArticulationReplacements` siguen siendo output; su parent permanece en `OriginalEvidence`.

---

# 4. Evidence terminology

**Observation:** representación por valor de un objeto original con lane, tipo, tiempos exactos y beats.

**ObservationId:** identidad estable dentro del chart canónico. Debe ser única, determinista y no depender de seed, rango, ADD ni objetos añadidos.

**TransformationWitness:** conjunto deduplicado de `ObservationId` y etiquetas que explica qué originales respaldan una afirmación. Varias etiquetas sobre el mismo ID siguen siendo un testigo independiente.

**ObservedValue:** un valor existe, por ejemplo una duración de `0.2 beat`.

**ObservedRelation:** una relación existe, por ejemplo una LN termina y luego aparece un head en la misma lane.

**CompatibleComposition:** un conjunto de relaciones puede coexistir en la transformación completa. No se deduce automáticamente de marginales observados por separado.

**SupportCertificate:** representación explicativa de testigos, requisitos satisfechos/faltantes, alcance y generalizaciones. Inicialmente no es un score.

**Scope:** nivel de procedencia: `Local`, `StructuralContext/Section` o `Global`.

**Generalization:** detalle omitido para ampliar comparabilidad, registrado de forma explícita y versionada.

**LocalMismatch:** existen contextos locales comparables, pero respaldan otra conducta. No habilita backoff automático.

**NoComparableContext:** no existe contexto local donde formular la comparación. Permite intentar un scope más amplio.

**AmbiguousEvidence:** la evidencia comparable se contradice o cambia de forma inestable según región/generalización. No debe convertirse en falsa certeza.

---

# 5. BehaviorDecisionAudit

La auditoría cubre números y decisiones categóricas. Cada fila debe registrar:

```text
Decision | Location | Current behavior | Affects style? | Category
Source | Future derivation candidate? | Phase | Notes
```

Categorías:

- `USER_INTENT`
- `HARD_VALIDITY_INVARIANT`
- `CURRENT_POLICY_CONTRACT`
- `FUTURE_POLICY_CONTRACT`
- `MAPPER_DERIVED_CANDIDATE`
- `IMPLEMENTATION_ONLY`
- `OPEN_DESIGN_DECISION`

Debe incluir como mínimo:

- defaults, thresholds, ventanas, caps y tolerancias;
- una oportunidad por head;
- lane uniforme;
- pooling entre lanes;
- identidad y partición del RNG;
- orden cronológico y desempates;
- agrupación decimal;
- definición de contexto comparable;
- construcción/deduplicación de candidates;
- fallbacks y orden de backoff;
- qué métricas se usan como denominador.

Una decisión solo puede clasificarse `IMPLEMENTATION_ONLY` si existe argumento o test que muestre que no cambia decisiones estilísticas.

---

# 6. MapperEvidenceProfile

## Phase A: contenido inicial

- fingerprint canónico del chart;
- versión del perfil y de la política conductual;
- `KeyCount` y cantidad original;
- observaciones de heads/taps/LNs con timing exacto y beats;
- intervalos y duraciones LN;
- releases exactos;
- grupos de heads simultáneos;
- held occupancy original en esos timestamps;
- transiciones consecutivas same-lane y gap observado;
- transiciones release→head;
- anchors originales dentro de parents LN;
- resumen de vocabulario temporal;
- provenance mediante `ObservationId`.

Colecciones públicas realmente inmutables; no basta exponer como `IReadOnlyList` una lista compartida.

## No incluir todavía

- secciones automáticas;
- confidence escalar;
- `MapperSupport` unificado;
- presupuesto ADD;
- roles de lane aprendidos;
- phrase model;
- múltiples articulaciones;
- decisiones que alteren selección.

El perfil depende solamente del chart original y versión de extracción. `Seed`, `AddChance`, `SelectedRange`, `AddedObjects` y `ArticulationReplacements` no participan.

---

# 7. Evidence backoff

Secuencia futura:

```text
Local
  ├─ ObservedSupport → usar y explicar
  ├─ LocalMismatch → conservar mismatch; no pedir apoyo global automáticamente
  ├─ AmbiguousEvidence → generalizar solo bajo política explícita o abstenerse
  └─ NoComparableContext
         ↓
StructuralContext / Section
         ↓ solo ante NoComparableContext
Global
         ↓ solo ante NoComparableContext
SKIP
```

Un singleton es evidencia real, pero mantiene separados `frequency`, `sample size`, `structural diversity`, `scope` y `stability`. `1/1` no significa confianza alta.

La cobertura siempre nombra numerador y denominador. Ejemplos:

- `SupportedTransformations / DistinctTransformations`;
- `SupportedOpportunities / OriginalOpportunities`.

Denominador cero se presenta como `N/A`.

---

# 8. Hard validity, policy contracts and style evidence

`HARD_VALIDITY_INVARIANT` responde si una transformación puede existir o serializarse de forma válida:

- tiempos ordenados y duración positiva;
- representación válida tras redondeo;
- no overlap;
- source inmutable;
- lane válida;
- output reparseable.

`CURRENT_POLICY_CONTRACT` describe la semántica conductual vigente, aunque sea revisable: no recursividad, evidencia `OriginalObjects`-only, articulación en segunda pasada, pass 1 idéntico OFF/ON y una oportunidad base por head. Solo puede cambiar bajo una nueva `BehaviorPolicyVersion` explícita. `FUTURE_POLICY_CONTRACT` reserva contratos de políticas futuras que aún no gobiernan generación.

`StyleEvidence` responde si el original respalda una transformación. Un certificado fuerte no puede volver legal una colisión. Una transformación legal sin evidencia puede terminar en `SKIP` en una política estricta. Ninguna policy ni evidencia puede compensar un `HARD_VALIDITY_INVARIANT` fallido.

El futuro pipeline debe mantener resultados separados, nunca `GeometrySupport=0.2` dentro de una suma compensable.

---

# 9. Rice roadmap

## ChordCompletionModel — FUTURE PROPOSED

Crear ejemplos de completado ocultando conceptualmente un miembro de un chord original. El objeto ocultado sigue siendo evidencia original; el estado reducido es un instrumento de consulta.

Evaluar el estado vertical resultante completo, no solo cada oportunidad contra el estado inicial. Si un chord original de tres heads crea tres oportunidades, no permitir que tres evaluaciones independientes `3→4` terminen en seis.

Conservar:

- conteo discreto y ratio;
- mezcla tap/LN;
- held occupancy como contexto y geometría, no como head nuevo;
- miembros originales deduplicados.

## Cross-key

Una política 1K–18K sin ramas por K. Normalización no borra que agregar una lane en 4K y 10K son incrementos diferentes.

## Contextual density — RESEARCH REQUIRED

Comparar recurrencia y segmentación en shadow mode. Rareza no equivale automáticamente a penalización.

## Lane selection audit

La elección uniforme actual es una hipótesis estilística. Investigar coocurrencia, sucesión y posibles roles demostrados; no introducir clasificadores de patrones o manos sin evidencia.

---

# 10. LN roadmap

- Extraer `shape witnesses` con duración, release y contexto temporal.
- Conservar diferencia entre duración trasladada y release exacto.
- Deduplicar por `(candidate, ObservationId)`; varias etiquetas no multiplican testigos.
- Condicionar gaps por transición: tap→tap, tap→LN, LN→tap, LN→LN, release→tap y release→LN head cuando corresponda.
- Separar colisión/orden de spacing estilístico.
- No interpretar el mínimo observado como autorización de cualquier gap superior.
- Extraer relaciones de contención: parent, anchor, child/intersecting LN y relación de releases.
- Distinguir `contained` de `crossing`; el código CURRENT no fuerza universalmente que un candidate interior termine antes de la parent.
- Preservar 1/5, 1/10, offsets y cambios BPM.

---

# 11. Articulation roadmap

## CURRENT

Una oportunidad interior fallida crea intent si `NonHeldColumns <= threshold`. `PlaceLongNote` expone `null` y flags agregados; no demuestra blockers originales.

## FUTURE PROPOSED

Introducir causas explícitas:

```text
NoShapeEvidence
NoGapEvidence
BlockedByOriginalHold
BlockedByOriginalHead
BlockedBySyntheticPlacement
BlockedBySpacing
```

La articulación se habilita solo si candidates respaldados fallaron por holds originales y existe evidencia retrigger compatible. Fallos por sintéticos no demuestran estilo Full-LN original.

`ParentArticulationPlan` es una fase posterior: lista atómica de segmentos y cortes, validada como conjunto. No se implementa en Phase A. Eliminar el cap actual requiere comprobar segmentos intermedios y compatibilidad global, no solo gaps individuales.

---

# 12. Candidate evidence

Un candidate puede recibir varias explicaciones de una observación:

```text
Observation O17
  ├─ aporta duration
  └─ coincide con exact release
```

El certificado conserva ambas etiquetas, pero `IndependentWitnessCount=1`. Solo IDs distintos aumentan testigos independientes.

Los testigos deben ser conjuntos ordenados y deduplicados. Una parent que participa en varias relaciones no se clona como evidencia. Alternativas idénticas originadas por varias rutas se fusionan antes de crear competencia probabilística.

---

# 13. Resulting-state validation

El perfil permanece original-only, pero cada transformación futura debe evaluar el resultado acumulado.

```text
Frozen evidence: original chord vocabulary
Current result: original + accepted synthetics/replacements
Question: does accepting this candidate leave a supported composition?
```

Soporte marginal individual no garantiza composición. Deben agruparse transformaciones mutuamente dependientes por timestamp/parent y validar el estado final. La geometría mutable también distingue blockers originales de conflictos creados por sintéticos.

---

# 14. SupportCertificate

Primera representación explicativa, sin confidence inventada:

```text
Candidate identity
Claim level: ObservedValue / ObservedRelation / CompatibleComposition
Scope
Witness IDs + tags
Satisfied requirements
Missing requirements
Generalizations applied
Evidence state: support / mismatch / no comparable / ambiguous
Hard validity: separate result
```

Phase A puede introducir el esqueleto inmutable aunque ningún selector lo consuma. No debe fingir que la teoría completa está resuelta.

---

# 15. MapperSupport

**RESEARCH REQUIRED.** No imponer fórmula multiplicativa/aditiva ni una probabilidad de “aprobación del mapper”. Primero validar certificados por dimensión y soporte conjunto de contextos comparables.

Un score posterior deberá demostrar utilidad ordenando reconstrucciones originales frente a alternativas, sin compensar `HardValidity` ni convertir ausencia de muestra en certeza.

---

# 16. AddChance semantics

## CURRENT Bernoulli semantics

Cada oportunidad realiza una tirada usando `Chance × ChordFactor × ContextualFactor`, limitada a la chance base. Candidate y lane usan el mismo stream; articulación usa un stream derivado.

## FUTURE budget hypothesis

ADD podría seleccionar un prefijo o conjunto de transformaciones respaldadas, deduplicadas y compatibles. La unidad —transformación, objeto o interacción— está abierta.

Existe una incompatibilidad real: un presupuesto global compartido entre pass 1 y articulación permite que activar articulación cambie pass 1; el contrato CURRENT promete identidad de pass 1 ON/OFF. No se resolverá silenciosamente. Cualquier modelo presupuestario será otra política versionada y deberá declarar qué contrato reemplaza.

---

# 17. Adaptive context

**RESEARCH REQUIRED, shadow first.** Construir una serie por timestamps originales con spacing, heads, releases, mezcla y holds.

Prototipos posibles:

- estructura de secuencias recurrentes;
- change-point segmentation;
- combinaciones justificadas.

No se selecciona PELT, CROPS ni otro método como definitivo. Cada método conserva decisiones de representación y penalización auditables. Evaluar reconstrucción de bloques retenidos, sensibilidad de límites y charts cortos antes de usar secciones para generación.

---

# 18. Multi-key invariance

El código soporta realmente 1K–18K; el roadmap conserva ese contrato.

- sin ramas 4K/7K/10K;
- conteos para geometría, ratios para comparación;
- evidencia aprendida dentro del chart, sin transferencias cross-key implícitas;
- fixtures 1K, 4K, 7K, 10K y 18K;
- permutación/espejado solo cuando la política no afirma roles espaciales.

---

# 19. Abstention policy

```text
No evidence
→ broader comparable evidence
→ SKIP
```

Pero `LocalMismatch` detiene el backoff automático. `AmbiguousEvidence` se conserva como tal. El sistema no fuerza el presupuesto ni utiliza defaults para alcanzar ADD 100.

Todo `SKIP` futuro debe distinguir falta de evidencia, mismatch, ambigüedad, hard invalidity, conflicto con sintéticos o presupuesto.

---

# 20. Validation strategy

## Unit

IDs, fingerprint, inmutabilidad, extracción, deduplicación, provenance y serialización.

## Fixtures

Rice, chord-heavy, LN, Full-LN, gaps por transición, timing raro, BPM variable y evidencia escasa.

## Property/metamorphic

Independencia de seed/ADD/rango, no contaminación sintética, determinismo, permutaciones válidas, output reparseable y no overlap.

## Cross-key

1K/4K/7K/10K/18K y estructuras equivalentes.

## Real charts

Distribución, cobertura con denominador, rendimiento y causas. No confundir evidencia histórica con rerun actual.

## Shadow/A-B/playtesting

Shadow antes de conducta; A/B con una sola política; playtesting como evidencia humana separada de correctitud estructural.

---

# 21. Incremental implementation plan

## Phase A — Evidence Infrastructure

**Goal:** observar y explicar sin cambiar generación.

**Files likely affected:** nuevos archivos `Core/Evidence`; `Model.cs`, integración mínima en `AddNotesEngine.cs`, CLI para export; tests; docs.

**Behavior change?:** no. Solo tiempo y métricas observacionales nuevas.

**New data structures:** `OriginalObservationId`, `OriginalObservation`, vocabularios iniciales, relations, `TransformationWitness`, `SupportCertificate` skeleton, `MapperEvidenceProfile`.

**Metrics:** `ProfileBuildMs`, estimación de tamaño, observation count, relation count.

**Tests:** determinismo, independencia de seed/ADD/rango, originales-only, IDs, timing, multikey, no alias mutable, output/RNG/counters iguales.

**Artifacts:** JSON, `BEHAVIOR_DECISION_AUDIT.md`, baseline pre/post e informe Phase A.

**Rollback:** retirar construcción/propiedad de perfil y CLI export; no hay migración de datos ni modificación de policy.

**Acceptance criteria:** los cuatro checks de regresión pasan; tests previos más tests nuevos verdes; perfil inspeccionable y versionado.

**Status:** COMPLETE (`phase-a.1`, 136 tests al cierre de Phase A).

## Phase B — Certificates and explicit failure causes in shadow

**Goal:** emitir certificados reales y separar causas sin usarlas para seleccionar.

**Files likely affected:** Evidence resolver, resultados de geometría, trace/tests.

**Behavior change?:** no.

**New data structures:** rejection detail, blockers por candidate, certificates por consulta.

**Metrics:** causas con denominador candidate/lane.

**Tests:** blockers originales/sintéticos, forma/gap/spacing separados.

**Artifacts:** comparison report.

**Rollback:** mantener resultados booleanos legacy y desconectar diagnostics.

**Acceptance criteria:** decisiones legacy idénticas; causas reconstruibles.

## Phase C1 — LN witness deduplication A/B

**Goal:** corregir exclusivamente el doble conteo de rutas duration/release por `ObservationId`.

**Files likely affected:** candidate builder, retrigger analyzer, policy version, experiments.

**Behavior change?:** sí, solo bajo nueva opción/version.

**New data structures:** candidate-witness map.

**Metrics:** labels vs independent witnesses.

**Tests:** un objeto/dos etiquetas cuenta uno; dos IDs cuentan dos.

**Artifacts:** A/B de deduplicación LN.

**Rollback:** seleccionar policy legacy.

**Acceptance criteria:** cambio atribuible, hard validity intacta, playtesting pendiente declarado.

**Research status (2026-09-06): HOLD.** La factorización canónica por ObservationId existe y el shadow respeta
exactamente los casos sin duplicados, pero held-out reconstruction empeora ligeramente top-1 y mean rank en las tres
variantes reales disponibles, todas de la misma familia Spring. No se autorizó ni implementó el A/B conductual.
Véase `PHASE_C1_LN_WITNESS_DEDUP_REPORT.md`.

## Phase C2 — Retrigger-specific frequency A/B

**Goal:** reemplazar el soporte agregado aplicado a cada gap por frecuencia específica por gap y tipo de transición.

**Behavior change?:** sí, solo bajo nueva opción/version.

**Acceptance criteria:** distribución 8:2 permanece 8:2 en el selector y el efecto no se confunde con C1.

## Phase D0 — ChordCompletion reconstruction / Shadow

**Goal:** reconstruir miembros ocultos de chords originales y medir capacidad explicativa sin intervenir generación.

**Behavior change?:** no.

**Acceptance criteria:** estados y denominadores reconstruibles en 1K/4K/7K/10K/18K.

## Phase D1 — ChordCompletion resulting-state A/B

**Goal:** evitar acumulación de completados individuales sin soporte conjunto.

**Files likely affected:** event grouping, rice policy, frozen/current state query.

**Behavior change?:** sí, versionado.

**New data structures:** chord witnesses, target state, timestamp plan.

**Metrics:** original/result chord states y rejected composition.

**Tests:** ejemplo 3→4 no termina en 6; held tails no enseñan heads.

**Artifacts:** cross-key/chord-heavy A/B.

**Rollback:** policy legacy.

**Acceptance criteria:** estados finales explicables y 1K–18K sin branches.

## Phase E — Adaptive context prototypes / Shadow

**Goal:** comparar recurrencia y segmentación.

**Files likely affected:** Evidence analysis y diagnostics; no engine selection.

**Behavior change?:** no.

**New data structures:** event series, proposed boundaries, validation blocks.

**Metrics:** held-out reconstruction y estabilidad de límites con denominador.

**Tests:** intro/clímax, A/B/A distante, BPM variable, chart corto.

**Artifacts:** prototype report.

**Rollback:** eliminar prototipo/caches.

**Acceptance criteria:** método seleccionado solo si supera baseline simple y sensibilidad queda documentada.

## Phase F1 — Comparable-context resolver / Shadow + validation

**Goal:** definir y validar `ObservedSupport`, `LocalMismatch`, `NoComparableContext` y `AmbiguousEvidence` antes de que alteren generación.

**Behavior change?:** no.

**Acceptance criteria:** resolución explicable y validación held-out sin backoff conductual.

## Phase F2 — Typed gaps and evidence backoff A/B

**Goal:** local/structural/global/SKIP con mismatch explícito y gaps por transición.

**Files likely affected:** evidence resolver, geometry spacing interface, policy options.

**Behavior change?:** sí, versionado.

**New data structures:** scope resolution y typed transition gap.

**Metrics:** backoffs por causa y cobertura definida.

**Tests:** mismatch no toma clímax global; outlier no relaja todas las lanes.

**Artifacts:** coverage/A-B report.

**Rollback:** fallback legacy.

**Acceptance criteria:** ningún fallback oculto; skips explicados.

## Phase G1 — Interior relation semantics A/B

**Goal:** versionar relaciones `contained`, `crossing` y `equal-end` respaldadas por originales.

**Behavior change?:** sí, versionado.

**Acceptance criteria:** un único cambio de semántica interior, medible por separado de articulación.

## Phase G2 — Causal articulation A/B

**Goal:** derivar oportunidades por relaciones y activar articulation por blocker original.

**Files likely affected:** interior builder, geometry detail, articulation pass.

**Behavior change?:** sí, versionado.

**New data structures:** containment witness, causal intent.

**Metrics:** contained/crossing, blocker categories.

**Tests:** parent corta respaldada; fallos por tap/gap/sintético no articulan.

**Artifacts:** LN-heavy A/B.

**Rollback:** eligibility/saturation legacy.

**Acceptance criteria:** causalidad demostrable y pass 1 contract explícito.

## Phase H — ParentArticulationPlan

**Goal:** investigar múltiples cortes atómicos.

**Files likely affected:** chart model, writer, planner, geometry.

**Behavior change?:** sí, policy nueva.

**New data structures:** parent plan y segmentos.

**Metrics:** parents, cuts, segments y interactions separados.

**Tests:** solver contra enumeración exhaustiva en fixtures pequeños.

**Artifacts:** solver report.

**Rollback:** cap de una articulación y representación anterior.

**Acceptance criteria:** aplicación atómica, todos los segmentos válidos, no recursión.

## Phase I — MapperSupport research

**Goal:** probar ordenaciones derivadas desde certificates.

**Files likely affected:** research tool inicialmente.

**Behavior change?:** shadow primero; luego A/B versionado.

**New data structures:** ranking explanation.

**Metrics:** recuperación held-out y calibración descriptiva, no “mapper confidence”.

**Tests:** mismos marginales/diferentes relaciones producen resultados distintos.

**Artifacts:** model comparison.

**Rollback:** certificates sin score.

**Acceptance criteria:** fórmula solo si supera alternativas simples sin romper explicabilidad.

## Phase J — AddChance budget research

**Goal:** definir unidad y conflictos del presupuesto.

**Files likely affected:** selector/planner, policy version, UI.

**Behavior change?:** sí y semánticamente incompatible con legacy.

**New data structures:** transformation units, conflict groups, planned capacity.

**Metrics:** requested/used/unused con denominador.

**Tests:** candidatos alternativos no inflan capacidad; anidamiento solo si se promete.

**Artifacts:** Bernoulli/budget A/B.

**Rollback:** policy Bernoulli.

**Acceptance criteria:** contrato de pass 1/articulation resuelto explícitamente.

## Phase K — Zero-config validation

**Goal:** retirar controles estilísticos de la UI principal.

**Files likely affected:** Web/CLI/docs; Core conserva versiones legacy reproducibles.

**Behavior change?:** cambia default solo tras aceptación.

**New data structures:** ninguna obligatoria.

**Metrics:** cobertura por familias y charts.

**Tests:** suite completa, real charts, AiMod y playtesting.

**Artifacts:** release report.

**Rollback:** interfaz laboratorio y policy anterior.

**Acceptance criteria:** controles ya no necesarios en colección diversa; preguntas abiertas no se ocultan.

---

# 22. Risks

| Riesgo | Manifestación | Mitigación |
|---|---|---|
| Overfitting a single chart | copia rarezas/errores | diversidad estructural, scope y held-out blocks |
| Singleton evidence | `1/1` parece certeza | separar frecuencia, N, diversidad y estabilidad |
| Marginal statistics combined incorrectly | valores válidos forman relación inválida | relations y CompatibleComposition |
| Context leakage | misma frase dona y valida | exclusión por bloques/observaciones |
| Synthetics contaminating evidence | feedback estilístico | builder solo usa `OriginalObjects`; tests |
| Double-counting witnesses | duration+release duplica autoridad | deduplicación por ObservationId |
| Candidate accumulation | 3→4 repetido termina en 6 | resulting-state/timestamp plan |
| False confidence | porcentaje sin interpretación | certificate descriptivo; no confidence Phase A |
| Performance/memory | ventanas/relaciones duplicadas | índices, shared IDs, medición y cache inmutable |
| Behavior versioning | resultados irreproducibles | profile/policy version en artifacts |
| RNG instability | IDs/diagnostics cambian stream | Evidence Layer sin RNG y regression tests |
| Mutable aliasing | chart cambia perfil después | records por valor + immutable collections |
| Ambiguous coverage | “80% evidence” | numerador/denominador y N/A |
| Gap overgeneralization | mínimo autoriza todo | transición tipada y relación resultante |
| Mixed blockers | holds reciben causa incorrecta | razones por candidate/lane y blockers explícitos |

---

# 23. Definition of done

La arbitrariedad estilística estará eliminada cuando:

1. Toda transformación entregue un certificado con originales y generalizaciones.
2. `ObservedValue`, `ObservedRelation` y `CompatibleComposition` no se confundan.
3. Las preferencias restantes estén auditadas y versionadas.
4. `HardValidity` sea independiente e inderrotable por scores.
5. La evidencia sea original-only y el estado resultante se valide.
6. `LocalMismatch`, `NoComparableContext` y ambigüedad tengan semántica distinta.
7. No existan reglas especiales por keymode.
8. ADD tenga una semántica documentada y reproducible.
9. La cobertura tenga denominador y la abstención sea explicable.
10. Tests, A/B y playtesting diverso respalden el default zero-config.
11. `Active manually sourced style decisions = 0` para la policy mapper-derived por defecto.
12. Toda decisión activa esté clasificada como `USER_INTENT`, `HARD_VALIDITY_INVARIANT`, `CURRENT/FUTURE_POLICY_CONTRACT`, `MAPPER_DERIVED` o `IMPLEMENTATION_ONLY` con evidencia de neutralidad estilística.
13. Ningún `OPEN_DESIGN_DECISION` que afecte estilo sea usado por la policy default final: debe resolverse o permanecer inactivo.

Documentar o versionar un magic number no lo convierte en mapper-derived. No se considera terminado solo porque desaparezcan sliders o constantes visibles.

---

# Roadmap Gate

1. **PASS — Distingue visión de implementación actual.** Todas las áreas usan CURRENT, EXPERIMENTAL NOW, FUTURE PROPOSED o RESEARCH REQUIRED.
2. **PASS — No contradice invariantes actuales.** Conserva source, timing, no-overlap, determinismo, rango y segunda pasada.
3. **PASS — Define evidence vs hard validity.** Son contratos separados y validity no es compensable.
4. **PASS — Preserva OriginalObjects-only.** Added objects y replacements nunca entran al perfil de la run.
5. **PASS — Distingue value/relation/composition.** Los tres niveles poseen semántica y tests futuros distintos.
6. **PASS — Distingue no evidence de local mismatch.** Solo `NoComparableContext` permite backoff automático.
7. **PASS — Incluye resulting-state validation.** Agrupa estado por timestamp/parent y consulta CurrentGeometry sin aprender de ella.
8. **PASS — Evita un MapperSupport prematuro.** Phase A introduce certificate skeleton, no score.
9. **PASS — Mantiene AddChance budget como hipótesis futura.** Bernoulli continúa CURRENT; incompatibilidad con pass 1 queda abierta.
10. **PASS — Evita decidir segmentación prematuramente.** Recurrencia/change-point son prototipos shadow, no elección definitiva.
11. **PASS — Tiene fases pequeñas y reversibles.** Cada incremento declara rollback y policy versionada cuando cambia conducta.
12. **PASS — Phase A puede implementarse sin cambiar output.** Solo agrega estructuras, perfil, métricas observacionales y export.
13. **PASS — Tiene tests objetivos.** Unit, metamorphic, fixtures, cross-key, baseline y RNG.
14. **PASS — Tiene rollback claro.** Phase A se desconecta sin migración; fases conductuales conservan legacy.
15. **PASS — No introduce reglas especiales por keymode.** Mantiene 1K–18K y una extracción única.

**ROADMAP GREEN — PHASE A IMPLEMENTATION AUTHORIZED**

Registro histórico: Phase A fue implementada y cerrada con 136 tests verdes.

---

# Phase B pre-gate

1. **PASS — Phase A baseline sigue verde.** Se reejecutaron 136/136 tests antes de modificar código.
2. **PASS — Evidence Layer sigue original-only.** `MapperEvidenceProfileBuilder` solo lee `OriginalObjects` y timing.
3. **PASS — Candidate selection no necesita modificación.** Diagnostics observa candidates ya construidos por legacy.
4. **PASS — RNG no se consume para diagnostics.** Identidades, certificates y filtros son deterministas y no usan RNG.
5. **PASS — Failure provenance puede añadirse sin alterar geometría.** La ruta legacy conserva autoridad sobre el bool.
6. **PASS — Candidate identity diagnóstica no congela semántica futura.** Es policy-local, versionada y paralela.
7. **PASS — HardValidity permanece independiente.** Es binaria y no entra en weights/scores.
8. **PASS — Output puede permanecer idéntico.** Snapshot pre/post y tests lo verifican.
9. **PASS — Métricas existentes pueden permanecer idénticas.** Solo se agregan diagnostics/timers nuevos.
10. **PASS — Rollback es desconectar diagnostics.** `DiagnosticsEnabled=false` evita collector y export.

**PHASE B GREEN — SHADOW IMPLEMENTATION AUTHORIZED**

Phase B se implementó exclusivamente bajo este gate. C1/C2 y posteriores continúan no autorizadas para cambio conductual.

---

# Phase B acceptance gate

1. Output legacy idéntico: **PASS**.
2. RNG transcript idéntico: **PASS**.
3. Candidate order idéntico: **PASS**.
4. Candidate weights idénticos: **PASS**.
5. Lane selection idéntica: **PASS**.
6. Articulation behavior idéntico: **PASS**.
7. Diagnostic legality coincide con legacy: **PASS**.
8. Blockers poseen provenance: **PASS**.
9. Original vs synthetic blockers distinguibles: **PASS**.
10. Duplicate witness paths observables: **PASS**.
11. Retrigger frequency específica observable: **PASS**.
12. Interior contained/crossing/equal-end observable: **PASS**.
13. Gate legacy de articulation comparable con causa: **PASS**.
14. Confidence no inventada: **PASS**.
15. MapperSupport no inventado: **PASS**.
16. ComparableContext no implementado prematuramente: **PASS** (`NotEvaluated`).
17. Multi-key tests 1K/4K/7K/10K/18K: **PASS**.
18. Profile sigue original-only: **PASS**, `phase-a.1`.
19. Roadmap/audit actualizados: **PASS**.
20. Rollback trivial: **PASS**.

**PHASE B COMPLETE — READY TO DESIGN PHASE C1**

---

# Phase C1 research gate

Design gate: fórmula legacy, observation authority, route metadata, combinación canónica, aislamiento y preservación
del resto: **PASS para shadow**.

Research gate:

1. duplicate authority observable: **PASS**;
2. impacto de ranking no trivial: **PASS**;
3. aggregation sin magic number: **PASS**;
4. no-duplicate exactness: **PASS**;
5. held-out sin degradación sistemática: **FAIL**;
6. diversidad de charts humanos: **RESEARCH REQUIRED**;
7. independencia de KeyCount: **PASS**;
8. scoring restante intacto: **PASS en shadow**;
9. policy A/B conductual: **NO AUTORIZADA**;
10. rollback: **PASS**.

**C1 HOLD — DEDUPLICATION RULE NOT JUSTIFIED**

No se implementó C2 ni ninguna policy conductual C1.
