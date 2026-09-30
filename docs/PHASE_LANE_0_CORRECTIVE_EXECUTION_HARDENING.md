# LANE.0 — Corrective execution final hardening

## Estado

`READY FOR MANUAL PUBLICATION AND FINAL INDEPENDENT AUDIT / BINDING V2 NOT YET ISSUED / C11 EXECUTION BLOCKED`

Baseline publicado: `b3a87c5420371af212ff5f0512e5137dffb1e2bc`; parent: `48e8df0564c7ece712b19a2e4d3de1e51034c36e`. Este successor responde a la auditoría F01–F06 sin modificar la preparación histórica, el evaluator, la solución congelada, Core científico, defaults ni conducta.

No se accedió a C11, no se abrió un `.osu` real y no se emitió un publication binding.

## Remediaciones F01–F06

### F01 — identidad de los bytes evaluados

El adapter hardened resuelve el descriptor congelado y, para cada chart, realiza una sola lectura binaria. Calcula SHA-256 sobre esos bytes, exige igualdad con `descriptor.Sha256`, decodifica esos mismos bytes con UTF-8 estricto y sólo entonces parsea. Keymode y object count se validan adicionalmente, pero ya no pueden sustituir a la identidad de contenido.

La segunda evaluación vuelve a resolver, leer, hashear y parsear de forma independiente. Un chart B con el mismo keymode y object count que A es rechazado si sus bytes no corresponden al SHA de A.

### F02 — runtime identity gate

Antes de cualquier manifest/corpus, el runtime valida:

- canonical SHA del contrato successor;
- HEAD Git;
- ausencia de modificaciones tracked staged y unstaged respecto de HEAD;
- identidades parent publicadas;
- solución congelada;
- tree identities del evaluator y harness;
- runtime scientific tree;
- implementación hardened y official runner.

Los untracked no invalidan `tracked-clean`, permitiendo que el binding futuro permanezca evidencia separada. La autoridad declarada es published source + official `dotnet run` bajo tracked-clean; no se afirma reproducible build ni firma binaria.

El inventario runtime cubre evaluator y `Lane0FeasibilityRunner`; modelo/parser/timeline; mapper profile y análisis; RICE/exact-head/composition; G1 feasibility/holdout/membership; geometría/engine; manifest y corpus resolver. Cada archivo contiene código o tipos transitivamente usados por la evaluación o por su gate de población.

La auditoría distinguió explícitamente:

- hash normalizado del archivo evaluator: `1719D85C540D683C5CDAC2018343ABBE1AF86F961E21A1DA743BEE52EBF61356`;
- tree identity `path|fileHash`: `20D0AC6BB0AF197ACC6BE1B5E71D1DF65BD2CDC0FCAB0E49FD6F036D03A0D8F4`.

Por tanto, el supuesto mismatch era falso y no se cambió ninguna identidad histórica.

### F03 — intento consumible

Después de todos los guards pre-corpus, manifest público y output readiness, el runner crea `.artifacts/lane_0_corrective.attempt.json` mediante `FileMode.CreateNew`. El receipt contiene schema, HEAD aprobado, SHA de los bytes exactos del binding, SHA del contrato hardened y count uno; no contiene timestamps, GUID, host ni rutas absolutas.

El receipt consume un **intento**, no un resultado exitoso. Nunca se borra automáticamente. Si adapter, evaluación o escritura fallan, permanece y una segunda invocación queda `BLOCKED` antes del corpus.

### F04 — publicación atómica

El hardened runner no usa `WriteArtifacts`. Construye dos paquetes completos de seis archivos a partir de `SerializeArtifacts`, agrega `sha256sums.txt` determinístico y compara ambos paquetes byte a byte.

Tras la igualdad, crea staging, escribe los seis archivos, verifica nombres exactos, ausencia de extras e identidad byte a byte, y finalmente publica mediante `Directory.Move(staging, final)`. Un fallo deja final ausente; staging parcial y receipt permanecen como evidencia, sin cleanup ni retry.

### F05 — superficie oficial

Se creó un proyecto console aislado no registrado en la solución:

