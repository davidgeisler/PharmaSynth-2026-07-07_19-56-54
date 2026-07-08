# PharmaSynth — Project Overview

> **"PharmaSynth: Gear Up, Synth It Up!"** — a VR chemistry-lab education game for Meta Quest 3.
> Unity 6000.5.2f1 · URP 17.5 · OpenXR 1.17.1 + XRI 3.5.1 · Hard contract deadline: **August 31, 2026**.

This document is the standalone orientation for a new engineer or the client. It reflects the repo state as of **2026-07-08** (end of Week 4 / start of Week 5 of an 8-week schedule). For the live day-by-day state, read `CLAUDE.md` at the repo root; for the approved master plan, see the reference at the bottom.

---

## 1. What PharmaSynth is

PharmaSynth is a **first-person guided lab simulator** that teaches pharmaceutical and medicinal organic chemistry. The player is a pharmacy student in a virtual laboratory who preps reagents, runs real synthesis procedures, performs confirmatory chemical tests, and is graded against the actual course rubric — with a robot guide (Pharmee) coaching and a human examiner (Dr. Jimenez) assessing.

**Design pillars** (master plan §3.1):

1. Authentic, guided chemistry — procedures come from the course lab manual, not invented.
2. Mistakes are safe, visible learning moments — every error is caught, explained, and graded, never a dead end.
3. Readable, comfortable VR UX — subtitle-first, no forced camera motion, teleport + vignetted smooth locomotion.
4. Mastery-based progression — a two-part 90% gate (rubric grade AND Bayesian mastery) before advancing.

