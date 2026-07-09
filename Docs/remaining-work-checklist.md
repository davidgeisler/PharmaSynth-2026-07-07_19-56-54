# PharmaSynth — Remaining-Work Checklist (THE single tracker)

Everything still to do before the **2026-08-31** turnover, consolidated **2026-07-09** after the W5.6 client-workflow batch (suite **385/385** green). Check items off as they land. `[ ]` open · **bold** = Tier-1 contract-critical (Tutorial + Prelims + Benzoic Acid + Aspirin). Companion docs: [gaps.md](gaps.md) (analysis), [project-overview.md](project-overview.md) (what exists), [on-device-test-plan.md](on-device-test-plan.md), [client-signoff-request.md](client-signoff-request.md), CLAUDE.md (session handoff).

**DONE and out of scope of this list** (see CLAUDE.md for detail): engine + scoring/BKT + progression persistence, all 11 experiments as data + auto-built stages + sim-rig verbs (17/42 stations), pour path + depletion (finite bottles, restart prompt), door-gated campaign flow (choice panel → episode → lab-coat → armed load → walk-in timer → debrief → teleport return → unlock announce), screen-locked HUD + Pharmee dialogue bar + only-while-speaking bubble, ScreenFader, in-lab Settings/Restart/Quit, MainMenu spawn room, real-size pass (RealSizes, 42 prefabs), labcoat PPE in the locker (click to don), swinging lab door (solid open + closed), head-collision pushback, 44 cutscene SOs wired, 33-MCQ post-lab quiz + yield stepper, CC0 audio wired (beeps/UI/glass/alarm/music), hub/select UI, results CSV backend.

---

## 1. Per-experiment content wiring

- [ ] **Product-seeded test vessels per experiment** — 17 manual-accurate confirmatory-test `ReactionRule`s exist, but layouts pour test reagents into the main vessel; each experiment needs a product-seeded test vessel so colours/ppt/gas observations actually show. *(Data done; scene wiring open.)*
- [ ] Remaining reaction rules: ethanol functional-group tests (P1), acetone Tollen's/Schiff details, caffeine, wine limewater chain.
- [ ] **Methane verb polish**: mortar grind (prepare-mixture), splint flame-test visual, flame/bubble VFX.
- [ ] Wine bespoke rubric (workmanship/appearance/presentation/documentation/flavour) — currently standard 6-category.
- [ ] Per-experiment ILO cards ×11 (intro-cutscene card art).
- [ ] Data-sheet expected-yield ranges per experiment; **client decision:** does yield feed the grade? (Currently only quiz MCQs drive Documentation.)
- [ ] `HazardZone` placement per experiment (hot surfaces, spills); `BreakableGlassware` on glass props (drop → shatter → cleanup task); `WeighingScaleController` wiring where weighing is graded.

## 2. Physics & interaction pass

- [x] **Physics-attributes / resting-pose audit for ALL items** (user request 2026-07-09, task #78) — DONE 2026-07-09: `PhysicsProfiles` table (42 items: mass + resting pose, companion to `RealSizes`), `GrabPhysicsPolicy` (kinematic-on-shelf → dynamic-on-release), builder applies profiles + rest-pose rotation; concave MeshColliders convexified (PhysX rejects them on dynamic bodies — 5 items fell through the world before the fix); degenerate flat-tool colliders padded. Verified: **Tools ▸ PharmaSynth ▸ Physics Audit (Drop Test)** 42/42 settle plausibly, **(Report)** clean (both re-runnable; + **(Fix Scene Items)** applied & scene saved). Suite 385 → 412.
- [ ] XRI **sockets** at stations/racks (props snap into place). *(The **drop respawn** half — kill-Z + idle return-to-home — is DONE: `DropRespawn` on every builder-spawned prop, re-freezes to shelf policy on arrival.)*
- [ ] Teleport anchors at each workstation (only the floor `TeleportationArea` exists).
- [ ] Refine crude convex hulls on tall apparatus (tripod, retort stand, burner).
- [ ] XRI interaction-layer audit (everything on default layers; sockets/hands need masks).
- [ ] Prop readability check after the human grab-test (real-scale items are small — slight scale-up vs highlight shader).

## 3. Art & models

- [ ] **Pharmee animation set** (idle-float/talk/gesture/celebrate/warn) + face-state materials + re-point `PharmeeFace.faceRenderer` at the screen mesh (still `Ears_Black_Matt_0`).
- [ ] **Dr. Jimenez**: source/rig the real scientist model (budget = client decision) + build `ExaminerNPC` (assessment mode: observes, no hints). Primitive stand-in placed.
- [ ] Real **fume hood** model (working `FumeHoodZone` + glass stand-in placed).
- [ ] **Clean reagent-label textures** (16 chemicals + apparatus; never copy garbled storyboard labels).
- [ ] PPE completion: gloves + goggles as clickable items like the coat; coat visibly ON the player (worn visual / mirror reflection); glove material swap on hands.
- [ ] Wrist-watch 3D model (primitive canvas today) + gesture tune on-device.
- [ ] Fermentation set (vessels, airlocks ×3, balloon), wine bottle + glass, tea/caffeine props, separatory funnel (check pack first), WASTE bin.
- [ ] VFX set: smoke, steam, fire, glass shatter, confetti (URP particles, ≤3k live).
- [ ] **Per-experiment demo video clips + `VideoPlayer` TV screen** (0 VideoClips exist; storyboard expectation).
- [ ] UI art skins: grade screen, tablet, HUD pills, station pads (readable primitives today); MainMenu room polish beyond primitives.
- [ ] Proper VR glass shader (stereo-instancing validated, Quest overdraw budget; cheap fallback ready).
- [ ] Lighting: full lightmap re-bake + light probes once layout locks (placeholder realtime today).

## 4. Audio (infrastructure + CC0 base DONE)

- [ ] Gentle lab **ambient loop** (helicopter clip removed; `ambient-lab` key is empty on purpose).
- [ ] Pour / bubble / boil / **burner-ignite** SFX (keys exist, no clips; Kenney has no liquid — try OpenGameArt CC0).
- [ ] Optional: AudioMixer groups + duck Pharmee voice under SFX.

## 5. UI & flow

- [ ] **Menu scene XR-ization**: activate the placed inactive XR rig, make its camera the canvas `worldCamera`, disable the flat camera, add `XRUIInputModule`, convert the `ExperimentSelect` overlay to world space. (Desktop mouse works today.)
- [ ] Settings **apply-listeners** on real targets: UI text scaler, tunneling vignette, snap-turn provider, subtitle pacing (back-end + sliders exist in both scenes).
- [ ] Results/History **screen UI** + "write CSV to persistentDataPath / share" button (`ResultsExport` backend done).
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
- [ ] Yield-in-grade decision (§1); Dr. Jimenez budget; analytics-descope confirmation.

## 8. Process

- [ ] User commits checkpoints regularly (two editor crashes have occurred); Claude commits only when asked; retire the dead `feature/asset-intake` branch.

---

**Reading the board:** engine, data, and the full client-confirmed game loop are done and regression-locked (385 assertions). What remains is a production pass — per-experiment observation wiring (§1), the physics/interaction pass (§2), art/audio (§3–4), small UI completions (§5) — then the externally-gated tail: client sign-offs (§7) and the Quest-3 on-device week (§6). Suggested order: §2 physics pass → §1 test vessels (Tier-1 first) → §5 menu XR + settings listeners → §3/§4 art-audio → device QA.
