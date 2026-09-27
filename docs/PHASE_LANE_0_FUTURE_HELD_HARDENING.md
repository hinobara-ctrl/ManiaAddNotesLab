# LANE.0 — Future-held hardening previo a recertificación

**Fecha de referencia y ejecución:** 27-09-2026  
**Baseline publicado verificado:** `9a8d28f311dc07dc685cc92a23c76b508323fc25`  
**Remoto:** `origin/main` verificado por `fetch`, `ls-remote` y divergencia `0/0`  
**Estado:** instrumentación endurecida; evaluación correctiva C11 **NO AUTORIZADA**

## Separación histórico/vigente

El commit `12ee8799528d9cf9d64d8d9a9ab4b45ba955db0f` conserva el resultado histórico `COMPLETE / FEASIBILITY_DEMONSTRATED`. Sus cifras, contrato y artifacts no se reescriben. El commit `9a8d28f311dc07dc685cc92a23c76b508323fc25` publicó la primera reparación future-held. El defecto G1 confirmado suspende la certificación global vigente: **SUSPENDED / PENDING_RECERTIFICATION**.

Este hardening no consulta C11, no produce nuevos agregados, no cambia `AddNotesEngine`, `LaneGeometryIndex`, RNG, geometría productiva, defaults ni `legacy-experimental.1`. Paso 2 correctivo C11: **NO AUTORIZADO**. LANE.DESIGN/GATE, G2/H y toda promoción continúan no autorizados.

## Integridad G1

La occurrence G1 corregida exige `AnchorTime`. Un target o donor G1 sin tiempo se clasifica como integridad ambigua y obliga a `INVALID` en el flujo correctivo. Para cada target se mantienen dos exclusiones independientes:

1. `temporal_exclusion`: `donor.AnchorTime >= target.AnchorTime`;
2. `future_held_identity_exclusion`: `donor.ObservationIds ∩ target.FutureHeldObservationIds != ∅`.

Un donor puede infringir ambas. `both_reasons` lo registra, pero `unique_excluded` sólo aumenta una vez. El invariante por consulta es:

`admitted + unique_excluded == donors_considered`

Las identities future-held proceden únicamente de `CompleteRelations` originales same-or-later y conservan parent, witness, head y release. No se unen fragments marginales para fabricar una relación.

## Índice temporal y coste

El builder dejó de recorrer todas las relaciones para cada occurrence. Agrupa una vez las relaciones completas por `AnchorTime`, recorre los anchors en orden descendente y construye snapshots acumulativos ordenados una vez por tiempo distinto. Occurrences del mismo anchor comparten el mismo `ImmutableArray` subyacente. El fixture acotado compara cada snapshot con una implementación sencilla y obtiene igualdad exacta, sin cargar C11.

## Ruta correctiva independiente

- `lane-0-run` conserva deliberadamente el builder y la semántica histórica defectuosa; sólo sirve para reproducción aislada con sus identidades originales.
- `lane-0-corrective-hardening-prepare` crea exclusivamente el contrato v2; no abre manifest ni corpus.
- `lane-0-corrective-validate` verifica contrato canónico, inventarios, dependencias, binding de publicación, HEAD aprobado, autorización humana y finalmente manifest.
- `lane-0-corrective-execute` está presente como guard y siempre se detiene antes de C11 en este paso. Incluso con readiness completo, informa que el cuerpo experimental requiere runner publicado y autorización separada.

La autorización futura vive en `docs/lane_0_corrective_publication_binding.json`, archivo deliberadamente inexistente. Así el contrato del mismo commit no contiene un HEAD autorreferencial. Ausencia de binding, HEAD, autorización o manifest produce `BLOCKED`; drift de contrato/implementación/harness/dependencias produce `INVALID`. `FEASIBILITY_DEMONSTRATED` y `LIMITED_PARK` quedan reservados para una única evaluación futura legítima.

## Identidades y canonicalización

Antes del hardening se recalcularon las identidades publicadas v1:

