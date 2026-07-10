using System;
using System.IO;
using UnityEngine;

/// Backend demo-mode switch (user 2026-07-10): panelists need a fast run-through
/// with auto-complete controls, while Chapter-3 study participants must see the
/// untouched normal flow. A config FILE decides whether the cube-room menu even
/// shows the "Demo Mode" button; pressing that button starts a demo SESSION
/// (separate throwaway save, all periods unlocked, HUD auto-complete controls,
/// infinite reagents). Config off = zero footprint.
[Serializable]
public class DemoConfig
{
    public bool demoEnabled;              // shows the menu button
    public bool infiniteSupply = true;    // demo sessions refill starved bottles
}

/// Pure config resolution + demo save-path mapping (edit-mode testable).
public static class DemoMode
{
    private static DemoConfig _config;      // null until resolved (lazily or by the loader)

    /// The config file's verdict — gates only the BUTTON's visibility.
    public static bool IsEnabled { get { EnsureResolved(); return _config != null && _config.demoEnabled; } }

    /// Demo sessions top starved bottles back up instead of forcing a restart.
    public static bool InfiniteSupply { get { EnsureResolved(); return _config != null && _config.infiniteSupply; } }

    /// Installed by DemoModeLoader once the config files are read — authoritative
    /// (wins over the lazy fallback, e.g. on Android where StreamingAssets needs
    /// an async read the sync fallback can't do).
    public static void SetResolved(DemoConfig config) => _config = config ?? new DemoConfig();

    /// Lazy fallback so the button works in the Editor / on desktop WITHOUT the
    /// DemoModeLoader component in the scene: read the config synchronously the
    /// first time it's queried. StreamingAssets is a real folder in the Editor and
    /// standalone player (only Android needs the async loader). The persistentData
    /// override still wins when present.
    private static void EnsureResolved()
    {
        if (_config != null) return;
        string persistent = ReadOrNull(Path.Combine(Application.persistentDataPath, "demo-config.json"));
        string streaming = null;
        string streamingPath = Path.Combine(Application.streamingAssetsPath, "demo-config.json");
        if (!streamingPath.Contains("://")) streaming = ReadOrNull(streamingPath);   // Android → loader handles it
        _config = Resolve(persistent, streaming);
    }

    private static string ReadOrNull(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch { return null; }
    }

    /// The persistentDataPath override wins outright when present (it can also
    /// force-disable); otherwise the shipped StreamingAssets default applies;
    /// missing/malformed → disabled.
    public static DemoConfig Resolve(string persistentJson, string streamingJson)
        => TryParse(persistentJson) ?? TryParse(streamingJson) ?? new DemoConfig();

    private static DemoConfig TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonUtility.FromJson<DemoConfig>(json); }
        catch { return null; }
    }

    /// The ready-made end product each demo stage also spawns (user: "in demo
    /// mode, these ready-made alongside these raw elements must be present").
    public static string ProductFor(string moduleId)
    {
        switch (moduleId)
        {
            case "tutorial-methane": return "Sodium Acetate";        // gas product — show the feedstock
            case "prelim-chemical-compounding": return "Ethanol";
            case "prelim-ethyl-alcohol": return "Ethanol";
            case "midterm-benzoic-acid": return "Benzoic Acid";
            case "midterm-acetanilide": return "Acetanilide";
            case "midterm-acetone": return "Acetone";
            case "midterm-chloroform": return "Chloroform";
            case "final-benzamide": return "Benzamide";
            case "final-aspirin": return "Aspirin";
            case "final-caffeine": return "Caffeine";
            case "final-winemaking": return "Grape Juice";
            default: return null;
        }
    }

    /// Demo sessions persist to their own file so panel demos never pollute the
    /// real progression ("pharmasynth_progress.json" → "pharmasynth_progress_demo.json").
    public static string SavePathFor(bool demoActive, string normalPath)
    {
        if (!demoActive || string.IsNullOrEmpty(normalPath)) return normalPath;
        int dot = normalPath.LastIndexOf('.');
        return dot < 0 ? normalPath + "_demo"
                       : normalPath.Substring(0, dot) + "_demo" + normalPath.Substring(dot);
    }
}

/// Whether THIS play session was entered through the Demo Mode button.
/// (Static so it survives the menu→lab scene load, like GameFlow.SelectedModuleId.)
public static class DemoSession
{
    public static bool Active;
}
