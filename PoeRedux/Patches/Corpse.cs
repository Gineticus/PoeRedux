using LibBundle3.Nodes;
using System.Text;

namespace PoeRedux.Patches;

public class Corpse : IPatch
{
    public string Name => "Corpse Patch";
    public object Description => "Removes corpses from the game.";

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
        if (monsters is null)
            return;

        var monsterFile = monsters.Children.OfType<FileNode>().FirstOrDefault(f => f.Name == "monster.ot");
        if (monsterFile is null)
            return;

        var record = monsterFile.Record;
        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());

        data = data.Replace("Life\r\n{\r\n}", "Life\r\n{\r\n\ton_spawned_dead = {RemoveEffects(); DisableRendering();}\r\n\ton_death = {Delay( 1.0, { DisableRendering(); } );}\r\n}");
        data = data.Replace("slow_animations_go_to_idle = true\r\n}", "slow_animations_go_to_idle = true\r\n\ton_start_Revive = {RemoveEffects(); EnableRendering();}\r\n}");

        var newBytes = Encoding.Unicode.GetBytes(data);
        record.Write(newBytes);
    }
}
