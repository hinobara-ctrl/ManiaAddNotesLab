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

La generación activa continúa en `legacy-experimental.1`; el perfil sigue en `phase-a.1` y diagnostics en `phase-c1-2-shadow.1`. Phase A, Phase B y C1–F2.1 no gobiernan el selector. F2.1 conservó FileExact y probó una traversal exacta: ambas separan una relación nominal que se serializa 250/249 ms, y la traversal también separa un boundary BPM redundante. Fusionarlas requeriría una hipótesis de quantization no codificada en `.osu`; no existe policy conductual. F2.2 es sólo una recomendación no autorizada; D1 tampoco está autorizado y C2 permanece deferred.

<!-- PROJECT-STATE:BEGIN -->
Current phase: F2.1 — COMPLETE — OUTCOME C — SHADOW ONLY<br>
Next recommended phase: F2.2 — Quantization Inference Feasibility / Research Design<br>
Behavior policy: `legacy-experimental.1`<br>
Evidence profile: `phase-a.1`<br>
Diagnostic schema: `phase-c1-2-shadow.1`<br>
Tests: 378 passed / 0 failed / 0 skipped
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
