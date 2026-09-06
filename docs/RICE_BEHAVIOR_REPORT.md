# Auditoría del comportamiento rice

## Respuestas

1. **Qué sigue igual:** un head tap original crea exactamente una oportunidad; si gana el roll, intenta un solo tap sintético en el mismo timestamp y elige uniformemente entre lanes legales. El sintético no crea oportunidades ni enseña densidad/contexto/gap.
2. **Qué modifican los factores:** `ChordFactor` reduce chance según heads simultáneos; `ContextFactor` reduce bursts locales de 1–3 beats. Ninguno aumenta chance sobre la base ni cambia timestamp/tipo/lane selection.
3. **Efecto de `SimultaneousHeads` en LN-heavy:** sí. En Spring, el cambio previo de `OccupiedColumnsLegacy` al modo heads elevó taps añadidos de ~192–193 a `256.73` por run en el binario final, porque las tails dejaron de penalizar probabilidad. Las tails todavía bloquean geometría.
4. **Dependencia de KeyCount:** la separación heads/tails es conceptual y funciona en todos los K; la magnitud con escala absoluta sí dependía de K.
5. **Bias de `AbsoluteLegacy`:** confirmado. Un full chord pasa de factor `0.4225` en 4K a `0.031864` en 10K.
6. **Efecto de `KeymodeRelative`:** reduce ese bias: full chord = `0.116029` en 4K–10K y coincide con legacy 7K. A porcentajes discretizados hay diferencias inevitables.
7. **Burst protection:** fixture equivalente de 1 beat produce `ContextFactor=0.45` en 4K–10K.
8. **Sustained:** el fixture de 4+ beats conserva factor `1.0` en 4K–10K.
9. **¿Hay evidencia objetiva para otra heurística rice?:** **NO**. No se añadieron `RiceChance`, strain, jack/trill, hand balance ni clasificadores de patrón.

## Spring of Dreams, ADD 50, seeds 1–100

| Métrica rice | Media/run |
|---|---:|
| oportunidades | 1,021.00 |
| rolls exitosos | 304.17 |
| placements | 256.73 |
| burst / sustained / normal | 52.36 / 14.82 / 189.55 |
| chance efectiva tap | 29.96% |
| chord factor tap | 71.66% |
| context factor tap | 82.08% |

La primera pasada es byte-a-byte equivalente en `AddedObjects` y métrica-a-métrica equivalente entre Articulation OFF/ON para las 100 seeds. Por tanto el nuevo modo LN no altera rice. La naturalidad de los 257 taps añadidos sigue siendo una cuestión de playtesting, no una conclusión automática.
