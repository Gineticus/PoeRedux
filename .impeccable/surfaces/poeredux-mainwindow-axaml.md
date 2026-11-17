---
version: 1
slug: "poeredux-mainwindow-axaml"
primary_target: "PoeRedux/MainWindow.axaml"
related_targets: []
---

# Main window

Scope: the PoE Redux desktop window (Avalonia) and the panels it opens: patch list, per-patch settings, Mod Colors editor, backup and restore. Mode: Operate.

Audience and job: PoE 1 and PoE 2 players. Quick path: pick game, pick Content.ggpk or _.index.bin, tick patches, Apply. Tuning path: open a patch's settings, change colors, zoom, reveal, kept folders, Apply again, Restore Original when needed.

Constraints: native Avalonia controls only (CheckBox, ToggleSwitch, Slider, ComboBox, TextBox, ListBox, Button); keyboard reachable; no modal for settings that fit beside the list; English UI; texts through ux-copy and ai-text-signs.

## Direction contract

THESIS: The window speaks the loot filter's color language: a mod group is a drop label, and the player sees on a waystone preview exactly how the game will print each line. It refuses the grey settings list with one accent.

OWN-WORLD: Near-black stage (#0b0c0f) with one raised panel tone (#14161b, #1b1d23 for selection); white text; light blue (#4AA8FF) for the primary action and checked state, pinned by the user on 18 Nov 2026 in place of amber; amber (#F0B429) only for warnings; mod group inks as drop-label chips: colored text on a dark tint of the same hue with a 2 px border of the hue. Segoe UI throughout, tabular figures for counts.

STORY: The player sees which game and file are loaded, which patches are on and what each changes, previews mod colors as the game will show them, applies, and can always restore the originals.

FIRST VIEWPORT: Top bar: name, PoE 1 / PoE 2 switch, game file path with Browse. Left column (about 460 px): patch list grouped Readability / Map and camera / Visual noise, each row a toggle, name and a short value (zoom 2.4, 5 of 7). Right area: the selected patch's settings; for Color Mods the group chips and a waystone or map tooltip preview with real mod lines in group colors. Footer: backup state and Restore Original on the left, Apply N patches in light blue on the right.

FORM: Loot Filter Tiers, my top-ranked grounded candidate (position 1 on the list), chosen by the user as the pick card. seed:b5e264db2d2ad2cb

SIGNATURE: the drop-label chip and the live tooltip preview that redraws as groups and colors change.

FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance
