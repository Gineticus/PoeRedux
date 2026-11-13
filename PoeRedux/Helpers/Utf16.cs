using PoeRedux.Patches;
using System.Text;

namespace PoeRedux.Helpers;

/// <summary>
/// Helpers for game text files stored as UTF-16 LE.
/// </summary>
internal static class Utf16
{
    private static readonly byte[] Bom = Encoding.Unicode.GetPreamble();

    /// <summary>
    /// Encodes <paramref name="text"/> with a byte order mark in front, unless the text already starts with one.
    /// </summary>
    public static byte[] WithBom(string text)
    {
        var bytes = Encoding.Unicode.GetBytes(text);
        return bytes.AsSpan().StartsWith(Bom) ? bytes : [.. Bom, .. bytes];
    }

    /// <summary>
    /// A transform that passes the text of a file to <paramref name="edit"/> and saves the result with a byte order mark.
    /// Empty files, and files for which <paramref name="edit"/> returns <see langword="null"/>, stay as they are.
    /// </summary>
    public static FileTransform Edit(Func<string, string?> edit) => bytes =>
    {
        var text = Encoding.Unicode.GetString(bytes);
        return text.Length > 0 && edit(text) is { } result ? WithBom(result) : null;
    };
}
