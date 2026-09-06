# Documentation Index

Los documentos maestros describen sólo el estado actual y se actualizan en cada fase. Los reports de fase permanecen como snapshots históricos y no se reescriben para hacerlos parecer actuales.

## Master / Living Docs

- [README](README.md) — entrada principal, propósito, estado y ejecución rápida.
- [PROJECT_STATUS](PROJECT_STATUS.md) — fuente breve de verdad sobre lo activo, shadow, riesgos y siguiente paso.
- [ROADMAP](ROADMAP.md) — secuencia A–K resumida para lectura humana.
- [ARCHITECTURE](ARCHITECTURE.md) — fronteras estables entre evidencia, generación, geometría y validez.
- [DOCUMENTATION_INDEX](DOCUMENTATION_INDEX.md) — mapa de la documentación del repositorio.
- [PROJECT_STATE](docs/PROJECT_STATE.json) — resumen machine-readable del estado verificable; los reports históricos prevalecen si existe divergencia.
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
