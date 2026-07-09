#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// One-shot self-test runner for when the MCP bridge is down: if
/// Temp/selftest-autorun-request.txt exists after a domain reload, run the
/// suite once, write the console result to Temp/selftest-autorun-result.txt,
/// and consume the request. Harmless when no request file is present.
[InitializeOnLoad]
public static class SelfTestAutoRun
{
    const string Request = "Temp/selftest-autorun-request.txt";
    const string Result = "Temp/selftest-autorun-result.txt";

    static SelfTestAutoRun()
    {
        if (!File.Exists(Request)) return;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            string captured = "";
            void Capture(string msg, string stack, LogType type)
            {
                if (msg.Contains("PharmaSynth Self-Tests")) captured += msg + "\n";
            }
            Application.logMessageReceived += Capture;
            try { PharmaSelfTests.Run(); }
            catch (System.Exception e) { captured += "EXCEPTION: " + e + "\n"; }
            finally { Application.logMessageReceived -= Capture; }
            File.WriteAllText(Result, captured.Length > 0 ? captured : "no summary captured");
            Debug.Log("[SelfTestAutoRun] wrote " + Result);
        };
    }
}
#endif
