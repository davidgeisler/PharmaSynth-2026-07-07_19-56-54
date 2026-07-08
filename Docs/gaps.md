# PharmaSynth — Gap Analysis (current build vs plan, contract, and shippable quality)

**Date:** 2026-07-08 · **Audience:** dev team + client · **Build state audited:** branch `main` @ `f1bfdb8` (all W1–W5 work committed; only the new `FaceCamera.cs` untracked), regression suite **157/157 green** (menu `Tools ▸ PharmaSynth ▸ Run Self-Tests`), Unity console zero-error.
**Companions:** `CLAUDE.md` (session handoff), the approved master plan (`C:\Users\MSI\.claude\plans\you-are-the-best-cozy-possum.md`), `Docs/audit-report.md` (inherited-code audit).

A calendar note up front: the plan's week labels are milestones, not dates. W1–W4 scope is **complete on 2026-07-08** — roughly four calendar weeks ahead of the plan's own schedule (W4 was slated for Jul 28–Aug 3) — and most of W6's content-authoring workstream was pulled forward and finished. Every "fix window" below uses the plan's W5–W8 labels (W5 = Tier-1 completion, W6 = Tier 2, W7 = Tier 3 + integration polish, W8 = QA & turnover), executed early where capacity allows.

---

## 1. Executive summary

**DONE and verified.** The entire game *engine* is feature-complete and headless-verified: task-graph experiment engine with phases/prerequisites/auto-check (`TaskGraph`), BKT mastery + rubric scoring + grading (`MasteryModel`, `ScoreCalculator`, `ExperimentGrader`), 9-type error/safety matrix (`MistakeLog` + hazard/fume-hood zones), orchestration with the two-part pass gate (`ExperimentRunner`), versioned-JSON save + unlock chain (`ProgressionService`, `ExperimentCatalog`, `ProgressionFlow`, `ExperimentLibrary`, `GameFlow`, `ExperimentLauncher`), chemistry sims (liquid, temperature, gas collection, crystallization, filtration), NPC guide brain + data-driven cutscene director, and the full in-lab UI set (HUD, tablet checklist, wrist watch, grade screen). All **11 experiments exist as v2 data** (task graphs, phases, skills, rubric weights, thresholds) and the self-tests load and solve every one to 100% respecting prerequisites. A **playable, hands-on Methane tutorial** is assembled in `Assets/Scenes/SampleScene.unity` (grab a prop, carry it to its matching station), a desktop-clickable `MainMenu.unity` exists, all 118 furniture meshes are solid and all 42 lab-equipment prefabs are grabbable. 64 C# scripts under `Assets/PharmaSynth/Scripts/`, 157 regression assertions, zero console errors.

**What remains.** Everything between "verified engine + one hands-on tutorial" and "shippable Quest 3 game": per-experiment *wiring* for the other 10 experiments (item-matched prop stations, ReactionRegistry rules — **zero authored so far** —, per-vessel `LiquidTaskBinding`, TemperatureSim/GasCollection/Crystallization/Filtration rig hookups, 4 CutsceneData SOs each), the quiz bank (0 of 33 MCQs) and the data-sheet yield-entry UI, the menu/hub flow in VR (MainMenu has no XR rig, no settings panel, no period hub, no experiment-select UI), the entire audio landscape (zero clips), the art pass (Pharmee animations + face fix, real UI canvases, clean reagent labels, proper URP glass shader, lighting re-bake), PPE flow beyond a poke-cube stand-in, Dr. Jimenez, and **all on-device work — the Quest 3 headset has not been delivered**. None of this threatens the engine; all of it is content, art, and device time, and the plan's slip policy (Caffeine → bespoke fail cutscenes → wine finale extras) still holds.

---

## 2. System status matrix

Status: **DONE** = built + verified · **PARTIAL** = works, known gaps · **TODO** = not built.

