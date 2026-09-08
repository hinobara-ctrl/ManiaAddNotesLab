# Documentation Index

Los documentos maestros describen sólo el estado actual y se actualizan en cada fase. Los reports de fase permanecen como snapshots históricos y no se reescriben para hacerlos parecer actuales.

## Master / Living Docs

- [README](README.md) — entrada principal, propósito, estado y ejecución rápida.
- [PROJECT_STATUS](PROJECT_STATUS.md) — fuente breve de verdad sobre lo activo, shadow, riesgos y siguiente paso.
- [ROADMAP](ROADMAP.md) — secuencia A–K resumida para lectura humana.
- [ARCHITECTURE](ARCHITECTURE.md) — fronteras estables entre evidencia, generación, geometría y validez.
- [DOCUMENTATION_INDEX](DOCUMENTATION_INDEX.md) — mapa de la documentación del repositorio.
- [PROJECT_STATE](docs/PROJECT_STATE.json) — resumen machine-readable del estado verificable y contratos separados de fase research/conductual; los reports históricos prevalecen si existe divergencia.
- [Phase closure protocol](docs/PHASE_CLOSURE_PROTOCOL.md) — orden obligatorio para iniciar y cerrar fases.

## Current Technical Docs

- [DESIGN](docs/DESIGN.md) — comportamiento implementado, fórmulas, toggles e invariantes actuales.
- [EXPERIMENTS](docs/EXPERIMENTS.md) — hipótesis y protocolo para A/B y playtesting.
- [Technical implementation roadmap](docs/MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md) — gates, contratos, riesgos y criterios completos por fase.
- [Behavior decision audit](docs/BEHAVIOR_DECISION_AUDIT.md) — inventario de decisiones numéricas y categóricas que aún afectan o podrían afectar estilo.

## Phase Reports / Historical Evidence

