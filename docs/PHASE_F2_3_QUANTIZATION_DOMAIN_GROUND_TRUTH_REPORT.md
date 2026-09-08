# Phase F2.3 — Quantization Domain and Labeled Ground Truth

**Estado: COMPLETE — OUTCOME B — RESEARCH/SHADOW ONLY.**  
**F2 branch decision: CONTINUE CONDITIONALLY.**  
**Behavior change:** false.  
**Research schema:** `phase-f2-3-quantization-domain-ground-truth.1`.

F2.3 es un punto de decisión. La metodología existe, pero falta el recurso externo que permitiría validar un domain contra intención humana independiente. No se implementó quantizer ni se abrió F2.4.

## 1. Initial state

- Commit inicial: `5fbde5e3fc15d4917e223b8d52a34b8cd84672cc`; branch `main`; working tree limpio.
- Estado canónico: F2.2 COMPLETE/B, F2.3 NEXT; generation `legacy-experimental.1`.
- Restore/build/DocConsistency/diff check: PASS.
- Suite inicial real: 415 passed, 0 failed, 0 skipped.
- `NU1900` sólo indicó que la red restringida impidió consultar vulnerabilidades NuGet; build/tests no dependieron de esa consulta.

## 2. F2.2 reproduction

Antes del hardening se ejecutó dos veces `f2-2-synthetic` hacia `.artifacts/f2-3/`. Los diez CSV coincidieron por SHA-256 con los artifacts F2.2 publicados y entre reruns. Se preservaron:

- forward compatibility exacta;
- non-identifiability `0.5` vs `250/499` → 250 ms;
- narrow domain unique y expanded domain ambiguous;
- collision class size 2;
- endpoint leakage correcto = 0 y positive control > 0;
- rice sentinel;
- artifacts deterministas.

Los CSV/report históricos F2.2 no se reescribieron.

## 3. Pre-F2.3 hardening

F2.2 fue endurecido y su schema de código pasó a `phase-f2-2-quantization-feasibility.2`:

1. `QuantizationHypothesisDomain.ContentHash` usa canonicalización `latent-beat-coordinate.1` y SHA-256.
2. La identidad semántica es únicamente `BeatPosition`; labels son aliases metadata.
3. Aliases de una misma posición se deduplican y no crean fake ambiguity.
4. Un mismo label no puede representar coordenadas distintas.
5. `Justification` null, empty o whitespace se rechaza.
6. El assumption certificate conserva domain ID, content hash y canonicalization version.

El mismo contenido en distinto orden produce el mismo hash. El mismo ID con candidates distintos produce otro hash. Cambiar sólo `half` por `two_quarters` conserva el hash porque labels no son semántica. El gate posterior al hardening pasó con 445 tests.

## 4. Domain source taxonomy

`QuantizationDomainSourceKind` distingue `ExternalConvention`, `SyntheticGroundTruth`, `EditorMetadata`, `MapperAnnotated`, `FileDerived`, `ChartDerived`, `ManualResearchDomain` y `Unknown`.

Cada descriptor conserva ID, hash, canonicalization, source description, justification, semantic candidate count, circularity/reason, relationship with truth, intended use y un flag explícito `MapperDerivedJustified`. Un domain circular no puede declararse mapper-derived.

## 5. Candidate domain sources considered

| Fuente | Availability | Circularity | Mapper-derived | Disposición |
|---|---|---|---|---|
| Fixed external vocabulary | Disponible como assumption | NonCircular | No | Method demonstration only |
| Snap metadata dentro de `.osu` | No encontrada | Unknown | No | Unavailable |
| Pre-serialization editor/source pair | Requiere source externo | NonCircular | No por defecto | Preferred conditional validation route |
| Mapper-provided annotation | Requiere participación | NonCircular si se captura pre-export | No: author-provided | Conditional route |
| Chart-derived denominators | Sólo tras cuantizar | Circular | No | Rejected |
| Exact file relations | Disponibles | NonCircular | Son file facts, no intent | Observation only |
| Synthetic latent domain | Disponible | NonCircular | No | Logic falsification only |
| Manual finite domain | Disponible | NonCircular | No | Research condition only |

