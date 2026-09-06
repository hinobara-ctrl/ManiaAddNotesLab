# Plan de integración — Algoritmo derivado del lenguaje del mapper

## Propósito y alcance

Este documento transforma la idea de un algoritmo completamente derivado del mapa en una propuesta de integración concreta para `ManiaAddNotesLab`.

No ordena implementar inmediatamente todas las fases ni afirma que las hipótesis estilísticas estén demostradas. Su función es permitir retomar el trabajo más adelante con una arquitectura, un orden experimental y criterios de aceptación claros.

La dirección perseguida es:

> El usuario decide cuánto intervenir; el mapa original demuestra qué transformaciones pertenecen a su lenguaje; la geometría decide cuáles son físicamente posibles.

La meta no es eliminar todos los números del programa. Siempre existirán cantidades relacionadas con formato, precisión, complejidad computacional y representación. La meta es eliminar como valores arbitrarios los números que cambien el estilo musical o jugable del resultado.

---

## 1. Situación actual

El proyecto ya contiene una base especialmente favorable para esta evolución:

- `OriginalObjects` es la fuente inmutable de estilo.
- Los sintéticos colocados afectan la geometría posterior, pero no enseñan densidad, gaps, duraciones ni contexto.
- `OriginalChartAnalysis` convierte el chart una vez a una representación temporal en beats.
- `LaneGeometryIndex` separa la legalidad física de la política probabilística.
- `BeatTimeline` conserva vocabulario relativo, offsets, divisiones no estándar y cambios de BPM.
- Existen analizadores locales para densidad, lane gap y retrigger gap.
- La generación es determinista por seed.
- La articulación usa una segunda pasada y un RNG derivado, por lo que no modifica las decisiones de la primera pasada.
- Web, CLI, trace y CSV ya permiten hacer comparaciones A/B masivas.
- Las cantidades verticales principales cuentan con versiones normalizadas por `KeyCount`.
- El parser admite 1K–18K sin tablas de política específicas por keymode.
- La instrumentación ya distingue objetos de interacciones mediante heads y releases añadidos.

Esto significa que no es necesario reconstruir el parser, el writer, la timeline ni la geometría. El cambio principal pertenece a la capa situada entre el análisis del chart y la selección de candidatos.

### 1.1 Qué sigue siendo heurístico

En `AddNotesOptions` todavía existen valores que describen estilo o deciden qué evidencia se considera relevante:

| Familia | Ejemplos actuales | Problema futuro |
|---|---|---|
| Densidad vertical | `DensityGraceColumns`, `DensityDecayPerColumn` | Nosotros decidimos cuándo comienza una wall y cuánto penalizarla |
| Densidad contextual | ventanas micro/contexto, ratio y factores de burst | Nosotros definimos qué es un pico y cuánto dura |
| Contexto LN | `LnWindowBeats`, caída por distancia, afinidad source | Nosotros decidimos alcance y ponderación |
| Lane gap | ventana, soporte mínimo y fallback fijo | Puede inventarse una separación ausente en el mapa |
| Interiores | duración mínima, contexto mínimo, anchors mínimos y cap | La elegibilidad depende de umbrales manuales |
| Articulación | saturación máxima, soporte, ventana y cap | Una estructura emergente continúa limitada manualmente |

Estos controles son útiles durante investigación. No deben desaparecer antes de que su reemplazo tenga cobertura observable y resultados verificables.

---

## 2. Frontera conceptual de responsabilidades

La arquitectura futura debe distinguir cuatro clases de decisiones.

### 2.1 Intención del usuario

Puede continuar siendo externa:

- chart de entrada;
- `AddChance` o intensidad solicitada;
- seed;
- rango seleccionado;
- opciones de exportación y diagnóstico.

La intensidad dice cuánto desea modificar el usuario, no cómo mapea el autor.

### 2.2 Invariantes

Son reglas permanentes aunque utilicen valores o comparaciones:

- nunca modificar el archivo fuente;
- no overlap;
- duración positiva;
- head anterior al release;
- output `.osu` válido;
- determinismo para una misma entrada, configuración y seed;
- solo originales enseñan estilo durante una ejecución;
- sintéticos no crean oportunidades recursivas;
- el rango selecciona oportunidades, pero el chart completo aporta contexto;
- los cambios de BPM y offsets se respetan;
- una transformación ilegal hace `SKIP`.

### 2.3 Evidencia derivada del mapper

Debe reemplazar gradualmente las constantes de estilo:

- tamaños de chord y transiciones de ocupación;
- escalas temporales y regímenes de densidad;
- gaps por lane;
- duraciones LN y releases;
- distancias contextuales relevantes;
- anchors interiores;
- frecuencia y spacing de articulaciones/retriggers;
- patrones que son comunes, raros o ausentes.

### 2.4 Detalles de implementación

Pueden permanecer internos siempre que no alteren el estilo:

- tolerancias para comparar `decimal`;
- redondeo al milisegundo requerido por `.osu`;
- umbrales de cambio de estrategia de búsqueda con resultado exacto equivalente;
- tamaños iniciales de colecciones;
- claves de cache;
- versión de serialización del perfil.

La prueba conceptual para cada constante es:

> Si cambiarla modifica de manera perceptible qué patrones aparecen, debe ser intención del usuario, una invariancia justificada o un valor derivado del mapa.

---

## 3. Arquitectura objetivo

```text
.osu
  │
  ▼
Parser + BeatTimeline
  │
  ▼
OriginalChartAnalysis ───────────────┐
  │                                 │
  ▼                                 ▼
MapperEvidenceProfile         FrozenGeometry
  │                                 │
  ├── global                       │
  ├── secciones                    │
  ├── vecindarios/eventos          │
  └── cobertura/confianza          │
  │                                 │
  ▼                                 │
OpportunityBuilder                  │
  │                                 │
  ▼                                 │
CandidateFactory                    │
  │                                 │
  ▼                                 │
EvidenceResolver + Backoff          │
  │                                 │
  ▼                                 │
MapperSupport(candidate) ◄──────────┘
  │
  ▼
Budget/Probability Selector
  │
  ▼
CurrentGeometry validation + insertion
  │
  ▼
Articulation pass
  │
  ▼
Writer + Trace + CSV
```

