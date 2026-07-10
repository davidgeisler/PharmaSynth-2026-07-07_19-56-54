using UnityEngine;

/// A grabbable matchstick (manuscript: combustion/flammability tests, Exp 3/4/7
/// + the methane splint). Hold its head near anything HOT — an active Heat
/// station above ignition temperature — and it lights: a small looping flame +
/// FlamePop, burning out after a while. Pure ignition predicate for tests.
public class Matchstick : MonoBehaviour
{
    public const float IgniteDistance = 0.22f;
    public const float IgniteTempC = 80f;
    [SerializeField] private float burnSeconds = 25f;

    private bool _lit, _spent;
    private float _litAt;
    private GameObject _flame;

    public bool IsLit => _lit;
    public bool IsSpent => _spent;

    /// Pure: does a heat source at this distance/temperature ignite the match?
    public static bool ShouldIgnite(float distance, float tempC, bool alreadyLit, bool spent)
        => !alreadyLit && !spent && distance <= IgniteDistance && tempC >= IgniteTempC;

    private void Update()
    {
        if (_lit && Time.time - _litAt > burnSeconds) Extinguish(true);
        if (_lit || _spent || !Application.isPlaying) return;

        foreach (var sim in FindObjectsByType<TemperatureSim>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, sim.transform.position);
            if (ShouldIgnite(d, sim.AtLeast(IgniteTempC) ? IgniteTempC : 0f, _lit, _spent))
            {
                Ignite();
                break;
            }
        }
    }

    public void Ignite()
    {
        if (_lit || _spent) return;
        _lit = true;
        _litAt = Time.time;
        EffectVfx.FlamePop(transform.position);
        AudioService.TryPlayAt("burner-ignite", transform.position);
        if (Application.isPlaying)
        {
            _flame = new GameObject("MatchFlame");
            _flame.transform.SetParent(transform, false);
            _flame.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var light = _flame.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.25f);
            light.range = 0.6f;
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
        }
    }

    public void Extinguish(bool spent)
    {
        _lit = false;
        if (spent) _spent = true;
        if (_flame != null) Destroy(_flame);
    }
}
