# LANE.0 Corrective Successor — Integration Pre-Binding Hardening

Fecha: 2026-10-04  
Baseline público exacto: `1d379ecd89eb7f202e6537e9f1e420b75fb35425`  
Parent: `7a1e55c5ddd415398ee750063c75697f4af47cec`  
Autoridad: `AUTHORIZED_FOR_PREBINDING_HARDENING_IMPLEMENTATION_ONLY`

## Cierre

Esta fase cierra X1–X4 como hardening local previo al binding. No es una auditoría,
no crea autoridad de ejecución y no modifica ciencia, conducta, RNG, defaults o producto.

- Status: `COMPLETE`
- Outcome: `READY_FOR_INDEPENDENT_PREBINDING_AUDIT`
- binding created: NO
- real receipt created: NO
- real successor execution: NO
- C11 accessed in this phase: NO
- C11 scientific evaluation: NO
- next required action: `HUMAN_REVIEW_REQUIRED`

Contrato canónico nuevo:
`9CC3961686885766A546996604AC237453A0725E832CF509FC27EA715E8A4665`.

## X1 — autoridad no redirigible

La ruta oficial recibe `SourceRoot`, `ExecutionRoot`, el HEAD esperado, identidades y el token
opaco de corpus. No recibe binding parseado, AuthorizationRoot, receipt path, staging path ni
output path. Desde `CanonicalSourceRoot` deriva exclusivamente:

`CanonicalSourceRoot/.artifacts/lane0-corrective-successor-integration-authorization`

El binding oficial es `AuthorizationRoot/canonical-binding.json`. Se leen los bytes exactos,
se parsean independientemente, se reconstruye la forma canónica, se exige igualdad byte a byte,
se recalcula SHA-256 y se verifican schema, HEAD, identidades, autorización humana y count=1.

El receipt oficial es:

`AuthorizationRoot/LANE.0.CORRECTIVE_SUCCESSOR.INTEGRATION.ONE_SHOT/receipt.json`

Mantiene `CreateNew`, escritura exclusiva, `FileShare.None`, `Flush(true)` y attemptCount=1.
El store sintético inyectable queda limitado a tests. Los controles negativos cubren binding
alternativo, hash/schema/HEAD/identidades erróneos, autorización falsa, count distinto de uno,
receipt canónico preexistente, directorio alternativo, rearmado alternativo y concurrencia con
como máximo un ganador.

## X2 — bytes finales propiedad de ExecutionRoot

Staging y artefactos finales se derivan exclusivamente bajo:

- `ExecutionRoot/.artifacts/integration-exchange/staging`
- `ExecutionRoot/.artifacts/integration-exchange/final-artifacts`

El worker construido desde el checkout detached ejecuta ambas pasadas y ejecuta dentro del mismo
proceso la finalización de `execution_identity.json` y `sha256sums.txt`. El host sólo lee los bytes,
compara pass 1/pass 2 y entrega sin transformación esos mismos diccionarios al verificador semántico.
El runner oficial no posee ni invoca un package finalizer. El E2E comprueba contención, identidad
final, checksums y byte-identidad a nivel de artefacto final.

## X3 — provenance del código ejecutado

El checkout se materializa detached, limpio y sin hardlinks. El worker se compila desde ese
ExecutionRoot sin reutilizar output del source. Antes del receipt, el proceso hijo emite provenance
con HEAD, assembly path, assembly hash, base directory, ExecutionRoot y marker; la autoridad lo
reobserva mediante Git y hashes del assembly. Ese worker vuelve a observar Git e identidades vivas,
deriva por sí mismo AuthorizationRoot, relee los bytes canónicos y crea el receipt fijo. El host sólo
hace preflight y verifica la respuesta: no crea la autoridad oficial. El mismo assembly, cuyo hash se
vuelve a comprobar después del receipt, ejecuta la ciencia y devuelve nuevamente provenance.

Cadena requerida:

