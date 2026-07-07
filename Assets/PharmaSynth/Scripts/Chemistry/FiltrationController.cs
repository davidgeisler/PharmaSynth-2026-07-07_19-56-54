using System;
using UnityEngine;
using UnityEngine.Events;

/// Filtration (gravity / Büchner): filtrate accumulates as the player pours/pumps
/// the mixture through. Tracks a 0..1 fraction toward a target volume, fires
/// Filtered when complete, and exposes a TaskGraph auto-check predicate.
public class FiltrationController : MonoBehaviour
{
    [SerializeField, Min(0.0001f)] private float targetVolumeMl = 100f;
    [SerializeField] private float filteredMl = 0f;
    [SerializeField] private Transform filtrateLevel;   // optional: scales with fill

    public UnityEvent onFiltered;
    public event Action Filtered;
    public event Action<float> FilteredChanged;

    private bool _done;

    public float Fraction => Mathf.Clamp01(filteredMl / targetVolumeMl);
    public bool IsDone => _done;

    /// Pour/pump filtrate through the funnel.
    public void AddFiltrate(float ml)
    {
        if (ml <= 0f || _done) return;
        filteredMl = Mathf.Min(targetVolumeMl, filteredMl + ml);
        FilteredChanged?.Invoke(Fraction);
        if (filtrateLevel != null) filtrateLevel.localScale = new Vector3(1f, Fraction, 1f);
        if (filteredMl >= targetVolumeMl)
        {
            _done = true;
            Filtered?.Invoke();
            onFiltered?.Invoke();
        }
    }

    public bool Filtered01(float fraction) => Fraction >= Mathf.Clamp01(fraction);

    public void ResetProcess() { filteredMl = 0f; _done = false; }
}
