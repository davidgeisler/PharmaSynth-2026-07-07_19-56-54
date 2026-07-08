# PharmaSynth — Remaining-Work Checklist

The single actionable tracker for everything still to do before the **2026-08-31** turnover. Check items off as they land. Grounded in a repo/scene inventory run on **2026-07-08** (self-tests 157/157 green). Companion docs: [gaps.md](gaps.md) (analysis, severities, client decisions), [project-overview.md](project-overview.md) (what exists), CLAUDE.md (session handoff).

Legend: `[x]` done & verified · `[ ]` open · **bold** = blocking for Tier-1 contract scope (Tutorial + Prelims + Benzoic Acid + Aspirin).

---

## 1. Per-experiment wiring matrix

Engine + data are done for all 11; everything below is scene/content wiring. "Sim rigs" = TemperatureSim / GasCollection / Crystallization / Filtration hookups via `TaskGraph.RegisterCondition`.

| # | Experiment | v2 data | Props+stations | Reaction rules | Vessel bindings | Sim rigs | Cutscenes ×4 | Quiz ×3 |
|---|-----------|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| T0 | **Methane (Tutorial)** | [x] | [x] carry-to-zone | [ ] | n/a (dry) | [x] burner heats + gas fills | [x] | [x] |
| P1 | **Chemical Compounding** | [x] | [ ] | [ ] | [ ] | [ ] | [x] | [x] |
| P2 | **Ethyl Alcohol** | [x] | [ ] | [ ] | [ ] | [ ] ferment+distil | [x] | [x] |
| M1 | **Benzoic Acid** | [x] | [ ] | [ ] | [ ] | [ ] cryst+filt | [x] | [x] |
| M2 | Acetanilide | [x] | [ ] | [ ] | [ ] | [ ] | [x] | [x] |
| M3 | Acetone | [x] | [ ] | [ ] | [ ] | [ ] distil 56° | [x] | [x] |
| M4 | Chloroform | [x] | [ ] | [ ] | [ ] | [ ] reflux+distil | [x] | [x] |
| F1 | Benzamide | [x] | [ ] | [ ] | [ ] | [ ] ice bath | [x] | [x] |
| F2 | **Aspirin** | [x] | [ ] | [ ] | [ ] | [ ] **overheat branch** | [x] | [x] |
| F3 | Caffeine (Tier 3) | [x] | [ ] | [ ] | [ ] | [ ] extraction | [x] | [x] |
| F4 | Wine Making | [x] | [ ] | [ ] | [ ] | [ ] time-skip | [x] +montage | [x] |

Counts: cutscene SOs **44/44** ✅ · quiz questions **33/33** ✅ (data; presentation UI still TODO) · reaction-rule assets **0** · hands-on scene wiring **1/11** (Methane).

- [x] **Methane heat/collect real verbs** — burner in zone heats (`TemperatureSim`), hot apparatus + tube in zone fills (`GasCollection`); tasks complete via auto-check (`MethaneApparatusRig` + `ZoneItemSensor`, regression-covered).
- [ ] Remaining Methane verb polish: mortar grind interaction (prepare-mixture), splint-flame test visual, flame/bubble VFX.
- [ ] **Wine bespoke rubric** (workmanship/appearance/presentation/documentation/flavour) — currently standard 6-category.
- [x] **Cutscene data — all 44** (`<Experiment>_Intro/ReagentPrep/Success/Failure` for all 11) authored with per-experiment chemistry + safety dialogue. Still TODO: wire each experiment's 4 SOs into its scene `CutsceneDirector`; staging/fades; wine time-skip montage.
- [ ] Per-experiment ILO cards ×11 (visual intro-cutscene card art).

## 2. Assets that do not exist yet (create / source / buy)

