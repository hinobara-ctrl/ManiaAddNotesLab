# Plan futuro — Algoritmo completamente derivado del mapa

## Estado de esta idea

Este documento **no corresponde a la fase actual de correcciones** de `ManiaAddNotesLab`.

Su propósito es dejar registrada una dirección futura para el proyecto, a revisar únicamente cuando el algoritmo actual haya sobrevivido a:

- correcciones de rice;
- generalización multi-key;
- LN interior;
- LN Articulation;
- validación estructural;
- playtesting humano.

La idea central es convertir el algoritmo en un sistema donde **ninguna variable de estilo sea elegida arbitrariamente por nosotros**.

---

# 1. Objetivo maestro

La versión final de ADD NOTES debería obedecer esta regla:

> **Todo valor que describa cómo mapea una persona debe nacer del mapa original.**

El algoritmo no debería terminar dependiendo de defaults como:

- `0.65`;
- `2 columnas`;
- `±4 beats`;
- `1.5×`;
- `1/8`;
- `1.25`;
- `3 LNs`;
- `2 anchors`;
- `8 beats`;
- etc.

si esos números tienen influencia directa sobre el estilo del resultado.

La meta no es encontrar “mejores números”.

La meta es **eliminar la necesidad de elegirlos**.

---

# 2. Qué puede seguir siendo externo

No todo debe ser aprendido.

## 2.1 Intención del usuario

Estos parámetros sí pueden seguir siendo externos:

- `AddChance`;
- `Seed`;
- `SelectedRange`.

`AddChance` representa cuánto quiere intervenir el usuario sobre el mapa.

No describe el estilo del mapper.

---

## 2.2 Invariantes

También pueden existir reglas constantes cuando representan seguridad, formato o definición del algoritmo:

- no overlap;
- duraciones positivas;
- determinismo;
- `OriginalObjects` como fuente de estilo;
- sintéticos no enseñan estilo durante la misma ejecución;
- timing relativo al mapa;
- output válido;
- source `.osu` nunca modificado;
- no recursividad salvo que una futura fase lo diseñe explícitamente.

Estas reglas no son “magic numbers de estilo”.

---

# 3. Qué debe desaparecer como configuración final

Toda variable que describa qué es:

- un chord grande;
- una LN larga;
- un burst;
- un gap pequeño;
- una duración normal;
- una distancia relevante;
- una cantidad suficiente de contexto;
- un retrigger típico;
- una cantidad razonable de oportunidades;

debería ser derivada desde `OriginalObjects`.

La página de experimentos puede seguir exponiendo estos knobs mientras investigamos.

La versión final ideal no debería necesitarlos.

---

# 4. MapperEvidenceProfile

Una posible arquitectura futura es construir, al cargar el chart:

```text
OriginalObjects
        ↓
ChartAnalysis
        ↓
MapperEvidenceProfile
```

`MapperEvidenceProfile` sería una representación inmutable del lenguaje real del mapa.

Podría contener distribuciones observadas de:

- tamaños de chord;
- ocupación vertical relativa;
- densidad de heads;
- duración de regiones densas;
- spacing temporal;
- duraciones LN;
- releases LN;
- gaps same-lane;
- release → head gaps;
- retriggers;
- anchors interiores;
- frecuencia de anchors;
- vocabulario temporal;
- divisiones raras;
- offsets;
- comportamiento alrededor de cambios BPM.

El motor no consultaría defaults estilísticos.

Consultaría evidencia.

---

# 5. Principio de backoff

Cuando no exista evidencia local suficiente:

```text
contexto local
    ↓
contexto de sección
    ↓
contexto global del chart
    ↓
SKIP
```

La regla maestra debería ser:

> **No evidence != use default.**
>
> **No evidence = do not invent.**

Por ejemplo, para un gap:

```text
LocalSupportedGap
        ↓
si no existe:
SectionSupportedGap
        ↓
si no existe:
GlobalSupportedGap
        ↓
si no existe:
SKIP
```

El algoritmo no debería caer automáticamente a un `1/8` elegido por nosotros.

---

# 6. Chord density derivada del mapa

