using System.IO;
using System.Text;
using System.Text.Json;

namespace PoeRedux.Services;

/// <summary>
/// Minimal reader for the 64-bit dat tables (.datc64). Layout comes from the bundled
/// schema, and the computed row width is checked against the file so a schema that has
/// drifted from the game fails loudly instead of returning plausible garbage.
/// </summary>
public sealed class DatFile
{
    private static readonly byte[] Separator = [0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB];

    private readonly byte[] _data;
    private readonly int _varOffset;
    private readonly Dictionary<string, Column> _columns;

    public int RowCount { get; }
    public int RowWidth { get; }

    private readonly record struct Column(string Type, bool Array, int Offset);

    public DatFile(byte[] data, DatSchema.Table table)
    {
        _data = data;
        RowCount = BitConverter.ToInt32(data, 0);
        if (RowCount <= 0)
            throw new InvalidDataException("dat file has no rows");

        _varOffset = IndexOfSeparator(data);
        if (_varOffset < 0)
            throw new InvalidDataException("dat file has no variable-data separator");

        RowWidth = (_varOffset - 4) / RowCount;
        var expected = 0;
        var columns = new Dictionary<string, Column>(StringComparer.Ordinal);
        foreach (var c in table.Columns)
        {
            if (c.Name is not null)
                columns[c.Name] = new(c.Type, c.Array, expected);
            expected += Width(c);
        }
        if (expected != RowWidth)
            throw new InvalidDataException(
                $"{table.Name}: schema says {expected} bytes per row, file has {RowWidth}. " +
                "The bundled dat schema is out of date for this game version.");
        _columns = columns;
    }

    private static int Width(DatSchema.Column c) => c.Array ? 16 : c.Type switch
    {
        "bool" or "i8" or "u8" => 1,
        "i16" or "u16" => 2,
        "i32" or "u32" or "f32" or "enumrow" => 4,
        "i64" or "u64" or "f64" or "string" or "row" => 8,
        "foreignrow" => 16,
        _ => throw new InvalidDataException("unknown dat column type " + c.Type)
    };

    private static int IndexOfSeparator(byte[] data)
    {
        for (var i = 4; i < data.Length - Separator.Length; i++)
        {
            var match = true;
            for (var j = 0; j < Separator.Length; j++)
                if (data[i + j] != Separator[j]) { match = false; break; }
            if (match)
                return i;
        }
        return -1;
    }

    /// <summary>Byte offset of a column within a row, for <see cref="DatWriter"/>.</summary>
    public int ColumnOffset(string column) => _columns.TryGetValue(column, out var c)
        ? c.Offset
        : throw new InvalidDataException("no such dat column: " + column);

    private int CellOffset(int row, string column) => 4 + row * RowWidth + ColumnOffset(column);

    public int Int32(int row, string column) => BitConverter.ToInt32(_data, CellOffset(row, column));

    /// <summary>Row index of a foreignrow or row reference, or null when unset.</summary>
    public int? Reference(int row, string column)
    {
        var raw = BitConverter.ToUInt64(_data, CellOffset(row, column));
        return raw >= 0xFEFEFEFEFEFEFE ? null : (int)raw;
    }

    public string String(int row, string column)
    {
        var start = _varOffset + (int)BitConverter.ToUInt64(_data, CellOffset(row, column));
        var end = start;
        while (end + 1 < _data.Length && !(_data[end] == 0 && _data[end + 1] == 0))
            end += 2;
        return Encoding.Unicode.GetString(_data, start, end - start);
    }

    public IEnumerable<int> Int32Array(int row, string column)
    {
        var at = CellOffset(row, column);
        var count = (int)BitConverter.ToUInt64(_data, at);
        var start = _varOffset + (int)BitConverter.ToUInt64(_data, at + 8);
        for (var i = 0; i < count; i++)
            yield return BitConverter.ToInt32(_data, start + i * 4);
    }

}

public sealed class DatSchema
{
    public sealed record Column(string? Name, string Type, bool Array);
    public sealed record Table(string Name, List<Column> Columns);

    public int Version { get; init; }
    public List<Table> Tables { get; init; } = [];

    public Table this[string name] =>
        Tables.FirstOrDefault(t => t.Name == name)
        ?? throw new InvalidDataException("no schema for table " + name);

    public static DatSchema Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "dat-schema.json");
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<DatSchema>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidDataException("dat schema is empty");
    }
}
