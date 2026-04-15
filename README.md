# LE Build Converter

**Convert lastepochtools.com builds for import into maxroll.gg's Last Epoch planner.**

Paste a lastepochtools planner URL, the app fetches the build, then walks you through step-by-step instructions to paste each category (gear, idols, blessings, passives, weaver tree, skills) into maxroll's Export/Import dialog.

This is useful for:
- Testing builds from YouTube guides without grinding for the items
- Getting a build onto maxroll so Ash's LE_hud mod can spawn the gear in-game instantly
- Using maxroll's loot filter system with builds that were designed on lastepochtools

## Download

**[Latest release](https://github.com/medick51o/LEBuildConverter/releases/latest)** — grab `LEBuildConverter_v1.0.0.zip`, extract, double-click `LEBuildConverter.WPF.exe`.

Windows 10/11 64-bit. No .NET install required — the runtime is bundled. Single self-contained 62 MB .exe.

On first launch Windows SmartScreen may show a "Windows protected your PC" popup because the .exe isn't code-signed. Click **More info → Run anyway**.

## How to use

1. **Create a maxroll.gg account** and log in. You need this to save the imported build — without it, the build is throwaway.
2. Launch the app and paste a lastepochtools planner URL (e.g. `https://www.lastepochtools.com/planner/BakypDvx`).
3. Click **Fetch Build**. The app scrapes the build and shows a summary — class, mastery, equipment, skills, passives, blessings — all with real item names.
4. Click **Start Import Wizard →**. Each step has a **Copy to Clipboard** button and a screenshot showing exactly what to click in maxroll.
5. Follow the wizard: open maxroll → set class/mastery → paste each category → specialize the 5 skills → paste each skill tree → save the build.
6. After saving, you'll get a shareable maxroll URL. Paste it into Ash's LE_hud mod gear spawner, generate a maxroll loot filter, or just archive it.

## Technical details

### How it works
1. Scrapes lastepochtools' internal planner API via `curl.exe` (bypasses their Cloudflare TLS fingerprinting that blocks .NET HttpClient).
2. Decodes lastepochtools' lz-string-encoded item/affix/blessing/idol IDs back to game-internal integers using a port of pieroxy's LZString library.
3. Maps the decoded IDs to human-readable names via two embedded databases (LET and maxroll) totalling ~564 KB — covering 1498 items, 468 uniques, 1112 affixes, 1134 skills, and all passive tree nodes.
4. Rebuilds the data in maxroll's JSON planner format (the same format their Export/Import dialog accepts).
5. Walks you through pasting each category into maxroll via a data-driven wizard.

### Why the two-step paste wizard instead of a direct import
Maxroll's API for creating builds requires authentication and isn't documented publicly. The Export/Import dialog is the sanctioned public path. Pasting into it is manual but works for every user without any auth/API integration.

### Stack
- **.NET 10** / WPF / C#
- **LZStringCSharp** — reference lz-string port (NuGet)
- **CommunityToolkit.Mvvm** — MVVM source generators (NuGet)
- **Embedded JSON databases** extracted from lastepochtools + maxroll's data files

## Credits

**Thanks to Frozen Sentinel** — their Runemaster build was published simultaneously to both lastepochtools.com and maxroll.gg, and we used that paired build as our **Rosetta Stone** to compare the two planner formats side-by-side. Diffing the raw JSON exports confirmed that lastepochtools' lz-string-encoded item/affix IDs decode to the exact same game-internal integers maxroll uses — and having that paired reference made the whole conversion process far easier to figure out.

**Thanks to BinaQc and the maxroll team** — the loot filter system they built for this season is the reason this tool exists at all. I kept wanting to pull builds from lastepochtools into maxroll just to use their loot filters, and after doing it manually one too many times, I finally built this thing. The maxroll loot filters are the best in the game.

**AaronActionRPG is cool** — he did nothing to help with this tool, but he's a staple of the Last Epoch content creator community and I figured I'd shout him out anyway.

## Building from source

Requires **.NET 10 SDK**.

```bash
git clone https://github.com/medick51o/LEBuildConverter.git
cd LEBuildConverter
dotnet build
dotnet run --project LEBuildConverter.WPF
```

To produce a single-file self-contained .exe:

```bash
dotnet publish LEBuildConverter.WPF/LEBuildConverter.WPF.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:IncludeAllContentForSelfExtract=true
```

## License

MIT — see LICENSE.

## Known limitations

- Idol grid positioning uses a placeholder formula (`y*5 + x`); may not match maxroll's actual grid for edge-case idol shapes
- Cloudflare sometimes rate-limits lastepochtools. If a fetch fails, wait 30 seconds and try again.
- The maxroll class/mastery must be set manually before importing passives. The app's Summary page shows a prominent warning banner with the required mastery.