La separación entre `FrozenGeometry` y `CurrentGeometry` debe conservarse:

- la geometría congelada describe el estado original y permite calcular evidencia;
- la geometría mutable evita que dos sintéticos se superpongan;
- ninguna colocación sintética modifica el perfil durante esa run.

### 3.1 Compatibilidad durante la migración

El motor actual debe permanecer disponible como política `LegacyExperimental`. La política nueva puede llamarse provisionalmente `MapperDerived`.

```csharp
public enum GenerationPolicy
{
    LegacyExperimental,
    MapperDerivedShadow,
    MapperDerived
}
```

`MapperDerivedShadow` calcula lo que habría decidido el sistema nuevo, pero devuelve exactamente el output de `LegacyExperimental`. Esto permite comparar cobertura y decisiones sin poner en riesgo los resultados.

---

## 4. `MapperEvidenceProfile`

El perfil debe ser inmutable, construido una vez por chart y consultable sin RNG. No debe incluir objetos sintéticos ni depender de `AddChance`.

Una forma conceptual, no definitiva, sería:

```csharp
public sealed record MapperEvidenceProfile(
    string ProfileVersion,
    int KeyCount,
    ChartEvidence Global,
    IReadOnlyList<SectionEvidence> Sections,
    ChordVocabulary Chords,
    DensityRegimeVocabulary DensityRegimes,
    LnShapeVocabulary LnShapes,
    GapVocabulary LaneGaps,
    RetriggerVocabulary Retriggers,
    AnchorVocabulary Anchors,
    TimingVocabulary Timing,
    EvidenceCoverage Coverage);
```

No conviene construir una gran clase que exponga diccionarios sin semántica. Cada vocabulario debería ofrecer consultas específicas y devolver tanto alternativas como procedencia.

```csharp
public sealed record EvidenceResolution<T>(
    IReadOnlyList<WeightedEvidence<T>> Values,
    EvidenceScope Scope,
    int SampleCount,
    double Coverage,
    ResolutionStatus Status);

public enum EvidenceScope
{
    Event,
    Local,
    Section,
    Global
}

public enum ResolutionStatus
{
    Supported,
    BackedOff,
    NoEvidence
}
```

La respuesta debe explicar no solo el resultado, sino por qué pudo producirlo. Esto es indispensable para depurar un sistema empírico.

### 4.1 Evidencia global

Resume el vocabulario completo del chart:

- histograma de heads simultáneos;
- histograma de ocupación relativa;
- transiciones observadas entre tamaños de chord;
- spacing entre timestamps con heads;
- duraciones LN;
- releases y offsets relativos;
- gaps same-lane;
- release→head gaps;
- frecuencia de anchors internos;
- combinaciones de duración, gap y ocupación.

No debe ser la primera opción en todas las consultas. Es el último nivel antes de `SKIP`.

### 4.2 Evidencia de sección

Captura cambios de lenguaje dentro del mismo chart. Una canción puede alternar rice, LN, breaks y clímax. Mezclar todo globalmente puede autorizar patrones correctos en una parte pero extraños en otra.

Cada sección debería almacenar:

- intervalo temporal;
- densidad y ocupación representativas;
- mezcla tap/LN;
- vocabulario rítmico;
- vocabulario LN;
- lane participation;
- confianza de sus límites;
- cantidad de evidencia disponible.

Los límites de sección no deben confundirse con timing sections del archivo. Un cambio BPM puede o no coincidir con un cambio estilístico.

### 4.3 Evidencia local o de evento

Es la evidencia más específica alrededor de una oportunidad:

- estado vertical del timestamp;
- parent LN, si existe;
- anchors contenidos;
- objetos vecinos;
- sección activa;
- ritmo inmediato;
- eventos equivalentes cercanos.

En vez de preguntar “¿cuántos objetos existen dentro de ±4 beats?”, debería preguntarse “¿qué observaciones equivalentes existen en la misma estructura o régimen?”.

### 4.4 Cobertura

El perfil debe poder declarar sus límites. Algunos ejemplos:

```text
ChordVocabularyCoverage: 0.94
LaneGapCoverage: 0.81
LnShapeCoverage: 0.76
InteriorAnchorCoverage: 0.18
RetriggerCoverage: 0.07
```

Los valores no representan calidad del mapa ni probabilidad de éxito. Describen qué proporción de las oportunidades observadas puede resolver cada vocabulario con evidencia.

Una cobertura baja es información válida. No debe ocultarse mediante defaults.

---

## 5. Backoff sin invención

La regla general será:

```text
EVENT/LOCAL
    ↓ si no hay evidencia compatible
SECTION
    ↓ si no hay evidencia compatible
GLOBAL
    ↓ si no hay evidencia compatible
SKIP
```

### 5.1 “No hay evidencia” no significa “muestra pequeña” automáticamente

Una sola observación es evidencia, pero su autoridad debe ser limitada. En lugar de un corte universal como `support >= 2`, el resolver debe conservar:

- frecuencia;
- diversidad de ubicaciones;
- procedencia;
- dispersión;
- compatibilidad con el estado actual;
- si el dato es un outlier respecto del resto del mapa.

Una observación única repetida por copia exacta en un jack local no equivale a una observación única distribuida en varias frases. La estructura del soporte importa además del conteo.

### 5.2 Evitar fallbacks silenciosos

Toda expansión de alcance debe aparecer en trace y CSV:

