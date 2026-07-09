using System;
using System.Collections.Generic;
using UnityEngine;

/// Thin scene driver for the GatekeeperModel: applies each state to the world —
/// door blocker on/off, Pharmee lines, the door choice panel, stage loading with
/// fades, and the walk-in run start. All heavy logic lives in the pure model.
public class PharmeeGatekeeper : MonoBehaviour
{
    [Serializable]
    public class GateLines
    {
        [TextArea] public string approach = "Hold on! You can't enter the lab just yet. What would you like to do today?";
        [TextArea] public string labTour = "Lab Tour it is! Roam freely and try the tools and reagents. Come back to me when you want to take on a campaign.";
        [TextArea] public string campaignExplain = "The Campaign takes you through the class periods. Pass every experiment with 90% or better to unlock the next. Ready to pick your episode?";
        [TextArea] public string episodePrompt = "Which episode will it be?";
        [TextArea] public string lockedEpisode = "That episode is still locked — clear the earlier ones first!";
        [TextArea] public string coatPrompt = "Safety first! Put on your lab coat at the locker before we begin.";
        [TextArea] public string readyPrompt = "All geared up! Are you prepared to begin?";
        [TextArea] public string thresholdWarn = "The period will start as soon as you walk in. Step through when you're ready!";
        [TextArea] public string congrats = "Congratulations! You handled that experiment brilliantly. Let's head back outside.";
        [TextArea] public string supplyWarn = "Oh no — there isn't enough reagent left to finish the experiment. We'll have to restart the period.";
    }

    [Header("Wiring")]
    [SerializeField] private NPCNarrationController narration;
    [SerializeField] private ChoicePanelController panel;
    [SerializeField] private PPEController ppe;
    [SerializeField] private ExperimentLauncher launcher;
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private GameObject doorBlocker;      // legacy holo barrier (optional)
    [SerializeField] private DoorOpener doorOpener;       // the real hinged lab door

    [Header("Return loop")]
    [SerializeField] private Transform frontDoorSpawn;    // where the player lands after passing
    [SerializeField] private Transform playerRig;         // XR Origin root
    [SerializeField] private Camera cameraOverride;       // falls back to Camera.main
    [SerializeField] private HudRigController hudRig;     // snapped after teleports
    [SerializeField] private ReagentSupplyMonitor supplyMonitor;   // insufficiency detector

    [Header("Dialogue")]
    [SerializeField] private GateLines lines = new GateLines();
    [SerializeField] private float lineSeconds = 4f;

    public GatekeeperModel Model { get; } = new GatekeeperModel();

    private readonly System.Collections.Generic.HashSet<string> _unlockedAtStart
        = new System.Collections.Generic.HashSet<string>();

    /// Periods in door-picker row order (matches GatekeeperModel.EpisodeOptions).
    public static readonly ExperimentPeriod[] EpisodeRows =
        (ExperimentPeriod[])Enum.GetValues(typeof(ExperimentPeriod));

    private bool _subscribed;

