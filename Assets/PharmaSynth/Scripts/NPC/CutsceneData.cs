using System;
using System.Collections.Generic;
using UnityEngine;

/// A VR-safe, data-driven cutscene: a sequence of subtitle "beats" with a Pharmee
/// expression and duration. No XR-camera animation (comfort) — narrative is carried
/// by subtitles + Pharmee staging + optional fades. Client-reviewable copy.
[CreateAssetMenu(fileName = "Cutscene", menuName = "PharmaSynth/Cutscene")]
public class CutsceneData : ScriptableObject
{
    public enum Kind { Intro, ReagentPrep, Success, Failure }

    [Serializable]
    public class Beat
    {
        [TextArea(1, 3)] public string subtitle;
        [Min(0.2f)] public float seconds = 3f;
        public PharmeeFaceExpression face = PharmeeFaceExpression.Neutral;
    }

    public Kind kind = Kind.Intro;
    [TextArea(1, 2)] public string title = "";
    public List<Beat> beats = new List<Beat>();

    public float TotalDuration()
    {
        float t = 0f;
        for (int i = 0; i < beats.Count; i++) if (beats[i] != null) t += Mathf.Max(0.2f, beats[i].seconds);
        return t;
    }
}