```text
requested=LocalLaneGap
resolvedScope=Section
localSamples=0
sectionSamples=6
globalSamples=19
status=BackedOff
```

Si termina en `SKIP`, debe registrarse la familia de evidencia ausente. Un aumento de skips después de eliminar defaults no debe diagnosticarse como fallo geométrico.

### 5.3 La sección no es un “default disfrazado”

El nivel sección debe contener observaciones reales. No puede rellenarse con valores predefinidos ni interpolaciones cuyo efecto estilístico sea equivalente a inventar una convención.

---

## 6. Auditoría de números mágicos

Antes de cambiar comportamiento se debe producir un inventario mecánico y luego clasificarlo manualmente.

Formato recomendado:

| Nombre/literal | Ubicación | Efecto | Categoría | Reemplazo previsto | Estado |
|---|---|---|---|---|---|
| `Chance` | opciones/UI | intensidad | `USER_INTENT` | conservar | aceptado |
| `Epsilon` | Core | comparación decimal | `IMPLEMENTATION_ONLY` | conservar y documentar | revisar equivalencia |
| 1K–18K | parser/validación | formato soportado | `FORMAT_OR_INVARIANT` | conservar | aceptado |
| `DensityGraceColumns` | densidad vertical | estilo | `MAPPER_DERIVED_CANDIDATE` | vocabulario de chords | pendiente |
| `DensityDecayPerColumn` | densidad vertical | estilo | `MAPPER_DERIVED_CANDIDATE` | soporte empírico del estado resultante | pendiente |
| ventanas de densidad | densidad temporal | estilo | `MAPPER_DERIVED_CANDIDATE` | regímenes temporales | pendiente |
| factores burst | densidad temporal | estilo | `MAPPER_DERIVED_CANDIDATE` | rareza/soporte de régimen | pendiente |
| `FallbackMinimumLaneGapBeats` | geometría LN | estilo/seguridad mezclados | `MAPPER_DERIVED_CANDIDATE` | backoff→SKIP | pendiente |
| `SourceAffinityMultiplier` | selección LN | estilo | `MAPPER_DERIVED_CANDIDATE` | frecuencia observada | pendiente |
| caída por distancia | selección LN | estilo | `MAPPER_DERIVED_CANDIDATE` | relevancia temporal aprendida | pendiente |
| mínimos/caps interiores | oportunidades LN | estilo/contención | `MAPPER_DERIVED_CANDIDATE` | capacidad estructural | pendiente |
| cap de articulación | segunda pasada | contención experimental | `MAPPER_DERIVED_CANDIDATE` | anchors+spacing+geometría | pendiente |

La auditoría debe incluir literales internos que no aparecen en `AddNotesOptions`, por ejemplo límites de búsqueda, redondeos, percentiles o switches basados en beats. Encontrar un número no demuestra que sea incorrecto; obliga a justificar su categoría.

---

## 7. Modelo vertical empírico

### 7.1 Qué debe aprender

Para cada timestamp original se puede registrar:

- `SimultaneousHeadColumns`;
- `HeadOccupancyRatio`;
- `HeldLnColumns`;
- `HeldRatio`;
- `NonHeldColumns`;
- `LegalLaneRatio` en la geometría original;
- tipo de los heads: tap/LN;
- estado anterior y posterior;
- sección y régimen.

La pregunta para una oportunidad no sería “¿cuántas columnas superan la gracia?”, sino:

> Si el estado original es `k`, ¿qué respaldo tiene el estado resultante `k+1` en un contexto equivalente de este mapa?

### 7.2 Distribuciones y transiciones

Conviene conservar dos modelos relacionados:

1. Distribución de estados: cuán frecuente es cada tamaño o ratio de chord.
2. Distribución de transiciones: qué tan natural es aumentar un estado determinado en una unidad.

Ejemplo conceptual:

```text
P(result=2 heads | local regime) = alta
P(result=3 heads | local regime) = media
P(result=4 heads | local regime) = baja
P(result=5 heads | local regime) = no observada
```

El soporte de `5→6` debe ser menor que el de `2→3` aunque ambos agreguen una columna. Esto soluciona directamente el problema original de paredes antinaturales sin depender de una curva exponencial elegida manualmente.

### 7.3 Generalización entre keymodes

El perfil debe guardar tanto cantidades discretas como proporciones:

- las lanes son discretas para geometría;
- el ratio permite comparar estados conceptualmente equivalentes;
- la evidencia principal sigue perteneciendo al mismo chart y su mismo keymode.

No se requiere extrapolar evidencia de un chart 4K hacia uno 7K durante la generación. La normalización sirve para que el algoritmo sea único, los diagnósticos sean comparables y las reglas no contengan ramas `if 4K/7K/10K`.

### 7.4 Estados no observados

Ausencia de un chord no siempre significa prohibición absoluta: un chart corto puede no tener muestras suficientes. Durante la fase experimental se deben comparar al menos tres interpretaciones:

- estricta: estado no observado = `SKIP`;
- jerárquica: local ausente → sección → global → `SKIP`;
- suavizada solo con estados vecinos observados, sin introducir una distribución externa.

La tercera opción es la más riesgosa porque el suavizado puede transformarse en un prior estilístico oculto. No debe adoptarse sin A/B explícito.

---

## 8. Densidad temporal y segmentación adaptativa

Esta es probablemente la fase de investigación más difícil.

### 8.1 Separar spacing, densidad y régimen

Son conceptos distintos:

- spacing: distancias entre eventos;
- densidad instantánea: interacción por unidad musical;
- régimen: tramo donde las propiedades estadísticas son relativamente estables;
- transición: cambio persistente de un régimen a otro;
- burst: desviación breve respecto de su régimen.

El sistema actual los aproxima mediante ventanas fijas. El nuevo modelo debe intentar descubrirlos desde la serie original.

### 8.2 Serie temporal base

Una representación útil por eventos, no necesariamente por bins fijos, podría almacenar:

