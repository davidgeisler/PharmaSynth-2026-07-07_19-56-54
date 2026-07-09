# PharmaSynth — Remaining-Work Checklist (THE single tracker)

Everything still to do before the **2026-08-31** turnover, consolidated **2026-07-09** after the W5.6 client-workflow batch (suite **385/385** green). Check items off as they land. `[ ]` open · **bold** = Tier-1 contract-critical (Tutorial + Prelims + Benzoic Acid + Aspirin). Companion docs: [gaps.md](gaps.md) (analysis), [project-overview.md](project-overview.md) (what exists), [on-device-test-plan.md](on-device-test-plan.md), [client-signoff-request.md](client-signoff-request.md), CLAUDE.md (session handoff).

**DONE and out of scope of this list** (see CLAUDE.md for detail): engine + scoring/BKT + progression persistence, all 11 experiments as data + auto-built stages + sim-rig verbs (17/42 stations), pour path + depletion (finite bottles, restart prompt), door-gated campaign flow (choice panel → episode → lab-coat → armed load → walk-in timer → debrief → teleport return → unlock announce), screen-locked HUD + Pharmee dialogue bar + only-while-speaking bubble, ScreenFader, in-lab Settings/Restart/Quit, MainMenu spawn room, real-size pass (RealSizes, 42 prefabs), labcoat PPE in the locker (click to don), swinging lab door (solid open + closed), head-collision pushback, 44 cutscene SOs wired, 33-MCQ post-lab quiz + yield stepper, CC0 audio wired (beeps/UI/glass/alarm/music), hub/select UI, results CSV backend.

---

## 1. Per-experiment content wiring

- [x] **Product-seeded test vessels per experiment** — DONE 2026-07-09: 8 layouts got a `TestTube_WithLiquid` vessel seeded with the product (Aspirin→salicylic for the FeCl3 phenol test, Benzoic, Acetanilide, Benzamide, Chloroform, Acetone, Ethanol, Wine→CO2 for limewater) and the `test-*` pour bindings moved onto it; Benzamide also gained the missing NaOH + NaNO2 reagent vials for its alkali/nitrous tests. `TestVesselSuite` (38 assertions) pins each seeded vessel, its bindings, AND that `MasterReactionRegistry.FindReaction(seed, reagent)` fires; plus every chemical any layout references must resolve through `SceneAssetLibrary`. *Exceptions: Methane = hand-built stage (own rig); ChemicalCompounding already pours ethanol into the test beaker first (functional as-is); Caffeine has no test rules yet (murexide — see next bullet). BenzoicAcid `test-ester` + Benzamide `test-acid` pours land on the seeded vessel but await their rules (next bullet).*
- [ ] Remaining reaction rules — mostly DONE 2026-07-09: added `Test_AcetoneIodoform` (yellow ppt), `Test_BenzoateEster` (ester odour, `test-ester`), `Test_BenzamideAcid` (hydrolysis ppt, `test-acid`); registry now 20 rules, all verified firing via the seeded test vessels. *Still open: acetone **Schiff** (needs a Schiff-reagent `ChemicalData` + station wiring) and **caffeine murexide/melting-point** (needs `Chem_Caffeine` + murexide reagent + a test binding — currently station-only).*
- [ ] **Methane verb polish**: mortar grind (prepare-mixture), splint flame-test visual, flame/bubble VFX.
- [x] Wine bespoke rubric — RESOLVED 2026-07-09 (client): keep the **standard 6-category rubric** for consistency with the other experiments; no bespoke wine categories. No code change needed.
- [ ] Per-experiment ILO cards ×11 (intro-cutscene card art).
- [ ] Data-sheet expected-yield ranges per experiment (display/reference only). **Yield-in-grade RESOLVED 2026-07-09 (client): yield is a lab-record entry ONLY — it never feeds the grade** (not a manuscript grading category; quiz MCQs stay the sole Documentation driver).
- [ ] `HazardZone` placement per experiment (hot surfaces, spills); `WeighingScaleController` wiring where weighing is graded. *(Glassware breakage + reagent spilling moved to the dedicated §2 penalties bullet.)*

## 2. Physics & interaction pass

