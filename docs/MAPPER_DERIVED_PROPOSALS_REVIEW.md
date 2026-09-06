# Propuestas futuras para ADD NOTES derivado del mapa

Revisión del código y documentación local realizada el 5 de septiembre de 2026.

Este documento responde a los 23 apartados de `FUTURE_MAPPER_DERIVED_ALGORITHM_PLAN (1).md`. Presenta propuestas para investigar e implementar más adelante. No modifica el algoritmo ni convierte las hipótesis del archivo recibido en requisitos ya aprobados.

**Recomendación principal:** construir un motor de transformaciones con testigos originales verificables. Los histogramas sirven para describir el chart; las relaciones entre eventos sirven para proponer cambios; una evaluación del conjunto resultante evita que cambios individualmente respaldados produzcan un patrón sin respaldo.

## Estado contrastado con el proyecto

La revisión original se realizó sobre una copia local sin repositorio Git consultable; no afirmaba sincronización con un remoto.

Ya existe un [blueprint de integración](MAPPER_DERIVED_INTEGRATION_BLUEPRINT.md) bastante completo. Las propuestas de abajo precisan decisiones que allí siguen abiertas y añaden experimentos para distinguir alternativas.

| Situación | Estado observado |
|---|---|
| Separar heads de holds | Implementado; las tails bloquean geometría sin contar como heads nuevos en el modo moderno. |
| Portabilidad | Parser 1K–18K y fórmulas relativas; la normalización actual todavía toma 7K como referencia. |
| Timing del mapa | Conversión en beats `decimal`, duraciones trasladadas y releases originales; tabla estándar disponible como A/B. |
| Interiores LN | Contexto original sin parent/virtual como observación en el modo moderno, pero siguen mínimos y cap. Inactivos por defecto. |
| Articulación | Segunda pasada, head original, gap aprendido, una sustitución por parent y RNG separado. Inactiva por defecto. |
| Retrigger | Ya carece de fallback numérico; conserva ventana, soporte mínimo, agrupación decimal y filtro `gap <= 1 beat`. |
| Lane gap | Menor gap con soporte suficiente, agregado entre lanes; fallback de `0.125 beat`. |
| Interacciones | Ya existen `AddedHeads`, `AddedReleases` y `AddedInteractions`. |
| Perfil y selector derivados | Arquitectura propuesta en documentación; no encontré su implementación en el Core revisado. |

El informe de articulación documenta 6,72 articulaciones/run en Spring of Dreams, ADD 50, seeds 1–100, con primera pasada equivalente ON/OFF. Es evidencia histórica del informe, no un experimento ejecutado de nuevo en esta revisión ni una validación humana de naturalidad.

## 1. Objetivo maestro: transformar con testigos

**Vale la pena implementar `TransformationWitness`.** Cada candidato debería poder señalar eventos originales que respaldan su forma y su relación con el contexto.

Ejemplo: una LN de un beat existe en el chart. Eso demuestra una duración. Para colocarla encima de una LN sostenida en otra lane, conviene buscar además ejemplos originales de esa relación: head interior, intervalo compartido y release relativo.

Separaría tres grados de afirmación:

- `ObservedValue`: el valor existe, por ejemplo una duración.
- `ObservedRelation`: existe la relación pertinente, por ejemplo una LN que comienza dentro de otra.
- `CompatibleComposition`: varias relaciones respaldadas pueden coexistir legalmente en el resultado propuesto.

El motor debe declarar cuál utilizó. No llamaría «estilo demostrado» a un candidato que solo reúne valores observados por separado.

Hay un límite conceptual importante: **un único chart no identifica toda la intención del mapper**. Si solo contiene singles, exigir que el tamaño de chord resultante ya exista impide añadir rice en los mismos timestamps. Si no demuestra retriggers, una sección Full-LN puede quedar intacta. Es una consecuencia coherente de la versión estricta, y debe ser visible antes de generar.

**Experimento:** comparar un motor de histogramas con otro de relaciones testificadas en mapas que tengan idénticos histogramas y distinto orden de eventos. El segundo debería distinguirlos.

## 2. Intención e invariantes: dos contratos separados

Conservar `AddChance`, `Seed` y `SelectedRange`, y agregar internamente una versión explícita de política. Exportación y diagnóstico pueden seguir siendo opciones operativas.

