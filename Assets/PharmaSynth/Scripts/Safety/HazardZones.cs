using UnityEngine;

/// A fume-hood working volume. Tracks whether the player's hand / active work is
/// inside it, so toxic/volatile reagents can require the hood (plan §3.7).
public class FumeHoodZone : MonoBehaviour
{
    [SerializeField] private string occupantTag = "";   // e.g. player hand collider tag
    private int _occupants;

    public bool IsOccupied => _occupants > 0;

    /// Position-based test (used by reagent validation without needing physics).
    public bool Contains(Vector3 worldPos)
    {
        var col = GetComponent<Collider>();
        return col != null && col.bounds.Contains(worldPos);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (string.IsNullOrEmpty(occupantTag) || other.CompareTag(occupantTag)) _occupants++;
    }
    private void OnTriggerExit(Collider other)
    {
        if (string.IsNullOrEmpty(occupantTag) || other.CompareTag(occupantTag)) _occupants = Mathf.Max(0, _occupants - 1);
    }
}

/// A hazard volume (spill, hot surface, corrosive) — contact reports a mistake to
/// the runner and can trigger a visual/audio warning. Debounced so a dwell reports once.
public class HazardZone : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private LabErrorType errorType = LabErrorType.ChemicalContact;
    [SerializeField] private string message = "Chemical contact!";
    [SerializeField] private string contactTag = "";   // player/hand
    [SerializeField] private float rearmSeconds = 2f;

    private float _lastReport = -999f;

    public void SetRunner(ExperimentRunner r) => runner = r;

    private void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(contactTag) && !other.CompareTag(contactTag)) return;
        Report();
    }

    /// Public so it is directly testable / callable by non-physics detectors.
    public void Report()
    {
        if (runner == null) return;
        if (Time.time - _lastReport < rearmSeconds) return;
        _lastReport = Time.time;
        runner.RecordMistake(errorType, message);
    }
}
