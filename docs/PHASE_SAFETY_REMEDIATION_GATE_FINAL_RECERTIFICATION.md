# SAFETY.REMEDIATION.GATE — Final recertification

Fecha: 2026-09-26  
Estado: **COMPLETE — NEEDS_REVIEW / NO PROMOTION**  
HEAD de entrada: `3840a58aa02765a98da658a7c65b78aae82cf521`

## Decisión

La recertificación integral no satisface el claim completo. La única matriz oficial terminó sus 220 pares primarios y cuatro pares G1, confirmó cero condiciones HardValidity en treatment y conservó determinismo, reparse, OFF/reference, evidencia G1 y RNG del gate. Sin embargo, el clasificador corregido midió `188 A + 21 B + 6 C`, no la partición candidata `188 A + 22 B + 5 D`. Cinco C conservan el mecanismo D ya demostrado bajo el componente focalizado congelado, pero un sexto caso no cumple B ni D. Además se registraron 70.835 diferencias de estado sin lineage causal atribuida por la definición fuerte. Ambas observaciones activan condiciones de parada.

Outcome científico: **NEEDS_REVIEW**. No se cambió el clasificador después de observar el resultado, no se repitió la matriz y no se modificó la remediación.

## Baseline e identidades

- repositorio inicial: `main == origin/main == 3840a58aa02765a98da658a7c65b78aae82cf521`;
- contrato final: `4CF7B40D15D9B236DE1A67D3E0E1A01778B673D59F9C0488C6F1FB2852D598AC`;
- implementación conductual: `B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835`;
- harness final: `486FF0F6F0982A5B337BE4157E07B5769F7CE2B21C2724D51E1DF583437909C3`;
- manifest C11: `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`;
- subcontrato matrix/hardening: `A95778412E2BBACA190F57B6FCC2522562BBE09728BDFEADEC7618694EC6D748`;
- subcontrato D focalizado: `8DA70E882A1957D8EB1C87A0749807D3CB3C12BC01CEF5FECEF5140CFA4455FF`;
- límite de ejecución: `DOTNET_GCHeapHardLimit=0x400000000` (16 GiB), sin paralelismo de matriz.

La suite histórica del follow-up fue 766/766; los guards publicados llevaron el baseline a 774/774; tres tests nuevos de recertificación produjeron una suite pre-freeze de 777/777. `DocConsistency` pasó antes del freeze.

## Corpus

Se usó exclusivamente `.artifacts/f2-1-corpus`, sin explorar `Songs`. El resolver congelado verificó once hashes únicos en doce ubicaciones, un duplicado exacto, once familias, metadata, keymode, objetos, timing points y BPM. El resolver explícito coincidió 11/11 con el discovery histórico restringido a esa misma carpeta. La ruta local no forma parte de la identidad científica.

## Matriz medida

| Medida | Resultado |
|---|---:|
| Pares primarios | 220 / 220 |
| Compatibilidad G1 | 4 / 4 |
| Total | 224 / 224 |
| Ejecuciones completas de matriz | 1 |
| Casos históricos | 215 |
| A — rechazo canónico directo | 188 |
| B — inalcanzable causal fuerte | 21 |
| C — unresolved medido | 6 |
| D — mecanismo focalizado válido entre esos C | 5 |
| Unresolved final | 1 |
| HardValidity control | 215 |
| HardValidity treatment | 0 |
| Treatment-only | 0 |
| Divergencias de estado sin atribución fuerte | 70.835 |
| Pares con reconvergencia completa | 0 |
| RNG del gate | 0 |
| Fallos OFF/reference | 0 |
| Fallos determinismo | 0 |
| Fallos reparse | 0 |
| Fallos identidad G1 | 0 |

La seguridad observada A cumple en C11: treatment elimina las 215 condiciones y no introduce ninguna. La explicación causal B no cumple: seguridad observada no sustituye causalidad individual.

## Los cinco casos D

El smoke pre-freeze y el subcontrato D que quedó referenciado por el contrato final reprodujeron 5/5 mecanismos con la misma identidad de código relevante. En cada caso el tap histórico se compromete idénticamente y la LN bloqueadora de control falta en treatment bajo lineage canónica activa sin reconvergencia:

