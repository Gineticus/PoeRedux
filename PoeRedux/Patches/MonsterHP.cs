using LibBundle3.Nodes;
using System.Text;

namespace PoeRedux.Patches;

public class MonsterHP : IPatch
{
    public string Name => "Monster HP Patch";
    public string Description => "Always shows monster HP.";

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        var monster = GameTree.FindFile(root, "metadata/monsters/monster.ot");
        if (monster is not null)
            plan.Add(monster, PatchMonsterFile);
    }

    private static byte[] PatchMonsterFile(byte[] bytes)
    {
        string data = Encoding.Unicode.GetString(bytes);
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
        return Encoding.Unicode.GetBytes(newData);
    }
}
