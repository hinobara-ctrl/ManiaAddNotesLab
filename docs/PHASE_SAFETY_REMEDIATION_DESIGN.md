# SAFETY.REMEDIATION.DESIGN — Canonical Geometry Authority Research

## Estado y frontera

**COMPLETE — READY_FOR_SEPARATE_REMEDIATION_GATE — RESEARCH ONLY.** Esta fase selecciona una arquitectura y congela la frontera de una posible implementación futura. No implementa la remediación, no modifica la generación default, no cambia collision/HardValidity/G1, y no autoriza ninguna fase sucesora.

- `RepositoryEntryHead`: `04661593e377aa261c8bcbd7a14a3087bc766964`.
- Branch de entrada: `main`; `origin/main` coincidía exactamente en la auditoría de entrada.
- Baseline: restore/build/DocConsistency/diff-check PASS; 727/727 tests PASS. El único warning fue `NU1900` por consulta de vulnerabilidades sin red.
- Dependencia científica congelada: SAFETY.CAUSAL conserva `LEGACY_GENERAL_CAUSE_FOUND + ORACLE_OR_SEMANTIC_MISMATCH_FOUND` sobre 9 treatment-only y 200 controles legacy.
- Contrato canónico de esta fase: SHA-256 `EFB31F2BF5026BE7353ACB30C15768389D077CC7244B0C91D66F8B6B6BD002F8`.

## Respuesta de autoridad

La coordenada durable y reproducible de geometría jugable es el **milisegundo entero materializado** del objeto `.osu`. Cuando una regla de colisión necesita beats, debe usar el **beat canónico reconstruido desde ese mismo entero y el timing map**. Ningún decimal anterior a materialización puede conceder permiso de placement.

La precisión latente sigue siendo legítima para intención del candidate, investigación, evidencia y la identidad exacta congelada de G1. No se sobrescribe ni se reinterpreta como igualdad de evidencia sólo porque dos valores terminen en el mismo milisegundo.

```text
intent/evidence decimal ──materialize AwayFromZero──> integer ms
           │                                      │
           └── retained as latent evidence        └── playable authority
                                                        │
                                                        └── canonical beat for collision
```

## Ciclo completo de representación

1. El `.osu` authored aporta `Int32` milliseconds.
2. El parser crea `ManiaObject`; no pierde precisión del archivo.
3. `OriginalChartAnalysis` deriva beats decimales mediante el timing point positivo activo. Para objetos originales estos beats son derivados canónicos del entero.
4. Candidate construction forma una intención decimal desde duraciones, anchors y relaciones observadas. Este valor puede participar en evidencia/G1.
5. `BeatTimeline.ToTimeMilliseconds` materializa con `decimal.Round(..., MidpointRounding.AwayFromZero)`; aquí se pierde precisión sub-ms.
6. `BuildReleaseCandidates/AddDuration` puede conservar el `intendedEndBeat` latente junto al `EndTime` ya entero.
7. El objeto colocado contiene actualmente ambas vistas; `LaneGeometryIndex` usa el decimal latente.
8. Commit/`AddedObjects` conservan el objeto materializado entero; el latent beat no es durable.
9. El writer serializa enteros sin crear el conflicto.
10. Reparse/HardValidity reconstruyen beats desde los enteros y aplican las mismas relaciones inclusivas/estrictas sobre geometría canónica.

El inventario reproducible, incluidos tipo numérico, redondeo, timing dependency, pérdida de información y autoridades, vive en `safety_remediation_representation_inventory.csv`.

## Inventario de responsabilidades

| Representación | Autoridad jugable/colisión | Autoridad de evidencia | Dependencia G1 |
|---|---:|---:|---:|
| `.osu`/`ManiaObject` integer ms | Sí | provenance authored | Sí para coordenadas originales |
| Beat original derivado del entero | Sí | Sí | Sí |
| Candidate-intent / latent decimal | No | Sí | Sí |
| Beat canónico reconstruido del ms materializado | Sí | No redefine evidencia | No reemplaza identidad |
| `AddedObjects`/serialization integer ms | Sí | No | No |
| HardValidity canonical geometry | Sí | No | No |

