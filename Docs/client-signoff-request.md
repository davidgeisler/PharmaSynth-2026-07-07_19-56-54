# PharmaSynth — Client Sign-Off Request

**Purpose:** the build is feature-complete and verified; these are the **decisions only the client can make**. Each is data-driven in the engine, so a change here is a ScriptableObject edit, not a code change. Please mark a choice per row and return.

_Prepared 2026-07-09. Source of truth for chemistry: the WCC lab manual (Appendix C). Storyboard art is a reference we exceed and must **not** be copied for chemistry._

> **UPDATE 2026-07-09 — Appendix C received & applied.** The client supplied `Docs/Documentations/manuscript.pdf`. Chemistry read straight from the manual and wired in (§C1–C3 + C6 below now resolved **from the manual**, not drafts). Remaining rows are genuinely optional client preferences (rubric weights, wine rubric, yield grading, Dr. Jimenez). The two truly external blockers left are **art/audio credits** and the **Quest 3 headset**.

---

## 1. Chemistry reconciliations (manual vs storyboard vs safety)

| # | Experiment | Point in question | Options | Our recommendation | Client decision |
|---|-----------|-------------------|---------|--------------------|-----------------|
| C1 | **Benzoic Acid** | Oxidation substrate | (a) **benzaldehyde + 0.1% KMnO₄** (Appendix C reagent list) · (b) toluene + KMnO₄ (plan's earlier tentative text) | **(a) benzaldehyde** — matches Appendix C; already authored | ☐ a  ☐ b |
| C2 | **Acetanilide** | Acylating agent | (a) **acetyl chloride** (manual) · (b) acetic anhydride (storyboard; safer, milder, VR-friendlier) | **(b) acetic anhydride** as the built default, with (a) noted — confirm acceptable for a teaching sim | ☐ a  ☐ b |
| C3 | **Benzamide** | Confirmatory diazo/nitrous test reagent | (a) **sodium nitrite** (correct) · (b) "sodium nitrate" (manual typo) | **(a) sodium nitrite** — manual's "nitrate" is a typo | ☐ a  ☐ b |
| C4 | **Acetone** | Storyboard art depicts wrong glassware/route | Use manual Exp 6 (dry distillation of calcium acetate, collect at 56 °C) | Follow manual; storyboard art disregarded | ☐ confirm |
| C5 | **Chloroform** | Storyboard art wrong | Use manual Exp 7 (bleaching powder + acetone, reflux/distil; AgNO₃ + inflammability tests) | Follow manual | ☐ confirm |
| C6 | **Confirmatory-test observations/products** | The pour-in-vessel reactions currently complete the step but several tests don't yet show a product/observation (colour change, precipitate, odour) | Author one `ReactionRule` per test using the manual's stated outcome | **Hold for this sheet** — we will not author test chemistry until the manual's expected results per test are confirmed here (list attached below) | ☐ send outcomes  ☐ we draft, you review |

**C6 — DRAFTS ADDED (2026-07-09), please confirm vs manual:** two clearly-standard positive tests are now wired as **drafts** (their observation text starts with "DRAFT (confirm vs manual)"): **(1) ethanol + KMnO₄** → purple fades (oxidation); **(2) benzoate + FeCl₃** → buff/salmon precipitate. Most other confirmatory tests are **negative results** (e.g. acetone with Tollens, chloroform with AgNO₃) which correctly need no reaction, or are already built (iodoform, ester, aspirin/salicylate FeCl₃).

**Still awaiting confirmed outcomes** (not yet drafted — need the manual's exact result): ethanol KMnO₄/bromine functional tests · benzoate + FeCl₃ (buff ppt?) · acetone Tollens (negative) / Schiff / bisulfite · caffeine murexide · benzamide alkali/acid/nitrous · chloroform AgNO₃ + inflammability · aspirin FeCl₃ (negative). For each: expected colour/precipitate/odour + whether it's a positive or negative result.

## 2. Scoring — rubric weights (need sign-off; plan §3.6)

Every experiment currently uses this normalized weight set (the manual's printed weights are internally inconsistent, so we normalized). **Confirm or override per experiment.**

| Criterion | Weight |
|---|---|
| Procedure (synthesis steps) | 0.40 |
| Chemical Tests | 0.20 |
| Materials & PPE / safety | 0.15 |
| Time Management | 0.10 |
| Sanitation / cleanup | 0.10 |
| Documentation (post-lab quiz) | 0.05 |

- Two-part gate to advance: **grade ≥ 90% AND BKT mastery ≥ 0.90**. ☐ confirm 90% · ☐ change to ____
- **Wine Making** currently uses this same 6-criterion rubric. Its manual has a bespoke rubric (workmanship / appearance / presentation / documentation / flavour). ☐ keep standard for now · ☐ implement bespoke wine rubric (follow-up task).
- **Data-sheet yield**: recorded on the tablet but **not yet part of the grade**. ☐ leave ungraded · ☐ fold yield-accuracy into Documentation (needs target yield per experiment).

## 3. Scope decisions carried from the plan

| # | Item | Built as | Client decision |
|---|------|----------|-----------------|
| S1 | Post-lab "Documentation" | **3 MCQs per experiment** on the tablet (essay grading isn't VR-feasible) | ☐ approve MCQ bank |
| S2 | Analytics dashboard (manuscript mention) | **Descoped** to a local Results/History screen + exportable scores | ☐ approve descope |
| S3 | Dr. Jimenez examiner NPC | Needs a **rigged scientist model** (Asset Store or authored) — budget? | ☐ budget: ____ · ☐ use posed/static fallback |
| S4 | Tutorial guidance | Pharmee guides tutorial; assessments = Dr. Jimenez observing, no hints | ☐ confirm |

## 4. Hardware / schedule

- **Quest 3 headset not yet delivered.** On-device comfort + 90 Hz validation are day-1 device items and cannot start without it. The plan's escalation deadline was W5. ☐ delivery date: ____ · ☐ escalate.
