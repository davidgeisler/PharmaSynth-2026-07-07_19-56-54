using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Pure rules for when a loose prop goes back to its shelf spot (§2: kill-Z +
/// idle return-to-home). Separated from the MonoBehaviour so the self-tests
/// can pin the policy.
public static class DropRespawnMath
{
    /// Fell out of the world (through a gap, off the bench into the void).
    public static bool ShouldRespawn(float y, float killZ) => y < killZ;

    /// Resting height below which an item counts as abandoned on the FLOOR.
    /// Bench/table tops sit ≈0.93 m, the workspace shelf higher still — items the
    /// player stages on any work surface must never be reclaimed mid-run (user
    /// 2026-07-12: carried equipment vanished back to the shelves, making
    /// experiments impossible). Floor litter still tidies itself.
    public const float FloorY = 0.5f;

    /// Sat still, un-held, away from home long enough that the player has
    /// clearly abandoned it — AND lying at floor level. Items resting on tables,
    /// shelves or station pads stay exactly where the player put them.
    public static bool ShouldReturnHome(float distanceFromHome, float speed, bool held, float idleSeconds,
                                        float restingY,
                                        float minIdle = 25f, float minDistance = 0.4f, float restSpeed = 0.05f,
                                        float floorY = FloorY)
        => !held && speed < restSpeed && idleSeconds >= minIdle && distanceFromHome > minDistance
           && restingY < floorY;

    /// Settle-freeze (W5.8 breakage pass): a released body that has genuinely
    /// come to rest goes kinematic IN PLACE. Released items used to stay
    /// dynamic indefinitely, so a physics-solver de-penetration spike on a
    /// resting beaker could cross the break threshold — kinematic items are
    /// immune in BreakableGlassware, and a still bench costs no solver time.
    /// Grabbing re-frees the body (XRI + GrabPhysicsPolicy own that side).
    public static bool ShouldSettleFreeze(bool held, bool kinematic, float speed, float settledSeconds,
                                          float minSettle = 2.5f, float restSpeed = 0.05f)
        => !held && !kinematic && speed < restSpeed && settledSeconds >= minSettle;
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

    // Serialized: the editor fix pass sets homes in edit mode and they must
    // survive into play mode.
    [SerializeField] private Vector3 _homePos;
    [SerializeField] private Quaternion _homeRot;
    [SerializeField] private bool _hasHome;
    private float _idle;
    private Rigidbody _rb;
    private XRGrab _grab;

    // Home SUPPLY (user 2026-07-12: an exhausted bottle respawned empty — and
    // bottles whose body renderer IS the fill visual respawned invisible, "only
    // the lid and name tag"). Captured on the first play-mode Update, after the
    // stage builder has filled spawned pourables; GoHome() restores it so every
    // respawn is a genuinely fresh replacement (lab tour = unlimited by nature).
    private ChemicalData _homeChem;
    private float _homeMl;
    private bool _supplyCaptured;
    private LiquidPhysics _liquid;

    void Awake() { if (_rb == null) Bind(GetComponent<Rigidbody>(), GetComponent<XRGrab>()); }

    public void Bind(Rigidbody rb, XRGrab grab) { _rb = rb; _grab = grab; }

    public void SetHome(Vector3 pos, Quaternion rot) { _homePos = pos; _homeRot = rot; _hasHome = true; }

    /// While part of an apparatus assembly, respawn is the assembly's problem —
    /// a mid-stack GoHome would teleport a piece out of the formation (W5.12).
    public bool Suspended { get; set; }

    public void SetKillZ(float y) => killZ = y;

    void Update()
    {
        if (Suspended || !_hasHome) return;
        if (!_supplyCaptured) CaptureSupply();
        bool held = _grab != null && _grab.isSelected;
        float speed = _rb != null && !_rb.isKinematic ? _rb.linearVelocity.magnitude : 0f;
        float dist = (transform.position - _homePos).magnitude;

        _idle = held || speed >= 0.05f ? 0f : _idle + Time.deltaTime;

        if (!held && DropRespawnMath.ShouldRespawn(transform.position.y, killZ)) { GoHome(); return; }
        if (DropRespawnMath.ShouldReturnHome(dist, speed, held, _idle, transform.position.y,
                                             minIdleSeconds, minDistance)) { GoHome(); return; }

        // Settle-freeze: at rest for a beat → kinematic in place (immune to
        // solver spikes; next grab re-frees it).
        if (_rb != null && DropRespawnMath.ShouldSettleFreeze(held, _rb.isKinematic, speed, _idle))
            _rb.isKinematic = true;
    }

    /// Record the current contents as this item's fresh-replacement supply.
    /// Public so tests (and future builders) can capture deterministically.
    public void CaptureSupply()
    {
        if (_liquid == null) _liquid = GetComponent<LiquidPhysics>();
        _homeChem = _liquid != null ? _liquid.currentChemical : null;
        _homeMl = _liquid != null ? _liquid.currentLiquidVolume : 0f;
        _supplyCaptured = true;
    }

    /// Teleport back to the shelf spot, re-freeze, and restore the home supply
    /// (public for tests/tools). Empty receivers reset to empty — a respawned
    /// vessel never keeps a mid-run mixture.
    public void GoHome()
    {
        if (!_hasHome) return;
        if (_rb != null)
        {
            _rb.isKinematic = true;   // back to shelf policy; next release re-frees it
        }
        transform.SetPositionAndRotation(_homePos, _homeRot);
        if (_supplyCaptured)
        {
            if (_liquid == null) _liquid = GetComponent<LiquidPhysics>();
            if (_liquid != null) _liquid.SetContents(_homeChem, _homeMl);
        }
        _idle = 0f;
    }
}
