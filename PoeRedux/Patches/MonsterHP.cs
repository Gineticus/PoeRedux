using LibBundle3.Nodes;
using System.Text;
using PoeRedux.Services;

namespace PoeRedux.Patches;

public class MonsterHP : IPatch
{
    public string Name => "Monster HP Patch";
    public object Description => "Always shows monster HP.";

    public double ZoomLevel { get; set; } = 2.4;

    private List<FileNode> fileNodes = [];

    private readonly string[] _extensions = {
        ".ot",
        ".otc",
    };

    private bool HasTargetExtension(string fileName) =>
        _extensions.Any(ext =>
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
        var metadata = NavigateTo(root, "metadata");
        if (metadata is null)
            return;

        // Patch metadata/monsters/monster.ot
        var monsters = NavigateTo(metadata, "monsters");
        if (monsters is not null)
        {
            var monsterFile = monsters.Children.OfType<FileNode>().FirstOrDefault(f => f.Name == "monster.ot");
            if (monsterFile is not null)
            {
                var record = monsterFile.Record;
                var bytes = record.Read();
                string data = Encoding.Unicode.GetString(bytes.ToArray());
                List<string> lines = data.Split("\r\n").ToList();

                if (!data.Contains("base_maximum_energy_shield = 1") && !data.Contains("base_maximum_life = 1"))
                {
                    int index = lines.FindIndex(x => x.Contains("item_drop_slots = 1"));
                    if (index != -1)
                    {
                        lines.Insert(index + 1, "\tbase_maximum_life = 1");
                        lines.Insert(index + 1, "\tbase_maximum_energy_shield = 1");
                    }
                }
                string newData = string.Join("\r\n", lines);
                var newBytes = Encoding.Unicode.GetBytes(newData);
                BackupManager.RecordOriginal(record);
                record.Write(newBytes);
            }
        }
    }
}