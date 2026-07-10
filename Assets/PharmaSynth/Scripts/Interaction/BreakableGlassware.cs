using UnityEngine;

/// Glassware that shatters when dropped hard (§2 mishandling penalties).
/// Only a FREE (dynamic) item can break — kinematic shelf items and held
/// items never do. On break: shatter SFX, a DroppedGlassware mistake against
/// the Sanitation rubric, and the item goes home via DropRespawn as a fresh
/// replacement so the experiment stays completable.
public class BreakableGlassware : MonoBehaviour
{
    [SerializeField] private float breakImpactSpeed = Mishandling.DefaultBreakSpeed;
    [SerializeField, Min(0f)] private float rearmSeconds = 1.5f;

    private ExperimentRunner _runner;
    private DropRespawn _respawn;
    private Rigidbody _rb;
    private string _label = "Glassware";
    private float _cooldownUntil;

    void Awake()
    {
        if (_rb == null)
            Bind(FindAnyObjectByType<ExperimentRunner>(), GetComponent<DropRespawn>(), GetComponent<Rigidbody>(), name);
    }

    /// Edit-mode / builder seam (Awake doesn't fire on edit-mode AddComponent).
    public void Bind(ExperimentRunner runner, DropRespawn respawn, Rigidbody rb, string label)
    {
        _runner = runner; _respawn = respawn; _rb = rb;
        if (!string.IsNullOrEmpty(label)) _label = label;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (_rb == null || _rb.isKinematic) return;          // on the shelf / in a hand
        if (Time.time < _cooldownUntil) return;
        if (!Mishandling.ShouldBreak(collision.relativeVelocity.magnitude, breakImpactSpeed)) return;
        Break();
    }

    /// Public so tests and hazard systems can force it.
    public void Break()
    {
        _cooldownUntil = Time.time + rearmSeconds;
        if (_runner != null)
            _runner.RecordMistake(LabErrorType.DroppedGlassware, _label + " shattered — handle glassware gently");
        if (AudioService.Instance != null) AudioService.Instance.Play("glass-shatter");
        // A liquid-carrying vessel leaves its contents pooled where it fell,
        // lingering then fading out (user 2026-07-10).
        var liquid = GetComponent<LiquidPhysics>();
        if (Application.isPlaying && liquid != null && liquid.currentLiquidVolume > 1f)
            SpillPuddle.Spawn(transform.position, SpillMistake.LiquidColorOf(liquid));
        if (_respawn != null) _respawn.GoHome();             // replacement back on the shelf
    }
}
