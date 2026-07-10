/// Pure name-based hazard-flag rules for ChemicalData (one table shared by the
/// editor audit menu, the raw-reagent forge, and the self-tests). Names are the
/// stable key — the chemistry SOs are authored per display name.
public static class HazardFlags
{
    public static bool IsOxidizer(string chemicalName)
    {
        string n = Norm(chemicalName);
        return n.Contains("permanganate") || n.Contains("hypochlorite") || n.Contains("bleach")
            || n.Contains("dichromate") || n.Contains("bichromate") || n.Contains("bromine");
    }

    public static bool IsConcentratedAcid(string chemicalName)
    {
        string n = Norm(chemicalName);
        bool acidFamily = n.Contains("sulfuric") || n.Contains("hydrochloric")
                       || n.Contains("nitric") || n.Contains("glacial");
        bool dilute = n.Contains("dilut") || n.Contains("0.1");
        return acidFamily && !dilute;
    }

    private static string Norm(string s) => string.IsNullOrEmpty(s) ? "" : s.ToLowerInvariant();
}
