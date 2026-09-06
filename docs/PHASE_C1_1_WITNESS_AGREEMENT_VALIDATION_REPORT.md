# Phase C1.1 — Witness vs Agreement Validation

Fecha de cierre: 2026-09-06  
Estado: **OUTCOME B — WITNESS AGREEMENT IS A DISTINCT SIGNAL**  
Efecto conductual: **ninguno**. `BehaviorPolicyVersion` continúa en `legacy-experimental.1`.

## Corpus

El directorio externo `tests` se recorrió de forma recursiva y read-only. De 1.212 archivos `.osu` válidos, 1.200 son salidas sintéticas cuyo nombre contiene `[ADD …]`; no se usaron como mapas humanos. Había 12 ubicaciones originales y dos copias byte-a-byte idénticas de un mismo chart. Tras deduplicar por SHA-256 quedaron:

| Medida | Valor |
|---|---:|
| Families | 11 |
| Charts humanos únicos | 11 |
| Charts con al menos una LN | 8 |
| Keymodes | 4K, 7K, 10K |
| Original objects | 50.836 |
| Original taps | 38.282 |
| Original LNs / targets | 12.554 |
| Timing points positivos | 28 |

La familia se define por `Artist + Title + Creator` normalizados. `Version` identifica charts dentro de una familia. En este corpus cada familia aporta una sola dificultad única, de modo que micro y macro no esconden familias con muchas dificultades. La definición, BPM, conteos y rutas relativas están en [MAP_FAMILY_VALIDATION_INVENTORY.md](MAP_FAMILY_VALIDATION_INVENTORY.md). Ningún artifact público contiene la ruta personal absoluta.

Cada evaluación aprende exclusivamente de los `OriginalObjects` del chart que se está reconstruyendo. El corpus sólo agrega resultados; nunca dona evidencia entre charts, familias o keymodes.

## C1 HOLD recap

C1 demostró que una observación original que llega al mismo candidato por `Duration` y `ExactRelease` constituye un solo testigo independiente. No demostró que las dos labels fueran una sola unidad total de autoridad. Su shadow alternativo reemplazó la suma por paths por una contribución por `ObservationId`, pero el corpus disponible entonces no permitía separar double-counting de acuerdo estructural.

C1.1 conserva simultáneamente:

- `IndependentWitnessCount = 1` para una observación;
- dos labels descriptivas cuando existen (`Duration`, `ExactRelease`);
- un `WitnessAgreement` sin weight, bonus, score ni confidence.

Ni esta representación ni las evaluaciones siguientes participan en generación.

## Legacy algebra

Para un source/query con head \(S\) y un donor con head \(H\), release \(R\) y duración \(D = R-H\), la ruta de duración propone:

`DurationEndpoint = S + D = S + (R - H)`

La ruta exact-release propone:

`ExactReleaseEndpoint = R`

Si `S == H`, entonces necesariamente:

`S + (R - H) = H + (R - H) = R`

Por tanto, cuando source y donor comparten el mismo head exacto, la convergencia no es una coincidencia estadística: expresa dos relaciones originales concordantes. En decimal finito, la evaluación conserva además si la identidad reaparece bit-exacta; una resta y suma de beats periódicos puede perder exactitud decimal aun cuando la igualdad algebraica y el timestamp original sean exactos.

## Convergence classification

Se instrumentó la misma construcción legacy de candidatos, registrando una ruta descriptiva sólo después de que supera el mismo envelope de duración. El merge continúa usando exactamente `int EndTime`.

| Clasificación, LOO-object | Casos | % de convergencias |
|---|---:|---:|
| SameHeadAgreement | 14.828 | 100% |
| MaterializationCoincidence | 0 | 0% |
| OtherConvergence | 0 | 0% |

De los 14.828 acuerdos, 11.905 (80,29%) fueron también igualdad decimal/beat-space exacta y los 14.828 (100%) fueron igualdad exacta del endpoint en milisegundos. Ninguno dependió de `Nearly` o de una tolerancia. El fixture sintético separado demuestra que `MaterializationCoincidence` es clasificable legítimamente cuando dos beats distintos se fusionan por el snap soportado, pero ese caso no apareció bajo la configuración real del corpus.

