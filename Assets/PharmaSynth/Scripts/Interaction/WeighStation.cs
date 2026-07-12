using UnityEngine;

/// A functional balance (W5.8: "the weighing scale too"): whatever rests on the
/// pan drives the live grams display (auto-tared contents, 1 g/ml proxy), and a
/// weigh-* task completes once the CORRECT load sits settled on the pan —
/// either a vessel holding enough of the required chemical (Aspirin: flask with
/// ~50 ml Salicylic Acid) or the required tool (Acetone: the acetate scoopula).
/// TaskGraph condition + ExperimentStarted resubscribe = Retry-safe (the
/// ZoneSimStation pattern). The pan is a trigger volume over the Balance model.
public class WeighStation : MonoBehaviour
{
    private ExperimentRunner _runner;
    private string _taskId;
    private string _requiredItemId;
    private string _requiredChemical;
    private float _requiredMl;
    private WeighingScaleController _scale;
    private bool _subscribed;
    private bool _doneAnnounced;

    private LabItem _occupantItem;
    private LiquidPhysics _occupantVessel;
    private int _occupants;
    private float _onPanSince = -1f;
    // Explicit vacancy flag: the old "-1 = vacant" timestamp sentinel misread a
    // legitimately NEGATIVE backdated timestamp (Time.time ≈ 0 in a fresh
    // batchmode editor) as an empty pan — the headless suite failed on it (W5.12).
    private bool _panTimed;

    public LabItem OccupantItem => _occupantItem;
    public float SecondsOnPan => _occupants > 0 && _panTimed ? Time.time - _onPanSince : 0f;

    /// Builder seam.
    public void Bind(ExperimentRunner runner, string taskId, string requiredItemId,
                     string requiredChemical, float requiredMl, WeighingScaleController scale)
    {
        _runner = runner; _taskId = taskId; _requiredItemId = requiredItemId;
        _requiredChemical = requiredChemical; _requiredMl = requiredMl; _scale = scale;
        Resubscribe();
        Register();
    }

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
        _doneAnnounced = false;
        if (_runner == null || _runner.Graph == null || string.IsNullOrEmpty(_taskId)) return;
        _runner.Graph.RegisterCondition(_taskId, () => IsSatisfied);
    }

    /// The weigh condition (public so the suite can pin it).
    public bool IsSatisfied
        => WeighMath.Satisfied(
            WeighMath.PanSettled(SecondsOnPan),
            _requiredChemical,
            _occupantVessel != null && _occupantVessel.currentChemical != null ? _occupantVessel.currentChemical.chemicalName : null,
            _occupantVessel != null ? _occupantVessel.currentLiquidVolume + _occupantVessel.currentPptVolume : 0f,
            _requiredMl,
            _requiredItemId,
            _occupantItem != null ? _occupantItem.itemId : null);

    /// Test hook — simulate a load without physics.
    public void ForceLoad(LabItem item, LiquidPhysics vessel, float secondsAgo)
    {
        _occupantItem = item; _occupantVessel = vessel;
        _occupants = (item != null || vessel != null) ? 1 : 0;
        _panTimed = _occupants > 0;
        _onPanSince = _occupants > 0 ? Time.time - secondsAgo : -1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        var item = LabItem.Resolve(other);
        var lp = other.GetComponentInParent<LiquidPhysics>();
        if (item == null && lp == null) return;
        _occupants++;
        if (item != null) _occupantItem = item;
        if (lp != null) _occupantVessel = lp;
        if (_occupants == 1) { _onPanSince = Time.time; _panTimed = true; }
    }

    private void OnTriggerExit(Collider other)
    {
        var item = LabItem.Resolve(other);
        var lp = other.GetComponentInParent<LiquidPhysics>();
        if (item == null && lp == null) return;
        _occupants = Mathf.Max(0, _occupants - 1);
        if (item != null && _occupantItem == item) _occupantItem = null;
        if (lp != null && _occupantVessel == lp) _occupantVessel = null;
        if (_occupants == 0) { _onPanSince = -1f; _panTimed = false; }
    }

    private void Update()
    {
        // Live display: auto-tared contents of whatever rests on the pan.
        if (_scale != null)
        {
            float grams = _occupantVessel != null
                ? WeighMath.MassOf(_occupantVessel.currentLiquidVolume + _occupantVessel.currentPptVolume)
                : (_occupants > 0 ? WeighMath.MassOf(0f, 5f) : 0f);   // bare tool: a token few grams
            _scale.SetTargetMass(grams);
        }

        if (!_doneAnnounced && IsSatisfied && _runner != null && _runner.IsRunning)
        {
            _doneAnnounced = true;
            FloatingText.Show("Measured!", transform.position + Vector3.up * 0.25f, new Color(0.6f, 1f, 0.7f));
            AudioService.TryPlayAt("mixture-complete", transform.position);
        }
    }
}
