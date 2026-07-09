using UnityEngine;

/// Thin driver for the lazy-follow HUD canvas: feeds the camera's pose into
/// HudFollowSolver each LateUpdate and applies the solved rig pose. Works with
/// the XR camera in-headset and the plain camera in the desktop simulator.
public class HudRigController : MonoBehaviour
{
    [SerializeField] private Camera cameraOverride;              // falls back to Camera.main
    [SerializeField] private HudFollowSolver.Params follow = HudFollowSolver.Params.Default;

    private HudFollowSolver.State _state;
    private bool _snapped;

    public HudFollowSolver.Params Follow { get => follow; set => follow = value; }

    public void SetCamera(Camera c) => cameraOverride = c;

    private Camera Cam => cameraOverride != null ? cameraOverride : Camera.main;

    /// Place the HUD directly on its anchor (scene start, after teleports).
    public void SnapToCamera()
    {
        var cam = Cam;
        if (cam == null) return;
        var t = cam.transform;
        _state = HudFollowSolver.Snapped(t.position, t.eulerAngles.y, in follow);
        Apply();
        _snapped = true;
    }

    private void LateUpdate()
    {
        var cam = Cam;
        if (cam == null) return;
        if (!_snapped) { SnapToCamera(); return; }
        var t = cam.transform;
        HudFollowSolver.Step(ref _state, t.position, t.eulerAngles.y, in follow, Time.deltaTime);
        Apply();
    }

    private void Apply()
    {
        transform.SetPositionAndRotation(_state.pos, Quaternion.Euler(0f, _state.yawDeg, 0f));
    }
}
