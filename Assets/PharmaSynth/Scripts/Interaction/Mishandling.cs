using System.Collections.Generic;
using UnityEngine;

/// Pure rules for mishandling penalties (§2: spill & breakage, user request
/// 2026-07-09): which apparatus is fragile, when an impact shatters it, and
/// when an un-held bottle counts as spilling. Kept plain-C# so the self-tests
/// pin the policy.
public static class Mishandling
{
    /// Glass / porcelain items that shatter when dropped. Metal, wood and
    /// plastic tools (tongs, spatula, racks, wash bottles…) never break.
    private static readonly HashSet<string> Breakables = new HashSet<string>
    {
        "Beaker_100mL", "Beaker_100mL_WithLiquid",
        "Beaker_500mL", "Beaker_500mL_WithLiquid",
        "ErlenmeyerFlask_400mL", "ErlenmeyerFlask_400mL_WithLiquid",
        "GraduatedCylinder_50mL", "GraduatedCylinder_50mL_WithLiquid",
        "TestTube", "TestTube_WithLiquid",
        "Vial", "Vial_Brown", "Vial_Brown_WithLabel", "Vial_WithLabel",
        "WatchGlass", "GlassRod", "Funnel", "Dropper",
        "EvaporatingDish", "Crucible",          // porcelain
    };

    public static bool IsBreakable(string prefabName) => Breakables.Contains(prefabName ?? "");
    public static IEnumerable<string> BreakableNames => Breakables;

    /// An impact at or above this speed shatters glass. 4.0 m/s ≈ a free fall
    /// of ~0.8 m onto a hard surface — a real drop from bench/shelf height
    /// breaks, but carrying an item and bumping a wall or a neighbouring bottle
    /// (a slow scrape, well under this) never does (user 2026-07-11: breakage
    /// was far too twitchy). Held items are additionally immune in
    /// BreakableGlassware regardless of speed.
    public const float DefaultBreakSpeed = 4.0f;

    public static bool ShouldBreak(float impactSpeed, float breakSpeed = DefaultBreakSpeed)
        => impactSpeed >= breakSpeed;

    /// Metal apparatus — everything else non-glass lands as a dull wooden knock.
    private static readonly HashSet<string> MetalItems = new HashSet<string>
    {
        "CrucibleTongs", "Forceps", "Scoopula", "Spatula", "IronRing",
        "Tripod", "RetortStand", "WireGauze", "BunsenBurner", "TestTubeHolder_Metal", "Balance",
    };

    /// SoundBank key for a drop/impact clatter, by material.
    public static string DropSoundKey(string prefabName)
    {
        if (IsBreakable(prefabName)) return "glass-clink";
        if (MetalItems.Contains(prefabName ?? "")) return "drop-metal";
        return "drop-wood";
    }

    /// SoundBank key for a fired reaction's observable outcome.
    public static string SfxForOutcome(ReactionOutcome outcome)
    {
        switch (outcome)
        {
            case ReactionOutcome.Fizzing:
            case ReactionOutcome.GasEvolved:
                return "reaction-fizz";
            case ReactionOutcome.None:
                return "";                       // negative test: nothing to hear
            default:
                return "mixture-complete";       // colour change / precipitate / odour cue
        }
    }

    /// A reagent bottle is SPILLING when nobody holds it, it still has liquid,
    /// and it lies tipped past the threshold (LiquidPourer drains it; this
    /// decides whether that drain is a graded mishandling event).
    public static bool IsSpilling(float tiltDegrees, bool held, float liquidMl, float tiltThreshold = 60f)
        => !held && liquidMl > 0.5f && tiltDegrees > tiltThreshold;
}
