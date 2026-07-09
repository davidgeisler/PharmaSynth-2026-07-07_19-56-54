using System.Collections.Generic;
using UnityEngine;

/// Pure helpers for the post-experiment return loop: what did this pass unlock
/// (for Pharmee's announcement), and where does the rig go so the CAMERA lands
/// on the front-door marker. No MonoBehaviours — edit-mode testable.
public static class UnlockDiff
{
    /// All currently-unlocked moduleIds.
    public static HashSet<string> UnlockedSet(ProgressionFlow flow)
    {
        var set = new HashSet<string>();
        if (flow == null) return set;
        foreach (var e in ExperimentCatalog.Entries)
            if (flow.IsUnlocked(e.moduleId)) set.Add(e.moduleId);
        return set;
    }

    /// Modules unlocked NOW that were not in `before` (catalog order).
    public static List<string> NewlyUnlocked(HashSet<string> before, ProgressionFlow after)
    {
        var list = new List<string>();
        if (after == null) return list;
        foreach (var e in ExperimentCatalog.Entries)
            if (after.IsUnlocked(e.moduleId) && (before == null || !before.Contains(e.moduleId)))
                list.Add(e.moduleId);
        return list;
    }

    /// Pharmee's line for the unlock moment; falls back to plain congratulations.
    public static string AnnouncementFor(IReadOnlyList<string> newIds)
    {
        if (newIds == null || newIds.Count == 0)
            return "Great work back there! Come see me when you're ready for the next challenge.";
        var titles = new List<string>();
        foreach (var id in newIds)
        {
            var entry = ExperimentCatalog.Get(id);
            titles.Add(entry != null ? entry.title : id);
        }
        return newIds.Count == 1
            ? "You've unlocked a new experiment: " + titles[0] + "!"
            : "You've unlocked new experiments: " + string.Join(", ", titles) + "!";
    }
}

/// Rig-vs-camera teleport math: an XR rig's origin is NOT the player's head, so
/// landing the HEAD on a marker means offsetting the rig by the (yaw-corrected)
/// head offset.
public static class TeleportMath
{
    /// Extra yaw to apply to the rig so the camera faces the marker's yaw.
    public static float RigYawFor(float markerYawDeg, float rigYawDeg, float camYawDeg)
        => rigYawDeg + Mathf.DeltaAngle(camYawDeg, markerYawDeg);

    /// Rig position such that, after the rig is yawed by `deltaYawDeg`, the camera
    /// stands exactly on the marker's XZ (rig Y = marker Y = floor height).
    public static Vector3 RigPositionFor(Vector3 markerPos, float deltaYawDeg, Vector3 rigPos, Vector3 camPos)
    {
        Vector3 off = camPos - rigPos;
        Vector3 offR = Quaternion.Euler(0f, deltaYawDeg, 0f) * off;
        return new Vector3(markerPos.x - offR.x, markerPos.y, markerPos.z - offR.z);
    }
}
