using LibBundle3.Nodes;
using System.Text;

namespace PoeRedux.Patches.Black;

public class Env : IPatch
{
    public string Name => "Env Patch";
    public string Description => "";

    // Settings whose { ... } value is emptied.
    private static readonly string[] Sections =
    [
        "\"player_light\":",
        "\"environment_mapping\":",
        "\"fog\":",
        "\"screenspace_fog\":",
        "\"area\":",
        "\"water\":",
        "\"post_transform\":",
        "\"audio\":",
        "\"global_illumination\":",
        "\"effect_spawner\":",
        "\"post_processing\":",
    ];

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        foreach (var file in GameTree.FindFiles(root, ".env"))
            plan.Add(file, bytes => Utf16.WithBom(Edit(Encoding.Unicode.GetString(bytes))));
    }

    private static string Edit(string text)
    {
        text = EnvironmentSettings.DisableShadows(text);
        foreach (var section in Sections)
            text = Braces.ReplaceBlock(text, text.IndexOf(section, StringComparison.Ordinal), "{}");
        text = EnvironmentSettings.SetToZero(text, "clouds_intensity");
        return EnvironmentSettings.SetToZero(text, "rain_intensity");
    }
}
