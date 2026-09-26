# SAFETY.REMEDIATION.GATE — Focused Follow-up

Fecha: 2026-09-25  
Estado: **COMPLETE — FIVE MECHANISMS DEMONSTRATED / HISTORICAL NEEDS_REVIEW PRESERVED / NO PROMOTION**  
HEAD de entrada: `62615bbf5874bed04a7cdd7dc1fba4b43f31ea45`

## Alcance y límites

Este seguimiento auditó dos debilidades del clasificador causal, reprodujo individualmente los cinco
casos `C_UNRESOLVED` y añadió una entrada C11 explícita. No reejecutó la matriz de 224 pares. No cambió
defaults, remediación, G1, AddChance, selección de lane, HardValidity ni autoridad canónica. El cierre
histórico permanece intacto: **188 A + 22 B + 5 C, NEEDS_REVIEW**. La categoría de seguimiento descrita
abajo es evidencia aditiva y no una reclasificación retroactiva.

El contrato focalizado fue congelado antes de la ejecución final:

- contrato: `645DAD6346130FBF0D4753053A35A40890A33FEE8F7F928768B8DDE9F4701083`;
- implementación conductual: `B7AA67D389AE71A775397997BCEC3185CD0E763ED19E12770CE03D1C18AF2835`;
- harness: `31FC70DE3C856D4E4EB9046520169C2A9E5899AE6E82777C390D366A3EE6E44F`;
- manifest C11: `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`;
- límite GC: `DOTNET_GCHeapHardLimit=0x400000000` (16 GiB).

## Auditoría del clasificador

Las dos hipótesis eran defectos reales de instrumentación:

1. El runner abría lineage a partir de cualquier diferencia entre conjuntos de lanes legales. Eso no
   demuestra que la autoridad canónica rechazó el commit elegido por control. La prueba ahora exige
   que la lane efectivamente seleccionada sea legal en legacy e ilegal en canonical, que los commits
   difieran, que treatment no haya comprometido el commit rechazado y que los estados sucesores sean
   distintos.
2. La divergencia no explicada se consultaba solamente mediante el siguiente `StateBefore`. Una primera
   diferencia en el sucesor de la última oportunidad podía quedar invisible. Ahora cada oportunidad
   inspecciona inmediatamente `StateBefore` y `StateAfter`.

Los caminos full, compact/streaming y el test de equivalencia usan la misma definición. Los controles
adversariales cubren legal-lane sets distintos con selección idéntica, cambio de selección sin rechazo,
rechazo real del commit, último sucesor, reconvergencia, nueva divergencia, canonical reject sin cambio de
estado, desaparición del blocker y cambio de geometría previa. La implementación compacta conserva
exactamente commits, estados, divergencias y reconvergencias de la referencia sencilla sin caché.

## Los cinco casos

En los cinco casos el tap histórico se compromete con la misma lane, tiempo, identidad, secuencia y
provenance en control y treatment. La violación de control es siempre `TapOnHeldLongNote`: una LN sintética
en la misma lane termina exactamente en el milisegundo del tap. Esa LN bloqueadora no existe en treatment.
La divergencia que gobierna su ausencia es canónica, anterior y no reconverge antes del target.

| Chart | Seed | Tap histórico | LN bloqueadora de control | Commit de la LN | Origen gobernado |
|---|---:|---|---|---|---|
| `02D9D178…` | 8 | `L1 @ 222416` | `L1 222331→222416`, S4468 | `OP-00004468…` | `OP-00003072…` |
| `20651C9B…` | 3 | `L6 @ 242439` | `L6 242351→242439`, S4663 | `OP-00004663…` | `OP-00000370…` |
| `20651C9B…` | 6 | `L6 @ 89821` | `L6 89595→89821`, S1510 | `OP-00001510…` | `OP-00000370…` |
| `20651C9B…` | 7 | `L5 @ 110572` | `L5 110460→110572`, S1934 | `OP-00001934…` | `OP-00000011…` |
| `20651C9B…` | 13 | `L0 @ 296968` | `L0 296880→296968`, S6170 | `OP-00006170…` | `OP-00001936…` |

Para cada fila el oracle independiente reproduce una violación en control y ninguna en treatment; el commit
de la LN es `null` en treatment en la misma oportunidad; no existe copia exacta ni alternativa con la misma
provenance; el target conserva lineage activa; y no hay reconvergencia entre su origen y el target.

