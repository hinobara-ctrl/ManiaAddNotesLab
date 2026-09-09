# Diseño implementado

## Núcleo estable e invariantes

El Core contiene parser, writer, timeline musical, análisis inmutable del chart e `AddNotesEngine`. CLI y Web son adaptadores; no duplican el algoritmo. OpenLR2/LR2 es solo referencia conceptual y no se importa código.

Al comenzar se congelan y convierten una sola vez a beats todos los objetos originales. Los heads originales crean oportunidades cronológicas; los sintéticos colocados afectan la geometría posterior, pero jamás crean oportunidades ni enseñan densidad, gaps o estilo. Un tap conserva el timestamp. Una LN nunca se recorta ni se convierte en tap: si ninguna combinación forma/lane es legal, se omite. El rango es inclusivo por head y no recorta releases.

La geometría usa listas ordenadas por lane y búsqueda binaria. Para un tap o intervalo LN solo se inspeccionan sus vecinos relevantes. Las lanes legales se calculan exactamente una vez por candidato de release. La colisión es conservadora: el intervalo ocupado incluye head y release; el gap aprendido se exige antes y después de una LN nueva.

## Phase A — Evidence Infrastructure / Shadow Mode

El motor construye un `MapperEvidenceProfile` inmutable exclusivamente desde `OriginalObjects` y timing original. El perfil contiene identidades estables por chart, observaciones temporales, chords, ocupación held, duraciones/releases LN, transiciones same-lane, retriggers, anchors interiores, provenance, fingerprint y versiones explícitas. `AddedObjects` y `ArticulationReplacements` nunca entran al builder.

Esta capa es puramente observacional: ningún dato del perfil participa todavía en oportunidades, probabilidad, candidates, lane selection o articulación. Web reutiliza un perfil por lote; CLI puede exportarlo con `--profile-output`. `TransformationWitness` y `SupportCertificate` existen solo como contratos extensibles sin confidence ni score. La política de generación continúa identificada como `legacy-experimental.1` y el perfil como `phase-a.1`.

El roadmap y sus límites están en [MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md](MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md). Phase B agrega certificados y causas en shadow. C1 permanece HOLD; D0–D0.2 reconstruyeron completion/context/agreement; F1 formalizó evidence state; F2 tipó gaps/backoff; F2.1 demostró information loss; F2.2–F2.3 formalizaron compatibility/domain/truth condicional. Phase E/E.1 cerraron Outcome B. D1.0 cerró Outcome A: representa `CompletionSet` conjunto por una misma occurrence y no confunde intersecciones marginales con joint evidence. D1.GATE cerró `READY` con el contrato futuro `phase-d1-gate-behavioral-experiment-contract.1`, sin conectarlo a generation. Generation, RNG y `BehaviorPolicyVersion=legacy-experimental.1` siguen intactos. F2.ACQ continúa bloqueado. No hay una siguiente candidata research; D1 es el próximo candidato conductual, conserva `FUTURE/NOT_AUTHORIZED` y requiere autorización separada. C2 permanece deferred.

## Hipótesis experimentales

Nada de esta sección es una conclusión de playtesting. Cada política tiene un interruptor A/B limpio en Web; densidad contextual, escalas relativas, gap local y vocabulario relativo vienen activos, mientras oportunidades interiores y articulación vienen inactivas.

Son **EXPERIMENTALES**: `DensityGraceColumns=2`, `DensityDecayPerColumn=0.65`, `VerticalDensityMode`, ventana micro `1` beat, contexto `±4` beats, `BurstDensityRatioThreshold=1.5`, factores `0.45/0.60/0.80/1.00`, ventana de gap `±4` beats, soporte mínimo `2`, fallback `0.125` beat, afinidad source `1.25`, caída por beat `0.20`, peso mínimo `0.20`, `InteriorMinimumSourceBeats=3`, `InteriorMinimumContextLnCount=3`, `InteriorMinimumSupportedAnchors=2`, `InteriorLengthRatio=1.5` como prioridad, `InteriorAbsoluteLongBeats=8` como prioridad y máximo interior `2`. También es experimental que el vocabulario temporal original sea la autoridad; reemplaza deliberadamente la antigua hipótesis de que una tabla estándar global debía ser autoritativa.

### Probabilidad y densidad

```text
DensityColumns = VerticalDensityMode == SimultaneousHeads
    ? SimultaneousHeadColumns
    : OccupiedColumns
HeadRatio = DensityColumns / KeyCount
EquivalentPenaltySteps = max(0, (HeadRatio - 2/7) / (1/7))
ChordFactor = DensityDecayPerColumn ^ EquivalentPenaltySteps
EffectiveChance = min(BaseChance, BaseChance × ChordFactor × ContextualFactor)
```

La fórmula mostrada corresponde a `KeymodeRelative`. `AbsoluteLegacy` conserva `max(0, DensityColumns-2)`. Ambas coinciden en 7K; un full chord recibe el mismo factor relativo en 4K–10K. El detector contextual tiene el mismo A/B: el modo relativo escala su piso como `KeyCount/7` y el mínimo micro como `2×KeyCount/7`.

`SimultaneousHeadColumns` cuenta lanes que reciben un tap head o LN head exactamente en el timestamp; no cuenta una tail por seguir activa. `HeldLnColumns` cuenta lanes con una LN activa. El factor de chord nació como protección anti-wall para heads simultáneos: por eso el candidato recomendado usa `SimultaneousHeads`. `HeldLnColumns` continúa siendo observable y la tail sigue participando íntegramente en geometría, overlap y gap. `OccupiedColumnsLegacy` conserva el comportamiento anterior para A/B. No existe un `HeldLanePressureFactor` en esta iteración.

