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

## Evidencia y generación actuales

`MapperEvidenceProfile` (`phase-a.1`) congela observations, chords, duraciones/releases LN, transiciones, retriggers, anchors, provenance y fingerprint. Phase B, C1 y C1.1 añaden diagnostics/research sobre candidatos, blockers, witness identity y agreement. C1.2 añade `SimultaneousOriginalEventGroup` como primitive general mínima y `ExactHeadEndpointRelation` como especialización LN; D0 añade relaciones exactas de chord completion como research separado `phase-d0-research.1`. Estas estructuras agrupan IDs y claims sin producir score y no gobiernan el selector.

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