No fuente disponible hoy justifica un mapper vocabulary productivo.

## 6. Ground truth taxonomy

| Level | Fuente | Fuerza y límite |
|---|---|---|
| 0 `NoLabel` | Human `.osu` | Observable only; no latent truth |
| 1 `RetrospectiveAnnotation` | Persona interpreta el export | Interpretive y afectada por information loss |
| 2 `AuthorConfirmed` | Mapper declara intención | Humana independiente, pero puede carecer de coordinate exacta |
| 3 `PreSerializationLatent` | Editor/source + export pair | Strong; conserva coordinates previas al integer-ms |
| 4 `ControlledGeneration` | Fixture programático | Complete synthetic truth; no prueba mapper vocabulary |

No se usa confidence probabilística: el nivel describe procedencia.

## 7. Full transition ground-truth schema

`LabeledTransitionTruth` conserva:

- source/destination endpoint types;
- source/destination latent beats;
- exact latent gap;
- ambos serialized timestamps;
- transition kind;
- timing map, timing hash y serialization version;
- truth source, level y provenance;
- para release-origin: LN head latent beat y head timestamp separados del release.

Los cuatro kinds quedan completos: Tap→Tap, Tap→LN, Release→Tap y Release→LN. El dataset no puede etiquetar sólo destination porque el gap depende de dos serializaciones potencialmente independientes.

## 8. Data-acquisition routes

| Ruta | Available now | Effort | Independence | Strength | Reproducibility | Decisión |
|---|---|---|---|---|---|---|
| Historical 11 `.osu` | Sí | Low | No latent label | Level 0 | High | Reject as truth |
| Retrospective annotation | No recolectada | Medium | Partial | Level 1 | Medium | Insufficient alone |
| Mapper confirmation | No recolectada | Medium | Yes | Level 2 | Medium | Conditional support |
| Pre-export editor pair | No disponible | Medium-high | Yes | Level 3 | High if captured | Preferred route |
| Programmatic pilot | Sí | Low | Synthetic | Level 4 synthetic | High | Implemented logic control |
| Domain-generated labels | Posible | Low | No | Self-label | High | Rejected circular validation |

La ruta realista es un package autorizado creado durante authoring: coordinates de ambos endpoints y LN head/release antes de export, timing map y `.osu` exportado. Requiere consentimiento/source licensing y aún no existe.

## 9. Circularity analysis

Un derivation es circular cuando necesita resolver quantization para decidir qué hypotheses alimentar a esa misma inference. “Denominators encontrados en las notas” queda `Circular`; exact file relations son `NonCircular` pero no latentes. Domains sintéticos/manuales son non-circular como assumptions, no mapper-derived. Metadata de editor desconocida queda `Unknown`, no promocionable.

## 10. Domain content hashing

Canonical form:

```text
latent-beat-coordinate.1 | sorted unique canonical decimal beat positions
```

No incluye enumeration order, aliases, runtime, paths, timestamps, ID ni prose. ID/source/justification permanecen fields separados de provenance. SHA-256 hace visible que `foo={half}` y `foo={half,quarter}` no son el mismo domain aunque compartan ID.

## 11. Semantic duplicate handling

La hypothesis semántica es la coordenada latente. `half@0.5` y `two_quarters@0.5` se normalizan en un solo candidate con dos aliases. Compatibility cardinality permanece 1. Coordenadas genuinamente distintas permanecen separadas aunque serialicen al mismo timestamp, produciendo ambiguity real.

## 12. Synthetic validation

El evaluator completo `Domain + LabeledTransitionTruth + ForwardModel` produjo:

- `UniqueCorrect`;
- `UniqueIncorrect`;
- `AmbiguousContainsTruth`;
- `AmbiguousExcludesTruth`;
- `UnsupportedUnderModel`.

Source y destination se evalúan por separado y luego se clasifica la relación. Los fixtures cubren integral/non-integral ms, redundant redline, real BPM change, LN crossing y 1K/4K/7K/10K/18K.

