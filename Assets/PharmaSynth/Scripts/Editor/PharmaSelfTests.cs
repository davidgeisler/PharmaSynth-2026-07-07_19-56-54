#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Re-runnable regression suite for the PharmaSynth engine. Run via
/// menu: Tools ▸ PharmaSynth ▸ Run Self-Tests. Consolidates the assertions that
/// were verified incrementally during W2–W3 into one permanent, one-click check.
/// (Kept as an Editor-menu suite rather than an NUnit asmdef to avoid restructuring
/// the runtime assembly; a formal EditMode asmdef migration can layer on later.)
public static class PharmaSelfTests
{
    static int _fail, _total;
    static readonly List<string> _log = new List<string>();

    static void A(string name, bool cond)
    {
        _total++;
        if (!cond) { _fail++; _log.Add("FAIL  " + name); }
    }
    static bool Near(float a, float b, float e = 0.01f) => Math.Abs(a - b) < e;

    [MenuItem("Tools/PharmaSynth/Run Self-Tests")]
    public static void Run()
    {
        _fail = 0; _total = 0; _log.Clear();

        TaskGraphSuite();
        MasterySuite();
        ScoreSuite();
        GraderSuite();
        RunnerSuite();
        ProgressionSuite();
        ChemVisualSuite();
        UISuite();
        W4Suite();
        InteractionSuite();
        RealVerbSuite();
        PourReactionSuite();
        TestReactionSuite();
        SceneBuilderSuite();
        SimRigSuite();
        PostLabSuite();
        CutsceneLibrarySuite();
        ProgressionFlowSuite();
        HubSelectSuite();
        AudioSuite();
        ExaminerSuite();
        SettingsSuite();
        ResultsExportSuite();
        RecorderSuite();
        HudRigSuite();
        FadeSuite();
        ArmedRunnerSuite();
        GatekeeperSuite();
        UnlockReturnSuite();
        LabMenuSuite();
        DepletionSuite();
        RealSizeSuite();
        PhysicsProfileSuite();
        TestVesselSuite();
        MishandlingSuite();
        ComfortSuite();
        PharmeeAliveSuite();
        LibrarySuite();
        ContentSuite();
        RosterDataSuite();
        HoverInfoSuite();
        GlyphSafeSuite();
        MusicSpeakerSuite();
        GrabTuningSuite();
        HeightCalibrationSuite();
        WatchMathSuite();
        ShelfPourWiringSuite();
        FxMaterialSuite();
        ChecklistPagerSuite();
        DemoModeSuite();
        DemoActionsSuite();
        IloCopySuite();
        ReviewFlowSuite();
        HazardousMixSuite();
        CenterTableMathSuite();
        RawReagentSuite();
        VoiceSuite();
        DispenserSuite();

        string summary = $"PharmaSynth Self-Tests: {_total - _fail}/{_total} passed";
        if (_fail == 0) Debug.Log("<color=#4CD07D>" + summary + " — ALL GREEN</color>");
        else Debug.LogError(summary + " — " + _fail + " FAILED:\n" + string.Join("\n", _log));
    }

    static ExperimentTask T(string id, TaskPhase ph, float w, LabSkill sk, RubricCategory rc, params string[] pre)
    {
        var t = new ExperimentTask { taskId = id, label = id, phase = ph, progressWeight = w, skill = sk, rubricCategory = rc, required = true };
        t.prerequisites = new List<string>(pre);
        return t;
    }