| System | Status | Evidence | What remains |
|---|---|---|---|
| Task-graph engine (phases, prereqs, weighted progress, auto-check) | **DONE** | `Scripts/Experiment/TaskGraph.cs` + `TaskGraphModel.cs`; self-test suites (prereq blocking, weighted progress, phase events, `Tick()` auto-check) | Nothing engine-side; per-experiment condition wiring (§3) |
| Scoring: BKT mastery + rubric + grader + two-part gate | **DONE** | `Scripts/Scoring/{MasteryModel,ScoreCalculator,ExperimentGrader}.cs`; `ExperimentRunner.ExperimentResult` gate = grade ≥ threshold AND mastery ≥ 0.90; hand-checked assertions (e.g. 81.08% realistic-run case) | Client sign-off on rubric weights (§5); Wine bespoke rubric (§4 #16) |
| Progression + save | **DONE** | `Scripts/Progression/ProgressionService.cs` — versioned JSON @ persistentDataPath + `.bak` recovery; 17-assertion suite | Autosave-on-pause hook + resume-offers-restart-step polish (plan §6), W7 |
| Catalog / flow / library / launcher backbone | **DONE** | `ExperimentCatalog` (11 entries, 1/2/4/4 period split, linear prereq chain), `ProgressionFlow` (next/unlocked/period-door/overall-%), `ExperimentLibrary.asset` (refs all 11 modules), `GameFlow.SelectedModuleId`, `ExperimentLauncher` (swap runner to any module by id); progression-flow self-test suite | UI surfaces that *drive* it: period hub + experiment-select (TODO, §4 #6) |
| Chemistry — liquid | **PARTIAL** | `LiquidPhysics` (event-driven, de-hardcoded) + `PharmaLiquid.shader` (stereo-instancing-safe) + `LiquidTaskBinding` (context-aware reagent→task, fume-hood check); applied to the 5 `_WithLiquid` prefabs | Fill-line approximation on `_WithLiquid` prefabs (§4 #2); per-vessel bindings for 10 experiments (§3) |
| Chemistry — temperature | **PARTIAL** | `TemperatureSim` — exact exponential heat model, target/overheat events (`Overheated`, `IsOverheated`), `AtLeast()` predicate; unit-tested | Not yet wired into any scene rig — incl. the Aspirin overheat branch (§4 #10) |
| Chemistry — gas / crystallization / filtration | **PARTIAL** | `GasCollection`, `CrystallizationController`, `FiltrationController` — all tested with done-events + auto-check predicates | Zero scene rigs; hook per experiment (§3) |
| Error / safety matrix | **DONE** | `MistakeLog` (9 `LabErrorType`s → rubric categories), `HazardZone`, `FumeHoodZone`, wrong-step auto-detect in runner, `LiquidTaskBinding` fume-hood violation; suites green | Broken-glassware cleanup task hook + fire-zone rewrite remain from audit (W6–W7) |
| NPC / cutscenes | **PARTIAL** | `PharmeeBrain` state machine wired to runner; `NPCNarrationController.Say()`; `CutsceneData` + `CutsceneDirector` (end cutscene ALWAYS); 4 Methane cutscene SOs | Pharmee has no skeletal animations; `PharmeeFace.faceRenderer` points at the wrong mesh (§4 #12); 40 cutscene SOs unauthored (§4 #7); Dr. Jimenez absent (§4 #13) |
| UI — HUD / tablet / watch / grade | **PARTIAL** | `ExperimentHudController`, `TabletChecklistController`, `WristWatchController` (supination+gaze+button fallback), `GradeScreenController` — all built, wired in-scene, formatter/builder logic self-tested | All are primitive canvases → art pass; watch gesture needs on-device tuning (§4 #17); quiz + data-sheet UI missing (§4 #15) |
| UI — main menu | **PARTIAL** | `MainMenu.unity` + `MainMenuController` (Tutorial/Laboratory/Settings/Quit; `ResolveLabTarget` via `ProgressionFlow`) — works with desktop camera + click | No XR Origin / `TrackedDeviceGraphicRaycaster`; Settings button toggles a panel that does not exist; no period-hub scene; no experiment-select UI (§4 #6) |
| Interactions — grab / stations | **PARTIAL** | `LabItem` + `ExperimentTaskStation.requiredItemId`/`AcceptsItem()`; Methane = 5 item-matched trigger stations + 5 grabbable props; 42/42 equipment prefabs grab-enabled; 118/118 furniture meshes collidered | Released props hover mid-air (§4 #3); no sockets/respawn; crude convex hulls on tall apparatus (§4 #4); 10 experiments unwired (§3) |
| Scenes | **PARTIAL** | `Assets/Scenes/SampleScene.unity` (assembled lab, playable Methane) + `MainMenu.unity` — the only two game scenes (the ChemLabEquipment pack ships two demo scenes besides) | Period hub / PPE ante-room (sub-areas or additive scenes per plan §4.3) |
| Content — 11 modules | **PARTIAL** | All 11 `.asset` files in `ScriptableObjects/Experiments/` with graphTasks/phases/skills/rubric; 16 reagent SOs in `/Chemicals`; self-tests solve every module | Per-experiment wiring is the bulk of remaining scope — see §3. **Zero `ReactionRule` assets exist**; quiz bank 0/33 |
| Art | **TODO** (in scope to create, per 2026-07-08 directive) | Imported packs URP-converted; PharmaLiquid shader done; primitive stand-ins everywhere | Proper URP glass shader (§4 #1), reagent labels (§4 #5), Pharmee anims/face, real UI canvases, PPE flow art, lighting re-bake (§4 #19), VFX set |
| Audio | **TODO** | Only dormant `AudioSource` hooks (`NPCNarrationController`, `BreakableGlassware`, `AcidCorrosion`); zero clips, no `AudioService`, no mixer | Everything (§4 #11) |
| On-device (Quest 3) | **TODO — blocked on hardware** | Android target configured (OpenXR sole loader, IL2CPP/ARM64/ASTC, Meta Quest features + FFR enabled); headset **not delivered** | 90 Hz perf pass, comfort, hand tracking, wrist ergonomics, ASTC/atlas/draw-call hardening, UAT, final APK (§4 #18) |

---

## 3. Per-experiment wiring matrix

✅ = exists · ✖ = missing. Task counts from the authored v2 data.

| # | Experiment (`ScriptableObjects/Experiments/`) | Tasks | v2 data | Reaction rules | Prop-stations | Vessel bindings | Cutscene SOs (4 ea) | Quiz bank (3 MCQ) |
|---|---|---|---|---|---|---|---|---|
| T0 | `Tutorial_Methane` | 5 | ✅ | ✖ | ✅ (5 item-matched stations + 5 props) | ✖ | ✅ (4 in `/Cutscenes`) | ✖ |
| P1 | `Prelim_ChemicalCompounding` | 6 | ✅ | ✖ | ✖ | ✖ | ✖ | ✖ |
| P2 | `Prelim_EthylAlcohol` | 7 | ✅ | ✖ | ✖ | ✖ | ✖ | ✖ |
| M1 | `Midterm_BenzoicAcid` | 9 | ✅ | ✖ | ✖ | ✖ | ✖ | ✖ |
| M2 | `Midterm_Acetanilide` | 10 | ✅ | ✖ | ✖ | ✖ | ✖ | ✖ |
| M3 | `Midterm_Acetone` | 10 | ✅ | ✖ | ✖ | ✖ | ✖ | ✖ |
| M4 | `Midterm_Chloroform` | 10 | ✅ | ✖ | ✖ | ✖ | ✖ | ✖ |
| F1 | `Final_Benzamide` | 9 | ✅ | ✖ | ✖ | ✖ | ✖ | ✖ |
| F2 | `Final_Aspirin` | 7 | ✅ (overheat branch in data) | ✖ | ✖ | ✖ | ✖ | ✖ |
| F3 | `Final_Caffeine` | 9 | ✅ | ✖ | ✖ | ✖ | ✖ | ✖ |
| F4 | `Final_WineMaking` | 8 | ✅ (standard rubric; bespoke rubric TODO) | ✖ | ✖ | ✖ | ✖ | ✖ |

Reading the matrix honestly: **ALL 11 have verified v2 data; only Methane has hands-on stations and cutscenes; NONE have reaction rules or quiz banks.** "Wiring" per experiment = item-matched stations + grabbable props (the Methane pattern), `ReactionRegistry` rules, per-vessel `LiquidTaskBinding`, and TemperatureSim / GasCollection / Crystallization / Filtration rig hookups where the procedure needs them, plus 4 `CutsceneData` SOs and 3 MCQs. Methane took well under a day once the pattern existed — the pattern is proven and repeatable, but it is 10× remaining.

---

## 4. Known defects & risks

Severity: **Blocker** (cannot ship), **High** (visible quality/contract risk), **Medium** (polish/robustness), **Low** (cosmetic/deferred-safe).

| # | Item | Severity | Detail | Fix window |
|---|---|---|---|---|
| 1 | **ChemLab glass shader never URP-ported** | High | 5 materials (`GlassMat`, `GlassInnerMat`, `AmberGlassInnerMat`, `TransparentLiquidMat`, `RedLiquidMat` in `Art/Equipment/ChemLabEquipment/Materials/`) were crudely remapped to URP Lit transparent on 2026-07-08 so glassware renders, but a proper glass shader validated for **Single Pass Instanced stereo on Quest** is still TODO — the plan flagged this as a W2 risk item with a cheap fallback ready. | W5–W6 (validate on device W8) |
| 2 | **`_WithLiquid` fill line approximate** | Medium | `LiquidPhysics.SendMeshBounds()` now takes bounds from the liquid child mesh, but the child's local offset is not fully compensated → fill line can sit slightly off in some vessels. | W6 (art pass) |
| 3 | **Released grabbables hover mid-air** | High | Props/vessels rest kinematic; XRI restores kinematic on release, so a dropped prop floats where released. Sockets + respawn (kill-Z + idle return, per plan §6) are TODO. Very visible in playtests. | W5 |
| 4 | **Crude convex hulls on tall apparatus** | Low | Tripod, retort stand, burner get blobby convex colliders — fine for grabbing, wrong for precise placement. | W6–W7 (art pass) |
| 5 | **Reagent bottles colour-coded only — no labels** | High | No label textures yet. The storyboard's labels are garbled AI text and **must never ship**; clean labels must be re-authored (batch task, plan §4.4). Chemistry-education product with unlabeled reagents is a contract-quality issue. | W6 |
| 6 | **Menu flow not VR-ready** | Blocker (for ship; not for current dev) | `MainMenu.unity` works desktop-only: needs an XR Origin + `TrackedDeviceGraphicRaycaster` for in-headset use; `MainMenuController.OnSettings()` toggles a `settingsPanel` that does not exist yet; **no period-hub scene; no experiment-select UI**. The logic backbone (`ExperimentCatalog`, `ProgressionFlow`, `ExperimentLibrary`, `GameFlow`, `ExperimentLauncher`) is built and tested — this is pure UI/scene work. | W7 (plan: "hub/menu/settings final") |
| 7 | **Cutscene data: 1 of 11 experiments** | High | Only Methane has its 4 `CutsceneData` SOs; the other 10 need 4 each (40 SOs). Director + templates are done; this is copy + staging. Client copy sign-off feeds this (§5). | W6–W7 |
| 8 | **Hands-on wiring: 1 of 11 experiments** | Blocker (contract core) | See §3. Tier-1 rows first (P1, P2, M1, F2 to match Methane), then Tier 2, then Caffeine. | W5 (Tier 1) → W6 (Tier 2) → W7 (F3) |
| 9 | **Zero `ReactionRegistry` rules** | High | No `ReactionRule` assets exist anywhere in `ScriptableObjects/` — wrong-reagent detection and reaction outcomes currently rely on `LiquidTaskBinding` context only. Rules are per-experiment data. | W5–W6 (with each experiment's wiring) |
| 10 | **Aspirin overheat branch not scene-wired** | High | `TemperatureSim.Overheated` exists and is tested, but no scene rig connects a burner to Final_Aspirin's overheat task branch. This is a named contract-visible feature (storyboard pp.188–193). | W5 |
| 11 | **Zero audio** | High | No SFX, no Pharmee beep voice (it *is* the character's voice), no ambient loop, no menu music, no `AudioService`/mixer. Hooks already exist in `NPCNarrationController` and `BreakableGlassware`. Plan wanted a placeholder pass W3–4 — this has slipped. | W5–W6 placeholder pass; W7 final |
| 12 | **Pharmee: no animations, wrong face mesh** | High | No skeletal animation set (idle-float/talk/gesture/celebrate/warn — a hard cutscene dependency per plan §3.4); `PharmeeFace.faceRenderer` still points at `Ears_Black_Matt_0` instead of the screen-face mesh; face-state materials TODO. | W5–W6 |
| 13 | **Dr. Jimenez not sourced or built** | Medium | No rigged examiner model; `ExaminerNPC` component TODO. Plan fallback stands: posed static examiner + subtitle-only presence. Needs client budget word (§5). | W6 (source) / W7 (fallback) |
| 14 | **PPE flow is a poke-cube stand-in** | Medium | No locker-room ante flow, no mirror/avatar reflection, no glove material swap. Grading hook (`DonPPE` gate) works. | W6 |
| 15 | **Quiz + data-sheet UI missing** | High | Post-lab 3-MCQ quiz UI and data-sheet yield-entry (number pad) not built; DataSheet-phase tasks currently complete like any other task. Quiz bank content = 0 of 33 questions. Both are graded contract features (Documentation criterion). | W6 (UI) + W7 (bank final) |
| 16 | **Wine Making bespoke rubric TODO** | Medium | F4 uses the standard 6-category rubric; the workmanship/appearance/presentation/documentation/flavour translation (plan §3.6) is a follow-up. Data-driven, cheap. | W6 (with F4 wiring) |
| 17 | **Wrist-watch ergonomics untuned; primitive canvas** | Medium | Supination + gaze gesture works in-engine with button fallback, but thresholds need on-device tuning (plan: day-1 on-device item); watch UI is a primitive canvas needing a real model. | W6 (art) + W8 (device tune) |
| 18 | **Quest 3 headset not delivered** | **Blocker** (external) | ALL on-device work pending: 90 Hz perf pass, comfort validation, hand-tracking input pass, ASTC/atlas/draw-call hardening against the §4.5 budget, wrist ergonomics, UAT, final APK. Plan's escalation deadline is **W5 (Aug 4–10): if still no device, flag the client now.** Mitigation: simulator-first + proactive perf hardening continues. | Escalate at W5 gate; device work W8 |
| 19 | **Lighting is placeholder** | Medium | Full lightmap re-bake + light probes pending (pack shipped no scene, so no reusable lightmaps); plan wants lighting locked before mass content positions freeze. General art pass (environment polish, real UI canvases) pending. | W5–W6 (lock) |
| 20 | **MCP screenshot/capture tooling failing** | Medium (process risk) | Unity MCP capture tools are failing on this machine and MCP-initiated play mode self-exits — visual verification currently depends on **human play-tests**. Keep the human-in-the-loop cadence; structural checks stay headless via self-tests. | Ongoing |
| 21 | **Work-tree strays** | Low (process risk) | All W1–W5 work is committed on `main` (latest `f1bfdb8` "fixed assets"). Two strays remain: the new `Scripts/UI/FaceCamera.cs` (+ .meta) is untracked, and the designated work branch `feature/asset-intake` is stale at the planning commit (`0b21f58`) — reconcile or retire it. Keep checkpointing often (two editor crashes have already occurred during package operations). | Immediate (on user go-ahead) |
| 22 | **Fill-rate risk: transparent glassware overdraw** | Medium | Known Quest killer per plan §4.5 (MSAA 4× must be validated against fill rate). Compounded by #1. Cannot be retired until device arrives. | W8 (device) |

---

## 5. Client decision items

Open items needing client word — none block current work, all block *ship*:

1. **Benzoic Acid chemistry (flag, decision recommended):** manual's Exp 4 text is a copy-paste defect. We implement **benzaldehyde + 0.1% KMnO4 oxidation** per Appendix C's own reagent list (benzaldehyde / 0.1% KMnO4 / 6N HCl / propyl alcohol), superseding the plan's tentative toluene route. Confirm.
2. **Acetanilide acylating agent (needs decision):** manual says **acetyl chloride**; storyboard uses **acetic anhydride** (the safer, more standard teaching route). Current data follows the manual; we recommend the client pick before M2's wiring is locked (W6).
3. **Benzamide nitrous-acid test (flag):** implemented with sodium **nitrite** — the manual's "nitrate" is a typo. Confirm.
4. **Scoring-weight sign-off (plan W2 hard exit criterion — still open):** the manual's printed weights are inconsistent; our normalized per-experiment rubric SOs need client sign-off. Cheap to change (data-driven), but sign-off is contractually gating.
5. **Analytics dashboard descope (flagged at W1 gate):** manuscript's dashboard is descoped to a **local Results/History screen + exportable scores file**. Confirm acceptance.
6. **Dr. Jimenez budget:** confirm Asset Store budget for a rigged scientist, or accept the documented fallback (posed static examiner, subtitle-only).
7. **Headset delivery escalation:** per plan, **W5 (Aug 4–10) is the escalation deadline** — if no Quest 3 by then, the client must be formally notified that on-device validation (90 Hz, comfort, hand tracking, UAT) compresses into W8 at their risk.
8. **Wine rubric translation:** approve the single-player translation of the bespoke workmanship/appearance/presentation/documentation/flavour rubric (plan §3.3/§3.6) before F4 grading is finalized.
9. **Copy/dialogue sign-off:** cutscene + Pharmee dialogue copy for all 11 experiments should go out for review before the W6–W7 cutscene-data pass (plan front-loads this to break the circular dependency).

---

## 6. Suggested order of attack, W5 → W8

Mapped to the plan's roadmap (§5) and its slip policy (drop order: **Caffeine → bespoke fail-cutscene staging → wine finale extras**; Tier 1 never at risk). We are ahead of the plan calendar; the ordering below spends that lead on the highest-risk unknowns first.

**W5 — Tier-1 completion + interaction hardening (plan: "P1, M1, F2 complete; lighting locked; headset escalation").**
1. Fix the grabbable release defect (#3): sockets + kill-Z/idle respawn — do this before wiring more experiments so every subsequent station inherits it.
2. Wire P1, P2, M1, F2 hands-on using the Methane pattern (stations + props + vessel bindings + ReactionRegistry rules), including the **Aspirin overheat scene rig** (#10) and Benzoic-acid crystallization/filtration hookups. This is also the plan's velocity re-baseline: measure per-experiment wiring cost here before committing the W6 volume.
3. Human XR Device Simulator grab-test of Methane's trigger-zone ergonomics (zone sizes/heights) — feedback feeds every later station.
4. Proper URP glass shader port (#1) + lock lighting re-bake (#19) before Tier-2 content freezes positions.
5. Placeholder audio pass begins (#11): Pharmee beep set first (it is the character's voice), pour/UI SFX, `AudioService` + mixer.
6. **Client packet:** headset escalation (#18/§5.7), scoring weights (§5.4), acetanilide decision (§5.2), dialogue copy out for review (§5.9). Commit the untracked `FaceCamera.cs` and reconcile the stale `feature/asset-intake` branch (#21).

**W6 — Tier 2 + game-shell UI (plan: "M2, M3, M4, F1, F4 + perf hardening").**
1. Wire M2, M3, M4, F1, F4 (F4 incl. time-skip staging + bespoke rubric #16). Fume-hood enforcement is contract-visible on M2/M4 — exercise it.
2. Quiz UI + data-sheet yield-entry UI (#15); author quiz bank alongside each experiment's wiring.
3. Menu flow VR-ready (#6, first half): XR Origin + `TrackedDeviceGraphicRaycaster` in MainMenu, real settings panel, period-hub + experiment-select driving the existing `ProgressionFlow`/`ExperimentLauncher` backbone.
4. Pharmee animation set + face-mesh fix (#12) — hard dependency for the W6–W7 cutscene pass. PPE ante-room flow (#14). Reagent label re-author batch (#5). Dr. Jimenez source-or-fallback decision executed (#13).
5. Proactive perf hardening per plan §4.5 (atlasing, draw-call audit, rigidbody budget ≤40) — do not defer to device arrival.

**W7 — Tier 3 + integration polish (plan: "F3 if on track; cutscene data all; final audio; hub/menu/settings/icons/labels final; quiz bank final").**
1. F3 Caffeine wiring — **first slip candidate** if W6 re-baseline says we're tight.
2. Cutscene-data pass for all experiments (40 SOs, #7) using signed-off copy; bespoke fail-cutscene staging is slip candidate #2 (template fail variant always plays regardless — the "end cutscene ALWAYS" guarantee is engine-level and already verified).
3. Final audio pass; art/UI polish (real canvases, watch model #17); wine finale extras (slip candidate #3); Results/History screen + score export (§5.5).
4. Robustness sweep from plan §6: autosave checkpoints, HMD-doff pause, resume behavior, skip debounce, glassware cleanup task.
5. Full simulator regression: every experiment happy-path + every error-matrix branch.

**W8 — QA & turnover (plan: "on-device QA, 90 Hz, ISO/IEC 25010 + UAT, final APK, handover docs").**
1. Day-1 on-device: wrist-gesture ergonomics (#17), comfort, hand tracking, glass-shader stereo + overdraw validation (#1/#22).
2. 90 Hz hold worst-case (all burners + particles + NPCs; 72 Hz fallback documented); ASTC/atlas/draw-call final hardening.
3. Revision-checklist UAT + ISO/IEC 25010 acceptance instrument; final APK (keystore documented); user guide + technical handover + implementation summary.

**Standing risks to watch:** headset delivery (the only true external blocker), client review latency (W7 is the designated absorber), transparent-glassware fill rate (#22), and work-tree strays (#21 — the untracked `FaceCamera.cs` and the stale `feature/asset-intake` branch; keep checkpointing often).
