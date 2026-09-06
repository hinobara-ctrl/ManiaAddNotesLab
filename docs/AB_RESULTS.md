# Resultados A/B reproducibles

Todas las parejas usan el mismo input, seed `42` y configuración restante. Son comprobaciones objetivas, no validación de calidad jugable.

| Comparación | A | B | Diferencia observada |
|---|---:|---:|---|
| Densidad contextual, chance 80% | OFF: 5 colocados, chance efectiva 43.80% | ON: 4 colocados, 41.05% | ON ajustó 5 oportunidades y añadió una LN menos. |
| Interiores, chance 100% | OFF: 7 oportunidades, 4 LN desde heads | ON: 9 oportunidades, 4 LN desde heads + 2 interiores | El máximo 2 se respetó; las métricas separan ambos orígenes. |
| Gap local, rango 1000 ms | fijo: nueva LN en lane 1 | local: nueva LN en lane 3 | El gap aprendido de 1/4 beat rechazó la lane cuyo gap era 1/5; el fallback 1/8 la aceptó. |
| Snap temporal, chance 100% | estándar: ejemplos `607→601`, `1337→1363` | relativo: conserva `557`, `1067`, `1337`, `1667` | El modo relativo conserva anchors/duraciones 1/5–1/10 con offset/BPM. |

Los `.osu` de cada lado están en `ab-results/`. Antes de concluir sobre AiMod o naturalidad deben revisarse visualmente y jugarse.

## Rendimiento manual LN-heavy

Comando equivalente en Release, seed `4242`, chance `1`, 1000 LNs de entrada:

| Medición | Antes | Después |
|---|---:|---:|
| Pared | 1076.59 ms | 514.24 ms |
| Objetos colocados | 1 | 0 |
| Retries/candidatos imposibles | 3718 | 3994 |
| Geometría interna | no instrumentada | 7.36 ms |
| Checks geométricos | no instrumentado | 34,958 |
| Objetos inspeccionados | no instrumentado | 40,263 |
| Conversiones beat | no instrumentado | 9,321 |

La reducción de pared observada es ~52.2%, pero no es un benchmark controlado y el comportamiento cambió por las nuevas políticas por defecto. La evidencia firme es estructural: el hot path usa búsqueda binaria y vecinos, no escanea los 1000 objetos por cada lane/candidato.