| Chart | Seed | Target | Blocker control | Resultado |
|---|---:|---|---|---|
| `02D9D178…` | 8 | `OP-00004470…` | L1 `222331→222416`, S4468 | D demostrado |
| `20651C9B…` | 3 | `OP-00004664…` | L6 `242351→242439`, S4663 | D demostrado |
| `20651C9B…` | 6 | `OP-00001515…` | L6 `89595→89821`, S1510 | D demostrado |
| `20651C9B…` | 7 | `OP-00001936…` | L5 `110460→110572`, S1934 | D demostrado |
| `20651C9B…` | 13 | `OP-00006173…` | L0 `296880→296968`, S6170 | D demostrado |

## Sexto caso y condición de parada

El caso nuevo es:

- chart `20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788`;
- seed `4`;
- target `OP-00000466-BaseHead-S466-T35009-ANA`, lane 2, ms 35009;
- históricamente figuraba entre los B;
- treatment no alcanza ni compromete el target, pero el clasificador corregido no encuentra lineage activa, origen gobernado ni prueba de no-reconvergencia;
- no puede ser B bajo la definición fuerte y tampoco D, porque D exige que el target se comprometa idénticamente.

No se investigó ni relajó la definición para reclasificarlo. Permanece `UNRESOLVED`.

## Fallo de consolidación

Tras escribir los artifacts completos de la matriz, el componente D esperaba exactamente cinco `C_UNRESOLVED` y recibió seis. Lanzó `InvalidDataException`; Windows mostró el código administrado `0xe0434352`. Esto no fue OOM ni invalidó los 224 resultados ya escritos. Sí impidió que el orquestador produjera automáticamente sus CSV finales renombrados. La ejecución no se repitió.

## Artifacts

Versionables:

- `docs/safety_remediation_gate_final_recertification_contract.json`;
- `docs/safety_remediation_gate_final_recertification_summary.json`;
- este informe.

Evidencia local preservada, individual y no versionada:

- `.artifacts/safety_remediation_gate_final_recertification/frozen/official-matrix/safety_remediation_gate_hardening_runs.csv` — 224 filas;
- `.artifacts/safety_remediation_gate_final_recertification/frozen/official-matrix/safety_remediation_gate_hardened_cases.csv` — 215 clasificaciones individuales (`188 A / 21 B / 6 C`);
- `.artifacts/safety_remediation_gate_final_recertification/frozen/official-matrix/safety_remediation_gate_causal_unreachable.csv` — 21 B certificados;
- `.artifacts/safety_remediation_gate_final_recertification/frozen/official-matrix/safety_remediation_gate_validation_hardening_summary.json`;
- `.artifacts/safety_remediation_gate_final_recertification/smoke/followup_result.json` — evidencia individual 5/5 D;
- contratos auxiliares e inventory bajo `.artifacts/safety_remediation_gate_final_recertification/frozen/`.

## Límites y autoridad

C11 sigue siendo corpus histórico de desarrollo, no holdout independiente ni evidencia de utilidad musical. `RECERTIFIED_WITHIN_C11` no se alcanza. El default continúa `legacy-experimental.1`; G1.GATE y SAFETY.REMEDIATION.GATE permanecen sin promoción. No se autoriza sucesora, release, commit ni push.

## Reproducción

Desde la raíz del repositorio, con una copia legítima del corpus C11 dedicado:

```powershell
$env:DOTNET_GCHeapHardLimit='0x400000000'
dotnet restore ManiaAddNotesLab.sln --disable-parallel
dotnet build ManiaAddNotesLab.sln --no-restore -m:1
dotnet test tests/ManiaAddNotesLab.Tests/ManiaAddNotesLab.Tests.csproj --no-build --no-restore -m:1 -- RunConfiguration.MaxCpuCount=1
dotnet run --project tools/DocConsistency --no-build -- --check
dotnet run --project tools/ManiaAddNotesLab.Experiments --no-build -- c11-frozen-verify <C11> docs/g1_gate_runtime_manifest.json
dotnet run --project tools/ManiaAddNotesLab.Experiments --no-build -- safety-remediation-gate-final-prepare . docs/g1_gate_runtime_manifest.json docs/safety_remediation_gate_final_recertification_contract.json .artifacts/safety_remediation_gate_final_recertification/frozen
dotnet run --project tools/ManiaAddNotesLab.Experiments --no-build -- safety-remediation-gate-final-run . <C11> docs docs .artifacts/safety_remediation_gate_final_recertification/frozen docs/safety_remediation_gate_final_recertification_contract.json
```

La última orden reproduce también el cierre no satisfactorio: completa la matriz, encuentra seis C y termina con `InvalidDataException` en la consolidación D. No debe repetirse para intentar obtener otra partición.
