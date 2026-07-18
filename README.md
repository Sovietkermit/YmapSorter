# Changelog

All notable changes to YtypGrouper will be documented in this file.

---
## [0.1.0] — 2026-01-19 — Py Initial Release

## [1.0] — 2026-07-18 — New Release

# YtypGrouper
 
> A standalone Windows tool that sorts GTA V YMAP and YTYP entity lists by their source `.ytyp` file — vanilla or mod.
 
![YtypGrouper UI](https://i.imgur.com/zytAB4p.png)
 
---
 
## What is it?
 
When building MLOs or working with YMAPs that reference props from multiple packs, your entity list quickly becomes a mix of vanilla props, mod props, and interior-specific archetypes — all unsorted.
 
**YtypGrouper** resolves each entity's `archetypeName` back to the `.ytyp` file that defines it (by scanning the full GTA V game cache, vanilla + mods), then reorders the entity list so that:
 
- All entities from the same `.ytyp` source are grouped together
- Groups are sorted alphabetically by source YTYP filename
- Within a group, entities are sorted alphabetically by `archetypeName`
- Unresolved archetypes (missing mod, unknown hash) are placed at the end
For **MLOs**, the tool also automatically remaps `attachedObjects` indices in `rooms` and `portals` after reordering — your interior stays fully functional.
 
---
 
## Features
 
- Sorts by **source YTYP** (not just alphabetically by archetype name)
- Full **MLO support** with `attachedObjects` remapping in rooms and portals
- Supports raw binary **`.ymap`** and **`.ytyp`** directly — no CodeWalker XML export required
- Also supports **CodeWalker XML exports** (`.ymap.xml` / `.ytyp.xml`)
- Binary in → binary out / XML in → XML out
- Resolves archetypes from **vanilla + mods** via full GameFileCache scan
- Internal MLO props labelled `(internal)` in the result summary
- Output folder chosen freely at runtime, path saved between sessions
- GTA V path saved automatically on first setup
---
 
## Requirements
 
- Windows x64
- GTA V installation
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
---
 
## How to use
 
**1. First launch — set your GTA V path**
 
Go to **File → Select GTA V Path** and point to your GTA V folder (the one containing `GTA5.exe`).
 
**2. Enable mods (optional)**
 
A prompt will ask whether to include your `mods/` folder. Say **Yes** if you want mod archetypes to be resolved — required if your YMAP or YTYP references props from installed mods.
 
**3. Wait for cache load**
 
YtypGrouper scans all RPF archives to build a complete archetype index. This can take a minute depending on your installation size and number of mods. The status bar at the bottom shows progress.
 
**4. Choose your output folder**
 
Click **Choose folder…** to pick where sorted files will be saved. This is remembered between sessions.
 
**5. Open and sort**
 
Click **Open File to Sort** and pick your file:
 
| Format | Input | Output |
|---|---|---|
| Binary YMAP | `.ymap` | `.ymap` (sorted) |
| Binary YTYP | `.ytyp` | `.ytyp` (sorted) |
| CodeWalker XML | `.ymap.xml` / `.ytyp.xml` | `.xml` (sorted) |
 
The result panel shows a breakdown of entity counts per source YTYP and flags any unresolved archetypes.
 
---
 
## Adding your own icon
 
In `YtypGrouper/YtypGrouper.csproj`, find the commented line:
 
```xml
<!-- <ApplicationIcon>logo.ico</ApplicationIcon> -->
```
 
Place your `logo.ico` file in the same folder as the `.csproj`, uncomment the line, and rebuild.
 
---
 
## Building from source
 
```bash
git clone https://github.com/sovietkermit/YtypGrouper.git
cd YtypGrouper
dotnet build -c Release
```
 
Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
 
---
 
## Dependencies
 
| Package | Version | License |
|---|---|---|
| [CodeWalker.Core](https://www.nuget.org/packages/CodeWalker.Core) | 1.0.3 | MIT |
| [Avalonia](https://avaloniaui.net) | 11.1.3 | MIT |
| [Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia) | 11.1.0.3 | MIT |
| [MessageBox.Avalonia](https://github.com/AvaloniaCommunity/MessageBox.Avalonia) | 3.1.6 | MIT |
| [Costura.Fody](https://github.com/Fody/Costura) | 5.7.0 | MIT |
| [Salaros.ConfigParser](https://github.com/salaros/config-parser) | 0.3.8 | MIT |
 
---
 
## Credits
 
**Author**
- [sovietkermit](https://github.com/sovietkermit)
**Inspirations & references**
- [Hancapo](https://github.com/Hancapo) — [TexturesTesting](https://github.com/Hancapo/TexturesTesting), which provided the GameFileCache integration pattern and the archetype-to-asset resolution approach this tool builds on
- [dexyfex](https://github.com/dexyfex) — [CodeWalker](https://github.com/dexyfex/CodeWalker), the foundation for all GTA V file parsing (`YmapFile`, `YtypFile`, `GameFileCache`, `MloArchetype`, archetype resolution)
**Libraries**
- [Avalonia UI](https://avaloniaui.net) — cross-platform UI framework
- [Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia) — UI theme
---
 
## License
 
MIT — see [LICENSE](LICENSE) for details.
