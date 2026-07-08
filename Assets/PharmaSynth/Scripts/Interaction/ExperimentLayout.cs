using System;
using System.Collections.Generic;
using UnityEngine;

/// Data description of one experiment's physical setup: where its stations, grabbable
/// props and reagent vessels go. The ExperimentSceneBuilder spawns this on module load,
/// so all 11 experiments share one lab scene instead of 11 hand-built scenes.
/// Positions are WORLD-space (the lab is a fixed room).
[CreateAssetMenu(fileName = "ExperimentLayout", menuName = "PharmaSynth/Experiment Layout")]
public class ExperimentLayout : ScriptableObject
{
    [Serializable]
    public class Station
    {
        public string taskId;
        public string label;
        public string requiredItemId;     // grabbable prop that completes it (empty = any)
        public Vector3 pos;               // pad centre, world space (y = surface)
    }

    [Serializable]
    public class Prop
    {
        public string prefabName;         // ChemLabEquipment prefab (by name) via the library
        public string itemId;             // matches a Station.requiredItemId
        public string displayName;        // proximity label
        public Vector3 pos;
        public float targetHeight = 0.16f;
        public bool pourable = false;     // add LiquidPourer + fill
        public string fillChemical = "";  // ChemicalData name to fill a pourable bottle with
    }

    [Serializable]
    public class Vessel
    {
        [Serializable] public class Bind { public string reagentChemical; public string taskId; }
        public string prefabName;
        public string displayName;
        public Vector3 pos;
        public float targetHeight = 0.2f;
        public string startChemical = "";     // what the vessel starts holding (optional)
        public List<Bind> bindings = new List<Bind>();   // reagent poured in → task completed
    }

    public string moduleId;
    public List<Station> stations = new List<Station>();
    public List<Prop> props = new List<Prop>();
    public List<Vessel> vessels = new List<Vessel>();
}
