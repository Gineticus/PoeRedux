---
name: PoE Redux
description: A dark Windows tool that speaks the loot filter's color language while it patches Path of Exile's own files.
colors:
  stage: "#0B0C0F"
  panel: "#14161B"
  raised: "#1B1D23"
  row-hover: "#16181E"
  hairline: "#262932"
  outline-stroke: "#3A3E49"
  outline-stroke-hover: "#4A4F5C"
  text: "#ECECEC"
  text-secondary: "#A9ADB6"
  text-muted: "#8A8F99"
  text-disabled: "#5C616B"
  accent: "#4AA8FF"
  accent-hover: "#6DBAFF"
  accent-pressed: "#2E8FE6"
  accent-disabled-bg: "#1D2A38"
  accent-disabled-text: "#7F93A8"
  focus-ring: "#8CCBFF"
  warning: "#F0B429"
  danger: "#FF6B5E"
  danger-button: "#5A1C17"
  danger-button-hover: "#702219"
  danger-button-text: "#FFE1DD"
  success: "#7BDB6E"
  notice-success-border: "#2F5A2A"
  notice-warning-border: "#6B5420"
  notice-error-border: "#7A2C25"
  game-mod-blue: "#8888FF"
  game-tooltip-body: "#050505"
  game-tooltip-header: "#1A1712"
  game-tooltip-rule: "#4A4236"
  game-tooltip-name: "#E0D5BD"
  game-tooltip-property: "#7F7F7F"
  code-frame: "#101217"
  code-text: "#C9CCD3"
  group-dangerous: "rgb(209,46,46)"
  group-annoying: "rgb(255,204,27)"
  group-good: "rgb(74,230,58)"
  group-great: "rgb(14,186,255)"
  group-neutral: "rgb(227,158,255)"
typography:
  title:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Inter, sans-serif"
    fontSize: "20px"
    fontWeight: 600
  heading:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Inter, sans-serif"
    fontSize: "15px"
    fontWeight: 600
  body:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Inter, sans-serif"
    fontSize: "14px"
    fontWeight: 400
  row-name:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Inter, sans-serif"
    fontSize: "14px"
    fontWeight: 600
  label:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Inter, sans-serif"
    fontSize: "12.5px"
    fontWeight: 600
  small:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Inter, sans-serif"
    fontSize: "12.5px"
    fontWeight: 400
  chip:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Inter, sans-serif"
    fontSize: "12.5px"
    fontWeight: 700
  chip-small:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Inter, sans-serif"
    fontSize: "11px"
    fontWeight: 700
  tooltip-line:
    fontFamily: "Segoe UI Variable Text, Segoe UI, Inter, sans-serif"
    fontSize: "13.5px"
    fontWeight: 400
  path:
    fontFamily: "Cascadia Mono, Consolas, monospace"
    fontSize: "13px"
    fontWeight: 400
  code:
    fontFamily: "Cascadia Mono, Consolas, monospace"
    fontSize: "12.5px"
    fontWeight: 400
rounded:
  chip: "3px"
  segment: "4px"
  control: "5px"
  panel: "6px"
spacing:
  hairline-gap: "2px"
  xs: "6px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "24px"
