using System.Text;

using PoeRedux.Services;

namespace PoeRedux.Tests;

public class ChangeDiffTests
{
    [Fact]
    public void BinaryBytes_ReportedAsByteCountChange_NotLineNoise()
    {
        // A .datc64-shaped header: row count then raw binary, no valid text encoding.
        byte[] original = [0x03, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB];
        byte[] current = [.. original, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE];

        var result = ChangeDiff.Format(original, current);

        Assert.Contains("binary", result);
        Assert.Contains(original.Length.ToString("N0"), result);
        Assert.Contains(current.Length.ToString("N0"), result);
        Assert.DoesNotContain("+", result.Replace("(binary", ""));
    }

    [Fact]
    public void Utf16WithBom_DecodesAndDiffsCorrectly()
    {
        var original = Utf16Bom("line one\r\nold text\r\nline three");
        var current = Utf16Bom("line one\r\nnew text\r\nline three");

        var result = ChangeDiff.Format(original, current);

        Assert.Contains("- old text", result);
        Assert.Contains("+ new text", result);
        Assert.DoesNotContain("line one", result); // unchanged lines are not shown
    }

    [Fact]
    public void Utf8Ascii_DecodesAndDiffsCorrectly()
    {
        var original = Encoding.UTF8.GetBytes("float a = 1.0;\r\nfloat b = 2.0;");
        var current = Encoding.UTF8.GetBytes("float a = 1.5;\r\nfloat b = 2.0;");

        var result = ChangeDiff.Format(original, current);

        Assert.Contains("- float a = 1.0;", result);
        Assert.Contains("+ float a = 1.5;", result);
    }

    [Fact]
    public void IdenticalText_ProducesNoOutputLines()
    {
        var bytes = Encoding.UTF8.GetBytes("same\r\ntext");
        var result = ChangeDiff.Format(bytes, bytes);
        Assert.Equal("", result.Trim());
    }

    [Fact]
    public void TryDecodeText_RejectsBinaryControlCharacters()
    {
        byte[] binary = [0x01, 0x02, 0x00, 0x00, 0x03, 0x04];
        Assert.False(ChangeDiff.TryDecodeText(binary, out _));
    }

    [Fact]
    public void TryDecodeText_AcceptsPlainAsciiText()
    {
        var bytes = Encoding.UTF8.GetBytes("hello\r\nworld");
        Assert.True(ChangeDiff.TryDecodeText(bytes, out var text));
        Assert.Equal("hello\r\nworld", text);
    }

    private static byte[] Utf16Bom(string text) =>
        [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)];
}
