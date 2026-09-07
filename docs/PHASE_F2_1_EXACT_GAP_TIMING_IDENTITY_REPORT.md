# Phase F2.1 — Exact Gap Timing Identity / Shadow Validation

**Estado durante el diseño: IN PROGRESS — RESEARCH/SHADOW ONLY.**  
**Behavior change:** no.  
**Research schema propuesta:** `phase-f2-1-exact-gap-timing-shadow.1`.

Este documento fija la pregunta, el gate, los outcomes y el criterio de generalidad antes de ejecutar validación sintética o corpus humano F2.1. El report F2 permanece histórico e inalterado.

## Pregunta de investigación

¿Existe una representación exacta, determinista y derivada exclusivamente de timestamps/timing points del chart que agrupe gaps serializados musicalmente equivalentes sin round, epsilon, nearest snap, vocabulario arbitrario de denominadores, frequency authority ni pérdida silenciosa de diferencias reales?

Se conservan dos niveles independientes:

1. `FileExactGap`: diferencia `decimal` reconstruida por F2; permanece como baseline y provenance.
2. `CandidateTimingIdentity`: representación alternativa sometida a ground truth y controles negativos. Nunca reemplaza destructivamente a FileExact.

## Criterios fijados antes de resultados

Una identity es **general enough** sólo si cubre los cuatro transition kinds F2, intervals same-segment y cross-redline, LNs cuyo head/release atraviesa redlines y todos los keymodes soportados con una sola implementación. Debe conservar provenance completa y formar clases de equivalencia calculables por occurrence, sin consultar el resto del corpus.

Una identity es **usable** sólo si obtiene cero false merge y cero false split en todo el ground truth sintético obligatorio, incluidos intervals nominalmente iguales que alternan timestamps por discretización y controles cercanos intencionalmente distintos. No se fija un mínimo de support humano: coverage, support y ambiguity son sólo descripción.

Una identity de **restricted domain** satisface exactitud/equivalencia y ground truth sólo en un subconjunto estructural explícito —por ejemplo same timing segment— y se abstiene fuera de él. No puede presentar el subconjunto como solución general.

## Modelos candidatos previos

| Modelo | Información | Exactitud / pérdida | Supuestos y riesgo | Estado teórico inicial |
|---|---|---|---|---|
| `FileExactDecimal` | timestamps enteros + redlines decimales | Exacto respecto del archivo; no pierde nada de F2 | No expresa intención previa a serialización; fragmenta intervals nominales | Control obligatorio, no identity musical nueva |
| `ExactSegmentTraversal` | secuencia exacta de redlines atravesadas y beat span exacto por segmento | Exacto y auditable; agrega estructura, no aproxima | Puede separar aún más y no corrige discretización | Admitido a synthetic como candidato descriptivo |
| `ExactMillisecondInterval` | diferencia entera de timestamps | Exacto en ms, pierde escala beat/BPM | Fusiona relaciones distintas que duran los mismos ms | Rechazado teóricamente; negative control obligatorio |
| `ExactRationalDeltaMsOverBeatLength` | delta ms y beat length decimal como razón exacta | Algebraicamente exacto | En same-segment equivale al beat gap F2; no recupera valor pre-serialización | Admitido sólo como equivalencia demostrable con baseline, no como solución |
| `EndpointPhaseRelation` | offsets exactos desde redline | Exacto pero depende de fase/posición absoluta | Separa recurrencias trasladadas; no define interval equivalence útil | Rechazado teóricamente |
| `NearestSubdivision` | lista de denominadores + métrica/distancia | Descarta error por una regla externa | Requiere vocabulario, tolerancia y tie-break no presentes en `.osu` | Prohibido/rechazado |
| `PairwiseEpsilon` | distancia entre gaps | Aproximado y no necesariamente transitivo | Membership depende de vecinos/orden | Prohibido/rechazado |
| `FrequencyChosenClass` | corpus y counts | No define identidad desde una occurrence | Convierte frecuencia en authority e invade C2 | Prohibido/rechazado |

## Holdout endpoint-aware predefinido

F2 excluyó los grupos de heads de previous y next mediante `{StartTime, StartBeat}`. F2.1 conservará ese resultado como `F2HeadGroup` y lo comparará con `TransitionEndpointGroup`:

- source Tap: `HeadEventGroup` por `{StartTime, StartBeat}`;
- source LN: `ReleaseEventGroup` por `{EndTime, EndBeat}` entre LNs;
- destination: `HeadEventGroup` por `{StartTime, StartBeat}`;
- exclusión target: unión exacta de los grupos source/destination.

