# Informe de articulación LN

## Modelo implementado

La segunda pasada recibe únicamente intents de oportunidades interiores que ganaron su roll, fallaron toda forma/lane normal y ocurrieron con `NonHeldColumns <= 1`. No vuelve a generar oportunidades.

Para una parent original `[S,E]`, `H` debe ser un head original estricto dentro de la parent. `R` se obtiene de un gap LN-release → siguiente head observado repetidamente en `OriginalObjects`: primero misma lane del parent y, si no hay soporte, fallback cross-lane documentado. Se exige `S < R < H < E`, mínimo LN local en ambos segmentos y geometría válida. El output sustituye la línea parent por `[S,R]` y `[H,E]` en la misma lane; `OriginalObjects` conserva `[S,E]` intacta. Máximo: una articulación por parent.

La selección usa una sub-seed estable, separada del RNG de la primera pasada. No usa releases-only como `H`, added objects, timestamps inventados, otra lane, feedback ni recursión.

## Spring of Dreams A/B

Chart real 7K: 4,608 objetos, 1,021 taps y 3,587 LNs. ADD 50, configuración moderna relativa, seeds 1–100.

| Métrica | OFF | ON |
|---|---:|---:|
| placements pass 1 | 980.17 | 980.17 |
| taps añadidos | 256.73 | 256.73 |
| LN interiores normales | 17.44 | 17.44 |
| fallos pass 1 | 617.98 | 617.98 |
| intents saturados | 8.34 | 8.34 |
| eligible | 0 | 8.34 |
| articulaciones | 0 | 6.72 |
| AddedInteractions | 1,703.61 | 1,717.05 |
| Result LN ratio | 77.1351% | 77.1625% |

Hubo **0 diferencias** en 14 métricas decisionales de pass 1, seed por seed. Las 100 seeds ON colocaron al menos una articulación; rango `3–10`, total `672`.

## Funnel y rechazos ON

| Métrica | Media/run |
|---|---:|
| interior no free lane | 14.64 |
| llega a Full/Near-Full | 8.34 |
| elegible tras head original | 8.34 |
| colocado | 6.72 |
| rejected anchor not head | 0 |
| rejected not saturated | 6.30 |
| rejected no retrigger gap | 0 |
| rejected left too short | 2.90 |
| rejected right too short | 0 |
| rejected parent cap | 0.81 |
| rejected geometry / already modified | 0 / 0 |

Los rechazos pueden coexistir entre candidates de un parent, por eso no deben sumarse como partición simple.

## Distribución multikey y saturación

La matriz 4K–10K usa Full, Near-Full y Moderate, chances `0.10/0.30/0.50`, 20 seeds por celda. Con chance real los placements son raros porque el fixture combina full chord y burst (`ChordFactor 0.116`, `ContextFactor 0.45`). Los tests deterministas a chance 1 confirman en cada K: parent objetivo articulado en Full y Near-Full; parent no articulado en Moderate (`NonHeld=2`). Esto respalda `NonHeldColumns` como trigger portable, sin afirmar naturalidad.

## Validez, determinismo y rendimiento

- 121 tests: semántica rice, selección de lane, no overlap, mismos extremos/lane, original inmutable, head original, release-only rechazado, 1/5, 1/10, offset, cambio BPM, mínimo de segmento, cap, no feedback, identidad pass 1, determinismo y writer reparseable.
- Spring ON: `Pass1Ms=94.96`, `ArticulationPassMs=0.92`, `GeometryMs=34.30` de media en la ejecución concurrente; son observaciones, no tests de tiempo.
- El retrigger se preindexa por lane y la geometría inspecciona vecinos del parent; no hay scan completo por candidate.

CSV A/B final: `src/ManiaAddNotesLab.Web/batch-results/20260905-215049-4cc400dfd49e411091374b9edb87c7be/runs.csv` (OFF) y `20260905-215050-0bc8c2ee5b384bcf9a0c2727374d8b48/runs.csv` (ON).
