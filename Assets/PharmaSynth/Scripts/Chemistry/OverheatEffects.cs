using UnityEngine;

/// Overheat consequence at a Heat station (user 2026-07-10 error-effects pass):
/// when the station's TemperatureSim crosses its overheat threshold, the vessel
/// on the pad starts SMOKING, its contents turn into a ruined dark mixture, the
/// alarm fires, and an Overheat mistake is recorded. Attached per Heat station
/// by ExperimentSceneBuilder.
public class OverheatEffects : MonoBehaviour
{
    private TemperatureSim _sim;
    private ExperimentRunner _runner;
    private ChemicalData _ruined;

    public void Bind(TemperatureSim sim, ExperimentRunner runner, ChemicalData ruined)
    {
        if (_sim != null) _sim.Overheated -= OnOverheated;
        _sim = sim; _runner = runner; _ruined = ruined;
        if (_sim != null) _sim.Overheated += OnOverheated;
    }

    private void OnDestroy()
    {
        if (_sim != null) _sim.Overheated -= OnOverheated;
    }

    private void OnOverheated()
    {
        Vector3 pos = transform.position + Vector3.up * 0.15f;
        EffectVfx.Smoke(pos);
        AudioService.TryPlayAt("gas-hiss", pos);
        LabAlarm.Trigger();
        RuinNearestVessel();
        if (_runner != null)
            _runner.RecordMistake(LabErrorType.Overheat, "Overheated — the batch is ruined. Ease off the heat next time.");
    }

    /// The vessel occupying this station (nearest LiquidPhysics within reach)
    /// turns into the ruined mixture — visible, irreversible feedback.
    private void RuinNearestVessel()
    {
        if (_ruined == null || !Application.isPlaying) return;
        LiquidPhysics best = null;
        float bestSq = 0.45f * 0.45f;
        foreach (var lp in FindObjectsByType<LiquidPhysics>(FindObjectsSortMode.None))
        {
            float sq = (lp.transform.position - transform.position).sqrMagnitude;
            if (sq < bestSq && lp.currentLiquidVolume > 0.5f) { bestSq = sq; best = lp; }
        }
        if (best != null)
        {
            best.currentChemical = _ruined;
            best.UpdateAllVisuals();
        }
    }
}
