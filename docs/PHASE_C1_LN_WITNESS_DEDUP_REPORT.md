# Phase C1 — LN Witness Deduplication

Fecha: 2026-09-06.  
Resultado: **C1 HOLD — DEDUPLICATION RULE NOT JUSTIFIED**.  
Behavior policy activa: `legacy-experimental.1`.  
Evidence profile: `phase-a.1`.  
Decision diagnostics shadow: `phase-c1-shadow.1`.

C1 investigó únicamente si dos rutas (`Duration` y `ExactRelease`) desde una misma
`OriginalObservation` deben sumar dos unidades de autoridad al mismo LN candidate. La investigación encontró una
agregación canónica a nivel algebraico, pero la reconstrucción held-out disponible no autoriza convertirla en
comportamiento: empeora de forma pequeña y consistente la recuperación de endpoints originales en la única familia
de chart humano disponible. No se creó un modo conductual C1, no cambió el selector y no se implementó C2.

## Baseline

Antes de C1:

- `dotnet restore`: PASS; advertencia ambiental `NU1900` por no poder consultar `api.nuget.org`.
- `dotnet build -c Release`: PASS, 0 errores.
- `dotnet test -c Release`: 157 passed, 0 failed, 0 skipped.
- `BehaviorPolicyVersion=legacy-experimental.1`.
- `EvidenceProfileVersion=phase-a.1`.
- `DecisionDiagnosticVersion=phase-b.1`.
- `MapperEvidenceProfile` original-only, diagnostics sin RNG, candidate key diagnostic-only, certificates sin
  autoridad de selección y weights legacy autoritativos.

Snapshot reproducible: `phase-c1-baseline/pre-code/ManiaAddNotesLab`. Los backups pre-Mapper-Derived no se tocaron.

Tras agregar exclusivamente instrumentación/research shadow, cinco outputs legacy siguen idénticos al cierre de B:

| Fixture | SHA-256 | Equality |
|---|---|---:|
| rice 4K | `1D3C7C1C93B3D4C9C86D9D542AB0780AF0FA6B0A6A05BF490735731BC98550B0` | PASS |
| LN 4K | `11B5FD207202BF9CEE16E4506AABA92C5D1FEE3CF754F369C21D736D8F698EB9` | PASS |
| 7K | `3D874B5F038E9D4371A5DBDDEB7D73CD9758FDEF338100E862F461EC97149104` | PASS |
| 18K | `219B1F9BC9D07B99F928CCC9AE4BB75F6C8E5A4FA5EC60365E89010D7176E391` | PASS |
| interior + articulation | `FBE3FDB25059000FFD3DD8B894B10CE040651D4A8989EAD72E458FE023A5CB1A` | PASS |

Spring ADD 50, seed 100, interior/articulation modernos también conserva el hash
`8347A6DE885930C643814344D93D0A74A8094088B44E3BB41D3B28D93D10030D`.

## Legacy weight decomposition

Para una oportunidad con head beat `h_s` y una observación LN `o=(h_o,r_o)`, legacy calcula una sola vez:

```text
distance(o,s) = abs(h_o - h_s)

DistanceWeight(o,s) =
    1                                           si distance <= 0.000001
    max(MinimumDistanceWeight,
        1 - DistanceDecayPerBeat
            * ceil(distance - 0.000001))        en otro caso
```

Con defaults, `DistanceDecayPerBeat=0.20` y `MinimumDistanceWeight=0.20`; C1 no los modifica.

La misma observación emite hasta dos rutas:

```text
Duration(o)     -> intendedEndBeat = h_s + (r_o - h_o)
ExactRelease(o) -> intendedEndTime = original endTime(o)
```

Cada ruta que sobrevive materialización/snap y el minimum-duration envelope añade exactamente el mismo
`DistanceWeight(o,s)` al candidate fusionado por `EndTime`. No existe multiplicador específico de Duration ni de
ExactRelease. `ExactReleaseVotes`, vote counts y `SnapAdjustmentMilliseconds` son metadata; no agregan otro término.

Después de sumar rutas se aplica una única afinidad a todo el candidate:

```text
SourceAffinity(c,s) = 1.25
    si nearly(duration(c), duration(source), 0.001 beat)
    o endTime(c) == endTime(source)
    en otro caso 1

LegacyWeight(c) = SourceAffinity(c,s)
                * sum(path p -> c, DistanceWeight(observation(p),s))
```

Interiores desactivan SourceAffinity como ya ocurría antes de C1. No hay bonus, average ni clamp posterior.

La factorización real es, por tanto:

