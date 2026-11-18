namespace PoeRedux.ViewModels;

/// <summary>
/// What each patch changes in the game files, shown above its settings. Every file and value named here was
/// checked in both games on 18 Nov 2026.
/// </summary>
public static class PatchNotes
{
    public static string For(string patchId, Models.PoeGame game) => patchId switch
    {
        "color-mods" => "Recolors chosen lines in the game's stat description files. The list holds every line that map and waystone prefixes and suffixes can show, read from the game's own mod data, plus strongbox, Expedition and altar lines.",
        "monster-hp" => "Gives every monster 1 base life and 1 base energy shield in metadata/monsters/monster.ot.",
        "camera" => "Sets the starting zoom in metadata/characters/character.ot.",
        "minimap" => "Edits the two minimap shaders, minimap_visibility_pixel.hlsl and minimap_blending_pixel.hlsl.",
        "atlas-fog" => "Empties the Atlas fog of war shader graph, worldmap_fogofwar.fxgraph.",
        "environment" => "Switches off layers in every area's .env file.",
        "light" => "Switches off light layers in every area's .env file.",
        "shadow" => "Sets shadows_enabled to false in every area's .env file.",
        "post-process" => "Switches off filters in the screen shader postprocessuber.hlsl.",
        "particles" => "Empties the particle files (.pet, .trl) under metadata/particles, except in the folders kept below.",
        "effects" => "Removes the visible parts of skill effect files (.ao) under metadata/effects/spells, except in the folders kept below.",
        "mtx" => "Empties shop effect files under metadata/effects/microtransactions, except in the folders kept below.",
        "corpse" => "Adds two events to metadata/monsters/monster.ot: a monster that appears already dead is not drawn, and a monster that dies stops being drawn a second later. A revived monster is drawn again.",
        "delirium" => "Replaces the Delirium fog and ash objects in metadata/effects/environment/league_affliction with empty ones.",
        "loading-screen" => "Adds visible = false to every element of the loading screen layouts in metadata/ui/loadingstate.",
        "black-screen" => "Strips effects, materials, shaders and area settings across the game files. Many visuals go missing; use it to test, not to play.",
        _ => "",
    };
}
