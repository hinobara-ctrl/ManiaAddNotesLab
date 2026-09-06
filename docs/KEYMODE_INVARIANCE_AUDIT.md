# Auditoría de invariancia por keymode

Fecha: 2026-09-05. Alcance: parser, modelo, probabilidad, geometría, timing, métricas, CLI, Web y CSV. Esta auditoría clasifica parámetros; no afirma naturalidad jugable.

## KeyCount soportado

El parser y la validación aceptan `CircleSize` entero redondeado entre **1K y 18K**, inclusive. Los tests de frontera cubren 1K y 18K; la matriz comparable cubre 4K–10K. No existe lógica específica por keymode.

## Inventario de parámetros

| Parámetro/medida | Clase | Acción | Evidencia |
|---|---|---|---|
| ventanas micro/contexto/LN/gap/retrigger | temporal | conservar en beats | `BeatTimeline`, incluidas divisiones 1/5 y 1/10 |
| duración mínima parent, mínimo LN local | temporal | conservar en beats | fixtures con offset; los ms solo aparecen al escribir |
| `DensityGraceColumns=2` | vertical absoluta | normalizar opcionalmente | equivale a `GraceRatio=2/7` en modo relativo |
| paso de penalización de una columna | vertical absoluta | normalizar opcionalmente | equivale a `PenaltyRatioStep=1/7` |
| umbral contextual mínimo de 2 heads | vertical absoluta | normalizar opcionalmente | `2 × KeyCount/7` en modo relativo |
| piso contextual de 1 head/beat | vertical absoluta | normalizar opcionalmente | `KeyCount/7` en modo relativo |
| `HeldLnColumns`, lanes legales | geometría | conservar absoluto y añadir ratio | ambos se publican; la legalidad sigue siendo discreta |
| `NonHeldColumns <= 1` | geometría/saturación | conservar como trigger | expresa Full/Near-Full de forma portable sin fijar held lanes |
| soporte mínimo de 2 observaciones | evidencia | conservar absoluto | cuenta ejemplos temporales, no columnas |
| máximo 2 anchors interiores por parent | cap estructural | conservar absoluto | no depende de K |
| máximo 1 articulación por parent | cap estructural | conservar absoluto | evita segmentación recursiva |
| chance y factores 0.65/0.45/0.60/0.80 | probabilidad | conservar | son multiplicadores adimensionales |

## Hallazgo y corrección

El modelo absoluto penalizaba un full chord con exponentes 2 en 4K, 5 en 7K y 8 en 10K. Se añadió `VerticalDensityScaleMode`:

```text
HeadRatio = SimultaneousHeadColumns / KeyCount
GraceRatio = 2 / 7
PenaltyRatioStep = 1 / 7
EquivalentPenaltySteps = max(0, (HeadRatio - GraceRatio) / PenaltyRatioStep)
ChordFactor = 0.65 ^ EquivalentPenaltySteps
```

Por ello un full chord vale `0.116029` en todos los K probados. `AbsoluteLegacy` permanece para A/B.

El detector contextual también contenía dos mínimos absolutos. `ContextualDensityScaleMode.KeymodeRelative` usa piso `KeyCount/7` y mínimo micro `2×KeyCount/7`; 7K queda exactamente preservado. `AbsoluteHeadCountLegacy` permanece disponible.

Durante los tests se encontró además que el índice geométrico mutable podía hacer que una nota sintética previa del mismo chord alterara `SimultaneousHeadColumns`. Se separó un índice de densidad congelado: sólo `OriginalObjects` enseñan densidad; `CurrentGeometry` sigue incluyendo sintéticos para colisiones.

## Evidencia

- Parser: 1K, 4K–10K y 18K.
- Factores relativos: full chord = `0.116029` en 4K–10K.
- Burst equivalente de 1 beat: factor contextual `0.45` en 4K–10K.
- Densidad sostenida: factor contextual `1.0` en 4K–10K.
- Tails: no cambian el chord factor en `SimultaneousHeads`, pero reducen lanes legales.
- Matriz reproducible: [cross-key-matrix.csv](../ab-results/cross-key-matrix.csv) y [vertical-density-matrix.csv](../ab-results/vertical-density-matrix.csv).

Conclusión objetiva: la política relativa elimina el sesgo matemático del full chord y conserva el punto 7K. La discretización de lanes impide igualdad perfecta a 25/50/75%. Esto no demuestra naturalidad; requiere charts reales y playtesting por K.
