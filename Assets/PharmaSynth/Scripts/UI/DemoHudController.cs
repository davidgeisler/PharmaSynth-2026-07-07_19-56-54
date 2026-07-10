using UnityEngine;

/// The demo HUD cluster (Skip Step / Finish Experiment / Auto-Answer Quiz),
/// visible only during a demo session and only when each verb applies. Built by
/// Tools ▸ PharmaSynth ▸ Demo ▸ Build Demo HUD; buttons call the On* methods.
public class DemoHudController : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private PostLabController postLab;
    [SerializeField] private GameObject cluster;        // row root
    [SerializeField] private GameObject skipButton;
    [SerializeField] private GameObject finishButton;
    [SerializeField] private GameObject quizButton;

    public void Bind(ExperimentRunner r, PostLabController p,
        GameObject clusterRoot, GameObject skip, GameObject finish, GameObject quiz)
    { runner = r; postLab = p; cluster = clusterRoot; skipButton = skip; finishButton = finish; quizButton = quiz; }

    private void Update()
    {
        bool running = DemoSession.Active && runner != null && runner.IsRunning;
        bool quizOpen = DemoSession.Active && postLab != null && postLab.IsOpen;
        Toggle(cluster, running || quizOpen);
        Toggle(skipButton, running);
        Toggle(finishButton, running);
        Toggle(quizButton, quizOpen);
    }

    private static void Toggle(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }

    public void OnSkipStep() => DemoActions.CompleteCurrentStep(runner);

    public void OnFinishExperiment() => DemoActions.CompleteAllTasks(runner);

    public void OnAutoQuiz()
    {
        if (DemoActions.AutoAnswerQuiz(postLab) && postLab != null) postLab.Submit();
    }
}