El factor contextual usa solo heads originales. Compara una ventana micro de 1 beat con una mediana inferior robusta de muestras laterales dentro de ±4 beats, excluyendo el centro. Un tramo necesita al menos dos heads por ventana y razón micro/contexto ≥1.5. Tramos elevados contiguos de 1, 2 o 3 beats usan `0.45`, `0.60` o `0.80`; uno sostenido por 4+ beats usa `1.0`. Así un burst breve recibe más freno que rice sostenido.

### Separación local por lane

Se observan gaps positivos de hasta 1 beat entre objetos originales consecutivos de la misma lane en ±4 beats. Los gaps se agrupan a seis decimales y el menor con al menos dos observaciones se adopta como convención local. Sin soporte suficiente se usa el fallback `0.125` beat. Los sintéticos no enseñan el gap; sí participan en geometría.

### Vocabulario temporal relativo al mapa

La timeline usa `decimal` e integra puntos BPM no heredados. Los releases originales usados como anchors conservan exactamente su milisegundo. Las plantillas de duración trasladan su diferencia en beats al head source, incluyendo 1/5, 1/10, offsets y cambios de BPM; solo la escritura final redondea al milisegundo exigido por `.osu`.

La cuantización antigua a divisores `1,2,3,4,6,8,12,16` permanece exclusivamente detrás de `MapRelativeSnapEnabled=false` para A/B.

### Formas LN y oportunidades interiores

El contexto observa LNs originales con heads en `±LnWindowBeats`. Cada observación vota por duración trasladada y release exacto. Los votos coincidentes se fusionan, se ponderan por distancia y reciben afinidad `1.25` si reproducen la source. Ningún candidato puede ser más corto que el mínimo local.

Con la hipótesis interior activa, una parent de al menos 3 beats aporta como máximo dos oportunidades si contiene al menos dos anchors originales y dispone de tres LNs originales de contexto. La selección prioriza soporte, separación entre anchors y cercanía al centro; no toma simplemente los dos primeros. Los umbrales `1.5×` y `8 beats` aportan prioridad observable, pero ya no excluyen una frase uniforme de LNs de 3–6 beats.

Cada oportunidad conserva explícitamente `ParentOriginalLn`, `InteriorAnchor` y su start beat. Primero busca contexto alrededor del anchor y, si no alcanza tres observaciones, alrededor del head de la parent. Ambos caminos usan exclusivamente LNs de `OriginalObjects` y excluyen la propia parent. `ParentOriginalLn` es metadata y límite, no una observation source. No se inserta la LN virtual, no se crean votos hacia el release/duración de la parent y no se aplica source affinity interior.

Los candidatos interiores siguen naciendo sólo de plantillas de duración y releases originales, con timing relativo al mapa y mínimo LN local original. Short/medium/long son bandas observacionales basadas en mediana y cuartil superior locales; no alteran el peso. Un candidato sin lanes legales se descarta una vez antes del sorteo, sin retry geométrico, tap fallback ni duración inventada.

## Invariancia por keymode

Las cantidades temporales se expresan en beats: ventanas, gaps, duraciones y distancias. Las cantidades verticales publican versión absoluta y normalizada: `SimultaneousHeadColumns/Ratio`, `HeldLnColumns/Ratio`, `NonHeldColumns/Ratio` y `LegalLaneRatio`. Las cantidades geométricas siguen siendo discretas: una lane cabe o no cabe.

Sólo `OriginalObjects` alimenta densidad, contexto y gaps. Un índice congelado calcula probability factors; `CurrentGeometry`, que sí recibe sintéticos, se usa únicamente para overlap, separación y lanes legales. El parser admite 1K–18K; la fórmula es única y no contiene tablas por K.

## Articulación LN en segunda pasada

`ArticulationEnabled` procesa exclusivamente fallos interiores que ya ganaron el roll y ocurrieron en Full/Near-Full (`NonHeldColumns <= 1`). La primera pasada termina antes y la selección usa RNG derivado estable, por lo que OFF/ON produce los mismos `AddedObjects` y decisiones de pass 1.

Para una parent `[S,E]`, `H` debe ser un head de `OriginalObjects` con `S<H<E`. `LocalRetriggerGap` aprende transiciones LN-release → siguiente head de la misma lane; busca primero evidencia en la lane parent y solo entonces usa soporte cross-lane local. No existe fallback numérico. `R=H-gap` debe cumplir `S<R<H<E`, y `[S,R]` y `[H,E]` deben superar el mínimo LN local y ser geométricamente válidos.

El output sustituye la parent por dos segmentos en la misma lane y con los mismos extremos externos. La parent permanece congelada en `OriginalObjects`. Hay máximo una articulación por parent, sin cross-lane, release-only head, feedback ni recursión.

## Instrumentación

Trace, Web y CSV separan oportunidades/tiradas/placements rice, base-LN e interiores; ratios verticales; rechazos; y el funnel de articulación. `AddedHeads`, `AddedReleases` y `AddedInteractions` cuentan interacción neta, incluyendo +1 head/+1 release por parent articulada. También se registran `Pass1Ms`, `ArticulationPassMs`, geometría, objetos inspeccionados y conversiones beat. Los tiempos de pared no forman parte de tests frágiles.

## Limitaciones

- Umbrales y factores aún requieren playtesting.
- No se infieren hand balance, SV, stacks ni semántica musical global.
- El parser no conserva comentarios intercalados en `[HitObjects]`, encoding o newline exactos.
- Reprocesar un output lo convierte en nueva fuente de verdad.
- El formato final sigue cuantizado a milisegundos.
