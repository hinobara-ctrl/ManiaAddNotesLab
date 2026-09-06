# Diagnóstico: ADD NOTES pierde efecto en secciones ocupadas por LNs

Fecha del análisis: 2026-09-05.

Este documento verifica el comportamiento observado al comparar el chart original de **Spring of Dreams [KNH - Lvl 81 (No SV)] CUSTOM** con el output **ADD 50 seed 100 run 20**, usando además las 100 filas de `runs.csv`. En esta etapa no se modificó el algoritmo.

## 1. Síntoma observado

En secciones total o parcialmente ocupadas por tails LN, ADD 50 añade elementos visibles de rice, pero la estructura de LNs largas cambia muy poco. Las LNs sintéticas tienden a reforzar heads que ya existían, en vez de producir suficiente interacción nueva entre las tails largas.

La hipótesis inicial era una de estas dos:

1. casi no nacen oportunidades LN interiores;
2. las oportunidades nacen, pero probabilidad, candidatos o geometría las eliminan.

Los archivos confirman que ocurren ambas cosas, principalmente la primera.

## 2. Integridad de los `.osu`

La comparación se realizó como multiset de líneas de `[HitObjects]` y mediante intervalos por lane.

| Comprobación | Resultado |
|---|---:|
| Objetos originales | 4.608 |
| Objetos del output | 5.226 |
| Objetos añadidos | 618 |
| Objetos originales ausentes en output | 0 |
| Overlaps inclusivos que involucren un sintético | 0 |

Conclusión: no se detectó corrupción del archivo, pérdida de objetos originales ni overlap sintético. El problema es de política algorítmica, no de parser/writer.

## 3. Evidencia de la run mostrada

Configuración relevante de seed 100:

```text
Chance                         = 0.50
SourceAffinity                 = 1.00
ContextualDensity              = ON
Chord decay                    = 0.65 después de 2 columnas
Local lane gap                 = ON
InteriorLnOpportunities        = ON
MaxInteriorOpportunities       = 2
MapRelativeSnap                = ON
```

Resultados:

| Métrica | Valor |
|---|---:|
| Original objects | 4.608 |
| Original taps | 1.021 |
| Original LNs | 3.587 |
| Base opportunities | 4.608 |
| Interior opportunities | 23 |
| Successful base rolls | 739 |
| Successful interior rolls | 0 |
| Added taps | 205 |
| Added LNs desde heads | 413 |
| Added LNs interiores | 0 |
| Failed placements | 121 |
| LN geometry skips | 103 |
| LN candidates built | 10.736 |
| LN candidates impossible | 8.558 |
| Density-adjusted opportunities | 4.132 |
| Context-adjusted opportunities | 1.289 |
| Mean effective chance | 0,154394 |

Aunque la configuración dice ADD 50, la chance efectiva media fue 15,44%.

### Desequilibrio de tipos

```text
Original LN ratio = 77,84%
Added LN ratio    = 66,83%
Result LN ratio   = 76,54%
```

La nueva capa es proporcionalmente más rice-heavy que el chart original. Las 413 LNs añadidas proceden exclusivamente de oportunidades en heads LN ya existentes. Ninguna procede del interior de una LN en esta seed.

## 4. Evidencia agregada de 100 runs

Cada nivel de chance contiene 20 seeds. El chart solo produjo 23 oportunidades interiores por ejecución, incluso con máximo 2 por source.

| Chance | Oportunidades interiores | Tiradas interiores | Interiores colocadas | Promedio colocado/run |
|---:|---:|---:|---:|---:|
| 0,10 | 460 | 9 | 9 | 0,45 |
| 0,20 | 460 | 32 | 27 | 1,35 |
| 0,30 | 460 | 35 | 32 | 1,60 |
| 0,40 | 460 | 43 | 38 | 1,90 |
| 0,50 | 460 | 56 | 49 | 2,45 |

`460 / 20 = 23` oportunidades por output. Frente a 3.587 LNs originales, equivale a solo 0,64 oportunidades interiores por cada 100 LNs. La opción está activa y funciona técnicamente, pero su alcance es demasiado pequeño para producir un cambio visible en las frases LN-heavy.

En ADD 50, 167.233 de 209.278 shapes LN evaluados fueron imposibles: 79,9%. El filtrado optimizado evita repetir esos checks, pero no cambia que el vocabulario de formas propuesto encaja con dificultad en la geometría existente.

