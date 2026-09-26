# SAFETY.REMEDIATION.GATE — Focused Forensic Attribution

Fecha: 2026-09-26  
Estado: **COMPLETE — NEEDS_REVIEW / NO PROMOTION**

## Alcance y protección de evidencia

La investigación partió de `077004a97913bef023b15e7e469702dcb798b93a`. `HEAD`, `main` y el
tracking local de `origin/main` coincidían; la consulta remota no pudo completarse por falta de red.
Los únicos archivos no rastreados iniciales fueron los tres documentos personales expresamente
excluidos. No se hizo `reset`, commit, push, tag ni release.

Se conservaron intactos la única matriz oficial de 224 pares y sus artifacts. La identidad conductual
histórica continúa `B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835`, el harness de
recertificación final continúa `486FF0F6F0982A5B337BE4157E07B5769F7CE2B21C2724D51E1DF583437909C3` y el
manifest C11 continúa `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`.

Los inputs congelados comprobados fueron:

| Artifact | SHA-256 | Registros |
|---|---|---:|
| `safety_remediation_gate_hardening_runs.csv` | `0E3D3BAA91B8EA640F380B6E7DA1EAC1EEB684BE01641B917AD42ECADDF23ECD` | 224 |
| `safety_remediation_gate_hardened_cases.csv` | `DEE28685FABD09750C09D186C67266F13B5C4BAF0CCD9E6C7DFAFB1C62AE5C1B` | 215 |
| `safety_remediation_gate_causal_unreachable.csv` | `1E27BD1373C4908E69CA610D88A2CCE0AFBFC1AA0A1B6A570B777BC9514B0A3C` | 21 |
| hardening summary | `981D70DC56F9553551BF6973774949AACA24B33D442DB9B81DEE63B74701C153` | — |
| focused D result | `0BAB75FF059D30877E8129BA872A5B33810B189685222B193AE8061D4DAF712F` | 5 |

Las relaciones contractuales y los conteos fueron exactos: 224 runs, 215 casos, `188 A / 21 B / 6 C`;
cinco de los seis C poseen evidencia D individual previa. Ningún resultado histórico fue sustituido.

## Método focalizado y no interferencia

Se usó exclusivamente `.artifacts/f2-1-corpus`; el resolver congelado comprobó los once contenidos
C11. No se exploró `Songs`. Se ejecutaron sólo ocho pares focalizados, secuencialmente y con
`DOTNET_GCHeapHardLimit=0x400000000` (16 GiB). Cada run comparó:

- control instrumentado contra la implementación simple sin gate;
- treatment contra una repetición independiente;
- bytes serializados, número y transcript de RNG;
- fingerprint completo de decisions y sufficient states.

Las ocho referencias y las ocho repeticiones fueron exactas. La instrumentación no consumió RNG ni
cambió commits u outputs. Primary seed 11 y secondary/G1 seed 11 se conservaron como experimentos
distintos: comparten el mismo primer mecanismo y conteo, pero sus outputs, transcripts y fingerprints
completos no son iguales; no fueron deduplicados.

## Hallazgo causal

Las ocho ejecuciones comparten el mismo mecanismo inicial:

1. `StateBefore` es completo e igual.
2. La geometría canónica elimina al menos una lane del conjunto legal.
3. La lane elegida por control **sigue siendo canónicamente legal**.
4. Al reducirse el conjunto, el mismo consumo RNG indexa otra lane válida en treatment.
5. Los commits difieren y `StateAfter` diverge materialmente en geometría materializada y latente.
6. `CanonicalAuthorityRejectedSelectedCommit` es falso, porque el commit elegido por control no fue
   rechazado. Por la definición fuerte congelada no se abre causal lineage.

Éste es un cambio de selección inducido por la cardinalidad/orden del conjunto canónico, no un rechazo
del commit seleccionado. Es causalmente observable, pero queda fuera de la única regla congelada capaz
de abrir lineage. No se relajó esa regla ni se reinterpretaron A/B/D.

