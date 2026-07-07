using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MasterRegistry", menuName = "Chemistry/Reaction Registry")]
public class ReactionRegistry : ScriptableObject
{
    public List<ReactionRule> rules;

    public ReactionRule FindReaction(ChemicalData a, ChemicalData b)
    {
        if (rules == null) return null;
        foreach (var rule in rules)
        {
            if (rule == null) continue;
            // Matches A+B or B+A
            if ((rule.inputChemicalA == a && rule.inputChemicalB == b) ||
                (rule.inputChemicalA == b && rule.inputChemicalB == a))
            {
                return rule;
            }
        }
        return null;
    }
}