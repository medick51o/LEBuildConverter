// ================================================================
//  LetToMaxroll.cs  -  lastepochtools -> maxroll build converter
//
//  Given a LET build JSON payload, produce maxroll-compatible import
//  blobs for each of maxroll's Export/Import buttons:
//    - AllEquipment  (items + idols + blessings + weaverItems)
//    - Passives
//    - WeaverTree
//    - Skills (dict keyed by tree ID)
//
//  Ported from let_to_maxroll.py.
// ================================================================

using System.Text.Json;
using System.Text.Json.Nodes;

namespace LEBuildConverter.Core;

public sealed class MaxrollBlobs
{
    public JsonObject AllEquipment { get; init; } = new();
    public JsonObject Passives { get; init; } = new();
    public JsonObject WeaverTree { get; init; } = new();
    /// <summary>Key = skill tree ID (e.g. "sb44eQ"), Value = the JSON to paste.</summary>
    public Dictionary<string, JsonObject> Skills { get; init; } = new();

    public int CharacterClass { get; init; }
    public int ChosenMastery { get; init; }
    public int Level { get; init; }
}

public static class LetToMaxroll
{
    // LET slot -> (maxroll slot, default itemType)
    private static readonly Dictionary<string, (string slot, int itemType)> SlotMap = new()
    {
        ["head"]       = ("head",    0),
        ["chest"]      = ("body",    1),
        ["waist"]      = ("waist",   2),
        ["feet"]       = ("feet",    3),
        ["hands"]      = ("hands",   4),
        ["weapon1"]    = ("weapon",  9),
        ["weapon2"]    = ("offhand", 19),
        ["ring1"]      = ("finger1", 21),
        ["ring2"]      = ("finger2", 21),
        ["amulet"]     = ("neck",    20),
        ["relic"]      = ("relic",   22),
        ["idol_altar"] = ("altar",   41),
    };

    public static MaxrollBlobs Convert(JsonDocument letDoc)
    {
        JsonElement root = letDoc.RootElement;

        // LET responses are wrapped: { "data": { ... } }
        JsonElement data = root;
        if (root.TryGetProperty("data", out JsonElement inner))
            data = inner;

        // ── Character bio ──
        int charClass = 0, mastery = 0, level = 0;
        if (data.TryGetProperty("bio", out JsonElement bio))
        {
            if (bio.TryGetProperty("characterClass", out JsonElement cc))
                charClass = cc.GetInt32();
            if (bio.TryGetProperty("chosenMastery", out JsonElement cm))
                mastery = cm.GetInt32();
            if (bio.TryGetProperty("level", out JsonElement lv))
                level = lv.GetInt32();
        }

        // ── Items ──
        var items = new JsonObject();
        if (data.TryGetProperty("equipment", out JsonElement equip))
        {
            foreach (var prop in equip.EnumerateObject())
            {
                var result = BuildItem(prop.Name, prop.Value);
                if (result is not null)
                {
                    var (slot, item) = result.Value;
                    items[slot] = item;
                }
            }
        }

        // ── Idols (LET sparse array) ──
        var idolsOut = new JsonArray();
        // Initialise 20 null slots
        for (int i = 0; i < 20; i++) idolsOut.Add(null);

        if (data.TryGetProperty("idols", out JsonElement idolsRaw)
            && idolsRaw.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement letIdol in idolsRaw.EnumerateArray())
            {
                if (letIdol.ValueKind == JsonValueKind.Null) continue;
                var built = BuildIdol(letIdol);
                if (built is null) continue;

                int x = letIdol.TryGetProperty("x", out JsonElement xe) ? xe.GetInt32() : 0;
                int y = letIdol.TryGetProperty("y", out JsonElement ye) ? ye.GetInt32() : 0;
                int idx = y * 5 + x;  // 5-wide grid (placeholder — may need tuning)
                if (idx >= 0 && idx < 20)
                    idolsOut[idx] = built;
            }
        }

        // ── Blessings (LET keyed dict 1..10) ──
        var blessingsOut = new JsonArray();
        for (int i = 0; i < 10; i++) blessingsOut.Add(null);

