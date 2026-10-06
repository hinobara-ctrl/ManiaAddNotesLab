# LANE.0 Corrective Successor — cierre de la ejecución oficial

## Alcance y autoridad

Este informe cierra documentalmente el intento oficial de `LANE.0 Corrective Successor` ligado al HEAD público `f439b1f05bef0a4442e09a018cf003cb0cf35b5e`. Es un registro forense: no autoriza retry, rearm, delete-to-retry, otra invocación oficial, remediación, successor, cambio de producto ni promoción.

La política de comportamiento continúa en `legacy-experimental.1`. No se modificaron engine, defaults, RNG, HardValidity ni contratos congelados.

## Identidades congeladas

- Public HEAD: `f439b1f05bef0a4442e09a018cf003cb0cf35b5e`.
- Binding canónico, documento SHA-256: `A543A87DF7585DD54043544ACB1DCDAD6D647114D904D2EB3147925557503A51`.
- Binding canónico, fields SHA-256: `E601DC82D3B7A50165354A1E7D7A9A1C976783BCCC814774E0AF09D93E7E752C`.
- Driver de la primera invocación, observado localmente: `E967BEB1CF54275021868D2109E4447AFAC9C9C3E81959FE53DB6F4963264EC2`.
- Driver de la segunda invocación, identidad auditada independientemente: `5B45D81F844F94726451ABF56E6E88BB5A499B2B89D1B4D529BC565FDDEAD11D`. El archivo físico de esta variante no fue observable durante este cierre.

## Primera invocación: fallo ambiental pre-receipt

La primera invocación humana autorizada se realizó dentro del entorno de ejecución de Codex. `GitDetachedCheckoutMaterializer` falló en `git clone --no-hardlinks --no-checkout`, antes de `PRE_RECEIPT_READY`, receipt, `RECEIPT_VERIFIED`, interpretación de CorpusRoot, admisión C11 o ciencia.

El error MSYS `NtCreateDirectoryObject(\BaseNamedObjects\...) / 0xC0000022` fue reproducido por diagnósticos no oficiales con el Git del sistema y el Git incluido en el runtime. La misma operación se completó posteriormente desde el PowerShell normal del usuario. Las auditorías conservaron el veredicto: fallo ambiental anterior al consumo durable, sin defecto causal de producto y sin convertir la primera invocación en consumo del one-shot.

## Segunda autorización e invocación

Una decisión humana separada autorizó exactamente una segunda invocación desde PowerShell normal, con ExecutionRoot machine-local:

`C:\Users\benja\OneDrive\Escritorio\Lane0OfficialExecution-f439b1f0-second`

La variante desechable del driver cambió únicamente ese literal de ExecutionRoot; su delta fue auditado como limpio. Se utilizó `pwsh -NoProfile -ExecutionPolicy Bypass`, limitado al proceso. No se aplicó `Unblock-File`, no se persistió una policy y no se alteraron los bytes auditados del driver.

## Resultado oficial final

- `classification`: `NON_PUBLISHABLE`.
- `scientificOutcome`: `null`.
- `receiptConsumed`: `true`.
- `scientificPassesStarted`: `0`.
- `scientificPassesCompleted`: `0`.
- `CompletedAuthoritySteps` capturado: `[]`.

El array vacío es pérdida de información del wrapper de excepción; no demuestra cero pasos de autoridad. La evidencia durable establece que se alcanzaron `PRE_RECEIPT_READY`, creación del receipt canónico, verificación independiente del receipt, `RECEIPT_VERIFIED`, creación de `official-authority-state.json` e interpretación post-receipt de CorpusRoot.

El receipt canónico está presente y consume permanentemente el one-shot. `official-authority-state.json` está presente con estado `RECEIPT_VERIFIED_ATTEMPT_CONSUMED`. No existen `response.json` ni `final-artifacts`. Ninguna pasada científica comenzó; byte determinism y deep semantic verification no fueron alcanzados.

## Fallo de admisión C11

La admisión exact-path comenzó en `LOCATION_00` y falló con `DirectoryNotFoundException` al intentar leer:

`20260906-131826-08255ad9b6104a4bac32bcbc4a445ed5/BUTAOTOME - Hakanaki Mono Ningen (YuEast 2018) [fake].osu`

Autoridad congelada esperada:

- chart SHA-256: `0338ADEB4CDBB086D7819736292CB932608EA53DAD840AD3B8BBEAA605AC8349`;
- family: `BUTAOTOME | HAKANAKI MONO NINGEN | YUEAST 2018`;
- keymode: `4K`;
- objetos originales: `1933`.

El CorpusRoot suministrado usa un layout físico aplanado más nuevo, mientras la autoridad exact-path congelada describe el layout encapsulado anterior con directorios `20260906-*`. La clasificación forense es:

`C11_EXACT_PATH_ADMISSION_LAYOUT_MISMATCH`

No es un fallo científico y este cierre no demuestra chart-content drift.

**HUMAN-ATTESTED SAME SCIENTIFIC C11; THIS CLOSURE DOES NOT ADD A NEW CRYPTOGRAPHIC CORPUS-EQUIVALENCE CLAIM.**

La identidad de contenido y la satisfacción de autoridad exact-path son propiedades distintas: mismo C11 científico no implica el mismo layout físico.

## Evidencia observable

| Evidencia | SHA-256 / estado |
|---|---|
| Canonical receipt | `1C7E263C0C16D8C44D57465390B73393B9940816D274DBE1D6FF8981FF33E8F7` |
| `official-authority-state.json` | `45460BE1AFE8319891CB64B1483EC0401B5BBA409E1B48B68855938EE54A2FA7` |
| Segundo `official-result.json` | `NOT_OBSERVABLE` |
| Segundo driver, identidad auditada | `5B45D81F844F94726451ABF56E6E88BB5A499B2B89D1B4D529BC565FDDEAD11D` |
| `canonical-binding.json` | `A543A87DF7585DD54043544ACB1DCDAD6D647114D904D2EB3147925557503A51` |

La ausencia local del segundo result/driver no se suplió mediante reconstrucción. El authority-state observado conserva HEAD, binding, las 13 identidades runtime y cierre binario del worker.

## Cierre

El source tree permaneció sin cambios rastreados o staged después de la ejecución. No hubo promoción de producto. El intento terminó `NON_PUBLISHABLE`, sin resultado científico, y su receipt prohíbe de forma irreversible retry, rearm y delete-to-retry.

Este cierre no autoriza una nueva remediación ni un successor. Cualquier investigación futura del layout o equivalencia criptográfica requiere un alcance separado; no existe siguiente acción científica autorizada.

`LANE.0 CORRECTIVE SUCCESSOR OFFICIAL EXECUTION DOCUMENTED — DURABLE ONE-SHOT CONSUMED — NON_PUBLISHABLE — C11 EXACT-PATH LAYOUT MISMATCH — NO RETRY OR SUCCESSOR AUTHORIZED`