Actualmente una política como:

```text
GraceColumns = 2
Decay = 0.65
```

define manualmente cuándo un chord comienza a ser peligroso.

La versión futura debería observar la distribución real del chart.

Ejemplo:

```text
1 head → extremadamente común
2 heads → común
3 heads → ocasional
4 heads → raro
5 heads → muy raro
6 heads → nunca
```

Una transición:

```text
2 → 3
```

tiene mucho más soporte que:

```text
5 → 6
```

La protección anti-wall podría provenir del soporte empírico del estado resultante.

Esto también generaliza de forma natural entre:

- 4K;
- 7K;
- 10K;
- otros keymodes.

Idealmente trabajar con:

```text
HeadOccupancyRatio = Heads / KeyCount
```

cuando corresponda.

---

# 7. Densidad contextual sin ventanas arbitrarias

Actualmente conceptos como:

- micro = 1 beat;
- contexto = ±4 beats;
- ratio = 1.5;
- factores para 1/2/3/4 beats;

son hipótesis útiles, pero siguen siendo elegidas por nosotros.

Una versión futura podría estudiar la serie temporal de densidad original y descubrir sus propias escalas.

Ejemplo:

```text
picos frecuentes:
0.5 beat
1 beat

secciones densas:
8 beats
12 beats
16 beats
```

El mapa ya demuestra que:

```text
~1 beat
```

es un transient,

mientras:

```text
8+ beats
```

es un nuevo régimen de densidad.

Esto podría evolucionar hacia un análisis del tipo:

```text
DensityRegimeAnalysis
```

sin un threshold fijo de “4 beats = sustained section”.

---

# 8. Lane gap sin fallback arbitrario

El gap debería ser completamente aprendido.

Fuentes posibles:

- gaps originales same-lane locales;
- gaps de la sección;
- gaps globales;
- soporte/frecuencia.

No utilizar un outlier único como autoridad.

Tampoco exigir necesariamente:

```text
support >= 2
```

si en el futuro podemos expresar soporte como distribución/peso empírico.

Si no existe evidencia:

```text
SKIP
```

en vez de inventar una separación.

---

# 9. LN duration y release vocabulary

Las formas LN deberían surgir exclusivamente de:

- duraciones originales observadas;
- releases originales;
- anchors originales;
- timing relativo al mapa.

No debería existir una afinidad arbitraria como:

```text
SourceAffinity = 1.25
```

si la propia frecuencia de la forma puede expresar su soporte.

Ejemplo:

```text
duración A observada 8 veces
duración B observada 2 veces
```

ya contiene información suficiente para ponderar A más que B.

---

# 10. Distancia contextual derivada

Los pesos de distancia también deberían revisarse.

En vez de:

```text
same timestamp = 1.00
1 beat = 0.80
2 beats = 0.60
...
```

se podría aprender qué distancias son realmente relevantes en el chart.

Por ejemplo:

- spacing típico de LNs;
- ritmo dominante;
- distancia típica entre eventos relacionados;
- periodicidad de frases.

La escala del contexto debería nacer del mapa.

---

# 11. LN interior sin mínimos arbitrarios

Una futura versión debería evitar reglas como:

```text
Parent >= 3 beats
Context >= 3 LN
Anchors >= 2
MaxInterior = 2
```

si la propia estructura puede decidirlo.

Una parent puede producir oportunidades interiores si:

- contiene anchors originales compatibles;
- existen duraciones originales compatibles;
- existe espacio suficiente;
- existe contexto temporal respaldado;
- la geometría lo permite.

Una LN demasiado corta quedará descartada naturalmente porque no podrá contener una estructura válida.

El número de oportunidades interiores podría depender de:

```text
cantidad de anchors compatibles
```

en vez de un cap fijo.

---

# 12. LN Articulation completamente emergente

La articulación tampoco debería necesitar un threshold como:

```text
HeldColumns >= KeyCount - 1
```

en la versión final.

Podría surgir del flujo:

