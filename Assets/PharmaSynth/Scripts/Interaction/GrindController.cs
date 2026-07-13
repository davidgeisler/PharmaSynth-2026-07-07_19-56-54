using UnityEngine;

/// The GRIND verb (W5.8): work the pestle in circles inside this mortar's bowl
/// and the grind task completes (dual-path on Methane: the legacy zone-touch
/// still works). When done, a powder heap appears in the bowl. A null/empty
/// taskId makes it purely educational — staged mortars still grind visually.
public class GrindController : MonoBehaviour
{
    private readonly OrbitMath _math = new OrbitMath();
    private ExperimentRunner _runner;
    private string _taskId;
    private Transform _pestle;
    private float _bowlRadius = 0.09f;
    private float _bowlBandY = 0.16f;
    private bool _subscribed;
    private bool _doneAnnounced;
    private int _lastShownPct = -1;
    private float _nextPopupAt;
    private GameObject _heap;

    public OrbitMath Math => _math;

    /// Builder seam. taskId may be null/empty (cosmetic grind).
    public void Bind(ExperimentRunner runner, string taskId, Transform pestle,
                     float bowlRadius = 0.09f, float bowlBandY = 0.16f, float requiredRevs = 3f)
    {
        _runner = runner; _taskId = taskId; _pestle = pestle;
        _bowlRadius = bowlRadius; _bowlBandY = bowlBandY;
        _math.requiredRevs = requiredRevs;
        Resubscribe();
        Register();
    }

    public void SetPestle(Transform pestle) => _pestle = pestle;

    /// Re-point which task this grind completes (W5.12: the Methane rig aims the
    /// shared workspace mortar at "prepare-mixture" while its tutorial runs, then
    /// clears it back to cosmetic afterwards). Re-registers the condition.
    public void SetTaskId(string taskId) { _taskId = taskId; Register(); }
    public string TaskId => _taskId;

    // W5.9: also re-Register on enable (see StirController note).
    private void OnEnable() { Resubscribe(); Register(); }
    private void OnDisable() { if (_runner != null) _runner.ExperimentStarted -= OnStarted; _subscribed = false; }

    private void Resubscribe()
    {
        if (_runner == null || _subscribed) return;
        _runner.ExperimentStarted += OnStarted;
        _subscribed = true;
    }

    private void OnStarted(ExperimentModuleDefinition _) => Register();

    public void Register()
    {
        _math.Reset();
        _doneAnnounced = false;
        _lastShownPct = -1;
        if (_heap != null) { Destroy(_heap); _heap = null; }
        if (_runner == null || _runner.Graph == null || string.IsNullOrEmpty(_taskId)) return;
        _runner.Graph.RegisterCondition(_taskId, () => _math.IsDone);
    }

    /// True while the pestle tip works inside the bowl (mortar-relative).
    public bool PestleInBowl()
    {
        if (_pestle == null) return false;
        Vector3 d = _pestle.position - transform.position;
        float horiz = new Vector2(d.x, d.z).magnitude;
        return horiz <= _bowlRadius && d.y > -0.02f && d.y <= _bowlBandY;
    }

    private void Update()
    {
        if (_pestle == null) return;
        if (_runner != null && !string.IsNullOrEmpty(_taskId) && !_runner.IsRunning) return;
        Vector3 d = _pestle.position - transform.position;
        Tick(d.x, d.z, PestleInBowl());
    }

    /// One grind sample (public so tests can drive it without physics).
    public void Tick(float x, float z, bool inside)
    {
        bool wasDone = _math.IsDone;
        _math.Feed(x, z, inside);

        if (!inside) return;
        int pct = Mathf.RoundToInt(_math.Progress01 * 100f);
        if (!_math.IsDone && pct != _lastShownPct && pct % 25 == 0 && pct > 0 && Time.time >= _nextPopupAt)
        {
            _lastShownPct = pct;
            _nextPopupAt = Time.time + 0.8f;
            FloatingText.Show("Grinding... " + pct + "%", transform.position + Vector3.up * 0.2f, new Color(1f, 0.92f, 0.7f), 0.8f);
            AudioService.TryPlayAt("stir", transform.position);
        }
        if (_math.IsDone && !wasDone && !_doneAnnounced)
        {
            _doneAnnounced = true;
            FloatingText.Show("Ground to a fine powder!", transform.position + Vector3.up * 0.24f, new Color(0.6f, 1f, 0.7f));
            AudioService.TryPlayAt("mixture-complete", transform.position);
            BuildHeap();
        }
    }

    /// Small tan powder heap resting in the bowl (runtime cosmetic).
    private void BuildHeap()
    {
        if (!Application.isPlaying || _heap != null) return;
        _heap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _heap.name = "PowderHeap";
        var col = _heap.GetComponent<Collider>();
        if (col != null) Destroy(col);
        _heap.transform.SetParent(transform, false);
        var rends = GetComponentsInChildren<Renderer>();
        float topY = 0.04f;
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            topY = (b.center.y - transform.position.y) + b.size.y * 0.1f;
        }
        _heap.transform.localPosition = new Vector3(0f, topY, 0f);
        var ls = transform.lossyScale;
        _heap.transform.localScale = new Vector3(0.06f / Mathf.Max(1e-4f, ls.x), 0.018f / Mathf.Max(1e-4f, ls.y), 0.06f / Mathf.Max(1e-4f, ls.z));
        var mr = _heap.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.9f, 0.86f, 0.72f);
        mr.sharedMaterial = mat;
        EffectVfx.Smoke(transform.position + Vector3.up * 0.08f, new Color(0.92f, 0.88f, 0.75f, 0.5f));
    }
}
