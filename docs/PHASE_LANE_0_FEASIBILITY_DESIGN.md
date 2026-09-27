# LANE.0 — Original-only spatial relation feasibility design

Estado: **PREREGISTRATION DRAFT — NO C11 RESULT INSPECTED**  
Modo: research-only, original-only, shadow, sin autoridad conductual  
HEAD de entrada aprobado: `be08f3f2f0191a5c972ad16449a7199dd07f2e3f`

## Pregunta primaria falsable

¿Las dos familias espaciales completas definidas aquí —completion de heads simultáneos (rice) y relación interior parent/anchor/witness (G1)— presentan, después de holdout estricto chart-local, soporte conjunto independiente recurrente en al menos dos charts y dos keymodes por familia, y además aparecen en el universo operativo actual, sin leakage, composición artificial, RNG ni interferencia conductual?

La respuesta será `FEASIBILITY_DEMONSTRATED` sólo si ambas familias satisfacen todos esos requisitos. Si la instrumentación e integridad son válidas pero alguna familia no los satisface, el cierre será `LIMITED_PARK`. Datos o identidades imprescindibles ausentes producen `BLOCKED`; un preregistro, hash, control adversarial, determinismo o no-interferencia fallido produce `INVALID`. Estos criterios se congelan antes de consultar C11.

## Representaciones reutilizadas

- `MapperEvidenceProfile` aporta `OriginalObservationId`, fingerprint, observaciones, chords, transiciones y anchors exclusivamente originales. Una observation puede sostener varios claims sin multiplicar su independencia.
- `OriginalChartAnalysis` materializa tiempos/beats originales y sus índices por lane sin convertir sintéticos en evidencia.
- `SimultaneousOriginalEventGroup` conserva el grupo exact-head completo y sus miembros tipados.
- `ChordCompletionResearch` ya expresa reduced-state → `(lane, head type)`, provenance de grupo completo, alternativas y whole-group holdout. LANE.0 no duplica esa estructura.
- `ResultingStateCompositionResearch` demuestra por qué dos miembros marginales no constituyen una composición conjunta; LANE.0 aplica la misma regla a tiempo+espacio.
- F1 proporciona la semántica que se conserva: support, contradiction/mismatch, no comparable context y ambiguity.
- `InteriorRelationFeasibilityResearch` conserva la occurrence G1 completa parent/anchor/witness, temporalidad exacta, lanes, identidad, geometría original y auditoría de gates.
- `InteriorRelationMembershipResearch` separa evidencia constructiva de independiente y alternativas de winner; LANE.0 no convierte membership en selector.
- `LaneGeometryIndex` representa `CurrentGeometry` y HardValidity. No aporta StyleEvidence y no participa en el resultado primario.

## Auditoría del selector legacy

| Ruta | Enumeración geométrica | Filtros/preferencias | Selección espacial | RNG | Dependencia sintética |
|---|---|---|---|---|---|
| Tap de base | `FindLegalTapLanes` en orden 0..K-1 | collision según autoridad legacy/canónica del treatment | índice uniforme `lanes[rng.Next(count)]` | una llamada entera si hay lanes | sí; el índice contiene originales y commits previos |
| LN de base | `FindLegalLnLanes` por cada shape | overlap y gap; shape evidence/context antes de lane | primero `TakeWeighted` sobre shapes legales; luego lane uniforme | `NextDouble` + `Next(count)` | sí |
| LN interior G1 | misma ruta LN | eligibility/cap/anchor/context forman oportunidad; después shape/gap/geometry | misma elección shape ponderada y lane uniforme | igual que LN | sí |
| Articulación | `CanReplaceWithArticulation` en lane del parent | head-anchor, retrigger, longitudes y geometry | no elige lane; conserva la lane original del parent | ruleta separada entre candidatos de corte | sí para clear segments; style context original-only |

HardValidity determina si una columna está disponible; no determina si está respaldada estilísticamente. La lane legacy para tap/LN sigue siendo uniforme entre las legales. LANE.0 no modifica ninguno de estos puntos.

## Dos familias exactas

### RICE_HEAD_COMPLETION

