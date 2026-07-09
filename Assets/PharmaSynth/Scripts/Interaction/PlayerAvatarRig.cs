using UnityEngine;

/// Drives the mirror-only first-person avatar (user 2026-07-10): stands the body at
/// the player's feet, turns it to the head's yaw, and feeds the Animation-Rigging IK
/// targets (head + both hands) from the HMD and controllers so the reflection moves
/// with you. Attach to the avatar root; `PlayerAvatarBuilder` wires the transforms.
/// The avatar renders on the PlayerAvatar layer, which the main camera culls and the
/// mirror includes — so you see this only in the mirror.
public class PlayerAvatarRig : MonoBehaviour
{
    [Header("Sources (player rig)")]
    [SerializeField] private Transform head;             // HMD camera (Camera.main fallback)
    [SerializeField] private Transform leftController;
    [SerializeField] private Transform rightController;

    [Header("IK targets (rig children)")]
    [SerializeField] private Transform headTarget;
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform rightHandTarget;
    [SerializeField] private Transform leftElbowHint;
    [SerializeField] private Transform rightElbowHint;

    [Header("Tuning")]
    [SerializeField] private float eyeHeight = 1.6f;     // fallback foot drop if no floor hit
    [SerializeField] private float footOffsetY = 0f;
    [SerializeField] private float turnResponse = 8f;    // body-yaw smoothing

    private float _yaw;
    private bool _yawInit;

    public void Bind(Transform h, Transform l, Transform r,
                     Transform headT, Transform lHandT, Transform rHandT, Transform lElbow, Transform rElbow)
    {
        head = h; leftController = l; rightController = r;
        headTarget = headT; leftHandTarget = lHandT; rightHandTarget = rHandT;
        leftElbowHint = lElbow; rightElbowHint = rElbow;
    }

    private void LateUpdate()
    {
        var h = head != null ? head : (Camera.main != null ? Camera.main.transform : null);
        if (h == null) return;

        // Stand at the player's feet (camera XZ, floor Y).
        transform.position = FootUnder(h.position, FloorYUnder(h.position, h.position.y - eyeHeight), footOffsetY);

        // Body faces the head's yaw (smoothed so a quick head-turn doesn't snap the torso).
        float targetYaw = h.eulerAngles.y;
        if (!_yawInit) { _yaw = targetYaw; _yawInit = true; }
        _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-turnResponse * Mathf.Max(0f, Time.deltaTime)));
        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        // Feed the IK targets from the live HMD + controllers.
        if (headTarget != null) headTarget.SetPositionAndRotation(h.position, h.rotation);
        if (leftController != null && leftHandTarget != null)
            leftHandTarget.SetPositionAndRotation(leftController.position, leftController.rotation);
        if (rightController != null && rightHandTarget != null)
            rightHandTarget.SetPositionAndRotation(rightController.position, rightController.rotation);

        // Elbow poles: out to the sides and slightly back, relative to the body.
        if (leftElbowHint != null) leftElbowHint.position = transform.position + transform.rotation * new Vector3(-0.40f, 1.0f, -0.25f);
        if (rightElbowHint != null) rightElbowHint.position = transform.position + transform.rotation * new Vector3(0.40f, 1.0f, -0.25f);
    }

    private static float FloorYUnder(Vector3 headPos, float fallback)
    {
        if (Physics.Raycast(headPos + Vector3.up * 0.1f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return fallback;
    }

    /// Pure helper (testable): foot point under the head at a given floor height.
    public static Vector3 FootUnder(Vector3 headPos, float floorY, float offsetY)
        => new Vector3(headPos.x, floorY + offsetY, headPos.z);
}
