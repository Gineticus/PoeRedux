namespace PoeRedux.Services;

/// <summary>
/// One quality band, as a fraction of a stat's craftable ceiling. The color says how good the
/// roll is, not what kind of stat it is, so a near perfect life roll and a near perfect
/// resistance roll look the same and the eye only has to learn three colors.
/// </summary>
public sealed record RollTier(
    string Name,
    double MinFraction,
    byte R,
    byte G,
    byte B,
    string Tag,
    bool Enabled = true);

public static class RollTiers
{
    public static readonly RollTier[] Defaults =
    [
        // Above 100% means above the best craftable roll, which only elevated, Maven, essence
        // and unique values can reach. Nothing needs to identify them: the value gives it away.
        new("Ultra", 1.01, 227, 107, 255, "ULTRA"),
        new("Perfect", 0.95, 255, 210, 74, "PERFECT"),
        new("Great", 0.88, 74, 230, 58, "GREAT"),
        new("Good", 0.78, 14, 186, 255, "GOOD"),
    ];

    /// <summary>Highest band first, and only the enabled ones, which is the order Rewrite needs.</summary>
    public static List<RollTier> Ordered(IEnumerable<RollTier> tiers) =>
        tiers.Where(t => t.Enabled && t.MinFraction > 0)
             .OrderByDescending(t => t.MinFraction)
             .ToList();
}
