# G1.DESIGN — Interior Relation Admission Contract / Shadow

## 1. Status

**COMPLETE — READY.** G1.DESIGN cierra como diseño e investigación shadow. `G1.GATE` queda como candidato siguiente y continúa `NOT_AUTHORIZED`.

## 2. Behavior change

`behaviorChange=false`. No se modificó ningún mapa generado, default, selector, eligibility, articulation ni ruta conductual.

## 3. Entry HEAD

`RepositoryEntryHead = c55489392c121cce64ff270133dd00fb68fa1c4b`, idéntico a `origin/main` al entrar. La policy activa era y sigue siendo `legacy-experimental.1`; el baseline tenía 684 tests aprobados.

## 4. G1.0 dependency

El diseño reutiliza directamente `InteriorRelationOccurrence`, identidades canónicas de query/result, IDs de parent/witness, anchors, clases de relación y fingerprint del chart definidos por G1.0. No existe una segunda representación de relaciones LN.

## 5. Recertification dependency

La dependencia histórica es G1.0 `COMPLETE — OUTCOME A / SHADOW`, **RECERTIFIED AFTER VALIDATION HARDENING** con `RECERTIFICATION PASS`. Sus holdouts validaron representación; no se reinterpretaron como policy de producto.

## 6. Problem statement

La pregunta es si una propuesta LN interior concreta, ya construida legalmente por legacy, puede clasificarse exactamente como miembro o no miembro del vocabulario original del chart sin escoger otra propuesta.

## 7. Claim being evaluated

> Esta relación exacta pertenece al vocabulario original observado de este chart para la query G1.0 exacta.

Es un claim de pertenencia, no de universalidad, preferencia, frecuencia, calidad ni utilidad.

## 8. Why membership is not selection

El pipeline termina en `identity → membership`. Compatibility, ranking y selection quedan fuera. Si `S={A,B,C}` y legacy propone `B`, entonces `B∈S` aunque A sea más frecuente. No se elige A, no se rechaza por ambiguity y no se busca C.

## 9. Query identity

La query congelada es `(keymode, exact parent duration, exact anchor offset from parent start, AnchorKind)`. Usa arithmetic exacta materializada; no cambia para elevar coverage.

## 10. Result identity

El resultado congelado es `(InteriorEndRelation, exact duration from anchor, exact offset from parent end)`, con clases `Contained`, `EqualEnd` y `Crossing`.

## 11. Full-chart evidence scope

El futuro claim de producto es chart-local, full-chart y `OriginalObjects`-only. Puede usar originales posteriores al candidate porque la conversión es offline. Cross-chart, synthetic, community priors y outputs generados nunca aportan authority.

## 12. Same-parent semantics

La misma parent puede demostrar uso original dentro de ese contexto. La provenance queda descriptiva como `SameParentOnly`, `OtherParentOnly`, `Both` o `None`; no es score ni modifica la decisión.

## 13. Alternative semantics

Un candidate exacto dentro de un result set plural es `CandidateObservedAmongAlternatives` y sigue siendo miembro. `ConflictingForSpecificClaim` no es veto universal para el claim de membership. C11 contiene 87 queries exactas con un observed result y 66 con múltiples; dentro de los result sets múltiples hubo 57 candidate members y 1.422 candidate shapes ausentes.

## 14. Admission states

Los estados son `CandidateObservedUnique`, `CandidateObservedAmongAlternatives`, `CandidateNotObserved`, `NoObservedRelation` y `UnresolvableExactIdentity`.

## 15. Hypothetical ADMIT/ABSTAIN mapping

Unique y AmongAlternatives producen `ADMIT` hipotético. NotObserved, NoObservedRelation y Unresolvable producen `ABSTAIN` hipotético. En G1.DESIGN estas salidas no gobiernan generation.

## 16. Exact candidate mapping

Cada shape enumerada por el builder real se convierte en una query y un result identity canónicos de G1.0. La prueba es igualdad exacta de joint identity; fragmentos marginales no se unen, y no hay tolerance, nearest, snap equivalence ni inferencia F2 latente.

## 17. Future insertion point

El punto aislable está dentro de la opportunity `LnInterior`: después de obtener un `placed` válido mediante el path legacy y antes de `added.Add`/`geometry.Insert`. Chance, candidate shape, lane, placement y geometry ya habrán consumido exactamente el mismo path que control.

## 18. RNG semantics

El evaluator consume cero RNG. Inmediatamente antes y después del gate, control y treatment deben conservar la misma posición RNG; cualquier divergencia posterior sólo puede descender causalmente de permitir u omitir la mutación.

## 19. No-reroll semantics

Un `ABSTAIN` termina esa opportunity. No hay reroll de shape/lane, fallback, candidate substitution, relation-class change ni recorrido para “encontrar un sí”.

## 20. Articulation isolation

G1 rejection no crea `ArticulationIntent` ni activa G2. Un eventual experimento G1 debe ejecutar con articulation OFF en ambos brazos.