**Propuesta:** separar `StyleEvidence` de `HardValidity`. Un score alto nunca compensa una colisión, una duración no positiva o un tiempo inválido tras serialización.

Mantener dos representaciones:

- Evidencia congelada de `OriginalObjects`.
- Estado actual de colocaciones y sustituciones, usado para conflictos y evaluación del resultado acumulado.

Consultar el resultado acumulado para comprobar límites aprendidos **no equivale a aprender de sintéticos**. Cambia el estado que evaluamos; no cambia el vocabulario que lo juzga.

El rango debe conservar su semántica actual: selecciona heads de oportunidades; el chart completo aporta contexto y colisiones. No convertirlo silenciosamente en un recorte de releases o del perfil.

**Aceptación:** perfil idéntico con distintas seeds, intensidades y rangos; generación determinista por versión; validación final sobre tiempos en milisegundos. Una parent articulada permanece en la evidencia original aunque el writer emita su sustitución.

## 3. Qué debe desaparecer: etiquetas solo cuando sean necesarias

No todo concepto necesita convertirse en una clasificación aprendida. Muchas veces puede desaparecer la pregunta «¿es larga?» y reemplazarse por «¿qué estructuras respaldadas caben aquí?».

| Concepto del plan | Consulta propuesta |
|---|---|
| Chord grande | Soporte del estado vertical resultante en un contexto comparable. |
| LN larga | Formas y anchors que contiene, sin gate previo de duración. |
| Gap pequeño | Compatibilidad de la transición concreta antes/después del candidato. |
| Duración normal | Distribución condicionada de formas; conservar varios modos. |
| Distancia relevante | Capacidad de un contexto para predecir observaciones originales excluidas. |
| Contexto suficiente | Evidencia disponible, estabilidad y alcance de la afirmación. |
| Retrigger típico | Distribución de transiciones equivalentes, incluyendo tipo del siguiente head. |
| Oportunidades razonables | Transformaciones distintas y compatibles, con presupuesto compartido. |

Short/medium/long pueden seguir siendo etiquetas descriptivas. Si solo sirven para el panel, no deberían alterar decisiones.

## 4. `MapperEvidenceProfile`: guardar relaciones y procedencia

**Es el primer componente nuevo que implementaría**, inicialmente en modo observación.

Además de los vocabularios del plan, guardaría:

- `ObservationId` y objetos originales participantes.
- Tipo de relación: simultaneidad, sucesión same-lane, contención LN, release compartido o cadena de retriggers.
- Tiempo exacto original, posición en beats y contexto de timing.
- Frecuencia de eventos y distribución entre regiones/frases.
- Contextos donde esa relación pudo ocurrir y contextos donde ocurrió.
- Alcance, incertidumbre y versión del método de extracción.

La última distinción permite pasar de «hay ocho retriggers» a «hay ocho entre cuarenta contextos comparables». El denominador también necesita una definición auditable; no se obtiene solo contando objetos.

Usaría colecciones realmente inmutables y un fingerprint de contenido. `IReadOnlyList` por sí solo no garantiza que nadie conserve una referencia mutable al almacenamiento.

No almacenaría millones de ventanas duplicadas. Mantendría índices por lane/timestamp y referencias a observaciones compartidas. Separaría datos puros del contador mutable `BeatTimeline.ConversionCount` al reutilizar análisis entre runs.

**Primer resultado útil:** JSON del perfil y respuestas trazables, sin afectar el `.osu`. Medir construcción, memoria y consultas en charts reales antes de ampliar la representación.

## 5. Backoff: distinguir desconocimiento de discrepancia local

El flujo local → sección → global → SKIP es una buena base, pero requiere una regla adicional: **no ampliar alcance solo porque la evidencia cercana no favorece el candidato**.

Propondría respuestas distintas:

| Estado | Significado | Acción propuesta |
|---|---|---|
| `NoComparableContext` | No hay observaciones equivalentes. | Ampliar alcance. |
| `ObservedSupport` | Hay testigos compatibles. | Mantener su distribución y procedencia. |
| `LocalMismatch` | Hay contextos comparables, pero muestran otro comportamiento. | Conservar la discrepancia; no ocultarla con evidencia del clímax. |
| `AmbiguousEvidence` | El soporte cambia mucho según contexto o exclusión de una región. | Comparar modelo más general; abstenerse si la conclusión necesaria sigue sin respaldo. |

`LocalMismatch` no prueba que el mapper prohíba algo. Es una política conservadora de intervención, no una afirmación sobre su intención.