- observation-level: un `DistanceWeight` común a las dos rutas de una observación;
- route-level: elección del endpoint/label y supervivencia tras materialización/envelope, sin coeficiente propio;
- candidate-level: `SourceAffinity` una vez después de agregar.

## Duplicate evidence semantics

Si Duration y ExactRelease de `O17` materializan el mismo endpoint, legacy aporta `2 × DistanceWeight(O17)`. Phase B
ya había establecido que siguen siendo dos labels pero un solo witness. La alternativa canónica shadow es:

```text
C1ShadowWeight(c) = SourceAffinity(c,s)
                  * sum(distinct ObservationId o supporting c,
                        DistanceWeight(o,s))
```

Se conservan `ObservationId`, `EvidenceLabels[]`, paths y provenance. Solo se calcula una autoridad por ID.
`MAX_PER_WITNESS` no es una alternativa distinta aquí: todas las rutas de una misma observación usan el mismo
DistanceWeight, de modo que `max(routes(o)) == DistanceWeight(o)`. Average produce lo mismo. Escoger únicamente el
label Duration o Release eliminaría provenance útil sin justificación.

La ambigüedad restante es semántica, no algebraica: la convergencia de dos rutas podría ser una señal de acuerdo entre
features aun cuando no constituya dos observaciones independientes. El modelo actual no separa “autoridad del
witness” de “acuerdo de labels”. Inventar ahora un bonus de acuerdo sería una segunda hipótesis y un magic number.

## Candidate-level examples

Fixture de cuatro LNs simultáneas con el mismo endpoint:

| Observation | Labels | Legacy pre-affinity | C1 shadow pre-affinity |
|---|---|---:|---:|
| O0 | Duration + ExactRelease | 2 | 1 |
| O1 | Duration + ExactRelease | 2 | 1 |
| O2 | Duration + ExactRelease | 2 | 1 |
| O3 | Duration + ExactRelease | 2 | 1 |

Candidate total: `LegacyWeight=10`, `C1ShadowWeight=5`, cuatro independent witnesses y cuatro duplicate paths.
El artifact `phase-c1-baseline/fixture-shadow-all.json` conserva el detalle por observación.

También se construyó un fixture donde candidate A tiene un witness duplicado y SourceAffinity, mientras candidate B
recibe cuatro rutas limpias a distancia de dos beats. Legacy ordena A primero (`2 × 1.25 = 2.5` frente a `2.4`);
shadow deduplicado ordena B primero (`1 × 1.25 = 1.25` frente a `2.4`). Candidate endpoints y vocabulario no cambian.

## C1 Design Gate

| # | Pregunta | Estado | Evidencia |
|---:|---|---|---|
| 1 | Fórmula legacy exacta | PASS | Suma de DistanceWeight por path, luego SourceAffinity candidate-level. |
| 2 | Parte que representa observation authority | PASS | DistanceWeight calculado una vez desde el head de la observación. |
| 3 | Metadata exclusiva de route | PASS | Label, endpoint propuesto, vote counters y snap adjustment. |
| 4 | ¿Duration/release duplican autoridad? | PASS con reserva | Numéricamente repiten el mismo factor; el acuerdo entre labels podría contener información no modelada. |
| 5 | ¿Sus contributions pueden diferir para un mismo ID/candidate? | PASS | No: ambas reciben el mismo DistanceWeight precomputado. |
| 6 | ¿Por qué difieren rutas? | PASS | Pueden llegar a endpoints distintos; si convergen, no difieren en peso. |
| 7 | ¿Existe combinación canónica? | PASS | Suma una vez por ObservationId y aplica la afinidad existente una vez. |
| 8 | ¿Hace falta evidencia experimental? | PASS | Sí, para decidir si retirar el acuerdo de rutas mejora o degrada la reconstrucción. |
| 9 | ¿Puede cambiar solo aggregation? | PASS | Candidate set/order previo, endpoints y paths permanecen iguales. |
| 10 | ¿Puede conservarse todo lo demás? | PASS | Shadow y tests aíslan distancia, affinity, timing, geometría y RNG. |

El Design Gate autoriza investigación shadow, no comportamiento.

## Shadow aggregation alternatives

Se compararon únicamente:

1. `LEGACY_PATH_SUM`, fórmula vigente.
2. `DISTINCT_OBSERVATION_DISTANCE_SUM_THEN_CANDIDATE_AFFINITY`, derivada exactamente de la factorización.

`MAX_PER_WITNESS` y average no generan una tercera salida bajo la fórmula actual, y no se añadieron combinaciones
arbitrarias.

