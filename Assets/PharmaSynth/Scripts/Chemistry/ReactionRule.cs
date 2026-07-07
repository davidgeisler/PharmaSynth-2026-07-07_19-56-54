using UnityEngine;

/// Observable outcome of a reaction — the gradeable signal a chemical test checks for.
public enum ReactionOutcome { None, ColorChange, Fizzing, Precipitate, Odor, GasEvolved }

[CreateAssetMenu(fileName = "NewReaction", menuName = "Chemistry/Reaction Rule")]
public class ReactionRule : ScriptableObject
{
    [Header("Reactants")]
    public ChemicalData inputChemicalA;
    public ChemicalData inputChemicalB;

    [Header("Result")]
    public ChemicalData resultLiquid;      // The resulting liquid (e.g., Water)

    [Header("Precipitate (Optional)")]
    public ChemicalData resultPrecipitate; // The solid/ppt (e.g., Cu(OH)2)
    public bool hasPrecipitate;            // Check this if it makes a solid

    [Header("Conditions")]
    [Tooltip("Minimum temperature (°C) for the reaction to proceed; 0 = no heat needed.")]
    public float minTemperatureC = 0f;

    [Header("Observable outcome (drives chemical-test grading)")]
    public ReactionOutcome outcome = ReactionOutcome.None;
    public bool evolvesGas = false;
    [Tooltip("Expected observation shown/checked in the data sheet (e.g. 'permanent brown', 'brisk effervescence').")]
    public string expectedObservation = "";

    /// True when the ambient/vessel temperature meets this reaction's requirement.
    public bool TemperatureSatisfied(float currentTemperatureC) => currentTemperatureC >= minTemperatureC;
}