- Occurrence: grupo original simultáneo completo con al menos dos heads.
- Target: un miembro original retenido en holdout.
- Query: keymode + lanes/tipos exactos de los miembros visibles tras retirar el target.
- Resultado conjunto: lane absoluta + tipo del target en la misma occurrence.
- Donor comparable: occurrence de otro grupo original del mismo chart con query exacta.
- Independencia: se excluye el grupo target completo y cualquier solapamiento de IDs.
- Universo operativo: cada miembro corresponde a una oportunidad `BaseHead` actual antes de chance/density; no implica que pase esos gates.

### G1_INTERIOR_SPATIAL

- Occurrence: parent LN original + anchor exacto + witness LN head de la misma occurrence original.
- Query: identidad temporal G1 exacta existente + lane absoluta del parent. El keymode ya forma parte de la identidad G1.
- Resultado conjunto: relación temporal exacta existente + lane absoluta del witness + delta entero witness-parent + indicador same-lane.
- Donor comparable: occurrence del mismo chart con query exacta, en otro parent y otro evento anchor.
- Independencia: se excluyen occurrence, evento anchor y parent target completos. Un witness usado para construir una propuesta no valida independientemente esa propuesta.
- Universo operativo: subset de occurrences cuyo anchor alcanza `CurrentOpportunity` en la auditoría G1 vigente. No estima commits ni placements.

No se aplican mirror, translation, proportional normalization ni equivalencia de deltas entre keymodes. Los resultados se estratifican por keymode.

## Universos y denominadores

1. **A — estructural original:** todas las occurrences completas de cada familia antes de caps/eligibility legacy.
2. **B — reconstruible en holdout:** una trial por target estructural, clasificada como joint support unique, joint support among alternatives, contradiction, no-context o ambiguity/integrity failure.
3. **C — operativo actual:** base-head opportunities correspondientes para rice y occurrences G1 cuyos anchors pasan el pipeline operativo actual. Se reporta aparte; nunca se usa como denominador de A o B.

## Soporte, alternativas y error

- `JointUnique`: donors comparables independientes existen, todos exponen sólo el resultado target.
- `JointAmongAlternatives`: el resultado target aparece y existen otros resultados completos observados.
- `Contradiction`: hay contexto comparable, pero ningún donor contiene el resultado target.
- `NoContext`: no existe donor comparable después de exclusiones.
- `AmbiguousIntegrity`: identidad incompleta/mal emparejada; invalida el ensayo.
- Soporte marginal temporal o espacial se mide por separado y nunca cuenta como joint reconstruction.
- Cobertura = trials con cualquier contexto / trials B.
- Reconstrucción = trials con joint support / trials B.
- Contradicción y abstención/no-context conservan sus propios denominadores.

## Exclusiones y controles adversariales

El auditor debe ejecutar y contar: target occurrence, target group, parent G1, release endpoint, future-held, same-event, synthetic teaching, cross-chart, donor compuesto con fragments de occurrences distintas, observation ID duplicada e identidad incompleta/mal emparejada. Los fixtures previos al corpus inducen cada contaminación y deben demostrar que se detecta o excluye. Una misma observation con múltiples claims conserva una sola identidad independiente.

## Integridad y no interferencia

- Corpus exclusivo: `.artifacts/f2-1-corpus`, 11 contenidos/12 rutas, manifest canónico `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445` y SHA individual verificado.
- El runner sólo parsea esos paths resueltos; no descubre `Songs`.
- Cero `IRandomSource`, cero generación productiva, cero candidate productiva, cero mutación de chart.
- Fingerprint y serialización semántica de cada resultado deben ser estables; dos evaluaciones puras dentro de la única invocación oficial deben ser byte-identical.
- Los aggregates publicados no contienen texto ni objetos `.osu` de terceros.

## Stop conditions y límites

Se detiene como `INVALID` ante drift de HEAD/implementación/harness/manifest/contrato reutilizado, corpus no exacto, leakage no excluido, bad control no detectado, RNG, mutation, denominador inconsistente u output no determinista. Se detiene como `BLOCKED` si C11 o una identidad congelada no puede reconstruirse.

C11 es desarrollo histórico, no holdout externo, no representa nuevos mappers por seed y no prueba utilidad humana. Un resultado positivo sólo justifica formular una investigación posterior separada; no elige lane, no crea score/probabilidad, no promueve G1 y no autoriza `LANE.DESIGN` o `LANE.GATE`.
