using PoeRedux.Models;

namespace PoeRedux.Patches.ModColors;

/// <param name="Key">First stat id of the description block.</param>
/// <param name="Group">Group the line starts in. Lines without a default are listed but left uncolored.</param>
/// <param name="Category">Where the line shows up, for lines that are not map or waystone affixes.</param>
public sealed record ModColorDefault(string Key, string Group, string? Category = null);

/// <summary>
/// Which mod lines are colored out of the box, and in which group.
/// <para>
/// The first part of each list is what PoE Redux colored before groups existed (red, green, yellow, blue
/// and pink became Dangerous, Annoying, Good, Great and Neutral). Entries were dropped when no mod in the
/// game uses the stat any more, checked against Mods.datc64 on 18 Nov 2026.
/// </para>
/// <para>
/// The second part covers map and waystone affix lines that were missing. A missing line got a group only
/// when PoE Redux already colored the same mechanic in either game: the same stat id, or a stat the game
/// merged or renamed (attack, cast and movement speed in one line; one line for all elemental resistances).
/// Every other line on map and waystone affixes is listed in the editor but off.
/// </para>
/// </summary>
public static class ModColorDefaults
{
    public const string Dangerous = "Dangerous";
    public const string Annoying = "Annoying";
    public const string Good = "Good";
    public const string Great = "Great";
    public const string Neutral = "Neutral";

    public const string Strongbox = "Strongbox";
    public const string Expedition = "Expedition";
    public const string Altar = "Altar";

    public static IReadOnlyList<ModColorDefault> For(PoeGame game) => game == PoeGame.PoE2 ? Poe2 : Poe1;