        if (data.TryGetProperty("blessings", out JsonElement blessingsRaw)
            && blessingsRaw.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in blessingsRaw.EnumerateObject())
            {
                if (!int.TryParse(prop.Name, out int slotNum)) continue;
                int slotIdx = slotNum - 1;
                if (slotIdx < 0 || slotIdx >= 10) continue;
                var b = BuildBlessing(prop.Value);
                if (b is not null) blessingsOut[slotIdx] = b;
            }
        }

        // All equipment blob
        var allEquipment = new JsonObject
        {
            ["items"] = items,
            ["idols"] = idolsOut,
            ["blessings"] = blessingsOut,
            ["weaverItems"] = new JsonArray(),
        };

        // ── Passives ──
        var passives = BuildPassives(data, charClass, mastery);

        // ── Weaver tree ──
        var weaver = BuildWeaver(data);

        // ── Skill trees ──
        var skills = BuildSkillTrees(data);

        return new MaxrollBlobs
        {
            AllEquipment = allEquipment,
            Passives = passives,
            WeaverTree = weaver,
            Skills = skills,
            CharacterClass = charClass,
            ChosenMastery = mastery,
            Level = level,
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static (string slot, JsonObject item)? BuildItem(string letSlot, JsonElement letEntry)
    {
        if (letEntry.ValueKind != JsonValueKind.Object) return null;
        if (!letEntry.TryGetProperty("id", out JsonElement idEl)) return null;
        if (!SlotMap.TryGetValue(letSlot, out var slotInfo)) return null;

        string letId = idEl.GetString() ?? "";
        DecodedItem decoded;
        try
        {
            decoded = LetIdDecoder.DecodeItemId(letId);
        }
        catch
        {
            return null;
        }

        var item = new JsonObject();

        switch (decoded.Tag)
        {
            case LetTag.Rare:
                // U-format = unique item with uniqueID populated
                item["itemType"] = slotInfo.itemType;
                item["subType"] = decoded.SubTypeId;
                item["uniqueID"] = decoded.UniqueId;
                break;

            case LetTag.Unique:
                // I-format — usually rare/crafted (rarity=0, uniqueId=0) but
                // may include a uniqueID for specific legendary/set encodings.
                item["itemType"] = decoded.BaseTypeId;
                item["subType"] = decoded.SubTypeId;
                if (decoded.UniqueId > 0)
                    item["uniqueID"] = decoded.UniqueId;
                break;

            default:
                return null;
        }

        // ── Affixes ──
        var affixes = new JsonArray();
        if (letEntry.TryGetProperty("affixes", out JsonElement letAffixes)
            && letAffixes.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement a in letAffixes.EnumerateArray())
                AppendAffix(affixes, a);
        }
        item["affixes"] = affixes;

        // ── Sealed / corrupted affixes ──
        if (letEntry.TryGetProperty("sealedAffix", out JsonElement sealedEl)
            && sealedEl.ValueKind == JsonValueKind.Object)
        {
            item["sealedAffix"] = BuildAffixEntry(sealedEl);
        }
        if (letEntry.TryGetProperty("corruptedAffix", out JsonElement corruptEl)
            && corruptEl.ValueKind == JsonValueKind.Object)
        {
            var list = new JsonArray { BuildAffixEntry(corruptEl) };
            item["corruptedAffixes"] = list;
            item["corrupted"] = true;
        }

        return (slotInfo.slot, item);
    }

    private static JsonObject? BuildIdol(JsonElement letIdol)
    {
        if (!letIdol.TryGetProperty("id", out JsonElement idEl)) return null;
        string letId = idEl.GetString() ?? "";
        DecodedItem decoded;
        try { decoded = LetIdDecoder.DecodeItemId(letId); }
        catch { return null; }

        var idol = new JsonObject();

        if (decoded.Tag == LetTag.Unique)
        {
            // I-format: baseTypeId is the idol itemType (26/29/30/33)
            idol["itemType"] = decoded.BaseTypeId;
            idol["subType"] = decoded.SubTypeId;
            if (decoded.UniqueId > 0)
            {
                idol["uniqueID"] = decoded.UniqueId;
                idol["uniqueRolls"] = new JsonArray();
            }
        }
        else if (decoded.Tag == LetTag.Rare)
        {
            // U-format idol — typically weaver/unique idols, itemType 33
            idol["itemType"] = 33;
            idol["subType"] = decoded.SubTypeId;
            idol["uniqueID"] = decoded.UniqueId;
            idol["uniqueRolls"] = new JsonArray();
        }
        else
        {
            return null;
        }

        var affixes = new JsonArray();
        if (letIdol.TryGetProperty("affixes", out JsonElement letAffixes)
            && letAffixes.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement a in letAffixes.EnumerateArray())
                AppendAffix(affixes, a);
        }
        idol["affixes"] = affixes;

        if (letIdol.TryGetProperty("corruptedAffix", out JsonElement corruptEl)
            && corruptEl.ValueKind == JsonValueKind.Object)
        {
            idol["corruptedAffixes"] = new JsonArray { BuildAffixEntry(corruptEl) };
            idol["corrupted"] = true;
        }

        return idol;
    }

    private static JsonObject? BuildBlessing(JsonElement letBlessing)
    {
        if (letBlessing.ValueKind != JsonValueKind.Object) return null;
        if (!letBlessing.TryGetProperty("id", out JsonElement idEl)) return null;

        string letId = idEl.GetString() ?? "";
        DecodedItem decoded;
        try { decoded = LetIdDecoder.DecodeItemId(letId); }
        catch { return null; }

        if (decoded.Tag != LetTag.Unique) return null;

        return new JsonObject
        {
            ["itemType"] = decoded.BaseTypeId,  // 34
            ["subType"] = decoded.SubTypeId,
            ["implicits"] = new JsonArray { 1 },
        };
    }

    private static JsonObject BuildPassives(JsonElement data, int charClass, int mastery)
    {
        var history = new JsonArray();
        int total = 0;
        if (data.TryGetProperty("charTree", out JsonElement ct)
            && ct.TryGetProperty("selected", out JsonElement sel)
            && sel.ValueKind == JsonValueKind.Object)
        {
            // Sort by numeric node id to produce deterministic output
            var ordered = sel.EnumerateObject()
                .Select(p => (Id: int.Parse(p.Name), Pts: p.Value.GetInt32()))
                .OrderBy(t => t.Id);
            foreach (var (id, pts) in ordered)
            {
                for (int i = 0; i < pts; i++) history.Add(id);
                total += pts;
            }
        }

        return new JsonObject
        {
            ["passives"] = new JsonObject
            {
                ["history"] = history,
                ["position"] = total,
            },
            ["class"] = charClass,
            ["mastery"] = mastery,
        };
    }

    private static JsonObject BuildWeaver(JsonElement data)
    {
        var history = new JsonArray();
        int total = 0;
        if (data.TryGetProperty("weaverTree", out JsonElement wt)
            && wt.TryGetProperty("selected", out JsonElement sel)
            && sel.ValueKind == JsonValueKind.Object)
        {
            var ordered = sel.EnumerateObject()
                .Select(p => (Id: int.Parse(p.Name), Pts: p.Value.GetInt32()))
                .OrderBy(t => t.Id);
            foreach (var (id, pts) in ordered)
            {
                for (int i = 0; i < pts; i++) history.Add(id);
                total += pts;
            }
        }

        return new JsonObject
        {
            ["weaverItems"] = new JsonArray(),
            ["weaver"] = new JsonObject
            {
                ["history"] = history,
                ["position"] = total,
            },
        };
    }

    private static Dictionary<string, JsonObject> BuildSkillTrees(JsonElement data)
    {
        var result = new Dictionary<string, JsonObject>();
        if (!data.TryGetProperty("skillTrees", out JsonElement trees)
            || trees.ValueKind != JsonValueKind.Array)
            return result;

        foreach (JsonElement tree in trees.EnumerateArray())
        {
            if (!tree.TryGetProperty("treeID", out JsonElement tidEl)) continue;
            string treeId = tidEl.GetString() ?? "";
            if (string.IsNullOrEmpty(treeId)) continue;

            var history = new JsonArray();
            int total = 0;
            if (tree.TryGetProperty("selected", out JsonElement sel)
                && sel.ValueKind == JsonValueKind.Object)
            {
                var ordered = sel.EnumerateObject()
                    .Select(p => (Id: int.Parse(p.Name), Pts: p.Value.GetInt32()))
                    .OrderBy(t => t.Id);
                foreach (var (id, pts) in ordered)
                {
                    for (int i = 0; i < pts; i++) history.Add(id);
                    total += pts;
                }
            }

            var treeObj = new JsonObject
            {
                ["history"] = history,
                ["position"] = total,
            };

            result[treeId] = new JsonObject
            {
                ["skillTrees"] = new JsonObject { [treeId] = treeObj },
            };
        }

        return result;
    }

    private static void AppendAffix(JsonArray target, JsonElement letAffix)
    {
        target.Add(BuildAffixEntry(letAffix));
    }

    private static JsonObject BuildAffixEntry(JsonElement letAffix)
    {
        string letId = letAffix.TryGetProperty("id", out JsonElement idEl)
            ? (idEl.GetString() ?? "") : "";
        int tier = letAffix.TryGetProperty("tier", out JsonElement tierEl)
            ? tierEl.GetInt32() : 1;
        int affixId = 0;
        try { affixId = LetIdDecoder.DecodeAffixId(letId); }
        catch { /* leave 0 */ }

        return new JsonObject
        {
            ["id"] = affixId,
            ["tier"] = tier,
            ["roll"] = 1,
        };
    }
}