La comparación es semántica y descriptiva. Una variación de support no elige el holdout.

## Gate F2.1 predefinido

1. baseline repo green; 2. F2 FileExact reproducible; 3. FileExact preservado como provenance; 4. candidate identity explícita; 5. sin round; 6. sin epsilon; 7. sin nearest snap; 8. sin lista arbitraria de denominadores; 9. sin frequency authority; 10. sin score; 11. sin probability; 12. sin confidence; 13. equivalencia reflexiva; 14. simétrica; 15. transitiva; 16. membership determinista; 17. synthetic ground truth disponible; 18. negative controls disponibles; 19. false merges medibles; 20. false splits medibles; 21. transition kind exacto; 22. TapHead distinto de LNHead; 23. Release distinto de Head; 24. same-segment probado; 25. crossing de timing change probado; 26. LN cruzando timing change probada; 27. original-only; 28. whole-head-group exclusion; 29. release endpoint holdout auditado; 30. cross-chart cero; 31. synthetic teaching cero; 32. shadow-only; 33. sin dependency de generation; 34. legacy byte-exact; 35. RNG legacy exacto; 36. artifacts deterministas; 37. implementación multikey; 38. sin branches estilísticas por keymode; 39. docs consistentes; 40. root guard verde; 41. suite completa verde.

No se retirará ninguna condición después de observar resultados. Si una candidate identity falla synthetic ground truth, no se ejecutará el corpus humano con ella para buscar justificación estadística.

## Outcomes predefinidos

- **Outcome A:** al menos una identity exacta, sin tolerancia/snap/frequency, forma equivalence classes válidas, pasa todo ground truth y controles negativos, cubre el dominio relevante y puede alimentar una futura repetición shadow F2. No autoriza behavior.
- **Outcome B:** una identity exacta pasa bajo un dominio estructural relevante pero restringido; la generalización queda incompleta. No behavior.
- **Outcome C:** ninguna identity temporal exacta y útil puede recuperarse del archivo sin introducir inferencia de quantization/tolerance. Esto recomendaría investigar esa hipótesis en otra fase explícita, no implementarla aquí.

## Baseline registrado

- commit inicial: `bbc6b7d084d8565f32821300be0322a6848d3b65`, rama `main`, working tree limpio;
- restore/build/DocConsistency/diff check: PASS; tests: 349/349;
- F2 se reprodujo sobre el snapshot de 11 charts: 50.762 candidates, 2 local support, 4.051 mismatch, 18 no-context, 46.691 ambiguous y 0 global support;
- ocho CSV agregados son SHA-256 idénticos a los publicados; chart summary sólo cambia en `relative_path` por el nombre del snapshot local.

Una carpeta Web posterior contenía además un chart humano nuevo (`senya - Nagori Tori`, 1.255 candidates). Se excluyó del snapshot control F2.1 para no cambiar silenciosamente el corpus histórico de 11 charts.

## Auditoría del formato temporal

El `.osu` conserva los timestamps de heads y releases como enteros en milisegundos. Los offsets y beat lengths de timing points se parsean como `decimal`, conservando el valor textual representable por .NET; sólo los puntos uninherited con beat length positivo entran a `ManiaChart.TimingPoints`. Los inherited/SV negativos permanecen en `Lines` para escritura, pero no afectan `BeatTimeline` ni beat identity.

`BeatTimeline` ordena redlines, integra el beat acumulado exactamente con aritmética `decimal` y reconstruye cada endpoint mediante `segment.StartBeat + (time - segment.Time) / segment.BeatLength`. La conversión inversa a output redondea al milisegundo entero con `MidpointRounding.AwayFromZero`. Por ello el archivo no conserva la coordenada sub-ms previa a serialización, el divisor de snap usado, una etiqueta “1/2 beat” ni la intención del mapper. Varias posiciones ideales pueden producir el mismo timestamp y una serie ideal repetida puede producir deltas enteros alternados.

## Implementación research

`ExactGapTimingIdentityResearch` conserva para cada transition original F2:

- fingerprint, lane, transition kind e IDs previous/next;
- endpoints tipados y sus timestamps/beats file-exact;
- redline exacta de cada endpoint;
- `FileExactDecimal` sin modificación;
- `ExactSegmentTraversal`, secuencia ordenada `{BeatLength, exact BeatSpan}`;
- head/release/transition endpoint groups;
- flags same-segment, redlines atravesadas, LN head→release cross-segment y shared release.

La equality de ambas identities es igualdad estructural de un valor canónico calculado por occurrence. Es reflexiva, simétrica, transitiva e independiente del orden de enumeración. No recibe RNG y no está referenciada por `AddNotesEngine`, CLI o Web.

