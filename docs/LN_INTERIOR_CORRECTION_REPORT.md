# Corrección de interacción LN interior

Fecha: 2026-09-05. Caso principal: `Spring of Dreams [KNH - Lvl 81 (No SV)] CUSTOM`, 4.608 objetos (1.021 taps, 3.587 LNs). Chance 0.50, seeds 1–100, resto de parámetros constante. No es un resultado de playtesting humano.

## Root causes verified

- **A — eligibility:** el modo legacy exigía 4 beats, tres LNs locales y además `1.5 × mediana` o 8 beats. Una frase uniformemente larga se comparaba consigo misma y quedaba fuera.
- **B — vertical density:** `CountOccupiedColumns` usaba la misma prueba que un tap y contaba tails activas durante todo el hold, reduciendo el factor anti-wall aunque no hubiera nuevos heads simultáneos.
- **C — virtual context:** la oportunidad interior construía una LN virtual desde el anchor hasta el release de la parent; `BuildLocalLnContext` la insertaba si no estaba entre originales y `BuildReleaseCandidates` le aplicaba afinidad de source.
- **D — shapes:** 79.9% de candidates LN del baseline no tenían lane legal; el sesgo anterior favorecía especialmente shapes largos hasta la parent release.

## Changes implemented

- **Context:** la oportunidad conserva parent/anchor/start explícitos. El modo `OriginalOnly` busca LNs originales alrededor del anchor y luego alrededor del head de la parent, excluye parent/added/virtual y rechaza si no llega al soporte mínimo. No hay affinity interior.
- **Eligibility:** `AnchorSupported` usa mínimo 3 beats, tres LNs originales, dos anchors originales y máximo dos oportunidades. 3 beats fue elegido porque 4 conservaba sólo 22 oportunidades en el smoke test, sin mejora material frente a 23. Ratio 1.5 y absoluto 8 son prioridad, no gates.
- **Vertical density:** `SimultaneousHeadColumns` y `HeldLnColumns` son medidas separadas. `SimultaneousHeads` alimenta el chord factor; holds conservan toda la autoridad geométrica. `OccupiedColumnsLegacy` queda disponible.
- **Interior candidates:** sólo plantillas/release originales y timing relativo al mapa; mínimo local original. Las bandas short/medium/long son métricas, no una fórmula. No se inventan micro-LNs ni fallback a tap.
- **Observability:** Core, CLI, Web, CSV y trace exponen embudo base/interior, rechazos de elegibilidad, chance interior, heads/holds, bandas, imposibilidad y causas overlap/gap. `LnShapeNoLegalLane` reutiliza el contador histórico `LnCandidatesImpossible` y se publica con el nombre nuevo en CSV para no duplicarlo.

## Tests

51 passed, 0 failed. Cubren original-only, ausencia de parent/virtual/synthetic source, votos y affinity, fallback original, eligibility uniforme, anchors added ignorados, heads vs tails, geometría aún bloqueada, 1/5 y 1/8 exactos, bandas/rechazos, máximo 2, determinismo, originales intactos, no-overlap y gap.

## Spring of Dreams A/B

| Modo | Interior opp/run | Rolls/run | Placed/run | Effective | Interior effective | Added taps | LN heads | LN interior | Result LN | Failed |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| A Legacy | 23.00 | 3.07 | 2.77 | 15.43% | 12.41% | 193.20 | 404.87 | 2.77 | 76.69% | 111.75 |
| B OriginalOnly | 23.00 | 3.71 | 3.57 | 15.45% | 16.37% | 192.14 | 406.11 | 3.57 | 76.71% | 111.06 |
| C + AnchorSupported | 76.00 | 10.71 | 9.01 | 15.41% | 13.92% | 191.98 | 405.97 | 9.01 | 76.74% | 113.14 |
| D + SimultaneousHeads | 76.00 | 29.79 | 16.07 | 32.87% | 38.78% | 252.18 | 680.78 | 16.07 | 77.09% | 596.05 |

El modo combinado aumenta oportunidades 3.30× y placements interiores 5.80×, siempre acotado a dos anchors por parent. El incremento global de D es grande (949.03 placements frente a 600.84) porque elimina deliberadamente el castigo de tails a todas las oportunidades, no porque suba chance ni fuerce LNs; debe evaluarse visualmente antes de cambiar parámetros.

## Candidate y geometría

| Modo | Short | Medium | Long | Interior impossible | Interior impossible rate | All LN impossible rate |
|---|---:|---:|---:|---:|---:|---:|
| A | 8.18 | 3.72 | 25.85 | 20.74 | 54.94% | 79.92% |
| B | 9.51 | 2.15 | 29.96 | 20.49 | 49.23% | 79.81% |
| C | 23.66 | 9.81 | 103.13 | 88.50 | 64.79% | 79.70% |
| D | 71.46 | 39.59 | 342.91 | 371.61 | 81.86% | 87.66% |

El porcentaje combinado no baja: D hace muchas más tiradas LN en una geometría ocupada. Esto es atribuible y no relajó la seguridad. Las medias de D son 2.618 heads simultáneos y 4.475 held lanes; su diferencia verifica que antes se mezclaban fenómenos distintos.

Rechazos de eligibility por run en D: 3.543 too short, 2 insufficient context, 4 no supported anchors, 0 legacy-length. Los contadores de forma pueden registrar overlap y gap en el mismo candidate imposible cuando lanes distintas fallan por razones distintas.

## Structural validity

En el output D seed 100: 4.608/4.608 originales presentes, 0 faltantes y 0 overlaps inclusivos adyacentes en cualquier lane. Las pruebas confirman gap válido y ningún overlap sintético. Parser/writer no fueron modificados.

## Performance

El índice geométrico se conserva y cada candidate calcula lanes una sola vez. Promedio `context + candidate + geometry` A: 32.12 ms; D: 58.82 ms. El coste absoluto crece 26.70 ms porque D evalúa muchas más tiradas/candidates, no por scans completos. Tests estructurales mantienen objetos inspeccionados acotados; no hay regresión importante del índice.

## Determinism

PASS: mismo input/config/seed produce output, oportunidades, métricas, trace, candidates y placements idénticos en los fixtures. RNG y orden estable no cambiaron.

## Artifacts

- A CSV: `src/ManiaAddNotesLab.Web/batch-results/20260905-182324-355157240ac2408e9a5d035d48d68d43/runs.csv`
- B CSV: `src/ManiaAddNotesLab.Web/batch-results/20260905-182324-5cdfb4f74f7c412fab2feed620d9d2f3/runs.csv`
- C CSV: `src/ManiaAddNotesLab.Web/batch-results/20260905-182324-26fec497ec9b4236abe80597965c1f26/runs.csv`
- D CSV/trace: `src/ManiaAddNotesLab.Web/batch-results/20260905-182917-a4eff1f350264a5b830142c36de64043/`
- Output manual D seed 100: `ab-ln-results/Spring-of-Dreams-D-seed100.osu`

## Questions for playtesting

1. ¿El aumento global de D se siente natural o `SimultaneousHeads` necesita una calibración posterior separada?
2. ¿16 LNs interiores/run aportan interacción visible en las frases problemáticas sin crear lectura monótona?
3. ¿La mezcla short/medium/long respeta el estilo percibido aunque sobrevivan más shapes largos?
4. ¿AiMod reporta algún aviso nuevo respecto del original en el output D seed 100?

EXPERIMENT READY FOR LN PLAYTESTING