**Client context.** This is a client handoff from a discontinued capstone project (client team Festejo / Flores / Garachico, TIP-QC; course content from World Citi Colleges' *Pharmaceutical and Medicinal Organic Chemistry Laboratory* manual — its Appendix C is the authoritative chemistry source). The previous developer quit mid-revisions, but their entire working Unity project (38 scripts, 49 prefabs, an assembled lab scene, an integration guide) survived in a handoff package. This build is therefore an **audit-and-continue**, not a from-scratch rebuild: the inherited code was formally audited (`Docs/audit-report.md`, verdict **GO — reuse the framework**: 7 reuse / 18 refactor / 4 rewrite / 7 discard), migrated, and extended.

**Team & contract.** Rasty Espartero (PM/dev) + David Geisler Mahayag (dev) + Claude as build assistant, under a signed implementation plan with a **hard deadline of August 31, 2026** (schedule began July 7, 2026). Work is tier-ordered so the contract-critical subset (Tier 1) is never at risk.

**Target platform.** Meta Quest 3 (confirmed), 90 Hz target with 72 Hz fallback, controllers and hand tracking. The physical headset has not yet been delivered — development runs in-editor via the XR Device Simulator, with a client escalation deadline in Week 5 if no device arrives.

---

## 2. The game

### 2.1 The per-experiment loop

Every experiment follows the same graded loop:

```
Intro cutscene (objective + ILO card)
  → Phase A: Reagent Preparation        (graded: measuring, diluting, mixing)
  → reagent-prep transition cutscene
  → Phase B: Synthesis                  (graded procedure steps at the lab island)
  → Phase C: Chemical Tests             (graded confirmatory tests with observable outcomes)
  → Phase D: Data Sheet + post-lab quiz (yield entry + 3 MCQs on the tablet)
  → End cutscene — ALWAYS plays, success OR failure variant
  → Grade screen (Grade %, mistakes, time, per-criterion breakdown,
                  PASSED ≥90 / TRY AGAIN, Retry / Continue)
```

Every step completion fires a task-accomplished toast, and the checklists (tablet + wrist watch) auto-check as world-state conditions are met. The four cutscene moments per experiment (intro, reagent-prep transition, success outro, failure outro) are data-driven `CutsceneData` assets played through reusable templates — never bespoke camera animation (the XR camera is never animated; comfort rule).

### 2.2 The 11-experiment roster

The roster is fixed in code as the single source of truth (`ExperimentCatalog`, 1 Tutorial / 2 Prelim / 4 Midterm / 4 Final). All 11 are authored as v2 data assets under `Assets/PharmaSynth/ScriptableObjects/Experiments/`. Task counts below are read directly from the assets (`graphTasks` entries — 90 tasks total). **Tiers are build order, not scope cuts** — all 11 ship.

| # | Period | Experiment | Module id | Asset | Tasks | Tier |
|---|--------|-----------|-----------|-------|:-----:|:----:|
| T0 | Tutorial | Methane Synthesis | `tutorial-methane` | `Tutorial_Methane.asset` | 5 | 1 |
| P1 | Prelim | Chemical Compounding | `prelim-chemical-compounding` | `Prelim_ChemicalCompounding.asset` | 6 | 1 |
| P2 | Prelim | Ethyl Alcohol | `prelim-ethyl-alcohol` | `Prelim_EthylAlcohol.asset` | 7 | 1 |
| M1 | Midterm | Benzoic Acid | `midterm-benzoic-acid` | `Midterm_BenzoicAcid.asset` | 9 | 1 |
| M2 | Midterm | Acetanilide | `midterm-acetanilide` | `Midterm_Acetanilide.asset` | 10 | 2 |
| M3 | Midterm | Acetone | `midterm-acetone` | `Midterm_Acetone.asset` | 10 | 2 |
| M4 | Midterm | Chloroform | `midterm-chloroform` | `Midterm_Chloroform.asset` | 10 | 2 |
| F1 | Final | Benzamide | `final-benzamide` | `Final_Benzamide.asset` | 9 | 2 |
| F2 | Final | Aspirin | `final-aspirin` | `Final_Aspirin.asset` | 7 | 1 |
| F3 | Final | Caffeine | `final-caffeine` | `Final_Caffeine.asset` | 9 | 3 |
| F4 | Final | Wine Making | `final-winemaking` | `Final_WineMaking.asset` | 8 | 2 |

Progression is a **linear prerequisite chain** in roster order (each experiment unlocks the next; period doors open when every experiment in the previous period is passed). Notable per-experiment features: Aspirin includes an **overheat error branch**; Wine Making includes a "7 days & 7 nights" time-skip and will get a bespoke rubric (currently on the standard 6-category rubric as a flagged follow-up).

**Chemistry flags raised to the client** (chemistry is reconciled against manual Appendix C, never the storyboard): Benzoic Acid uses **benzaldehyde + 0.1% KMnO4 oxidation** per the Appendix C reagent list (supersedes the plan's tentative toluene route); Acetanilide's acylating agent is acetyl chloride per the manual (acetic anhydride is the safer alternative, pending client confirmation); Benzamide's nitrous-acid test uses sodium **nitrite** (the manual's "nitrate" is a typo).

### 2.3 NPCs

- **Pharmee** — the floating robot guide. Subtitle dialogue + robot beeps (no voice-over), happy/warning face states, greets on start, instructs the current step, warns per error type, celebrates or encourages at the end mirroring the pass/fail gate. Skippable narration.
- **Dr. Jimenez** — the human examiner. Present in assessment contexts, observes, gives **no hints**. Not yet built (roadmap item 5); a rigged scientist model will be sourced, with a posed-static fallback defined in the plan.

### 2.4 The two-part 90% gate

Passing an experiment requires **both**:

1. **Rubric grade ≥ 90%** — a weighted per-criterion score (Procedure, Chemical Tests, Materials & PPE, Time Management, Sanitation, Documentation) with weights normalized per experiment (the manual's printed weights are inconsistent, so weights are data on each module asset).
2. **BKT mastery ≥ 0.90** — a Bayesian Knowledge Tracing estimate P(learned) per lab skill (Measuring, Heating, Filtration, Transfer, Safety, TestInterpretation), updated on every observed step and lowered by mistakes.

Both thresholds are configurable per module (`masteryThreshold`, default 0.90). This is deliberate pedagogy: a single lucky clean run can score a high grade yet still fail the mastery gate — verified by the self-tests (one clean pass = 96% grade but 0.674 mastery → not passed).

---

## 3. Architecture

### 3.1 The core pattern: plain-C# cores under thin MonoBehaviours

All engine logic (task graph, BKT, scoring, grading, progression, catalog queries) lives in **plain C# classes with no scene or MonoBehaviour dependency**. MonoBehaviours (`ExperimentRunner`, the UI controllers, the chem-rig components) are thin glue that build these cores from data assets and forward Unity lifecycle/events.

**Why:** MCP-driven play mode is unreliable on this machine (play sessions exit silently), so the project's verification strategy is **edit-mode assertion suites** that construct and exercise the pure cores headlessly — 157 assertions run without ever entering play mode (§7). Two supporting conventions: everything is in the **global namespace** (matching the inherited code style), and every component exposes a public `SetX()`/bind method because `Awake`/`OnEnable` don't fire on `AddComponent` in edit mode.

All runtime code is under `Assets/PharmaSynth/Scripts/` — **64 C# files** across 9 folders.

### 3.2 System-by-system map

**`Experiment/` (9 files)** — the experiment engine.

| Class | Role |
|---|---|
| `TaskGraphModel.cs` | Pure data types: `TaskPhase` (ReagentPrep/Synthesis/ChemicalTests/DataSheet), `LabSkill` (6 skills), `RubricCategory` (6 criteria), `TaskCompletionResult`, and `ExperimentTask` (id, label, phase, prerequisites, progress weight, skill, rubric category, required flag, hint). |
| `TaskGraph.cs` | Runtime graph over a task list: prerequisite gating (`BlockedByPrerequisite` = the wrong-step-order signal), weighted `Progress01`, phase/completion events, and pluggable `RegisterCondition(id, Func<bool>)` + `Tick()` for world-state **auto-checking**. |
| `ExperimentModuleDefinition.cs` | The ScriptableObject an experiment IS (see §4). v2 fields: `graphTasks`, `masteryThreshold`, `bkt`, `trackedSkills`, `rubricWeights`, `parTimeSeconds`, ILOs, plus `BuildTaskGraph()/BuildMasteryModel()/BuildScoreCalculator()` factories. Legacy `tasks` list retained for the inherited flow manager. |
| `ExperimentRunner.cs` | MonoBehaviour orchestrator for one attempt: owns TaskGraph + MasteryModel + MistakeLog + grader built from the module; raises `ExperimentStarted/TaskCompleted/PhaseCompleted/ProgressChanged/MistakeRecorded/ExperimentFinished`; auto-records out-of-order attempts as WrongStep; `Update()` advances time + ticks auto-check; computes the two-part gate in `ExperimentResult`; idempotent `Finish`, `Retry` rebuilds. |
| `MistakeLog.cs` | The 9-type error taxonomy (`LabErrorType`: WrongReagent, WrongStep, DroppedGlassware, Overheat, FireSafety, MissingPPE, FumeHoodViolation, ChemicalContact, HazardousAction), counts, `MistakeRecorded` event, and `CategoryFor()` mapping errors to rubric criteria. |
| `ExperimentFlowManager.cs` | **Legacy** inherited event spine — still drives the one inherited Ethyl module until migrated. New work uses `ExperimentRunner`. |
| `ExperimentTaskTrigger.cs` / `ExperimentErrorReporter.cs` / `ProcedureChecklistUI.cs` | Inherited collider-trigger task completion, error reporting, and checklist UI kept from the audit's reuse tier. |

**`Scoring/` (3 files)** — all pure C#.

| Class | Role |
|---|---|
| `MasteryModel.cs` | Standard Bayesian Knowledge Tracing per `LabSkill` (`BktParameters`: pL0=0.25, pTransit=0.2, pSlip=0.1, pGuess=0.2 by default, tunable per module); `Observe(skill, correct)`, `OverallMastery()`, `IsMastered(threshold)`. |
| `ScoreCalculator.cs` | `RubricWeights` (auto-normalized — fixes the manual's inconsistent weights) → `GradeBreakdown` with per-criterion contributions and Total 0..100; time sub-score vs par time. |
| `ExperimentGrader.cs` | Turns run state (graph completion + MistakeLog + elapsed/par + quiz) into per-category sub-scores and a final `GradeBreakdown`; procedural/safety/sanitation mistakes penalize their mapped categories. |

**`Progression/` (5 files)** — save, roster, and flow.

| Class | Role |
|---|---|
| `ProgressionService.cs` | Versioned JSON save (`persistentDataPath/pharmasynth_progress.json` + `.bak` backup, corruption-safe); per-module best grade/mastery/passed/attempts; `IsPassed`/`IsUnlocked`. Path injectable for tests. |
| `ExperimentCatalog.cs` | The ordered 11-entry roster (module id, asset name, title, period, single prerequisite, tier) as static plain data — the one source of truth for menu/hub/experiment-select. |
| `ProgressionFlow.cs` | Read-only queries over the service + catalog: `IsUnlocked`, `NextExperiment()`, `IsPeriodComplete/Unlocked`, `OverallCompletion01()`. |
| `ExperimentLibrary.cs` | ScriptableObject holding direct serialized references to all 11 module assets (`ScriptableObjects/ExperimentLibrary.asset`) — build-safe lookup by moduleId, no Resources/AssetDatabase. |
| `GameFlow.cs` | Tiny static cross-scene state: `SelectedModuleId` written by the menu, read by the lab scene's launcher. |

**`Chemistry/` (12 files)** — the chemistry sim and rigs.

| Class | Role |
|---|---|
| `ChemicalData.cs` | ScriptableObject per reagent: identity, state, liquid colors/viscosity, boiling point, precipitate color, pH, gas evolution, `HazardType`, `requiresFumeHood`. |
| `ReactionRule.cs` / `ReactionRegistry.cs` | SO-based A+B reaction rules (rewritten per audit): result liquid, `ReactionOutcome`, minimum temperature, gas evolution, expected observation; registry with null-guarded lookup. |
| `LiquidPhysics.cs` | Event-driven liquid fill/wobble/color in vessels (de-hardcoded from the inherited version); fires `LiquidAdded`/`ReactionOccurred`/`WrongReagentMixed`. Renders via the authored `PharmaLiquid.shader` (URP, stereo-instancing-safe). |
| `LiquidTaskBinding.cs` | Context-aware reagent→task binding: the correct reagent completes its step (order enforced), an unexpected reagent = WrongReagent, out-of-order = WrongStep, and `requiresFumeHood` reagents used outside the hood = FumeHoodViolation. |
| `TemperatureSim.cs` | Exact exponential `HeatModel` (timestep-independent, unit-tested) + target/overheat events + `AtLeast()` auto-check predicate (distillation cut-offs, the aspirin overheat branch). |
| `GasCollection.cs` | Fill-fraction gas collection, balloon scaling, full event, `Collected()` predicate. |
| `CrystallizationController.cs` / `FiltrationController.cs` | Timed liquid→crystal rig and filtrate-accumulation rig, each with done events/predicates for auto-check. |
| `LiquidPourer.cs` / `PowderPourer.cs` / `PowderPhysics.cs` | Inherited pour-stream systems (LineRenderer + particles, Quest-friendly). |

**`NPC/` (6 files)** — Pharmee and cutscenes.

| Class | Role |
|---|---|
| `PharmeeBrain.cs` | Dialogue state machine (Idle/Greeting/Instructing/Warning/Celebrating/Encouraging) subscribed to the runner; instructs from task hints, warns per error type, celebrates/encourages mirroring the gate. |
| `PharmeeFace.cs` | `IPharmeeFace` material-tint face states (Neutral/Happy/Warning). |
| `NPCNarrationController.cs` | Subtitle playback + reactive one-liner `Say()` API (beep audio hooks present, audio pass pending). |
| `CutsceneData.cs` | VR-safe data cutscene: subtitle beats + Pharmee expression + duration; `Kind` = Intro/ReagentPrep/Success/Failure. |
| `CutsceneDirector.cs` | Subscribes to the runner: intro on start, transition on reagent-prep phase complete, success/failure on finish — the **end cutscene always plays**. |
| `ModuleCutsceneController.cs` | Inherited PlayableDirector-based controller (double-fire and Hold-wrap bugs fixed per audit). |

**`UI/` (10 files)** — all world-space; the live experiment surfaces are driven by `ExperimentRunner` (event subscriptions, except the wrist watch, which polls the runner's state each frame).

| Class | Role |
|---|---|
| `ExperimentHudController.cs` | Title, HH:MM:SS count-up timer, progress bar that dips on mistakes, task-accomplished toasts. |
| `TabletChecklistController.cs` | Phase-grouped auto-checking checklist built from `graphTasks` + reaction footer. |
| `GradeScreenController.cs` | Grade %, mistakes, time, per-criterion breakdown, PASSED/TRY AGAIN; Continue gated on pass; Retry/Continue UnityEvents; auto-shows on finish. |
| `WristWatchController.cs` | Wrist-flip gesture (supination + gaze with hysteresis) **plus an InputAction button fallback**; compact step/progress/mastery readout. Right hand default. |
| `MainMenuController.cs` | Menu buttons: Tutorial (launches Methane), Laboratory (resolves the player's next experiment via `ProgressionFlow` and loads the lab), Settings toggle, Quit. |
| `FaceCamera.cs` | Billboard utility: keeps world-space labels/canvases rotated to face the player's camera (Y-axis upright mode + yaw offset). |
| `TimerController.cs` / `MoveInstructionsOnTilt.cs` / `ChemLabelUpdater.cs` / `UIfuncs.cs` | Inherited utilities (`MoveInstructionsOnTilt` was the seed of the wrist-watch gesture). |

**`Interaction/` (12 files)** — XR interaction layer.

| Class | Role |
|---|---|
| `ExperimentTaskStation.cs` | A world location where a step happens: completes its bound task via XRI select, trigger enter, or `Activate()`; optional `requiredItemId` + `AcceptsItem()` so **only the correct grabbable prop** completes it. Also hosts the static `ExperimentStationRegistry` (taskId→Transform for waypoints). |
| `LabItem.cs` | Identity on a grabbable prop (`itemId`, e.g. `lit-splint`) matched against stations — the hands-on "bring the right item to the right apparatus" mechanic. |
| `ExperimentLauncher.cs` | Loads any of the 11 experiments by moduleId from the `ExperimentLibrary`, swaps the runner's module, starts a fresh attempt; can auto-launch `GameFlow.SelectedModuleId` on scene load; `onModuleLoaded` event for scene rewiring. |
| `ExperimentStarter.cs` | Poke-to-Begin/Retry button binding. |
| `WaypointGuide.cs` | Moves the guidance marker to the current step's registered station. |
| `DevExperimentDriver.cs` | Keyboard test driver (see §5). Editor-only unless `enableInBuild`. |
| `BreakableGlassware.cs`, `WeighingScaleController.cs`, `XRBottleUI.cs`, `TabletGestureController.cs`, `TVRepositionController.cs`, `EquipmentLabelVisibilityController.cs` | Inherited interaction components kept per the audit (glass shatter, scale, bottle UI, gesture relay, TV preset, label visibility). |

**`Safety/` (6 files)** — the error/safety matrix's world sensors.

| Class | Role |
|---|---|
| `PPEController.cs` | PPE locker interaction; donning PPE removes the lab-entry blockers and drives worn visuals/mirror hooks (`onPPEWorn`). No sensor records `MissingPPE` yet — that error type exists only in the taxonomy/grader. |
| `HazardZones.cs` | `FumeHoodZone` (occupancy + `Contains` check used by `LiquidTaskBinding`) and `HazardZone` (contact → debounced `RecordMistake`). |
| `FireProcedureZone.cs`, `AcidCorrosion.cs`, `SaltBurn.cs`, `SpoonSaltController.cs` | Inherited hazard/demo components being folded into the unified matrix. |

**`Editor/` (1 file)** — `PharmaSelfTests.cs`, the regression suite (§7).

### 3.3 Event flow

```
ExperimentModuleDefinition (data)
        │  BuildTaskGraph / BuildMasteryModel / BuildScoreCalculator
        ▼
ExperimentRunner ──ExperimentStarted──────────┐
   │  TaskCompleted / PhaseCompleted /        ▼
   │  ProgressChanged / MistakeRecorded    PharmeeBrain → NPCNarrationController + PharmeeFace
   │        │                              CutsceneDirector (intro / transition / success / failure)
   │        ├─→ ExperimentHudController   (timer, progress, toasts)
   │        ├─→ TabletChecklistController (auto-checking checklist)
   │        └─→ WristWatchController      (compact step/mastery)
   └──ExperimentFinished(ExperimentResult)──→ GradeScreenController → ProgressionService.RecordResult
```

Inputs into the runner: `ExperimentTaskStation`/`LabItem` (hands-on), `LiquidTaskBinding` (pouring), auto-check predicates from `TemperatureSim`/`GasCollection`/`CrystallizationController`/`FiltrationController` via `TaskGraph.RegisterCondition`, safety zones via `RecordMistake`, and `DevExperimentDriver` (keyboard).

### 3.4 Cross-scene flow

`MainMenuController` (MainMenu scene) → `GameFlow.SelectedModuleId` → scene load → `ExperimentLauncher` (lab scene) reads the id, fetches the module from `ExperimentLibrary.asset`, swaps it into the `ExperimentRunner`, and begins. `ProgressionFlow` (over `ProgressionService` + `ExperimentCatalog`) answers "what's unlocked / what's next / which period doors are open."

---

## 4. Content pipeline — experiments are DATA

Experiments are **ScriptableObjects, not scenes or code**. Adding experiment #12 requires zero engine changes.

**To author a new experiment module:**

1. Create the asset: right-click → *Create → VR ChemLab → Experiment Module* (or duplicate an existing one) under `Assets/PharmaSynth/ScriptableObjects/Experiments/`.
2. Set `moduleId` (kebab-case, e.g. `midterm-acetone`), `moduleTitle`, and the intended learning outcomes.
3. Author `graphTasks`: per task an id, label, phase (ReagentPrep/Synthesis/ChemicalTests/DataSheet), prerequisite task ids, progress weight, `LabSkill`, `RubricCategory`, required flag, and a hint (surfaced by Pharmee and the tablet).
4. Set the mastery/scoring block: `masteryThreshold` (default 0.90), BKT parameters, `rubricWeights` (auto-normalized), `parTimeSeconds`.
5. Register it in `ExperimentCatalog.Entries` (id, asset name, title, period, prerequisite, tier) and add the asset to `ExperimentLibrary.asset`.
6. Wire the world: reaction rules in the `ReactionRegistry`, per-vessel `LiquidTaskBinding`, station/prop wiring (`ExperimentTaskStation` + `LabItem`), and 4 `CutsceneData` assets.
7. Run *Tools ▸ PharmaSynth ▸ Run Self-Tests* — the content suite loads and solves every cataloged module to 100% respecting prerequisites.

For all 11 experiments, steps 1–5 are **done**; step 6 (world wiring + cutscene data) is done for Methane only and is the per-experiment remaining work.

**Reagents** are `ChemicalData` assets — 16 exist under `ScriptableObjects/Chemicals/` (e.g. `Chem_SodiumAcetate`, `Chem_Benzaldehyde`, `Chem_PotassiumPermanganate`, `Chem_AceticAnhydride`, `Chem_SulfuricAcid`) carrying visuals, physical properties for tests/thresholds, and safety data (`requiresFumeHood` drives the fume-hood rule).

**Cutscenes** are `CutsceneData` assets (subtitle beats + Pharmee expression + duration; kinds Intro/ReagentPrep/Success/Failure) under `ScriptableObjects/Cutscenes/` — currently the 4 Methane ones (`Methane_Intro`, `Methane_ReagentPrep`, `Methane_Success`, `Methane_Failure`); each remaining experiment needs its 4.

**Chemistry source of truth:** the lab manual's **Appendix C** — *never* the storyboard/cutscene PDFs, which contain garbled AI-generated labels and copy-paste chemistry errors. They are visual references with explicit license to exceed. All storyboard-derived reagent labels are being re-authored clean.

---

## 5. Scenes & how to play-test

Two scenes under `Assets/Scenes/`:

- **`MainMenu.unity`** — the lobby: Tutorial / Laboratory / Settings / Quit, driven by `MainMenuController` (Laboratory resolves your next unlocked experiment from the save and loads the lab).
- **`SampleScene.unity`** — the assembled laboratory (10.6×11.2 m room, widened central aisle, collision-passed furniture) with `ExperimentSystems` (runner + starter + guide + dev driver, module = Methane), the world-space `LabHUD` / `LabTablet` / `GradeScreen`, `RobotNPC` (Pharmee), 5 Methane stations, PPE locker, Begin button, waypoint marker, and the XR rig.

**Hands-on Methane flow (the real gameplay):** open SampleScene → Play → grab one of the five props under `MethaneProps` (staged in front of the right island; the earlier stand-in "ReagentBench" object no longer exists in the scene) and carry it into its matching station trigger zone. Five `LabItem` props map to the five stations: `reagent-jar`→PrepareMixture, `glass-tube`→SetupApparatus, `burner`→HeatMixture, `collection-tube`→CollectGas, `lit-splint`→TestGas. Order is enforced (wrong prop is ignored; wrong order records a WrongStep mistake).

**Keyboard fallback:** `DevExperimentDriver` on `ExperimentSystems` — **B** = begin/restart, **1–5** = complete step N, **F** = finish (shows the grade screen), **R** = retry. This drives the entire loop without XR hardware.

**XR input without a headset:** the XR Device Simulator is imported and staged in the scene. Note: MCP-driven play mode is unreliable on this machine — a human presses Play.

**Regression suite:** menu **Tools ▸ PharmaSynth ▸ Run Self-Tests** — currently **157/157 assertions green** (see §7).

---

## 6. Build & platform

**Android / Quest 3 (the active build target):**

- Graphics: **Vulkan-first** (+ OpenGLES3 fallback), single-pass instanced rendering.
- Scripting: **IL2CPP**, **ARM64**.
- Textures: **ASTC**.
- XR: **OpenXR is the sole active loader** (verified — the deprecated `com.unity.xr.oculus` package was removed). `com.unity.xr.meta-openxr` supplies Quest-specific features. **Meta Quest Support**, **Meta Quest Touch Plus + Oculus Touch controller profiles**, **Hand Interaction profile**, and **Foveated Rendering** (plain OpenXR lacks FFR) were enabled on Android during the W2 config pass, **but the on-disk `Assets/XR/Settings/OpenXR Package Settings.asset` currently has every one of those feature flags reset to disabled** (regressed somewhere between the W2 and W5 commits — likely one of the editor crashes during XR/package operations; the Meta feature-set selection survives in the OpenXR editor settings). **Re-enable them in Project Settings → XR Plug-in Management → OpenXR (Android) before any device build.** `InitManagerOnStart = TRUE` on Android.
- **Standalone (editor/PC) keeps `InitManagerOnStart = FALSE`** — deliberate: OpenXR auto-init on this headset-less PC kills play mode, so the editor and XR Device Simulator run safely; Quest builds auto-init as normal.
- No Cinemachine, by design — the XR camera is never animated.

**Quest 3 performance budget** (master plan §4.5, 90 Hz target / 72 Hz fallback):

| Budget | Limit |
|---|---|
| Draw calls | ≤ 150 |
| Visible triangles | ≤ 1.2 M |
| Active rigidbodies | ≤ 40 (kinematic-on-grab) |
| Live particles | ≤ 3 k |
| Textures | 2K ASTC; MSAA 4× validated against fill rate (transparent glassware overdraw is the known killer) |
| Lighting | Baked + probes, +1 realtime shadowed key light; FFR via meta-openxr; shader-fill liquids |

A consequence to watch: all 42 lab-equipment prefabs are now gravity-on physics grabbables — experiment scene composition must rest them on collidered surfaces and stay within the 40-rigidbody budget.

---

## 7. Verification strategy

Three layers, designed around the constraint that **MCP-driven play mode is unreliable** on the dev machine:

1. **The self-test suite** — `Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.cs`, run via **Tools ▸ PharmaSynth ▸ Run Self-Tests**: **157/157 assertions green**, organized in 13 suites (TaskGraph, Mastery/BKT, Score, Grader, Runner, Progression, ChemVisual, UI, W4 cutscene/cryst/filt/hazard, Interaction, ProgressionFlow, Library, Content). Because the engine is plain-C#-core, the suite exercises everything headlessly in edit mode: prerequisite blocking, weighted progress, BKT convergence past the 0.90 gate, hand-checked grade math (e.g. a realistic 4-mistake run = exactly 81.08%), the two-part gate, JSON save round-trips, catalog/prereq-chain integrity, and loading + solving **every one of the 11 modules to 100%** respecting prerequisites. Re-run it after any engine or content change.
2. **The zero-error console gate** — every review checkpoint requires a clean Unity console. The console is currently zero-error.
3. **Human play-testing** — a human presses Play for anything runtime: XR Device Simulator playthroughs (happy path + error branches), grab-test ergonomics (trigger-zone sizes/heights are an open validation item), and visual verification via editor captures. **On-device Quest 3 QA** (comfort, 90 Hz hold worst-case, hand tracking, wrist-gesture ergonomics, full revision-checklist UAT, ISO/IEC 25010 acceptance mapping) is a day-1 activity when the headset arrives.

Supporting habits: chemistry QA against Appendix C; grep the whole `Assets/` tree for a type name before deleting any script (a past deletion broke a dependent component).

---

## 8. Team & process

- **People:** Rasty Espartero (PM + dev), David Geisler Mahayag (dev), Claude Code as build assistant (~1.3 human FTE + AI multiplier).
- **Branches:** all W1–W5 milestone commits live on **`main`** (the current branch); **`feature/asset-intake`** exists but was left behind at the initial approved-planning commit. Commits and pushes happen **only when explicitly requested** — never proactively, no destructive git operations. Handoff binaries and digest images are gitignored; the original handoff archives live in an off-repo, MD5-verified backup.
- **Living documents:** `CLAUDE.md` (repo root) is the always-current state/handoff — read it first each session; the approved master plan (path below) records every design decision and is updated when decisions change. Game-design changes are confirmed with the user/client before implementation.
- **Client gates:** week-by-week checkpoints per the plan — audit findings & tier order (W1), scoring-weight sign-off (W2, hard exit criterion), dialogue copy sign-off (W4), headset escalation deadline (W5), rolling module reviews (W6), final validation & turnover (W8). Open client flags: the three chemistry reconciliations (§2.2), the wine rubric, and Dr. Jimenez model budget. Slip policy (pre-agreed): Caffeine → bespoke fail-cutscene staging → wine finale extras, in that order; Tier 1 is never at risk.
- **Status at a glance (2026-07-08):** Weeks 1–4 done, Week 5 in progress. Engine feature-complete and verified; all 11 experiments authored as data; Methane playable hands-on end-to-end; main menu wired. Remaining: per-experiment world wiring + cutscene data, art/audio passes, Dr. Jimenez, menus/hub polish, and on-device validation.

---

## References

- **Master plan (approved 2026-07-07):** `C:\Users\MSI\.claude\plans\you-are-the-best-cozy-possum.md` — full design spec, architecture, asset gap list, week-by-week roadmap, error-handling matrix, Quest 3 perf budget, verification strategy.
- **Known gaps & remaining work:** [`Docs/gaps.md`](gaps.md) — the companion document tracking what is *not* done yet and why.
- **Inherited-code audit:** [`Docs/audit-report.md`](audit-report.md).
- **Live session state:** [`CLAUDE.md`](../CLAUDE.md) (repo root).
