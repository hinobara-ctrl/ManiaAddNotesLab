# Informe multikey 4K–10K

Matriz: chances `0.10/0.30/0.50`, seeds `1–20`, escenarios Full (`NonHeld=0`), Near-Full (`1`) y Moderate (`2`). La tabla siguiente muestra Full a chance 0.50; cada cifra es media de 20 seeds. Las cantidades absolutas no deben coincidir entre keymodes: la comparación válida es la política relativa.

| K | HeadRatio | HeldRatio | ChordFactor | ContextFactor | Tap opp/roll/place | Interior opp/roll/free | Artic. eligible/place | AddedInteractions |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 4 | 0.730 | 0.684 | 0.116 | 0.45 | 2 / 0.05 / 0 | 2 / 0.30 / 0 | 0.10 / 0.10 | 0.70 |
| 5 | 0.775 | 0.661 | 0.116 | 0.45 | 3 / 0 / 0 | 2 / 0.40 / 0 | 0.10 / 0.10 | 0.70 |
| 6 | 0.807 | 0.650 | 0.116 | 0.45 | 4 / 0.05 / 0 | 2 / 0.35 / 0 | 0.05 / 0.05 | 0.60 |
| 7 | 0.833 | 0.647 | 0.116 | 0.45 | 5 / 0.05 / 0 | 2 / 0.20 / 0 | 0 / 0 | 0.60 |
| 8 | 0.852 | 0.645 | 0.116 | 0.45 | 6 / 0.15 / 0 | 2 / 0.30 / 0 | 0.10 / 0.10 | 0.90 |
| 9 | 0.867 | 0.645 | 0.116 | 0.45 | 7 / 0.20 / 0 | 2 / 0.45 / 0 | 0.05 / 0.05 | 0.80 |
| 10 | 0.879 | 0.645 | 0.116 | 0.45 | 8 / 0.30 / 0 | 2 / 0.35 / 0 | 0.05 / 0.05 | 0.70 |

La baja frecuencia de rolls es esperable: `0.50 × 0.116 × 0.45 ≈ 2.61%`. Los ceros de una celda con 20 seeds no son fallo estructural. Con chance 1 y sin factor contextual, los tests dirigidos colocan la articulación del parent objetivo en Full y Near-Full para todos los K; Moderate la rechaza para todos los K.

## Factor vertical A/B

| K | Full AbsoluteLegacy | Full KeymodeRelative |
|---:|---:|---:|
| 4 | 0.422500 | 0.116029 |
| 5 | 0.274625 | 0.116029 |
| 6 | 0.178506 | 0.116029 |
| 7 | 0.116029 | 0.116029 |
| 8 | 0.075419 | 0.116029 |
| 9 | 0.049022 | 0.116029 |
| 10 | 0.031864 | 0.116029 |

## Evidencia estructural vs playtesting

Queda verificado: fórmula única 4K–10K, límites 1K/18K, no-overlap, gap válido, timing relativo, determinismo, writer válido, cap por parent y primera pasada idéntica ON/OFF. La matriz completa está en [cross-key-matrix.csv](../ab-results/cross-key-matrix.csv).

No queda verificado por fixtures: comodidad, lectura, hand balance, dificultad o naturalidad. Esos puntos necesitan charts reales de personas que jueguen cada keymode.
