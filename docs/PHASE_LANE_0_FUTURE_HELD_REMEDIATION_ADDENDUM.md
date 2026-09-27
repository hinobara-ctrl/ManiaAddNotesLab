# LANE.0 — Future-held remediation addendum

**Estado:** PASO 1 COMPLETE / READY FOR HUMAN REVIEW  
**Conducta productiva:** sin cambios  
**Evaluación C11 correctiva:** no ejecutada; requiere publicación y autorización expresa  
**HEAD aprobado al iniciar:** `12ee8799528d9cf9d64d8d9a9ab4b45ba955db0f`

## Alcance y preservación histórica

Este addendum corrige el instrumento de investigación, no el generador. `AddNotesEngine`, RNG, defaults, caps, eligibility y selectores productivos permanecen intactos. El informe, contrato y artifacts históricos de LANE.0 no se modifican. Sus cifras `40.360/37.080` para rice y `16.881/3.373`, con `288/11` operativas G1, siguen describiendo exclusivamente la ejecución original. La certificación global `FEASIBILITY_DEMONSTRATED` queda **pendiente de recertificación**; no se presume qué ocurrirá con los 11 casos G1.

El contrato histórico conserva SHA-256 canónico `62B2F4F67C34E8F57893A3A025D567000E1BB12B6517C13D97FE9EEC3FE0A3C2` y el manifest C11 conserva `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`.

## Diagnóstico contrastado

El hallazgo de Jules es correcto y está delimitado a la rama G1 del instrumento LANE.0:

1. G1.0 construía donors prior-only con `donor.AnchorTime < target.AnchorTime` y su auditor independiente contaba leakage cuando `donor.AnchorTime >= target.AnchorTime`.
2. `Lane0Occurrence` no transportaba `AnchorTime`.
3. `G1Occurrences` escribía siempre `[]` en `FutureHeldObservationIds`.
4. El control adversarial existente fabricaba a mano arrays future-held coincidentes; probaba la reacción del auditor, pero no la producción de esa información por el builder oficial.
5. El auditor LANE.0 sólo intersectaba dos arrays manuales. Por ello no podía reconstruir la frontera temporal histórica a partir de occurrences reales.

Las garantías target, target-group, parent, release, same-event, synthetic, composición, duplicate e incomplete existían. La garantía temporal G1 se había perdido en el enlace builder→auditor. No se encontró evidencia de que el mismo defecto afecte rice.

## Reproducción mínima y reparación

El fixture sintético usa tres parents LN no solapados, de igual duración y lane, cada uno con una relación interior equivalente. Se ejecuta primero `InteriorRelationFeasibilityResearch.Evaluate` y luego exactamente `Lane0FeasibilityRunner.BuildG1Occurrences`, el builder que consume el runner.

La modalidad histórica de reproducción produce:

- `AnchorTime = null`;
- `FutureHeldObservationIds = []` para todas las occurrences;
- intersección vacía frente a las identities de un donor posterior.

La modalidad reparada:

- transporta el `AnchorTime` original completo;
- construye, para cada target, el conjunto de IDs originales de relaciones situadas en el mismo anchor o en anchors posteriores, incluidos parent, witness, head witnesses y release witnesses;
- rechaza explícitamente `donor.AnchorTime >= target.AnchorTime`;
- rechaza independientemente cualquier donor cuyas `ObservationIds` intersecten el conjunto future-held del target.

Se aplican ambas garantías porque son semánticamente independientes: el tiempo expresa el contrato prior-only; las identities demuestran la provenance original que hizo posible la decisión. Un donor anterior e independiente se conserva. El mismo tiempo y el tiempo posterior se excluyen. Parent/group/event/release continúan como controles separados y no se reutilizan para simular temporalidad.

## Tres niveles de garantía

| Control | Auditor detecta | Builder real aporta datos | Flujo excluye |
|---|---|---|---|
| future anchor posterior | Sí, comparación explícita | Sí, `AnchorTime` | Sí, no entra en `Eligible` |
| boundary mismo tiempo | Sí, `>=` | Sí | Sí |
| future-held identity | Sí, intersección con `donor.ObservationIds` | Sí, identities completas | Sí |
| release endpoint | Sí | Sí, `ReleaseWitnessIds` | Sí |
| target/group/parent/event | Sí | Sí | Sí |
| synthetic/cross-chart/composition/duplicate/incomplete | Sí | El runner conserva provenance y chart identity; los fixtures inducen cada corrupción | Sí o ejecución `INVALID` |

