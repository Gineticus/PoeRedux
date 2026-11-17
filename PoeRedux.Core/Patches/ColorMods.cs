using LibBundle3.Nodes;
using PoeRedux.Helpers;
using PoeRedux.Models;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

/// <summary>
/// Colors the text of the chosen mods in the stat description files.
/// </summary>
public class ColorMods : IPatch
{
    private static readonly Dictionary<string, string> Colors = new()
    {
        { "red", "rgb(209,46,46)" },
        { "green", "rgb(74,230,58)" },
        { "blue", "rgb(14,186,255)" },
        { "yellow", "rgb(255,204,27)" },
        { "pink", "rgb(227,158,255)" },
    };

    private readonly string _directory;
    private readonly string _extension;
    private int _modsFound;

    public ColorMods(PoeGame game)
    {
        if (game == PoeGame.PoE2)
        {
            _directory = "data/statdescriptions";
            _extension = ".csd";
            ColorModsOptions = Poe2Options();
        }
        else
        {
            _directory = "metadata/statdescriptions";
            _extension = ".csd";
            ColorModsOptions = Poe1Options();
        }
    }

    public string Name => "Color Mods Patch";
    public string Description => "Changes colors of mods in the game.";
    public List<ColorModsOption> ColorModsOptions { get; set; }

    /// <summary>
    /// Description blocks of the mods in <see cref="ColorModsOptions"/> that the last apply found in the game files.
    /// Zero means the game no longer has these mods where this patch looks for them.
    /// </summary>
    public int ModsFound => _modsFound;

    public void Collect(DirectoryNode root, PatchPlan plan)
    {
        _modsFound = 0;
        var transform = Utf16.Edit(Recolor);
        foreach (var file in GameTree.FindFiles(GameTree.FindDirectory(root, _directory), _extension))
            plan.Add(file, transform);
    }

    private enum ReadState
    {
        SeekingDescription,
        ReadingStatName,
        ReadingLineCount,
        Recoloring,
    }

    // A "description" line starts a block. The next line lists its stats, and the first one decides the color.
    // Then, for each language, a line with a count n is followed by n lines of text, which get the color.
    private string Recolor(string text)
    {
        string[] lines = text.Split("\r\n");
        var state = ReadState.SeekingDescription;
        string color = "";
        bool enabled = false;
        int linesLeft = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            if (line.StartsWith("description"))
            {
                state = ReadState.ReadingStatName;
                continue;
            }

            switch (state)
            {
                case ReadState.ReadingStatName:
                    string[] words = line.Split(' ');
                    var option = words.Length >= 2 ? ColorModsOptions.FirstOrDefault(x => x.Name == words[1]) : null;
                    if (option is not null)
                        Interlocked.Increment(ref _modsFound); // files are patched in parallel
                    if (option is not null && Colors.TryGetValue(option.Color.ToLower(), out var rgb))
                    {
                        color = rgb;
                        enabled = option.IsEnabled;
                        state = ReadState.ReadingLineCount;
                    }
                    else
                    {
                        state = ReadState.SeekingDescription;
                    }
                    break;

                case ReadState.ReadingLineCount:
                    // Lines that are not a number, such as lang "Russian", are skipped.
                    if (int.TryParse(line.Replace("\t", "").Split(' ')[0], out int count))
                    {
                        linesLeft = count;
                        state = ReadState.Recoloring;
                    }
                    break;

                case ReadState.Recoloring:
                    lines[i] = RecolorLine(line, color, enabled);
                    if (--linesLeft == 0)
                        state = ReadState.ReadingLineCount;
                    break;
            }
        }