## Held-out reconstruction methodology

Para cada LN original target:

1. se excluye su `ObservationId` de los donors;
2. se conserva la ventana legacy `±4 beats` y los demás objetos originales;
3. se construye exactamente el vocabulario de duration/release restante y el mismo minimum-duration envelope;
4. se desactiva SourceAffinity durante evaluación porque leer duración/end del target sería target leakage;
5. se rankea el mismo candidate set por LegacyWeight y C1ShadowWeight, con desempate estable por EndTime;
6. se registra presencia/rank del endpoint original.

Una observación no puede votar por sí misma: el fixture de una sola LN produce un target evaluado y cero candidate
vocabulary. Se registra `HasStructuralTwinDonor` cuando otra LN tiene exactamente el mismo head/end; no es self-vote,
pero puede facilitar reconstrucción y limita la fuerza de la validación. No se usaron outputs ADD como charts de
investigación para evitar aprendizaje desde sintéticos.

Artifact reproducible: `phase-c1-baseline/held-out-reconstruction.json`.

## Reconstruction results

Solo se localizaron tres archivos humanos originales razonables, todos variantes de la misma familia Spring of
Dreams. Por tanto no son tres estilos independientes.

| Chart | K | Objects/LN | Vocabulary | True present | Legacy top-1 | C1 top-1 | Mean rank L/C1 | Duplicate targets | Rank/top changed |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Spring CUSTOM No SV | 7 | 4608/3587 | 3586 | 3494 | 1955 | 1938 | 2.290/2.343 | 2726 | 580/121 |
| Spring No SV | 7 | 4608/3587 | 3586 | 3494 | 1955 | 1938 | 2.290/2.343 | 2726 | 580/121 |
| Spring base | 7 | 4608/3587 | 3586 | 3497 | 1966 | 1950 | 2.277/2.325 | 2726 | 572/112 |

En CUSTOM/No SV, sobre los 3494 targets cuyo endpoint estaba presente:

- top-1 empeora en 50 y mejora en 33; neto `-17`;
- rank empeora en 144 y mejora en 72;
- mediana permanece 1;
- 1548 targets tienen structural twin donor.

Candidate-level CUSTOM/No SV:

| Métrica | Numerador | Denominador | Ratio |
|---|---:|---:|---:|
| candidate con duplicate witness | 3363 | 74556 candidates | 4.511% |
| duplicate paths | 4452 | 290992 evidence paths | 1.530% |
| targets afectados | 2726 | 3586 con vocabulary | 76.018% |
| true target presente | 3494 | 3587 targets | 97.407% |

Los 16 fixtures `.osu` sintéticos disponibles suman 42 targets, 102 candidates y 8 candidates duplicados; no
produjeron cambios de rank/top. Son controles estructurales, no evidencia de naturalidad.

## C1 Research Gate

| # | Pregunta | Estado | Evidencia |
|---:|---|---|---|
| 1 | ¿Existe duplicate authority real? | PASS | 4452 paths redundantes en held-out CUSTOM; 2633 en la run histórica seed 100. |
| 2 | ¿Impacta ranking de forma no trivial? | PASS | 580 rankings y 121 top candidates cambian en held-out CUSTOM. |
| 3 | ¿Hay aggregation sin magic number? | PASS | Distinct ObservationId sum conserva DistanceWeight y SourceAffinity. |
| 4 | ¿No-duplicate conserva exactitud? | PASS | Hard test exacto para todos los candidates sin duplicate. |
| 5 | ¿Held-out la respalda o evita degradación sistemática? | **FAIL** | Top-1 y mean rank empeoran en las tres variantes reales. |
| 6 | ¿Se sostiene en más de un tipo de chart? | **RESEARCH REQUIRED** | Solo existe una familia humana; fixtures no sustituyen diversidad. |
| 7 | ¿No depende de KeyCount? | PASS | Misma agregación y tests 1K/4K/7K/10K/18K. |
| 8 | ¿No cambia otro scoring? | PASS en shadow | Distance, affinity, set, order y timing se preservan. |
| 9 | ¿Policy A/B versionada? | RESEARCH REQUIRED | No se creó porque el gate conductual falló. |
| 10 | ¿Rollback trivial? | PASS | Desconectar diagnostics/research deja el hot path legacy. |

**C1 HOLD — DEDUPLICATION RULE NOT JUSTIFIED**

La regla es una descripción coherente de autoridad independiente, pero no está demostrado que eliminar el segundo
label del peso sea la representación correcta del acuerdo entre rutas. Activarla pese al resultado held-out sería
escoger teoría sobre evidencia y ocultar una degradación. C1 permanece incompleta.