G1 conserva exactamente duración padre, anchor offset, `AnchorKind`, candidate `EndBeat`, `InteriorEndRelation`, duración desde anchor, offset desde parent end, query/result signatures y estado `Unique/AmongAlternatives`. Canonicalizar esos campos de evidencia sería contaminación semántica y vuelve incompatible cualquier diseño.

## Superficies de colisión

| Superficie | Clasificación | Resultado |
|---|---|---|
| tap vs LN previa / LN end vs tap posterior | afectada y observada | 209 casos congelados y 6 deltas directos adicionales |
| LN head vs LN previa | no afectada | overlap LN usa desigualdad estricta; igualdad de borde sigue legal |
| LN vs LN overlap | no afectada | misma razón; ningún delta directo observado |
| equal starts | no afectada | los starts observados ya provienen de integer ms |
| equal releases | no afectada | igualdad de releases sola no es hard violation |
| zero-gap LN→LN | no afectada | hard collision y spacing requerido siguen separados |
| same-ms/distinct-decimal endpoint | afectada y observada | es la forma representacional de los 215 deltas directos |
| timing-point boundary | no observado en corpus | cubierto estructuralmente por la autoridad; quedan por certificar trayectorias completas |

No se cambia `>=` a `>`: placement y HardValidity ya coinciden en que el endpoint de una LN está ocupado. La contradicción era únicamente qué representación llegaba al predicado.

## Candidatos evaluados

### A — Materialized-Geometry Canonicalization

Materializa a integer ms y reconstruye el beat jugable antes de insertar/consultar geometry. Cubre la causa y es compatible con G1 si conserva el latent por separado. Es viable, pero por sí sola deja dos consumidores que todavía podrían implementar reglas divergentes.

### B — Canonical Pre-Commit HardValidity

Aplica un veto canónico después del placement legacy. Cubre los casos, pero crea placement más un segundo selector, duplica reglas y eleva el riesgo de drift. Es un fallback de defensa, no la arquitectura primaria recomendada. El shadow aquí es puro: no muta estado ni consume RNG.

### C — Shared Canonical Playable Geometry

Introduce un modelo explícito de geometría jugable compartido por placement y HardValidity. Sus coordenadas provienen de integer ms y, si necesita beats, de su reconstrucción canónica. Es el único candidato con single source of truth probado por construcción y el menor riesgo futuro de drift. **Preferido.**

### D — Latent Values as Evidence Only

Es un invariante obligatorio: latent/intended precision nunca concede permiso jugable. No es por sí sola una arquitectura completa y se compone naturalmente con C.

La matriz completa usa `PROVEN`, `SUPPORTED`, `UNKNOWN` e `INCOMPATIBLE` sin scores inventados en `safety_remediation_candidate_matrix.csv`.

## Footprint directo de sombra

El runner reprodujo 11 charts, 220 runs control y 4 runs treatment, AddChance 0,50, sobre **307.167 propuestas realmente comprometidas**. El observer produjo exactamente la misma proyección que `AddedObjects` en 224/224 runs, consumió cero RNG y no pudo vetar decisiones.

| Resultado directo | Conteo |
|---|---:|
| unchanged accept | 306.952 |
| legacy accept → candidate reject | 215 |
| unexpected legacy conflict | 0 |
| known 209 addressed | 209 |
| known 209 not addressed | 0 |
| adicionales fuera de los 209 | 6 |
| familia de los 215 | `TapOnHeldLongNote` |

Los seis adicionales están en treatment y comparten exactamente la misma superficie; no cambian la clasificación causal congelada. Los cuatro candidatos aplicados a la misma frontera canónica tienen el mismo footprint directo, pero difieren radicalmente en autoridad, duplicación y riesgo de drift.

**Advertencia:** `215 direct shadow deltas != 215 final output changes`. El generador es path-dependent: el primer rechazo futuro podría cambiar occupancy, oportunidades posteriores, decisiones RNG-indexed y densidad. Esta fase no ejecutó esa trayectoria contrafactual.

## Timing, redondeo e idempotencia