| Identidad v1 | Declarada | Calculada | Método | Estado |
|---|---|---|---|---|
| Contrato | `F28F35AA991F3AF20BEEBBEE6AC1D9D3C182E71C62E4044E4DB0792857F8B0A7` | igual | round-trip tipado `System.Text.Json`, camelCase, sin indentación | VERIFIED |
| Implementación | `A69C084D6631E24B49E66229B5D666E1B8F74BAD306FFE1CAF7BEC9619BBF0B4` | igual | inventario ordenado `ruta/normalizada|SHA256(bytes)` y SHA-256 UTF-8 del conjunto | VERIFIED |
| Harness | `6B1FE0A10C62C6E2D155A2CF279791B5CD431F2F13A6653F4B498696AC0FB49E` | igual | mismo algoritmo sobre cuatro archivos declarados | VERIFIED |

Los hashes de archivos usan bytes reales. Un cambio LF↔CRLF altera el SHA individual y, por tanto, la identidad de árbol. El JSON canónico es semántico: whitespace externo no cambia el hash después del round-trip, pero sí lo hace cualquier propiedad o valor.

El contrato v1 permanece congelado. Este hardening emite `lane-0-future-held-hardening.2` con nuevas identidades:

- contrato canónico: `E0BCDA19B3E05E5ECA3EE8FCC7380C4AC74ACBDDE3241D77CD2F1B6EA6274F35`;
- implementación: `63F8897F39370D41260CEF962E20F82D288F502EA0AEE8AB82C3C1088F92B82E`;
- harness: `B4C579411846085DAC6C977BD6D734528358C3A1E103D097D4EA46273FDBD212`.

No se verificaron SHA individuales de `.osu` privados; el manifest sólo podrá verificarse materialmente durante el futuro paso autorizado.

## Formato futuro, sin resultados

`lane_0_corrective_comparison_template.csv` separa chart, familia, keymode, universos A/B/C, support histórico/corregido, sentinel rice y placements. `lane_0_corrective_g1_operational_template.csv` reserva una fila por cada uno de los 11 IDs G1 históricamente soportados con donors considerados/admitidos, exclusiones temporales, por identidad, doble motivo, exclusión única y estado. Ambos archivos contienen sólo headers; no se inventan IDs ni valores.

La población futura permanece congelada, pendiente de verificación material: 11 contenidos únicos, 12 rutas, 11 familias, 4K/7K/10K y 50.836 objetos.

## Validación ejecutada

Con `DOTNET_GCHeapHardLimit=0x400000000`, `DOTNET_GCConserveMemory=9` y ejecución secuencial:

- `dotnet restore ManiaAddNotesLab.sln --disable-parallel`: correcto; único aviso `NU1900` por consulta de vulnerabilidades NuGet inaccesible;
- `dotnet build ManiaAddNotesLab.sln -c Release -m:1 --no-restore`: correcto, 0 errores;
- tests focalizados LANE.0: 28/28;
- suite completa: **860 passed / 0 failed / 0 skipped**;
- DocConsistency: `PASS`;
- `git diff --check`: correcto; sólo avisos informativos de conversión LF→CRLF futura.

Los bad controls de readiness producen `INVALID` ante cualquier identidad/dependencia falsa y `BLOCKED` ante binding, HEAD, autorización o manifest ausente. Las invocaciones reales de validación y ejecución con binding/manifest ausentes devolvieron `BLOCKED` y código 1 antes de acceder a C11. No se ejecutó evaluación correctiva.

## Exposición por archivos

Un `git archive` completo incluiría `docs/ASTRA_POST_SAFETY_PROV_STRATEGIC_REVIEW.md` y dieciséis fixtures `.osu` versionados bajo `samples/`. Los cinco archivos personales ASTRA/HANDOFF adicionales continúan untracked y no entrarían en el archive salvo que alguien los añadiera explícitamente. `.artifacts/` no está rastreado y requiere respaldo externo. Este trabajo no movió, añadió, borró ni redistribuyó ninguno de esos archivos.

## Stop

Después de tests y consistencia documental, el trabajo se detiene. Falta publicación manual del hardening, auditoría del nuevo HEAD, creación separada del binding y autorización expresa de Paso 2. No existe resultado C11 corregido.
