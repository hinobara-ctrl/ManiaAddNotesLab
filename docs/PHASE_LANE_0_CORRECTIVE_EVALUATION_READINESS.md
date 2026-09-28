# LANE.0 — readiness previo a la evaluación correctiva C11

**Fecha de preparación:** 27-09-2026  
**Estado:** `READY_FOR_MANUAL_PUBLICATION_AND_INDEPENDENT_AUDIT / EXECUTION BLOCKED`  
**C11:** `NOT_AUTHORIZED / NOT_ACCESSED / NOT_EXECUTED`  
**Conducta:** `RESEARCH ONLY / ORIGINAL ONLY / NO BEHAVIORAL PROMOTION`

## Baseline y preflight

La preparación comenzó en `main` con `HEAD=f40e4d3e4be29909c54be33528d4e167f4593dbe`. El baseline instrumental original `c471d10ed48e42ab33b8a981ef22c6eb26774f3d` es ancestro de ese HEAD. El working tree estaba limpio salvo cinco documentos personales `ASTRA/HANDOFF` no rastreados, que se conservaron sin modificación. `git diff --check` era correcto.

`git fetch origin main` y `git ls-remote origin refs/heads/main` fallaron porque GitHub no respondió por el puerto 443. La referencia local cacheada `origin/main` coincidía con `f40e4d3`, pero **no se presenta como verificación remota**. Por ello la verificación en vivo del SHA publicado permanece como prerrequisito pendiente y condición de bloqueo.

No se ejecutó `pull`, `reset`, `rebase`, `git add`, commit, push, tag ni operación destructiva.

## Cierre instrumental v3 verificado

El verificador independiente existente se ejecutó con PowerShell 7, el runtime compatible con `System.Text.Json`. Tanto el working tree como los blobs de `HEAD` coincidieron en:

- contrato v3: `221D5133D8058FEBEAEA0A13F83058219D900261B033282EE389A82BA20FAAF7`;
- implementación: `2F938F18D98C6299214C080195633A68B68C688CABF49DC0F4DEFFCA9C1897D9`;
- harness: `1EFC2B824FAA6F0798C3FFA679FCA073939D88B2C751612C06BB75C240E96309`;
- seis dependencias normalizadas: coincidentes en working tree y `HEAD`.

El procedimiento sigue separando hash canónico del contrato, identidad de código UTF-8/LF normalizada, hash de commit, hash exacto de bytes y manifest del corpus.

## Preregistro preparado

`docs/lane_0_corrective_evaluation_preregistration_contract.json` es un contrato separado de los contratos históricos e instrumentales. Su SHA-256 canónico es:

`6392C579B87EC318C1DE381201D797B004650AB961398E3E2AB23F7A49A83D33`

La identidad del harness preparatorio es:

`AC1DD7469AC2876B1A31226DDB311CDF74F5D64ABEACC6C442C461F5F87964E3`

El contrato congela antes de observar C11:

- pregunta correctiva y preguntas excluidas;
- familias exactas `RICE_HEAD_COMPLETION` y `G1_INTERIOR_SPATIAL`;
- universos A, B y C, unidad de análisis y denominadores separados;
- queries completas sin mirror, translation ni equivalencia cross-keymode;
- snapshot histórico, métricas adicionales no excluyentes y templates con columnas correctivas vacías;
- semántica sin doble conteo para `TemporalExclusion`, `FutureHeldIdentityExclusion`, `BothTemporalAndIdentity`, `UniqueFutureHeldExcluded` y `UniqueExcluded`;
- controles adversariales, sentinel rice, clasificación y stops;
- manifest C11 esperado `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445` como referencia contractual, no como verificación ejecutada;
- orden obligatorio: preregistro → validación → congelación → publicación manual → auditoría independiente → autorización humana → una evaluación.

No se introdujo ningún umbral. El criterio científico se reutiliza literalmente del contrato histórico: soporte conjunto independiente para cada familia en al menos dos charts y dos keymodes, al menos un target operativo soportado, integridad cero, RNG cero, no interferencia y repetición byte-idéntica.

## Universos y snapshot histórico

| Familia | A/B | Contexto | Soporte conjunto | Unique | Alternativas | Contradicción | No-context | C operativo | C soportado |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Rice | 40.360 | 39.596 | 37.080 | 693 | 36.387 | 2.516 | 764 | 40.360 | 37.080 |
| G1 | 16.881 | 9.540 | 3.373 | 192 | 3.181 | 6.167 | 7.341 | 288 | 11 |

Estas cifras permanecen exclusivamente históricas (`12ee879`). Las columnas correctivas de `lane_0_corrective_evaluation_comparison_template.csv` están vacías.

