# Diario de experimentos

Hipótesis no confirmadas. Comparar con el mismo chart, rango y seed; cambiar una sola variable por vez.

```text
Experiment:
Chart:
Config A:
Config B:
Seed(s):
Métricas objetivas:
Observación visual/AiMod:
Playtest:
Decisión provisional:
```

## H1 — Densidad vertical de chords

El decaimiento por columnas debería evitar que chords originales de 4–5 notas escalen con frecuencia a paredes 7K. Retención `0.65` tras dos columnas debería hacer posibles, pero raros, acordes de 5–7 notas. Comparar percentiles, no solo media.

## H2 — Densidad contextual de bursts

`ON` debería reducir acordes añadidos en bursts de 1–3 beats. Medir chance efectiva, ajustes contextuales y tamaños de chord.

## H3 — Secciones densas sostenidas

Una sección densa sostenida de 4+ beats debería conservar el AddChance normal y no confundirse con un pico excepcional.

## H4 — Gap local

`ON` debería conservar seguridad sin imponer 1/8 cuando el mapper usa repetidamente gaps válidos menores, y no sobrerreaccionar a un outlier único. Revisar skips y cercanía visual.

## H5 — Vocabulario temporal

El modo relativo debería preservar mejor que el estándar obligatorio releases/duraciones 1/5, 1/10, offsets y BPM sin avisos nuevos de AiMod. Distinguir avisos ya presentes en el input.

## H6 — Interiores LN

`ON` debería aumentar densidad percibida en charts LN-heavy rellenando solo LNs largas con soporte local, anchors originales y source intacta. Evaluar oportunidades, tiradas y LNs por origen.

## H7 — Rendimiento

Objetos inspeccionados debería ser cercano a un múltiplo de `oportunidades × keys × candidatos`, no `oportunidades × objetos`. Registrar counters y medición manual Release en fixture LN-heavy; no testear tiempo de pared.

## Hipótesis LN de esta iteración

- **H-LN1:** elegibilidad respaldada por anchors aumenta oportunidades en LN-heavy uniforme. El A/B automatizado la apoya objetivamente (23 → 76 oportunidades/run), pendiente naturalidad humana.
- **H-LN2:** contexto original-only elimina el bias artificial hacia el release de la parent. Confirmado estructuralmente por tests; su efecto perceptual sigue pendiente.
- **H-LN3:** `SimultaneousHeads` evita penalización accidental por tails sostenidas. Confirmado por fixture A/B; su intensidad final sigue pendiente de playtesting.
- **H-LN4:** candidates cortos respaldados por originales mejoran supervivencia geométrica. Se observan y preservan 1/5 y 1/8; todavía no se confirma perceptualmente.
- **H-LN5:** la combinación aumenta interacción percibida sin perder validez estructural. La parte estructural y el aumento objetivo están confirmados; la interacción percibida no puede marcarse confirmada sin playtesting.

Resultados completos y rutas de los CSV: [LN_INTERIOR_CORRECTION_REPORT.md](LN_INTERIOR_CORRECTION_REPORT.md).

## Hipótesis multikey y articulación

- **H-K1:** la escala vertical relativa elimina bias absoluto por KeyCount. Confirmada matemáticamente y en fixtures Full 4K–10K; naturalidad pendiente.
- **H-K2:** la detección contextual de burst es portable. Confirmada estructuralmente: burst 1 beat = 0.45 y sustained 4+ = 1.0 en 4K–10K; percepción pendiente.
- **H-K3:** `SimultaneousHeads` mantiene tails fuera del probability factor en todos los K. Confirmada por tests 4K/7K/10K; las tails siguen reduciendo lanes legales.
- **H-RICE1:** rice simple más chord/context protection es suficiente sin heurísticas de patrón. Sin evidencia objetiva que justifique más reglas; mantener como hipótesis hasta playtesting multikey.
- **H-ART1:** articulación por saturación funciona independientemente de K. Confirmada estructuralmente en Full/Near-Full 4K–10K.
- **H-ART2:** `NonHeldColumns` es mejor trigger cross-key que un número fijo de held lanes. Apoyada por Full/Near-Full vs Moderate; pendiente validación humana.
- **H-ART3:** `AddedInteractions` aumenta en Full-LN sin alterar rice/pass 1. Confirmada objetivamente en Spring seeds 1–100; naturalidad pendiente.

