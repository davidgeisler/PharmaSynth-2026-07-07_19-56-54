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

    private bool _gestureVisible;
    private bool _manualVisible;

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
        if (panel != null && panel.activeSelf != show) panel.SetActive(show);
        if (show && summaryText != null) summaryText.text = BuildSummary(runner);
    }

    public static string BuildSummary(ExperimentRunner runner)
    {
        if (runner == null || runner.Graph == null) return "";
        string current = "—";
        foreach (var t in runner.Graph.AvailableTasks()) { current = t.label; break; }
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