```text
successful interior opportunity
        ↓
normal LN candidates respaldados
        ↓
ninguno tiene geometría legal
        ↓
la causa es ocupación por holds
        ↓
existe release→repress vocabulary original
        ↓
ARTICULATE
```

La geometría misma indica que no existe espacio para una LN paralela.

No hace falta decidir manualmente qué significa “Full-LN suficiente”.

---

# 13. LocalRetriggerGap derivado

Para Articulation:

```text
release → repress
```

debería provenir de transiciones originales equivalentes.

Ejemplo:

```text
release → 1/8 → head
release → 1/8 → head
release → 1/4 → head
```

La distribución original define los retriggers disponibles.

No usar un retrigger default inventado.

---

# 14. Eliminar caps de articulación cuando sea posible

Durante experimentación puede ser correcto:

```text
MaxArticulationsPerOriginalLn = 1
```

para mantener el sistema conservador.

Pero la versión final debería intentar derivar la capacidad desde:

- anchors disponibles;
- separación entre anchors;
- duración de segmentos;
- retrigger vocabulary;
- geometría;
- evidencia original.

Si el mapa solo permite una articulación coherente, aparecerá una.

Si permite varias de forma clara, el algoritmo debería poder descubrirlo.

---

# 15. AddedInteractions en vez de solamente AddedObjects

Una futura evaluación del algoritmo debería distinguir:

```text
AddedObjects
```

de:

```text
AddedInteractions
```

Una articulación:

```text
LN original
```

que se convierte en:

```text
hold → release → repress → hold
```

puede añadir mucha interacción jugable aunque el número de objetos apenas cambie.

La métrica no debe presentarse como “dificultad”.

Solo como cantidad de eventos nuevos:

- heads;
- releases;
- retriggers.

---

# 16. MapperSupport / EvidenceScore

A largo plazo podría ser posible unificar varias heurísticas actuales.

Hoy existen conceptos como:

- `ChordFactor`;
- `ContextualFactor`;
- `SourceAffinity`;
- `DistanceWeight`;
- soporte de anchors;
- soporte de duración;
- geometría.

Una futura arquitectura podría expresarlos como evidencia:

```text
ChordSupport
TemporalSupport
LnShapeSupport
AnchorSupport
RetriggerSupport
GeometrySupport
```

y construir:

```text
MapperSupport(candidate)
```

La pregunta dejaría de ser:

```text
¿Qué multiplicadores aplicamos?
```

y pasaría a ser:

```text
¿Cuánto respaldo tiene esta transformación
dentro del lenguaje del mapa original?
```

La fórmula exacta de combinación NO está definida todavía.

Debe investigarse.

---

# 17. Evolución futura de AddChance

La interpretación más ambiciosa sería dejar de pensar:

```text
ADD 50
=
50% de Bernoulli
× varios multiplicadores
```

y pensar:

> **ADD 50 solicita utilizar aproximadamente el 50% del presupuesto de oportunidades respaldadas por el mapa.**

El mapa decide dónde existe soporte fuerte.

Ejemplo:

```text
A → soporte alto
B → soporte alto
C → burst excepcional
D → wall no respaldada
E → articulation muy respaldada
```

ADD podría favorecer:

```text
A
B
E
```

antes de gastar presupuesto en:

```text
C
D
```

Esto podría permitir que `ChordFactor` y `ContextFactor`
dejen de existir como multiplicadores independientes.

Esta es una hipótesis futura, no una decisión cerrada.

---

# 18. Magic Number Audit

Antes de perseguir esta arquitectura, realizar una auditoría exhaustiva.

Para cada constante del algoritmo:

```text
Name
Current value
Where used
Behavior affected
Category
```

Categorías:

```text
USER_INTENT
FORMAT_OR_INVARIANT
MAPPER_DERIVED_CANDIDATE
IMPLEMENTATION_ONLY
```

Ejemplos:

```text
AddChance
→ USER_INTENT

NoOverlap
→ FORMAT_OR_INVARIANT

DensityDecay = 0.65
→ MAPPER_DERIVED_CANDIDATE

binary-search threshold interno
→ IMPLEMENTATION_ONLY
```

Meta:

> No debe quedar ninguna constante de estilo sin justificar.