`ExactSegmentTraversal` fue implementada porque añade provenance exacta para crossings, no porque se presumiera musicalmente superior. En same-segment su información se reduce a beat length + FileExact span. En multi-segment conserva cada porción y puede distinguir un boundary redundante; nunca fusiona por cercanía.

## Synthetic ground truth y controles negativos

Se fijaron ocho escenarios, separados del corpus humano:

| Escenario | Ground truth | FileExact | SegmentTraversal |
|---|---|---|---|
| 1/2 beat a 500 ms/beat, 250/250 ms | equivalente | PASS | PASS |
| 1/2 beat a 499 ms/beat, 250/249 ms | equivalente | **FALSE SPLIT** | **FALSE SPLIT** |
| 249 vs 250 ms a 500 ms/beat, intencionalmente distintos | diferentes | PASS | PASS |
| 250 ms bajo 500 vs 250 ms/beat | diferentes | PASS | PASS; `ExactMilliseconds` habría false-merge |
| mismo gap, transition kinds distintos | diferentes | PASS | PASS |
| mismo beat gap cruzando redline redundante de igual BPM | equivalente | PASS | **FALSE SPLIT** |
| path exacto cruzando cambio BPM | provenance | PASS | PASS descriptivo |
| LN head→release cruzando BPM y release→next posterior | endpoint/provenance | PASS | PASS descriptivo |

False merges de FileExact y SegmentTraversal: 0 en los controles construidos. False splits obligatorios: FileExact 1; SegmentTraversal 2. La traversal no satisface el criterio de identity musical usable. `ExactMilliseconds` fue descartada porque fusionaría 250 ms = 1/2 beat a un BPM con 250 ms = 1 beat a otro. La razón exacta `deltaMs / beatLength` reproduce FileExact en un segmento. Phase/absolute coordinates separan recurrencias trasladadas. Epsilon, nearest snap, denominadores y frequency fueron rechazados antes de implementación.

Este resultado no depende de soporte humano: demuestra una pérdida de información. Dados sólo timestamp entero y redline, no se puede distinguir qué coordenada sub-ms o subdivisión nominal originó el valor. Cualquier mapping no inyectivo que fusione 250/249 debe incorporar una regla adicional no codificada en el archivo.

## Timing changes

Los tests cubren same-segment, cross-redline, igual BPM con redline redundante, cambio real de BPM, LN cuyo head/release cruza el cambio y release→next completamente posterior. `SegmentTraversal` conserva una lista exacta de porciones; no simplifica al BPM inicial o final. Los inherited timing points no participan porque el parser correctamente los separa de redlines de beat timing.

La representación es exacta para crossings, pero no usable como equivalencia musical general: incluir boundaries produce false splits; retirarlos exige decidir qué estructura temporal puede descartarse. F2.1 no toma esa decisión.

## Audit endpoint-aware holdout

El control sintético contiene dos LNs de lanes distintas con heads diferentes y el mismo release. Sus siguientes heads ocurren en tiempos distintos. Bajo `F2HeadGroup`, la segunda LN permanece como donor global y el target queda `GlobalMismatch`. Bajo `TransitionEndpointGroup`, el release compartido excluye esa LN y el mismo target queda `Global NoComparableContext`.

| Holdout | Excluded observations | Global donors | Global state |
|---|---:|---:|---|
| F2 head groups | 2 | 1 | `LocalMismatch` |
| Endpoint-aware | 3 | 0 | `NoComparableContext` |

Esto demuestra que el riesgo es real y que endpoint-aware holdout es semánticamente más fiel para release-origin transitions: el endpoint retenido no puede enseñarse a sí mismo mediante otra LN. Endpoint leakage quedó en cero. No se corrige retrospectivamente F2 ni se reinterpretan sus cifras.

El impacto humano de este hardening queda **NOT RUN**: la candidate timing identity falló el synthetic gate, y ejecutar el corpus después para obtener una justificación estadística violaría el protocolo predefinido. El runner futuro puede aplicar ambos holdouts cuando exista una identity candidata que pase ground truth.

## Corpus humano F2.1

**NOT RUN después del synthetic gate.** No existen `f2_1_global/chart/family/identity_class/transition/stratification` humanos. F2 FileExact sí fue reproducido previamente como control obligatorio sobre los mismos 11 charts: 50.762 candidates, 1.809 gaps, 2 support, 4.051 mismatch, 18 no-context, 46.691 ambiguous y global support 0.

