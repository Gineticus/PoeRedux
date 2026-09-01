using LibBundle3.Nodes;
using System.Text;
using PoeRedux.Services;

namespace PoeRedux.Patches;

public class MonsterHP : IPatch
{
    public string Name => "Monster HP Patch";
    public object Description =>
        "Gives every monster a life bar, including ones the game normally draws none for.";

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
        var monsters = NavigateTo(root, "metadata", "monsters");
        var monsterFile = monsters?.Children.OfType<FileNode>().FirstOrDefault(f => f.Name == "monster.ot");
        if (monsterFile is null)
        {
            PatchReport.FileMissing("metadata/monsters/monster.ot");
            return;
        }

        var record = monsterFile.Record;
        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());
        List<string> lines = data.Split("\r\n").ToList();

        if (!data.Contains("base_maximum_energy_shield = 1") && !data.Contains("base_maximum_life = 1"))
        {
            int index = lines.FindIndex(x => x.Contains(Anchor));
            if (index == -1)
            {
                PatchReport.AnchorMissing("monster.ot", Anchor);
                return;
            }
            lines.Insert(index + 1, "\tbase_maximum_life = 1");
            lines.Insert(index + 1, "\tbase_maximum_energy_shield = 1");
        }
        string newData = string.Join("\r\n", lines);
        var newBytes = Encoding.Unicode.GetBytes(newData);
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
    }

    private const string Anchor = "item_drop_slots = 1";
}