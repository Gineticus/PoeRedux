using LibBundle3.Nodes;
using System.Text;

namespace PoeRedux.Patches;

public class AtlasFog : IPatch
{
    public string Name => "Atlas Fog Patch";
    public object Description => "Removes fog from the Atlas.";

    private string ReplaceArrayProperty(string data, string propertyName)
    {
        string searchPattern = $"\"{propertyName}\":";
        int index = data.IndexOf(searchPattern);
        
        if (index < 0) return data;
        
        int bracketStart = data.IndexOf('[', index);
        if (bracketStart < 0) return data;
        
        int bracketCount = 1;
        int i = bracketStart + 1;
        
        while (i < data.Length && bracketCount > 0)
        {
            if (data[i] == '[') bracketCount++;
            else if (data[i] == ']') bracketCount--;
            i++;
        }
        
        if (bracketCount == 0)
        {
            int commaIndex = data.IndexOf(',', i - 1);
            if (commaIndex > 0 && commaIndex < i + 5)
            {
                data = data.Remove(index, commaIndex - index + 1);
                data = data.Insert(index, $"\"{propertyName}\": [],");
            }
        }
        
        return data;
    }

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
        var dir = NavigateTo(root, "metadata", "materials", "environment", "worldmap");
        if (dir is null) return;

        var file = dir.Children.OfType<FileNode>().FirstOrDefault(f => f.Name == "worldmap_fogofwar.fxgraph");
        if (file is null) return;

        var record = file.Record;
        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());

        data = ReplaceArrayProperty(data, "nodes");
        data = ReplaceArrayProperty(data, "links");

        var newBytes = Encoding.Unicode.GetBytes(data);
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        record.Write(newBytes);
    }
}