components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.stage}"
    typography: "{typography.row-name}"
    rounded: "{rounded.control}"
    padding: "10px 22px"
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
    textColor: "{colors.stage}"
  button-primary-pressed:
    backgroundColor: "{colors.accent-pressed}"
    textColor: "{colors.stage}"
  button-primary-disabled:
    backgroundColor: "{colors.accent-disabled-bg}"
    textColor: "{colors.accent-disabled-text}"
  button-outline:
    backgroundColor: "transparent"
    textColor: "{colors.text}"
    rounded: "{rounded.control}"
    padding: "7px 14px"
  button-outline-hover:
    backgroundColor: "{colors.raised}"
  button-outline-disabled:
    backgroundColor: "transparent"
    textColor: "{colors.text-muted}"
  button-text:
    backgroundColor: "transparent"
    textColor: "{colors.text-secondary}"
    rounded: "{rounded.control}"
    padding: "5px 8px"
  button-text-hover:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
  button-text-disabled:
    backgroundColor: "transparent"
    textColor: "{colors.text-disabled}"
  button-danger:
    backgroundColor: "{colors.danger-button}"
    textColor: "{colors.danger-button-text}"
    rounded: "{rounded.control}"
    padding: "7px 14px"
  button-danger-hover:
    backgroundColor: "{colors.danger-button-hover}"
    textColor: "#FFFFFF"
  segment:
    backgroundColor: "transparent"
    textColor: "{colors.text-secondary}"
    rounded: "{rounded.segment}"
    padding: "6px 14px"
  segment-checked:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.stage}"
  segment-track:
    backgroundColor: "{colors.panel}"
    rounded: "{rounded.panel}"
    padding: "3px"
  patch-row:
    backgroundColor: "transparent"
    textColor: "{colors.text}"
    rounded: "{rounded.control}"
    padding: "9px 12px"
  patch-row-hover:
    backgroundColor: "{colors.row-hover}"
  patch-row-selected:
    backgroundColor: "{colors.raised}"
  panel:
    backgroundColor: "{colors.panel}"
    rounded: "{rounded.panel}"
    padding: "20px 24px"
  text-field:
    rounded: "{rounded.control}"
  chip:
    typography: "{typography.chip}"
    rounded: "{rounded.chip}"
    padding: "4px 10px"
  chip-small:
    typography: "{typography.chip-small}"
    rounded: "{rounded.chip}"
    padding: "1px 6px"
  game-tooltip:
    backgroundColor: "{colors.game-tooltip-body}"
    typography: "{typography.tooltip-line}"
    padding: "10px 16px"
    width: "240px"
  code-frame:
    backgroundColor: "{colors.code-frame}"
    textColor: "{colors.code-text}"
    typography: "{typography.code}"
    rounded: "{rounded.segment}"
    padding: "10px 16px"
---

# Design System: PoE Redux

## Overview

**Creative North Star: "The Loot Filter"**

PoE Redux is a dark operating window that speaks the color language players already read in a loot filter. A mod group is a drop label: its color as text and border over a dark tint of the same hue. Everything around those labels stays quiet so the group inks and the game's own tooltip are the loudest things on screen. The window is a near-black stage with one raised panel tone, white text, and a single light blue that means "the main action" or "switched on".

Density is that of a working tool: a fixed 400 px patch list on the left, the selected patch's settings in one raised panel on the right, a header bar for the game and file, a footer for backup state and Apply. The game's item tooltip is reproduced faithfully (near-black body, warm brown rules, centered lines) and is the one place the window borrows the game's materials instead of its own.

The Windows 11 title bar is painted in the stage color with light text, so the window reads as one surface from caption to footer.

**Key Characteristics:**
- Near-black stage, one panel tone, one raised tone for selection; depth by tone, not shadow.
- One accent (light blue) for the primary action, checked segments, toggles, sliders, progress, caret.
- Mod group colors come from the user's data and appear only as drop-label chips, colored mod text and the tooltip preview.
- One sans family at four sizes; monospace only for file paths and game file text.
- Hand-drawn 16 px open-stroke icons.

## Colors

A cool near-black neutral ladder with one light blue accent, plus status colors and a borrowed game palette that lives only inside game frames.

### Primary
- **Filter Light Blue** (accent): the Apply button, the checked PoE 1 / PoE 2 segment, ToggleSwitch on state, slider fill, progress bar, text caret and selection tint (`#664AA8FF`). Hover lightens to accent-hover, press deepens to accent-pressed. Text on it is the stage color, never white.
- **Focus Ice** (focus-ring): the 2 px keyboard focus ring on segments and patch rows.

### Neutral
- **Stage** (stage): window background and Windows 11 caption; also the text color on any accent fill.
- **Panel** (panel): the settings panel, the segment track, notice banners.
- **Raised** (raised): the selected patch row, hover fill of outline and text buttons, segment hover.
- **Row Hover** (row-hover): a patch row under the pointer, one step above stage.
- **Hairline** (hairline): 1 px rules under the header and above the footer, disabled outline borders, code frame border.
- **Outline Stroke** (outline-stroke, outline-stroke-hover): the 1 px border of secondary buttons.
- **Text / Secondary / Muted / Disabled**: body text; group labels, notes and backup state; descriptions, summaries and hints at 12.5 px; disabled text buttons.

