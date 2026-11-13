using LibBundle3.Nodes;
using PoeRedux.Helpers;
using System.Text;

namespace PoeRedux.Patches;

public class AtlasFog : IPatch
{
    public string Name => "Atlas Fog Patch";
    public string Description => "Removes fog from the Atlas.";

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        var file = GameTree.FindFile(root, "metadata/materials/environment/worldmap/worldmap_fogofwar.fxgraph");
        if (file is not null)
            plan.Add(file, PatchFogOfWar);
    }

    private static byte[] PatchFogOfWar(byte[] bytes)
    {
        string data = Encoding.Unicode.GetString(bytes);

        data = EmptyArray(data, "nodes");
        data = EmptyArray(data, "links");

        return Utf16.WithBom(data);
    }

    // "name": [ ... ], becomes "name": [],
    private static string EmptyArray(string data, string propertyName)
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
}