No se midieron merges ni estados humanos alternativos porque ninguna nueva identity sobrevivió ground truth. Publicar tablas vacías o ejecutar igualmente el corpus habría ocultado el motivo del Outcome C.

## Determinismo y artifacts

El runner `f2-1-synthetic` produce cinco CSV estables y un detail local pequeño:

- `f2_1_identity_model_summary.csv`;
- `f2_1_synthetic_validation_summary.csv`;
- `f2_1_negative_control_summary.csv`;
- `f2_1_timing_segment_summary.csv`;
- `f2_1_endpoint_holdout_summary.csv`;
- `.artifacts/f2-1/f2_1_detail.json`, ignorado.

No contienen timestamps de ejecución, rutas absolutas ni timings de máquina. Una segunda ejecución produjo hashes SHA-256 idénticos para los cinco CSV.

```powershell
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- f2-1-synthetic docs .artifacts/f2-1/f2_1_detail.json
```

## Gate final

| # | Condición | Resultado |
|---:|---|---|
| 1 | Baseline repo green | PASS |
| 2 | F2 FileExact reproducible | PASS |
| 3 | FileExact preservado como provenance | PASS |
| 4 | Candidate identity explícita | PASS |
| 5 | Sin round | PASS |
| 6 | Sin epsilon | PASS |
| 7 | Sin nearest snap | PASS |
| 8 | Sin denominadores arbitrarios | PASS |
| 9 | Sin frequency authority | PASS |
| 10 | Sin score | PASS |
| 11 | Sin probability | PASS |
| 12 | Sin confidence | PASS |
| 13 | Reflexiva | PASS |
| 14 | Simétrica | PASS |
| 15 | Transitiva | PASS |
| 16 | Membership determinista | PASS |
| 17 | Synthetic ground truth | PASS — disponible |
| 18 | Negative controls | PASS — disponibles |
| 19 | False merges medibles | PASS — 0 exactas; ExactMilliseconds rechazado |
| 20 | False splits medibles | **FAIL candidate usability — 1 FileExact / 2 traversal** |
| 21 | Transition kind exacto | PASS |
| 22 | TapHead != LNHead | PASS |
| 23 | Release != Head | PASS |
| 24 | Same-segment probado | PASS |
| 25 | Timing-change crossing probado | PASS |
| 26 | LN crossing timing change probado | PASS |
| 27 | Original-only | PASS |
| 28 | Whole-head-group exclusion | PASS |
| 29 | Release endpoint holdout auditado | PASS sintético; humano NOT RUN |
| 30 | Cross-chart cero | PASS |
| 31 | Synthetic teaching cero | PASS |
| 32 | Shadow-only | PASS |
| 33 | Sin generation dependency | PASS |
| 34 | Legacy byte-exact | PASS por regresión histórica |
| 35 | Legacy RNG exacto | PASS por suite completa |
| 36 | Artifacts deterministas | PASS |
| 37 | Multikey | PASS 1K/4K/7K/10K/18K |
| 38 | Sin branches estilísticas por keymode | PASS |
| 39 | Docs consistentes | PASS |
| 40 | Root guard verde | PASS |
| 41 | Suite completa verde | PASS — 378/378 |

El gate para declarar una timing identity usable **FAIL** por la condición 20. Las demás condiciones validan que el resultado negativo es reproducible y auditable; no convierten la candidate fallida en solución.

## Outcome y limitaciones

**COMPLETE — OUTCOME C — SHADOW ONLY.** Ninguna identity temporal exacta y útil puede recuperar equivalencia nominal general desde la información actualmente preservada por `.osu` sin introducir quantization inference. FileExact sigue siendo el baseline físico; SegmentTraversal es provenance exacta útil para crossings, pero no una equivalence identity musical.

El ground truth es sintético por necesidad: el corpus humano no etiqueta intención. Los controles prueban no-identificabilidad bajo discretización, no que todo mapper use un snap concreto. El audit endpoint-aware humano queda pendiente de una futura identity válida. El corpus sigue desbalanceado hacia 7K y carece de evidencia humana 1K/18K, aunque esto no determina Outcome C.

La siguiente recomendación es **F2.2 — Quantization Inference Feasibility / Research Design**, `NEXT / NOT AUTHORIZED YET`: decidir si el proyecto debe estudiar explícitamente una hipótesis de quantization y qué nueva información/ground truth necesitaría. No autoriza tolerancia, denominadores, nearest snap, behavior ni A/B.

Generation continúa exclusivamente en `legacy-experimental.1`. No existe nueva `BehaviorPolicyVersion`, toggle F2/F2.1, A/B ni cambio de output.