### Status
- **Warning Amber** (warning): warning notice icon only. Amber is never an action color.
- **Danger Coral** (danger): error notice icon, invalid path field border. Destructive confirmation buttons use the darker danger-button fill with pale text.
- **Success Green** (success): success notice icon and the update-available link.
- Notice banners keep the panel fill and take a dark border of their status hue (notice-success-border, notice-warning-border, notice-error-border).

### Mod Group Inks
Defaults shipped in the data, editable by the user: Dangerous red, Annoying yellow, Good green, Great blue, Neutral violet. Each chip fills with the group color mixed 16% into the stage. **Game Mod Blue** (game-mod-blue) is the color the game prints unrecolored mod lines in, used for unchecked lines.

### Game Frame
Body, header, rule, item name and property grey of the in-game tooltip, plus the cooler code frame for game file text. These values belong to tooltip frames only.

### Named Rules
**The One Accent Rule.** Light blue marks exactly two meanings: the one main action on screen, and the on state of a control. Nothing else is blue except mod data.

**The Data Owns Its Colors Rule.** Group colors are user data. The chrome never takes a group hue, and a group hue is never hard-coded into a view.

**The Game Frame Rule.** Warm browns and the tooltip greys appear only inside a game tooltip frame; they never leak into the app's own chrome.

## Typography

**UI Font:** Segoe UI Variable Text (with Segoe UI, Inter, sans-serif)
**Path/Code Font:** Cascadia Mono (with Consolas, monospace)

**Character:** The Windows system voice, set tight and plain, so the colored mod text and the game tooltip carry the personality.

### Hierarchy
- **Title** (600, 20 px): the selected patch name at the top of the settings panel; confirm dialog titles.
- **Heading** (600, 15 px): setting group names inside a panel and flyout titles.
- **Body** (400, 14 px): default window text; patch names at 600.
- **Label** (600, 12.5 px, secondary color): list group labels (Readability, Map and camera, Visual noise; mod categories; Before / After). Sentence case.
- **Small / Muted** (400, 12.5 px): patch descriptions, summaries, hints.
- **Chip** (700, 12.5 px; 11 px on the small line chip).
- **Tooltip line** (400, 13.5 px, centered, wrapping): lines inside the game tooltip; the item name at 16 px.
- **Path** (mono 13 px) for the game file field; **Code** (mono 12.5 px, left aligned) for game file text in the Before / After frames.

### Named Rules
**The Tabular Count Rule.** Every count, percentage and progress stage uses tabular figures (`+tnum`), so numbers in the list and footer do not jitter.

**The One Family Rule.** One sans family at 20 / 15 / 14 / 12.5; monospace only where the content is a path or a game file.

## Layout

Fixed desktop window: 1240 x 800 by default, minimum 1120 x 660. Four rows: a 52 px header (game switch, path field, Browse), an optional notice banner, the body, and the footer. The body is a 400 px scrolling patch list beside one panel that fills the rest, inset 16 px from the window edge. Inside Color Mods, the mod list takes the remaining width beside a 340 px preview column with a 24 px gutter.

Spacing runs on 2 / 6 / 8 / 12 / 16 / 24 px: 2 px inside segment tracks, 8 px between icon and label and between adjacent controls, 12 px row and list padding, 16 px window gutters, 24 px panel side padding and dialog margins. Settings views cap their column at 520 to 560 px and align left.

## Elevation & Depth

Flat. Depth is three tones (stage, panel, raised) and 1 px hairlines; there are no drop shadows. The only BoxShadow in the build is the focus ring.

### Shadow Vocabulary
- **Focus ring** (`0 0 0 2px #8CCBFF`, inset on patch rows): keyboard focus only.

### Named Rules
**The Tone Not Shadow Rule.** A surface rises by moving one step up the neutral ladder, never by casting a shadow.

## Shapes

