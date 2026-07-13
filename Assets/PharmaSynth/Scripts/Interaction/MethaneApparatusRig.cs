using UnityEngine;

/// Methane (Experiment 1) completion — LOCATION-FREE (user 2026-07-13: "we can
/// perform anywhere in the lab as long as we complete the steps"). The old rig
/// gated heat/collect on FIXED trigger zones; this instead detects the ACTIONS
/// by item proximity, so the tutorial works wherever the player does it:
///   prepare-mixture : grinding a mortar (the rig aims the workspace mortar at it)
///   setup-apparatus : the collection tube brought up to the hard-glass tube
///   heat-mixture    : a LIT burner held near the hard-glass tube (it heats)
///   collect-gas     : the hot tube + collection tube held together (gas fills)
///   test-gas        : a LIT match brought to the FILLED collection tube (pop)
/// The rig owns its own TemperatureSim + GasCollection (no station objects).
/// Items are found at runtime by LabItem.itemId, so the player can use their own
/// workspace burner/tube/mortar anywhere.
public class MethaneApparatusRig : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private TemperatureSim temperature;
    [SerializeField] private GasCollection gas;

    [Header("Tuning")]
    [SerializeField] private float burnerSourceC = 220f;
    [SerializeField] private float heatDoneC = 120f;
    [SerializeField] private float gasMlPerSecond = 22f;      // ~5 s to fill once hot
    [SerializeField, Range(0.1f, 1f)] private float collectedFraction = 0.9f;
    [SerializeField] private float heatDistance = 0.35f;     // lit burner within this of the tube heats it
    [SerializeField] private float collectDistance = 0.30f;  // collection tube within this of the tube collects
    [SerializeField] private float assembleDistance = 0.30f; // tube + collection this close = "assembled"

    private const string ModuleId = "tutorial-methane";
    private bool _active, _prevHeating, _splintFired;
    private float _collectedAt = -1f;
    public const float SplintMatchDistance = 0.25f;
    public const float SplintAutoSeconds = 20f;

    private Transform _tube, _collect;
    private GrindController _mortar;

    /// Pure (W5.8): the splint test fires when a LIT match reaches the filled
    /// tube — or automatically after a grace period so nothing ever stalls.
    public static bool SplintShouldFire(bool collected, bool alreadyFired, float matchDistance, bool matchLit, float sinceCollected)
        => collected && !alreadyFired
           && ((matchLit && matchDistance <= SplintMatchDistance) || sinceCollected >= SplintAutoSeconds);

    /// Pure proximity check (suite-pinned).
    public static bool WithinReach(float distance, float reach) => distance <= reach;

    /// Edit-mode/test binding (OnEnable doesn't fire on AddComponent in edit mode).
    /// Zones are gone — the rig only needs the runner + its own sims.
    public void Bind(ExperimentRunner r, TemperatureSim t, GasCollection g)
    {
        runner = r; temperature = t; gas = g;
        EnsureSims();
        Resubscribe();
        TryRegisterIfRunning();
    }

    private void OnEnable() { EnsureSims(); Resubscribe(); TryRegisterIfRunning(); }

    private void OnDisable()
    {
        if (runner != null) runner.ExperimentStarted -= HandleExperimentStarted;
        ReleaseMortar();
    }

    private void EnsureSims()
    {
        if (temperature == null) temperature = GetComponent<TemperatureSim>() ?? gameObject.AddComponent<TemperatureSim>();
        if (gas == null) gas = GetComponent<GasCollection>() ?? gameObject.AddComponent<GasCollection>();
    }

    private void Resubscribe()
    {
        if (runner == null) return;
        runner.ExperimentStarted -= HandleExperimentStarted;
        runner.ExperimentStarted += HandleExperimentStarted;
    }

    /// If the rig came alive AFTER methane already started (the stage is shown a
    /// frame later than ExperimentStarted), register now so nothing is missed.
    private void TryRegisterIfRunning()
    {
        if (runner != null && runner.IsRunning && runner.Module != null && runner.Module.moduleId == ModuleId)
            HandleExperimentStarted(runner.Module);
    }

    /// Registers the location-free completion conditions for a fresh attempt.
    public void HandleExperimentStarted(ExperimentModuleDefinition module)
    {
        bool wasActive = _active;
        _active = module != null && module.moduleId == ModuleId && runner != null && runner.Graph != null;
        _prevHeating = false; _splintFired = false; _collectedAt = -1f;
        if (!_active) { if (wasActive) ReleaseMortar(); return; }

        EnsureSims();
        temperature.ResetSim();
        gas.ResetCollection();
        _tube = _collect = null;

        // prepare-mixture: aim the nearest workspace mortar's grind at this step.
        AcquireMortar();

        runner.Graph.RegisterCondition("setup-apparatus", Assembled);
        runner.Graph.RegisterCondition("heat-mixture", () => temperature != null && temperature.AtLeast(heatDoneC));
        runner.Graph.RegisterCondition("collect-gas", () => gas != null && gas.Collected(collectedFraction));
        runner.Graph.RegisterCondition("test-gas", () => _splintFired);
    }

    // ---- item lookup -------------------------------------------------------

    private void FindItems()
    {
        if (_tube != null && _collect != null) return;
        foreach (var li in FindObjectsByType<LabItem>(FindObjectsSortMode.None))
        {
            if (li == null) continue;
            if (_tube == null && li.itemId == "glass-tube") _tube = li.transform;
            else if (_collect == null && li.itemId == "collection-tube") _collect = li.transform;
        }
    }

    private void AcquireMortar()
    {
        // The first mortar with a grind verb becomes the "prepare-mixture" mortar.
        foreach (var g in FindObjectsByType<GrindController>(FindObjectsSortMode.None))
        {
            if (g == null) continue;
            _mortar = g;
            _mortar.SetTaskId("prepare-mixture");
            return;
        }
    }

    private void ReleaseMortar()
    {
        if (_mortar != null) { _mortar.SetTaskId(null); _mortar = null; }
    }

    private bool Assembled()
    {
        FindItems();
        return _tube != null && _collect != null
               && WithinReach(Vector3.Distance(_tube.position, _collect.position), assembleDistance);
    }

    // ---- per-frame action detection ----------------------------------------

    private void Update()
    {
        if (!_active || runner == null || !runner.IsRunning) return;
        FindItems();
        if (_tube == null) return;

        // Heat: any LIT burner within reach of the hard-glass tube — anywhere.
        bool heating = AnyLitBurnerNear(_tube.position);
        if (temperature != null) temperature.SetHeating(heating, burnerSourceC);
        if (heating && !_prevHeating) EffectVfx.FlamePop(_tube.position + Vector3.up * 0.08f);
        _prevHeating = heating;

        // Collect: hot apparatus + collection tube held at the tube → gas fills.
        bool hot = temperature != null && temperature.AtLeast(heatDoneC);
        if (hot && gas != null && _collect != null
            && WithinReach(Vector3.Distance(_tube.position, _collect.position), collectDistance))
            gas.AddGas(gasMlPerSecond * Time.deltaTime);

        // Splint: LIT match brought to the FILLED collection tube → pop.
        bool collected = gas != null && gas.Collected(collectedFraction);
        if (collected && _collectedAt < 0f) _collectedAt = Time.time;
        if (!collected) _collectedAt = -1f;
        if (!_splintFired && collected && _collect != null)
        {
            float best = float.MaxValue; bool anyLit = false;
            foreach (var m in FindObjectsByType<Matchstick>(FindObjectsSortMode.None))
            {
                if (m == null || !m.IsLit) continue;
                anyLit = true;
                best = Mathf.Min(best, Vector3.Distance(m.transform.position, _collect.position));
            }
            if (SplintShouldFire(true, _splintFired, anyLit ? best : float.MaxValue, anyLit, Time.time - _collectedAt))
            {
                EffectVfx.FlamePop(_collect.position + Vector3.up * 0.1f);
                FloatingText.Show("Pop! Methane confirmed", _collect.position + Vector3.up * 0.2f, new Color(0.7f, 1f, 0.7f));
                _splintFired = true;
            }
        }
    }

    private bool AnyLitBurnerNear(Vector3 pos)
    {
        foreach (var b in FindObjectsByType<BurnerController>(FindObjectsSortMode.None))
            if (b != null && b.IsLit && WithinReach(Vector3.Distance(b.transform.position, pos), heatDistance))
                return true;
        return false;
    }
}
