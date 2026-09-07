# Phase D0.2 — Exact Context Coverage and View Agreement / Shadow

**Estado:** COMPLETE — OUTCOME A  
**Schema research:** `phase-d0-2-research.1`  
**Behavior change:** ninguno  
**Corpus:** 11 familias, 11 charts humanos, 4K/7K/10K  
**Población:** 40.360 trials; 36.387 competencias D0 primarias

## Pregunta e hipótesis

D0.2 pregunta si las ocho descripciones exactas D0.1 poseen relaciones observables de cobertura, acuerdo, contradicción y dependencia, y si el acuerdo marginal puede separarse del soporte conjunto demostrado por occurrences originales. La hipótesis se estudia sin score, majority vote, ranking, probabilidad, confidence, `MapperSupport`, similarity, mirror, translation, taxonomía de patrones, ventanas aproximadas ni acceso desde generation.

El resultado es **Outcome A**: la estructura se reconstruye de manera exacta y con provenance completa. No se autoriza sumar vistas ni seleccionar una completion. El hallazgo central es:

> `Marginal support` no implica `ObservedJointContext`.

## Modelo exacto

Todas las vistas contienen `ReducedState`. Los atoms adicionales son:

- `H`: held-before exacto;
- `P`: previous head state exacto;
- `Pg`: previous beat gap exacto;
- `N`: next head state exacto;
- `Ng`: next beat gap exacto.

Las vistas existentes, sin combinaciones nuevas, son `ReducedOnly`, `ReducedHeld`, `ReducedPrevious`, `ReducedPreviousTransition` (`P+Pg`), `ReducedNext`, `ReducedNextTransition` (`N+Ng`), `ReducedPrevNext` (`P+Pg+N+Ng`) y `ReducedHeldPrevNext` (`H+P+Pg+N+Ng`).

El grafo declarado conserva los refinamientos reales:

```text
ReducedOnly
├─ ReducedHeld ───────────────────────┐
├─ ReducedPrevious → PreviousTransition ─┐
└─ ReducedNext     → NextTransition ─────┴→ ReducedPrevNext → ReducedHeldPrevNext
```

`PreviousTransition` y `NextTransition` son parents conjuntivos de `ReducedPrevNext`; `ReducedHeld` y `ReducedPrevNext` son parents de `ReducedHeldPrevNext`. Los pares alcanzables en este grafo se reportan como dependency/nesting. Los demás se denominan cross-dimension, nunca evidencia independiente.

Por target y vista, `ContextViewObservation` guarda comparabilidad, firma exacta, conjunto de completions `{lane, TapHead|LongNoteHead}`, target presente, completion única y grupos donor por completion. `ExactCompletionOccurrence` conserva además IDs de observación y contexto original, de modo que cada afirmación puede rastrearse hasta el chart. `DistinctDonorGroupCountAcrossViews` deduplica un mismo grupo aunque aparezca en varias vistas.

## Cobertura

Se construyeron 322.880 observaciones de vista y 1.130.080 comparaciones pairwise. Hubo **22 firmas exactas de cobertura**:

| Vista | Comparable trials |
|---|---:|
| ReducedOnly | 39.596 |
| ReducedHeld | 35.259 |
| ReducedPrevious | 28.039 |
| ReducedPreviousTransition | 19.199 |
| ReducedNext | 28.163 |
| ReducedNextTransition | 19.523 |
| ReducedPrevNext | 7.372 |
| ReducedHeldPrevNext | 6.880 |

Las firmas más frecuentes fueron `11111111` (6.880), `11111100` (6.549), `11000000` (4.244), `11101000` (3.214) y `11101100` (2.904). `00000000` representa explícitamente 764 trials sin reduced-state donor comparable. La cobertura decreciente no se interpreta como pureza ni authority.

## Agreement, overlap y conflicto

Agregando los 28 pares sobre los mismos targets:

| Relación | Registros |
|---|---:|
| Completion sets iguales | 95.746 |
| Subset u overlap no vacío | 311.037 |
| Disjoint | 17.228 |
| Ambos unique en target | 47.076 |
| Ambos unique en mismo wrong | 6.455 |
| Unique conflict pairwise | 6.508 |

A nivel target, 10.386 trials tienen múltiples vistas unique concordando en el target, 3.256 concuerdan en la misma completion incorrecta y 3.666 contienen conflicto entre completions únicas. De estos conflictos, **2.482** incluyen simultáneamente una selección exacta del target y otra wrong. `ViewConflictRecord` conserva el mapa exacto `ViewId → Completion`; no elige ganador.

## Donor overlap

La comparación se hace por completion y por par, sobre IDs de grupos donor exactos:

| Relación donor | Registros |
|---|---:|
| Identical | 188.174 |
| Left strict subset | 9.195 |
| Right strict subset | 472.372 |
| Partial overlap | 27.409 |
| Disjoint | 45.887 |

Los subsets dominantes en pares nesting confirman que varias vistas que “votan igual” son refinamientos de los mismos donors. Los 27.409 overlaps parciales y 45.887 disjoint demuestran, además, que equality de completions no equivale a equality de evidencia. Los counts son descriptivos; no se normalizan a una probability.

## Agreement marginal frente a contexto conjunto

### PreviousTransition + NextTransition → ReducedPrevNext

| Métrica | Count |
|---|---:|
| Trials con alguna completion en la intersección marginal | 11.495 |
| Completion references en intersección marginal | 14.723 |
| Marginal + joint witness confirmado | 8.245 |
| Marginal pero joint no comparable | 4.972 |
| Marginal pero joint no soporta esa completion | 1.506 |
| Joint unique target | 5.425 |
| Joint unique wrong | 1.109 |

