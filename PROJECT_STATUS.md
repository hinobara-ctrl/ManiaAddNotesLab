# Project Status

Última actualización: 2026-09-08. Este documento representa únicamente el estado actual y debe sobrescribirse al cerrar cada fase.

## Resumen

ManiaAddNotesLab funciona como laboratorio CLI/Web para generar variantes `.osu` deterministas, ejecutar lotes y estudiar decisiones del algoritmo. La política conductual activa es `legacy-experimental.1`; el perfil de evidencia es `phase-a.1` y el schema diagnóstico actual es `phase-c1-2-shadow.1`. D0–D1.0 y las ramas F/E permanecen separados de generation con schemas research propios.

| Fase | Estado | Efecto sobre generación |
|---|---|---|
| A — Evidence Infrastructure | **COMPLETE** | Ninguno; construye un perfil original-only. |
| B — Certificates and Failures | **COMPLETE** | Ninguno; explica candidatos, blockers y rechazos en shadow opt-in. |
| C1 — LN Witness Authority | **HOLD — HYPOTHESIS REFRAMED** | Ninguno; sólo compara un peso alternativo en shadow/research. |
| C1.1 — Witness vs Agreement Validation | **COMPLETE — OUTCOME B** | Ninguno; demuestra que el acuerdo same-head es una señal separada. |
| C1.2 — Exact-Head Relation Modeling | **COMPLETE — OUTCOME A** | Ninguno; la relación H→R explica agreement C1.1. |
| C2 — Retrigger-Specific Frequency | **DEFERRED** | Ninguno; hipótesis separada no iniciada. |
| D0 — ChordCompletion Reconstruction | **COMPLETE — OUTCOME A** | Ninguno; reconstruye relations exactas en shadow. |
| D0.1 — Exact Completion Competition Context | **COMPLETE — OUTCOME A** | Ninguno; contexto exacto discrimina alternatives con una frontera de cobertura. |
| D0.2 — Exact Context Coverage and View Agreement | **COMPLETE — OUTCOME A** | Ninguno; separa agreement, donor overlap y joint witness exacto. |
| E — Adaptive Context Prototypes / Shadow | **COMPLETE — OUTCOME B** | Ninguno; recurrence y boundaries son research-only y no gobiernan contexto. |
| E.1 — Exact Recurrence Failure Stratification / Shadow | **COMPLETE — OUTCOME B** | Ninguno; explica exact absence, multiplicity y refinements sin cambiar el resolver. |
| D1.0 — Resulting-State Composition Feasibility / Shadow | **COMPLETE — OUTCOME A / NOT_AUTHORIZED** | Ninguno; distingue joint witness de soporte marginal. |
| D1.GATE — Resulting-State Behavioral Experiment Gate / Shadow | **NEXT / NOT_AUTHORIZED** | Ninguno; sólo pre-registra un eventual A/B y su rollback. |
| D1 — ChordCompletion Resulting-State A/B | **FUTURE / NOT_AUTHORIZED** | Sí cuando se autorice: fase conductual versionada separada de D1.0. |
| F1 — Comparable-context Resolver / Shadow + Validation | **COMPLETE — OUTCOME A** | Ninguno; formaliza support, mismatch, ausencia y ambiguity por candidate. |
| F2 — Typed gaps and evidence backoff A/B | **COMPLETE — OUTCOME B — SHADOW ONLY** | Ninguno; identity/backoff exactos válidos, coverage insuficiente para A/B. |
| F2.1 — Exact Gap Timing Identity / Shadow Validation | **COMPLETE — OUTCOME C — SHADOW ONLY** | Ninguno; demuestra que equivalencia nominal general requiere inferencia ausente del archivo. |
| F2.2 — Quantization Inference Feasibility / Research Design | **COMPLETE — OUTCOME B — RESEARCH/SHADOW ONLY** | Ninguno; formaliza compatibility condicional y demuestra el problema del domain. |
| F2.3 — Quantization Hypothesis Domain and Labeled Ground Truth Acquisition / Research Design | **COMPLETE — OUTCOME B — CONTINUE CONDITIONALLY** | Ninguno; formaliza domain/truth y localiza el recurso externo faltante. |
| F2.ACQ — Controlled Pre-Serialization Ground Truth Acquisition Prerequisite | **BLOCKED / CONDITIONAL ON EXTERNAL DATA** | Ninguno; no es accionable hasta que exista el package mapper-authored autorizado. |

