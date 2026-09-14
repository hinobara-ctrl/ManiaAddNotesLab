# ASTRA — Post-SAFETY.PROV strategic review

**OPINIÓN / PROPUESTA PARA REVISIÓN HUMANA. NO ES ESTADO CANÓNICO NI AUTORIZACIÓN.**

Fecha: 2026-09-09. Repositorio revisado: [hinobara-ctrl/ManiaAddNotesLab](https://github.com/hinobara-ctrl/ManiaAddNotesLab). Snapshot: `b926d117c88495e2daf40b37b2a814cd37e35c5f`, rama `main`, árbol inicialmente limpio. La consulta directa de refs confirmó el mismo hash para `HEAD` y `refs/heads/main` remotos. La página web indexada mostraba un estado anterior; no se utilizó como autoridad del cierre actual.

Esta revisión compara la visión inicial, mi propuesta anterior, el roadmap desarrollado, los reports de resultados y las rutas relevantes del código. No es una certificación línea por línea de todo el repositorio. No ejecuté nuevos experimentos, tests, benchmarks o playtests; no inspeccioné los outputs D1 retenidos ni reconstruí métricas conductuales retiradas. Los resultados numéricos citados pertenecen a los reports existentes. No cambié algoritmos, defaults, contratos históricos, documentos maestros ni estado canónico. No hubo commit ni push.

## Executive summary

**Conservaría la visión mapper-derived, pero abandonaría la expectativa de una eliminación lineal de heurísticas que desemboca necesariamente en un selector universal.** El proyecto ha avanzado mucho en describir qué conoce y qué no puede afirmar. Todavía no ha demostrado que esa descripción mejore sistemáticamente las variantes jugables frente a legacy.

Mi valoración principal:

1. **La dirección científica no se ha perdido.** Original-only, procedencia, relaciones conjuntas, abstención, separación de geometría y estilo, y una UI simple siguen siendo objetivos coherentes.
2. **La distancia hasta el producto cambió.** Se pasó de sustituir números por observaciones a estudiar identidad, compatibilidad y atribución. Era necesario en varios puntos, pero continuar indefinidamente ampliando infraestructura tendría un rendimiento decreciente.
3. **Mi propuesta anterior fue demasiado optimista sobre las primeras sustituciones.** C1 mostró que deduplicar identidad no autoriza deduplicar toda señal predictiva. F2 mostró tanto un límite de identidad temporal como un problema de semántica de admisión. E mostró que añadir contexto exacto puede eliminar casos útiles sin resolver los incorrectos.
4. **D1 no refutó resulting-state.** El ensayo quedó inválido por seguridad no atribuible y un defecto de identidad de baseline. Tampoco demostró que el treatment funcione. No usaría sus outputs para rescatar una conclusión.
5. **SAFETY.PROV es un activo suficiente para detener esta línea de infraestructura por ahora.** Demuestra viabilidad técnica bajo el alcance certificado; no convierte el runner D1 en un A/B causal validado ni aporta evidencia de naturalidad.
6. **Propondría G1.0 de factibilidad en sombra antes de cualquier G1/G2 conductual.** Reutilizaría primitivas existentes, estudiaría una única familia de relaciones interiores y fijaría una salida legítima de PARK. H, MapperSupport y budget no deberían seguir automáticamente.
7. **El siguiente recurso escaso es una evaluación independiente orientada al uso.** C11 es valioso como conjunto de desarrollo/regresión, pero su reutilización no lo convierte en validación externa. El corpus comunitario debe tener identidad, exposición y particiones propias.

La secuencia sugerida está en [ASTRA_ROADMAP_V2_PROPOSAL.md](ASTRA_ROADMAP_V2_PROPOSAL.md). Es una alternativa para discutir, no un reemplazo aprobado de [ROADMAP.md](../ROADMAP.md).

## 1. Lectura del estado actual

### 1.1 Lo que realmente opera

La policy normal sigue siendo `legacy-experimental.1`. Aporta un generador funcional y determinista, con heurísticas de probabilidad, densidad, gap y pesos LN. El perfil está en `phase-a.1` y diagnostics en `phase-c1-2-shadow.1`. Interiores y articulación están implementados, pero permanecen inactivos por defecto. Sus modos experimentales no equivalen a una promoción mapper-derived. [Estado](../PROJECT_STATUS.md), [opciones y modelos](../src/ManiaAddNotesLab.Core/Model.cs), [motor](../src/ManiaAddNotesLab.Core/AddNotesEngine.cs).

La capa nueva representa y audita bastante más de lo que gobierna. Esto es una decisión válida de aislamiento; también significa que contar fases completadas sobreestima el progreso conductual si no se acompaña de esa distinción.

### 1.2 Qué significa el resultado de cada línea

| Línea | Resultado existente | Lectura estratégica |
|---|---|---|
| A/B | Perfil, certificates, blockers y diagnostics sin cambio conductual. | Base útil y reutilizable. No necesita otra reconstrucción general. |
| C1/C1.1/C1.2 | C1 HOLD/reformulada; C1.1 B; C1.2 A. | La identidad del witness y las relaciones concordantes son dimensiones distintas. No existe fórmula de peso aprobada. |
| D0/D0.1/D0.2 | A en representación, discriminación exacta y separación marginal/joint. | Hay relaciones reales y una frontera de cobertura; no una política de elección ya validada. |
| F1 | A descriptivo. | Lenguaje útil de evidencia por candidato; su semántica no debe copiarse entre problemas sin revisar la afirmación que representa. |
| F2/F2.1/F2.2/F2.3 | B/C/B/B; rama CONTINUE_CONDITIONALLY. | Hay un bloqueo de datos para identidad latente y preguntas de admisión que no desaparecen al resolverlo. |
| F2.ACQ | BLOCKED por paquete externo. | No es un siguiente trabajo de programación accionable mientras falte ese recurso. |
| E/E.1 | B/B, PARKED. | Recurrencia real, pero refinamientos exactos insuficientes y segmentación sensible. No justifica otro refinamiento por inercia. |
| D1.0/D1.GATE | A de factibilidad y READY de contrato. | Activos históricos; sus cierres no validan el ensayo posterior. |
| D1 | C/PARKED. | Ensayo inválido para interpretación conductual; hipótesis de utilidad abierta. |
| D1.SAFETY | C/PARKED. | Semántica de validez útil; intento forensic que atribuyó desde snapshots, inválido. |
| SAFETY.PROV | A, `behaviorChange=false`. | Captura real viable; no certifica cualquier runner, RNG o futuro planner. |
| C2/G1/G2/H/I/J/K | C2 DEFERRED; restantes futuras, sin autorización conductual. | No forman una cola automática de ejecución. |

Fuentes de los estados: [PROJECT_STATE.json](PROJECT_STATE.json), [roadmap técnico](MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md) y reports específicos citados abajo. La suite de 656/656 es el snapshot del cierre SAFETY.PROV; no una ejecución nueva de esta revisión.

## 2. Cuánto cambió la visión original

Comparé [el plan inicial](FUTURE_MAPPER_DERIVED_ALGORITHM_PLAN.md), [mi revisión previa](MAPPER_DERIVED_PROPOSALS_REVIEW.md), [el blueprint](MAPPER_DERIVED_INTEGRATION_BLUEPRINT.md) y [el roadmap técnico](MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md).

**Poco cambió el propósito; cambió mucho la hipótesis de cómo alcanzarlo.** El propósito sigue siendo aumentar interacción usando el chart como fuente de lenguaje. La implementación inicialmente imaginada —extraer distribuciones, reemplazar defaults y unificar soporte— suponía que los obstáculos serían principalmente de ingeniería. Los resultados muestran obstáculos de información y de evaluación.

No pondría un porcentaje de alejamiento: no existe una medida que lo sostenga. Sí distinguiría tres ejes:

- **Fidelidad a la fuente:** mejor que al inicio; menos posibilidades de llamar evidencia a una inferencia o de contar fuentes dependientes como confirmaciones independientes.
- **Simplicidad de desarrollo:** peor; aparecieron numerosas ramas, contratos y diagnósticos. Parte fue aprendizaje necesario y parte podría convertirse en mantenimiento sin nueva decisión.
- **Validación de utilidad final:** todavía limitada. La investigación ha aprendido a desconfiar justificadamente de métricas fáciles, pero aún necesita mostrar qué mejora concreta percibe una persona al usar el generador.

### 2.1 Qué conservaría del plan inicial

| Principio | Evaluación actual |
|---|---|
| OriginalObjects aporta el lenguaje durante la run. | Conservar. Validar con muchos charts no requiere que se donen estilo entre sí. |
| Sintéticos no enseñan estilo. | Conservar. Consultar el resultado acumulado no modifica el conjunto de evidencia. |
| Geometría y evidencia son responsabilidades distintas. | Conservar y aplicar la semántica real por operación, incluyendo serialización. |
| No esconder defaults como evidencia. | Conservar. También aplica a hipótesis, dominios, identidades y desempates. |
| NoEvidence→SKIP. | Conservar para una política estricta, con semántica concreta de lo que falta. |
| Una sola implementación entre keymodes. | Conservar; los resultados humanos deben estratificarse, no suponerse portables por tests sintéticos. |
| Chart + Add% + Seed + Range. | Conservar como objetivo de interacción con el usuario; no exige resolver primero todas las líneas de investigación. |
| Medir interacciones además de objetos. | Conservar; esas cantidades no son dificultad ni satisfacción. |

### 2.2 Qué reformularía, incluida mi propuesta anterior

| Supuesto inicial | Qué cambió | Reformulación propuesta |
|---|---|---|
| Frecuencia observada sustituye un peso manual de forma natural. | El conteo no determina por sí solo autoridad, scope ni relación con la query. | Frecuencia es una característica disponible, cuyo uso requiere una pregunta y una evaluación propias. |
| Deduplicar votos LN sería una primera mejora sencilla. | C1.1 localizó una señal same-head que se perdía al deduplicar conducta. | Deduplicar identidad y preservar claims; retirar la propuesta de reemplazo conductual ciego. |
| Soporte marginal de componentes respalda una composición. | D0.2/D1.0 muestran el límite directamente. | La composición necesita una afirmación conjunta definida; no basta sumar memberships. |
| Contexto más específico resuelve ambigüedad. | Puede producir unicidad incorrecta o destruir comparabilidad. | Publicar cobertura, alternativas y errores conjuntamente. |
| Eliminar fallbacks de gap es una sustitución relativamente fácil. | La identidad latente no es recuperable en general y el resolver F2 exige unanimidad. | Separar identidad, pertenencia al vocabulario, compatibilidad contextual y elección. |
| Timing relativo exacto conserva toda identidad musical relevante. | Lo exacto respecto del archivo no recupera coordenadas perdidas en la exportación. | Declarar facts del archivo, modelo asumido y desconocimiento por separado. |
| Capacidad geométrica permite eliminar caps de interiores/articulación. | Que una estructura quepa no demuestra que deba repetirse muchas veces. | Demostrar primero utilidad de una transformación; después estudiar capacidad y composición. |
| MapperSupport es el destino necesario de los certificates. | Los certificates pueden ser útiles sin reducirse a un escalar. | Un score global es opcional y debe resolver un problema demostrado. |
| ADD-budget es la culminación natural. | Añade un contrato nuevo, conflictos y competencia entre familias. | Tratarlo como decisión de producto independiente, no como requisito de pureza. |

## 3. Descubrimientos que considero decisivos

### 3.1 La propuesta de deduplicación necesitaba ser refutada de esta forma

C1.1 observó 14.828 convergencias, todas `SameHeadAgreement`, bajo la configuración evaluada. Los 3.103 empeoramientos de UniqueWitness se concentraron en targets con twin exacto. Al excluir todo el head-group desaparecieron los acuerdos y las diferencias de peso. C1.2 explicó los 3.771 twins y los 3.103 casos empeorados mediante relaciones exactas `H→R`. [C1.1](PHASE_C1_1_WITNESS_AGREEMENT_VALIDATION_REPORT.md), [C1.2](PHASE_C1_2_EXACT_HEAD_RELATION_MODELING_REPORT.md).

Mi conclusión anterior «un testigo, un voto» era correcta para identidad de muestra y demasiado fuerte como regla de selección. La suma legacy tampoco queda validada como ley general. **La limpieza conceptual de una fórmula no sustituye la información que esa fórmula estaba aprovechando.**

### 3.2 La necesidad de pensar en composición sobrevivió; su autoridad no está probada

D1.0 encontró 2.913 targets originales cuyos dos members tenían soporte marginal y no tenían soporte conjunto held-out en `ReducedOnly`. También encontró 374.520 pares hipotéticos marginal-only entre 1.283.958 elegibles. Es una demostración material de la distinción. [D1.0](PHASE_D1_0_RESULTING_STATE_COMPOSITION_FEASIBILITY_REPORT.md).

El primer número merece atención: algunos estados que efectivamente escribió el mapper tampoco se reconstruyen con esa exigencia. Por tanto, «sin joint witness» significa «no demostrado bajo este protocolo», no «musicalmente incorrecto». Una política estricta puede abstenerse ahí, pero debe reconocer el coste.

Además, D0/D1.0 reconstruyen un mapa al que se le retiraron miembros. ADD interviene un mapa que ya estaba completo. **Reconstrucción y ampliación son tareas distintas.** La evaluación held-out acredita relaciones y sirve para comparar modelos; no prueba por sí sola que el mapa completo necesite más notas.

### 3.3 La semántica de ambigüedad es ahora una decisión estratégica pendiente

El [resolver F2](../src/ManiaAddNotesLab.Core/TypedGapBackoffResearch.cs) clasifica `ObservedSupport` únicamente cuando `supportingDonors == comparableDonors`. Si el candidato aparece junto a otros gaps, clasifica `AmbiguousEvidence` y termina en SKIP. Local significa misma lane y tipo de transición a lo largo del chart, no una sección temporal validada. [Report F2](PHASE_F2_TYPED_GAPS_EVIDENCE_BACKOFF_REPORT.md).

Eso responde una pregunta fuerte: «¿todos los ejemplos de este scope muestran el mismo gap?». No responde simplemente «¿este gap tiene testigos?». Un mapa con más vocabulario puede producir más ambigüedad bajo esa definición aunque sus timestamps sean perfectamente identificables.

Ejemplo conceptual, no experimento: en un scope original aparecen gaps A y B. La existencia de B refuta la afirmación universal «este scope siempre usa A». No refuta la afirmación existencial «A está demostrado en este scope». Tampoco decide si usar A es apropiado en esta query.

Separaría cuatro preguntas antes de diseñar otra policy:

1. **Identidad:** ¿qué valor o relación estamos comparando?
2. **Pertenencia:** ¿existe un testigo válido de este candidato?
3. **Compatibilidad:** ¿ese testigo demuestra los requisitos contextuales de la transformación?
4. **Elección:** si quedan varios candidatos compatibles, ¿quién elige y con qué contrato?

D1.GATE acepta `JointUnique` y `JointAmongAlternatives`; F2 no permite esa pluralidad para gaps. No afirmo que uno esté equivocado ni que deban ser idénticos: son contratos diferentes. Sí considero que esa diferencia necesita una justificación expresa antes de expandir F1/F2 a G.

**Resolver F2.ACQ no garantiza arreglar la degeneración F2.** Los datos externos resolverían parte de identidad/validación latente; no decidirían si un vocabulario multimodal debe vetar todos sus candidatos. No cuantifiqué el peso relativo de ambas causas ni reinterpreto el Outcome B histórico.

Una revisión futura podría admitir soporte conjunto entre alternativas sin inventar probabilidades. Eso requeriría un contrato nuevo; no autoriza relajar mismatch, buscar un sí mediante backoff ni reutilizar donors incompatibles.

### 3.4 E enseñó a no confundir menos errores con mejor decisión

E.1 encontró 16.145 de 16.224 NoContext debidos a ausencia exacta de la pareja de vecinos en otra parte. Solo 79 provenían de retirar las occurrences aparentes por higiene. Añadir spacing exacto hizo que 1.065 Mismatch pasaran a NoContext, mientras 1.716 reconstrucciones correctas también perdían contexto. No hubo Mismatch→Reconstructed mediante ese filtro. [E.1](PHASE_E_1_EXACT_RECURRENCE_FAILURE_STRATIFICATION_REPORT.md).

Eso justifica aparcar esta familia de refinamientos. No demuestra que todo contexto adaptativo sea inútil ni que deba añadirse fuzzy matching. Demuestra que este camino exacto ha dado suficiente información para dejar de iterarlo por ahora.

El gate futuro también merece revisión: comparar el número absoluto de mismatches de un método que responde con un baseline que devuelve casi todo ambiguo penaliza la capacidad de responder. E reportó Global con 28.312/28.312 ambiguos; el baseline fijo produjo una reconstrucción y 28.307 ambiguos. La comparación histórica sigue siendo válida bajo su contrato; para otro estudio pediría una evaluación conjunta de aciertos, errores y abstención en una población común, sin escoger umbrales después de ver resultados. [E](PHASE_E_ADAPTIVE_CONTEXT_PROTOTYPES_REPORT.md).

### 3.5 La no-identificabilidad temporal es un límite real, no una deuda de ingeniería

F2.2 mostró dos coordenadas latentes distintas que producen el mismo timestamp visible bajo el mismo timing. F2.3 separó el dominio de hipótesis de la verdad etiquetada y documentó que el piloto es sintético. No existe inverso único general desde el `.osu`. [F2.2](PHASE_F2_2_QUANTIZATION_INFERENCE_FEASIBILITY_REPORT.md), [F2.3](PHASE_F2_3_QUANTIZATION_DOMAIN_GROUND_TRUTH_REPORT.md).

Mantendría dos posibilidades separadas: estudiar el archivo exacto y estudiar inferencia bajo assumptions explícitas. El objetivo original no justifica presentar la segunda como observación directa. Una colección adicional de `.osu` no recupera automáticamente el estado pre-export perdido.

### 3.6 La infraestructura causal corrigió una pregunta mal planteada

D1 contó intersecciones inclusivas del output completo; D1.SAFETY intentó atribuir condiciones ausentes del source sin provenance de la mutación. Ambos cerraron C, por razones de medición/contrato. D1 tenía además un HEAD inicial incorrecto en su certificado. Los counts públicos 200/171 no permiten concluir mejor o peor seguridad del treatment. [D1](PHASE_D1_RESULTING_STATE_AB_REPORT.md), [D1.SAFETY](PHASE_D1_SAFETY_ATTRIBUTABLE_GEOMETRY_REPORT.md).

La separación entre facts geométricos, violaciones bajo la regla real y atribución causal era necesaria. El aprendizaje adicional es de ejecución: una regla escrita no basta si el runner verifica el abort después de toda la matriz o si un agregado infiere una causa que el contrato prohíbe.

## 4. SAFETY.PROV: qué reutilizar y qué no suponer

El [report](PHASE_SAFETY_PROV_MUTATION_LEVEL_PROVENANCE_REPORT.md), el [recorder/comparator](../src/ManiaAddNotesLab.Core/GenerationProvenanceResearch.cs) y las [pruebas existentes](../tests/ManiaAddNotesLab.Tests/PhaseSafetyProvMutationProvenanceTests.cs) muestran captura durante el engine, separación DecisionEvent/MutationEvent, estados antes/después, posición RNG, intents y serialización. La certificación OFF/ON preservó bytes y decisiones en fixtures, incluyendo articulación y varios K.

**Lo daría por DONE en su alcance certificado.** No abriría SAFETY.PROV.1 ni una plataforma de seguridad universal como siguiente fase.

Pero preservaría estas limitaciones al reutilizarlo:

- La igualdad geométrica no basta para igualdad de estado generativo. RNG, cursor, intents y futuros estados de policy deben entrar en el contrato de una nueva integración.
- La certificación usó RNGs posicionados; `SeededRandom` normal no expone por sí mismo esa interfaz. La posición de llamadas tampoco debe tratarse como prueba genérica del estado interno de cualquier RNG y cualquier historial de métodos.
- El comparador exige alignment exacto de `OpportunityKey`; no resuelve automáticamente cambios de universo de oportunidades. Esto importa especialmente para G1/G2.
- El enlace de serialización puede abstenerse ante correspondencias no unívocas. Un label de identidad o un hash no crea información ausente.
- El resultado certifica maquinaria y controles concretos, no atribución humana general ni intención musical.

Una revisión estática del [runner D1 aparcado](../tools/ManiaAddNotesLab.Experiments/D1BehavioralAbRunner.cs) confirma que conserva el abort posterior a ambas matrices y un cálculo `max(0, outputDifference - directDifference)` para downstream. Son rutas históricas sin autorización de ejecución. **Conectar el recorder nuevo no convertiría ese runner automáticamente en un ensayo válido.** No lo ejecuté ni reconstruí sus métricas.

La próxima integración, si se autoriza, necesita una prueba acotada del contrato del experimento específico: dónde se toma una decisión, cuándo se verifica seguridad y cuándo se detiene. Eso es trabajo instrumental subordinado a una pregunta, no otro programa autónomo de infraestructura.

## 5. ¿Estamos dedicando demasiado esfuerzo a infraestructura?

**Hasta este punto hubo inversión justificada; a partir de aquí sí veo riesgo de exceso si se continúa por inercia.** No dispongo de horas ni costes para medirlo. Mi evaluación se basa en qué decisiones desbloqueó cada línea.

A/B hicieron observables problemas reales. C1 evitó promover una idea demasiado simple. D0/F1 precisaron qué se estaba afirmando. F2 localizó un recurso externo faltante. E/E.1 dieron un resultado negativo suficientemente claro. SAFETY.PROV resolvió una carencia identificada por dos intentos inválidos.

El rendimiento marginal cae cuando una fase nueva solo vuelve a demostrar que una representación es exacta, publica otro gran agregado y no cambia la decisión de continuar, aparcar o conseguir datos diferentes. Más cases sintéticos tampoco corrigen por sí solos la ausencia de evaluación humana independiente.

Los reports muestran costes de representación reales: F1 produjo un detalle local de aproximadamente 4,6 GB; F2, unos 2,3 GB; E.1, unos 179 MB. Son tamaños históricos, no una medición actual de disco. Justifican reutilizar IDs, streaming y agregados, pero **no** empezar ahora una gran reescritura por rendimiento. [F1](PHASE_F1_COMPARABLE_CONTEXT_RESOLVER_REPORT.md), [F2](PHASE_F2_TYPED_GAPS_EVIDENCE_BACKOFF_REPORT.md), [E.1](PHASE_E_1_EXACT_RECURRENCE_FAILURE_STRATIFICATION_REPORT.md).

Propondría que toda investigación futura especifique: qué decisión cambia con un resultado positivo, qué decisión cambia con uno negativo y en qué punto se deja de construir. Una fase que no puede responder eso debería quedar fuera de la cola.

La carga documental también merece contención. Hay repetición entre estado, roadmap, arquitectura y reports; por ejemplo, `PROJECT_STATUS.md` repite una fila SAFETY.PROV. No lo corregí. Es un síntoma menor de coste de sincronización, no motivo para otra fase. Una propuesta estratégica no necesita fingir un cierre experimental ni modificar todas las fuentes canónicas.

## 6. Riesgos principales actuales

| Riesgo | Por qué importa | Respuesta propuesta |
|---|---|---|
| Objetivo de producto insuficientemente definido. | «Más evidencia» o «más interacción» puede no mejorar una variante jugable. | Elegir el problema perceptual y su evaluación antes del siguiente behavioral A/B. |
| Exactitud convertida en finalidad. | Un método puede ser impecable y casi siempre abstenerse. | Medir utilidad y abstención sin promover coverage a correctness. |
| Ambigüedad tratada como veto universal. | Un lenguaje con varias alternativas puede volverse inoperante. | Separar membership, compatibilidad, contradicción y selección por tarea. |
| C11 reutilizado como validación independiente. | Las representaciones y decisiones ya se adaptaron a sus hallazgos. | Conservarlo como desarrollo/regresión y reservar datos nuevos antes de usarlos. |
| Portabilidad humana sobreafirmada. | Los fixtures 1K/18K y el 10K rice no validan LN humana en esos K. | Publicar cobertura real por keymode/tipo de chart y no extrapolar. |
| Escalada de composición. | Pasar de una articulación a planes múltiples multiplica estados y contratos. | Postergar H hasta demostrar necesidad y efecto de un solo corte. |
| Reapertura por coste hundido. | Mucho trabajo en D no demuestra que D sea lo siguiente más útil. | Reabrir solo cuando responda una necesidad prioritaria y tenga medición válida. |
| Evidencia positiva confundida con preferencia. | Que otro pasaje contenga la relación no significa que aquí debamos agregarla. | Separar reconstrucción, intervención y evaluación humana. |

## 7. G1/G2/H: sí a una factibilidad acotada, no a la cadena automática

### 7.1 Por qué G1.0 merece discutirse

G1 ocupa hoy una sección breve de A/B en el roadmap, pese a que G cambia semántica LN y puede cambiar oportunidades. Sería un salto desproporcionado frente al rigor alcanzado en D y F. [G1–H](MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md#phase-g1--interior-relation-semantics-ab).

Ya hay material para una investigación más pequeña: `InteriorAnchorObservation`, clasificación `Contained/Crossing/EqualEnd`, witnesses y blockers. El report B observó en Spring 189 candidatos contained, 344 crossing y 33 equal-end, y diez intents de articulación clasificados MixedBlock. Son candidatos/intents de una ejecución específica, **no** frecuencias independientes de preferencias del mapper ni una encuesta de todas las parents. [B](PHASE_B_CERTIFICATES_AND_FAILURES_REPORT.md), [perfil](../src/ManiaAddNotesLab.Core/MapperEvidenceProfile.cs), [diagnostics](../src/ManiaAddNotesLab.Core/DecisionDiagnostics.cs).

### 7.2 Pregunta propuesta para G1.0

> ¿Existe una familia acotada de relaciones LN interiores, observables en originales, que pueda respaldar candidatos completos y tenga suficiente presencia fuera de una sola familia para justificar estudiar una intervención?

No bastaría contar que dos intervalos son contained o crossing: eso sería clasificar geometría. G1.0 tendría que distinguir la relación entre parent, anchor, LN añadible y release; qué parte es un valor, qué parte es una relación conjunta y qué transformación pretende evaluar.

El alcance sugerido sería:

1. Reutilizar la extracción existente y declarar una identidad de relación; empezar por relaciones del archivo exacto que no dependan de recuperar snaps latentes.
2. Enumerar la población estructural original por separado del filtro legacy. Si solo se observan candidatos que sobrevivieron sus mínimos/caps, no se puede evaluar qué oportunidades excluyen esos mínimos/caps.
3. Mantener intactos el conjunto productivo de oportunidades, RNG, candidatos y output. La enumeración adicional sería research-only.
4. Usar reconstrucción con exclusiones coherentes: la target no puede reaparecer como donor, release o held futuro. La parent puede ser contexto conocido de la query; no se convierte por ello en su propio testigo independiente. Un holdout más fuerte por episodio debe reportarse como otra tarea, sin mezclar denominadores.
5. Conservar diferencia entre soporte del candidato completo y mera etiqueta contained/crossing. No resolver competencia mediante frecuencia ni unanimidad por defecto.
6. Reportar por chart/familia/K: población estructural, cobertura, soporte entre alternativas, conflicto/no-context, factibilidad geométrica, pérdidas por los gates legacy y colisiones tras ms. La factibilidad y la evidencia deben quedar separadas.
7. Terminar con una decisión de viabilidad y una limitación explícita. Si no hay señal suficiente, PARK, sin añadir automáticamente otro contexto, cuantizador o clasificador.

Estos son requisitos para discutir el diseño, no autorización ni un gate ya congelado. Los mínimos de utilidad o de diversidad deben fijarse antes del eventual estudio; esta revisión no inventa un número universal.

### 7.3 G2 necesita distinguir dos causalidades

Una cosa es identificar **por qué falla una LN paralela**; otra es atribuir **qué cambió en la ejecución por un tratamiento**. B aborda la primera de forma diagnóstica y SAFETY.PROV aporta infraestructura para la segunda. Ninguna demuestra que articular sea la respuesta musical apropiada.

G2 necesita relación original de retrigger, semántica de mixed blockers, candidate completo, validación de segmentos y contrato de primera pasada. Cambiar eligibility interior y el trigger de articulación simultáneamente mezclaría hipótesis. Primero aislaría una de ellas.

No haría depender toda factibilidad G de F2.ACQ: algunas relaciones se pueden describir en el archivo exacto sin inferir identidad latente. Si la propuesta concreta de G2 exige agrupar retriggers nominales, esa parte sí hereda el bloqueo. La dependencia debe seguir a la afirmación, no al nombre de la fase.

### 7.4 H debe esperar

La programación dinámica o un plan atómico pueden resolver conflictos entre cortes; no establecen cuántos cortes son deseables. Hoy falta evidencia independiente de utilidad de la articulación simple, de que su cap sea el cuello de botella prioritario y de soporte de las cadenas completas.

Mantendría H postergado por más tiempo que G1/G2. Solo lo reconsideraría después de un caso demostrado donde varios cortes aporten algo que una sola articulación no puede aportar. La posibilidad de diseñar un solver no es una razón suficiente para hacerlo.

## 8. D/resulting-state: mantener vivo el concepto y aparcado el ensayo

**No eliminaría D como idea. No repetiría D1 ahora.** D1.0 es uno de los resultados representacionales más sólidos y ataca un problema concreto de composición. D1 carece de resultado conductual interpretable, no de toda motivación.

Para otro intento pediría, después de una autorización específica:

- Una pregunta sobre utilidad del resulting-state que siga siendo prioritaria frente a interiores u otros problemas reales.
- Un contrato nuevo, identidad automática de baseline y una política/version diferenciadas; D1 histórico permanece C.
- Verificación del runner y atribución con provenance real bajo su tratamiento, incluyendo negativos que intenten romper la atribución.
- Seguridad comprobada en el punto de ejecución acordado, con detención antes de continuar el lote ante un hard failure; no solamente un gate post-matriz.
- Una población/dataset preregistrados y una decisión explícita sobre el papel de C11 como desarrollo.
- Separación entre efecto directo, cascada de decisiones, validez final y juicio humano. Un filtro que consume cero RNG puede cambiar llamadas RNG posteriores al cambiar geometría.

No leería métricas retenidas para escoger cuál tratamiento nuevo conviene. Tampoco reutilizaría los antiguos counts downstream como si la nueva infraestructura los validara retrospectivamente.

Si el problema prioritario resultara ser walls de rice y no LN interior, un nuevo estudio D acotado podría preceder a G1. Esa alternativa depende de necesidad observada y contrato válido; SAFETY.PROV por sí sola no establece la prioridad.

## 9. MapperSupport y AddChance: opciones, no destinos obligatorios

### MapperSupport

Lo mantendría `NOT_AUTHORIZED` y lo sacaría de la ruta crítica. No hace falta una cifra universal para representar evidencia, descartar una violación o admitir un candidato que satisface un contrato conjunto explícito.

Lo discutiría cuando existan varias transformaciones completas realmente admisibles, un problema de ordenación demostrado y una forma independiente de evaluar esa ordenación. Compararía primero alternativas simples y mantendría correlaciones, scopes y tamaño de muestra visibles. Un score aprendido o diseñado sobre el corpus puede introducir preferencias externas aunque la run solo lea su chart; eso debe declararse.

No convertiría los estados F1 en números por conveniencia, ni mezclaría rice, LN y articulación en la misma escala antes de tener evidencia de comparabilidad. Un selector por conjuntos podría ser suficiente para ciertas familias sin un MapperSupport global.

### AddChance como presupuesto

Mantendría Bernoulli mientras no haya una necesidad de producto que justifique el cambio. La UI de cuatro controles puede existir con Bernoulli; un presupuesto no es condición de zero-config.

Budget cobra sentido si los usuarios necesitan intensidad predecible y el sistema ya puede definir unidades de transformación y conflictos. Antes hay que decidir si el denominador son oportunidades originales, transformaciones compatibles o interacciones; cómo representar capacidad aproximada; y si se promete anidamiento al subir ADD.

Un presupuesto compartido puede redistribuir gasto entre pass 1 y articulación, rompiendo la equivalencia ON/OFF actual. Los planes atómicos pueden impedir prefijos válidos. Estas son decisiones de producto y política, no un refactor del control `Chance`. [J y contratos](MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md#phase-j--addchance-budget-research).

## 10. C11 y el corpus comunitario

### 10.1 C11: útil, pequeño y ya expuesto

El [inventario C11](MAP_FAMILY_VALIDATION_INVENTORY.md) contiene 11 charts, 11 grupos de familia definidos por `Artist + Title + Creator`, 50.836 objetos y ocho charts con LNs. Solo hay dos charts 4K, uno 10K y ocho 7K; el 10K no aporta LN. No hay charts humanos 1K/18K. La mayor parte de la investigación contextual y composicional reutilizó esta misma colección.

Esa definición de familia sirve para inventario; **no certifica independencia estadística entre mappers o estilos**. El inventario incluye varios charts con el mismo Creator. Las occurrences de un chart, sus seeds y sus vistas tampoco son réplicas humanas independientes.

C11 debería seguir congelado para reproducibilidad, diagnóstico y regresión. Sus holdouts siguen siendo útiles; lo que no puede afirmarse es que el mismo corpus, después de orientar muchas decisiones de diseño, sea una reserva externa intacta. Más trials sobre esos once charts no resuelven ese límite.

Los reports distinguen sistemáticamente naturalidad pendiente de conteos estructurales. No encontré un corpus independiente de evaluaciones humanas que cierre esa brecha. «Chart humano» describe autoría de la entrada, no playtesting de la salida.

### 10.2 Tres recursos distintos

| Recurso | Para qué sirve | Para qué no sirve por sí solo |
|---|---|---|
| C11 congelado | Reproducir, depurar y contrastar hipótesis de desarrollo. | Estimar generalización independiente de nuevas decisiones adaptadas a C11. |
| Nuevos charts comunitarios + evaluaciones | Validación externa de cobertura, comportamiento y preferencias, según protocolo. | Recuperar automáticamente coordenadas latentes pre-export. |
| Paquetes F2.ACQ pre-serialización | Validar hipótesis temporales contra truth independiente con ambos endpoints. | Probar naturalidad o representatividad de todo el producto. |

No mezclaría esos recursos ni llamaría C12 al corpus nuevo sin definir previamente qué significa ese nombre.

### 10.3 Integración comunitaria propuesta

1. **Manifest nuevo e independiente:** ID/version, fecha de adquisición, hash original, origen, autoría declarada, permiso de uso y redistribución, K, timing y relación con otras versiones. No importar silenciosamente una carpeta con el discovery C11.
2. **Control de duplicados a dos niveles:** hash de bytes y comparación semántica de objetos/timing, con revisión de remaps, cortes, cambios de metadata, SV o versiones derivadas. No colapsar semánticamente variantes que sí cambian la tarea; conservar su relación.
3. **Procedencia de transformación:** el filtro `[ADD …]` actual es útil para el corpus histórico, pero un nombre sin esa marca no demuestra autoría humana intacta. Pedir declaración y conservar linaje de edición; no fingir un detector perfecto. [Discovery actual](../src/ManiaAddNotesLab.Core/C11CorpusDiscovery.cs).
4. **Separar adquisición, desarrollo y reserva:** clasificar metadata mínima para balancear antes de explorar resultados. Congelar una reserva por grupos de mapper/familia/versiones relacionadas. Evitar que la misma canción modificada o la misma genealogía de chart aparezca en ambos lados sin control.
5. **Registrar exposición:** si un chart de reserva se usa para escoger features, thresholds o ejemplos, ya fue expuesto y pasa a desarrollo; la reserva se renueva. El mero hecho de no entrenar una red no impide adaptación al conjunto de evaluación.
6. **Cubrir carencias por intención de uso:** LN fuera de 7K, mezclas rice/LN, secciones saturadas, timing variable, mapas cortos/escasos, varias autorías y dificultades. Ninguna cantidad arbitraria garantiza representatividad; publicar la cobertura alcanzada.
7. **No convertir revisión humana en truth única:** mapper y jugador pueden discrepar. Registrar qué juzgaron: conservación del lenguaje, legibilidad, interés de interacción, exceso/defecto o problema temporal. La familiaridad con K y mapa es parte del contexto del juicio.
8. **Prerregistrar la evaluación conductual futura:** separar evaluadores o sesiones de diseño cuando sea viable; presentación ciega/contrabalanceada y rangos equivalentes; seeds como variabilidad de ejecución, no como nuevos sujetos. Definir los criterios antes de abrir la reserva.

No descargué, moví, clasifiqué ni analicé mapas comunitarios en esta revisión. No hay aquí una afirmación de que esa reserva o los paquetes F2.ACQ ya existan.

## 11. Opciones estratégicas

| Opción | Ventaja | Coste o límite | Mi evaluación |
|---|---|---|---|
| Continuar A–K en orden restante. | Conserva narrativa y aprovecha código. | Confunde dependencias históricas con necesidad actual; G/H/I/J pueden encadenarse sin utilidad demostrada. | No recomendada. |
| Priorizar otro D tras SAFETY.PROV. | Problema composicional concreto y representación probada. | Necesita nuevo contrato/runner y una razón de producto para priorizar rice/resulting-state. | Alternativa condicional. |
| G1.0 pequeño + evaluación independiente preparada. | Se acerca al problema LN con primitivas existentes y evita varios blockers de timing si restringe su claim. | Puede terminar legítimamente en PARK; no prueba por sí solo jugabilidad. | Recomendación principal para discutir. |
| Pausar desarrollo de algoritmo y evaluar legacy. | Identifica qué problemas importan realmente y crea un baseline humano. | No reduce arbitrariedad de inmediato; requiere personas y protocolo. | Preferible si aún no podemos formular el problema de uso de G1.0. |
| Resolver primero toda cuantización, contexto y score global. | Arquitectura teórica unificada. | Datos faltantes, incertidumbre alta y distancia grande a utilidad. | Postergaría mucho esta ruta. |

## 12. Qué NO haría ahora

- No promovería ningún treatment ni ocultaría heurísticas cambiando el nombre de policy.
- No reabriría D1/D1.SAFETY históricos ni atribuiría sus outputs mediante snapshots.
- No abriría otra fase genérica de provenance sin una pregunta concreta que la necesite.
- No ejecutaría G1/G2 conductuales directamente desde sus descripciones breves actuales.
- No implementaría múltiples articulaciones, un score universal o budget por completar el roadmap.
- No continuaría E con más features exactas o un fuzzy fallback improvisado.
- No inventaría dominios latentes ni recolectaría más `.osu` como si eso desbloqueara F2.ACQ.
- No repetiría C1 como sustitución de pesos por mera deduplicación; esa hipótesis debe considerarse retirada en su forma original.
- No llamaría default seguro o natural a legacy por el hecho de que los tratamientos nuevos estén aparcados. Es baseline funcional, no garantía perceptual universal.
- No convertiría esta revisión en un cierre de fase ni sincronizaría sus propuestas a `PROJECT_STATE.json`.

## 13. Clasificación propuesta de las ramas

Los estados de esta columna son mi recomendación de planificación, **no escrituras al estado canónico**.

| Rama | Estado canónico resumido | Disposición ASTRA propuesta | Condición para reconsiderar |
|---|---|---|---|
| A/B | COMPLETE | DONE en alcance actual; mantenimiento puntual. | Necesidad concreta de una policy autorizada. |
| C1 simple | HOLD/reformulada | Retirar el reemplazo ciego como candidato; conservar investigación. | Nueva hipótesis que modele relaciones sin borrar señal. |
| C1.1/C1.2 | COMPLETE B/A | DONE representacional. | Una pregunta nueva de selección, no más confirmación de la misma identidad. |
| D0/D0.1/D0.2/F1/D1.0 | COMPLETE A | DONE en investigación realizada; biblioteca reutilizable. | Requisitos concretos del siguiente experimento. |
| D1.GATE | COMPLETE READY histórico | DONE histórico; no pase reutilizable. | Nuevo contrato para cualquier otro intento. |
| D1 | COMPLETE C/PARKED | PARKED; concepto resulting-state permanece vivo. | Prioridad de uso, nueva medición atribuible y autorización específica. |
| D1.SAFETY | COMPLETE C/PARKED | PARKED; retirar el método forensic basado en snapshots como ruta válida. | Reutilizar solo semántica/oracle con provenance verificada. |
| SAFETY.PROV | COMPLETE A | DONE en alcance certificado; parar expansión general. | Integración mínima exigida por experimento autorizado. |
| F2–F2.3 | Investigación COMPLETE; rama condicional. | BLOCKED para la ruta latente; separar pregunta de admisión. | Datos F2.ACQ y una justificación de qué decisión desbloquean. |
| F2.ACQ | BLOCKED | BLOCKED; sin siguiente coding phase. | Paquete externo completo, independiente y autorizado. |
| E/E.1 | COMPLETE B/PARKED | PARKED por más tiempo; cerrar la cadena de refinamiento exacto. | Una pregunta diferente y evidencia nueva, no simplemente otro feature. |
| C2 | DEFERRED | DEFERRED. | Demostrar que peso por gap, separado de tipo/identidad/contexto, es un cuello de botella útil. |
| G1.0 | No existe como fase canónica. | Candidata NEXT para discusión, shadow y acotada. | Objetivo de uso y diseño humano aprobados. |
| G1/G2 | Futuras, no autorizadas. | DEFERRED hasta factibilidad y separación de hipótesis. | Relación completa, alcance, baseline y medición atribuible. |
| H | Futura. | DEFERRED a largo plazo. | Beneficio demostrado de varios cortes frente a uno. |
| I / MapperSupport | NOT_AUTHORIZED. | Fuera de ruta crítica, posiblemente innecesario globalmente. | Problema de ranking y evaluación independiente. |
| J / budget | Futura. | Fuera de ruta crítica. | Necesidad de intensidad predecible y unidades/conflictos definidos. |
| K / zero-config | Futura. | Objetivo de producto, no premio por completar A–J. | Default evaluado y controles estilísticos innecesarios en el alcance declarado. |
| Corpus comunitario | Recolección en preparación según el usuario. | Candidata de preparación NEXT, con permiso y manifest propios. | Partición, exposición y uso definidos antes de evaluar. |

## 14. Mi recomendación y cambio principal al roadmap

**Cambiaría un roadmap de componentes por un roadmap de afirmaciones comprobables.** No exigiría que cada letra produzca un reemplazo ni que todas las ramas culminen en un modelo único.

El siguiente trabajo debería poder terminar de una de estas formas útiles: «esta relación sirve para estudiar una intervención concreta», «esta representación no basta y se aparca», o «falta este dato externo». El proyecto ya ha demostrado que sabe llegar a esas respuestas; necesita evitar convertir cada respuesta negativa en tres nuevas fases de infraestructura.

Mantendría tres niveles explícitos de ambición:

- **Certificación estructural:** representación y hechos exactos del archivo, con abstención bajo claims limitadas.
- **Investigación de generalización:** assumptions explícitas, validación independiente y ninguna promoción automática de inferencia a observación.
- **Producto evaluado:** transformaciones que aportan utilidad a personas dentro de un alcance declarado.

El criterio original `Active manually sourced style decisions = 0` puede conservarse como aspiración de una policy estricta. No lo usaría como único criterio de éxito del producto: la elección de representación, scope y función objetivo contiene supuestos aunque no aparezca un slider. Tampoco aceptaría la evasión de mover una preferencia a `CURRENT_POLICY_CONTRACT` para declarar el contador en cero.

Reformularía el éxito como: **sin preferencias estilísticas ocultas, evidencia y generalizaciones trazables, validez no compensable, abstención explicable y utilidad independiente demostrada**. Si el usuario quiere mantener un alcance aún más estricto —ninguna generalización estilística no derivable del chart— debemos aceptar explícitamente que algunas familias de transformación quedarán sin resolver.

## 15. Preguntas para discutir antes de otra autorización

1. ¿Qué problema de uso queremos resolver primero: walls de rice, poca interacción LN, monotonía, control de intensidad u otro observado por personas?
2. ¿Basta que una relación completa esté demostrada entre alternativas compatibles, o queremos exigir una elección única? ¿En qué casos la pluralidad es contradicción real?
3. ¿Qué significa que un mapper-supported ADD haya mejorado el mapa completo, además de reconstruir notas retiradas y aumentar contadores?
4. ¿Aceptamos un objetivo estrictamente file-derived de alcance limitado y muchas abstenciones, o deseamos estudiar inferencia explícita con otro contrato?
5. ¿G1.0 debe estudiar primero una relación interior concreta? ¿Cuál tendría valor aunque G2/H nunca se implementaran?
6. ¿Cómo separaremos la causa geométrica de un fallo, la atribución del efecto de la policy y el juicio musical sobre la articulación?
7. ¿Quién puede evaluar en cada K y qué parte del corpus comunitario quedará sin usar para diseñar decisiones?
8. ¿Hay una ruta real para conseguir el paquete F2.ACQ, y qué decisión concreta cambiaría al obtenerlo?
9. ¿Qué resultado negativo nos haría abandonar una rama en lugar de abrir otra subfase?
10. ¿Qué propiedad de ADD es irrenunciable: Bernoulli, previsibilidad, anidamiento, pass 1 idéntico con articulación o alguna combinación explícita?

## 16. Respuestas finales solicitadas

1. **Mayor aprendizaje:** una observación exacta, una explicación estructural y una decisión de intervención son niveles distintos; la evidencia no incluye automáticamente su propia regla de uso.
2. **Mayor error a evitar:** construir una maquinaria cada vez más rigurosa para una afirmación demasiado fuerte o poco útil, y confundir nuevos certificados con progreso perceptual del generador.
3. **Qué haría después:** discutir objetivo de uso y semántica de alternativas; preparar una reserva comunitaria independiente; considerar un G1.0 limitado reutilizando lo existente. Si el problema de uso sigue indefinido, evaluar primero legacy.
4. **Qué cambiaría más del roadmap original:** eliminaría la obligación implícita de llegar linealmente a MapperSupport + budget + planes múltiples. Mantendría esas ramas como opciones que deben justificar su necesidad.
5. **Qué discutir antes de otra fase:** qué se intenta mejorar, qué cuenta como respaldo suficiente, cómo se medirá el beneficio y el daño, qué datos permanecerán independientes y cuándo se abandona la hipótesis.

---

**Esta revisión no autoriza G1.0, G1, G2, H, D nuevo, SAFETY.GATE, MapperSupport, budget ni adquisición de datos.** Los estados y contratos existentes permanecen intactos. Solo se han creado documentos de opinión para una conversación posterior.
