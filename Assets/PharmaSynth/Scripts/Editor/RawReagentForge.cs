#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

/// Generates the ChemicalData assets for every RawReagentCatalog row that the
/// game doesn't already know (matched by normalised chemicalName), stamps the
/// HazardousMix flags from the shared HazardFlags rules, and registers everything
/// in the SceneAssetLibrary so layouts and the cabinet builder resolve them.
/// Consumable rows (SmallBox/IceBucket) are physical props, not chemicals — no
/// SO is made for them. Idempotent.
public static class RawReagentForge
{
    const string ChemDir = "Assets/PharmaSynth/ScriptableObjects/Chemicals/";
    const string LibPath = "Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset";

    [MenuItem("Tools/PharmaSynth/Generate Raw Reagent Data")]
    public static void Generate()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>(LibPath);
        if (lib == null) { Debug.LogError("[RawReagentForge] SceneAssetLibrary not found."); return; }

        // Existing chemicals by normalised name (SO scan, not just the library).
        var existing = new System.Collections.Generic.Dictionary<string, ChemicalData>();
        foreach (string guid in AssetDatabase.FindAssets("t:ChemicalData", new[] { "Assets/PharmaSynth/ScriptableObjects" }))
        {
            var c = AssetDatabase.LoadAssetAtPath<ChemicalData>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null) existing[Norm(c.chemicalName)] = c;
        }

        int created = 0, reused = 0, registered = 0;
        foreach (var row in RawReagentCatalog.Rows)
        {
            if (row.labware == RawReagentCatalog.LabwareKind.SmallBox
                || row.labware == RawReagentCatalog.LabwareKind.IceBucket) continue;   // props, not chemicals

            ChemicalData chem;
            if (existing.TryGetValue(Norm(row.chemicalName), out chem)) reused++;
            else
            {
                chem = ScriptableObject.CreateInstance<ChemicalData>();
                chem.chemicalName = row.chemicalName;
                chem.state = row.state;
                chem.liquidColor = row.color;
                chem.liquidTopColor = Color.Lerp(row.color, Color.white, 0.25f);
                chem.pH = row.pH;
                chem.hazard = row.hazard;
                chem.requiresFumeHood = row.fumeHood;
                chem.isDangerous = row.hazard == HazardType.Corrosive || row.hazard == HazardType.Toxic;
                AssetDatabase.CreateAsset(chem, ChemDir + "Chem_" + SafeFile(row.chemicalName) + ".asset");
                existing[Norm(row.chemicalName)] = chem;
                created++;
            }
            chem.isOxidizer = HazardFlags.IsOxidizer(chem.chemicalName);
            chem.isConcentratedAcid = HazardFlags.IsConcentratedAcid(chem.chemicalName);
            EditorUtility.SetDirty(chem);

            if (lib.chemicals != null && !lib.chemicals.Contains(chem))
            {
                lib.chemicals.Add(chem);
                registered++;
            }
        }
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log($"[RawReagentForge] catalog {RawReagentCatalog.Rows.Count} rows → {created} chemicals created, "
                  + $"{reused} reused, {registered} newly registered in the SceneAssetLibrary.");
    }

    static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    static string SafeFile(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }
}
#endif
