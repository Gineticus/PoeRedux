using LibBundle3.Nodes;
using PoeRedux.Helpers;
using System.Text;

namespace PoeRedux.Patches;

public class Corpse : IPatch
{
    public string Name => "Corpse Patch";
    public string Description => "Removes corpses from the game.";

    public void Collect(DirectoryNode root, PatchPlan plan) =>
        HideCorpses(root, plan, onDeath: "{Delay( 1.0, { DisableRendering(); } );}");

    /// <summary>
    /// Edits metadata/monsters/monster.ot: monsters that spawn dead are not rendered, <paramref name="onDeath"/>
    /// runs when a monster dies, and a revived monster is rendered again.
    /// </summary>
    internal static void HideCorpses(DirectoryNode root, PatchPlan plan, string onDeath)
    {
        var monster = GameTree.FindFile(root, "metadata/monsters/monster.ot");
        if (monster is null)
            return;

        plan.Add(monster, bytes =>
        {
            string data = Encoding.Unicode.GetString(bytes);

            data = data.Replace("Life\r\n{\r\n}", "Life\r\n{\r\n\ton_spawned_dead = {RemoveEffects(); DisableRendering();}\r\n\ton_death = " + onDeath + "\r\n}");
            data = data.Replace("slow_animations_go_to_idle = true\r\n}", "slow_animations_go_to_idle = true\r\n\ton_start_Revive = {RemoveEffects(); EnableRendering();}\r\n}");

            return Encoding.Unicode.GetBytes(data);
        });
    }
}
