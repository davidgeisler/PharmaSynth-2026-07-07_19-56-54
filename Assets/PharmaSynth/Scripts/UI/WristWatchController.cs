using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// Wrist-flip progress tracker (user's headline feature). Flipping the wrist so
/// the watch face turns up (supination) while glancing toward it shows a compact
/// panel: current step, progress %, mastery %. A button/thumbstick fallback
/// toggles it so the feature works without the gesture (and is testable pre-HMD).
///
/// Gesture detection is via the anchor transform's up-vector, which works for both
/// controller supination and hand-tracking palm-up. Hysteresis prevents flicker.
public class WristWatchController : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;

    [Header("Anchor & view")]
    [SerializeField] private Transform watchAnchor;     // on the wrist (right hand default)
    [SerializeField] private Transform headTransform;   // HMD camera, for the glance check
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text summaryText;

    [Header("Gesture")]
    [SerializeField, Range(0f, 1f)] private float supinationShow = 0.6f;   // face-up threshold to show
    [SerializeField, Range(0f, 1f)] private float supinationHide = 0.4f;   // lower = hysteresis to hide
    [SerializeField, Range(0f, 1f)] private float gazeThreshold = 0.5f;    // head looking toward wrist
    [SerializeField] private bool requireGaze = true;

    [Header("Fallback input")]
    [SerializeField] private InputActionReference toggleAction;            // button/thumbstick fallback

    [Header("Holo checklist (large panel — the user's headline feature)")]
    [SerializeField] private GameObject holoPanel;      // world-space holographic board
    [SerializeField] private TMP_Text holoTitle;
    [SerializeField] private TMP_Text holoBody;
    [SerializeField] private TMP_Text holoSummary;      // one-line header (absorbed the wrist mini-panel)
    [SerializeField] private TMP_Text holoReaction;     // balanced-reaction footer (absorbed the LabTablet's)
    [SerializeField, TextArea] private string balancedReaction = "";
    [SerializeField] private float holoDistance = 1.15f;
    [SerializeField] private float holoHeightOffset = -0.05f;

    private bool _gestureVisible;
    private bool _manualVisible;
    private bool _lastShow;

    public void BindHolo(GameObject panel, TMP_Text title, TMP_Text body)
    { holoPanel = panel; holoTitle = title; holoBody = body; }

    public void BindHolo(GameObject panel, TMP_Text title, TMP_Text summary, TMP_Text body, TMP_Text reaction)
    { holoPanel = panel; holoTitle = title; holoSummary = summary; holoBody = body; holoReaction = reaction; }

    public void SetReaction(string reaction) => balancedReaction = reaction;

    private void OnEnable()
    {
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.performed += OnTogglePressed;
            toggleAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (toggleAction != null && toggleAction.action != null)
            toggleAction.action.performed -= OnTogglePressed;
    }

    private void OnTogglePressed(InputAction.CallbackContext _) => _manualVisible = !_manualVisible;

    private void Update()
    {
        if (watchAnchor != null)
        {
            float faceUp = Vector3.Dot(watchAnchor.up, Vector3.up);
            // Hysteresis: raise above show-threshold to appear, drop below hide-threshold to vanish.
            if (!_gestureVisible && faceUp >= supinationShow) _gestureVisible = true;
            else if (_gestureVisible && faceUp < supinationHide) _gestureVisible = false;

            if (_gestureVisible && requireGaze && headTransform != null)
                _gestureVisible = IsGazingAt(headTransform.position, headTransform.forward, watchAnchor.position, gazeThreshold);
        }

        bool show = _gestureVisible || _manualVisible;
        // No experiment content, no panels — otherwise the simulator's resting
        // palm-up controllers summon an empty board in the corridor/lab tour.
        if (runner == null || runner.Graph == null) show = false;
        // The wrist mini-panel is retired (user 2026-07-10: one procedures
        // display, centered) — the holo board below is the single panel now.
        if (panel != null && panel.activeSelf) panel.SetActive(false);

        // Large holographic procedures board: appears in front of the player on
        // the same gesture. Focused checklist (active phase in detail, others
        // collapsed) + status header + reaction footer.
        if (show && !_lastShow) PlaceHolo();
        _lastShow = show;
        if (holoPanel != null && holoPanel.activeSelf != show) holoPanel.SetActive(show);
        if (show && runner != null)
        {
            if (holoTitle != null) holoTitle.text = runner.Module != null ? runner.Module.moduleTitle : "Procedures";
            if (holoSummary != null) holoSummary.text = ChecklistPager.BuildHeader(runner);
            if (holoBody != null && runner.Graph != null)
                holoBody.text = ChecklistPager.BuildFocusedText(runner.Graph);
            if (holoReaction != null) holoReaction.text = GlyphSafe.Sanitize(balancedReaction);
        }
    }

    /// Park the holo board in front of the player's face, upright, readable.
    private void PlaceHolo()
    {
        if (holoPanel == null || headTransform == null) return;
        Vector3 fwd = headTransform.forward; fwd.y = 0f;
        fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
        holoPanel.transform.position = headTransform.position + fwd * holoDistance + Vector3.up * holoHeightOffset;
        holoPanel.transform.rotation = Quaternion.LookRotation(fwd);   // +Z away → UI reads correctly
    }

    public static string BuildSummary(ExperimentRunner runner)
    {
        if (runner == null || runner.Graph == null) return "";
        string current = "—";
        foreach (var t in runner.Graph.AvailableTasks()) { current = GlyphSafe.Sanitize(t.label); break; }
        return "Step: " + current
             + "\nProgress " + ExperimentHudController.FormatPercent(runner.Progress01)
             + "\nMastery " + Mathf.RoundToInt(runner.OverallMastery * 100f) + "%";
    }

    /// True when the head is looking roughly toward the wrist.
    public static bool IsGazingAt(Vector3 headPos, Vector3 headForward, Vector3 targetPos, float dotThreshold)
    {
        Vector3 toTarget = targetPos - headPos;
        if (toTarget.sqrMagnitude < 1e-6f) return true;
        return Vector3.Dot(headForward.normalized, toTarget.normalized) >= dotThreshold;
    }
}
