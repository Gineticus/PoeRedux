using System.Text;

using PoeRedux.Services;

namespace PoeRedux.Tests;

public class DatWriterTests
{
    /// <summary>
    /// Builds a synthetic 3-row, 2-column (Id string, Text string) dat table, mirroring
    /// clientstrings.datc64's layout: [rowcount][fixed rows of 8-byte heap offsets][0xBB
    /// separator][heap]. No real game data, so the round trip is provable without the ggpk.
    /// </summary>
    private static (byte[] Data, int RowWidth) BuildTable(params (string Id, string Text)[] rows)
    {
        const int rowWidth = 16; // two 8-byte string cells
        const int separatorLength = 8;
        var heap = new List<byte>();
        var heapOffsets = new List<(ulong Id, ulong Text)>();

        // Offsets are relative to the start of the separator, not the start of the heap, so the
        // first string in the heap sits at relative offset 8, matching DatFile/DatWriter's own
        // "_varOffset + offset" convention.
        foreach (var (id, text) in rows)
        {
            var idOffset = (ulong)(separatorLength + heap.Count);
            heap.AddRange(Encoding.Unicode.GetBytes(id + "\0"));
            var textOffset = (ulong)(separatorLength + heap.Count);
            heap.AddRange(Encoding.Unicode.GetBytes(text + "\0"));
            heapOffsets.Add((idOffset, textOffset));
        }

        var data = new List<byte>();
        data.AddRange(BitConverter.GetBytes(rows.Length));
        foreach (var (idOffset, textOffset) in heapOffsets)
        {
            data.AddRange(BitConverter.GetBytes(idOffset));
            data.AddRange(BitConverter.GetBytes(textOffset));
        }
        data.AddRange(Enumerable.Repeat((byte)0xBB, 8));
        data.AddRange(heap);
        return (data.ToArray(), rowWidth);
    }

    private static string ReadString(byte[] data, int rowWidth, int row, int columnOffset)
    {
        var separator = IndexOfSeparator(data);
        var cellOffset = 4 + row * rowWidth + columnOffset;
        var offset = separator + (int)BitConverter.ToUInt64(data, cellOffset);
        var end = offset;
        while (end + 1 < data.Length && !(data[end] == 0 && data[end + 1] == 0))
            end += 2;
        return Encoding.Unicode.GetString(data, offset, end - offset);
    }

    private static int IndexOfSeparator(byte[] data)
    {
        for (var i = 4; i < data.Length - 8; i++)
        {
            var match = true;
            for (var j = 0; j < 8; j++)
                if (data[i + j] != 0xBB) { match = false; break; }
            if (match)
                return i;
        }
        throw new InvalidDataException("no separator");
    }

    [Fact]
    public void SetString_ChangesOnlyTheTargetCell()
    {
        var (data, rowWidth) = BuildTable(
            ("RowA", "original a"),
            ("RowB", "original b"),
            ("RowC", "original c"));

        var updated = DatWriter.SetString(data, rowWidth, row: 1, columnOffset: 8, "compact b");

        Assert.Equal("original a", ReadString(updated, rowWidth, 0, 8));
        Assert.Equal("compact b", ReadString(updated, rowWidth, 1, 8));
        Assert.Equal("original c", ReadString(updated, rowWidth, 2, 8));

        Assert.Equal("RowA", ReadString(updated, rowWidth, 0, 0));
        Assert.Equal("RowB", ReadString(updated, rowWidth, 1, 0));
        Assert.Equal("RowC", ReadString(updated, rowWidth, 2, 0));
    }

    [Fact]
    public void SetString_RowCountAndRowWidthAreUnchanged()
    {
        var (data, rowWidth) = BuildTable(("Only", "value"));

        var updated = DatWriter.SetString(data, rowWidth, row: 0, columnOffset: 8, "much longer replacement text than the original");

        Assert.Equal(BitConverter.ToInt32(data, 0), BitConverter.ToInt32(updated, 0));
        // The fixed section is untouched: same bytes for the first rowWidth+4 bytes as before,
        // aside from the one 8-byte offset cell SetString targeted.
        for (var i = 4; i < 4 + rowWidth; i++)
        {
            if (i >= 4 + 8 && i < 4 + 16) continue; // the Text cell itself, expected to change
            Assert.Equal(data[i], updated[i]);
        }
    }

    [Fact]
    public void SetString_GrowsTheFileByTheNewStringLength()
    {
        var (data, rowWidth) = BuildTable(("Id", "short"));
        var replacement = "a considerably longer replacement value";

        var updated = DatWriter.SetString(data, rowWidth, row: 0, columnOffset: 8, replacement);

        var expectedGrowth = Encoding.Unicode.GetByteCount(replacement + "\0");
        Assert.Equal(data.Length + expectedGrowth, updated.Length);
    }

    [Fact]
    public void SetString_AppliedTwice_SecondWriteStillReadsCorrectly()
    {
        var (data, rowWidth) = BuildTable(("Id", "v1"));

        var once = DatWriter.SetString(data, rowWidth, row: 0, columnOffset: 8, "v2");
        var twice = DatWriter.SetString(once, rowWidth, row: 0, columnOffset: 8, "v3");

        Assert.Equal("v3", ReadString(twice, rowWidth, 0, 8));
        Assert.Equal("Id", ReadString(twice, rowWidth, 0, 0));
    }
}
