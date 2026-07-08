# PharmaSynth — Gameplay Rules

**"PharmaSynth: Gear Up, Synth It Up!"** is a first-person guided chemistry-lab simulation for Meta Quest 3. This document is the plain-language rulebook: how a play session flows, what the 11 experiments are, how you unlock them, how you are graded, what counts as a mistake, how the game decides you have *mastered* a skill, and what help is always at hand. It describes the rules of the game as designed and implemented — no engine internals. (Current build note: the full rule set below is implemented and machine-verified; the Methane tutorial is the first experiment fully playable hands-on in VR.)

## Contents

1. [The player journey](#1-the-player-journey)
2. [The 11 experiments](#2-the-11-experiments)
3. [Progression rules](#3-progression-rules)
4. [Grading rubric](#4-grading-rubric)
5. [Mistakes & penalties](#5-mistakes--penalties)
6. [The mastery model, in plain words](#6-the-mastery-model-in-plain-words)
7. [Safety rules in-sim](#7-safety-rules-in-sim)
8. [Assists — you are never lost](#8-assists--you-are-never-lost)

---

## 1. The player journey

Every session follows the same loop:

1. **Main menu.** Four options: **Tutorial** (jump straight into the guided Methane tutorial), **Laboratory** (enter the lab at whatever experiment you should tackle next — the game remembers your progress and picks it for you), **Settings**, and **Quit**.
2. **Gear up (PPE).** Before working, you don your personal protective equipment — lab coat, goggles, gloves — at the locker. Putting on PPE is what opens the way into the lab proper, and PPE conduct is graded throughout (see [Section 7](#7-safety-rules-in-sim)).
3. **Begin the experiment.** Press the Begin button at the bench to start the attempt. A count-up timer starts.
4. **Intro cutscene.** Pharmee, your robot guide, sets the scene: what you are making today and why. Cutscenes are subtitle-and-staging based (the camera never moves on its own — a VR comfort rule) and can be skipped.
5. **Reagent preparation.** Gather, measure, grind, and mix the starting materials. When this phase completes, a short transition cutscene bridges you into the synthesis.
6. **Synthesis.** The heart of the experiment: assemble apparatus, heat, react, collect, crystallise, filter — whatever the procedure calls for, in the right order.
7. **Chemical tests.** Prove what you made really is what you think it is: flame tests, litmus, ferric chloride, iodoform, and so on, per experiment.
8. **Data sheet & quiz.** Record your results and answer the post-lab quiz. Your quiz score feeds the *Documentation* part of your grade.
9. **End cutscene — always.** Whether you passed or failed, an ending cutscene plays: a success variant celebrating the synthesis, or a failure variant. You never fall off a cliff into a bare menu.
10. **Grade screen.** Your grade percentage, number of mistakes, elapsed time, the per-category breakdown, your mastery percentage, and a verdict: **PASSED** or **TRY AGAIN**. If you fell short, the screen tells you *why* (grade below the mark, or mastery not yet demonstrated).
11. **Next.** **Retry** is always available and restarts a fresh attempt. **Continue** appears only once you have passed — it is how you move on to the next experiment.

Tasks are **auto-checked**: the game watches the world state (what you poured, what you heated, what filled with gas) and ticks steps off the moment the condition is truly met. Carrying the right item to the right station completes that step; the wrong item is simply ignored, and doing things out of order is recorded as a mistake.

## 2. The 11 experiments

The course runs across four school periods — Tutorial, Prelim, Midterm, Final — with 11 experiments in a fixed order. Each experiment has a set number of graded steps (its procedure checklist).

| # | Period | Experiment | What you synthesize / learn | Steps |
|---|--------|-----------|------------------------------|-------|
| 1 | Tutorial | Tutorial: Methane Synthesis | Make methane gas by heating sodium acetate with soda lime; collect it over water and confirm it with a burning splint. Learn apparatus assembly, gas collection and lab safety basics. | 5 |
| 2 | Prelim | Prelim: Chemical Compounding | Identify functional groups by their characteristic reactions — combustion, metal, halogen and oxidation tests — and record observations accurately. | 6 |
| 3 | Prelim | Prelim: Ethyl Alcohol Synthesis | Ferment sugar to ethanol with yeast, confirm CO2 with limewater, distil the 70–80 °C fraction, and confirm ethanol by combustion, iodoform and ester tests. | 7 |
| 4 | Midterm | Midterm 1: Benzoic Acid | Oxidise benzaldehyde to benzoic acid with dilute KMnO4, isolate and recrystallise the product, and confirm it by litmus, FeCl3 and ester tests. | 9 |
| 5 | Midterm | Midterm 2: Acetanilide | Acetylate aniline (in the fume hood!) to acetanilide, crystallise on ice, and confirm the amide by hydrolysis and bromination tests. | 10 |
| 6 | Midterm | Midterm 3: Acetone | Prepare acetone by dry distillation of acetate salts, collect the 56 °C fraction, and confirm the ketone with Tollen's, Schiff's, iodoform and bisulfite tests. | 10 |
| 7 | Midterm | Midterm 4: Chloroform | Make chloroform by the haloform reaction of acetone with bleaching powder, purify by distillation, and confirm by non-flammability and silver-nitrate tests. | 10 |
| 8 | Final | Final 1: Benzamide | Prepare benzamide from benzoyl chloride and ammonia in an ice bath, oven-dry the crystals, and confirm the amide by alkaline, acid and nitrous-acid tests. | 9 |
| 9 | Final | Final 2: Aspirin | Acetylate salicylic acid with acetic anhydride, keeping the water bath in check (overheating ruins the batch), crystallise on ice, vacuum-filter, and confirm purity with the ferric-chloride test. | 7 |
| 10 | Final | Final 3: Caffeine (from tea) | Extract caffeine from tea with a base and solvent extraction, purify by sublimation, and confirm identity by the murexide test and melting point. | 9 |
| 11 | Final | Final 4: Wine Making | Prepare a fermenting must from fruit, sugar and yeast, confirm fermentation with limewater, then rack and evaluate the finished wine. | 8 |

That is **90 graded steps** across the course. Every experiment moves through the same four phases: **Reagent Preparation → Synthesis → Chemical Tests → Data Sheet**.

> **Design note:** Wine Making currently uses the standard six-category rubric ([Section 4](#4-grading-rubric)); a bespoke workmanship/appearance/flavour rubric for it is a planned follow-up.

## 3. Progression rules

- **Linear unlock chain.** The experiments unlock strictly in the order of the table above. Only the Tutorial is open on a fresh save; passing an experiment unlocks the next one, all the way to Wine Making.
- **The two-part 90% gate.** Passing an experiment requires **both** of these on the same attempt:
  1. **Grade gate** — your rubric grade is **90% or higher**, and
  2. **Mastery gate** — your skill-mastery estimate is **0.90 or higher** ([Section 6](#6-the-mastery-model-in-plain-words)).
  A brilliant-but-lucky run can clear the grade and still fail the mastery gate; the grade screen tells you which gate fell short.
- **Period doors.** The lab's periods (Tutorial → Prelim → Midterm → Final) open in order: a period's door opens only when **every** experiment in all earlier periods has been passed. The Tutorial period is always open.
- **Retry is always allowed.** There is no attempt limit and no lockout. Retry restarts a clean attempt of the same experiment.
- **Your best sticks.** The game keeps your **best grade**, **best mastery**, attempt count, and passed status per experiment. Once you have passed something, it stays passed — a later bad run can never re-lock your progress. Progress is saved to disk (with a backup copy, so a corrupted save recovers automatically).
- **Overall completion** is simply the fraction of the 11 experiments passed; a full clear reads 100%.

## 4. Grading rubric

Your grade is a weighted average of six categories (weights follow the WCC lab-manual rubric). The default weights:

| Category | Weight | What it measures |
|----------|-------:|------------------|
| Procedure | 40% | Did you complete the procedure steps, correctly and in order? Procedural mistakes (wrong reagent, wrong order, overheating) deduct here. |
| Chemical Tests | 20% | Did you perform the confirmation tests? |
| Materials & PPE | 15% | Safe handling of materials and equipment. Safety mistakes (fire, missing PPE, fume-hood violations, chemical contact, hazardous actions) deduct here. |
| Time Management | 10% | Finish at or under the experiment's par time for full credit; credit then falls off steadily, reaching zero at double the par time. |
| Sanitation | 10% | Keep the lab clean — every piece of dropped/broken glassware deducts here. |
| Documentation | 5% | Your data-sheet / post-lab quiz score. |

Weights are **auto-normalized** — they always behave as fractions of 100% even if a specific experiment's rubric is tuned differently (this also irons out the manual's inconsistent printed weights).

**Par times per experiment:**

| Experiment | Par time |
|-----------|----------|
| Tutorial: Methane Synthesis | 5:00 |
| Prelim: Chemical Compounding | 6:00 |
| Prelim: Ethyl Alcohol Synthesis | 10:00 |
| Midterm 1: Benzoic Acid | 15:00 |
| Midterm 2: Acetanilide | 15:00 |
| Midterm 3: Acetone | 16:40 |
| Midterm 4: Chloroform | 18:20 |
| Final 1: Benzamide | 15:00 |
| Final 2: Aspirin | 12:00 |
| Final 3: Caffeine (from tea) | 16:40 |
| Final 4: Wine Making | 10:00 |

Par is generous — it is a "don't dawdle" incentive, not a speedrun timer, and it is only 10% of the grade.

## 5. Mistakes & penalties

The game recognises **nine kinds of lab error**. Each one is announced the moment it happens (Pharmee warns you, the HUD shows a toast, and the visible progress bar dips), is counted on the grade screen, deducts from a specific rubric category, and counts as negative evidence against the related skill in the mastery model.

| Error | In plain words | Example | Rubric category hurt |
|-------|----------------|---------|----------------------|
| Wrong reagent | You added a chemical no step of this experiment calls for. | Pouring HCl into the fermentation flask. | Procedure |
| Wrong step | Right idea, wrong time — you attempted a step before its prerequisites were done. | Lighting the burner before the apparatus is assembled. | Procedure |
| Dropped glassware | You dropped and broke a vessel. | A beaker shatters on the floor. | Sanitation |
| Overheat | You let the temperature run past the safe limit for the reaction. | Boiling the aspirin water bath until the product decomposes. | Procedure |
| Fire safety | Unsafe behaviour around open flame. | Leaving a burner roaring next to flammables. | Materials & PPE |
| Missing PPE | Working without your protective gear on. | Handling reagents before visiting the locker. | Materials & PPE |
| Fume hood violation | Handling a toxic/volatile reagent outside the fume hood. | Acetylating aniline out on the open bench. | Materials & PPE |
| Chemical contact | You touched a harmful substance or contaminated area. | Reaching into a corrosive spill. | Materials & PPE |
| Hazardous action | Any other flagged dangerous act in a hazard area. | Leaning into a marked hot/hazard zone. | Materials & PPE |

**How much do mistakes cost?** Each *procedural* mistake removes 10% of the Procedure sub-score; each *safety* mistake removes 15% of the Materials & PPE sub-score; each broken glassware removes 20% of the Sanitation sub-score. (These steps are designer-tunable per build.) Mistakes are honest but not fatal: a mistake never ends the run — you always get to finish, see the end cutscene, and learn from the grade breakdown.

## 6. The mastery model, in plain words

Behind the grade there is a second judge: a **Bayesian Knowledge Tracing** model that estimates, for each lab skill, the probability that you have actually *learned* it — not just gotten lucky.

**Six skills are tracked:** Measuring, Heating, Filtration, Transfer (moving materials and handling apparatus), Safety, and Test Interpretation. Every step of every experiment is tagged with the skill it exercises.

**How it works, without the math:**

- The game starts moderately skeptical: before you demonstrate anything, it assumes only a 25% chance you already know a skill.
- Every time you complete a step **correctly**, that is evidence you know its skill, and the estimate rises. Every **mistake** is evidence against the related skill (overheating counts against *Heating*; safety violations count against *Safety*; wrong reagent/step and broken glass count against *Transfer*/handling), and the estimate falls.
- The model deliberately discounts luck: it knows a student can guess right without understanding, and can slip up despite understanding. One good result is therefore never enough — roughly **two to three clean demonstrations of a skill** are needed before the game is 90% confident in it.
- Your **overall mastery** is the average across the skills the experiment tracks, and the pass gate wants it at **0.90 or higher**.

**Why one perfect run may not pass.** In a short experiment, some skills only come up once. One flawless demonstration lifts a skill's estimate to roughly two-thirds confidence — good, but not proof. So you can score a 96% grade and still see "Mastery not yet demonstrated — keep practicing" on the grade screen. That is intentional pedagogy, not a bug: the game is asking you to *show it again* until competence is unambiguous. Experiments that exercise a skill several times can be mastered in a single strong run.

## 7. Safety rules in-sim

Safety is not flavour — it is enforced, graded, and tracked as its own skill.

- **PPE before entry.** Gear up at the locker (lab coat, goggles, gloves) before entering the work area; the lab is blocked until you do. Working without PPE is a *Missing PPE* mistake.
- **Fume hood for the nasty stuff.** Reagents flagged toxic or volatile must be handled inside the fume hood. In the current reagent set, five chemicals carry that flag: **acetic anhydride, ammonia solution, aniline, benzaldehyde, and benzoyl chloride**. Using one of these outside the hood is an automatic *Fume Hood Violation*.
- **Hazard zones.** Spills, hot surfaces, and corrosive areas are live hazard volumes: contact reports a mistake and triggers a warning. (Zones re-arm after a couple of seconds, so lingering in one doesn't machine-gun your grade.)
- **Heat discipline.** Reactions have temperature targets *and* overheat thresholds. Push past the threshold and it is an *Overheat* mistake — and in Aspirin, a ruined batch.
- **Consequences are proportional.** Safety errors sting the Materials & PPE grade (15% of the grade at default weights), drag down your Safety mastery, and summon a warning from Pharmee — but they never hard-fail the run on the spot.

## 8. Assists — you are never lost

PharmaSynth is a guided sim. The full assist stack:

- **Pharmee, the robot guide.** Greets you at the start, speaks the instruction for your current step (every step has an authored hint), warns you with a matching line and a warning face whenever you make a mistake ("Let's not skip ahead…", "Careful — it's overheating!"), and celebrates or encourages you at the end depending on the result. Pharmee speaks in subtitles with robot beeps. During assessment, the examiner **Dr. Jimenez** observes and gives **no hints** — Pharmee's guidance is the teaching mode.
- **Waypoint marker.** A floating beacon hovers over the station your current step happens at, and moves as you progress. It hides when nothing is actionable.
- **Wrist watch (flip to check).** Flip your wrist so the watch face turns up (right hand by default) while glancing at it, and a compact panel appears: current step, progress %, mastery %. A controller-button fallback toggles the same panel for players who prefer not to use the gesture.
- **Tablet checklist.** A grab-able tablet shows the full procedure grouped by phase, live-ticking as you work — done steps checked, the current step highlighted with an arrow, pending steps unchecked — plus the balanced reaction equation for the experiment.
- **HUD.** The experiment title, a count-up timer (HH:MM:SS), and a progress bar that fills as you complete steps and visibly dips when you err. Task toasts pop for a couple of seconds when a step completes ("Collect methane over water ✓") or a mistake is recorded.
- **Auto-checking.** You never have to declare "I finished the step" — the game verifies world conditions itself and advances the moment a step is genuinely done.
- **Skip rules.** Cutscenes and Pharmee's narration are skippable at any time. The end cutscene *always plays* (success or failure variant) but you may skip through it. Guidance (waypoint, watch, tablet, hints) cannot be turned off in the current build; hint-free assessment presentation is part of the planned Dr. Jimenez exam mode.
- **For testers (desktop, non-VR):** a developer keyboard driver can run the whole loop in the editor — **B** begins the experiment, **1–5** complete steps, **F** finishes, **R** retries.
