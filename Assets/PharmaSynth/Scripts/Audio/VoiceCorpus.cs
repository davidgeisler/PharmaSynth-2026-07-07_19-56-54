using System.Collections.Generic;

/// Every code-authored NPC line, with its speaker — the voice-over corpus
/// (user 2026-07-10: both NPCs must speak). The manifest exporter adds the
/// cutscene SO beats on top (assets aren't reachable from runtime code).
/// Numbers were deliberately kept OUT of spoken lines (grade bands, finite
/// unlock variants), so this enumeration is exhaustive and finite.
public static class VoiceCorpus
{
    public struct Line
    {
        public VoiceSpeaker speaker;
        public string text;
        public Line(VoiceSpeaker s, string t) { speaker = s; text = t; }
    }

    public static List<Line> CodeLines()
    {
        var lines = new List<Line>();

        // Pharmee pools (variety, praise, warnings, tour, review flow).
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Greetings);
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Praise);
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Celebrate);
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Encourage);
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Idle);
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.WrongReagent);
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.WrongStep);
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Overheat);
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Safety);
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.TourBeats);
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.TestsDoneLines);
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.DebriefCongrats);

        // Dr. Jimenez pools (exam voice + review verdicts).
        AddPool(lines, VoiceSpeaker.Jimenez, PharmeeLines.ExamGreeting);
        AddPool(lines, VoiceSpeaker.Jimenez, PharmeeLines.ExamRemarks);
        AddPool(lines, VoiceSpeaker.Jimenez, PharmeeLines.JimenezQuizBrief);
        AddPool(lines, VoiceSpeaker.Jimenez, PharmeeLines.JimenezPassRemarks);
        AddPool(lines, VoiceSpeaker.Jimenez, PharmeeLines.JimenezFailRemarks);

        // Banded debrief remarks (finite bands, numbers live on the grade card).
        lines.Add(new Line(VoiceSpeaker.Pharmee, PharmeeLines.DebriefRemark(98f)));
        lines.Add(new Line(VoiceSpeaker.Pharmee, PharmeeLines.DebriefRemark(94f)));
        lines.Add(new Line(VoiceSpeaker.Pharmee, PharmeeLines.DebriefRemark(90f)));

        // Door-gate lines (the scene uses the code defaults).
        var gate = new PharmeeGatekeeper.GateLines();
        AddPool(lines, VoiceSpeaker.Pharmee, new[]
        {
            gate.approach, gate.labTour, gate.campaignExplain, gate.episodePrompt,
            gate.lockedEpisode, gate.coatPrompt, gate.readyPrompt, gate.thresholdWarn,
            gate.congrats, gate.supplyWarn, gate.welcome,
        });

        // Guided-tour beats (location-triggered guide).
        AddPool(lines, VoiceSpeaker.Pharmee, LabTourGuide.DefaultBeatTexts);

        // ILO opening dialogue (verbatim Appendix C + game-authored trio).
        lines.Add(new Line(VoiceSpeaker.Pharmee, IloCopy.LeadIn));
        foreach (var id in ModuleIds)
            foreach (var ilo in IloCopy.ForModule(id))
                lines.Add(new Line(VoiceSpeaker.Pharmee, ilo));

        // Hazard warnings (HUD toast copy doubles as the spoken warning).
        foreach (HazardousMix.HazardOutcome o in System.Enum.GetValues(typeof(HazardousMix.HazardOutcome)))
        {
            string w = HazardousMix.WarnLineFor(o);
            if (!string.IsNullOrEmpty(w)) lines.Add(new Line(VoiceSpeaker.Pharmee, w));
        }

        // Unlock announcements: finite variants over the catalog (one unlock at a
        // time in a linear roster) + the nothing-new fallback.
        foreach (var e in ExperimentCatalog.Entries)
            lines.Add(new Line(VoiceSpeaker.Pharmee, UnlockDiff.AnnouncementFor(new List<string> { e.moduleId })));
        lines.Add(new Line(VoiceSpeaker.Pharmee, UnlockDiff.AnnouncementFor(new List<string>())));

        // Dedupe by speaker+normalised text (pools share a few lines).
        var seen = new HashSet<string>();
        var unique = new List<Line>();
        foreach (var l in lines)
        {
            if (string.IsNullOrEmpty(l.text)) continue;
            string key = (int)l.speaker + ":" + VoiceLineId.For(l.text);
            if (seen.Add(key)) unique.Add(l);
        }
        return unique;
    }

    public static readonly string[] ModuleIds =
    {
        "tutorial-methane", "prelim-chemical-compounding", "prelim-ethyl-alcohol",
        "midterm-benzoic-acid", "midterm-acetanilide", "midterm-acetone", "midterm-chloroform",
        "final-benzamide", "final-aspirin", "final-caffeine", "final-winemaking",
    };

    private static void AddPool(List<Line> into, VoiceSpeaker s, string[] pool)
    {
        if (pool == null) return;
        foreach (var t in pool) into.Add(new Line(s, t));
    }
}