### Art & models
- [ ] **Fume hood model** — a glass **stand-in + working `FumeHoodZone` volume is now placed** on the back counter (`FumeHood_StandIn`); a real hood model (sash, extractor) is still an art-pass item. Required for Acetanilide/Benzamide/Caffeine/Chloroform safety rules.
- [ ] **Procedure demo videos** — **0 VideoClip assets exist in the project** (user expectation: TV-screen demos of what to perform, per storyboard). To create: short per-experiment demo clips + a `VideoPlayer` TV screen in the lab. NOTED for asset production.
- [ ] **Dr. Jimenez** rigged scientist (source Asset Store; fallback = posed static examiner at observation desk). Budget = client decision.
- [ ] Pharmee **animation set** (enter / idle-float / talk / gesture / celebrate / warn) + **face-state materials**.
- [ ] **Clean reagent-label textures** for all 16 chemicals + apparatus labels (never copy storyboard labels — garbled AI text).
- [ ] Wrist-watch **3D model** (currently a primitive canvas).
- [ ] Fermentation set: vessels + airlocks ×3 + balloon (Ethyl Alcohol, Wine).
- [ ] Wine bottle + wine glass (tasting finale).
- [ ] Tea/caffeine props (tea leaves, kettle) — check pack first.
- [ ] Separatory funnel (Caffeine/Chloroform) — check pack first; author if absent.
- [ ] WASTE bin + cleanup props (sanitation mechanic).
- [ ] Mirror/avatar reflection for the PPE ante-room (or the agreed illusion: coat prop + glove swap).
- [ ] VFX set: smoke, steam, fire, glass shatter, confetti (URP particles, ≤3k live).
- [ ] Grade-screen art + polished world-space canvas skins (HUD/tablet/watch/grade are primitive panels).
- [ ] Period-hub room dressing (3 doors).
- [ ] Aisle/station **pad art** (current pads are colored primitive cubes — readable but placeholder).
- Note: **`Assets/PharmaSynth/Prefabs/` and `Timeline/` folders are empty** — game-ready prefab variants (e.g., pre-wired station kits) and Timeline assets are all still to be made.

