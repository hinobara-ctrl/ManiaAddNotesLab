# Architecture

Este documento resume la arquitectura estable. Los detalles de fórmulas y opciones viven en [DESIGN.md](docs/DESIGN.md).

## Flujo conceptual

```text
OriginalObjects
    ↓
Evidence Layer
    ↓
observations → witness identity → observed claims → observed relations → certificates

Generation Layer
    ↓
opportunities → candidates → geometry → selection → output
```

El destino mapper-derived es que las estructuras recurrentes futuras nazcan de valores y relaciones demostrados por el mapa, sin una taxonomía externa de jack/trill/stream. El estado actual todavía conserva selección y heurísticas legacy; la Evidence Layer nueva opera en shadow.

### Adaptive context research

Phase E añade `AdaptiveContextResearch` como capa totalmente separada de generation. Construye una serie temporal exacta desde grupos originales, indexa recurrence no contigua y propone boundaries contiguas explícitamente inferidas. Recurrence y segmentation poseen resultados, métodos y parámetros distintos; ninguna region tiene autoridad de Section.

```text
OriginalObjects → OriginalTemporalEventSeries
                     ├─ exact recurrence relations
                     └─ inferred stable-run boundaries/regions
                              ↓
                    held-out diagnostics only
```

El schema `phase-e-adaptive-context-shadow.1` conserva IDs de chart, método, parámetros, target exclusions y donors. AddedObjects, RNG, F2 scopes y selección nunca entran en esta capa.

E.1 añade `ExactRecurrenceFailureResearch` sobre esa serie, sin alterar `exact-neighbor-recurrence.1`. Conserva candidatos pre-holdout, exclusions, donors válidos, multiplicidad semántica, flags observables y counterfactuals exactos bajo `phase-e-1-exact-recurrence-failure-shadow.1`. Los counterfactuals sólo filtran donors ya saneados y distinguen support de coverage destruction; no forman un resolver alternativo.

### Resulting-state phase contracts

D1.0 y D1 son contratos distintos. **D1.0 — Resulting-State Composition Feasibility / Shadow** es investigación diagnóstica sin conducta: debe comprobar si múltiples completion members poseen un mismo joint witness original, porque soporte individual no implica soporte de composición. **D1 — ChordCompletion Resulting-State A/B** conserva el contrato histórico conductual, versionado y con rollback legacy. D1.0 es un prerequisite de factibilidad; aun si concluye favorablemente, no autoriza D1 ni conecta su evidencia a generation.

```text
member support (marginal) != completion-set support (joint occurrence)
research resulting-state validation != selection
```

## Fronteras principales

### OriginalEvidence y CurrentGeometry

`OriginalEvidence` es una vista inmutable construida sólo desde `OriginalObjects` y timing original. Contiene identidades, observaciones y relaciones que describen el chart. No depende de seed, AddChance, rango ni objetos añadidos.

`CurrentGeometry` representa lo que físicamente existe durante una run: originales, sintéticos aceptados y sustituciones. Se consulta para colisiones, spacing y lanes legales. Mirar este estado no significa aprender estilo de los sintéticos; sólo comprueba si el siguiente cambio cabe en el resultado acumulado.

```text
OriginalObjects ──→ OriginalEvidence ──→ style observations
       │
       └──────────→ CurrentGeometry ←── accepted synthetic objects
                              │
                              └──→ hard-valid placement or rejection
```

### HardValidity y StyleEvidence

`HardValidity` responde si una transformación puede existir: lane válida, duración positiva, tiempos serializables, no-overlap y spacing requerido. Es un gate binario e inderrotable.

`StyleEvidence` responde qué valores o relaciones originales respaldan una transformación. Un certificado fuerte nunca puede compensar una colisión. En una policy futura estricta, una transformación legal pero sin evidencia podrá terminar en `SKIP`.

### WITNESS IDENTITY ≠ RELATION COUNT

`ObservationId` deduplica la identidad de un objeto original; no borra las afirmaciones ni las relaciones distintas que ese objeto demuestra. Un witness puede respaldar varias relaciones sin convertirse por ello en varias muestras independientes.

```text
OriginalObservation O17
    independent witness: O17
    observed values: Duration D, ExactRelease R
    observed relations: ExactHead(H), HeadToRelease(H, R)
```

