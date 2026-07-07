using System;
using UnityEngine;
using UnityEngine.Events;

/// Exact exponential heat model: temperature approaches the heat source (when
/// heating) or ambient (when cooling). Stable for any timestep, so it is
/// deterministically unit-testable. Powers distillation cut-offs (56 °C acetone,
/// 70-80 °C ethanol) and the aspirin overheat branch.
public class HeatModel
{
    public float Ambient;
    public float HeatRate = 0.35f;   // approach rate toward the source (per second)
    public float CoolRate = 0.15f;   // approach rate toward ambient

    public float Current { get; private set; }
    private bool _heating;
    private float _sourceTemp;

    public HeatModel(float ambient = 22f)
    {
        Ambient = ambient;
        Current = ambient;
    }

    public void SetHeating(bool on, float sourceTemp)
    {
        _heating = on;
        _sourceTemp = sourceTemp;
    }

    public void Step(float dt)
    {
        float target = _heating ? _sourceTemp : Ambient;
        float rate = _heating ? HeatRate : CoolRate;
        // Exact solution of dT/dt = -rate*(T - target): T = target + (T-target)*e^(-rate*dt)
        Current = target + (Current - target) * Mathf.Exp(-rate * Mathf.Max(0f, dt));
    }
}

/// Per-vessel temperature with target-reached and overheat threshold events, plus
/// a condition predicate for TaskGraph auto-check. Thin wrapper over HeatModel.
public class TemperatureSim : MonoBehaviour
{
    [SerializeField] private float ambientC = 22f;
    [SerializeField] private float heatRate = 0.35f;
    [SerializeField] private float coolRate = 0.15f;

    [Header("Thresholds")]
    [SerializeField] private float targetC = 56f;    // e.g. acetone distillate
    [SerializeField] private float overheatC = 120f; // e.g. aspirin decomposition

    [Header("Events")]
    public UnityEvent onReachedTarget;
    public UnityEvent onOverheated;

    public event Action ReachedTarget;
    public event Action Overheated;

    private HeatModel _model;
    private bool _targetFired;
    private bool _overheatFired;

    public float CurrentC => Model.Current;
    public bool IsOverheated => _overheatFired;

    private HeatModel Model
    {
        get
        {
            if (_model == null)
            {
                _model = new HeatModel(ambientC) { HeatRate = heatRate, CoolRate = coolRate };
            }
            return _model;
        }
    }

    public void SetHeating(bool on, float sourceTemp) => Model.SetHeating(on, sourceTemp);

    private void Update() => Tick(Time.deltaTime);

    /// Advance the sim (public for deterministic tests).
    public void Tick(float dt)
    {
        Model.Step(dt);
        if (!_targetFired && Model.Current >= targetC)
        {
            _targetFired = true;
            ReachedTarget?.Invoke();
            onReachedTarget?.Invoke();
        }
        if (!_overheatFired && Model.Current >= overheatC)
        {
            _overheatFired = true;
            Overheated?.Invoke();
            onOverheated?.Invoke();
        }
    }

    /// TaskGraph auto-check predicate: has the vessel reached at least this temperature?
    public bool AtLeast(float temperatureC) => Model.Current >= temperatureC;

    public void ResetSim()
    {
        _model = null;
        _targetFired = false;
        _overheatFired = false;
    }
}
