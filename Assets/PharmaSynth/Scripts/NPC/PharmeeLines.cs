/// Rich dialogue pools for the NPCs (user 2026-07-10: "populate more messages and
/// interactions for Pharmee and Dr. for a rich gameplay"). Pure static data +
/// a deterministic picker so the self-tests can pin variety without RNG. Pharmee
/// rotates through these instead of repeating one canned line; Dr. Jimenez draws
/// his stern examiner remarks from the exam pools. Client-reviewable copy (the
/// §5 dialogue sign-off still applies).
public static class PharmeeLines
{
    // ---- Pharmee (friendly robot guide) -------------------------------------
    public static readonly string[] Greetings =
    {
        "Welcome back to the lab! I'm Pharmee — follow your tablet and I'll be right here.",
        "Suited up and ready? Let's synthesise something great today!",
        "Great to see you! Take it step by step and check your wrist for the procedure.",
        "Lab's all yours. Work safe, work smart, and call on me anytime.",
    };

    public static readonly string[] Praise =
    {
        "Nice work!", "Perfect.", "That's the way!", "Excellent technique.",
        "Spot on.", "You're getting the hang of this!", "Cleanly done.",
    };

    public static readonly string[] Celebrate =
    {
        "Outstanding! Experiment complete — beautiful synthesis!",
        "You nailed it! That's textbook lab work.",
        "Brilliant! Your product looks great and your method was sound.",
        "Success! Dr. Jimenez will be impressed with that one.",
    };

    public static readonly string[] Encourage =
    {
        "Good effort — review the steps and give it another go. You've got this!",
        "Almost there. Check where it slipped and try once more.",
        "Don't worry, every chemist reworks a prep. Let's run it again.",
        "So close! Tighten up the tricky steps and you'll pass next time.",
    };

    public static readonly string[] Idle =
    {
        "Take your time — precision beats speed in here.",
        "Remember to keep your goggles on while you work.",
        "Reagents run low, so pour only what the step needs.",
        "Your wrist checklist updates as you go — glance at it anytime.",
        "The fume hood is there for the smelly stuff. Use it!",
        "Doing great. Steady hands.",
        "If a bottle runs dry, I can reset the period for you.",
    };

    public static readonly string[] WrongReagent =
    {
        "Hmm, that isn't the reagent this step needs — check your tablet.",
        "Wrong bottle! Look again at the procedure for the right reagent.",
        "That's not it. The step calls for a different chemical.",
    };

    public static readonly string[] WrongStep =
    {
        "Let's not skip ahead — finish the current step first.",
        "One step at a time; that one comes later.",
        "Hold on — complete what's in front of you before moving on.",
    };

    public static readonly string[] Overheat =
    {
        "Careful — it's overheating! Ease off the heat.",
        "Too hot! Back off the burner before it boils over.",
        "Watch the temperature — lower the flame.",
    };

    public static readonly string[] Safety =
    {
        "Safety first! Mind the hazard and keep your PPE on.",
        "Easy — handle the glassware gently, please.",
        "Careful there. Let's keep the bench clean and safe.",
        "Mind that spill — clean as you go.",
    };

    // ---- Dr. Jimenez (stern examiner, gives NO hints) ------------------------
    public static readonly string[] ExamGreeting =
    {
        "I am Dr. Jimenez. I will be observing this assessment. Proceed.",
        "This is a graded exercise. Work carefully — I am watching your technique.",
        "Begin when ready. I evaluate method, safety, and results.",
    };

    public static readonly string[] ExamRemarks =
    {
        "Mind your procedure.",
        "I'm noting your technique.",
        "Observe your safety protocol.",
        "Efficiency counts toward your grade.",
        "Precision, please.",
        "I expect proper use of the fume hood.",
        "Keep your workspace orderly.",
    };

    // ---- Post-experiment review flow (user 2026-07-11: congrats → Jimenez quiz
    // briefing → score remarks → entrance debrief). Finite pools, numbers stay on
    // the grade card so every line is voice-recordable.
    public static readonly string[] TestsDoneLines =
    {
        "Excellent — every test is complete! Dr. Jimenez will review your work now. Let's head over to him.",
        "That's the last test done! Time for your assessment review — Dr. Jimenez is waiting.",
        "All tests finished — wonderful work! Off to Dr. Jimenez for your documentation and review.",
    };

    public static readonly string[] JimenezQuizBrief =
    {
        "Well done reaching this stage. Before your grade is final, you will document your work.",
        "Answer the questions on the tablet. Your documentation counts toward your final grade. Begin.",
        "Take your time — accuracy over speed. The tablet is ready for you.",
    };

    public static readonly string[] JimenezPassRemarks =
    {
        "A commendable performance. Your technique meets the standard.",
        "Satisfactory work. Your method was sound and your record is complete.",
        "You have met the requirements of this assessment. Well executed.",
        "Precise and orderly. I am marking this assessment as passed.",
    };

    public static readonly string[] JimenezFailRemarks =
    {
        "Not yet to standard. Review your procedure and attempt it again.",
        "Your record shows gaps. Repeat the exercise with more care.",
        "This attempt falls short of the requirement. I expect better on your retry.",
    };

    public static readonly string[] DebriefCongrats =
    {
        "Congratulations on completing the assessment and the quiz!",
        "Assessment complete — quiz and all. Well done making it through!",
        "That's a full experiment, tests, and documentation finished. Great job!",
    };

    /// Banded performance remark for Pharmee's entrance debrief (Debrief is only
    /// reachable after a pass, so bands split the passing range). Pure for tests.
    public static string DebriefRemark(float gradeTotal)
    {
        if (gradeTotal >= 97f) return "A flawless run — truly impressive work.";
        if (gradeTotal >= 93f) return "A strong performance — you clearly know your way around this lab.";
        return "A solid pass — keep practising and the tricky steps will feel natural.";
    }

    // ---- Guided lab tour (storyboard beats, refined to exceed) ---------------
    // Played in order when the player picks "Lab Tour" — Pharmee walks them through
    // each area instead of the old single free-roam line. The last beat is the closer.
    public static readonly string[] TourBeats =
    {
        "Welcome to the lab! Let's take a quick tour so you feel right at home before any graded work.",
        "Flick your wrist face-up and glance at it — your holographic procedures board lays out every step of the experiment, live.",
        "The bench in front of you is your main workspace: prepare reagents, run your reactions, and handle glassware here.",
        "Over there is the equipment cabinet — open it and pick the apparatus your tablet calls for.",
        "The reagent shelf holds your chemicals. Take only what each step needs — the bottles are limited!",
        "Flick your wrist anytime for the live checklist, and watch the progress bar and timer up top to track how you're doing.",
        "The Settings button up top lets you tune audio, text size and comfort options whenever you like.",
        "Follow the glowing markers to each step. Poke me whenever you're ready to take on a graded campaign!",
    };

    /// Deterministic pool picker (tests pin it): wraps any index, non-negative.
    public static string Pick(string[] pool, int n)
    {
        if (pool == null || pool.Length == 0) return "";
        int i = ((n % pool.Length) + pool.Length) % pool.Length;
        return pool[i];
    }
}
