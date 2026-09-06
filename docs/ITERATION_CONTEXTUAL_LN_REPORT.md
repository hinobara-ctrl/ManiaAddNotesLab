# Informe completo de la iteración contextual/LN

Este documento reúne los cambios realizados en `ManiaAddNotesLab` desde el prompt de rediseño de ADD NOTES. Describe la implementación real, no una propuesta futura. El proyecto continúa siendo un laboratorio independiente: no se integró con HRandomPlus, osu!stable/lazer, tosu, Wine ni sistemas de randomización de columnas.

## 1. Estado antes de la iteración

El proyecto ya incluía:

- Core independiente con parser/writer `.osu`, timeline musical y motor ADD NOTES.
- Una oportunidad por cada head original elegible.
- Tap source → tap sintético y LN source → LN sintética.
- Contexto LN local, fusión de candidatos, votos por duración/release y weighted random.
- Selección uniforme entre lanes legales.
- rango temporal, seed reproducible, output sin overwrite, trace, CSV, CLI y Web para lotes.
- normalización vertical de chords con columnas de gracia y decaimiento.
- gap geométrico fijo de `0.125` beat.
- snapping LN obligatorio a una tabla estándar.
- geometría que realizaba scans y conversiones repetidas en caminos calientes.
- 18 tests automatizados.

## 2. Invariantes preservadas

Se mantuvo la separación solicitada:

- `OriginalObjects` generan oportunidades y enseñan densidad, gaps y lenguaje LN.
- `OriginalObjects + AddedObjects` determinan ocupación, colisiones y geometría futura.
- `AddedObjects` no generan nuevas oportunidades ni contaminan el aprendizaje durante una ejecución.
- Un release LN no crea una oportunidad base.
- La LN original nunca se corta, mueve o reescribe.
- Una LN sintética imposible se omite: no se acorta y no se convierte en tap.
- La lane sigue siendo uniforme entre las lanes legales.
- El resultado sigue siendo determinista por input, opciones y seed.

No se agregaron clasificadores de patrones, CandidateScorer, H-Random, S-Random, strain model, hand balance, ML ni autoajuste.

## 3. Normalización de densidad vertical

Se conservó la fórmula previa:

```text
steps = max(0, OccupiedColumns - DensityGraceColumns)
ChordDensityFactor = DensityDecayPerColumn ^ steps
```

Defaults experimentales:

- `DensityGraceColumns = 2`.
- `DensityDecayPerColumn = 0.65`.

La ocupación incluye taps, LNs activas y sintéticos ya colocados. El factor nunca incrementa la probabilidad. Puede desactivarse para A/B usando decaimiento `1.00` desde la Web.

## 4. Nueva normalización de densidad contextual

Se añadió `HeadDensityAnalyzer`, separado de geometría y densidad vertical. Cuenta exclusivamente heads originales:

```text
Tap head = 1
LN head  = 1
```

No considera releases, duración continua, dificultad subjetiva ni objetos añadidos.

Para cada oportunidad calcula:

- `MicroHeadDensity`: densidad en aproximadamente 1 beat.
- `ContextHeadDensity`: mediana inferior robusta de muestras laterales dentro de ±4 beats.
- `DensityRatio = Micro / max(1, Context)`.
- span total de la región elevada que contiene el punto.
- `ContextualDensityFactor`.

La región central no entra directamente en su propio baseline. La búsqueda de span mira hacia atrás y hacia adelante, por lo que el primer y último beat de una sección pueden reconocer el tramo completo.

Defaults experimentales:

- `MicroDensityWindowBeats = 1.0`.
- `ContextDensityWindowBeats = 4.0` por lado.
- `BurstDensityRatioThreshold = 1.5`.
- densidad micro mínima para considerar elevación: 2 heads por ventana.
- span ≤1 beat: factor `0.45`.
- span 2 beats: factor `0.60`.
- span 3 beats: factor `0.80`.
- span ≥4 beats: factor `1.00`.

La fórmula final quedó centralizada:

```text
EffectiveChance = min(
    BaseAddNoteChance,
    BaseAddNoteChance × ChordDensityFactor × ContextualDensityFactor)
```

