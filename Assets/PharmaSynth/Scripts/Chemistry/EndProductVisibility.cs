using System.Collections.Generic;
using UnityEngine;

/// Hides ready-made END-PRODUCT bottles outside demo sessions (user 2026-07-11:
/// "in regular laboratory mode the end-game reagents must not be present").
/// Lives on a storage ROOT (ReagentShelf / ReagentCabinets) — the root stays
/// active so this keeps running, and gated bottles are fully SetActive(false)
/// so the supply monitor, hover cards and grabs all ignore them. Play-mode only:
/// in the editor everything stays visible for arranging; Unity restores the
/// authored state when Play ends.
public class EndProductVisibility : MonoBehaviour
{
    private readonly List<GameObject> _gated = new List<GameObject>();
    private bool _scanned;
    private bool _lastShow = true;

    /// Test/rescan seam.
    public int Rescan()
    {
        _gated.Clear();
        foreach (var lp in GetComponentsInChildren<LiquidPhysics>(true))
            if (lp.currentChemical != null && DemoMode.IsEndProduct(lp.currentChemical.chemicalName))
                _gated.Add(lp.gameObject);
        _scanned = true;
        return _gated.Count;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (!_scanned) { Rescan(); _lastShow = !DemoSession.Active; }   // force first apply
        bool show = DemoSession.Active;
        if (show == _lastShow) return;
        _lastShow = show;
        foreach (var go in _gated)
            if (go != null && go.activeSelf != show) go.SetActive(show);
    }
}
