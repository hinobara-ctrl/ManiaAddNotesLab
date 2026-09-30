# LANE.0 — Corrective execution preparation

## Estado

`READY FOR MANUAL PUBLICATION AND INDEPENDENT AUDIT / BINDING NOT YET ISSUED / C11 EXECUTION BLOCKED`

Esta fase prepara la única ruta futura de evaluación correctiva. No autoriza ni ejecuta C11, no abre `.osu` reales, no reconstruye los once `OccurrenceId` históricos y no produce conteos corregidos ni un outcome científico nuevo.

## Baseline y alcance

- baseline publicado: `9b065535248e7dd7581822af80cf6534b16922eb`;
- parent: `f3037260f3f5d7e74e906885b6b7ed1c3f2d1510`;
- solución congelada: `BFEB1689414A2FB712170A967B9BA6DAD70BFE656D307A25821A976D81942A37`;
- contrato de preregistro: `6392C579B87EC318C1DE381201D797B004650AB961398E3E2AB23F7A49A83D33`;
- contrato de hardening del evaluator: `A61F0933A49534857096D3A568E9C07F37805F9468CC94001546767073CD2E18`;
- implementación del evaluator: `20D0AC6BB0AF197ACC6BE1B5E71D1DF65BD2CDC0FCAB0E49FD6F036D03A0D8F4`;
- harness del evaluator: `724837E42F2308DA3159BAE00F3339656C03EE936B9E350F416124A7E92AA173`;
- manifest esperado: `AC28C73F65B7FC896E02046E9715C8A24156FBB9B200439657B6A6F0F3E81445`.

Los archivos científicos y de hardening anteriores permanecen intactos respecto de `9b065535…`. No se modificaron la solución, `Program.cs`, el engine, el evaluator, sus tests ni los contratos históricos.

## Arquitectura successor

La nueva implementación separa tres responsabilidades:

1. `Lane0CorrectiveExecutionPreparation` valida evidencia publicada, ruta canónica, binding, HEAD y autorización.
2. `Lane0FrozenCorrectiveCorpusAdapter` encapsula el único acceso futuro al manifest y al corpus explícito. La llamada a `FrozenC11ManifestResearch.Load` y `C11CorpusDiscovery.ResolveFrozen` sólo existe detrás del gate autorizado.
3. `Lane0CorrectiveEvaluationRunner` conserva toda la ciencia congelada. El adapter no añade queries, thresholds, selección, reroll, RNG ni autoridad conductual.

El adapter futuro exige 12 ubicaciones físicas, 11 contenidos únicos, una ubicación duplicada exacta, 11 familias y keymodes 4/7/10. La identidad científica es el SHA-256 del contenido; la ruta local no lo es. Los inputs únicos se ordenan ordinalmente por `ChartId` y cada carga vuelve a parsearlos.

## Ciclo de vida del publication binding

El formato cerrado es `lane-0-corrective-evaluation-publication-binding.1` y exige exactamente:

- `schemaVersion`;
- `approvedPublishedHead` de 40 hex exactos;
- las identidades del contrato de preparación, preregistro, hardening y manifest;
- `explicitHumanAuthorization`;
- `authorizedExecutionCount = 1`.

Se rechazan propiedades extra o faltantes, hashes malformados, HEAD o identidades divergentes y cualquier count distinto de uno. Binding ausente o autorización `false` producen `BLOCKED`; binding malformado o divergente produce `INVALID`.

El archivo autoritativo `docs/lane_0_corrective_evaluation_publication_binding.json` permanece deliberadamente ausente. Este successor aún no tiene un SHA publicado y auditado, por lo que no existe un HEAD válido que pueda autorizarlo sin circularidad. Ningún template fue creado.

## Orden pre-corpus congelado

1. validar contratos e identidades publicadas;
2. exigir la ruta canónica y leer/validar el binding;
3. comprobar `approvedPublishedHead == HEAD`;
4. comprobar autorización humana explícita;
5. comprobar una única evaluación autorizada;
6. cargar y verificar el manifest congelado;
7. resolver y abrir exclusivamente el corpus root explícito;
8. construir once inputs únicos y ordinales;
9. ejecutar dos evaluaciones independientes y comparar todos los artifacts byte a byte;
10. escribir una sola salida completa.

