# Project Status

Última actualización: 2026-09-06. Este documento representa únicamente el estado actual y debe sobrescribirse al cerrar cada fase.

## Resumen

ManiaAddNotesLab funciona como laboratorio CLI/Web para generar variantes `.osu` deterministas, ejecutar lotes y estudiar decisiones del algoritmo. La política conductual activa es `legacy-experimental.1`; el perfil de evidencia es `phase-a.1` y el schema diagnóstico actual es `phase-c1-shadow.1`.

| Fase | Estado | Efecto sobre generación |
|---|---|---|
| A — Evidence Infrastructure | **COMPLETE** | Ninguno; construye un perfil original-only. |
| B — Certificates and Failures | **COMPLETE** | Ninguno; explica candidatos, blockers y rechazos en shadow opt-in. |
| C1 — LN Witness Authority | **HOLD** | Ninguno; sólo compara un peso alternativo en shadow/research. |

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

## Por qué C1 está en HOLD

Phase B demostró que `Duration` y `ExactRelease` pueden ser dos evidence paths del mismo objeto original. C1 obtuvo una factorización algebraicamente limpia: sumar una vez el `DistanceWeight` por `ObservationId` y aplicar después la afinidad existente.

Sin embargo, convertir esa factorización en autoridad conductual no está justificado. En held-out reconstruction, la alternativa redujo ligeramente top-1 y empeoró el mean rank en las tres variantes humanas disponibles, que pertenecen a una sola familia de chart. Esto sugiere que la convergencia de labels podría contener una señal útil de acuerdo entre features. No existe todavía una forma no arbitraria de separar esa señal de la autoridad independiente.

Por eso C1 permanece en **HOLD**, no en “failed”: el hallazgo es válido, la alternativa continúa observable y no se inventó una aggregation para forzar el avance.

## Validación actual

- `dotnet restore`: PASS.
- `dotnet build -c Release`: PASS, 0 errores.
- `dotnet test -c Release`: **171 passed, 0 failed, 0 skipped**.
- Cinco fixtures conductuales permanecen byte a byte iguales a Phase B.
- Spring ADD 50 seed 100 conserva el hash histórico documentado.

La consulta de vulnerabilidades de NuGet puede emitir `NU1900` cuando `api.nuget.org` no está accesible; no afectó la compilación ni las pruebas de este cierre.

## Riesgos y problemas abiertos

- Sólo existe una familia de chart humano para contrastar C1; los fixtures sintéticos no prueban naturalidad.
- Muchas decisiones estilísticas siguen siendo thresholds, ventanas, caps, pooling o desempates legacy.
- La elección uniforme de lane y la composición del estado vertical aún no se derivan del mapa.
- `LocalMismatch`, contextos comparables, secciones adaptativas y `CompatibleComposition` no gobiernan generación.
- Interiores y articulación necesitan playtesting multi-chart; “interior” no implica universalmente release contenido.
- Diagnostics detallados pueden producir JSON muy grandes; deben seguir siendo opt-in y filtrados.
- El writer no preserva exactamente comentarios intercalados, encoding ni estilo de newline del source.

## Próximo paso recomendado

No usar C2 para esquivar la pregunta de C1. Primero reunir charts humanos diversos —otros mappers, estilos y keymodes— y diseñar un experimento que separe explícitamente:

1. autoridad de un witness independiente;
2. acuerdo entre varias labels del mismo witness.

Sólo una regla que supere esa validación sin magic numbers debe recibir una nueva `BehaviorPolicyVersion`. Hasta entonces, C1 continúa en HOLD y C2 permanece pendiente.

Detalles y evidencia: [Phase C1 report](docs/PHASE_C1_LN_WITNESS_DEDUP_REPORT.md).

