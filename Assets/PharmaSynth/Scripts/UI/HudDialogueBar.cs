using TMPro;
using UnityEngine;

/// Screen-bottom dialogue bar (storyboard style): Pharmee's portrait + the line he
/// is currently speaking, mirrored from NPCNarrationController so the player can
/// read him without looking at him. Visible ONLY while a line is live.
public class HudDialogueBar : MonoBehaviour
{
    [SerializeField] private NPCNarrationController narration;
    [SerializeField] private GameObject barRoot;      // toggled with the line
    [SerializeField] private TMP_Text speakerText;    // "Pharmee"
    [SerializeField] private TMP_Text lineText;

    private bool _subscribed;

    /// Edit-mode/test binding.
    public void Bind(NPCNarrationController n, GameObject root, TMP_Text speaker, TMP_Text line)
    {
        Unsubscribe();
        narration = n; barRoot = root; speakerText = speaker; lineText = line;
        Subscribe();
        if (barRoot != null) barRoot.SetActive(false);
    }

    private void OnEnable()
    {
        Subscribe();
        if (barRoot != null && (narration == null || !narration.IsSpeaking))
            barRoot.SetActive(false);
    }

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed || narration == null) return;
        narration.LineStarted += HandleLineStarted;
        narration.LineEnded += HandleLineEnded;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || narration == null) return;
        narration.LineStarted -= HandleLineStarted;
        narration.LineEnded -= HandleLineEnded;
        _subscribed = false;
    }

    /// Public for headless tests.
    public void HandleLineStarted(string line, float seconds)
    {
        if (lineText != null) lineText.text = line;
        if (speakerText != null && string.IsNullOrEmpty(speakerText.text)) speakerText.text = "Pharmee";
        if (barRoot != null) barRoot.SetActive(true);
    }

    public void HandleLineEnded()
    {
        if (barRoot != null) barRoot.SetActive(false);
        if (lineText != null) lineText.text = string.Empty;
    }
}
