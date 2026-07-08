# PharmaSynth — Content Authoring Cookbook

This is the step-by-step guide for adding or altering **game content without touching the engine**: experiment modules, reagents, reaction rules, in-scene hands-on wiring, cutscenes, and dialogue. The engine (TaskGraph, BKT mastery, rubric grading, progression gate, NPC brain, cutscene director) is feature-complete and locked behind the regression suite (`Tools ▸ PharmaSynth ▸ Run Self-Tests`, 157/157 green as of 2026-07-08) — content is DATA (ScriptableObjects) plus scene wiring, with exactly one small code touch per new experiment (a `CatalogEntry`). Follow each section's checklist and finish every content change with the verification pass in [§7](#7-verification-duty).

## Table of contents

1. [Authoring a new experiment module](#1-authoring-a-new-experiment-module)
2. [Authoring reagents and reaction rules](#2-authoring-reagents-and-reaction-rules)
3. [Wiring a hands-on experiment in the lab scene](#3-wiring-a-hands-on-experiment-in-the-lab-scene-the-methane-pattern)
4. [Authoring cutscenes](#4-authoring-cutscenes)
5. [Authoring dialogue and hints](#5-authoring-dialogue-and-hints)
6. [Chemistry accuracy rules](#6-chemistry-accuracy-rules)
7. [Verification duty](#7-verification-duty)
8. [Appendix: enum index reference](#8-appendix-enum-index-reference)

---

## 1. Authoring a new experiment module

An experiment is one `ExperimentModuleDefinition` asset (class: `Assets/PharmaSynth/Scripts/Experiment/ExperimentModuleDefinition.cs`). All 11 roster experiments already exist under `Assets/PharmaSynth/ScriptableObjects/Experiments/`. Create new ones via **Assets ▸ Create ▸ VR ChemLab ▸ Experiment Module**.

### 1.1 Fields (v2 data model)

| Field | Type | Meaning |
|---|---|---|
| `moduleId` | string | Stable kebab-case id, e.g. `tutorial-methane`. Must match the `CatalogEntry` (§1.4) and is the key used by `ExperimentLibrary.Get()` / `ExperimentLauncher.Launch()`. |
| `moduleTitle` | string | Display title, e.g. `Tutorial: Methane Synthesis`. |
| `intendedLearningOutcomes` | List\<string\> | ILOs shown to the player/client; plain sentences. |
| `tasks` | List\<ExperimentTaskDefinition\> | **Legacy** — only the inherited `ExperimentFlowManager` reads it. Leave empty (`[]`) for new content. |
| `graphTasks` | List\<ExperimentTask\> | **The procedure.** See §1.2. |
| `masteryThreshold` | float 0–1 | Two-part gate (manuscript = **0.90**): pass requires grade `Total >= masteryThreshold*100` **and** BKT `OverallMastery() >= masteryThreshold`. |
| `bkt` | `BktParameters` | `pL0` (default 0.25), `pTransit` (0.20), `pSlip` (0.10), `pGuess` (0.20). |
| `trackedSkills` | List\<LabSkill\> | **Leave empty to auto-derive** — `BuildMasteryModel()` collects the distinct `skill` values used by `graphTasks`. Only set it to track extra skills the tasks don't reference. |
| `rubricWeights` | `RubricWeights` | `procedure` 0.40 / `chemicalTests` 0.20 / `materialsAndPPE` 0.15 / `timeManagement` 0.10 / `sanitation` 0.10 / `documentation` 0.05 by default. Weights are **auto-normalized** by `ScoreCalculator`, so they need not sum to 1 (this deliberately absorbs the manual's inconsistent printed weights). |
| `parTimeSeconds` | float | Par time for the TimeManagement criterion (`ScoreCalculator.TimeSubScore`: ≤ par → 1.0, 2× par → 0.0). Methane uses 300. |

### 1.2 `ExperimentTask` (one `graphTasks` entry)

Defined in `Assets/PharmaSynth/Scripts/Experiment/TaskGraphModel.cs`:

| Field | Type | Meaning |
|---|---|---|
| `taskId` | string | Stable kebab-case id, unique within the module (e.g. `heat-mixture`). Referenced by stations, bindings, prerequisites, and `RegisterCondition`. |
| `label` | string | Checklist/HUD text. |
| `phase` | `TaskPhase` | `ReagentPrep`, `Synthesis`, `ChemicalTests`, `DataSheet`. Phase completion fires events (the ReagentPrep cutscene hangs off this). |
| `prerequisites` | List\<string\> | taskIds that must be complete first. Completing out of order returns `BlockedByPrerequisite` → the runner auto-records a **WrongStep** mistake. No cycles — the self-test proves every graph is solvable. |
| `progressWeight` | float ≥ 0 | Relative contribution to `Progress01` (completed weight / total weight). |
| `skill` | `LabSkill` | `Measuring`, `Heating`, `Filtration`, `Transfer`, `Safety`, `TestInterpretation` — which BKT competency a correct completion (or related mistake) updates. |
| `rubricCategory` | `RubricCategory` | `Procedure`, `ChemicalTests`, `MaterialsAndPPE`, `TimeManagement`, `Sanitation`, `Documentation` — where this task's outcome scores. |
| `required` | bool | Required tasks gate phase/module completion; optional tasks only add score. |
| `hint` | string | **This is Pharmee's spoken instruction for the step** (see §5). Write it as a directive sentence. |

### 1.3 Worked example (what the serialized asset looks like)

This is the real shape of a task inside a module asset (from `Tutorial_Methane.asset`; enums serialize as ints — see [§8](#8-appendix-enum-index-reference)):

```yaml
moduleId: tutorial-methane
moduleTitle: 'Tutorial: Methane Synthesis'
intendedLearningOutcomes:
- Assemble a gas-generation apparatus safely
- Collect a gas over water and confirm it by combustion
tasks: []                      # legacy list — always empty for v2 content
graphTasks:
- taskId: heat-mixture
  label: Heat the mixture with the burner
  phase: 1                     # Synthesis
  prerequisites:
  - setup-apparatus            # enforced order; skipping = WrongStep mistake
  progressWeight: 1
  skill: 1                     # LabSkill.Heating
  rubricCategory: 0            # RubricCategory.Procedure
  required: 1
  hint: Apply gentle then strong heat; watch for gas bubbling through the trough.
masteryThreshold: 0.9
bkt: {pL0: 0.25, pTransit: 0.2, pSlip: 0.1, pGuess: 0.2}
trackedSkills: []              # empty = auto-derived from graphTasks
rubricWeights: {procedure: 0.4, chemicalTests: 0.2, materialsAndPPE: 0.15,
                timeManagement: 0.1, sanitation: 0.1, documentation: 0.05}
parTimeSeconds: 300
```

Prefer editing in the Inspector; the enum popups spare you the int mapping.

### 1.4 New-experiment checklist

1. **Create the asset** under `Assets/PharmaSynth/ScriptableObjects/Experiments/` (menu: Assets ▸ Create ▸ VR ChemLab ▸ Experiment Module). File name convention: `<Period>_<Name>.asset` (e.g. `Midterm_BenzoicAcid`).
2. **Add it to `Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset`** (`modules` list). This is what makes it loadable at runtime by `moduleId` via `ExperimentLauncher` — no Resources folder, no AssetDatabase in builds.
3. **Add a `CatalogEntry` to `Assets/PharmaSynth/Scripts/Progression/ExperimentCatalog.cs`** — the ONE code touch. Constructor: `new CatalogEntry(moduleId, assetName, title, period, prerequisiteModuleId, tier)`. `assetName` = the asset file name without extension; `prerequisiteModuleId` = the previous experiment in the linear chain (`null` only for the tutorial). The prerequisite must appear **earlier** in the roster list — the self-test asserts the chain is ordered.
4. **Extend the self-test `ContentSuite`** in `Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.cs`: add `("<AssetName>", <taskCount>)` to the tuple array in `ContentSuite()`. If the roster size changes from 11, also update `ProgressionFlowSuite` ("catalog: 11 entries" + the 1/2/4/4 period split) and `LibrarySuite` ("library: 11 modules").
5. **Run the suite** (§7). It will verify: asset loads, task count matches, graph is solvable to 100% respecting prerequisites (no unreachable steps/cycles), every used phase completes, and the mastery/score spines build.

> Note: all 11 contracted experiments are already authored as data. The remaining per-experiment TODO is scene/content wiring: reaction rules (§2), per-vessel `LiquidTaskBinding` + grabbable-prop stations (§3), and the 4 `CutsceneData` assets (§4 — Methane only so far).

---

## 2. Authoring reagents and reaction rules

### 2.1 `ChemicalData` (a reagent)

Class: `Assets/PharmaSynth/Scripts/Chemistry/ChemicalData.cs`. Assets live in `Assets/PharmaSynth/ScriptableObjects/Chemicals/` named `Chem_<Name>.asset` (16 exist). Create via **Assets ▸ Create ▸ Chemistry ▸ ChemicalData**.

| Field | Type | Meaning |
|---|---|---|
| `chemicalName` | string | Display/log name (`"Soda Lime"`). Used in mistake messages. |
| `state` | `PhysicalState` | `Liquid`, `Solid`, `Powder`, `Gas`. |
| `liquidColor` / `liquidTopColor` | Color (HDR) | Drives the `PharmaSynth/Liquid` shader fill/surface tint. |
| `sceneColourAmount` | float 0–1 | Transparency/scene-color blend for the liquid shader. |
| `viscosity` | float 0–1 | Pour behavior. |
| `boilingPointC` | float | Distillation cut-offs (acetone 56, ethanol ~78) — pair with `TemperatureSim.AtLeast()`. |
| `precipitateColor` | Color (HDR) | Color the chemical shows in a positive test. |
| `pH` | float 0–14 | For litmus/pH test steps. |
| `evolvesGas` | bool | Whether this chemical evolves gas. |
| `hazard` | `HazardType` | `None`, `Toxic`, `Corrosive`, `Flammable`, `Volatile` — drives spill/contact feedback. |
| `requiresFumeHood` | bool | If true, handling it while the player is **not** inside a `FumeHoodZone` records a `FumeHoodViolation` (enforced by `LiquidTaskBinding.HandleReagent`). |
| `isDangerous` | bool | Legacy flag kept for the inherited spill logic — set it to match `hazard != None`. |

### 2.2 `ReactionRule`

Class: `Assets/PharmaSynth/Scripts/Chemistry/ReactionRule.cs`. Create via **Assets ▸ Create ▸ Chemistry ▸ Reaction Rule**.

| Field | Type | Meaning |
|---|---|---|
| `inputChemicalA` / `inputChemicalB` | `ChemicalData` | Reactants — matching is symmetric (A+B or B+A). |
| `resultLiquid` | `ChemicalData` | The resulting liquid. |
| `resultPrecipitate` / `hasPrecipitate` | `ChemicalData` / bool | Optional solid product. |
| `minTemperatureC` | float | Minimum temperature for the reaction; 0 = no heat needed. Check with `rule.TemperatureSatisfied(tempSim.CurrentC)`. |
| `outcome` | `ReactionOutcome` | `None`, `ColorChange`, `Fizzing`, `Precipitate`, `Odor`, `GasEvolved` — the gradeable signal a chemical test checks for. |
| `evolvesGas` | bool | Feed evolved gas into a `GasCollection`. |
| `expectedObservation` | string | Data-sheet text, e.g. `"brisk effervescence"` — what the player should record. |

### 2.3 Register in a `ReactionRegistry`

Class: `Assets/PharmaSynth/Scripts/Chemistry/ReactionRegistry.cs` (create via **Assets ▸ Create ▸ Chemistry ▸ Reaction Registry**; default asset name `MasterRegistry`). Add every rule to its `rules` list — `FindReaction(a, b)` is a linear symmetric lookup and null-guarded. A vessel's `LiquidPhysics` resolves mixes through the registry and raises `LiquidAdded` / `ReactionOccurred` / `WrongReagentMixed`; **the registry only decides what chemically happens — right/wrong-for-this-step is decided by `LiquidTaskBinding` (§3.3), never by "no rule found".**

---

## 3. Wiring a hands-on experiment in the lab scene (the Methane pattern)

The lab is `Assets/Scenes/SampleScene.unity`. `ExperimentSystems` holds the `ExperimentRunner` (+ `ExperimentStarter`, `WaypointGuide`, `DevExperimentDriver`). The pattern below is live for Methane and is the template for the other 10.

### 3.1 Task stations (zone pads)

Component: `ExperimentTaskStation` (`Assets/PharmaSynth/Scripts/Interaction/ExperimentTaskStation.cs`), one per procedure step, placed at the apparatus where that step happens:

- `runner` → the scene `ExperimentRunner`; `taskId` → the graph task it completes.
- **Hands-on trigger mode:** add a **trigger collider**, set `activateOnTriggerEnter = true`, and set `requiredItemId` — only a grabbable prop whose `LabItem.itemId` matches completes the station (`AcceptsItem()`); a wrong prop is silently ignored (not a mistake), while a **correct prop at the wrong time** goes through `runner.CompleteTask()` and records **WrongStep**.
- `activateOnSelect = true` instead hooks an `XRBaseInteractable`'s `selectEntered` (the old poke-station mode). `requiredTag` optionally filters trigger colliders (e.g. only the hand).
- Stations self-register in `ExperimentStationRegistry` by `taskId`, which is what `WaypointGuide` uses to point the marker at the current step.

### 3.2 Grabbable props

Each prop = mesh + collider + `Rigidbody` + `XRGrabInteractable` + `LabItem` (`itemId`, `displayName`). Methane's five (under `MethaneProps` on the `ReagentBench`, front-right of the lab):

| `LabItem.itemId` | Station `taskId` |
|---|---|
| `reagent-jar` | `prepare-mixture` |
| `glass-tube` | `setup-apparatus` |
| `burner` | `heat-mixture` |
| `collection-tube` | `collect-gas` |
| `lit-splint` | `test-gas` |

Physics budget (Quest 3): rest props on collidered surfaces and keep concurrent active rigidbodies **≤ 40** (kinematic-on-grab). All 42 `ChemLabEquipment` prefabs are already grab-enabled.

### 3.3 Pour steps — `LiquidTaskBinding` per vessel

For steps that mean "add reagent X to vessel Y", put a `LiquidTaskBinding` (`Assets/PharmaSynth/Scripts/Chemistry/LiquidTaskBinding.cs`) on the vessel:

- `vessel` → the vessel's `LiquidPhysics`; `runner` → the runner; `fumeHood` → the scene `FumeHoodZone` (if any reagent needs it).
- `expectedReagents` → a list of `ReagentStep { reagent (ChemicalData), taskId }`.
- Behavior (context-aware, per `HandleReagent`): an expected reagent completes its task (prerequisites enforce order → out-of-order = **WrongStep**); a reagent **no step expects** = **WrongReagent**; a `requiresFumeHood` reagent handled while the hood is unoccupied = **FumeHoodViolation**.

### 3.4 World-state auto-checks — `RegisterCondition`

For steps completed by physics/simulation rather than placement, bind a predicate: `runner.RegisterCondition(taskId, () => predicate)`. The runner's `Update()` calls `graph.Tick()` every frame and auto-completes an **available** task whose condition is true. Built-in predicates:

| System | Predicate | Example |
|---|---|---|
| `TemperatureSim` | `AtLeast(tempC)` | distillation cut-off `t.AtLeast(56f)`; also `onOverheated` → `runner.RecordMistake(LabErrorType.Overheat, …)` for the aspirin branch |
| `GasCollection` | `Collected(fraction)` | `g.Collected(0.9f)` for collect-gas |
| `CrystallizationController` | `Crystallized01(fraction)` | ice-bath crystallization done |
| `FiltrationController` | `Filtered01(fraction)` | Büchner filtration done |

**Gotcha:** the TaskGraph is rebuilt on every `StartExperiment()` / `Retry()`, so register conditions from a handler subscribed to `runner.ExperimentStarted` (not once in `Awake`), or they are lost on retry.

### 3.5 Labels

World-space labels live under the scene's `WorldLabels` group. Give each label/canvas a `FaceCamera` component (`Assets/PharmaSynth/Scripts/UI/FaceCamera.cs`): `yAxisOnly = true` keeps signs upright; `yawOffset` corrects assets whose readable side is not −Z. Text is readable from any side, never mirrored.

### 3.6 Testing the wiring

Press Play in `SampleScene`: grab a prop and carry it into its matching zone (order enforced). Keyboard fallback via `DevExperimentDriver` on `ExperimentSystems`: **B** = begin, **1–5** = complete step, **F** = finish, **R** = retry. MCP-driven play mode is unreliable on this PC — a human presses Play.

---

## 4. Authoring cutscenes

### 4.1 `CutsceneData`

Class: `Assets/PharmaSynth/Scripts/NPC/CutsceneData.cs`; create via **Assets ▸ Create ▸ PharmaSynth ▸ Cutscene**; assets in `Assets/PharmaSynth/ScriptableObjects/Cutscenes/` named `<Experiment>_<Kind>.asset` (only Methane's 4 exist so far).

- `kind` — `Intro`, `ReagentPrep`, `Success`, `Failure`.
- `title` — short scene title.
- `beats` — ordered list of `Beat { subtitle, seconds (min 0.2, default 3), face }`, where `face` is a `PharmeeFaceExpression` (`Neutral`, `Happy`, `Warning`). Each beat shows the subtitle for `seconds` via `NPCNarrationController.Say()` and sets Pharmee's face.

**Every experiment needs all four.** The Failure variant is not optional: the end cutscene ALWAYS plays (success OR failure) — a user requirement enforced by `CutsceneDirector.SelectOutro`.

### 4.2 `CutsceneDirector` wiring

On `RobotNPC`: assign `runner`, `narration` (the `NPCNarrationController`), `faceBehaviour` (the `PharmeeFace`, an `IPharmeeFace`), and the four cutscene slots (`intro` / `reagentPrep` / `success` / `failure`). It subscribes to the runner and plays: **Intro** on `ExperimentStarted`, **ReagentPrep** on `PhaseCompleted(TaskPhase.ReagentPrep)`, **Success/Failure** on `ExperimentFinished` by `result.passed`. `Skip()` stops the routine and the narration. Keep `PharmeeBrain.deferIntroToDirector = true` and wire `onCutsceneFinished → PharmeeBrain.InstructCurrent` so brain and director never talk over each other.

### 4.3 VR rule

**NEVER animate the XR camera.** No Cinemachine in the project (deliberate). Cutscenes are subtitles + Pharmee face/staging + optional fades — the player's head stays the player's head. Writing beats = writing client-reviewable copy; keep each subtitle one readable sentence, 3–5 s.

---

## 5. Authoring dialogue and hints

### 5.1 Step instructions come from the task `hint`

`PharmeeBrain.InstructCurrent()` speaks `InstructionFor(task)` for the first currently-available task: **the task's `hint`, or `"Next: " + label` if the hint is empty.** So writing good `hint` text in `graphTasks` (§1.2) IS authoring Pharmee's guidance — no separate dialogue file. Hints re-fire after every completed task (and after the intro cutscene finishes).

### 5.2 Reaction lines per error type

`PharmeeBrain` (`Assets/PharmaSynth/Scripts/NPC/PharmeeBrain.cs`) holds a serialized `DialogueSet` — edit the lines in the Inspector on `RobotNPC`:

| Line | Spoken when |
|---|---|
| `greeting` | experiment start (only if `deferIntroToDirector` is false) |
| `celebrate` / `encourage` | finish, passed / failed (suppressed when the director's outro carries it) |
| `wrongReagent` | `LabErrorType.WrongReagent` |
| `wrongStep` | `LabErrorType.WrongStep` |
| `overheat` | `LabErrorType.Overheat` |
| `safety` | **all other** error types (FireSafety, MissingPPE, FumeHoodViolation, ChemicalContact, HazardousAction, DroppedGlassware) |

Every mistake also sets the `Warning` face. Line duration = `lineSeconds` (default 3.5 s). Delivery is `NPCNarrationController.Say(subtitle, seconds, clip)` — the optional `AudioClip` is where Pharmee's robotic beeps attach later.

---

## 6. Chemistry accuracy rules

1. **Single source of truth: the manuscript's Appendix C** (the WCC lab manual — procedures, reagent lists, weights, rubric). Reachable via the Google-Doc export (`…/export?format=txt`); see `Docs/Documentations/gdocs_link_for_the_manuscript`.
2. **NEVER copy chemistry or label text from the storyboard/cutscene PDFs** (`Docs/handoff_assets/`). Their labels are garbled AI text and some pages carry copy-paste chemistry errors. They are staging references to exceed, not sources. Re-author clean reagent-label textures from Appendix C names.
3. **Current client flags** (already reconciled in the plan §3.3; do not silently change them back):
   - **Benzoic acid = benzaldehyde + 0.1% KMnO₄ oxidation** (per Appendix C's reagent list: benzaldehyde / 0.1% KMnO₄ / 6N HCl / propyl alcohol) — this supersedes the plan's tentative "toluene + KMnO₄" route.
   - **Acetanilide acylating agent = acetyl chloride per the manual**; acetic anhydride is the safer alternative — flagged for client sign-off before final content lock.
   - **Benzamide nitrous-acid test uses sodium nitrite** — the manual's "sodium nitrate" is a typo.
4. Any new discrepancy between manual, storyboard, and plan: implement the manual's chemistry, note the conflict in the plan file, and flag it to the client — never pick silently.

---

## 7. Verification duty

Every content change ends with this pass — no exceptions:

1. **Extend `ContentSuite`** in `Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.cs` for any new/edited module: the tuple array maps asset name → expected task count (currently `Tutorial_Methane` 5, `Prelim_ChemicalCompounding` 6, `Prelim_EthylAlcohol` 7, `Midterm_BenzoicAcid` 9, `Final_Aspirin` 7, `Midterm_Acetanilide` 10, `Midterm_Acetone` 10, `Midterm_Chloroform` 10, `Final_Benzamide` 9, `Final_Caffeine` 9, `Final_WineMaking` 8). The suite auto-verifies loadability, task count, prerequisite solvability to 100%, per-phase completion, and that the mastery/score builders work.
2. **Run the suite:** menu **Tools ▸ PharmaSynth ▸ Run Self-Tests**. Expected output: `PharmaSynth Self-Tests: N/N passed — ALL GREEN` (157/157 at time of writing). Any red = fix before moving on.
3. **Keep the console zero-error.** A clean console after a scene save + suite run is the project's standing acceptance gate.
4. For scene wiring changes, also do the **human play-test** (§3.6) — grab-path plus keyboard fallback — since MCP-driven play mode is unreliable on this machine.
5. Commit **only when asked** (work branch: `main`; `feature/asset-intake` is a stale stub at the planning commit — do not use it).

---

## 8. Appendix: enum index reference

Only needed when reading/editing `.asset` YAML by hand (the Inspector shows names):

| Enum | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| `TaskPhase` | ReagentPrep | Synthesis | ChemicalTests | DataSheet | | |
| `LabSkill` | Measuring | Heating | Filtration | Transfer | Safety | TestInterpretation |
| `RubricCategory` | Procedure | ChemicalTests | MaterialsAndPPE | TimeManagement | Sanitation | Documentation |
| `PhysicalState` | Liquid | Solid | Powder | Gas | | |
| `HazardType` | None | Toxic | Corrosive | Flammable | Volatile | |
| `ReactionOutcome` | None | ColorChange | Fizzing | Precipitate | Odor | GasEvolved |
| `CutsceneData.Kind` | Intro | ReagentPrep | Success | Failure | | |
| `PharmeeFaceExpression` | Neutral | Happy | Warning | | | |
| `ExperimentPeriod` | Tutorial | Prelim | Midterm | Final | | |
