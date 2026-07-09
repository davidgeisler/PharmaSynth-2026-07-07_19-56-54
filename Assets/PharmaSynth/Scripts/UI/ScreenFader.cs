using System;
using UnityEngine;

/// Pure fade math: eased alpha ramp between targets. Edit-mode testable.
public class FadeState
{
    private float _from, _to, _duration, _t;

    public float Alpha { get; private set; }
    public bool Busy { get; private set; }

    public void Begin(float toAlpha, float seconds)
    {
        _from = Alpha;
        _to = Mathf.Clamp01(toAlpha);
        _duration = Mathf.Max(0.01f, seconds);
        _t = 0f;
        Busy = true;
    }

    /// Advance by dt; returns the current alpha. Clears Busy on arrival.
    public float Step(float dt)
    {
        if (!Busy) return Alpha;
        _t += Mathf.Max(0f, dt);
        float k = Mathf.Clamp01(_t / _duration);
        Alpha = Mathf.Lerp(_from, _to, Ease01(k));
        if (k >= 1f) { Alpha = _to; Busy = false; }
        return Alpha;
    }

    /// Smoothstep 3t²−2t³.
    public static float Ease01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}

/// VR-safe screen fade: a black quad floating just in front of the camera (a
/// screen-space canvas is unreliable in the XR compositor). One instance per
/// scene, parented under the active camera. Used for teleports + scene loads.
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [SerializeField] private Renderer quad;              // auto-built under Camera.main if null
    [SerializeField] private bool fadeInOnStart = true;
    [SerializeField] private float defaultSeconds = 0.4f;

    private readonly FadeState _state = new FadeState();
    private Action _onDone;
    private Material _mat;

    public FadeState State => _state;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        EnsureQuad();
        if (fadeInOnStart)
        {
            SetAlpha(1f);
            FadeIn();
        }
        else SetAlpha(0f);
    }

    private void EnsureQuad()
    {
        if (quad != null) { _mat = quad.material; return; }
        var cam = Camera.main;
        if (cam == null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "FadeQuad";
        UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(2f, 2f, 1f);
        quad = go.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        _mat = new Material(shader);
        _mat.SetFloat("_Surface", 1f);                    // transparent
        _mat.SetFloat("_Blend", 0f);                      // alpha blend
        _mat.SetOverrideTag("RenderType", "Transparent");
        _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_ZWrite", 0);
        _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _mat.renderQueue = 4600;                          // over everything, incl. overlays
        _mat.color = new Color(0f, 0f, 0f, 0f);
        quad.sharedMaterial = _mat;
        quad.enabled = false;
    }

    private void SetAlpha(float a)
    {
        // Drive the pure state to the value so subsequent fades start from it.
        _state.Begin(a, 0.01f);
        _state.Step(1f);
        Apply(a);
    }

    private void Apply(float a)
    {
        if (quad == null || _mat == null) return;
        var c = _mat.color; c.a = a; _mat.color = c;
        quad.enabled = a > 0.001f;
    }

    private void Update()
    {
        if (!_state.Busy) return;
        Apply(_state.Step(Time.deltaTime));
        if (!_state.Busy && _onDone != null)
        {
            var cb = _onDone; _onDone = null;
            cb();
        }
    }

    public void FadeOut(float seconds = -1f, Action done = null)
    {
        EnsureQuad();
        _onDone = done;
        _state.Begin(1f, seconds > 0f ? seconds : defaultSeconds);
        if (quad != null) quad.enabled = true;
    }

    public void FadeIn(float seconds = -1f, Action done = null)
    {
        EnsureQuad();
        _onDone = done;
        _state.Begin(0f, seconds > 0f ? seconds : defaultSeconds);
    }

    /// Fade to black, run the action (teleport / rebuild), fade back in.
    public void FadeAround(Action mid, float outSeconds = -1f, float inSeconds = -1f)
    {
        FadeOut(outSeconds, () =>
        {
            try { mid?.Invoke(); }
            finally { FadeIn(inSeconds); }
        });
    }

    /// Null-safe helper: with no fader in the scene (menus, tests) the action
    /// runs immediately — callers never need to care.
    public static void FadeOutThen(Action act, float seconds = 0.35f)
    {
        if (Instance != null && Instance.isActiveAndEnabled) Instance.FadeOut(seconds, act);
        else act?.Invoke();
    }
}
