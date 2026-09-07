# Behavior Decision Audit

Estado: inventario de Phase A, taxonomía refinada antes de Phase B.  
Fecha: 2026-09-06.  
Alcance: Core, parser/writer, CLI, Web y política experimental vigente.

Esta auditoría no elimina ni modifica decisiones. Identifica números y elecciones no numéricas que pueden afectar el resultado para que futuras sustituciones sean versionadas y atribuibles.

## Principio permanente de evidencia

**WITNESS IDENTITY ≠ RELATION COUNT.** `ObservationId` identifica una muestra original independiente. Deduplicar ese ID no autoriza a borrar claims o relaciones distintas: O17 puede conservar `Duration`, `ExactRelease`, `ExactHead(H)` y `HeadToRelease(H,R)` y seguir siendo un solo witness. Tampoco se permite transformar esas relaciones directamente en weight, bonus, confidence o `MapperSupport`; primero deben representarse y validarse.

## Taxonomía de contratos

- `HARD_VALIDITY_INVARIANT`: condiciones no compensables por evidencia o estilo: lane válida, duración positiva, no overlap, tiempos/serialización `.osu` válidos y source no sobrescrito.
- `CURRENT_POLICY_CONTRACT`: semántica revisable que define `legacy-experimental.1`: evidencia original-only, no recursividad, articulación en segunda pasada, pass 1 idéntico OFF/ON y demás compromisos conductuales vigentes. Solo cambia con una nueva `BehaviorPolicyVersion`.
- `FUTURE_POLICY_CONTRACT`: semántica explícita de una policy futura, todavía no activa.

Una policy contract no es una verdad física ni de formato. Un `HARD_VALIDITY_INVARIANT` nunca puede ser compensado por un certificado, score o preferencia estilística.

