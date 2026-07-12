# Assets to generate — Tripo brief (W5.12, 2026-07-12)

Derived from the manuscript Appendix C apparatus lists (`Docs/experiments-reference.md`).
Generate each in the Unity **AI panel** (MCP Tripo loses the reference-image instance ID —
see CLAUDE.md gotcha), then save the prefab into this folder under the **exact name** in
bold so the wiring can path-load it. After saving, tell Claude and it wires each into
`SceneAssetLibrary` + `RealSizes`/`PhysicsProfiles` + `Mishandling` and places it.

Style to match the ChemLab pack: clean borosilicate glass / matte metal, neutral studio
look, single object centered, no background props.

## Priority 1 — completes the distillation train (6 of 9 experiments distill)
1. **Condenser** — a Liebig condenser: straight inner glass tube inside an angled outer
   water jacket, two hose nipples (top + bottom), ~25 cm, borosilicate glass. Real length 0.28 m.
2. **RubberStopper** — a tapered rubber/cork stopper with ONE bored hole through the centre
   (for a delivery tube), dark grey-brown, ~3 cm. Real length 0.04 m.
3. **DeliveryTube** — a bent glass delivery tube (right-angle "L", ~15 cm legs) joined to a
   short length of amber rubber tubing; for leading CO2/vapour from a stoppered flask to a
   receiver. Real length 0.20 m. (Also serves the Methane gas-collection + fermentation steps.)

## Priority 2 — standard, improvised today
4. **WaterBath** — a shallow round metal water bath / beaker-in-bath: a wide low aluminium
   pan of water a flask sits in, ~15 cm across. Real length 0.16 m. (A 500 mL beaker of
   water improvises this today — lowest urgency.)
5. **UtilityClamp** — a 3-prong burette/utility clamp with a boss head, matte steel + cork
   jaws, ~18 cm; clamps a flask or condenser to the retort stand. Real length 0.20 m.
   (We have the iron ring; this is the "hold the glassware" upgrade.)

## Priority 3 — nice to have
6. **Aspirator** — a bench water-aspirator / filter pump, small tapered brass-and-glass
   fitting, ~12 cm. Real length 0.12 m. (Listed in every experiment header but rarely
   central to a graded step.)

## Already done — DO NOT regenerate
- In this folder (prefabs): SeparatoryFunnel, PowderJar, IceBucket, Matchbox, LitmusBox,
  CottonSwabs, FilterPaper, MatchstickSingle, LitmusStripSingle, CottonSwabSingle.
- FlorenceFlask.glb exists here — needs a PREFAB made from it (wiring task, not generation).
- Raw model packs already in `Art/Equipment/` (wiring task, not generation):
  DistillationFlask (now prefabbed as DistillingFlask), MechanicalPipette, Thermometer.
