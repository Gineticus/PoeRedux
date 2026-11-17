using LibBundle3.Nodes;
using PoeRedux.Helpers;
using System.Text;

namespace PoeRedux.Patches;

/// <summary>
/// Turns off full-screen filters in shaders/postprocessuber.hlsl. Each filter runs under an "if" on its own
/// flag; the patch replaces the flag with 0. Each "if" occurs exactly once in both games (checked 18 Nov 2026).
/// </summary>
public class PostProcess : SwitchPatch
{
    public override string Id => "post-process";
    public override string Name => "Screen Filters";
    public override string Description => "Turns off the dark screen edges, background blur and gray-out.";
    public override PatchGroup Group => PatchGroup.VisualNoise;

    public override IReadOnlyList<PatchSwitch> Switches { get; } =
    [
        new("vignette", "Dark screen edges", t => t.Replace("if( vignette_enable )", "if( 0 )", StringComparison.Ordinal)),
        new("depth_of_field", "Background blur", t => t.Replace("if ( dof_enable )", "if ( 0 )", StringComparison.Ordinal)),
        new("desaturation", "Gray-out on low life or when chilled", t => t.Replace("if( desaturation_enable )", "if( 0 )", StringComparison.Ordinal)),
    ];

    public override void Collect(DirectoryNode root, PatchPlan plan)
    {
        if (!OnSwitches.Any() || GameTree.FindFile(root, "shaders/postprocessuber.hlsl") is not { } shader)
            return;
        plan.Add(shader, bytes => Encoding.ASCII.GetBytes(ApplySwitches(Encoding.ASCII.GetString(bytes))));
    }
}
