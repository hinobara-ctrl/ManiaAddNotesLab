# SAFETY.REMEDIATION — OP-466 RNG drift, geometry audit and Jules handoff

## Estado y alcance

Esta tarea es una reconstrucción observacional. No ejecutó un nuevo contrafactual, no forzó admisión, no consultó una geometría incompleta y no declara verificación independiente.

Estado científico preservado:

- **DOCUMENTED:** `E_SELECTION_SET_REMAP` 8/8.
- **DOCUMENTED:** consolidación histórica `188 A / 21 B / 5 D / 1 C`.
- **DOCUMENTED:** contrafactual OP-185 `ABSENT_BOTH`.
- **DOCUMENTED:** admisión OP-466 `MECHANISM_IDENTIFIED`.
- **DOCUMENTED:** OP-466 histórico `C_UNRESOLVED`.
- **DOCUMENTED:** global `NEEDS_REVIEW / NO PROMOTION`.

HEAD de entrada: `075cd7f95985178c37fb80337e5ad5c059a67bae`, rama `main`. GitHub no respondió en `github.com:443`; no se verificó el remoto. Los tres documentos personales sin seguimiento se preservaron sin leer ni modificar.

## Fuentes e integridad

Se consultaron los informes de Final Recertification, Focused Forensic Attribution, Selection-Set Remapping Design/Gate/Harness Hardening, OP-185→OP-466 Design/Gate y Candidate Admission Forensics, junto con sus contratos publicados. También se inspeccionaron:

- `.artifacts/safety_op466_candidate_admission/`: 8/8 entradas de `sha256sums.txt` verificadas;
- `.artifacts/safety_op185_op466_counterfactual/`: 12/12 verificadas;
- `.artifacts/safety_selection_set_remapping/`: 20/20 verificadas;
- `AddNotesEngine.cs`, `LaneGeometryIndex.cs`, `Model.cs` y los runners forenses pertinentes.

El manifiesto del handoff registra cada SHA-256 y el experimento productor. Ningún artifact histórico fue modificado.

## Método de reconstrucción RNG

La fuente primaria es `rng_consumption_divergences.csv`, que contiene las 30 oportunidades entre OP-370 y OP-466 con consumo desigual. Para cada fila se conservan posiciones, cantidades y commits originales.

- **DOCUMENTED:** posiciones, llamadas consumidas y commits provienen del CSV original.
- **DOCUMENTED:** las llamadas canónicas exactas se extraen de `rng_transcripts.json`, control canonical A.
- **CODE_CONFIRMED:** un tap comprometido consume roll `Double` y selector `Integer`; una LN comprometida consume roll, `Double` ponderado e `Integer` de lane.
- **RECONSTRUCTED:** los `NextDouble` legacy se reproducen por posición con `SeededRandom`/`System.Random(4)`. La reproducción coincide exactamente con los anclajes directamente observados: posición 532 = `0.379909015903207`; posición 667 = `0.30024478691641465`.
- **UNKNOWN:** maximum y resultado de selectores legacy no preservados, salvo OP-370 (`Next(1)=0`) y OP-466 (`Next(3)=1`).
- **UNKNOWN:** una sola llamada Double sin commit no permite distinguir abstención probabilística de admisión seguida por imposibilidad geométrica porque el CSV no preservó el umbral/candidate de esa oportunidad.

No se infirieron lanes por el mero hecho de que haya consumo idéntico: conjuntos o cardinalidades diferentes pueden seguir consumiendo exactamente un `Next` por brazo.

## Tabla exhaustiva del desfase

La tabla completa con posiciones, llamadas tipadas, maximum/resultados disponibles, candidates, lanes, commits y procedencia está en `rng_drift_reconstruction.csv`. Esta vista resume todas sus filas. `U` significa que la rama exacta no quedó preservada; `commit` prueba admisión, geometría no vacía y selección, pero no el conjunto exacto de lanes.

|#|Opportunity|ms|Tipo|L|A|Local|Delta antes→después|Legacy|Canonical A|
|-:|---|--:|---|--:|--:|--:|---:|---|---|
|1|S370-T29933|29933|Tap|2|1|+1|0→1|lanes `[4]`, commit|lanes `[]`, sin selector|
|2|S373-T30046|30046|Tap|2|1|+1|1→2|commit|U|
|3|S382-T30666|30666|Tap|1|2|-1|2→1|U|commit|
|4|S386-T30892|30892|Tap|2|1|+1|1→2|commit|U|
|5|S391-T31118|31118|LN|3|1|+2|2→4|commit|U|
|6|S393-T31230|31230|Tap|1|2|-1|4→3|U|commit|
|7|S394-T31287|31287|Tap|1|2|-1|3→2|U|commit|
|8|S398-T31456|31456|Tap|2|1|+1|2→3|commit|U|
|9|S406-T31738|31738|Tap|1|2|-1|3→2|U|commit|
|10|S409-T31851|31851|Tap|2|1|+1|2→3|commit|U|
|11|S412-T32020|32020|Tap|2|1|+1|3→4|commit|U|
|12|S416-T32133|32133|LN|3|1|+2|4→6|commit|U|
|13|S417-T32189|32189|LN|1|3|-2|6→4|U|commit|
|14|S420-T32302|32302|Tap|2|1|+1|4→5|commit|U|
|15|S421-T32358|32358|Tap|1|2|-1|5→4|U|commit|
|16|S425-T32527|32527|Tap|2|1|+1|4→5|commit|U|
|17|S431-T32753|32753|Tap|2|1|+1|5→6|commit|U|
|18|S433-T32809|32809|Tap|2|1|+1|6→7|commit|U|
|19|S434-T32866|32866|Tap|2|1|+1|7→8|commit|U|
|20|S435-T32922|32922|Tap|2|1|+1|8→9|commit|U|
|21|S443-T33204|33204|Tap|1|2|-1|9→8|U|commit|
|22|S444-T33204|33204|Tap|2|1|+1|8→9|commit|U|
|23|S447-T33373|33373|Tap|2|1|+1|9→10|commit|U|
|24|S452-T33655|33655|Tap|1|2|-1|10→9|U|commit|
|25|S455-T33993|33993|Tap|1|2|-1|9→8|U|commit|
|26|S458-T34332|34332|LN|1|3|-2|8→6|U|commit|
|27|S463-T34783|34783|LN|1|3|-2|6→4|U|commit|
|28|S464-T34783|34783|LN|3|1|+2|4→6|commit|U|
|29|S465-T34783|34783|LN|3|1|+2|6→8|commit|U|
|30|S466-T35009|35009|Tap|2|1|+1|8→9|lane 2, commit|probability abstain|

## Conciliación exacta

Para cada fila se verificó:

`deltaAfter = deltaBefore + legacyConsumed - canonicalConsumed`.

No hubo incumplimientos en 30/30 filas.

- Antes de OP-370: `0`.
- Después de OP-370: `+1`.
- Filas positivas hasta antes de OP-466: `+22` llamadas legacy.
- Filas negativas hasta antes de OP-466: `-14` llamadas legacy.
- Antes de OP-466: `+22 - 14 = +8`, exactamente `667 - 659`.
- OP-466 aporta `+1`: legacy usa roll + selector; canonical sólo roll.
- Después de OP-466: `+9`, exactamente `669 - 660`.

Por distribución, las 30 filas contienen 15 eventos `+1`, cuatro `+2`, ocho `-1` y tres `-2`; su suma incluyendo OP-466 es `+9`. No son ocho oportunidades divergentes y existen compensaciones en ambas direcciones.

Los runs completos terminan ambos con 13.730 llamadas. **DOCUMENTED:** los hashes completos son distintos pero los conteos finales iguales. Por tanto, después de OP-466 existe una compensación neta de `-9`. **UNKNOWN:** los artifacts preservados para esta investigación no contienen una tabla legacy/canonical de todas las diferencias posteriores a OP-466, por lo que no se atribuyen artificialmente las oportunidades exactas de reconvergencia del conteo.

## Primera divergencia y ramas

En OP-370 ambos brazos llegan en 532, consumen el mismo `NextDouble()=0.379909015903207` y pasan el roll `0.5`.

- Legacy encuentra `[4]`, ejecuta `Next(1)=0`, compromete lane 4 y termina en 534.
- Canonical encuentra conjunto activo `[]`, no llama al selector, no compromete y termina en 533.

Esto es rama **B**: ambas trayectorias superan admisión y sólo una tiene lanes. Es el primer desfase, no una explicación exclusiva del offset final.

Las filas con commits permiten confirmar ramas de selección: tap = roll + selector; LN = roll + selección ponderada + selector. Las filas sin commit y con una sola llamada quedan `UNKNOWN` porque podrían ser rama A (falló probabilidad) o una variante B/D posterior a admisión. No se inventó la condición faltante.

## Auditoría geométrica de OP-466

### Legacy