### ReducedHeld + ReducedPrevNext → ReducedHeldPrevNext

| Métrica | Count |
|---|---:|
| Trials con alguna completion en la intersección marginal | 7.096 |
| Completion references en intersección marginal | 7.852 |
| Marginal + joint witness confirmado | 7.603 |
| Marginal pero joint no comparable | 225 |
| Marginal pero joint no soporta esa completion | 24 |
| Joint unique target | 5.281 |
| Joint unique wrong | 889 |

Los confirmations y las ausencias aparecen en las 11 familias. En Previous/Next, por ejemplo, cada familia contiene confirmations conjuntas y también casos marginales sin vista conjunta comparable. Por construcción exacta y nesting verificado, una completion de una vista joint siempre pertenece a ambos marginals; por eso `MarginalsDisagreeButJointSupported` es cero. Esto es una consecuencia auditable del modelo, no un filtro de resultados.

La lectura micro global confirma el volumen anterior. La lectura macro, dando el mismo peso a cada familia, arroja 58,46% de confirmations Previous/Next por completion marginal y 91,52% en Held/PrevNext; el conflicto unique target-level promedia 8,66% por familia. Estas proporciones son descripciones con denominador, no thresholds ni autoridad.

La intersección de completion sets se registra como empty, multi, singleton-target o singleton-wrong. Sigue siendo una operación descriptiva: sólo `ReducedPrevNext` o `ReducedHeldPrevNext` demuestran que atoms y completion coexistieron en un mismo grupo donor.

## Integridad y leakage

- target group usado como donor: **0**;
- target LN reintroducida en future-held: **0**;
- donor LN reintroducida en su surrounding future-held: **0**;
- held tail tratado como head: **0**;
- violaciones del nesting donor prometido: **0**;
- transferencia de evidence entre charts: **0** por fingerprint e índices chart-local.

Las lanes, tipos de head y beats/gaps son exactos. No existe tolerancia ni redondeo para identity. Los ms materializados permanecen provenance descriptiva.

## Estratificación y rendimiento

El archivo de estratos conserva las combinaciones naturales observadas de target type, chord size, reduced member count, held presente/ausente, baseline completion count, número de vistas comparables, número de vistas que resuelven el target y coverage signature. No se aplicaron bins manuales.

La ejecución global midió 3.367,2543 ms construyendo observaciones, 8.963,7353 ms comparando pares y 113,328 ms comparando vistas joint. Se generaron 6.089.294 referencias donor. El detalle target-level, local e ignorado por Git, ocupó 1.832.701.054 bytes; los CSV versionables contienen sólo agregados auditables.

## Pruebas y regresión

Se añadieron 30 casos D0.2 que cubren A–AF mediante theories y fixtures integrados: igualdad/subset/overlap/disjoint/ausencia, unique target/wrong/conflict, relaciones donor identical/subset/partial/disjoint, grafo y nesting, joint contexts, provenance, exclusión whole-group, saneamiento LN, held-tail, identidad Tap/LN, beat gap exacto, cobertura determinista, no cross-chart, permutación de lanes, no RNG, output sin cambio y 1K/4K/7K/10K/18K sin branches por K.

La generación sigue byte-exact en los cinco snapshots históricos; transcript RNG, active weights, lanes y articulación no cambiaron. `BehaviorPolicyVersion`, `EvidenceProfileVersion` y `DecisionDiagnosticVersion` permanecen respectivamente en `legacy-experimental.1`, `phase-a.1` y `phase-c1-2-shadow.1`.

## Gate de 40 condiciones

Las 40 condiciones cierran PASS: determinismo y original-only; exclusión target y cero leakage; identidad exacta de completions, lanes, tipos y timing; grafo/nesting explícitos; provenance y overlap donor; agreement, conflicto y unique-wrong observables; separación marginal/joint en los dos tests; intersección no rebautizada como joint; firmas de cobertura; micro/macro multi-family; implementación multikey; ausencia de weights, probabilities, vote, confidence y `MapperSupport`; generación/RNG/output/weights/lanes/articulation intactos; suite, consistencia documental y root guard verdes.

## Outcome y siguiente fase

**OUTCOME A — EXACT VIEW AGREEMENT AND JOINT-WITNESS STRUCTURE ARE RECONSTRUCTIBLE.**

La representation explica cuándo las vistas concuerdan, qué donors comparten y cuándo existe coocurrencia real. También preserva 3.666 conflictos y miles de acuerdos marginales no demostrados conjuntamente. Nada de esto asigna authority.

La siguiente recomendación evidence-driven es **F1 — Comparable-context Resolver / Shadow + Validation**: definir de manera research-only `ObservedSupport`, `LocalMismatch`, `NoComparableContext` y `AmbiguousEvidence` usando la provenance y las abstenciones ahora explícitas. F1 no recibe permiso para elegir una vista, hacer backoff conductual o modificar generation. D1 sigue **NOT AUTHORIZED** y C2 **DEFERRED**.

## Artefactos

- `d0_2_global_summary.csv`, `d0_2_chart_summary.csv`, `d0_2_family_summary.csv`;
- `d0_2_view_pair_summary.csv`;
- `d0_2_joint_context_summary.csv`;
- `d0_2_coverage_signature_summary.csv`;
- `d0_2_conflict_summary.csv`;
- `d0_2_stratification_summary.csv`;
- detalle target-level local en `.artifacts/d0-2/d0_2_detail.json` (ignorado por Git).