```text
beat
head count
release count
active holds
tap/LN mix
delta desde evento previo
delta hasta evento siguiente
```

Las duraciones recurrentes y periodicidades del mapa pueden proponer escalas candidatas. Por ejemplo, si las autocorrelaciones o run lengths muestran estructuras repetidas a 0.5, 1 y 8 beats, esas escalas pasan a ser vocabulario observado.

### 8.3 Posibles estrategias de segmentación

Se deben evaluar estrategias interpretables antes de algoritmos complejos:

- cambios persistentes en spacing mediano;
- cambios en densidad de heads/releases;
- cambios en proporción tap/LN;
- límites musicales ya visibles por silencios relativos al vocabulario del propio mapa;
- change-point detection sobre features normalizadas.

Aunque un detector no exponga un knob de estilo, siempre tendrá criterios internos. La pregunta correcta no es si posee números, sino si esos números cambian la preferencia estilística o solo controlan estabilidad estadística.

### 8.4 Definición emergente de burst

Un burst debería ser una excursión breve y rara respecto del régimen que lo contiene. Una sección sostenida densa debe convertirse en su propio régimen y dejar de recibir penalización por ser “más densa que el contexto global”.

Esto conserva el hallazgo de la política actual —frenar picos breves y no castigar densidad sostenida— pero reemplaza las escalas `1/2/3/4 beats` por duraciones observadas.

---

## 9. Lane gaps y retriggers

### 9.1 Lane gap

El perfil debe registrar gaps originales entre objetos consecutivos de una misma lane, incluyendo:

- tipo de transición: tap→tap, tap→LN, LN→tap, LN→LN;
- sección;
- lane o grupo de lanes equivalentes;
- spacing/régimen;
- frecuencia y dispersión;
- posición musical relativa cuando sea relevante.

La consulta debe buscar primero transiciones del mismo tipo y contexto. Si no encuentra evidencia, amplía a sección y luego a global. Sin evidencia global compatible, la LN se omite.

Eliminar el fallback fijo no elimina la seguridad. `LaneGeometryIndex` continúa impidiendo overlap; el gap aprendido conserva la separación estilística adicional.

### 9.2 Retrigger

Para articulación, el vocabulario relevante es específicamente:

```text
LN release → siguiente head en la misma lane
```

El backoff recomendado es:

```text
misma lane + misma sección/régimen
→ otras lanes equivalentes en sección
→ misma lane global
→ global cross-lane
→ SKIP
```

Cada expansión debe ser visible. Un fallback cross-lane no debe parecer evidencia same-lane en los informes.

### 9.3 Evidencia rara

En lugar de exigir dos repeticiones universales, se puede reducir la autoridad de un gap raro mediante su peso y procedencia. Sin embargo, para operaciones destructivas sobre una parent —como articulación— puede ser razonable exigir evidencia más fuerte que para seleccionar una LN sintética normal. Esa asimetría debe tratarse como política de seguridad o riesgo, no ocultarse como una preferencia musical.

---

## 10. Vocabulario de formas LN

### 10.1 Separar formas de ubicaciones absolutas

El perfil debe distinguir:

- duración relativa en beats;
- release absoluto observado;
- offset dentro de la estructura rítmica;
- relación con anchors cercanos;
- tipo de sección;
- estado de lanes durante la duración.

Una duración trasladada y un release original exacto son dos tipos de evidencia diferentes. Ambos pueden coincidir en un mismo candidato y reforzarlo.

### 10.2 Sustituir afinidad y distancia manuales

La afinidad hacia la forma source no necesita un multiplicador fijo si la forma ya está representada por su frecuencia. Si copiar la duración source es muy típico, aparecerá repetidamente en el vocabulario. Si es excepcional, no recibirá autoridad artificial.

La relevancia por distancia puede derivarse de:

- periodicidad dominante de frases;
- spacing típico entre LNs relacionadas;
- límites de sección;
- recurrencia de formas en posiciones equivalentes;
- caída observada de similitud, no una pendiente elegida.

La distancia no debería decidir por sí sola que un candidato es válido; solo debe ayudar a ordenar evidencia ya compatible.

### 10.3 Preservar timing exacto

`BeatTimeline` debe seguir siendo la única autoridad de conversión. El perfil trabaja en beats `decimal`; los releases originales conservan el milisegundo original y la escritura final redondea solo donde el formato lo requiere.

Esta invariancia no debe cambiar durante la migración.

---

## 11. Oportunidades interiores LN

### 11.1 De umbrales a capacidad estructural

Una parent debería ser candidata cuando su interior contiene una o más transformaciones respaldadas y geométricamente posibles. No porque supere por sí sola una duración fija.

Una forma de construir oportunidades es:

1. Obtener anchors originales estrictamente dentro de `[S,E]`.
2. Clasificar qué papel demuestra cada anchor: head, release, coincidencia repetida, retrigger.
3. Consultar formas LN compatibles desde ese anchor.
4. Descartar candidatos cuyos extremos no caben dentro de la parent cuando corresponda.
5. Consultar gap, régimen y geometría.
6. Crear una oportunidad por estructura respaldada, no por una cuota fija.

Una LN corta normalmente no producirá oportunidades porque no contendrá anchors y formas válidas. Así desaparece la necesidad de definir universalmente “LN larga”.

### 11.2 Controlar explosión combinatoria

Quitar `MaxInteriorOpportunitiesPerSource` sin reemplazo puede crear demasiados candidatos. El reemplazo no debe ser otro cap estilístico oculto. Algunas opciones de contención estructural:

- fusionar candidatos equivalentes por `(anchor, release, lane-set)`;
- dominancia: descartar candidatos con igual resultado y menor evidencia;
- incompatibilidad: construir grupos donde solo uno puede sobrevivir;
- presupuesto de intervención compartido;
- selección global por soporte antes de consultar geometría mutable.

