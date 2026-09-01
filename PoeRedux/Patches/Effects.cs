using LibBundle3.Nodes;
using System.Text;
using System.Text.RegularExpressions;
using PoeRedux.Services;

namespace PoeRedux.Patches;

public class Effects : IPatch, ISelectiveSweep
{
    public string Name => "Effects Patch";
    public object Description =>
        "Strips spell effect visuals. Sound and animation timing stay, and effects you need to see to survive are kept.";

    private List<FileNode> fileNodes = [];

    public IReadOnlyList<string> Extensions { get; } = [
        ".aoc",
        ".ao",
    ];

    internal static readonly HashSet<string> ClientKeep = new(StringComparer.Ordinal) {
        "ClientAnimationController",
        "SoundEvents",
        "BoneGroups",
        "AnimatedRender",
        "SkinMesh",
    };

    public HashSet<string> DisabledPrefixes { get; } = new(StringComparer.Ordinal) {
    // expedition effects
        "metadata/effects/spells/monsters_effects/league_expedition/dynamic_marker",
    // world bosses effects
        "metadata/effects/spells/monsters_effects/atlasofworldsbosses",
    // affliction effects
        "metadata/effects/spells/monsters_effects/league_azmeri/guiding_light",
        "metadata/effects/spells/monsters_effects/league_azmeri/monster_fx",
        "metadata/effects/spells/monsters_effects/league_azmeri/resources/affecting_area",
        "metadata/effects/spells/monsters_effects/league_azmeri/resources/feature_room_dust",
        "metadata/effects/spells/monsters_effects/league_azmeri/resources/guiding_light",
        "metadata/effects/spells/monsters_effects/league_azmeri/resources/wisp_doodads",
    // legion effects
        "metadata/effects/spells/monsters_effects/league_legion/rewardsystem",
    // blight effects
        "metadata/effects/spells/monsters_effects/league_blight/rewardsystem",
    // ultimatum effects
        "metadata/effects/spells/monsters_effects/league_archnemesis",
        "metadata/effects/spells/monsters_effects/league_ritual/cold_ritual",
        "metadata/effects/spells/monsters_effects/league_ultimatum/mechanics/fx/arena_limit.pet",
    // sanctum effects
        "metadata/effects/spells/monsters_effects/league_sanctum",
        "metadata/effects/spells/monsters_effects/league_hellscape/mechanics",
    // maven effects
        "metadata/effects/spells/monsters_effects/atlasofworldsbosses/maven",
    // drox, the warlord effects
        "metadata/effects/spells/monsters_effects/atlasexiles/adjudicator",
        // "metadata/effects/spells/monsters_effects/atlasexiles/adjudicatormonsters",
    // guardian of the chimera effects
        "metadata/effects/spells/ground_effects/chimera_smoke",
        "metadata/effects/spells/ground_effects/evil",
        "metadata/effects/spells/ground_effects_v2/smoke_blind_chimera",
        "metadata/effects/spells/monsters_effects/atlasofworldsbosses/chimera",
    // sirus, awakener of worlds effects
        "metadata/effects/spells/monsters_effects/atlasexiles/orion",
    // prophecy effects
        "metadata/effects/spells/monsters_effects/prophecy_league",
    // deadly ground effects
        "metadata/effects/spells/ground_effects/caustic",
        "metadata/effects/spells/ground_effects_v2/caustic_arrow_ground",
        "metadata/effects/spells/ground_effects_v2/desecrated",
        "metadata/effects/spells/ground_effects_v2/desecrated_maligaro",
        "metadata/effects/spells/ground_effects_v2/desecrated_red",
        "metadata/effects/spells/ground_effects_v3/caustic",
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

            if (c == '"' || c == '[' ||
                (c == '/' && i + 1 < text.Length && (text[i + 1] == '/' || text[i + 1] == '*')))
            {
                i = SkipSyntax(text, i);
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int idStart = i;
                int j = i + 1;
                while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] == '_'))
                    j++;

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

    internal static string StripClientBlocks(string data, HashSet<string> keepSet)
    {
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
            // Effects.cs intentionally drops non-kept sub-blocks entirely (no empty stub).

            pos = blockEnd;
        }

        return string.Concat(
            data.AsSpan(0, clientOpenBrace + 1),
            result.ToString(),
            data.AsSpan(clientCloseBrace));
    }

    private bool TryPatchFile(FileNode file)
    {
        var record = file.Record;

        if (DisabledPrefixes.Any(p => record.Path.Replace('\\', '/').StartsWith(p, StringComparison.Ordinal)))
            return false;

        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());

        if (string.IsNullOrEmpty(data))
            return false;

        var patched = StripClientBlocks(data, ClientKeep);
        if (patched == data)
            return false;
        data = patched;

        var newBytes = Encoding.Unicode.GetBytes(data);
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
        return true;
    }

    private bool HasTargetExtension(string fileName) =>
        Extensions.Any(ext =>
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
        if (dir is null)
        {
            PatchReport.FileMissing(Scope);
            return;
        }
        CollectFileNodesRecursively(dir);

        var changed = 0;
        foreach (var file in fileNodes)
            if (TryPatchFile(file)) changed++;

        if (changed == 0)
            PatchReport.NoMatch(Scope, "a client block with a strippable sub block");
    }

    public string Root => Scope;

    private const string Scope = "metadata/effects/spells";
}