## 13. Domain-vs-truth independence

La independencia es un field obligatorio de evaluación: `Independent`, `DomainConstructedFromTruth`, `SameUnlabeledFile` o `Unknown`. Los cinco controles sintéticos publicados se etiquetan honestamente `DomainConstructedFromTruth`; prueban ramas lógicas, no validación independiente. El contrato puede representar un future mapper truth como `Independent`, pero no afirma que ya exista.

Un futuro `DomainDesignSet` debe quedar congelado antes de abrir el `IndependentValidationSet`. Aunque no exista ML, diseñar y validar el domain en las mismas labels invalidaría una conclusión fuerte.

## 14. Pilot dataset

Se implementó sólo un pilot **programmatic synthetic**. Contiene ambos endpoints, gap, source LN head cuando aplica, timing maps y provenance pre-serialization. No contiene ni finge human labels. Su finalidad es validar schema, serializer y evaluator.

## 15. Human corpus

**NOT RUN.** Pregunta potencial: cardinalidad model-conditional de un domain externo. Esa descripción no ayuda a justificar el domain ni mide intent accuracy, y no cambia la decisión de acquisition. Los 11 charts históricos permanecen observational corpus; no se promovieron retroactivamente a labels.

## 16. Rice sentinel

Se conservaron repeated fast, same-lane jack-like, alternating y close neighbors. A 500 ms/beat, gaps `0.124` y `0.125` serializan 62 y 63 ms y no se fusionan cuando el observable los distingue. No existen pattern classifier, hand model, JackPenalty ni TrillBonus.

## 17. Predefined gate F2.3

Este gate se fijó antes de interpretar la decisión:

| # | Condición | Estado |
|---:|---|---|
| 1 | baseline green | PASS |
| 2 | F2.2 reproducible | PASS |
| 3 | domain semantic identity explicit | PASS |
| 4 | domain content hash | PASS |
| 5 | hash deterministic | PASS |
| 6 | aliases no fake ambiguity | PASS |
| 7 | justification required | PASS |
| 8 | source kind explicit | PASS |
| 9 | circularity explicit | PASS |
| 10 | ground-truth taxonomy | PASS |
| 11 | source+destination labels | PASS |
| 12 | LN release truth | PASS |
| 13 | timing-change truth | PASS |
| 14 | labels independent from tested domain | **NOT AVAILABLE for human validation; representable and enforced in protocol** |
| 15 | synthetic separated from human | PASS |
| 16 | no unlabeled-human accuracy claim | PASS |
| 17 | no coverage selection | PASS |
| 18 | no frequency authority | PASS |
| 19 | no score | PASS |
| 20 | no probability | PASS |
| 21 | no confidence | PASS |
| 22 | no ML | PASS |
| 23 | no nearest behavior | PASS |
| 24 | no epsilon behavior | PASS |
| 25 | no generation dependency | PASS |
| 26 | endpoint-aware holdout | PASS |
| 27 | endpoint leakage zero | PASS |
| 28 | original-only where applicable | PASS |
| 29 | synthetic teaching zero | PASS |
| 30 | cross-chart zero | PASS |
| 31 | deterministic artifacts | PASS |
| 32 | multi-key | PASS |
| 33 | rice sentinel | PASS |
| 34 | no keymode stylistic branches | PASS |
| 35 | legacy byte-exact | PASS by regression |
| 36 | legacy RNG exact | PASS by regression |
| 37 | docs consistent | PASS |
| 38 | root guard green | PASS |
| 39 | full suite green | PASS |

La ausencia del punto 14 en datos humanos impide Outcome A, pero no invalida la infraestructura.

## 18. Outcome

**OUTCOME B.** Domain hashing, semantic identity, truth schema y validation methodology están definidos. Existe una ruta práctica concebible —capture pre-export mapper data— pero depende de participación/autorización externa y todavía no se realizó. No hay independent validation set actual ni domain mapper-derived justificado.