Informes: [KEYMODE_INVARIANCE_REPORT.md](KEYMODE_INVARIANCE_REPORT.md), [RICE_BEHAVIOR_REPORT.md](RICE_BEHAVIOR_REPORT.md) y [LN_ARTICULATION_REPORT.md](LN_ARTICULATION_REPORT.md).

## D0 — Exact chord-completion reconstruction

D0 está cerrado en **Outcome A** como research shadow. Define chord como un exact-head group original con dos o más members, separa `TapHead`, `LongNoteHead` y `HeldBeforeHead`, y aplica leave-one-head-group-out. Un success exige completion lane+type exactos demostrados por otro grupo del mismo chart. No usa score, probability, similarity, mirror ni taxonomy.

Runner reproducible:

```powershell
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- d0-corpus <corpus-read-only> docs <detail-json-local>
```

El runner excluye `[ADD …]`, deduplica SHA-256, deja el detalle grande fuera del repositorio y regenera `d0_chart_summary.csv`, `d0_family_summary.csv` y `d0_global_summary.csv`.

## D0.1 — Exact completion competition context

D0.1 está cerrado en **Outcome A** como research shadow. Evalúa ocho vistas exactas y paralelas sobre los 36.387 targets D0 con completions competidoras. Previous/next inmediato, gaps exactos y held-before discriminan miles de alternatives, pero la cobertura cae al exigir contexto bidireccional. El saneamiento simétrico retira el grupo completo de donors y de held futura; no existe score, probability, ladder ni selección.

Runner reproducible:

```powershell
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- d0-1-corpus <corpus-read-only> docs <detail-json-local>
```

Regenera los cuatro CSV `d0_1_*`; el JSON detallado grande permanece local. Su report histórico conserva la recomendación que abrió D0.2.

## D0.2 — Exact context coverage and view agreement

D0.2 está cerrado en **Outcome A** como research shadow. Conserva completion sets y donor group IDs por vista, compara los 28 pares, registra 22 coverage signatures y preserva conflictos unique sin resolverlos. Las conjunciones `ReducedPrevNext` y `ReducedHeldPrevNext` distinguen una intersección marginal de una occurrence conjunta real.

Runner reproducible:

```powershell
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- d0-2-corpus <corpus-read-only> docs <detail-json-local>
```

Regenera summaries chart/family/global, pairwise, joint, coverage, conflicts y estratos naturales. El detalle target-level permanece local e ignorado. El report histórico conserva la recomendación que abrió F1.

## F1 — Comparable-context resolver

F1 está cerrado en **Outcome A** como research shadow. Resuelve cada completion candidate en `ObservedSupport`, `LocalMismatch`, `NoComparableContext` o `AmbiguousEvidence`, conservando disposiciones por vista, donors, dependency y marginal-vs-joint sin score ni selection authority.

```powershell
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- f1-corpus <corpus-read-only> docs <detail-json-local>
```

Regenera once CSV `f1_*`: chart/family/global, estados, coverage, conflictos, joint, estratos, evidencia por vista, unique support y dependency. El JSON de provenance completo se escribe por streaming y permanece local/ignorado. F1 no activa backoff, D1 ni C2; su sucesora F2 ya cerró por separado como Outcome B shadow-only.

## F2 — Typed gaps and evidence backoff

F2 está cerrado **Outcome B / shadow-only**. Tipa transitions exactas, consulta misma lane y sólo amplía a chart-global cuando falta contexto. No existe A/B conductual: 2/50.762 candidates tuvieron support local inequívoco y ninguno obtuvo support global.

```powershell
dotnet run --project tools/ManiaAddNotesLab.Experiments -c Release -- f2-corpus <corpus-read-only> docs <detail-json-local>
```

Regenera nueve CSV `f2_*`. El detail queda ignorado en `.artifacts/f2/`. No existe `f2_ab_summary.csv` porque crear un artifact vacío ocultaría que el behavioral gate falló.

## Checklist

- Context burst: densidad contextual OFF/ON.
- Sustained rice: confirmar que no se trate como burst aislado.
- Timing vocabulary: estándar/relativo y AiMod.
- LN interior: interiores OFF/ON.
- Conservar CSV, trace, seed e input intacto.
