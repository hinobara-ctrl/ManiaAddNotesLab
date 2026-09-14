# G1.0 Validation Hardening / Recertification Addendum

**Status: RECERTIFICATION PASS. behaviorChange=false.**

Este documento es un addendum post-G1.0. No crea una fase nueva, no modifica el contrato congelado ni reescribe retrospectivamente el pre-run design o el reporte histórico.

## 1. Baseline e identidad

- RepositoryEntryHead: `7b6805a9124a889f8dbeb2d9b9042c181a6aaba3`.
- origin/main al entrar: `7b6805a9124a889f8dbeb2d9b9042c181a6aaba3`.
- Branch: `main`.
- Worktree de entrada: limpio salvo `docs/ASTRA_POST_SAFETY_PROV_STRATEGIC_REVIEW.md` y `docs/ASTRA_ROADMAP_V2_PROPOSAL.md`, documentos no versionados ya conocidos y preservados.
- Baseline: restore PASS con el warning conocido NU1900 del feed de vulnerabilidades, Release build PASS, 672/672 tests, DocConsistency PASS y `git diff --check` PASS.
- Default conductual: `legacy-experimental.1`.
- Contract canonical SHA-256: `7C04E4CD9B45EE9351FBDC3C179083AAE43A5906115815A9F7987E0C51412194`.
- Contract file-byte SHA-256: `C7D79283A190026DC4D2A4A7A73049E1D8732C4CC243CF9FEBFF51771540CAF3`.
- C11 fingerprint: `878585D604E807B7E46AB429990C7DA6DB396AB0B7E930DAC2B631C8DE7ECD77`.

El outcome histórico vinculante al entrar fue **G1.0 COMPLETE — OUTCOME A / SHADOW**. El contrato, el design congelado y el reporte histórico no fueron modificados.

## 2. Motivo y hallazgos de la auditoría

La representación de occurrences, la separación same-occurrence frente a marginales, las clases de relación, la construcción chart-local/prior-only, el orden determinista y el mirror del gate actual sí estaban conectados a datos reales.

La certificación de leakage no lo estaba: `BuildHoldouts` llenaba `TargetLeakageCount`, `ParentLeakageCount`, `ReleaseLeakageCount`, `FutureLeakageCount`, `SameEventLeakageCount`, `SyntheticLeakageCount` y `CrossChartLeakageCount` con ceros literales. `Outcome()` sumaba esos campos, por lo que “measured leakage = 0” era tautológico aunque la construcción del donor set contuviera una regresión.

También eran insuficientes estos controles del synthetic gate:

- `target_self_leakage_rejected`, `release_leakage_rejected` y `cross_chart_rejected` sólo observaban contadores preinicializados o una construcción normal que nunca introducía la condición mala;
- `anchor_support_attrition` aceptaba cualquier resultado no vacío mediante `Length > 0`;
- `bad_random_id_control_rejected` duplicaba el control positivo de determinismo y nunca manipulaba un ID;
- `bad_frequency_authority_absent` sólo comprobaba que el valor fuera un enum válido;
- el control de synthetic original-only demostraba prevención durante construcción, no detección de contaminación ya aceptada.

No se encontraron otros contadores G1.0 con nombres de leakage/violation/rejected conectados al Outcome A que requirieran corrección fuera de este conjunto.

## 3. Auditor independiente

`InteriorRelationHoldoutAuditor` recibe target, tarea y donor set ya aceptado. Es puro, determinista, research-only y no usa RNG. No construye ni filtra el donor set. Recorre el resultado de construction y calcula violations independientemente mediante las identidades congeladas:

- target witness como donor;
- misma parent occurrence sólo en `ParentOccurrence`;
- parent del target usada como witness/release donor;
- donor no estrictamente anterior;
- evento target usado como parent donor;
- provenance synthetic explícita;
- chart fingerprint diferente.

Los IDs de observación se comparan sólo después de comprobar chart identity, porque son chart-local. `Assess` mantiene separadas la relación joint completa, la duración marginal y endpoint/release marginal; su resultado sólo describe `NoObservedRelation`, `ObservedUnique`, `ObservedAmongAlternatives` o `ConflictingForSpecificClaim` y no contiene winner/selection authority. Un validador aparte recomputa el semantic occurrence ID canónico sin RNG.

## 4. Controles adversariales y positivos

El synthetic gate endurecido cerró **31/31**. En particular, cada control siguiente introdujo deliberadamente una condición prohibida y obtuvo un contador mayor que cero:

| Control | Violación inyectada | Detección |
|---|---|---|
| Target | target occurrence insertada como donor | `TargetLeakageCount > 0` |
| Parent | donor con la parent del target en `ParentOccurrence` | `ParentLeakageCount > 0` |
| Release | donor witness igual a la parent/release identity del target | `ReleaseLeakageCount > 0` |
| Future | donor con `AnchorTime >= target.AnchorTime` | `FutureLeakageCount > 0` |
| Same event | donor parent igual al target witness event | `SameEventLeakageCount > 0` |
| Synthetic | donor marcado explícitamente synthetic | `SyntheticLeakageCount > 0` |
| Cross-chart | fingerprint diferente | `CrossChartLeakageCount > 0` |
| Semantic ID | ID reemplazado por 64 ceros | canonical mismatch detectado |
| Frequency authority | ocho occurrences A frente a una B | sigue `ObservedAmongAlternatives`, sin winner |
| Marginal as joint | duración y endpoint en donors distintos | marginal-only true, exact joint false |

