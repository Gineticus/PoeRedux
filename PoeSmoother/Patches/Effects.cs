using LibBundle3.Nodes;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeSmoother.Patches;

public partial class Effects : IPatch
{
    public string Name => "Effects Patch";
    public object Description => "Disables all effects in the game.";

    private List<FileNode> fileNodes = [];

    private readonly string[] extensions = {
        ".aoc",
        ".ao",
    };

    private readonly HashSet<string> _clientKeep = new(StringComparer.Ordinal) {
        "ClientAnimationController",
        "SoundEvents",
        "BoneGroups",
        "AnimatedRender",
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
    
    private static int FindMatchingBrace(string text, int openIndex)
    {
        int depth = 1;
        for (int i = openIndex + 1; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') depth--;
            if (depth == 0) return i;
        }
        return -1;
    }

    private static string StripClientBlocks(string data, HashSet<string> keepSet)
    {
        // Find the top-level "client" block
        var clientMatch = ClientBlockRegex().Match(data);
        if (!clientMatch.Success)
            return data;

        int clientOpenBrace = data.IndexOf('{', clientMatch.Index + clientMatch.Length - 1);
        if (clientOpenBrace < 0)
            return data;

        int clientCloseBrace = FindMatchingBrace(data, clientOpenBrace);
        if (clientCloseBrace < 0)
            return data;

        string clientBody = data.Substring(clientOpenBrace + 1, clientCloseBrace - clientOpenBrace - 1);

        // Extract sub-blocks: name followed by { ... }
        var result = new StringBuilder();
        int pos = 0;
        while (pos < clientBody.Length)
        {
            var blockMatch = SubBlockRegex().Match(clientBody, pos);
            if (!blockMatch.Success)
            {
                // Append remaining whitespace/newlines
                result.Append(clientBody.AsSpan(pos));
                break;
            }

            // Append text before this block (whitespace/newlines)
            string beforeBlock = clientBody[pos..blockMatch.Index];

            string blockName = blockMatch.Groups[1].Value;
            int blockOpenBrace = clientBody.IndexOf('{', blockMatch.Index + blockMatch.Length - 1);
            if (blockOpenBrace < 0) break;

            int blockCloseBrace = FindMatchingBrace(clientBody, blockOpenBrace);
            if (blockCloseBrace < 0) break;

            int blockEnd = blockCloseBrace + 1;

            if (keepSet.Contains(blockName))
            {
                result.Append(beforeBlock);
                result.Append(clientBody.AsSpan(blockMatch.Index, blockEnd - blockMatch.Index));
            }

            pos = blockEnd;
        }

        // Rebuild the full data with the filtered client block
        return string.Concat(
            data.AsSpan(0, clientOpenBrace + 1),
            result.ToString(),
            data.AsSpan(clientCloseBrace));
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
        var dir = NavigateTo(root, "metadata", "effects", "spells");
        if (dir is not null)
            CollectFileNodesRecursively(dir);

        foreach (var file in fileNodes)
        {
            TryPatchFile(file);
        }
    }

    [GeneratedRegex(@"(?:^|\n)\s*client\s*\{", RegexOptions.Singleline)]
    private static partial Regex ClientBlockRegex();

    [GeneratedRegex(@"(\w+)\s*\{", RegexOptions.Singleline)]
    private static partial Regex SubBlockRegex();
}