using LibBundle3.Nodes;
using PoeRedux.Helpers;

namespace PoeRedux.Patches;

public class Delirium : IPatch
{
    public string Name => "Delirium Patch";
    public string Description => "Disables the delirium effects in the game.";

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        var transform = Utf16.Edit(Edit);
        foreach (var file in GameTree.FindFiles(GameTree.FindDirectory(root, "metadata/effects/environment/league_affliction"), ".ao", ".aoc"))
            plan.Add(file, transform);
    }

    // Swaps the delirium object for a stripped-down one with the same parent.
    private static string Edit(string data)
    {
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

        return data;
    }
}