Esta diferencia no invalida el álgebra de same-head: muestra el límite de representar beats racionales mediante `decimal` y confirma que el merge conductual efectivo ocurre en el endpoint entero legacy.

## Leave-one-object-out

Cada LN fue query y se retiró de donors y de la geometría. Las demás observaciones del chart, incluidas LNs simultáneas, permanecieron.

| Medida global (micro) | Resultado |
|---|---:|
| Targets | 12.554 |
| Target shape presente | 11.774 (93,79%) |
| Target con alguna lane legal | 10.976 (87,43%) |
| Target legal en su lane original | 10.541 (83,96%) |
| Legacy geometry top-1 | 4.796 |
| UniqueWitness geometry top-1 | 4.682 |
| Legacy mean rank | 2,5916 |
| UniqueWitness mean rank | 2,6337 |
| Legacy mean normalized weight | 0,35290 |
| UniqueWitness mean normalized weight | 0,34406 |
| Mean delta Unique − Legacy | **−0,008843** |
| Improved / Worsened / Tie | 3.258 / 3.103 / 4.615 |
| No evaluable | 1.578 |
| Mean log probability ratio | −0,02409 |

La vista macro entre los ocho charts/familias con LNs también es negativa: mean delta por chart **−0,02181**. El resultado no es una anomalía exclusiva de Spring.

Resumen por chart/familia con LN (LOO-object):

| Chart/familia | K | Targets | Legal | Improved | Worsened | Tie | Mean delta |
|---|---:|---:|---:|---:|---:|---:|---:|
| Midorigo Queen Bee | 7 | 21 | 6 | 0 | 2 | 4 | −0,05291 |
| Hakanaki Mono Ningen | 4 | 1.570 | 1.491 | 677 | 130 | 684 | +0,00471 |
| Kara Kara Kara no Kara | 7 | 3.958 | 2.942 | 835 | 967 | 1.140 | −0,01561 |
| Spring of Dreams | 7 | 3.587 | 3.475 | 901 | 1.277 | 1.297 | −0,00758 |
| Celestial Axes | 7 | 69 | 60 | 0 | 2 | 58 | −0,00352 |
| Ko Inu | 7 | 3.000 | 2.725 | 838 | 602 | 1.285 | −0,00690 |
| DESTINY | 7 | 296 | 231 | 2 | 98 | 131 | −0,04433 |
| MikiMiki Romantic Night | 7 | 53 | 46 | 5 | 25 | 16 | −0,04833 |

Los tres charts sin LNs se inventariaron y se ejecutaron, pero no se incluyen como ceros artificiales en los promedios macro de reconstrucción LN.

## Leave-one-head-group-out

Además de retirar la target, se excluyeron como donors todos los objetos con exactamente el mismo `StartTime`. No se usó ventana ni threshold nuevo para definir el grupo.

| Medida global (micro) | Resultado |
|---|---:|
| Targets | 12.554 |
| Target shape presente | 11.484 (91,48%) |
| Target con alguna lane legal | 10.726 (85,44%) |
| Target legal en su lane original | 10.317 (82,18%) |
| Legacy / Unique geometry top-1 | 4.426 / 4.426 |
| Legacy / Unique mean rank | 2,7061 / 2,7061 |
| Legacy / Unique mean normalized weight | 0,33530 / 0,33530 |
| Improved / Worsened / Tie | 0 / 0 / 10.726 |
| Mean delta | **0 exacto** |
| Agreements encontrados | **0** |

Toda diferencia entre `LegacyPathSum` y `UniqueWitness` desapareció. Esto demuestra que, en este corpus y con la ruta legacy actual, el fenómeno duplicado observado proviene íntegramente de otros donors del mismo head-group exacto.

## Structural twin sensitivity

Un structural twin se definió sin similaridad estilística: otro LN original con el mismo `StartTime` y `EndTime` exactos. No se introdujeron nombres de patterns ni thresholds.

| Grupo, LOO-object | Targets | Legal | Improved | Worsened | Tie | Mean delta |
|---|---:|---:|---:|---:|---:|---:|
| Con twin exacto / SameHead target | 3.771 | 3.461 | 76 | **3.103** | 282 | **−0,03848** |
| Sin twin exacto / no duplicate target | 8.783 | 7.515 | **3.182** | 0 | 4.333 | **+0,004804** |

