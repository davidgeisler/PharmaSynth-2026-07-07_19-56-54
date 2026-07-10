using UnityEngine;
using UnityEngine.SceneManagement;

/// Drives the cube spawn-room menu (3 options, user 2026-07-10): Laboratory enters
/// the lab at the entrance (Pharmee's gate flow then handles episode choice);
/// Settings toggles the settings panel; Quit exits the game. The lab scene's
/// ExperimentLauncher reads GameFlow.SelectedModuleId on load; the Tutorial is now
/// reached inside the lab via Pharmee's episode picker rather than a menu button.
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string labSceneName = "SampleScene";
    [SerializeField] private string fallbackModuleId = "tutorial-methane";
    [SerializeField] private GameObject settingsPanel;

    /// Compute which experiment "Enter Laboratory" pre-selects as the default: the
    /// player's next unlocked-but-unpassed experiment, or the tutorial if none/unknown.
    /// (Only a default — the in-lab gate flow lets the player pick.) Pure so the
    /// self-tests can check it without a live ProgressionService on disk.
    public static string ResolveLabTarget(ProgressionFlow flow, string fallback)
    {
        var next = flow?.NextExperiment();
        return next != null ? next.moduleId : fallback;
    }

    public void OnLaboratory()
    {
        DemoSession.Active = false;          // normal play never inherits a demo session
        EnterLab();
    }

    /// The config-gated Demo Mode button (visible only when the backend file
    /// enables it): same entry, but on the throwaway demo save with every
    /// period unlocked and the HUD auto-complete controls armed.
    public void OnDemoLaboratory()
    {
        DemoSession.Active = true;
        EnterLab();
    }

    private void EnterLab()
    {
        var service = new ProgressionService();
        service.Load();
        GameFlow.Select(ResolveLabTarget(ProgressionFlow.Create(service), fallbackModuleId));
        ScreenFader.FadeOutThen(() => SceneManager.LoadScene(labSceneName));
    }

    public void OnSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void OnQuit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
