# SAFETY.REMEDIATION — Selection-Set Remapping Gate

Fecha: 2026-09-26  
Outcome: **E_DEMONSTRATED — LOCAL CAUSAL MECHANISM ONLY / NO PROMOTION**

## Baseline e identidades

La investigación comenzó sobre `main` limpio en `52f21af56f8034e07fcaa9d849cb9440b9429e73`;
`origin/main` local coincidía y la consulta remota falló por falta de red. Sólo estaban no rastreados los
tres documentos personales excluidos. No se hizo commit, push, tag, PR ni release.

| Identidad | SHA-256 |
|---|---|
| Contrato v2 | `A1A83F4515ADDD18398842B5FECC1BD0F43BC60E1D62F6B2B18B84A369244E66` |
| Implementación conductual | `B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835` |
| Harness experimental | `917F8157247C05968736DFE883BC578EE6332E4665CD9EA890FD89B79E0CB220` |
| Corpus C11 | `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445` |

El primer freeze (`1284384E…`) fue invalidado **antes de observar contrafactuales oficiales** porque no
permitía detenerse tras OP-185 para revisar memoria. Se preservó como
`safety_selection_set_remapping_contract_v1_invalidated_before_execution.json`. El v2 agregó únicamente
ejecución `priority-only`; no hubo resultados oficiales bajo v1.

Los hashes de los artifacts históricos quedaron incorporados al contrato. La matriz 224, sus 215 casos,
la consolidación `188 A / 21 B / 5 D / 1 C` y la forensia 70.835→8 episodios permanecieron intactas.

## Selector auténtico y contrafactual

El código productivo selecciona exactamente:

```text
lanes[rng.Next(lanes.Count)]
```

El instrumento aislado no usa módulo ni inventa una muestra. Registra el prefijo real completo de
`SeededRandom`, crea dos RNG nuevos con la misma seed, reproduce cada llamada previa y verifica su
resultado exacto. Desde ese mismo estado invoca `Next(legacy.Count)` y `Next(canonical.Count)`. Cada
brazo consume una llamada; el RNG real de la generación nunca se modifica.

## OP-185 prioritario

Para chart `20651C9B…`, seed 4, `OP-00000185-BaseHead-S185-T16399-ANA`:

- prefijo: 260 llamadas, SHA-256 `4C7EFA5135195BAE044D8E238DE280B25F6F1A7A564CB845946AC17C038833A9`;
- legacy `[0,1,2,3,6]`: `Next(5)=3`, lane 3;
- canonical `[0,1,2,3]`: `Next(4)=2`, lane 2;
- lane 3 continúa canónicamente legal;
- una llamada RNG en cada replay, misma posición final 261;
- padres completos e iguales;
- sucesores completos y materialmente distintos en estado materializado y geometría latente;
- sin intervención G1 concurrente.

Resultado: `E_SELECTION_SET_REMAP`. La etapa prioritaria tomó 2,85 s, usó el límite de heap de 16 GiB
y dejó estable el proceso coordinador alrededor de 86 MiB. Sólo después de verificar este PASS se
ejecutaron las otras siete.

## Resultados individuales

| Estrato | Chart | Seed | Primera oportunidad | Legacy → canonical | Índice | Lane | Prefijo | E |
|---|---|---:|---|---|---|---|---:|---|
| primary | `20651C9B…` | 4 | `OP-185…T16399` | `[0,1,2,3,6]` → `[0,1,2,3]` | 3→2 | 3→2 | 260 | PASS |
| primary | `02D9D178…` | 5 | `OP-659…T51279` | `[1,4,6]` → `[1,4]` | 1→0 | 4→1 | 932 | PASS |
| primary | `02D9D178…` | 20 | `OP-183…T18552` | `[0,1,3]` → `[0,1]` | 1→0 | 1→0 | 280 | PASS |
| primary | `20651C9B…` | 10 | `OP-48…T6475` | `[0,1,3,4,5]` → `[0,1,4,5]` | 1→0 | 1→0 | 80 | PASS |
| primary | `20651C9B…` | 11 | `OP-55…T6926` | `[0,1,2,3,6]` → `[0,1,2,3]` | 3→2 | 3→2 | 95 | PASS |
| primary | `20651C9B…` | 14 | `OP-51…T6700` | `[1,3,5,6]` → `[1,3,6]` | 1→0 | 3→1 | 84 | PASS |
| primary | `20651C9B…` | 19 | `OP-51…T6700` | `[0,1,3,5,6]` → `[1,3,5,6]` | 1→1 | 1→3 | 92 | PASS |
| secondary/G1 | `20651C9B…` | 11 | `OP-55…T6926` | `[0,1,2,3,6]` → `[0,1,2,3]` | 3→2 | 3→2 | 95 | PASS |

Los ocho replays reprodujeron las selecciones históricas. En seed 19 el índice es 1 en ambos máximos,
pero el elemento en índice 1 cambia porque canonical elimina la lane 0: demuestra que el remapping no
requiere siempre un índice numérico distinto. Primary y secondary/G1 seed 11 conservan evidencia local
idéntica hasta esa oportunidad, pero sus outputs, transcripts y fingerprints completos son distintos;
siguen siendo experimentos separados.

## Cardinalidad, orden y contenido

Los ocho casos oficiales tienen cardinalidad distinta y orden ascendente. Demuestran que cambiar
`maxExclusive` puede cambiar el índice devuelto, y que eliminar elementos anteriores también puede
remapear una lane aun conservando el índice.

Los fixtures aislados separan otras posibilidades:

- cardinalidad 5→4: índice/lane 3→2, remap;
- misma cardinalidad y mismo orden: selección idéntica, control negativo;
- mismos elementos/cardinalidad con orden invertido: mismo índice 2, lane 2→1, remap por orden;
- cardinalidad 3→2 con sets distintos: índice/lane 0→0, control negativo.

Los tests adicionales cubren igual cardinalidad con contenido distinto, lane de control rechazada,
canonical vacío y los demás ABSTAIN preregistrados. Ningún fixture pretende equivalencia geométrica con
un chart humano.

## Definición experimental E y abstenciones

E exige padres completos/iguales, misma oportunidad y configuración comparable, ordered sets reales,
intervención aislada, lane de control todavía canónica, selección auténtica distinta, mismo seed/prefijo
y una llamada RNG, y sucesores completos/materialmente distintos. Los ocho casos satisfacen todo.

E se abstiene ante padres incompletos/desiguales, oportunidad/configuración distinta, sets iguales,
lane de control rechazada, selección igual, RNG no comparable, intervención concurrente o sucesores no
materialmente distintos. E no se incorporó al clasificador oficial y no modifica A/B/C/D.

## OP-370, OP-466 y límite downstream

OP-370 conserva un rechazo directo real a nivel de objeto, pero `beforeEqual=false`; no es el origen de
E. Es además la primera oportunidad donde las posiciones RNG dejan de coincidir. OP-466 tiene padres
distintos, control compromete lane 2 y treatment no alcanza candidate decision equivalente.

Entre OP-185 y OP-466 se observaron:

- cero reconvergencias completas;
- dos intervenciones canónicas registradas;
- cuarenta oportunidades con commits distintos;
- secuencia lógica de oportunidades idéntica;
- drift RNG desde OP-370.

Esto demuestra continuidad de una trayectoria divergente, no una cadena causal aislada al target. Las
intervenciones posteriores y el drift son causas concurrentes posibles. Un contrafactual de trayectoria
que mantuviera únicamente la selección inicial requeriría intervenir comportamiento, decisiones y RNG
durante cientos de oportunidades; no es una copia local del selector y no está autorizado. Resultado:
E explica las ocho primeras divergencias, pero **no atribuye completamente la ausencia de OP-466**.
El caso permanece `C_UNRESOLVED`.

## No interferencia

En 8/8 runs:

- control instrumentado = referencia simple en bytes, llamadas y transcript RNG;
- treatment = repetición en bytes, RNG y fingerprint diagnóstico;
- prefijo RNG control/treatment exacto antes de la primera selección;
- G1 evidence identity inmutable;
- conteo histórico individual de observaciones exacto.

La ejecución completa tomó 14,23 s, fue secuencial y estuvo limitada a 16 GiB. No se ejecutó la matriz
completa ni se exploró `Songs`.

## Artifacts

Los artifacts nuevos están en `.artifacts/safety_selection_set_remapping/`: contratos v1/v2, manifest,
decisiones, counterfactuals, evidencia del selector, resultados E, no-interferencia, OP-185, downstream,
summary y `sha256sums.txt`. La etapa prioritaria permanece en `priority/`.

## Outcome

**E_DEMONSTRATED** significa únicamente que el mecanismo causal local satisface los criterios
preregistrados en 8/8 primeras divergencias. No significa `RECERTIFIED_WITHIN_C11`, no reclasifica los
215 casos, no promueve SAFETY.REMEDIATION.GATE o G1 y no modifica defaults. El estado científico global
continúa **NEEDS_REVIEW / NO PROMOTION** por el target downstream no atribuido.

## Reproducción

```powershell
dotnet test tests/ManiaAddNotesLab.Tests/ManiaAddNotesLab.Tests.csproj -c Release --filter "FullyQualifiedName~SelectionSetRemappingResearchTests"
dotnet tools/ManiaAddNotesLab.Experiments/bin/Release/net8.0/ManiaAddNotesLab.Experiments.dll safety-selection-set-remapping-prepare . docs/g1_gate_runtime_manifest.json docs/safety_selection_set_remapping_contract.json .artifacts/safety_selection_set_remapping
$env:DOTNET_GCHeapHardLimit='0x400000000'
dotnet tools/ManiaAddNotesLab.Experiments/bin/Release/net8.0/ManiaAddNotesLab.Experiments.dll safety-selection-set-remapping-run-priority . .artifacts/f2-1-corpus docs/g1_gate_runtime_manifest.json docs/safety_selection_set_remapping_contract.json .artifacts/safety_selection_set_remapping/priority
dotnet tools/ManiaAddNotesLab.Experiments/bin/Release/net8.0/ManiaAddNotesLab.Experiments.dll safety-selection-set-remapping-run . .artifacts/f2-1-corpus docs/g1_gate_runtime_manifest.json docs/safety_selection_set_remapping_contract.json .artifacts/safety_selection_set_remapping
```
