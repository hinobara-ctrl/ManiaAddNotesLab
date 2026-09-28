# LANE.0 — cierre instrumental de contadores future-held

**Fecha:** 27-09-2026  
**Baseline auditado:** `c471d10ed48e42ab33b8a981ef22c6eb26774f3d`  
**Estado científico:** LANE.0 HISTORICAL FEASIBILITY_DEMONSTRATED; GLOBAL CERTIFICATION SUSPENDED / PENDING_RECERTIFICATION  
**Autorización:** C11 STEP 2 NOT_AUTHORIZED; NO BEHAVIORAL PROMOTION

## Hallazgo y corrección

`Lane0LeakageAudit.FutureHeld` sumaba `TemporalExclusion + FutureHeldIdentityExclusion`. Los motivos son ortogonales, pero pueden coincidir en el mismo donor; la suma era un total diagnóstico con doble conteo posible, no una cardinalidad de donors únicos.

La propiedad ambigua se eliminó. `UniqueFutureHeldExcluded` expresa ahora la unión exacta:

`TemporalExclusion + FutureHeldIdentityExclusion - BothTemporalAndIdentity`

Los tres contadores individuales se preservan. `UniqueExcluded` continúa siendo distinto: es la unión global de todas las barreras, no sólo las dos future-held. La partición sigue siendo `Eligible.Length + UniqueExcluded == DonorsConsidered`.

Un fixture construido desde el builder oficial comprueba separadamente: donor anterior independiente admitido; sólo temporal; sólo identidad; ambas causas; y otra barrera independiente. En cada caso fija los contadores, la unión future-held, la exclusión global, donors considerados y elegibles.

## Procedencia del contrato v2

Antes de editar, el verificador oficial aceptó contrato, implementación, harness y dependencias v2, y se detuvo `BLOCKED` por ausencia deliberada de binding/autorización/manifest. Un verificador local separado reprodujo el hash canónico tipado `E0BCDA19B3E05E5ECA3EE8FCC7380C4AC74ACBDDE3241D77CD2F1B6EA6274F35` y la identidad de implementación `63F8897F39370D41260CEF962E20F82D288F502EA0AEE8AB82C3C1088F92B82E`.

La identidad harness v2 `B4C579411846085DAC6C977BD6D734528358C3A1E103D097D4EA46273FDBD212` coincide con los bytes del checkout publicado, pero no con los blobs de `c471d10`, cuyo inventario produce `73D7E5D8205884F9E6B8753689A84F335D01640956B75171B71C9144712C5A16`. La diferencia está localizada en `tools/DocConsistency/Program.cs`: el working tree tenía 1.302 CRLF dentro de 1.856 LF, mientras el blob Git contiene sólo LF. No es drift semántico, pero demuestra que una identidad de bytes del checkout no es portable entre políticas de newline.

El contrato v2 permanece intacto y conserva su procedencia. No se cambiaron sus hashes para acomodar el código nuevo.

## Contrato sucesor v3

`lane-0-future-held-counter-closure.3` sustituye v2 únicamente como identidad instrumental futura; reutiliza su contrato padre, manifest esperado, límites de autorización y dependencias científicas. Su algoritmo de código:

1. decodifica UTF-8 y elimina un BOM opcional;
2. normaliza CRLF y CR a LF;
3. calcula SHA-256 de los bytes UTF-8 normalizados;
4. ordena ordinalmente `path|fileHash`, une filas con LF y vuelve a calcular SHA-256.

Esto no convierte el hash normalizado en hash exacto de bytes, hash de commit, hash de manifest ni hash canónico JSON. Esas identidades continúan separadas. El contrato se canonicaliza mediante round-trip tipado `System.Text.Json`, camelCase y JSON sin indentación.

Identidades v3 finales:

- contrato: `221D5133D8058FEBEAEA0A13F83058219D900261B033282EE389A82BA20FAAF7`;
- implementación: `2F938F18D98C6299214C080195633A68B68C688CABF49DC0F4DEFFCA9C1897D9`;
- harness: `1EFC2B824FAA6F0798C3FFA679FCA073939D88B2C751612C06BB75C240E96309`.

El verificador separado es `tools/IndependentLane0IdentityVerifier.ps1`; no llama a las funciones de hash del runner. Informa además las identidades normalizadas de working tree y blobs `HEAD` para dejar visible cualquier diferencia real de contenido.

## Validación

Validación inicial de `c471d10`, antes de editar: restore correcto con aviso `NU1900`, build Release correcto, **860 passed / 0 failed / 0 skipped**, DocConsistency `PASS` y `git diff --check` limpio.

Validación final con límite de 16 GiB y ejecución secuencial: restore correcto con `NU1900`; build Release correcto, 0 errores; **863 passed / 0 failed / 0 skipped**; DocConsistency `PASS`; verificador independiente v3 coincidente para contrato, implementación, harness y seis dependencias; `git diff --check` correcto con avisos informativos LF→CRLF. El harness v3 no existe todavía como conjunto completo en `HEAD` porque incluye este nuevo verificador; su contraste Git queda pendiente del commit manual y auditoría posterior.

No se abrió C11, no se proporcionó manifest, no se creó binding, no se activó autorización humana y no se ejecutó ninguna matriz. `AddNotesEngine`, `LaneGeometryIndex`, RNG, `legacy-experimental.1`, treatments y resultados históricos permanecen intactos.

## Límites y stop

La conectividad con GitHub no estuvo disponible durante el preflight final: HEAD y `origin/main` cacheado coincidían con `c471d10` y la divergencia cacheada era `0/0`, pero `fetch` y `ls-remote` en vivo quedan `NOT_VERIFIED`. Después de publicar manualmente se debe auditar el nuevo SHA y sólo entonces podría prepararse un binding separado. C11, LANE.DESIGN/GATE y G2/H siguen no autorizados.
