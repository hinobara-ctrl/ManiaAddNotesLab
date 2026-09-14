# PHASE G1.0 — INTERIOR RELATION FEASIBILITY — PRE-RUN DESIGN

**Status: FROZEN BEFORE C11 AGGREGATES. behaviorChange=false.**

- RepositoryEntryHead: `b926d117c88495e2daf40b37b2a814cd37e35c5f`
- origin/main: `b926d117c88495e2daf40b37b2a814cd37e35c5f`
- Contract SHA-256: `7C04E4CD9B45EE9351FBDC3C179083AAE43A5906115815A9F7987E0C51412194`
- Implementation snapshot SHA-256: `E70709A74CB45F880F1599F40F1A851635F0B1CBDF75C3065992C3F35269972A`
- C11 fingerprint: `878585D604E807B7E46AB429990C7DA6DB396AB0B7E930DAC2B631C8DE7ECD77`
- Inventory: 11 charts / 11 families / 50836 objects / 12554 LNs / keymodes 4,7,10.

## Frozen relation identity

One occurrence is one original parent LN, one exact interior anchor from `MapperEvidenceProfile`, and one original LN witness whose head is that same anchor. The record preserves both observation IDs, exact serialized times, exact file-derived decimal beats, lanes, duration from anchor, offset from parent end, anchor kind, relation class and deterministic semantic ID. Marginal duration/release values are never joined into an occurrence.

## Frozen populations

The structural population enumerates all original-only strict interior anchors before production gates. The current-gated population mirrors the present `AnchorSupported + OriginalOnly` pipeline at LnWindowBeats=4, source minimum=3 beats, context minimum=3, anchor support minimum=2 and cap=2. Pipeline attrition is ordered; ranking is descriptive and cap attrition is separate.

## Frozen holdouts

`TargetObservation` excludes the complete target witness from every donor role. `ParentOccurrence` additionally excludes donors from the same parent. Both are chart-local, prior-only (`donor.AnchorTime < target.AnchorTime`), whole-event safe, and exclude target-as-parent, target release, synthetic and cross-chart evidence. Parent context remains a query fact, not an independent relation donor. The exact query is keymode + parent duration + anchor offset + AnchorKind; the exact joint result is relation class + duration from anchor + offset from parent end.

## Metrics and outcomes

Frozen metrics are the contract inventory, structural/current populations, relation and anchor-kind counts, same/different-lane description, marginal-vs-joint, alternatives, both holdouts, leakage, ordered gate attrition, cap exclusions, geometry validity, serialization validity, per chart/family/keymode distribution, determinism and zero RNG. Outcome A requires exact representation, zero leakage/drift/RNG, a relation class in more than one C11 family, and real held-out comparable cases. Exact representation without that basis is B. Inference, leakage, synthetic contamination or behavior drift is C/STOP.

## Hard aborts

Any default/policy/RNG/output change, normal CLI/Web callsite, G1/G2/H behavior, MapperSupport, C2, community corpus, synthetic style evidence, target/cross-chart leakage, post-result contract edit or DocConsistency failure aborts. No commit or push is authorized.