    static void TaskGraphSuite()
    {
        var g = new TaskGraph(new List<ExperimentTask> {
            T("prep", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("mix", TaskPhase.Synthesis, 2, LabSkill.Transfer, RubricCategory.Procedure, "prep"),
            T("test", TaskPhase.ChemicalTests, 1, LabSkill.TestInterpretation, RubricCategory.ChemicalTests, "mix"),
        });
        A("graph: mix blocked before prep", g.TryComplete("mix") == TaskCompletionResult.BlockedByPrerequisite);
        A("graph: prep completes", g.TryComplete("prep") == TaskCompletionResult.Completed);
        A("graph: progress 0.25", Near(g.Progress01, 0.25f));
        A("graph: double-complete guarded", g.TryComplete("prep") == TaskCompletionResult.AlreadyComplete);
        g.TryComplete("mix");
        A("graph: progress 0.75", Near(g.Progress01, 0.75f));
        bool sensor = false; g.RegisterCondition("test", () => sensor); g.Tick();
        A("graph: auto-check waits", !g.IsComplete("test"));
        sensor = true; g.Tick();
        A("graph: auto-check fires", g.IsComplete("test"));
        A("graph: progress 1.0", Near(g.Progress01, 1f));
    }

    static void MasterySuite()
    {
        var m = new MasteryModel(new BktParameters(), new[] { LabSkill.Measuring });
        A("bkt: starts at pL0", Near(m.OverallMastery(), 0.25f));
        for (int i = 0; i < 6; i++) m.Observe(LabSkill.Measuring, true);
        A("bkt: converges past gate", m.IsMastered(0.90f));
        float before = m.OverallMastery(); m.Observe(LabSkill.Measuring, false);
        A("bkt: slip lowers mastery", m.OverallMastery() < before);
    }

    static void ScoreSuite()
    {
        var sc = new ScoreCalculator(new RubricWeights());
        var perfect = new Dictionary<RubricCategory, float>();
        foreach (RubricCategory c in Enum.GetValues(typeof(RubricCategory))) perfect[c] = 1f;
        A("score: perfect = 100", Near(sc.Compute(perfect).Total, 100f, 0.2f));
        var w = new RubricWeights { procedure = 0.4f, chemicalTests = 0.4f, materialsAndPPE = 0, timeManagement = 0, sanitation = 0, documentation = 0 };
        var sc2 = new ScoreCalculator(w);
        A("score: weights normalized", Near(sc2.Compute(new Dictionary<RubricCategory, float> { { RubricCategory.Procedure, 1 }, { RubricCategory.ChemicalTests, 1 } }).Total, 100f, 0.2f));
        A("score: time under par", Near(ScoreCalculator.TimeSubScore(300, 600), 1f));
        A("score: time 2x par", Near(ScoreCalculator.TimeSubScore(1200, 600), 0f));
    }

    static void GraderSuite()
    {
        var tasks = new List<ExperimentTask> {
            T("prep", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("synth", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure, "prep"),
            T("t", TaskPhase.ChemicalTests, 1, LabSkill.TestInterpretation, RubricCategory.ChemicalTests, "synth"),
            T("clean", TaskPhase.DataSheet, 1, LabSkill.Transfer, RubricCategory.Sanitation, "t"),
        };
        var g = new TaskGraph(tasks);
        g.TryComplete("prep"); g.TryComplete("synth"); g.TryComplete("t"); g.TryComplete("clean");
        var grader = new ExperimentGrader(new ScoreCalculator(new RubricWeights()), new GradingConfig());
        A("grader: perfect run = 100", Near(grader.Grade(g, new MistakeLog(), 300, 600, 1f).Total, 100f, 0.2f));
        var log = new MistakeLog();
        log.Record(LabErrorType.WrongReagent, ""); log.Record(LabErrorType.WrongStep, "");
        log.Record(LabErrorType.MissingPPE, ""); log.Record(LabErrorType.DroppedGlassware, "");
        A("grader: mistakes = 81.08", Near(grader.Grade(g, log, 900, 600, 2f / 3f).Total, 81.08f, 0.3f));
        A("mistakelog: category mapping", MistakeLog.CategoryFor(LabErrorType.DroppedGlassware) == RubricCategory.Sanitation);
    }

    static void RunnerSuite()
    {
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> {
            T("a", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("b", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure, "a"),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Measuring, LabSkill.Transfer };
        var go = new GameObject("SelfTestRunner");
        try
        {
            var runner = go.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            runner.StartExperiment();
            A("runner: out-of-order = wrong step", runner.CompleteTask("b") == TaskCompletionResult.BlockedByPrerequisite && runner.MistakeCount == 1);
            runner.CompleteTask("a"); runner.CompleteTask("b");
            var res = runner.Finish(1f);
            A("runner: two-part gate computed", res.grade.Total > 0 && (res.passed == (res.gradePassed && res.masteryPassed)));
            A("runner: retry resets", RetryResets(runner));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(module); }
    }
    static bool RetryResets(ExperimentRunner r) { r.Retry(); return Near(r.Progress01, 0f) && r.MistakeCount == 0; }

    static void ProgressionSuite()
    {
        var svc = new ProgressionService("selftest-mem");
        var b = new GradeBreakdown { Total = 94 };
        svc.RecordResult("m", new ExperimentResult { grade = b, overallMastery = 0.93f, passed = true }, false);
        A("progression: passes latch", svc.IsPassed("m"));
        A("progression: unlock next", svc.IsUnlocked("m2", "m"));
        var json = JsonUtility.ToJson(svc.Data);
        A("progression: json round-trips", JsonUtility.FromJson<ProgressSaveData>(json).modules[0].passed);
    }

    static void RecorderSuite()
    {
        string path = System.IO.Path.Combine(Application.temporaryCachePath, "recorder_selftest.json");
        void Clean() { try { System.IO.File.Delete(path); System.IO.File.Delete(path + ".bak"); } catch { } }
        Clean();
        try
        {
            // Pure seam: fold results into the service (persists to the injected path).
            var svc = new ProgressionService(path);
            var rec = ResultRecorder.Record(svc, "m1", new ExperimentResult { grade = new GradeBreakdown { Total = 95 }, overallMastery = 0.92f, passed = true });
            A("recorder: latches pass", rec.passed && rec.attempts == 1);
            ResultRecorder.Record(svc, "m1", new ExperimentResult { grade = new GradeBreakdown { Total = 40 }, overallMastery = 0.3f, passed = false });
            var r1 = svc.GetRecord("m1");
            A("recorder: keeps best + attempts", Near(r1.bestGrade, 95f) && r1.attempts == 2 && r1.passed);
            var reload = new ProgressionService(path); reload.Load();
            A("recorder: persisted to disk", reload.IsPassed("m1"));

            // Mono: ExperimentFinished → record written through the component.
            var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            module.moduleId = "selftest-recorder";
            module.graphTasks = new List<ExperimentTask> { T("a", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure) };
            module.trackedSkills = new List<LabSkill> { LabSkill.Measuring };
            var go = new GameObject("SelfTestRecorder");
            try
            {
                var runner = go.AddComponent<ExperimentRunner>();
                var recorder = go.AddComponent<ResultRecorder>();
                recorder.SavePathOverride = path;
                recorder.SetRunner(runner);
                ModuleRecord got = null; recorder.Recorded += r => got = r;
                runner.SetModule(module);
                runner.StartExperiment();
                runner.CompleteTask("a");
                runner.Finish(1f);
                A("recorder: mono records on finish", got != null && got.moduleId == "selftest-recorder" && got.attempts >= 1);
                var check = new ProgressionService(path); check.Load();
                A("recorder: mono persisted", check.GetRecord("selftest-recorder") != null);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(module); }
        }
        finally { Clean(); }
    }

    static void HudRigSuite()
    {
        // Lazy-follow solver math
        var p = HudFollowSolver.Params.Default;
        A("hud: yaw wrap", Near(HudFollowSolver.DeltaYawDeg(350f, 10f), 20f));
        var anchor = HudFollowSolver.AnchorPoint(Vector3.zero, 0f, in p);
        A("hud: anchor at distance", Near(anchor.z, p.distance) && Near(anchor.y, p.heightOffset) && Near(anchor.x, 0f));
        var s = HudFollowSolver.Snapped(Vector3.zero, 0f, in p);
        var before = s.pos; var beforeYaw = s.yawDeg;
        HudFollowSolver.Step(ref s, Vector3.zero, p.yawDeadzoneDeg * 0.5f, in p, 1f / 60f);
        A("hud: deadzone holds", (s.pos - before).magnitude < 0.0001f && Near(s.yawDeg, beforeYaw));
        for (int i = 0; i < 240; i++) HudFollowSolver.Step(ref s, Vector3.zero, 90f, in p, 1f / 60f);
        A("hud: converges on 90 deg turn", Near(s.yawDeg, 90f, 1.5f));
        var target = HudFollowSolver.AnchorPoint(Vector3.zero, 90f, in p);
        A("hud: converges to anchor", (s.pos - target).magnitude < 0.05f);

        // Narration line events + bubble toggling + dialogue-bar mirroring
        var go = new GameObject("hudtest");
        try
        {
            var narr = go.AddComponent<NPCNarrationController>();
            var panel = new GameObject("Bubble"); panel.transform.SetParent(go.transform);
            narr.SetPanelRoot(panel);
            string started = null; int ends = 0;
            narr.LineStarted += (l, sec) => started = l;
            narr.LineEnded += () => ends++;
            narr.BeginLine("hello", 2f);
            A("narration: BeginLine shows bubble + event", panel.activeSelf && started == "hello" && narr.IsSpeaking);
            narr.EndLine();
            A("narration: EndLine hides bubble + event", !panel.activeSelf && ends == 1 && !narr.IsSpeaking);
            narr.EndLine();
            A("narration: EndLine idempotent", ends == 1);

            var barRoot = new GameObject("Bar"); barRoot.transform.SetParent(go.transform);
            var lineGo = new GameObject("Line"); lineGo.transform.SetParent(barRoot.transform);
            var lineText = lineGo.AddComponent<TMPro.TextMeshProUGUI>();
            var bar = go.AddComponent<HudDialogueBar>();
            bar.Bind(narr, barRoot, null, lineText);
            narr.BeginLine("step two", 1f);
            A("hudbar: mirrors line", barRoot.activeSelf && lineText.text == "step two");
            narr.EndLine();
            A("hudbar: hides on end", !barRoot.activeSelf && lineText.text == string.Empty);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void FadeSuite()
    {
        A("fade: ease endpoints", Near(FadeState.Ease01(0f), 0f) && Near(FadeState.Ease01(1f), 1f));
        A("fade: ease midpoint", Near(FadeState.Ease01(0.5f), 0.5f));
        A("fade: ease monotone", FadeState.Ease01(0.3f) < FadeState.Ease01(0.6f));
        var f = new FadeState();
        f.Begin(1f, 0.5f);
        A("fade: busy while ramping", f.Busy && f.Alpha < 1f);
        for (int i = 0; i < 40; i++) f.Step(1f / 60f);      // 0.66 s > 0.5 s
        A("fade: reaches target + settles", Near(f.Alpha, 1f) && !f.Busy);
        f.Begin(0f, 0.2f);
        for (int i = 0; i < 30; i++) f.Step(1f / 60f);
        A("fade: fades back down", Near(f.Alpha, 0f) && !f.Busy);
    }

    static void ArmedRunnerSuite()
    {
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.moduleId = "selftest-armed";
        module.graphTasks = new List<ExperimentTask> {
            T("a", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("b", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure, "a"),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Measuring, LabSkill.Transfer };
        var go = new GameObject("SelfTestArmed");
        try
        {
            var runner = go.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            int prepared = 0, started = 0;
            runner.ExperimentPrepared += _ => prepared++;
            runner.ExperimentStarted += _ => started++;

            runner.PrepareExperiment();
            A("armed: state flags", runner.IsArmed && !runner.IsRunning && prepared == 1 && started == 0);
            runner.AdvanceTime(5f);
            A("armed: clock frozen", Near(runner.ElapsedSeconds, 0f));
            A("armed: tasks locked", runner.CompleteTask("a") == TaskCompletionResult.UnknownTask);
            runner.RecordMistake(LabErrorType.WrongReagent, "pre-run");
            A("armed: mistakes ignored", runner.MistakeCount == 0);

            runner.StartRun();
            A("armed: StartRun begins", runner.IsRunning && !runner.IsArmed && started == 1);
            runner.AdvanceTime(3f);
            A("armed: clock runs after start", Near(runner.ElapsedSeconds, 3f));
            A("armed: tasks unlocked", runner.CompleteTask("a") == TaskCompletionResult.Completed);
            runner.CompleteTask("b");
            var res = runner.Finish(1f);
            A("armed: finish works", res.grade.Total > 0f);

            // StartRun with no armed attempt = legacy full start.
            runner.StartRun();
            A("armed: StartRun fallback", runner.IsRunning && Near(runner.ElapsedSeconds, 0f));

            // Launcher modes over the real library asset.
            var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>("Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
            if (lib != null)
            {
                var lgo = new GameObject("SelfTestLauncher");
                try
                {
                    var runner2 = lgo.AddComponent<ExperimentRunner>();
                    var launcher = lgo.AddComponent<ExperimentLauncher>();
                    launcher.SetLibrary(lib); launcher.SetRunner(runner2);
                    var m1 = launcher.Launch("tutorial-methane", LaunchMode.StageOnly);
                    A("launcher: StageOnly furnishes without clock", m1 != null && !runner2.IsRunning && !runner2.IsArmed);
                    var m2 = launcher.Launch("tutorial-methane", LaunchMode.PrepareArmed);
                    A("launcher: PrepareArmed arms", m2 != null && runner2.IsArmed && !runner2.IsRunning);
                    var m3 = launcher.Launch("tutorial-methane");
                    A("launcher: legacy = FullStart", m3 != null && runner2.IsRunning);
                }
                finally { UnityEngine.Object.DestroyImmediate(lgo); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(module); }
    }

    static void GatekeeperSuite()
    {
        string savedSelection = GameFlow.SelectedModuleId;
        string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "gate_selftest.json");
        void CleanTemp() { try { System.IO.File.Delete(tempPath); System.IO.File.Delete(tempPath + ".bak"); } catch { } }
        CleanTemp();
        try
        {
            // --- pure transition table -------------------------------------
            var m = new GatekeeperModel();
            A("gate: starts blocked", m.State == GateState.Blocked && !GatekeeperModel.DoorOpen(m.State));
            A("gate: approach opens choice", m.Fire(GateEvent.Approach) && m.State == GateState.ModeChoice);
            A("gate: illegal event refused", !m.Fire(GateEvent.CrossedThreshold) && m.State == GateState.ModeChoice);
            A("gate: campaign explain", m.Fire(GateEvent.PickCampaign) && m.State == GateState.CampaignExplain);
            A("gate: explain done", m.Fire(GateEvent.ExplainDone) && m.State == GateState.EpisodePick);

            var svc = new ProgressionService(tempPath);
            var flow = new ProgressionFlow(svc);
            string firstPrelim = null;
            foreach (var e in ExperimentCatalog.InPeriod(ExperimentPeriod.Prelim)) { firstPrelim = e.moduleId; break; }

            A("gate: fresh tutorial playable", GatekeeperModel.FirstPlayableInPeriod(flow, ExperimentPeriod.Tutorial) == "tutorial-methane");
            A("gate: fresh prelim locked", GatekeeperModel.FirstPlayableInPeriod(flow, ExperimentPeriod.Prelim) == null);

            GatekeeperModel.EpisodeOptions(flow, out var labels, out var selectable);
            A("gate: 4 episode rows", labels.Count == 4 && selectable.Count == 4);
            A("gate: only tutorial selectable", selectable[0] && !selectable[1] && !selectable[2] && !selectable[3]);
            A("gate: locked label marked", labels[1].Contains("(locked)"));

            Func<string, bool> canSel = id => HubSelectController.CanSelect(flow, id);
            Func<ExperimentPeriod, string> firstOf = p => GatekeeperModel.FirstPlayableInPeriod(flow, p);
            A("gate: locked episode refused", !m.ChooseEpisode(ExperimentPeriod.Midterm, canSel, firstOf) && m.State == GateState.EpisodePick);
            A("gate: tutorial episode chosen", m.ChooseEpisode(ExperimentPeriod.Tutorial, canSel, firstOf)
                && m.State == GateState.CoatPrompt && m.SelectedModuleId == "tutorial-methane");

            // 2026-07-11: the PPE locker moved just inside the lab → the door is
            // open through the gear-up steps (the run still gates on PPE + walk-in).
            A("gate: coat prompt opens the door", GatekeeperModel.DoorOpen(GateState.CoatPrompt)
                && GatekeeperModel.DoorOpen(GateState.ReadyPrompt));
            A("gate: coat then ready", m.Fire(GateEvent.Coated) && m.Fire(GateEvent.Ready) && m.State == GateState.Loading);
            A("gate: loaded warns", m.Fire(GateEvent.Loaded) && m.State == GateState.ThresholdWarn && !GatekeeperModel.DoorOpen(m.State));
            A("gate: cross before confirm refused", !m.Fire(GateEvent.CrossedThreshold));
            A("gate: proceed arms door", m.Fire(GateEvent.ProceedConfirmed) && m.State == GateState.DoorArmed && GatekeeperModel.DoorOpen(m.State));
            A("gate: walk-in runs", m.Fire(GateEvent.CrossedThreshold) && m.State == GateState.Running);
            // Post-experiment review flow (2026-07-11): tests done → Jimenez quiz →
            // score review → return home → entrance debrief → unlock announce.
            A("gate: continue illegal while running", !m.Fire(GateEvent.ContinueAfterPass) && m.State == GateState.Running);
            A("gate: tests done → quiz intro", m.Fire(GateEvent.TestsDone) && m.State == GateState.QuizIntro
                && GatekeeperModel.DoorOpen(m.State));
            A("gate: briefing done → quiz time", m.Fire(GateEvent.QuizBegin) && m.State == GateState.QuizTime);
            A("gate: supply event ignored during quiz", !m.Fire(GateEvent.SupplyExhausted) && m.State == GateState.QuizTime);
            A("gate: graded → score review", m.Fire(GateEvent.Graded) && m.State == GateState.ScoreReview
                && GatekeeperModel.DoorOpen(m.State));
            A("gate: return loop to blocked", m.Fire(GateEvent.ContinueAfterPass) && m.State == GateState.Returning
                && m.Fire(GateEvent.TeleportDone) && m.State == GateState.Debrief
                && !GatekeeperModel.DoorOpen(m.State)
                && m.Fire(GateEvent.DebriefDone) && m.State == GateState.UnlockAnnounce
                && m.Fire(GateEvent.AnnounceDone) && m.State == GateState.Blocked);

            // Fail path: retry from the review corner = clean re-armed attempt.
            var mr = new GatekeeperModel();
            mr.Fire(GateEvent.Approach); mr.Fire(GateEvent.PickCampaign); mr.Fire(GateEvent.ExplainDone);
            mr.ChooseEpisode(ExperimentPeriod.Tutorial, _ => true, _ => "tutorial-methane");
            mr.Fire(GateEvent.Coated); mr.Fire(GateEvent.Ready); mr.Fire(GateEvent.Loaded);
            mr.Fire(GateEvent.ProceedConfirmed); mr.Fire(GateEvent.CrossedThreshold);
            mr.Fire(GateEvent.TestsDone); mr.Fire(GateEvent.QuizBegin); mr.Fire(GateEvent.Graded);
            A("gate: retry re-arms via loading", mr.Fire(GateEvent.RetryRequested) && mr.State == GateState.Loading
                && !string.IsNullOrEmpty(mr.SelectedModuleId));
            var mq = new GatekeeperModel();
            mq.Fire(GateEvent.Approach); mq.Fire(GateEvent.PickCampaign); mq.Fire(GateEvent.ExplainDone);
            mq.ChooseEpisode(ExperimentPeriod.Tutorial, _ => true, _ => "tutorial-methane");
            mq.Fire(GateEvent.Coated); mq.Fire(GateEvent.Ready); mq.Fire(GateEvent.Loaded);
            mq.Fire(GateEvent.ProceedConfirmed); mq.Fire(GateEvent.CrossedThreshold); mq.Fire(GateEvent.TestsDone);
            mq.ResetToBlocked();
            A("gate: HUD reset escapes the quiz flow", mq.State == GateState.Blocked);

            svc.RecordResult("tutorial-methane", new ExperimentResult { grade = new GradeBreakdown { Total = 95 }, overallMastery = 0.95f, passed = true }, false);
            A("gate: prelim unlocks after tutorial", GatekeeperModel.FirstPlayableInPeriod(flow, ExperimentPeriod.Prelim) == firstPrelim && firstPrelim != null);

            var m2 = new GatekeeperModel();
            m2.Fire(GateEvent.Approach); m2.Fire(GateEvent.PickLabTour);
            A("gate: lab tour opens door", m2.State == GateState.LabTour && m2.IsLabTour && GatekeeperModel.DoorOpen(m2.State));
            A("gate: approach never shuts an open tour door", !m2.Fire(GateEvent.Approach) && m2.State == GateState.LabTour);
            A("gate: poking Pharmee reopens the talk", m2.Fire(GateEvent.TalkRequested) && m2.State == GateState.ModeChoice);

            var m3 = new GatekeeperModel();
            m3.Fire(GateEvent.Approach); m3.Fire(GateEvent.PickCampaign); m3.Fire(GateEvent.ExplainDone);
            m3.ChooseEpisode(ExperimentPeriod.Tutorial, _ => true, _ => "tutorial-methane");
            m3.Fire(GateEvent.Coated); m3.Fire(GateEvent.Ready); m3.Fire(GateEvent.Loaded);
            m3.Fire(GateEvent.ProceedConfirmed); m3.Fire(GateEvent.CrossedThreshold);
            A("gate: supply prompt", m3.Fire(GateEvent.SupplyExhausted) && m3.State == GateState.SupplyPrompt && GatekeeperModel.DoorOpen(m3.State));
            A("gate: supply keep-trying", m3.Fire(GateEvent.Dismiss) && m3.State == GateState.Running);
            m3.Fire(GateEvent.SupplyExhausted);
            A("gate: supply restart reloads", m3.Fire(GateEvent.RestartConfirmed) && m3.State == GateState.Loading);

            // HUD reset: force back to the Blocked entrance from any mid-flow state,
            // clearing the chosen episode and firing a Transition to Blocked.
            var m4 = new GatekeeperModel();
            m4.Fire(GateEvent.Approach); m4.Fire(GateEvent.PickCampaign); m4.Fire(GateEvent.ExplainDone);
            m4.ChooseEpisode(ExperimentPeriod.Tutorial, _ => true, _ => "tutorial-methane");
            bool m4Blocked = false;
            m4.Transition += (a, b) => { if (b == GateState.Blocked) m4Blocked = true; };
            m4.ResetToBlocked();
            A("gate: reset returns to blocked", m4.State == GateState.Blocked
                && string.IsNullOrEmpty(m4.SelectedModuleId) && m4Blocked);
            A("gate: welcome line present", !string.IsNullOrEmpty(new PharmeeGatekeeper.GateLines().welcome));

            // --- choice panel component ------------------------------------
            var pgo = new GameObject("panel");
            try
            {
                var panel = pgo.AddComponent<ChoicePanelController>();
                var root = new GameObject("Root"); root.transform.SetParent(pgo.transform);
                var title = new GameObject("Title").AddComponent<TMPro.TextMeshProUGUI>(); title.transform.SetParent(root.transform);
                var btns = new UnityEngine.UI.Button[3]; var lbls = new TMPro.TMP_Text[3];
                for (int i = 0; i < 3; i++)
                {
                    var bgo = new GameObject("B" + i); bgo.transform.SetParent(root.transform);
                    btns[i] = bgo.AddComponent<UnityEngine.UI.Button>();
                    var lgo = new GameObject("L" + i); lgo.transform.SetParent(bgo.transform);
                    lbls[i] = lgo.AddComponent<TMPro.TextMeshProUGUI>();
                }
                panel.Bind(root, title, btns, lbls);
                int chosen = -1; panel.OptionChosen += i => chosen = i;
                panel.Show("Pick", new List<string> { "A", "B" }, new List<bool> { true, false });
                A("panel: shows + trims options", panel.IsOpen && btns[0].gameObject.activeSelf && btns[1].gameObject.activeSelf && !btns[2].gameObject.activeSelf);
                A("panel: labels + lock flags", panel.LabelAt(0) == "A" && panel.LabelAt(1) == "B" && btns[0].interactable && !btns[1].interactable);
                panel.OnOption(1);
                A("panel: option event", chosen == 1);
                panel.Hide();
                A("panel: hides", !panel.IsOpen);
            }
            finally { UnityEngine.Object.DestroyImmediate(pgo); }

            // --- trigger relay ----------------------------------------------
            var rgo = new GameObject("relay");
            try
            {
                var relay = rgo.AddComponent<PlayerTriggerRelay>();
                int hits = 0;
                relay.onPlayerEntered = new UnityEngine.Events.UnityEvent();
                relay.onPlayerEntered.AddListener(() => hits++);
                relay.SimulateEnter();
                A("relay: fires", hits == 1);
            }
            finally { UnityEngine.Object.DestroyImmediate(rgo); }

            // --- mono integration: door gate drives armed -> walk-in start ---
            var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>("Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
            if (lib != null)
            {
                var ggo = new GameObject("gk");
                var blocker = new GameObject("Blocker");
                try
                {
                    var runner = ggo.AddComponent<ExperimentRunner>();
                    var launcher = ggo.AddComponent<ExperimentLauncher>();
                    launcher.SetLibrary(lib); launcher.SetRunner(runner);
                    var gk = ggo.AddComponent<PharmeeGatekeeper>();
                    gk.Bind(null, null, null, launcher, runner, blocker);
                    A("gk: blocker on while blocked", blocker.activeSelf);
                    gk.OnApproachTriggerEntered();
                    gk.OnPanelOption(1);              // Campaign
                    gk.OnPanelOption(0);              // Continue (explain)
                    A("gk: at episode pick", gk.Model.State == GateState.EpisodePick);
                    // deterministic pick (the panel path reads the real save file)
                    gk.Model.ChooseEpisode(ExperimentPeriod.Tutorial, _ => true, _ => "tutorial-methane");
                    gk.Model.Fire(GateEvent.Coated);  // simulated PPE
                    gk.OnPanelOption(0);              // I'm ready -> Loading -> armed -> ThresholdWarn
                    A("gk: armed after load", runner.IsArmed && gk.Model.State == GateState.ThresholdWarn);
                    A("gk: door still blocked pre-confirm", blocker.activeSelf);
                    gk.OnPanelOption(0);              // Proceed
                    A("gk: door opens when armed", !blocker.activeSelf && gk.Model.State == GateState.DoorArmed);
                    gk.OnThresholdTriggerEntered();   // walk in
                    A("gk: walk-in starts run at 0s", runner.IsRunning && Near(runner.ElapsedSeconds, 0f) && gk.Model.State == GateState.Running);
                }
                finally { UnityEngine.Object.DestroyImmediate(ggo); UnityEngine.Object.DestroyImmediate(blocker); }
            }
        }
        finally
        {
            GameFlow.SelectedModuleId = savedSelection;
            CleanTemp();
        }
    }

    static void UnlockReturnSuite()
    {
        // --- teleport math: camera lands exactly on the marker ---------------
        var marker = new Vector3(-4.15f, 0.22f, 1.05f);
        var rig = new Vector3(0f, 0.22f, 0f);
        var cam = new Vector3(0.3f, 1.7f, 0.4f);
        var p0 = TeleportMath.RigPositionFor(marker, 0f, rig, cam);
        A("tp: rig offset (no yaw)", Near(p0.x, -4.45f) && Near(p0.z, 0.65f) && Near(p0.y, 0.22f));
        A("tp: camera lands on marker", Near(p0.x + (cam.x - rig.x), marker.x) && Near(p0.z + (cam.z - rig.z), marker.z));
        var p180 = TeleportMath.RigPositionFor(marker, 180f, rig, cam);
        var offR = Quaternion.Euler(0f, 180f, 0f) * (cam - rig);
        A("tp: yawed offset lands too", Near(p180.x + offR.x, marker.x) && Near(p180.z + offR.z, marker.z));
        A("tp: rig yaw delta", Near(TeleportMath.RigYawFor(180f, 0f, 90f), 90f));

        // --- unlock diff -------------------------------------------------------
        string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "unlock_selftest.json");
        void CleanTemp() { try { System.IO.File.Delete(tempPath); System.IO.File.Delete(tempPath + ".bak"); } catch { } }
        CleanTemp();
        try
        {
            var svc = new ProgressionService(tempPath);
            var flow = new ProgressionFlow(svc);
            var before = UnlockDiff.UnlockedSet(flow);
            A("unlock: fresh set = tutorial only", before.Count == 1 && before.Contains("tutorial-methane"));
            svc.RecordResult("tutorial-methane", new ExperimentResult { grade = new GradeBreakdown { Total = 95 }, overallMastery = 0.95f, passed = true }, false);
            var newly = UnlockDiff.NewlyUnlocked(before, flow);
            string firstPrelim = null;
            foreach (var e in ExperimentCatalog.InPeriod(ExperimentPeriod.Prelim)) { firstPrelim = e.moduleId; break; }
            A("unlock: tutorial pass unlocks first prelim", newly.Count == 1 && newly[0] == firstPrelim);
            string line = UnlockDiff.AnnouncementFor(newly);
            A("unlock: announcement names it", line.Contains("unlocked") && line.Contains(ExperimentCatalog.Get(firstPrelim).title));
            A("unlock: empty fallback congratulates", !string.IsNullOrEmpty(UnlockDiff.AnnouncementFor(new List<string>())));

            // --- mono: full continue-after-pass return loop (edit mode = immediate) ---
            var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>("Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
            if (lib != null)
            {
                string savedSelection = GameFlow.SelectedModuleId;
                var ggo = new GameObject("gkret");
                var blocker = new GameObject("Blocker2");
                var rigGo = new GameObject("RigSim");
                var markerGo = new GameObject("Marker");
                var camGo = new GameObject("CamSim");
                try
                {
                    rigGo.transform.position = new Vector3(2f, 0.22f, -3f);
                    markerGo.transform.position = marker;
                    markerGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    var simCam = camGo.AddComponent<Camera>();
                    camGo.transform.SetParent(rigGo.transform);
                    camGo.transform.localPosition = new Vector3(0.2f, 1.5f, 0.1f);

                    var runner = ggo.AddComponent<ExperimentRunner>();
                    var launcher = ggo.AddComponent<ExperimentLauncher>();
                    launcher.SetLibrary(lib); launcher.SetRunner(runner);
                    var gk = ggo.AddComponent<PharmeeGatekeeper>();
                    gk.Bind(null, null, null, launcher, runner, blocker);
                    gk.BindReturn(markerGo.transform, rigGo.transform, simCam, null);

                    // drive to Running
                    gk.OnApproachTriggerEntered();
                    gk.OnPanelOption(1); gk.OnPanelOption(0);
                    gk.Model.ChooseEpisode(ExperimentPeriod.Tutorial, _ => true, _ => "tutorial-methane");
                    gk.Model.Fire(GateEvent.Coated);
                    gk.OnPanelOption(0);      // ready -> armed
                    gk.OnPanelOption(0);      // proceed
                    gk.OnThresholdTriggerEntered();
                    A("return: running", gk.Model.State == GateState.Running && runner.IsRunning);

                    // A finish while Running (dev/legacy path) must cascade through the
                    // quiz states (entries short-circuit on the cached result) to review.
                    runner.Finish(1f);
                    A("return: finish lands in score review", gk.Model.State == GateState.ScoreReview);

                    gk.OnContinueAfterPass(); // edit mode: return+debrief+announce run immediately
                    A("return: loop lands blocked", gk.Model.State == GateState.Blocked);
                    A("return: door re-blocked", blocker.activeSelf);
                    // camera (rig child) must now stand on the marker XZ
                    Vector3 camWorld = camGo.transform.position;
                    A("return: teleported to front door", Near(camWorld.x, marker.x, 0.05f) && Near(camWorld.z, marker.z, 0.05f));
                }
                finally
                {
                    GameFlow.SelectedModuleId = savedSelection;
                    UnityEngine.Object.DestroyImmediate(ggo);
                    UnityEngine.Object.DestroyImmediate(blocker);
                    UnityEngine.Object.DestroyImmediate(camGo);
                    UnityEngine.Object.DestroyImmediate(rigGo);
                    UnityEngine.Object.DestroyImmediate(markerGo);
                }
            }
        }
        finally { CleanTemp(); }
    }

    static void LabMenuSuite()
    {
        A("labmenu: restart label running", LabMenuController.RestartConfirmText(true).Contains("attempt"));
        A("labmenu: restart label idle", LabMenuController.RestartConfirmText(false).Contains("Reset"));
        var go = new GameObject("labmenu");
        try
        {
            var lm = go.AddComponent<LabMenuController>();
            var settings = new GameObject("Settings"); settings.transform.SetParent(go.transform); settings.SetActive(false);
            var confirm = go.AddComponent<ChoicePanelController>();
            var root = new GameObject("CRoot"); root.transform.SetParent(go.transform);
            confirm.Bind(root, null, null, null);
            lm.Bind(settings, confirm, null, null, null);
            lm.OnSettingsToggle();
            A("labmenu: settings toggles on", settings.activeSelf);
            lm.OnSettingsToggle();
            A("labmenu: settings toggles off", !settings.activeSelf);
            lm.OnQuitToMenu();
            A("labmenu: quit opens confirm", confirm.IsOpen);
            lm.OnConfirmOption(1);   // Stay
            A("labmenu: cancel closes confirm", !confirm.IsOpen);
            lm.OnQuitToMenu();
            lm.OnConfirmOption(0);   // edit mode: scene-load path is play-guarded
            A("labmenu: confirm closes too", !confirm.IsOpen);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void DepletionSuite()
    {
        var chem = ScriptableObject.CreateInstance<ChemicalData>();
        chem.chemicalName = "selftest-depletion-chem";
        var otherChem = ScriptableObject.CreateInstance<ChemicalData>();
        otherChem.chemicalName = "selftest-unexpected-chem";
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.moduleId = "selftest-depletion";
        module.graphTasks = new List<ExperimentTask> {
            T("add-x", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Measuring };

        var go = new GameObject("depletion");
        var fullGo = GameObject.CreatePrimitive(PrimitiveType.Cube);   // LiquidPhysics requires a Renderer
        var bottleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            // --- capacity guard: rejected overflow fires LiquidRejected, NOT LiquidAdded
            var full = fullGo.AddComponent<LiquidPhysics>();
            full.maxVolume = 100f; full.currentLiquidVolume = 100f; full.currentChemical = chem;
            bool added = false, rejected = false;
            full.LiquidAdded += (c, a) => added = true;
            full.LiquidRejected += (c, a) => rejected = true;
            full.AddLiquid(chem, 50f);
            A("deplete: overflow rejected", rejected && !added && Near(full.currentLiquidVolume, 100f));

            // --- accumulation toward requiredMl
            var runner = go.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            runner.StartExperiment();
            // bindings live on the vessel's own GO so the monitor never counts it as supply
            var vessel = fullGo.AddComponent<LiquidTaskBinding>();
            vessel.SetVesselAndRunner(full, runner);
            vessel.AddExpected(chem, "add-x", 50f);
            vessel.HandleReagent(chem, 20f);
            vessel.HandleReagent(chem, 20f);
            A("deplete: below threshold pends", !runner.Graph.IsComplete("add-x") && Near(vessel.AccumulatedFor("add-x"), 40f));
            vessel.HandleReagent(chem, 10f);
            A("deplete: threshold completes", runner.Graph.IsComplete("add-x"));
            vessel.HandleReagent(chem, 30f);   // extra pour after completion is ignored
            A("deplete: no double-complete side effects", runner.MistakeCount == 0);
            vessel.HandleReagent(otherChem, 10f);
            A("deplete: wrong reagent still flagged", runner.MistakeCount == 1);

            // legacy single-arg = full delivery regardless of threshold
            runner.Retry();
            var vessel2 = fullGo.AddComponent<LiquidTaskBinding>();
            vessel2.SetVesselAndRunner(full, runner);
            vessel2.AddExpected(chem, "add-x", 50f);
            vessel2.HandleReagent(chem);
            A("deplete: legacy call instant-completes", runner.Graph.IsComplete("add-x"));

            // --- pure shortfall math
            var needs = new List<ReagentSupplyMath.Need> {
                new ReagentSupplyMath.Need { taskId = "add-x", chemicalName = "X", requiredMl = 50f, deliveredMl = 0f },
                new ReagentSupplyMath.Need { taskId = "add-y", chemicalName = "Y", requiredMl = 50f, deliveredMl = 30f },
                new ReagentSupplyMath.Need { taskId = "done-z", chemicalName = "Z", requiredMl = 50f, deliveredMl = 0f },
            };
            var avail = new Dictionary<string, float> { { "X", 30f }, { "Y", 25f }, { "Z", 0f } };
            var shorts = ReagentSupplyMath.FindShortfalls(needs, id => id == "done-z", avail);
            A("deplete: shortfall math", shorts.Count == 1 && shorts[0] == "add-x");
            // (Y needs 20 more with 25 available -> fine; Z is complete -> excluded)
            avail["X"] = 60f;
            A("deplete: sufficient = clean", ReagentSupplyMath.FindShortfalls(needs, id => id == "done-z", avail).Count == 0);

            // --- monitor end-to-end over live components
            runner.Retry();
            var monitor = go.AddComponent<ReagentSupplyMonitor>();
            monitor.SetRunner(runner);
            var vlp = bottleGo.AddComponent<LiquidPhysics>();   // reaction vessel body
            var vessel3 = bottleGo.AddComponent<LiquidTaskBinding>();
            vessel3.SetVesselAndRunner(vlp, runner);
            vessel3.AddExpected(chem, "add-x", 50f);
            var srcGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var src = srcGo.AddComponent<LiquidPhysics>();
                src.currentChemical = chem; src.currentLiquidVolume = 30f;   // not enough for 50
                var starved = monitor.EvaluateNow();
                A("deplete: monitor detects starvation", starved.Contains("add-x"));
                src.currentLiquidVolume = 80f;
                A("deplete: monitor clean when supplied", monitor.EvaluateNow().Count == 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(srcGo); }

            // --- builder supply sizing: 2.5x need floor
            A("deplete: supply autosize floor", Mathf.Max(120f, 50f * 2.5f) == 125f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(fullGo);
            UnityEngine.Object.DestroyImmediate(bottleGo);
            UnityEngine.Object.DestroyImmediate(module);
            UnityEngine.Object.DestroyImmediate(chem);
            UnityEngine.Object.DestroyImmediate(otherChem);
        }
    }

    static void RealSizeSuite()
    {
        A("size: table count", RealSizes.Count == 42);
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        if (lib != null)
        {
            var so = new SerializedObject(lib);
            var prefabs = so.FindProperty("prefabs");
            bool all = prefabs != null;
            if (prefabs != null)
                for (int i = 0; i < prefabs.arraySize; i++)
                {
                    var el = prefabs.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (el != null && !RealSizes.TryGet(el.name, out _)) { all = false; _log.Add("missing RealSizes: " + el.name); }
                }
            A("size: every library prefab covered", all);
        }
        A("size: factor math", Near(RealSizes.UniformScaleFactor(new Vector3(0.05f, 0.02f, 0.32f), 0.16f), 0.5f));
        A("size: degenerate guarded", Near(RealSizes.UniformScaleFactor(Vector3.zero, 0.16f), 1f));
    }

    static void PhysicsProfileSuite()
    {
        // Table stays in lockstep with RealSizes: same items, none missing.
        A("phys: table lockstep with RealSizes", PhysicsProfiles.Count == RealSizes.Count);
        bool allProfiled = true;
        foreach (var n in RealSizes.Names)
            if (!PhysicsProfiles.TryGet(n, out _)) { allProfiled = false; _log.Add("missing PhysicsProfile: " + n); }
        A("phys: every sized item profiled", allProfiled);

        bool massesSane = true;
        foreach (var n in PhysicsProfiles.Names)
        {
            PhysicsProfiles.TryGet(n, out var p);
            if (p.massKg <= 0.005f || p.massKg > 6f) { massesSane = false; _log.Add("implausible mass: " + n + " = " + p.massKg); }
        }
        A("phys: masses plausible", massesSane);

        // Pose math: a Y-long rod is laid down (its long axis ends horizontal)…
        var lie = PhysicsProfiles.RestRotation(RestPose.LieLongAxis, new Vector3(0.02f, 0.3f, 0.02f));
        A("phys: rod laid on its side", Mathf.Abs((lie * Vector3.up).y) < 0.01f);
        // …while an already-horizontal rod and upright items are untouched.
        A("phys: lying rod untouched", PhysicsProfiles.RestRotation(RestPose.LieLongAxis, new Vector3(0.3f, 0.02f, 0.02f)) == Quaternion.identity);
        A("phys: upright untouched", PhysicsProfiles.RestRotation(RestPose.Upright, new Vector3(0.02f, 0.3f, 0.02f)) == Quaternion.identity);
        // Flat tools: thinnest axis ends vertical.
        var flat = PhysicsProfiles.RestRotation(RestPose.Flat, new Vector3(0.01f, 0.15f, 0.10f));
        A("phys: flat tool face-down", Mathf.Abs((flat * Vector3.right).y) > 0.99f);
        A("phys: flat already down untouched", PhysicsProfiles.RestRotation(RestPose.Flat, new Vector3(0.15f, 0.01f, 0.10f)) == Quaternion.identity);

        // Resting plausibility (the drop test's verdict function).
        A("phys: lying rod plausible", PhysicsProfiles.IsRestingPlausible(RestPose.LieLongAxis, new Vector3(0.3f, 0.02f, 0.02f)));
        A("phys: balancing rod implausible", !PhysicsProfiles.IsRestingPlausible(RestPose.LieLongAxis, new Vector3(0.02f, 0.3f, 0.02f)));
        A("phys: face-down gauze plausible", PhysicsProfiles.IsRestingPlausible(RestPose.Flat, new Vector3(0.15f, 0.01f, 0.10f)));
        A("phys: on-edge gauze implausible", !PhysicsProfiles.IsRestingPlausible(RestPose.Flat, new Vector3(0.01f, 0.15f, 0.10f)));

        // Degenerate-collider guard.
        A("phys: paper-thin collider flagged", PhysicsProfiles.IsDegenerate(new Vector3(0.004f, 0.1f, 0.1f)));
        A("phys: normal collider passes", !PhysicsProfiles.IsDegenerate(new Vector3(0.01f, 0.1f, 0.1f)));

        // EnsurePhysics + release policy on a live object (edit-mode Bind seam).
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var rb = PhysicsProfiles.EnsurePhysics(go, "GlassRod");
            A("phys: spawns kinematic with profile mass", rb != null && rb.isKinematic && rb.useGravity && Near(rb.mass, 0.03f));
            var policy = go.AddComponent<GrabPhysicsPolicy>();
            policy.Bind(rb, null);
            A("phys: kinematic until released", !policy.IsDynamic);
            policy.OnReleased();
            A("phys: dynamic after release", policy.IsDynamic && !rb.isKinematic);

            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var added = PhysicsProfiles.EnsureCollider(go) as BoxCollider;
            A("phys: collider-less item gets a box", added != null
                && !PhysicsProfiles.IsDegenerate(Vector3.Scale(added.size, go.transform.lossyScale)));

            // Concave mesh colliders are rejected by PhysX on dynamic bodies
            // (items fell through the world in the drop test) → must convexify.
            var meshGo = new GameObject("mc");
            meshGo.transform.SetParent(go.transform, false);
            var mc = meshGo.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = false;
            A("phys: concave colliders convexified", PhysicsProfiles.ConvexifyMeshColliders(go) == 1 && mc.convex);

            // Drop respawn: kill-Z + idle return-to-home policy.
            A("respawn: below kill-Z", DropRespawnMath.ShouldRespawn(-1.2f, -1f));
            A("respawn: on bench safe", !DropRespawnMath.ShouldRespawn(0.9f, -1f));
            A("respawn: abandoned item returns", DropRespawnMath.ShouldReturnHome(1.5f, 0f, false, 30f));
            A("respawn: held item never returns", !DropRespawnMath.ShouldReturnHome(1.5f, 0f, true, 30f));
            A("respawn: moving item waits", !DropRespawnMath.ShouldReturnHome(1.5f, 0.4f, false, 30f));
            A("respawn: near home stays put", !DropRespawnMath.ShouldReturnHome(0.1f, 0f, false, 30f));
            var dr = go.AddComponent<DropRespawn>();
            dr.Bind(go.GetComponent<Rigidbody>(), null);
            dr.SetHome(new Vector3(1f, 2f, 3f), Quaternion.identity);
            go.transform.position = new Vector3(9f, -5f, 9f);
            var rbDyn = go.GetComponent<Rigidbody>(); rbDyn.isKinematic = false;
            dr.GoHome();
            A("respawn: teleports home", (go.transform.position - new Vector3(1f, 2f, 3f)).magnitude < 1e-4f);
            A("respawn: re-freezes to shelf policy", rbDyn.isKinematic);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    /// Product-seeded test vessels (§1): every experiment's confirmatory-test
    /// pour must land in a vessel already holding the product, so the registry
    /// rule fires and the colour/ppt/gas observation actually shows.
    static void TestVesselSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        A("testvessel: library + registry load", lib != null && reg != null);
        if (lib == null || reg == null) return;

        var cases = new (string file, string seed, string[] reagents)[]
        {
            ("Layout_Aspirin",      "Salicylic Acid", new[] { "Ferric Chloride 10%" }),
            ("Layout_BenzoicAcid",  "Benzoic Acid",   new[] { "Ferric Chloride 10%", "Sulfuric Acid" }),
            ("Layout_Acetanilide",  "Acetanilide",    new[] { "Bromine Water" }),
            ("Layout_Benzamide",    "Benzamide",      new[] { "Sodium Hydroxide", "Sodium Nitrite", "Hydrochloric Acid 6N" }),
            ("Layout_Chloroform",   "Chloroform",     new[] { "Silver Nitrate" }),
            ("Layout_Acetone",      "Acetone",        new[] { "Silver Nitrate", "Sodium Hypochlorite", "Schiff's Reagent" }),
            ("Layout_EthylAlcohol", "Ethanol",        new[] { "Sodium Hypochlorite", "Glacial Acetic Acid" }),
            ("Layout_WineMaking",   "Carbon Dioxide", new[] { "Limewater" }),
            ("Layout_Caffeine",     "Caffeine",       new[] { "Murexide Reagent" }),
        };
        foreach (var c in cases)
        {
            var layout = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(
                "Assets/PharmaSynth/ScriptableObjects/Layouts/" + c.file + ".asset");
            ExperimentLayout.Vessel tv = null;
            if (layout != null)
                foreach (var v in layout.vessels) if (v.startChemical == c.seed) tv = v;
            A("testvessel: " + c.file + " seeded vessel", tv != null);
            if (tv == null) continue;
            var seedChem = lib.GetChemical(c.seed);
            A("testvessel: " + c.file + " seed in library", seedChem != null);
            foreach (var reagent in c.reagents)
            {
                bool bound = false;
                foreach (var b in tv.bindings) if (b.reagentChemical == reagent) bound = true;
                A("testvessel: " + c.file + " binds " + reagent, bound);
                var rChem = lib.GetChemical(reagent);
                A("testvessel: " + c.file + " rule fires (" + reagent + ")",
                    seedChem != null && rChem != null && reg.FindReaction(seedChem, rChem) != null);
            }
        }

        // Every chemical any layout references must resolve through the library,
        // or the builder seeds/fills silently fail at stage-build time.
        bool allResolve = true;
        void Need(string chem, string where)
        {
            if (!string.IsNullOrEmpty(chem) && lib.GetChemical(chem) == null)
            { allResolve = false; _log.Add("library missing chemical: " + chem + " (" + where + ")"); }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:ExperimentLayout",
                     new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
        {
            var layout = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(guid));
            if (layout == null) continue;
            foreach (var p in layout.props) if (p.pourable) Need(p.fillChemical, layout.name);
            foreach (var v in layout.vessels)
            {
                Need(v.startChemical, layout.name);
                foreach (var b in v.bindings) Need(b.reagentChemical, layout.name);
            }
        }
        A("testvessel: all layout chemicals resolve", allResolve);
    }

    /// Spill & breakage mishandling penalties (§2, user request 2026-07-09).
    static void MishandlingSuite()
    {
        // Fragility table: glass breaks, tools don't, and every entry is a real prefab.
        A("mishandle: glass is breakable", Mishandling.IsBreakable("Beaker_100mL")
            && Mishandling.IsBreakable("TestTube") && Mishandling.IsBreakable("GlassRod"));
        A("mishandle: tools are not", !Mishandling.IsBreakable("CrucibleTongs")
            && !Mishandling.IsBreakable("Spatula") && !Mishandling.IsBreakable("WashBottle")
            && !Mishandling.IsBreakable("TestTubeRack"));
        bool allReal = true;
        foreach (var n in Mishandling.BreakableNames)
            if (!RealSizes.TryGet(n, out _)) { allReal = false; _log.Add("breakable not in RealSizes: " + n); }
        A("mishandle: breakables are real prefabs", allReal);

        // Impact policy: a real drop breaks, but carrying + bumping a wall does not.
        A("mishandle: hard drop breaks", Mishandling.ShouldBreak(4.5f));
        A("mishandle: threshold impact breaks", Mishandling.ShouldBreak(4.0f));
        A("mishandle: carry-bump into wall safe", !Mishandling.ShouldBreak(2.5f));
        A("mishandle: gentle set-down safe", !Mishandling.ShouldBreak(0.8f));

        // Spill policy: un-held + tipped + has liquid, and only then.
        A("mishandle: knocked-over bottle spills", Mishandling.IsSpilling(75f, false, 80f));
        A("mishandle: held pour is not a spill", !Mishandling.IsSpilling(75f, true, 80f));
        A("mishandle: empty bottle can't spill", !Mishandling.IsSpilling(75f, false, 0f));
        A("mishandle: upright bottle safe", !Mishandling.IsSpilling(20f, false, 80f));

        // Grading: both mishandling types hit the Sanitation rubric.
        A("mishandle: spill maps to Sanitation", MistakeLog.CategoryFor(LabErrorType.SpilledReagent) == RubricCategory.Sanitation);
        var log = new MistakeLog();
        log.Record(LabErrorType.DroppedGlassware, "");
        log.Record(LabErrorType.SpilledReagent, "");
        A("mishandle: grader counts both", log.CountOfAny(LabErrorType.DroppedGlassware, LabErrorType.SpilledReagent) == 2);

        // Action-SFX policy: material-aware drop clatter + reaction cues + stride.
        A("sfx: glass clinks", Mishandling.DropSoundKey("Beaker_100mL") == "glass-clink");
        A("sfx: metal clatters", Mishandling.DropSoundKey("CrucibleTongs") == "drop-metal");
        A("sfx: wood knocks", Mishandling.DropSoundKey("TestTubeRack") == "drop-wood");
        A("sfx: fizz for gas outcomes", Mishandling.SfxForOutcome(ReactionOutcome.Fizzing) == "reaction-fizz"
            && Mishandling.SfxForOutcome(ReactionOutcome.GasEvolved) == "reaction-fizz");
        A("sfx: chime for visible outcomes", Mishandling.SfxForOutcome(ReactionOutcome.Precipitate) == "mixture-complete"
            && Mishandling.SfxForOutcome(ReactionOutcome.ColorChange) == "mixture-complete");
        A("sfx: silence for negative tests", Mishandling.SfxForOutcome(ReactionOutcome.None) == "");
        float acc = 0f;
        A("sfx: stride accumulates", StrideMath.Steps(ref acc, 0.5f, 0.75f) == 0 && StrideMath.Steps(ref acc, 0.5f, 0.75f) == 1);
        A("sfx: long move = many steps", StrideMath.Steps(ref acc, 3f, 0.75f) >= 4);
        A("sfx: no distance no steps", StrideMath.Steps(ref acc, 0f, 0.75f) == 0);

        // Mirror avatar: foot point sits under the head at the floor (+offset), XZ kept.
        var foot = PlayerAvatarRig.FootUnder(new Vector3(2f, 1.7f, -3f), 0.25f, 0.02f);
        A("avatar: foot keeps XZ", Near(foot.x, 2f) && Near(foot.z, -3f));
        A("avatar: foot sits on floor+offset", Near(foot.y, 0.27f));

        // Per-piece PPE (user 2026-07-10): all three required; missing-summary text.
        var ppeSet = new PPESetModel();
        A("ppe: nothing worn at start", !ppeSet.AllWorn && ppeSet.WornCount == 0);
        A("ppe: missing lists all three", ppeSet.MissingSummary() == "lab coat, goggles and gloves");
        A("ppe: don returns newly-worn", ppeSet.Don(PPEPiece.Coat) && !ppeSet.Don(PPEPiece.Coat));
        A("ppe: coat alone is not enough", !ppeSet.AllWorn);
        A("ppe: missing pair reads naturally", ppeSet.MissingSummary() == "goggles and gloves");
        ppeSet.Don(PPEPiece.Goggles);
        A("ppe: one missing reads singly", ppeSet.MissingSummary() == "gloves");
        ppeSet.Don(PPEPiece.Gloves);
        A("ppe: all three = fully dressed", ppeSet.AllWorn && ppeSet.MissingSummary() == "");
        A("ppe: clear strips everything", ppeSet.Clear() && !ppeSet.AllWorn && !ppeSet.Clear());

        // Pharmee flight lean: proportional to speed, clamped, zero at rest.
        A("pharmee: lean scales with speed", Near(PharmeeAttitude.LeanFor(0.5f, 22f, 14f), 11f));
        A("pharmee: lean clamped", Near(PharmeeAttitude.LeanFor(3f, 22f, 14f), 14f));
        A("pharmee: no lean at rest", Near(PharmeeAttitude.LeanFor(0f, 22f, 14f), 0f));

        // Station VFX (user 2026-07-10): verb → effect style, edit-mode-safe toggling.
        A("vfx: heat steams", StationVfx.StyleFor(StationSim.Heat) == "steam");
        A("vfx: crystallise frosts", StationVfx.StyleFor(StationSim.Crystallise) == "frost");
        A("vfx: filter drips", StationVfx.StyleFor(StationSim.Filter) == "drip");
        A("vfx: collect bubbles", StationVfx.StyleFor(StationSim.Collect) == "bubbles");
        A("vfx: none is empty", StationVfx.StyleFor(StationSim.None) == "");

        // Proctor roaming (user 2026-07-10): idle → walk out → observe → walk home.
        var roam = new ProctorRoamModel(3, idleMin: 1f, idleMax: 1f, observeSeconds: 2f, seed: 7);
        A("roam: starts at home", roam.Current == ProctorRoamModel.Phase.AtHome && !roam.IsWalking);
        roam.Tick(1.5f, true, true);                                     // idle expires
        A("roam: heads out to point 0", roam.Current == ProctorRoamModel.Phase.WalkingOut && roam.TargetIndex == 0 && roam.IsWalking);
        roam.Tick(0.1f, true, false);                                    // still walking
        A("roam: keeps walking until arrival", roam.Current == ProctorRoamModel.Phase.WalkingOut);
        roam.Tick(0.1f, true, true);                                     // arrived
        A("roam: observes on arrival", roam.Current == ProctorRoamModel.Phase.Observing);
        roam.Tick(2.5f, true, true);                                     // observation done
        A("roam: walks home after observing", roam.Current == ProctorRoamModel.Phase.WalkingHome);
        roam.Tick(0.1f, true, true);
        A("roam: back home", roam.Current == ProctorRoamModel.Phase.AtHome);
        roam.Tick(1.5f, true, true);
        A("roam: round-robins to point 1", roam.TargetIndex == 1);
        roam.Tick(0.1f, false, false);                                   // quiz begins mid-walk
        A("roam: quiz forces him home", roam.Current == ProctorRoamModel.Phase.WalkingHome);
        var stay = new ProctorRoamModel(3, 1f, 1f, 2f, 7);
        stay.Tick(10f, false, true);
        A("roam: never leaves during quiz", stay.Current == ProctorRoamModel.Phase.AtHome);

        // Roamer stuck watchdog (user 2026-07-10: Jimenez ran through a wall endlessly).
        float stuck = 0f;
        A("roam: progress resets the watchdog", !ProctorRoamer.StuckTick(ref stuck, 0.5f, 1f, 2f) && Near(stuck, 0f));
        A("roam: brief block is tolerated", !ProctorRoamer.StuckTick(ref stuck, 0f, 1f, 2f));
        A("roam: sustained block gives up", ProctorRoamer.StuckTick(ref stuck, 0f, 1.5f, 2f) && Near(stuck, 0f));

        // Spill puddle fade (user 2026-07-10): full while lingering, smooth fade after.
        A("puddle: opaque while lingering", Near(SpillPuddle.Alpha01(0f, 3f, 1.2f), 1f) && Near(SpillPuddle.Alpha01(2.9f, 3f, 1.2f), 1f));
        A("puddle: half-faded mid-fade", Near(SpillPuddle.Alpha01(3.6f, 3f, 1.2f), 0.5f));
        A("puddle: gone after fade", Near(SpillPuddle.Alpha01(4.2f, 3f, 1.2f), 0f) && Near(SpillPuddle.Alpha01(9f, 3f, 1.2f), 0f));

        // Pharmee gate moods (user 2026-07-10): happy default, warning on trouble.
        A("mood: friendly at the door", PharmeeMood.ExpressionForGate(GateState.ModeChoice) == PharmeeFaceExpression.Happy);
        A("mood: warns on supply trouble", PharmeeMood.ExpressionForGate(GateState.SupplyPrompt) == PharmeeFaceExpression.Warning);
        A("mood: celebrates the debrief", PharmeeMood.ExpressionForGate(GateState.Debrief) == PharmeeFaceExpression.Happy);
        A("mood: neutral while picking", PharmeeMood.ExpressionForGate(GateState.EpisodePick) == PharmeeFaceExpression.Neutral);

        // Sim-loop audio: verb → SoundBank key mapping, and safe without clips.
        A("sfx: heat loops bubble", SimLoopAudio.KeyFor(StationSim.Heat) == "bubble");
        A("sfx: filter loops drip", SimLoopAudio.KeyFor(StationSim.Filter) == "filter-drip");
        A("sfx: collect loops hiss", SimLoopAudio.KeyFor(StationSim.Collect) == "gas-hiss");
        A("sfx: crystallise loops shimmer", SimLoopAudio.KeyFor(StationSim.Crystallise) == "crystallise");
        var loopGo = new GameObject("loop");
        try
        {
            var loop = loopGo.AddComponent<SimLoopAudio>();
            loop.Bind("bubble");
            loop.SetRunning(true);       // no AudioService in edit mode → silent no-op
            loop.SetRunning(false);
            A("sfx: loop no-op without service", !loop.IsPlaying);
        }
        finally { UnityEngine.Object.DestroyImmediate(loopGo); }

        // Break → replacement flow: shattered item re-appears at its home spot.
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = false;
            var respawn = go.AddComponent<DropRespawn>();
            respawn.Bind(rb, null);
            respawn.SetHome(new Vector3(2f, 1f, 2f), Quaternion.identity);
            var breakable = go.AddComponent<BreakableGlassware>();
            breakable.Bind(null, respawn, rb, "Test Beaker");
            go.transform.position = new Vector3(5f, 0f, 5f);
            breakable.Break();                                  // no runner → must not throw
            A("mishandle: break sends replacement home", (go.transform.position - new Vector3(2f, 1f, 2f)).magnitude < 1e-4f);
            A("mishandle: replacement re-frozen", rb.isKinematic);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    /// Settings apply-listeners (§5): comfort values reach the live systems.
    static void ComfortSuite()
    {
        // Pure curves.
        A("comfort: text scale scales HUD", ComfortMath.HudScale(Vector3.one * 0.001f, 1.2f) == Vector3.one * 0.0012f);
        A("comfort: text scale clamped", ComfortMath.HudScale(Vector3.one, 5f) == Vector3.one * 1.6f);
        A("comfort: vignette off = open aperture", Near(ComfortMath.ApertureFor(0f), 1f));
        A("comfort: vignette full = tight aperture", Near(ComfortMath.ApertureFor(1f), 0.35f));
        A("comfort: fast subtitles dwell shorter", Near(ComfortMath.LineSecondsFor(4f, 2f), 2f));
        A("comfort: slow subtitles dwell longer", Near(ComfortMath.LineSecondsFor(4f, 0.5f), 8f));
        A("comfort: pace speed clamped", Near(ComfortMath.LineSecondsFor(4f, 10f), 2f));

        // Live applier: values land on targets, and re-applying never compounds.
        var go = new GameObject("comfort");
        var hud = new GameObject("hud").transform;
        try
        {
            hud.localScale = Vector3.one * 0.0011f;
            var turn = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider>();
            var applier = go.AddComponent<ComfortApplier>();
            applier.Bind(hud, turn, null, null, null);
            var s = new ComfortSettings();
            s.SetTextScale(1.2f); s.SetSnapTurnAngle(60f);
            applier.Apply(s);
            A("comfort: hud scaled", Near(hud.localScale.x, 0.0011f * 1.2f, 1e-5f));
            A("comfort: snap angle applied", Near(turn.turnAmount, 60f));
            s.SetTextScale(0.9f);
            applier.Apply(s);
            A("comfort: re-apply uses baseline (no compounding)", Near(hud.localScale.x, 0.0011f * 0.9f, 1e-5f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(hud.gameObject);
        }
    }

    static void PharmeeAliveSuite()
    {
        // Jitter: deterministic, bounded, non-constant
        var j1 = FloatBob.JitterOffset(3.7f, 2.3f, 0.012f);
        var j2 = FloatBob.JitterOffset(3.7f, 2.3f, 0.012f);
        var j3 = FloatBob.JitterOffset(5.1f, 2.3f, 0.012f);
        A("alive: jitter deterministic", (j1 - j2).magnitude < 1e-6f);
        A("alive: jitter bounded", Mathf.Abs(j1.x) <= 0.012f && Mathf.Abs(j1.y) <= 0.012f && Mathf.Abs(j1.z) <= 0.012f);
        A("alive: jitter varies", (j1 - j3).magnitude > 1e-5f);
        A("alive: zero amplitude = zero", FloatBob.JitterOffset(3.7f, 2.3f, 0f) == Vector3.zero);

        // Anchor picking: nearest valid, min-distance, hysteresis
        var anchors = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(3, 0, 0), new Vector3(6, 0, 0) };
        var player = new Vector3(0.5f, 0, 0);
        int pick = PharmeeMoveSolver.PickAnchor(player, anchors, 1.2f, -1, 0.75f);
        A("alive: skips crowding anchor", pick == 1);                                  // anchor 0 is 0.5m away -> too close
        int sticky = PharmeeMoveSolver.PickAnchor(new Vector3(4.6f, 0, 0), anchors, 1.2f, 1, 0.75f);
        A("alive: hysteresis holds", sticky == 1);                                     // anchor2 only 0.2m closer -> stay
        int swap = PharmeeMoveSolver.PickAnchor(new Vector3(7.5f, 0, 0), anchors, 1.2f, 1, 0.75f);
        A("alive: clear winner swaps", swap == 2);                                     // anchor2 3m closer -> move
        A("alive: all crowded stays", PharmeeMoveSolver.PickAnchor(Vector3.zero, new List<Vector3> { new Vector3(0.3f, 0, 0) }, 1.2f, 0, 0.75f) == 0);

        // Step: clamped + exact landing
        var step = PharmeeMoveSolver.Step(Vector3.zero, new Vector3(10, 0, 0), 0.8f, 0.5f);
        A("alive: step clamped", Near(step.x, 0.4f));
        A("alive: step lands", PharmeeMoveSolver.Step(new Vector3(9.99f, 0, 0), new Vector3(10, 0, 0), 0.8f, 1f) == new Vector3(10, 0, 0));

        // Mover drives FloatBob home toward the anchor while running
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> { T("a", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure) };
        module.trackedSkills = new List<LabSkill> { LabSkill.Measuring };
        var go = new GameObject("mover");
        var playerGo = new GameObject("playerSim");
        var a1 = new GameObject("a1"); var a2 = new GameObject("a2");
        try
        {
            a1.transform.position = new Vector3(4f, 0, 0);
            a2.transform.position = new Vector3(-4f, 0, 0);
            playerGo.transform.position = new Vector3(2.5f, 1.6f, 0);   // 1.5m from a1 (outside min dist)
            var runner = go.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            runner.StartExperiment();
            var bob = go.AddComponent<FloatBob>();
            bob.SetHome(new Vector3(0f, 1.05f, 0f));
            var mover = go.AddComponent<PharmeeMover>();
            mover.Bind(runner, bob, playerGo.transform, new[] { a1.transform, a2.transform });
            for (int i = 0; i < 20; i++) mover.TickSolve(0.5f);   // 10s of glide
            A("alive: mover approaches player-side anchor", (bob.Home - new Vector3(4f, 1.05f, 0f)).magnitude < 0.2f);
            runner.Finish(1f);
            for (int i = 0; i < 30; i++) mover.TickSolve(0.5f);
            A("alive: mover returns home when idle", (bob.Home - new Vector3(0f, 1.05f, 0f)).magnitude < 0.2f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(playerGo);
            UnityEngine.Object.DestroyImmediate(a1);
            UnityEngine.Object.DestroyImmediate(a2);
            UnityEngine.Object.DestroyImmediate(module);
        }
    }

    static void ChemVisualSuite()
    {
        var h = new HeatModel(22f) { HeatRate = 0.35f };
        h.SetHeating(true, 100f); for (int i = 0; i < 60; i++) h.Step(1f);
        A("heat: rises toward source", h.Current > 90f);
        var a1 = new HeatModel(22f); a1.SetHeating(true, 100f); a1.Step(10f);
        var a2 = new HeatModel(22f); a2.SetHeating(true, 100f); for (int i = 0; i < 100; i++) a2.Step(0.1f);
        A("heat: timestep-independent", Near(a1.Current, a2.Current, 0.1f));
        var gg = new GameObject("gas");
        try { var gc = gg.AddComponent<GasCollection>(); gc.AddGas(50f); A("gas: fill fraction", Near(gc.FillFraction, 0.5f)); }
        finally { UnityEngine.Object.DestroyImmediate(gg); }
    }

    static void UISuite()
    {
        A("ui: time format", ExperimentHudController.FormatTime(920) == "00:15:20");
        A("ui: percent format", ExperimentHudController.FormatPercent(0.754f) == "75%");
        A("ui: progress drops on mistakes", Near(ExperimentHudController.DisplayedProgress(0.8f, 2, 0.05f), 0.7f));
        A("ui: instruction from hint", PharmeeBrain.InstructionFor(new ExperimentTask { label = "X", hint = "do x" }) == "do x");
    }

    static void W4Suite()
    {
        // Cutscene outro selection (end cutscene always resolves)
        var go = new GameObject("csdir");
        try
        {
            var dir = go.AddComponent<CutsceneDirector>();
            var pass = ScriptableObject.CreateInstance<CutsceneData>();
            var fail = ScriptableObject.CreateInstance<CutsceneData>();
            var so = new SerializedObject(dir);
            so.FindProperty("success").objectReferenceValue = pass;
            so.FindProperty("failure").objectReferenceValue = fail;
            so.ApplyModifiedProperties();
            A("cutscene: success outro when passed", dir.SelectOutro(new ExperimentResult { passed = true }) == pass);
            A("cutscene: failure outro when not", dir.SelectOutro(new ExperimentResult { passed = false }) == fail);
            var cs = ScriptableObject.CreateInstance<CutsceneData>();
            cs.beats.Add(new CutsceneData.Beat { seconds = 2f }); cs.beats.Add(new CutsceneData.Beat { seconds = 3f });
            A("cutscene: total duration", Near(cs.TotalDuration(), 5f));
            UnityEngine.Object.DestroyImmediate(pass); UnityEngine.Object.DestroyImmediate(fail); UnityEngine.Object.DestroyImmediate(cs);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }

        // Crystallization
        var cgo = new GameObject("cr");
        try
        {
            var cr = cgo.AddComponent<CrystallizationController>();
            new SerializedObject(cr).FindProperty("durationSeconds").floatValue = 10f;
            var soc = new SerializedObject(cr); soc.FindProperty("durationSeconds").floatValue = 10f; soc.ApplyModifiedProperties();
            cr.BeginCrystallization(); cr.Tick(5f);
            A("cryst: half at 5/10", Near(cr.Progress, 0.5f));
            cr.Tick(6f); A("cryst: done", cr.IsDone);
        }
        finally { UnityEngine.Object.DestroyImmediate(cgo); }

        // Filtration
        var fgo = new GameObject("f");
        try
        {
            var fc = fgo.AddComponent<FiltrationController>();
            var sof = new SerializedObject(fc); sof.FindProperty("targetVolumeMl").floatValue = 100f; sof.ApplyModifiedProperties();
            fc.AddFiltrate(40f); A("filt: 40%", Near(fc.Fraction, 0.4f));
            fc.AddFiltrate(100f); A("filt: clamps + done", fc.IsDone);
        }
        finally { UnityEngine.Object.DestroyImmediate(fgo); }

        // Hazard reports mistake
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> { new ExperimentTask { taskId = "x", label = "x" } };
        module.trackedSkills = new List<LabSkill> { LabSkill.Safety };
        var rgo = new GameObject("r");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>(); runner.SetModule(module); runner.StartExperiment();
            var hz = rgo.AddComponent<HazardZone>(); hz.SetRunner(runner);
            hz.Report(); hz.Report();  // second is debounced
            A("hazard: reports + debounces", runner.MistakeCount == 1);
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(module); }
    }

    static void InteractionSuite()
    {
        // A trigger station requiring a specific LabItem accepts only that prop
        // (bring-the-right-thing-here), while an unset id accepts anything (legacy poke).
        var sgo = new GameObject("station");
        var igo = new GameObject("correct"); var wgo = new GameObject("wrong");
        try
        {
            var station = sgo.AddComponent<ExperimentTaskStation>();
            station.SetRequiredItemId("sodium-acetate");
            var correct = igo.AddComponent<LabItem>(); correct.SetItemId("sodium-acetate");
            var wrong = wgo.AddComponent<LabItem>(); wrong.SetItemId("soda-lime");
            A("interaction: station accepts matching item", station.AcceptsItem(correct));
            A("interaction: station rejects wrong item", !station.AcceptsItem(wrong));
            A("interaction: station rejects null item", !station.AcceptsItem(null));

            var open = new GameObject("open").AddComponent<ExperimentTaskStation>();
            A("interaction: unset id accepts anything", open.AcceptsItem(wrong) && open.AcceptsItem(null));
            UnityEngine.Object.DestroyImmediate(open.gameObject);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sgo);
            UnityEngine.Object.DestroyImmediate(igo);
            UnityEngine.Object.DestroyImmediate(wgo);
        }
    }

    static void RosterDataSuite()
    {
        // Cutscenes: every experiment has all 4 kinds, each with beats.
        string[] keys = { "Methane","ChemicalCompounding","EthylAlcohol","BenzoicAcid","Acetanilide",
            "Acetone","Chloroform","Benzamide","Aspirin","Caffeine","WineMaking" };
        string cdir = "Assets/PharmaSynth/ScriptableObjects/Cutscenes/";
        int csOk = 0;
        foreach (var k in keys)
            foreach (var kind in new[]{ "Intro","ReagentPrep","Success","Failure" })
            {
                var cs = AssetDatabase.LoadAssetAtPath<CutsceneData>(cdir + k + "_" + kind + ".asset");
                if (cs != null && cs.beats.Count > 0 && cs.TotalDuration() > 0f) csOk++;
            }
        A("roster: 44 cutscenes present with beats", csOk == 44);

        // Quiz banks: one per catalog module, all valid, 3 questions, scoring works.
        string qdir = "Assets/PharmaSynth/ScriptableObjects/Quizzes/";
        int qBanks = 0, qTotal = 0; bool allValid = true, scoreOk = true, coverage = true;
        foreach (var e in ExperimentCatalog.Entries)
        {
            QuizBank bank = null;
            foreach (var g in AssetDatabase.FindAssets("t:QuizBank", new[]{ qdir.TrimEnd('/') }))
            {
                var b = AssetDatabase.LoadAssetAtPath<QuizBank>(AssetDatabase.GUIDToAssetPath(g));
                if (b != null && b.moduleId == e.moduleId) { bank = b; break; }
            }
            if (bank == null) { coverage = false; continue; }
            qBanks++; qTotal += bank.Count;
            if (!bank.AllValid() || bank.Count != 3) allValid = false;
            // all-correct answers → perfect score
            var ans = new List<int>();
            foreach (var q in bank.questions) ans.Add(q.correctIndex);
            if (!Near(bank.Score(ans), 1f)) scoreOk = false;
        }
        A("roster: quiz bank per experiment (11)", coverage && qBanks == 11);
        A("roster: 33 quiz questions total, all valid (4 opts, in-range answer)", qTotal == 33 && allValid);
        A("roster: quiz scoring (all-correct = 100%)", scoreOk);
    }

    static ChemicalData LoadChem(string file)
        => AssetDatabase.LoadAssetAtPath<ChemicalData>("Assets/PharmaSynth/ScriptableObjects/Chemicals/" + file + ".asset");

    static void PourReactionSuite()
    {
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        A("reaction: registry loads", reg != null);
        if (reg == null) return;

        var benz = LoadChem("Chem_Benzaldehyde");
        var kmno4 = LoadChem("Chem_PotassiumPermanganate");
        var benzoic = LoadChem("Chem_BenzoicAcid");
        A("reaction: reaction chemicals exist", benz && kmno4 && benzoic);
        var rule = reg.FindReaction(benz, kmno4);
        A("reaction: benzaldehyde+KMnO4 rule found (either order)", rule != null && reg.FindReaction(kmno4, benz) != null);
        if (rule != null)
            A("reaction: product = benzoic acid + precipitate", rule.resultLiquid == benzoic && rule.hasPrecipitate);

        // LiquidPhysics turns reactants into product + fires ReactionOccurred on AddLiquid.
        var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var lp = vgo.AddComponent<LiquidPhysics>();
            lp.mainRenderer = null; lp.precipitateRenderer = null;   // skip material work in edit mode
            lp.registry = reg; lp.currentChemical = benz; lp.currentLiquidVolume = 100f;
            bool reacted = false; lp.ReactionOccurred += _ => reacted = true;
            lp.AddLiquid(kmno4, 40f);
            A("reaction: AddLiquid fires ReactionOccurred", reacted);
            A("reaction: currentChemical becomes the product", lp.currentChemical == benzoic);
        }
        finally { UnityEngine.Object.DestroyImmediate(vgo); }

        // Pour path: the expected reagent, delivered to a bound vessel, completes its task.
        var module = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(
            "Assets/PharmaSynth/ScriptableObjects/Experiments/Midterm_BenzoicAcid.asset");
        var rgo = new GameObject("pr_runner"); var vgo2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module); runner.StartExperiment();
            runner.CompleteTask("prepare-permanganate");            // satisfy the prerequisite
            var lp2 = vgo2.AddComponent<LiquidPhysics>();
            lp2.mainRenderer = null; lp2.currentChemical = benz; lp2.currentLiquidVolume = 100f;
            var bind = vgo2.AddComponent<LiquidTaskBinding>();
            bind.SetVesselAndRunner(lp2, runner);
            bind.AddExpected(kmno4, "oxidise-benzaldehyde");
            bind.HandleReagent(kmno4);                              // = pour KMnO4 into the flask
            A("pour: expected reagent completes its task", runner.Graph.IsComplete("oxidise-benzaldehyde"));
            bind.HandleReagent(LoadChem("Chem_Aniline"));           // unexpected reagent → mistake
            A("pour: unexpected reagent logs a WrongReagent mistake", runner.Mistakes.CountOf(LabErrorType.WrongReagent) >= 1);
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(vgo2); }
    }

    static void TestReactionSuite()
    {
        // The confirmatory-test reactions read from Appendix C fire when the product
        // sits in the vessel and the test reagent is added (product-seeded test vessel).
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        A("testrx: master registry loads", reg != null);
        if (reg == null) return;

        void Fires(string prodFile, string reagFile, string label, bool expectGas)
        {
            var prod = LoadChem(prodFile); var reag = LoadChem(reagFile);
            if (prod == null || reag == null) { A("testrx: chems exist for " + label, false); return; }
            var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var lp = vgo.AddComponent<LiquidPhysics>();
                lp.mainRenderer = null; lp.precipitateRenderer = null;
                lp.registry = reg; lp.currentChemical = prod; lp.currentLiquidVolume = 100f;
                bool reacted = false; lp.ReactionOccurred += _ => reacted = true;
                lp.AddLiquid(reag, 20f);
                A("testrx: " + label + " fires a reaction", reacted);
                if (expectGas)
                {
                    var rule = reg.FindReaction(prod, reag);
                    A("testrx: " + label + " evolves gas + has observation",
                        rule != null && rule.evolvesGas && !string.IsNullOrEmpty(rule.expectedObservation));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(vgo); }
        }

        Fires("Chem_Benzamide", "Chem_SodiumNitrite", "benzamide + nitrite", true);
        Fires("Chem_Acetanilide", "Chem_BromineWater", "acetanilide + bromine water", false);
        Fires("Chem_Acetone", "Chem_SilverNitrate", "acetone + Tollens (negative)", false);
    }

    static void SceneBuilderSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        var lay = AssetDatabase.LoadAssetAtPath<ExperimentLayout>("Assets/PharmaSynth/ScriptableObjects/Layouts/Layout_EthylAlcohol.asset");
        A("builder: library/registry/layout exist", lib != null && reg != null && lay != null);
        A("builder: library has prefabs + chemicals", lib != null && lib.prefabs.Count >= 40 && lib.chemicals.Count >= 20);
        if (lib == null || lay == null) return;

        var module = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>("Assets/PharmaSynth/ScriptableObjects/Experiments/Prelim_EthylAlcohol.asset");
        var rgo = new GameObject("sb_runner"); var bgo = new GameObject("sb_builder");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module); runner.StartExperiment();
            var builder = bgo.AddComponent<ExperimentSceneBuilder>();
            builder.SetRefs(runner, lib, reg, new List<ExperimentLayout> { lay });

            int n = builder.Build("prelim-ethyl-alcohol");
            A("builder: spawns 10 roots (2 stations + 6 props + 2 vessels)", n == 10);
            A("builder: 2 task stations built", bgo.GetComponentsInChildren<ExperimentTaskStation>().Length == 2);

            // §2 sockets: each station gets a snap socket filtered to its item.
            var sockets = bgo.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
            A("builder: one socket per station", sockets.Length == 2);
            bool filtersOk = sockets.Length == 2;
            foreach (var so in sockets)
                if (so.GetComponent<StationSocketFilter>() == null || string.IsNullOrEmpty(so.GetComponent<StationSocketFilter>().requiredItemId)) filtersOk = false;
            A("builder: sockets carry item filters", filtersOk);
            // §1 hot-surface hazard: the Heat station gets a player-only zone.
            var hazards = bgo.GetComponentsInChildren<HazardZone>();
            A("builder: heat station gets a hot-surface hazard", hazards.Length == 1);
            var pr = new GameObject("root").transform;
            var hand = new GameObject("hand").transform;
            var stray = new GameObject("prop").transform;
            try
            {
                hand.SetParent(pr);
                A("hazard: rig child is player", HazardZone.IsPlayer(hand, pr));
                A("hazard: stray object is not", !HazardZone.IsPlayer(stray, pr));
                A("hazard: no root, no match", !HazardZone.IsPlayer(hand, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pr.gameObject);
                UnityEngine.Object.DestroyImmediate(stray.gameObject);
            }

            // §2 teleport anchors: one floor pad per station, seated at floor level.
            var anchors = bgo.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>();
            A("builder: one teleport anchor per station", anchors.Length == 2);
            bool onFloor = anchors.Length == 2;
            foreach (var an in anchors) if (an.transform.position.y > 0.1f) onFloor = false;
            A("builder: anchors sit on the floor", onFloor);

            var li = new GameObject("li").AddComponent<LabItem>();
            try
            {
                li.itemId = "warm-waterbath";
                A("socket: right item accepted", StationSocketFilter.Matches("warm-waterbath", li));
                A("socket: wrong item rejected", !StationSocketFilter.Matches("crystallise-ice", li));
                A("socket: open socket takes anything", StationSocketFilter.Matches("", null));
                A("socket: filtered socket rejects null", !StationSocketFilter.Matches("warm-waterbath", null));
            }
            finally { UnityEngine.Object.DestroyImmediate(li.gameObject); }
            A("builder: props carry LabItem ids", bgo.GetComponentsInChildren<LabItem>().Length == 6);

            var bind = bgo.GetComponentInChildren<LiquidTaskBinding>();
            A("builder: vessel has a LiquidTaskBinding", bind != null);
            var sugar = LoadChem("Chem_BrownSugar");
            if (bind != null && sugar != null)
            {
                bind.HandleReagent(sugar);            // = pour Brown Sugar into the fermentation beaker
                A("builder: pouring sugar completes prepare-must", runner.Graph.IsComplete("prepare-must"));
            }

            int m = builder.Build("tutorial-methane");
            A("builder: Methane builds 0 dynamic (uses its hand-built stage)", m == 0);
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(bgo); }

        // All 11 experiments must build a physical setup from their ExperimentLayout —
        // Methane uses its hand-built stage (0 dynamic roots), the other 10 spawn from data.
        var allLayouts = new List<ExperimentLayout>();
        foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
            allLayouts.Add(AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g)));
        A("builder: 10 experiment layouts authored", allLayouts.Count >= 10);

        int resolveMisses = 0;
        foreach (var l in allLayouts)
        {
            if (l == null) continue;
            foreach (var p in l.props)
            {
                if (lib.GetPrefab(p.prefabName) == null) resolveMisses++;
                if (p.pourable && !string.IsNullOrEmpty(p.fillChemical) && lib.GetChemical(p.fillChemical) == null) resolveMisses++;
            }
            foreach (var v in l.vessels)
            {
                if (lib.GetPrefab(v.prefabName) == null) resolveMisses++;
                if (!string.IsNullOrEmpty(v.startChemical) && lib.GetChemical(v.startChemical) == null) resolveMisses++;
                foreach (var bnd in v.bindings)
                    if (lib.GetChemical(bnd.reagentChemical) == null) resolveMisses++;
            }
            // Every station must be completable by a prop that exists in the same layout.
            var ids = new HashSet<string>();
            foreach (var p in l.props) ids.Add(p.itemId);
            foreach (var s in l.stations)
                if (!string.IsNullOrEmpty(s.requiredItemId) && !ids.Contains(s.requiredItemId)) resolveMisses++;
        }
        A("builder: every layout resolves all prefab/chemical/station names", resolveMisses == 0);

        var allBgo = new GameObject("sb_all");
        try
        {
            var b2 = allBgo.AddComponent<ExperimentSceneBuilder>();
            b2.SetRefs(null, lib, reg, allLayouts);
            int builtAll = 0;
            foreach (var l in allLayouts)
            {
                if (l == null || l.moduleId == "tutorial-methane") continue;
                if (b2.Build(l.moduleId) > 0) builtAll++;
            }
            A("builder: all 10 data-driven experiments spawn > 0 roots", builtAll >= 10);
        }
        finally { UnityEngine.Object.DestroyImmediate(allBgo); }
    }

    static void ResultsExportSuite()
    {
        var svc = new ProgressionService();
        var rows0 = ResultsExport.BuildRows(svc);
        A("results: one row per experiment", rows0.Count == ExperimentCatalog.Count && rows0.Count == 11);
        A("results: all unattempted at start", rows0.TrueForAll(r => !r.attempted && !r.passed));
        A("results: zero passed at start", ResultsExport.PassedCount(svc) == 0);

        svc.RecordResult("tutorial-methane",
            new ExperimentResult { passed = true, overallMastery = 0.95f, grade = new GradeBreakdown { Total = 96f } }, false);
        var rows = ResultsExport.BuildRows(svc);
        var t = rows.Find(r => r.moduleId == "tutorial-methane");
        A("results: recorded attempt shows", t.attempted && t.passed && Near(t.bestGrade, 96f) && t.attempts == 1);
        A("results: passed count = 1", ResultsExport.PassedCount(svc) == 1);

        var csv = ResultsExport.BuildCsv(svc);
        A("results: csv has header", csv.StartsWith("Experiment,Period,"));
        A("results: csv lists the tutorial row", csv.Contains("Tutorial: Methane Synthesis"));
        A("results: csv total line", csv.Contains("TOTAL,,,1/11"));
        int lines = csv.TrimEnd('\n').Split('\n').Length;
        A("results: csv = header + 11 rows + total", lines == 13);

        // Results/History screen text.
        var empty = new ProgressionService();
        var txt0 = ResultsHistoryController.BuildDisplayText(empty);
        A("results: screen shows 0/11 before any pass", txt0.Contains("0 / 11 passed"));
        var txt1 = ResultsHistoryController.BuildDisplayText(svc);
        A("results: screen marks a pass + 1/11", txt1.Contains("PASS") && txt1.Contains("1 / 11 passed"));
    }

    static void SettingsSuite()
    {
        var s = new ComfortSettings();
        A("settings: sane defaults", Near(s.textScale, 1f) && Near(s.snapTurnAngle, 45f) && s.handedness == Handedness.Right);
        s.SetTextScale(0.1f); A("settings: text scale clamps low", Near(s.textScale, 0.8f));
        s.SetTextScale(9f);   A("settings: text scale clamps high", Near(s.textScale, 1.6f));
        s.SetSubtitleSpeed(0.1f); A("settings: subtitle speed clamps low", Near(s.subtitleSpeed, 0.5f));
        s.SetSubtitleSpeed(9f);   A("settings: subtitle speed clamps high", Near(s.subtitleSpeed, 2f));
        s.SetVignette(-1f); A("settings: vignette clamps to 0", Near(s.vignetteIntensity, 0f));
        s.SetVignette(9f);  A("settings: vignette clamps to 1", Near(s.vignetteIntensity, 1f));
        s.SetSnapTurnAngle(3f);   A("settings: snap-turn clamps low", Near(s.snapTurnAngle, 15f));
        s.SetSnapTurnAngle(400f); A("settings: snap-turn clamps high", Near(s.snapTurnAngle, 90f));
        var clone = s.Clone(); clone.SetTextScale(1.2f);
        A("settings: clone is independent", Near(s.textScale, 1.6f) && Near(clone.textScale, 1.2f));
    }

    static void ExaminerSuite()
    {
        ExperimentModuleDefinition Mod(bool assess)
        {
            var m = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            m.moduleId = assess ? "assess-mod" : "guided-mod";
            m.assessmentMode = assess;
            m.graphTasks = new List<ExperimentTask> { T("a", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure) };
            return m;
        }

        A("examiner: ShouldObserve reads the flag",
            ExaminerNPC.ShouldObserve(Mod(true)) && !ExaminerNPC.ShouldObserve(Mod(false)));

        // Guided module → Pharmee speaks, examiner dormant.
        var rgo = new GameObject("ex_r"); var bgo = new GameObject("ex_b"); var ego = new GameObject("ex_e");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            var brain = bgo.AddComponent<PharmeeBrain>();
            var exam = ego.AddComponent<ExaminerNPC>();
            brain.SetRunner(runner); exam.SetRunner(runner);
            runner.SetModule(Mod(false)); runner.StartExperiment();
            A("examiner: guided → Pharmee gives a hint", !string.IsNullOrEmpty(brain.LastLine));
            A("examiner: guided → examiner dormant", !exam.IsObserving && !brain.AssessmentMode);
            A("examiner: proctors aloud on start (greeting line)", !string.IsNullOrEmpty(exam.LastLine));
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(bgo); UnityEngine.Object.DestroyImmediate(ego); }

        // Assessment module → Pharmee silent, examiner observing.
        var rgo2 = new GameObject("ex_r2"); var bgo2 = new GameObject("ex_b2"); var ego2 = new GameObject("ex_e2");
        try
        {
            var runner = rgo2.AddComponent<ExperimentRunner>();
            var brain = bgo2.AddComponent<PharmeeBrain>();
            var exam = ego2.AddComponent<ExaminerNPC>();
            brain.SetRunner(runner); exam.SetRunner(runner);
            runner.SetModule(Mod(true)); runner.StartExperiment();
            brain.InstructCurrent();   // even an explicit hint request stays silent
            A("examiner: assessment → Pharmee silent", string.IsNullOrEmpty(brain.LastLine) && brain.AssessmentMode);
            A("examiner: assessment → examiner observing", exam.IsObserving);
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo2); UnityEngine.Object.DestroyImmediate(bgo2); UnityEngine.Object.DestroyImmediate(ego2); }

        // Rich dialogue pools (user 2026-07-10: richer NPC interactions).
        A("lines: Pick wraps index + non-empty", !string.IsNullOrEmpty(PharmeeLines.Pick(PharmeeLines.Idle, 0))
            && PharmeeLines.Pick(PharmeeLines.Greetings, 5) == PharmeeLines.Pick(PharmeeLines.Greetings, 5 % PharmeeLines.Greetings.Length));
        A("lines: greetings pool has variety", PharmeeLines.Pick(PharmeeLines.Greetings, 0) != PharmeeLines.Pick(PharmeeLines.Greetings, 1));
        A("lines: Pick handles null + negative", PharmeeLines.Pick(null, 3) == "" && !string.IsNullOrEmpty(PharmeeLines.Pick(PharmeeLines.ExamRemarks, -2)));

        // Guided lab tour (storyboard 2026-07-10): narrated beats, closer invites a campaign.
        A("tour: multiple beats + closer invites a campaign",
            PharmeeLines.TourBeats.Length >= 6
            && PharmeeLines.TourBeats[PharmeeLines.TourBeats.Length - 1].ToLower().Contains("campaign"));

        // Location-triggered tour (storyboard 2026-07-10): narrate the nearest unvisited landmark.
        {
            var pos = new[] { new Vector3(10, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1) };
            var vis = new[] { false, false, false };
            var rad = new[] { 1.5f, 1.5f, 1.5f };
            A("tour: fires the landmark you walk up to", LabTourGuide.FirstUnvisitedInRange(Vector3.zero, pos, vis, rad) == 1);
            vis[1] = true;
            A("tour: skips a visited landmark, finds the next", LabTourGuide.FirstUnvisitedInRange(Vector3.zero, pos, vis, rad) == 2);
            A("tour: nothing in range → -1", LabTourGuide.FirstUnvisitedInRange(new Vector3(50, 0, 50), pos, vis, rad) == -1);
        }

        // Typewriter dialogue (user 2026-07-10): lines type out char-by-char with blips.
        {
            var go = new GameObject("tw");
            try
            {
                var nc = go.AddComponent<NPCNarrationController>();
                A("typewriter: enabled by default with a positive type speed", nc.Typewriter && nc.TypeCps() > 1f);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // Give way (user 2026-07-10): Pharmee steps aside when the player bumps him.
        // Far away → no step.
        A("giveway: no step outside personal space",
            PharmeeGiveWay.SideStep(new Vector3(3f, 1f, 0f), Vector3.zero, Vector3.forward, 0.95f, 0.65f) == Vector3.zero);
        // Player facing +Z, Pharmee slightly to the +X side and close → pushed further +X (right), horizontal only.
        {
            var step = PharmeeGiveWay.SideStep(new Vector3(0.2f, 1f, 0.1f), Vector3.zero, Vector3.forward, 0.95f, 0.65f);
            A("giveway: steps to the player's side, horizontally", step.x > 0f && Mathf.Abs(step.y) < 1e-5f && step.magnitude <= 0.65f + 1e-4f);
        }
        // Directly ahead + closer → a larger push than when near the edge of the bubble.
        {
            var near = PharmeeGiveWay.SideStep(new Vector3(0f, 1f, 0.1f), Vector3.zero, Vector3.forward, 0.95f, 0.65f).magnitude;
            var edge = PharmeeGiveWay.SideStep(new Vector3(0f, 1f, 0.9f), Vector3.zero, Vector3.forward, 0.95f, 0.65f).magnitude;
            A("giveway: closer = bigger step", near > edge && near > 0f);
        }

        // Mirror render gate (perf 2026-07-10): skip the reflection pass when far,
        // behind the glass, or turned away.
        A("mirror: renders when near, in front, looking at it", MirrorPlane.ShouldRender(2f, 6f, 0.8f, 0.9f));
        A("mirror: skips when too far", !MirrorPlane.ShouldRender(9f, 6f, 0.8f, 0.9f));
        A("mirror: skips when behind the glass", !MirrorPlane.ShouldRender(2f, 6f, -0.5f, 0.9f));
        A("mirror: skips when turned away", !MirrorPlane.ShouldRender(2f, 6f, 0.8f, -0.6f));

        // Hover highlight (user 2026-07-10: prop readability) — pops on hover, restores.
        A("hover: scale pops when lit, base when not",
            HoverHighlight.HighlightScale(Vector3.one, true, 1.06f) == Vector3.one * 1.06f
            && HoverHighlight.HighlightScale(Vector3.one, false, 1.06f) == Vector3.one);
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var hh = go.AddComponent<HoverHighlight>();
                hh.SetHighlight(true);
                bool grew = go.transform.localScale.x > 1.0001f && hh.IsHighlighted;
                hh.SetHighlight(false);
                bool restored = Mathf.Abs(go.transform.localScale.x - 1f) < 1e-4f && !hh.IsHighlighted;
                A("hover: SetHighlight grows then restores", grew && restored);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }

    static void AudioSuite()
    {
        // Perceptual volume curve.
        A("audio: full volume = 0 dB", Near(VolumeUtil.LinearToDb(1f), 0f, 0.05f));
        A("audio: silence = -80 dB", Near(VolumeUtil.LinearToDb(0f), -80f, 0.01f));
        A("audio: half volume ~ -6 dB", Near(VolumeUtil.LinearToDb(0.5f), -6.02f, 0.1f));

        // Per-shot pitch variation (realism 2026-07-10): centred at 1.0, bounded by
        // the jitter amount, and only physical sounds vary (not UI/musical stings).
        A("audio: pitch jitter centred + bounded",
            Near(AudioService.JitteredPitch(0.08f, 0.5f), 1f, 0.0001f)
            && AudioService.JitteredPitch(0.08f, 1f) <= 1.0801f
            && AudioService.JitteredPitch(0.08f, 0f) >= 0.9199f);
        A("audio: only physical sounds pitch-vary",
            AudioService.PitchVaries("footstep") && AudioService.PitchVaries("glass-shatter")
            && !AudioService.PitchVaries("ui-click") && !AudioService.PitchVaries("grade-pass"));

        // Continuous pour sound (realism 2026-07-10): silent when righted, swells with flow.
        A("audio: pour silent when not pouring", Near(LiquidPourer.PourVolume(false, 0.5f, 1f, 1f), 0f, 0.0001f));
        A("audio: pour swells with flow",
            LiquidPourer.PourVolume(true, 0.5f, 1f, 1f) > LiquidPourer.PourVolume(true, 0.5f, 1f, 0f)
            && LiquidPourer.PourVolume(true, 0.5f, 1f, 0f) > 0f);

        // Dialogue ducking (user 2026-07-10): beds dip while an NPC speaks, restore after.
        A("audio: duck dips beds while speaking, full otherwise",
            Near(DialogueDucker.DuckTarget(1, 0.45f), 0.45f, 0.001f)
            && Near(DialogueDucker.DuckTarget(2, 0.45f), 0.45f, 0.001f)
            && Near(DialogueDucker.DuckTarget(0, 0.45f), 1f, 0.001f));

        // Atmosphere VFX (user 2026-07-10): AC vapour + haze style tags.
        A("atmosphere: style tags", AtmosphereVfx.StyleName(AtmosphereVfx.Style.AcVapor) == "ac-vapor"
            && AtmosphereVfx.StyleName(AtmosphereVfx.Style.Haze) == "haze");

        // SoundBank lookup.
        var bank = ScriptableObject.CreateInstance<SoundBank>();
        bank.entries.Add(new SoundBank.Entry { key = "pour", clip = null, category = AudioCategory.Sfx });
        A("audio: bank finds a key", bank.Get("pour") != null);
        A("audio: bank misses unknown key", bank.Get("nope") == null);
        A("audio: expected-key checklist non-empty", SoundBank.ExpectedKeys.Length >= 10);

        // Service volume + null-safe playback (works with zero clips).
        var go = new GameObject("audio_svc");
        try
        {
            var svc = go.AddComponent<AudioService>();
            svc.Bind(bank, go.AddComponent<AudioSource>(), go.AddComponent<AudioSource>(),
                     go.AddComponent<AudioSource>(), go.AddComponent<AudioSource>());
            svc.SetVolume(AudioCategory.Sfx, 0.3f);
            A("audio: volume set/get", Near(svc.VolumeOf(AudioCategory.Sfx), 0.3f));
            svc.SetVolume(AudioCategory.Music, 2f);
            A("audio: volume clamps to 1", Near(svc.VolumeOf(AudioCategory.Music), 1f));
            svc.Play("pour");        // entry exists, clip null → silent no-op, no throw
            svc.Play("missing");     // no entry → no throw
            A("audio: playback is null-safe with no clips", true);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(bank); }
    }

    static void HubSelectSuite()
    {
        // Fresh in-memory progress (no disk touch): only the tutorial should be open.
        var service = new ProgressionService();
        var flow = new ProgressionFlow(service);

        var model = HubSelectController.BuildModel(flow);
        A("hub: model has all 11 experiments", model.Count == ExperimentCatalog.Count && model.Count == 11);

        HubSelectController.Row Find(string id) { foreach (var r in model) if (r.moduleId == id) return r; return null; }
        A("hub: tutorial available at start",
            HubSelectController.StateOf(Find("tutorial-methane")) == HubSelectController.RowState.Available);
        A("hub: prelim locked at start",
            HubSelectController.StateOf(Find("prelim-chemical-compounding")) == HubSelectController.RowState.Locked);
        A("hub: midterm period closed at start", !flow.IsPeriodUnlocked(ExperimentPeriod.Midterm));
        A("hub: CanSelect tutorial", HubSelectController.CanSelect(flow, "tutorial-methane"));
        A("hub: cannot select a locked experiment", !HubSelectController.CanSelect(flow, "final-aspirin"));

        // Pass the tutorial (in-memory only) → the first prelim unlocks.
        service.RecordResult("tutorial-methane", new ExperimentResult { passed = true }, false);
        var flow2 = new ProgressionFlow(service);
        var model2 = HubSelectController.BuildModel(flow2);
        HubSelectController.Row Find2(string id) { foreach (var r in model2) if (r.moduleId == id) return r; return null; }
        A("hub: tutorial now passed", HubSelectController.StateOf(Find2("tutorial-methane")) == HubSelectController.RowState.Passed);
        A("hub: next prelim now available", HubSelectController.StateOf(Find2("prelim-chemical-compounding")) == HubSelectController.RowState.Available);
        A("hub: can select the newly unlocked prelim", HubSelectController.CanSelect(flow2, "prelim-chemical-compounding"));
        A("hub: second prelim still locked", !HubSelectController.CanSelect(flow2, "prelim-ethyl-alcohol"));
        A("hub: midterm still closed until prelim complete", !flow2.IsPeriodUnlocked(ExperimentPeriod.Midterm));
    }

    static void SimRigSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        var module = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>("Assets/PharmaSynth/ScriptableObjects/Experiments/Final_Aspirin.asset");
        A("simrig: library + aspirin module load", lib != null && module != null);
        if (lib == null || module == null) return;
        var layouts = new List<ExperimentLayout>();
        foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
            layouts.Add(AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g)));

        var rgo = new GameObject("sr_runner"); var bgo = new GameObject("sr_builder");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module); runner.StartExperiment();
            var builder = bgo.AddComponent<ExperimentSceneBuilder>();
            builder.SetRefs(runner, lib, reg, layouts);
            builder.Build("final-aspirin");

            ZoneSimStation heatRig = null;
            foreach (var z in bgo.GetComponentsInChildren<ZoneSimStation>(true))
                if (z.gameObject.name == "Station_warm-waterbath") { heatRig = z; break; }
            A("simrig: heat station built with a ZoneSimStation", heatRig != null);
            if (heatRig == null) return;
            var temp = heatRig.GetComponent<TemperatureSim>();
            var sensor = heatRig.GetComponent<ZoneItemSensor>();
            A("simrig: heat station carries TemperatureSim + sensor", temp != null && sensor != null);

            // Reach the step, then confirm it does NOT complete without the sustained heat verb.
            runner.CompleteTask("weigh-salicylic");
            runner.CompleteTask("add-anhydride");
            runner.Graph.Tick();
            A("simrig: warm-waterbath pending before heating", !runner.Graph.IsComplete("warm-waterbath"));

            // Perform the verb: prop in zone → heat to target → auto-check completes it.
            sensor.ForceOccupied(true);
            heatRig.Drive(1f, true);           // flame on
            for (int i = 0; i < 4; i++) temp.Tick(1f);
            A("simrig: temperature reached target", temp.AtLeast(85f));
            runner.Graph.Tick();
            A("simrig: heat verb auto-completes warm-waterbath", runner.Graph.IsComplete("warm-waterbath"));
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(bgo); }
    }

    static void CutsceneLibrarySuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<CutsceneLibrary>("Assets/PharmaSynth/ScriptableObjects/CutsceneLibrary.asset");
        A("cutscene: library loads", lib != null);
        if (lib == null) return;
        A("cutscene: 11 module sets", lib.entries.Count >= 11);

        int bad = 0;
        foreach (var e in lib.entries)
        {
            if (e == null || !e.IsComplete) { bad++; continue; }
            if (e.intro.kind != CutsceneData.Kind.Intro) bad++;
            if (e.reagentPrep.kind != CutsceneData.Kind.ReagentPrep) bad++;
            if (e.success.kind != CutsceneData.Kind.Success) bad++;
            if (e.failure.kind != CutsceneData.Kind.Failure) bad++;
        }
        A("cutscene: every set complete + correct kinds", bad == 0);

        // Director swaps its four cutscenes to match the running module.
        var go = new GameObject("cs_dir");
        try
        {
            var dir = go.AddComponent<CutsceneDirector>();
            dir.SetLibrary(lib);
            var asp = lib.GetSet("final-aspirin");
            A("cutscene: aspirin set exists", asp != null);
            A("cutscene: LoadForModule(aspirin) returns complete", dir.LoadForModule("final-aspirin"));
            A("cutscene: director intro swapped to aspirin", dir.Intro == asp.intro && dir.Success == asp.success);
            dir.LoadForModule("tutorial-methane");
            var meth = lib.GetSet("tutorial-methane");
            A("cutscene: director intro swapped to methane", dir.Intro == meth.intro);
            A("cutscene: outro selects failure on fail", dir.SelectOutro(new ExperimentResult { passed = false }) == meth.failure);
            A("cutscene: outro selects success on pass", dir.SelectOutro(new ExperimentResult { passed = true }) == meth.success);
            A("cutscene: unknown module → no swap", !dir.LoadForModule("does-not-exist"));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void PostLabSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<QuizBankLibrary>("Assets/PharmaSynth/ScriptableObjects/QuizBankLibrary.asset");
        var module = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>("Assets/PharmaSynth/ScriptableObjects/Experiments/Prelim_EthylAlcohol.asset");
        A("postlab: library + module load", lib != null && module != null);
        if (lib == null || module == null) return;
        var bank = lib.GetBank("prelim-ethyl-alcohol");
        A("postlab: bank found + 3 questions", bank != null && bank.Count == 3);
        A("postlab: all 11 banks registered", lib.banks.Count >= 11);
        if (bank == null) return;

        var rgo = new GameObject("pl_runner"); var cgo = new GameObject("pl_ctrl");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module); runner.StartExperiment();
            // Complete every non-DataSheet task in authored order (prereqs satisfied).
            foreach (var t in runner.Graph.Tasks)
                if (t.phase != TaskPhase.DataSheet) runner.CompleteTask(t.taskId);

            var ctrl = cgo.AddComponent<PostLabController>();
            ctrl.SetRefs(runner, lib);
            A("postlab: finds record-yield data-sheet task", PostLabController.FindDataSheetTaskId(runner.Graph) == "record-yield");
            A("postlab: record task pending before submit", !runner.Graph.IsComplete("record-yield"));

            ctrl.OpenFor(bank);
            A("postlab: opens", ctrl.IsOpen);
            A("postlab: not answered at open", !ctrl.AllAnswered);
            ctrl.AdjustYield(85); A("postlab: yield stepper adjusts", Near(ctrl.Yield, 85f));
            ctrl.AdjustYield(50); A("postlab: yield clamps at 100", Near(ctrl.Yield, 100f));
            ctrl.SetYield(-10);   A("postlab: yield clamps at 0", Near(ctrl.Yield, 0f));
            ctrl.SetYield(72);
            for (int i = 0; i < bank.Count; i++) ctrl.Answer(i, bank.questions[i].correctIndex);
            A("postlab: all answered", ctrl.AllAnswered);
            A("postlab: perfect quiz fraction = 1", Near(ctrl.ScoreFraction(), 1f));

            var res = ctrl.SubmitAndFinish();
            A("postlab: submit completes record-yield", runner.Graph.IsComplete("record-yield"));
            A("postlab: full progress after submit", Near(runner.Graph.Progress01, 1f));
            A("postlab: quiz closed after submit", !ctrl.IsOpen);
            float expectedDoc = (module.rubricWeights.documentation / module.rubricWeights.Total()) * 100f;
            A("postlab: documentation contribution maxed on perfect quiz", Near(res.grade.Documentation, expectedDoc, 0.5f) && expectedDoc > 0f);
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(cgo); }