## Qué funciona hoy

- Parser/writer independiente para osu!mania 1K–18K, timing con BPM variable y salida reparseable.
- CLI para una ejecución y Web para lotes masivos de chances/seeds.
- Generación determinista por input, opciones y seed; el source nunca se sobrescribe.
- Taps añadidos en timestamps originales y LNs construidas con duraciones/releases observados.
- Geometría por lane con no-overlap y separación temporal.
- Densidad vertical por heads simultáneos, normalización relativa por keymode y protección contextual de bursts.
- Gap local, vocabulario temporal relativo al mapa, oportunidades interiores y articulación disponibles como políticas experimentales.
- Trace, CSV, métricas, perfil de evidencia y diagnostics exportables.

## Qué está implementado en shadow mode

`MapperEvidenceProfile` congela observaciones, relaciones y provenance exclusivamente desde `OriginalObjects`. Phase B añade certificados, evaluación de HardValidity, blockers originales/sintéticos y causas de fallo. C1 calcula, sólo para investigación, un peso alternativo que cuenta una contribución por `OriginalObservationId` y conserva las labels `Duration`/`ExactRelease` como explicación.

Con diagnostics desactivados, C1 no materializa su mapa de witnesses. C1.2 añade un modelo research-only de grupos simultáneos y relaciones exact-head, y extiende certificates con values/relations descriptivas. Ningún score, certificate, relation ni peso C1 decide actualmente oportunidades, candidatos, lanes, RNG o articulación.

D0 reutiliza `SimultaneousOriginalEventGroup` y representa `ReducedState → CompletionMember` mediante lane y head type exactos. `HeldBeforeHeadState` conserva tails activas como contexto separado: nunca se convierten en heads. Whole-group holdout excluye todo el target de sus donors. Estas relations tampoco gobiernan generation.

D0.1 añade ocho vistas paralelas de contexto exacto: held-before, previous/next inmediato y sus gaps exactos, además de combinaciones bidireccionales. No son un ladder. Target y donor usan saneamiento simétrico para que una LN del grupo retirado no reaparezca como held futura. `ExactCompletionContextResearch` enumera alternatives y outcomes; no selecciona ninguna.

D0.2 conserva por separado identidad de vista, completion exacta y grupos donor. Clasifica coverage, completion-set relations, donor overlap, conflictos unique y estabilidad de refinamientos. `ReducedPrevNext` y `ReducedHeldPrevNext` funcionan como pruebas de contexto conjunto observado: una intersección marginal nunca se rebautiza como joint evidence.

F1 resuelve cada completion candidate como `ObservedSupport`, `LocalMismatch`, `NoComparableContext` o `AmbiguousEvidence`. Conserva support/mismatch view-local, abstenciones, dependency edges, donors y dos assessments marginal-vs-joint. Ningún estado, número de vistas o donor count gobierna generación.

F2 shadow tipa spacing same-lane como TapHead/LongNoteRelease → TapHead/LongNoteHead con gap `decimal` exacto. Local significa misma lane y transition kind; sólo no-context consulta Global chart-local. El gate conductual falló y no existe policy F2 productiva ni opt-in.

F2.2 añade un forward serializer research-only y domains finitos obligatoriamente explícitos. Cada resultado conserva assumption certificate y cardinalidad `UnsupportedUnderModel`, `UniqueUnderModel` o `AmbiguousUnderModel`; nunca selecciona un snap. El hardening previo reemplazó el leakage literal F2.1 por violations medidas con provenance.