O17 cuenta una sola vez en `IndependentWitnessCount`. Sus claims `Duration` y `ExactRelease`, y su relación de head exacto con la query/source, conservan provenance separada. Ninguna cantidad de relaciones se interpreta aquí como weight, bonus, confidence o `MapperSupport`.

### Exact chord-completion research

D0 reutiliza la primitive C1.2 para incluir todo grupo simultáneo original, también tap-only. Un `OriginalHeadState` separa lanes `TapHead`, lanes `LongNoteHead` y member IDs. `HeldBeforeHeadLanes` es contexto independiente y sólo incluye LNs iniciadas estrictamente antes del head actual.

```text
SimultaneousOriginalEventGroup
    ↓ exact members
OriginalHeadState - one member
    ↓ ObservedRelation: ChordCompletion
CompletionMember(exact lane, exact TapHead | LongNoteHead)
```

La relation key omite timestamp absoluto para reconocer recurrencia dentro del mismo chart, pero cada witness conserva source group, timestamp/beat y full/reduced/completion observation IDs. Whole-group holdout impide que members del target se donen evidencia entre sí. No hay mirror, translation, similarity, pattern taxonomy ni transferencia cross-chart.

### Exact completion context research

D0.1 envuelve cada occurrence D0 en `ExactCompletionContext`: held-before estricto, previous/next original inmediato y gaps de transición exactos. Sus ocho vistas son índices independientes, no un orden de backoff. La identidad conserva `decimal` exacto; los ms son provenance descriptiva.

```text
ReducedState + ContextView
    → exact same-chart donor occurrences
    → completion set (lane + TapHead/LNHead)
    → descriptive outcome
```

Antes de construir el contexto held del siguiente grupo, se retiran todos los IDs del grupo actual. Esta operación se aplica simétricamente a targets y donors, de modo que una LN completion no pueda reintroducirse como evidencia contextual futura. El resultado conserva witness groups y enumera alternatives, pero no elige una completion ni una vista.

### Exact view agreement and observed joint context

D0.2 añade una capa research separada que nunca entra a generation. `ContextViewObservation` mantiene tres identidades independientes: vista, completion `{lane, head type}` y grupos donor. `ViewPairAgreement` compara conjuntos exactos y overlap donor; `ViewConflictRecord` preserva contradicciones sin resolverlas.

```text
marginal view A ─┐
                  ├─ completion-set intersection (descriptiva)
marginal view B ─┘

original donor matching A+B+completion
                  └─ ObservedJointContext (evidencia conjunta)
```

`ReducedPrevNext` verifica la conjunción real de previous/next transitions y `ReducedHeldPrevNext` verifica held + contexto bidireccional. La intersección marginal no crea un witness. El dependency graph identifica parents/children para que dos refinamientos del mismo donor no se cuenten como fuentes independientes. Coverage y ausencia permanecen explícitas.

### Comparable-context resolver

F1 añade otra capa research separada. Para cada completion candidate —target o alternativa observada— traduce las siete vistas contextuales D0.2 a disposiciones `Supporting`, `Contradicting` y `Abstaining`, y de ellas deriva exactamente un estado combinado: `ObservedSupport`, `LocalMismatch`, `NoComparableContext` o `AmbiguousEvidence`.

```text
candidate + exact contextual views
    → view-local support / contradiction / abstention
    → marginal-vs-joint assessments + unresolved conflicts
    → one descriptive evidence state
```

Los flags view-local, occurrences, donor IDs y dependency edges sobreviven al estado combinado. Dependency describe nesting y no multiplica evidencia; donors disjoint no prueban independencia. `ComparableContextResolverResearch` no es consumido por generation, no acepta RNG y no ordena ni elige candidates.

### Typed-gap shadow y backoff conservador

F2 modela transitions same-lane originales mediante endpoints exactos `TapHead`, `LongNoteHead` y `LongNoteRelease`. Una LN previa aporta release→next-head; un tail nunca se convierte en head. `TypedGapBackoffResearch` usa gap `decimal` exacto y provenance de ambos objetos.

```text
candidate transition kind + exact gap
    → LocalLane evidence
    → GlobalChart sólo si LocalLane = NoComparableContext
    → descriptive ADMIT o SKIP
```

Esta capa cerró Outcome B y permanece shadow-only. No existe integración con geometry/generation, versión conductual F2, selector de gaps o scope Section. Las occurrences se normalizan una vez y las resoluciones conservan donor keys para evitar duplicación cuadrática.

### Exact gap timing identity research

