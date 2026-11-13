using LibBundle3.Nodes;

namespace PoeRedux.Patches.Black;

public class Mat : IPatch
{
    public string Name => "Mat Patch";
    public string Description => "";

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        foreach (var file in GameTree.FindFiles(root, ".mat"))
            plan.Add(file, _ => Utf16.WithBom("{\r\n\"version\":4,\r\n\"defaultgraph\": {\r\n \"version\":3\r\n }\r\n}"));
    }
}
