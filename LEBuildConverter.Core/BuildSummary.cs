// ================================================================
//  BuildSummary.cs  -  UI-friendly representation of a build
//
//  Takes a LET build JSON payload + converted MaxrollBlobs and
//  produces a flat display object the WPF views can bind to
//  (class/mastery names, equipped items with human names, specialised
//  skills with names, passive counts, etc.).
// ================================================================

using System.Text.Json;

namespace LEBuildConverter.Core;

public sealed class ItemSummary
{
    public string SlotName { get; init; } = "";
    public string ItemName { get; init; } = "";
    public int ItemType { get; init; }
    public int SubType { get; init; }
    public int? UniqueId { get; init; }
    public bool IsUnique => UniqueId.HasValue && UniqueId.Value > 0;
    public List<string> Affixes { get; init; } = new();
    public bool IsCorrupted { get; init; }

    public string Display =>
        Affixes.Count > 0
            ? $"{SlotName}: {ItemName}  ({string.Join(", ", Affixes)})"
            : $"{SlotName}: {ItemName}";
}

public sealed class SkillSummary
{
    public string TreeId { get; init; } = "";
    public string SkillName { get; init; } = "";
    public int PointsSpent { get; init; }
    public int SlotNumber { get; init; }

    public string Display => $"{SkillName} ({PointsSpent} pts)";
}

public sealed class BuildSummary
{
    public string Slug { get; init; } = "";
    public string ClassName { get; init; } = "";
    public string MasteryName { get; init; } = "";
    public int CharacterClass { get; init; }
    public int ChosenMastery { get; init; }
    public int Level { get; init; }

    public List<ItemSummary> Equipment { get; init; } = new();
    public List<ItemSummary> Idols { get; init; } = new();
    public List<string> Blessings { get; init; } = new();

    public int PassivePointsSpent { get; init; }
    public int WeaverPointsSpent { get; init; }

    public List<SkillSummary> Skills { get; init; } = new();

    public string HeaderLine =>
        $"{ClassName} → {MasteryName}  (level {Level})";

    public static BuildSummary FromLetBuild(JsonDocument letDoc, string slug, NameLookup names, MaxrollBlobs blobs)
    {
        var root = letDoc.RootElement;
        var data = root.TryGetProperty("data", out var innerD) ? innerD : root;

        var summary = new BuildSummary
        {
            Slug = slug,
            CharacterClass = blobs.CharacterClass,
            ChosenMastery = blobs.ChosenMastery,
            Level = blobs.Level,
            ClassName = names.ClassName(blobs.CharacterClass),
            MasteryName = names.MasteryName(blobs.CharacterClass, blobs.ChosenMastery),
        };

        // ── Equipment ──
        if (data.TryGetProperty("equipment", out var equip)
            && equip.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in equip.EnumerateObject())
            {
                var item = BuildItemSummary(prop.Name, prop.Value, names);
                if (item is not null) summary.Equipment.Add(item);
            }
        }

        // ── Idols ──
        if (data.TryGetProperty("idols", out var idolsRaw)
            && idolsRaw.ValueKind == JsonValueKind.Array)
        {
            foreach (var letIdol in idolsRaw.EnumerateArray())
            {
                if (letIdol.ValueKind == JsonValueKind.Null) continue;
                var item = BuildItemSummary("idol", letIdol, names);
                if (item is not null) summary.Idols.Add(item);
            }
        }