## 5. Penalización desigual causada por tails activas

Se recorrieron los heads originales y se contó cuántas LNs originales estaban activas en cada timestamp. Después se aplicó únicamente la fórmula vertical actual:

```text
ChordFactor = 0.65 ^ max(0, ActiveColumns - 2)
```

| Source del head | Heads | ChordFactor medio | Con ≥4 LNs activas | Con ≥6 LNs activas |
|---|---:|---:|---:|---:|
| LN | 3.587 | 0,393 | 2.607 (72,7%) | 1.278 (35,6%) |
| Tap | 1.021 | 0,754 | 270 (26,4%) | 15 (1,5%) |

Distribución global de heads:

| LNs activas | Heads originales |
|---:|---:|
| 0 | 201 |
| 1 | 260 |
| 2 | 389 |
| 3 | 881 |
| 4 | 942 |
| 5 | 642 |
| 6 | 529 |
| 7 | 764 |

Antes del factor contextual, un head LN promedio recibe en ADD 50 aproximadamente `0,50 × 0,393 = 19,65%`, mientras un head tap promedio recibe `0,50 × 0,754 = 37,70%`.

Esta diferencia no se debe al tipo de objeto de forma explícita. Aparece porque la normalización vertical cuenta tails sostenidas como columnas ocupadas durante toda su duración. En este chart, los heads LN coinciden mucho más frecuentemente con varias tails activas.

## 6. Causas raíz confirmadas

### Causa A — Elegibilidad interior demasiado restrictiva

La implementación exige actualmente:

```text
Duration >= 4 beats
Local LN count >= 3
AND (
    Duration >= 1.5 × local median
    OR Duration >= 8 beats
)
```

En una sección uniformemente compuesta por LNs largas, una LN típica no es `1.5×` más larga que sus vecinas. Si tampoco alcanza 8 beats, queda excluida aunque contenga anchors útiles y exista espacio conceptual para más interacción.

Consecuencia verificada: solo 23 oportunidades interiores para 3.587 LNs.

### Causa B — Chord density confunde head chord con ocupación sostenida

El objetivo original del decaimiento era evitar transformar chords de 4–5 heads en paredes 7K. Sin embargo, `CountOccupiedColumns` incluye LNs activas aunque no exista un nuevo head simultáneo.

Esto convierte una protección contra picos verticales en una penalización continua de toda frase con tails. El efecto sucede antes de la tirada y explica por qué subir ADD Chance o cambiar afinidad no recupera el comportamiento esperado.

### Causa C — El source virtual contamina el contexto interior

Al resolver una oportunidad interior, el código crea una LN virtual desde el anchor hasta el release de la LN padre. `BuildLocalLnContext` añade esa LN virtual a las observaciones si no existe como original.

Esto rompe la intención de que el estilo se aprenda exclusivamente desde `OriginalObjects`. Además, la LN virtual aporta:

- un voto de duración hasta el final de la LN padre;
- un voto de release en ese mismo final;
- posible multiplicador de source affinity.

Así, una oportunidad creada para añadir interacción interna queda sesgada hacia una LN larga que termina junto con la parent. Con `SourceAffinity=1.00` desaparece el multiplicador adicional, pero permanecen los dos votos artificiales. Esta causa no produjo las cero interiores de seed 100 —ninguna superó la tirada—, pero afectará la forma de las que sí sobrevivan.

### Causa D — La geometría rechaza gran parte del vocabulario LN

El 79,9% de los candidatos LN de ADD 50 no tenía ninguna lane legal. Esto no es una regresión de rendimiento ni un error del índice: los intervalos propuestos realmente chocan con una geometría muy ocupada y con el gap requerido.

La selección de lanes funciona correctamente y no produjo overlaps. El problema está antes: se proponen demasiadas formas largas para el espacio disponible y muy pocas oportunidades/candidatos cortos respaldados por el mapa.

## 7. Por qué mover los números indicados no resolvió el problema

- `MaxInteriorOpportunities=2` solo eleva el techo; no vuelve elegibles más sources.
- `SourceAffinity=1.00` elimina el bonus multiplicativo, pero no elimina los dos votos de la LN virtual.
- aumentar ADD Chance multiplica una probabilidad ya reducida por tails activas y también añade más rice fuera de esas secciones.
- reducir el gap puede admitir algunas formas, pero arriesga recrear LNs excesivamente cercanas y no corrige la falta de oportunidades.
- desactivar densidad contextual no elimina la penalización vertical producida por tails.

