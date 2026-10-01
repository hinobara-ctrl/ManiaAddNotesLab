# LANE.0 — Corrective execution final pre-binding hardening

## Estado

`READY FOR MANUAL PUBLICATION AND FINAL INDEPENDENT AUDIT / NEW BINDING V2 NOT YET ISSUED / C11 EXECUTION BLOCKED`

Baseline publicado de F03×F07: `72bffacbe483d01a26743a18185bac37a7f4ef19`; parent: `53adc67627a88aa0e8f47adcfc0adbebb29f0f76`. El baseline histórico declarado por el contrato permanece deliberadamente en `b3a87c5…`. Este successor repara exclusivamente la autoridad durable del intento sobre el estado F01–F09 publicado sin modificar la preparación histórica, el evaluator, la solución congelada, Core científico, defaults ni conducta.

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

Los untracked no invalidan `tracked-clean`, permitiendo que el binding futuro permanezca evidencia separada. F07 separa además el checkout de desarrollo del checkout efectivo de build y ejecución; no se afirma reproducible build ni firma binaria.

El inventario runtime cubre evaluator y `Lane0FeasibilityRunner`; modelo/parser/timeline; mapper profile y análisis; RICE/exact-head/composition; G1 feasibility/holdout/membership; geometría/engine; manifest y corpus resolver. Cada archivo contiene código o tipos transitivamente usados por la evaluación o por su gate de población.

La auditoría distinguió explícitamente:

- hash normalizado del archivo evaluator: `1719D85C540D683C5CDAC2018343ABBE1AF86F961E21A1DA743BEE52EBF61356`;
- tree identity `path|fileHash`: `20D0AC6BB0AF197ACC6BE1B5E71D1DF65BD2CDC0FCAB0E49FD6F036D03A0D8F4`.

Por tanto, el supuesto mismatch era falso y no se cambió ninguna identidad histórica.

### F03 — intento consumible

**F03 REOPENED BY F07 ISOLATION.** La implementación anterior derivaba `.artifacts/lane_0_corrective.attempt.json` desde el `ExecutionRoot` aislado. Un primer checkout E1 podía consumir su receipt y fallar, pero un segundo checkout E2 no observaba ese archivo y podía cruzar nuevamente `FileMode.CreateNew`; por tanto `authorizedExecutionCount = 1` no era durable entre execution roots.

La corrección separa `ExecutionRepositoryRoot` de `AuthorizationRoot`. Después de autorización, output readiness y manifest público, el runner crea exclusivamente `<AuthorizationRoot>/.artifacts/lane_0_corrective.attempt.json` mediante `FileMode.CreateNew`, `FileAccess.Write`, `FileShare.None` y `Flush(true)`. El receipt contiene schema, HEAD aprobado, SHA de los bytes exactos del binding, SHA del contrato hardened y count uno; no contiene timestamps, GUID, host ni rutas absolutas.

El receipt consume un **intento**, no un resultado exitoso. Nunca se borra, mueve, rearma ni reintenta automáticamente. Si E1 falla después del receipt, el receipt fuente permanece. E2, aunque sea un checkout aislado diferente, usa el mismo `AuthorizationRoot`, observa exactamente el mismo receipt y queda `BLOCKED` antes del adapter y de C11. El alcance es la raíz de autorización canónica y su ceremonia de binding; no pretende resistir una copia deliberada a otra máquina ni un host, administrador o filesystem hostil.

### F04 — publicación atómica

El hardened runner no usa `WriteArtifacts`. Construye dos paquetes completos de seis archivos a partir de `SerializeArtifacts`, agrega `sha256sums.txt` determinístico y compara ambos paquetes byte a byte.

Tras la igualdad, crea staging, escribe los seis archivos, verifica exactamente seis entradas, exige que cada entrada sea un archivo regular con nombre permitido e identidad byte a byte, y finalmente publica mediante `Directory.Move(staging, final)`. Un fallo deja final ausente; staging parcial y receipt permanecen como evidencia, sin cleanup ni retry.

### F05 — superficie oficial

El proyecto console continúa aislado y no registrado en la solución. Desde F07 sólo se invoca mediante el launcher versionado:

```powershell
pwsh -NoProfile -File tools/InvokeLane0CorrectiveExecutionIsolated.ps1 -RepositoryRoot . -ExecutionRoot <DEDICATED_NONEXISTENT_PATH> -CorpusRoot <EXPLICIT_FROZEN_C11_ROOT>
```

