using UnityEngine;

/// Scene driver for HazardousMix: subscribes to a vessel's WrongReagentMixed
/// (the previously-silent no-rule mix) and stages the consequence — outcome VFX
/// at the vessel, positional SFX, the lab alarm + warning vignette for the
/// dangerous ones, and the graded mistake. One reaction per vessel per 2 s so a
/// continuous pour doesn't spam penalties. Attached by ExperimentSceneBuilder
/// (vessels + pourables) and ShelfPourWiring (shelf/cabinet bottles).
public class HazardousMixReactor : MonoBehaviour
{
    public const float CooldownSeconds = 2f;

    private LiquidPhysics _liquid;
    private ExperimentRunner _runner;
    private float _nextOk;

    private void Awake()
    {
        if (_liquid == null)
            Bind(GetComponent<LiquidPhysics>(), FindAnyObjectByType<ExperimentRunner>());
    }

    /// Edit-mode / builder seam.
    public void Bind(LiquidPhysics liquid, ExperimentRunner runner)
    {
        if (_liquid != null) _liquid.WrongReagentMixed -= OnWrongMix;
        _liquid = liquid; _runner = runner;
        if (_liquid != null) _liquid.WrongReagentMixed += OnWrongMix;
    }

    private void OnDestroy()
    {
        if (_liquid != null) _liquid.WrongReagentMixed -= OnWrongMix;
    }

    private void OnWrongMix(ChemicalData current, ChemicalData incoming)
    {
        if (Time.time < _nextOk) return;
        _nextOk = Time.time + CooldownSeconds;

        var outcome = HazardousMix.Classify(current, incoming);
        if (outcome == HazardousMix.HazardOutcome.None) return;

        Vector3 pos = transform.position + Vector3.up * 0.08f;
        Color tint = HazardousMix.TintFor(outcome, current);
        switch (outcome)
        {
            case HazardousMix.HazardOutcome.ToxicGas:
                EffectVfx.Smoke(pos, tint);
                AudioService.TryPlayAt("gas-hiss", pos);
                LabAlarm.Trigger();
                break;
            case HazardousMix.HazardOutcome.FireOrExplosion:
                EffectVfx.FireBurst(pos);
                EffectVfx.Smoke(pos + Vector3.up * 0.12f);
                AudioService.TryPlayAt("burner-ignite", pos);
                LabAlarm.Trigger();
                if (ScreenFader.Instance != null)
                    ScreenFader.Instance.PulseWarning(new Color(1f, 0.45f, 0.1f));
                break;
            case HazardousMix.HazardOutcome.AcidSpatter:
                EffectVfx.Spatter(pos, tint);
                AudioService.TryPlayAt("reaction-fizz", pos);
                LabAlarm.Trigger();
                if (ScreenFader.Instance != null)
                    ScreenFader.Instance.PulseWarning(new Color(1f, 0.2f, 0.15f));
                break;
            default:
                EffectVfx.ColorFlash(pos, tint);
                AudioService.TryPlayAt("reaction-fizz", pos);
                break;
        }

        if (_runner != null)
            _runner.RecordMistake(HazardousMix.ErrorTypeFor(outcome), HazardousMix.WarnLineFor(outcome));
    }
}
