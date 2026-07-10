using UnityEngine;

/// Pure litmus colour response (manuscript: pH checks in Exp 3, 4, 8).
public static class LitmusMath
{
    public static readonly Color AcidRed = new Color(0.85f, 0.2f, 0.18f);
    public static readonly Color NeutralViolet = new Color(0.62f, 0.45f, 0.65f);
    public static readonly Color BaseBlue = new Color(0.2f, 0.35f, 0.85f);

    /// Red below ~4.5, blue above ~8.3, graded violet between.
    public static Color ColorForPH(float pH)
    {
        if (pH <= 4.5f) return AcidRed;
        if (pH >= 8.3f) return BaseBlue;
        float t = Mathf.InverseLerp(4.5f, 8.3f, pH);
        return t < 0.5f
            ? Color.Lerp(AcidRed, NeutralViolet, t * 2f)
            : Color.Lerp(NeutralViolet, BaseBlue, (t - 0.5f) * 2f);
    }
}

/// A grabbable litmus strip: touch it to any liquid (trigger or collision with a
/// vessel holding a chemical) and it tints to the liquid's pH — one-shot, like
/// the real thing. Built by the cabinet builder's consumables box.
public class LitmusStrip : MonoBehaviour
{
    [SerializeField] private Renderer strip;
    private bool _used;
    private MaterialPropertyBlock _mpb;

    public bool Used => _used;

    public void Bind(Renderer r) => strip = r;

    private void OnTriggerEnter(Collider other) => TryRead(other);
    private void OnCollisionEnter(Collision c) => TryRead(c.collider);

    private void TryRead(Collider other)
    {
        if (_used) return;
        var lp = other.GetComponentInParent<LiquidPhysics>();
        if (lp == null || lp.currentChemical == null || lp.currentLiquidVolume <= 0.5f) return;
        Apply(lp.currentChemical.pH);
    }

    /// Public + pure-drivable for tests and the methane-style rigs.
    public void Apply(float pH)
    {
        _used = true;
        if (strip == null) strip = GetComponentInChildren<Renderer>();
        if (strip == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Color c = LitmusMath.ColorForPH(pH);
        _mpb.SetColor("_BaseColor", c);
        _mpb.SetColor("_Color", c);
        strip.SetPropertyBlock(_mpb);
    }
}
