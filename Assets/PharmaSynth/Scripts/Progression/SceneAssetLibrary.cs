using System.Collections.Generic;
using UnityEngine;

/// Runtime-safe name→asset lookup for the ExperimentSceneBuilder (serialized direct
/// references, so it works in a build — no AssetDatabase/Resources). Holds the
/// equipment prefabs and the reagent ChemicalData the layouts reference by name.
[CreateAssetMenu(fileName = "SceneAssetLibrary", menuName = "PharmaSynth/Scene Asset Library")]
public class SceneAssetLibrary : ScriptableObject
{
    public List<GameObject> prefabs = new List<GameObject>();
    public List<ChemicalData> chemicals = new List<ChemicalData>();

    public GameObject GetPrefab(string n)
    {
        for (int i = 0; i < prefabs.Count; i++)
            if (prefabs[i] != null && prefabs[i].name == n) return prefabs[i];
        return null;
    }

    public ChemicalData GetChemical(string n)
    {
        for (int i = 0; i < chemicals.Count; i++)
            if (chemicals[i] != null && chemicals[i].chemicalName == n) return chemicals[i];
        return null;
    }
}