Cualquier fallo en los pasos 1–5 termina con cero llamadas al adapter. La ruta también rechaza un binding que no esté en el path canónico y un output que no sea `.artifacts/lane_0_corrective`.

## Inputs científicos publicados

El snapshot RICE/G1 y la regla histórica G1 de once casos se reconstruyen determinísticamente desde `lane_0_corrective_evaluator_hardening_contract.json` y se comparan contra las constantes del evaluator. La regla exige keymode 7 y distribución 9/2 entre los dos chart hashes publicados. No se inventan identidades individuales.

## Determinismo y semántica one-shot

La rama futura autorizada carga y parsea dos veces el corpus, ejecuta dos veces el evaluator y serializa cada resultado de manera independiente. Sólo una igualdad byte a byte de los seis artifacts permite escribir. Cualquier divergencia, uso de RNG, mutación de inputs, cambio conductual o resultado `INVALID/BLOCKED` impide la escritura.

La única salida admitida es `.artifacts/lane_0_corrective/`, con política `NONEXISTENT_OR_COMPLETELY_EMPTY_ONLY`. No hay retry, overwrite, limpieza, variación metodológica ni selección de outcome. Los únicos nombres permitidos son los seis ya congelados por el evaluator.

## Controles sintéticos

El proyecto successor separado no está registrado en la solución congelada. Sus bad controls cubren binding ausente/no autorizado/malformado, todas las identidades, HEAD, count, placeholder, rutas no canónicas, inventario faltante/inesperado/mismatched/duplicado, orden estable, parse único, RNG cero, no mutación, dos evaluaciones reales, higiene de output, reproducción del snapshot/regla y ausencia del binding final.

Todas las cartas usadas por esos tests son strings sintéticos en memoria. No se consultó un directorio real de corpus ni una biblioteca `Songs`.

## Identidades successor

- contrato canónico: `61C05856AC8BB48ABFC5BCFD3AA47599D2D5F567CBA6E39EC7AF27DD235B8F1E`;
- implementation tree: `A518278E7B0CCBEA8C7916720035C598073382CFF2616E790340DD6F45F8D219`;
- harness tree: `0501B1AA2625259AB66249A749C0963CE725C6678E2591CEF5D3CC3D7ADDC170`;
- verifier normalizado: `182CBAEFCD3B010EBA7C4505DA8F105BF3732E9D5D2BCD4D042531B920B201E0`.

El verifier recalcula esas identidades, las identidades parent, la solución, la ausencia del binding, el estado cerrado y la igualdad de los archivos congelados contra el baseline. También comprueba que no exista una ruta productiva/Web/CLI hacia el successor.

## Validación local

Con `DOTNET_GCHeapHardLimit=0x400000000`, `DOTNET_GCConserveMemory=9` y ejecución secuencial:

- restore de la solución: PASS;
- build Release de la solución: PASS, 0 errores;
- suite congelada: 921 passed, 0 failed, 0 skipped (868 core + 53 corrective);
- evaluator correctivo directo: 53 passed, 0 failed, 0 skipped;
- successor directo: 31 passed, 0 failed, 0 skipped;
- DocConsistency: PASS;
- verifier de hardening: PASS;
- verifier de execution preparation: PASS.

El warning `NU1900` indicó únicamente que no fue posible consultar el índice remoto de vulnerabilidades de NuGet; los paquetes ya estaban disponibles y no afectó compilación ni tests.

## Resultado de esta fase

- C11 access: **NO**;
- `.osu` real opened: **NO**;
- corrective evaluation executed: **NO**;
- scientific result produced: **NO**;
- behavior change: **NO**;
- RNG change: **NO**;
- default change: **NO**;
- publication binding issued: **NO**.

El estado científico anterior no cambia: `LANE.0 HISTORICAL FEASIBILITY_DEMONSTRATED / GLOBAL CERTIFICATION SUSPENDED / PENDING_RECERTIFICATION / C11 NOT AUTHORIZED / NO BEHAVIORAL PROMOTION`.
