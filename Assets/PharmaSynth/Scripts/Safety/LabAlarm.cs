using UnityEngine;

/// The lab's hazard alarm (manuscript intro: errors trigger a "visual and
/// auditory alert, such as flashing lights, warning messages, or alarm beeps").
/// One red ceiling fixture + one dynamic light (Quest-cheap), flashing for a few
/// seconds with the alarm SFX whenever a dangerous mix/overheat fires. Built by
/// Tools ▸ PharmaSynth ▸ Build Lab Alarm; re-triggers extend the window.
public class LabAlarm : MonoBehaviour
{
    public static LabAlarm Instance { get; private set; }

    [SerializeField] private Light alarmLight;
    [SerializeField] private Renderer fixtureRenderer;   // emissive shell (optional)
    [SerializeField] private float flashSeconds = 3f;
    [SerializeField] private float flashPeriod = 0.5f;   // full on/off cycle
    [SerializeField] private float lightIntensity = 2.4f;

    private float _until;
    private MaterialPropertyBlock _mpb;

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Bind(Light l, Renderer fixture) { alarmLight = l; fixtureRenderer = fixture; }

    /// Static convenience — safe when no alarm exists in the scene.
    public static void Trigger()
    {
        if (Instance != null && Application.isPlaying) Instance.TriggerNow();
    }

    public void TriggerNow()
    {
        bool fresh = Time.time >= _until;
        _until = Time.time + flashSeconds;
        if (fresh) AudioService.TryPlay("alarm");
    }

    /// Pure flash phase: on during the first half of each period. Testable.
    public static bool FlashOn(float sinceStart, float period)
    {
        if (period <= 0f) return false;
        float k = Mathf.Repeat(sinceStart, period) / period;
        return k < 0.5f;
    }

    private void Update()
    {
        bool active = Time.time < _until;
        bool on = active && FlashOn(_until - Time.time, flashPeriod);
        if (alarmLight != null)
        {
            alarmLight.enabled = on;
            alarmLight.intensity = lightIntensity;
        }
        if (fixtureRenderer != null)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mpb.SetColor("_EmissionColor", on ? new Color(1.6f, 0.1f, 0.08f) : Color.black);
            _mpb.SetColor("_BaseColor", on ? new Color(1f, 0.25f, 0.2f) : new Color(0.35f, 0.08f, 0.08f));
            fixtureRenderer.SetPropertyBlock(_mpb);
        }
    }
}
