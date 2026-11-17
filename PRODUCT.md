# Product

<!-- impeccable:product-schema 1 -->

## Platform

desktop (Windows). Not one of the schema's web/ios/android/adaptive values: PoE Redux is a native Windows desktop app built with Avalonia.

## Users

Path of Exile 1 and Path of Exile 2 players, in two equal groups:

- Players who open PoE Redux after a game update, pick patches, apply them, and go back to the game. They want readable map mods, less visual noise, more FPS, and a game that still starts.
- Players who tune each patch: which mods get which color, how far the camera zooms out, how strongly the minimap reveals, which effect folders stay untouched. They restore originals and re-apply often.

The simple path (choose game, choose file, choose patches, apply) must work without opening any settings; the settings are there for the second group.

## Product Purpose

PoE Redux edits the game's own files (Content.ggpk for the standalone client, _.index.bin for Steam and Epic) to change what the game shows: colors of chosen map and waystone mods, default camera zoom, minimap reveal, monster life bars, and removal of fog, particles, effects, light and other visual noise. Every change can be undone: the original bytes of every changed file are kept in a backup and written back by Restore Original.

Success means the patched game starts, plays, and looks the way the user chose, and Restore Original returns it to the files it had before.

## Positioning

It works on the game's files directly, for both games, from one small Windows program, with backups of every original it replaces. Every file and mod it targets is checked against the current game data, not taken on trust.

## Operating Context

- Used between play sessions. A game update replaces patched files, so patches are applied again after each update.
- Standalone installs are patched through Content.ggpk (PoE 1 about 70 GB, PoE 2 about 150 GB); Steam and Epic installs through Bundles2/_.index.bin.
- Backups live in %LOCALAPPDATA%\PoeRedux\Backups, one set per game. Verifying game files with PackCheck or Steam makes them obsolete.

## Capabilities and Constraints

- Windows 10 version 2004 or later; shipped as one self-contained x64 exe built by .github/workflows/release.yml on a v-tag.
- .NET 10, LibGGPK3 2.7.5 for reading and writing the game index, native oo2core.dll for Oodle; the exe disables CET because oo2core.dll is not CET-aware (issue #29).
- No network access except the GitHub release check for PoE Redux updates. Patch-server and CDN features were considered and declined.
- An apply is all-or-nothing: every selected patch is read and patched in memory, then written, then the index is saved once.
- Restore Original must not write an old original over a file the game has replaced since the apply; doing so mixes two game versions.
- UI language is English. Terms: "PoE 1", "PoE 2", "patch", "Apply Patches", "Restore Original", "backup".

## Brand Commitments

- Name: PoE Redux. Icon: icon.ico in the repository root.
- No mention of AI tools in commits, pull requests or the product.

## Evidence on Hand

- README.md lists every patch per game; Images/window.png shows the current WPF window.
- Real game data of both games was extracted and checked on 2026-11-18. There are no testimonials, download counts or reviews to quote, and none may be invented.

## Product Principles

1. The game must still start. A patch that cannot prove its target exists in the current game data does not run, and the user is told which patch found nothing.
2. Every change is reversible, and reversing it never mixes game versions.
3. The quick path needs no settings; depth is one step away, never in the way.
4. Show the game's own words: mods appear as the text the game prints, not as internal stat ids.
5. Nothing leaves the machine except the update check.
