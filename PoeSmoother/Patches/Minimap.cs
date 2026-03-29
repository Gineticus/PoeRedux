using LibBundle3.Nodes;

namespace PoeSmoother.Patches;

public class Minimap : IPatch
{
    public string Name => "Minimap Patch";
    public object Description => "Reveals the entire minimap by default.";

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
        var shaders = NavigateTo(root, "shaders");
        if (shaders is null)
            return;

        foreach (var f in shaders.Children)
        {
            if (f is FileNode file1 && file1.Name == "minimap_visibility_pixel.hlsl")
            {
                var record = file1.Record;
                var bytes = record.Read();
                string data = System.Text.Encoding.ASCII.GetString(bytes.ToArray());

                if (data.Contains("res_color = max(res_color, 0.18f);"))
                {
                    continue;
                }

                List<string> lines = data.Split("\r\n").ToList();
                int index = lines.FindIndex(line => line.Contains("res_color = float4(1.0f, 0.0f, 0.0f, 1.0f);"));
                if (index == -1) continue;
                lines.Insert(index + 1, $"\tres_color = max(res_color, 0.18f);");

                string newData = string.Join("\r\n", lines);
                var newBytes = System.Text.Encoding.ASCII.GetBytes(newData);
                record.Write(newBytes);
            }
            
            if (f is FileNode file2 && file2.Name == "minimap_blending_pixel.hlsl")
            {
                var record = file2.Record;
                var bytes = record.Read();
                string data = System.Text.Encoding.ASCII.GetString(bytes.ToArray());

                data = data.Replace("float4 walkable_color = float4(1.0f, 1.0f, 1.0f, 0.01f);", "float4 walkable_color = float4(0.0f, 0.0f, 0.0f, 0.3f);");
                data = data.Replace("float4 walkability_map_color = lerp(walkable_color, float4(0.5f, 0.5f, 1.0f, 0.5f), walkable_to_edge_ratio);", "float4 walkability_map_color = lerp(walkable_color, float4(12.0f, 12.0f, 12.0f, 0.1f), walkable_to_edge_ratio);");
                
                var newBytes = System.Text.Encoding.ASCII.GetBytes(data);
                record.Write(newBytes);
            }
        }
    }
}
