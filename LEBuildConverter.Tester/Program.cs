using System.Text.Json;
using LEBuildConverter.Core;

Console.WriteLine("=== LEBuildConverter.Core smoke test ===\n");

// ── 1. Decoder ──
Console.WriteLine("-- LetIdDecoder --");
AssertEq(LetIdDecoder.DecodeItemId("UAzBMBYEZSA").SubTypeId, 2, "head subType");
AssertEq(LetIdDecoder.DecodeItemId("UAzBMBYEZSA").UniqueId, 412, "head uniqueId");
AssertEq(LetIdDecoder.DecodeItemId("IIwBhBYWAmMSA").BaseTypeId, 4, "hands baseType");
AssertEq(LetIdDecoder.DecodeItemId("IIwBhBYWAmMSA").SubTypeId, 12, "hands subType");
AssertEq(LetIdDecoder.DecodeAffixId("AKwBgTEA"), 502, "affix 502");
AssertEq(LetIdDecoder.DecodeAffixId("AIwBmA4g"), 1018, "affix 1018");
AssertEq(LetIdDecoder.DecodeAffixId("AAwNgnEA"), 69, "affix 69");
Console.WriteLine("  decoder OK\n");

// ── 2. NameLookup ──
Console.WriteLine("-- NameLookup --");
var names = NameLookup.Instance;
AssertEq(names.UniqueName(412), "Dominance of the Tundra", "unique 412");
AssertEq(names.AffixName(502), "Intelligence", "affix 502");
AssertEq(names.SkillName("sb44eQ"), "Enchant Weapon", "skill sb44eQ");
AssertEq(names.SkillName("fw3d"), "Flame Ward", "skill fw3d");
AssertEq(names.SkillName("rn7iv"), "Runic Invocation", "skill rn7iv");
AssertEq(names.ClassName(1), "Mage", "class 1");
AssertEq(names.MasteryName(1, 3), "Runemaster", "mastery 1/3");
Console.WriteLine("  name lookup OK\n");

// ── 3. Slug extraction ──
Console.WriteLine("-- Slug extraction --");
AssertEq(LetFetcher.ExtractSlug("BakypDvx"), "BakypDvx", "bare slug");
AssertEq(LetFetcher.ExtractSlug("https://www.lastepochtools.com/planner/BakypDvx"), "BakypDvx", "full URL");
AssertEq(LetFetcher.ExtractSlug("https://www.lastepochtools.com/planner/BakypDvx?foo=bar"), "BakypDvx", "URL with query");
AssertEq(LetFetcher.ExtractSlug("https://www.lastepochtools.com/planner/BakypDvx#extra"), "BakypDvx", "URL with fragment");
Console.WriteLine("  slug OK\n");

// ── 4. Live fetch + convert ──
Console.WriteLine("-- Live fetch --");
Console.WriteLine("  fetching https://www.lastepochtools.com/planner/BakypDvx ...");
using var doc = await LetFetcher.FetchBuildAsync("BakypDvx");
Console.WriteLine("  fetched OK");

Console.WriteLine("-- Convert --");
var blobs = LetToMaxroll.Convert(doc);
Console.WriteLine($"  class: {blobs.CharacterClass}  mastery: {blobs.ChosenMastery}  level: {blobs.Level}");
Console.WriteLine($"  skills: {string.Join(", ", blobs.Skills.Keys)}");

// ── 5. Build summary ──
Console.WriteLine("\n-- Build summary --");
var summary = BuildSummary.FromLetBuild(doc, "BakypDvx", names, blobs);
Console.WriteLine($"  {summary.HeaderLine}");
Console.WriteLine($"  passives: {summary.PassivePointsSpent}   weaver: {summary.WeaverPointsSpent}");
Console.WriteLine("  Equipment:");
foreach (var e in summary.Equipment)
    Console.WriteLine($"    {e.Display}");
Console.WriteLine("  Skills:");
foreach (var s in summary.Skills)
    Console.WriteLine($"    slot {s.SlotNumber}: {s.Display}");
Console.WriteLine("  Blessings:");
foreach (var b in summary.Blessings)
    Console.WriteLine($"    {b}");

// ── 6. Verify against known Python output ──
Console.WriteLine("\n-- Compare to Python output --");
string pythonOutDir = @"C:\Users\andre\Downloads\LastEpoch-Mods\LEBuildConverter\output_BakypDvx";
if (Directory.Exists(pythonOutDir))
{
    string pythonAllEquip = File.ReadAllText(Path.Combine(pythonOutDir, "01_all_equipment.json"));
    string csAllEquip = blobs.AllEquipment.ToJsonString();

    using var pyDoc = JsonDocument.Parse(pythonAllEquip);
    using var csDoc = JsonDocument.Parse(csAllEquip);
    var pyItems = pyDoc.RootElement.GetProperty("items");
    var csItems = csDoc.RootElement.GetProperty("items");
    int matches = 0, diffs = 0;
    foreach (var p in pyItems.EnumerateObject())
    {
        if (!csItems.TryGetProperty(p.Name, out var csItem))
        {
            Console.WriteLine($"  MISSING in C#: {p.Name}");
            diffs++;
            continue;
        }
        int pyIt = p.Value.GetProperty("itemType").GetInt32();
        int pySub = p.Value.GetProperty("subType").GetInt32();
        int? pyUid = p.Value.TryGetProperty("uniqueID", out var u1) ? u1.GetInt32() : null;
        int csIt = csItem.GetProperty("itemType").GetInt32();
        int csSub = csItem.GetProperty("subType").GetInt32();
        int? csUid = csItem.TryGetProperty("uniqueID", out var u2) ? u2.GetInt32() : null;
        bool match = pyIt == csIt && pySub == csSub && pyUid == csUid;
        if (match) matches++; else diffs++;
        string ok = match ? "OK" : "DIFF";
        Console.WriteLine($"  [{ok}] {p.Name}: py({pyIt},{pySub},{pyUid}) vs cs({csIt},{csSub},{csUid})");
    }
    Console.WriteLine($"\n  matches={matches}  diffs={diffs}");
}
else
{
    Console.WriteLine("  (Python output dir not found — skipping comparison)");
}

Console.WriteLine("\n=== TESTS DONE ===");

static void AssertEq<T>(T actual, T expected, string label)
{
    if (!EqualityComparer<T>.Default.Equals(actual, expected))
    {
        Console.WriteLine($"  FAIL: {label}  expected={expected}  actual={actual}");
        Environment.Exit(1);
    }
    Console.WriteLine($"  OK: {label} = {actual}");
}