Todos los 3.103 empeoramientos están concentrados en targets con twin exacto. En targets sin twin, deduplicar nunca empeoró la probabilidad normalizada evaluable y produjo mejoras pequeñas. En cambio, para twins exactos, la segunda label favorece consistentemente el endpoint que el mapper realmente reutilizó. La señal aparece en las ocho familias con LNs, no sólo en Spring.

Al aplicar LOO-head-group quedan 3.481 twins reconstruibles, 3.211 geometry-evaluable, y ambos pesos empatan en todos ellos porque se retiró precisamente la evidencia simultánea que originaba el acuerdo.

## Geometry-aware reconstruction

La geometría de evaluación contiene todos los `OriginalObjects` salvo la target. Luego usa el mismo `LaneGeometryIndex.FindLegalLnLanes`, el mismo gap local y descarta shapes sin lanes antes de normalizar, igual que la ruta que llega a `TakeWeighted`. Un test compara directamente la lista de lanes diagnóstica con la legacy bajo estado equivalente.

La target no puede bloquearse a sí misma. Se reportan por separado:

- alguna lane legal para el endpoint: 10.976 en LOO-object;
- lane original legal: 10.541.

La diferencia de 435 casos confirma que “shape reproducible” y “reproducible en la lane histórica” son preguntas distintas. La decisión B se sostiene después del filtro geométrico; no depende únicamente del ranking previo de shapes.

## Target normalized weight

Para cada target legal se calcula exactamente:

`TargetNormalizedWeight = TargetWeight / SumWeightsOfLegalCandidates`

El denominador contiene sólo shapes con al menos una lane legal, el mismo conjunto que alcanza `TakeWeighted`. No se consume RNG y la métrica no predice qué lane elegiría el sorteo posterior.

El delta pareado no usa threshold: `Improved`, `Worsened` y `Tie` comparan el `double` producido por las fórmulas reales. El resultado global negativo esconde dos efectos opuestos: UniqueWitness mejora levemente candidatos sin acuerdo, pero elimina una señal fuerte y repetida en structural twins/same-head agreements.

## Rank / Top1

Top-1 no reemplaza la probabilidad ponderada, pero se conserva como diagnóstico. En LOO-object UniqueWitness reduce top-1 de 4.796 a 4.682 y empeora mean rank de 2,5916 a 2,6337. En LOO-head-group ambos modelos producen exactamente 4.426 top-1 y mean rank 2,7061. Esta concordancia con normalized weight descarta que el hallazgo dependa de una sola métrica.

## Results by agreement kind

- `NoDuplicateWitness`: 8.783 targets; delta micro +0,004804; 3.182 improved, 0 worsened.
- `SameHeadAgreement`: 3.771 targets; delta micro −0,03848; 76 improved, 3.103 worsened.
- `MaterializationCoincidence`: 0 targets del corpus.
- `OtherConvergence`: 0 targets del corpus.
- `MultipleCauses`: 0 targets del corpus.

La degradación C1 no está distribuida al azar: está concentrada específicamente donde la misma observación demuestra simultaneidad exacta y endpoint exacto por dos relaciones.

## Micro vs macro family results

Micro (todos los targets) da delta −0,008843. Macro (primero cada chart/familia con LN) da −0,02181. Las magnitudes difieren porque las familias pequeñas muestran efectos más negativos, pero ambas direcciones coinciden. Ocho familias independientes contienen same-head agreements y empeoramientos asociados; no se interpreta cada target o cada copia de un chart como un estilo independiente.

Los CSV públicos contienen las vistas por chart, familia y scope estructural:

- [c1_1_chart_summary.csv](c1_1_chart_summary.csv)
- [c1_1_family_summary.csv](c1_1_family_summary.csv)
- [c1_1_agreement_summary.csv](c1_1_agreement_summary.csv)

El detalle por target queda fuera de Git en el directorio local de experimentos (aprox. 59,8 MB).

## Cross-key observations