        return string.Join("\r\n", lines);
    }

    // Enabled: "text" becomes "<color>{{text}}", and an existing <...> color is swapped for this one.
    // Disabled: <...>{{text}} becomes text again.
    private static string RecolorLine(string line, string color, bool enabled)
    {
        if (line.Contains('<'))
        {
            return enabled
                ? Regex.Replace(line, "<.*?>", _ => $"<{color}>")
                : Regex.Replace(line, "<.*?>{{(.*?)}}", m => m.Groups[1].Value);
        }

        return enabled
            ? Regex.Replace(line, "\".*?\"", m => $"\"<{color}>{{{{{m.Value.Replace("\"", "")}}}}}\"")
            : line;
    }

    private static List<ColorModsOption> Poe1Options() =>
    [
        // maps and more
        new ColorModsOption("map_monsters_reflect_%_physical_damage", "yellow", true),
        new ColorModsOption("map_rare_monsters_physical_thorns_for_x_damage", "yellow", true),
        new ColorModsOption("map_monsters_reflect_%_elemental_damage", "green", true),
        new ColorModsOption("map_rare_monsters_elemental_thorns_for_x_damage", "green", true),
        new ColorModsOption("map_player_cannot_expose", "red", true),
        new ColorModsOption("map_players_no_regeneration_including_es", "red", true),
        new ColorModsOption("map_player_non_curse_aura_effect_+%", "red", true),
        new ColorModsOption("map_monsters_avoid_elemental_ailments_%", "red", true),
        new ColorModsOption("map_monsters_cannot_be_leeched_from", "red", true),
        new ColorModsOption("map_monsters_cannot_be_stunned", "red", true),
        new ColorModsOption("map_additional_player_maximum_resistances_%", "red", true),
        new ColorModsOption("map_monsters_are_hexproof", "red", true),
        new ColorModsOption("map_player_cooldown_speed_+%_final", "red", true),
        new ColorModsOption("map_player_life_and_es_recovery_speed_+%_final", "red", true),
        new ColorModsOption("map_ground_orion_meteor", "red", true),
        new ColorModsOption("map_player_create_enemy_meteor_daemon_on_flask_use_%_chance", "red", true),
        new ColorModsOption("map_uber_drowning_orb_ambush", "red", true),
        new ColorModsOption("map_petrificiation_statue_ambush", "red", true),
        new ColorModsOption("map_exarch_traps", "red", true),
        new ColorModsOption("map_packs_have_uber_tentacle_fiends", "red", true),
        new ColorModsOption("map_uber_map_additional_synthesis_boss", "red", true),
        new ColorModsOption("map_monster_add_x_grasping_vines_on_hit", "red", true),
        new ColorModsOption("map_uber_map_player_damage_cycle", "red", true),
        new ColorModsOption("map_player_death_mark_on_rare_unique_kill_ms", "red", true),
        new ColorModsOption("map_uber_sawblades_ambush", "red", true),
        new ColorModsOption("map_rare_monster_volatile_on_death_%", "red", true),
        new ColorModsOption("map_rare_monsters_shaper_touched", "red", true),
        new ColorModsOption("map_supporter_maven_follower", "red", true),
        new ColorModsOption("map_player_global_defences_+%", "red", true),
        new ColorModsOption("map_monsters_base_block_%", "red", true),
        // strongboxes
        new ColorModsOption("chest_display_explodes_corpses", "red", true),
        new ColorModsOption("chest_display_explosion", "red", true),
        new ColorModsOption("chest_display_freeze", "red", true),
        new ColorModsOption("chest_display_ice_nova", "red", true),
        new ColorModsOption("chest_spawn_rogue_exiles", "red", true),
        // expedition
        new ColorModsOption("all_damage_can_freeze", "red", true),
        new ColorModsOption("apply_petrification_for_X_seconds_on_hit", "red", true),
        new ColorModsOption("base_cold_immunity", "red", true),
        new ColorModsOption("base_fire_immunity", "red", true),
        new ColorModsOption("base_lightning_immunity", "red", true),
        new ColorModsOption("chaos_immunity", "red", true),
        new ColorModsOption("physical_immunity", "red", true),
        new ColorModsOption("cannot_have_life_leeched_from", "red", true),
        new ColorModsOption("cannot_have_mana_leeched_from", "red", true),
        // altars
        new ColorModsOption("life_mana_es_recovery_rate_+%_per_endurance_charge", "red", true),
        new ColorModsOption("global_defences_+%_per_frenzy_charge", "red", true),
        new ColorModsOption("critical_strike_multiplier_+_per_power_charge", "red", true),
        new ColorModsOption("random_projectile_direction", "red", true),
        new ColorModsOption("chaos_damage_per_minute_while_affected_by_flask", "red", true),
        new ColorModsOption("create_enemy_meteor_daemon_on_flask_use_%_chance", "red", true),
        new ColorModsOption("drain_x_flask_charges_over_time_on_hit_for_6_seconds", "red", true),
        // altars
        new ColorModsOption("map_item_drop_quantity_+%", "pink", true),
        new ColorModsOption("map_item_drop_rarity_+%", "pink", true),
        // altars
        new ColorModsOption("chance_%_to_drop_additional_divine_orb", "yellow", true),
        new ColorModsOption("map_boss_additional_divine_orb_to_drop", "yellow", true),
        new ColorModsOption("%_chance_to_duplicate_dropped_currency", "blue", true),
        new ColorModsOption("%_chance_to_duplicate_dropped_divination_cards", "blue", true),
        new ColorModsOption("%_chance_to_duplicate_dropped_scarabs", "blue", true),
        new ColorModsOption("%_chance_to_duplicate_dropped_uniques", "blue", true),
    ];

    private static List<ColorModsOption> Poe2Options() =>
    [
        // red
        new ColorModsOption("map_monsters_%_all_damage_to_gain_as_fire", "red", true),
        new ColorModsOption("map_monsters_%_physical_damage_to_gain_as_fire", "red", true),
        new ColorModsOption("map_monsters_%_all_damage_to_gain_as_cold", "red", true),
        new ColorModsOption("map_monsters_%_physical_damage_to_gain_as_cold", "red", true),
        new ColorModsOption("map_monsters_%_all_damage_to_gain_as_lightning", "red", true),
        new ColorModsOption("map_monsters_%_physical_damage_to_gain_as_lightning", "red", true),
        new ColorModsOption("map_monsters_%_all_damage_to_gain_as_chaos", "red", true),
        new ColorModsOption("map_monsters_%_physical_damage_to_gain_as_chaos", "red", true),
        new ColorModsOption("map_monsters_damage_+%", "red", true),
        new ColorModsOption("map_monster_damage_+%_final_from_deadly_atlas", "red", true),
        new ColorModsOption("map_monster_damage_+%_final_from_vital_atlas", "red", true),
        new ColorModsOption("map_monsters_attack_speed_+%", "red", true),
        new ColorModsOption("map_monsters_cast_speed_+%", "red", true),
        new ColorModsOption("map_monsters_movement_speed_+%", "red", true),
        new ColorModsOption("map_monsters_critical_strike_chance_+%", "red", true),
        new ColorModsOption("map_monsters_critical_strike_multiplier_+", "red", true),
        new ColorModsOption("map_monsters_chance_to_poison_on_hit_%", "red", true),
        new ColorModsOption("map_monsters_chance_to_inflict_bleeding_%", "red", true),
        new ColorModsOption("map_packs_fire_projectiles", "red", true),
        new ColorModsOption("map_monsters_penetrate_elemental_resistances_%", "red", true),
        new ColorModsOption("map_monsters_elemental_ailment_chance_+%", "red", true),
        new ColorModsOption("map_additional_player_maximum_resistances_%", "red", true),
        new ColorModsOption("map_player_life_and_es_recovery_speed_+%_final", "red", true),
        // green
        new ColorModsOption("map_monsters_life_+%", "green", true),
        new ColorModsOption("monster_life_+%_final_from_map", "green", true),
        new ColorModsOption("map_heist_monster_life_+%_final_from_sextant", "green", true),
        new ColorModsOption("map_monsters_armour_break_physical_damage_%_dealt_as_armour_break", "green", true),
        new ColorModsOption("map_monsters_accuracy_rating_+%", "green", true),
        new ColorModsOption("map_monsters_hit_damage_stun_multiplier_+%", "green", true),
        new ColorModsOption("map_monsters_additional_fire_resistance", "green", true),
        new ColorModsOption("map_monsters_additional_cold_resistance", "green", true),
        new ColorModsOption("map_monsters_additional_lightning_resistance", "green", true),
        new ColorModsOption("map_monsters_additional_chaos_resistance", "green", true),
        new ColorModsOption("map_monsters_ailment_threshold_+%", "green", true),
        new ColorModsOption("map_monsters_stun_threshold_+%", "green", true),
        new ColorModsOption("map_base_ground_fire_damage_to_deal_per_10_seconds", "green", true),
        new ColorModsOption("map_base_ground_fire_damage_to_deal_per_minute", "green", true),
        new ColorModsOption("map_ground_ice_base_magnitude", "green", true),
        new ColorModsOption("map_ground_ice", "green", true),
        new ColorModsOption("map_ground_lightning", "green", true),
        new ColorModsOption("map_ground_lightning_base_magnitude", "green", true),
        new ColorModsOption("flask_charges_gained_+%", "green", true),
        new ColorModsOption("map_players_cannot_gain_flask_charges", "green", true),
        new ColorModsOption("map_player_cooldown_speed_+%_final", "green", true),
        new ColorModsOption("map_monsters_base_self_critical_strike_multiplier_-%", "green", true),
        new ColorModsOption("map_monsters_curse_effect_on_self_+%_final", "green", true),
        new ColorModsOption("map_monsters_curse_effect_+%", "green", true),
        // yellow
        new ColorModsOption("map_monster_potency_+%_final_from_map", "yellow", true),
        new ColorModsOption("map_monster_potency_+%", "yellow", true),
        new ColorModsOption("map_item_drop_rarity_+%_final_from_map", "yellow", true),
        new ColorModsOption("map_item_drop_rarity_+%", "yellow", true),
        new ColorModsOption("map_pack_size_+%_final_from_map", "yellow", true),
        new ColorModsOption("map_pack_size_+%", "yellow", true),
        new ColorModsOption("map_number_of_magic_and_rare_packs_+%_final_and_rare_monster_modifiers_chance_+%_final_from_map", "yellow", true),
        new ColorModsOption("map_number_of_magic_and_rare_packs_+%_and_rare_monster_modifiers_chance_%", "yellow", true),
        new ColorModsOption("map_number_of_magic_packs_+%", "yellow", true),
        new ColorModsOption("map_number_of_rare_packs_+%", "yellow", true),
        new ColorModsOption("map_rare_monster_modifiers_chance_%", "yellow", true),
        // white
        new ColorModsOption("map_map_item_drop_chance_+%_final_from_map", "blue", true),
        new ColorModsOption("map_map_item_drop_chance_+%", "blue", true),
    ];
}
