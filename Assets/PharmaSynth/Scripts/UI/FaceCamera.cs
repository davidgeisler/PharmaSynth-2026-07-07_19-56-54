using UnityEngine;

/// Rotates a world-space label or canvas to face the player's camera every frame,
/// so text stays readable from any side (never mirrored / seen from behind).
/// Y-axis mode keeps signs upright; full mode also pitches toward the viewer.
public class FaceCamera : MonoBehaviour
{
    [Tooltip("Rotate around Y only (stay upright) — right for signs, panels and labels.")]
    public bool yAxisOnly = true;

    [Tooltip("Extra yaw in degrees if an asset's readable side is not its -Z face.")]
    public float yawOffset = 0f;

    private Transform _cam;

    private void LateUpdate()
    {
        if (_cam == null)
        {
            var c = Camera.main;
            if (c == null) return;      // no camera yet (e.g. edit mode)
            _cam = c.transform;
        }

        // UGUI canvases and 3D TMP text are readable when their forward (+Z)
        // points AWAY from the viewer.
        Vector3 dir = transform.position - _cam.position;
        if (yAxisOnly) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up)
                             * Quaternion.Euler(0f, yawOffset, 0f);
    }
}