Small, quiet radii that tighten as elements shrink: 6 px for panels, banners and the segment track; 5 px for buttons, fields and patch rows; 4 px for segments, code frames and the minimap preview; 3 px for chips; 2 px for the 12 px color swatch in the group menu. The game tooltip keeps square corners, as in the game. Chips carry a 2 px border (1.5 px on the small chip); everything else uses 1 px strokes.

Icons are hand-drawn open strokes in a 16 x 16 box, 1.5 px stroke, round caps and joins, inheriting the button's foreground and dimming to 40% when the button is disabled.

## Components

### Buttons
- **Shape:** gently rounded (5 px).
- **Primary:** light blue fill, stage-colored bold text, 10 x 22 px padding; one per window (Apply), bound to Ctrl+Enter. Disabled turns to a dim navy slab with grey-blue text rather than grey.
- **Outline:** transparent with a 1 px outline stroke, 7 x 14 px; hover fills raised. Used for Browse, Restore Original, Add.
- **Text:** borderless secondary text, 5 x 8 px; hover fills raised and brightens text. Used for small actions (View changes, Reset to defaults, Open log, icon-only dismiss and delete).
- **Danger:** dark red fill with pale text, only on the destructive confirm in a dialog.
- **Disabled:** secondary buttons recede to muted text and a hairline border instead of a grey slab.

### Segmented Game Switch
Two radio buttons on a 3 px padded panel track (6 px radius). Unchecked: secondary text, raised on hover. Checked: accent fill with stage text. Focus: 2 px ice ring.

### Patch Row
A full-width selectable row (5 px radius, 9 x 12 px padding): ToggleSwitch, name at 600 over a muted one-line description, and a muted tabular summary on the right. Hover row-hover, selected raised, focus inset ice ring. Selecting a row shows its settings; the toggle decides whether Apply uses it.

### Panel
The settings area: panel fill, 6 px radius, 20 x 24 px padding, title and wrapped notes (max 720 px) above the patch's own view.

### Notice Banner
Panel fill, 6 px radius, 1 px border in a dark status hue, 16 px status icon, selectable wrapping text, text buttons for Open log and dismiss.

### Inputs / Fields
- **Style:** Fluent TextBox at 5 px radius; the game path field is monospace 13 px. Caret is accent, selection is accent at 40%.
- **Error:** border turns danger coral; the problem is in the tooltip.

### Chips (signature)
- **Style:** the loot filter drop label. Group color for the text and a 2 px border, background the group color mixed 16% into the stage, 3 px radius, 4 x 10 px padding, bold 12.5 px, with a tabular count at 80% opacity.
- **Small:** on each mod line, 1.5 px border, 1 x 6 px padding, 11 px, so the group never depends on color alone. Opens a menu of groups with 12 px swatches.

### Game Tooltip Preview (signature)
A reproduction of the in-game item tooltip: #050505 body, 1 px warm brown border, a warm header strip with the item name centered at 16 px, a grey tier line, a warm rule inset 24 px, then mod lines centered in their group colors. Min width 240 px. It redraws live as groups and colors change. The same frame in a cool, rounded code variant shows game file text left-aligned in monospace for Before / After.

## Do's and Don'ts

### Do:
- **Do** keep light blue (#4AA8FF) for the single primary action and for on states; put stage-colored text on it.
- **Do** show a mod group as a drop-label chip: hue text, 2 px hue border, hue mixed 16% into the stage.
- **Do** raise surfaces by tone (stage, panel #14161B, raised #1B1D23) and separate regions with 1 px hairlines.
- **Do** use tabular figures for every count and percentage.
- **Do** draw new icons as 16 px open strokes, 1.5 px, round caps, inheriting the foreground.
- **Do** show mod lines in the game's own words and colors, with Game Mod Blue for lines left unchanged.

### Don't:
- **Don't** use amber (#F0B429) for anything but warnings.
- **Don't** add drop shadows; the only shadow is the focus ring.
- **Don't** use the tooltip's warm browns outside a game tooltip frame.
- **Don't** give a mod group meaning by color alone; it always carries its name in a chip.
- **Don't** add a second filled accent button to a screen; other actions are outline or text buttons.