Por lo tanto, el hallazgo no se soluciona adecuadamente mediante los controles actuales.

## 8. Plan de solución

La corrección debe implementarse por etapas y con toggles A/B. No conviene cambiar simultáneamente elegibilidad, probabilidad y shapes sin poder atribuir el efecto.

### Etapa 1 — Separar head chord de held-lane occupancy

Crear dos mediciones distintas:

```text
SimultaneousHeadColumns
HeldLnColumns
```

Usar inicialmente `SimultaneousHeadColumns` para `ChordDensityFactor`, porque ese factor nació para impedir que un chord de heads de 4–5 notas escale a 7K. Mantener `HeldLnColumns` en trace/métricas y dejar que la geometría determine si una lane físicamente cabe.

No crear todavía un nuevo `HeldLanePressureFactor`. Si el playtesting demuestra que hace falta, deberá ser independiente y configurable; no debe quedar oculto dentro del factor de chord.

Toggle propuesto:

```text
VerticalDensityMode:
  OccupiedColumnsLegacy
  SimultaneousHeads
```

### Etapa 2 — Hacer que la evidencia interna gobierne la elegibilidad

Reordenar `BuildInteriorOpportunities`:

1. comprobar duración mínima simple;
2. obtener anchors originales interiores;
3. comprobar contexto LN original;
4. crear oportunidades si existe soporte interno suficiente;
5. usar longitud relativa solo para ranking/prioridad, no como puerta obligatoria.

Primera hipótesis experimental:

```text
InteriorMinimumSourceBeats = 3 o 4
InteriorMinimumContextLnCount = 3
InteriorMinimumSupportedAnchors = 2
MaxInteriorOpportunitiesPerSource = 2
```

`InteriorLengthRatio=1.5` y `InteriorAbsoluteLongBeats=8` dejarían de ser el único acceso. Podrían aportar prioridad a sources excepcionalmente largas.

### Etapa 3 — Restaurar contexto original-only

Una `LnInteriorOpportunity` debe conservar referencia separada a:

- `ParentOriginalLn`;
- `InteriorAnchor`;
- `OpportunityStartBeat`.

La parent sirve para justificar el intervalo y limitar oportunidades, no para insertarse como observación virtual. `LocalLnContext` debe contener únicamente LNs originales.

Para anchors cuyo contexto por head sea escaso:

- usar contexto original cerca del anchor;
- permitir fallback al contexto original de la parent;
- si ambos carecen de evidencia, rechazar la oportunidad.

Nunca añadir la LN virtual a `LnObservation`.

### Etapa 4 — Candidatos interiores cortos respaldados

Reutilizar el mismo motor de candidatos, pero distinguir la afinidad:

- oportunidad base: puede conservar source affinity actual;
- oportunidad interior: no favorecer automáticamente el release de la parent;
- favorecer por frecuencia las duraciones originales observadas en el contexto;
- conservar anchors exactos y map-relative timing;
- no inventar una duración corta ausente del mapa.

Los candidatos pueden ordenarse/ponderarse usando evidencia real y luego filtrarse una sola vez por geometría, como ocurre actualmente.

### Etapa 5 — Observabilidad antes de afinar números

Añadir contadores de rechazo de elegibilidad:

```text
InteriorRejectedTooShort
InteriorRejectedInsufficientLnContext
InteriorRejectedNoSupportedAnchors
InteriorRejectedRelativeLengthLegacy
InteriorCandidatesShort/Medium/Long
InteriorCandidatesImpossible
InteriorMeanEffectiveChance
SimultaneousHeadColumnsMean
HeldLnColumnsMean
```

Mostrar en Web, no solo CSV:

- oportunidades interiores;
- tiradas interiores;
- interiores colocadas;
- chance interior media;
- principales motivos de rechazo.

Esto permitirá saber si una nueva run falla en oportunidad, probabilidad, shape o geometría sin inspeccionar manualmente miles de líneas.

## 9. Estrategia de pruebas

### Tests unitarios nuevos