Tampoco resolvería el problema de muestras pequeñas normalizando frecuencias sin más: **un singleton obtiene peso relativo 1 cuando es el único candidato**, pero no adquiere certeza por ello. Frecuencia, diversidad estructural y estabilidad deben permanecer separadas.

Compararía resolutores mediante reconstrucción de originales excluidos por bloques. La elección del criterio de estabilidad sigue siendo una decisión de modelo que debe documentarse; llamarla estadística no la vuelve libre de sesgo.

**Caso decisivo:** una intro de singles y un clímax de chords. La ausencia de triples en la intro no debería disparar automáticamente un préstamo global de triples.

## 6. Chord density: completar estados y comprobar el conjunto

**Propuesta de alto valor: `ChordCompletionModel`.** Trabajar por timestamp además de por objeto, conservando cuántas oportunidades originales aportan a ese evento.

Construir ejemplos auxiliares a partir de chords originales: ocultar conceptualmente un miembro y comprobar si el contexto restante permite reconstruirlo. El target es un objeto original conocido; el ejemplo reducido es un instrumento de evaluación, nunca nueva evidencia musical.

Esto responde mejor a «¿puedo completar esta chord?» que contar transiciones temporales `2 heads → 3 heads`: pasar de una chord a otra en el tiempo no demuestra que deba agregarse una nota al mismo timestamp.

Guardar tanto conteos como ratios, más mezcla tap/LN y estado de holds. Los holds son contexto y geometría, no una penalización automática equivalente a nuevos heads.

**Control imprescindible:** si tres heads originales crean tres oportunidades, no evaluar las tres únicamente contra el estado original de tres heads. Tres cambios individualmente `3→4` podrían terminar en seis heads. Evaluar cada ampliación contra el estado resultante, con evidencia siempre congelada, o seleccionar un estado objetivo conjunto respaldado.

Agrupar por timestamp no debería conceder gratuitamente nuevas oportunidades ni multiplicar votos por el número de combinaciones posibles de una chord. Registrar una unidad de testigo original y auditar cualquier ponderación por tamaño.

**Pruebas futuras:** chart solo-singles produce abstención bajo soporte estricto; chord de cuatro no autoriza seis por acumulación; tails añadidas no cambian el perfil; comparar estados resultantes y no solo medias de factor.

## 7. Densidad contextual: segmentos por eventos y repetición

No comenzaría por un detector complejo de frases musicales. Implementaría una serie por timestamps originales distintos con spacing en beats, número de heads, releases y mezcla tap/LN. Así no se necesita una rejilla inicial de un beat.

Compararía dos prototipos en sombra:

1. **Recurrencia de secuencias:** identificar secuencias repetidas de spacing y ocupación. Sus duraciones proponen escalas de contexto reales.
2. **Segmentación por cambios:** buscar intervalos donde cambia persistentemente la distribución de esos eventos, seleccionando la complejidad mediante un criterio explícito y evaluación sobre bloques excluidos.