F2.3 endurece domains con content hash, identidad semántica por coordinate y justification obligatoria. Añade truth completa source→destination con LN head/release separados, taxonomías de source/circularity y un evaluator descriptivo. El pilot es exclusivamente sintético; no existen human latent labels.

### Resultado D1.0

D1.0 evaluó 42.048 pair holdouts exhaustivos en los mismos 11 charts/familias 4K–7K–10K. En `ReducedOnly`, 37.112 targets tuvieron el par exacto observado conjuntamente; 2.913 tuvieron ambos members soportados marginalmente pero nunca juntos en una occurrence donor válida. Entre 1.283.958 pares hipotéticos estructuralmente elegibles, 374.520 fueron marginal-only. La diferencia aparece en 10/11 familias y el joint support en todas.

Whole-group, target-observation, future-held, synthetic, cross-chart, held-tail-as-head y cross-occurrence leakage midieron cero. Dos controles positivos inválidos dispararon como estaba previsto, el orden canónico tuvo cero violaciones y una repetición completa produjo artifacts byte-identical. Es **OUTCOME A de factibilidad representacional**, no una recomendación de aceptar o rechazar candidatos.

## Qué afecta realmente la generación

La policy legacy sigue tomando decisiones mediante Bernoulli por oportunidad, factores manuales de chord/contexto, weights LN por distancia y afinidad, gap local con fallback, selección ponderada de forma y lane legal uniforme. Los toggles experimentales documentados sí pueden alterar el resultado cuando se activan o desactivan.

Los defaults modernos activos incluyen densidad por heads, escalas relativas, densidad contextual, gap local y timing relativo al mapa. Las oportunidades LN interiores y la articulación están implementadas pero permanecen desactivadas por defecto. Phase A, Phase B y C1 no reemplazan estas reglas.

## Por qué C1 sigue en HOLD después de C1.1

Phase B demostró que `Duration` y `ExactRelease` pueden ser dos evidence paths del mismo objeto original. C1 obtuvo una factorización algebraicamente limpia: sumar una vez el `DistanceWeight` por `ObservationId` y aplicar después la afinidad existente.

El corpus C1.1 contiene 11 charts/familias humanas únicas, ocho con LNs, y 12.554 targets en 4K/7K/10K. Las 14.828 convergencias observadas fueron `SameHeadAgreement`; ninguna fue coincidencia de materialización, tolerancia u otra causa. Al retirar todo el head-group exacto desaparecieron tanto los acuerdos como cualquier diferencia entre weights.

En LOO-object, UniqueWitness mejoró levemente los targets sin twin exacto, pero empeoró 3.103 targets con twin estructural en ocho familias. Esto demuestra que una observación sigue siendo un testigo independiente, pero sus labels concordantes contienen una señal estructural distinta. Resultado C1.1: **OUTCOME B**. La deduplicación conductual original queda rechazada/reformulada y C1 permanece en **HOLD** hasta modelar ambas dimensiones sin magic weights.

## Resultado C1.2

El corpus permaneció idéntico: 11 charts/familias, ocho con LN y 12.554 targets. C1.2 representó 7.206 exact-head groups y 10.426 relaciones `H→R`: 1.643 relaciones tienen witnesses repetidos y 2.731 grupos contienen endpoints competidores. En held-out, 9.073 targets tienen contexto same-head y 3.771 endpoints quedan soportados; esto incluye **3.771/3.771 structural twins y 3.103/3.103 casos worsened-by-UniqueWitness** de C1.1.

Outcome A confirma capacidad explicativa, no autoridad conductual. Witness identity permanece deduplicada y claims/relations se preservan sin weight, bonus, confidence ni `MapperSupport`.

## Resultado D0