| Estrato | Chart | Seed | Primera no equivalencia | Orden | Selección control→treatment | Observaciones | Episodios |
|---|---|---:|---|---:|---|---:|---:|
| primary | `20651C9B…` | 4 | `OP-00000185…T16399` | 185 | 3→2 | 10.007 | 1 |
| primary | `02D9D178…` | 5 | `OP-00000659…T51279` | 677 | 4→1 | 4.823 | 1 |
| primary | `02D9D178…` | 20 | `OP-00000183…T18552` | 195 | 1→0 | 5.305 | 1 |
| primary | `20651C9B…` | 10 | `OP-00000048…T6475` | 48 | 1→0 | 10.144 | 1 |
| primary | `20651C9B…` | 11 | `OP-00000055…T6926` | 55 | 3→2 | 10.137 | 1 |
| primary | `20651C9B…` | 14 | `OP-00000051…T6700` | 51 | 3→1 | 10.141 | 1 |
| primary | `20651C9B…` | 19 | `OP-00000051…T6700` | 51 | 1→3 | 10.141 | 1 |
| secondary/G1 | `20651C9B…` | 11 | `OP-00000055…T6926` | 55 | 3→2 | 10.137 | 1 |

En todos los casos la primera diferencia aparece en `StateAfter`, con ambos estados causalmente
completos. Los campos distintos son `MaterializedGenerationStateHash` y
`LatentCommittedGeometryHash`; RNG position, cursor y pending articulation permanecen iguales en ese
punto. No se observó incompletitud causal.

## Sexto caso: seed 4

La primera divergencia es `OP-00000185-BaseHead-S185-T16399-ANA`, no OP-370. Antes de OP-185 los
padres son iguales y completos. Control ve lanes `[0,1,2,3,6]` y selecciona 3; treatment ve
`[0,1,2,3]` y selecciona 2. La lane 3 de control sigue siendo canónicamente válida, por lo que el
origen no satisface el predicado de rechazo seleccionado. Los sucesores difieren materialmente y las
posiciones RNG coinciden en 261.

### OP-00000370

En `OP-00000370-BaseHead-S370-T29933-ANA`, control selecciona/compromete lane 4 y canonical tiene
conjunto vacío. El rechazo canónico es real; los commits difieren y los sucesores difieren. Sin embargo,
`beforeEqual=false`: los padres completos ya descendían de OP-185 sin lineage gobernada. Por eso:

- la clasificación case-level A es válida como rechazo directo del target de esa oportunidad;
- OP-370 no abre lineage bajo la definición congelada;
- OP-370 no puede apropiarse retroactivamente del origen de la divergencia.

Corresponde a la alternativa “A válida, pero la divergencia ya existía antes”.

### OP-00000466

En `OP-00000466-BaseHead-S466-T35009-ANA`, control llega al candidato lane 2 y lo compromete;
treatment no alcanza una candidate decision equivalente. Los padres son completos pero distintos,
incluido RNG 667 frente a 659, y no existe lineage activa. Aunque el snapshot de control muestra que
lane 2 no pertenece a sus lanes canónicas `[1,3]`, no hay parent equality ni candidato treatment que
demuestre una transición directa comparable. La ausencia del target es consistente con la trayectoria
iniciada en OP-185, pero no constituye una cadena causal fuerte bajo el contrato congelado.

El sexto caso permanece `C_UNRESOLVED`.

## Descomposición de 70.835

El contador histórico quedó reproducido exactamente, tanto agregado como por run. No representa
70.835 causas. Cada run contiene un único episodio continuo desde su primer `StateAfter` divergente
hasta la última oportunidad, sin reconvergencia completa. Por tanto:

- 70.835 oportunidades no equivalentes persistentes;
- 8 episodios diagnósticos;
- 8 primeras divergencias materiales;
- 0 orígenes que cumplan el rechazo-selected-commit fuerte;
- 0 reconvergencias completas;
- 0 casos de hash igual con incompletitud;
- un mismo mecanismo observado en las ocho ejecuciones.

