# PharmaSynth Mechanics Guide

The definitive reference to **how every game mechanic works** in PharmaSynth (Meta Quest 3 VR chemistry-lab education game, Unity 6000.5.2f1 / URP / OpenXR + XRI 3.5.1), written for developers extending the game. Every section is grounded in the actual runtime code under `Assets/PharmaSynth/Scripts/` (all classes live in the **global namespace**, matching the inherited code style) and the data assets under `Assets/PharmaSynth/ScriptableObjects/`. The architecture is deliberately layered: **plain, unit-testable C# cores** (`TaskGraph`, `MasteryModel`, `ScoreCalculator`, `ExperimentGrader`, `MistakeLog`, `HeatModel`, `ProgressionService`, `ProgressionFlow`, `ExperimentCatalog`) wrapped by **thin MonoBehaviours** that only do scene glue. All mechanics described here are verified by the regression suite in `Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.cs` (menu **Tools ▸ PharmaSynth ▸ Run Self-Tests**, currently 157/157 green).

## Table of contents

1. [Task graph](#1-task-graph)
2. [Scoring (rubric grade)](#2-scoring-rubric-grade)
3. [Mastery (BKT) and the two-part pass gate](#3-mastery-bkt-and-the-two-part-pass-gate)
4. [Mistakes and the error matrix](#4-mistakes-and-the-error-matrix)
5. [Interactions (grabbable props, stations, guidance)](#5-interactions-grabbable-props-stations-guidance)
6. [Chemistry simulations](#6-chemistry-simulations)
7. [Safety systems](#7-safety-systems)
8. [NPC (Pharmee) and cutscenes](#8-npc-pharmee-and-cutscenes)
9. [Progression, save data, and game flow](#9-progression-save-data-and-game-flow)
10. [UI systems](#10-ui-systems)
11. [System event map (who listens to what)](#11-system-event-map-who-listens-to-what)

---

## 1. Task graph

**Files:** `Assets/PharmaSynth/Scripts/Experiment/TaskGraphModel.cs` (data types), `Assets/PharmaSynth/Scripts/Experiment/TaskGraph.cs` (runtime engine), `Assets/PharmaSynth/Scripts/Experiment/ExperimentModuleDefinition.cs` (authoring container).

### How it works

Every experiment is **data**: an `ExperimentModuleDefinition` ScriptableObject (create via menu `VR ChemLab/Experiment Module`) whose `graphTasks` list is the procedure. Each `ExperimentTask` node has:

| Field | Type | Default | Meaning |
|---|---|---|---|
| `taskId` | `string` | `"task-id"` | Stable id; referenced by stations, bindings, prerequisites |
| `label` | `string` | `"Task"` | Shown on tablet / HUD toasts / wrist watch |
| `phase` | `TaskPhase` | `Synthesis` | One of the 4 graded phases |
| `prerequisites` | `List<string>` | empty | Task ids that must be complete before this one is available |
| `progressWeight` | `float` (`[Min(0)]`) | `1` | Relative contribution to overall progress |
| `skill` | `LabSkill` | `Measuring` | Which BKT competency a correct completion updates |
| `rubricCategory` | `RubricCategory` | `Procedure` | Which rubric criterion this task scores under |
| `required` | `bool` | `true` | Required tasks gate phase/module completion; optional tasks only add score |
| `hint` | `string` | `""` | Spoken by Pharmee / shown when the player is stuck |

The four phases (`TaskPhase`, plan §3.2) are **`ReagentPrep`, `Synthesis`, `ChemicalTests`, `DataSheet`**. The six BKT skills (`LabSkill`) are `Measuring, Heating, Filtration, Transfer, Safety, TestInterpretation`. The six rubric criteria (`RubricCategory`) are `Procedure, ChemicalTests, MaterialsAndPPE, TimeManagement, Sanitation, Documentation`.

`TaskGraph` is a **plain C# class** (no MonoBehaviour) built from the module's tasks via `module.BuildTaskGraph()`. The constructor skips null tasks, empty ids, and duplicate ids, and sums `progressWeight` into a total. Core behavior:

- **Availability:** `IsAvailable(taskId)` = task exists, is not complete, and `PrerequisitesMet(taskId)` (every listed prerequisite is in the completed set). `AvailableTasks()` enumerates all currently-doable tasks in authored order — the first one is treated everywhere (waypoint, watch, Pharmee) as "the current step".
- **Completion:** `TryComplete(taskId)` returns a `TaskCompletionResult`: `Completed`, `AlreadyComplete`, `BlockedByPrerequisite`, or `UnknownTask`. **`BlockedByPrerequisite` is the canonical "wrong step order" signal** — `ExperimentRunner.CompleteTask` converts it into a `WrongStep` mistake (see §4).
- **Events:** `TaskCompleted(ExperimentTask)` on every completion; `PhaseCompleted(TaskPhase)` fires **once** when the last *required* task of a phase completes (`IsPhaseComplete` returns false for a phase with no required tasks); `AllRequiredCompleted` fires **once** when `RequiredRemaining() == 0`.
- **Weighted progress:** `Progress01` = (sum of completed tasks' weights) / (total weight), clamped to `[0,1]`. This is the *clean* completion ratio — mistake penalties on the visible bar are applied by the HUD layer only (§10). If total weight is `<= 0`, it returns `1` when anything is complete, else `0`.
- **Auto-check:** `RegisterCondition(taskId, Func<bool> condition)` binds a world-state predicate to a task; `Tick()` evaluates conditions **only for currently-available tasks** and calls `TryComplete` when a predicate returns true. `ExperimentRunner.Update()` calls `graph.Tick()` every frame, so tasks like "heat to 56 °C" complete themselves the moment the world state is right (see the chemistry predicates in §6).
- **Reset:** `Reset()` clears completion state (retry actually rebuilds a fresh graph via `ExperimentRunner.StartExperiment()`).

Example authored data: `Assets/PharmaSynth/ScriptableObjects/Experiments/Tutorial_Methane.asset` has 5 tasks (`prepare-mixture` → `setup-apparatus` → `heat-mixture` → `collect-gas` → `test-gas`), each weight 1 (20% apiece), chained by prerequisites, `masteryThreshold: 0.9`, `parTimeSeconds: 300`.

### How to extend

- **New experiment:** author a new `ExperimentModuleDefinition` asset — no code. Fill `graphTasks` with ids/prereqs/weights/skills/rubric categories, set `masteryThreshold`, `bkt`, `rubricWeights`, `parTimeSeconds`. Add it to `ExperimentLibrary.asset` and (if it should appear in progression) `ExperimentCatalog` (§9).
- **Branching/optional steps:** set `required = false` for optional score-only tasks; prerequisites form a DAG, not just a chain — parallel branches simply list the same prerequisite.
- **New auto-check:** call `runner.RegisterCondition(taskId, predicate)` after `StartExperiment()` (conditions live on the graph, so re-register after every start/retry — `ExperimentLauncher.onModuleLoaded` is the hook for this).
- **New phase or skill:** extend the `TaskPhase` / `LabSkill` enum in `TaskGraphModel.cs`, then update `TabletChecklistController.PhaseLabel` (§10) and check `MistakeLog.SkillFor` mappings (§4).

---

## 2. Scoring (rubric grade)

**Files:** `Assets/PharmaSynth/Scripts/Scoring/ScoreCalculator.cs` (`RubricWeights`, `GradeBreakdown`, `ScoreCalculator`), `Assets/PharmaSynth/Scripts/Scoring/ExperimentGrader.cs` (`GradingConfig`, `ExperimentGrader`).

### How it works

**`RubricWeights`** (serialized per module in `ExperimentModuleDefinition.rubricWeights`) holds the six category weights. Defaults:

| Category | Default weight |
|---|---|
| `procedure` | 0.40 |
| `chemicalTests` | 0.20 |
| `materialsAndPPE` | 0.15 |
| `timeManagement` | 0.10 |
| `sanitation` | 0.10 |
| `documentation` | 0.05 |

Weights **need not sum to 1** — `ScoreCalculator.Compute` normalizes by `Total()` (using 1 if the total is 0), which deliberately absorbs the WCC lab manual's inconsistent printed weights. Each category contributes `(weight / normalizedTotal) * clamp01(subScore) * 100`, producing a `GradeBreakdown` struct with per-category percentages and `Total` in 0..100.

**`ExperimentGrader.Grade(graph, log, elapsedSeconds, parSeconds, quizFraction)`** builds the per-category sub-scores (each 0..1):

1. **Completion ratio per category** — for each `RubricCategory`, the fraction of that category's tasks completed (unweighted task count). A category with **no authored tasks gets full credit (1.0)** unless a penalty applies.
2. **Mistake penalties** (`GradingConfig`, serialized on `ExperimentRunner.gradingConfig`, client-tunable):
   - `Procedure` sub-score loses `procedureMistakeStep` (**0.10**) per `WrongReagent` / `WrongStep` / `Overheat` mistake.
   - `MaterialsAndPPE` loses `safetyMistakeStep` (**0.15**) per `FireSafety` / `MissingPPE` / `FumeHoodViolation` / `ChemicalContact` / `HazardousAction` mistake.
   - `Sanitation` loses `sanitationMistakeStep` (**0.20**) per `DroppedGlassware`.
   - `ChemicalTests` is the pure completion ratio (no mistake penalty).
3. **`TimeManagement`** = `ScoreCalculator.TimeSubScore(elapsed, par)`: full credit (1.0) at or under `parTimeSeconds`, then **linear decay to 0 at 2× par**. `parSeconds <= 0` disables the criterion (always 1).
4. **`Documentation`** = `clamp01(quizFraction)` — the post-lab quiz score 0..1. `ExperimentRunner.Finish(quizFraction = 0f)` takes it as a parameter (the `DevExperimentDriver`'s **F** key passes `1f`).

All sub-scores are clamped to `[0,1]` before weighting.

### How to extend

- **Tune penalties per deployment** by editing the `GradingConfig` fields on the `ExperimentRunner` in the scene (all `[Range(0,1)]`).
- **Per-experiment weighting** (e.g. Wine Making's bespoke workmanship/appearance/flavour rubric — a flagged follow-up) is just a different `rubricWeights` on that module asset; if new *categories* are needed, extend `RubricCategory`, `RubricWeights.WeightOf`, `GradeBreakdown`, `ExperimentGrader.Grade`'s sub-score dictionary, and `GradeScreenController.BuildBreakdown`.
- **A different time curve** (e.g. grace window, steeper falloff) only touches the static `ScoreCalculator.TimeSubScore`.
- **Quiz UI:** roadmap item 8 — build the data-sheet/quiz canvas, compute a 0..1 fraction, and pass it into `runner.Finish(fraction)` instead of a constant.

---

## 3. Mastery (BKT) and the two-part pass gate

**Files:** `Assets/PharmaSynth/Scripts/Scoring/MasteryModel.cs` (`BktParameters`, `MasteryModel`), `Assets/PharmaSynth/Scripts/Experiment/ExperimentRunner.cs` (`ExperimentResult`, the gate).

### How it works

**`BktParameters`** (serialized per module as `ExperimentModuleDefinition.bkt`) are standard Bayesian Knowledge Tracing parameters, defaults from plan §3.6:

| Parameter | Default | Meaning |
|---|---|---|
| `pL0` | 0.25 | Prior probability the skill is already learned |
| `pTransit` | 0.20 | Probability of learning after an opportunity |
| `pSlip` | 0.10 | Probability of a mistake despite knowing |
| `pGuess` | 0.20 | Probability of being right without knowing |

**`MasteryModel`** tracks `P(learned)` per `LabSkill`. It is built by `module.BuildMasteryModel()` — using `trackedSkills` if authored, else auto-deriving the distinct skills referenced by `graphTasks`. `Observe(skill, correct)` applies the Bayes posterior update (correct: `p(1-slip) / (p(1-slip) + (1-p)guess)`; incorrect: `p·slip / (p·slip + (1-p)(1-guess))`) and then the learning step `posterior + (1-posterior)·pTransit`. A skill first observed at runtime is added with the `pL0` prior. `OverallMastery()` is the **mean** `P(learned)` across tracked skills; `IsMastered(threshold)` compares it to the gate.

**How observations flow in:** `ExperimentRunner` calls `_mastery.Observe(task.skill, true)` on every task completion (positive evidence), and `RecordMistake` calls `Observe(MistakeLog.SkillFor(type), false)` (negative evidence) — both only if the skill `IsTracked`.

**The two-part pass gate** lives in `ExperimentRunner.BuildResult` and is emitted as an `ExperimentResult` struct:

```csharp
bool gradePass   = grade.Total >= threshold * 100f;   // grade % gate
bool masteryPass = mastery >= threshold;              // BKT gate
passed = gradePass && masteryPass;
```

Note that **one field — `module.masteryThreshold` (default 0.90) — drives both gates**: grade must reach `threshold × 100` percent AND overall BKT mastery must reach `threshold`. Fallbacks when no module is assigned: threshold 0.9, par 600 s. `ExperimentResult` also carries `grade` (the full `GradeBreakdown`), `overallMastery`, `gradePassed`, `masteryPassed`, `mistakeCount`, and `elapsedSeconds`.

This is intentional pedagogy: the self-tests verify that a single clean run of Methane scores ~96% grade (grade gate passes) but only ~0.67 mastery — **the player must repeat or perform enough positive observations to convince the BKT model**, exactly as the manuscript's 90% mastery requirement intends. `Evaluate(quizFraction)` computes the same result without ending the attempt (live grade preview); `Finish(quizFraction)` is idempotent and fires `ExperimentFinished` exactly once; `Retry()` rebuilds everything fresh.

### How to extend

- **Tune difficulty per module** via `bkt` and `masteryThreshold` on the module asset — e.g. a higher `pTransit` makes single-run mastery achievable for the tutorial.
- **Separate grade vs. mastery thresholds:** add a second float to `ExperimentModuleDefinition` and split the two lines in `BuildResult` — everything downstream (`GradeScreenController`, `ProgressionService`) already consumes the separate `gradePassed` / `masteryPassed` flags.
- **More granular evidence:** call `runner.RecordMistake` (negative) or reach into task completion (positive) — do *not* bypass the runner, or the mastery/grade bookkeeping desyncs.

---

## 4. Mistakes and the error matrix

**Files:** `Assets/PharmaSynth/Scripts/Experiment/MistakeLog.cs`, `Assets/PharmaSynth/Scripts/Experiment/ExperimentRunner.cs`, `Assets/PharmaSynth/Scripts/UI/ExperimentHudController.cs`.

### How it works

**`LabErrorType`** is the 9-value error taxonomy (plan §3.7): `WrongReagent, WrongStep, DroppedGlassware, Overheat, FireSafety, MissingPPE, FumeHoodViolation, ChemicalContact, HazardousAction`.

`MistakeLog` (plain C#) records `(type, message)` entries, exposes `Count` / `CountOf(type)` / `CountOfAny(types)`, fires `MistakeRecorded(LabErrorType, string)` per entry, and holds the two static mapping tables:

- **`CategoryFor(type)`** — which rubric criterion the mistake deducts from: `WrongReagent` / `WrongStep` / `Overheat` → `Procedure`; `DroppedGlassware` → `Sanitation`; everything else (fire, PPE, fume hood, contact, hazardous) → `MaterialsAndPPE`.
- **`SkillFor(type)`** — which BKT skill it reflects on: `Overheat` → `Heating`; `FireSafety` / `MissingPPE` / `FumeHoodViolation` / `ChemicalContact` / `HazardousAction` → `Safety`; the rest (wrong reagent/step, dropped glass) → `Transfer`.

**How mistakes reach the runner** — two paths:

1. **Explicit:** any system calls `runner.RecordMistake(type, message)`. Callers today: `LiquidTaskBinding` (WrongReagent, FumeHoodViolation — §6), `HazardZone.Report()` (§7). This records into the `MistakeLog` **and** applies the negative BKT observation.
2. **Automatic wrong-order detection:** `runner.CompleteTask(taskId)` returning `BlockedByPrerequisite` auto-records `WrongStep` with the message `"Attempted '<taskId>' out of order"`. So *any* interaction routed through `CompleteTask` (stations, liquid bindings, dev keys) gets order-enforcement for free.

The runner re-broadcasts every entry through its own `MistakeRecorded` event, which is what the HUD, Pharmee, and grader-facing counters consume.

**HUD bar dip:** the visible progress bar is *not* `TaskGraph.Progress01`. `ExperimentHudController` keeps its own mistake counter and displays the pure static function:

```csharp
DisplayedProgress(graphProgress01, mistakes, penaltyStep) // = clamp01(progress − mistakes × penaltyStep)
```

with `mistakeProgressPenalty` defaulting to **0.05** (5% dip per mistake, storyboard behavior). The underlying graph progress stays clean, so completing more tasks "recovers" the bar; the grade penalty is computed independently by `ExperimentGrader` (§2).

### How to extend

- **New error type:** add the enum value, then update the three mapping switch points: `MistakeLog.CategoryFor`, `MistakeLog.SkillFor`, and the `CountOfAny` groupings in `ExperimentGrader.Grade` — plus a warn line in `PharmeeBrain.WarnLineFor` if it deserves a bespoke reaction.
- **New detector:** give the component an `ExperimentRunner` reference and call `RecordMistake` — debounce it like `HazardZone` does (§7) so a dwell doesn't spam entries.

---

## 5. Interactions (grabbable props, stations, guidance)

**Files:** `Assets/PharmaSynth/Scripts/Interaction/LabItem.cs`, `ExperimentTaskStation.cs` (also contains `ExperimentStationRegistry`), `ExperimentStarter.cs`, `WaypointGuide.cs`, `DevExperimentDriver.cs`. (`ExperimentLauncher.cs` is covered in §9.)

### How it works

**`LabItem`** is the identity component on a grabbable prop: `itemId` (a stable string like `"reagent-jar"`, `"lit-splint"`), `displayName`, and the static resolver `LabItem.Resolve(Collider)` which walks `GetComponentInParent<LabItem>()` (colliders are often on children). Props in the scene are standard **XRI**: `XRGrabInteractable` + `Rigidbody` + collider + `LabItem`.

**`ExperimentTaskStation`** is a world location bound to one `taskId`; completing the station calls `runner.CompleteTask(taskId)` (so prerequisite order and the WrongStep auto-mistake come from §1/§4 for free). It has **two activation paths**:

- **Poke/select path** (`activateOnSelect`, default *true*): on enable it hooks the co-located `XRBaseInteractable.selectEntered` → `Activate()`. This was the original abstract "poke-cube" interaction.
- **Trigger-zone path** (`activateOnTriggerEnter`, default *false*): `OnTriggerEnter` filters by optional `requiredTag`, then — if `requiredItemId` is set — resolves the entering collider to a `LabItem` and **ignores any prop whose `itemId` doesn't match** (wrong prop = silent no-op, not a mistake; bringing the right prop out of order *is* a WrongStep via the runner). `AcceptsItem(LabItem)` is the pure predicate used by edit-mode tests. This is the current hands-on Methane flow: five grabbable props (`reagent-jar`→`prepare-mixture`, `glass-tube`→`setup-apparatus`, `burner`→`heat-mixture`, `collection-tube`→`collect-gas`, `lit-splint`→`test-gas`) carried from the `ReagentBench` into their matching station zones.

**`ExperimentStationRegistry`** (static) maps `taskId → Transform`; stations `Register` on `OnEnable` and `Unregister` on `OnDisable`. It exists so guidance can find *where* a step happens.

**`WaypointGuide`** polls each `Update`: takes the **first** entry of `runner.Graph.AvailableTasks()` as the current step, looks its station up in the registry, and floats the `marker` at `station.position + Vector3.up * heightOffset` (default 0.55). It hides when nothing is available or the run has ended.

**`ExperimentStarter`** hooks a `selectEntered` on its own interactable (the Begin button; `beginOnSelect` default true) → `Begin()`, which optionally clears the station registry (`clearStationRegistryOnBegin`, default true — stale stations from a previous module drop out; live stations re-register when re-enabled) and calls `runner.StartExperiment()`. `Retry()` is an alias for `Begin()` — the grade screen's Retry event wires here.

**`DevExperimentDriver`** (on the `ExperimentSystems` object) is the keyboard fallback for editor testing, active only in the Editor unless `enableInBuild`:

| Key | Action |
|---|---|
| **B** | `starter.Begin()` (or `runner.StartExperiment()` if no starter) |
| **1–5** | `runner.CompleteTask` on graph task index 0–4 (order still enforced) |
| **F** | `runner.Finish(1f)` — note it passes a full quiz fraction |
| **R** | `runner.Retry()` |

It reads `Keyboard.current` from the new Input System.

### How to extend

- **New hands-on step:** create the prop (`XRGrabInteractable` + `Rigidbody` + collider + `LabItem` with a unique `itemId`), create a trigger-collider station with `ExperimentTaskStation` (`activateOnTriggerEnter = true`, `requiredItemId` = the prop id, `taskId` = the graph task), assign the runner. Nothing else — order, mistakes, waypoints, HUD, Pharmee all react through the runner's events.
- **Physical-process steps** (pour/heat/filter) should *not* use stations — bind the chemistry components' predicates or `LiquidTaskBinding` instead (§6).
- **Per-module station rebuild:** when swapping experiments at runtime, use `ExperimentLauncher.onModuleLoaded` to re-point stations (`SetTaskId` / `SetRequiredItemId` / `SetRunner` are the runtime setters, since edit-mode `OnEnable` doesn't re-fire).
- **Physics budget:** props are gravity-on rigidbodies after the 2026-07-08 collision pass — keep concurrent active rigidbodies within the Quest budget (≤40, kinematic-on-grab) and rest props on collidered surfaces.

---

## 6. Chemistry simulations

**Files:** `Assets/PharmaSynth/Scripts/Chemistry/` — `LiquidPhysics.cs`, `LiquidTaskBinding.cs`, `ReactionRule.cs`, `ReactionRegistry.cs`, `ChemicalData.cs`, `TemperatureSim.cs`, `GasCollection.cs`, `CrystallizationController.cs`, `FiltrationController.cs`; shader at `Assets/PharmaSynth/Art/Shaders/PharmaLiquid.shader`.

### How it works

**`ChemicalData`** (SO, menu `Chemistry/ChemicalData`; 16 authored under `ScriptableObjects/Chemicals/`) describes a reagent: identity (`chemicalName`, `state` — `PhysicalState { Liquid, Solid, Powder, Gas }`), visuals (`liquidColor`, `liquidTopColor`, `sceneColourAmount`, `viscosity`), test properties (`boilingPointC`, `precipitateColor`, `pH`, `evolvesGas`), and safety (`hazard` — `HazardType { None, Toxic, Corrosive, Flammable, Volatile }`, `requiresFumeHood`, legacy `isDangerous`).

**`ReactionRule`** (SO, menu `Chemistry/Reaction Rule`) = `inputChemicalA` + `inputChemicalB` → `resultLiquid`, optional `resultPrecipitate`/`hasPrecipitate`, a `minTemperatureC` condition (`TemperatureSatisfied(t)` helper), and the gradeable observable: `outcome` (`ReactionOutcome { None, ColorChange, Fizzing, Precipitate, Odor, GasEvolved }`), `evolvesGas`, `expectedObservation` text for the data sheet. **`ReactionRegistry`** (SO, menu `Chemistry/Reaction Registry`) holds the rule list; `FindReaction(a, b)` matches either order and is null-guarded.

**`LiquidPhysics`** (per-vessel MonoBehaviour on the `_WithLiquid` prefabs) owns volumes (`currentLiquidVolume` / `currentPptVolume` vs `maxVolume`), drives the liquid shader every frame (fill level with tilt correction, wobble springs from vessel velocity), and — crucially — **reports chemistry as events instead of hardcoding experiment logic** (the audit's critical fix #3):

- `LiquidAdded(ChemicalData chem, float amount)` — fired on **every** `AddLiquid` call, before volume/merge handling.
- `ReactionOccurred(ReactionRule rule)` — a registered rule matched: the vessel's contents become `resultLiquid` (and precipitate volume grows if `hasPrecipitate`), colors lerp via `UpdateAllVisuals`.
- `WrongReagentMixed(ChemicalData current, ChemicalData incoming)` — two different chemicals met with **no** registry rule. This is only a *report*; whether it is actually "wrong" is decided by the context-aware binding below.

`AddLiquid` also handles: overflow (ignored past `maxVolume`), waking from empty (adopts the incoming chemical), same-chemical top-ups. `PourOut(amount)` removes volume and returns the current chemical.

**`LiquidTaskBinding`** is the bridge from vessel chemistry to experiment logic — one per vessel, holding a list of `ReagentStep { ChemicalData reagent; string taskId; }`. On `LiquidAdded` it runs `HandleReagent(chem)`:

1. **Fume-hood check:** if `chem.requiresFumeHood` and the assigned `FumeHoodZone` is missing or not `IsOccupied` → `runner.RecordMistake(FumeHoodViolation, ...)`.
2. **Reagent→task lookup** (`TaskForReagent`): if *no* step in this experiment expects the reagent → `RecordMistake(WrongReagent, "Unexpected reagent: ...")`.
3. Otherwise → `runner.CompleteTask(taskId)` — which itself enforces order and auto-records `WrongStep` if poured too early.

So: **unexpected reagent = WrongReagent; right reagent at the wrong time = WrongStep; right reagent at the right time = step complete.**

**Process rigs** — all plain-core, timestep-driven, each exposing an event pair (C# `event` + inspector `UnityEvent`) *and* a `TaskGraph` auto-check predicate:

| Component | Drives | Fires | Predicate for `RegisterCondition` |
|---|---|---|---|
| `TemperatureSim` (wraps `HeatModel`) | vessel temperature; exact exponential approach `T = target + (T−target)·e^(−rate·dt)` (timestep-independent); defaults: ambient 22 °C, heatRate 0.35/s, coolRate 0.15/s, `targetC` 56, `overheatC` 120 | `ReachedTarget` / `Overheated` (each once; `IsOverheated` latches) | `AtLeast(temperatureC)` |
| `GasCollection` | 0..1 fill toward `capacityMl` (default 100); scales an optional balloon transform | `FillChanged(float)`, `Full` (once) | `Collected(fraction)` |
| `CrystallizationController` | timed liquid→crystal over `durationSeconds` (default 8) after `BeginCrystallization()`; cross-fades renderers via `_BaseColor` alpha | `Crystallized` (once) | `Crystallized01(fraction)` |
| `FiltrationController` | filtrate accumulation via `AddFiltrate(ml)` toward `targetVolumeMl` (default 100); Y-scales an optional level transform | `FilteredChanged(float)`, `Filtered` (once) | `Filtered01(fraction)` |

Each has a `Reset*()` for retry, and `Tick(dt)` is public for deterministic tests. Overheat wiring is by intent: hook `TemperatureSim.onOverheated` → `runner.RecordMistake(LabErrorType.Overheat, ...)` (the aspirin decomposition branch).

**The liquid shader** — `PharmaSynth/Liquid` (`Art/Shaders/PharmaLiquid.shader`, URP, stereo-instancing-safe). The fill plane is computed in object space along `_UpVector`; `_SceneColourAmount` is approximated as transparency rather than sampling the camera opaque texture (Quest perf). Properties (all driven per-frame by `LiquidPhysics` via cached property IDs):

`_LiquidColour`, `_TopColour` (surface lighten 0–1), `_SceneColourAmount`, `_Fill` (0–1), `_WobbleX`, `_WobbleZ`, `_UpVector` (local up), `_LocalYMin` / `_LocalYMax` (mesh-bounds fill range), `_RimPower`, `_RimStrength`.

### How to extend

- **New reagent:** author a `ChemicalData` asset; set `requiresFumeHood`/`hazard` for safety behavior and the visual colors for the shader.
- **New reaction:** author a `ReactionRule`, add it to the vessel's `ReactionRegistry`. Set `minTemperatureC` + `expectedObservation` for temperature-gated, test-gradeable reactions.
- **Wiring a new experiment's chemistry** (remaining per-experiment TODO): add a `LiquidTaskBinding` per vessel with its expected `ReagentStep`s (`AddExpected(reagent, taskId)` at runtime), and register the process predicates against the module's task ids after launch.
- **New process rig:** follow the established pattern — plain core with `Tick(dt)`, once-only completion event + `UnityEvent`, a `bool` predicate for auto-check, and a `Reset` method.

---

## 7. Safety systems

**Files:** `Assets/PharmaSynth/Scripts/Safety/PPEController.cs`, `Assets/PharmaSynth/Scripts/Safety/HazardZones.cs` (contains both `FumeHoodZone` and `HazardZone`).

### How it works

**`PPEController`** (on the PPE locker) implements the PPE gate. Poking/selecting the locker's `XRBaseInteractable` (or calling `DonPPE()` directly) sets `PPEWorn` (idempotent), **deactivates every GameObject in `labEntryBlockers`** (e.g. the locked-door collider — this is the lab-entry gate) and activates the `wornVisuals` (coat/gloves on the player). It raises `onPPEWorn` (UnityEvent, for a mirror/avatar reflection) and `PPEWornChanged` (C# event). `RemovePPE()` reverses the visuals and re-fires `PPEWornChanged` (it does not re-block entry).

**`FumeHoodZone`** is a trigger volume tracking occupancy: `OnTriggerEnter/Exit` count occupants (optionally filtered by `occupantTag`), exposing `IsOccupied`. It also offers a physics-free `Contains(worldPos)` test against its collider bounds. `LiquidTaskBinding` consults `IsOccupied` for the fume-hood violation check (§6).

**`HazardZone`** is a trigger volume for spills / hot surfaces / corrosives. On contact (optional `contactTag` filter) it calls `Report()`, which is **debounced** — `rearmSeconds` (default 2) must elapse (`Time.time - _lastReport`) before it reports again, so dwelling in a hazard logs one mistake, not one per physics tick. `Report()` calls `runner.RecordMistake(errorType, message)` with a configurable `errorType` (default `ChemicalContact`) and is public so non-physics detectors can reuse it.

### How to extend

- **Grade the PPE state:** subscribe to `PPEWornChanged` and record `MissingPPE` if the player begins an experiment without PPE (the enum + penalty path already exist).
- **New hazard:** drop a trigger collider with `HazardZone`, pick the `LabErrorType` and message, assign the runner (`SetRunner` for runtime binds).
- **Hood-scoped work:** for "must be performed inside the hood" *steps* (not just reagents), register an auto-check predicate combining the step's world condition with `fumeHood.IsOccupied` or `Contains(...)`.

---

## 8. NPC (Pharmee) and cutscenes

**Files:** `Assets/PharmaSynth/Scripts/NPC/PharmeeBrain.cs` (also defines `PharmeeState`, `PharmeeFaceExpression`, `IPharmeeFace`), `PharmeeFace.cs`, `NPCNarrationController.cs`, `CutsceneData.cs`, `CutsceneDirector.cs`. (`ModuleCutsceneController.cs` is the inherited legacy PlayableDirector path.)

### How it works

**`PharmeeBrain`** is the robot guide's state machine — states `Idle, Greeting, Instructing, Warning, Celebrating, Encouraging` — reacting to `ExperimentRunner` events with a subtitle line and a face expression (`Neutral / Happy / Warning` via the `IPharmeeFace` interface; `PharmeeFace` implements it by tinting the screen-face renderer's `_EmissionColor` — or any configured color property — per expression). All copy lives in the serialized `DialogueSet` (client-reviewable): `greeting`, `celebrate`, `encourage`, `wrongReagent`, `wrongStep`, `overheat`, `safety`.

- **On start:** greets + instructs the first step — *unless* `deferIntroToDirector` is true (see handoff below).
- **On task completed:** `InstructCurrent()` — speaks the first available task's `hint` (or the fallback `"Next: " + label`).
- **On mistake:** warns with the line matched to the `LabErrorType` (`wrongReagent` / `wrongStep` / `overheat`; everything else uses the `safety` line) and the Warning face.
- **On finished:** celebrates if `result.passed`, else encourages — again skipped when deferred to the director.

Lines are delivered through **`NPCNarrationController.Say(subtitle, seconds = 3, clip = null)`** — the reactive single-line API: it interrupts the current line, shows the subtitle + skip button, waits the clip length (if audio) or `seconds` (min 0.1), then clears. The controller also has the legacy scripted-sequence path (`tutorialLines` + `PlayTutorialNarration`) and `SkipNarration()` → `onNarrationFinished`.

**`CutsceneData`** (SO, menu `PharmaSynth/Cutscene`) is a VR-safe, data-driven cutscene: `kind` (`Intro / ReagentPrep / Success / Failure`), a `title`, and a list of `Beat { subtitle, seconds (min 0.2, default 3), face }`. **No XR-camera animation, ever** (comfort rule) — narrative is subtitles + Pharmee staging. Four Methane cutscenes exist under `ScriptableObjects/Cutscenes/` (`Methane_Intro/ReagentPrep/Success/Failure`); every other experiment still needs its four.

**`CutsceneDirector`** subscribes to the runner and plays the right cutscene at the right beat:

- `ExperimentStarted` → `intro`
- `PhaseCompleted(ReagentPrep)` → `reagentPrep` (only that phase triggers a mid-run cutscene)
- `ExperimentFinished` → `SelectOutro(result)` = `success` if passed else `failure` — **the end cutscene always plays** (user requirement; both variants must be assigned).

Beats run in a coroutine (`Say` + face per beat), bracketed by `onCutsceneStarted` / `onCutsceneFinished` UnityEvents and interruptible via `Skip()`. **The handoff pattern:** the scene sets `PharmeeBrain.deferIntroToDirector = true` and wires `director.onCutsceneFinished → brain.InstructCurrent`, so the intro cutscene speaks first and the brain takes over step-by-step guidance the moment it ends — no subtitle conflict.

### How to extend

- **Per-experiment cutscenes:** author 4 `CutsceneData` assets per module and swap them onto the director when launching (an `onModuleLoaded` listener is the natural place). The director's fields are currently single-module serialized references.
- **New dialogue reactions:** extend `DialogueSet` + the `WarnLineFor` switch; audio voice (Pharmee beeps) plugs into `NarrationLine.voiceClip` / `Say`'s `clip` parameter — the `AudioSource` hook already exists.
- **A richer face:** implement `IPharmeeFace` on any component (animator-driven, texture-swapping) and point `faceBehaviour` at it — both the brain and the director drive it through the interface.
- **Dr. Jimenez (examiner, roadmap item 5):** follow the PharmeeBrain pattern — subscribe to the same runner events but *observe without hints* (assessment mode).

---

## 9. Progression, save data, and game flow

**Files:** `Assets/PharmaSynth/Scripts/Progression/ProgressionService.cs`, `ExperimentCatalog.cs`, `ProgressionFlow.cs`, `ExperimentLibrary.cs`, `GameFlow.cs`; `Assets/PharmaSynth/Scripts/Interaction/ExperimentLauncher.cs`; `Assets/PharmaSynth/Scripts/UI/MainMenuController.cs`. Scenes: `Assets/Scenes/MainMenu.unity` and `Assets/Scenes/SampleScene.unity` (the lab).

### How it works

**`ProgressionService`** (plain C#, injectable save path for tests) persists per-module bests as **versioned JSON**: `ProgressSaveData { version = 1, List<ModuleRecord> }` where `ModuleRecord = { moduleId, bestGrade (0..100), bestMastery (0..1), passed, attempts }`. Default file: `Application.persistentDataPath/pharmasynth_progress.json`, with a **`.bak` backup slot**: `Save()` copies the previous good file to `.bak` before overwriting; `Load()` tries primary → backup (logs a recovery warning) → fresh empty data. `RecordResult(moduleId, ExperimentResult)` bumps `attempts`, keeps the best grade/mastery, **latches `passed` true once earned**, and auto-saves. Gate queries: `IsPassed(id)` and `IsUnlocked(id, prerequisiteId)` (no prerequisite = always unlocked).

**`ExperimentCatalog`** (static, plain data) is the ordered 11-experiment roster and the **single source of truth** for ids, titles, periods, and the linear prerequisite chain:

`tutorial-methane` → `prelim-chemical-compounding` → `prelim-ethyl-alcohol` → `midterm-benzoic-acid` → `midterm-acetanilide` → `midterm-acetone` → `midterm-chloroform` → `final-benzamide` → `final-aspirin` → `final-caffeine` → `final-winemaking`

Periods (`ExperimentPeriod`): `Tutorial` (1), `Prelim` (2), `Midterm` (4), `Final` (4). Each `CatalogEntry` also records the asset file name under `ScriptableObjects/Experiments/` and its build tier (1–3, informational). Helpers: `Get(id)`, `PrerequisiteOf(id)`, `InPeriod(period)`, `Count`.

**`ProgressionFlow`** is the read-only query layer over a service, answering the menu/hub questions: `IsUnlocked(id)` (unknown ids are treated as **locked**), `NextExperiment()` (first roster entry that is unlocked but not passed; null when all passed), `IsPeriodComplete(period)` (every experiment in it passed; an empty period is *not* complete), `IsPeriodUnlocked(period)` (all earlier periods complete; Tutorial always open — the period-door mechanic), `PassedCount()`, `OverallCompletion01()`, `AllComplete()`.

**Runtime module loading:**

- **`ExperimentLibrary`** (SO asset at `ScriptableObjects/ExperimentLibrary.asset`, menu `PharmaSynth/Experiment Library`) holds direct serialized references to all 11 `ExperimentModuleDefinition` assets — build-safe (no Resources/AssetDatabase). `Get(moduleId)` resolves by the module's own `moduleId` field.
- **`GameFlow`** (static) is the deliberately tiny cross-scene handoff: `SelectedModuleId` (default `"tutorial-methane"`) + `Select(id)`. Menus write it; the lab reads it.
- **`ExperimentLauncher`** (in the lab scene): `Launch(moduleId)` → `library.Get` → `runner.SetModule(mod)` → fires `onModuleLoaded(mod)` (**the hook where scene wiring — stations, props, bindings, cutscenes — rebuilds for the new module**) → `runner.StartExperiment()`. With `launchSelectedOnStart` it auto-launches `GameFlow.SelectedModuleId` on scene load.

**`MainMenuController`** (MainMenu scene) drives the four buttons: **Tutorial** selects `tutorial-methane` and loads the lab scene (`labSceneName`, default `"SampleScene"`); **Laboratory** loads the save, computes `ResolveLabTarget(flow, fallback)` (the player's `NextExperiment()`, or the tutorial if none), selects it, and loads the lab; **Settings** toggles the panel; **Quit** exits (and stops play mode in-editor).

### How to extend

- **Recording results:** wire `runner.ExperimentFinished` → `progressionService.RecordResult(module.moduleId, result)` wherever the grade screen's Continue flow lives — the service and gate queries are ready.
- **Adding a 12th experiment:** author the module asset, add it to `ExperimentLibrary.asset`, and append a `CatalogEntry` (id must equal the asset's `moduleId`; set its prerequisite). Everything else — unlock chain, period doors, completion % — follows automatically.
- **Save migrations:** bump `ProgressionService.CurrentVersion` and handle old versions in `TryLoadFrom` (the marked migration hook).
- **Period hub scene (roadmap item 8):** drive the three doors from `ProgressionFlow.IsPeriodUnlocked` and the experiment-select list from `IsUnlocked`/`IsPassed` per catalog entry.

---

## 10. UI systems

**Files:** `Assets/PharmaSynth/Scripts/UI/ExperimentHudController.cs`, `TabletChecklistController.cs`, `GradeScreenController.cs`, `WristWatchController.cs`, `FaceCamera.cs`. All world-space canvases in the lab scene (`LabHUD`, `LabTablet`, `GradeScreen`); all controllers subscribe to `ExperimentRunner` events (no polling except the once-per-second timer). The string/format builders are pure statics, unit-tested.

### How it works

**HUD (`ExperimentHudController`):** shows module title, a count-up **HH:MM:SS** timer (`FormatTime`, updated only when the whole second changes), a progress bar (`Image` with type Filled), and toasts. Task completions toast `"<label>  ✓"`; mistakes toast the mistake message *and dip the bar*: the displayed value is `DisplayedProgress(graphProgress, mistakes, penaltyStep)` = `clamp01(progress − mistakes × 0.05)` by default (§4). Toasts auto-hide after `toastSeconds` (2.5).

**Tablet checklist (`TabletChecklistController`):** renders the whole procedure into one TMP text via the pure static `BuildChecklistText(graph)` — tasks grouped by phase with bold phase headers (`PhaseLabel`: "Reagent Preparation", "Synthesis", "Chemical Tests", "Data Sheet") and per-task marks: `☑` complete, `▶` current (available), `☐` pending. Rebuilds on start/task/progress events. A `balancedReaction` string renders in the reaction footer.

**Grade screen (`GradeScreenController`):** auto-shows on `ExperimentFinished` (root is hidden on enable). Displays rounded grade %, mistake count, time, **PASSED / TRY AGAIN** from the two-part gate, and `BuildBreakdown(result)`: all six per-criteria contributions + `Mastery: N%` + — on failure — red-tinted gate reasons ("Grade below the pass mark." / "Mastery not yet demonstrated — keep practicing."). `passedVisuals`/`failedVisuals` toggle accordingly, and **the Continue button only activates when passed** (the progression gate made visible). `OnRetryPressed`/`OnContinuePressed` hide the screen and fire the `onRetry`/`onContinue` UnityEvents (Retry is wired to `ExperimentStarter.Begin`).

**Wrist watch (`WristWatchController`)** — the headline wrist-flip checklist:

- **Supination gesture:** `Vector3.Dot(watchAnchor.up, Vector3.up)` with **hysteresis** — show at ≥ `supinationShow` (0.6), hide below `supinationHide` (0.4) — preventing flicker at the boundary. Works for both controller supination and hand-tracking palm-up since it reads the anchor transform.
- **Gaze check** (`requireGaze`, default true): the panel additionally requires `IsGazingAt(headPos, headForward, wristPos, gazeThreshold = 0.5)` — head looking roughly toward the wrist.
- **Button fallback:** an `InputActionReference` (`toggleAction`) toggles `_manualVisible`, so the feature works without the gesture (and pre-HMD). The panel shows when *either* path is active.
- Content: `BuildSummary(runner)` = current step label, `Progress N%`, `Mastery N%` — refreshed while visible.

**`FaceCamera`:** a `LateUpdate` billboard that rotates world-space labels/canvases so their **+Z points away from the viewer** (UGUI/TMP readable orientation), with `yAxisOnly` (default true) keeping signs upright and `yawOffset` for assets whose readable side isn't −Z. Caches `Camera.main` lazily.

### How to extend

- **Restyle safely:** the logic/format layer is pure statics (`DisplayedProgress`, `FormatTime`, `FormatPercent`, `BuildChecklistText`, `BuildBreakdown`, `BuildSummary`) — swap the canvases/prefabs without touching tested logic; the tablet doc even notes a per-item-prefab checklist can layer on later.
- **Handedness setting:** the watch is right-hand-by-default per the plan — expose `watchAnchor` re-parenting in the Settings menu (roadmap item 8).
- **New HUD widgets:** subscribe to the runner's events (`ExperimentStarted`, `TaskCompleted`, `PhaseCompleted`, `ProgressChanged`, `MistakeRecorded`, `ExperimentFinished`) rather than polling; use `SetRunner`-style bind methods for anything that must work in edit-mode tests (scene `OnEnable` doesn't fire on `AddComponent` there).

---

## 11. System event map (who listens to what)

`ExperimentRunner` is the hub. Its C# events and their consumers:

| Runner event | Consumers |
|---|---|
| `ExperimentStarted(module)` | HUD (reset title/timer), Tablet (rebuild), PharmeeBrain (greet, unless deferred), CutsceneDirector (intro) |
| `TaskCompleted(task)` | HUD (toast), Tablet (rebuild), PharmeeBrain (instruct next); runner itself observes BKT positive evidence |
| `PhaseCompleted(phase)` | CutsceneDirector (ReagentPrep cutscene) |
| `ProgressChanged(float)` | HUD (bar), Tablet (rebuild) |
| `MistakeRecorded(type, msg)` | HUD (dip + toast), PharmeeBrain (warning line + face) |
| `ExperimentFinished(result)` | GradeScreen (auto-show), CutsceneDirector (success/failure outro — always), PharmeeBrain (celebrate/encourage, unless deferred) |

Inputs into the runner: `ExperimentTaskStation.Activate` / `LiquidTaskBinding.HandleReagent` / auto-check predicates via `RegisterCondition` + `Tick` → `CompleteTask`; `HazardZone.Report` / `LiquidTaskBinding` → `RecordMistake`; `ExperimentStarter.Begin` / `ExperimentLauncher.Launch` → `StartExperiment`; `DevExperimentDriver` (keyboard) → all of the above.

When adding any new mechanic, plug into this hub: **report world events to the runner; react to the runner's events** — never wire system-to-system directly, and the scoring, mastery, guidance, and narrative layers stay consistent for free.
