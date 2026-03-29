using LibBundle3.Nodes;
using PoeSmoother.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeSmoother.Patches;

public class ColorMods : IPatch
{
    public string Name => "Color Mods Patch";
    public object Description => "Changes colors of mods in the game.";
    public List<ColorModsOption> ColorModsOptions { get; set; } = new()
    {
        // maps and more
        new ColorModsOption("map_monsters_reflect_%_physical_damage", "yellow", true),
        new ColorModsOption("map_monsters_reflect_%_elemental_damage", "green", true),
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
    };

    private readonly Dictionary<string, string> _color_conversions = new()
    {
        { "red", "premiumchatoutline" },
        { "green", "quest" },
        { "blue", "divination" },
        { "yellow", "necropolisupside" },
        { "pink", "archnemesismodchaospurple" },
    };
    private enum ReadState
    {
        WritingData,
        ReadingData,
        ReadingDescription,
        ReadingToDescription
    }

    private readonly string[] _extensions = {
        ".txt",
    };

    private List<FileNode> fileNodes = [];

    private void CollectFileNodesRecursively(DirectoryNode dir)
    {
        foreach (var node in dir.Children)
        {
            switch (node)
            {
                case DirectoryNode childDir:
                    CollectFileNodesRecursively(childDir);
                    break;

                case FileNode fileNode:
                    if (HasTargetExtension(fileNode.Name))
                        fileNodes.Add(fileNode);
                    break;
            }
        }
    }

    private void TryPatchFile(FileNode file)
    {
        var record = file.Record;
        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());

        if (string.IsNullOrEmpty(data))
            return;

        var lines = data.Split("\r\n").ToList();

        ReadState state = ReadState.ReadingToDescription;

        string? currentAnnotation = null;
        bool? currentIsEnabled = null;

        int linesToWrite = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];

            if (line.StartsWith("description"))
            {
                state = ReadState.ReadingDescription;
                continue;
            }

            if (state == ReadState.ReadingToDescription) continue;

            // Read the description on the next line.
            if (state == ReadState.ReadingDescription)
            {
                string[] description = line.Split(' ');
                if (description.Length < 2)
                {
                    state = ReadState.ReadingToDescription;
                    continue;
                }

                string modType = description[1];

                if (ColorModsOptions.FirstOrDefault(x => x.Name == modType) is ColorModsOption option
                    && _color_conversions.TryGetValue(option.Color.ToLower(), out string? annotation))
                {
                    currentAnnotation = annotation;
                    currentIsEnabled = option.IsEnabled;
                    state = ReadState.ReadingData;
                }
                else
                {
                    currentAnnotation = null;
                    state = ReadState.ReadingToDescription;
                }

                continue;
            }

            if (state == ReadState.ReadingData)
            {
                // Replace tabs in line with nothing.
                string firstNumber = line.Replace("\t", "").Split(' ')[0];

                // May be a "lang" value. If the value is a number write those lines.
                if (int.TryParse(firstNumber, out int value))
                {
                    state = ReadState.WritingData;
                    linesToWrite = value;
                    continue;
                }
                ;
            }

            if (state == ReadState.WritingData)
            {
                if (line.Contains('<')) // Already annotated.
                {
                    if (currentIsEnabled == false)
                    {
                        // Remove annotation.
                        // <.*?>{{value}} -> "value".
                        string pattern = "<.*?>{{(.*?)}}";
                        string replacement = Regex.Replace(line, pattern, new MatchEvaluator(match =>
                        {
                            return $"{match.Groups[1].Value}";
                        }));
                        lines[i] = replacement;
                    }
                    else
                    {
                        // Replace text between brackets with new annotation.
                        string pattern = "<.*?>";
                        string replacement = Regex.Replace(line, pattern, new MatchEvaluator(match =>
                        {
                            return $"<{currentAnnotation}>";
                        }));
                        lines[i] = replacement;
                    }
                }
                else
                {
                    // Surround the value with the annotation.
                    // "value" -> "<annotation>{{value}}".

                    string pattern = "\".*?\"";
                    string replacement = Regex.Replace(line, pattern, new MatchEvaluator(match =>
                    {
                        return $"\"<{currentAnnotation}>{{{{{match.Value.Replace("\"", "")}}}}}\"";
                    }));
                    lines[i] = replacement;
                }

                linesToWrite--;
                if (linesToWrite == 0)
                {
                    state = ReadState.ReadingData;
                    continue;
                }
            }
        }

        var newData = string.Join("\r\n", lines);
        var newBytes = Encoding.Unicode.GetBytes(newData);
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        record.Write(newBytes);
    }

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
        var dir = NavigateTo(root, "metadata", "statdescriptions");
        if (dir is not null)
            CollectFileNodesRecursively(dir);

        foreach (var file in fileNodes)
        {
            TryPatchFile(file);
        }
    }
}