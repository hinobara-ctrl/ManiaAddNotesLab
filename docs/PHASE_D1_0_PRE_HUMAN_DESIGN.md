# Phase D1.0 — Pre-Human Design Certificate

Estado: **FROZEN BEFORE HUMAN RESULTS**.  
Baseline autorizado: `30d430397f6f36de88ca18b864f2bb59c9e18eb1`.  
Schema research: `phase-d1-0-resulting-state-composition-shadow.1`.  
Contrato: research-only, shadow-only, diagnostic-only, `behaviorChange=false`.

## Pregunta

¿Puede el chart original distinguir de forma exacta y auditable entre completion members soportados marginalmente y un CompletionSet observado conjuntamente en una misma occurrence original comparable? D1.0 valida representación y abstención; nunca decide qué candidate aceptar.

## Principios congelados

`observed != inferred`; `marginal != joint`; `agreement != independence`; `support != authority`; `frequency != validity`; `coverage != correctness`; `ambiguity != no evidence`; `research != behavior`; `individually supported != jointly supported`; `member support != composition support`; `candidate order != composition identity`; `resulting-state validation != selection`; `joint witness != intersection of marginal witnesses`.

## Poblaciones

- Control `k=1`: reproducción D0 `ReducedState → CompletionMember` con whole-group holdout.
- Primaria `k=2`: para cada chord group original con al menos dos heads se enumeran exhaustivamente todos los pares unordered retirados. El target es el par real y el reduced state puede quedar vacío.
- `k≥3`: **NOT RUN / DEFERRED**. Su enumeración crece combinatoriamente, no es necesaria para el mínimo caso no trivial y ampliaría artifacts sin responder mejor la pregunta primaria. No se usa sampling, top-N ni cap estilístico.

## Identidades

`CompletionMember = lane + TapHead|LongNoteHead`. Held tails y releases no son members. `CompletionSet` es exacto, duplicate-free, canónico y order-independent. `ResultingState = ReducedState ∪ CompletionSet` conserva lanes y tipos exactos; A+B y B+A deben producir identidad y hashes iguales.

## Hard invalidity

Antes de consultar evidencia se clasifican: duplicate member, lane fuera de KeyCount, member ya presente y dos heads en la misma lane. `HardInvalid` describe imposibilidad estructural y no se denomina “mapper unsupported”.

## Joint witness y holdout

Un CompletionSet sólo está observado si todos sus members proceden de una misma original simultaneous event-group occurrence bajo el mismo reduced/context state. Donors marginales diferentes nunca se intersectan para fabricar joint evidence. Se excluye todo el target group, no sólo los dos members retirados. Target y donor reutilizan el saneamiento held-state D0.1 que retira el grupo completo del contexto held futuro.

Cada occurrence conserva chart fingerprint, group ID, reduced state, CompletionSet, full resulting state, held-before, IDs visibles/retirados y contexto exacto. Hypothetical states nunca se convierten en donors.

## Taxonomías

Target pair-holdout:

- `ObservedJointUnique`: donors comparables y sólo el CompletionSet target.
- `ObservedJointAmongAlternatives`: target observado junto a otros sets exactos.
- `TargetAbsentFromJointEvidence`: hay donors comparables pero no reconstruyen el set target; no implica que el target original sea incorrecto.
- `NoComparableCompositionContext`: abstención por ausencia de donor válido.

Hypothetical marginal pairs:

- `JointObserved`;
- `MarginalOnly` — ambos members marginales, set structurally valid, ninguna occurrence conjunta;
- `HardInvalid`;
- `NotMarginallySupported`.

No se colapsan ambiguity, mismatch y ausencia.

## Context views

Se evalúan independientemente las ocho vistas históricas: `ReducedOnly`, `ReducedHeld`, `ReducedPrevious`, `ReducedPreviousTransition`, `ReducedNext`, `ReducedNextTransition`, `ReducedPrevNext` y `ReducedHeldPrevNext`. No forman ladder; no existe winner, backoff ni búsqueda de un “sí”. Previous+Next marginal no sustituye una occurrence `ReducedPrevNext`.

## Métricas congeladas

Target: eligible, comparable, joint unique, joint among alternatives, absent y no-context. Marginal/joint: ambos target members marginales, target set joint y target marginal-only. Hypothetical: candidate pairs, structurally eligible, hard invalid, joint observed y marginal-only. Se publican por vista, chart, family, keymode, reduced-head count, held presence y Tap+Tap/Tap+LN/LN+LN. Micro y macro family permanecen separados; no hay score global.

Audits: target whole-group leakage, target-observation leakage, future-held leakage, synthetic teaching, cross-chart evidence, held-tail-as-head, joint-witness cross-occurrence leakage, order invariance, operaciones de enumeración y determinismo.

## Corpus humano congelado

Si el gate sintético pasa, se usa exclusivamente el snapshot C11 existente: 11 charts humanos deduplicados, 11 families, 4K/7K/10K y 50.836 objetos originales. Se excluyen `[ADD …]`, samples, Web outputs, archivos nuevos y duplicados SHA-256. 1K/18K son implementation fixtures, no evidencia humana.

## Gate sintético

Debe pasar antes del humano: control D0 `k=1`; joint same-occurrence; marginal-only; bad cross-donor fabrication detectable; whole-group holdout; future-held hygiene; orden canónico; hard invalidity; Tap/LN/release/held semantics; empty/non-empty reduced states; rice denso; 1K/4K/7K/10K/18K; deterministic rerun/permutation; original-only; legacy output/RNG exactos. El positive control deliberado debe producir leakage `>0`; el modelo correcto, cero. Si falla, `HUMAN = NOT RUN`.

## Outcomes predefinidos

### Outcome A

La representación joint es exacta/auditable; existe soporte target no trivial recurrente en múltiples charts/families; la discrepancia marginal-only es material y reproducible; se detecta sin frecuencia/score; leakage correcto es cero; orden/provenance son correctos; y queda una pregunta conductual futura falsable. No autoriza D1.

### Outcome B

La representación es válida, pero coverage limitada, alternatives/abstención o heterogeneidad impiden diseñar resulting-state behavior defendible. Puede recomendar research más estrecho o PARK.

### Outcome C

La evidencia no permite evaluar útilmente exact joint composition en este corpus/representación. No abrir D1; PARK y reevaluar roadmap.

Outcome A no exige coverage “bonita”: detectar muchas composiciones `MarginalOnly` puede ser el resultado importante.

## Interpretaciones prohibidas

No frequency selection, majority, confidence, probability, score, tolerance, fuzzy similarity, quantization, rank, top-N, random sampling, pattern/hand classifier, sequential policy, accumulated synthetic teaching, candidate rejection, production toggle, nueva `BehaviorPolicyVersion` ni cambio de generation. `MarginalOnly` significa únicamente “no observado conjuntamente en la evidence comparable”. Aunque el outcome sea A, D1 — ChordCompletion Resulting-State A/B sigue `NOT_AUTHORIZED`.
