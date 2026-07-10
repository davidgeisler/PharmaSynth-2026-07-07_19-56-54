#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Wires the 2026-07-10 NPC/audio polish batch into SampleScene:
///   1. Pharmee expressions — PharmeeFace re-pointed at the robot's EYES + MOUTH
///      meshes (was Ears_Black_Matt_0), default-happy; PharmeeMood resets the face
///      after every line; the gatekeeper's faceBehaviour drives gate moods.
///   2. Dr. Jimenez proctor roaming — ProctorRoamer + observation points at the
///      reagent shelf, equipment shelf, dynamic stage and fume hood.
///   3. AC proximity hum — ProximityHum on the air-con / vent assets (falls back to
///      the fume hood if no AC mesh exists).
///
/// Tools ▸ PharmaSynth ▸ Wire NPC Polish (SampleScene, edit mode, idempotent).
public static class LabNpcPolishBuilder
{
    [MenuItem("Tools/PharmaSynth/Wire NPC Polish")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[NpcPolish] exit Play mode first."); return; }

        // ---- 1. Pharmee face ------------------------------------------------
        var robot = GameObject.Find("RobotNPC");
        if (robot == null) { Debug.LogError("[NpcPolish] no RobotNPC"); return; }
        var face = robot.GetComponentInChildren<PharmeeFace>(true);
        if (face == null) face = robot.AddComponent<PharmeeFace>();

        var faceParts = new List<Renderer>();
        foreach (var r in robot.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name.ToLower();
            if (n.StartsWith("eyes") || n.StartsWith("mouth")) faceParts.Add(r);
        }
        if (faceParts.Count > 0) face.BindRenderers(faceParts.ToArray());
        Debug.Log("[NpcPolish] face renderers: " + faceParts.Count + " (" + string.Join(", ", faceParts.ConvertAll(r => r.name)) + ")");

        var narration = robot.GetComponentInChildren<NPCNarrationController>(true);
        var mood = robot.GetComponent<PharmeeMood>();
        if (mood == null) mood = robot.AddComponent<PharmeeMood>();
        mood.Bind(narration, face);

        var gk = robot.GetComponentInChildren<PharmeeGatekeeper>(true);
        if (gk != null)
        {
            var soGk = new SerializedObject(gk);
            soGk.FindProperty("faceBehaviour").objectReferenceValue = face;
            soGk.ApplyModifiedProperties();
        }
        var brain = robot.GetComponentInChildren<PharmeeBrain>(true);
        if (brain != null)
        {
            var soBr = new SerializedObject(brain);
            var p = soBr.FindProperty("faceBehaviour");
            if (p != null && p.objectReferenceValue == null)
            { p.objectReferenceValue = face; soBr.ApplyModifiedProperties(); }
        }

        // ---- 2. Jimenez roaming ---------------------------------------------
        var jim = GameObject.Find("DrJimenez");
        if (jim == null) jim = GameObject.Find("RiggedDrjimenez");
        if (jim == null)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name.ToLower().Contains("jimenez") && t.parent == null) { jim = t.gameObject; break; }
        }
        if (jim != null)
        {
            var runnerGo = GameObject.Find("ExperimentSystems");
            var runner = runnerGo != null ? runnerGo.GetComponent<ExperimentRunner>() : null;
            var animator = jim.GetComponentInChildren<Animator>(true);

            // Observation points: proud of each landmark, standing height on the floor.
            var group = GameObject.Find("ProctorPoints");
            if (group != null) Object.DestroyImmediate(group);
            group = new GameObject("ProctorPoints");
            Undo.RegisterCreatedObjectUndo(group, "Wire NPC Polish");
            var points = new List<Transform>();
            void Point(string landmark, Vector3 fallback, Vector3 offset)
            {
                var lm = GameObject.Find(landmark);
                Vector3 p = (lm != null ? lm.transform.position : fallback) + offset;
                p.y = jim.transform.position.y;                        // keep his standing height
                var pt = new GameObject("Watch_" + landmark).transform;
                pt.SetParent(group.transform, false);
                pt.position = p;
                points.Add(pt);
            }
            Point("ReagentShelf", new Vector3(-4.5f, 0f, -3f), new Vector3(0.9f, 0f, 0f));
            Point("DynamicStage", new Vector3(-2f, 0f, -5f), new Vector3(0.8f, 0f, 0.8f));
            Point("EquipmentShelf", new Vector3(-4.8f, 0f, -6.5f), new Vector3(0.9f, 0f, 0.3f));
            Point("FumeHood_StandIn", new Vector3(2f, 0f, -6.5f), new Vector3(0f, 0f, 1.0f));

            var roamer = jim.GetComponent<ProctorRoamer>();
            if (roamer == null) roamer = jim.AddComponent<ProctorRoamer>();
            roamer.Bind(animator, runner, points);

            // Solid body: a CharacterController stops him phasing through walls while
            // roaming (ProctorRoamer moves via cc.Move) AND blocks the player.
            var cc = jim.GetComponent<CharacterController>();
            if (cc == null) cc = jim.AddComponent<CharacterController>();
            cc.radius = 0.28f;
            cc.height = 1.7f;
            cc.center = new Vector3(0f, 0.88f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.15f;

            // Walk animation: ensure the controller has a "Walking" bool + a Walk
            // state wired Idle⇄Walk (the clip exists; wiring may not).
            EnsureWalkState(animator);
            EditorUtility.SetDirty(jim);
            Debug.Log("[NpcPolish] Jimenez roamer wired (animator=" + (animator != null) + ", runner=" + (runner != null) + ", points=" + points.Count + ", cc added)");
        }
        else Debug.LogWarning("[NpcPolish] Dr. Jimenez not found — roamer skipped");

        // ---- 3. AC hum --------------------------------------------------------
        int hums = 0;
        var humHosts = new List<GameObject>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            string n = t.name.ToLower();
            if ((n.Contains("aircon") || n.Contains("air_cond") || n.Contains("airconditioner")
                 || n.Contains("ac_unit") || n.Contains("hvac") || n.Contains("vent"))
                && t.GetComponentInChildren<Renderer>() != null)
                humHosts.Add(t.gameObject);
        }
        if (humHosts.Count == 0)
        {
            var hood = GameObject.Find("FumeHood_StandIn");
            if (hood != null) humHosts.Add(hood);   // the hood's fan IS the room's machine noise
        }
        foreach (var host in humHosts)
        {
            if (host.GetComponent<ProximityHum>() == null)
            {
                var hum = host.AddComponent<ProximityHum>();
                hum.Bind("ambient-lab", 0.5f);
                EditorUtility.SetDirty(host);
            }
            hums++;
        }
        Debug.Log("[NpcPolish] proximity hums on " + hums + " host(s): "
            + string.Join(", ", humHosts.ConvertAll(h => h.name)));

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(robot.scene);
        Debug.Log("<color=#4CD07D>[NpcPolish] done</color>");
    }

    /// Make sure the animator has a bool "Walking" and an Idle⇄Walk transition pair.
    static void EnsureWalkState(Animator animator)
    {
        if (animator == null) return;
        var ctrl = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (ctrl == null)
        {
            // scene instances often reference an override/runtime — resolve the base asset
            var rt = animator.runtimeAnimatorController;
            if (rt is AnimatorOverrideController ov)
                ctrl = ov.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        }
        if (ctrl == null) { Debug.LogWarning("[NpcPolish] Jimenez animator controller not editable — walk wiring skipped"); return; }

        bool hasParam = false;
        foreach (var p in ctrl.parameters)
            if (p.name == "Walking" && p.type == AnimatorControllerParameterType.Bool) hasParam = true;
        if (!hasParam) ctrl.AddParameter("Walking", AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;
        UnityEditor.Animations.AnimatorState idle = null, walk = null;
        foreach (var s in sm.states)
        {
            string n = s.state.name.ToLower();
            if (n.Contains("idle")) idle = s.state;
            if (n.Contains("walk")) walk = s.state;
        }
        if (walk == null)
        {
            // find a walk clip in the project (uthana text-to-motion set)
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip walk"))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) { walk = sm.AddState("Walk"); walk.motion = clip; break; }
            }
        }
        if (idle == null || walk == null)
        { Debug.LogWarning("[NpcPolish] idle=" + (idle != null) + " walk=" + (walk != null) + " — transitions skipped"); return; }

        bool hasOut = false, hasBack = false;
        foreach (var tr in idle.transitions)
            if (tr.destinationState == walk) hasOut = true;
        foreach (var tr in walk.transitions)
            if (tr.destinationState == idle) hasBack = true;
        if (!hasOut)
        {
            var tr = idle.AddTransition(walk);
            tr.hasExitTime = false; tr.duration = 0.15f;
            tr.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Walking");
        }
        if (!hasBack)
        {
            var tr = walk.AddTransition(idle);
            tr.hasExitTime = false; tr.duration = 0.15f;
            tr.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "Walking");
        }
        EditorUtility.SetDirty(ctrl);
        Debug.Log("[NpcPolish] walk state OK (param=Walking, idle='" + idle.name + "', walk='" + walk.name + "')");
    }
}
#endif
