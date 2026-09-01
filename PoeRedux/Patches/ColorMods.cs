using LibBundle3.Nodes;
using PoeRedux.Models;
using System.Text;
using System.Text.RegularExpressions;
using PoeRedux.Services;

namespace PoeRedux.Patches;

public class ColorMods : IPatch
{
    public string Name => "Color Mods Patch";
    public object Description => "Changes colors of mods in the game.";
    public List<ColorModsOption> ColorModsOptions { get; set; } = new()
    {
        // maps and more
        new ColorModsOption("map_monsters_reflect_%_physical_damage", "Good", true, "Map / Atlas"),
        new ColorModsOption("map_monsters_reflect_%_elemental_damage", "Annoying", true, "Map / Atlas"),
        new ColorModsOption("map_player_cannot_expose", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_players_no_regeneration_including_es", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_player_non_curse_aura_effect_+%", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_monsters_avoid_elemental_ailments_%", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_monsters_cannot_be_leeched_from", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_monsters_cannot_be_stunned", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_additional_player_maximum_resistances_%", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_monsters_are_hexproof", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_player_cooldown_speed_+%_final", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_player_life_and_es_recovery_speed_+%_final", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_ground_orion_meteor", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_player_create_enemy_meteor_daemon_on_flask_use_%_chance", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_uber_drowning_orb_ambush", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_petrificiation_statue_ambush", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_exarch_traps", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_packs_have_uber_tentacle_fiends", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_uber_map_additional_synthesis_boss", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_monster_add_x_grasping_vines_on_hit", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_uber_map_player_damage_cycle", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_player_death_mark_on_rare_unique_kill_ms", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_uber_sawblades_ambush", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_rare_monster_volatile_on_death_%", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_rare_monsters_shaper_touched", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_supporter_maven_follower", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_player_global_defences_+%", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_monsters_base_block_%", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_cannot_evade", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_can_only_damage_enemies_in_X_radius", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_damage_taken_while_stationary_+%", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_damage_taken_+%_per_nearby_ally", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_endgame_fog_depth", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_ground_lightning_base_magnitude", "Dangerous", true, "Map / Atlas"),
        new ColorModsOption("map_melee_physical_damage_taken_%_to_deal_to_attacker", "Dangerous", true, "Map / Atlas"),
        // strongboxes
        new ColorModsOption("chest_display_explodes_corpses", "Dangerous", true, "Strongbox"),
        new ColorModsOption("chest_display_explosion", "Dangerous", true, "Strongbox"),
        new ColorModsOption("chest_display_freeze", "Dangerous", true, "Strongbox"),
        new ColorModsOption("chest_display_ice_nova", "Dangerous", true, "Strongbox"),
        new ColorModsOption("chest_spawn_rogue_exiles", "Dangerous", true, "Strongbox"),
        // expedition
        new ColorModsOption("all_damage_can_freeze", "Dangerous", true, "Expedition"),
        new ColorModsOption("apply_petrification_for_X_seconds_on_hit", "Dangerous", true, "Expedition"),
        new ColorModsOption("base_cold_immunity", "Dangerous", true, "Expedition"),
        new ColorModsOption("base_fire_immunity", "Dangerous", true, "Expedition"),
        new ColorModsOption("base_lightning_immunity", "Dangerous", true, "Expedition"),
        new ColorModsOption("chaos_immunity", "Dangerous", true, "Expedition"),
        new ColorModsOption("physical_immunity", "Dangerous", true, "Expedition"),
        new ColorModsOption("cannot_have_life_leeched_from", "Dangerous", true, "Expedition"),
        new ColorModsOption("cannot_have_mana_leeched_from", "Dangerous", true, "Expedition"),
        // altars
        new ColorModsOption("life_mana_es_recovery_rate_+%_per_endurance_charge", "Dangerous", true, "Altar"),
        new ColorModsOption("global_defences_+%_per_frenzy_charge", "Dangerous", true, "Altar"),
        new ColorModsOption("critical_strike_multiplier_+_per_power_charge", "Dangerous", true, "Altar"),
        new ColorModsOption("random_projectile_direction", "Dangerous", true, "Altar"),
        new ColorModsOption("chaos_damage_per_minute_while_affected_by_flask", "Dangerous", true, "Altar"),
        new ColorModsOption("create_enemy_meteor_daemon_on_flask_use_%_chance", "Dangerous", true, "Altar"),
        new ColorModsOption("drain_x_flask_charges_over_time_on_hit_for_6_seconds", "Dangerous", true, "Altar"),
        // altars
        new ColorModsOption("map_item_drop_quantity_+%", "Neutral", true, "Altar"),
        new ColorModsOption("map_item_drop_rarity_+%", "Neutral", true, "Altar"),
        // altars
        new ColorModsOption("chance_%_to_drop_additional_divine_orb", "Good", true, "Altar"),
        new ColorModsOption("map_boss_additional_divine_orb_to_drop", "Good", true, "Altar"),
        new ColorModsOption("%_chance_to_duplicate_dropped_currency", "Great", true, "Altar"),
        new ColorModsOption("%_chance_to_duplicate_dropped_divination_cards", "Great", true, "Altar"),
        new ColorModsOption("%_chance_to_duplicate_dropped_scarabs", "Great", true, "Altar"),
        new ColorModsOption("%_chance_to_duplicate_dropped_uniques", "Great", true, "Altar"),
    };

    /// <summary>The color of each group, overridden from the editor.</summary>
    public Dictionary<string, (byte R, byte G, byte B)> GroupColors { get; set; } =
        ModColorGroups.DefaultColors();

    /// <summary>The short label each group writes in tag mode.</summary>
    public Dictionary<string, string> GroupTags { get; set; } = ModColorGroups.DefaultTags();

    /// <summary>
    /// Colors a short tag in front of the mod instead of the whole line, so the mod itself stays
    /// readable. Proven possible by the vanilla necropolis descriptions, which style one word
    /// mid string.
    /// </summary>
    public bool TagPrefix { get; set; }


    /// <summary>
    /// Turns a group name into the markup the game understands. Legacy color names from an
    /// older config map onto their group first.
    /// </summary>
    private string? Annotation(string group) =>
        GroupColors.TryGetValue(ModColorGroups.Normalize(group), out var c)
            ? $"rgb({c.R},{c.G},{c.B})"
            : null;

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

    private bool TryPatchFile(FileNode file)
    {
        var record = file.Record;
        var bytes = record.Read();
        string data = Encoding.Unicode.GetString(bytes.ToArray());

        if (string.IsNullOrEmpty(data))
            return false;

        var lines = data.Split("\r\n").ToList();

        ReadState state = ReadState.ReadingToDescription;

        string? currentAnnotation = null;
        string currentTag = "";
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
                    && Annotation(option.Group) is string annotation)
                {
                    currentAnnotation = annotation;
                    currentTag = GroupTags.TryGetValue(ModColorGroups.Normalize(option.Group), out var tag)
                        ? tag
                        : ModColorGroups.Normalize(option.Group);
                    currentIsEnabled = option.IsEnabled;
                    state = ReadState.ReadingData;
                }
                else
                {
                    // Still walk the ranges, so a color left over from a previous apply of a
                    // mod that is no longer listed gets stripped instead of sticking forever.
                    currentAnnotation = null;
                    currentIsEnabled = false;
                    state = ReadState.ReadingData;
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
                lines[i] = ModTextStyler.Restyle(line, currentAnnotation, currentTag, currentIsEnabled == true, TagPrefix);

                linesToWrite--;
                if (linesToWrite == 0)
                {
                    state = ReadState.ReadingData;
                    continue;
                }
            }
        }

        var newData = string.Join("\r\n", lines);
        if (newData == data)
            return false;

        var newBytes = Encoding.Unicode.GetBytes(newData);
        if (!newBytes.AsSpan().StartsWith(Encoding.Unicode.GetPreamble()))
        {
            newBytes = [.. Encoding.Unicode.GetPreamble(), .. newBytes];
        }
        BackupManager.RecordOriginal(record);
        record.Write(newBytes);
        return true;
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
        if (dir is null)
        {
            PatchReport.FileMissing(Scope);
            return;
        }
        CollectFileNodesRecursively(dir);

        var changed = 0;
        foreach (var file in fileNodes)
            if (TryPatchFile(file)) changed++;

        // Nothing selected is a legitimate no-op, so only a selection that reached no file is a miss.
        if (changed == 0 && ColorModsOptions.Any(o => o.IsEnabled))
            PatchReport.NoMatch(Scope, "any selected mod description");
    }

    private const string Scope = "metadata/statdescriptions";
}
