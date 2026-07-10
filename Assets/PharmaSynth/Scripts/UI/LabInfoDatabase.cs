using System.Collections.Generic;
using System.Text;

/// Knowledge base behind the hover-inspector pane (user 2026-07-10): pointing at a
/// piece of equipment, a reagent bottle or an NPC pops a card with its name and a
/// short, learn-as-you-play blurb — "what it's for + how to use it" for apparatus,
/// trivia + hazard for reagents. Pure + tested; the resolver (HoverInspector) feeds
/// it a chemical name / prop name and gets back a ready-to-display entry.
public enum LabInfoCategory { Equipment, Reagent, Person }

public sealed class LabInfoEntry
{
    public readonly string Title;
    public readonly LabInfoCategory Category;
    public readonly string Body;
    public LabInfoEntry(string title, LabInfoCategory cat, string body)
    { Title = title; Category = cat; Body = body; }
}

public static class LabInfoDatabase
{
    // ---- reagents: exact-name lookup (candidate = ChemicalData.chemicalName) -------
    private static readonly Dictionary<string, LabInfoEntry> _reagents = new Dictionary<string, LabInfoEntry>();
    // ---- equipment: ordered token → entry, first substring match wins (specific first)
    private static readonly List<KeyValuePair<string, LabInfoEntry>> _equip = new List<KeyValuePair<string, LabInfoEntry>>();
    private static bool _built;

    /// Normalise to lowercase alphanumerics so "Beaker_100mL" and "100 mL Beaker"
    /// both reduce to a comparable key.
    public static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    /// Reagent card by chemical name. Always returns an entry (authored, else a
    /// safe generic) so every bottle teaches at least the safety basics.
    public static LabInfoEntry Reagent(string chemicalName)
    {
        Build();
        if (_reagents.TryGetValue(Norm(chemicalName), out var e)) return e;
        string title = string.IsNullOrEmpty(chemicalName) ? "Reagent" : chemicalName;
        // The manuscript materials catalog carries blurbs for the raw-reagent
        // stock (batch H) — authored entries above still win.
        string catalogBlurb = RawReagentCatalog.BlurbFor(chemicalName);
        if (!string.IsNullOrEmpty(catalogBlurb))
            return new LabInfoEntry(title, LabInfoCategory.Reagent, catalogBlurb);
        return new LabInfoEntry(title, LabInfoCategory.Reagent,
            "A laboratory reagent. Handle with gloves and goggles, keep the bottle capped, and pour only what the procedure calls for.");
    }

    /// Equipment card by prop/prefab/display name — null if we have nothing authored
    /// (so random static geometry doesn't pop an empty card).
    public static LabInfoEntry Equipment(string candidate)
    {
        Build();
        string n = Norm(candidate);
        if (n.Length == 0) return null;
        foreach (var kv in _equip)
            if (n.Contains(kv.Key)) return kv.Value;
        return null;
    }

    public static LabInfoEntry Person(bool pharmee)
    {
        Build();
        return pharmee
            ? new LabInfoEntry("Pharmee", LabInfoCategory.Person,
                "Your robot lab guide. He hands out the procedure, cheers you on and flags mistakes — poke him any time to talk or to end the lab tour.")
            : new LabInfoEntry("Dr. Jimenez", LabInfoCategory.Person,
                "The examiner. He proctors the graded run and records your work, but gives no hints — plan your steps before you start.");
    }

    private static void AddReagent(string name, string body)
        => _reagents[Norm(name)] = new LabInfoEntry(name, LabInfoCategory.Reagent, body);

    private static void AddEquip(string token, string title, string body)
        => _equip.Add(new KeyValuePair<string, LabInfoEntry>(token, new LabInfoEntry(title, LabInfoCategory.Equipment, body)));

    // Test hooks.
    public static int ReagentCount { get { Build(); return _reagents.Count; } }
    public static int EquipmentCount { get { Build(); return _equip.Count; } }