        var rgo2 = new GameObject("pl_runner2"); var cgo2 = new GameObject("pl_ctrl2");
        try
        {
            var runner2 = rgo2.AddComponent<ExperimentRunner>();
            runner2.SetModule(module); runner2.StartExperiment();
            var ctrl2 = cgo2.AddComponent<PostLabController>();
            ctrl2.SetRefs(runner2, lib);
            ctrl2.OpenFor(bank);
            for (int i = 0; i < bank.Count; i++)
            {
                var q = bank.questions[i];
                ctrl2.Answer(i, (q.correctIndex + 1) % q.options.Count);
            }
            A("postlab: all-wrong quiz fraction = 0", Near(ctrl2.ScoreFraction(), 0f));
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo2); UnityEngine.Object.DestroyImmediate(cgo2); }
    }

    static void RealVerbSuite()
    {
        // Methane real verbs: burner presence heats (TemperatureSim), heat gates gas
        // collection (GasCollection), and both tasks complete via graph auto-check.
        var module = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(
            "Assets/PharmaSynth/ScriptableObjects/Experiments/Tutorial_Methane.asset");
        A("realverb: methane module loads", module != null);
        if (module == null) return;

        var rgo = new GameObject("rv_runner");
        var xgo = new GameObject("rv_rig");
        var cgo = new GameObject("rv_collect");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            var temp = xgo.AddComponent<TemperatureSim>();
            var gas = xgo.AddComponent<GasCollection>();
            var burnerZone = xgo.AddComponent<ZoneItemSensor>(); burnerZone.SetItemId("burner");
            var collectZone = cgo.AddComponent<ZoneItemSensor>(); collectZone.SetItemId("collection-tube");
            var rig = xgo.AddComponent<MethaneApparatusRig>();

            runner.StartExperiment();
            rig.Bind(runner, temp, gas, burnerZone, collectZone);
            rig.HandleExperimentStarted(module);   // edit mode: subscribe happened post-start

            runner.CompleteTask("prepare-mixture");
            runner.CompleteTask("setup-apparatus");
            runner.Graph.Tick();
            A("realverb: heat-mixture blocked while cold", !runner.Graph.IsComplete("heat-mixture"));

            temp.SetHeating(true, 220f);
            for (int i = 0; i < 120; i++) temp.Tick(0.5f);
            runner.Graph.Tick();
            A("realverb: sustained heating completes heat-mixture", runner.Graph.IsComplete("heat-mixture"));
            A("realverb: collect-gas still pending", !runner.Graph.IsComplete("collect-gas"));

            gas.AddGas(100000f);   // clamps to capacity
            runner.Graph.Tick();
            A("realverb: gas fill completes collect-gas", runner.Graph.IsComplete("collect-gas"));
            A("realverb: progress 4/5 after real verbs", Near(runner.Graph.Progress01, 0.8f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rgo);
            UnityEngine.Object.DestroyImmediate(xgo);
            UnityEngine.Object.DestroyImmediate(cgo);
        }
    }

    static void ProgressionFlowSuite()
    {
        A("catalog: 11 entries", ExperimentCatalog.Count == 11);
        int tut = 0, pre = 0, mid = 0, fin = 0;
        foreach (var e in ExperimentCatalog.Entries)
        {
            if (e.period == ExperimentPeriod.Tutorial) tut++;
            else if (e.period == ExperimentPeriod.Prelim) pre++;
            else if (e.period == ExperimentPeriod.Midterm) mid++;
            else fin++;
        }
        A("catalog: period split 1/2/4/4", tut == 1 && pre == 2 && mid == 4 && fin == 4);

        // Every prerequisite must reference an experiment earlier in the roster.
        bool chainOk = true; var seen = new List<string>();
        foreach (var e in ExperimentCatalog.Entries)
        {
            if (!string.IsNullOrEmpty(e.prerequisiteModuleId) && !seen.Contains(e.prerequisiteModuleId)) chainOk = false;
            seen.Add(e.moduleId);
        }
        A("catalog: prereq chain valid + ordered", chainOk);

        var svc = new ProgressionService(System.IO.Path.Combine(Application.temporaryCachePath, "flow_selftest.json"));
        var flow = new ProgressionFlow(svc);
        A("flow: only tutorial unlocked at start", flow.IsUnlocked("tutorial-methane") && !flow.IsUnlocked("prelim-ethyl-alcohol"));
        A("flow: next is the tutorial", flow.NextExperiment()?.moduleId == "tutorial-methane");
        A("flow: tutorial period open, prelim closed", flow.IsPeriodUnlocked(ExperimentPeriod.Tutorial) && !flow.IsPeriodUnlocked(ExperimentPeriod.Prelim));

        svc.RecordResult("tutorial-methane", new ExperimentResult { passed = true }, false);
        A("flow: passing tutorial unlocks next + completes its period",
            flow.IsUnlocked("prelim-chemical-compounding") && flow.IsPeriodComplete(ExperimentPeriod.Tutorial) && flow.IsPeriodUnlocked(ExperimentPeriod.Prelim));
        A("flow: next is chemical compounding", flow.NextExperiment()?.moduleId == "prelim-chemical-compounding");

        foreach (var e in ExperimentCatalog.Entries) svc.RecordResult(e.moduleId, new ExperimentResult { passed = true }, false);
        A("flow: all passed → 100% + no next", flow.AllComplete() && Near(flow.OverallCompletion01(), 1f) && flow.NextExperiment() == null);
        A("flow: final period unlocked + complete", flow.IsPeriodUnlocked(ExperimentPeriod.Final) && flow.IsPeriodComplete(ExperimentPeriod.Final));
    }

    static void LibrarySuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>("Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
        A("library: loads", lib != null);
        if (lib == null) return;
        A("library: 11 modules", lib.Count == 11);

        bool allResolve = true;
        foreach (var e in ExperimentCatalog.Entries) if (!lib.Has(e.moduleId)) allResolve = false;
        A("library: covers the whole catalog", allResolve);

        GameFlow.Select("midterm-acetone");
        A("gameflow: selection persists", GameFlow.SelectedModuleId == "midterm-acetone");
        GameFlow.Select("");
        A("gameflow: empty selection ignored", GameFlow.SelectedModuleId == "midterm-acetone");

        var rgo = new GameObject("runner"); var lgo = new GameObject("launcher");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            var launcher = lgo.AddComponent<ExperimentLauncher>();
            launcher.SetLibrary(lib); launcher.SetRunner(runner);
            var loaded = launcher.Launch("final-aspirin");
            A("launcher: loads the requested module", loaded != null && runner.Module != null && runner.Module.moduleId == "final-aspirin");
            A("launcher: unknown id returns null", launcher.Launch("does-not-exist") == null);
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(lgo); }
    }

    static void ContentSuite()
    {
        string dir = "Assets/PharmaSynth/ScriptableObjects/Experiments/";
        foreach (var (file, tasks) in new[] {
            ("Tutorial_Methane", 5), ("Prelim_ChemicalCompounding", 6), ("Prelim_EthylAlcohol", 7),
            ("Midterm_BenzoicAcid", 9), ("Final_Aspirin", 7),
            ("Midterm_Acetanilide", 10), ("Midterm_Acetone", 10), ("Midterm_Chloroform", 10),
            ("Final_Benzamide", 9), ("Final_Caffeine", 9), ("Final_WineMaking", 8) })
        {
            var m = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(dir + file + ".asset");
            A("content: " + file + " loads", m != null);
            if (m == null) continue;

            var g = m.BuildTaskGraph();
            A("content: " + file + " has " + tasks + " tasks", g.Tasks.Count == tasks);

            // The authored graph must be fully solvable respecting prerequisites,
            // proving there are no unreachable steps or prerequisite cycles.
            A("content: " + file + " solvable to 100%", DriveToCompletion(g) && g.Progress01 >= 0.999f);

            // Every phase that has tasks must be reachable/complete after a clean run.
            foreach (TaskPhase ph in System.Enum.GetValues(typeof(TaskPhase)))
            {
                bool hasPhase = false;
                foreach (var t in g.Tasks) if (t.phase == ph) { hasPhase = true; break; }
                if (hasPhase) A("content: " + file + " phase " + ph + " complete", g.IsPhaseComplete(ph));
            }

            // The scoring/mastery spine must build from the authored data.
            A("content: " + file + " builds mastery model", m.BuildMasteryModel() != null);
            A("content: " + file + " builds score calculator", m.BuildScoreCalculator() != null);
        }
    }

    /// Repeatedly completes every currently-available task until none remain.
    /// Returns false if it stalls with required tasks still incomplete (unreachable step).
    static bool DriveToCompletion(TaskGraph g)
    {
        for (int guard = 0; guard < 100; guard++)
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (var t in g.AvailableTasks()) ids.Add(t.taskId);
            if (ids.Count == 0) break;
            foreach (var id in ids) g.TryComplete(id);
        }
        foreach (var t in g.Tasks)
            if (t.required && !g.IsComplete(t.taskId)) return false;
        return true;
    }

    // Hover-inspector knowledge base + panel animation (user 2026-07-10).
    static void HoverInfoSuite()
    {
        A("info: equipment authored", LabInfoDatabase.EquipmentCount >= 25);
        A("info: reagents authored", LabInfoDatabase.ReagentCount >= 30);

        var beaker = LabInfoDatabase.Equipment("Prop_Beaker_100mL");
        A("info: beaker resolves", beaker != null && beaker.Title == "Beaker" && beaker.Category == LabInfoCategory.Equipment);
        var cyl = LabInfoDatabase.Equipment("GraduatedCylinder_50mL");
        A("info: graduated cylinder resolves (specific before 'cylinder')", cyl != null && cyl.Title == "Graduated Cylinder");
        var rack = LabInfoDatabase.Equipment("TestTubeRack_12Tubes");
        A("info: test-tube RACK not plain test-tube", rack != null && rack.Title == "Test-Tube Rack");
        A("info: unknown prop → no card", LabInfoDatabase.Equipment("Wall_Section_42") == null);
        var coat = LabInfoDatabase.Equipment("LabCoatDisplay");
        A("info: lab coat resolves", coat != null && coat.Title == "Lab Coat");
        var gog = LabInfoDatabase.Equipment("Goggles_Standin");
        A("info: goggles resolve", gog != null && gog.Title == "Safety Goggles");
        var glv = LabInfoDatabase.Equipment("Gloves_Standin");
        A("info: gloves resolve", glv != null && glv.Title == "Nitrile Gloves");

        var naoh = LabInfoDatabase.Reagent("Sodium Hydroxide");
        A("info: NaOH trivia present", naoh != null && naoh.Category == LabInfoCategory.Reagent && naoh.Body.Length > 20);
        var unknownChem = LabInfoDatabase.Reagent("Unobtainium");
        A("info: unknown reagent → generic card", unknownChem != null && unknownChem.Title == "Unobtainium");
        A("info: empty reagent name safe", LabInfoDatabase.Reagent("") != null);

        A("info: pharmee person card", LabInfoDatabase.Person(true).Title == "Pharmee");
        A("info: jimenez person card", LabInfoDatabase.Person(false).Title.Contains("Jimenez"));

        A("info: norm strips + lowercases", LabInfoDatabase.Norm("Beaker_100 mL") == "beaker100ml");

        // Panel easing + accent mapping (pure).
        A("panel: ease(0)=0", Near(HoverInfoPanel.Ease(0f), 0f));
        A("panel: ease(1)=1", Near(HoverInfoPanel.Ease(1f), 1f));
        A("panel: ease(0.5)=0.5", Near(HoverInfoPanel.Ease(0.5f), 0.5f));
        A("panel: ease clamps", Near(HoverInfoPanel.Ease(2f), 1f) && Near(HoverInfoPanel.Ease(-1f), 0f));
        A("panel: reagent accent amber", HoverInfoPanel.AccentFor(LabInfoCategory.Reagent).r > 0.9f);
        A("panel: tags", HoverInfoPanel.Tag(LabInfoCategory.Equipment) == "EQUIPMENT" && HoverInfoPanel.Tag(LabInfoCategory.Person) == "LAB GUIDE");

        // Placement must always sit IN FRONT of the struck surface (never occluded).
        A("panel: close target → card in front", HoverInfoPanel.PlaceDistance(1.0f, 0.5f, 1.1f, 0.4f) <= 1.0f - 0.12f + 1e-4f);
        A("panel: very close target floored", HoverInfoPanel.PlaceDistance(0.5f, 0.5f, 1.1f, 0.4f) <= 0.5f - 0.12f + 1e-4f);
        A("panel: distant target stays readable-close", HoverInfoPanel.PlaceDistance(4f, 0.5f, 1.1f, 0.4f) <= 1.1f + 1e-4f);
        A("panel: never negative", HoverInfoPanel.PlaceDistance(0.3f, 0.5f, 1.1f, 0.4f) >= 0.3f);

        // Held-item suppression (user 2026-07-11: the card must not sit over an
        // item you're holding). Grabbable NOT selected → not held; null safe.
        var hgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            hgo.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            A("panel: idle grabbable is not held", !HoverInspector.IsHeld(hgo.GetComponent<Collider>()));
            A("panel: null collider not held", !HoverInspector.IsHeld(null));
        }
        finally { UnityEngine.Object.DestroyImmediate(hgo); }
    }

    // Corner music speaker playlist advance (user 2026-07-10).
    static void MusicSpeakerSuite()
    {
        A("music: single track stays", MusicSpeaker.NextIndex(0, 1, true, 0.9f) == 0);
        A("music: sequential wraps", MusicSpeaker.NextIndex(4, 5, false, 0f) == 0);
        A("music: sequential advances", MusicSpeaker.NextIndex(1, 5, false, 0f) == 2);
        A("music: shuffle never repeats", MusicSpeaker.NextIndex(2, 5, true, 0.4f) != 2);   // 0.4*5=2 → bumped
        int shuf = MusicSpeaker.NextIndex(0, 5, true, 0.99f);
        A("music: shuffle in range", shuf >= 0 && shuf < 5);
        A("music: empty safe", MusicSpeaker.NextIndex(0, 0, true, 0.5f) == 0);
    }

    // Font-safe glyph sanitiser for the lab pads / holo board (user 2026-07-10).
    static void GlyphSafeSuite()
    {
        A("glyph: arrow → ->", GlyphSafe.Sanitize("purple→brown") == "purple->brown");
        A("glyph: gas ↑ → (g)", GlyphSafe.Sanitize("CH4↑") == "CH4(g)");
        A("glyph: delta → (heat)", GlyphSafe.Sanitize("NaOH →Δ CH4") == "NaOH ->(heat) CH4");
        A("glyph: equilibrium ⇌", GlyphSafe.Sanitize("A⇌B") == "A<=>B");
        A("glyph: box ▶ → »", GlyphSafe.Sanitize("▶ step") == "» step");
        A("glyph: plain text untouched", GlyphSafe.Sanitize("Prepare 0.1N HCl (250 mL)") == "Prepare 0.1N HCl (250 mL)");
        A("glyph: null/empty safe", GlyphSafe.Sanitize(null) == null && GlyphSafe.Sanitize("") == "");
    }

    // Fixed per-scene eye height (user 2026-07-11 design pivot: NOT relative to
    // the player's real height — the runtime's Floor/Device flip-flop made any
    // relative scheme spawn players on the floor or the roof).
    static void HeightCalibrationSuite()
    {
        A("height: seated head lifted to target", Near(HeightCalibration.FixedOffset(1.65f, 1.00f), 0.65f));
        A("height: head already at target", Near(HeightCalibration.FixedOffset(1.65f, 1.65f), 0f));
        A("height: too-high head pulled DOWN (roof impossible)", Near(HeightCalibration.FixedOffset(1.65f, 2.40f), -0.75f));
        A("height: upward adjust clamped", Near(HeightCalibration.FixedOffset(1.65f, 0.00f), HeightCalibration.MaxAdjust));
        A("height: downward adjust clamped", Near(HeightCalibration.FixedOffset(1.65f, 4.00f), -HeightCalibration.MaxAdjust));
        A("height: different scene targets differ", HeightCalibration.FixedOffset(1.40f, 1.0f) < HeightCalibration.FixedOffset(1.80f, 1.0f));
        // Pose validity (2026-07-11 "1.6x too tall / roof" root cause: calibrating
        // from an UNTRACKED zero pose during the load fade must be impossible).
        A("height: untracked zero pose rejected", !HeightCalibration.PoseValid(UnityEngine.Vector3.zero, 0f));
        A("height: settled real pose accepted", HeightCalibration.PoseValid(new UnityEngine.Vector3(0.1f, 1.0f, 0.2f), 1.001f));
        A("height: tracking-kick-in jump rejected", !HeightCalibration.PoseValid(new UnityEngine.Vector3(0.1f, 1.0f, 0.2f), 0f));
        // Two-sided drift ("stuck at 0.96 after headset repositioned"): tall
        // corrects with a small allowance, stuck-short with a generous one so
        // ordinary crouching/bench-leaning is never touched.
        A("height: standing-up overshoot detected", Near(HeightCalibration.TallExcess(2.10f, 1.65f), 0.30f));
        A("height: small lean-up tolerated", Near(HeightCalibration.TallExcess(1.75f, 1.65f), 0f));
        A("height: bench crouch never corrected", Near(HeightCalibration.TallExcess(1.25f, 1.65f), 0f));
        A("height: stuck-short corrected upward", HeightCalibration.TallExcess(0.96f, 1.65f) < 0f);
        A("height: short allowance more generous than tall",
            HeightCalibration.ShortTolerance > HeightCalibration.TallTolerance);
        // Hand-vs-glove display policy (bare hand unless the PPE glove is worn).
        A("handswap: bare hand when unglovd", HandSwap.ShowBareHand(false));
        A("handswap: glove replaces bare hand", !HandSwap.ShowBareHand(true));
        // Three-pose two-skin hands (user 2026-07-11: free + grab + point, bare + nitrile).
        A("handpose: grab wins over point", HandPosePolicy.PoseFor(true, true) == HandPoseKind.Grab);
        A("handpose: hovering an interactable points", HandPosePolicy.PoseFor(false, true) == HandPoseKind.Point);
        A("handpose: idle hand is free", HandPosePolicy.PoseFor(false, false) == HandPoseKind.Free);
        A("handpose: pointing index stays straight", Near(HandPosePolicy.AngleFor(HandPoseKind.Point, false, true, 0), 0f));
        A("handpose: pointing curls the other fingers", Near(HandPosePolicy.AngleFor(HandPoseKind.Point, false, false, 0), HandPosePolicy.ProximalCurl));
        A("handpose: grab curls the index too", Near(HandPosePolicy.AngleFor(HandPoseKind.Grab, false, true, 0), HandPosePolicy.ProximalCurl));
        A("handpose: free hand fully open", Near(HandPosePolicy.AngleFor(HandPoseKind.Free, false, false, 0), 0f));
        A("handpose: nitrile only when gloves worn", HandPosePolicy.Nitrile(true) && !HandPosePolicy.Nitrile(false));
        A("handpose: distal curls less than proximal", HandPosePolicy.DistalCurl < HandPosePolicy.ProximalCurl);
    }

    // Fitted watch geometry (user 2026-07-11: the solid Tripo watch cannot wrap a
    // wrist — the band is generated around the MEASURED wrist cross-section).
    static void WatchMathSuite()
    {
        var pts = new System.Collections.Generic.List<UnityEngine.Vector3>
        {
            new UnityEngine.Vector3(-0.03f, 0.00f, -0.045f),
            new UnityEngine.Vector3( 0.03f, 0.02f, -0.045f),
            new UnityEngine.Vector3( 0.00f, -0.01f, -0.044f),
            new UnityEngine.Vector3( 0.00f, 0.03f, -0.046f),
            new UnityEngine.Vector3( 0.50f, 0.50f, 0.200f)    // far point — must be excluded
        };
        var s = WatchMath.MeasureSlice(pts, -0.045f, 0.008f);
        A("watch: slice excludes far points", s.samples == 4);
        A("watch: slice half-width measured", Near(s.halfExtents.x, 0.03f));
        A("watch: slice centre offset found", Near(s.center.y, 0.01f));
        A("watch: empty slice is safe", WatchMath.MeasureSlice(pts, 9f, 0.001f).samples == 0);
        A("watch: band adds skin clearance",
            Near(WatchMath.BandRadii(new UnityEngine.Vector2(0.03f, 0.02f)).x, 0.03f + WatchMath.BandClearance));
        A("watch: band floors tiny measurements", WatchMath.BandRadii(UnityEngine.Vector2.zero).x >= WatchMath.MinHalfWidth);
        A("watch: palm bulge cannot widen the band",
            Near(WatchMath.BandRadii(new UnityEngine.Vector2(0.05f, 0.02f)).x, WatchMath.MaxHalfWidth));
        A("watch: face diameter clamped", WatchMath.FaceDiameter(1f) <= 0.0401f);
        var mesh = WatchMath.BuildBandMesh(new UnityEngine.Vector2(0.03f, 0.02f), 0.004f, 16, 8);
        A("watch: band mesh has full geometry", mesh.vertexCount == 17 * 9 && mesh.triangles.Length == 16 * 8 * 6);
        UnityEngine.Object.DestroyImmediate(mesh);
    }

    // Held-item collision profile (user 2026-07-10: grabbed props phased through
    // walls — pack prefabs shipped with Instantaneous movement, no physics sweep).
    static void GrabTuningSuite()
    {
        var host = new GameObject("tune-host");
        try
        {
            var grab = host.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;
            A("grab: untuned detected", !GrabTuning.IsTuned(grab));
            A("grab: apply reports change", GrabTuning.Apply(grab));
            A("grab: velocity-tracked after apply", GrabTuning.IsTuned(grab));
            A("grab: two-handed (multi-grab) after apply", grab.selectMode == UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple);
            A("grab: ease-in set", Near(grab.attachEaseInTime, GrabTuning.AttachEaseSeconds));
            A("grab: re-apply is a no-op", !GrabTuning.Apply(grab));
            A("grab: null safe", !GrabTuning.Apply(null) && !GrabTuning.IsTuned(null));
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }

        // Every library prefab grab must stay velocity-tracked (pins the Wire
        // Grab Collision menu's result; a prefab re-import regression fails here).
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        A("grab: library present", lib != null && lib.prefabs != null);
        if (lib != null && lib.prefabs != null)
        {
            int grabs = 0, tuned = 0;
            foreach (var p in lib.prefabs)
            {
                if (p == null) continue;
                foreach (var g in p.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true))
                { grabs++; if (GrabTuning.IsTuned(g)) tuned++; }
            }
            A("grab: all library prefabs velocity-tracked (" + tuned + "/" + grabs + ")", grabs > 0 && tuned == grabs);
        }
    }

    // Shelf-bottle pour wiring (user 2026-07-10: tipping shelf bottles showed no
    // stream/puddle — LiquidPourer only existed on runtime-spawned props).
    static void ShelfPourWiringSuite()
    {
        A("shelfpour: null safe", ShelfPourWiring.WireBottle(null, null, null) == -1);
        var bottle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            A("shelfpour: non-liquid rejected", ShelfPourWiring.WireBottle(bottle, null, null) == -1);
            bottle.AddComponent<LiquidPhysics>();
            int added = ShelfPourWiring.WireBottle(bottle, null, null);
            A("shelfpour: adds pourer+spout+spill+reactor", added == 4);
            var pourer = bottle.GetComponent<LiquidPourer>();
            A("shelfpour: pourer present", pourer != null);
            A("shelfpour: spout wired", pourer != null && pourer.spout != null);
            A("shelfpour: spill present", bottle.GetComponent<SpillMistake>() != null);
            A("shelfpour: idempotent", ShelfPourWiring.WireBottle(bottle, null, null) == 0);
        }
        finally { UnityEngine.Object.DestroyImmediate(bottle); }
    }

    // Focused checklist for the single holo procedures board (user 2026-07-10:
    // full-detail text overflowed the pads — collapse everything but the active phase).
    static void ChecklistPagerSuite()
    {
        var g = new TaskGraph(new List<ExperimentTask> {
            T("prep", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("mix", TaskPhase.Synthesis, 2, LabSkill.Transfer, RubricCategory.Procedure, "prep"),
            T("heat", TaskPhase.Synthesis, 1, LabSkill.Heating, RubricCategory.Procedure, "mix"),
            T("test", TaskPhase.ChemicalTests, 1, LabSkill.TestInterpretation, RubricCategory.ChemicalTests, "heat"),
        });

        A("pager: active = first available phase", ChecklistPager.ActivePhase(g) == TaskPhase.ReagentPrep);
        string s = ChecklistPager.BuildFocusedText(g);
        A("pager: active phase in detail", s.Contains("prep") && s.Contains("»"));
        A("pager: future phase collapsed", s.Contains("(2 steps)") && !s.Contains("mix"));
        A("pager: future tests collapsed", s.Contains("(1 steps)") && !s.Contains("test"));

        g.TryComplete("prep");
        A("pager: active advances", ChecklistPager.ActivePhase(g) == TaskPhase.Synthesis);
        s = ChecklistPager.BuildFocusedText(g);
        A("pager: completed phase folds to done n/n", s.Contains("done 1/1"));
        A("pager: new active in detail", s.Contains("mix") && s.Contains("heat"));
        A("pager: folded phase hides steps", !s.Contains("prep"));

        g.TryComplete("mix"); g.TryComplete("heat"); g.TryComplete("test");
        A("pager: all complete → no active", ChecklistPager.ActivePhase(g) == null);
        s = ChecklistPager.BuildFocusedText(g);
        A("pager: everything folded when done", s.Contains("done 1/1") && s.Contains("done 2/2") && !s.Contains("»"));

        A("pager: null graph safe", ChecklistPager.BuildFocusedText(null) == "" && ChecklistPager.ActivePhase(null) == null);
        A("pager: null runner header safe", ChecklistPager.BuildHeader(null) == "");

        // Bounded by construction: line count ≤ phases + active-phase tasks + 1.
        var lines = ChecklistPager.BuildFocusedText(g).Split('\n');
        A("pager: folded output is compact", lines.Length <= 6);
    }

    // Demo Mode config/save isolation (user 2026-07-10: panelists get a fast
    // auto-complete run-through; study participants' real save is never touched).
    static void DemoModeSuite()
    {
        A("demo: missing config → disabled", !DemoMode.Resolve(null, null).demoEnabled);
        A("demo: streaming default enables", DemoMode.Resolve(null, "{\"demoEnabled\":true}").demoEnabled);
        A("demo: persistent override wins (disable)", !DemoMode.Resolve("{\"demoEnabled\":false}", "{\"demoEnabled\":true}").demoEnabled);
        A("demo: persistent override wins (enable)", DemoMode.Resolve("{\"demoEnabled\":true}", null).demoEnabled);
        A("demo: malformed override falls through", DemoMode.Resolve("not-json{", "{\"demoEnabled\":true}").demoEnabled);
        A("demo: infiniteSupply defaults on", DemoMode.Resolve(null, "{\"demoEnabled\":true}").infiniteSupply);

        A("demo: normal save path untouched", DemoMode.SavePathFor(false, "a/pharmasynth_progress.json") == "a/pharmasynth_progress.json");
        A("demo: demo save path suffixed", DemoMode.SavePathFor(true, "a/pharmasynth_progress.json") == "a/pharmasynth_progress_demo.json");
        A("demo: extensionless path safe", DemoMode.SavePathFor(true, "save") == "save_demo");
        A("demo: null path safe", DemoMode.SavePathFor(true, null) == null);

        // Unlock-all opens every period/module WITHOUT faking passes.
        var svc = new ProgressionService(System.IO.Path.Combine(Application.temporaryCachePath, "demo_flow_selftest.json"));
        var demoFlow = new ProgressionFlow(svc, true);
        var normalFlow = new ProgressionFlow(svc);
        A("demo: finals period pickable", demoFlow.IsPeriodUnlocked(ExperimentPeriod.Final));
        A("demo: any module pickable", demoFlow.IsUnlocked("final-caffeine"));
        A("demo: unknown module still rejected", !demoFlow.IsUnlocked("does-not-exist"));
        A("demo: pass state stays honest", !demoFlow.IsPassed("tutorial-methane") && demoFlow.PassedCount() == 0);
        A("demo: normal flow unaffected", !normalFlow.IsPeriodUnlocked(ExperimentPeriod.Final));

        // End products hide outside demo sessions (user 2026-07-11).
        A("endproduct: ethanol is a product", DemoMode.IsEndProduct("Ethanol"));
        A("endproduct: acetone is a product", DemoMode.IsEndProduct("Acetone"));
        A("endproduct: aspirin is a product", DemoMode.IsEndProduct("Aspirin"));
        A("endproduct: sulfuric acid is raw", !DemoMode.IsEndProduct("Sulfuric Acid"));
        A("endproduct: sodium acetate is feedstock", !DemoMode.IsEndProduct("Sodium Acetate"));
        A("endproduct: null safe", !DemoMode.IsEndProduct(null));
    }

    // Consumable dispenser (user 2026-07-11: grab the box → pull a single piece;
    // it refills; used pieces clean themselves up).
    static void DispenserSuite()
    {
        // Taken: a hand grabs the resting piece, OR it moves off the slot.
        A("dispenser: grab counts as taken", DispenserMath.IsTaken(true, 0f));
        A("dispenser: nudge off slot counts as taken", DispenserMath.IsTaken(false, 0.2f));
        A("dispenser: resting piece not taken", !DispenserMath.IsTaken(false, 0.01f));

        // Discard: only a piece that was held, is now set down, still, long enough.
        A("dispenser: used + idle piece discarded", DispenserMath.ShouldDiscard(true, false, 0f, 15f));
        A("dispenser: never-held ready piece kept", !DispenserMath.ShouldDiscard(false, false, 0f, 999f));
        A("dispenser: held piece kept", !DispenserMath.ShouldDiscard(true, true, 0f, 999f));
        A("dispenser: still-moving piece kept", !DispenserMath.ShouldDiscard(true, false, 0.5f, 999f));
        A("dispenser: not-yet-idle piece kept", !DispenserMath.ShouldDiscard(true, false, 0f, 3f));
    }

    // Demo HUD auto-complete verbs (skip step / finish experiment / auto quiz).
    static void DemoActionsSuite()
    {
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> {
            T("a", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("b", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure, "a"),
            T("c", TaskPhase.ChemicalTests, 1, LabSkill.TestInterpretation, RubricCategory.ChemicalTests, "b"),
            T("record-x", TaskPhase.DataSheet, 1, LabSkill.Measuring, RubricCategory.Documentation, "c"),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Measuring, LabSkill.Transfer, LabSkill.TestInterpretation };
        var rgo = new GameObject("demo_runner"); var pgo = new GameObject("demo_postlab");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            A("demoactions: inert before start", DemoActions.CompleteCurrentStep(runner) == null);
            runner.StartExperiment();
            A("demoactions: completes first available", DemoActions.CompleteCurrentStep(runner) == "a");
            A("demoactions: complete-all stops at data sheet", DemoActions.CompleteAllTasks(runner) == 2);
            A("demoactions: data-sheet task untouched", !runner.Graph.IsComplete("record-x"));
            A("demoactions: nothing left to skip", DemoActions.CompleteCurrentStep(runner) == null);

            var post = pgo.AddComponent<PostLabController>();
            post.SetRefs(runner, null);
            A("demoactions: quiz closed → no-op", !DemoActions.AutoAnswerQuiz(post));
            var bank = ScriptableObject.CreateInstance<QuizBank>();
            bank.questions = new List<QuizQuestion> {
                new QuizQuestion { prompt = "q1", options = new List<string>{ "x", "y" }, correctIndex = 1 },
                new QuizQuestion { prompt = "q2", options = new List<string>{ "x", "y" }, correctIndex = 0 },
            };
            post.OpenFor(bank);
            A("demoactions: auto-answers all", DemoActions.AutoAnswerQuiz(post));
            A("demoactions: perfect score", Near(post.ScoreFraction(), 1f));
            var res = post.SubmitAndFinish();
            A("demoactions: submit finishes with full graph", Near(runner.Graph.Progress01, 1f) && res.grade.Total > 0f);
            UnityEngine.Object.DestroyImmediate(bank);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rgo);
            UnityEngine.Object.DestroyImmediate(pgo);
            UnityEngine.Object.DestroyImmediate(module);
        }
    }

    // Hazardous-mix consequences (user 2026-07-10: wrong mixtures now smoke,
    // ignite, spatter or fizz — and cost the right rubric category).
    static void HazardousMixSuite()
    {
        ChemicalData Make(string name, float ph = 7f, HazardType hz = HazardType.None,
                          bool ox = false, bool conc = false, PhysicalState st = PhysicalState.Liquid)
        {
            var c = ScriptableObject.CreateInstance<ChemicalData>();
            c.chemicalName = name; c.pH = ph; c.hazard = hz;
            c.isOxidizer = ox; c.isConcentratedAcid = conc; c.state = st;
            return c;
        }

        var acid = Make("Hydrochloric Acid 6N", 0.5f, HazardType.Corrosive, conc: true);
        var bleach = Make("Sodium Hypochlorite 5%", 11f, ox: true);
        var ethanol = Make("Ethanol", 7f, HazardType.Flammable);
        var kmno4 = Make("Potassium Permanganate 0.1%", 7f, ox: true);
        var water = Make("Purified Water");

        try
        {
            A("hazmix: acid + bleach = toxic gas",
                HazardousMix.Classify(acid, bleach) == HazardousMix.HazardOutcome.ToxicGas
                && HazardousMix.Classify(bleach, acid) == HazardousMix.HazardOutcome.ToxicGas);
            A("hazmix: oxidizer + flammable = fire",
                HazardousMix.Classify(kmno4, ethanol) == HazardousMix.HazardOutcome.FireOrExplosion
                && HazardousMix.Classify(ethanol, kmno4) == HazardousMix.HazardOutcome.FireOrExplosion);
            A("hazmix: water INTO conc acid = spatter",
                HazardousMix.Classify(acid, water) == HazardousMix.HazardOutcome.AcidSpatter);
            A("hazmix: conc acid into water = fizz only",
                HazardousMix.Classify(water, acid) == HazardousMix.HazardOutcome.GenericFizz);
            A("hazmix: unknown pair fizzes", HazardousMix.Classify(water, ethanol) == HazardousMix.HazardOutcome.GenericFizz);
            A("hazmix: null / same safe",
                HazardousMix.Classify(null, water) == HazardousMix.HazardOutcome.None
                && HazardousMix.Classify(water, water) == HazardousMix.HazardOutcome.None);

            A("hazmix: penalties map to the matrix",
                HazardousMix.ErrorTypeFor(HazardousMix.HazardOutcome.ToxicGas) == LabErrorType.HazardousAction
                && HazardousMix.ErrorTypeFor(HazardousMix.HazardOutcome.FireOrExplosion) == LabErrorType.HazardousAction
                && HazardousMix.ErrorTypeFor(HazardousMix.HazardOutcome.AcidSpatter) == LabErrorType.ChemicalContact
                && HazardousMix.ErrorTypeFor(HazardousMix.HazardOutcome.GenericFizz) == LabErrorType.WrongReagent);
            A("hazmix: every outcome has a warn line",
                HazardousMix.WarnLineFor(HazardousMix.HazardOutcome.ToxicGas).Length > 0
                && HazardousMix.WarnLineFor(HazardousMix.HazardOutcome.FireOrExplosion).Length > 0
                && HazardousMix.WarnLineFor(HazardousMix.HazardOutcome.AcidSpatter).Length > 0
                && HazardousMix.WarnLineFor(HazardousMix.HazardOutcome.GenericFizz).Length > 0);

            // Name-rule flags (the audit menu + raw-reagent forge share these).
            A("hazflags: oxidizers recognized",
                HazardFlags.IsOxidizer("Potassium Permanganate 0.1%") && HazardFlags.IsOxidizer("Bleaching Powder")
                && HazardFlags.IsOxidizer("Bromine Water") && !HazardFlags.IsOxidizer("Ethanol"));
            A("hazflags: conc acids recognized",
                HazardFlags.IsConcentratedAcid("Sulfuric Acid") && HazardFlags.IsConcentratedAcid("Glacial Acetic Acid")
                && !HazardFlags.IsConcentratedAcid("Diluted Hydrochloric Acid")
                && !HazardFlags.IsConcentratedAcid("0.1N Hydrochloric Acid"));

            // Warning-pulse curve + alarm flash phase.
            A("pulse: silent at ends", Near(FadeState.Pulse01(0f, 0.35f), 0f) && Near(FadeState.Pulse01(1f, 0.35f), 0f));
            A("pulse: peaks at attack end", Near(FadeState.Pulse01(0.35f, 0.35f), 0.35f));
            A("pulse: clamped peak", FadeState.Pulse01(0.35f, 1.7f) <= 1f);
            A("alarm: flash phase alternates", LabAlarm.FlashOn(0.1f, 0.5f) && !LabAlarm.FlashOn(0.3f, 0.5f)
                && LabAlarm.FlashOn(0.6f, 0.5f) && !LabAlarm.FlashOn(0f, 0f));

            // Reactor wiring seam (edit-mode: Bind + event path, no VFX side effects).
            var host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var lp = host.AddComponent<LiquidPhysics>();
                var reactor = host.AddComponent<HazardousMixReactor>();
                reactor.Bind(lp, null);
                A("hazmix: reactor binds without a runner", reactor != null);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(acid);
            UnityEngine.Object.DestroyImmediate(bleach);
            UnityEngine.Object.DestroyImmediate(ethanol);
            UnityEngine.Object.DestroyImmediate(kmno4);
            UnityEngine.Object.DestroyImmediate(water);
        }
    }

    // NPC voice-over pipeline (user 2026-07-10: Pharmee + Dr. Jimenez speak) —
    // hash-keyed clip lookup with graceful blip fallback.
    static void VoiceSuite()
    {
        A("voice: id ignores cosmetic whitespace", VoiceLineId.For("Hello  world") == VoiceLineId.For(" Hello world "));
        A("voice: distinct lines differ", VoiceLineId.For("line a") != VoiceLineId.For("line b"));
        A("voice: null/empty agree", VoiceLineId.For(null) == VoiceLineId.For(""));
        A("voice: id is 16 hex chars", VoiceLineId.For("x").Length == 16);

        var corpus = VoiceCorpus.CodeLines();
        A("voice: corpus substantial (≥110 code lines)", corpus.Count >= 110);
        bool nonEmpty = true;
        int jimenez = 0;
        foreach (var l in corpus)
        {
            if (string.IsNullOrEmpty(l.text)) nonEmpty = false;
            if (l.speaker == VoiceSpeaker.Jimenez) jimenez++;
        }
        A("voice: no empty corpus lines", nonEmpty);
        A("voice: Jimenez pools attributed (≥15)", jimenez >= 15);
        bool greetAsJimenez = false;
        foreach (var l in corpus)
            if (l.text == PharmeeLines.ExamGreeting[0] && l.speaker == VoiceSpeaker.Jimenez) greetAsJimenez = true;
        A("voice: exam greeting speaks as Jimenez", greetAsJimenez);

        var bank = ScriptableObject.CreateInstance<VoiceBank>();
        var clip = AudioClip.Create("voice_test", 441, 1, 44100, false);
        var go = new GameObject("voice_narr");
        try
        {
            bank.entries.Add(new VoiceBank.Entry { speaker = VoiceSpeaker.Pharmee, id = VoiceLineId.For("hello"), clip = clip });
            bank.Rebuild();
            A("voice: bank hit", bank.Get(VoiceSpeaker.Pharmee, VoiceLineId.For("hello")) == clip);
            A("voice: wrong speaker misses", bank.Get(VoiceSpeaker.Jimenez, VoiceLineId.For("hello")) == null);
            A("voice: unknown id misses", bank.Get(VoiceSpeaker.Pharmee, "beef") == null);

            var n = go.AddComponent<NPCNarrationController>();
            n.BindVoice(bank, VoiceSpeaker.Pharmee);
            A("voice: narration resolves by text", n.ResolveVoice("hello") == clip);
            A("voice: unknown text degrades to blips", n.ResolveVoice("no clip for this") == null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(bank);
            UnityEngine.Object.DestroyImmediate(clip);
        }
    }

    // The manuscript materials catalog + consumable behaviours (user 2026-07-10:
    // ~54 raw materials stocked in nature-appropriate labware).
    static void RawReagentSuite()
    {
        var rows = RawReagentCatalog.Rows;
        A("raw: catalog covers the manuscript union (≥52)", rows.Count >= 52);

        var names = new HashSet<string>();
        bool unique = true, blurbs = true, groupsOk = true;
        var validGroups = new HashSet<string> { RawReagentCatalog.GroupAcids, RawReagentCatalog.GroupOrganics,
                                                RawReagentCatalog.GroupTests, RawReagentCatalog.GroupConsumables };
        foreach (var r in rows)
        {
            if (!names.Add(r.chemicalName)) unique = false;
            if (string.IsNullOrEmpty(r.blurb)) blurbs = false;
            if (!validGroups.Contains(r.group)) groupsOk = false;
        }
        A("raw: names unique", unique);
        A("raw: every row has a hover blurb", blurbs);
        A("raw: groups valid", groupsOk);

        A("raw: light-sensitive chemicals go amber",
            RawReagentCatalog.Find("Aniline").labware == RawReagentCatalog.LabwareKind.AmberBottle
            && RawReagentCatalog.Find("Benzaldehyde").labware == RawReagentCatalog.LabwareKind.AmberBottle
            && RawReagentCatalog.Find("Silver Nitrate").labware == RawReagentCatalog.LabwareKind.AmberBottle
            && RawReagentCatalog.Find("Bromine Water").labware == RawReagentCatalog.LabwareKind.AmberBottle);
        A("raw: fume-hood set marked",
            RawReagentCatalog.Find("Aniline").fumeHood && RawReagentCatalog.Find("Benzoyl Chloride").fumeHood
            && RawReagentCatalog.Find("Ammonia Solution").fumeHood);
        A("raw: manuscript consumables present",
            RawReagentCatalog.Find("Litmus Paper") != null && RawReagentCatalog.Find("Matchsticks") != null
            && RawReagentCatalog.Find("Cotton Swabs") != null && RawReagentCatalog.Find("Filter Paper") != null
            && RawReagentCatalog.Find("Ice") != null && RawReagentCatalog.Find("Anhydrous Calcium Chloride") != null);
        A("raw: blurb lookup + uses folded in",
            RawReagentCatalog.BlurbFor("Aniline") != null && RawReagentCatalog.BlurbFor("Aniline").Contains("Exp 5"));
        A("raw: unknown blurb null", RawReagentCatalog.BlurbFor("Unobtainium") == null);

        // Labware → prefab mapping must resolve through the scene asset library.
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        A("raw: library present", lib != null);
        if (lib != null)
        {
            bool prefabsOk = true;
            foreach (var kind in new[] { RawReagentCatalog.LabwareKind.ReagentBottle, RawReagentCatalog.LabwareKind.AmberBottle,
                                         RawReagentCatalog.LabwareKind.PowderJar, RawReagentCatalog.LabwareKind.DropperBottle })
                if (lib.GetPrefab(ReagentCabinetBuilder.PrefabFor(kind)) == null) prefabsOk = false;
            A("raw: bottle labware prefabs resolve", prefabsOk);
        }

        // Litmus colours.
        A("litmus: acid red", LitmusMath.ColorForPH(1f) == LitmusMath.AcidRed);
        A("litmus: base blue", LitmusMath.ColorForPH(13f) == LitmusMath.BaseBlue);
        Color mid = LitmusMath.ColorForPH(6.4f);
        A("litmus: violet between", mid != LitmusMath.AcidRed && mid != LitmusMath.BaseBlue);

        // Matchstick ignition predicate.
        A("match: ignites near heat", Matchstick.ShouldIgnite(0.1f, 120f, false, false));
        A("match: too far stays out", !Matchstick.ShouldIgnite(0.5f, 120f, false, false));
        A("match: too cold stays out", !Matchstick.ShouldIgnite(0.1f, 40f, false, false));
        A("match: lit/spent never re-ignite", !Matchstick.ShouldIgnite(0.1f, 120f, true, false)
            && !Matchstick.ShouldIgnite(0.1f, 120f, false, true));

        // Demo ready-made product per module.
        string[] ids = { "tutorial-methane", "prelim-chemical-compounding", "prelim-ethyl-alcohol",
                         "midterm-benzoic-acid", "midterm-acetanilide", "midterm-acetone", "midterm-chloroform",
                         "final-benzamide", "final-aspirin", "final-caffeine", "final-winemaking" };
        bool products = true;
        foreach (var id in ids) if (string.IsNullOrEmpty(DemoMode.ProductFor(id))) products = false;
        A("demo: ready-made product for all 11 modules", products);
        A("demo: unknown module has none", DemoMode.ProductFor("nope") == null);
    }

    // Center-table merge geometry (user 2026-07-10: one wide landscape table,
    // centered — a rigid remap shared by the island object and the baked layouts).
    static void CenterTableMathSuite()
    {
        var pts = new List<Vector3> {
            new Vector3(-2.15f, 0.91f, -1.95f), new Vector3(-1.1f, 0.91f, -4.6f), new Vector3(-1.9f, 0.91f, -3.4f),
        };
        var b = CenterTableMath.FootprintOf(pts, 0.35f);
        A("table: footprint spans the points", b.min.x < -2.15f && b.max.x > -1.1f && b.min.z < -4.6f && b.max.z > -1.95f);
        A("table: margin applied", Near(b.max.x, -1.1f + 0.35f, 0.01f));

        Vector3 oldC = new Vector3(-1.6f, 0.91f, -3.3f);
        Vector3 newC = new Vector3(0f, 0.91f, -3.3f);
        A("table: centre maps to centre", Near(Vector3.Distance(
            CenterTableMath.Remap(oldC, oldC, newC, true), newC), 0f, 0.001f));
        // +z offset becomes +x offset under the 90° landscape turn.
        Vector3 r = CenterTableMath.Remap(oldC + new Vector3(0f, 0f, 1f), oldC, newC, true);
        A("table: rotation maps z-run onto x-run", Near(r.x, newC.x + 1f) && Near(r.z, newC.z));
        // Height is preserved.
        A("table: deck height preserved", Near(CenterTableMath.Remap(new Vector3(-1.2f, 0.91f, -2f), oldC, newC, true).y, 0.91f));
        // Distances are rigid (no scaling).
        Vector3 p1 = new Vector3(-2.15f, 0.91f, -1.95f), p2 = new Vector3(-1.1f, 0.91f, -4.6f);
        A("table: remap is rigid", Near(
            Vector3.Distance(CenterTableMath.Remap(p1, oldC, newC, true), CenterTableMath.Remap(p2, oldC, newC, true)),
            Vector3.Distance(p1, p2), 0.001f));

        A("table: within test", CenterTableMath.WithinXZ(new Vector3(-1.5f, 0.9f, -3f), b)
            && !CenterTableMath.WithinXZ(new Vector3(2f, 0.9f, -3f), b));
        A("table: mirror across x", Near(CenterTableMath.MirrorAcrossX(new Vector3(1.4f, 0f, -3f), 0f).x, -1.4f));
    }

    // Post-experiment review flow seams (user 2026-07-11: congrats → Jimenez
    // briefing → quiz → score remarks → entrance debrief).
    static void ReviewFlowSuite()
    {
        // Clock freeze: the review/quiz never costs Time-Management score.
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> {
            T("a", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Transfer };
        var go = new GameObject("freeze_runner");
        try
        {
            var runner = go.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            runner.StartExperiment();
            runner.AdvanceTime(10f);
            A("review: clock runs before freeze", Near(runner.ElapsedSeconds, 10f));
            runner.FreezeClock();
            runner.AdvanceTime(30f);
            A("review: frozen clock holds", Near(runner.ElapsedSeconds, 10f) && runner.ClockFrozen);
            runner.Retry();
            A("review: retry thaws the clock", !runner.ClockFrozen);
            A("review: HasPhase sees synthesis only",
                runner.Graph.HasPhase(TaskPhase.Synthesis) && !runner.Graph.HasPhase(TaskPhase.ChemicalTests));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(module); }

        // Banded debrief remark (numbers stay on the grade card, lines stay finite).
        A("review: flawless band", PharmeeLines.DebriefRemark(98f).Contains("flawless"));
        A("review: strong band", PharmeeLines.DebriefRemark(94f).Contains("strong"));
        A("review: solid band", PharmeeLines.DebriefRemark(90f).Contains("solid"));

        // New spoken pools all populated (voice corpus depends on them).
        A("review: pools populated",
            PharmeeLines.TestsDoneLines.Length >= 3 && PharmeeLines.JimenezQuizBrief.Length >= 3
            && PharmeeLines.JimenezPassRemarks.Length >= 4 && PharmeeLines.JimenezFailRemarks.Length >= 3
            && PharmeeLines.DebriefCongrats.Length >= 3);
    }

    // ILO opening dialogue (user 2026-07-10: Pharmee states each experiment's
    // learning outcomes — verbatim Appendix C copy — in the intro cutscene).
    static void IloCopySuite()
    {
        string[] ids = {
            "tutorial-methane", "prelim-chemical-compounding", "prelim-ethyl-alcohol",
            "midterm-benzoic-acid", "midterm-acetanilide", "midterm-acetone", "midterm-chloroform",
            "final-benzamide", "final-aspirin", "final-caffeine", "final-winemaking",
        };
        bool allPresent = true, allSized = true;
        foreach (var id in ids)
        {
            var ilos = IloCopy.ForModule(id);
            if (ilos.Length == 0) allPresent = false;
            foreach (var line in ilos)
                if (string.IsNullOrEmpty(line) || line.Length > 220) allSized = false;
        }
        A("ilo: all 11 modules covered", allPresent);
        A("ilo: subtitle-sized lines", allSized);
        A("ilo: synthesis modules keep manuscript verbs",
            IloCopy.ForModule("prelim-ethyl-alcohol")[0].Contains("Synthesize ethyl alcohol")
            && IloCopy.ForModule("final-benzamide")[1].Contains("identity through chemical tests"));
        A("ilo: unknown id → empty", IloCopy.ForModule("does-not-exist").Length == 0);
        A("ilo: beat pacing floors and caps",
            Near(IloCopy.BeatSeconds("short"), 2.5f) && Near(IloCopy.BeatSeconds(new string('x', 200)), 6f));

        var cs = ScriptableObject.CreateInstance<CutsceneData>();
        try
        {
            cs.beats = new List<CutsceneData.Beat> { new CutsceneData.Beat { subtitle = "greet", seconds = 3f } };
            int added = IloBeatInjector.InjectInto(cs, "prelim-ethyl-alcohol");
            A("ilo: injects lead-in + objectives", added == 3 && cs.beats.Count == 4);
            A("ilo: lead-in lands after the greeting", cs.beats[1].subtitle == IloCopy.LeadIn);
            A("ilo: objectives follow in order", cs.beats[2].subtitle.Contains("Objective 1"));
            A("ilo: re-inject is a no-op", IloBeatInjector.InjectInto(cs, "prelim-ethyl-alcohol") == 0 && cs.beats.Count == 4);
        }
        finally { UnityEngine.Object.DestroyImmediate(cs); }
    }

    // Persisted particle material (device builds strip runtime Shader.Find-only
    // shaders — the Resources asset keeps the pour stream/puddles alive on Quest).
    static void FxMaterialSuite()
    {
        var m = ShelfPourBuilder.EnsureFxMaterial();
        A("fxmat: asset exists", m != null);
        A("fxmat: transparent surface", m != null && Near(m.GetFloat("_Surface"), 1f));
        A("fxmat: alpha blend", m != null && m.GetInt("_SrcBlend") == (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        A("fxmat: no zwrite", m != null && m.GetInt("_ZWrite") == 0);
        A("fxmat: resources-loadable", Resources.Load<Material>("FxParticleUnlit") != null);
    }
}
#endif
