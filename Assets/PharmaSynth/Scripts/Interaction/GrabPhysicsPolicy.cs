using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Kinematic-on-shelf / dynamic-on-release policy (task #78). Props spawn
/// kinematic so shelves stay tidy and the rigidbody budget stays cheap; the
/// first time the player releases one, it goes dynamic so it falls and settles
/// instead of freezing mid-air (XRGrabInteractable restores the grab-time
/// kinematic state on release, which would leave items floating — this runs
/// after that restore and overrides it).
public class GrabPhysicsPolicy : MonoBehaviour
{
    private Rigidbody _rb;
    private XRGrab _grab;

    void Awake() { if (_rb == null) Bind(GetComponent<Rigidbody>(), GetComponent<XRGrab>()); }

    /// Edit-mode seam (Awake doesn't fire on AddComponent in edit mode).
    public void Bind(Rigidbody rb, XRGrab grab)
    {
        if (_grab != null) _grab.selectExited.RemoveListener(OnSelectExited);
        _rb = rb; _grab = grab;
        if (_grab != null) _grab.selectExited.AddListener(OnSelectExited);
    }

    void OnDestroy() { if (_grab != null) _grab.selectExited.RemoveListener(OnSelectExited); }

    private void OnSelectExited(SelectExitEventArgs _) => OnReleased();

    /// Make the item obey gravity from now on. Public so tests and non-XR
    /// callers (e.g. a drop-respawn system) can drive the same transition.
    public void OnReleased()
    {
        if (_rb == null) return;
        _rb.isKinematic = false;
        _rb.useGravity = true;
    }

    public bool IsDynamic => _rb != null && !_rb.isKinematic;
}
