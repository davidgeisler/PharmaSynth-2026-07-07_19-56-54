#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Exports the full voice-over manifest (user 2026-07-10: NPCs speak): every
/// code-authored line (VoiceCorpus) plus every cutscene beat, one row per
/// unique (speaker, text) with its stable id. Tools/voice/generate-voice.ps1
/// consumes the manifest; changed lines re-key and regenerate individually.
public static class VoiceManifestExporter
{
    const string OutPath = "Assets/PharmaSynth/Audio/Voice/voice-manifest.json";

    [System.Serializable]
    public class ManifestLine { public string id; public string speaker; public string text; public int chars; }

    [System.Serializable]
    public class Manifest { public List<ManifestLine> lines = new List<ManifestLine>(); }

    [MenuItem("Tools/PharmaSynth/Voice/Export Voice Manifest")]
    public static void Export()
    {
        var manifest = new Manifest();
        var seen = new HashSet<string>();

        void Add(VoiceSpeaker speaker, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            string id = VoiceLineId.For(text);
            string key = speaker + ":" + id;
            if (!seen.Add(key)) return;
            manifest.lines.Add(new ManifestLine
            {
                id = id,
                speaker = speaker.ToString(),
                text = VoiceLineId.Normalize(text),
                chars = VoiceLineId.Normalize(text).Length,
            });
        }

        foreach (var l in VoiceCorpus.CodeLines()) Add(l.speaker, l.text);

        // Cutscene beats (Pharmee narrates all four cutscenes per module).
        int beats = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CutsceneData", new[] { "Assets/PharmaSynth/ScriptableObjects/Cutscenes" }))
        {
            var cs = AssetDatabase.LoadAssetAtPath<CutsceneData>(AssetDatabase.GUIDToAssetPath(guid));
            if (cs == null || cs.beats == null) continue;
            foreach (var b in cs.beats)
                if (b != null) { Add(VoiceSpeaker.Pharmee, b.subtitle); beats++; }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
        File.WriteAllText(OutPath, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(OutPath);

        int chars = 0, pharmee = 0, jimenez = 0;
        foreach (var l in manifest.lines)
        {
            chars += l.chars;
            if (l.speaker == "Pharmee") pharmee++; else jimenez++;
        }
        // eleven_flash_v2_5 ≈ 0.5 credits per character.
        Debug.Log($"[VoiceManifest] {manifest.lines.Count} unique lines ({pharmee} Pharmee, {jimenez} Jimenez, "
                  + $"{beats} cutscene beats folded in), {chars:n0} characters ≈ {chars / 2:n0} ElevenLabs credits "
                  + $"on eleven_flash_v2_5 (~${chars / 2 / 1000f * 0.22f:F0}-ish of a Creator plan). Wrote {OutPath}.");
    }
}
#endif