## 21. Eligibility isolation

Se congelaron los valores actuales: source mínimo 3 beats, context LN mínimo 3, anchors soportados mínimo 2, ventana LN 4 beats y cap 2, además de ranking y `AnchorSupported`. G1.DESIGN no revisa sus owners ni perfiles de intensidad.

## 22. Safety/provenance boundary

Legacy geometry debe aceptar primero el placement; membership no implica safety. SAFETY.PROV puede observar proposal, membership decision, mutation allowed/skipped, direct divergence, downstream ancestry y reconvergence sin ampliar genéricamente su infraestructura.

## 23. C11 shadow methodology

Se usaron los 11 charts/11 familias del corpus de desarrollo C11, keymodes 4/7/10. Se enumeraron, sin weighted RNG, todas las release shapes que el builder real puede producir para las opportunities actuales. C11 es discovery, no validación independiente ni evidencia de generalización a osu!mania.

## 24. Results

Se encontraron 298 opportunities actuales y 4.226 candidate shapes. Las 4.226 (100% con denominador 4.226) fueron exactamente representables; 0 quedaron `UnresolvableExactIdentity`. Estados: 76 Unique, 57 AmongAlternatives, 2.907 NotObserved y 1.186 NoObservedRelation.

## 25. Per-family distribution

Seis familias mostraron direct-effect potential: Hakanaki Mono Ningen 20 admits, Kara Kara Kara no Kara 31, Spring of Dreams 50, Celestial Axes 6, Ko Inu 22 y Mikimiki Romantic Night 4. Cinco familias no tenían opportunity actual o no produjeron admit; Destiny produjo 98 shapes y 0 admits. Véase `g1_design_membership_by_family.csv` para todos los denominadores y keymodes.

## 26. Same-parent vs other-parent provenance

Entre los 133 admits: 91 `SameParentOnly` (68,4211%), 15 `OtherParentOnly` (11,2782%) y 27 `Both` (20,3008%); `None=0`. El denominador de cada porcentaje es 133. Estos valores describen de dónde proviene el soporte, no cuánto vale.

## 27. Relation-class distribution

El universo representable contiene 1.877 `Contained`, 128 `EqualEnd` y 2.221 `Crossing` (denominador 4.226). No se introdujo preferencia de clase.

## 28. Direct-effect potential

El mapping shadow produciría 133 `ADMIT` y 4.093 `ABSTAIN` sobre 4.226 shapes posibles. Esto mide potencial de intervención, no outputs por seed, utilidad humana ni recomendación de promoción.

## 29. What DESIGN proves

Prueba que la identity G1.0 puede aplicarse exactamente al universo actual, que un evaluator puro puede decidir pertenencia—including alternatives—sin RNG ni selector, y que existe un insertion point aislable con potencial directo en más de una familia.

## 30. What DESIGN does not prove

No prueba que agregar una nota mejore un chart, que el vocabulario observado sea una policy óptima, que C11 generalice, que una lane sea preferible ni que reconstruction equivalga a augmentation. Tampoco resuelve eligibility, intensity, AddChance, G2, H o MapperSupport.

## 31. Readiness decision

**READY.** Se satisfacen exact identity, pure membership, zero RNG, no selector/frequency/fuzzy authority, alternatives-as-membership, insertion isolation, no articulation fallback, comportamiento neutral y efecto potencial en seis familias.

## 32. Future G1.GATE status

`G1.GATE — Interior Relation Admission Behavioral Gate / Shadow` queda **NEXT CANDIDATE / NOT_AUTHORIZED**. Este cierre no implementa toggle, callsite productivo, A/B ni runner conductual.

## 33. G2/H status

G2 articulation y H multiple articulation continúan **NOT_AUTHORIZED**. D1 y D1.SAFETY conservan `COMPLETE — OUTCOME C / PARKED`.

## 34. Limitations

Los resultados pertenecen a C11, un corpus de desarrollo. Full-chart y same-parent responden al claim explícito de vocabulario local, pero requerirán revisión humana antes de autorizar el gate. Las opportunities siguen fuertemente condicionadas por eligibility legacy congelada.

## 35. Tests

La matriz determinista cubre los 20 casos pedidos y bad controls adversariales: evidence synthetic/cross-chart, frequency winner, fake marginal join, fuzzy match, replacement, articulation routing y RNG. El cierre final registra 698 passed, 0 failed, 0 skipped.

## 36. Git status

El trabajo queda sin staging, preservando los dos documentos Astra previamente no trackeados. El detalle candidate-level se encuentra bajo `.artifacts/g1_design/` y permanece ignorado.

## 37. No commit/push

No se ejecutó `git add`, commit, push, tag ni release. El worktree queda listo para revisión humana.

## Frozen contract

Contrato: `docs/g1_design_interior_relation_admission_contract.json`  
Canonical SHA-256: `15A16B6EFBF779CFF2C42A8C9A0DD46E025019252BEFA968E831233DC802AA68`