F2.1 conserva `FileExactDecimal` y añade `ExactSegmentTraversal`: una secuencia exacta de beat length y beat span por cada redline atravesada. También distingue `HeadEventGroup`, `ReleaseEventGroup` y `TransitionEndpointGroup`, de modo que una LN con release compartido no pueda reaparecer como donor del mismo endpoint.

La traversal es provenance, no quantizer. El ground truth demuestra que no puede recuperar una relación nominal que perdió coordenadas sub-ms al serializarse, y que incluso puede separar una redline redundante. F2.1 cerró Outcome C; no hay integración con generation, tolerancia, denominator vocabulary ni behavior.

El hardening pre-F2.2 elevó el schema de código F2.1 a `phase-f2-1-exact-gap-timing-shadow.2`: `TargetEndpointLeakageCount` deriva ahora de violations medidas con provenance. El report histórico `.1` no se reescribe.

### Quantization inference feasibility research

F2.2 añade una capa separada que modela únicamente el forward path:

```text
explicit latent domain + positive-redline timing map
    → integer-ms serialization away from zero
    → exact compatibility set for an observed timestamp
    → unsupported / unique-under-model / ambiguous-under-model
```

`QuantizationHypothesisDomain` no tiene vocabulario default: el caller debe declarar ID, source, justification y candidates. Cada salida conserva un assumption certificate con domain, circularity, serializer version y timing-map hash. Distintas posiciones latentes pueden colisionar en un mismo timestamp; el modelo preserva todas y nunca elige una. No se referencia desde generation, CLI o Web productivos.

F2.3 endurece el domain: `ContentHash` canoniza coordinates, aliases no crean hypotheses duplicadas y justification es obligatoria. `QuantizationDomainGroundTruthResearch` separa domain source/circularity de truth source/level/independence y representa una transición completa con ambos endpoints, gap, timing map y LN head/release separados.

```text
domain descriptor ───────────────┐
                                 ├─ descriptive validation result
labeled source→destination truth ┘
          + forward serializer
```

El pilot es programmatic synthetic y está marcado `DomainConstructedFromTruth`; prueba ramas del evaluator, no mapper intent. La arquitectura no contiene acquisition automática ni soporte de formatos externos. `F2.ACQ` sólo describe el prerequisite futuro de obtener packages pre-export independientes.

## Evidencia y generación actuales

`MapperEvidenceProfile` (`phase-a.1`) congela observations, chords, duraciones/releases LN, transiciones, retriggers, anchors, provenance y fingerprint. C1.2 añade relaciones exact-head; D0 chord completion; D0.1 matching exacto; D0.2 agreement/joint witness; F1 estados candidate-centric como `phase-f1-research.1`; F2 typed gaps/backoff como `phase-f2-typed-gap-shadow.1`; F2.1 timing provenance como `.2`; F2.2 compatibility hardened como `phase-f2-2-quantization-feasibility.2`; F2.3 domain/truth contracts como `phase-f2-3-quantization-domain-ground-truth.1`. Estas estructuras agrupan IDs, claims, assumptions y provenance sin producir score y no gobiernan el selector.

La generación activa (`legacy-experimental.1`) continúa usando sus analizadores y parámetros históricos para crear oportunidades, calcular chance, construir candidatos y seleccionar lanes. `DecisionDiagnostics` (`phase-c1-2-shadow.1`) observa la ruta vigente y expone values/relations en certificates sin consumir RNG ni modificar el `.osu`. El perfil persistente continúa en `phase-a.1`.

## Invariantes

- El archivo source no se sobrescribe.
- Sólo heads originales crean oportunidades; no hay recursión sintética.
- Los sintéticos jamás enseñan estilo durante la misma run.
- El perfil de evidencia permanece original-only aunque una parent sea articulada en output.
- HardValidity no se convierte en un score compensable.
- Una misma entrada, opciones, seed y versión produce las mismas decisiones.
- Los cambios conductuales deben usar una nueva `BehaviorPolicyVersion`.

## Dirección futura

La arquitectura objetivo debe poder explicar cada transformación con originales concretos y generalizaciones declaradas:

```text
observed value
    → observed relation
        → compatible composition
            → hard-valid transformation
                → supported recurrent structure
```

Si no existe evidencia comparable, el backoff futuro puede ampliar alcance de forma explícita y finalmente abstenerse. `No evidence = SKIP`, no un patrón inventado ni un taxonomy classifier externo.