La CLI dedicada interna acepta exactamente `--repo-root <isolated> --authorization-root <source> --corpus-root <explicit>`. El launcher aporta `--authorization-root` sin añadir un argumento humano a la ceremonia oficial. Binding de ejecución, contrato, manifest, final y staging derivan del checkout aislado; el receipt durable deriva sólo del source autorizado. El runner invoca únicamente `Lane0CorrectiveExecutionHardening.Execute`; nunca la ruta histórica `Lane0CorrectiveExecutionPreparation.Execute`.

### F06 — output readiness

Antes del manifest/corpus y antes de consumir el intento, final y staging deben estar ausentes en `ExecutionRepositoryRoot`, y el receipt debe estar ausente en `AuthorizationRoot`. Un archivo o directorio en cualquiera de esos paths produce `BLOCKED`, sin llamadas al adapter. La ruta no acepta siquiera un final vacío.

### F07 — build y ejecución en clon local aislado

El launcher resuelve sólo `RepositoryRoot`, exige HEAD válido y tracked-clean, comprueba que el binding canónico exista y no esté tracked en ese HEAD, y exige un `ExecutionRoot` externo y absolutamente inexistente. `CorpusRoot` permanece opaco: no se resuelve, enumera ni prueba en el launcher.

La preparación usa exclusivamente `git clone --no-hardlinks --no-checkout` desde el repositorio local y checkout detached del HEAD exacto; no hace fetch ni pull. Antes de copiar el binding exige cero cambios tracked y cero entradas untracked/ignored. Copia únicamente los bytes del binding, confirma SHA-256 idéntico y exige que sea la única entrada untracked antes del build. Después ejecuta `dotnet run` sin `--no-build`, propaga su exit code e imprime y conserva `ExecutionRoot` sin cleanup, retry ni borrado.

El build continúa completamente aislado de los untracked del source. En runtime, `AuthorizationRoot` debe ser distinto del checkout aislado, tener el mismo HEAD de 40 hex que el runtime, estar tracked-clean, conservar el binding canónico untracked y ofrecer bytes de binding idénticos a los inyectados en el checkout aislado. Sólo esa raíz fuente aporta la autoridad consumible; final y staging siguen perteneciendo al checkout aislado.

La hermeticidad del host y del toolchain queda explícitamente fuera de alcance: no se afirma SDK firmado, compilación reproducible bit a bit ni aislamiento del sistema operativo.

Los tests sintéticos demuestran que archivos locales benignos, `Evil.cs`, `Directory.Build.props` e ignored inputs del checkout fuente no alcanzan el checkout efectivo. También cubren fuente staged/unstaged, binding ausente, raíz preexistente, HEAD exacto detached y corpus opaco.

### F08 — integridad de entradas de staging

La validación dejó de enumerar sólo archivos. Ahora enumera todas las entradas inmediatas de staging, exige cardinalidad seis, exige que las seis sean archivos regulares y luego verifica nombres y bytes. Un archivo o subdirectorio adicional produce `INVALID`, mantiene final ausente y conserva staging como evidencia.

### F09 — HEAD autorizado antes de ejecutar código del repositorio

Una comprobación situada únicamente dentro del launcher no basta para establecer el trust root: al comenzar el `.ps1`, ya se estaría ejecutando código perteneciente al HEAD actualmente checkout. Por ello la ruta oficial comienza con un **external host precheck** ejecutado mediante primitivas PowerShell/.NET del host y Git, sin cargar scripts, binarios, proyectos ni targets del repositorio.

El precheck trata el binding únicamente como datos: exige presencia, schema v2 y `approvedPublishedHead` de 40 hex; obtiene el HEAD mediante Git; exige igualdad binding/source; comprueba tracked unstaged/staged clean, que el binding no esté tracked en ese HEAD y que el receipt durable del source esté ausente. Sólo entonces puede invocar el launcher rastreado. Los archivos untracked benignos distintos del binding no bloquean este nivel porque F07 garantiza que no se copian al checkout de ejecución.

Como defensa en profundidad, el launcher vuelve a parsear esos mismos bytes y exige ausencia del receipt durable antes de examinar o crear `ExecutionRoot`: exige schema v2, HEAD bien formado e igual al source HEAD y `authorizedExecutionCount` entero. El parser C# interno conserva la autoridad completa sobre shape, hashes, autorización, count uno, runtime HEAD y el `CreateNew` atómico.