Un contador cero ya no constituye por sí solo la prueba: los tests inducen positivamente cada clase pertinente.

## Clasificación experimental

Se añadió un clasificador explícito con precedencia de validez:

- `INVALID`: contrato/preregistro, controles, integridad, determinismo, no interferencia o RNG fallan.
- `BLOCKED`: los prerrequisitos son válidos, pero falta una entrada o recurso antes de consultar evidencia.
- `FEASIBILITY_DEMONSTRATED`: ejecución válida y criterio de soporte satisfecho.
- `LIMITED_PARK`: ejecución válida y completa, pero evidencia insuficiente.

Esto evita degradar un experimento inválido a `LIMITED_PARK`. No cambia retrospectivamente la interpretación de artifacts históricos; gobierna la futura evaluación correctiva.

## No interferencia y límites

Los fixtures verifican fingerprint de chart idéntico antes/después y `ResearchRngCalls = 0`. La reparación vive sólo en `tools/ManiaAddNotesLab.Experiments`; no existe call site productivo nuevo. No se consultó C11, no se recalcularon agregados y no se tocó `.artifacts/`.

## Preregistro correctivo

`docs/lane_0_future_held_remediation_contract.json` congela pregunta, defecto, semántica, denominadores históricos, controles, outcomes, comparación emparejada, análisis individual obligatorio de los 11 casos operativos G1, stops y límites. Su campo `requiredEvaluationPublishedHead` permanece `PENDING_USER_PUBLICATION_AND_APPROVAL`: el hash del HEAD de evaluación no puede conocerse antes del commit personal del usuario y no debe inventarse.

Identidades congeladas del paso 1:

- contrato canónico: `F28F35AA991F3AF20BEEBBEE6AC1D9D3C182E71C62E4044E4DB0792857F8B0A7`;
- reparación instrumental: `A69C084D6631E24B49E66229B5D666E1B8F74BAD306FFE1CAF7BEC9619BBF0B4`;
- harness correctivo: `6B1FE0A10C62C6E2D155A2CF279791B5CD431F2F13A6653F4B498696AC0FB49E`.

La próxima ejecución autorizada deberá mantener los denominadores congelados, comparar cada chart/family/keymode contra el snapshot histórico, conservar rice como sentinel y publicar cada uno de los 11 IDs operativos G1 con donor count corregido, exclusiones, estado y disposición de soporte. Ningún umbral podrá adaptarse después de observar resultados.

## Reproducción histórica aislada

El runner y contrato se publicaron en `12ee879...`, pero la ejecución original exigía `HEAD=be08f3f...`. Para reproducir ese estado sin alterar el checkout actual:

```powershell
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("mania-lane0-history-" + [guid]::NewGuid())
git worktree add --detach $temp be08f3f2f0191a5c972ad16449a7199dd07f2e3f
git -C $temp restore --source 12ee8799528d9cf9d64d8d9a9ab4b45ba955db0f -- `
  tools/ManiaAddNotesLab.Experiments/Lane0FeasibilityRunner.cs `
  tools/ManiaAddNotesLab.Experiments/Program.cs `
  tests/ManiaAddNotesLab.Tests/Lane0FeasibilityTests.cs `
  tests/ManiaAddNotesLab.Tests/ManiaAddNotesLab.Tests.csproj `
  docs/PHASE_LANE_0_FEASIBILITY_DESIGN.md `
  docs/lane_0_feasibility_contract.json
```

Ese directorio queda detached en el HEAD exigido y recibe únicamente el harness publicado posterior. Antes de cualquier reproducción se deben verificar los hashes congelados; no se cambia `RequireHead`, no se regenera el contrato y el corpus se suministra explícitamente según el manifest. La evaluación histórica no forma parte de este paso.

## Estado de cierre del paso 1

La reparación instrumental y su preregistro quedan listos para revisión humana. La evaluación correctiva C11, nuevas cifras, LANE.DESIGN, LANE.GATE y cualquier promoción permanecen no autorizados hasta que el usuario publique, entregue el nuevo HEAD y autorice el paso 2.
