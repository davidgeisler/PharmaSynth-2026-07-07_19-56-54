using UnityEngine;

/// Collapses the HUD's Settings/Restart/Quit actions behind a single icon button.
/// The icon's onClick calls Toggle(); each action button's onClick also calls
/// Close() so the list dismisses after a pick. Starts hidden. Pure SetActive —
/// the action buttons keep their own LabMenuController wiring untouched.
public class HudMenuDropdown : MonoBehaviour
{
    [SerializeField] private GameObject listPanel;   // the vertical Settings/Restart/Quit list

    public void SetList(GameObject panel) { listPanel = panel; if (listPanel != null) listPanel.SetActive(false); }

    private void OnEnable() { if (listPanel != null) listPanel.SetActive(false); }

    public void Toggle() { if (listPanel != null) listPanel.SetActive(!listPanel.activeSelf); }

    public void Close() { if (listPanel != null) listPanel.SetActive(false); }
}