Se aplica a taps, LNs base y oportunidades interiores. Los valles y las secciones densas sostenidas nunca aumentan la chance sobre la base.

## 5. Gap local aprendido por lane

El antiguo `MinimumLaneGapBeats` fijo fue sustituido por dos conceptos separados:

- `LocalSupportedLaneGap` aprendido.
- `FallbackMinimumLaneGapBeats` conservador.

`LocalLaneGapAnalyzer` inspecciona únicamente objetos originales consecutivos de cada lane cerca de la oportunidad:

```text
gap = Next.StartBeat - Previous.EndBeat
```

Para taps, `EndBeat = StartBeat`. Se ignoran overlap, gaps negativos, gaps mayores a 1 beat y objetos añadidos. Los valores se agrupan a seis decimales y se utiliza el menor gap con soporte suficiente.

Defaults experimentales:

- `UseLocalLaneGap = true`.
- `LaneGapWindowBeats = 4.0` por lado.
- `LaneGapMinimumSupport = 2`.
- `FallbackMinimumLaneGapBeats = 0.125`.

La restricción geométrica conceptual es:

```text
Previous.EndBeat + G <= StartBeat
EndBeat + G <= Next.StartBeat
```

El trace informa `local` o `fallback`, valor y cantidad de evidencia.

## 6. Vocabulario temporal relativo al mapa

Se eliminó la tabla estándar como autoridad predeterminada. Ahora la regla experimental es: el mapa original define su vocabulario temporal.

Cambios principales:

- `TimingPoint` y las conversiones internas usan `decimal`.
- `OriginalObject.StartBeat` y `EndBeat` se precalculan una vez.
- un release proveniente de un anchor original conserva exactamente su milisegundo.
- una plantilla de duración conserva la diferencia musical observada y la traslada al nuevo head.
- se conservan 1/5, 1/10, offsets consistentes y cambios de BPM.
- la conversión a milisegundos ocurre cuando se materializa el candidato `.osu`.

No se creó un `LocalSnapProfile` adicional porque anchors exactos más traslación decimal de duraciones resolvieron la necesidad sin imponer fases externas.

El modo previo continúa disponible únicamente para A/B:

```text
MapRelativeSnapEnabled = false
```

Ese modo usa los divisores estándar `1, 2, 3, 4, 6, 8, 12, 16`.

## 7. Oportunidades interiores de LN

Se añadió `LnInteriorOpportunity` como segundo origen de oportunidades. Está desactivado por defecto.

Una LN source puede generarlas solo cuando:

- dura al menos `4` beats;
- existe contexto de al menos `3` LNs;
- mide al menos `1.5×` la mediana local o alcanza `8` beats;
- contiene heads o releases originales estrictamente interiores.

Los anchors se obtienen con índices ordenados y búsqueda por rango. Nunca se inventan timestamps ni se usan objetos añadidos. Se priorizan anchors con evidencia repetida y luego cercanía al centro de la LN.

Defaults experimentales:

- `InteriorLnOpportunitiesEnabled = false`.
- `MaxInteriorOpportunitiesPerSource = 1`.
- la Web permite `0–2`.

Cada oportunidad interior utiliza exactamente el mismo motor que una LN base: chance, ambos factores de densidad, contexto LN, duración mínima local, gap, candidatos, weighted random, geometría y lane uniforme. La LN source permanece intacta.

## 8. Resolución de formas LN

Se preservaron y adaptaron las reglas existentes:

- contexto en `±4` beats por head LN.
- un voto por duración trasladada y otro por release exacto de cada observación.
- fusión de candidatos que producen el mismo `EndTime`.
- eliminación de candidatos anteriores al head o menores que el mínimo LN local.
- peso por distancia con caída `0.20` por banda de beat y mínimo `0.20`.
- afinidad source `1.25` cuando se reproduce su duración o release.
- weighted random entre shapes físicamente posibles.

Ahora cada `ReleaseCandidate` se resuelve una sola vez a `ResolvedReleaseCandidate`, incluyendo sus `LegalLanes`. Los shapes con cero lanes se descartan antes del sorteo. Esto conserva la equivalencia del retry sin reemplazo cuando la geometría no cambia dentro de la oportunidad.

