# SAFETY.REMEDIATION — OP-185 → OP-466 single-intervention counterfactual gate

Fecha: 2026-09-26  
Resultado: `ABSENT_BOTH`  
Autoridad: experimento focalizado; no reclasifica OP-466 ni autoriza promoción.

## Resumen

Se ejecutó una única pareja oficial sobre el chart
`20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788`, seed 4. Ambos brazos
usaron la política geométrica canónica. A seleccionó lane 2 normalmente en OP-185; B consumió la misma
llamada auténtica `Next(4) == 2`, pero el wrapper preregistrado entregó índice/lane 3 una sola vez.

El cambio alteró la geometría y el output posterior, pero no recuperó el target exacto OP-466. Ambos
brazos alcanzaron OP-466, ninguno produjo candidate para el target y ninguno lo comprometió. No hubo
objeto similar lane 2/35009.

Conclusión estricta: sustituir exclusivamente la selección de OP-185 fue insuficiente para recuperar
OP-466 bajo esta política y seed. Esto no demuestra que OP-185 sea irrelevante para otras diferencias ni
que no forme parte de una causa histórica compuesta. OP-466 permanece `C_UNRESOLVED`; el estado global
sigue `NEEDS_REVIEW / NO PROMOTION`.

## Preregistro e identidades

El diseño se cerró en
`PHASE_SAFETY_OP185_OP466_COUNTERFACTUAL_DESIGN.md` antes de observar el brazo B. `prepare` ejecutó
solamente controles canónicos y luego congeló:

| Identidad | Valor |
|---|---|
| Entry HEAD | `14a07fe0820c1005e8d814adffbbd431d185ac1e` |
| Implementación conductual | `B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835` |
| Harness experimental | `0EA221ECA42C2287820A121BB32A32214F0EA1937C4359156AE1462A7FC7164A` |
| Corpus C11 | `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445` |
| Artifacts históricos combinados | `4BAB6A64C5692117ABC0AA67D47080F4518233893E7D13EADF1249A721FEEABC` |
| Controles preflight | `A42921C760063EACD2402CB2DEFE17ED9711621E72A66C590FA0AD3199999CF6` |
| Contrato canónico | `6A7DA86F3EBCF41A352813372DAB159FBC51F69DD8E7797F7A5F1CFD71110620` |

No se cambió el contrato tras conocer el resultado.

## Controles previos

Pasaron antes del freeze:

- dos generaciones canonical normal exactamente iguales;
- wrapper activo entregando el valor normal lane 2 exactamente igual a referencia;
- output histórico `462307AE…38E999`;
- 13.730 llamadas y transcript RNG histórico `8F5589B0…762DED`;
- diagnostics histórico `26804F0A…8F994`;
- chart/profile/engine/RNG independientes entre ejecuciones;
- OP-185 real con lanes `[0,1,2,3]`, selected lane 2 y posición previa 260;
- padre/sucesor completos y sucesor en posición 261.

Los 19 tests adversariales focalizados también pasaron antes del contrato.

## Validación final

- Restore secuencial: correcto.
- Build Release `-m:1`: correcto, 0 errores.
- Tests: 826 passed, 0 failed, 0 skipped.
- DocConsistency: `PASS`.
- `git diff --check`: correcto.
- Implementación conductual final: `B7AA67D3…AF2835`, sin cambio.

La única advertencia fue `NU1900`, porque el entorno sin red no pudo consultar el índice de
vulnerabilidades de NuGet; los paquetes locales restauraron y compilaron correctamente.

## Identidad inicial

Justo antes de OP-185, ambos brazos tuvieron el mismo estado suficiente completo:

- hash `492149CC33A1EC2F39B120B79D7881A29CF4A94E48990EC89E46F108DEE53222`;
- materialized `771629AC…0274`;
- latent `6F068DB6…E5C`;
- root/stage RNG 259;
- opportunity cursor 185;
- pending articulation vacío;
- configuración `A5221530…D3B`;
- prefijo auténtico de 260 llamadas `4C7EFA51…33A9`.

Las instancias de chart y profile no compartieron referencia.

## Intervención OP-185

La llamada auténtica fue exactamente una en ambos brazos:

| Propiedad | A | B |
|---|---:|---:|
| Posición antes | 260 | 260 |
| Maximum | 4 | 4 |
| Valor auténtico | 2 | 2 |
| Valor entregado | 2 | 3 |
| Posición después | 261 | 261 |
| Lane comprometida | 2 | 3 |

