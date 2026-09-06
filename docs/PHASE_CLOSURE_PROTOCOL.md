# Phase Closure Protocol

Este documento maestro define el procedimiento obligatorio. Una fase no está cerrada sólo porque el código y los tests pasen: código, report, audit, roadmaps, master docs, estado canónico, consistencia documental y regresión deben describir el mismo resultado. Los reports cerrados son evidencia histórica inmutable; una fase posterior explica cualquier reinterpretación sin reescribirlos. No se hace commit ni push automáticamente.

## Before starting a phase

1. Leer `docs/PROJECT_STATE.json`.
2. Leer el report de la fase actual.
3. Leer el report de la fase anterior.
4. Leer roadmap técnico y `BEHAVIOR_DECISION_AUDIT.md`.
5. Ejecutar `dotnet run --project tools/DocConsistency -- --check`.
6. Ejecutar baseline restore/build/test.
7. Verificar que no existan cambios previos sin documentar.

## Required phase-closing order

1. Run baseline before phase.
2. Implement/research exactly one main hypothesis.
3. Run phase-specific validation.
4. Write immutable phase report.
5. Determine explicit outcome: COMPLETE / HOLD / REJECTED / etc.
6. Update `BEHAVIOR_DECISION_AUDIT.md`.
7. Update technical roadmap.
8. Update `docs/PROJECT_STATE.json`.
9. Update `PROJECT_STATUS.md`.
10. Update `ROADMAP.md`.
11. Update `ARCHITECTURE.md` only if architecture changed.
12. Update README only if visitor-facing state changed.
13. Update `DOCUMENTATION_INDEX.md`.
14. Run documentation consistency checker.
15. Run restore/build/test.
16. Verify behavior regression according to phase contract.
17. Only then consider phase closed.

El orden es deliberado: el report fija el resultado histórico y las fuentes vivas lo resumen después. Si el report y `PROJECT_STATE.json` divergen, el report cerrado determina qué ocurrió y el estado canónico debe corregirse.
