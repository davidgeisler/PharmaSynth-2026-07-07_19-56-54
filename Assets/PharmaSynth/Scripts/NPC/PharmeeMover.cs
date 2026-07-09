using System.Collections.Generic;
using UnityEngine;

/// Pure hover-follow math for Pharmee's in-lab movement: pick the anchor nearest
/// the player (with hysteresis so he doesn't ping-pong), glide toward it at a
/// clamped speed, and never crowd the player. Edit-mode testable.
public static class PharmeeMoveSolver
{
    /// Nearest anchor that keeps minPlayerDist from the player; the current anchor
    /// is sticky unless another is `hysteresis` metres closer to the player.
    public static int PickAnchor(Vector3 playerPos, IReadOnlyList<Vector3> anchors,
                                 float minPlayerDist, int currentIndex, float hysteresis)
    {
        if (anchors == null || anchors.Count == 0) return -1;
        int best = -1; float bestD = float.MaxValue;
        for (int i = 0; i < anchors.Count; i++)
        {
            float d = Flat(anchors[i] - playerPos).magnitude;
            if (d < minPlayerDist) continue;              // too close — he'd crowd the player
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best < 0) return currentIndex;                // everywhere is crowded: stay put
        if (currentIndex >= 0 && currentIndex < anchors.Count)
        {
            float cur = Flat(anchors[currentIndex] - playerPos).magnitude;
            if (cur >= minPlayerDist && cur - bestD < hysteresis) return currentIndex;   // sticky
        }
        return best;
    }

    /// One glide step, clamped to maxSpeed·dt; lands exactly on the target.
    public static Vector3 Step(Vector3 current, Vector3 target, float maxSpeed, float dt)
        => Vector3.MoveTowards(current, target, Mathf.Max(0f, maxSpeed) * Mathf.Max(0f, dt));

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}

/// Runtime driver: while an experiment runs, Pharmee glides between hover anchors
/// to stay near (but not on top of) the player — "he follows and watches me work".
/// When idle he returns to his door-home. Drives FloatBob's home so bob + jitter
/// keep composing on top of the motion.
public class PharmeeMover : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private FloatBob bob;                 // whose home we glide
    [SerializeField] private Transform player;             // XR camera (falls back to Camera.main)
    [SerializeField] private Transform[] anchors = new Transform[0];
    [SerializeField] private float minPlayerDist = 1.2f;
    [SerializeField] private float hysteresis = 0.75f;
    [SerializeField] private float maxSpeed = 0.8f;
    [SerializeField] private float hoverY = 1.05f;

    private Vector3 _homeWorld;      // door-home to return to when idle
    private int _anchorIndex = -1;
    private bool _homeCached;

    public void Bind(ExperimentRunner r, FloatBob b, Transform p, Transform[] a)
    { runner = r; bob = b; player = p; anchors = a ?? new Transform[0]; }

    private void Start()
    {
        CacheHome();
    }

    private void CacheHome()
    {
        if (_homeCached) return;
        _homeWorld = transform.position;
        _homeCached = true;
    }

    private void Update() => TickSolve(Time.deltaTime);

    /// Update body — public so tests can drive it headless with explicit dt.
    public void TickSolve(float dt)
    {
        CacheHome();
        if (bob == null) return;

        Vector3 targetWorld;
        bool running = runner != null && runner.IsRunning;
        if (running)
        {
            var p = player != null ? player : (Camera.main != null ? Camera.main.transform : null);
            if (p == null) return;
            var pts = new List<Vector3>(anchors.Length);
            foreach (var a in anchors) if (a != null) pts.Add(a.position);
            _anchorIndex = PharmeeMoveSolver.PickAnchor(p.position, pts, minPlayerDist, _anchorIndex, hysteresis);
            if (_anchorIndex < 0 || _anchorIndex >= pts.Count) return;
            targetWorld = pts[_anchorIndex];
        }
        else
        {
            _anchorIndex = -1;
            targetWorld = _homeWorld;   // gate duty
        }
        targetWorld.y = hoverY;

        // Glide FloatBob's HOME (local space) so bob/jitter stay layered on top.
        Transform parent = transform.parent;
        Vector3 targetLocal = parent != null ? parent.InverseTransformPoint(targetWorld) : targetWorld;
        bob.SetHome(PharmeeMoveSolver.Step(bob.Home, targetLocal, maxSpeed, dt));
    }
}