- [Phase A — Evidence Infrastructure](docs/PHASE_A_EVIDENCE_INFRASTRUCTURE_REPORT.md) — cierre verificable del perfil original-only y regresión conductual nula.
- [Phase B — Certificates and Failures](docs/PHASE_B_CERTIFICATES_AND_FAILURES_REPORT.md) — cierre de diagnostics, blockers y failure taxonomy en shadow.
- [Phase C1 — LN Witness Deduplication](docs/PHASE_C1_LN_WITNESS_DEDUP_REPORT.md) — investigación, resultados held-out y decisión HOLD.
- [Phase C1.1 — Witness vs Agreement Validation](docs/PHASE_C1_1_WITNESS_AGREEMENT_VALIDATION_REPORT.md) — corpus multi-family, holdouts, geometría y Outcome B.
- [Phase C1.1 corpus inventory](docs/MAP_FAMILY_VALIDATION_INVENTORY.md) — charts humanos deduplicados, familias, keymodes y conteos reproducibles.
- [Phase C1.2 — Exact-Head Relation Modeling](docs/PHASE_C1_2_EXACT_HEAD_RELATION_MODELING_REPORT.md) — modelo `H→R`, provenance, reconstrucción held-out y Outcome A.
- [C1.2 chart summary](docs/c1_2_chart_summary.csv), [family summary](docs/c1_2_family_summary.csv) y [global summary](docs/c1_2_global_summary.csv) — counts, denominadores y vistas micro/macro.
- [Phase D0 — Chord Completion Reconstruction](docs/PHASE_D0_CHORD_COMPLETION_RECONSTRUCTION_REPORT.md) — exact reduced states, held-before safety, whole-group holdout y Outcome A.
- [D0 chart summary](docs/d0_chart_summary.csv), [family summary](docs/d0_family_summary.csv) y [global summary](docs/d0_global_summary.csv) — recurrence exacta, competition, estratos naturales y micro/macro.
- [Phase D0.1 — Exact Completion Competition Context](docs/PHASE_D0_1_EXACT_COMPLETION_COMPETITION_CONTEXT_REPORT.md) — ocho vistas exactas paralelas, saneamiento anti-leakage, frontera cobertura/resolución y Outcome A.
- [D0.1 chart summary](docs/d0_1_chart_summary.csv), [family summary](docs/d0_1_family_summary.csv), [global summary](docs/d0_1_global_summary.csv) y [context-view strata](docs/d0_1_context_view_summary.csv) — outcomes por vista, familia/chart, denominadores primarios y estratos naturales.
- [Phase D0.2 — Exact Context Coverage and View Agreement](docs/PHASE_D0_2_EXACT_CONTEXT_COVERAGE_VIEW_AGREEMENT_REPORT.md) — coverage, dependency graph, conflictos, donor overlap, joint witnesses y Outcome A.
- [D0.2 chart summary](docs/d0_2_chart_summary.csv), [family summary](docs/d0_2_family_summary.csv), [global summary](docs/d0_2_global_summary.csv), [view-pair summary](docs/d0_2_view_pair_summary.csv), [joint-context summary](docs/d0_2_joint_context_summary.csv), [coverage signatures](docs/d0_2_coverage_signature_summary.csv), [conflicts](docs/d0_2_conflict_summary.csv) y [natural strata](docs/d0_2_stratification_summary.csv) — agregados versionables; el detalle target-level permanece local.
- [Phase F1 — Comparable-context Resolver](docs/PHASE_F1_COMPARABLE_CONTEXT_RESOLVER_REPORT.md) — estados candidate-centric, abstención, dependency, provenance y separación marginal/joint; Outcome A sin cambio conductual.
- [F1 chart summary](docs/f1_chart_summary.csv), [family summary](docs/f1_family_summary.csv), [global summary](docs/f1_global_summary.csv), [evidence states](docs/f1_evidence_state_summary.csv), [coverage](docs/f1_coverage_summary.csv), [conflicts](docs/f1_conflict_summary.csv), [joint context](docs/f1_joint_context_summary.csv), [natural strata](docs/f1_stratification_summary.csv), [view evidence](docs/f1_view_evidence_summary.csv), [unique support](docs/f1_unique_support_summary.csv) y [dependency](docs/f1_dependency_summary.csv) — agregados públicos; la provenance completa permanece local.
- [Phase F2 — Typed Gaps and Evidence Backoff](docs/PHASE_F2_TYPED_GAPS_EVIDENCE_BACKOFF_REPORT.md) — identidad exacta de transitions/gaps, backoff conservador Local→Global y Outcome B shadow-only sin cambio conductual.
- [F2 chart summary](docs/f2_chart_summary.csv), [family summary](docs/f2_family_summary.csv), [global summary](docs/f2_global_summary.csv), [transition summary](docs/f2_transition_summary.csv), [gap summary](docs/f2_gap_summary.csv), [evidence states](docs/f2_evidence_state_summary.csv), [backoff summary](docs/f2_backoff_summary.csv), [skip summary](docs/f2_skip_summary.csv) y [natural strata](docs/f2_stratification_summary.csv) — agregados públicos deterministas; el detalle candidate-level permanece local e ignorado.
- [Phase F2.1 — Exact Gap Timing Identity](docs/PHASE_F2_1_EXACT_GAP_TIMING_IDENTITY_REPORT.md) — auditoría de formato, ground truth sintético, timing traversal exacta, holdout de releases y Outcome C sin quantizer ni conducta.
- [F2.1 identity models](docs/f2_1_identity_model_summary.csv), [synthetic validation](docs/f2_1_synthetic_validation_summary.csv), [negative controls](docs/f2_1_negative_control_summary.csv), [timing segments](docs/f2_1_timing_segment_summary.csv) y [endpoint holdout](docs/f2_1_endpoint_holdout_summary.csv) — evidencia pública determinista; el detail permanece local e ignorado.
- [Phase F2.2 — Quantization Inference Feasibility](docs/PHASE_F2_2_QUANTIZATION_INFERENCE_FEASIBILITY_REPORT.md) — forward serialization exacta, proof de no-identificabilidad, domains explícitos, compatibility sets y Outcome B sin quantizer.
- [F2.2 models](docs/f2_2_model_summary.csv), [forward serialization](docs/f2_2_forward_serialization_summary.csv), [collisions](docs/f2_2_collision_summary.csv), [hypothesis domains](docs/f2_2_hypothesis_domain_summary.csv), [compatibility](docs/f2_2_compatibility_summary.csv), [synthetic truth](docs/f2_2_synthetic_ground_truth_summary.csv), [ambiguity](docs/f2_2_ambiguity_summary.csv), [timing changes](docs/f2_2_timing_change_summary.csv), [endpoint holdout](docs/f2_2_endpoint_holdout_summary.csv) y [rice sentinel](docs/f2_2_rice_sentinel_summary.csv) — artifacts sintéticos versionables; el detail permanece local e ignorado.
- [Phase F2.3 — Quantization Domain and Labeled Ground Truth](docs/PHASE_F2_3_QUANTIZATION_DOMAIN_GROUND_TRUTH_REPORT.md) — hashing semántico, truth source→destination, acquisition feasibility y decisión CONTINUE CONDITIONALLY sin F2.4.
- [F2.3 domain catalog](docs/f2_3_domain_catalog.csv), [domain hashes](docs/f2_3_domain_hash_summary.csv), [truth sources](docs/f2_3_ground_truth_source_summary.csv), [truth schema](docs/f2_3_ground_truth_schema_summary.csv), [circularity](docs/f2_3_circularity_summary.csv), [domain validation](docs/f2_3_domain_validation_summary.csv), [acquisition](docs/f2_3_acquisition_feasibility_summary.csv), [transition coverage](docs/f2_3_transition_coverage_summary.csv), [rice sentinel](docs/f2_3_rice_sentinel_summary.csv) y [branch decision](docs/f2_3_branch_decision_summary.csv) — evidence pública determinista; detail local ignorado.
- [Phase E — Adaptive Context Prototypes](docs/PHASE_E_ADAPTIVE_CONTEXT_PROTOTYPES_REPORT.md) — recurrence/segmentation separadas, validation held-out, sensitivity y Outcome B sin conducta.
- [Phase E pre-human certificate](docs/PHASE_E_PRE_HUMAN_DESIGN.md) — métodos, parámetros, baselines, gates y outcomes congelados antes del corpus humano.
- [Phase E method catalog](docs/e_method_catalog.csv), [synthetic truth](docs/e_synthetic_ground_truth_summary.csv), [chart summary](docs/e_chart_summary.csv), [family summary](docs/e_family_summary.csv), [recurrence](docs/e_recurrence_summary.csv), [boundaries](docs/e_boundary_summary.csv), [held-out](docs/e_heldout_validation_summary.csv), [stability](docs/e_stability_summary.csv), [sensitivity](docs/e_sensitivity_summary.csv), [stratification](docs/e_stratification_summary.csv) y [leakage](docs/e_leakage_summary.csv) — agregados públicos deterministas; detail target/donor local e ignorado.
- [Phase E.1 — Exact Recurrence Failure Stratification](docs/PHASE_E_1_EXACT_RECURRENCE_FAILURE_STRATIFICATION_REPORT.md) — exact absence, mismatch properties, multiplicity y coverage destruction; Outcome B sin conducta.
- [Phase E.1 pre-human certificate](docs/PHASE_E_1_PRE_HUMAN_DESIGN.md) — taxonomía, observables, counterfactuals y gates congelados antes del humano.
- [E.1 Phase E reproduction](docs/e1_phase_e_reproduction.csv), [synthetic gate](docs/e1_synthetic_gate_summary.csv), [link semantics](docs/e1_relation_semantics_summary.csv), [chart](docs/e1_chart_summary.csv), [family](docs/e1_family_summary.csv), [property matrix](docs/e1_disposition_property_summary.csv), [NoContext](docs/e1_no_context_breakdown.csv), [Mismatch](docs/e1_mismatch_breakdown.csv), [multiplicity](docs/e1_donor_multiplicity_summary.csv), [counterfactuals](docs/e1_counterfactual_refinement_summary.csv), [spacing](docs/e1_exact_spacing_summary.csv), [LN strata](docs/e1_ln_stratification_summary.csv) y [leakage](docs/e1_leakage_summary.csv) — agregados públicos; detail local ignorado.
- [Contextual/LN iteration](docs/ITERATION_CONTEXTUAL_LN_REPORT.md) — historial completo del rediseño de densidad, timing e interacción LN previo a Mapper-Derived.
- [LN occupied-sections diagnosis](docs/DIAGNOSIS_LN_OCCUPIED_SECTIONS.md) — evidencia y causas raíz del bajo efecto ADD en secciones ocupadas por LNs.
- [LN interior correction](docs/LN_INTERIOR_CORRECTION_REPORT.md) — implementación y A/B de elegibilidad/contexto interior.
- [LN articulation](docs/LN_ARTICULATION_REPORT.md) — modelo de segunda pasada, funnel y resultados históricos.
- [Keymode invariance audit](docs/KEYMODE_INVARIANCE_AUDIT.md) — clasificación y revisión de portabilidad vertical/temporal.
- [Keymode invariance report](docs/KEYMODE_INVARIANCE_REPORT.md) — matriz objetiva multikey y límites de interpretación.
- [Rice behavior](docs/RICE_BEHAVIOR_REPORT.md) — auditoría de rice y ausencia deliberada de taxonomía de patrones.
- [A/B results](docs/AB_RESULTS.md) — comparaciones reproducibles de las políticas experimentales iniciales.

Los outputs `.osu`, CSV de ejecución y baselines grandes permanecen como artefactos locales ignorados por Git. Los resultados relevantes están resumidos en los reports `.md`.

## Future Vision / Research

- [Mapper-derived vision](docs/FUTURE_MAPPER_DERIVED_ALGORITHM_PLAN.md) — objetivo conceptual de eliminar preferencias estilísticas arbitrarias.
- [Critical proposals review](docs/MAPPER_DERIVED_PROPOSALS_REVIEW.md) — revisión de riesgos, alternativas y experimentos necesarios.
- [Integration blueprint](docs/MAPPER_DERIVED_INTEGRATION_BLUEPRINT.md) — propuesta arquitectónica extensa que precede al roadmap incremental vigente.

## Fixtures

- [samples/README](samples/README.md) — descripción de los `.osu` sintéticos pequeños incluidos para pruebas reproducibles.
