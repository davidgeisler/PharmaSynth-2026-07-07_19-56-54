using UnityEngine;

/// Identity carried by a grabbable lab prop (reagent jar, glass tube, lit splint,
/// collection tube, burner…). An ExperimentTaskStation can require a specific
/// itemId so that bringing the *right* prop to the *right* apparatus — not just any
/// grabbed object — completes that step. This turns the abstract poke-stations into
/// hands-on "gather the correct item and place it" interactions (plan §3.2, W5).
public class LabItem : MonoBehaviour
{
    [Tooltip("Stable id matched against ExperimentTaskStation.requiredItemId, e.g. 'sodium-acetate', 'glass-tube', 'lit-splint'.")]
    public string itemId = "";

    [Tooltip("Human-readable label for tooltips / debugging.")]
    public string displayName = "";

    public void SetItemId(string id) => itemId = id;

    /// Resolve the LabItem owning a collider (colliders are often on children).
    public static LabItem Resolve(Collider other)
        => other != null ? other.GetComponentInParent<LabItem>() : null;
}