`AUTHORIZED HEAD == SOURCE HEAD == ISOLATED HEAD == AUTHORITY RUNTIME HEAD == SCIENTIFIC RUNTIME HEAD`

El bootstrap exterior sólo puede seleccionar los roots explícitos de la operación; no puede
proporcionar binding bytes, otra raíz de autoridad, namespace de receipt, output/staging, HEAD
observado o identidades observadas. Toda contradicción con Git, contenido vivo o provenance falla
cerrada.

El E2E de producción crea un repositorio Git temporal con las fuentes actuales reales de Core,
Experiments y `ManiaAddNotesLab.IntegrationResearchWorker`, lo commitea, crea un checkout detached
sin hardlinks, compila el worker real, deriva provenance real, altera después el source anfitrión y
demuestra que sólo se ejecuta el worker aislado. Usa un chart sintético vacío; no toca C11.

Para evitar que servidores persistentes de MSBuild hereden handles de pipes y bloqueen el límite de
proceso, el build aislado desactiva node reuse y shared compilation. Esto no cambia bytes científicos.

## Matriz de autoridad y provenance

| Propiedad | Autoridad esperada | Observación real | Proceso | Fail-closed |
|---|---|---|---|---|
| Source HEAD | binding + baseline autorizado | `git rev-parse HEAD` | launcher | desigualdad |
| Source cleanliness | tracked clean | `git status --porcelain --untracked-files=no` | launcher | cualquier tracked drift |
| AuthorizationRoot | convención fija bajo SourceRoot | derivación y contención canónica | ruta oficial | escape/redirect |
| binding bytes/SHA | archivo canónico único | lectura exacta + reconstrucción + SHA-256 | loader oficial | byte/hash/schema drift |
| isolated HEAD/detached/clean | HEAD exacto, detached, clean | Git del checkout | materializer + launcher | contradicción |
| authority runtime | worker exacto de ExecutionRoot | provenance previa al receipt | child worker + host verifier | HEAD/path/hash/marker drift |
| scientific runtime | mismo worker exacto | provenance posterior al receipt | child worker + host verifier | HEAD/path/hash/marker drift |
| component identities | contenido vivo normalizado | hashes por archivo y aggregate | observer | cualquier mismatch |
| receipt | namespace fijo de AuthorizationRoot | `CreateNew` exclusivo + durable flush | store oficial | existente/segunda creación |
| staging/final | ubicaciones fijas de ExecutionRoot | derivación y contención | worker + reader | path alternativo/escape |

## Matriz de ownership

| Superficie | Owner |
|---|---|
| BINDING | AuthorizationRoot |
| RECEIPT | AuthorizationRoot |
| BUILD | ExecutionRoot |
| RUNTIME | ExecutionRoot |
| STAGING | ExecutionRoot |
| FINAL_ARTIFACTS | ExecutionRoot |
| CORPUS | token opaco antes del receipt; corpus externo admitido sólo después |

## CorpusRoot

Antes de crear exitosamente el receipt, `CorpusRoot` permanece como string opaco. No se normaliza,
consulta, enumera, parsea, hashea ni compara con los demás roots. Los tests poison-token conservan
este orden. La autoridad pública exacta sigue siendo
`DA86B97DA2E0309BE4C5FC035E54397846EA5B2DBA6989BDEDE4424939EDF884` y no se leyó C11.

## Identidades de componentes

Regla: UTF-8 normalizado, BOM removido, CRLF/CR→LF, SHA-256 por archivo, filas
`relative/path|FILE_SHA256` ordenadas ordinalmente, LF entre filas sin LF final y SHA-256 aggregate.

- adapter: `032F9660D8C0012C990983CE307CDA9D20B1C49116BEAA7F3C5E3DB1EBD875B0`
- runner: `C2983EC9CD0CC8B2547864C29C62F11280000B2039AEB7154B7D216F3F2DFE74`
- authority/runtime/worker: `32F8B90A44F3B4601EF3C978A70B3D21C514168FA1A5D00B28B7E0A37E1D6380`
- semantic verifier: `67969A60704553AA6CFB134F8B7F67236757EA6A2CF51C9CD7C4EF3C8FA5A4DB`

