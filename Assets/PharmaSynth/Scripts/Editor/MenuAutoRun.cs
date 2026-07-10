#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Menu-execution twin of SelfTestAutoRun, for when the MCP bridge is down:
/// if Logs/menu-autorun-request.txt exists, execute each listed line in order,
/// capture the console output, write it to Logs/menu-autorun-result.txt, and
/// consume the request. (Logs/, NOT Temp/ — Unity wipes Temp at editor startup,
/// which destroys any request queued while the editor was closed.) Line forms:
///   Tools/PharmaSynth/Wire Shelf Pourers      — execute that menu item
///   OPEN Assets/Scenes/SampleScene.unity      — open that scene first (single mode)
///   CAPTURE px py pz yaw pitch out.png        — DevCapture from that pose to the
///                                               given path (relative to project)
///   # comment                                 — ignored
/// Runs on the next domain reload in an interactive editor, or via
///   Unity.exe -batchmode -quit -projectPath <proj> -executeMethod MenuAutoRun.RunNow
/// Harmless when no request file is present.
[InitializeOnLoad]
public static class MenuAutoRun
{
    const string Request = "Logs/menu-autorun-request.txt";
    const string Result = "Logs/menu-autorun-result.txt";

    static MenuAutoRun()
    {
        if (!File.Exists(Request)) return;
        EditorApplication.delayCall += () => Execute();
    }

    /// Batchmode entry: Unity.exe ... -executeMethod MenuAutoRun.RunNow
    public static void RunNow() => Execute();

    static void Execute()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string[] lines = File.ReadAllLines(Request);
        File.Delete(Request);
        string captured = "";
        void Capture(string msg, string stack, LogType type)
        {
            captured += type + "  " + msg + "\n";
        }
        Application.logMessageReceived += Capture;
        try
        {
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                captured += "--- " + line + " ---\n";
                try
                {
                    if (line.StartsWith("OPEN "))
                    {
                        string scenePath = line.Substring(5).Trim();
                        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    }
                    else if (line.StartsWith("CAPTURE "))
                    {
                        var p = line.Substring(8).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                        if (p.Length >= 6)
                        {
                            var req = new DevCapture.Request
                            {
                                px = float.Parse(p[0]), py = float.Parse(p[1]), pz = float.Parse(p[2]),
                                yaw = float.Parse(p[3]), pitch = float.Parse(p[4]),
                                output = Path.GetFullPath(p[5]),
                            };
                            File.WriteAllText(DevCapture.RequestPath, JsonUtility.ToJson(req));
                            EditorApplication.ExecuteMenuItem("Tools/PharmaSynth/Dev Capture");
                        }
                        else captured += "BAD CAPTURE LINE (need px py pz yaw pitch out)\n";
                    }
                    else
                    {
                        bool ok = EditorApplication.ExecuteMenuItem(line);
                        if (!ok) captured += "MENU NOT FOUND: " + line + "\n";
                    }
                }
                catch (System.Exception e) { captured += "EXCEPTION: " + e + "\n"; }
            }
        }
        finally { Application.logMessageReceived -= Capture; }
        File.WriteAllText(Result, captured.Length > 0 ? captured : "no output captured");
        Debug.Log("[MenuAutoRun] wrote " + Result);
    }
}
#endif
