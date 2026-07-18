# Changelog

All notable changes to YtypGrouper will be documented in this file.

---

## [0.1.0] — 2025-07-18 — Initial Release

### Overview
YtypGrouper is a standalone C#/Avalonia tool that sorts GTA V YMAP and YTYP entity lists by their **source `.ytyp` file**, rather than alphabetically by archetype name. It resolves every archetype to its origin YTYP (vanilla or mod) via a full `GameFileCache` scan, then reorders the `<Item>` entries accordingly — preserving the complete file structure including MLO room/portal `attachedObjects` remapping.

---

### Added

#### Core
- **YTYP-based sorting** — entities are grouped and ordered alphabetically by the name of their source `.ytyp` file (vanilla or mod), instead of alphabetically by `archetypeName`.
- **MLO support** — full MLO entity sorting with automatic `attachedObjects` remapping in `rooms` and `portals` after reordering, matching the index changes exactly.
- **Internal MLO props label** — entities whose archetype is defined in the same YTYP as the MLO itself are tagged `(internal)` in the result summary.
- **Unresolved entities** — archetypes that cannot be resolved in the GameFileCache (missing mod, unknown hash) are placed at the end of the entity list under `(unresolved)`.
- **Sort summary** — after each operation, the UI displays a breakdown: entity count per source YTYP group, output path, and unresolved count with a warning.

#### File format support
- **CodeWalker XML export** (`.ymap.xml` / `.ytyp.xml`) — reads and writes XML with indented formatting, `UTF-8` without BOM.
- **Binary `.ymap`** — loads raw binary YMAP via `YmapFile.Load(byte[])`, sorts `AllEntities` in place, and re-exports binary via `YmapFile.Save()`.
- **Binary `.ytyp`** — loads raw binary YTYP via `YtypFile.Load(byte[])`, sorts MLO entities in place, and re-exports binary via `YtypFile.Save()`.

#### GameFileCache integration
- Full GTA V game cache loading (vanilla + optional mods) via `CodeWalker.Core` — same pattern as TexturesTesting.
- Configurable DLC string (`mp2024_01_g9ec`), mods folder exclusions (`Installers;_CommonRedist`).
- Audio, vehicle and ped loading disabled for faster cache init.
- GTA V path and output directory persisted in `config.ini` next to the executable and restored on next launch.

#### UI
- **GTA V path picker** — loads and caches the game path; asks once whether to enable the mods folder via a styled in-theme dialog.
- **Output folder picker** — user chooses the export destination at runtime (no longer forced to an `export/` subfolder next to the executable); path is saved and restored.
- **File type filter** — picker offers four options: all supported files, YMAP binary, YTYP binary, CodeWalker XML.
- **Dark red/orange theme** — custom Avalonia styles with `#E05020` / `#FF6633` accent, `#140E0E` background, `#AA8880` secondary text.
- **Segoe UI typography** — matches CodeWalker's system font stack (`Segoe UI, Tahoma, Arial`); result console uses `Consolas` for technical output.
- **Custom confirm dialog** — "Enable mods?" prompt is a native Avalonia window that respects the app's design language (no third-party MessageBox theme bleed).
- **Logo slot** — `ApplicationIcon` line present and commented in `.csproj`; drop `logo.ico` next to the `.csproj` and uncomment to activate.
- `x64` platform target enforced in both Debug and Release configurations to match `CodeWalker.Core.dll` (AMD64).

---

### Dependencies
| Package | Version |
|---|---|
| Avalonia | 11.1.3 |
| Semi.Avalonia | 11.1.0.3 |
| CodeWalker.Core | 1.0.3 |
| MessageBox.Avalonia | 3.1.6 |
| Costura.Fody | 5.7.0 |
| Salaros.ConfigParser | 0.3.8 |
| Microsoft.Extensions.Logging | 8.0.0 |

---

### Known limitations
- Binary `.ymap` export re-serialises all entities via `BuildCEntityDefs()` — LOD hierarchy links (`Parent`, `Children`) are preserved in memory but callers should verify in-game if the YMAP contains complex LOD chains.
- Archetypes from mods installed outside the standard GTA V `mods/` folder structure will appear as `(unresolved)` unless the mod path is included in the game cache scan.
- The app must be run as **x64** — the `CodeWalker.Core` NuGet package targets AMD64 only.
