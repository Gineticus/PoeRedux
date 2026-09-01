namespace PoeRedux.Models;

/// <summary>
/// How a mod line should be styled. Passed into the editors and mutated in place, like the
/// collections they edit, so Exit keeps the change.
/// </summary>
public class StyleOptions
{
    /// <summary>Color a short tag in front of the mod rather than the whole line.</summary>
    public bool TagPrefix { get; set; }

    /// <summary>Measure a roll against every ladder for its stat, not just the hardest one.</summary>
    public bool PerLadder { get; set; } = true;

    /// <summary>Share of the ceiling a ladder must reach before its top counts as perfect.</summary>
    public double MinLadderShare { get; set; } = 0.5;

    /// <summary>Shorten the " (Tier: {0})" line the client appends after a mod. Tier colors only.</summary>
    public bool CompactTier { get; set; }

}
