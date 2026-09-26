# SAFETY.SELECTION_SET_REMAPPING — Post-publication harness hardening

Fecha: 2026-09-26  
Ámbito: mantenimiento de instrumentación; no es una nueva ejecución experimental.  
Estado científico preservado: `E_SELECTION_SET_REMAP` 8/8, OP-466 `C_UNRESOLVED`, global
`NEEDS_REVIEW / NO PROMOTION`.

## Estado de entrada

La copia local comenzó en `main` / `61f7e14f5f4462876de6a9263f07f881f05f3dfd`. El
`origin/main` local apunta al mismo commit. `git ls-remote` no pudo comprobar GitHub porque el entorno
no tenía conectividad; por ello este addendum no afirma haber verificado el remoto en línea. Los tres
documentos personales no rastreados (`ASTRA_ROADMAP_V2_PROPOSAL.md`, `HANDOFF_GLOBAL_2026-09-23.md`
y `HANDOFF_REENTRY_PROMPT_2026-09-23.md`) se preservaron sin cambios.

## Hallazgo y corrección

El campo downstream `OpportunitySequenceIdentity` se calculaba así:

```csharp
pairs.Select(x => x.OpportunityKey)
    .SequenceEqual(pairs.Select(x => x.OpportunityKey))
```

Era tautológico: ambas expresiones enumeraban el mismo objeto `pairs`, ya producido después del
emparejamiento. No comparaba la secuencia control con la secuencia treatment y no podía detectar una
oportunidad faltante, adicional, intercambiada o renombrada.

El `Pair` histórico sí observaba ambas secuencias originales y ya imponía igualdad de cantidad,
`OpportunityOrder` y `OpportunityKey` antes de construir cada par. Por tanto, el problema no afectó
los pares aceptados ni el resultado E; afectó la veracidad/procedencia de un booleano diagnóstico
redundante.

La corrección introduce `SelectionSetOpportunityPairingResearch.Compare/Pair`. La comparación se
realiza una vez, en el último punto donde existen las dos secuencias originales. Un resultado no exacto
detiene el emparejamiento con un diagnóstico específico. El objeto `ValidatedOpportunityPairing`
transporta tanto los pares como la garantía obtenida. `AnalyzeDownstream` consume esa garantía en vez
de reconstruir dos supuestas secuencias desde una sola lista emparejada.

La igualdad de secuencia comprende solamente `(OpportunityOrder, OpportunityKey)`. Las identidades de
commit pueden diferir legítimamente entre brazos y se conservan en cada par; no forman parte de la
identidad de oportunidad. No se alteró el orden de generación, outputs, commits, consumo RNG,
geometría, G1, reglas E ni clasificador A/B/C/D.

## Tests adversariales

Los tests focalizados cubren:

1. secuencias idénticas;
2. oportunidad faltante en treatment;
3. oportunidad adicional en treatment;
4. dos oportunidades intercambiadas;
5. misma longitud con claves distintas;
6. misma secuencia con commits diferentes;
7. emparejamiento correcto, preservación de commits y flag de autoridad.

Los negativos fallan antes de `Zip`; ninguno compara dos referencias al mismo objeto como prueba de
validez. Cuatro controles adicionales prueban el manifest de hashes del verificador: íntegro, contenido
alterado con hash anterior, archivo faltante y archivo inesperado.

## Identidades históricas verificadas

| Identidad | SHA-256 |
|---|---|
| Entry HEAD contractual | `52f21af56f8034e07fcaa9d849cb9440b9429e73` |
| Implementación conductual | `B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835` |
| Harness experimental histórico | `917F8157247C05968736DFE883BC578EE6332E4665CD9EA890FD89B79E0CB220` |
| Corpus C11 | `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445` |
| Contrato v2 | `A1A83F4515ADDD18398842B5FECC1BD0F43BC60E1D62F6B2B18B84A369244E66` |
| Commit que publicó el experimento | `61f7e14f5f4462876de6a9263f07f881f05f3dfd` |

