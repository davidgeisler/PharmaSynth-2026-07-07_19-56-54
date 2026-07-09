using UnityEngine;
using UnityEngine.Events;

/// Fires an event when the PLAYER (any collider under the rig root) enters this
/// trigger volume — used by the door-gate approach + threshold zones. Ignores
/// props, NPCs and stray physics bodies.
public class PlayerTriggerRelay : MonoBehaviour
{
    [SerializeField] private Transform playerRoot;    // the XR Origin (XR Rig) root
    public UnityEvent onPlayerEntered;

    public void SetPlayerRoot(Transform t) => playerRoot = t;

    private void OnTriggerEnter(Collider other)
    {
        if (playerRoot == null) return;
        if (other.transform == playerRoot || other.transform.IsChildOf(playerRoot))
            onPlayerEntered?.Invoke();
    }

    /// Headless test hook — same code path as a real entry.
    public void SimulateEnter() => onPlayerEntered?.Invoke();
}
