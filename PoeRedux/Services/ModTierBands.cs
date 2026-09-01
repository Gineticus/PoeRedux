using System.Text.RegularExpressions;

namespace PoeRedux.Services;

/// <summary>
/// How strong a roll can get for one stat. <paramref name="Max"/> is the best any craftable mod
/// grants; <paramref name="FamilyMaxima"/> is the best per mod ladder, because one stat usually
/// has several: a one-hand ladder and a two-hand ladder reach different values, and the item
/// class never reaches the description file.
/// </summary>
public sealed record StatScale(int Max, int ModCount, int[] FamilyMaxima);

public static class ModTierBands
{
    private const int DomainItem = 1;
    private const int GenerationPrefix = 1;
    private const int GenerationSuffix = 2;

    /// <summary>Slots a mod can grant a stat in, so hybrids contribute a ceiling to each.</summary>
    private const int StatSlots = 8;

    /// <summary>A ladder needs this many tiers before its top counts as a target worth hitting.</summary>
    private const int MinimumTiers = 3;

    /// <summary>
    /// Mod ids are systematically `FamilyNameTier`, as in IncreasedLife0..12 and
    /// IncreasedCastSpeedTwoHand1..7. Stripping the tier suffix groups a ladder and keeps the
    /// one-hand and two-hand variants apart, which grouping by spawn tags does not: the top life
    /// tiers carry different tags and would split their own ladder.
    /// </summary>
    private static readonly Regex TierSuffix = new(@"[_0-9]+$", RegexOptions.Compiled);

    public static Dictionary<string, StatScale> Build(byte[] modsTable, byte[] statsTable, out int skipped)
    {
        var schema = DatSchema.Load();
        var mods = new DatFile(modsTable, schema["Mods"]);
        var stats = new DatFile(statsTable, schema["Stats"]);

        var statIds = new string[stats.RowCount];
        for (var r = 0; r < stats.RowCount; r++)
            statIds[r] = stats.String(r, "Id");

        // stat -> family -> the maximum of every mod in that family
        var ladders = new Dictionary<int, Dictionary<string, List<int>>>();
        for (var r = 0; r < mods.RowCount; r++)
        {
            if (mods.Int32(r, "Domain") != DomainItem)
                continue;
            var generation = mods.Int32(r, "GenerationType");
            if (generation != GenerationPrefix && generation != GenerationSuffix)
                continue;
            // A mod that can never roll normally, essence and unique only, would raise the
            // ceiling past anything reachable by crafting.
            if (!mods.Int32Array(r, "SpawnWeight_Values").Any(w => w > 0))
                continue;

            var family = TierSuffix.Replace(mods.String(r, "Id"), "");
            // Every slot counts. A hybrid mod is a real way to reach a value for each stat it
            // grants, and some stats have no single stat mod at all.
            for (var slot = 1; slot <= StatSlots; slot++)
            {
                if (mods.Reference(r, $"StatsKey{slot}") is not int statRow || statRow >= statIds.Length)
                    continue;
                if (!ladders.TryGetValue(statRow, out var byFamily))
                    ladders[statRow] = byFamily = new(StringComparer.Ordinal);
                if (!byFamily.TryGetValue(family, out var list))
                    byFamily[family] = list = [];
                list.Add(mods.Int32(r, $"Stat{slot}Max"));
            }
        }

        skipped = 0;
        var result = new Dictionary<string, StatScale>(StringComparer.Ordinal);
        foreach (var (statRow, byFamily) in ladders)
        {
            var max = byFamily.Values.SelectMany(v => v).DefaultIfEmpty(0).Max();
            // Reductions and flags roll negative or zero, so a fraction of a ceiling is
            // meaningless for them.
            if (max <= 0)
            {
                skipped++;
                continue;
            }

            // One-off ladders, a single fishing rod mod for instance, are not a target anyone
            // aims for, so their top would light up mid rolls of the real ladders.
            var real = byFamily.Values.Where(v => v.Count >= MinimumTiers).ToList();
            var maxima = (real.Count > 0 ? real : byFamily.Values.ToList())
                .Select(v => v.Max())
                .Where(m => m > 0)
                .Distinct()
                .Order()
                .ToArray();

            var modCount = byFamily.Values.Sum(v => v.Count);
            result[statIds[statRow]] = new StatScale(max, modCount, maxima);
        }
        return result;
    }
}