En 11 familias/11 charts humanos 4K–7K–10K, D0 encontró 14.463 chord groups y ejecutó 40.360 reconstrucciones leave-one-head-group-out. Hubo exact reduced-state comparable en 39.596 trials y el target lane+type quedó soportado en 37.080. La relación es recurrente en las 11 familias, por lo que D0 cierra **OUTCOME A**.

Sólo 693 successes fueron unique; 36.387 dejaron el target entre completions competidoras. Esto valida la representación exacta, no una probability ni un selector. Tap/LN heads permanecen distintos, held tails tratados como heads = 0 y ninguna evidence cruza charts.

## Resultado D0.1

En la población primaria de 36.387 competencias, `ReducedPrevious` resolvió 6.303 targets y `ReducedNext` 6.264. Los gaps exactos resolvieron 6.075/5.921; el contexto bidireccional resolvió 5.292, pero perdió comparabilidad en 29.166. La señal aparece en las 11 familias y no depende de un único chart.

D0.1 cierra **OUTCOME A** porque existe poder discriminante exacto, recurrente y libre de leakage. La cobertura cae al aumentar especificidad: por eso el resultado no elige una vista ni autoriza backoff, score, authority, D1 o cambios de generación.

## Resultado D0.2

Sobre los mismos 40.360 trials y 36.387 casos primarios, D0.2 observó 22 coverage signatures, 95.746 completion sets pairwise iguales, 311.037 con subset/overlap y 17.228 disjoint. A nivel target hubo 10.386 acuerdos unique en el target, 3.256 acuerdos unique en el mismo wrong y 3.666 conflictos; 2.482 conflictos mezclan target y wrong.

Previous/Next aportó 8.245 completion references confirmadas por la vista conjunta, pero también 4.972 acuerdos sin joint comparable y 1.506 sin soporte de esa completion en el joint. Held/PrevNext confirmó 7.603 y preservó 249 excepciones. Ambos patrones aparecen en las 11 familias. D0.2 cierra **OUTCOME A** porque agreement, conflicto, donor overlap y joint witness son reconstruibles con provenance; no porque exista una selection policy.

## Resultado F1

Sobre 249.202 candidate resolutions y los mismos 40.360 targets, F1 clasificó el target en 17.942 `ObservedSupport` (44,45%), 4.021 `LocalMismatch` (9,96%), 3.266 `NoComparableContext` (8,09%) y 15.131 `AmbiguousEvidence` (37,49%). Support y ambiguity aparecen en las 11 familias; las 22 coverage signatures siguen visibles.

F1 preserva por separado support/contradiction, candidates no-target, conflictos y joint occurrence. Target-group leakage, future-held leakage, held-tail-as-head y los nueve nesting audits terminaron en cero. Por ello cierra **OUTCOME A** como lenguaje research-only auditable, no como selector ni backoff autorizado.

## Resultado F2

F2 shadow reconstruyó 50.762 transitions y 1.809 gaps exactos. Local produjo 2 support, 4.051 mismatch, 18 no-context y 46.691 ambiguous. Los 18 backoffs terminaron en 13 global mismatch y 5 global ambiguous; global support fue cero. Whole-group leakage, synthetic teaching y held-tail-as-head fueron cero.

La representación cierra **OUTCOME B**: es exacta y auditable, pero una policy admitiría sólo 2/50.762 candidates y omitiría el resto. No se implementó A/B, no se creó versión F2 conductual y legacy continúa default.

## Resultado F2.1

F2 se reprodujo exactamente sobre el snapshot histórico de 11 charts. F2.1 separó FileExact de una candidate `ExactSegmentTraversal` y construyó ground truth sintético con BPM integral/no integral, crossings, LNs y controles cercanos. FileExact produjo un false split cuando 1/2 beat nominal se serializó 250/249 ms; la traversal produjo ese split y otro al cruzar una redline redundante con el mismo BPM. No hubo false merges en las identities exactas, mientras `ExactMilliseconds` fue descartada porque fusionaría relaciones beat distintas con igual duración ms.

