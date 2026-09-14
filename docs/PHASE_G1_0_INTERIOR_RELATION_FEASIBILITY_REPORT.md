# PHASE G1.0 — INTERIOR LN RELATION FEASIBILITY REPORT

**Status: COMPLETE — OUTCOME A / SHADOW. behaviorChange=false.**

## 1. Baseline and frozen identity

Initial repository HEAD and origin/main were `b926d117c88495e2daf40b37b2a814cd37e35c5f`; baseline Release tests were 656/656 and default policy was `legacy-experimental.1`. Two pre-existing untracked strategic review documents made the initial worktree non-clean; they were preserved and excluded from this phase's snapshots.
Contract `7C04E4CD9B45EE9351FBDC3C179083AAE43A5906115815A9F7987E0C51412194`; implementation snapshot `E70709A74CB45F880F1599F40F1A851635F0B1CBDF75C3065992C3F35269972A`; C11 fingerprint `878585D604E807B7E46AB429990C7DA6DB396AB0B7E930DAC2B631C8DE7ECD77`.

Before accepting the human aggregate, the research-only geometry check was corrected so an exact candidate endpoint may meet a tap at equality, matching `LaneGeometryIndex`. The affected preliminary snapshot was discarded; the design and implementation snapshot above were frozen again before the definitive C11 run. Outcome criteria and relation/holdout definitions were not changed.

## 2. Problem and current architecture

G1.0 asks whether exact original-only parent/anchor/witness relations exist before any behavioral G1. `MapperEvidenceProfile` already provides stable observations and interior anchors; this phase adds a research-only census and never calls it from CLI/Web generation. Current generation first gates parent length/context and anchor support/context, ranks anchors, then caps at two. In AnchorSupported mode, `InteriorLengthRatio` and `InteriorAbsoluteLongBeats` are not active gates.

## 3. Exact relation, anchor and complete witness

An occurrence is one original parent LN plus one exact profile anchor plus one original LN witness whose head is that anchor. It retains exact IDs/times/beats/endpoints/lanes. Contained, EqualEnd and Crossing are separate. Head, Release and Head+Release anchors remain separate; a release-only anchor has no complete child relation unless an LN head exists at that same anchor. Same-lane is descriptive, not authority.

## 4. Structural population and relation results

C11 contains 12554 original LNs. The census found 22162 structural anchors and 16881 complete same-occurrence relations. Marginal-only anchors: 11667. Parent-duration bands: <3: 15726; 3-<4: 555; 4-<8: 563; 8+: 37.
- Contained: 5617 occurrences / 7 families / 7 charts.
- EqualEnd: 3093 occurrences / 7 families / 7 charts.
- Crossing: 8171 occurrences / 6 families / 6 charts.
Same-lane: 0; different-lane: 16881. Geometry-valid after witness holdout on its observed lane: 16881; invalid: 0. Serialization-valid exact-ms facts: 16881; unresolved/collision: 0.

## 5. Alternatives, holdouts and leakage

The query identity is exact keymode + parent duration + anchor offset + AnchorKind. The result identity is exact relation class + duration from anchor + offset from parent end. No vote or frequency winner exists. TargetObservation and ParentOccurrence are reported separately; donors are earlier, chart-local and original-only.
Holdout queries: 33762; comparable: 22332; exact joint supported: 14584; marginal-only: 284; unique: 1138; among alternatives: 13446; conflicting exact claim: 7748.
Target, parent, release, future, same-event, synthetic and cross-chart measured leakage: 0 in every category.

## 6. Current-gated population and attrition

Current interior opportunities: 298; complete relation occurrences attached to them: 288. First-exclusion counts: source length 20076, source context 185, anchor support 8, anchor context 0, cap=2 1595. Cap exclusions carry 706 Contained, 56 EqualEnd and 100 Crossing occurrences. These are ordered pipeline facts, not proof that excluded relations were bad.
Parents with 0/1/2/3+ structural anchors: 4729/3046/1926/2853. Ranking only orders; cap exclusions retain their relation-class labels in the CSV.

## 7. Magic-number ownership

`MaxInteriorOpportunitiesPerSource=2` is provisionally an INTENSITY_OR_CAPACITY_CANDIDATE because it removes already-eligible anchors rather than defining relations. `ArticulationMaxNonHeldColumns=1` is only the separate G2 boundary and is not studied. Window, source-length, context and minimum-anchor thresholds remain LEGACY_UNRESOLVED: their attrition is measured but C11 does not establish ownership. Relative/absolute length values are legacy compatibility in the current AnchorSupported policy and had zero current gate effect.

## 8. What G1.0 does not prove

Counts do not authorize a preferred class, endpoint, lane, selector, quantity, articulation or default. C11 is a development corpus, not independent validation. Geometry validity is not style evidence. File-exact relations do not establish latent musical identity.

## 9. Outcome and recommendation

Outcome A: exact same-occurrence relations are representable, leakage/RNG/mirror checks are zero, at least one relation class occurs in more than one C11 family, and held-out comparable cases exist. This justifies only a future human-reviewed G1 behavioral design/gate; G1 behavior remains NOT_AUTHORIZED.
G2 articulation: NOT_AUTHORIZED. H multiple articulation: NOT_AUTHORIZED. C2 remains DEFERRED. F2.ACQ remains BLOCKED ON EXTERNAL DATA. MapperSupport remains NOT_AUTHORIZED. D1 and D1.SAFETY remain COMPLETE/C/PARKED; SAFETY.PROV remains COMPLETE/A/SHADOW. Default remains `legacy-experimental.1`.

## 10. Validation and repository state

Final validation: `dotnet restore` PASS (only the known NU1900 vulnerability-service warning), Release solution build PASS, full tests **672/672**, DocConsistency PASS and `git diff --check` PASS. The final current-opportunity mirror mismatch count is zero, research RNG calls are zero, and `.artifacts/g1_0/g1_0_detail.json` is ignored with no `.artifacts/g1_0` file tracked.

The review worktree contains 11 modified tracked files plus the G1.0 untracked source/test/runner and public artifacts. The two pre-existing untracked ASTRA strategic documents remain untouched and outside the phase snapshot. Staging is empty. No `git add`, commit, push, tag or release was performed.
