using UnityEngine;

/// One-shot procedural particle bursts (VFX-set completion, user 2026-07-10),
/// the event twin of StationVfx's looping station effects. All built at runtime
/// from a shared soft-dot material (no asset deps, Quest-cheap, auto-destroy):
///   • Shatter  — glass breaks into a quick outward spray of pale shards (fired
///                from BreakableGlassware.Break, pairs with the glass-break SFX).
///   • Confetti — a colourful upward burst on a passing grade (GradeScreen).
///   • FlamePop — a brief orange flame puff (burner ignite / methane splint test).
/// Each spawner returns immediately; the emitter fades and self-destroys.
public static class EffectVfx
{
    private static Texture2D _dot;
    private static Material _mat;

    public enum Kind { Shatter, Confetti, FlamePop }

    /// Glass-shatter burst: pale shards spray out and fall.
    public static void Shatter(Vector3 pos, Color tint) => Play(Kind.Shatter, pos, tint);
    public static void Shatter(Vector3 pos) => Play(Kind.Shatter, pos, new Color(0.85f, 0.92f, 0.98f, 0.9f));

    /// Grade-pass confetti: multicoloured upward burst.
    public static void Confetti(Vector3 pos) => Play(Kind.Confetti, pos, Color.white);

    /// Flame puff: burner ignite / splint flame test.
    public static void FlamePop(Vector3 pos) => Play(Kind.FlamePop, pos, new Color(1f, 0.55f, 0.15f, 0.9f));

    public static void Play(Kind kind, Vector3 pos, Color tint)
    {
        if (!Application.isPlaying) return;               // one-shots are a runtime effect
        var go = new GameObject("EffectVfx_" + kind);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        var col = ps.colorOverLifetime; col.enabled = true;
        float life;
        int burst;

        switch (kind)
        {
            case Kind.Shatter:
                life = 0.7f; burst = 22;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
                main.startColor = tint;
                main.gravityModifier = 1.4f;
                shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.04f;
                col.color = Fade(tint, tint.a);
                break;

            case Kind.Confetti:
                life = 2.2f; burst = 60;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
                main.startColor = ConfettiPalette();
                main.gravityModifier = 0.9f;
                shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 32f; shape.radius = 0.05f;
                go.transform.localRotation = Quaternion.identity;   // fire upward
                col.color = Fade(Color.white, 1f);
                break;

            default: // FlamePop
                life = 0.6f; burst = 18;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                main.startColor = tint;
                shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 14f; shape.radius = 0.03f;
                col.color = FlameGradient();
                var siz = ps.sizeOverLifetime; siz.enabled = true;
                siz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
                break;
        }

        var em = new ParticleSystem.Burst(0f, (short)burst);
        emission.SetBursts(new[] { em });

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = SharedMaterial();
        r.sortingOrder = 12;

        ps.Play();
        Object.Destroy(go, life + 0.5f);
    }

    private static Color ConfettiPalette()
    {
        // Deterministic-ish bright hue set; ParticleSystem randomises between two
        // colours if we set a gradient, but a single bright start is Quest-cheap.
        Color[] c = { new Color(1f, 0.35f, 0.4f), new Color(0.35f, 0.8f, 1f),
                      new Color(1f, 0.85f, 0.3f), new Color(0.5f, 1f, 0.55f),
                      new Color(0.8f, 0.5f, 1f) };
        return c[Mathf.Abs(Time.frameCount) % c.Length];
    }

    private static Gradient Fade(Color c, float peak)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(peak, 0f), new GradientAlphaKey(peak, 0.6f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static Gradient FlameGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.5f),
                    new GradientColorKey(new Color(0.6f, 0.15f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static Material SharedMaterial()
    {
        if (_mat != null) return _mat;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        _mat = new Material(sh);
        _mat.SetTexture("_BaseMap", SoftDot());
        _mat.SetFloat("_Surface", 1f);
        _mat.SetFloat("_Blend", 0f);
        _mat.renderQueue = 3000;
        return _mat;
    }

    private static Texture2D SoftDot()
    {
        if (_dot != null) return _dot;
        const int n = 64;
        _dot = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                _dot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        _dot.Apply();
        return _dot;
    }
}