El verificador recalcula el contrato canónico; la implementación desde el checkout actual; el harness
histórico leyendo criptográficamente los blobs de `61f7e14`; y el manifest C11 mediante
`FrozenC11ManifestResearch`. También verifica los inputs históricos del contrato, los manifests SHA-256
principal y priority (20 + 8 entradas), la ausencia de archivos inesperados, el resumen, las ocho filas
E y las ocho filas de no interferencia.

## Reproducción histórica versus verificación posterior

`SafetySelectionSetRemappingRunner.Run` exige simultáneamente:

- `GitHead(repositoryRoot) == RepositoryEntryHead`;
- implementación actual igual al fingerprint congelado;
- harness actual igual al fingerprint congelado;
- manifest/corpus igual a C11;
- inputs históricos con sus hashes exactos.

Un checkout limpio de `52f21af` no contiene los archivos del nuevo harness; `git show` confirma que
`SafetySelectionSetRemappingRunner.cs` todavía no existía allí. Un checkout limpio de `61f7e14` sí los
contiene, pero su HEAD ya no coincide con el entry HEAD contractual. Por eso una reproducción histórica
exacta no equivale a ejecutar el runner desde el nuevo `main`.

Un entorno histórico reconstruido requeriría un worktree aislado en `52f21af`, superponer exactamente
los siete archivos de harness publicados en `61f7e14` sin cambiar el HEAD, suministrar los inputs y corpus
congelados, y usar `DOTNET_GCHeapHardLimit=0x400000000`. Esa operación vuelve a ejecutar el experimento;
no se realizó durante este mantenimiento.

La operación posterior autorizada es:

```powershell
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- `
  safety-selection-set-remapping-verify-published . `
  docs/g1_gate_runtime_manifest.json `
  docs/safety_selection_set_remapping_contract.json `
  .artifacts/safety_selection_set_remapping `
  .artifacts/safety_selection_set_remapping_harness_hardening
```

Ésta sólo verifica evidencia ya materializada. No abre charts, no explora `Songs`, no ejecuta engine ni
RNG y no debe describirse como reproducción experimental.

## Nueva evidencia separada

La evidencia se escribe exclusivamente en
`.artifacts/safety_selection_set_remapping_harness_hardening/`:

- `verification_summary.json`: identidades, conteos, tipo de operación y resultados;
- `sha256sums.txt`: hash SHA-256 del resumen.

El fingerprint de mantenimiento se registra en ese resumen y es deliberadamente distinto de
`917F815…`. El harness histórico permanece verificable desde los blobs publicados y sus artifacts no se
reescribieron. La carpeta nueva es evidencia local ignorada, no una modificación retrospectiva de los
artifacts científicos.

Resultado final de esta revisión:

- harness de mantenimiento: `BD94C91346BD57315D5A354CBEB50CBE97F0EF9256A47514C392E7C56939B8C4`;
- `verification_summary.json`: `E7C87675A0BEAB2D4D73EBA6D58066B81965D61FE3A046283A3430EC16B7AA91`;
- `sha256sums.txt`: `22040B27ABED05C9CE370774964FEC7EE000ACE0466FCD6E8AF2633EF734597C`;
- tests focalizados de opportunity/pairing: 22/22 (la clase incluye los siete nuevos controles);
- tests focalizados del verificador: 4/4;
- suite completa: 807/807, 0 fallos, 0 omitidos;
- restore y build Release: correctos, 0 errores;
- DocConsistency: `PASS`;
- `git diff --check`: correcto.

Restore/build emitieron únicamente `NU1900`: no fue posible consultar el índice de vulnerabilidades de
NuGet por la misma falta de red; no hubo fallo de restauración ni compilación.

## Límites

- No se repitieron las ocho ejecuciones ni la matriz de 224 pares.
- No se investigó ni implementó el contrafactual de trayectoria OP-185 → OP-466.
- La verificación offline confirma integridad, estructura, conteos y coherencia de lo publicado; no
  sustituye la ejecución causal bajo las identidades originales.
- La ausencia de conectividad impidió comparar contra GitHub en línea.
- No hubo commit, push, tag, branch, pull request ni release.
