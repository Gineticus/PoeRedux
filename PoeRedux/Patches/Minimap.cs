using LibBundle3.Nodes;
using System.Text;

namespace PoeRedux.Patches;

public class Minimap : IPatch
{
    public string Name => "Minimap Patch";
    public string Description => "Reveals the entire minimap by default.";

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        if (GameTree.FindFile(root, "shaders/minimap_blending_pixel.hlsl") is { } blending)
            plan.Add(blending, PatchBlendingShader);

        if (GameTree.FindFile(root, "shaders/minimap_visibility_pixel.hlsl") is { } visibility)
            plan.Add(visibility, PatchVisibilityShader);
    }

    private static byte[]? PatchVisibilityShader(byte[] bytes)
    {
        string data = Encoding.ASCII.GetString(bytes);

        if (data.Contains("res_color = max(res_color, 0.18f);"))
        {
            return null;
        }

        List<string> lines = data.Split("\r\n").ToList();
        int index = lines.FindIndex(line => line.Contains("res_color = float4(1.0f, 0.0f, 0.0f, 1.0f);"));
        if (index == -1) return null;
        lines.Insert(index + 1, "\tres_color = max(res_color, 0.18f);");

        string newData = string.Join("\r\n", lines);
        return Encoding.ASCII.GetBytes(newData);
    }

    private static byte[] PatchBlendingShader(byte[] bytes)
    {
        string data = Encoding.ASCII.GetString(bytes);

        data = data.Replace("float4 walkable_color = float4(1.0f, 1.0f, 1.0f, 0.01f);", "float4 walkable_color = float4(0.0f, 0.0f, 0.0f, 0.3f);");
        data = data.Replace("float4 walkability_map_color = lerp(walkable_color, float4(0.5f, 0.5f, 1.0f, 0.5f), walkable_to_edge_ratio);", "float4 walkability_map_color = lerp(walkable_color, float4(12.0f, 12.0f, 12.0f, 0.1f), walkable_to_edge_ratio);");

        return Encoding.ASCII.GetBytes(data);
    }
}
