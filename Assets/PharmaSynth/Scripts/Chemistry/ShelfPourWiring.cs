using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Pour wiring for HAND-PLACED bottles (user 2026-07-10: tipping a reagent-shelf
/// bottle showed no stream/puddle). ExperimentSceneBuilder wires runtime-spawned
/// pourables, but the 16 shelf display bottles (and batch-H cabinet stock) are
/// scene objects with LiquidPhysics only. This mirrors the builder's pourable
/// block for an existing bottle, idempotently, callable from the editor menu
/// (Tools ▸ PharmaSynth ▸ Wire Shelf Pourers) and from runtime builders.
///
/// Edit-mode note: SpillMistake/LiquidPourer self-bind in Awake/Start at play
/// time, so edit-mode wiring only has to ADD the components and set the
/// serialized fields (registry, spout); the runner param matters only for
/// runtime callers.
public static class ShelfPourWiring
{
    /// Ensure the bottle pours visibly and grades spills. Returns the number of
    /// components/objects added (0 = was already fully wired, -1 = not a liquid
    /// container).
    public static int WireBottle(GameObject bottle, ExperimentRunner runner, ReactionRegistry registry)
    {
        if (bottle == null) return -1;
        var lp = bottle.GetComponent<LiquidPhysics>();
        if (lp == null) return -1;

        int added = 0;
        if (lp.registry == null && registry != null) { lp.registry = registry; added++; }

        var pourer = bottle.GetComponent<LiquidPourer>();
        if (pourer == null) { pourer = bottle.AddComponent<LiquidPourer>(); added++; }
        if (pourer.spout == null)
        {
            var spout = new GameObject("Spout").transform;
            spout.SetParent(bottle.transform, false);
            spout.localPosition = new Vector3(0f, 0.12f, 0f);
            pourer.spout = spout;
            added++;
        }

        if (bottle.GetComponent<SpillMistake>() == null)
        {
            var spill = bottle.AddComponent<SpillMistake>();
            spill.Bind(runner, lp, bottle.GetComponent<XRGrab>(), bottle.name);
            added++;
        }

        if (bottle.GetComponent<HazardousMixReactor>() == null)
        {
            bottle.AddComponent<HazardousMixReactor>().Bind(lp, runner);   // bad-mix consequences
            added++;
        }
        return added;
    }
}
