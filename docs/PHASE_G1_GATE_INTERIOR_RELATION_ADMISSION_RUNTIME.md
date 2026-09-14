# Phase G1.GATE — Interior Relation Admission Runtime

## Cierre

**COMPLETE — NEEDS_REVIEW — EXPERIMENTAL TREATMENT ONLY — NO PROMOTION.**

G1.GATE conectó por primera vez la membership congelada de G1.DESIGN al runtime real. El gate se inserta después de que legacy resuelve oportunidad, Bernoulli, shape, lane y geometría, y antes de `added.Add`/`geometry.Insert`. `ADMIT` conserva exactamente la propuesta existente; `ABSTAIN` la descarta sin consumir RNG, rerollear, sustituir lane/shape ni derivar articulación.

Este cierre no mide naturalidad ni utilidad. Certifica el mecanismo, cuantifica su efecto y aplica criterios de parada mecánicos. El resultado es `NEEDS_REVIEW` porque el tratamiento introdujo nueve violaciones hard atribuibles. No se autoriza G1 de utilidad, G2, H, UI, defaults ni promoción.

## Estado de entrada

- HEAD y `origin/main`: `22707d40fbd92d39885a1caee5bcde005aecdb04`.
- Baseline: restore/build PASS; 703 tests PASS.
- Política default: `legacy-experimental.1`.
- Dependencia congelada G1.DESIGN: `15A16B6EFBF779CFF2C42A8C9A0DD46E025019252BEFA968E831233DC802AA68`.
- Archivo local `docs/ASTRA_ROADMAP_V2_PROPOSAL.md`: preservado y fuera del alcance.

## Contrato runtime

La policy de tratamiento es `g1-interior-relation-admission.1`; su contrato canonical tiene SHA-256 `0281FDA2A16E26CD9A176D2A59BF80DA421D2A419D70FAD33B0F02790088FB9A`.

El índice se construye una vez por chart desde la normalización full-chart de objetos originales: `!IsSynthetic && Origin == None`. La clave conserva keymode, duración de parent, offset y tipo de anchor; el resultado conserva relación interior, duración desde anchor y offset respecto al final de parent. La tabla congelada es:

| Estado | Decisión |
|---|---|
| `CandidateObservedUnique` | ADMIT |
| `CandidateObservedAmongAlternatives` | ADMIT |
| `CandidateNotObserved` | ABSTAIN |
| `NoObservedRelation` | ABSTAIN |
| `Unresolvable` | ABSTAIN |

La ruta es privada de investigación: los overloads y defaults existentes delegan al comportamiento legacy. Configuraciones incompatibles con articulación, eligibility no congelada, chart distinto, RNG no posicionable o contrato incorrecto son rechazadas.

## Certificación C11

Se reutilizó la intersección exacta de 11 IDs históricos, una familia por chart, 4K/7K/10K, con seeds 1–20: 220 pares control/tratamiento. Chance `0.50`; articulación y tratamientos no relacionados OFF. El manifest tiene SHA-256 `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`.

El gate evaluó 1.006 propuestas: 107 unique, 10 among-alternatives, 355 not-observed, 534 no-relation y 0 unresolvable. Admitió/commiteó 117 y abstuvo/suprimió 889. Hubo 0 fallos de propuesta exacta, 0 llamadas RNG del gate, 0 replacements y 0 articulation intents.

El control añadió 301.245 objetos y el tratamiento 300.413: delta -832 (-0,276187%). Las LNs añadidas bajaron de 38.097 a 37.312. Hubo primera divergencia directa en 126/220 pares; 94 reconvergieron en geometría/estado de generación y 208 en posición RNG final. El cambio downstream observado fue de 9.575 objetos. Estas cifras describen mecánica, no calidad.

## SAFETY.PROV y criterio de parada

El snapshot final rápido encontró 200 violaciones hard en control y 192 en tratamiento, pero dejó nueve casos treatment-only inicialmente no atribuibles. Se ejecutó entonces el recorder mutation-level completo sobre los cuatro pares implicados.

La recertificación clasificó los nueve como `TapOnHeldLongNote` y enlazó cada uno a oportunidades `TreatmentDownstream` posteriores a la primera supresión G1. No quedaron casos no atribuibles. Los cuatro traces no reconvergieron antes del final y contienen 5.484, 5.457, 5.435 y 9.949 oportunidades downstream respectivamente. Los hashes y primeras divergencias están en `g1_gate_forensic_summary.csv`; los traces completos permanecen en `.artifacts/g1_gate_forensic/` por tamaño.

Por contrato, una sola violación hard introducida y atribuible obliga a detener. Resultado: **9 attributable introduced violations → NEEDS_REVIEW**. Que el total agregado del tratamiento sea menor que el control no neutraliza una violación nueva por caso.

## Controles y aislamiento

- Default/null gate: byte-identical al legacy.
- Repetición determinista: exacta.
- Evidencia synthetic/cross-chart: rechazada o normalizada fuera.
- Gate con RNG, reroll de candidate/lane, sustitución de release, mutación previa, reconstrucción en ADMIT, mutación en ABSTAIN y articulación desde abstención: detectados por tests/auditor.
- Eligibility, oportunidad, candidate, lane y propuesta pre-gate: congelados.
- Índice: chart-local e inmutable.

## Decisión

G1.GATE queda cerrado como `COMPLETE / NEEDS_REVIEW`. La implementación permanece únicamente como superficie research explícita para reproducir y estudiar el fallo. No debe exponerse en CLI/Web ni activarse por default. No se autoriza medir utilidad sobre este tratamiento fallido, ajustar la mapping, añadir rerolls, abrir G2/H ni promover policy.

## Reproducción

```powershell
dotnet test tests/ManiaAddNotesLab.Tests/ManiaAddNotesLab.Tests.csproj -c Release --filter FullyQualifiedName~PhaseG1GateRuntimeTests
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- g1-gate-certify src/ManiaAddNotesLab.Web/batch-results docs .artifacts/g1_gate
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- g1-gate-forensic src/ManiaAddNotesLab.Web/batch-results 02D9D1781418E7442956D4D901C8C611E58256556AE9A828E72128C3B5B8E3EB 9 .artifacts/g1_gate_forensic
```

El comando forensic se repite para NOWISEE seeds 11 y 17, y KIKUO (`20651…C788`) seed 11. Los artifacts públicos son el contrato, manifest, resumen, pares, decisiones, familias, provenance y resumen forense indexados en `DOCUMENTATION_INDEX.md`.
