# ManiaAddNotesLab

ManiaAddNotesLab es un laboratorio independiente en .NET 8 para investigar cómo aumentar interacción en difficulties de osu!mania sin sobrescribir el mapa original. Nació inspirado por la idea de **ADD NOTES** de LR2/OpenLR2, pero no incorpora código de esos proyectos: son una referencia conceptual.

El objetivo actual es evolucionar desde un generador experimental con heurísticas explícitas hacia un sistema capaz de justificar transformaciones usando el lenguaje del propio mapa:

```text
original chart → evidence → relations → future recurrent structures
```

No utiliza una biblioteca hardcodeada de patrones —jack, trill, stream u otras taxonomías externas— para decidir qué estilo debe producir. La dirección de investigación es que los objetos originales aporten valores y relaciones; la geometría determine qué puede existir; y el sistema se abstenga cuando falte evidencia.

## Estado actual

- **Phase A — COMPLETE:** infraestructura de evidencia original-only.
- **Phase B — COMPLETE:** certificados y causas de fallo en shadow mode.
- **Phase C1 — HOLD:** la autoridad numérica de witnesses LN todavía no tiene una regla suficientemente justificada.
- **Phase C1.1 — COMPLETE, OUTCOME B:** witness identity y relation agreement son dimensiones distintas.
- **Phase C1.2 — COMPLETE, OUTCOME A:** relaciones exact-head `H→R` explican la señal C1.1 sin asignar pesos.
- **Phase D0 — COMPLETE, OUTCOME A:** relations exactas de chord completion son reconstruibles y explicativas en shadow.
- **Phase D0.1 — COMPLETE, OUTCOME A:** contexto exacto separa miles de completions competidoras, con una frontera explícita de cobertura.
- **Phase D0.2 — COMPLETE, OUTCOME A:** agreement, donor overlap y contexto conjunto observado son reconstruibles sin selection authority.
- **Phase F1 — COMPLETE, OUTCOME A:** support, mismatch, no-context y ambiguity son reconstruibles por candidate sin gobernar el selector.
- **Phase F2 — COMPLETE, OUTCOME B, SHADOW ONLY:** typed gaps/backoff exactos son auditables, pero la cobertura no permite conectar A/B.
- **Phase F2.1 — COMPLETE, OUTCOME C, SHADOW ONLY:** no existe una equivalencia temporal nominal general recuperable sin inferir quantization ausente del archivo.
- **Phase F2.2 — COMPLETE, OUTCOME B, RESEARCH/SHADOW ONLY:** la inferencia forward es falsable bajo un domain finito explícito, pero el proyecto no puede justificar ese domain desde el mapper ni medir accuracy humana sin labels.
- **Phase F2.3 — COMPLETE, OUTCOME B, CONTINUE CONDITIONALLY:** domain hashing y truth completa son auditables, pero falta un package mapper-authored pre-export independiente.
- **Phase E — COMPLETE, OUTCOME B, SHADOW ONLY:** existe recurrencia exacta útil y boundaries localmente estables, pero mismatch, abstención y sensibilidad impiden promover una representación.
- **Phase E.1 — COMPLETE, OUTCOME B, SHADOW ONLY:** 99,51% de NoContext es ausencia exacta; spacing correlaciona débilmente y su refinement destruye coverage, por lo que la rama recurrence queda aparcada.

La generación activa continúa en `legacy-experimental.1`; el perfil sigue en `phase-a.1` y diagnostics en `phase-c1-2-shadow.1`. Phase A, Phase B y C1–E.1 no gobiernan el selector. La rama F2 sigue `CONTINUE_CONDITIONALLY` y `F2.ACQ` continúa bloqueado. E.1 aparca el refinamiento de recurrence sin fuzzy fallback. La siguiente recomendación de investigación es **D1.0 — Resulting-State Composition Feasibility / Shadow**, sólo `NEXT / NOT_AUTHORIZED` y sin cambio conductual. La fase histórica **D1 — ChordCompletion Resulting-State A/B** permanece futura, conductual, versionada y también `NOT_AUTHORIZED`; D1.0 no la autoriza. C2 permanece deferred.

<!-- PROJECT-STATE:BEGIN -->
Current phase: E.1 — COMPLETE — OUTCOME B<br>
Next actionable research candidate: D1.0 — Resulting-State Composition Feasibility / Shadow<br>
Next actionable authorization: NOT_AUTHORIZED<br>
Next behavioral phase: D1 — ChordCompletion Resulting-State A/B<br>
Next behavioral authorization: NOT_AUTHORIZED<br>
Blocked prerequisite: F2.ACQ — BLOCKED<br>
Research branch: F2 — CONTINUE_CONDITIONALLY<br>
Behavior policy: `legacy-experimental.1`<br>
Behavior change: none<br>
Evidence profile: `phase-a.1`<br>
Diagnostic schema: `phase-c1-2-shadow.1`<br>
Tests: 518 passed / 0 failed / 0 skipped
<!-- PROJECT-STATE:END -->

Este repositorio es un laboratorio de investigación, **no un algoritmo final ni una release lista para usuarios**. Varias políticas actuales siguen siendo hipótesis pendientes de playtesting diverso.

## Ejecutar

Requiere [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

### Interfaz Web

En Windows, haz doble clic en `INICIAR_INTERFAZ.cmd`, o ejecuta:

```powershell
dotnet run --project src/ManiaAddNotesLab.Web
```

Después abre `http://127.0.0.1:5178`. No abras `wwwroot/index.html` directamente: la interfaz necesita el servidor local. Permite cargar un `.osu`, barrer chances/seeds, modificar controles experimentales y descargar resultados de un lote.

### CLI

```powershell
dotnet run --project src/ManiaAddNotesLab.Cli -- samples/01-rice-4k.osu --chance 0.30 --seed 12345
```

Ejemplo con trace y reporte CSV:

```powershell
dotnet run --project src/ManiaAddNotesLab.Cli -- samples/04-release-chord-4k.osu --chance 1 --seed 7 --trace --report experiments.csv
```

La ayuda de la CLI enumera las opciones A/B y las exportaciones de evidencia/diagnóstico:

```powershell
dotnet run --project src/ManiaAddNotesLab.Cli -- --help
```

## Verificar

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet run --project tools/DocConsistency -- --check
```

## Leer más

- [Estado actual](PROJECT_STATUS.md)
- [Roadmap resumido](ROADMAP.md)
- [Arquitectura](ARCHITECTURE.md)
- [Índice completo de documentación](DOCUMENTATION_INDEX.md)
- [Diseño técnico implementado](docs/DESIGN.md)
