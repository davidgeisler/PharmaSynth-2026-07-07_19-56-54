using UnityEngine;

/// Shows/hides the cube-room "Demo Mode" button from the config file's verdict.
/// Lives on an always-active parent (the menu panel) because a disabled button
/// can't re-enable itself — and the config loads asynchronously on Android, so
/// this polls rather than checking once. [ExecuteAlways] so it also reflects the
/// config in the Editor Scene view (the button shows without entering Play mode).
[ExecuteAlways]
public class DemoButtonVisibility : MonoBehaviour
{
    [SerializeField] private GameObject demoButton;

    public void Bind(GameObject button) => demoButton = button;

    private void Update()
    {
        if (demoButton != null && demoButton.activeSelf != DemoMode.IsEnabled)
            demoButton.SetActive(DemoMode.IsEnabled);
    }
}
