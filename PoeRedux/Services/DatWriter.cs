using System.IO;
using System.Text;

namespace PoeRedux.Services;

/// <summary>
/// Rewrites one string cell of a 64-bit dat table (.datc64). The new bytes are appended to the
/// end of the variable-data heap and the row's 8-byte offset cell is pointed at them; the fixed
/// section and every other row's heap entry are left exactly where they were, so nothing else in
/// the file has to move.
/// </summary>
public static class DatWriter
{
    private static readonly byte[] Separator = [0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB];

    /// <summary>
    /// Returns new file bytes with <paramref name="column"/> on <paramref name="row"/> set to
    /// <paramref name="value"/>. <paramref name="rowWidth"/> and the column's byte offset within
    /// a row come from the same schema-driven read <see cref="DatFile"/> already validated.
    /// </summary>
    public static byte[] SetString(byte[] data, int rowWidth, int row, int columnOffset, string value)
    {
        var varOffset = IndexOfSeparator(data);
        var cellOffset = 4 + row * rowWidth + columnOffset;

        var newHeapBytes = Encoding.Unicode.GetBytes(value + "\0");
        var newHeapOffset = (ulong)(data.Length - varOffset);

        var result = new byte[data.Length + newHeapBytes.Length];
        Buffer.BlockCopy(data, 0, result, 0, data.Length);
        Buffer.BlockCopy(newHeapBytes, 0, result, data.Length, newHeapBytes.Length);
        BitConverter.GetBytes(newHeapOffset).CopyTo(result, cellOffset);
        return result;
    }

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
        throw new InvalidDataException("dat file has no variable-data separator");
    }
}