El audit endpoint-aware demostró sintéticamente que dos LNs con release compartido requieren excluir el `ReleaseEventGroup`: el estado cambió de global mismatch a no-context al retirar el endpoint duplicado. Ninguna identity nueva pasó el synthetic gate general, por lo que el corpus humano F2.1 no se ejecutó para buscar una justificación estadística. F2.1 cierra **OUTCOME C**, sin quantizer, behavior ni A/B.

## Resultado F2.2

El contador F2.1 `TargetEndpointLeakageCount` era un cero literal. Ahora deriva de una lista de violations auditadas; un donor deliberadamente sin filtrar produce leakage 1 y el holdout endpoint-aware correcto produce 0. El schema de código F2.1 sube a `.2` sin reescribir su report histórico.

El forward model confirma el redondeo integer-ms away-from-zero y demuestra que `0,5 beat` y `250/499 beat` producen el mismo timestamp 250 ms bajo beat length 499. Un domain estrecho devuelve unique; al añadir la segunda historia, el mismo observable queda ambiguous. Por tanto la inferencia es formalizable sólo bajo un domain externo visible, no recuperable como observation ni justificado actualmente por el mapper.

F2.2 cierra **OUTCOME B**. El corpus humano no se ejecutó: sin latent labels no puede medir accuracy ni autorizar denominadores. No existe quantizer ni cambio conductual.

## Resultado F2.3

El mismo domain en distinto orden produce igual hash; cambiar candidates bajo el mismo ID cambia el hash; aliases de una misma coordinate se deduplican sin fake ambiguity. Justification vacía se rechaza. El evaluator de transition truth reproduce `UniqueCorrect`, `UniqueIncorrect`, ambas ramas ambiguous y unsupported sobre source+destination completos.

Las fuentes auditadas separan external convention, synthetic truth, editor metadata, mapper annotation, file/chart derivation, manual y unknown. Derivar denominators de notas es circular. El `.osu` histórico sigue siendo Level 0 sin label. La mejor ruta independiente requiere capturar coordinates antes del export con participación/autorización de un mapper; ese recurso no está disponible ahora.

F2.3 cierra **OUTCOME B** y la rama queda **CONTINUE CONDITIONALLY**. `F2.ACQ` es un prerequisite recomendado únicamente si puede obtenerse el package pre-export y congelarse DomainDesignSet/IndependentValidationSet. No se abrió F2.4 ni behavior.

## Resultado E

Phase E cerró **OUTCOME B** con dos resultados internos separados. Recurrence es **B**: 10.980 relations exactas, 10.764 no contiguas y 4.143 reconstrucciones held-out demuestran señal en las 11 familias, pero 1.451 mismatch y 16.224 no-context impiden promoción. Segmentation es **B**: las boundaries son estables bajo 187 holdouts por configuración, pero cambian de 288 a 89 al variar `min-run-2`→`min-run-3`.

`AdaptiveContextResearch` permanece original-only, chart-local y desconectado de generation/RNG/F2/D1. No existe Section productiva, selector de contexto ni fuzzy fallback. El report completo y los artifacts públicos preservan provenance y configuraciones por separado.

## Resultado E.1

E.1 cerró **OUTCOME B**. De 16.224 NoContext, 16.145 (99,51%) son ausencia literal del par exacto en otra ubicación y sólo 79 provienen de occurrences eliminadas por hygiene. Los 1.451 mismatch muestran spacing divergence en 71,19%/75,81% (ms/beat file-derived), pero también aparece en 61,74%/69,39% de Reconstructed. El refinement spacing convierte 1.065 mismatch y 1.716 reconstructed a NoContext; no convierte ningún mismatch a support. Held divergence fue cero.