| Decision | Location | Current behavior | Affects style? | Category | Source | Future derivation candidate? | Phase | Notes |
|---|---|---|---:|---|---|---:|---|---|
| Add chance | `AddNotesOptions.Chance` | Bernoulli por oportunidad con factores | Sí, intensidad | USER_INTENT | Usuario | No; semántica puede investigarse | J | Presupuesto es hipótesis futura |
| Seed | CLI/Web, `SeededRandom` | Reproduce stream pseudoaleatorio | Sí, variante | USER_INTENT | Usuario | No | A | Debe quedar registrada |
| Selected range | `StartMs`/`EndMs` | Filtra heads de oportunidades, no contexto | Sí, alcance | USER_INTENT | Usuario | No | A | Rango inclusivo |
| Source file immutable | writer/flujo CLI | Escribe ruta nueva | No, seguridad | HARD_VALIDITY_INVARIANT | Contrato | No | A | Nunca sobrescribir input |
| OriginalObjects evidence boundary | Core | Solo originales enseñan contexto | Sí | CURRENT_POLICY_CONTRACT | `legacy-experimental.1` | Solo con policy explícita | A | Phase A lo prueba |
| No recursive opportunities | `BuildOpportunities` | Sintéticos no crean oportunidades | Sí | CURRENT_POLICY_CONTRACT | `legacy-experimental.1` | Solo con nueva policy explícita | Future | No cambiar silenciosamente |
| No overlap | `LaneGeometryIndex` | Intervalos/taps incompatibles se rechazan | No, validez | HARD_VALIDITY_INVARIANT | Geometría | No | A | Score nunca compensa |
| Positive LN duration | parser/engine | end debe seguir al head | No, validez | HARD_VALIDITY_INVARIANT | Formato | No | A | Revalidar tras ms |
| Mania only | parser | exige `Mode:3` | No | HARD_VALIDITY_INVARIANT | Formato | No | A | Fuera de alcance otros modos |
| Supported key count | parser/engine | 1K–18K | No, contrato | CURRENT_POLICY_CONTRACT | Implementación validada | Solo con policy explícita | A | Código y tests coinciden |
| Timing point validity | parser/timeline | usa uninherited positive beat length | No | HARD_VALIDITY_INVARIANT | Formato | No | A | BPM changes integrados |
| Determinism | engine/RNG | misma entrada/opciones/seed | Sí | CURRENT_POLICY_CONTRACT | `legacy-experimental.1` | No dentro de versión | A | Timers exceptuados |
| Final writer millisecond representation | `BeatTimeline`/writer | round away from zero | Potencialmente | HARD_VALIDITY_INVARIANT | Formato `.osu` | No sin cambio de formato | A | Validar colapso final |
| Parent remains original evidence after articulation | chart/analysis | replacement solo cambia output | Sí | CURRENT_POLICY_CONTRACT | `legacy-experimental.1` | Solo con policy explícita | A | Profile ignora replacements |
| Articulation pass separation | `Apply` | segunda pasada | Sí | CURRENT_POLICY_CONTRACT | `legacy-experimental.1` | Revisable solo con policy nueva | J | Preserva pass 1 ON/OFF |
| One base opportunity per original head | `BuildOpportunities` | cada objeto original aporta una | Sí | CURRENT_POLICY_CONTRACT | `legacy-experimental.1` | Sí, solo con policy nueva | D0/J | Contrato vigente pero revisable |
| Opportunity chronological order | `BuildOpportunities` | time, kind, order | Sí | MAPPER_DERIVED_CANDIDATE | Diseño legacy | Sí | D/J | Afecta conflictos y RNG |
| Base before interior tie-break | sort de opportunities | enum `BaseHead` antes de interior | Sí | MAPPER_DERIVED_CANDIDATE | Diseño legacy | Sí | G/J | Debe versionarse |
| Uniform legal-lane selection | `PlaceTap`/`PlaceLongNote` | `rng.Next(lanes.Count)` | Sí | MAPPER_DERIVED_CANDIDATE | Diseño legacy | Sí | D | No asume roles del mapper |
| Candidate weighted random | `TakeWeighted` | ruleta de weights | Sí | MAPPER_DERIVED_CANDIDATE | Diseño legacy | Sí | C/I/J | Score futuro no definido |
| Density grace columns | options/density | 2 columnas en escala legacy/7K | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | D | Referencia relativa aún usa 7K |
| Density decay | options/density | `0.65^steps` | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | D | Reemplazar por completion evidence |
| Vertical density input | density mode | heads simultáneos o occupied legacy | Sí | MAPPER_DERIVED_CANDIDATE | Hipótesis experimental | Sí | D | Holds siguen como contexto |
| Relative vertical normalization | density | pasos equivalentes a 1/7 | Sí | MAPPER_DERIVED_CANDIDATE | Generalización manual | Sí | D | Portable, pero no mapper-derived |
| Micro density window | `HeadDensityAnalyzer` | 1 beat | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | E | Shadow segmentation primero |
| Context density radius | analyzer | ±4 beats | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | E | No reemplazar por otro fijo |
| Robust lower median context | analyzer | mediana inferior lateral | Sí | MAPPER_DERIVED_CANDIDATE | Diseño manual | Sí | E | Decisión no expuesta |
| Context baseline floor | analyzer | 1 o `KeyCount/7` | Sí | MAPPER_DERIVED_CANDIDATE | Generalización manual | Sí | E | Evita división pequeña |
| Minimum micro density | analyzer | 2 o `2K/7` | Sí | MAPPER_DERIVED_CANDIDATE | Generalización manual | Sí | E | Gate estilístico |
| Burst ratio | options/analyzer | 1.5 | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | E | Rareza no implica penalización |
| Burst span sampling | analyzer | pasos enteros hasta 4 | Sí | MAPPER_DERIVED_CANDIDATE | Diseño manual | Sí | E | Reemplazar solo tras shadow |
| Burst factors | options/analyzer | 0.45/0.60/0.80/1 | Sí | MAPPER_DERIVED_CANDIDATE | Defaults manuales | Sí | E/I | No unificar prematuramente |
| Local LN context radius | `LnWindowBeats` | ±4 beats | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | E/F | Distancia no equivale a estructura |
| Source affinity | candidate builder | multiplica por 1.25 | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | C | Frecuencia/testigos futuros |
| Distance decay bands | `DistanceWeight` | ceil por beat, caída 0.2 | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | E/I | Contexto predictivo futuro |
| Minimum distance weight | `DistanceWeight` | piso 0.2 | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | E/I | Autoriza influencia distante |
| Duration and release vote addition | candidate builder | suma ambas rutas | Sí | MAPPER_DERIVED_CANDIDATE | Diseño legacy | Sí | C1 HOLD | C1.1 mostró que deduplicar ayuda levemente sin twin pero degrada twins same-head en ocho familias; legacy sigue activo, no validado como fórmula general |
| Exact source-form test | `Nearly`/end equality | afinidad por duración/release source | Sí | MAPPER_DERIVED_CANDIDATE | Diseño legacy | Sí | C | Tolerancia participa |
| Minimum local LN duration envelope | candidate builder | descarta menor que mínimo observado | Sí | MAPPER_DERIVED_CANDIDATE | Generalización manual | Sí | C/F | Mínimo no prueba relación |
| Map-relative vocabulary authority | candidate builder | conserva endpoints/duraciones originales | Sí | MAPPER_DERIVED_CANDIDATE | Hipótesis experimental | Refinar con relations | C | Estructuralmente validado |
| Standard snap divisor table | `BeatTimeline` | 1,2,3,4,6,8,12,16 en legacy | Sí | MAPPER_DERIVED_CANDIDATE | Tabla manual A/B | Sí | Future | No es default moderno |
| Lane gap pooling | `LocalLaneGapAnalyzer` | agrega todas las lanes/tipos | Sí | MAPPER_DERIVED_CANDIDATE | Diseño legacy | Sí | F | Pierde roles/transición |
| Lane gap search window | gap analyzer | ±4 beats | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | F | Scope futuro |
| Lane gap cutoff | gap analyzer | solo gaps `<=1 beat` | Sí | MAPPER_DERIVED_CANDIDATE | Default interno | Sí | F | Silencio vs spacing no resuelto |
| Lane gap rounding | gap analyzer | seis decimales | Sí, potencial | OPEN_DESIGN_DECISION | Precisión/equivalencia mezcladas | Sí | F | No reclasificar sin tests |
| Lane gap support gate | gap analyzer | count >=2 | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | F | Singleton requiere descripción rica |
| Smallest supported lane gap | gap analyzer | elige mínimo | Sí | MAPPER_DERIVED_CANDIDATE | Diseño legacy | Sí | F | No autoriza gaps mayores |
| Fixed lane gap fallback | options/gap | 0.125 beat | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | F | Backoff→SKIP futuro |
| Same gap before and after LN | geometry call | una separación simétrica | Sí | MAPPER_DERIVED_CANDIDATE | Diseño legacy | Sí | F | Tipar ambos lados |
| Interior minimum parent length | interior builder | 3 beats | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | G | Capacidad estructural futura |
| Interior minimum LN context | interior builder | 3 LNs | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | G | No equivale a diversidad |
| Interior minimum anchors | interior builder | 2 | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | G | Relations futuras |
| Interior relative/absolute priority | interior builder | 1.5× / 8 beats | Sí | MAPPER_DERIVED_CANDIDATE | Defaults manuales | Sí | G | Actualmente prioridad descriptiva moderna |
| Max interior opportunities | interior builder | 2 por parent | Sí | MAPPER_DERIVED_CANDIDATE | Cap conservador | Sí | G/J | Reemplazar por transformaciones distintas |
| Anchor sources | interior builder | heads y releases estrictamente internos | Sí | MAPPER_DERIVED_CANDIDATE | Diseño experimental | Refinar tipos | G | Release-only no es articulation head |
| Anchor ranking | interior builder | score, separación, centro, tiempo | Sí | MAPPER_DERIVED_CANDIDATE | Desempates manuales | Sí | G/I | Decisión no numérica |
| Parent-head context fallback | interior resolver | si anchor local insuficiente, usa head parent | Sí | MAPPER_DERIVED_CANDIDATE | Backoff legacy | Sí | F/G | Debe distinguir mismatch |
| Interior release containment | candidate path | start está dentro; end no queda universalmente limitado a parent | Sí | OPEN_DESIGN_DECISION | Contrato incompleto | Sí | G | Distinguir contained/crossing |
| Articulation saturation threshold | `NonHeldColumns <= 1` default | gate agregado | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | G | Causa por original holds futura |
| Articulation requires original head | resolver | release-only anchor se rechaza | Sí | CURRENT_POLICY_CONTRACT | Semántica de repress actual | Revisable solo con relación distinta | G2 | Coherente con input físico |
| Retrigger transition definition | analyzer | LN release→next object same-lane | Sí | MAPPER_DERIVED_CANDIDATE | Diseño experimental | Sí | C/F | Tipar next tap/LN |
| Retrigger gap cutoff | analyzer | positivo y <=1 beat | Sí | MAPPER_DERIVED_CANDIDATE | Default interno | Sí | F | No hay fallback numérico actual |
| Retrigger local window | analyzer | ±4 beats | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | F | Scope futuro |
| Retrigger support gate | analyzer | count >=2 | Sí | MAPPER_DERIVED_CANDIDATE | Default manual | Sí | C/F | Singleton no equivale a certeza |
| Retrigger lane backoff | analyzer | same-lane, luego cross-lane local | Sí | MAPPER_DERIVED_CANDIDATE | Diseño experimental | Sí | F | Roles de lane aún desconocidos |
| Retrigger alternative weight | articulation resolver | total evidence + exact releases + same-lane bonus | Sí | MAPPER_DERIVED_CANDIDATE | Diseño manual | Sí | C/I | Total agregado se aplica a cada gap |
| One articulation per parent | model/resolver | dos segmentos exactos | Sí | MAPPER_DERIVED_CANDIDATE | Cap conservador | Sí | H | Requiere ParentArticulationPlan |
| Articulation weighted tie-break | resolver | ruleta de candidates | Sí | MAPPER_DERIVED_CANDIDATE | Diseño legacy | Sí | H/I/J | Plan completo futuro |
| Articulation RNG derivation | `DeriveArticulationRandom` | sub-seed salt y fallback hash | Sí, reproducibilidad | CURRENT_POLICY_CONTRACT | Contrato pass 1 | Versionar si cambia | A/J | Phase A no lo toca |
| RNG salt/hash constants | RNG | FNV-like multiplier/salts | No si stream contract fijo; sí entre versiones | IMPLEMENTATION_ONLY | Reproducibilidad | No dentro de policy version | A | Cambiar rompe snapshots |
| Binary search implementation | analysis/geometry | lower-bound ordenado | No | IMPLEMENTATION_ONLY | Rendimiento | No | A | Resultado exacto equivalente |
| Stopwatch use | engine | timers no decisionales | No | IMPLEMENTATION_ONLY | Diagnóstico | No | A | Excluidos de equality |
| Collection initial capacities | geometry/candidates | preallocation | No | IMPLEMENTATION_ONLY | Rendimiento | No | A | No cambia orden |
| CSV 0 for zero-denominator ratios | stats | retorna 0 | No en generación; sí en interpretación | OPEN_DESIGN_DECISION | Presentación legacy | Sí | A | Evidencia futura debe mostrar N/A |
| Candidate semantic identity | generación legacy | sigue sin existir identidad semántica futura | Sí | OPEN_DESIGN_DECISION | Diseño faltante | Sí | D0/J | Phase B no la resuelve |
| Diagnostic candidate identity | `DiagnosticCandidateKey` | key policy-local por oportunidad, kind, orden y tiempos | No en generación | IMPLEMENTATION_ONLY | Phase B shadow | No; es provisional | B | No cambia equality/sort/merge legacy |
| Diagnostic detail filter | CLI/Web export | `summary`, `relevant` o `all`; sin sampling | No | IMPLEMENTATION_ONLY | Operación/export | No | B | Relevant = placed + interior + no-shape; summary siempre global |
| Diagnostic version | `DecisionDiagnosticVersions` | `phase-c1-2-shadow.1` separada de profile/policy | No | IMPLEMENTATION_ONLY | Reproducibilidad | No | C1.2 | Phase B cerró en `phase-b.1`; C1 añadió weights/provenance y C1.2 observed values/relations |
| C1 shadow witness aggregation | candidate diagnostics/research | calcula una contribución por OriginalObservationId sin gobernar selection | No mientras permanezca shadow | IMPLEMENTATION_ONLY | Investigación C1 | No activa | C1 HOLD | C1.1 Outcome B: independent authority y agreement son dimensiones separadas; unique aggregation no se activa |
| Exact-head relation representation | research shadow | representa H→R con witness IDs y claims, sin score | No mientras permanezca shadow | IMPLEMENTATION_ONLY | C1.2 | No activa | C1.2 COMPLETE/A | Explica 3.771/3.771 twins y 3.103/3.103 worsened C1.1; no asigna autoridad |
| Exact chord-completion relation representation | D0 research shadow | reduced exact head state → completion lane/type con whole-group holdout | No mientras permanezca shadow | IMPLEMENTATION_ONLY | D0 | No activa | D0 COMPLETE/A | 37.080/40.360 targets soportados; 36.387 permanecen entre completions competidoras; held-before separado |
| Exact completion context views | D0.1 research shadow | ocho vistas exactas y paralelas combinan reduced state con held/vecinos/transiciones inmediatas | No mientras permanezca shadow | IMPLEMENTATION_ONLY | D0.1 | No activa | D0.1 COMPLETE/A | Resuelve miles de competencias con frontera explícita cobertura/resolución; sin ladder, score, authority ni generation |
| Exact context agreement and joint witness | D0.2 research shadow | conserva view/completion/donor identity, coverage, conflict y dos conjunction checks exactos | No mientras permanezca shadow | IMPLEMENTATION_ONLY | D0.2 | No activa | D0.2 COMPLETE/A | 22 coverage signatures, 3.666 conflicts; marginal support queda separado de observed joint context con provenance |
| Comparable-context evidence state | F1 research shadow | clasifica cada candidate como support, mismatch, no-context o ambiguous conservando hechos por vista y provenance | No mientras permanezca shadow | IMPLEMENTATION_ONLY | F1 | No activa | F1 COMPLETE/A | 249.202 resoluciones; cuatro estados explícitos, dependency no-vote y marginal separado de joint; no selecciona candidate |
| Exact typed-gap/backoff representation | F2 research shadow | tipa cuatro transitions, conserva gap `decimal` exacto y sólo consulta chart-global tras `NoComparableContext` local | No mientras permanezca shadow | IMPLEMENTATION_ONLY | F2 | No activa | F2 COMPLETE/B | 2/50.762 supports locales, 0 globales; mismatch/ambiguity abstienen; no gobierna geometry, RNG ni generation |
| Exact timing traversal and endpoint holdout | F2.1 research shadow | conserva FileExact y secuencia exacta de redlines; excluye releases compartidos como un endpoint event | No mientras permanezca shadow | IMPLEMENTATION_ONLY | F2.1 | No activa | F2.1 COMPLETE/C | La traversal es provenance, no equivalencia musical: false-split en discretización y redline redundante; no quantizer ni generation |
| Comparable local context | ventanas y conteos | equivalencia temporal aproximada | Sí | OPEN_DESIGN_DECISION | Diseño faltante | Sí | E/F | Requiere mismatch/no-context |
| Generalization policy | implícita por pooling/fallback | no está versionada como tal | Sí | OPEN_DESIGN_DECISION | Diseño faltante | Sí | F/I | Debe quedar en certificate |
| Evidence confidence | no existe | no se muestra porcentaje | Sí | OPEN_DESIGN_DECISION | Investigación | Sí | I | Phase A no inventa fórmula |
| Global ADD budget unit | no existe | Bernoulli actual | Sí | OPEN_DESIGN_DECISION | Hipótesis futura | Sí | J | Transformación/objeto/interacción |
| Shared pass budget | no existe | pass 1 y articulation separados | Sí | OPEN_DESIGN_DECISION | Incompatibilidad conocida | Sí | J | No resolver silenciosamente |
| Automatic section algorithm | no existe | contexto por ventana fija | Sí | OPEN_DESIGN_DECISION | Investigación | Sí | E | Recurrence/change-point shadow |
| Lane role equivalence | no existe | pooling/uniformidad implícitos | Sí | OPEN_DESIGN_DECISION | Investigación | Sí | D/F | No imponer manos externas |