### Audio (folder is empty — 0 files)
- [ ] SFX set: pour, bubble, boil, glass shatter, alarm, success/fail stingers, UI clicks.
- [ ] **Pharmee robotic beep "voice" set** (it IS the character's voice).
- [ ] Ambient lab loop + menu music.
- [ ] `AudioService` + mixer groups (master/SFX/voice/ambient) and wiring to the existing AudioSource hooks (`NPCNarrationController`, `BreakableGlassware`) — only 2 AudioSources exist in the scene today (defaults).

### Data
- [x] **Quiz bank: 33 MCQs** (3 per experiment) authored as 11 `QuizBank` assets (`ScriptableObjects/Quizzes/`) + new `QuizBank`/`QuizQuestion` type with `Score()`. Regression-covered. **Still TODO: the tablet quiz UI to present them + feed `QuizBank.Score` into the grader's quiz fraction.**
- [ ] **ReactionRule assets + a ReactionRegistry asset per experiment** (0 exist) — powers LiquidPhysics reactions & wrong-mix detection.
- [ ] Data-sheet definitions (expected yield ranges per experiment).

## 3. Built & tested code that is NOT yet placed in the scene

These components pass self-tests but appear **zero times** in SampleScene — the mechanic can't fire in play until placed:

- [x] **`FumeHoodZone`** — placed (stand-in hood, back counter). Still to wire into `LiquidTaskBinding` checks per experiment.
- [ ] **`HazardZone`** — place on hot surfaces / spill areas / acid zones **per experiment** (deliberately deferred: a generic zone at a station would punish correct actions).
- [ ] **`LiquidTaskBinding`** — attach per experiment vessel with expected reagent→task map (the pour path).
- [x] **Balance placed** on the right island (kinematic, grabbable). `WeighingScaleController` wiring per experiment still open.
- [ ] **`BreakableGlassware`** (inherited) — enable on glass props: drop → shatter → cleanup task + DroppedGlassware mistake.
- [ ] `CrystallizationController` / `FiltrationController` / `TemperatureSim` / `GasCollection` rigs as scene stations (ice bath, Büchner setup, burner, trough).
- [ ] `PPEController` full flow (see §6 — current scene has only the poke-sign).

## 4. Attribute passes on existing assets

### Done (verified this week)
- [x] **118/118 Environment meshes solid** (walls, doors, tables, cabinets, windows, counters — static MeshColliders).
- [x] **42/42 equipment prefabs grab-ready** (convex/box collider + Rigidbody + XRGrabInteractable; liquid meshes skipped; skinned tools = box-from-bounds).
- [x] URP port of the 5 broken ChemLab glass/liquid materials; `_WithLiquid` glass/liquid roles fixed; `LiquidPhysics.mainRenderer` → liquid mesh.
- [x] 16 reagent vessels filled, tinted, seated in the `3x4` cabinet grid.
- [x] Real Methane props + billboarded labels (`FaceCamera` on all world text/panels).
- [x] Rig CharacterController radius 0.1 → 0.25 (thumbstick locomotion collides; simulator WASD moves the HMD and legitimately doesn't).
- [x] Station zone pads grounded on the island; PPE sign clear of meshes; HUD bar Filled-type; Pharmee bubble raised.
- [x] **Waypoint beacon**: floor circular glow + bobbing/spinning down-arrow (`WaypointBeacon`) replaces the yellow blob.
- [x] **Pharmee alive + interactable**: `FloatBob` hover animation; poke the robot (`PharmeePoke` + collider + XRSimpleInteractable) to repeat the current step hint.

### Still to do
- [ ] **XRI sockets** (`XRSocketInteractor`) at stations/racks so props snap into place — 0 in scene.
- [ ] **Drop respawn** (kill-Z + idle return-to-home) — released props currently hover (XRI restores their kinematic state on release).
- [ ] **Teleport anchors** at each workstation (0 `TeleportationAnchor` in scene; only the floor `TeleportationArea`).
- [ ] Refine crude convex hulls on tall apparatus (tripod, retort stand, burner).
- [ ] **Proper VR glass shader** (current = flat URP Lit transparent; needs stereo-instancing validation + Quest overdraw budget; cheaper fallback ready).
- [ ] Liquid fill-line child-offset compensation (fill height is approximate on `_WithLiquid` vessels).
- [ ] XRI **interaction-layer audit** (everything currently on default layers; sockets/hands will need masks).
- [ ] **Re-point `PharmeeFace.faceRenderer`** at the robot's screen mesh (still `Ears_Black_Matt_0`) + face materials.
- [ ] Lighting: full lightmap re-bake + light probes once layout locks (current = placeholder realtime).
- [ ] Prop readability: real-scale apparatus is small — evaluate slight scale-up or outline/highlight shader after the grab-test.

## 5. Scenes, menus & UI flow
- [ ] **Menu scene XR-ization**: XR Origin + `TrackedDeviceGraphicRaycaster` + ray interactors (menu is desktop-click only today).
- [ ] Settings panel content: audio sliders, text size, subtitle speed, vignette intensity, snap-turn angle, seated/standing, handedness (`SettingsService` to build).
- [ ] **Period hub** (3 mastery-gated doors; `ProgressionFlow.IsPeriodUnlocked` ready) + experiment-select UI (`ExperimentCatalog`/`ExperimentLibrary` ready).
- [ ] **Post-lab quiz UI** (3 MCQs on tablet) + **data-sheet yield-entry UI** (number pad) — DataSheet-phase tasks currently complete like generic tasks.
- [ ] Results/History screen + exportable scores file (the agreed analytics descope).
- [ ] PPE ante-room flow: locker interaction → coat/goggles/gloves visibly donned → door gate (current = one poke sign).

## 6. NPC & narrative
- [ ] Pharmee skeletal animations wired to `PharmeeBrain` states.
- [ ] `ExaminerNPC` component (assessment mode: observes, no hints) + Dr. Jimenez staging.
- [ ] Dialogue copy pass for all 11 experiments → **client sign-off** (front-load; W3-item now late).
- [ ] Cutscene staging polish: fades, prop staging, per-experiment `CutsceneData` (40 SOs, §1), wine "7 days & 7 nights" time-skip montage + tasting finale.

## 7. On-device & release (needs the Quest 3)
- [ ] **Headset escalation**: if no Quest 3 delivered by W5 (Aug 4–10), flag the client — contractual risk.
- [ ] Day-1 on-device: 90 Hz hold worst-case (72 fallback), comfort pass, wrist-gesture ergonomics, hand-tracking input pass, MSAA 4× vs fill-rate decision, FFR validation.
- [ ] Perf hardening (proactive from W4–6): ASTC/atlasing, draw-call audit vs ≤150, tris ≤1.2M, rigidbody budget ≤40 kinematic-on-grab.
- [ ] Android keystore (not yet created) + signing docs.
- [ ] Full revision-checklist UAT + **ISO/IEC 25010 acceptance instrument**.
- [ ] Final APK + user guide + technical handover + implementation summary.

## 8. Client decisions & process hygiene
- [ ] **Chemistry sign-offs**: Benzoic Acid = benzaldehyde route (manual defect), Acetanilide acylating agent (acetyl chloride vs safer acetic anhydride), Benzamide nitrite typo.
- [ ] **Scoring-weight sign-off** (hard W2 exit criterion — still open).
- [ ] Dr. Jimenez budget confirmation; analytics-descope confirmation.
- [ ] Commit the untracked `FaceCamera.cs` (+meta) and current doc set; retire the dead `feature/asset-intake` branch.
- [ ] Keep committing checkpoints (two editor crashes have occurred; user commits, Claude commits only when asked).

---

**Reading the board:** the engine (100%) and experiment data (100%) are done; the remaining work is ~an art/audio/content-wiring production pass (§§1–6) plus the on-device tail (§7). Tier-1 bolded items are the contract-critical path. Suggested order stays as gaps.md §6: Methane real-verbs + Ethyl Alcohol wiring → menu XR + quiz/data-sheet UI → per-experiment wiring in tier order → audio + art passes → device QA.
