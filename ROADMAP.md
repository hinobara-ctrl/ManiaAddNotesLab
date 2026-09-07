# Roadmap

Resumen humano del plan incremental. El diseño, gates, rollback y criterios completos están en [Mapper-Derived Implementation Roadmap](docs/MAPPER_DERIVED_IMPLEMENTATION_ROADMAP.md).

| Fase | Estado | Propósito |
|---|---|---|
| A — Evidence infrastructure | ✅ COMPLETE | Perfil inmutable, original-only, con IDs, relaciones y provenance; no cambia generación. |
| B — Certificates/failure diagnostics | ✅ COMPLETE | Certificados y causas explícitas por candidato/lane en shadow mode. |
| C1 — LN witness authority | ⏸ **HOLD — HYPOTHESIS REFRAMED** | La deduplicación conductual fue rechazada; witness identity y relation agreement deben modelarse por separado. |
| C1.1 — Witness vs agreement | ✅ **COMPLETE — OUTCOME B** | Same-head agreement es señal estructural distinta de independent-witness authority; sin cambio conductual. |
| C1.2 — Exact-Head Relation Modeling / Shadow | ✅ **COMPLETE — OUTCOME A** | `H → R` explica la población agreement C1.1; no asigna pesos ni activa C1. |
| C2 — Retrigger-specific frequency | ⏸ **DEFERRED** | Hipótesis separada; no se inicia durante C1.2. |
| D0 — ChordCompletion reconstruction | ✅ **COMPLETE — OUTCOME A** | Relations exactas reduced-state→completion son reconstruibles; no tienen authority. |
| D0.1 — Exact completion competition context | ➡️ **NEXT / NOT AUTHORIZED YET** | Estudiar contexto original exacto para alternatives competidoras, sólo en shadow. |
| D1 — Resulting-state chords | ⏳ Pending | Validar el estado vertical conjunto y evitar acumulación sin soporte. |
| E — Adaptive context | ⏳ Pending | Comparar recurrencia y segmentación en shadow antes de elegir secciones. |
| F1 — Comparable contexts | ⏳ Pending | Definir support, mismatch, no-context y ambigüedad con validación held-out. |
| F2 — Typed gaps/backoff | ⏳ Pending | Gaps por transición y backoff local/estructural/global/SKIP versionado. |
| G1 — Interior relations | ⏳ Pending | Separar y validar relaciones contained, crossing y equal-end. |
| G2 — Causal articulation | ⏳ Pending | Articular sólo cuando blockers originales y evidencia retrigger lo justifiquen. |
| H — ParentArticulationPlan | ⏳ Pending | Investigar múltiples cortes como un plan atómico y válido. |
| I — MapperSupport research | ⏳ Pending | Evaluar rankings derivados de certificates sin inventar confidence. |
| J — AddChance budget | ⏳ Pending | Investigar unidades, conflictos y semántica de presupuesto frente a Bernoulli. |
| K — Zero-config validation | ⏳ Pending | Retirar controles estilísticos del flujo principal sólo tras validación diversa. |

Regla de avance: shadow y evidencia antes de conducta; una hipótesis principal por A/B; toda modificación conductual requiere versión, rollback y validación atribuible.