## Resumen

El inventario contiene **93 decisiones**: 3 `USER_INTENT`, 6 `HARD_VALIDITY_INVARIANT`, 9 `CURRENT_POLICY_CONTRACT`, 49 `MAPPER_DERIVED_CANDIDATE`, 15 `IMPLEMENTATION_ONLY` y 11 `OPEN_DESIGN_DECISION`. No hay `FUTURE_POLICY_CONTRACT` activo todavía. C1.2 representa la señal exact-head que C1.1 aisló; D0 representa completion exacta chart-local; D0.1 demuestra poder discriminante de contexto exacto; D0.2 separa agreement de views, overlap donor y observed joint context; F1 formaliza el estado candidate-centric; F2 prueba identidad exacta de gaps/backoff; F2.1 demuestra que FileExact/segment traversal no recuperan equivalencia nominal perdida sin quantization inference. F2 permanece Outcome B y F2.1 cierra Outcome C, ambos shadow-only. Ninguna fase gobierna el selector; la sustitución conductual continúa en HOLD.

Se debe actualizar al introducir cada policy version. La reclasificación distingue validez inderrotable de semántica conductual revisable; no modifica generación. Para la futura policy mapper-derived por defecto, el criterio final es `Active manually sourced style decisions = 0`. Cualquier `OPEN_DESIGN_DECISION` estilística debe resolverse o quedar inactiva, y `IMPLEMENTATION_ONLY` requiere evidencia de neutralidad estilística.