La rama exact-recurrence queda **PARKED**: la diagnosis es útil, pero añadir exact features sólo filtra evidence y destruye coverage. No se añadió fuzzy similarity, vote, score, probability, resolver nuevo ni behavior.

## Validación actual

- `dotnet restore`: PASS.
- `dotnet build -c Release`: PASS, 0 errores.
- `dotnet test -c Release`: **550 passed, 0 failed, 0 skipped** con D1.0; el hardening pre-D1.0 cerró con 518.
- Cinco fixtures conductuales permanecen byte a byte iguales a Phase B.
- Spring ADD 50 seed 100 conserva el hash histórico documentado.

La consulta de vulnerabilidades de NuGet puede emitir `NU1900` cuando `api.nuget.org` no está accesible; no afectó la compilación ni las pruebas de este cierre.

## Riesgos y problemas abiertos

- El corpus tiene 11 familias, pero sólo ocho contienen LNs; 10K no aporta targets LN y no existen charts humanos 1K/18K en esta muestra.
- `WitnessAgreement` es descriptivo: todavía no existe una regla justificada para convertirlo en autoridad, bonus o score.
- F2.3 identifica una ruta posible de ground truth pre-export, pero depende de un package mapper-authored aún no disponible; sin él la rama debe permanecer condicional o aparcarse.
- Muchas decisiones estilísticas siguen siendo thresholds, ventanas, caps, pooling o desempates legacy.
- La elección uniforme de lane y la composición del estado vertical aún no se derivan del mapa.
- `LocalMismatch`, contextos comparables, secciones adaptativas y `CompatibleComposition` no gobiernan generación.
- Interiores y articulación necesitan playtesting multi-chart; “interior” no implica universalmente release contenido.
- Diagnostics detallados pueden producir JSON muy grandes; deben seguir siendo opt-in y filtrados.
- El writer no preserva exactamente comentarios intercalados, encoding ni estilo de newline del source.

## Próximo paso recomendado

`F2.ACQ` permanece **BLOCKED ON EXTERNAL DATA** y F2 sigue `CONTINUE_CONDITIONALLY`. D1.0 cerró Outcome A sin conducta. Se recomienda **D1.GATE — Resulting-State Behavioral Experiment Gate / Shadow** como `NEXT / NOT_AUTHORIZED`: sólo puede pre-registrar semántica, métricas, rollback y stopping criteria de un eventual A/B. La fase histórica **D1 — ChordCompletion Resulting-State A/B** permanece `FUTURE / NOT_AUTHORIZED` y requiere autorización separada. C2 permanece **DEFERRED** y MapperSupport no está autorizado.

<!-- PROJECT-STATE:BEGIN -->
Current phase: D1.0 — COMPLETE — OUTCOME A<br>
Next actionable research candidate: D1.GATE — Resulting-State Behavioral Experiment Gate / Shadow<br>
Next actionable authorization: NOT_AUTHORIZED<br>
Next behavioral phase: D1 — ChordCompletion Resulting-State A/B<br>
Next behavioral authorization: NOT_AUTHORIZED<br>
Blocked prerequisite: F2.ACQ — BLOCKED<br>
Research branch: F2 — CONTINUE_CONDITIONALLY<br>
Behavior policy: `legacy-experimental.1`<br>
Behavior change: none<br>
Evidence profile: `phase-a.1`<br>
Diagnostic schema: `phase-c1-2-shadow.1`<br>
Tests: 550 passed / 0 failed / 0 skipped
<!-- PROJECT-STATE:END -->

Detalles y evidencia: [D1.0](docs/PHASE_D1_0_RESULTING_STATE_COMPOSITION_FEASIBILITY_REPORT.md) describe el cierre actual; [E.1](docs/PHASE_E_1_EXACT_RECURRENCE_FAILURE_STRATIFICATION_REPORT.md), [E](docs/PHASE_E_ADAPTIVE_CONTEXT_PROTOTYPES_REPORT.md) y los reports anteriores permanecen como evidencia histórica.
