using UnityEngine;

/// Pharmee flight attitude (user 2026-07-10): lean the body into the movement
/// direction so he reads as flying through air, pulse the hover waves at his
/// base, and add a gentle bob-nod while he talks. Composes with FaceCamera
/// (root yaw) and FloatBob (position) by twisting only the body CHILD.
/// Head-level counter-rotation awaits a rigged model (animation-set bullet).
public class PharmeeAttitude : MonoBehaviour
{
    [SerializeField] private Transform bodyRoot;                 // "Robot Origin"
    [SerializeField] private Transform[] waves = new Transform[0];
    [SerializeField] private FloatBob bob;                       // velocity source (home glide)
    [SerializeField] private NPCNarrationController narration;   // talk-time motion
    [SerializeField] private float maxLeanDeg = 14f;
    [SerializeField] private float leanPerMps = 22f;             // degrees per m/s
    [SerializeField] private float leanSharpness = 5f;
    [SerializeField] private float wavePulse = 0.12f;
    [SerializeField] private float waveSpeedMultiplier = 30f;    // user 2026-07-10: waves 30x faster

    private Quaternion _baseLocal;
    private Vector3[] _waveBase = new Vector3[0];
    private Vector3 _prevHome;
    private bool _hasPrev, _talking, _cached;
    private float _lean;
    private Vector3 _leanAxis = Vector3.forward;

    public void Bind(Transform body, Transform[] waveRings, FloatBob b, NPCNarrationController n)
    { bodyRoot = body; waves = waveRings ?? new Transform[0]; bob = b; narration = n; _cached = false; }

    /// Pure lean curve — self-tests pin it.
    public static float LeanFor(float speedMps, float degPerMps, float maxDeg)
        => Mathf.Min(Mathf.Max(0f, speedMps) * degPerMps, maxDeg);

    void OnEnable()
    {
        if (narration != null)
        {
            narration.LineStarted += OnLineStarted;
            narration.LineEnded += OnLineEnded;
        }
    }

    void OnDisable()
    {
        if (narration != null)
        {
            narration.LineStarted -= OnLineStarted;
            narration.LineEnded -= OnLineEnded;
        }
    }

    void OnLineStarted(string line, float seconds) => _talking = true;
    void OnLineEnded() => _talking = false;

    void Cache()
    {
        if (_cached || bodyRoot == null) return;
        _baseLocal = bodyRoot.localRotation;
        _waveBase = new Vector3[waves.Length];
        for (int i = 0; i < waves.Length; i++)
            if (waves[i] != null) _waveBase[i] = waves[i].localScale;
        _cached = true;
    }

    void LateUpdate()
    {
        Cache();
        if (bodyRoot == null) return;
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        // Velocity from the glide home (bob/jitter noise excluded).
        Vector3 vel = Vector3.zero;
        if (bob != null)
        {
            Vector3 home = bob.Home;
            if (_hasPrev) vel = (home - _prevHome) / dt;
            _prevHome = home; _hasPrev = true;
        }
        vel.y = 0f;

        float targetLean = LeanFor(vel.magnitude, leanPerMps, maxLeanDeg);
        if (vel.sqrMagnitude > 1e-6f)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, vel.normalized);   // tips the top INTO the motion
            _leanAxis = bodyRoot.parent != null ? bodyRoot.parent.InverseTransformDirection(axis) : axis;
        }
        _lean = Mathf.Lerp(_lean, targetLean, leanSharpness * dt);

        Quaternion talk = Quaternion.identity;
        if (_talking)
        {
            float t = Time.time;
            talk = Quaternion.Euler(Mathf.Sin(t * 3.1f) * 2.5f, 0f, Mathf.Sin(t * 2.3f) * 1.8f);
        }
        bodyRoot.localRotation = Quaternion.AngleAxis(_lean, _leanAxis) * talk * _baseLocal;

        // Hover waves: staggered breathing pulse, faster while moving.
        float rate = (2.2f + Mathf.Min(vel.magnitude * 2f, 2f)) * Mathf.Max(0.01f, waveSpeedMultiplier);
        for (int i = 0; i < waves.Length && i < _waveBase.Length; i++)
        {
            if (waves[i] == null) continue;
            float s = 1f + wavePulse * Mathf.Sin(Time.time * rate + i * 0.9f);
            waves[i].localScale = _waveBase[i] * s;
        }
    }
}
