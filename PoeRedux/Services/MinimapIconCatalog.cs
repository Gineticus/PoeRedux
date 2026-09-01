using System.IO;
using System.Text;
using BundleIndex = LibBundle3.Index;

namespace PoeRedux.Services;

/// <summary>
/// The icon ids the minimap can draw, read out of data/minimapicons.datc64 in row order so an
/// icon written as a bare row index resolves to its name. Only the first column is needed, so
/// this skips the dat schema: the Id offset sits at the start of every row, relative to the
/// row/data separator.
/// </summary>
public static class MinimapIconCatalog
{
    private const string TablePath = "data/minimapicons.datc64";

    /// <summary>Icon ids in row order. The index into this list is what a bare number means.</summary>
    public static Task<List<string>> LoadAsync(string gameFilePath) =>
        Task.Run(() => Load(gameFilePath));

    public static List<string> Load(string gameFilePath) => GameFile.Read(gameFilePath, Read);

    public static List<string> Read(BundleIndex index) =>
        index.TryGetFile(TablePath, out var file)
            ? Parse(file.Read().ToArray())
            : throw new FileNotFoundException($"{TablePath} is not in the index.");

    public static List<string> Parse(byte[] table)
    {
        var rows = (int)BitConverter.ToUInt32(table, 0);
        var separator = IndexOfSeparator(table);
        var width = (separator - 4) / rows;

        var ids = new List<string>(rows);
        for (var row = 0; row < rows; row++)
        {
            var offset = separator + (int)BitConverter.ToUInt64(table, 4 + row * width);
            var end = offset;
            while (end + 1 < table.Length && !(table[end] == 0 && table[end + 1] == 0))
                end += 2;
            ids.Add(Encoding.Unicode.GetString(table, offset, end - offset));
        }
        return ids;
    }

    /// <summary>
    /// Splits a loot filter icon id into its parts. Those 396 ids are self describing, which is
    /// what makes a drawn preview possible without decoding the game's texture atlas.
    /// </summary>
    public static bool TryDescribe(string id, out string size, out string color, out string shape)
    {
        size = color = shape = "";
        if (!id.StartsWith("LootFilter", StringComparison.Ordinal))
            return false;

        var rest = id["LootFilter".Length..];
        foreach (var candidate in (string[])["Large", "Medium", "Small"])
        {
            if (!rest.StartsWith(candidate, StringComparison.Ordinal))
                continue;
            size = candidate;
            rest = rest[candidate.Length..];
            break;
        }
        if (size.Length == 0)
            return false;

        foreach (var candidate in Colors)
        {
            if (!rest.StartsWith(candidate, StringComparison.Ordinal))
                continue;
            color = candidate;
            shape = rest[candidate.Length..];
            break;
        }
        return color.Length > 0 && Shapes.Contains(shape);
    }

    public static readonly string[] Colors =
        ["Blue", "Brown", "Cyan", "Green", "Grey", "Orange", "Pink", "Purple", "Red", "White", "Yellow"];

    public static readonly string[] Shapes =
    [
        "Circle", "Cross", "Diamond", "Hexagon", "Kite", "Moon",
        "Pentagon", "Raindrop", "Square", "Star", "Triangle", "UpsideDownHouse",
    ];

    private static int IndexOfSeparator(byte[] table)
    {
        for (var i = 4; i < table.Length - 8; i++)
        {
            var match = true;
            for (var j = 0; j < 8; j++)
                if (table[i + j] != 0xBB) { match = false; break; }
            if (match)
                return i;
        }
        throw new InvalidDataException("minimapicons.datc64 has no row/data separator.");
    }
}