```powershell
dotnet run --project tools/ManiaAddNotesLab.CorrectiveExecution/ManiaAddNotesLab.CorrectiveExecution.csproj -c Release -- --repo-root . --corpus-root <EXPLICIT_FROZEN_C11_ROOT>
```

La CLI acepta exclusivamente repo root y corpus root explícito. Binding, contrato, manifest, final, staging y receipt se derivan internamente de rutas canónicas. El runner invoca sólo `Lane0CorrectiveExecutionHardening.Execute`; nunca invoca la ruta histórica `Lane0CorrectiveExecutionPreparation.Execute`.

### F06 — output readiness

Antes del manifest/corpus y antes de consumir el intento, final, staging y receipt deben estar ausentes. Un archivo o directorio en cualquiera de esos paths produce `BLOCKED`, sin llamadas al adapter. La nueva ruta no acepta siquiera un final vacío.

## Binding v2 y orden del gate

El schema futuro es `lane-0-corrective-evaluation-publication-binding.2`, con ocho propiedades exactas: schema, HEAD, contrato hardened, preregistro, evaluator hardening, manifest, autorización humana y count. Binding v1, extras, faltantes o hashes malformados son rechazados.

El orden congelado es A–AA: contrato; HEAD; tracked-clean; runtime science; implementación/runner; binding; schema; contract hash; parent hashes; HEAD binding; autorización; count uno; final/staging/receipt ausentes; manifest público; receipt `CreateNew`; corpus explícito; byte hash+parse; inputs ordinales; segunda carga independiente; dos evaluaciones; dos paquetes; comparación; staging; verificación; rename final.

No se toca corpus antes de crear el receipt en Q.

## Identidades successor

- contract canonical: `AABF22D6ED17FEC1D62A83C23C164DABBF9A0D98A7347E2AB5172A7ECB5B372B`;
- runtime scientific tree: `CD0FCFA43EC457CF2B49B07F1F18CDFB1515D112AEDA6C579A128B6630FF6BD8`;
- hardening implementation tree: `41362B4685B345A0BB1FB89E0874EBF48886AD70D969866E106C9C0820D64BF1`;
- official runner tree: `96DB4A9EA0D12A8A60D69B47F327D4EA93B2551161C1CA59D2BC4EA6A329AC8D`;
- hardening harness tree: `FFCF02548CD8198B2AB45DD7C8C030779663688DC5783EF79FE9D5DFE54DFCE3`;
- verifier normalized file: `35EFC2F99D9C958E4F751CB2A468BB02330C80972BE11954FEFB4554E4C785BA`.

## Validación

Con heap limitado a 16 GiB, conservación 9 y ejecución secuencial:

- restore solución: PASS;
- build solución Release: PASS, 0 errores;
- suite congelada: 921 passed, 0 failed, 0 skipped;
- evaluator directo: 53 passed, 0 failed, 0 skipped;
- execution-preparation histórico: 31 passed, 0 failed, 0 skipped;
- execution-hardening successor: 37 passed, 0 failed, 0 skipped;
- dedicated runner build: PASS;
- smoke sin binding y corpus inexistente: `BLOCKED` antes de corpus;
- DocConsistency: PASS;
- evaluator hardening verifier: PASS;
- execution-preparation verifier: PASS;
- execution-hardening verifier: PASS.

`NU1900` fue únicamente la indisponibilidad del feed remoto de vulnerabilidades de NuGet y no afectó build ni tests.

## Estado científico

No ocurrió ejecución correctiva ni se produjeron conteos, OccurrenceIds o artifacts científicos C11. El estado sigue siendo:

`LANE.0 HISTORICAL FEASIBILITY_DEMONSTRATED / GLOBAL CERTIFICATION SUSPENDED / PENDING_RECERTIFICATION / C11 NOT AUTHORIZED / NO BEHAVIORAL PROMOTION`.

El binding canónico permanece ausente. Este informe no afirma que la auditoría independiente final haya ocurrido y no autoriza recertificación, LANE.DESIGN ni etapas posteriores.