- [x] **Physics-attributes / resting-pose audit for ALL items** (user request 2026-07-09, task #78) — DONE 2026-07-09: `PhysicsProfiles` table (42 items: mass + resting pose, companion to `RealSizes`), `GrabPhysicsPolicy` (kinematic-on-shelf → dynamic-on-release), builder applies profiles + rest-pose rotation; concave MeshColliders convexified (PhysX rejects them on dynamic bodies — 5 items fell through the world before the fix); degenerate flat-tool colliders padded. Verified: **Tools ▸ PharmaSynth ▸ Physics Audit (Drop Test)** 42/42 settle plausibly, **(Report)** clean (both re-runnable; + **(Fix Scene Items)** applied & scene saved). Suite 385 → 412.
- [x] XRI **sockets** at stations — DONE 2026-07-09: builder spawns an `XRSocketInteractor` per station pad with a `StationSocketFilter` (accepts ONLY the station's required item — wrong props bounce off; all 42 prefabs verified to carry `XRGrabInteractable`). **Drop respawn** (kill-Z + idle return-to-home) also DONE: `DropRespawn` on every spawned prop. *Still open: sockets on test-tube RACK holes (needs per-mesh anchor authoring — do with the art pass).*
- [x] **Spill & breakage mishandling penalties** (user request 2026-07-09) — CORE DONE same day: `Mishandling` policy table (20 breakable glass/porcelain prefabs; metal/wood/plastic never break; break ≥2.8 m/s ≈ 0.4 m drop; spill = un-held + tipped >60° + has liquid), `BreakableGlassware` reworked onto the modern runner (dynamic-only, shatter SFX key, `DroppedGlassware` mistake → Sanitation rubric, replacement via `DropRespawn.GoHome`), new `SpillMistake` (one `SpilledReagent` mistake per episode, re-arms when righted; the lost ml already drains via `LiquidPourer` → starvation → restart path), grader counts both against Sanitation. Builder auto-attaches to spawned glass/pourables. 14 assertions (`MishandlingSuite`). *Still open (art/VFX bucket): puddle decal + cleanup wipe task, shatter VFX; client may veto free replacements in exam periods (sign-off list).*
- [ ] Teleport anchors at each workstation (only the floor `TeleportationArea` exists).
- [ ] Refine crude convex hulls on tall apparatus (tripod, retort stand, burner).
- [ ] XRI interaction-layer audit (everything on default layers; sockets/hands need masks).
- [ ] Prop readability check after the human grab-test (real-scale items are small — slight scale-up vs highlight shader).

## 3. Art & models

- [ ] **Pharmee animation set** (idle-float/talk/gesture/celebrate/warn) + face-state materials + re-point `PharmeeFace.faceRenderer` at the screen mesh (still `Ears_Black_Matt_0`).
- [ ] **Dr. Jimenez**: model ON HOLD (client may supply their own — decision 2026-07-09); `ExaminerNPC` behaviour work can proceed against the primitive stand-in.
- [ ] Real **fume hood** model — reference image generated (`Art/Generated/Models/FumeHood_Ref.png`); **image→3D blocked by an MCP bug** (Tripo needs `referenceImageInstanceId`, but this project's 64-bit instance IDs lose precision in JSON transit). **Manual 30-s step for the user: select FumeHood_Ref.png in the AI panel → Generate 3D Model → save to `Art/Generated/Models/`.** Then: retopo if heavy, swap into `FumeHoodZone` stand-in.
- [ ] **Clean reagent-label textures** — base designs DONE 2026-07-09 (`Art/Generated/Labels/LabelBase_Apothecary.png` + `LabelBase_Modern.png`, both text-free by design). Next: pick ONE style (user call), then an editor script composites the 16 chemical names + hazard notes as crisp TMP/GUI text onto the base and applies to bottle materials — never AI typography.
- [ ] PPE completion: gloves + goggles as clickable items like the coat; coat visibly ON the player (worn visual / mirror reflection); glove material swap on hands.
- [ ] Wrist-watch 3D model (primitive canvas today) + gesture tune on-device.
- [ ] Fermentation set (vessels, airlocks ×3, balloon), wine bottle + glass, tea/caffeine props, separatory funnel (check pack first), WASTE bin.
- [ ] VFX set: smoke, steam, fire, glass shatter, confetti (URP particles, ≤3k live).
- [ ] **Per-experiment demo video clips + `VideoPlayer` TV screen** (0 VideoClips exist; storyboard expectation).
- [ ] UI art skins: grade screen, tablet, HUD pills, station pads (readable primitives today); MainMenu room polish beyond primitives.
- [ ] Proper VR glass shader (stereo-instancing validated, Quest overdraw budget; cheap fallback ready).
- [ ] Lighting: full lightmap re-bake + light probes once layout locks (placeholder realtime today).

## 4. Audio (infrastructure + CC0 base DONE)

- [x] Gentle lab **ambient loop** — DONE 2026-07-09: AI-generated soft ventilation hum (`Audio/Generated/ambient-lab.wav`), wired at volume 0.15 (Ambient category). **Needs a listen in-editor.**
- [x] Pour / bubble-boil / **burner-ignite** SFX — DONE 2026-07-09: AI-generated (ElevenLabs), wired to their SoundBank keys. **Needs a listen.**
- [ ] **Action & apparatus SFX set** (user request 2026-07-09) — CLIPS STARTED: `stir` + `footstep` generated & wired as new SoundBank keys (AI budget approved). Still to generate: grab pick-up + release, drop clatter per material (glass clink exists / metal / wood), reaction fizz cue, mixture-complete chime, crystallise shimmer, filtration drip, gas hiss, PPE rustle, door creak, socket click. Then the **trigger hookups**: footsteps ↔ locomotion, drop clatter ↔ `GrabPhysicsPolicy`/collision impulse, stir ↔ vessel swirl, boil loop ↔ `TemperatureSim` at target, reaction cue ↔ `ReactionRule` fired, shatter already wired via `BreakableGlassware`.
- [ ] Optional: AudioMixer groups + duck Pharmee voice under SFX.

## 5. UI & flow

- [ ] **Menu scene XR-ization**: activate the placed inactive XR rig, make its camera the canvas `worldCamera`, disable the flat camera, add `XRUIInputModule`, convert the `ExperimentSelect` overlay to world space. (Desktop mouse works today.)
- [x] Settings **apply-listeners** — DONE 2026-07-09: `ComfortApplier` on `Services` (SampleScene) subscribes to `SettingsService.Changed` and applies: HUD/text scale (scales `HudRig` root — frustum-fit divides by scale so everything renders bigger, baseline cached to avoid compounding), snap-turn angle → `SnapTurnProvider.turnAmount`, vignette intensity → `TunnelingVignetteController` aperture (1→0.35 curve), subtitle pacing → `SetSubtitlePace` seams on `PharmeeBrain` + `PharmeeGatekeeper` (dwell = base/speed). Pure curves in `ComfortMath`, 10 assertions. *(Menu scene has no comfort targets; prefs persist and apply on lab load.)*
- [x] Results/History **screen UI** — DONE 2026-07-09: MainMenu gained a **Results** button (cloned Settings styling, slotted above Quit) opening `ResultsPanel`: per-experiment PASS/TRY/-- badges + best grade/mastery/attempts + overall count (`ResultsHistoryController`, verified rendering all 11 rows), **Export CSV** → `persistentDataPath/pharmasynth_results.csv` with the path echoed on screen, Close. *(In-headset "share" beyond the on-disk CSV = descoped analytics; revisit only if the client asks.)*
- [ ] HUD comfort validation on-device: ScreenLocked (current, storyboard) vs LazyFollow (built-in fallback mode) — decide after headset testing.
- [ ] Pharmee **random idle chatter / comments on player actions** (client's "future update" request; mover + jitter done).
- [ ] Cutscene staging polish: prop staging per beat, wine "7 days & 7 nights" time-skip montage + tasting finale (data + copy exist).
- [ ] Dialogue copy pass for all 11 experiments → **client sign-off**.

## 6. On-device & release (needs the Quest 3)

- [ ] **Headset escalation**: if no Quest 3 by W5 (Aug 4–10), flag the client — contractual risk.
- [ ] Day-1 on-device: 90 Hz hold worst-case (72 fallback), comfort pass, wrist-gesture ergonomics, hand-tracking pass, MSAA 4× vs fill-rate, FFR validation, HUD mode decision.
- [ ] Perf hardening: ASTC/atlasing, draw-call audit vs ≤150, tris ≤1.2M, rigidbody budget ≤40 kinematic-on-grab.
- [ ] Android keystore + signing docs.
- [ ] Full revision-checklist UAT + ISO/IEC 25010 acceptance instrument.
- [ ] Final APK + user guide + technical handover + implementation summary.

## 7. Client decisions (see client-signoff-request.md)

- [ ] Chemistry sign-offs: Benzoic Acid benzaldehyde route, Acetanilide acylating agent (acetyl chloride vs safer anhydride), Benzamide nitrite typo, confirmatory-test outcomes.
- [ ] **Scoring-weight sign-off** (hard W2 exit criterion — still open).
- [x] Yield-in-grade decision — RESOLVED: record-only (§1). Breakage policy — RESOLVED: lose points + shelf replacement everywhere incl. exams, never a forced restart (matches implementation). Wine rubric — RESOLVED: standard 6 categories.
- [ ] Dr. Jimenez budget/model — **ON HOLD: the client may supply their own model of him**; keep the primitive stand-in until then. Analytics-descope confirmation still open.

## 8. Process

- [ ] User commits checkpoints regularly (two editor crashes have occurred); Claude commits only when asked; retire the dead `feature/asset-intake` branch.

---

**Reading the board:** engine, data, and the full client-confirmed game loop are done and regression-locked (385 assertions). What remains is a production pass — per-experiment observation wiring (§1), the physics/interaction pass (§2), art/audio (§3–4), small UI completions (§5) — then the externally-gated tail: client sign-offs (§7) and the Quest-3 on-device week (§6). Suggested order: §2 physics pass → §1 test vessels (Tier-1 first) → §5 menu XR + settings listeners → §3/§4 art-audio → device QA.