## Once casos G1 históricos

Los artifacts agregados permitidos localizan los once soportes operativos históricos en dos charts 7K:

- 9 casos en `20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788`;
- 2 casos en `E73B098A4D4C99D716D2C53B3EA3A4BFD1A75059881F30DB416631B5EDB972D4`.

Los artifacts históricos publicados son agregados y **no emitieron los once `OccurrenceId` exactos**. No se inventaron IDs y no se abrió C11 para reconstruirlos. `lane_0_corrective_g1_case_template.csv` contiene once slots vinculados a chart, familia, keymode y ordinal histórico; la evaluación futura autorizada deberá reconstruir la identidad exacta, comprobar la pertenencia a los once soportes históricos y publicar para cada uno donors considerados/legítimos, las cuatro métricas future-held, exclusión única total, soporte superviviente y estado final.

Esta carencia es un riesgo instrumental y una condición de interrupción si la identidad histórica no puede reconstruirse de forma determinista durante la ejecución autorizada.

## Controles sintéticos y no interferencia

Cinco tests preparatorios nuevos comprueban:

- estructura e identidad canónica del preregistro;
- cifras históricas y columnas correctivas vacías;
- target, target-group, parent, release, temporal same-or-later, colisión future-held previa, ambas causas, same-event, synthetic, cross-chart y composición artificial;
- admisión de un donor anterior independiente;
- partición y unión future-held sin doble conteo;
- dos construcciones G1 byte-semánticamente equivalentes, RNG cero y fingerprint del chart intacto;
- sentinel rice no afectado por la exclusión temporal específica de G1.

Los controles positivos fueron rechazados y el control negativo legítimo fue admitido. No se usaron observaciones sintéticas como evidencia estilística.

## Validación local

Con `DOTNET_GCHeapHardLimit=0x400000000`, `DOTNET_GCConserveMemory=9` y ejecución secuencial:

- `dotnet restore ManiaAddNotesLab.sln --disable-parallel`: correcto; sólo `NU1900` por indisponibilidad de NuGet;
- `dotnet build ManiaAddNotesLab.sln -c Release -m:1`: correcto, 0 errores;
- `dotnet test ManiaAddNotesLab.sln -c Release -m:1`: **868 passed / 0 failed / 0 skipped**;
- `dotnet run --project tools/DocConsistency -c Release -- --check`: `PASS`;
- verificador v3 independiente: `PASS`;
- verificador independiente del preregistro: `PASS`;
- `git diff --check`: `PASS`.

El aumento 863→868 corresponde exclusivamente a cinco tests sintéticos nuevos. No se ejecutó la matriz histórica de 224 pares ni una evaluación C11.

## Ruta futura y bloqueo efectivo

La preparación no modificó `Lane0FeasibilityRunner`, `Program.cs`, `AddNotesEngine`, `LaneGeometryIndex`, políticas productivas, RNG, AddChance, selección de columnas, treatments, defaults ni `legacy-experimental.1`. Al conservar intactos todos los archivos de las identidades v3, esas identidades siguen verificables en working tree y `HEAD`.

No existe cuerpo de ejecución correctiva, binding de publicación, token, variable de entorno ni argumento CLI que autorice C11. El contrato declara:

- `evaluationBodyPresent=false`;
- `authorizationBindingPresent=false`;
- `humanAuthorizationGranted=false`;
- `currentState=BLOCKED`.

Después de publicación manual y auditoría del nuevo SHA, una tarea separada deberá diseñar el cuerpo mínimo de evaluación sin alterar los criterios. Esa tarea deberá detenerse antes de acceder a C11 hasta recibir autorización humana explícita para una única ejecución.

## Estado verdadero y prerequisitos pendientes

- `LANE.0 HISTORICAL FEASIBILITY_DEMONSTRATED`;
- `GLOBAL CERTIFICATION SUSPENDED`;
- `PENDING_RECERTIFICATION`;
- `C11 NOT AUTHORIZED`;
- `NO BEHAVIORAL PROMOTION`.

Pendiente antes de cualquier evaluación: recuperar conectividad y verificar el SHA remoto real; revisión humana de este preregistro; congelación/publicación manual; auditoría independiente del commit publicado y de las identidades del harness preparatorio; resolución reproducible de las once identities históricas; implementación y auditoría separadas del runner correctivo; autorización humana explícita; manifest y once hashes individuales sólo después de dicha autorización.

No se emite clasificación científica nueva. El estado actual es preparación local válida para revisión, con ejecución `BLOCKED`.