El control positivo separado conserva un donor set válido con cero violations. Dos evaluaciones idénticas conservan semantic IDs, orden de relations/gates/holdouts y agregados. El control aislado de anchor support contiene una relación estructural real, pasa source length, source context y el gate legacy de longitud, falla exclusivamente `AnchorSupport`, queda clasificado con ese first-exclusion gate y no llega a current opportunity.

## 5. Recertificación C11

Se ejecutó el mismo inventory C11 de 11 charts, 11 familias, 50.836 objetos, 12.554 LNs y keymodes 4K/7K/10K. No se añadieron mapas, seeds, community corpus ni thresholds. El snapshot final de implementación de la recertificación fue `8906BCD2602329281B83D040308A345A9E45C03853BAF4067953761AACEC988C`.

Los CSV recalculados de census, families, anchor kinds, marginal-vs-joint, alternatives, holdouts y legacy gate attrition fueron **byte-identical** a sus baselines históricos.

| Aggregate | Histórico | Recertificación |
|---|---:|---:|
| Original LNs | 12.554 | 12.554 |
| Structural anchors | 22.162 | 22.162 |
| Complete relations | 16.881 | 16.881 |
| Contained | 5.617 | 5.617 |
| EqualEnd | 3.093 | 3.093 |
| Crossing | 8.171 | 8.171 |
| Current opportunities | 298 | 298 |
| Relations at current opportunities | 288 | 288 |
| Source-length first exclusions | 20.076 anchors / 15.726 relations | 20.076 / 15.726 |
| Anchor-support first exclusions | 8 anchors / 3 relations | 8 / 3 |
| Cap=2 first exclusions | 1.595 anchors / 862 relations | 1.595 / 862 |
| Holdout queries | 33.762 | 33.762 |
| Comparable | 22.332 | 22.332 |
| Exact joint supported | 14.584 | 14.584 |
| Marginal-only | 284 | 284 |
| Unique | 1.138 | 1.138 |
| Among alternatives | 13.446 | 13.446 |
| Conflicting exact claim | 7.748 | 7.748 |

## 6. Leakage real aceptado, determinismo y neutralidad

Sumando ambas tareas, C11 produjo: target 0, parent 0, release 0, future 0, same-event 0, synthetic 0 y cross-chart 0. Ahora esos ceros provienen del auditor posterior al accepted set y no de constantes.

El semantic occurrence hash permaneció `51FB7647B8BF0931F433BCC4CECD7D49ABE8858C46CE17E70AFFA46656A77510`; ordering repeat fue idéntico, mirror mismatches 0 y `RngCalls = 0`. Los bad controls tampoco usan Random, `Guid.NewGuid()` ni randomized property testing.

La neutralidad conductual es estructural: sólo cambian tipos research G1.0, su runner, tests y documentación. No se añadieron hooks al engine ni callsites CLI/Web normales. Por ello se conservan bytes, AddedObjects, ArticulationReplacements, transcript RNG, opportunities, candidate ordering, decisions, geometry y serialization de generación normal.

## 7. Correcciones documentales

- `ArticulationMaxNonHeldColumns=1` pasa de `INTENSITY_OR_CAPACITY_CANDIDATE` a `LEGACY_UNRESOLVED`: es un boundary G2 separado y G1.0 no lo estudió.
- `MaxInteriorOpportunitiesPerSource=2` conserva `INTENSITY_OR_CAPACITY_CANDIDATE`, apoyado por el attrition G1.0 ya observado, sin recomendar un valor.
- Behavior Decision Audit ahora se presenta como living inventory y conserva su historia.
- `ROADMAP REVIEW REQUIRED` se conserva como requisito histórico de cierre SAFETY.PROV. La revisión estratégica/humana posterior que autorizó y ejecutó G1.0 lo satisfizo como blocker actual; el bloqueo vigente es la revisión humana separada para `G1.DESIGN`.

## 8. Resultado y fronteras

**RECERTIFICATION PASS.** G1.0 conserva **COMPLETE — OUTCOME A / SHADOW** y queda **RECERTIFIED AFTER VALIDATION HARDENING**. No se crea un Outcome nuevo.

`G1.DESIGN` sigue siendo sólo el próximo candidato y permanece **NOT_AUTHORIZED** hasta revisión humana. G1 behavioral, G2 y H permanecen **NOT_AUTHORIZED**. D1 y D1.SAFETY conservan COMPLETE/C/PARKED; SAFETY.PROV conserva COMPLETE/A/SHADOW; F2 sigue CONTINUE_CONDITIONALLY, F2.ACQ BLOCKED ON EXTERNAL DATA, C2 DEFERRED y MapperSupport NOT_AUTHORIZED.

## 9. Validación y Git

Validación final: restore PASS con el warning NU1900 conocido, Release build PASS, **684/684 tests**, DocConsistency PASS y `git diff --check` PASS. `.artifacts/g1_0_validation/` está ignorado y no contiene archivos versionados.

El worktree queda preparado para revisión humana con los cambios del hardening y los dos documentos Astra preexistentes preservados. Staging vacío. **No git add, commit, push, tag ni release.**
