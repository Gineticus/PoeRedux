using LibBundle3.Nodes;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

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

    private readonly HashSet<string> _pathProtect = new(StringComparer.Ordinal) {
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

        if (_pathProtect.Any(p => record.Path.Replace('\\', '/').StartsWith(p, StringComparison.Ordinal)))
            return;

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