Agrupar un episodio no lo transforma en causal lineage.

## Defectos del harness confirmados

1. **Cobertura causal estrecha, preservada deliberadamente.** El nombre
   `CanonicalAuthorityChangedCommit` se alimentaba sólo de “canonical rechazó la lane elegida por
   control”. No cubre un commit distinto causado por el cambio de cardinalidad cuando la lane de control
   continúa legal. Esto explica las observaciones, pero cambiar la semántica del clasificador congelado
   requiere autorización separada.
2. **Consolidación rígida.** `Finalize` esperaba implícitamente la partición favorable y no podía
   materializar un resultado medido `188/21/6`. Ahora acepta cualquier partición A/B/C de 215 casos,
   aplica D sólo a identidades demostradas y conserva los C restantes.
3. **Identidad incompleta de caso.** La identidad original omitía `stratum` y `frozen_arm`: seis casos
   primary/secondary colisionaban y producían 209 identidades para 215 filas. La identidad offline ahora
   incluye ambos campos. Esta corrección no cambia clasificación ni conducta.

La consolidación offline, sobre copias y sin matriz nueva, produjo exactamente
`188 A / 21 B / 5 D / 1 C`, `NEEDS_REVIEW`. Los SHA-256 de los dos inputs constan en su summary.

## Tests adversariales

La suite focal cubre padres iguales/completos con rechazo válido, selección distinta sin rechazo,
legal-lane sets distintos, padres ya diferentes antes de un rechazo, hashes iguales incompletos,
divergencia final, reject sin sucesor material distinto, reconvergencia y nueva lineage, episodios
continuos no atribuidos, equivalencia cache/no-cache, fingerprints incrementales y transcript RNG.
También cubre que D se aplique sólo a identidades demostradas, que un C restante sobreviva y que
primary/secondary sean identidades distintas.

## Artifacts nuevos

Todo quedó separado en `.artifacts/safety_remediation_gate_forensic_attribution/`:

- `forensic_attribution_runs.json`: estados, candidates, OP-370/OP-466, fingerprints y episodios;
- `forensic_attribution_runs.csv`: resumen por run;
- `forensic_offline_final_recertification_cases.csv`: consolidación derivada 215/215;
- `forensic_offline_consolidation_summary.json`: partición y hashes de inputs;
- `sha256sums.txt`: hashes de los artifacts anteriores.

Los artifacts oficiales congelados no fueron modificados.

## Limitaciones y outcome

La reproducción demuestra el mecanismo inicial y la persistencia, pero no autoriza redefinir causalidad.
No se ejecutó un counterfactual que conserve cardinalidad ni se cambió el selector. El fallo nativo
intermitente de `.NET`/Windows volvió a aparecer durante una invocación corta de consolidación; el
proceso también emitió su excepción administrada por separado. Las reproducciones científicas finales
terminaron normalmente y pasaron sus controles de referencia; el popup no se usa como evidencia.

Resultado: **NEEDS_REVIEW / NO PROMOTION**. Default, G1, HardValidity y comportamiento del engine no
cambiaron. Una futura investigación puede preregistrar una causalidad para selection-set remapping, pero
esta fase no la implementa.

## Reproducción

```powershell
$env:DOTNET_GCHeapHardLimit='0x400000000'
dotnet tools/ManiaAddNotesLab.Experiments/bin/Release/net8.0/ManiaAddNotesLab.Experiments.dll safety-remediation-gate-forensic-run .artifacts/f2-1-corpus docs/g1_gate_runtime_manifest.json .artifacts/safety_remediation_gate_forensic_attribution
dotnet tools/ManiaAddNotesLab.Experiments/bin/Release/net8.0/ManiaAddNotesLab.Experiments.dll safety-remediation-gate-final-consolidate-offline .artifacts/safety_remediation_gate_final_recertification/frozen/official-matrix/safety_remediation_gate_hardened_cases.csv .artifacts/safety_remediation_gate_final_recertification/smoke/followup_result.json .artifacts/safety_remediation_gate_forensic_attribution
```
