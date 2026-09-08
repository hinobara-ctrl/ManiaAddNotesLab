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

## Canonical phase semantics

- `currentPhase` identifica la fase cerrada más reciente en el orden canónico de `phases`; debe tener estado `COMPLETE`, `HOLD` o `REJECTED` y coincidir con el report cerrado.
- `nextRecommendedPhase` identifica exactamente una fase de investigación accionable recomendada que todavía no comenzó; debe ser distinta de `currentPhase` y tener estado `NEXT`. No puede apuntar a un prerequisite `BLOCKED`. Una recomendación no constituye autorización para implementarla y debe declarar `authorization` explícita.
- `researchBranches` conserva la decisión de cada rama y su dependencia bloqueante. `CONTINUE_CONDITIONALLY` no equivale a `NEXT`: si `blockedBy` apunta a una fase, ésta debe existir con estado `BLOCKED`, explicar `blockedReason` y permanecer separada de la fase accionable recomendada.
- `behaviorChange` declara si la sincronización actual alteró conducta. Una actualización documental sin cambios de generación debe mantenerlo en `false`; los bloques maestros lo proyectan como `Behavior change: none`.
- `testStatus` conserva el snapshot del último cierre completo. No se actualiza con una ejecución parcial ni se usa para ocultar failures o skipped tests.

`README.md` y `PROJECT_STATUS.md` deben contener exactamente un bloque `PROJECT-STATE`. El bloque proyecta, como campos separados, la fase actual, la siguiente candidata accionable y su autorización, el prerequisite bloqueado, la decisión de rama, la policy conductual, el cambio conductual y el snapshot de tests. El orden de los campos no es semántico. `ROADMAP.md` debe contener filas exactas e independientes para current, next actionable y blocker; un identificador prefijo como `F2` nunca puede satisfacer por accidente una fila `F2.1` o `F2.ACQ`.

## PRE-COMMIT / PRE-PUSH CHECK

Desde la raíz del repositorio, ejecutar en este orden:

```powershell
dotnet run --project tools/DocConsistency -- --check
dotnet test -c Release
git diff --check
git status --short
```

Además, revisar manualmente que todo documento maestro afectado por el cierre esté incluido en el diff y que el report, `PROJECT_STATE.json`, README, status, roadmaps, arquitectura, audit e índice describan el mismo resultado. Un checker verde no sustituye esta revisión.

Este protocolo no autoriza `git add`, commit ni push. Esas acciones requieren una instrucción explícita posterior.
