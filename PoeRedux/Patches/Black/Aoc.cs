using LibBundle3.Nodes;
using System.Text;
using System.Text.RegularExpressions;
using PoeRedux.Services;

namespace PoeRedux.Patches.Black;

public class Aoc : IPatch
{
    public string Name => "Aoc Patch";
    public object Description => "";

    private List<FileNode> fileNodes = [];

    private readonly string[] extensions = {
        ".aoc",
        ".ao",
    };

    private readonly HashSet<string> _clientKeep = new(StringComparer.Ordinal) {
        "ClientAnimationController",
        // "SoundEvents",
        "BoneGroups",
        // "AnimatedRender",
        // "FixedMesh",
        "SkinMesh",
    };

    private void CollectFileNodesRecursively(DirectoryNode dir)
    {
        foreach (var node in dir.Children)
        {
            switch (node)
            {
                case DirectoryNode childDir:
                    CollectFileNodesRecursively(childDir);
                    break;

                case FileNode fileNode:
                    if (HasTargetExtension(fileNode.Name))
                        fileNodes.Add(fileNode);
                    break;
            }
        }
    }

    // Advance past a string literal, comment, or bracketed region starting at index i.
    // Returns the index AFTER the construct, or i+1 if no special construct.
    private static int SkipSyntax(string text, int i)
    {
        char c = text[i];

        // String literal "..."
        if (c == '"')
        {
            for (int j = i + 1; j < text.Length; j++)
            {
                if (text[j] == '\\' && j + 1 < text.Length) { j++; continue; }
                if (text[j] == '"') return j + 1;
            }
            return text.Length;
        }

        // Line comment //
        if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
        {
            int nl = text.IndexOf('\n', i + 2);
            return nl < 0 ? text.Length : nl + 1;
        }

        // Block comment /* */
        if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
        {
            int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
            return end < 0 ? text.Length : end + 2;
        }

        // Bracketed region [...] (may contain nested braces/brackets)
        if (c == '[')
        {
            int depth = 1;
            int j = i + 1;
            while (j < text.Length && depth > 0)
            {
                char cj = text[j];
                if (cj == '"' || cj == '/' && j + 1 < text.Length && (text[j + 1] == '/' || text[j + 1] == '*'))
                {
                    j = SkipSyntax(text, j);
                    continue;
                }
                if (cj == '[') depth++;
                else if (cj == ']') depth--;
                j++;
            }
            return j;
        }

        return i + 1;
    }

    private static int FindMatchingBrace(string text, int openIndex)
    {
        int depth = 1;
        int i = openIndex + 1;
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '"' || c == '[' ||
                (c == '/' && i + 1 < text.Length && (text[i + 1] == '/' || text[i + 1] == '*')))
            {
                i = SkipSyntax(text, i);
                continue;
            }
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return i;
            }
            i++;
        }
        return -1;
    }

    // Finds the next top-level "name { ... }" sub-block starting at or after `from`.
    // Skips over strings, comments, and bracketed regions so identifiers inside
    // arrays like `foo = [ Bar { ... } ]` aren't treated as sub-blocks.
    private static bool FindNextSubBlock(string text, int from, out int nameStart, out string name, out int openBrace)
    {
        int i = from;
        while (i < text.Length)
        {
            char c = text[i];

            // Skip strings, comments, bracketed regions entirely.
            if (c == '"' || c == '[' ||
                (c == '/' && i + 1 < text.Length && (text[i + 1] == '/' || text[i + 1] == '*')))
            {
                i = SkipSyntax(text, i);
                continue;
            }

            // Identifier start
            if (char.IsLetter(c) || c == '_')
            {
                int idStart = i;
                int j = i + 1;
                while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] == '_'))
                    j++;

                // Skip whitespace after identifier
                int k = j;
                while (k < text.Length && char.IsWhiteSpace(text[k])) k++;

                if (k < text.Length && text[k] == '{')
                {
                    nameStart = idStart;
                    name = text[idStart..j];
                    openBrace = k;
                    return true;
                }

                i = j;
                continue;
            }

            i++;
        }

        nameStart = -1;
        name = string.Empty;
        openBrace = -1;
        return false;
    }

    private static string StripClientBlocks(string data, HashSet<string> keepSet)
    {
        // Find the top-level "client" block (must not be inside a string/comment/bracket).
        if (!FindTopLevelBlock(data, "client", out int clientOpenBrace))
            return data;

        int clientCloseBrace = FindMatchingBrace(data, clientOpenBrace);
        if (clientCloseBrace < 0)
            return data;

        string clientBody = data.Substring(clientOpenBrace + 1, clientCloseBrace - clientOpenBrace - 1);

        var result = new StringBuilder();
        int pos = 0;
        while (pos < clientBody.Length)
        {
            if (!FindNextSubBlock(clientBody, pos, out int nameStart, out string blockName, out int blockOpenBrace))
            {
                result.Append(clientBody.AsSpan(pos));
                break;
            }

            int blockCloseBrace = FindMatchingBrace(clientBody, blockOpenBrace);
            if (blockCloseBrace < 0)
            {
                // Unbalanced — bail out, preserve remainder verbatim.
                result.Append(clientBody.AsSpan(pos));
                break;
            }

            int blockEnd = blockCloseBrace + 1;

            // Append text before this block (whitespace/comments/etc.)
            result.Append(clientBody.AsSpan(pos, nameStart - pos));

            if (keepSet.Contains(blockName))
            {
                result.Append(clientBody.AsSpan(nameStart, blockEnd - nameStart));
            }
            else
            {
                result.Append($"{blockName} {{}}");
            }

            pos = blockEnd;
        }

        return string.Concat(
            data.AsSpan(0, clientOpenBrace + 1),
            result.ToString(),
            data.AsSpan(clientCloseBrace));
    }

    // Locates a top-level block by name, returning the index of its opening '{'.
    private static bool FindTopLevelBlock(string data, string blockName, out int openBrace)
    {
        int pos = 0;
        while (FindNextSubBlock(data, pos, out _, out string name, out int ob))
        {
            if (name == blockName)
            {
                openBrace = ob;
                return true;
            }
            int close = FindMatchingBrace(data, ob);
            if (close < 0) break;
            pos = close + 1;
        }
        openBrace = -1;
        return false;
    }

    private void TryPatchFile(FileNode file)
    {
        var record = file.Record;
        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());

        if (string.IsNullOrEmpty(data))
            return;

        data = StripClientBlocks(data, _clientKeep);

        var newBytes = Encoding.Unicode.GetBytes(data);
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
    }

    private bool HasTargetExtension(string fileName) =>
        extensions.Any(ext =>
            fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

    private static DirectoryNode? NavigateTo(DirectoryNode root, params string[] path)
    {
        DirectoryNode current = root;
        foreach (var name in path)
        {
            var next = current.Children.OfType<DirectoryNode>().FirstOrDefault(d => d.Name == name);
            if (next is null) return null;
            current = next;
        }
        return current;
    }

    public void Apply(DirectoryNode root)
    {
        if (root is not null)
            CollectFileNodesRecursively(root);

        foreach (var file in fileNodes)
        {
            TryPatchFile(file);
        }
    }
}