Filas exactas `path|hash`:

```text
tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorFrozenC11Adapter.cs|44E629FD93D83FC0F9F6010B795258675455CA0C4899D464DADA6EDEAB0CC5E0
tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorInternalResearchRunner.cs|74A93B937FBE8519673209597BE1053495A156AEC1C62B97068CB8E99D725B53
tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorIsolatedLauncher.cs|617F3A219C11DEC5F1754222CF876BAD943371D435404319BD36FC1DC5CAA711
tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorIsolatedRuntime.cs|D001C523A1862047695F4DDA979DD48F0C7C85F88442C533771AEDEFAA015B0D
tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorPrebindingAuthority.cs|E7B41C5A29AA56A52CD6D6C47B2E90ED7D457204CA06887107526C984215D69D
tools/ManiaAddNotesLab.Experiments/ManiaAddNotesLab.Experiments.csproj|8F0C8F639DD7A22658DFCB3E9F4995D865517FC42C5427B6C04A0F048D50D8BD
tools/ManiaAddNotesLab.IntegrationResearchWorker/ManiaAddNotesLab.IntegrationResearchWorker.csproj|CFE00E764EC99D311CFA0972E998C12008B1A001B5E364CDFE191959897B2C9F
tools/ManiaAddNotesLab.IntegrationResearchWorker/Program.cs|4F4A50C209BEE7A65EE914B2E0FF6AD259641FE27F7CE1F73638CA395E8F2E6A
tools/ManiaAddNotesLab.Experiments/Lane0CorrectiveSuccessorDeepSemanticPackageVerifier.cs|3F5950D0C80232FEC4590E3FACCDB1F695FD422935CA6BCB9B1361DF031BB809
```

El contrato JSON conserva el file set exacto. La guard nueva recomputa los aggregates contra el árbol
vivo. La guard histórica F2FD conserva validación interna; su comparación explícita contra el árbol
vivo rechaza correctamente las identidades evolucionadas.

## X4 — estado documental

Los documentos vivos distinguen ahora:

- `NOT_STARTED / NOT_AUTHORIZED` como hecho histórico de preregistro/auditorías antiguas;
- implementación, remediación y este hardening como estado actual;
- acceso C11 histórico: `YES — READ_ONLY_EXPLICIT_PATH_INVENTORY_ONLY`;
- evaluación científica C11: NO;
- ejecución successor real: NO;
- binding real y receipt real: ABSENT.

Los cuatro artifacts históricos permanecen inmutables: 7C0A…, 8763…, F2FD… y DA86….

## Validación

La evidencia ejecutada usa únicamente fixtures sintéticos temporales. Incluye loader/binding,
receipt/concurrencia, opacidad, ownership, provenance, worker real, semantic verifier, exact 12-path
metadata pública, rechazo histórico y ausencia de callsite productivo. `NU1900`, cuando aparece, se
limita a metadata de vulnerabilidades NuGet no disponible y no cambia el resultado compilado.

Resultados locales: integración focalizada `71/0/0`; guards nueva+histórica `20/0/0`;
CorrectiveEvaluator completo `178/0/0`; suite principal completa `944/0/0`; total SAFE
`1122/0/0`. Solution build, DocConsistency y `git diff --check` pasan. GitHub CI/check-runs = NONE.

## Veredicto

**INTEGRATION PREBINDING HARDENING READY FOR HUMAN PUBLICATION**

El siguiente paso posterior a una eventual publicación humana es una
`INDEPENDENT PREBINDING AUDIT OF THE FUTURE PUBLISHED SHA`. No se inventa ese SHA y no se inicia
esa auditoría en esta fase.
