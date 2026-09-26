# SAFETY.REMEDIATION — OP-466 Candidate Admission Forensics

## Resultado

**Outcome diagnóstico: `MECHANISM_IDENTIFIED`.**

La condición concreta que separa las tres trayectorias en OP-466 es la admisión probabilística previa a construir el candidato:

```csharp
effectiveChance > 0
    && (effectiveChance >= 1 || rng.NextDouble() < effectiveChance)
```

En las tres trayectorias `effectiveChance` vale exactamente `0.5`. Legacy consume en la posición RNG 667 la muestra `0.30024478691641465`, pasa porque `0.30024478691641465 < 0.5`, construye el candidato, enumera lanes, ejecuta `Next(3) = 1`, selecciona lane 2 y compromete el target histórico. Canonical A y canonical B consumen en la posición 659 la misma muestra auténtica `0.7305439518441185`; ambas fallan porque `0.7305439518441185 >= 0.5` y salen antes de construir candidato o consultar lanes.

Esto identifica el mecanismo inmediato de admisión. No convierte automáticamente el sexto caso histórico en una categoría certificada, no prueba que OP-185 sea su causa exclusiva y no atribuye cada transición anterior del transcript. El estado científico global se conserva en `NEEDS_REVIEW`, sin promoción.

## Protección inicial de evidencia

- Rama: `main`.
- HEAD local observado: `41c501c73e775087738c01d726c950d7dede6350`.
- `git ls-remote origin refs/heads/main` no pudo conectar a `github.com:443`; por ello no se afirmó sincronía remota.
- Estado inicial: únicamente los tres documentos personales sin seguimiento solicitados.
- Se preservaron sin modificación `docs/ASTRA_ROADMAP_V2_PROPOSAL.md`, `docs/HANDOFF_GLOBAL_2026-09-23.md` y `docs/HANDOFF_REENTRY_PROMPT_2026-09-23.md`.
- No se usaron `git add`, commit, push, tag, release, PR, reset ni limpieza.

## Baseline e identidades

- Implementación conductual congelada: `B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835`.
- Corpus C11: `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`.
- Chart: `20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788`.
- Seed: `4`.
- Configuración: `Chance=0.50`, articulación OFF, G1 no aplicable, resto de opciones congeladas de `CurrentOptions()`.
- Límite del proceso: `DOTNET_GCHeapHardLimit=0x400000000` (16 GiB).

Los SHA-256 de las entradas usadas en `.artifacts/safety_selection_set_remapping/` y `.artifacts/safety_op185_op466_counterfactual/` se comprobaron contra sus respectivos `sha256sums.txt` antes de utilizarlas. El inventario nuevo contiene las identidades individuales de informes, código y artifacts consultados.

## Camino real de generación

Las rutas y funciones reales son:

1. `AddNotesEngine.Apply` construye una sola secuencia lógica con `BuildOpportunities`. La oportunidad OP-466 existe independientemente de los objetos añadidos anteriormente.
2. `BeginOpportunity` captura el estado y posición RNG padre.
3. `HeadDensityAnalyzer.Analyze`, `LaneGeometryIndex.CountOccupiedColumns`, `CountSimultaneousHeadColumns` y `CountHeldLnColumns` calculan entradas de densidad. La instancia `densityGeometry` contiene exclusivamente originales y nunca recibe inserciones sintéticas.
4. `CalculateVerticalDensity` produce el factor vertical. El motor calcula `effectiveChance` y ejecuta el roll probabilístico.
5. Si el roll falla, registra `ProbabilityAbstain`, llama `CompleteOpportunity(..., null)` y continúa. En esta rama no se invoca `PlaceTap`.
6. Si pasa, `PlaceTap` consulta `FindLegalTapLanes` sobre las geometrías legacy y canonical, registra la decisión candidata y escoge la autoridad activa.
7. Sólo con una lista activa no vacía ejecuta `lanes[rng.Next(lanes.Count)]`.
8. El objeto seleccionado se añade a geometría y output, y `CompleteOpportunity` registra el commit.

Por tanto, «sin candidato» en A/B no es un rechazo geométrico: las lanes no fueron evaluadas porque el retorno se produjo en el paso 5.

## Entradas exactas de OP-466

Las entradas previas al roll se reconstruyeron mediante las mismas clases públicas del motor sobre el chart original:

| Entrada | Valor |
|---|---:|
| source / tiempo / beat | `S466` / `35009` / `74.49995000000002979998` |
| keymode | 7K |
| chance base | `0.5` |
| columnas ocupadas / heads simultáneos / LN sostenidas | `4 / 1 / 3` |
| densidad micro / contexto / ratio | `4 / 5 / 0.8` |
| factor contextual | `1` |
| columnas usadas por el modo vertical | `1` (`SimultaneousHeads`) |
| factor chord | `1` |
| **effectiveChance** | **`0.5`** |

Estas entradas dependen sólo de originales. Los estados materializado y latente sí difieren entre L, A y B, pero no alimentan esta condición. Sólo podrían afectar la geometría posterior si el roll autorizara continuar.

## Comparación L/A/B en OP-466

| Etapa | L — legacy | A — canonical normal | B — canonical OP-185 intervenido |
|---|---|---|---|
| oportunidad | misma key, orden 468, alcanzada | misma key, orden 468, alcanzada | misma key, orden 468, alcanzada |
| RNG padre | 667 | 659 | 659 |
| llamada de admisión | `NextDouble()` | `NextDouble()` | `NextDouble()` |
| muestra | `0.30024478691641465` | `0.7305439518441185` | `0.7305439518441185` |
| umbral | `0.5` | `0.5` | `0.5` |
| resultado | PASS | FAIL | FAIL |
| candidato | tap construido | no construido | no construido |
| lanes | legacy `[1,2,3]`; canonical `[1,3]` | no evaluadas | no evaluadas |
| selección | `Next(3)=1` → lane 2 | ninguna | ninguna |
| commit | target exacto | ninguno | ninguno |

Los estados padres de A y B no son iguales: la intervención de OP-185 produjo trayectorias materiales distintas. Sin embargo, para la admisión de OP-466 usan las mismas entradas originales, la misma posición, la misma muestra auténtica y el mismo resultado. Los transcripts auténticos completos de A/B son idénticos: 13.730 llamadas, SHA-256 `8F5589B0045254ECCAE68DFD3E01037D8C5771CA6BD7081132B173C2B6762DED`.

## Primera diferencia pertinente de consumo RNG

La primera diferencia se origina en OP-370 (`OP-00000370-BaseHead-S370-T29933-ANA`, orden 372):

- Ambos brazos llegan con posición 532 y consumen exactamente el mismo `NextDouble() = 0.379909015903207` frente a umbral `0.5`; ambos pasan la admisión.
- Legacy encuentra el conjunto activo `[4]`, ejecuta `Next(1) = 0`, selecciona lane 4 y compromete el tap. Sale en posición 534.
- Canonical registra legacy `[4]` y canonical `[]`; su autoridad activa queda sin lane, no ejecuta selección y no compromete objeto. Sale en posición 533.

OP-370 inicia el desfase de una llamada, pero no es su único contribuyente. Entre OP-370 y OP-466 hay varias oportunidades con consumos distintos; el CSV focalizado registra cada cambio. Antes de OP-466 el desfase acumulado es `667 - 659 = 8`; tras OP-466 pasa a 9 porque legacy consume roll y selección mientras canonical sólo consume el roll.

La cadena demostrada para la admisión es:

`diferencias anteriores de ramas/selecciones` → `offset acumulado de transcript` → `muestra distinta en OP-466 con umbral idéntico` → `PASS en L / FAIL en A y B`.

La pericia no aísla contrafactualmente cuál de todos los cambios anteriores es necesario o suficiente por sí solo para producir el offset final. Sí demuestra que, dado el estado RNG observado al llegar a OP-466, la muestra distinta basta para explicar completamente la decisión de admisión.

## Hipótesis H1–H6

- **H1 aceptada para el mecanismo inmediato.** El mismo umbral recibe muestras diferentes por el desfase histórico y produce resultados opuestos.
- **H2 rechazada.** No hay otra condición previa que impida el candidato en A/B; `effectiveChance` es positivo y menor que 1, por lo que el roll es la condición decisiva.
- **H3 no explica la admisión.** El estado materializado/latente difiere, pero no participa en el cálculo probabilístico original-only. Sigue siendo relevante para una eventual geometría posterior.
- **H4 rechazada.** Key, orden, source, tiempo y contexto lógico original coinciden.
- **H5 aceptada aguas arriba.** Hay múltiples diferencias concurrentes de consumo y commits; OP-370 es la primera, no la única.
- **H6 no observada.** Código, reproducción y artifacts son coherentes.

## Reproducción focalizada y no interferencia

Se ejecutó una sola pareja L/A. B no se reejecutó; se reutilizó su artifact congelado. El recorder envuelve `SeededRandom`, registra después de cada llamada y no invoca RNG adicional.

La pareja focalizada coincidió exactamente con las referencias:

| Identidad | L | A |
|---|---|---|
| output serializado SHA-256 | `71D78994...E93D94` | `462307AE...38E999` |
| llamadas RNG | 13.730 | 13.730 |
| transcript SHA-256 | `804CA1EF...B80FB90` | `8F5589B0...762DED` |
| diagnostics fingerprint | `9FBA7641...52AA6B` | `26804F0A...D74875` |

También coincidieron OP-466, orden 468, estados suficientes, candidato/commit y ausencia de candidato/commit según brazo. G1 no corresponde a esta comparación primaria. No se ejecutó la matriz de 224 pares, no se exploró `Songs` y no cambió código productivo.

## Nueva evidencia

Directorio: `.artifacts/safety_op466_candidate_admission/`.

- `source_inventory.json`: archivos e identidades usados.
- `admission_decisions.json`: entradas, muestras y decisiones exactas L/A/B.
- `lab_comparison.json`: comparabilidad y diferencias preservadas.
- `first_rng_divergence.json`: OP-370 con llamadas y ramas.
- `rng_consumption_divergences.csv`: todas las oportunidades hasta OP-466 cuyo consumo por oportunidad difiere.
- `op466_explanation.json`: conclusión e hipótesis.
- `no_interference.json`: comparación congelada exacta.
- `summary.json`: outcome e identidades.
- `sha256sums.txt`: hashes de la evidencia nueva.

## Archivos de código inspeccionados

- `src/ManiaAddNotesLab.Core/AddNotesEngine.cs`
- `src/ManiaAddNotesLab.Core/Model.cs` (`IRandomSource`, `SeededRandom`, opciones)
- `src/ManiaAddNotesLab.Core/D1BehavioralExperiment.cs` (`IRandomPositionSource`)
- `src/ManiaAddNotesLab.Core/ChartAnalysis.cs`
- `src/ManiaAddNotesLab.Core/LaneGeometryIndex.cs`
- `src/ManiaAddNotesLab.Core/SafetyRemediationGateResearch.cs`
- `tools/ManiaAddNotesLab.Experiments/SafetySelectionSetRemappingRunner.cs`
- `tools/ManiaAddNotesLab.Experiments/SafetyOp185Op466CounterfactualRunner.cs`

La instrumentación nueva está aislada en `SafetyOp466CandidateAdmissionRunner.cs` y su entrada CLI en `Program.cs`. No se modificaron `AddNotesEngine`, `SeededRandom`, `HardValidity`, `LaneGeometryIndex`, G1, AddChance, defaults, clasificador oficial, contratos ni artifacts históricos.

## Validación y reproducción

Reproducción focalizada:

```powershell
$env:DOTNET_GCHeapHardLimit='0x400000000'
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- `
  safety-op466-candidate-admission-run . .artifacts/f2-1-corpus `
  docs/g1_gate_runtime_manifest.json `
  .artifacts/safety_op466_candidate_admission
```

Validación final prescrita:

```powershell
dotnet restore ManiaAddNotesLab.sln --disable-parallel
dotnet build ManiaAddNotesLab.sln -c Release -m:1
dotnet test ManiaAddNotesLab.sln -c Release -m:1
dotnet run --project tools/DocConsistency -c Release -- --check
git diff --check
```

Resultados observados:

- restore: correcto; `NU1900` únicamente porque el índice de vulnerabilidades de NuGet no fue accesible;
- build Release: correcto, 0 errores;
- tests: **826/826 aprobados**, 0 fallos, 0 omitidos;
- DocConsistency: `DOCUMENTATION CONSISTENCY: PASS`;
- `git diff --check`: correcto; sólo se informó la advertencia de normalización LF→CRLF para `Program.cs`.

Ninguna de estas conclusiones autoriza cambios conductuales, promoción ni reclasificación histórica.
