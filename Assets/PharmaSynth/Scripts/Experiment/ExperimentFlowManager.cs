using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class ExperimentFlowManager : MonoBehaviour
{
    public static ExperimentFlowManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private ExperimentModuleDefinition moduleDefinition;

    [Header("Top HUD")]
    [SerializeField] private TMP_Text moduleTitleText;
    [SerializeField] private TMP_Text moduleStatusText;
    [SerializeField] private float statusVisibleSeconds = 3f;

    [Header("Intended Learning Outcomes")]
    [SerializeField] private GameObject outcomesPanel;
    [SerializeField] private TMP_Text outcomesText;

    [Header("Summary")]
    [SerializeField] private GameObject summaryPanel;
    [SerializeField] private TMP_Text summaryTitleText;
    [SerializeField] private TMP_Text summaryBodyText;

    [Header("Readability")]
    [SerializeField] private Color titleColor = new Color(0.95f, 0.95f, 1f);
    [SerializeField] private Color statusOkColor = new Color(0.35f, 1f, 0.6f);
    [SerializeField] private Color statusErrorColor = new Color(1f, 0.45f, 0.45f);
    [SerializeField] private Color statusInfoColor = new Color(1f, 0.85f, 0.2f);

    [Header("Score Tuning")]
    [SerializeField] private int wrongStepPenalty = 5;
    [SerializeField] private int wrongReagentPenalty = 8;
    [SerializeField] private int droppedGlassPenalty = 10;
    [SerializeField] private int fireIncidentPenalty = 15;

    [Header("Events")]
    public UnityEvent onModuleStarted;
    public UnityEvent onModuleCompleted;

    public event Action<string> TaskCompleted;
    public event Action<string> MistakeRecorded;

    private readonly HashSet<string> completedTaskIds = new HashSet<string>();
    private readonly MistakeLog mistakes = new MistakeLog();
    private Coroutine statusRoutine;
    private int currentScore;
    private int maxScore;
    private bool moduleStarted;
    private bool moduleEnded;

    public int CurrentScore => currentScore;
    public int MaxScore => maxScore;
    public int MistakeCount => mistakes.Count;
    public MistakeLog Mistakes => mistakes;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeMaxScore();
        currentScore = 0;
        InitializeHud();
    }

    private void Start()
    {
        ShowModuleTitle();
        ShowLearningOutcomes();
    }

    private void InitializeMaxScore()
    {
        maxScore = 0;
        if (moduleDefinition == null || moduleDefinition.tasks == null)
            return;

        for (int i = 0; i < moduleDefinition.tasks.Count; i++)
        {
            ExperimentTaskDefinition task = moduleDefinition.tasks[i];
            if (task != null)
                maxScore += Mathf.Max(0, task.scoreValue);
        }

        if (maxScore <= 0)
            maxScore = 100;
    }

    private void InitializeHud()
    {
        if (moduleTitleText != null)
            moduleTitleText.color = titleColor;

        if (summaryPanel != null)
            summaryPanel.SetActive(false);
    }

    public void BeginModule()
    {
        if (moduleStarted)
            return;

        moduleStarted = true;
        moduleEnded = false;
        if (outcomesPanel != null)
            outcomesPanel.SetActive(false);

        PushStatus("Experiment started. Follow each procedure carefully.", statusInfoColor);
        onModuleStarted?.Invoke();
    }

    public void ShowLearningOutcomes()
    {
        if (outcomesPanel == null)
            return;

        outcomesPanel.SetActive(true);
        if (outcomesText == null)
            return;

        if (moduleDefinition == null || moduleDefinition.intendedLearningOutcomes == null || moduleDefinition.intendedLearningOutcomes.Count == 0)
        {
            outcomesText.text = "Intended Learning Outcomes\n- Understand the experiment workflow\n- Apply safe laboratory handling\n- Interpret reaction outcomes";
            return;
        }

        string lines = "Intended Learning Outcomes\n";
        for (int i = 0; i < moduleDefinition.intendedLearningOutcomes.Count; i++)
        {
            lines += "- " + moduleDefinition.intendedLearningOutcomes[i] + "\n";
        }

        outcomesText.text = lines.TrimEnd();
    }

    public void CompleteTask(string taskId)
    {
        if (moduleEnded || string.IsNullOrWhiteSpace(taskId) || completedTaskIds.Contains(taskId))
            return;

        completedTaskIds.Add(taskId);
        string label = taskId;

        if (moduleDefinition != null && moduleDefinition.tasks != null)
        {
            for (int i = 0; i < moduleDefinition.tasks.Count; i++)
            {
                ExperimentTaskDefinition task = moduleDefinition.tasks[i];
                if (task != null && task.taskId == taskId)
                {
                    label = task.taskLabel;
                    currentScore += Mathf.Max(0, task.scoreValue);
                    break;
                }
            }
        }

        PushStatus(label + " Completed", statusOkColor);
        TaskCompleted?.Invoke(taskId);

        if (AreRequiredTasksComplete())
            EndModule();
    }

    public void MarkWrongReagent(string primaryChemical, string secondaryChemical)
    {
        ApplyPenalty(wrongReagentPenalty);
        string message = "Wrong reagent mix: " + primaryChemical + " + " + secondaryChemical;
        mistakes.Record(LabErrorType.WrongReagent, message);
        PushStatus(message, statusErrorColor);
        MistakeRecorded?.Invoke(message);
    }

    public void MarkWrongStep(string stepName)
    {
        ApplyPenalty(wrongStepPenalty);
        string message = "Wrong step detected: " + stepName;
        mistakes.Record(LabErrorType.WrongStep, message);
        PushStatus(message, statusErrorColor);
        MistakeRecorded?.Invoke(message);
    }

    public void MarkGlasswareDropped(string glasswareName)
    {
        ApplyPenalty(droppedGlassPenalty);
        string message = "Glassware dropped: " + glasswareName;
        mistakes.Record(LabErrorType.DroppedGlassware, message);
        PushStatus(message, statusErrorColor);
        MistakeRecorded?.Invoke(message);
    }

    public void MarkFireSafetyIncident(string details)
    {
        ApplyPenalty(fireIncidentPenalty);
        string message = "Fire procedure issue: " + details;
        mistakes.Record(LabErrorType.FireSafety, message);
        PushStatus(message, statusErrorColor);
        MistakeRecorded?.Invoke(message);
    }

    public void EndModule()
    {
        if (moduleEnded)
            return;

        moduleEnded = true;
        onModuleCompleted?.Invoke();
        ShowSummary();
    }

    private void ShowModuleTitle()
    {
        if (moduleTitleText == null)
            return;

        if (moduleDefinition == null || string.IsNullOrWhiteSpace(moduleDefinition.moduleTitle))
            moduleTitleText.text = "VR Chemistry Experiment";
        else
            moduleTitleText.text = moduleDefinition.moduleTitle;
    }

    private void ShowSummary()
    {
        if (summaryPanel != null)
            summaryPanel.SetActive(true);

        if (summaryTitleText != null)
            summaryTitleText.text = "Experiment Summary";

        if (summaryBodyText == null)
            return;

        int cappedScore = Mathf.Clamp(currentScore, 0, maxScore);
        float percent = maxScore <= 0 ? 0f : (float)cappedScore / maxScore * 100f;

        string body = "Final Score: " + cappedScore + " / " + maxScore + "\n";
        body += "Completion: " + percent.ToString("0") + "%\n";
        body += "Tasks Completed: " + completedTaskIds.Count;
        summaryBodyText.text = body;
    }

    private void ApplyPenalty(int penalty)
    {
        if (penalty <= 0)
            return;

        currentScore = Mathf.Max(0, currentScore - penalty);
    }

    private bool AreRequiredTasksComplete()
    {
        if (moduleDefinition == null || moduleDefinition.tasks == null || moduleDefinition.tasks.Count == 0)
            return false;

        for (int i = 0; i < moduleDefinition.tasks.Count; i++)
        {
            ExperimentTaskDefinition task = moduleDefinition.tasks[i];
            if (task != null && task.requiredForCompletion && !completedTaskIds.Contains(task.taskId))
                return false;
        }

        return true;
    }

    private void PushStatus(string status, Color color)
    {
        if (moduleStatusText == null)
            return;

        moduleStatusText.color = color;
        moduleStatusText.text = status;

        if (statusRoutine != null)
            StopCoroutine(statusRoutine);

        statusRoutine = StartCoroutine(ClearStatusAfterDelay());
    }

    private IEnumerator ClearStatusAfterDelay()
    {
        yield return new WaitForSeconds(statusVisibleSeconds);
        if (moduleStatusText != null)
            moduleStatusText.text = string.Empty;
    }
}
