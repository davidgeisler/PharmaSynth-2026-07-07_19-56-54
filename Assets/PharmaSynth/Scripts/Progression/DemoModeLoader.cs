using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// Reads the demo-mode config at startup (on the Services GO in both scenes) and
/// installs it into DemoMode. Two locations:
///   • StreamingAssets/demo-config.json — the shipped default. On Android it
///     lives INSIDE the APK, so it must be read via UnityWebRequest.
///   • persistentDataPath/demo-config.json — the field override; wins when present.
public class DemoModeLoader : MonoBehaviour
{
    public const string FileName = "demo-config.json";

    private void Awake()
    {
        if (Application.isPlaying) StartCoroutine(LoadRoutine());
    }

    private IEnumerator LoadRoutine()
    {
        string persistent = ReadOrNull(Path.Combine(Application.persistentDataPath, FileName));

        string streaming = null;
        string streamingPath = Path.Combine(Application.streamingAssetsPath, FileName);
        if (streamingPath.Contains("://"))          // Android: inside the APK
        {
            using (var req = UnityWebRequest.Get(streamingPath))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                    streaming = req.downloadHandler.text;
            }
        }
        else
        {
            streaming = ReadOrNull(streamingPath);
        }

        DemoMode.SetResolved(DemoMode.Resolve(persistent, streaming));
    }

    private static string ReadOrNull(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch { return null; }
    }
}
