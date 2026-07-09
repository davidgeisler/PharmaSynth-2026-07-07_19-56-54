using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Pure rules for when a loose prop goes back to its shelf spot (§2: kill-Z +
/// idle return-to-home). Separated from the MonoBehaviour so the self-tests
/// can pin the policy.
public static class DropRespawnMath
{
    /// Fell out of the world (through a gap, off the bench into the void).
    public static bool ShouldRespawn(float y, float killZ) => y < killZ;

    /// Sat still, un-held, away from home long enough that the player has
    /// clearly abandoned it.
    public static bool ShouldReturnHome(float distanceFromHome, float speed, bool held, float idleSeconds,
                                        float minIdle = 25f, float minDistance = 0.4f, float restSpeed = 0.05f)
        => !held && speed < restSpeed && idleSeconds >= minIdle && distanceFromHome > minDistance;
}

/// Sends a dropped prop home: below kill-Z → instant respawn; resting far from
/// home and untouched for ~25 s → quiet return. Restores the shelf policy
/// (kinematic) on arrival so the shelf stays tidy. Home is captured via
/// SetHome() by the spawner (Awake doesn't fire on edit-mode AddComponent).
public class DropRespawn : MonoBehaviour
{
    [SerializeField] private float killZ = -1f;
    [SerializeField] private float minIdleSeconds = 25f;
    [SerializeField] private float minDistance = 0.4f;

    private Vector3 _homePos;
    private Quaternion _homeRot;
    private bool _hasHome;
    private float _idle;
    private Rigidbody _rb;
    private XRGrab _grab;

    void Awake() { if (_rb == null) Bind(GetComponent<Rigidbody>(), GetComponent<XRGrab>()); }

    public void Bind(Rigidbody rb, XRGrab grab) { _rb = rb; _grab = grab; }

    public void SetHome(Vector3 pos, Quaternion rot) { _homePos = pos; _homeRot = rot; _hasHome = true; }

    public void SetKillZ(float y) => killZ = y;

    void Update()
    {
        if (!_hasHome) return;
        bool held = _grab != null && _grab.isSelected;
        float speed = _rb != null && !_rb.isKinematic ? _rb.linearVelocity.magnitude : 0f;
        float dist = (transform.position - _homePos).magnitude;

        _idle = held || speed >= 0.05f ? 0f : _idle + Time.deltaTime;

        if (!held && DropRespawnMath.ShouldRespawn(transform.position.y, killZ)) { GoHome(); return; }
        if (DropRespawnMath.ShouldReturnHome(dist, speed, held, _idle, minIdleSeconds, minDistance)) GoHome();
    }

    /// Teleport back to the shelf spot and re-freeze (public for tests/tools).
    public void GoHome()
    {
        if (!_hasHome) return;
        if (_rb != null)
        {
            _rb.isKinematic = true;   // back to shelf policy; next release re-frees it
        }
        transform.SetPositionAndRotation(_homePos, _homeRot);
        _idle = 0f;
    }
}
