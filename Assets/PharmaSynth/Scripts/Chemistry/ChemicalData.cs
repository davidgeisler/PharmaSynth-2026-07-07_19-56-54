using UnityEngine;

public enum PhysicalState { Liquid, Solid, Powder, Gas }

/// Hazard class — drives spill/contact feedback and the fume-hood requirement.
public enum HazardType { None, Toxic, Corrosive, Flammable, Volatile }

[CreateAssetMenu(fileName = "ChemicalData", menuName = "Chemistry/ChemicalData")]
public class ChemicalData : ScriptableObject
{
    [Header("Identity")]
    public string chemicalName = "Water";
    public PhysicalState state = PhysicalState.Liquid;

    [Header("Visuals")]
    [ColorUsage(true, true)]
    public Color liquidColor;

    [ColorUsage(true, true)]
    public Color liquidTopColor;

    [Range(0.0f, 1.0f)]
    public float sceneColourAmount;
    [Range(0f, 1f)]
    public float viscosity = 0.5f;

    [Header("Physical properties (for tests & thresholds)")]
    [Tooltip("Boiling point in °C — distillation cut-offs (e.g. acetone 56, ethanol ~78).")]
    public float boilingPointC = 100f;
    [Tooltip("Colour of the precipitate/product this chemical shows in a positive test.")]
    [ColorUsage(true, true)]
    public Color precipitateColor = Color.white;
    [Range(0f, 14f)] public float pH = 7f;
    public bool evolvesGas = false;

    [Header("Safety")]
    public HazardType hazard = HazardType.None;
    [Tooltip("Toxic/volatile reagents must be handled in the fume hood.")]
    public bool requiresFumeHood = false;
    public bool isDangerous = false; // legacy flag (kept for existing spill logic)
    [Tooltip("Strong oxidizer — meeting a Flammable chemical outside a known reaction ignites (HazardousMix).")]
    public bool isOxidizer = false;
    [Tooltip("Concentrated acid — pouring other liquids into it spatters (HazardousMix). Set by Tools ▸ PharmaSynth ▸ Audit Chemical Hazard Flags.")]
    public bool isConcentratedAcid = false;
}