    public static IReadOnlyList<ModColorDefault> Poe1 { get; } =
    [
        // Colored before groups existed. The two monster reflect stats were removed: no mod uses them any
        // more, maps roll the thorns stats below instead.
        new("map_rare_monsters_physical_thorns_for_x_damage", Good),
        new("map_rare_monsters_elemental_thorns_for_x_damage", Annoying),
        new("map_player_cannot_expose", Dangerous),
        new("map_players_no_regeneration_including_es", Dangerous),
        new("map_player_non_curse_aura_effect_+%", Dangerous),
        new("map_monsters_avoid_elemental_ailments_%", Dangerous),
        new("map_monsters_cannot_be_leeched_from", Dangerous),
        new("map_monsters_cannot_be_stunned", Dangerous),
        new("map_additional_player_maximum_resistances_%", Dangerous),
        new("map_monsters_are_hexproof", Dangerous),
        new("map_player_cooldown_speed_+%_final", Dangerous),
        new("map_player_life_and_es_recovery_speed_+%_final", Dangerous),
        new("map_ground_orion_meteor", Dangerous),
        new("map_player_create_enemy_meteor_daemon_on_flask_use_%_chance", Dangerous),
        new("map_uber_drowning_orb_ambush", Dangerous),
        new("map_petrificiation_statue_ambush", Dangerous),
        new("map_exarch_traps", Dangerous),
        new("map_packs_have_uber_tentacle_fiends", Dangerous),
        new("map_uber_map_additional_synthesis_boss", Dangerous),
        new("map_monster_add_x_grasping_vines_on_hit", Dangerous),
        new("map_uber_map_player_damage_cycle", Dangerous),
        new("map_player_death_mark_on_rare_unique_kill_ms", Dangerous),
        new("map_uber_sawblades_ambush", Dangerous),
        new("map_rare_monster_volatile_on_death_%", Dangerous),
        new("map_rare_monsters_shaper_touched", Dangerous),
        new("map_supporter_maven_follower", Dangerous),
        new("map_player_global_defences_+%", Dangerous),
        new("map_monsters_base_block_%", Dangerous),
        new("chest_display_explodes_corpses", Dangerous, Strongbox),
        new("chest_display_explosion", Dangerous, Strongbox),
        new("chest_display_freeze", Dangerous, Strongbox),
        new("chest_display_ice_nova", Dangerous, Strongbox),
        new("chest_spawn_rogue_exiles", Dangerous, Strongbox),
        new("all_damage_can_freeze", Dangerous, Expedition),
        new("apply_petrification_for_X_seconds_on_hit", Dangerous, Expedition),
        new("base_cold_immunity", Dangerous, Expedition),
        new("base_fire_immunity", Dangerous, Expedition),
        new("base_lightning_immunity", Dangerous, Expedition),
        new("chaos_immunity", Dangerous, Expedition),
        new("physical_immunity", Dangerous, Expedition),
        new("cannot_have_life_leeched_from", Dangerous, Expedition),
        new("cannot_have_mana_leeched_from", Dangerous, Expedition),
        new("life_mana_es_recovery_rate_+%_per_endurance_charge", Dangerous, Altar),
        new("global_defences_+%_per_frenzy_charge", Dangerous, Altar),
        new("critical_strike_multiplier_+_per_power_charge", Dangerous, Altar),
        new("random_projectile_direction", Dangerous, Altar),
        new("chaos_damage_per_minute_while_affected_by_flask", Dangerous, Altar),
        new("create_enemy_meteor_daemon_on_flask_use_%_chance", Dangerous, Altar),
        new("drain_x_flask_charges_over_time_on_hit_for_6_seconds", Dangerous, Altar),
        new("map_item_drop_quantity_+%", Neutral, Altar),
        new("map_item_drop_rarity_+%", Neutral),
        new("chance_%_to_drop_additional_divine_orb", Good, Altar),
        new("map_boss_additional_divine_orb_to_drop", Good, Altar),
        new("%_chance_to_duplicate_dropped_currency", Great, Altar),
        new("%_chance_to_duplicate_dropped_divination_cards", Great, Altar),
        new("%_chance_to_duplicate_dropped_scarabs", Great, Altar),
        new("%_chance_to_duplicate_dropped_uniques", Great, Altar),
        // Added in pull request 47 for Maven invitation and Delirium mods. Its sixth line,
        // map_damage_taken_while_stationary_+%, was left out: no mod uses it.
        new("map_cannot_evade", Dangerous),
        new("map_can_only_damage_enemies_in_X_radius", Dangerous),
        new("map_damage_taken_+%_per_nearby_ally", Dangerous),
        new("map_endgame_fog_depth", Dangerous),
        new("map_ground_lightning_base_magnitude", Dangerous),
        new("map_melee_physical_damage_taken_%_to_deal_to_attacker", Dangerous),

        // Missing map affix lines, grouped like the same stat in the PoE 2 list.
        new("map_monsters_accuracy_rating_+%", Annoying),
        new("map_monsters_additional_chaos_resistance", Annoying),
        new("map_monsters_additional_cold_resistance", Annoying),
        new("map_monsters_additional_fire_resistance", Annoying),
        new("map_monsters_additional_lightning_resistance", Annoying),
        new("map_monsters_attack_speed_+%", Dangerous),
        new("map_monsters_cast_speed_+%", Dangerous),
        new("map_monsters_movement_speed_+%", Dangerous),
        new("map_monsters_base_self_critical_strike_multiplier_-%", Annoying),
        new("map_monsters_critical_strike_chance_+%", Dangerous),
        new("map_monsters_critical_strike_multiplier_+", Dangerous),
        new("map_monsters_curse_effect_on_self_+%_final", Annoying),
        new("map_monsters_damage_+%", Dangerous),
        new("map_monsters_life_+%", Annoying),
        new("map_monsters_penetrate_elemental_resistances_%", Dangerous),
        new("map_packs_fire_projectiles", Dangerous),
        new("map_number_of_magic_packs_+%", Good),
        new("map_number_of_rare_packs_+%", Good),
        new("map_pack_size_+%", Good),
        new("map_players_cannot_gain_flask_charges", Annoying),
        new("map_ground_ice_base_magnitude", Annoying),
        new("map_base_ground_fire_damage_to_deal_per_10_seconds", Annoying),
        // Same mechanic as a PoE 2 line under a different stat id.
        new("map_monsters_%_physical_damage_to_add_as_fire", Dangerous),
        new("map_monsters_%_physical_damage_to_add_as_cold", Dangerous),
        new("map_monsters_%_physical_damage_to_add_as_lightning", Dangerous),
        new("map_monsters_%_physical_damage_to_add_as_chaos", Dangerous),
        new("map_monsters_physical_damage_%_to_add_as_random_element", Dangerous),
        new("map_monsters_poison_on_hit", Dangerous),
        new("map_monsters_additional_elemental_resistance", Annoying),
        new("map_player_charges_gained_+%", Annoying),
        new("map_monsters_maximum_life_%_to_add_to_maximum_energy_shield", Annoying),
    ];