## Selected witness authority rule

Ninguna regla fue seleccionada para generation. La fórmula `DistinctObservationDistanceSumThenCandidateAffinity`
queda como alternativa shadow explícita, no como policy aprobada.

## Implementation

Implementado solo en shadow/research:

- descomposición de `CandidateEvidence` en legacy/C1 shadow;
- conteo mínimo por identidad de objeto original, materializado únicamente con diagnostics/research;
- per-observation contributions y labels en diagnostics;
- reconstrucción leave-one-observation-out;
- comando `c1-held-out` y benchmark `c1-performance` en experiment tooling;
- fixtures de duplicados, no-duplicados, ranking, affinity, distance, timing raro y cross-key.

No existe opción capaz de seleccionar C1 para generation. Con diagnostics OFF, el hot path no materializa el set de
witnesses de C1.

## Policy versioning

`BehaviorPolicyVersion` sigue `legacy-experimental.1`. El schema explicativo cambia de `phase-b.1` a
`phase-c1-shadow.1`. `EvidenceProfileVersion` permanece `phase-a.1`; no cambió extracción.

## Direct effect

No hay efecto conductual. En Spring seed 100 shadow observa 28,563 LN candidates, 1,913 con peso alternativo distinto,
110,533 paths, 107,900 independent witnesses y 2,633 paths retirables de autoridad. La suma de weights sería
62,798.60 legacy frente a 59,680.35 C1 shadow. Estos números no alimentan el selector.

## Downstream effect

No aplica: sin policy conductual no existe cascade. Taps, articulación, placements y result LN ratio son legacy.

## Cross-key

Tests 1K/4K/7K/10K/18K confirman que la agrupación es por identidad de observación, sin threshold ni rama por K.
HardValidity no consulta witness count.

## Real charts

La búsqueda excluyó backups repetidos, outputs ADD y resultados sintéticos como evidencia humana. Solo se halló Spring
en tres variantes estrechamente relacionadas. No se afirma generalidad. Los samples del proyecto se utilizaron solo
como fixtures controlados.

## Spring 1–100

No se ejecutó un A/B conductual seeds 1–100 porque el Research Gate quedó HOLD y no existe una policy C1 autorizada.
Simular outputs deduplicados habría implementado REPLACE antes de justificarlo. Se mantuvo el caso histórico seed 100
en shadow y la reconstrucción exhaustiva de las 3,587 LNs originales.

## Determinism

- Legacy diagnostics OFF/ON conserva output y transcript RNG según la suite.
- Shadow C1 no llama RNG.
- Held-out usa orden estable y no usa seed.
- Candidate endpoints/order base son compartidos; solo se comparan dos columnas de weight.

## Tests

Resultado final de esta iteración: 171 passed, 0 failed, 0 skipped. Los 14 casos C1 nuevos cubren A–G, self-vote,
no-duplicate exactness, rank change, SourceAffinity, DistanceWeight, timing raro y 1K/4K/7K/10K/18K. La suite Phase B
sigue verificando RNG, blockers, articulation, JSON sin confidence/MapperSupport y output legacy.

## Performance

Spring, Release, cinco runs alternadas después de warm-up, ADD 50/interior/articulation modernos:

| Modo | CandidateBuildMs mean | Pass1Ms mean |
|---|---:|---:|
| diagnostics OFF | 28.578 | 93.504 |
| diagnostics ON + C1 shadow | 50.008 | 908.315 |

Delta candidate-build observado: +21.430 ms. El total ON incluye Phase B blockers/certificates completos
(`CertificateMs=145.577`, `FailureMs=99.429`), no solo C1. Diagnostics OFF no crea el diccionario de witnesses y no
depende del JSON de 25/455 MB. Artifact: `phase-c1-baseline/performance.json`.

## Remaining questions

1. ¿Duration+ExactRelease convergentes deben conservar una señal de acuerdo separada de authority?
2. ¿Puede esa señal derivarse sin un coeficiente manual?
3. ¿El deterioro persiste en charts humanos de otras personas, estilos y keymodes?
4. ¿Cómo excluir bloques/frases casi idénticos además del ObservationId individual?
5. ¿Qué métrica held-out debe resolver empates donde ambos rankings contienen el target?

## C2 recommendation

No avanzar a C2 como sustituto de este fallo. Primero ampliar C1 con charts humanos diversos y un experimento que
separe explícitamente independent witness authority de route agreement. C2 (frecuencia retrigger por gap/tipo) sigue
siendo una hipótesis distinta y no fue implementada.