El corpus aporta 4K, 7K y 10K; sólo 4K y 7K contienen LNs. Por tanto no se afirma validación humana LN para 10K. Fixtures sintéticos 1K, 4K, 7K, 10K y 18K verifican que la instrumentación no contiene branches por keycount. Ningún chart ni keymode dona evidencia a otro.

## Performance

Las 25.108 reconstrucciones (dos holdouts para 12.554 targets) tardaron aproximadamente 97,01 s acumulados en la ejecución final de esta máquina, sin contar discovery/serialización. El JSON detallado local mide aproximadamente 59,8 MB; por eso sólo se versionan summaries pequeños. La instrumentación se invoca desde el runner de investigación y no añade este coste al hot path normal cuando no se solicita.

## Interpretation

Los datos permiten conservar dos afirmaciones sin colapsarlas:

1. una observación es un solo testigo independiente;
2. sus dos labels pueden demostrar relaciones distintas y concordantes.

La suma legacy no queda validada como fórmula general: fuera de same-head, UniqueWitness mejora ligeramente. Tampoco queda validado reemplazarla ciegamente por autoridad única: en twins exactos elimina una señal predictiva multi-family, visible después de geometría y en probabilidad normalizada. C1.1 explica qué representa gran parte del aparente double-counting, pero todavía no decide cuánto debería pesar el acuerdo.

## Decision gate

**OUTCOME B — WITNESS AGREEMENT IS A DISTINCT SIGNAL.**

Se cumplen sus condiciones:

- `SameHeadAgreement` predice consistentemente el endpoint target en ocho familias;
- retirar la segunda label reduce la probabilidad normalizada especialmente en twins exactos;
- LOO-head-group elimina todos los acuerdos y toda diferencia de weights, localizando causalmente la señal;
- geometry-aware, rank, top-1 y normalized weight apuntan en la misma dirección.

La hipótesis conductual original de C1 queda **rechazada/reformulada**, no porque dos paths deban sumar siempre, sino porque `IndependentWitnessAuthority` y `WitnessAgreement` son dimensiones separadas.

Siguiente fase recomendada: **C1.2 — Witness Agreement Modeling / Shadow**. Debe preservar acuerdos descriptivos y estudiar su capacidad explicativa sin asignar todavía magic weights, bonus, score o confidence. C2 permanece pendiente y no se inicia.

## Regression contract

C1.1 no cambia generación. El cierre debe mantener:

- output legacy byte-a-byte;
- transcripción RNG;
- candidate weights activos;
- lane selection;
- articulación;
- `BehaviorPolicyVersion`.

La batería específica verifica clasificación, álgebra, coincidencia por materialización, testigo único/dos labels, no-duplicate, ambos holdouts, geometría, denominador `TakeWeighted`, ausencia de RNG, corpus read-only y fixtures 1K–18K.

Resultado final aislado, sin interferir con el proceso Web abierto:

- `dotnet restore`: PASS (`NU1900` ambiental al consultar vulnerabilidades de NuGet).
- `dotnet build -c Release`: PASS, 0 errores.
- `dotnet test -c Release`: **188 passed, 0 failed, 0 skipped**.
- output legacy: PASS, cinco snapshots históricos byte-a-byte.
- RNG: PASS, suite de transcripción ON/OFF y research sin fuente RNG.
- candidate weights activos: PASS; la ruta de generación sigue usando `Candidate.Weight` legacy.
- lane selection: PASS, snapshots y tests de geometría/selección existentes.
- articulación: PASS, snapshot combinado y tests ON/OFF existentes.

| Snapshot | SHA-256 |
|---|---|
| rice 4K | `1D3C7C1C93B3D4C9C86D9D542AB0780AF0FA6B0A6A05BF490735731BC98550B0` |
| LN 4K | `11B5FD207202BF9CEE16E4506AABA92C5D1FEE3CF754F369C21D736D8F698EB9` |
| 7K | `3D874B5F038E9D4371A5DBDDEB7D73CD9758FDEF338100E862F461EC97149104` |
| 18K | `219B1F9BC9D07B99F928CCC9AE4BB75F6C8E5A4FA5EC60365E89010D7176E391` |
| interior + articulation | `FBE3FDB25059000FFD3DD8B894B10CE040651D4A8989EAD72E458FE023A5CB1A` |
