# SAFETY.REMEDIATION — OP-185 → OP-466 single-intervention counterfactual design

Fecha de preregistro: 2026-09-26  
Estado al redactar: diseño cerrado; resultado del brazo intervenido todavía no observado.  
Alcance: un chart, seed 4, dos ejecuciones canónicas independientes, una intervención.

## Pregunta e hipótesis

Se evaluará si, bajo la misma política geométrica canónica, entregar lane 3 en vez de lane 2
exclusivamente en `OP-00000185-BaseHead-S185-T16399-ANA` cambia la aparición posterior del target
histórico exacto `OP-00000466-BaseHead-S466-T35009-ANA`.

La hipótesis permite que la intervención cambie la trayectoria, pero no presupone recuperación. Una
recuperación exacta demostraría un efecto de esta intervención dentro de la política canónica congelada;
no demostraría que OP-185 fuera la causa histórica exclusiva legacy/canonical.

## Estado científico preservado

- `E_SELECTION_SET_REMAP`: demostrado 8/8.
- Recertificación: 188 A / 21 B / 5 D / 1 C.
- OP-466: `C_UNRESOLVED`.
- Global: `NEEDS_REVIEW / NO PROMOTION`.

El experimento no puede cambiar retrospectivamente estas clasificaciones ni promocionar remediación o G1.

## Viabilidad e implementación aislada

No se modifica `AddNotesEngine`. El punto productivo sigue siendo exactamente
`lanes[rng.Next(lanes.Count)]`. El motor ya recibe la abstracción `IRandomSource`; el harness suministra
`SingleRngInterventionRandom`, que envuelve una instancia real e independiente de `SeededRandom`.

En todas las llamadas, el wrapper consume y registra el valor auténtico. Sólo cuando la posición previa
es 260 exige `Next(4) == 2` y puede entregar 3 al motor. No omite ni añade llamadas. El transcript
auténtico conserva el valor 2; la sustitución entregada queda en un registro separado. Cualquier máximo,
valor o cantidad diferente invalida la ejecución.

El sink de diagnóstico publica candidates al completar la oportunidad, no antes de `Next`. Por ello el
wrapper no usa un key mutable en vivo. La identidad se asegura mediante una cadena cerrada de
precondiciones: ejecución canónica congelada, padre suficiente completo, prefijo auténtico exacto de 260
llamadas, posición 260, `Next(4) == 2`, opportunity cursor/key y lanes `[0,1,2,3]`; el diagnóstico de la
misma ejecución debe confirmar todo después de la llamada. Si no lo hace, el resultado es `INVALID`.

## Caso congelado

- Chart SHA-256: `20651C9B11DBB0D7BA2D64A8F167577AE0452762EEA83C5A7558BA89B8D2C788`.
- Seed: 4.
- Política de ambos brazos: remediation treatment canónica, G1 ausente, chance 0.50,
  articulation desactivada como en la evidencia E histórica.
- Intervención: OP-185, 16399 ms, lanes canónicas `[0,1,2,3]`.
- A: consume `Next(4) == 2`, entrega 2, commit lane 2.
- B: consume `Next(4) == 2`, entrega 3, commit lane 3.
- Prefijo esperado: 260 llamadas; sucesor de OP-185 en posición 261.
- Mediador observado: OP-370, 29933 ms.
- Target: OP-466, provenance S466, lane 2, 35009 ms.
- Commit exacto esperado: `2|35009||Tap|466|HeadOpportunity|74.49995000000002979998|74.49995000000002979998`.

Los números anteriores son criterios, no sustitutos de la comprobación runtime.

## Brazos y aislamiento

Cada brazo vuelve a parsear el texto del chart, construye un `MapperEvidenceProfile` independiente,
crea un engine nuevo y usa un RNG nuevo. Se comprueba además que chart y profile no comparten referencia.

Antes del contrato se ejecutan:

1. canonical normal A;
2. canonical normal repeat;
3. canonical con wrapper activo pero entrega pass-through 2.

Los tres deben coincidir exactamente en bytes serializados, commits/diagnóstico, llamadas y transcript
RNG. La referencia debe coincidir además con los hashes históricos publicados. Sólo entonces se congela
el contrato.

## Igualdad inicial y no interferencia

Inmediatamente antes de OP-185 se exige igualdad completa de:

- geometría materializada;
- geometría latente comprometida;
- posiciones root/stage RNG;
- cursor de oportunidad;
- articulación pendiente;
- hash de estado suficiente;
- configuración;
- key y orden;
- prefijo/transcript RNG auténtico.

Ambos sucesores deben ser completos, terminar en RNG 261 y diferir materialmente sólo a partir de la
selección entregada. Debe existir un único evento de intervención.

Después de OP-185 no se sincroniza, fuerza ni corrige ninguna decisión. RNG, commits y oportunidades
pueden divergir naturalmente como mediadores.

## OP-370 y OP-466

Para OP-370 se registran alcance, candidate, lanes legales, selección, commit, rechazo, padre y RNG.

Para OP-466 se separan:

- oportunidad alcanzada;
- candidate producida;
- lane 2 propuesta/seleccionada;
- geometría canónica que permite lane 2;
- commit exacto.

Un objeto sólo parecido en lane 2/35009 no es recuperación exacta.

## Outcomes congelados

- `RECOVERED_EXACT`: A no compromete el target y B sí compromete exactamente la identidad congelada.
- `ABSENT_BOTH`: ninguno compromete el target exacto.
- `SIMILAR_NOT_EXACT`: B produce algo similar, pero no la identidad exacta.
- `UNEXPECTED_BASELINE`: A no reproduce la trayectoria canónica histórica o compromete inesperadamente el target.
- `INVALID`: falla identidad, aislamiento, legalidad, RNG, determinismo o precondición.
- `BLOCKED`: falta corpus/artifact o existe impedimento técnico/material.

Los criterios no se modificarán después de observar B.

## Condiciones de parada

Se detendrá sin reinterpretar si cambia la implementación conductual, falta evidencia congelada, el
control histórico deriva, los padres no son idénticos, lane 3 no es legal, la llamada no es exactamente
`Next(4)==2`, hay cero/más de una intervención, el consumo no termina en 261 o falla el límite de recursos.

No se ejecutará la matriz de 224 pares, no se explorará `Songs` y no se probarán lanes/seeds alternativos.

## Evidencia prevista

Toda la evidencia nueva se escribirá en `.artifacts/safety_op185_op466_counterfactual/`, separada de
los artifacts históricos: contrato, identidades, manifest, controles, snapshot previo, intervención,
comparaciones OP-185/370/466, trayectoria, transcripts/fingerprints, resumen y hashes.

