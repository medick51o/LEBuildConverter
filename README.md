# LE Build Converter

Windows app that converts **lastepochtools.com** builds for import into **maxroll.gg**'s Last Epoch planner.

Paste a lastepochtools URL → app fetches the build → walks you through pasting each category (equipment, passives, weaver tree, skills) into maxroll's Export/Import dialog.

## Credits

**Thanks to Frozen Sentinel** — their Runemaster build was published simultaneously to both lastepochtools.com and maxroll.gg, and we used that paired build as our **Rosetta Stone** to compare the two planner formats side-by-side. Diffing the raw JSON exports confirmed that lastepochtools' lz-string-encoded item/affix IDs decode to the exact same game-internal integers maxroll uses — and having that paired reference made the whole conversion process far easier to figure out.

## How to run

### Option 1: Run the published .exe (easiest)
The self-contained executable lives at:
```
bin\Release\net10.0-windows\win-x64\publish\LEBuildConverter.WPF.exe
```
Double-click to launch. No .NET install required — the runtime is bundled.

### Option 2: Run from source
```
cd LEBuildConverter.WPF
dotnet run
```
Requires .NET 10 SDK.

## How to use

1. Paste a lastepochtools planner URL (e.g. `https://www.lastepochtools.com/planner/BakypDvx`)
2. Click **Fetch Build** — the app will scrape the build data
3. Review the build summary (class, mastery, equipment, skills, passives)
4. Click **Start Import Wizard** to walk through the paste steps:
   - Step 1: Open maxroll planner
   - Step 2: Manually set class/mastery in maxroll (maxroll does NOT auto-switch)
   - Step 3: Copy + paste All Equipment
   - Step 4: Copy + paste Passives
   - Step 5: Copy + paste Weaver Tree (if applicable)
   - Step 6: Specialize the 5 skills in maxroll
   - Step 7-N: Copy + paste each skill tree

Each step has a **Copy to Clipboard** button and a screenshot placeholder (swap in real screenshots later by replacing the PNGs in `LEBuildConverter.WPF/Assets/screenshots/`).

## Architecture

```
LEBuildConverter_WPF/
├── LEBuildConverter.sln
├── LEBuildConverter.Core/            pure logic, no UI
│   ├── LetIdDecoder.cs               lz-string → game integer IDs
│   ├── LetFetcher.cs                 curl.exe subprocess (bypasses Cloudflare TLS fingerprinting)
│   ├── NameLookup.cs                 loads embedded JSON databases
│   ├── LetToMaxroll.cs               main conversion logic
│   ├── BuildSummary.cs               UI-friendly build representation
│   └── Data/
│       ├── le_names_data.json        (embedded, 262 KB) LET item/skill/affix names
│       └── maxroll_names_data.json   (embedded, 302 KB) maxroll cross-reference
├── LEBuildConverter.WPF/             WPF UI shell
│   ├── MainWindow.xaml               single-window multi-state UI
│   ├── ViewModels/
│   │   ├── MainViewModel.cs          app state + wizard orchestration
│   │   └── WizardStep.cs             one step in the wizard
│   └── Assets/screenshots/           placeholder PNGs (swap later)
└── LEBuildConverter.Tester/          console smoke tests
    └── Program.cs                    decoder + name lookup + fetch + convert
```

## Dependencies

- **.NET 10** (SDK for build, runtime bundled in self-contained .exe)
- **LZStringCSharp** 1.4.0 (NuGet) — pieroxy lz-string port
- **CommunityToolkit.Mvvm** 8.4.2 (NuGet) — MVVM source generators
- **curl.exe** (Windows 10+ built-in at `C:\Windows\System32\curl.exe`) — used as subprocess to bypass Cloudflare's TLS fingerprinting on lastepochtools.com. The C# HttpClient's JA3 hash gets 403'd; curl's doesn't.

## Technical notes

### Why curl.exe instead of HttpClient?
Cloudflare fingerprints TLS handshakes (JA3). .NET HttpClient's fingerprint is flagged by lastepochtools' Cloudflare rules and gets 403'd regardless of how many browser headers you spoof. `curl.exe` (bundled with every Windows 10+) has a normal fingerprint and works fine. This is a simpler solution than pulling in BoringSSL or a custom TLS library.

### Why two JSON databases (LET + maxroll)?
The IDs are identical between the two sites (game-internal integers), but the display names differ slightly (e.g. LET says "Melee Damage Leeched as Health", maxroll says "Melee Health Leech"). We load both and prefer LET names with maxroll fallback. The `rosetta.py` cross-reference during research validated that 468/468 uniques match by ID, 1498/1498 items match, and 0 ID collisions exist.

### Why isn't `uniqueRolls` / `implicits` in the output JSON?
Maxroll expects exact element counts per unique item (head wants `[1,1]`, body wants `[1,1,1,1]`, etc.) and LET's source data uses fixed-size placeholder arrays that lie about the count. Omitting these fields entirely lets maxroll supply correct defaults on import. Verified in the Python POC — items still populate correctly with all stats.

### Why do I have to manually set class/mastery?
Maxroll's planner does NOT auto-switch class based on the passives JSON's `class`/`mastery` fields. The passives tree node IDs are namespaced per class — pasting Runemaster passives into a Primalist planner silently fails. The wizard explicitly tells you which class/mastery to pick for this reason.

## Known limitations

- Idol grid positioning uses `y*5 + x` as a placeholder formula — may not match maxroll's actual grid addressing for all idol shapes
- Skill tree IDs are hardcoded to ones we've seen (`sb44eQ`, `fl71ds`, `rn7iv`, `fw3d`, `vm53dx`) — other classes' skills will resolve via the embedded name database, no hardcoding needed
- Screenshots are placeholder PNGs — replace them in `Assets/screenshots/` with real maxroll UI captures
- curl.exe dependency means running on pre-Win10 systems requires installing curl from https://curl.se/windows/
