using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Pure rules for scooping solids (W5.12, user: "some reagents are needed to be
/// scooped… scoop adds a specific amount per scoop… the visual increases and so
/// does the scale text"). Kept plain so the suite pins the policy.
public static class ScoopMath
{
    /// Fixed charge one dip transfers, in grams (1 g/ml proxy — WeighMath's
    /// convention, so the balance display and VesselStatus read consistently).
    public const float GramsPerScoop = 2f;

    /// A dip picks up ONLY from a solid/powder store with something left, and
    /// only while the scoop is empty (no double-dipping a full scoop).
    public static bool CanPickUp(bool carrying, PhysicalState state, float availableMl)
        => !carrying && (state == PhysicalState.Solid || state == PhysicalState.Powder)
           && availableMl > 0.01f;

    /// The last scoopful takes whatever remains.
    public static float ScoopCharge(float availableMl, float perScoop = GramsPerScoop)
        => Mathf.Min(Mathf.Max(0f, availableMl), perScoop);

    /// A carried charge deposits into any OTHER container (empty or not — the
    /// vessel's own capacity/reaction rules take over via AddLiquid).
    public static bool CanDeposit(bool carrying, bool sameContainer)
        => carrying && !sameContainer;

    /// Popup for a deposit: running total so repeated scoops read as progress.
    public static string DepositLabel(string chem, float addedG, float totalG)
        => "+" + addedG.ToString("0.#") + " g " + chem + "  (" + totalG.ToString("0.#") + " g total)";
}

/// Scoopula/spatula verb: dip into a solid reagent's jar to pick up a fixed
/// charge (a visible tinted heap rides the blade), touch a receiving vessel to
/// deposit it — per-scoop FloatingText totals, and the balance/VesselStatus
/// update through the normal contents path. Self-contained: no scene authoring
/// needed beyond adding the component (the proximity probe works off renderer
/// bounds; only a HELD scoop transfers, so shelf contact never scoops).
public class ScoopController : MonoBehaviour
{
    [SerializeField] private float probeRadius = 0.035f;
    [SerializeField] private float actionCooldown = 0.5f;

    private XRGrab _grab;
    private ChemicalData _carrying;
    private float _carryingG;
    private float _readyAt;
    private GameObject _heap;
    private LiquidPhysics _lastSource;   // the jar we just dipped — don't instantly re-deposit into it

    public bool Carrying => _carrying != null;

    void Awake() { if (_grab == null) Bind(GetComponent<XRGrab>()); }

    /// Edit-mode / builder seam.
    public void Bind(XRGrab grab) => _grab = grab;

    void Update()
    {
        if (!Application.isPlaying) return;
        bool held = _grab != null && _grab.isSelected;
        if (!held || Time.time < _readyAt) return;

        // Forgiving probe: anything near the blade counts as touched.
        var probe = ProbeCenter();
        var cols = Physics.OverlapSphere(probe, probeRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var col in cols)
        {
            if (col == null || col.transform.IsChildOf(transform)) continue;
            var lp = col.GetComponentInParent<LiquidPhysics>();
            if (lp == null) continue;
            if (!Carrying)
            {
                if (lp.currentChemical == null
                    || !ScoopMath.CanPickUp(false, lp.currentChemical.state, lp.currentLiquidVolume)) continue;
                float charge = ScoopMath.ScoopCharge(lp.currentLiquidVolume);
                var chem = lp.PourOut(charge);
                if (chem == null) continue;
                _carrying = chem; _carryingG = charge; _lastSource = lp;
                _readyAt = Time.time + actionCooldown;
                ShowHeap(chem);
                FloatingText.Show("+" + charge.ToString("0.#") + " g " + chem.chemicalName,
                                  probe + Vector3.up * 0.05f, new Color(1f, 0.95f, 0.6f), 0.8f);
                return;
            }
            if (!ScoopMath.CanDeposit(true, lp == _lastSource)) continue;
            lp.AddLiquid(_carrying, _carryingG);
            FloatingText.Show(ScoopMath.DepositLabel(_carrying.chemicalName, _carryingG, lp.currentLiquidVolume),
                              probe + Vector3.up * 0.05f, new Color(0.6f, 1f, 0.7f), 0.8f);
            _carrying = null; _carryingG = 0f; _lastSource = null;
            _readyAt = Time.time + actionCooldown;
            HideHeap();
            return;
        }
        // Once the loaded scoop has LEFT its source jar, forget it — so a later
        // deliberate return to the same jar deposits back instead of being
        // mistaken for the original dip.
        if (Carrying && _lastSource != null)
        {
            bool stillTouching = false;
            foreach (var col in cols)
                if (col != null && col.GetComponentInParent<LiquidPhysics>() == _lastSource) stillTouching = true;
            if (!stillTouching) _lastSource = null;
        }
    }

    private Vector3 ProbeCenter()
    {
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return transform.position;
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b.center;
    }

    /// Small tinted mound riding the blade while a charge is carried.
    private void ShowHeap(ChemicalData chem)
    {
        if (_heap == null)
        {
            _heap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _heap.name = "ScoopHeap";
            var hc = _heap.GetComponent<Collider>();
            if (hc != null) Destroy(hc);
            _heap.transform.SetParent(transform, false);
            _heap.transform.localScale = new Vector3(0.028f, 0.012f, 0.028f);
            var r = _heap.GetComponent<Renderer>();
            r.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ScoopHeap_Runtime" };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        _heap.transform.position = ProbeCenter() + Vector3.up * 0.008f;
        var rend = _heap.GetComponent<Renderer>();
        if (rend != null && rend.sharedMaterial != null)
        {
            var c = chem != null ? chem.liquidColor : Color.gray; c.a = 1f;
            if (rend.sharedMaterial.HasProperty("_BaseColor")) rend.sharedMaterial.SetColor("_BaseColor", c);
            else rend.sharedMaterial.color = c;
        }
        _heap.SetActive(true);
    }

    private void HideHeap() { if (_heap != null) _heap.SetActive(false); }
}