Los límites de rendimiento pueden existir, pero deben producir el mismo resultado lógico que procesar toda la colección o fallar explícitamente. No deberían cortar “los primeros N” y alterar estilo por orden incidental.

### 11.3 Parent como límite, no como maestra

Debe conservarse el hallazgo actual: la parent es metadata y frontera geométrica, no una observación añadida artificialmente al contexto. Así se evita que cada oportunidad interior vote automáticamente por llegar al release de la parent.

---

## 12. Articulación emergente

La segunda pasada actual es una buena frontera arquitectónica y debe conservarse.

### 12.1 Trigger futuro

La articulación puede surgir cuando:

1. existía una oportunidad interior respaldada;
2. ganó el mecanismo de intensidad o presupuesto;
3. existían candidatos LN normales respaldados;
4. ninguno era legal por ocupación de holds;
5. el anchor es un head original estricto dentro de la parent;
6. existe vocabulario original de release→repress;
7. los dos segmentos resultantes son válidos;
8. la parent aún no fue transformada de manera incompatible.

En este flujo, la saturación se demuestra por la causa geométrica del fallo. Ya no es necesario decidir de antemano que `NonHeldColumns <= 1` siempre significa Full-LN suficiente.

### 12.2 Varias articulaciones

El cap de una articulación por parent es prudente en la fase actual. Para retirarlo se necesita resolver un conjunto de cortes compatibles dentro de `[S,E]`:

```text
S < R1 < H1 < R2 < H2 < E
```

Cada segmento debe conservar duración válida, spacing respaldado y geometría. Esto deja de ser una selección local simple y se acerca a un problema de interval scheduling ponderado.

Una solución interpretable sería construir todas las articulaciones compatibles y elegir el conjunto no solapado con mayor soporte total, limitado además por el presupuesto de intervención. Hasta que exista esta selección, retirar el cap sería prematuro.

### 12.3 Semántica de interacción

La evaluación debe continuar usando:

```text
AddedHeads
AddedReleases
AddedInteractions = AddedHeads + AddedReleases
```

Una articulación modifica la interacción jugable aunque no incremente el número neto de objetos de la misma forma que una LN paralela. Ninguna de estas métricas debe etiquetarse automáticamente como dificultad.

---

## 13. `MapperSupport`

### 13.1 Objetivo

`MapperSupport(candidate)` debe responder cuánto respaldo tiene una transformación dentro del lenguaje original. No debe confundirse con probabilidad final ni legalidad geométrica.

Dimensiones posibles:

```text
ChordSupport
TemporalRegimeSupport
LnShapeSupport
LaneGapSupport
AnchorSupport
RetriggerSupport
GeometrySupport
```

### 13.2 No sumar multiplicadores prematuramente

Una fórmula como:

```text
score = chord × temporal × shape × gap × anchor
```

parece limpia, pero presupone independencia y escala comparable. Un único valor cercano a cero puede anular evidencia fuerte aunque represente simplemente baja cobertura.

Antes de unificar, cada dimensión debe poder producir:

- supported / unsupported / unknown;
- scope usado;
- frecuencia;
- rareza dentro de su distribución;
- razones de incompatibilidad;
- geometría legal.

El primer `MapperSupport` puede ser una estructura explicativa y un orden lexicográfico, no necesariamente un `double`.

```csharp
public sealed record MapperSupport(
    SupportDecision Decision,
    ChordSupport Chord,
    TemporalSupport Temporal,
    LnShapeSupport? LnShape,
    GapSupport? Gap,
    AnchorSupport? Anchor,
    RetriggerSupport? Retrigger,
    GeometrySupport Geometry,
    IReadOnlyList<string> Reasons);
```

### 13.3 Separar tres preguntas

Para cada candidato:

1. ¿Está respaldado por el mapper?
2. ¿Es geométricamente legal ahora?
3. ¿Debe seleccionarse con la intensidad solicitada?

Mezclar estas preguntas vuelve imposible saber si una colocación faltó por estilo, geometría o presupuesto.

---

## 14. Evolución de `AddChance`

### 14.1 Modelo actual

Actualmente cada oportunidad realiza un Bernoulli con una chance efectiva modificada por factores. Esto es simple y determinista, pero puede gastar muchas tiradas en oportunidades imposibles o producir distribuciones desiguales según cuántas oportunidades genere cada subsistema.

### 14.2 Modelo de presupuesto

Una semántica futura puede ser:

> ADD 50 solicita utilizar aproximadamente la mitad de las oportunidades respaldadas y compatibles, priorizando las que mejor representan el mapa.

Pipeline conceptual:

1. Enumerar oportunidades desde originales.
2. Construir y deduplicar candidatos.
3. Resolver evidencia y geometría congelada.
4. Agrupar incompatibilidades.
5. Calcular presupuesto desde intensidad y cantidad/costo de interacciones.
6. Ordenar por soporte con desempate aleatorio derivado de seed.
7. Seleccionar un conjunto compatible.
8. Revalidar contra `CurrentGeometry` al materializar.

### 14.3 Qué debería presupuestarse

No está decidido si el presupuesto debe contar:

- oportunidades;
- objetos añadidos;
- heads;
- releases;
- `AddedInteractions`;
- costo relativo por tipo de transformación.

Presupuestar objetos favorece taps sobre LNs; presupuestar interacciones trata una LN como dos eventos y una articulación como interacción adicional. La elección afecta el comportamiento y necesita experimento, no solo diseño.

### 14.4 Mantener estabilidad por seed

Cada oportunidad debe poseer una identidad estable derivada de datos originales, por ejemplo:

```text
chart fingerprint
source sequence
opportunity kind
anchor time
candidate form
lane
```

El desempate debe usar sub-seeds por identidad. Agregar un candidato nuevo a una sección no debería cambiar aleatoriamente todas las decisiones anteriores por desplazar el stream del RNG.