## 9. Índice geométrico y rendimiento

Se añadió `LaneGeometryIndex`:

- una lista cronológica de intervalos por lane;
- búsqueda binaria del lugar donde se insertaría el candidato;
- comprobación principal de `Previous` y `Next`;
- inserción ordenada de cada sintético aceptado;
- posiciones beat ya precalculadas.

Se eliminaron del hot path los scans completos del chart, conversiones beat repetidas y reevaluaciones de una misma forma LN.

Instrumentación añadida:

- `ContextBuildMs`.
- `CandidateBuildMs`.
- `GeometryMs`.
- `WriterMs`.
- `GeometryChecks`.
- `GeometryObjectsExamined`.
- `BeatConversions`.
- `LnCandidatesBuilt`.
- `LnCandidatesImpossible`.
- `LnGeometryChecks`.

Medición manual Release sobre 1000 LNs, chance 100%, seed 4242:

| Métrica | Antes | Después |
|---|---:|---:|
| Tiempo de pared | 1076.59 ms | 514.24 ms |
| Geometría interna | no instrumentada | 7.36 ms |
| GeometryChecks | no instrumentado | 34,958 |
| ObjectsExamined | no instrumentado | 40,263 |
| BeatConversions | no instrumentado | 9,321 |

La reducción observada fue aproximadamente 52.2%. No es un benchmark controlado porque cambiaron políticas del algoritmo; la evidencia estructural estable es que la geometría dejó de crecer como scan completo por objeto/lane/candidato.

## 10. Métricas y trace

Se agregaron métricas separadas para:

- oportunidades base e interiores;
- tiradas exitosas base e interiores;
- taps añadidos;
- LNs desde heads base;
- LNs desde interiores;
- ajustes por chord y por contexto;
- chance efectiva media;
- candidatos construidos/imposibles y skips geométricos.

El trace ahora muestra:

- tipo y timestamp de oportunidad;
- chance base;
- densidad micro/contexto, ratio, span y factor contextual;
- columnas ocupadas, factor de chord y chance efectiva;
- fuente/valor/evidencia del gap;
- contexto y mínimo LN;
- EndBeat/EndTime, peso, votos, anchors exactos, ajuste estándar y lanes legales;
- source y evidencia de oportunidades interiores;
- resumen estructural y tiempos internos.

CLI y Web exportan estas variables en CSV. `WriterMs` se mide en el adaptador porque el Core no escribe archivos.

## 11. CLI

Se añadieron opciones A/B:

```text
--context-density on|off
--burst-ratio <n>
--local-gap on|off
--fallback-gap-beats <n>
--map-relative-snap on|off
--interior-ln on|off
--max-interior <0..2>
```

La consola imprime métricas separadas y contadores de rendimiento. El CSV acumulativo incluye configuración, resultados por origen, densidad, geometría y timings. La salida continúa usando rutas únicas para impedir overwrite.

## 12. Web experimental

La Web sigue reutilizando directamente `AddNotesEngine`. Se añadió un bloque “Experimental” con controles para:

- densidad contextual ON/OFF;
- threshold micro/contexto;
- factores de span 1/2/3 beats;
- gap local o fallback-only;
- ventana y soporte del gap;
- vocabulario relativo;
- interiores LN ON/OFF;
- máximo de interiores por source.

La normalización vertical existente continúa configurable mediante columnas de gracia y retención; `1.00` desactiva su efecto.

### Corrección de validación numérica

Los inputs de threshold y factores tenían `min=0.01/1.01` con `step=0.05`. En HTML, los pasos se calculan desde `min`, por lo que defaults como `0.60` quedaban fuera de la grilla válida (`0.56` y `0.61` eran los vecinos aceptados).

Se cambió el paso a `0.01`, manteniendo los defaults:

- threshold `1.50`;
- factor 1 beat `0.45`;
- factor 2 beats `0.60`;
- factor 3 beats `0.80`.

### Corrección del mensaje de conexión

El texto crudo `Failed to fetch` se sustituyó por diagnósticos claros:

- si se abre `index.html` como `file:`, indica iniciar el servidor y usar `http://127.0.0.1:5178`;
- si se pierde el backend, indica mantener activa la consola de `dotnet run` y reintentar.

Se verificó el servidor con GET `200`, POST `202` y un lote completado `1/1`.

## 13. Fixtures y comparaciones A/B

Fixtures añadidos:

- `experimental-context-burst.osu`.
- `experimental-sustained-rice.osu`.
- `experimental-local-gap.osu`.
- `experimental-ln-interior.osu`.
- `experimental-timing-vocabulary.osu`.

Outputs A/B reproducibles, seed 42:

- contexto OFF/ON: 5 frente a 4 objetos colocados; ON ajustó 5 oportunidades.
- interiores OFF/ON: 7 frente a 9 oportunidades; ON añadió 2 LNs interiores.
- gap fijo/local: la nueva LN cambió de lane porque el gap local de 1/4 rechazó una separación aceptada por el fallback 1/8.
- estándar/relativo: el estándar desplazó timings como `607→601`; el relativo conservó 1/5, 1/10, offsets y anchors.
- rice sostenido: 0 ajustes contextuales y chance efectiva igual a la base del 50%.

Estas son diferencias objetivas, no conclusiones sobre calidad jugable.

## 14. Tests y validación

La suite pasó de 18 a 42 tests. Cobertura nueva:

- sparse, burst de 1/2/3 beats, inicio/fin de sección sostenida y valle;
- equivalencia de densidad para taps, heads LN y mezcla;
- AddedObjects excluidos del perfil;
- gap 1/8 soportado frente a 1/4 y outlier único 1/64;
- fallback sin evidencia;
- mapa estándar, 1/5, 1/10, offset consistente, anchors exactos y cambio BPM;
- round-trip decimal sin drift acumulativo en el fixture exacto;
- LN corta/aislada, LN larga elegible, anchors originales y máximo interior;
- AddedObjects excluidos de anchors interiores;
- factor contextual y rechazo geométrico de interiores;
- filtrado de shapes imposibles antes del weighted random;
- crecimiento estructural por vecinos en lane grande;
- no-overlap por lane;
- determinismo de output, métricas decisionales y trace decisional.

Última validación de código:

```text
dotnet restore             PASS
dotnet build -c Release    PASS — 0 warnings, 0 errors
dotnet test -c Release     PASS — 42 passed, 0 failed
Web GET/POST/batch          PASS
```

Los timers se excluyen de la igualdad determinista porque miden tiempo real; todas las decisiones y counters reproducibles sí se comparan.

## 15. Archivos principales afectados

Core:

- `Model.cs`: opciones, orígenes, objetos temporizados y métricas.
- `BeatTimeline.cs`: representación decimal y contadores.
- `OsuBeatmap.cs`: timing points decimales.
- `ChartAnalysis.cs`: análisis original, densidad, gap e índices de anchors.
- `LaneGeometryIndex.cs`: geometría binaria por lane.
- `AddNotesEngine.cs`: integración de todas las políticas.

Superficies:

- `ManiaAddNotesLab.Cli/Program.cs`.
- `ManiaAddNotesLab.Web/Program.cs`.
- `wwwroot/index.html`.
- `wwwroot/app.js`.

Pruebas y documentación:

- `AddNotesEngineTests.cs`.
- `ExperimentalPolicyTests.cs`.
- `README.md`.
- `DESIGN.md`.
- `EXPERIMENTS.md`.
- `AB_RESULTS.md`.

## 16. Riesgos y siguiente fase

Siguen sin validarse mediante playtesting:

- columnas de gracia y decaimiento vertical;
- threshold, ventanas y factores contextuales;
- criterio de soporte y fallback del gap;
- afinidad source y pesos por distancia;
- frecuencia/eligibilidad de interiores;
- autoridad del vocabulario relativo en mapas reales diversos;
- percepción de densidad en charts LN-heavy.

La siguiente fase debe ser playtesting humano y revisión con AiMod usando las parejas A/B ya generadas. No corresponde añadir otra ronda de heurísticas antes de recoger esa evidencia.

EXPERIMENT READY FOR PLAYTESTING