    public static IReadOnlyList<ModColorDefault> Poe2 { get; } =
    [
        // Colored before groups existed. Removed because no waystone shows them: monster_life_+%_final_from_map
        // and map_players_cannot_gain_flask_charges (no mod uses them), map_heist_monster_life_+%_final_from_sextant
        // (only a PoE 1 heist leftover uses it) and flask_charges_gained_+% (a belt stat: it recolored belts).
        new("map_monsters_%_all_damage_to_gain_as_fire", Dangerous),
        new("map_monsters_%_physical_damage_to_gain_as_fire", Dangerous),
        new("map_monsters_%_all_damage_to_gain_as_cold", Dangerous),
        new("map_monsters_%_physical_damage_to_gain_as_cold", Dangerous),
        new("map_monsters_%_all_damage_to_gain_as_lightning", Dangerous),
        new("map_monsters_%_physical_damage_to_gain_as_lightning", Dangerous),
        new("map_monsters_%_all_damage_to_gain_as_chaos", Dangerous),
        new("map_monsters_%_physical_damage_to_gain_as_chaos", Dangerous),
        new("map_monsters_damage_+%", Dangerous),
        new("map_monster_damage_+%_final_from_deadly_atlas", Dangerous),
        new("map_monster_damage_+%_final_from_vital_atlas", Dangerous),
        new("map_monsters_attack_speed_+%", Dangerous),
        new("map_monsters_cast_speed_+%", Dangerous),
        new("map_monsters_movement_speed_+%", Dangerous),
        new("map_monsters_critical_strike_chance_+%", Dangerous),
        new("map_monsters_critical_strike_multiplier_+", Dangerous),
        new("map_monsters_chance_to_poison_on_hit_%", Dangerous),
        new("map_monsters_chance_to_inflict_bleeding_%", Dangerous),
        new("map_packs_fire_projectiles", Dangerous),
        new("map_monsters_penetrate_elemental_resistances_%", Dangerous),
        new("map_monsters_elemental_ailment_chance_+%", Dangerous),
        new("map_additional_player_maximum_resistances_%", Dangerous),
        new("map_player_life_and_es_recovery_speed_+%_final", Dangerous),
        new("map_monsters_life_+%", Annoying),
        new("map_monsters_armour_break_physical_damage_%_dealt_as_armour_break", Annoying),
        new("map_monsters_accuracy_rating_+%", Annoying),
        new("map_monsters_hit_damage_stun_multiplier_+%", Annoying),
        new("map_monsters_additional_fire_resistance", Annoying),
        new("map_monsters_additional_cold_resistance", Annoying),
        new("map_monsters_additional_lightning_resistance", Annoying),
        new("map_monsters_additional_chaos_resistance", Annoying),
        new("map_monsters_ailment_threshold_+%", Annoying),
        new("map_monsters_stun_threshold_+%", Annoying),
        new("map_base_ground_fire_damage_to_deal_per_10_seconds", Annoying),
        new("map_base_ground_fire_damage_to_deal_per_minute", Annoying),
        new("map_ground_ice_base_magnitude", Annoying),
        new("map_ground_ice", Annoying),
        new("map_ground_lightning", Annoying),
        new("map_ground_lightning_base_magnitude", Annoying),
        new("map_player_cooldown_speed_+%_final", Annoying),
        new("map_monsters_base_self_critical_strike_multiplier_-%", Annoying),
        new("map_monsters_curse_effect_on_self_+%_final", Annoying),
        new("map_monsters_curse_effect_+%", Annoying),
        new("map_monster_potency_+%_final_from_map", Good),
        new("map_monster_potency_+%", Good),
        new("map_item_drop_rarity_+%_final_from_map", Good),
        new("map_item_drop_rarity_+%", Good),
        new("map_pack_size_+%_final_from_map", Good),
        new("map_pack_size_+%", Good),
        new("map_number_of_magic_and_rare_packs_+%_final_and_rare_monster_modifiers_chance_+%_final_from_map", Good),
        new("map_number_of_magic_and_rare_packs_+%_and_rare_monster_modifiers_chance_%", Good),
        new("map_number_of_magic_packs_+%", Good),
        new("map_number_of_rare_packs_+%", Good),
        new("map_rare_monster_modifiers_chance_%", Good),
        new("map_map_item_drop_chance_+%_final_from_map", Great),
        new("map_map_item_drop_chance_+%", Great),

        // Waystone lines the game merged or renamed: these replace stats the list above already colors.
        new("map_monster_attack_cast_and_movement_speed_+%", Dangerous),
        new("map_monsters_additional_elemental_resistance", Annoying),
        new("map_player_charges_gained_+%", Annoying),
        new("map_monsters_maximum_life_%_to_add_to_maximum_energy_shield", Annoying),
    ];
}