**DOCUMENTED / GEOMETRY_ALREADY_EVALUATED.** En el estado legacy, la autoridad legacy obtuvo `[1,2,3]`, seleccionó lane 2 y comprometió el target. La sombra canónica sobre ese mismo estado obtuvo `[1,3]`; lane 2 es ilegal bajo reglas canónicas aplicadas al estado legacy.

Esto no responde la legalidad en A/B porque sus estados padres son distintos.

### Canonical A

**GEOMETRY_INCOMPLETE.** Se conservan el estado suficiente, hashes materializado/latente, posición RNG, commits desde OP-185 hasta OP-466 y fingerprints completos. La probabilidad falló antes de `PlaceTap`, por lo que no hay candidate ni lanes OP-466.

Faltan los objetos sintéticos concretos comprometidos antes de OP-185 o un snapshot serializado completo del padre OP-466. El hash acumulador no permite recuperar objetos, lanes, intervalos ni vecinos. Lane 2 permanece **UNKNOWN**.

### Canonical B

**GEOMETRY_INCOMPLETE.** Tiene hashes padres propios y trayectoria de commits posterior a la intervención OP-185, pero comparte el mismo prefijo incompleto: antes de OP-185 sólo se preservó el hash del estado, no sus objetos. Tampoco existe un `pre_op466_snapshot.json` ni candidate geometry decision en OP-466. Lane 2 permanece **UNKNOWN**.

### Por qué no se realizó consulta pasiva

`LaneGeometryIndex` requiere la colección concreta de `TimedManiaObject`. La autoridad canónica reproyecta los milisegundos durables mediante `CanonicalPlayableGeometry` y evalúa vecinos/endpoints reales. Los hashes de `materializedGenerationState` y `latentCommittedGeometry` son identidades, no snapshots invertibles.

Sin poder reconstruir y volver a igualar los fingerprints históricos, una consulta produciría una geometría aproximada. Por contrato se detuvo la evaluación: no se forzó admisión, no se creó candidate y no se modificó el motor.

## Paquete para Jules

Ubicación: `.artifacts/safety_op466_jules_handoff/`.

Contenido:

- `manifest.json`: HEAD, alcance, SHA-256, identidades y derivación;
- seis copias byte a byte del artifact de admisión;
- `downstream_trajectory.csv`, `pre_op185_snapshot.json` y `op466_result.json`, copias byte a byte;
- `rng_drift_reconstruction.csv`: tabla derivada de 30 filas;
- `rng_fragment.json`: fragmentos canónicos exactos y replay legacy con anclajes;
- `geometry_evidence.json`: suficiencia separada L/A/B;
- `README.md`: procedencia, etiquetas y lectura;
- `sha256sums.txt`: integridad del paquete.

Se excluyeron charts, corpus C11, Songs, documentos personales, rutas privadas y transcript completo de 3,9 MB. El paquete no publica ni sincroniza nada y no debe describirse como auditoría independiente.

## Limitaciones y siguiente paso propuesto

La reconstrucción del offset 0→8→9 es completa dentro del intervalo solicitado. No es completa para la reconvergencia posterior hasta el conteo final ni para las ramas sin commit cuyos umbrales/candidates no se guardaron.

La geometría A/B no es reconstruible con fidelidad suficiente desde los artifacts actuales. Un próximo experimento requeriría autorización separada: capturar de forma observacional la lista ordenada completa de objetos de geometría canónica inmediatamente antes de OP-466, comprobarla contra el estado histórico y usar `InspectTapLane`/`FindLegalTapLanes` sin forzar el roll ni comprometer un objeto. Esta propuesta no fue implementada.

## Validación

La validación final se ejecuta secuencialmente con límite de heap de 16 GiB:

```powershell
dotnet restore ManiaAddNotesLab.sln --disable-parallel
dotnet build ManiaAddNotesLab.sln -c Release -m:1
dotnet test ManiaAddNotesLab.sln -c Release -m:1
dotnet run --project tools/DocConsistency -c Release -- --check
git diff --check
```

Resultados efectivos:

- restore correcto; única advertencia `NU1900` por no poder consultar el índice de vulnerabilidades de NuGet;
- build Release correcto, 0 errores;
- tests **826/826**, 0 fallos, 0 omitidos;
- DocConsistency: `DOCUMENTATION CONSISTENCY: PASS`;
- `git diff --check`: correcto;
- fingerprint conductual recalculado: `B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835`, coincidencia exacta con el congelado.

ZIP de entrega: `.artifacts/safety_op466_jules_handoff.zip`, 34.113 bytes, SHA-256 `72F07EC2E947F9A3859A28C2E790DA1399E79B29D677A83F4D2FC0DBDDD9CF43`.