Lane 3 estaba en el conjunto canónico real. Ambos sucesores fueron completos. El evento de intervención
registrado fue exactamente uno y no hubo intervención posterior.

## Trayectoria downstream y RNG

Los outputs divergen (`462307AE…38E999` frente a `5902D771…86671B`) y los diagnostics divergen
(`26804F0A…8F994` frente a `A82C7CD9…E0478`), como consecuencia material esperada del commit lane 2/3.

Sin embargo, ambos brazos conservaron 13.730 llamadas y el mismo transcript RNG auténtico completo
`8F5589B0…762DED`. No apareció una oportunidad con posiciones RNG distintas hasta OP-466 ni en el resto
del run. La selección cambió geometría/estado, pero en esta trayectoria no cambió el número, tipo ni
resultado auténtico de llamadas posteriores.

Esto no es sincronización artificial: cada brazo evolucionó independientemente; la igualdad de consumo
es un resultado observado.

## OP-370

Ambos brazos alcanzaron OP-370 con RNG padre 532. En ambos existió una candidate con:

- legacy lanes `[4]`;
- canonical lanes `[]`;
- ningún selected lane;
- ningún commit;
- sucesor RNG 533.

Los estados padres y hashes de geometría difieren por la intervención previa, pero la autoridad canónica
rechazó la candidate en ambos. El brazo intervenido llegó al estado geométrico asociado históricamente
con lane 3 en OP-185, pero no se cambió a autoridad legacy y por ello no reprodujo el commit legacy de
OP-370. OP-370 no produjo divergencia adicional de RNG.

## OP-466

Ambos brazos alcanzaron la oportunidad exacta, order 468, con RNG padre 659 y sucesor 660. En ambos:

- no se produjo candidate decision;
- lane 2 no fue propuesta;
- no hubo commit;
- no apareció el objeto exacto;
- no apareció objeto similar lane 2/35009.

Los campos `controlGeometryPermitsLane2=false` y `treatmentGeometryPermitsLane2=false` del artifact
significan **no evaluado por ausencia de candidate**, no una demostración de rechazo geométrico. La
precondición aleatoria de candidate no llegó a la evaluación de lanes en ninguno de los brazos.

Por tanto, el outcome preregistrado correcto es `ABSENT_BOTH`, no `SIMILAR_NOT_EXACT` ni
`RECOVERED_EXACT`.

## Causalidad y límites

El experimento demuestra el efecto inmediato de la sustitución lane 2→3 y observa sus estados
downstream bajo dos trayectorias canónicas deterministas. No aísla cada mediador posterior ni compara
contra una trayectoria legacy completa. En particular:

- el efecto en OP-185 es material y atribuible a la intervención única;
- OP-370 conserva rechazo canónico en ambos brazos;
- OP-466 no llega a candidate en ninguno;
- la intervención no fue suficiente para recuperar el target;
- no se demuestra irrelevancia general ni exclusividad causal histórica.

No se ejecutaron seeds o lanes alternativos, repeticiones para buscar un outcome favorable, `Songs` ni
la matriz de 224 pares.

## Artifacts

La evidencia completa está separada en `.artifacts/safety_op185_op466_counterfactual/`:

- `frozen_contract.json`, `identity_inventory.json`, `case_manifest.json`;
- `preflight_controls.json`, `pre_op185_snapshot.json`, `single_intervention.json`;
- `op185_comparison.json`, `op370_comparison.json`, `op466_result.json`;
- `downstream_trajectory.csv`, `rng_transcripts.json`, `summary.json`;
- `sha256sums.txt`.

Los charts no se copiaron. Los artifacts históricos no se sobrescribieron.

## Reproducción

Preparación y freeze — sólo debe usarse antes de una nueva ejecución científicamente distinta:

```powershell
$env:DOTNET_GCHeapHardLimit='0x400000000'
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- `
  safety-op185-op466-prepare . .artifacts/f2-1-corpus `
  docs/g1_gate_runtime_manifest.json `
  docs/safety_op185_op466_counterfactual_contract.json `
  .artifacts/safety_op185_op466_counterfactual
```

Ejecución del contrato congelado:

```powershell
$env:DOTNET_GCHeapHardLimit='0x400000000'
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release --no-restore -- `
  safety-op185-op466-run . .artifacts/f2-1-corpus `
  docs/g1_gate_runtime_manifest.json `
  docs/safety_op185_op466_counterfactual_contract.json `
  .artifacts/safety_op185_op466_counterfactual
```

La ejecución aquí documentada no debe repetirse para seleccionar otro resultado.
