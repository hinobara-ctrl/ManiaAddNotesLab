# Project Status

Última actualización: 2026-09-06. Este documento representa únicamente el estado actual y debe sobrescribirse al cerrar cada fase.

## Resumen

ManiaAddNotesLab funciona como laboratorio CLI/Web para generar variantes `.osu` deterministas, ejecutar lotes y estudiar decisiones del algoritmo. La política conductual activa es `legacy-experimental.1`; el perfil de evidencia es `phase-a.1` y el schema diagnóstico actual es `phase-c1-shadow.1`.

| Fase | Estado | Efecto sobre generación |
|---|---|---|
| A — Evidence Infrastructure | **COMPLETE** | Ninguno; construye un perfil original-only. |
| B — Certificates and Failures | **COMPLETE** | Ninguno; explica candidatos, blockers y rechazos en shadow opt-in. |
| C1 — LN Witness Authority | **HOLD** | Ninguno; sólo compara un peso alternativo en shadow/research. |
| C1.1 — Witness vs Agreement Validation | **COMPLETE — OUTCOME B** | Ninguno; demuestra que el acuerdo same-head es una señal separada. |

## Qué funciona hoy

- Parser/writer independiente para osu!mania 1K–18K, timing con BPM variable y salida reparseable.
- CLI para una ejecución y Web para lotes masivos de chances/seeds.
- Generación determinista por input, opciones y seed; el source nunca se sobrescribe.
- Taps añadidos en timestamps originales y LNs construidas con duraciones/releases observados.
- Geometría por lane con no-overlap y separación temporal.
- Densidad vertical por heads simultáneos, normalización relativa por keymode y protección contextual de bursts.
- Gap local, vocabulario temporal relativo al mapa, oportunidades interiores y articulación disponibles como políticas experimentales.
- Trace, CSV, métricas, perfil de evidencia y diagnostics exportables.

## Qué está implementado en shadow mode

`MapperEvidenceProfile` congela observaciones, relaciones y provenance exclusivamente desde `OriginalObjects`. Phase B añade certificados, evaluación de HardValidity, blockers originales/sintéticos y causas de fallo. C1 calcula, sólo para investigación, un peso alternativo que cuenta una contribución por `OriginalObservationId` y conserva las labels `Duration`/`ExactRelease` como explicación.

Con diagnostics desactivados, C1 no materializa su mapa de witnesses. Ningún score, certificate ni peso C1 decide actualmente oportunidades, candidatos, lanes, RNG o articulación.

## Qué afecta realmente la generación

La policy legacy sigue tomando decisiones mediante Bernoulli por oportunidad, factores manuales de chord/contexto, weights LN por distancia y afinidad, gap local con fallback, selección ponderada de forma y lane legal uniforme. Los toggles experimentales documentados sí pueden alterar el resultado cuando se activan o desactivan.

Los defaults modernos activos incluyen densidad por heads, escalas relativas, densidad contextual, gap local y timing relativo al mapa. Las oportunidades LN interiores y la articulación están implementadas pero permanecen desactivadas por defecto. Phase A, Phase B y C1 no reemplazan estas reglas.

## Por qué C1 sigue en HOLD después de C1.1

Phase B demostró que `Duration` y `ExactRelease` pueden ser dos evidence paths del mismo objeto original. C1 obtuvo una factorización algebraicamente limpia: sumar una vez el `DistanceWeight` por `ObservationId` y aplicar después la afinidad existente.

El corpus C1.1 contiene 11 charts/familias humanas únicas, ocho con LNs, y 12.554 targets en 4K/7K/10K. Las 14.828 convergencias observadas fueron `SameHeadAgreement`; ninguna fue coincidencia de materialización, tolerancia u otra causa. Al retirar todo el head-group exacto desaparecieron tanto los acuerdos como cualquier diferencia entre weights.

En LOO-object, UniqueWitness mejoró levemente los targets sin twin exacto, pero empeoró 3.103 targets con twin estructural en ocho familias. Esto demuestra que una observación sigue siendo un testigo independiente, pero sus labels concordantes contienen una señal estructural distinta. Resultado C1.1: **OUTCOME B**. La deduplicación conductual original queda rechazada/reformulada y C1 permanece en **HOLD** hasta modelar ambas dimensiones sin magic weights.

## Validación actual

- `dotnet restore`: PASS.
- `dotnet build -c Release`: PASS, 0 errores.
- `dotnet test -c Release`: **188 passed, 0 failed, 0 skipped** en el cierre aislado C1.1.
- Cinco fixtures conductuales permanecen byte a byte iguales a Phase B.
- Spring ADD 50 seed 100 conserva el hash histórico documentado.

La consulta de vulnerabilidades de NuGet puede emitir `NU1900` cuando `api.nuget.org` no está accesible; no afectó la compilación ni las pruebas de este cierre.

## Riesgos y problemas abiertos

- El corpus tiene 11 familias, pero sólo ocho contienen LNs; 10K no aporta targets LN y no existen charts humanos 1K/18K en esta muestra.
- `WitnessAgreement` es descriptivo: todavía no existe una regla justificada para convertirlo en autoridad, bonus o score.
- Muchas decisiones estilísticas siguen siendo thresholds, ventanas, caps, pooling o desempates legacy.
- La elección uniforme de lane y la composición del estado vertical aún no se derivan del mapa.
- `LocalMismatch`, contextos comparables, secciones adaptativas y `CompatibleComposition` no gobiernan generación.
- Interiores y articulación necesitan playtesting multi-chart; “interior” no implica universalmente release contenido.
- Diagnostics detallados pueden producir JSON muy grandes; deben seguir siendo opt-in y filtrados.
- El writer no preserva exactamente comentarios intercalados, encoding ni estilo de newline del source.

## Próximo paso recomendado

No iniciar C2. Preparar una fase separada **C1.2 — Witness Agreement Modeling / Shadow** que conserve explícitamente:

1. autoridad de un witness independiente;
2. acuerdo entre varias labels del mismo witness.

Debe estudiar capacidad explicativa sin asignar todavía weight, bonus, score o confidence. Sólo una futura regla conductual con validación atribuible puede recibir una nueva `BehaviorPolicyVersion`. Hasta entonces, C1 continúa en HOLD y C2 permanece pendiente.

Detalles y evidencia: [Phase C1.1 report](docs/PHASE_C1_1_WITNESS_AGREEMENT_VALIDATION_REPORT.md). El [reporte C1](docs/PHASE_C1_LN_WITNESS_DEDUP_REPORT.md) permanece como evidencia histórica.
