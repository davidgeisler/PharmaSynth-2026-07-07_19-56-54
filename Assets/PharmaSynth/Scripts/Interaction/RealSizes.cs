using System.Collections.Generic;
using UnityEngine;

/// Real-world size table for the ChemLab prefabs: name → LONGEST dimension in
/// metres. The pack's meshes are authored true-to-scale, so these pin each item
/// to its realistic size and normalisation scales by the longest axis — the old
/// bounds-HEIGHT normalisation inflated flat tools (spatula, iron ring, wire
/// gauze) by 3-16x, which read as comically massive in-headset.
public static class RealSizes
{
    private static readonly Dictionary<string, float> Table = new Dictionary<string, float>
    {
        { "AlcoholBurner", 0.125f },
        { "Balance", 0.26f },
        { "Beaker_100mL", 0.08f },
        { "Beaker_100mL_WithLiquid", 0.08f },
        { "Beaker_500mL", 0.13f },
        { "Beaker_500mL_WithLiquid", 0.13f },
        { "BunsenBurner", 0.45f },              // incl. gas hose
        { "ClayTriangle", 0.13f },
        { "Crucible", 0.055f },
        { "CrucibleTongs", 0.32f },
        { "Dropper", 0.12f },
        { "ErlenmeyerFlask_400mL", 0.16f },
        { "ErlenmeyerFlask_400mL_WithLiquid", 0.16f },
        { "EvaporatingDish", 0.11f },
        { "Forceps", 0.12f },
        { "Funnel", 0.20f },
        { "GlassRod", 0.25f },
        { "GraduatedCylinder_50mL", 0.21f },
        { "GraduatedCylinder_50mL_WithLiquid", 0.21f },
        { "IronRing", 0.18f },
        { "Motar", 0.11f },
        { "Pestle", 0.10f },
        { "RetortStand", 0.50f },
        { "Scoopula", 0.15f },
        { "Spatula", 0.16f },
        { "TestTube", 0.13f },
        { "TestTube_WithLiquid", 0.13f },
        { "TestTubeBrush", 0.20f },
        { "TestTubeHolder_Metal", 0.13f },
        { "TestTubeHolder_Wooden", 0.26f },
        { "TestTubeRack", 0.18f },
        { "TestTubeRack_12Tubes", 0.18f },
        { "TestTubeRack_WithDryingPins", 0.18f },
        { "Tripod", 0.18f },
        { "Vial", 0.09f },
        { "Vial_Brown", 0.09f },
        { "Vial_Brown_WithLabel", 0.09f },
        { "Vial_WithLabel", 0.09f },
        { "WashBottle", 0.25f },
        { "WashBottle_WithLabel", 0.25f },
        { "WatchGlass", 0.10f },
        { "WireGauze", 0.15f },
    };

    public static int Count => Table.Count;

    public static bool TryGet(string prefabName, out float longestMeters)
        => Table.TryGetValue(prefabName ?? "", out longestMeters);

    /// Uniform factor that brings a bounds' LONGEST axis to the target length.
    public static float UniformScaleFactor(Vector3 currentWorldSize, float targetLongest)
    {
        float longest = Mathf.Max(currentWorldSize.x, Mathf.Max(currentWorldSize.y, currentWorldSize.z));
        if (longest < 1e-4f || targetLongest <= 0f) return 1f;
        return targetLongest / longest;
    }
}