Se probaron boundaries exactos, valores inmediatamente adyacentes, midpoints positivos/negativos, offsets negativos, dos segmentos de timing y repetición `beat → ms → beat`. `AwayFromZero` produce `+1/-1` en los midpoints mínimos probados. `canonicalize(canonicalize(x)) == canonicalize(x)` para la representación propuesta. `BeatTimeline` utiliza redlines positivas/no heredadas para el tiempo jugable; inherited timing no introduce una autoridad paralela.

Counterexample mínimo:

```text
LN: EndTime = 500 ms, latent EndBeat = 0.999999...
Tap: StartTime = 500 ms, beat = 1

legacy latent geometry: accept
candidate canonical geometry: reject TapOnHeldLongNote
canonical HardValidity: reject TapOnHeldLongNote
```

Control exacto `EndBeat = 1`: legacy y canonical rechazan. Control LN→LN con borde igual: ambos aceptan por overlap estricto.

## Bad controls

1. Cambiar `>=` por `>`: **REJECTED**, altera safety en vez de corregir autoridad.
2. Sobrescribir latent/G1: **REJECTED**, contamina evidencia congelada.
3. Segunda implementación independiente: **REJECTED**, duplica autoridad y puede derivar.
4. Writer roundtrip como oracle: **REJECTED**, serialization no prueba equivalencia runtime.
5. Same-ms como misma identidad: **REJECTED**, playable equality no es evidence equality.
6. Direct delta como final delta: **REJECTED**, ignora path dependence.
7. Sintéticos enseñando estilo: **REJECTED**, original-only permanece invariante.
8. Arreglar sólo tap/LN: **PASSED por el diseño**, se auditó toda la superficie bajo una autoridad común.
9. Representación no idempotente: **PASSED**, los fixtures son idempotentes.
10. Check que muta/consume RNG: **PASSED**, observer y shadow son read-only/RNG-free.

## Invariantes congelados para una implementación futura

**Representación:** todo endpoint jugable tiene un integer ms durable y, cuando se consulta en beat-space, un beat canónico derivado exclusivamente de ese entero y el timing map. Recanonicalizarlo no cambia el valor.

**Placement:** toda consulta de colisión consume exclusivamente geometría jugable canónica; latent intent no otorga permiso.

**Commit:** si placement acepta una mutación, insertarla en el mismo estado canónico no introduce una hard collision bajo los predicados compartidos. Spacing conserva su regla separada.

**Evidencia/G1:** latent intent y las identidades exactas existentes sobreviven sin canonicalización; sólo originales aportan style authority.

**Serialization:** el objeto serializado preserva el mismo integer ms que gobernó placement. Reparse reconstruye la misma geometría jugable.

## Diseño preferido y futura certificación

Se recomienda **C + el invariante D**: un `CanonicalPlayableGeometry` explícito compartido por `LaneGeometryIndex` y HardValidity, construido en la frontera de materialización. Intent/evidence permanecen en campos separados. El rollback futuro debe ser una versión de policy/implementación completa, nunca una relajación silenciosa de HardValidity.

La implementación esperablemente cambiará sólo decisiones donde el latent y el endpoint materializado discrepan para una relación de colisión; el footprint observado mínimo es 215 accepts→rejects. Aun así, cualquier rechazo puede cambiar la trayectoria, el consumo posicional de RNG posterior, la densidad y el output final. Una fase separada debe certificar A/B contrafactual completo, bytes y RNG, todos los keymodes soportados, timing boundaries, collision families, HardValidity final, densidad, determinismo, G1 invariance y rollback. También debe investigar explícitamente los seis deltas adicionales.

## Conclusión

`READY_FOR_SEPARATE_REMEDIATION_GATE` significa únicamente que la arquitectura está suficientemente especificada para que el mantenedor considere autorizar otra fase. La implementación/gate sigue `NOT_AUTHORIZED`; G1 utility, G2 y H siguen `NOT_AUTHORIZED`. G1.GATE permanece `COMPLETE — NEEDS_REVIEW / NO PROMOTION`; SAFETY.CAUSAL permanece `COMPLETE — OUTCOME A / CAUSE FOUND / NO REMEDIATION`; default permanece `legacy-experimental.1`.

No se inició la siguiente fase y no se hizo staging, commit, push, tag ni release.