Outcome A no aplica porque la ruta no está materializada. Outcome C no aplica porque el package pre-export es técnicamente obtenible y suficientemente informativo si se autoriza.

## 19. F2 BRANCH DECISION

**CONTINUE CONDITIONALLY.**

F2 no debe avanzar a quantizer mientras falte el recurso. La condición exacta es obtener un package mapper-authored autorizado que incluya, antes de integer-ms export, source/destination latent beats, transition kind, LN head/release separados, timing map y `.osu` resultante; además debe existir separación DomainDesignSet/IndependentValidationSet.

## 20. If CONTINUE

No aplica: la decisión no es `CONTINUE` incondicional y no se abre un experimento de quantizer.

## 21. If CONTINUE CONDITIONALLY

Missing dependency: **authorized pre-serialization mapper-authored transition package**. El siguiente trabajo, sólo si se autoriza y el recurso puede obtenerse, es un prerequisite de acquisition (`F2.ACQ`), no F2.4: definir formato de captura, recolectar un pilot pequeño, congelar design/validation split y verificar cobertura de cuatro transitions, timing changes, LNs, rice y varios keymodes.

## 22. If PARK

No aplica ahora. Si el package no puede conseguirse, F2 debe pasar a `PARK` sin otra fase y el roadmap debería reevaluar D1 o Phase E según sus prerequisitos, sin autorizarlas automáticamente.

## 23. Limitations

- No human latent labels disponibles.
- No editor source format integrado ni asumido.
- Author confirmation retrospectiva puede sufrir recall bias.
- Synthetic validation no establece human mapper vocabulary.
- External convention domain sigue siendo una assumption.
- Candidate hash identifica contenido semántico, no verdad.
- El pilot no mide naturalidad ni behavior.

## 24. Files changed

- Hardened `QuantizationInferenceFeasibilityResearch`.
- Added `QuantizationDomainGroundTruthResearch`.
- Added F2.2 hardening/F2.3 tests.
- Added `f2-3-synthetic` runner.
- Added ten `f2_3_*` CSVs.
- Added this report and updated living state/docs.

## 25. Artifacts

- `f2_3_domain_catalog.csv`
- `f2_3_domain_hash_summary.csv`
- `f2_3_ground_truth_source_summary.csv`
- `f2_3_ground_truth_schema_summary.csv`
- `f2_3_circularity_summary.csv`
- `f2_3_domain_validation_summary.csv`
- `f2_3_acquisition_feasibility_summary.csv`
- `f2_3_transition_coverage_summary.csv`
- `f2_3_rice_sentinel_summary.csv`
- `f2_3_branch_decision_summary.csv`
- local ignored `.artifacts/f2-3/f2_3_detail.json`

Todos contienen datos; no se crearon CSV decorativos vacíos.

## 26. Determinism

No RNG, timestamps de ejecución, machine paths ni runtime measurements. Domain hashes y orden son estables. Dos reruns independientes produjeron hashes SHA-256 idénticos para diez CSV y detail JSON.

## 27. Regression

La suite final conserva byte-exact output, RNG transcript, no-overlap, parser/writer, endpoint leakage y F2.2. Generation/CLI/Web no referencian los nuevos research types. Resultado final: 446 passed, 0 failed, 0 skipped.

## 28. Final verification

```powershell
git status --short
git diff --check
dotnet build -c Release
dotnet test -c Release
dotnet run --project tools/DocConsistency -- --check
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- f2-3-synthetic docs .artifacts/f2-3/f2_3_detail.json
git check-ignore .artifacts/f2-3/f2_3_detail.json
```

Todos pasan; el status contiene únicamente cambios F2.3 sin commit.

## 29. Explicit confirmation

- `BehaviorPolicyVersion = legacy-experimental.1`.
- Generation unchanged.
- No quantizer behavior.
- No F2 A/B.
- C2 remains deferred.
- D1 remains unauthorized.
- Phase E remains unopened.
- No commit, push, tag or release.

**DO NOT TURN A RESEARCH ASSUMPTION INTO MAPPER TRUTH.**
