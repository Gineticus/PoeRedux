using LibBundle3.Nodes;
using System.Text;

namespace PoeSmoother.Patches;

public class Delirium : IPatch
{
    public string Name => "Delirium Patch";
    public object Description => "Disables the delirium effects in the game.";

    private List<FileNode> fileNodes = [];

    private readonly string[] extensions = {
        ".ao",
        ".aoc",
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

    private void TryPatchFile(FileNode file)
    {
        var record = file.Record;
        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());

        if (string.IsNullOrEmpty(data))
            return;

        if (data.Contains("Metadata/FmtParent") && !data.Contains("AnimatedRender"))
        {
            data = "version 3\nextends \"Metadata/FmtParent\"";
        }
        else if (data.Contains("Metadata/FmtParent") && data.Contains("AnimatedRender"))
        {
            data = "version 3\nextends \"Metadata/FmtParent\"\n\nclient\n{\n\tAnimatedRender\n\t{\n\t\tcannot_be_disabled = true\n\t}\n}";
        }
        else if (data.Contains("Metadata/Parent"))
        {
            data = @"version 3
extends ""Metadata/Parent""

BaseAnimationEvents
{
}

AnimationController
{
	metadata = ""Art/Models/Effects/enviro_effects/weather_attachments/generic_rig/weather_rig.amd""
}

client
{
    ClientAnimationController
    {
        skeleton = ""Art/Models/Effects/enviro_effects/weather_attachments/generic_rig/weather_rig.ast""
    }

    BoneGroups
    {
        bone_group = ""box false aux_box1 aux_box2 aux_box3 ""
    }
}";
        }

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
        var dir = NavigateTo(root, "metadata", "effects", "environment", "league_affliction");
        if (dir is not null)
            CollectFileNodesRecursively(dir);

        foreach (var file in fileNodes)
        {
            TryPatchFile(file);
        }
    }
}