        // ── Blessings ──
        if (data.TryGetProperty("blessings", out var blessingsRaw)
            && blessingsRaw.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in blessingsRaw.EnumerateObject())
            {
                if (!prop.Value.TryGetProperty("id", out var idEl)) continue;
                string letId = idEl.GetString() ?? "";
                try
                {
                    var decoded = LetIdDecoder.DecodeItemId(letId);
                    if (decoded.Tag == LetTag.Unique)
                    {
                        summary.Blessings.Add(names.BlessingName(decoded.SubTypeId));
                    }
                }
                catch { /* skip */ }
            }
        }

        // Build a new summary with all fields populated (can't use `with` on classes)
        return new BuildSummary
        {
            Slug = summary.Slug,
            CharacterClass = summary.CharacterClass,
            ChosenMastery = summary.ChosenMastery,
            Level = summary.Level,
            ClassName = summary.ClassName,
            MasteryName = summary.MasteryName,
            Equipment = summary.Equipment,
            Idols = summary.Idols,
            Blessings = summary.Blessings,
            PassivePointsSpent = CountSelectedPoints(data, "charTree"),
            WeaverPointsSpent = CountSelectedPoints(data, "weaverTree"),
            Skills = BuildSkillSummaries(data, names),
        };
    }

    private static int CountSelectedPoints(JsonElement data, string treeName)
    {
        if (!data.TryGetProperty(treeName, out var tree)) return 0;
        if (!tree.TryGetProperty("selected", out var sel)) return 0;
        if (sel.ValueKind != JsonValueKind.Object) return 0;
        int total = 0;
        foreach (var p in sel.EnumerateObject())
            total += p.Value.GetInt32();
        return total;
    }

    private static List<SkillSummary> BuildSkillSummaries(JsonElement data, NameLookup names)
    {
        var list = new List<SkillSummary>();
        if (!data.TryGetProperty("skillTrees", out var trees)
            || trees.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var tree in trees.EnumerateArray())
        {
            string treeId = tree.TryGetProperty("treeID", out var t) ? (t.GetString() ?? "") : "";
            int slotNum = tree.TryGetProperty("slotNumber", out var s) ? s.GetInt32() : 0;
            int points = 0;
            if (tree.TryGetProperty("selected", out var sel) && sel.ValueKind == JsonValueKind.Object)
                foreach (var p in sel.EnumerateObject())
                    points += p.Value.GetInt32();

            if (string.IsNullOrEmpty(treeId)) continue;

            list.Add(new SkillSummary
            {
                TreeId = treeId,
                SkillName = names.SkillName(treeId),
                PointsSpent = points,
                SlotNumber = slotNum,
            });
        }
        return list.OrderBy(s => s.SlotNumber).ToList();
    }

    private static ItemSummary? BuildItemSummary(string letSlot, JsonElement letEntry, NameLookup names)
    {
        if (!letEntry.TryGetProperty("id", out var idEl)) return null;
        string letId = idEl.GetString() ?? "";

        DecodedItem decoded;
        try { decoded = LetIdDecoder.DecodeItemId(letId); }
        catch { return null; }

        int itemType, subType;
        int? uniqueId = null;
        string itemName;

        if (decoded.Tag == LetTag.Rare)
        {
            // U-format = unique with uniqueID
            itemType = InferItemTypeFromSlot(letSlot, decoded.SubTypeId);
            subType = decoded.SubTypeId;
            uniqueId = decoded.UniqueId;
            itemName = names.UniqueName(decoded.UniqueId);
        }
        else if (decoded.Tag == LetTag.Unique)
        {
            // I-format — base rare or idol/blessing
            itemType = decoded.BaseTypeId;
            subType = decoded.SubTypeId;
            if (decoded.UniqueId > 0)
            {
                uniqueId = decoded.UniqueId;
                itemName = names.UniqueName(decoded.UniqueId);
            }
            else
            {
                itemName = names.ItemBaseName(itemType, subType);
            }
        }
        else
        {
            return null;
        }

        var affixList = new List<string>();
        if (letEntry.TryGetProperty("affixes", out var affixes)
            && affixes.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in affixes.EnumerateArray())
            {
                if (!a.TryGetProperty("id", out var aidEl)) continue;
                string aid = aidEl.GetString() ?? "";
                int tier = a.TryGetProperty("tier", out var tEl) ? tEl.GetInt32() : 1;
                try
                {
                    int affixId = LetIdDecoder.DecodeAffixId(aid);
                    affixList.Add($"T{tier} {names.AffixName(affixId)}");
                }
                catch { /* skip */ }
            }
        }

        bool corrupted = letEntry.TryGetProperty("corruptedAffix", out _);

        return new ItemSummary
        {
            SlotName = letSlot,
            ItemName = itemName,
            ItemType = itemType,
            SubType = subType,
            UniqueId = uniqueId,
            Affixes = affixList,
            IsCorrupted = corrupted,
        };
    }

    private static int InferItemTypeFromSlot(string letSlot, int subType) => letSlot switch
    {
        "head" => 0,
        "chest" => 1,
        "waist" => 2,
        "feet" => 3,
        "hands" => 4,
        "weapon1" => 9,
        "weapon2" => 19,
        "ring1" => 21,
        "ring2" => 21,
        "amulet" => 20,
        "relic" => 22,
        "idol_altar" => 41,
        "idol" => 26,
        _ => 0,
    };
}