---

## 15. Instrumentación necesaria

### 15.1 Perfil read-only

La interfaz diagnóstica debería mostrar:

- número y límites de secciones;
- distribución de chords absoluta y relativa;
- regímenes encontrados;
- vocabulario de spacing;
- duraciones/releases LN;
- lane gaps;
- retriggers;
- anchors;
- cobertura local, sección y global;
- familias que terminarían frecuentemente en `SKIP`.

### 15.2 Decisión por oportunidad

El trace futuro debe poder reconstruir:

```text
OpportunityId
Source
Section
CandidateKind
EvidenceScope por dimensión
SampleCount por dimensión
SupportDecision
FrozenGeometryResult
BudgetRank / probability
CurrentGeometryResult
FinalDecision
```

### 15.3 Métricas agregadas

Además de las actuales:

- oportunidades respaldadas;
- oportunidades sin evidencia;
- backoffs local→section;
- backoffs section→global;
- skips por vocabulario;
- skips geométricos;
- candidatos dominados/deduplicados;
- distribución de soporte de colocados y omitidos;
- desviación entre presupuesto solicitado y utilizado;
- interacciones añadidas por familia;
- cobertura por sección;
- estados verticales nuevos no observados en el original;
- formas LN nuevas no observadas;
- gaps nuevos no observados.

Una meta fuerte para el modo estricto sería que los tres últimos permanezcan en cero, salvo transformaciones cuya composición esté explícitamente demostrada.

### 15.4 Perfil serializable

Conviene permitir exportar un `mapper-profile.json` para comparar versiones del analizador sin regenerar `.osu`. Debe incluir:

- fingerprint del input;
- versión del perfil;
- versión del algoritmo;
- unidades;
- distribuciones y cobertura;
- ninguna ruta privada necesaria para interpretarlo.

No debe usarse automáticamente un perfil cuyo fingerprint no coincide con el chart.

---

## 16. Estrategia de migración por fases

### Fase 0 — Congelar baseline

Antes del refactor:

- conservar los 121 tests y ampliar el conteo si ya cambió;
- guardar CSV de charts representativos con varias seeds;
- conservar checksums de outputs seleccionados;
- registrar configuración completa;
- separar resultados estructurales de opiniones de playtesting.

Salida: baseline reproducible.

### Fase 1 — Magic Number Audit

No cambiar comportamiento. Inventariar opciones y literales del Core, CLI, Web y experimentos. Clasificar cada uno y documentar dudas.

Salida: tabla completa, sin constantes estilísticas invisibles.

### Fase 2 — Perfil mínimo en sombra

Crear `MapperEvidenceProfile` con:

- vocabulario global de chords;
- duraciones/releases LN;
- lane gaps;
- retriggers;
- timing vocabulary;
- cobertura.

El output debe seguir idéntico bit a bit al baseline.

Salida: perfil exportable y pruebas de que solo usa originales.

### Fase 3 — Backoff y eliminación de fallbacks fáciles

Activar, uno por uno:

- lane gap local→section/global→SKIP;
- retrigger equivalente;
- frecuencia LN sin `SourceAffinityMultiplier`;
- resolución de evidencia sin soporte mínimo binario.

Cada sustitución mantiene toggle A/B temporal.

Salida: ningún release/gap inventado por esos subsistemas.

### Fase 4 — Modelo vertical empírico

Construir soporte de estado y transición de chord. Ejecutar primero en sombra, luego A/B contra la curva actual.

Validar especialmente:

- 5→6 y near-full;
- secciones LN-held donde las tails no deben contarse como heads nuevos;
- 4K–10K equivalentes;
- charts con casi ningún chord;
- charts chord-heavy.

Salida: protección anti-wall sin `GraceColumns` ni `Decay` estilísticos.

### Fase 5 — Secciones y regímenes temporales

Implementar segmentación y mostrarla antes de usarla. Comparar límites detectados con inspección humana. Luego reemplazar ventanas/factores de burst.

Salida: bursts breves diferenciados de densidad sostenida mediante escalas observadas.

### Fase 6 — Contexto y pesos LN derivados

Sustituir ventana fija, caída de distancia y afinidad por consultas al vocabulario de sección/evento.

Salida: candidatos LN ordenados por evidencia observable, sin multiplicadores manuales.

### Fase 7 — Interiores derivados

Construir oportunidades desde anchors/formas compatibles y deduplicación estructural. Mantener el cap antiguo como control hasta demostrar que la selección por compatibilidad lo reemplaza.

Salida: número de oportunidades explicado por estructura, no por duración/cap.

### Fase 8 — Articulación emergente

Reemplazar el trigger de saturación por causalidad geométrica. Investigar selección de múltiples cortes compatibles.

Salida: articulaciones producidas solo cuando la vía normal está respaldada pero físicamente bloqueada.

### Fase 9 — `MapperSupport`

Unificar el reporte y orden de evidencia. No eliminar diagnósticos por dimensión.

Salida: decisión explicable y estable para cada candidato.

### Fase 10 — Presupuesto de intensidad

Comparar Bernoulli actual con presupuesto por oportunidades, objetos e interacciones. Mantener semántica elegida documentada.

Salida: intensidad predecible y distribución priorizada por soporte.

### Fase 11 — Zero-config

Ocultar knobs estilísticos solo después de que sus reemplazos estén aceptados. Mantenerlos en una pantalla de compatibilidad o laboratorio mientras existan resultados antiguos que reproducir.

Salida final:

```text
Chart
Add %
Seed
Range
Generate
```

Panel de perfil y decisiones en modo read-only.

---

## 17. Estrategia de pruebas

### 17.1 Tests unitarios del perfil