1. Una sección donde todas las LNs son largas y de duración similar debe producir oportunidades si contiene anchors originales suficientes.
2. Una LN larga sin anchors internos no debe producirlas.
3. AddedObjects no pueden volver elegible una parent ni crear anchors.
4. El contexto interior contiene exclusivamente objetos originales.
5. El release de la parent no recibe votos artificiales ni source affinity interior.
6. Duraciones cortas 1/4, 1/5 y 1/8 observadas siguen disponibles sin standard snap.
7. Con el mismo chord de heads, las tails de fondo no cambian `ChordDensityFactor` en modo `SimultaneousHeads`.
8. `HeldLnColumns` continúa afectando geometría.
9. Un candidato corto legal puede sobrevivir aunque candidatos largos sean imposibles.
10. No-overlap, gap, output intacto y determinismo permanecen verdes.

### Fixtures

- LN-heavy uniforme con LNs de 3–6 beats y anchors internos.
- LN-heavy sin anchors.
- misma secuencia de heads, A/B con y sin tails sostenidas de fondo.
- mezcla de duraciones cortas y largas.
- fixture real recortado de la frase problemática, si se autoriza crear una copia mínima.

### A/B sobre el mapa real

Mantener constantes chart, seed y ADD Chance:

1. legacy completo;
2. solo densidad por heads;
3. solo nueva elegibilidad/contexto interior;
4. ambos cambios.

Ejecutar al menos seeds 1–100 para evitar que una seed con cero tiradas interiores distorsione la conclusión.

## 10. Criterios de aceptación

La corrección estará lista para playtesting si:

- se conservan los 4.608 objetos originales;
- no aparecen overlaps ni violaciones del gap;
- el output sigue siendo determinista;
- una sección LN-heavy uniforme con anchors ya no queda excluida por compararse consigo misma;
- las oportunidades interiores usan solamente evidencia original;
- el factor anti-wall responde a heads simultáneos y no penaliza toda una tail por accidente;
- las métricas permiten localizar cada rechazo;
- en Spring of Dreams las oportunidades interiores aumentan materialmente sobre 23 sin explosión ilimitada;
- `MaxInteriorOpportunitiesPerSource <= 2` sigue respetándose;
- la mayoría de la nueva interacción interior usa duraciones presentes en el mapa;
- rendimiento Release no sufre una regresión relevante respecto del índice actual;
- `dotnet restore`, `build` y `test` permanecen verdes.

No se fija todavía una cantidad “correcta” de LNs interiores. La naturalidad, frecuencia final y balance entre rice/LN requieren comparación visual y playtesting humano.

## 11. Orden recomendado de implementación

1. Añadir métricas de rechazo y baselines del mapa real.
2. Corregir el contexto original-only y eliminar votos virtuales.
3. Cambiar la puerta de elegibilidad por soporte de anchors.
4. Separar densidad de heads y tails con toggle A/B.
5. Añadir controles Web y CSV correspondientes.
6. Ejecutar tests sintéticos.
7. Ejecutar matriz A/B de 100 seeds sobre Spring of Dreams.
8. Revisar AiMod y capturas sincronizadas.
9. Realizar playtesting antes de ajustar nuevos números.

## 12. Conclusión

El hallazgo queda comprobado. El `.osu` generado es estructuralmente válido, pero la política actual produce una subrepresentación de interacción LN interior en frases ocupadas por tails. La causa dominante es la combinación de elegibilidad interior extremadamente escasa y penalización de probabilidad basada en ocupación sostenida. A esto se suman un sesgo artificial hacia el release de la parent y una elevada imposibilidad geométrica de shapes largos.

La solución recomendada es corregir esas responsabilidades de forma separada y observable, no aumentar ADD Chance ni relajar globalmente la geometría.

## 13. Estado de la corrección (2026-09-05)

Las cuatro causas fueron verificadas directamente en `AddNotesEngine` y corregidas detrás de toggles independientes. El A/B de seeds 1–100 confirma que la elegibilidad por anchors eleva 23 a 76 oportunidades interiores por run y que el modo combinado coloca 16.07 LNs interiores por run frente a 2.77 legacy. La parent virtual fue retirada del contexto moderno y las tails quedaron separadas de los heads para el factor anti-wall. La geometría no fue relajada.

Véase [LN_INTERIOR_CORRECTION_REPORT.md](LN_INTERIOR_CORRECTION_REPORT.md) para métricas, validez, rendimiento y preguntas de playtesting.
