using LibBundle3.Nodes;
using PoeRedux.Models;
using System.Text;
using System.Text.RegularExpressions;
using PoeRedux.Services;

namespace PoeRedux.Patches;

public class ColorMods2 : IPatch
{
    public string Name => "Color Mods Patch";
    public object Description => "Changes colors of mods in the game.";

    public List<ColorModsOption> ColorModsOptions { get; set; } = new()
    {
        // Dangerous
        new ColorModsOption("map_monsters_%_all_damage_to_gain_as_fire", "Dangerous", true),
        new ColorModsOption("map_monsters_%_physical_damage_to_gain_as_fire", "Dangerous", true),
        new ColorModsOption("map_monsters_%_all_damage_to_gain_as_cold", "Dangerous", true),
        new ColorModsOption("map_monsters_%_physical_damage_to_gain_as_cold", "Dangerous", true),
        new ColorModsOption("map_monsters_%_all_damage_to_gain_as_lightning", "Dangerous", true),
        new ColorModsOption("map_monsters_%_physical_damage_to_gain_as_lightning", "Dangerous", true),
        new ColorModsOption("map_monsters_%_all_damage_to_gain_as_chaos", "Dangerous", true),
        new ColorModsOption("map_monsters_%_physical_damage_to_gain_as_chaos", "Dangerous", true),
        new ColorModsOption("map_monsters_damage_+%", "Dangerous", true),
        new ColorModsOption("map_monster_damage_+%_final_from_deadly_atlas", "Dangerous", true),
        new ColorModsOption("map_monster_damage_+%_final_from_vital_atlas", "Dangerous", true),
        new ColorModsOption("map_monsters_attack_speed_+%", "Dangerous", true),
        new ColorModsOption("map_monsters_cast_speed_+%", "Dangerous", true),
        new ColorModsOption("map_monsters_movement_speed_+%", "Dangerous", true),
        new ColorModsOption("map_monsters_critical_strike_chance_+%", "Dangerous", true),
        new ColorModsOption("map_monsters_critical_strike_multiplier_+", "Dangerous", true),
        new ColorModsOption("map_monsters_chance_to_poison_on_hit_%", "Dangerous", true),
        new ColorModsOption("map_monsters_chance_to_inflict_bleeding_%", "Dangerous", true),
        new ColorModsOption("map_packs_fire_projectiles", "Dangerous", true),
        new ColorModsOption("map_monsters_penetrate_elemental_resistances_%", "Dangerous", true),
        new ColorModsOption("map_monsters_elemental_ailment_chance_+%", "Dangerous", true),
        new ColorModsOption("map_additional_player_maximum_resistances_%", "Dangerous", true),
        new ColorModsOption("map_player_life_and_es_recovery_speed_+%_final", "Dangerous", true),
        // Annoying
        new ColorModsOption("map_monsters_life_+%", "Annoying", true),
        new ColorModsOption("monster_life_+%_final_from_map", "Annoying", true),
        new ColorModsOption("map_heist_monster_life_+%_final_from_sextant", "Annoying", true),
        new ColorModsOption("map_monsters_armour_break_physical_damage_%_dealt_as_armour_break", "Annoying", true),
        new ColorModsOption("map_monsters_accuracy_rating_+%", "Annoying", true),
        new ColorModsOption("map_monsters_hit_damage_stun_multiplier_+%", "Annoying", true),
        new ColorModsOption("map_monsters_additional_fire_resistance", "Annoying", true),
        new ColorModsOption("map_monsters_additional_cold_resistance", "Annoying", true),
        new ColorModsOption("map_monsters_additional_lightning_resistance", "Annoying", true),
        new ColorModsOption("map_monsters_additional_chaos_resistance", "Annoying", true),
        new ColorModsOption("map_monsters_ailment_threshold_+%", "Annoying", true),
        new ColorModsOption("map_monsters_stun_threshold_+%", "Annoying", true),
        new ColorModsOption("map_base_ground_fire_damage_to_deal_per_10_seconds", "Annoying", true),
        new ColorModsOption("map_base_ground_fire_damage_to_deal_per_minute", "Annoying", true),
        new ColorModsOption("map_ground_ice_base_magnitude", "Annoying", true),
        new ColorModsOption("map_ground_ice", "Annoying", true),
        new ColorModsOption("map_ground_lightning", "Annoying", true),
        new ColorModsOption("map_ground_lightning_base_magnitude", "Annoying", true),
        new ColorModsOption("flask_charges_gained_+%", "Annoying", true),
        new ColorModsOption("map_players_cannot_gain_flask_charges", "Annoying", true),
        new ColorModsOption("map_player_cooldown_speed_+%_final", "Annoying", true),
        new ColorModsOption("map_monsters_base_self_critical_strike_multiplier_-%", "Annoying", true),
        new ColorModsOption("map_monsters_curse_effect_on_self_+%_final", "Annoying", true),
        new ColorModsOption("map_monsters_curse_effect_+%", "Annoying", true),
        // Good
        new ColorModsOption("map_monster_potency_+%_final_from_map", "Good", true),
        new ColorModsOption("map_monster_potency_+%", "Good", true),
        new ColorModsOption("map_item_drop_rarity_+%_final_from_map", "Good", true),
        new ColorModsOption("map_item_drop_rarity_+%", "Good", true),
        new ColorModsOption("map_pack_size_+%_final_from_map", "Good", true),
        new ColorModsOption("map_pack_size_+%", "Good", true),
        new ColorModsOption("map_number_of_magic_and_rare_packs_+%_final_and_rare_monster_modifiers_chance_+%_final_from_map", "Good", true),
        new ColorModsOption("map_number_of_magic_and_rare_packs_+%_and_rare_monster_modifiers_chance_%", "Good", true),
        new ColorModsOption("map_number_of_magic_packs_+%", "Good", true),
        new ColorModsOption("map_number_of_rare_packs_+%", "Good", true),
        new ColorModsOption("map_rare_monster_modifiers_chance_%", "Good", true),
        // Great
        new ColorModsOption("map_map_item_drop_chance_+%_final_from_map", "Great", true),
        new ColorModsOption("map_map_item_drop_chance_+%", "Great", true),
    };

    /// <summary>The color of each group, overridden from the editor.</summary>
    public Dictionary<string, (byte R, byte G, byte B)> GroupColors { get; set; } =
        ModColorGroups.DefaultColors();

    /// <summary>The short label each group writes in tag mode.</summary>
    public Dictionary<string, string> GroupTags { get; set; } = ModColorGroups.DefaultTags();

    /// <summary>Colors a short tag in front of the mod instead of the whole line.</summary>
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
        ".csd",
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
                lines[i] = ModTextStyler.Restyle(
                    line, currentAnnotation, currentTag, currentIsEnabled == true, TagPrefix);

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
        var dir = NavigateTo(root, "data", "statdescriptions");
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

    private const string Scope = "data/statdescriptions";
}