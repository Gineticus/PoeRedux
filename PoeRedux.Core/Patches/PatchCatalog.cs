using PoeRedux.Models;

namespace PoeRedux.Patches;

public static class PatchCatalog
{
    /// <summary>
    /// The patches offered for <paramref name="game"/>, in list order. A patch is offered only when its target
    /// files exist in that game: Corpses needs anchors that PoE 2's monster.ot does not have, and Atlas Fog's
    /// shader graph exists only in PoE 2.
    /// </summary>
    public static List<IPatch> Create(PoeGame game)
    {
        var patches = new List<IPatch>
        {
            new ColorMods(game),
            new MonsterHP(),
            new Camera(),
            new Minimap(),
        };
        if (game == PoeGame.PoE2)
            patches.Add(new AtlasFog());
        patches.AddRange(
        [
            new EnvironmentLayers(),
            new Light(),
            new Shadow(),
            new PostProcess(),
            new Particles(),
            new Effects(),
            new Mtx(),
        ]);
        if (game == PoeGame.PoE1)
            patches.Add(new Corpse());
        patches.AddRange(
        [
            new Delirium(),
            new LoadingScreen(),
            new BlackScreen(),
        ]);
        return patches;
    }
}
