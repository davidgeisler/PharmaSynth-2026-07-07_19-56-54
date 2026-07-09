using UnityEngine;
using UnityEngine.SceneManagement;

/// Drives the main-menu buttons. Tutorial launches the guided Methane tutorial;
/// Laboratory enters the lab at whatever the player should tackle next (via the
/// progression flow); Settings toggles the settings panel; Quit exits. The lab scene's
/// ExperimentLauncher reads GameFlow.SelectedModuleId on load.
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string labSceneName = "SampleScene";
    [SerializeField] private string tutorialModuleId = "tutorial-methane";
    [SerializeField] private GameObject settingsPanel;

    /// Compute which experiment "Enter Laboratory" should open: the player's next
    /// unlocked-but-unpassed experiment, or the tutorial if none/unknown. Pure so the
    /// self-tests can check it without a live ProgressionService on disk.
    public static string ResolveLabTarget(ProgressionFlow flow, string fallback)
    {
        var next = flow?.NextExperiment();
        return next != null ? next.moduleId : fallback;
    }

    public void OnTutorial()
    {
        GameFlow.Select(tutorialModuleId);
        ScreenFader.FadeOutThen(() => SceneManager.LoadScene(labSceneName));
    }

    public void OnLaboratory()
    {
        var service = new ProgressionService();
        service.Load();
        GameFlow.Select(ResolveLabTarget(new ProgressionFlow(service), tutorialModuleId));
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
