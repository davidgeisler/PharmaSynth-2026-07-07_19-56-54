#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Stamps the HazardousMix flags (isOxidizer / isConcentratedAcid) onto every
/// ChemicalData asset from the pure HazardFlags name rules, and ensures the
/// Chem_RuinedMixture SO (the dark sludge an overheated batch turns into)
/// exists and is registered in the SceneAssetLibrary. Idempotent.
public static class ChemHazardFlagAudit
{
    const string ChemDir = "Assets/PharmaSynth/ScriptableObjects/Chemicals/";
    const string RuinedPath = ChemDir + "Chem_RuinedMixture.asset";

    [MenuItem("Tools/PharmaSynth/Audit Chemical Hazard Flags")]
    public static void Audit()
    {
        int changed = 0, total = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ChemicalData", new[] { "Assets/PharmaSynth/ScriptableObjects" }))
        {
            var chem = AssetDatabase.LoadAssetAtPath<ChemicalData>(AssetDatabase.GUIDToAssetPath(guid));
            if (chem == null) continue;
            total++;
            bool ox = HazardFlags.IsOxidizer(chem.chemicalName);
            bool acid = HazardFlags.IsConcentratedAcid(chem.chemicalName);
            if (chem.isOxidizer != ox || chem.isConcentratedAcid != acid)
            {
                chem.isOxidizer = ox;
                chem.isConcentratedAcid = acid;
                EditorUtility.SetDirty(chem);
                changed++;
            }
        }
        EnsureRuinedMixture();
        AssetDatabase.SaveAssets();
        Debug.Log($"[ChemHazardFlagAudit] {total} chemicals audited, {changed} flag sets updated; Chem_RuinedMixture ensured.");
    }

    /// Load-or-create the ruined-batch chemical (overheat consequence) and make
    /// sure the scene asset library can resolve it.
    public static ChemicalData EnsureRuinedMixture()
    {
        var ruined = AssetDatabase.LoadAssetAtPath<ChemicalData>(RuinedPath);
        if (ruined == null)
        {
            ruined = ScriptableObject.CreateInstance<ChemicalData>();
            ruined.chemicalName = "Ruined Mixture";
            ruined.state = PhysicalState.Liquid;
            ruined.liquidColor = new Color(0.16f, 0.11f, 0.07f, 0.95f);       // charred sludge
            ruined.liquidTopColor = new Color(0.22f, 0.15f, 0.09f, 0.95f);
            ruined.viscosity = 0.85f;
            ruined.pH = 7f;
            AssetDatabase.CreateAsset(ruined, RuinedPath);
        }
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        if (lib != null && lib.chemicals != null && !lib.chemicals.Contains(ruined))
        {
            lib.chemicals.Add(ruined);
            EditorUtility.SetDirty(lib);
        }
        return ruined;
    }
}
#endif