    private static void Build()
    {
        if (_built) return;
        _built = true;

        // ---- EQUIPMENT (order: multi-word / specific tokens BEFORE their prefixes) ----
        AddEquip("graduatedcylinder", "Graduated Cylinder",
            "Measures liquid volume accurately. Set it on the bench and read the bottom of the meniscus at eye level.");
        AddEquip("erlenmeyer", "Erlenmeyer Flask",
            "Cone-shaped flask. The narrow neck lets you swirl the contents without spilling and slows evaporation — ideal for reactions and titrations.");
        AddEquip("testtuberack", "Test-Tube Rack",
            "Holds test tubes upright while samples react, settle or cool.");
        AddEquip("testtubeholder", "Test-Tube Holder",
            "Spring clamp for gripping a hot test tube while you heat it — keeps your hand clear of the flame.");
        AddEquip("testtubebrush", "Test-Tube Brush",
            "Bristle brush for scrubbing the inside of test tubes and narrow glassware clean.");
        AddEquip("testtube", "Test Tube",
            "Holds small samples for reactions and tests. Heat gently along the side, mouth pointed away from people.");
        AddEquip("washbottle", "Wash Bottle",
            "Squeeze bottle of distilled water for rinsing glassware and washing residues down.");
        AddEquip("watchglass", "Watch Glass",
            "Shallow glass dish — evaporate a few drops, hold solids for weighing, or cover a beaker.");
        AddEquip("beaker", "Beaker",
            "Wide-mouth container for holding, mixing and heating liquids. Its marks are rough guides only — use a cylinder for accurate volumes.");
        AddEquip("bunsenburner", "Bunsen Burner",
            "Gas burner for strong heat. Light it with the striker, then open the air collar for a hot blue flame.");
        AddEquip("alcoholburner", "Alcohol Burner",
            "Spirit lamp giving a gentle flame for mild heating. Light the wick; cap it to put it out — never blow it.");
        AddEquip("cruribletongs", "Crucible Tongs", "Long tongs for lifting hot crucibles and dishes safely.");
        AddEquip("crucibletongs", "Crucible Tongs", "Long tongs for lifting hot crucibles and evaporating dishes safely.");
        AddEquip("crucible", "Crucible",
            "Ceramic cup that survives very high heat for igniting or fusing solids. Move it only with tongs.");
        AddEquip("claytriangle", "Clay Triangle", "Supports a crucible on a tripod or ring during strong heating.");
        AddEquip("evaporatingdish", "Evaporating Dish",
            "Shallow porcelain dish for boiling off solvent to leave a solid residue behind.");
        AddEquip("glassrod", "Stirring Rod",
            "Glass rod that stirs solutions and guides pouring down its length so liquid doesn't splash.");
        AddEquip("dropper", "Dropper", "Adds liquid one drop at a time for small, precise additions.");
        AddEquip("scoopula", "Scoopula", "Scoop for transferring solid reagent. Keep it clean and dry between chemicals.");
        AddEquip("spatula", "Spatula", "Transfers small amounts of solid reagent. Wipe it clean between chemicals.");
        AddEquip("forceps", "Forceps", "Fine tweezers for handling small solids, crystals or paper without touching them.");
        AddEquip("motar", "Mortar & Pestle", "The mortar (bowl) and pestle (grinder) crush and grind solids into a fine powder.");
        AddEquip("mortar", "Mortar & Pestle", "The mortar (bowl) and pestle (grinder) crush and grind solids into a fine powder.");
        AddEquip("pestle", "Mortar & Pestle", "The pestle grinds solids against the mortar bowl into a fine powder.");
        AddEquip("retortstand", "Retort Stand", "Vertical support rod. Clamps and rings attach to it to hold apparatus above the bench.");
        AddEquip("ironring", "Iron Ring", "Clamps to the stand to support a funnel, wire gauze or flask over a burner.");
        AddEquip("wiregauze", "Wire Gauze", "Metal mesh that spreads a flame's heat evenly under glassware.");
        AddEquip("tripod", "Tripod", "Three-legged stand that holds glassware over a burner — pair it with wire gauze.");
        AddEquip("funnel", "Funnel", "Guides liquid into narrow openings and holds filter paper for gravity filtration.");
        AddEquip("balance", "Balance", "Weighs reagents. Tare it with the empty container on the pan, then add reagent to the target mass.");
        AddEquip("vial", "Reagent Vial", "Small capped bottle for storing or dispensing a reagent or a product.");
        AddEquip("labspeaker", "Lab Speaker", "Plays ambient background music for the lab. It gets louder as you walk toward it — a handy audio landmark for this corner.");
        AddEquip("speaker", "Lab Speaker", "Plays ambient background music for the lab. It gets louder as you walk toward it.");
        // PPE wearables (locker displays).
        AddEquip("labcoat", "Lab Coat", "Protects your clothes and skin from splashes. One of the three required PPE pieces — put it on at the locker before an experiment.");
        AddEquip("coat", "Lab Coat", "Protects your clothes and skin from splashes. One of the three required PPE pieces — put it on at the locker before an experiment.");
        AddEquip("goggle", "Safety Goggles", "Shield your eyes from splashes, fumes and flying glass. Required PPE — wear them the whole time you work.");
        AddEquip("glove", "Nitrile Gloves", "Protect your hands from corrosive and toxic reagents. Required PPE — pull on a pair before handling any chemical.");
        AddEquip("clothes", "Casual Clothes", "Your everyday outfit, seen in the mirror. Pull the lab coat, goggles and gloves on over it before starting.");

        // ---- REAGENTS (chemical-name keyed; trivia + hazard) --------------------------
        AddReagent("Sodium Hydroxide", "Caustic soda, a strong base. Dissolving it releases heat. Corrosive to skin and eyes — gloves and goggles are essential.");
        AddReagent("Sodium Acetate", "Salt of acetic acid. Heated with soda lime it gives off methane in the tutorial synthesis.");
        AddReagent("Soda Lime", "A NaOH/CaO mixture. Heated with sodium acetate it produces methane gas.");
        AddReagent("Glacial Acetic Acid", "Nearly pure acetic acid (>99%) — 'glacial' because it freezes just below room temperature. Pungent and corrosive.");
        AddReagent("Acetic Anhydride", "An acetylating agent used to make aspirin and acetanilide. Reacts with water and irritates the eyes and lungs.");
        AddReagent("Potassium Permanganate", "Deep-purple strong oxidiser. Stains skin brown; oxidises benzaldehyde to benzoic acid.");
        AddReagent("Benzaldehyde", "Almond-smelling aldehyde. Oxidised by potassium permanganate to benzoic acid in the midterm.");
        AddReagent("Acetyl Chloride", "A very reactive acetylating agent that fumes in moist air. Converts aniline to acetanilide.");
        AddReagent("Aniline", "Oily aromatic amine — toxic and absorbed through skin. Acetylation protects it as acetanilide.");
        AddReagent("Ethanol", "Drinking alcohol and a common solvent. Highly flammable — keep it away from open flames.");
        AddReagent("Sulfuric Acid", "Dense, strongly acidic and dehydrating. Always add acid TO water, never the reverse. Causes severe burns.");
        AddReagent("Hydrochloric Acid 6N", "A strong acid that fumes slightly. Supplies H+ and chloride for tests and neutralisations.");
        AddReagent("Sodium Nitrite", "Generates nitrous acid in place for the nitrous-acid test on benzamide. Toxic if swallowed.");
        AddReagent("Ammonia Solution", "Aqueous ammonia — sharp smell, weak base. Turns red litmus blue, so it flags evolved acidic gases.");
        AddReagent("Salicylic Acid", "The starting material for aspirin; acetylation turns it into acetylsalicylic acid.");
        AddReagent("Ferric Chloride 10%", "Iron(III) test reagent — gives a violet colour with phenols such as salicylic acid.");
        AddReagent("Silver Nitrate", "Forms white or cream precipitates with halides. Stains skin black in light.");
        AddReagent("Bromine Water", "Orange bromine solution that decolourises with unsaturated or easily-oxidised compounds.");
        AddReagent("Sodium Hypochlorite", "Bleach solution — an oxidiser and chlorinating agent used in the iodoform-type test.");
        AddReagent("Schiff's Reagent", "A colourless dye reagent that turns magenta with aldehydes but stays clear with ketones like acetone.");
        AddReagent("Murexide", "Ammonium purpurate. A purple murexide colour confirms caffeine.");
        AddReagent("Murexide Reagent", "The reagent set for the murexide test — a purple product confirms caffeine.");
        AddReagent("Caffeine", "Bitter alkaloid from tea and coffee, confirmed here by the purple murexide test.");
        AddReagent("Chloroform", "Dense, sweet-smelling solvent (CHCl3) and a suspected carcinogen — use it only in the fume hood.");
        AddReagent("Acetone", "Volatile, highly flammable solvent (propanone). Being a ketone it is Schiff-negative.");
        AddReagent("Benzoic Acid", "White crystalline aromatic acid — the product of oxidising benzaldehyde, and a food preservative.");
        AddReagent("Acetanilide", "White amide solid, the acetylation product of aniline. Once used to reduce fever.");
        AddReagent("Benzamide", "The simplest aromatic amide; alkali hydrolyses it to release ammonia for the litmus test.");
        AddReagent("Aspirin", "Acetylsalicylic acid — made by acetylating salicylic acid; a common pain reliever.");
        AddReagent("Limewater", "Calcium hydroxide solution. It turns milky with carbon dioxide, confirming the gas.");
        AddReagent("Yeast", "Living fungus that ferments sugars into ethanol and carbon dioxide in the wine-making module.");
        AddReagent("Grape Juice", "The sugar source that yeast ferments into wine (ethanol).");
        AddReagent("Sodium Carbonate", "Washing soda — a mild base and a by-product of the methane synthesis.");
        AddReagent("Calcium Carbonate", "Chalk or limestone; treated with acid it releases carbon dioxide.");
        AddReagent("Carbon Dioxide", "The gas that turns limewater milky — the classic confirmatory test for CO2.");
        AddReagent("Manganese Dioxide", "A dark oxidiser and catalyst; helps break down peroxides and oxidise organics.");
        AddReagent("Iodoform", "Pale-yellow solid with an antiseptic smell — the positive result of the iodoform test.");
    }
}
