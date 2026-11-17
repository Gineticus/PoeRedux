using System.Buffers.Binary;
using System.Text;

namespace PoeRedux.Data;

/// <summary>
/// A .datc64 table: a row count, fixed-width rows, eight 0xBB bytes, then a data section that holds
/// strings and arrays. Strings are UTF-16 LE and end with four zero bytes; cells point at them with
/// offsets that count from the first 0xBB byte.
/// </summary>
public sealed class Dat64
{
    public const ulong NullKey = 0xFEFEFEFEFEFEFEFE;
    private static readonly byte[] Separator = [0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB];

    private readonly byte[] _bytes;

    public Dat64(byte[] bytes)
    {
        _bytes = bytes;
        if (bytes.Length < 12)
            throw new InvalidDataException("The table is too short.");
        RowCount = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        DataOffset = bytes.AsSpan(4).IndexOf(Separator);
        if (DataOffset < 0)
            throw new InvalidDataException("The table has no data section.");
        DataOffset += 4;
        RowWidth = RowCount == 0 ? 0 : (DataOffset - 4) / RowCount;
        if (4 + RowCount * RowWidth != DataOffset)
            throw new InvalidDataException("The rows do not fill the space before the data section.");
    }

    public int RowCount { get; }
    public int RowWidth { get; }

    /// <summary>Offset of the first 0xBB byte, where the data section starts.</summary>
    public int DataOffset { get; }

    public ReadOnlySpan<byte> Bytes => _bytes;

    public ReadOnlySpan<byte> Row(int index) => _bytes.AsSpan(4 + index * RowWidth, RowWidth);

    public int RowOffset(int index) => 4 + index * RowWidth;

    public int Int32(int row, int column) => BinaryPrimitives.ReadInt32LittleEndian(Row(row)[column..]);

    public ulong UInt64(int row, int column) => BinaryPrimitives.ReadUInt64LittleEndian(Row(row)[column..]);

    /// <summary>A 16-byte foreign key to another table's row, or null.</summary>
    public int? ForeignRow(int row, int column)
    {
        var key = UInt64(row, column);
        return key == NullKey ? null : checked((int)key);
    }

    public string String(int row, int column) => StringAt(UInt64(row, column));

    /// <summary>The string at <paramref name="offset"/> in the data section.</summary>
    public string StringAt(ulong offset)
    {
        var start = DataOffset + checked((int)offset);
        if (start < DataOffset || start > _bytes.Length)
            throw new InvalidDataException($"String offset {offset} is outside the data section.");
        var end = start;
        // Four zero bytes on a two-byte boundary end the string.
        while (end + 4 <= _bytes.Length && !(_bytes[end] == 0 && _bytes[end + 1] == 0 && _bytes[end + 2] == 0 && _bytes[end + 3] == 0))
            end += 2;
        if (end + 4 > _bytes.Length)
            throw new InvalidDataException($"The string at {offset} has no end marker.");
        return Encoding.Unicode.GetString(_bytes, start, end - start);
    }

    /// <summary>The elements of an array cell (a 64-bit count, then a 64-bit offset into the data section).</summary>
    public ReadOnlySpan<byte> Array(int row, int column, int elementSize, out int count)
    {
        var length = UInt64(row, column);
        var offset = UInt64(row, column + 8);
        if (length > 100_000)
            throw new InvalidDataException($"Array at row {row}, column {column} claims {length} elements.");
        count = (int)length;
        var start = DataOffset + checked((int)offset);
        return _bytes.AsSpan(start, count * elementSize);
    }

    public List<int> Int32Array(int row, int column)
    {
        var span = Array(row, column, 4, out var count);
        var list = new List<int>(count);
        for (var i = 0; i < count; i++)
            list.Add(BinaryPrimitives.ReadInt32LittleEndian(span[(i * 4)..]));
        return list;
    }

    public List<int> ForeignRowArray(int row, int column)
    {
        var span = Array(row, column, 16, out var count);
        var list = new List<int>(count);
        for (var i = 0; i < count; i++)
            list.Add(checked((int)BinaryPrimitives.ReadUInt64LittleEndian(span[(i * 16)..])));
        return list;
    }
}
