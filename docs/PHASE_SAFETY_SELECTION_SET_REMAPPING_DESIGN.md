# SAFETY.REMEDIATION — Selection-Set Remapping Design

Estado: **PREREGISTERED BEFORE OFFICIAL COUNTERFACTUALS**  
Baseline: `52f21af56f8034e07fcaa9d849cb9440b9429e73`

## Hipótesis

H1: con un estado RNG idéntico, `lanes[rng.Next(lanes.Count)]` puede seleccionar otra lane cuando la
autoridad canónica modifica la cardinalidad o el orden del conjunto, incluso si la lane elegida por
control permanece canónicamente legal. Esa selección distinta puede producir sucesores materiales
distintos sin ser un rechazo directo del commit de control.

## Población y límites

La población oficial contiene exclusivamente las ocho ejecuciones ya localizadas por la forensia:
02D9D178 seeds 5/20 primary y 20651C9B seeds 4/10/11/14/19 primary más seed 11 secondary/G1.
El corpus es `.artifacts/f2-1-corpus`, validado por el manifest C11. Primary y secondary son unidades
separadas. No se explora `Songs`, no se ejecuta la matriz de 224 y no se modifican artifacts históricos.

## Selector auténtico y estado congelado

El selector productivo observado es exactamente `lanes[rng.Next(lanes.Count)]`, sin reroll. El
instrumento captura cada llamada anterior como Double o Integer, incluido maximum y resultado. Cada
contrafactual crea un `SeededRandom(seed)` nuevo, reproduce y verifica bit a bit el prefijo, y ejecuta
una única llamada `Next(cardinality)` sobre una copia aislada. No se aproxima mediante módulo y nunca
modifica el RNG de la generación de referencia.

Variables constantes: chart, seed, options, AddChance 0,50, articulation OFF, orden lógico de
oportunidades, prefijo RNG, configuración comparable, opportunity key y estados padres. La única
intervención local oficial es el ordered legal-lane set entregado al selector.

## Contrafactuales

- A: ordered legacy legal lanes reales.
- B: ordered canonical legal lanes reales.
- C: igual cardinalidad, fixture sintético explícito; no implica equivalencia geométrica.
- D: mismos elementos con orden permutado, fixture sintético explícito.
- E-control: conjuntos distintos que conservan la misma selección, fixture negativo.

C y D no se aplican a charts reales como si sus restricciones fueran intercambiables.

## Estado suficiente y equivalencia

Se conserva el estado suficiente congelado: geometría materializada, geometría latente comprometida,
posición RNG root/stage, opportunity cursor, pending articulation y secuencia de oportunidades. La
igualdad causal requiere ambos estados completos y hashes iguales. La no interferencia compara bytes,
commits, número/transcript RNG, fingerprints, configuración e identidad G1.

## Categoría experimental E

`E_SELECTION_SET_REMAP` se demuestra sólo si:

1. los padres son completos e iguales;
2. es la misma oportunidad y configuración comparable;
3. legacy/canonical ordered sets están identificados y difieren;
4. la intervención canónica está aislada;
5. la lane de control sigue presente en canonical;
6. la selección auténtica reproducida difiere;
7. seed, prefijo, posición y una llamada RNG son iguales;
8. no existe otra intervención concurrente que explique directamente esa selección;
9. ambos sucesores son completos y materialmente distintos.

E se abstiene si falla cualquier criterio, en particular padres incompletos/desiguales, oportunidad o
configuración distinta, set sin cambio, lane de control rechazada, selección igual, RNG no comparable,
intervención concurrente o sucesores no materialmente distintos. E es experimental y no modifica A/B/C/D.

## Lineage experimental

E abre únicamente en la primera transición que satisface los nueve criterios. Una igualdad completa
posterior cierra la lineage. Una intervención posterior se registra por separado; un rechazo directo
puede seguir siendo A a nivel de objeto, pero no se convierte en origen de E. Persistencia temporal no
demuestra por sí sola que E explique un target ausente.

## Caso prioritario y downstream

Primero se ejecutará OP-185 de 20651C9B seed 4 y controles sintéticos. Sólo si pasan no interferencia y
memoria se extiende a siete runs. OP-370 y OP-466 se inspeccionan sin atribución automática. Una cadena
completa a OP-466 exigiría aislar E durante toda la trayectoria, conservar estados/decisiones/RNG
comparables, descartar causas independientes y no forzar lanes ilegales. Si eso requiere una mutación
conductual sustancial, no se implementará sin autorización.

## Controles y stopping criteria

Controles positivos/negativos cubren cardinalidad, orden, contenido, empty set, mismo/distinto consumo
RNG, padres, sucesores, A posterior, reconvergencia, causa independiente y estratos distintos. Se detiene
la ejecución oficial ante drift de contrato/implementation/harness/corpus, fallo de prefijo RNG,
no-interferencia, identidad G1, memoria, corpus o artifacts.

## Outcomes

- `E_DEMONSTRATED`: E satisfecha en todos los casos oficiales; no implica atribución downstream ni recertificación.
- `PARTIAL`: sólo algunos casos o sólo causalidad local.
- `NEEDS_REVIEW`: contradicción o defecto de instrumentación.
- `BLOCKED`: falta una dependencia necesaria.

## Artifacts esperados

Contrato, manifest de ocho casos, decisiones originales, contrafactuales, evidencia del selector,
E/ABSTAIN, tests, no interferencia, OP-185, downstream OP-370/OP-466, hashes y summary bajo
`.artifacts/safety_selection_set_remapping/`, más un reporte final separado.
