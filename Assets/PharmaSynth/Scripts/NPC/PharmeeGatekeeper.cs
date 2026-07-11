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
        [TextArea] public string coatPrompt = "Safety first! Gear up at the locker — lab coat, goggles, AND gloves — before we begin.";
        [TextArea] public string readyPrompt = "All geared up! Are you prepared to begin?";
        [TextArea] public string thresholdWarn = "The period will start as soon as you walk in. Step through when you're ready!";
        [TextArea] public string congrats = "Congratulations! You handled that experiment brilliantly. Let's head back outside.";
        [TextArea] public string supplyWarn = "Oh no — there isn't enough reagent left to finish the experiment. We'll have to restart the period.";
        [TextArea] public string welcome = "Welcome to the lab! I'm Pharmee. Come talk to me at the door whenever you're ready to begin.";
    }

    [Header("Wiring")]
    [SerializeField] private NPCNarrationController narration;
    [SerializeField] private ChoicePanelController panel;
    [SerializeField] private PPEController ppe;
    [SerializeField] private ExperimentLauncher launcher;
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private GameObject doorBlocker;      // legacy holo barrier (optional)
    [SerializeField] private DoorOpener doorOpener;       // the real hinged lab door
    [SerializeField] private MonoBehaviour faceBehaviour; // optional IPharmeeFace (expressions)
    [SerializeField] private LabTourGuide tourGuide;      // location-triggered lab tour (optional)

    [Header("Return loop")]
    [SerializeField] private Transform frontDoorSpawn;    // where the player lands after passing
    [SerializeField] private Transform playerRig;         // XR Origin root
    [SerializeField] private Camera cameraOverride;       // falls back to Camera.main
    [SerializeField] private HudRigController hudRig;     // snapped after teleports
    [SerializeField] private ReagentSupplyMonitor supplyMonitor;   // insufficiency detector

    [Header("Review corner (post-experiment quiz flow, 2026-07-11)")]
    [SerializeField] private PostLabController postLab;   // the quiz tablet (autoOpen off in-scene)
    [SerializeField] private Transform reviewCornerSpawn; // lands the player facing Jimenez + tablet
    [SerializeField] private ExaminerNPC examiner;        // Dr. Jimenez's voice channel

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
    private float _baseLineSeconds = -1f;
    private int _tourIndex;
    private int _remarkVariant;
    private bool _returnOnLoad;                 // retry-from-review: teleport home inside the load fade
    private ExperimentResult? _lastResult;      // cached at ExperimentFinished for the review remarks

    /// Comfort seam: subtitle pacing multiplier (see PharmeeBrain.SetSubtitlePace).
    public void SetSubtitlePace(float speed)
    {
        if (_baseLineSeconds < 0f) _baseLineSeconds = lineSeconds;
        lineSeconds = ComfortMath.LineSecondsFor(_baseLineSeconds, speed);
    }

    private void OnEnable()
    {
        Subscribe();
        ApplyDoor(Model.State);
        if (panel != null) panel.Hide();
    }

    private void OnDisable() => Unsubscribe();

    /// Greet the player the moment they teleport into the lab (from the cube spawn
    /// room), just after the screen fade-in settles.
    private void Start()
    {
        if (Application.isPlaying)
        {
            // Menu → lab entry does no runtime placement, so the player would sit
            // at the authored rig transform (~0.7 m behind the entrance). Route the
            // initial spawn through the SAME target/code path Restart uses, so
            // "walk in from the cube room" and "press Restart" land identically —
            // but only AFTER the fixed-height calibration has sized the capsule
            // (teleporting into the doorway while it is still un-sized makes physics
            // depenetration shove the rig onto the roof). The player rests at the
            // safe authored spawn during the brief calibration window.
            if (isActiveAndEnabled) StartCoroutine(SpawnAtEntranceRoutine());
            else { TeleportToFrontDoor(); After(0.6f, SpeakWelcome); }
        }
    }

    /// Wait for the rig's height calibration to settle (or a safety timeout), then
    /// land the player at the entrance exactly where Restart does.
    private System.Collections.IEnumerator SpawnAtEntranceRoutine()
    {
        var boost = playerRig != null ? playerRig.GetComponent<SeatedHeightBoost>() : null;
        float t = 0f;
        while (boost != null && !boost.Calibrated && t < 3f)
        {
            t += Time.deltaTime;
            yield return null;
        }
        TeleportToFrontDoor();
        After(0.6f, SpeakWelcome);
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        Model.Transition += OnTransition;
        if (panel != null) panel.OptionChosen += OnPanelOption;
        if (ppe != null) ppe.PPEWornChanged += OnPPEWorn;
        if (supplyMonitor != null) supplyMonitor.SupplyExhausted += OnSupplyExhausted;
        if (runner != null)
        {
            runner.PhaseCompleted += OnRunnerPhase;
            runner.ExperimentFinished += OnRunnerFinished;
            runner.ExperimentStarted += OnRunnerStarted;
        }
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        Model.Transition -= OnTransition;
        if (panel != null) panel.OptionChosen -= OnPanelOption;
        if (ppe != null) ppe.PPEWornChanged -= OnPPEWorn;
        if (supplyMonitor != null) supplyMonitor.SupplyExhausted -= OnSupplyExhausted;
        if (runner != null)
        {
            runner.PhaseCompleted -= OnRunnerPhase;
            runner.ExperimentFinished -= OnRunnerFinished;
            runner.ExperimentStarted -= OnRunnerStarted;
        }
        _subscribed = false;
    }

    // ---- post-experiment review flow (user 2026-07-11) ----------------------

    private void OnRunnerStarted(ExperimentModuleDefinition m) => _lastResult = null;

    /// ChemicalTests complete while Running → begin the review flow. Modules
    /// without a ChemicalTests phase route off their Synthesis completion instead.
    private void OnRunnerPhase(TaskPhase p)
    {
        if (Model.State != GateState.Running) return;
        if (p == TaskPhase.ChemicalTests) { Model.Fire(GateEvent.TestsDone); return; }
        if (p == TaskPhase.Synthesis && runner != null && runner.Graph != null
            && !runner.Graph.HasPhase(TaskPhase.ChemicalTests))
            Model.Fire(GateEvent.TestsDone);
    }

    /// Quiz submitted (or a dev/legacy Finish) → the result is in. From QuizTime
    /// this lands in ScoreReview; a finish that arrives while still Running (the
    /// DevExperimentDriver F key) cascades through the quiz states, whose entries
    /// short-circuit when the result already exists.
    private void OnRunnerFinished(ExperimentResult r)
    {
        _lastResult = r;
        if (Model.State == GateState.QuizTime) Model.Fire(GateEvent.Graded);
        else if (Model.State == GateState.Running) Model.Fire(GateEvent.TestsDone);
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
        if (Model.State != GateState.CoatPrompt) return;
        if (ppe == null || ppe.PPEWorn) { Model.Fire(GateEvent.Coated); return; }
        // Partially dressed — tell the player what's still missing (per-piece PPE).
        Say("Almost there — you still need your " + ppe.MissingSummary() + ".");
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
        var flow = ProgressionFlow.Create(svc);
        bool ok = Model.ChooseEpisode(period,
            id => HubSelectController.CanSelect(flow, id),
            p => GatekeeperModel.FirstPlayableInPeriod(flow, p));
        if (!ok) Say(lines.lockedEpisode);
    }

    // ---- state application --------------------------------------------------

    /// Guided lab tour (storyboard): Pharmee narrates each area in sequence, one
    /// beat auto-advancing to the next, instead of the old single free-roam line.
    /// Stops the moment the player leaves the tour (poke → ModeChoice).
    private void StartLabTour() { _tourIndex = 0; SpeakTourBeat(); }

    private void SpeakTourBeat()
    {
        if (Model.State != GateState.LabTour) return;             // player ended the tour
        if (_tourIndex >= PharmeeLines.TourBeats.Length) return;  // tour finished; free roam
        Say(PharmeeLines.TourBeats[_tourIndex]);
        _tourIndex++;
        After(lineSeconds + 1.5f, SpeakTourBeat);                 // gentle auto-advance
    }

    private void OnTransition(GateState from, GateState to)
    {
        if (from == GateState.LabTour && to != GateState.LabTour) tourGuide?.End();
        ApplyDoor(to);
        // Face mood tracks the conversation (PharmeeMood resets to happy after lines).
        (faceBehaviour as IPharmeeFace)?.SetExpression(PharmeeMood.ExpressionForGate(to));
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
                launcher?.Launch(GameFlow.SelectedModuleId, LaunchMode.StageOnly);
                // Location-triggered tour if a guide is wired + its landmarks resolve;
                // otherwise the timed narrated sequence (storyboard). Poke to end either.
                if (tourGuide == null || tourGuide.Begin(s => Say(s)) == 0) StartLabTour();
                break;

            case GateState.CampaignExplain:
                Say(lines.campaignExplain);
                panel?.ShowMessage("Campaign: clear each period's experiments — 90% or better to advance.", "Continue");
                break;

            case GateState.EpisodePick:
                ShowEpisodePicker();
                break;

            case GateState.CoatPrompt:
                // Put the wearables back on their pegs + strip any stale worn PPE, so the
                // locker is always dressable here (the coat had gone missing after a lab-
                // tour restart). This makes PPEWorn false, so the player re-dons cleanly.
                if (WearableReseat.Instance != null) WearableReseat.Instance.Reseat();
                if (ppe != null && ppe.PPEWorn) { Model.Fire(GateEvent.Coated); return; }
                Say(lines.coatPrompt);
                panel?.Show("Wear the lab coat, goggles and gloves from the locker beside you.", new List<string> { "Back" });
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
                // The PPE locker sits just INSIDE the lab (2026-07-11), so the
                // player may already be past the threshold when the timer arms —
                // the walk-in trigger would never fire. Auto-start in that case.
                After(0.8f, () =>
                {
                    if (Model.State == GateState.DoorArmed && PlayerInsideLab())
                        Model.Fire(GateEvent.CrossedThreshold);
                });
                break;

            case GateState.Running:
                panel?.Hide();
                SnapshotUnlocks();
                runner?.StartRun();          // the walk-in timer start
                break;

            case GateState.QuizIntro:
                panel?.Hide();
                // Dev/legacy finish already graded everything — skip the staging.
                if (_lastResult.HasValue) { Model.Fire(GateEvent.QuizBegin); break; }
                runner?.FreezeClock();       // the review never costs Time-Management score
                Say(PharmeeLines.Pick(PharmeeLines.TestsDoneLines, _remarkVariant++));
                After(lineSeconds * 0.9f, () =>
                {
                    if (Model.State != GateState.QuizIntro) return;
                    DoFaded(() => TeleportTo(reviewCornerSpawn));
                    After(0.9f, () => SpeakJimenezBrief(0));
                });
                break;

            case GateState.QuizTime:
                panel?.Hide();
                if (_lastResult.HasValue) { Model.Fire(GateEvent.Graded); break; }
                postLab?.Open();             // never score-gated (manuscript)
                break;

            case GateState.ScoreReview:
                panel?.Hide();
                // The grade screen + Success/Failure outro fire off ExperimentFinished
                // on their own; once the outro ends, Jimenez + Pharmee speak the remarks.
                After(0.8f, WaitOutroThenRemarks);
                break;

            case GateState.Returning:
                // One fade: full lab reset (props re-seated, wearables back on their
                // pegs) + teleport home — the user-approved return sequence.
                DoFaded(() => { ResetLabForReturn(); Model.Fire(GateEvent.TeleportDone); });
                break;

            case GateState.Debrief:
                // Now AT the entrance, after the return teleport: quiz-completion
                // congrats + a banded performance remark, then the unlock announce.
                panel?.Hide();
                Say(PharmeeLines.Pick(PharmeeLines.DebriefCongrats, _remarkVariant++) + " "
                    + PharmeeLines.DebriefRemark(_lastResult.HasValue ? _lastResult.Value.grade.Total : 100f));
                After(lineSeconds + 1f, () => Model.Fire(GateEvent.DebriefDone));
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

    /// Pharmee's spawn/entrance greeting (scene load + after a HUD reset).
    public void SpeakWelcome()
    {
        Say(lines.welcome);
        AudioService.TryPlay("pharmee-greet");
    }

    /// HUD Reset (user 2026-07-10): everything in the lab returns to its original
    /// spawn — props/bottles re-seated, the wearables taken off — the player is
    /// teleported back to the entrance where Pharmee waits, the gate re-closes, and
    /// Pharmee greets. Wrapped in the black fade like every other transition.
    public void ResetToEntrance()
    {
        void Do()
        {
            string id = runner != null && runner.Module != null ? runner.Module.moduleId : GameFlow.SelectedModuleId;
            ExperimentStationRegistry.Clear();
            launcher?.Launch(id, LaunchMode.StageOnly);   // props/bottles back to original spawns
            // Wearables back on their pegs (+ worn PPE stripped) so the next campaign is dressable.
            if (WearableReseat.Instance != null) WearableReseat.Instance.Reseat();
            else if (ppe != null) ppe.RemovePPE();        // wearables off
            TeleportToFrontDoor();                         // rig back to the entrance by Pharmee
            Model.ResetToBlocked();                        // door re-closes; player must re-approach
            SpeakWelcome();
        }
        if (ScreenFader.Instance != null && Application.isPlaying)
            ScreenFader.Instance.FadeAround(Do);
        else
            Do();
    }

    /// GradeScreen Retry. From the review corner (the normal fail path) this is a
    /// CLEAN re-armed attempt: back to the door, stage rebuilt, walk-in timer —
    /// consistent with the supply-restart path. Outside the review flow (dev
    /// shortcuts) it keeps the legacy in-place FullStart.
    public void OnRetryRequested()
    {
        var grade = UnityEngine.Object.FindFirstObjectByType<GradeScreenController>(FindObjectsInactive.Include);
        if (grade != null) grade.Hide();
        if (Model.State == GateState.ScoreReview)
        {
            _returnOnLoad = true;                        // teleport home inside the load fade
            Model.Fire(GateEvent.RetryRequested);        // → Loading → armed at the door
            return;
        }
        var mod = runner != null ? runner.Module : null;
        string id = mod != null ? mod.moduleId : GameFlow.SelectedModuleId;
        ExperimentStationRegistry.Clear();
        if (ScreenFader.Instance != null && Application.isPlaying)
            ScreenFader.Instance.FadeAround(() => launcher?.Launch(id, LaunchMode.FullStart));
        else
            launcher?.Launch(id, LaunchMode.FullStart);
    }

    /// Jimenez's two-beat quiz briefing at the review corner, then the quiz opens.
    private void SpeakJimenezBrief(int beat)
    {
        if (Model.State != GateState.QuizIntro) return;
        if (beat >= 2) { Model.Fire(GateEvent.QuizBegin); return; }
        if (examiner != null)
            examiner.SpeakLine(PharmeeLines.Pick(PharmeeLines.JimenezQuizBrief, beat));
        After(lineSeconds, () => SpeakJimenezBrief(beat + 1));
    }

    /// Hold the spoken score remarks until the Success/Failure outro cutscene ends.
    private void WaitOutroThenRemarks()
    {
        if (Model.State != GateState.ScoreReview) return;
        var director = UnityEngine.Object.FindFirstObjectByType<CutsceneDirector>(FindObjectsInactive.Include);
        if (Application.isPlaying && director != null && director.IsPlaying && director.onCutsceneFinished != null)
        {
            UnityEngine.Events.UnityAction handler = null;
            handler = () =>
            {
                director.onCutsceneFinished.RemoveListener(handler);
                SpeakScoreRemarks();
            };
            director.onCutsceneFinished.AddListener(handler);
        }
        else SpeakScoreRemarks();
    }

    /// Jimenez gives the verdict, Pharmee follows up — numbers stay on the grade
    /// card so every spoken line comes from a finite (voice-recordable) pool.
    private void SpeakScoreRemarks()
    {
        if (Model.State != GateState.ScoreReview) return;
        bool passed = _lastResult.HasValue && _lastResult.Value.passed;
        if (examiner != null)
            examiner.SpeakLine(PharmeeLines.Pick(
                passed ? PharmeeLines.JimenezPassRemarks : PharmeeLines.JimenezFailRemarks, _remarkVariant++));
        After(lineSeconds + 0.5f, () =>
        {
            if (Model.State != GateState.ScoreReview) return;
            Say(PharmeeLines.Pick(passed ? PharmeeLines.Celebrate : PharmeeLines.Encourage, _remarkVariant++));
        });
    }

    /// The user-approved return: props/bottles re-seated, wearables back on their
    /// pegs, player at the entrance — all inside the Returning fade.
    private void ResetLabForReturn()
    {
        string id = runner != null && runner.Module != null ? runner.Module.moduleId : GameFlow.SelectedModuleId;
        ExperimentStationRegistry.Clear();
        launcher?.Launch(id, LaunchMode.StageOnly);
        if (WearableReseat.Instance != null) WearableReseat.Instance.Reseat();
        else if (ppe != null) ppe.RemovePPE();
        TeleportToFrontDoor();
    }

    /// Whether the player's head is already inside the lab proper (past the
    /// front-wall plane at z≈−0.2, i.e. deeper than the entrance strip).
    private bool PlayerInsideLab()
    {
        var cam = cameraOverride != null ? cameraOverride : Camera.main;
        Transform head = cam != null ? cam.transform : playerRig;
        return head != null && head.position.z < -0.45f;
    }

    /// Fade wrapper with the edit-mode/test fallback (immediate).
    private void DoFaded(Action mid)
    {
        if (ScreenFader.Instance != null && Application.isPlaying)
            ScreenFader.Instance.FadeAround(mid);
        else
            mid();
    }

    private void SnapshotUnlocks()
    {
        _unlockedAtStart.Clear();
        var svc = new ProgressionService();
        svc.Load();
        foreach (var id in UnlockDiff.UnlockedSet(ProgressionFlow.Create(svc)))
            _unlockedAtStart.Add(id);
    }

    private void TeleportToFrontDoor() => TeleportTo(frontDoorSpawn);

    /// Move the rig so the CAMERA lands on the marker, facing the marker's yaw.
    private void TeleportTo(Transform marker)
    {
        if (marker == null || playerRig == null) return;
        var cam = cameraOverride != null ? cameraOverride : Camera.main;
        Vector3 camPos = cam != null ? cam.transform.position : playerRig.position;
        float camYaw = cam != null ? cam.transform.eulerAngles.y : playerRig.eulerAngles.y;
        float markerYaw = marker.eulerAngles.y;
        float rigYaw = playerRig.eulerAngles.y;
        float deltaYaw = Mathf.DeltaAngle(camYaw, markerYaw);
        Vector3 newPos = TeleportMath.RigPositionFor(marker.position, deltaYaw, playerRig.position, camPos);
        playerRig.SetPositionAndRotation(newPos,
            Quaternion.Euler(0f, TeleportMath.RigYawFor(markerYaw, rigYaw, camYaw), 0f));
        if (hudRig != null) hudRig.SnapToCamera();
        if (SpawnBurstFX.Instance != null) SpawnBurstFX.Instance.PlayAtPlayer();   // cyan materialize
    }

    private void AnnounceUnlocks()
    {
        var svc = new ProgressionService();
        svc.Load();
        var newly = UnlockDiff.NewlyUnlocked(_unlockedAtStart, ProgressionFlow.Create(svc));
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
        var flow = ProgressionFlow.Create(svc);
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
        if (_returnOnLoad) { _returnOnLoad = false; TeleportToFrontDoor(); }   // retry-from-review
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

    /// Edit-mode/test binding for the review-corner quiz flow refs.
    public void BindQuiz(PostLabController quiz, Transform reviewSpawn, ExaminerNPC ex)
    {
        postLab = quiz; reviewCornerSpawn = reviewSpawn; examiner = ex;
    }
}