- se construye solo desde `OriginalObjects`;
- insertar sintéticos no modifica fingerprint ni vocabularios;
- histogramas suman correctamente;
- estados verticales usan el `KeyCount` real;
- cambios BPM conservan duraciones relativas;
- secciones cubren el chart sin solaparse de forma inválida;
- backoff devuelve scope y muestras correctos;
- sin evidencia retorna `NoEvidence`, no un default.

### 17.2 Tests metamórficos

Son especialmente valiosos para este diseño:

- trasladar todo el chart en milisegundos conserva evidencia en beats;
- cambiar BPM preservando estructura en beats conserva el perfil musical;
- permutar lanes conserva distribuciones que no dependen de handedness;
- agregar un objeto sintético al output no altera el perfil de la run original;
- repetir una sección aumenta soporte de su vocabulario sin crear formas nuevas;
- una estructura equivalente normalizada en varios keymodes produce decisiones conceptualmente equivalentes.

### 17.3 Tests de invariantes del generador

- no overlap;
- LN positiva y snapped según vocabulario;
- source intacta;
- determinismo;
- rango correcto;
- output reparseable;
- ninguna forma/gap no respaldado en modo estricto;
- pass 1 idéntica al activar solo una segunda pasada que no debe retroalimentarla.

### 17.4 A/B masivo

Por cada fase:

- misma entrada;
- mismas seeds;
- mismo rango e intensidad;
- una política cambiada;
- métricas objetivas y distribución, no solo promedio;
- inspección AiMod;
- comparación visual;
- playtesting humano separado.

Charts mínimos sugeridos:

- rice limpio;
- chord-heavy;
- LN uniforme;
- LN con duraciones mixtas;
- Full/Near-Full LN;
- timing 1/5 y 1/10;
- BPM variable y offsets;
- chart corto con evidencia escasa;
- estilos con secciones claramente distintas;
- 4K, 7K y al menos un keymode alto.

### 17.5 Golden tests con cautela

Los outputs exactos por seed son útiles para detectar cambios accidentales, pero una mejora deliberada los invalida. Deben etiquetarse por versión de política y acompañarse de invariantes semánticos. El objetivo no es congelar para siempre una secuencia aleatoria incidental.

---

## 18. Criterios de aceptación por fase

Una fase no debería activarse por defecto hasta cumplir:

### Correctitud

- todos los tests existentes pasan;
- no aparecen overlaps ni tiempos inválidos;
- AiMod no muestra avisos nuevos atribuibles al cambio;
- input nunca se sobrescribe;
- resultados deterministas.

### Explicabilidad

- cada decisión declara evidencia y scope;
- cada `SKIP` distingue falta de evidencia, presupuesto y geometría;
- los fallbacks son contabilizables;
- ningún default estilístico aparece de manera silenciosa.

### Cobertura

- se conoce el porcentaje de oportunidades resolubles;
- la cobertura se observa por chart y sección;
- una cobertura baja no se maquilla mediante invención;
- se prueba al menos un chart escaso.

### Portabilidad

- no se agregan ramas por keymode;
- los ratios se comparan correctamente;
- la geometría continúa siendo discreta;
- existen fixtures equivalentes en varios K.

### Naturalidad

- A/B visual aprobado en charts reales;
- playtesting documentado;
- resultados raros se investigan con trace;
- las conclusiones humanas no se presentan como hechos estructurales.

---

## 19. Riesgos y mitigaciones

### 19.1 Charts con poca evidencia

Riesgo: demasiados `SKIP`, especialmente en mapas cortos o muy uniformes.

Mitigación:

- mostrar cobertura antes de ejecutar;
- backoff jerárquico explícito;
- permitir que ADD alcance menos de lo solicitado y explicarlo;
- no inventar patrones para cumplir una cuota.

### 19.2 Sobreajuste al mapa

Riesgo: copiar demasiado literalmente errores, rarezas o una única frase.

Mitigación:

- distinguir frecuencia de diversidad estructural;
- medir dispersión por secciones;
- no tratar una repetición local como autoridad global;
- conservar rareza como peso, no como prohibición automática.

### 19.3 El mapa original puede ser inconsistente

Riesgo: el perfil aprende convenciones contradictorias.

Mitigación:

- almacenar distribuciones en vez de una única regla;
- mostrar multimodalidad;
- condicionar por sección/régimen;
- hacer `SKIP` cuando no existe una resolución compatible clara.

### 19.4 “Cero números” puede convertirse en una ilusión

Riesgo: esconder preferencias en thresholds estadísticos, suavizados o modelos complejos.

Mitigación:

- auditoría continua;
- documentar qué efecto tiene cada parámetro interno;
- mantener algoritmos interpretables;
- A/B de priors o smoothing;
- clasificar explícitamente valores de estabilidad versus estilo.

### 19.5 Explosión de candidatos

Riesgo: interiores, releases, lanes y múltiples articulaciones producen combinaciones grandes.

Mitigación:

- índices por sección/lane/beat;
- deduplicación temprana;
- eliminación por dominancia;
- resolución de incompatibilidades;
- caches inmutables;
- métricas de candidatos construidos/examinados;
- no escanear todo el chart por candidato.

### 19.6 Cambio semántico de ADD

Riesgo: ADD 50 deja de sentirse comparable con versiones anteriores.

Mitigación:

- versionar la política;
- mantener Bernoulli legacy;
- mostrar presupuesto solicitado y utilizado;
- comparar `AddedInteractions`, no solo objetos;
- documentar el cambio en UI y archivos de resultados.

### 19.7 Dependencia circular

Riesgo: seleccionar candidatos modifica la geometría y, por orden, cambia oportunidades posteriores.

Mitigación:

- perfil y soporte estilístico siempre congelados;
- separar preselección con geometría original de materialización con geometría mutable;
- identidades y sub-seeds estables;
- registrar rechazos tardíos por conflicto sintético.

---

## 20. Rendimiento y almacenamiento

