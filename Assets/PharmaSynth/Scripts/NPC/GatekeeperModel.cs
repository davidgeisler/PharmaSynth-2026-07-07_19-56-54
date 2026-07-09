using System;
using System.Collections.Generic;

/// States of the door-gated game loop (confirmed client workflow, 2026-07-09):
/// Pharmee blocks the lab door; the player picks Lab Tour or Campaign → episode →
/// lab-coat → ready → the lab loads/resets → threshold warning → the period starts
/// the moment they walk in → debrief → teleport back → unlock announcement → repeat.
public enum GateState
{
    Blocked, ModeChoice, CampaignExplain, EpisodePick, CoatPrompt,
    ReadyPrompt, Loading, ThresholdWarn, DoorArmed, Running,
    SupplyPrompt, Debrief, Returning, UnlockAnnounce, LabTour
}

public enum GateEvent
{
    Approach, PickLabTour, PickCampaign, ExplainDone, EpisodeChosen,
    Coated, Ready, Loaded, ProceedConfirmed, CrossedThreshold,
    ContinueAfterPass, DebriefDone, TeleportDone, AnnounceDone,
    SupplyExhausted, RestartConfirmed, Dismiss,
    TalkRequested   // poking Pharmee re-opens the conversation (e.g. during Lab Tour)
}

/// Pure, table-driven state machine for the Pharmee door gate. No Unity types —
/// fully edit-mode testable; the thin PharmeeGatekeeper MonoBehaviour applies each
/// transition to the scene (door blocker, panels, launcher, runner, fades).
public class GatekeeperModel
{
    public GateState State { get; private set; } = GateState.Blocked;

    /// The campaign module chosen at the door (set by ChooseEpisode).
    public string SelectedModuleId { get; private set; }

    public bool IsLabTour => State == GateState.LabTour;

    /// (from, to) after every successful Fire.
    public event Action<GateState, GateState> Transition;

    /// Attempt a transition; returns false (state unchanged) for illegal events.
    public bool Fire(GateEvent e)
    {
        GateState next = Next(State, e);
        if (next == State) return false;
        GateState from = State;
        State = next;
        Transition?.Invoke(from, next);
        return true;
    }

    /// The pure transition table. Illegal (state,event) pairs return the same state.
    public static GateState Next(GateState s, GateEvent e)
    {
        switch (s)
        {
            case GateState.Blocked:
                if (e == GateEvent.Approach || e == GateEvent.TalkRequested) return GateState.ModeChoice;
                break;
            case GateState.ModeChoice:
                if (e == GateEvent.PickLabTour) return GateState.LabTour;
                if (e == GateEvent.PickCampaign) return GateState.CampaignExplain;
                if (e == GateEvent.Dismiss) return GateState.Blocked;
                break;
            case GateState.LabTour:
                // NOTE: Approach must NOT transition here — walking to the open door
                // re-enters the approach trigger and would slam it shut again.
                // Poke Pharmee (TalkRequested) to change plans instead.
                if (e == GateEvent.TalkRequested) return GateState.ModeChoice;
                break;
            case GateState.CampaignExplain:
                if (e == GateEvent.ExplainDone) return GateState.EpisodePick;
                if (e == GateEvent.Dismiss) return GateState.ModeChoice;
                break;
            case GateState.EpisodePick:
                if (e == GateEvent.EpisodeChosen) return GateState.CoatPrompt;
                if (e == GateEvent.Dismiss) return GateState.ModeChoice;
                break;
            case GateState.CoatPrompt:
                if (e == GateEvent.Coated) return GateState.ReadyPrompt;
                if (e == GateEvent.Dismiss) return GateState.ModeChoice;
                break;
            case GateState.ReadyPrompt:
                if (e == GateEvent.Ready) return GateState.Loading;
                if (e == GateEvent.Dismiss) return GateState.ModeChoice;
                break;
            case GateState.Loading:
                if (e == GateEvent.Loaded) return GateState.ThresholdWarn;
                break;
            case GateState.ThresholdWarn:
                if (e == GateEvent.ProceedConfirmed) return GateState.DoorArmed;
                if (e == GateEvent.Dismiss) return GateState.ModeChoice;
                break;
            case GateState.DoorArmed:
                if (e == GateEvent.CrossedThreshold) return GateState.Running;
                if (e == GateEvent.Dismiss) return GateState.ThresholdWarn;
                break;
            case GateState.Running:
                if (e == GateEvent.SupplyExhausted) return GateState.SupplyPrompt;
                if (e == GateEvent.ContinueAfterPass) return GateState.Debrief;
                break;
            case GateState.SupplyPrompt:
                if (e == GateEvent.RestartConfirmed) return GateState.Loading;
                if (e == GateEvent.Dismiss) return GateState.Running;       // keep trying
                break;
            case GateState.Debrief:
                if (e == GateEvent.DebriefDone) return GateState.Returning;
                break;
            case GateState.Returning:
                if (e == GateEvent.TeleportDone) return GateState.UnlockAnnounce;
                break;
            case GateState.UnlockAnnounce:
                if (e == GateEvent.AnnounceDone) return GateState.Blocked;
                break;
        }
        return s;
    }

    /// Whether the physical door blocker should be OFF in this state.
    public static bool DoorOpen(GateState s)
        => s == GateState.LabTour || s == GateState.DoorArmed || s == GateState.Running
        || s == GateState.SupplyPrompt || s == GateState.Debrief;

    /// Pick the episode (period): resolves the first playable module in it, checks
    /// selectability, stores the module and advances. False = locked/empty, no move.
    public bool ChooseEpisode(ExperimentPeriod period, Func<string, bool> canSelect,
                              Func<ExperimentPeriod, string> firstPlayable)
    {
        if (State != GateState.EpisodePick) return false;
        string moduleId = firstPlayable != null ? firstPlayable(period) : null;
        if (string.IsNullOrEmpty(moduleId)) return false;
        if (canSelect != null && !canSelect(moduleId)) return false;
        SelectedModuleId = moduleId;
        return Fire(GateEvent.EpisodeChosen);
    }

    /// The first unlocked-but-unpassed module in a period; falls back to the first
    /// unlocked (replay); null when the period is fully locked.
    public static string FirstPlayableInPeriod(ProgressionFlow flow, ExperimentPeriod period)
    {
        if (flow == null) return null;
        string firstUnlocked = null;
        foreach (var e in ExperimentCatalog.InPeriod(period))
        {
            if (!flow.IsUnlocked(e.moduleId)) continue;
            if (firstUnlocked == null) firstUnlocked = e.moduleId;
            if (!flow.IsPassed(e.moduleId)) return e.moduleId;
        }
        return firstUnlocked;
    }

    /// Episode-picker rows for the door dialogue: label + selectable per period,
    /// in catalog order (Tutorial, Prelim, Midterm, Final). Pure for tests.
    public static void EpisodeOptions(ProgressionFlow flow,
        out List<string> labels, out List<bool> selectable)
    {
        labels = new List<string>();
        selectable = new List<bool>();
        foreach (ExperimentPeriod p in Enum.GetValues(typeof(ExperimentPeriod)))
        {
            bool periodOpen = flow != null && flow.IsPeriodUnlocked(p);
            string first = periodOpen ? FirstPlayableInPeriod(flow, p) : null;
            bool ok = periodOpen && !string.IsNullOrEmpty(first);
            labels.Add(p + (ok ? "" : "  (locked)"));
            selectable.Add(ok);
        }
    }
}
