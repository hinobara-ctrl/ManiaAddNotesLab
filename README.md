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
- **Phase D1.0 — COMPLETE, OUTCOME A, SHADOW ONLY:** confirmó que soporte marginal no equivale a una composición conjunta observada.
- **Phase D1.GATE — COMPLETE, OUTCOME READY, SHADOW ONLY:** dejó congelado y verificable el contrato de un posible D1, sin ejecutar el A/B ni cambiar generación.
- **Phase D1 — COMPLETE, OUTCOME C, EXPERIMENTAL ONLY:** el A/B congelado se ejecutó y abortó por no poder atribuir de forma válida el chequeo de overlap; la rama quedó aparcada y no fue promovida.
- **Phase D1.SAFETY — COMPLETE, OUTCOME C, SHADOW ONLY:** separó raw relation, hard violation y atribución causal, pero el forensic prototype intentó atribuir una diferencia final sin mutation provenance; hard abort y framework PARKED.
- **SAFETY.PROV — COMPLETE, OUTCOME A, SHADOW ONLY:** captura decisiones/mutaciones durante generation con lineage exacta, reconvergencia y serialización, manteniendo bytes/RNG/decisiones idénticos OFF/ON.
- **G1.0 — COMPLETE, OUTCOME A, RECERTIFIED, SHADOW ONLY:** censó relaciones interiores completas y ahora audita independientemente el donor set con bad controls adversariales; C11 mantiene cero leakage real y los mismos agregados.
- **G1.DESIGN — COMPLETE, READY, RECERTIFIED, SHADOW ONLY:** membership exacta reproducida sobre 4.226/4.226 shapes del candidate-builder; 133 son members hipotéticos, no placements post-geometry ni efectos conductuales. La API es estructuralmente RNG-free y no existe selector.
- **G1.GATE — COMPLETE, NEEDS_REVIEW, NO PROMOTION:** el treatment runtime congelado suprimió 889/1.006 propuestas sin RNG/reroll, pero SAFETY.PROV atribuyó nueve `TapOnHeldLongNote` nuevos a efectos downstream; se activó el stop condition.
- **SAFETY.CAUSAL — COMPLETE, OUTCOME A, FORENSICS ONLY:** el replay exacto demuestra que los 9 treatment-only y los 200 controles legacy comparten un mismatch entre el `EndBeat` decimal latente usado por placement y el beat reconstruido desde milisegundos usado por HardValidity. Causa encontrada; remediation y promoción no autorizadas.
- **SAFETY.REMEDIATION.DESIGN — COMPLETE, READY_FOR_SEPARATE_REMEDIATION_GATE, RESEARCH ONLY:** seleccionó geometría jugable canónica compartida, cubrió en shadow los 209 casos conocidos y halló 6 deltas directos adicionales. No implementó la corrección; el gate conductual separado sigue sin autorización.
- **SAFETY.REMEDIATION.GATE — COMPLETE, FORENSIC ATTRIBUTION NEEDS_REVIEW / NO PROMOTION:** la única matriz final confirmó cero violaciones treatment y midió inicialmente `188 A + 21 B + 6 C`; la consolidación oficial preservó cinco mecanismos D ya demostrados y cerró `188 A + 21 B + 5 D + 1 C_UNRESOLVED`. La investigación focal reprodujo las 70.835 observaciones como 8 episodios persistentes iniciados por remapping de selección al reducirse el lane set, fuera de la definición causal congelada.
- **SAFETY.SELECTION-SET-REMAPPING — COMPLETE, E_DEMONSTRATED LOCAL ONLY:** un contrato separado reprodujo con el selector auténtico las 8/8 primeras divergencias usando el mismo prefijo y una llamada RNG. E no demuestra la cadena completa a OP-466, no cambia A/B/C/D y no autoriza promoción.
- **SAFETY.OP185→OP466 — COMPLETE, ABSENT_BOTH:** la intervención única preregistrada cambió lane 2→3 en OP-185, pero ninguno de los dos brazos canónicos produjo ni comprometió el target OP-466.
- **SAFETY.OP466.CANDIDATE_ADMISSION — COMPLETE, MECHANISM_IDENTIFIED:** canonical A/B consumen el mismo roll auténtico `0.7305439518441185` con chance `0.5` y se abstienen antes de construir candidato; es la causa inmediata observada.
- **SAFETY.OP466.RNG_GEOMETRY_AUDIT — COMPLETE, HANDOFF ONLY:** las 30 filas explican `+8` llamadas antes de OP-466, `+1` durante OP-466 y `+9` después; en ese momento los artifacts previos no permitían reconstruir geometría A/B completa.
- **SAFETY.OP466.GEOMETRY_OBSERVATION — COMPLETE, RESEARCH ONLY:** el contrato oficial v3 reconstruyó ambos padres exactos; lane 2 es ilegal en A y B por una LN sintética que termina inclusivamente en 35009 ms. Es una barrera hipotética adicional, no la causa de la abstención ni una prueba de selección/commit contrafactual.
- **LANE.0 — HISTÓRICO COMPLETE / FEASIBILITY_DEMONSTRATED, CERTIFICACIÓN GLOBAL SUSPENDED / PENDING_RECERTIFICATION:** el resultado de `12ee879` conserva 37.080/40.360 supports rice y 3.373/16.881 G1, incluidos 11/288 operativos, pero no se presenta como certificación vigente tras confirmarse el defecto future-held. No existe selector ni sucesora autorizada.
- **LANE.0.REMEDIATION — COMPLETE / HISTORICAL PREPARATION:** el builder G1 histórico no transportaba `AnchorTime` ni identities future-held. El instrumento restauró ambas garantías y las cifras anteriores se conservan como históricas; el intento correctivo posterior no produjo recertificación publicable.
- **LANE.0.HARDENING — COMPLETE / F03×F07 CLOSED:** separó exclusiones temporales e identitarias y congeló la ruta one-shot. El intento oficial ejecutado sobre `243c43a` consumió correctamente su autoridad y terminó `INVALID`; el hardening de ejecución permanece cerrado.
- **LANE.0 counter closure — LOCAL RECERTIFICATION READY:** `UniqueFutureHeldExcluded` representa la unión sin doble conteo y un contrato sucesor conserva v2 mientras normaliza identidades de código frente a CRLF/LF. No cambia resultados ni autoriza C11.
- **LANE.0 corrective evaluation preparation — HISTORICAL PREREGISTRATION:** el preregistro, snapshots y templates describen lo realmente congelado y ejecutado; se preservan sin reescribir como evidencia histórica.
- **LANE.0 corrective evaluator core — PUBLISHED / RESEARCH-ONLY / ATTEMPT CONSUMED:** el núcleo publicado en `d005534` fue ejecutado una vez bajo la autoridad oficial y produjo `INVALID / NOT PUBLISHABLE`.
- **LANE.0 post-attempt forensics — DOCUMENTED / SCI-01 RECORDED / NO RETRY:** la referencia Spring congelada no pertenece al manifest público; es una causa suficiente no exclusiva del `INVALID`. El binding y receipt consumidos se preservan, no hay promoción y ningún successor está preregistrado o autorizado.