El perfil añade un costo inicial que debería amortizarse durante una run masiva.

Recomendaciones:

- construir un perfil una vez por chart y reutilizarlo entre seeds/chances;
- hacerlo independiente de seed y `AddChance`;
- indexar por lane, sección y beat;
- usar estructuras ordenadas y búsqueda binaria como el Core actual;
- cachear resoluciones puras cuando la clave dependa solo del original;
- no cachear legalidad contra `CurrentGeometry`;
- versionar cache por fingerprint y versión del perfil;
- medir `ProfileBuildMs`, `ProfileLoadMs`, `EvidenceResolveMs` y memoria aproximada;
- verificar que la paralelización de runs comparta solo estructuras inmutables.

Para lotes grandes, reutilizar el perfil puede ser más eficiente que el diseño actual, que reconstruye analizadores por ejecución.

---

## 21. Cambios previstos por componente

### `ManiaAddNotesLab.Core`

Agregar progresivamente:

```text
Evidence/
  MapperEvidenceProfile.cs
  EvidenceResolution.cs
  ChordVocabulary.cs
  DensityRegimeVocabulary.cs
  LnShapeVocabulary.cs
  GapVocabulary.cs
  RetriggerVocabulary.cs
  AnchorVocabulary.cs
  SectionAnalyzer.cs

Generation/
  OpportunityId.cs
  CandidateEvidenceResolver.cs
  MapperSupport.cs
  BudgetSelector.cs
```

No es necesario mover archivos existentes de inmediato. Primero deben introducirse límites claros y tests; un refactor físico posterior puede reducir riesgo.

### `AddNotesEngine`

Debería evolucionar hacia un orquestador:

- recibe chart, opciones, RNG y perfil opcional;
- crea oportunidades;
- consulta policy seleccionada;
- valida/materializa;
- ejecuta segunda pasada;
- produce estadísticas.

Las fórmulas concretas no deberían seguir acumulándose dentro de esta clase.

### CLI

Agregar eventualmente:

- `--policy legacy|shadow|derived`;
- `--profile-output`;
- `--profile-input` con verificación de fingerprint;
- resumen de cobertura y backoff;
- modo estricto.

### Web

Durante investigación:

- selector de política;
- panel del perfil;
- comparación legacy/derived;
- cobertura y razones de `SKIP`;
- descarga del perfil y trace.

En la interfaz final, ocultar los controles estilísticos y conservarlos solo en un laboratorio avanzado versionado.

### Herramienta de experimentos

Debe poder:

- generar perfiles de fixtures;
- comparar perfiles entre versiones;
- ejecutar matriz legacy/shadow/derived;
- verificar identidad del output en shadow;
- calcular distribución de scopes y soporte;
- detectar formas/gaps no observados.

---

## 22. Decisiones de diseño que deben quedar abiertas

No conviene fijarlas antes de obtener datos:

1. Cómo detectar secciones y regímenes sin imponer estilo.
2. Cómo representar confianza sin convertirla en un threshold universal.
3. Si los estados no observados admiten smoothing.
4. Cómo combinar dimensiones de soporte correlacionadas.
5. Qué unidad consume el presupuesto: oportunidades, objetos o interacciones.
6. Cómo balancear familias tap/LN/articulation bajo un mismo presupuesto.
7. Cuándo una evidencia cross-lane es equivalente a same-lane.
8. Cómo detectar lanes o manos con roles distintos sin imponer conocimiento externo.
9. Cómo seleccionar múltiples articulaciones compatibles.
10. Qué nivel de cobertura mínimo justifica activar zero-config.

Estas preguntas deben responderse mediante experimentos reproducibles y playtesting, no por conveniencia de implementación.

---

## 23. Primer incremento recomendado

El primer cambio funcionalmente seguro debería contener solo:

1. `MagicNumberAudit.md` generado desde una búsqueda exhaustiva y revisión manual.
2. `MapperEvidenceProfile` mínimo con vocabularios globales ya disponibles.
3. Fingerprint y versión del perfil.
4. Exportación JSON y resumen en CSV.
5. `MapperDerivedShadow`, sin modificar ningún `.osu` generado.
6. Tests que demuestren que sintéticos, seed y `AddChance` no cambian el perfil.
7. Comparación en los fixtures existentes y en `Spring of Dreams`.

Este incremento responde primero la pregunta decisiva:

> ¿Cuánto del comportamiento que necesitamos puede explicar realmente el mapa, y dónde falta evidencia?

Solo después conviene sustituir la primera heurística.

### Criterio de cierre del primer incremento

- output legacy idéntico antes/después;
- perfil determinista;
- fuente original-only demostrada por tests;
- cobertura visible por familia;
- tiempo de construcción medido;
- backoff todavía no altera decisiones;
- documentación de hallazgos, incluyendo charts donde el perfil sea insuficiente.

---

## 24. Definición de éxito final

El algoritmo puede considerarse conceptualmente terminado cuando:

- la única intención estilística externa sea cuánto añadir;
- toda transformación pueda señalar evidencia original concreta o agregada;
- ausencia de evidencia produzca `SKIP`;
- geometría y seguridad permanezcan independientes del estilo;
- no existan ramas por keymode;
- el resultado sea determinista y válido;
- la intensidad sea interpretable;
- el perfil explique sus límites;
- los controles experimentales ya no sean necesarios para obtener un resultado razonable;
- el playtesting confirme naturalidad en una colección diversa de charts.

La interfaz final podrá ser pequeña porque la complejidad habrá sido trasladada a evidencia observable, no escondida en defaults.

```text
Usuario:   cuánto intervenir
Mapper:    qué lenguaje está permitido
Geometría: qué cabe realmente
Motor:     cómo seleccionar de forma reproducible
```

Esa división mantiene la filosofía original de `ManiaAddNotesLab` y ofrece un camino incremental para convertirla en una implementación verificable.