PELT es una referencia útil para optimizar objetivos de segmentación penalizados; su eficiencia lineal depende de condiciones y no define por sí mismo qué es una sección musical. La penalización sigue siendo una elección que puede afectar el resultado. [Killick, Fearnhead y Eckley](https://arxiv.org/abs/1101.1438).

Para estudiar sensibilidad, comparar varias penalizaciones y localizar límites persistentes tiene precedente en trabajos con CROPS. Eso no elimina la necesidad de escoger una política final para este proyecto. [Haynes, Fearnhead y Eckley](https://arxiv.org/abs/1602.01254).

Una sección densa y sostenida puede formar su propio régimen. Un pico breve puede ser un motivo intencional: **ser raro no significa automáticamente que deba penalizarse**. Evaluaría el estado que produciría ADD en ese motivo, en vez de castigar toda rareza del original.

**Experimento:** fixtures con la misma densidad media y distinta distribución temporal; añadir intro/clímax, patrón alternante, mapa corto, silencios y BPM variable. Comparar segmentaciones y decisiones, no solo número de segmentos.

## 8. Lane gap: transiciones por lado y por tipo

El analizador actual agrega gaps entre todas las lanes, toma el menor con soporte y aplica un mismo mínimo a ambos lados de la nueva LN. Eso pierde diferencias entre `tap→LN`, `LN→tap` y `LN→LN`.

**Propuesta:** `GapEvidenceBefore` y `GapEvidenceAfter`, condicionados por los objetos vecinos reales y por lane o rol demostrado. Si no existe vecino a un lado, no inventar una transición para ese borde.

Mantener por separado:

- Colisión y orden temporal, obligatorios.
- Separación estilística, consultada al perfil.
- Soporte del ritmo resultante completo.

No usar «mínimo observado» como autorización automática de todos los gaps mayores. Un mínimo puede servir como restricción de separación, pero no demuestra que un hueco arbitrario pertenezca al vocabulario rítmico. Para el modo estricto, buscar testigos de la transición resultante; para un modelo de envolvente, declarar explícitamente la generalización usada.

Registrar gaps positivos sin el corte universal de un beat y condicionar su interpretación por contexto: un silencio largo no es automáticamente un retrigger lento reutilizable.

**Experimento:** mapa con dos convenciones de gap según el tipo de transición; un outlier diminuto no debe relajar indiscriminadamente todas las lanes. Medir cobertura perdida al sustituir el fallback por backoff/SKIP.

## 9. LN duration y release: votos con identidad

**Propuesta inmediata posterior al perfil:** sustituir la afinidad source por soporte trazable de formas y relaciones.

El código actual genera votos de duración y de release desde cada observación y los suma si llegan al mismo endpoint. En ocasiones un solo objeto respalda el mismo candidato por ambas vías. Son dos explicaciones, pero no dos demostraciones independientes.

Deduplicaría por `(candidate, observationId)` para el conteo de testigos independientes, conservando las etiquetas `duration` y `release` para explicación. Las distintas observaciones del mismo patrón sí siguen aportando frecuencia.

No usaría automáticamente una duración frecuente en cualquier fase rítmica. El perfil debe conservar relaciones entre head, duración, release y anchors. Un release exacto y una duración trasladada son operaciones distintas con evidencia distinta.

Conservar el timing relativo y los endpoints originales exactos. Validar otra vez después de convertir a milisegundos: un intervalo válido en beats puede colapsar o tocar un vecino tras el redondeo.

**Prueba:** un candidato no duplica su autoridad por representar dos veces el mismo testigo; dos testigos realmente diferentes sí incrementan su soporte. Cubrir divisiones raras, offsets y cruce de BPM.

## 10. Distancia contextual: elegir contextos por capacidad predictiva

No reemplazaría `0.80/0.60/...` por otra curva elegida manualmente. **Primero probaría eliminar la ponderación por distancia dentro de contextos estructuralmente equivalentes.**

Compararía vecindad temporal, misma región y misma posición de un motivo repetido. Elegiría el nivel de detalle que mejor recupera observaciones originales excluidas, con preferencia documentada por el modelo más simple cuando no mejora la predicción.

Esto permite que una frase repetida distante sea más relevante que una LN cercana de otra sección. Si el mapa no demuestra periodicidad, no debe aparecer una «frase de ocho beats» por defecto.

Una representación de secuencias necesitará decisiones sobre longitud y equivalencia. Estas también deben auditarse; se pueden probar longitudes observadas y generalizaciones progresivas sin afirmar que son neutrales.

**Experimento:** alternancia de secciones A/B donde A reaparece lejos. Evaluar reconstrucción fuera del bloque donante y evitar que copias casi idénticas contaminen simultáneamente entrenamiento y validación.

## 11. LN interior: vocabulario de contención y oportunidades únicas

**Propuesta:** extraer de originales relaciones `parent contiene anchor + otra LN`, incluyendo la posición relativa y relación entre releases. Usar esos testigos para consultar oportunidades sin exigir previamente tres beats, tres LNs o dos anchors.

La parent candidata aporta posición y estructura. No se inserta como observación virtual para votar por su propio release. Una parent original que participa en otro testigo real no debe contarse dos veces por sus distintos papeles.

Los anchors deben tener tipo. Un release-only puede respaldar ciertas relaciones interiores, pero no debe convertirse automáticamente en head de articulación.

Deduplicar transformaciones idénticas que aparecen bajo varias parents superpuestas. Puede haber varios testigos y varias oportunidades originales asociadas a un resultado; no se deben transformar en colocaciones duplicadas ni en tiradas extra inadvertidas.

Hay una precisión de contrato pendiente: el código revisado crea el **start interior** dentro de la parent, pero `BuildReleaseCandidates`/`PlaceLongNote` no muestran un límite general que obligue al nuevo release a terminar antes que ella. La propuesta futura debe distinguir explícitamente `contained` y `crossing`, y exigir evidencia para cada relación. No asumir que «interior» ya impone esa frontera en toda la implementación.

**Experimento:** parent corta con una estructura demostrada puede participar; parent muy larga sin relaciones respaldadas puede abstenerse. Duplicar la explicación de una oportunidad no debe aumentar su probabilidad.

## 12. LN Articulation emergente: demostrar la causa del fallo

**Propuesta de alto valor:** reemplazar el gate `NonHeldColumns <= 1` por un resultado geométrico explicativo.

`PlaceLongNote` devuelve actualmente un objeto o `null`. Para el diseño futuro conviene distinguir:

```text
NoShapeEvidence
NoGapEvidence
BlockedByOriginalHold
BlockedByOriginalHead
BlockedBySyntheticPlacement
BlockedBySpacing
```

Un fracaso con cero formas no prueba saturación. Un fracaso creado por sintéticos anteriores tampoco demuestra un pasaje Full-LN del mapper.

Evaluaría primero candidatos respaldados en geometría original y guardaría objetos bloqueantes por candidato/lane. Una comprobación diagnóstica puede retirar conceptualmente los holds identificados para verificar que explican la imposibilidad, manteniendo las restantes restricciones. Esa comprobación no es una colocación autorizada.

Solo después, si existe vocabulario de articulación compatible, construiría la sustitución real en la lane de la parent y validaría sus segmentos contra la geometría actual. Mantener segunda pasada y RNG independiente mientras se conserva el contrato actual.

**Experimento:** fallos por forma ausente, tap, gap o sintético no activan articulación; bloqueos por holds pueden hacerlo aunque el conteo agregado no sea Near-Full. También probar fallos mixtos para evitar atribuir toda la causa a un único blocker.

## 13. LocalRetriggerGap: conservar frecuencia por gap

Aquí no hace falta «eliminar un fallback»: ya no existe uno numérico. La mejora concreta es la calidad de la evidencia que llega al selector.

`RetriggerGapDecision` devuelve gaps y `EvidenceCount` agregado. Después, `ResolveArticulations` usa ese total al pesar cada gap, más releases exactos y un bono same-lane. Así se pierde parte de la diferencia específica entre un gap observado ocho veces y otro dos veces.

**Propuesta:** devolver una lista de `(gap, transitionType, witnessIds, count, scope)`. Pesar cada alternativa con sus propios testigos; conservar same-lane como procedencia o condición, evitando añadirle automáticamente un `+1`.

Distinguir `LN release→tap` de `LN release→LN head`: la articulación propuesta termina en otra LN. Ambas transiciones son información útil, pero no equivalencia demostrada.

Ampliar alcance temporal y lane como ejes separados. No usar cross-lane global sin revisar si las lanes muestran roles distintos. Si no se demuestra equivalencia, permitir SKIP.

**Experimento:** distribución original 8:2 conserva preferencia por el primer gap cuando el resto es comparable; el resultado no usa 10 como soporte idéntico de cada alternativa.

## 14. Múltiples articulaciones: planificar la parent completa

Quitar el cap de uno requiere cambiar la representación actual de `ArticulationReplacement`, que guarda exactamente dos segmentos.

**Propuesta:** `ParentArticulationPlan` con lista de segmentos y referencias a todos los cortes, aplicado atómicamente a una parent original.

Para candidatos `(R_i,H_i)`, construir un grafo dirigido temporal. Conectar cortes solo cuando:

```text
H_i < R_j
```

y el segmento `[H_i,R_j]` es legal y tiene soporte. Validar también `[S,R_first]` y `[H_last,E]`, más gaps y spacing de la cadena. La opción de no articular siempre permanece disponible.

Programación dinámica puede resolver variantes con costes y soporte descomponibles. Si el score depende de toda la cadena, hay que ampliar el estado o reconocer la aproximación. **Intervalos de gap que no se solapan no bastan:** dos cortes individualmente válidos pueden dejar entre ellos una LN demasiado corta o sin respaldo.

Maximizar una suma de pesos positivos tendería a llenar todos los cortes posibles. El número final debe estar sujeto al presupuesto y al soporte de la cadena de articulaciones, no solo a geometría.

No generar oportunidades recursivas desde los segmentos nuevos. Todos los anchors proceden del original y se planifican juntos.

**Experimento:** dos cortes compatibles, dos cortes incompatibles por segmento intermedio y tres cortes donde la mejor combinación no incluye el mejor corte individual. Contrastar el solver con enumeración exhaustiva en fixtures pequeños.

## 15. AddedInteractions: conservar el vector y evitar doble conteo

La base ya está implementada:

```text
AddedInteractions = AddedHeads + AddedReleases
```

Una articulación de una parent en dos segmentos añade netamente un head y un release; añade un objeto neto en el archivo, aunque no sea un `AddedObject` de la primera pasada.

**Propuesta:** añadir `AddedRetriggers` como relación y `HeldBeatDelta` como cambio de tiempo sostenido. Un retrigger se compone de release y repress; no sumarlo una tercera vez a `AddedInteractions`.

Para m cortes, la sustitución crea m+1 segmentos y añade m heads, m releases y m objetos netos. Separar `ArticulatedParents` de `ArticulationCount`: hoy coinciden por el cap de uno; después dejarían de coincidir.

No hay evidencia para convertir una interacción en una cantidad fija de dificultad. Mantener el vector permite comparar un tap, una LN paralela y una articulación sin fingir que tienen el mismo efecto jugable.

## 16. MapperSupport: contrato explicativo antes que score único

**Implementaría primero un `SupportCertificate`.** Contendría testigos, scope, relaciones necesarias satisfechas, evidencia faltante y conflictos.

La legalidad debe ser un gate separado. Un candidato imposible no obtiene «menos GeometrySupport»; no puede materializarse.

Evitar multiplicar marginales como si fueran independientes. Duración, densidad, ocupación y tipo de sección pueden describir el mismo fenómeno; un mismo objeto puede participar en varios vocabularios.

Para el primer prototipo usaría soporte conjunto de contextos comparables cuando exista. Al generalizar, declararía qué detalle se omitió: por ejemplo conservar tipo de transición y régimen, pero omitir identidad exacta de lane solo si se admite esa generalización.

Un score escalar posterior puede medirse por su capacidad de ordenar reconstrucciones originales frente a alternativas. El score no será una probabilidad de «mapper approval», ni debe convertir la falta de muestra en certeza.

**Experimento:** construir dos mapas con las mismas frecuencias de duración, gap y chord, pero combinaciones diferentes. Un modelo que les asigna las mismas transformaciones todavía está perdiendo relaciones esenciales.

## 17. AddChance como presupuesto: definir primero qué se cuenta

**Mi primera elección sería presupuestar unidades de transformación respaldadas**, y mostrar interacciones como resultado. Contar candidatos brutos sería incorrecto: cien duraciones alternativas para un mismo head no son cien oportunidades independientes.

Propuesta reproducible:

1. Congelar perfil y universo de transformaciones para el rango.
2. Agrupar alternativas mutuamente excluyentes y fusionar duplicados.
3. Crear una secuencia de decisiones compatible, ordenada por soporte y desempates estables derivados de seed.
4. Definir la capacidad respecto de esa planificación explícita.
5. Usar un prefijo según ADD, con redondeo documentado para cantidades indivisibles.

No llamaría «capacidad máxima» al resultado de un planificador greedy; sería capacidad planificada. Hallar el máximo global con restricciones arbitrarias puede ser mucho más costoso.

Este diseño puede ofrecer resultados anidados al subir ADD si cada prefijo es válido. Las articulaciones múltiples complican esa propiedad: un plan completo puede estar respaldado mientras un subconjunto de cortes no lo está. Esos planes deben ser unidades atómicas o exigir validez de cada prefijo. Mostrar aproximación y presupuesto no usado, sin forzar cambios.

Hay una incompatibilidad a resolver con el contrato actual: **un presupuesto global compartido por pass 1 y articulación puede cambiar pass 1 al activar articulación**. No se puede prometer a la vez esa competencia global y la identidad ON/OFF actual sin un mecanismo adicional. Mantendría Legacy intacto y versionaría esta semántica como otro modo experimental.

**Experimento:** ADD creciente con misma seed no retira transformaciones cuando se promete anidamiento; cien candidatos de la misma oportunidad no inflan el denominador; ADD 100 no inventa vocabulario ni garantiza modificar todo el mapa.

## 18. Magic Number Audit: incluir números y decisiones sin números

No he realizado una auditoría exhaustiva; sí identifiqué objetivos concretos para ella.

| Ubicación actual | Qué auditar |
|---|---|
| `AddNotesOptions` | Ventanas, factores, mínimos, prioridades y caps expuestos. |
| `HeadDensityAnalyzer` | Pisos relativos a 7K, pasos de un beat, span 1–4 y clamp. |
| Ambos analizadores de gap | Corte de un beat, redondeo a seis decimales y pooling de lanes. |
| `BuildReleaseCandidates` | Doble voto del mismo objeto, afinidad y mínimo local como envolvente. |
| `Nearly` / `DistanceWeight` | Tolerancias que alteran afinidad o bandas temporales. |
| `BuildInteriorOpportunities` | Suma anchor+context, prioridad por separación y preferencia por el centro. |
| `ResolveArticulations` | Total de evidencias aplicado por gap, voto de release y bono same-lane. |
| Selección de lane | Uniformidad actual: hipótesis de estilo aunque no tenga un slider. |
| Generación de oportunidades | Una por head y las equivalencias/deduplicaciones elegidas. |

**Propuesta:** `BehaviorDecisionAudit`, ampliando el inventario numérico con decisiones categóricas, desempates y fuentes de identidad RNG. Clasificar un valor como `IMPLEMENTATION_ONLY` exige mostrar que cambiarlo preserva las decisiones relevantes, no solo que es pequeño o está dentro de una función auxiliar.

Distinguir precisión de formato de tolerancia de evidencia: agrupar gaps por seis decimales puede dividir o fusionar convenciones según BPM y redondeo original. Guardar tiempos originales y la incertidumbre de cuantización ayuda a estudiarlo; no sustituirlo a ciegas por otro epsilon.

## 19. Orden futuro: priorizar preguntas que desbloquean decisiones

| Incremento | Trabajo concreto | Evidencia para avanzar |
|---|---|---|
| A | Snapshot del baseline, auditoría de decisiones, IDs de testigos, perfil mínimo y exportación. | Perfil determinista y output legacy idéntico. |
| B | Certificados de soporte y causas precisas de rechazo en sombra. | Separar falta de forma, gap, holds y conflicto sintético. |
| C | Frecuencia por retrigger, deduplicación de votos LN y comparación sin affinity. | A/B atribuible; no afirmar naturalidad por conteos. |
| D | ChordCompletion y comprobación del estado acumulado. | No crear estados colectivos sin soporte. |
| E | Segmentación/contextos alternativos en sombra. | Mejor reconstrucción fuera del bloque, con sensibilidad documentada. |
| F | Backoff por transición y eliminación del lane fallback. | Cobertura y cambios por sección conocidos. |
| G | Contención interior y trigger de articulación por causa. | Casos cortos respaldados y falsos triggers resueltos. |
| H | Planes de múltiples articulaciones. | Solver validado y representación de sustituciones generalizada. |
| I | Comparar selector por probabilidad y presupuesto. | Semántica, denominador, conflictos y estabilidad explícitos. |
| J | UI final con diagnóstico; playtesting diverso. | Sliders innecesarios en la colección de evaluación. |

No haría depender el primer perfil de resolver perfectamente las secciones. Puede empezar con evidencia global y vecindarios estructurales declarados, sin fingir que una ventana fija ya es una sección aprendida.

La validación por ocultación sirve para contrastar modelos, pero no demuestra que añadir notas a un mapa ya completo sea musicalmente deseable. Playtesting sigue siendo una etapa diferente.

## 20. Portabilidad: proporciones sin borrar la geometría

Usar el mismo algoritmo para todos los K y aprender de cada chart. No hace falta transferir por defecto la evidencia de un 4K a un 10K.

**Propuesta:** conservar conteos discretos, ratios y relaciones entre lanes. El ratio 1 de una full chord es comparable; la factibilidad de añadir otra lane sigue siendo cero. En keymodes distintos, un incremento de una nota representa proporciones distintas.

Las lanes no son necesariamente intercambiables. Si el mapa demuestra una lane con un papel específico, no repartir allí notas uniformemente por conveniencia. Tampoco imponer manos o roles de teclado que no provienen del chart.

Compararía un selector uniforme con otro condicionado por coocurrencia y sucesión original. La equivalencia de lanes y cualquier normalización espacial son hipótesis a auditar.

**Pruebas:** 1K, 4K, 7K, 10K y 18K; transformación de etiquetas de lane con resultado estructural equivalente cuando la política no usa proximidad física; espejado con correspondencia de IDs si se promete determinismo estructural. No exigir igualdad byte a byte por seed entre keymodes con distinto número de oportunidades.

## 21. Criterio de finalización: trazabilidad y evidencia fuera de muestra

El criterio numérico del archivo es útil, pero incompleto. Un algoritmo sin sliders puede seguir imponiendo estilo por su representación, función objetivo o desempates.

**Propuesta:** exigir cuatro contratos verificables:

- Toda transformación explica qué valores y relaciones provienen del original, y qué generalizaciones aplicó.
- Las preferencias restantes están auditadas y versionadas.
- La validez geométrica y de formato se verifica independientemente del soporte.
- La naturalidad se evalúa en charts y estilos distintos de los usados para ajustar el modelo.

No establecería un porcentaje universal de cobertura para declarar éxito. Un mapa pobre en evidencia puede producir muchos SKIP correctamente. Sí exigiría conocer qué familias y contextos fallan, y que la abstención no esconda un bug de extracción.

## 22. UI final: explicar decisiones con ejemplos del mapa

Conservar Chart, Add %, Seed, Range y Generate. El panel read-only propuesto puede ser mucho más útil si cada resumen lleva a ejemplos.

**Propuesta de producto:** al seleccionar una adición, mostrar sus testigos en el mapa original y la razón de la transformación. Al seleccionar un SKIP, mostrar si faltó vocabulario, hubo discrepancia local, conflicto geométrico o presupuesto.

Vista previa antes de generar:

```text
Oportunidades originales
Transformaciones distintas con evidencia
Capacidad planificada compatible
Presupuesto solicitado / aplicado
Sin evidencia / discrepancia local / conflicto
```

La cobertura debe incluir denominador y definición. «80%» sin aclarar si se refiere a heads, parents o candidatos no es informativo. Con denominador cero, mostrar «no aplica» en vez de 0%.

No presentar `Confidence 92%` como probabilidad de que guste al mapper. Mostrar cantidades observables: testigos, regiones, tipo de relación y alcance del backoff.

## 23. Filosofía final: el mapa permite proponer, no leer intenciones

La dirección del archivo merece desarrollarse. La versión más defendible sería:

> ADD utiliza relaciones demostradas por el chart para proponer más interacción, evalúa el conjunto resultante y se abstiene cuando no puede respaldar la transformación bajo su política declarada.

La mejora de mayor valor es pasar de «reutilizo números que aparecen en el mapa» a «puedo explicar qué relación original justifica este cambio». La segunda mejora es controlar la acumulación: vocabulario válido no garantiza una composición válida.

Las primeras piezas que considero suficientemente concretas para codear en una futura iteración son el perfil con procedencia, las frecuencias por retrigger, la deduplicación de votos LN, los rechazos geométricos explicativos y la evaluación vertical conjunta. Contexto adaptativo, score unificado y presupuesto global necesitan experimentos antes de fijar su diseño.

## Referencias locales verificadas

- [Diseño implementado](DESIGN.md).
- [Blueprint existente](MAPPER_DERIVED_INTEGRATION_BLUEPRINT.md).
- [Análisis temporal, lane gaps y retriggers](../src/ManiaAddNotesLab.Core/ChartAnalysis.cs).
- [Motor: oportunidades, votos LN y segunda pasada](../src/ManiaAddNotesLab.Core/AddNotesEngine.cs).
- [Opciones, sustituciones y métricas](../src/ManiaAddNotesLab.Core/Model.cs).
- [Índice geométrico](../src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs).
- [Timeline y cuantización](../src/ManiaAddNotesLab.Core/BeatTimeline.cs).
- [Informe de articulación](LN_ARTICULATION_REPORT.md).
- [Informe rice](RICE_BEHAVIOR_REPORT.md).
- [Corrección de interiores](LN_INTERIOR_CORRECTION_REPORT.md).
- [Informe multikey](KEYMODE_INVARIANCE_REPORT.md).
- [Pruebas de keymodes y articulación](../tests/ManiaAddNotesLab.Tests/KeymodeAndArticulationTests.cs).

Alcance de validación de esta revisión: lectura de código, documentación y pruebas existentes; consulta de referencias primarias para segmentación. No se ejecutaron nuevos tests, benchmarks ni playtests y no se modificó código de generación.
