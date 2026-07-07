using System;
using UnityEngine;
using UnityEngine.Events;

/// Collects evolved gas over a run (e.g. CH4 over water, CO2 into a balloon or
/// limewater). Tracks a 0..1 fill fraction that drives a balloon scale / bubble
/// VFX, fires an event when full, and exposes a TaskGraph auto-check predicate.
public class GasCollection : MonoBehaviour
{
    [SerializeField, Min(0.0001f)] private float capacityMl = 100f;
    [SerializeField] private float currentMl = 0f;

    [Header("Optional visual")]
    [SerializeField] private Transform balloon;               // scales with fill
    [SerializeField] private Vector3 balloonMinScale = Vector3.one * 0.2f;
    [SerializeField] private Vector3 balloonMaxScale = Vector3.one;

    [Header("Events")]
    public UnityEvent onFull;
    public event Action Full;
    public event Action<float> FillChanged; // 0..1

    private bool _fullFired;

    public float FillFraction => Mathf.Clamp01(currentMl / capacityMl);
    public bool IsFull => _fullFired;

    /// Add evolved gas (called by a reaction/heat step). Clamped to capacity.
    public void AddGas(float ml)
    {
        if (ml <= 0f) return;
        currentMl = Mathf.Min(capacityMl, currentMl + ml);
        float f = FillFraction;
        FillChanged?.Invoke(f);
        UpdateBalloon(f);
        if (!_fullFired && currentMl >= capacityMl)
        {
            _fullFired = true;
            Full?.Invoke();
            onFull?.Invoke();
        }
    }

    /// TaskGraph auto-check predicate: has at least this fraction been collected?
    public bool Collected(float fraction) => FillFraction >= Mathf.Clamp01(fraction);

    private void UpdateBalloon(float f)
    {
        if (balloon != null)
            balloon.localScale = Vector3.Lerp(balloonMinScale, balloonMaxScale, f);
    }

    public void ResetCollection()
    {
        currentMl = 0f;
        _fullFired = false;
        UpdateBalloon(0f);
    }
}