La categoría explicativa nueva es **`D_CONFLICTING_OBJECT_ABSENT`**:

> El objeto histórico se compromete idénticamente, pero el objeto sintético concreto con el que colisionaba
> está ausente por efectos downstream de una divergencia anterior gobernada por autoridad canónica, sin
> reconvergencia relevante.

Esto explica 5/5 mecanismos. No los convierte en `B_CAUSALLY_PROVEN_UNREACHABLE`, porque el objeto histórico
sí es alcanzado y comprometido. La tabla histórica y el outcome `NEEDS_REVIEW` no se sobrescriben.

## Corpus C11 reproducible

`C11CorpusDiscovery.ResolveFrozen` recibe únicamente un directorio explícito y las once expectativas del
manifest. Enumera `.osu` sólo dentro de ese directorio, calcula SHA-256 antes de parsear, no retiene charts
inesperados y reporta:

- hashes faltantes;
- contenidos inesperados;
- archivos inválidos;
- drift de familia/keymode/object count;
- ubicaciones duplicadas con contenido exacto.

El corpus local histórico resolvió **12 ubicaciones, 11 hashes únicos y un duplicado exacto**. La ruta nueva
y el discovery anterior, ejecutado sólo sobre esas 12 copias, coincidieron 11/11 en SHA-256, identidad de
contenido authored, familia, keymode, taps, LNs, objetos, timing points y BPM mínimo/máximo. C11 sigue siendo
el corpus histórico de desarrollo, no una reserva independiente.

### Reconstrucción en otro checkout

1. Obtener legítimamente los once `.osu` originales. El repositorio no redistribuye charts de terceros.
2. Copiarlos a cualquier carpeta dedicada; nombres y subcarpetas no establecen identidad.
3. Ejecutar, sin apuntar a `Songs`:

   ```powershell
   $env:DOTNET_GCHeapHardLimit='0x400000000'
   dotnet run --project tools/ManiaAddNotesLab.Experiments -- `
     c11-frozen-verify <directorio-c11> docs/g1_gate_runtime_manifest.json
   ```

4. Si falta un hash, suministrar el archivo original correspondiente. No se busca automáticamente en una
   biblioteca global ni se simula el contenido ausente.

El comando `c11-frozen-compare` compara el resolver nuevo con el discovery histórico dentro del directorio
dedicado. Un scan inicial de una biblioteca desconocida de 400 GB sigue siendo costoso y queda expresamente
fuera de este procedimiento.

## Ejecuciones y recursos medidos

- Verificación C11 explícita: 0,139 s, peak working set muestreado 43,7 MiB, private 24,9 MiB.
- Cinco reproducciones, incluyendo referencia sencilla y repetición determinista: 25,082 s, peak working
  set muestreado 144,9 MiB, private 112,2 MiB.
- Las cinco comparaciones control/referencia fueron exactas en bytes, RNG count y transcript hash.
- Las cinco repeticiones treatment fueron deterministas en bytes, RNG y fingerprint diagnóstico.
- Se ejecutó sólo la muestra de cinco pares; no se hizo una nueva afirmación sobre los 224 pares.

Durante el desarrollo un mismatch del hash del manifest escapó como excepción .NET no capturada y Windows
mostró `0xe0434352`. Esa ejecución fue descartada. Los comandos focalizados ahora capturan errores de entrada,
escriben un diagnóstico legible y retornan exit code 1; no fue un resultado de test ni evidencia de corrupción.

## Artifacts y outcome

- `docs/safety_remediation_gate_followup_contract.json`: scope e identidades congeladas.
- `docs/safety_remediation_gate_unresolved_followup.json`: evidencia individual de los cinco casos.
- `.artifacts/safety_remediation_gate_followup/`: logs y repetición medida local, ignorados por Git.

Outcome científico: **las dos debilidades eran reales y quedaron corregidas; los cinco mecanismos quedaron
causalmente demostrados como ausencia del blocker; C11 es reproducible desde una carpeta explícita sin
escanear Songs; la implementación conductual no cambió.** El outcome histórico continúa `NEEDS_REVIEW`, no
hay promoción y una recertificación completa requeriría un contrato y una matriz separados.
