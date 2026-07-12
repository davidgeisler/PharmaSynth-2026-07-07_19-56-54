# CLAUDE.md — PharmaSynth (VR / Meta Quest 3)

Read this first each session. It tells you what the game IS, how it flows, and **which doc to open for which work** — the details live in `Docs/`.

## Project summary
- **PharmaSynth: "Gear Up, Synth It Up!"** — a first-person guided VR chemistry-lab education game for **Meta Quest 3**. Unity **6000.5.2f1**, URP 17.5, OpenXR 1.17.1 + XRI 3.5.1, Input System. Client handoff (audit-and-continue). **Hard contract deadline: 2026-08-31.**
- **Chemistry authority = the client manuscript, Appendix C** (`Docs/Documentations/manuscript.pdf` → extract via `pdftotext -layout`; the Read tool can't open PDFs here). The storyboard is a reference to EXCEED, never a chemistry source. All known manuscript deviations + client flags: `Docs/experiments-reference.md` header table.
- Everything runtime lives under `Assets/PharmaSynth/Scripts/` (global namespace); experiments are **DATA** (ScriptableObjects), not scenes. Master plan (design spec): `C:\Users\MSI\.claude\plans\you-are-the-best-cozy-possum.md`.

## ⭐ SESSION START (first 5 minutes)
1. `git log --oneline -3` on **main** (the user commits checkpoints themselves; `feature/asset-intake` is a dead stub — never use it).
2. Menu **Tools ▸ PharmaSynth ▸ Run Self-Tests** → expect **969/969 ALL GREEN** (+3 deliberate warnings: two W5.9 guard tests + the Unknown-moduleId negative test) + zero-error console. **EDIT MODE ONLY** — in Play mode ~7 isPlaying-gated assertions legitimately fail; check `Unity_ManageEditor GetState` first. MCP down? → write `Temp/selftest-autorun-request.txt` (suite on next domain reload) or `Logs/menu-autorun-request.txt` (menu list; headless via `Unity.exe -batchmode -quit -projectPath <proj> -executeMethod MenuAutoRun.RunNow` with the editor CLOSED; Logs/ not Temp/ — Unity wipes Temp).
3. Open work lives in ONE tracker: **`Docs/remaining-work-checklist.md`** (§13 = the user's queued playtest issues). Check items off as they land.

## THE GAME FLOW (canonical; full detail → `Docs/gameplay-flow.md`)
Boot → **cube spawn room** (MainMenu scene: Laboratory / Settings / Quit; amber Demo button if config-enabled) → fade into **SampleScene** at the front door, Pharmee greets. Pharmee **guards the lab door** (pure FSM `GatekeeperModel`): approach → **Lab Tour** (ungraded guided tour) or **Campaign** → explain → **episode picker** (locked periods dimmed) → don **coat+goggles+gloves** at the in-lab locker → "I'm ready" → fade + stage builds **ARMED** (clock held) → threshold warning → door opens → **crossing the threshold starts the timer**. The player runs the experiment (pours/verbs/sims complete TaskGraph tasks in prerequisite order; mistakes are graded; reagents are finite → starvation offers a restart). Chemical tests done → clock FREEZES → fade-teleport to the **review corner** → Jimenez briefs → **quiz tablet** (3 MCQs + record-only yield; never score-gated) → submit = `Finish` → **grade screen** (floored %, PASSED/TRY AGAIN) + success/failure **outro cutscene** + spoken verdicts. **Pass** → Continue → one-fade lab reset + teleport home → entrance debrief → **unlock announcement** (all-11-passed = campaign-complete celebration) → door re-blocks, loop repeats. **Fail** → Retry (clean re-armed attempt) or **Choose Another** (back to the picker). HUD Restart aborts un-graded and fully resets. Demo sessions: separate save, all unlocked, skip buttons, infinite supply, end products visible.
- Two-part gate: **rubric grade ≥90 AND BKT mastery ≥0.90** per module. Progression = linear 11-module chain, period doors.

## THE 11 EXPERIMENTS (full data → `Docs/experiments-reference.md`)
| # | moduleId | Period | Manuscript | Product | Signature verbs |
|---|----------|--------|------------|---------|-----------------|
| 1 | tutorial-methane | Tutorial | game-authored | (gas; splint pop) | grind, heat, collect, splint |
| 2 | prelim-chemical-compounding | Prelim | Exp 2 ⚠ diverges (client flag) | — (ID lab) | test battery |
| 3 | prelim-ethyl-alcohol | Prelim | Exp 3 | Ethanol | ferment, distill, iodoform/ester |
| 4 | midterm-benzoic-acid | Midterm | Exp 4 (errata; game = benzaldehyde+KMnO₄) | Benzoic Acid | oxidise, filter, acidify, ester(propyl) |
| 5 | midterm-acetanilide | Midterm | Exp 5 | Acetanilide | acylate (acetyl chloride), crystallise |
| 6 | midterm-acetone | Midterm | Exp 6 | Acetone | WEIGH, dry-distill, 4 tests |
| 7 | midterm-chloroform | Midterm | Exp 7 | Chloroform | haloform, decant, dichromate oxidation |
| 8 | final-benzamide | Final | Exp 8 | Benzamide | ice bath, STIR, nitrous (nitrite!) |
| 9 | final-aspirin | Final | game-authored | Aspirin | WEIGH on scale, crystallise, FeCl₃ |
| 10 | final-caffeine | Final | game-authored | Caffeine | grind (edu), extract, sublime, murexide |
| 11 | final-winemaking | Final | Exp 9 (NON-grape) | Wine | ferment Mixed Fruit Juice, CO₂/limewater |

## Architecture map (mechanics detail → `Docs/systems-reference.md`)
All under `Assets/PharmaSynth/Scripts/`, **thin MonoBehaviours over pure suite-tested cores** (mandatory pattern; every component has a `Bind()` seam — edit-mode AddComponent fires no Awake/OnEnable).
- `Experiment/` TaskGraph(+Model) · ExperimentModuleDefinition · ExperimentRunner (StartExperiment / PrepareExperiment+StartRun armed seam / Finish / **Abort** / FreezeClock) · MistakeLog · QuizBank(+Library)
- `Scoring/` MasteryModel (BKT) · ScoreCalculator · ExperimentGrader — the two-part gate
- `Progression/` ProgressionService (JSON save) · ExperimentCatalog (11-chain) · ProgressionFlow · GameFlow · ResultRecorder · UnlockDiff · DemoMode/DemoSession · ResultsExport
- `Chemistry/` LiquidPhysics (wake-from-empty + Ledger) · LiquidPourer (PourTick/ResolveTarget) · LiquidTaskBinding (requiredMl, completesTask) · ReagentSupplyMonitor (Unlatch) · ReactionRule/Registry · TemperatureSim · Gas/Crystallise/Filter controllers · HazardousMix(+Reactor) · ShelfPourWiring · PharmaLiquid shader
- `Interaction/` ExperimentSceneBuilder (stage spawner; EnsureLiquidVisual; RackKit/spares) · ExperimentTaskStation · ZoneItemSensor/ZoneSimStation (ignition gate) · verbs: OrbitMath+Stir/GrindController, WeighMath/Station(+ScaleController), ScoopMath/ScoopController (solids, 2 g/dip), CleanupMath/CleanableVessel+BrushController (scrub dirty glass), Matchstick/MatchStriker/BurnerController · AssemblyMath/ApparatusSnap (snap kits; grab=group, ACTIVATE=detach) · Mishandling/BreakableGlassware (break 7.0 m/s; DisplayNameFor) · DropRespawn (settle-freeze; floor-only return; refills supply) · PhysicsProfiles/RealSizes/GrabTuning/GrabPhysicsPolicy · MethaneApparatusRig (SplintShouldFire) · feedback: VesselStatus/MixFeedback/FloatingText/StationStatusLabel/HoverInspector · LayoutTidyMath/WorkspaceShelfMath (2 rows) · ExperimentLauncher · DoorOpener · DevExperimentDriver (B/1-5/F/R/P=pour-debug)
- `NPC/` GatekeeperModel (**the flow FSM**, IsReviewState) · PharmeeGatekeeper (driver; ReviewFlowActive; ResetToEntrance; OnAbandonAfterFail) · PharmeeBrain · PharmeeLines (ALL dialogue pools + CampaignComplete) · NPCNarrationController · ExaminerNPC · ProctorRoamer · LabTourGuide · CutsceneDirector (SkipNextOutro) · Pharmee face/mood/poke/attitude
- `UI/` HudRig/HudDialogueBar · ChoicePanel · ScreenFader (composing callbacks) · GradeScreenController (+backButton) · GradeDisplay (floored %) · PostLabController (quiz; Open freezes clock; Close) · WristWatchController (holo checklist = THE procedures panel; SuppressNpcPokes) · HoloScroller (wrap+scroll+page buttons) · LabInfoDatabase · GlyphSafe · VesselStatusMath · SettingsService/ComfortApplier · ProximityLabel
- `Safety/` PPESetModel/PPEController · FumeHoodZone · HazardZone | `Audio/` AudioService · SoundBank | `Editor/` PharmaSelfTests (1023) · all builder menus (+ WorkspaceKitsBuilder, ChemicalStateAudit, Fix Holo Board Scroll) · DevCapture · W5.8/W5.9 data appliers · MenuAutoRun/SelfTestAutoRun

**Key scene objects (SampleScene):** ExperimentSystems (runner/launcher/builder/monitor/recorder) · HudRig · RobotNPC (Pharmee + gatekeeper) at the corridor corner · LabDoorController + door triggers + FrontDoorSpawn · PPELocker · ScreenFader · Services · PostLabTablet + GradeScreen (review corner) · MethaneStage + DynamicStage · ReagentShelf (west 3x4 cubby: raws + gated end products) · ReagentCabinets (east) · WorkspaceShelf (gantry platforms) · WorldLabels · XR Origin (CC r0.25 + HeadCollisionPushback). MainMenu: cube room + MenuCanvas. Build Settings = [MainMenu(0), SampleScene(1)].

## 📚 DOC INDEX — open the right doc for the work
| Working on… | Open |
|---|---|
| An experiment's chemistry/steps/reagents/tests/quiz/layout | `Docs/experiments-reference.md` |
| Flow, gate states, review, grading, restarts, demo mode | `Docs/gameplay-flow.md` |
| Any mechanic (liquids/verbs/breakage/feedback/NPC), **builder menus + rebuild orders**, adding content | `Docs/systems-reference.md` |
| Running, testing, simulator keys, Quest build config, headset toggle | `Docs/build-and-run.md` |
| What's still to do (incl. §13 queued user playtest issues) | `Docs/remaining-work-checklist.md` |
| Manuscript evidence / deviations detail | `Docs/manuscript-reconciliation.md` (+ `storyboard-reconciliation.md`) |
| Client decisions pending | `Docs/client-signoff-request.md` |
| Art/audio/video assets still to produce | `Docs/asset-production-spec.md` |
| Quest 3 day-1 device pass | `Docs/on-device-test-plan.md` |
| Why/when something changed (W1→W5.10 history) | `Docs/changelog.md` |

## Known gotchas (carry forward)
- `Unity_RunCommand` compiles inside `Unity.AI.*` → fully-qualify/alias `UnityEngine.UI.*` (`UImage`, `UButton`); `System.Reflection`/`ISet`/`GetInstanceID` blocked. File writes + `AssetDatabase.DeleteAsset` flagged "requires user interaction" → use Bash for files, load-or-overwrite for assets. **Menu items are exempt** (DevCapture works).
- **Never RunCommand while the user is in Play mode** (`OpenScene` throws; scene edits force-exit play) — check `Unity_ManageEditor GetState` first. One scene per command; `AssetDatabase.Refresh()` before cross-scene asset loads; a `Refresh:true` menu execute can swallow the run in the domain reload.
- Edit mode: `OnEnable`/`Awake` don't fire on `AddComponent` → `Bind()` seams everywhere. Never `renderer.material` in edit mode (use `sharedMaterial`/MPB — TMP `outlineWidth` also instances materials!). `AddComponent<LiquidPhysics>` needs a Renderer host.
- Before deleting a script, grep ALL of `Assets/` for the type name. FBX imports have no colliders. ChemLab `_WithLiquid` prefabs: verify mesh names, not GO names.
- MCP "named pipe not found" = editor busy → wait for `Logs/Editor.log` quiet, retry. "Connection revoked" = AI-seat licensing hiccup → re-approve under Project Settings → AI, or use the request-file fallbacks. `Unity_Camera_Capture` broken → use DevCapture (**yaw 0–360 only**, negative misparses).
- Transparent geometry doesn't z-write → text/canvas visibility = **sortingOrder** (HUD 30000, bubble 29000, world panels 4000–5000, TMP labels 20000).
- Poppler/PDF: TEMP hijacked → Read can't open PDFs; use `"C:/Program Files/Git/mingw64/bin/pdftotext.exe"` or pypdf. Internet via `curl` (github-raw 429s). Windows FS is case-insensitive: case-only asset renames need `AssetDatabase.RenameAsset`.
- Dialogue copy edits invalidate `VoiceBank` text-hashes → re-export the voice manifest after copy changes. Suite warnings that are EXPECTED: the 3 listed in SESSION START.
- The suite pins behavior (Mishandling lists, ContentSuite task counts, layout spacing…) — move pinned assertions IN THE SAME change as the behavior.

## ⚡ Efficiency policy (user directive 2026-07-12 — binding for all sessions)
- **Test at phase boundaries, not per micro-edit**: run the suite ONCE after a coherent batch compiles, and always before ending a turn that changed code/data. Docs-only or asset-move-only changes: no suite (compile check at most). Never re-run without intervening changes. (A run costs ~400 tokens — cheap insurance; the waste is frequency, not the run.)
- **Logs are LIVING, not append-only**: when a feature changes, UPDATE the affected lines in `systems-reference.md` / `gameplay-flow.md` / `experiments-reference.md` in place — never append dated narrative blobs. **When you CORRECT or SUPERSEDE something** — a fact you realize is wrong, or a mid-work pivot (approach A→B) — EDIT the canonical line to the final truth; never leave the stale claim and append "actually B" (a doc that contradicts itself makes a cold session guess which is current — a wrong canonical line is worse than a verbose log because sessions TRUST the docs and won't re-verify). **Fix the doc in the SAME change as the code**, so canonical facts never silently drift. `Docs/changelog.md` gets **one line per batch** recording the END STATE, not the journey (date · name · 1-sentence summary · suite count). The WHY of a decision lives in code comments at the decision site + suite assertions, not in prose logs. Checklist items: flip `[x]` + ≤1-line note; fold long-completed sections into a single DONE line.
- **CLAUDE.md is injected into every message of every session** — every KB here is a recurring tax. Hard cap ~100 lines; "Current state" is REPLACED, never appended.
- **Docs before agents**: consult the doc index before spawning explore/plan subagents (each costs 100k+ tokens and re-derives what the docs already say). Agents are for genuinely unmapped territory only; when justified, give TIGHT briefs with the context supplied + "report ≤40 lines, structured". Prefer targeted section reads over full-file reads.
- **Session scoping**: batch related small items into ONE session prompt; new session per work theme (long mixed sessions pay compounding context tax). Plan-mode ceremony is for multi-system batches — never inflate a small well-specified fix into explorers + plan agents.
- **Unity tool discipline**: prefer MENU ITEMS over RunCommand (RunCommand results echo the whole script back — every script costs double); keep unavoidable RunCommand scripts minimal; small `maxEntries` on console reads. **Verify the suite by Reading `Logs/selftest-result.txt`** (the suite writes its one-line summary there) instead of wrapping the run in a capture script.
- **One cheapest-sufficient check per fact** — never suite-assert + scene-inspect + DevCapture the same thing. DevCapture (~1.5k tokens/image) only when the VISUAL is the question, one shot per change.
- **Bulk content goes through scripts, not the conversation**: generators/extractors write files directly (the experiments-reference pattern); read back only a summary line.
- **Output discipline**: short delta summaries mid-batch; full recap only at batch end.

## Environment & conventions
- Windows 11; PowerShell primary, Git Bash available. Active build target **Android** (IL2CPP/ARM64/ASTC, Quest features configured). **Headset play**: Tools ▸ PharmaSynth ▸ Headset Play Mode (OpenXR on Play) — ON drives a Quest-Link headset in editor Play (currently ON); OFF = headless keyboard/simulator (init with no headset can stall Play). **No Cinemachine — never animate the XR camera.**
- **Unity MCP** = official Assistant server (AI seat). MCP tools cost no credits; `Unity_AssetGeneration_*` and Assistant chat DO — flag before spending. **Art creation is IN scope** (user has credits; flag AI-generation spends first). Tripo swap convention: save model prefabs to `Art/Generated/Refs/<ExactName>.prefab` (see systems-reference §9).
- Git: work on **main**; commit only when asked; no destructive ops. Off-repo backups: `C:\Users\MSI\PharmaSynth-handoff-backup\`.
- Every change: suite green + zero-error console + DevCapture for visual work. Quest 3 not delivered — test in-editor (or headset via Link); a human play-tests by pressing Play (MCP play mode is unreliable). Escalate to client if no headset by W5 (Aug 4–10).
- Experiments are DATA; confirm game-design changes with the user; builders/menus for scene edits (idempotent, re-runnable); match inherited code style.

## Current state (2026-07-12, end of W5.12 — suite 1023/1023)
Engine, all 11 experiments, the full client-confirmed loop, the W5.8–W5.10 overhauls, and the **W5.12 §13 playtest batch** (floor-only respawn + bottle refills, 7.0 break leniency + nature audit + display names, pour sphere-assist + P overlay, scoop verb, 2-row workspace shelf + apparatus kits, ApparatusSnap assemblies, brush cleaning, holo wrap/scroll, dialogue queue/dwell, watch-gesture suppression) are DONE and regression-locked. **Pending: joint headset pour session (§13e — dev key P) + a W5.12 feel pass** (snap/detach, kit reachability, break feel), then §10–§12 residue. Unity MCP approval currently REVOKED (Project Settings → AI) — request-file/headless fallbacks work fine. Blocked buckets: on-device week (no headset), client sign-offs, voice generation (needs ELEVENLABS_API_KEY).