---

# 19. Orden futuro sugerido

## Fase 1 — Magic Number Audit

Inventariar todas las constantes.

No cambiar todavía el algoritmo.

---

## Fase 2 — MapperEvidenceProfile

Crear el perfil inmutable de evidencia.

Migrar análisis dispersos hacia esa representación.

---

## Fase 3 — Eliminar fallbacks fáciles

Prioridad:

- fixed lane gap fallback;
- retrigger fallback;
- source affinity;
- mínimos de soporte;
- mínimos LN/interior;
- caps simples.

Sustituir por:

```text
evidence
→ broader evidence
→ SKIP
```

---

## Fase 4 — Modelo vertical empírico

Sustituir:

```text
GraceColumns
Decay
```

por distribución real de chords / ocupación del mapa.

Debe funcionar de forma natural en cualquier KeyCount.

---

## Fase 5 — Modelo temporal adaptativo

Sustituir:

- micro window;
- context window;
- burst ratio;
- span thresholds;
- burst factors;

por escalas/regímenes descubiertos en el mapa.

Esta probablemente será una de las fases más difíciles.

---

## Fase 6 — Evidence-based candidate weighting

Sustituir pesos manuales de:

- distancia;
- affinity;
- anchors;
- shapes;

por frecuencia/soporte empírico.

---

## Fase 7 — Unified MapperSupport

Investigar si:

```text
ChordFactor
ContextFactor
Ln weights
Articulation support
```

pueden unificarse bajo un modelo común de evidencia.

---

## Fase 8 — Budget-based AddChance

Investigar una semántica donde:

```text
AddChance
```

sea el único control humano de intensidad,

mientras:

```text
MapperSupport
```

decide dónde gastar ese presupuesto.

---

## Fase 9 — Zero-config validation

Ocultar los sliders experimentales.

Mantener únicamente:

```text
Chart
Add %
Seed
Range
Generate
```

Los parámetros derivados pueden seguir visibles como diagnóstico,
pero en modo read-only.

Validar con charts reales:

- distintos keymodes;
- rice;
- LN;
- Full-LN;
- timings extraños;
- distintos estilos de mapper.

---

# 20. Principio de portabilidad entre keymodes

La versión final no debe necesitar:

```text
if 4K ...
if 7K ...
if 10K ...
```

La política debe derivarse de:

- proporciones;
- geometría;
- evidencia temporal;
- lenguaje original.

Ante estructuras equivalentes respecto de su KeyCount:

```text
4/4 chord
7/7 chord
10/10 chord
```

la política debería ser conceptualmente equivalente.

---

# 21. Criterio de finalización conceptual

Una posible regla maestra para considerar terminado el algoritmo:

> **Si cambiar manualmente una constante numérica cambia el estilo del resultado, esa constante no debería permanecer como un valor arbitrario.**
>
> Debe ser una de:
>
> 1. intención explícita del usuario;
> 2. invariancia necesaria;
> 3. valor derivado del mapa;
> 4. detalle de implementación sin efecto estilístico.

---

# 22. UI final ideal

La página de experimentos puede seguir existiendo durante desarrollo.

Pero la interfaz final ideal sería aproximadamente:

```text
Chart
Add %
Seed
Range
Generate
```

Y opcionalmente un panel diagnóstico read-only:

```text
Mapper profile

Chord vocabulary
Density regimes
LN duration vocabulary
Lane gaps
Retrigger vocabulary
Timing vocabulary
Anchor structure
Evidence coverage
```

Sin sliders necesarios para “hacer funcionar” el algoritmo.

---

# 23. Filosofía final

La meta no es:

> construir un randomizador que produzca patrones que nosotros consideramos buenos.

La meta es:

> construir un sistema que utilice el mapa original como demostración de qué transformaciones son válidas.

ADD NOTES aporta la intención de aumentar interacción.

El mapper aporta el lenguaje.

La geometría determina qué es físicamente posible.

Cuando falta evidencia:

```text
SKIP
```

antes que:

```text
INVENT
```

Ese sería el cierre conceptual del proyecto.