    private void OnEnable()
    {
        Subscribe();
        ApplyDoor(Model.State);
        if (panel != null) panel.Hide();
    }

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed) return;
        Model.Transition += OnTransition;
        if (panel != null) panel.OptionChosen += OnPanelOption;
        if (ppe != null) ppe.PPEWornChanged += OnPPEWorn;
        if (supplyMonitor != null) supplyMonitor.SupplyExhausted += OnSupplyExhausted;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        Model.Transition -= OnTransition;
        if (panel != null) panel.OptionChosen -= OnPanelOption;
        if (ppe != null) ppe.PPEWornChanged -= OnPPEWorn;
        if (supplyMonitor != null) supplyMonitor.SupplyExhausted -= OnSupplyExhausted;
        _subscribed = false;
    }

    // ---- scene entry points (trigger relays, PPE, panel) -------------------

    /// DoorApproachTrigger → the gate conversation (only from Blocked — walking
    /// through an OPEN door must never slam it shut again).
    public void OnApproachTriggerEntered() => Model.Fire(GateEvent.Approach);

    /// Poking Pharmee re-opens the conversation (e.g. to end a Lab Tour).
    public void OnPharmeeTalk() => Model.Fire(GateEvent.TalkRequested);

    /// LabThresholdTrigger → the period starts the moment the player walks in.
    public void OnThresholdTriggerEntered()
    {
        if (Model.State == GateState.DoorArmed) Model.Fire(GateEvent.CrossedThreshold);
    }

    public void OnPPEWorn()
    {
        if (Model.State == GateState.CoatPrompt) Model.Fire(GateEvent.Coated);
    }

    /// Door panel option pressed — meaning depends on the current state.
    public void OnPanelOption(int index)
    {
        switch (Model.State)
        {
            case GateState.ModeChoice:
                Model.Fire(index == 0 ? GateEvent.PickLabTour : GateEvent.PickCampaign);
                break;
            case GateState.CampaignExplain:
                Model.Fire(GateEvent.ExplainDone);
                break;
            case GateState.EpisodePick:
                if (index >= 0 && index < EpisodeRows.Length) ChooseEpisode(EpisodeRows[index]);
                break;
            case GateState.CoatPrompt:
                Model.Fire(GateEvent.Dismiss);
                break;
            case GateState.ReadyPrompt:
                Model.Fire(index == 0 ? GateEvent.Ready : GateEvent.Dismiss);
                break;
            case GateState.ThresholdWarn:
                Model.Fire(index == 0 ? GateEvent.ProceedConfirmed : GateEvent.Dismiss);
                break;
            case GateState.SupplyPrompt:
                if (index == 0)
                {
                    // Penalty: the starved attempt is recorded as failed (grade screen
                    // suppressed), the player returns to the door, the stage reloads.
                    if (runner != null && runner.IsRunning)
                    {
                        runner.Finish(0f);
                        var grade = UnityEngine.Object.FindFirstObjectByType<GradeScreenController>(FindObjectsInactive.Include);
                        if (grade != null) grade.Hide();
                    }
                    TeleportToFrontDoor();
                    Model.Fire(GateEvent.RestartConfirmed);
                }
                else Model.Fire(GateEvent.Dismiss);
                break;
        }
    }

    private void ChooseEpisode(ExperimentPeriod period)
    {
        var svc = new ProgressionService();
        svc.Load();
        var flow = new ProgressionFlow(svc);
        bool ok = Model.ChooseEpisode(period,
            id => HubSelectController.CanSelect(flow, id),
            p => GatekeeperModel.FirstPlayableInPeriod(flow, p));
        if (!ok) Say(lines.lockedEpisode);
    }

    // ---- state application --------------------------------------------------

    private void OnTransition(GateState from, GateState to)
    {
        ApplyDoor(to);
        switch (to)
        {
            case GateState.Blocked:
                panel?.Hide();
                break;

            case GateState.ModeChoice:
                Say(lines.approach);
                panel?.Show("What would you like to do?", new List<string> { "Lab Tour", "Campaign" });
                break;

            case GateState.LabTour:
                panel?.Hide();
                Say(lines.labTour);
                launcher?.Launch(GameFlow.SelectedModuleId, LaunchMode.StageOnly);
                break;

            case GateState.CampaignExplain:
                Say(lines.campaignExplain);
                panel?.ShowMessage("Campaign: clear each period's experiments — 90% or better to advance.", "Continue");
                break;

            case GateState.EpisodePick:
                ShowEpisodePicker();
                break;

            case GateState.CoatPrompt:
                if (ppe != null && ppe.PPEWorn) { Model.Fire(GateEvent.Coated); return; }
                Say(lines.coatPrompt);
                panel?.Show("Wear your lab coat from the locker beside you.", new List<string> { "Back" });
                break;

            case GateState.ReadyPrompt:
                Say(lines.readyPrompt);
                panel?.Show("Are you prepared to begin?", new List<string> { "I'm ready", "Not yet" });
                break;

            case GateState.Loading:
                panel?.Hide();
                LoadSelected();
                break;

            case GateState.ThresholdWarn:
                Say(lines.thresholdWarn);
                panel?.Show("The period will start as soon as you walk in.", new List<string> { "Proceed", "Not yet" });
                break;

            case GateState.DoorArmed:
                panel?.Hide();
                break;

            case GateState.Running:
                panel?.Hide();
                SnapshotUnlocks();
                runner?.StartRun();          // the walk-in timer start
                break;

            case GateState.Debrief:
                panel?.Hide();
                Say(lines.congrats);
                After(lineSeconds, () => Model.Fire(GateEvent.DebriefDone));
                break;

            case GateState.Returning:
                if (ScreenFader.Instance != null && Application.isPlaying)
                    ScreenFader.Instance.FadeAround(() => { TeleportToFrontDoor(); Model.Fire(GateEvent.TeleportDone); });
                else { TeleportToFrontDoor(); Model.Fire(GateEvent.TeleportDone); }
                break;

            case GateState.UnlockAnnounce:
                AnnounceUnlocks();
                After(lineSeconds + 1f, () => Model.Fire(GateEvent.AnnounceDone));
                break;

            case GateState.SupplyPrompt:
                Say(lines.supplyWarn);
                panel?.Show("Not enough reagents left to finish. Restart the period?",
                    new List<string> { "Restart period", "Keep trying" });
                break;
        }
    }

    /// ReagentSupplyMonitor: a required pour-step can no longer be satisfied.
    public void OnSupplyExhausted(List<string> starvedTaskIds) => Model.Fire(GateEvent.SupplyExhausted);

    // ---- return loop ---------------------------------------------------------

    /// GradeScreen Continue (pass-gated) — begin the debrief + return home.
    public void OnContinueAfterPass() => Model.Fire(GateEvent.ContinueAfterPass);

    /// GradeScreen Retry — rebuild the whole stage (props re-seated, bottles
    /// refilled) and start a fresh attempt in place.
    public void OnRetryRequested()
    {
        var mod = runner != null ? runner.Module : null;
        string id = mod != null ? mod.moduleId : GameFlow.SelectedModuleId;
        ExperimentStationRegistry.Clear();
        if (ScreenFader.Instance != null && Application.isPlaying)
            ScreenFader.Instance.FadeAround(() => launcher?.Launch(id, LaunchMode.FullStart));
        else
            launcher?.Launch(id, LaunchMode.FullStart);
    }

    private void SnapshotUnlocks()
    {
        _unlockedAtStart.Clear();
        var svc = new ProgressionService();
        svc.Load();
        foreach (var id in UnlockDiff.UnlockedSet(new ProgressionFlow(svc)))
            _unlockedAtStart.Add(id);
    }

    private void TeleportToFrontDoor()
    {
        if (frontDoorSpawn == null || playerRig == null) return;
        var cam = cameraOverride != null ? cameraOverride : Camera.main;
        Vector3 camPos = cam != null ? cam.transform.position : playerRig.position;
        float camYaw = cam != null ? cam.transform.eulerAngles.y : playerRig.eulerAngles.y;
        float markerYaw = frontDoorSpawn.eulerAngles.y;
        float rigYaw = playerRig.eulerAngles.y;
        float deltaYaw = Mathf.DeltaAngle(camYaw, markerYaw);
        Vector3 newPos = TeleportMath.RigPositionFor(frontDoorSpawn.position, deltaYaw, playerRig.position, camPos);
        playerRig.SetPositionAndRotation(newPos,
            Quaternion.Euler(0f, TeleportMath.RigYawFor(markerYaw, rigYaw, camYaw), 0f));
        if (hudRig != null) hudRig.SnapToCamera();
    }

    private void AnnounceUnlocks()
    {
        var svc = new ProgressionService();
        svc.Load();
        var newly = UnlockDiff.NewlyUnlocked(_unlockedAtStart, new ProgressionFlow(svc));
        Say(UnlockDiff.AnnouncementFor(newly));
    }

    /// Runtime: delayed action; edit mode/tests: immediate (deterministic).
    private void After(float seconds, Action act)
    {
        if (Application.isPlaying && isActiveAndEnabled) StartCoroutine(AfterRoutine(seconds, act));
        else act();
    }

    private System.Collections.IEnumerator AfterRoutine(float seconds, Action act)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));
        act();
    }

    private void ShowEpisodePicker()
    {
        var svc = new ProgressionService();
        svc.Load();
        var flow = new ProgressionFlow(svc);
        GatekeeperModel.EpisodeOptions(flow, out var labels, out var selectable);
        Say(lines.episodePrompt);
        panel?.Show("Choose your episode", labels, selectable);
    }

    private void LoadSelected()
    {
        string id = Model.SelectedModuleId;
        if (string.IsNullOrEmpty(id)) id = GameFlow.SelectedModuleId;
        GameFlow.Select(id);
        if (ScreenFader.Instance != null && Application.isPlaying)
            ScreenFader.Instance.FadeAround(() => DoLoad(id));
        else
            DoLoad(id);
    }

    private void DoLoad(string id)
    {
        launcher?.Launch(id, LaunchMode.PrepareArmed);   // stage rebuilt + armed, clock held
        Model.Fire(GateEvent.Loaded);
    }

    private void ApplyDoor(GateState s)
    {
        bool open = GatekeeperModel.DoorOpen(s);
        if (doorBlocker != null) doorBlocker.SetActive(!open);
        if (doorOpener != null) doorOpener.SetOpen(open);
    }

    private void Say(string line)
    {
        if (narration != null && !string.IsNullOrEmpty(line)) narration.Say(line, lineSeconds);
    }

    /// Edit-mode/test binding (OnEnable does not fire on AddComponent in edit mode,
    /// so this also performs the event subscription).
    public void Bind(NPCNarrationController n, ChoicePanelController p, PPEController ppeCtrl,
                     ExperimentLauncher l, ExperimentRunner r, GameObject blocker)
    {
        Unsubscribe();
        narration = n; panel = p; ppe = ppeCtrl; launcher = l; runner = r; doorBlocker = blocker;
        Subscribe();
        ApplyDoor(Model.State);
    }

    /// Edit-mode/test binding for the return loop refs.
    public void BindReturn(Transform frontDoor, Transform rig, Camera cam, HudRigController hud)
    {
        frontDoorSpawn = frontDoor; playerRig = rig; cameraOverride = cam; hudRig = hud;
    }
}