La generación default continúa en `legacy-experimental.1`; el perfil sigue en `phase-a.1` y diagnostics en `phase-c1-2-shadow.1`. G1.GATE y SAFETY.REMEDIATION.GATE existen sólo como treatments research explícitos y no están expuestos en CLI/Web. La recertificación final conserva `NEEDS_REVIEW`: seguridad observada 215→0, pero causalidad final `188 A + 21 B + 5 D + 1 unresolved`. G1 de utilidad, G2/H y cualquier sucesora siguen sin autorización.

<!-- PROJECT-STATE:BEGIN -->
Current phase: LANE.0.HARDENING — COMPLETE — OUTCOME READY_FOR_PUBLICATION_REVIEW<br>
Next actionable research candidate: none<br>
Next actionable authorization: N/A<br>
Next behavioral phase: none<br>
Next behavioral authorization: N/A<br>
Blocked prerequisite: F2.ACQ — BLOCKED<br>
Research branch: F2 — CONTINUE_CONDITIONALLY<br>
Behavior policy: `legacy-experimental.1`<br>
Behavior change: none<br>
Evidence profile: `phase-a.1`<br>
Diagnostic schema: `phase-c1-2-shadow.1`<br>
Tests: 921 passed / 0 failed / 0 skipped
<!-- PROJECT-STATE:END -->

Este repositorio es un laboratorio de investigación, **no un algoritmo final ni una release lista para usuarios**. Varias políticas actuales siguen siendo hipótesis pendientes de playtesting diverso.

`LANE.0` conserva su cierre histórico, pero su certificación global está **SUSPENDED / PENDING_RECERTIFICATION**. El único intento correctivo oficial está **CONSUMED / INVALID / NOT PUBLISHABLE** y no admite retry. La reparación y el hardening no eligieron lane ni introdujeron frecuencia, score, selector o conducta. Un eventual successor aún no está diseñado, preregistrado ni autorizado; `LANE.DESIGN`, `LANE.GATE` y cualquier integración siguen sin autorización.

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