La invariancia congelada es `BINDING_HEAD_EQUALS_SOURCE_HEAD_EQUALS_ISOLATED_HEAD_EQUALS_RUNTIME_HEAD`. Los controles sintéticos prueban que un HEAD B con un launcher capaz de crear `unauthorized-launcher-ran.marker` jamás se ejecuta cuando el binding aprueba A; marker, `ExecutionRoot` y corpus opaco permanecen ausentes.

La ruta oficial se expresa normativamente como `HOST_PRECHECK_APPROVED_HEAD_AND_TRACKED_CLEAN → tracked isolated launcher`. No se afirma firma criptográfica, binding firmado, host/OS confiable, toolchain firmado ni build reproducible.

## Binding v2 y orden del gate

El schema futuro es `lane-0-corrective-evaluation-publication-binding.2`, con ocho propiedades exactas: schema, HEAD, contrato hardened, preregistro, evaluator hardening, manifest, autorización humana y count. Binding v1, extras, faltantes o hashes malformados son rechazados.

El host ejecuta HA–HH antes de cualquier código del repositorio, incluyendo HG `REQUIRE_DURABLE_ATTEMPT_RECEIPT_ABSENT`; el launcher autorizado repite LA–LL antes del orden interno A–AC. El runner mantiene: contrato; HEAD; tracked-clean; runtime science; implementación/runner/launcher; binding; schema; contract hash; parent hashes; HEAD binding; autorización; count uno; `AuthorizationRoot` distinto, con HEAD y tracked-clean válidos; binding fuente untracked y byte-identical; final/staging ausentes en execution root; receipt ausente en authorization root; manifest público; receipt durable `CreateNew`; corpus explícito; byte hash+parse; inputs ordinales; segunda carga independiente; dos evaluaciones; dos paquetes; comparación; staging; verificación de todas las entradas; rename final.

No se toca corpus antes de crear el receipt durable en S.

## Identidades successor

- contract canonical: `B065FBC8F418D244ACF41C8ED9B12F490257FD4D7AA3D4D5B5BD2CC6EBCCAC11`;
- runtime scientific tree: `CD0FCFA43EC457CF2B49B07F1F18CDFB1515D112AEDA6C579A128B6630FF6BD8`;
- hardening implementation tree: `C2AAB12D8A5A0AF896F17568FC0F74502EC55596D71BD2246BBCFA5275541D02`;
- official runner tree: `B9CBAFE606DD358AF953748B65A48C37B43CF92595320E4F424304C925A7D1CF`;
- isolated launcher normalized file: `EAF008D43B9C1A783EEBC96EDFD73D4DD1B5B2FDC1ED91663A9299AF91FEF435`;
- hardening harness tree: `CD33FA5FA2C792493BD7C0DF7A959ABA1B991781B6D49578F14DE8C095D4F74F`;
- verifier normalized file: `D6406E3F80FE9D5ED4264C05718938365ABB7AC02C54224BDEBACFCB4D3BF168`.

## Validación

Con heap limitado a 16 GiB, conservación 9 y ejecución secuencial:

- restore solución: PASS;
- build solución Release: PASS, 0 errores;
- suite congelada sobre el checkout fuente con los bytes históricos exactos: 921 passed, 0 failed, 0 skipped;
- suite congelada dentro del clon LF aislado: 920 passed, 1 failed, 0 skipped; el único fallo fue el control histórico exact-byte `HistoricalV2RouteRejectsChangedInstrumentIdentity`, reproducido también en un clon limpio de `72bffac` sin ningún overlay. El checkout Git vuelve a materializar varios archivos históricos con line endings distintos de los bytes congelados, por lo que no constituye una regresión F03×F07;
- evaluator directo: 53 passed, 0 failed, 0 skipped;
- execution-preparation histórico: 31 passed, 0 failed, 0 skipped;
- execution-hardening successor: 68 passed, 0 failed, 0 skipped;
- adversarial E1→fallo post-receipt→E2: PASS; E2 `BLOCKED`, segundo adapter `AccessCalls == 0`, receipt byte-identical y final E2 ausente;
- isolated launcher synthetic clone: PASS; exact detached HEAD, binding-only copy and source-only inputs excluded;
- F08 bad controls for extra file and extra directory: PASS; final absent and staging retained;
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

No se emitió ningún binding nuevo para esta remediación. Puede existir en el checkout fuente un binding untracked del HEAD anterior; se conservó como estado local protegido y no fue leído, parseado, copiado ni modificado. Este informe no afirma que la auditoría independiente final haya ocurrido y no autoriza recertificación, LANE.DESIGN ni etapas posteriores.
