using LibBundle3.Nodes;
using PoeRedux.Helpers;
using PoeRedux.Models;
using System.Buffers.Binary;
using System.Text;

namespace PoeRedux.Patches;

/// <summary>
/// Shortens the line above each mod in the advanced item description ("Prefix Modifier "Name"" becomes
/// "P "Name""). Rewrites three rows of the English client strings table. Checked on 18 Nov 2026:
/// <list type="bullet">
/// <item>PoE 1 has data/clientstrings.datc64 (UTF-16 strings) and data/clientstrings.datcl64 (UTF-32 strings),
/// PoE 2 has data/balance/clientstrings.datc64; each row has the id at byte 0 and the text at byte 8.</item>
/// <item>Every string in these tables ends with four zero bytes.</item>
/// </list>
/// New strings are appended to the data section and the row points at them; every other byte stays where it was.
/// </summary>
public class CompactModLines(PoeGame game) : IPatch
{
    private static readonly Dictionary<string, string> Replacements = new(StringComparer.Ordinal)
    {
        ["ModDescriptionLinePrefix"] = "P \"{0}\"",
        ["ModDescriptionLineSuffix"] = "S \"{0}\"",
        ["ModDescriptionLineCrafted"] = "Crafted {0}",
    };

    public string Id => "compact-mod-lines";
    public string Name => "Compact Mod Lines";
    public string Description => "Shortens the Prefix, Suffix and Crafted lines of advanced mod descriptions.";
    public PatchGroup Group => PatchGroup.Readability;

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        var dir = game == PoeGame.PoE2 ? "data/balance/" : "data/";
        if (GameTree.FindFile(root, dir + "clientstrings.datc64") is { } utf16)
            plan.Add(utf16, bytes => Rewrite(bytes, Encoding.Unicode, 2));
        if (GameTree.FindFile(root, dir + "clientstrings.datcl64") is { } utf32)
            plan.Add(utf32, bytes => Rewrite(bytes, Encoding.UTF32, 4));
    }

    /// <summary>The table with the three rows pointing at new strings, or null when none of them is there.</summary>
    internal static byte[]? Rewrite(byte[] table, Encoding encoding, int charSize)
    {
        var reader = new StringTable(table, encoding, charSize);
        var appended = new MemoryStream();
        var changes = new List<(int Row, ulong Offset)>();
        var dataLength = (ulong)(table.Length - reader.DataOffset);
        for (var row = 0; row < reader.RowCount; row++)
        {
            if (!Replacements.TryGetValue(reader.String(row, 0), out var text) || reader.String(row, 8) == text)
                continue;
            changes.Add((row, dataLength + (ulong)appended.Length));
            appended.Write(encoding.GetBytes(text));
            appended.Write(new byte[4]);
        }
        if (changes.Count == 0)
            return null;

        var result = new byte[table.Length + appended.Length];
        table.CopyTo(result, 0);
        appended.ToArray().CopyTo(result, table.Length);
        foreach (var (row, offset) in changes)
            BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(reader.RowOffset(row) + 8), offset);

        // Read the new table back: every row must give the same id and text as before, except the new texts.
        var check = new StringTable(result, encoding, charSize);
        for (var row = 0; row < reader.RowCount; row++)
        {
            var id = reader.String(row, 0);
            var expected = changes.Any(c => c.Row == row) ? Replacements[id] : reader.String(row, 8);
            if (check.String(row, 0) != id || check.String(row, 8) != expected)
                throw new InvalidDataException($"Rewriting the client strings would change row {row} ({id}).");
        }
        return result;
    }

    /// <summary>The id and text columns of a client strings table.</summary>
    private sealed class StringTable
    {
        private readonly byte[] _bytes;
        private readonly Encoding _encoding;
        private readonly int _charSize;

        public StringTable(byte[] bytes, Encoding encoding, int charSize)
        {
            _bytes = bytes;
            _encoding = encoding;
            _charSize = charSize;
            RowCount = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            DataOffset = bytes.AsSpan(4).IndexOf(new byte[] { 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB }) + 4;
            if (DataOffset < 4 || RowCount <= 0 || (DataOffset - 4) % RowCount != 0)
                throw new InvalidDataException("The client strings table does not have the expected layout.");
            RowWidth = (DataOffset - 4) / RowCount;
            if (RowWidth < 16)
                throw new InvalidDataException("The client strings rows are too short for an id and a text.");
        }

        public int RowCount { get; }
        public int RowWidth { get; }
        public int DataOffset { get; }

        public int RowOffset(int row) => 4 + row * RowWidth;

        public string String(int row, int column)
        {
            var start = DataOffset + checked((int)BinaryPrimitives.ReadUInt64LittleEndian(_bytes.AsSpan(RowOffset(row) + column)));
            var end = start;
            while (end + 4 <= _bytes.Length && BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(end)) != 0)
                end += _charSize;
            if (end + 4 > _bytes.Length)
                throw new InvalidDataException($"Row {row} has a string without an end.");
            return _encoding.GetString(_bytes, start, end - start);
        }
    }
}
