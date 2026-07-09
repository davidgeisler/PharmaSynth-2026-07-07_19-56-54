using UnityEngine;

/// Swings a hinged door leaf open/closed and toggles its colliders — the lab
/// entrance the PharmeeGatekeeper controls. Runtime animates the swing; edit
/// mode snaps instantly (headless-testable).
public class DoorOpener : MonoBehaviour
{
    [SerializeField] private Transform door;      // the door-leaf transform (pivot = hinge)
    [SerializeField] private float openYaw = -100f;
    [SerializeField] private float seconds = 0.8f;

    private Quaternion _closed, _open;
    private bool _cached, _isOpen;

    public bool IsOpen => _isOpen;
    public Transform Door => door;

    public void SetDoor(Transform d, float yaw)
    {
        door = d; openYaw = yaw; _cached = false;
        Cache();
    }

    private void Awake() => Cache();

    private void Cache()
    {
        if (_cached || door == null) return;
        _closed = door.localRotation;
        _open = _closed * Quaternion.Euler(0f, openYaw, 0f);
        _cached = true;
    }

    /// Open/shut the doorway. The leaf's colliders stay ENABLED at all times —
    /// the swing itself clears the doorway, and the open leaf remains a solid
    /// panel against the wall (no phasing through it).
    public void SetOpen(bool open)
    {
        Cache();
        if (!_cached) return;
        _isOpen = open;
        if (!Application.isPlaying)
            door.localRotation = open ? _open : _closed;
    }

    private void Update()
    {
        if (!_cached || door == null) return;
        var target = _isOpen ? _open : _closed;
        float speed = Mathf.Abs(openYaw) / Mathf.Max(0.1f, seconds);
        door.localRotation = Quaternion.RotateTowards(door.localRotation, target, speed * Time.deltaTime);
    }
}
