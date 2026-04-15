// ================================================================
//  NameLookup.cs  -  human-readable name resolver for LE IDs
//
//  Ports le_names.py + rosetta.py fallback logic.  Loads two
//  embedded JSON resources (LET and maxroll name databases) and
//  exposes simple name() functions that always return a string
//  (falling back to "#id" for unknown IDs).
//
//  Cross-reference strategy: prefer LET names first (more consistent
//  wording), fall back to maxroll, then placeholder.
// ================================================================

using System.Reflection;
using System.Text.Json;

namespace LEBuildConverter.Core;

public sealed class NameLookup
{
    private static readonly Lazy<NameLookup> _instance = new(() => new NameLookup());
    public static NameLookup Instance => _instance.Value;

    private readonly JsonDocument _letDoc;
    private readonly JsonDocument _maxrollDoc;
    private readonly JsonElement _let;
    private readonly JsonElement _max;

    private NameLookup()
    {
        _letDoc = LoadEmbedded("Data.le_names_data.json");
        _maxrollDoc = LoadEmbedded("Data.maxroll_names_data.json");
        _let = _letDoc.RootElement;
        _max = _maxrollDoc.RootElement;
    }

    private static JsonDocument LoadEmbedded(string name)
    {
        var asm = typeof(NameLookup).Assembly;
        // The embedded resource is named: LEBuildConverter.Core.Data.<filename>
        string resourceName = $"LEBuildConverter.Core.{name}";
        using Stream? stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            // Fallback: search for a name that ends with our filename
            string[] all = asm.GetManifestResourceNames();
            string? match = all.FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                throw new InvalidOperationException(
                    $"Embedded resource '{resourceName}' not found. Available: {string.Join(", ", all)}");
            using Stream? s2 = asm.GetManifestResourceStream(match);
            return JsonDocument.Parse(s2!);
        }
        return JsonDocument.Parse(stream);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static string? TryGetString(JsonElement root, string section, string key)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty(section, out JsonElement sec)) return null;
        if (sec.ValueKind != JsonValueKind.Object) return null;
        if (!sec.TryGetProperty(key, out JsonElement val)) return null;
        return val.ValueKind == JsonValueKind.String ? val.GetString() : null;
    }

    private string PreferLetThenMax(string section, string key, string fallback)
    {
        return TryGetString(_let, section, key)
            ?? TryGetString(_max, section, key)
            ?? fallback;
    }

    // ── Public lookups ──────────────────────────────────────────────────

    public string ItemTypeName(int itemType) =>
        PreferLetThenMax("item_types", itemType.ToString(), $"#type{itemType}");

    public string ItemBaseName(int itemType, int subType) =>
        PreferLetThenMax("items", $"{itemType}/{subType}", $"#{itemType}/{subType}");

    public string UniqueName(int uniqueId) =>
        PreferLetThenMax("uniques", uniqueId.ToString(), $"#u{uniqueId}");

    /// <summary>
    /// Returns the unique name if uniqueId > 0, else the base item name.
    /// </summary>
    public string ItemName(int itemType, int subType, int? uniqueId)
    {
        if (uniqueId.HasValue && uniqueId.Value > 0)
            return UniqueName(uniqueId.Value);
        return ItemBaseName(itemType, subType);
    }

    public string AffixName(int affixId)
    {
        // Check LET first, then maxroll.affixes, then maxroll.affixes_internal (stale display-name fallback)
        return TryGetString(_let, "affixes", affixId.ToString())
            ?? TryGetString(_max, "affixes", affixId.ToString())
            ?? TryGetString(_max, "affixes_internal", affixId.ToString())
            ?? $"#a{affixId}";
    }

    public string BlessingName(int subType) =>
        // Blessings are itemType 34 in LET data
        PreferLetThenMax("items", $"34/{subType}", $"#b{subType}");

    public string SkillName(string treeId)
    {
        if (string.IsNullOrEmpty(treeId)) return "";
        return TryGetString(_let, "abilities", treeId)
            ?? TryGetString(_max, "abilities", treeId)
            ?? treeId;
    }

    public string SkillTreeNodeName(string treeId, int nodeId) =>
        PreferLetThenMax("skill_tree_nodes", $"{treeId}/{nodeId}", $"#sn{treeId}/{nodeId}");

    public string ClassName(int classId)
    {
        // masteries[classId][0] is the base class name
        return MasteryName(classId, 0);
    }

    public string MasteryName(int classId, int masteryId)
    {
        if (_let.TryGetProperty("masteries", out JsonElement masteries)
            && masteries.TryGetProperty(classId.ToString(), out JsonElement clazz)
            && clazz.TryGetProperty(masteryId.ToString(), out JsonElement name)
            && name.ValueKind == JsonValueKind.String)
        {
            return name.GetString() ?? $"#m{classId}/{masteryId}";
        }
        if (_max.TryGetProperty("masteries", out JsonElement mm)
            && mm.TryGetProperty(classId.ToString(), out JsonElement mc)
            && mc.TryGetProperty(masteryId.ToString(), out JsonElement mn)
            && mn.ValueKind == JsonValueKind.String)
        {
            return mn.GetString() ?? $"#m{classId}/{masteryId}";
        }
        return $"#m{classId}/{masteryId}";
    }

    public string PassiveNodeName(int classId, int nodeId)
    {
        if (_let.TryGetProperty("passive_nodes", out JsonElement pn)
            && pn.TryGetProperty("by_class_id", out JsonElement byClass)
            && byClass.TryGetProperty(classId.ToString(), out JsonElement clazz)
            && clazz.TryGetProperty("nodes", out JsonElement nodes)
            && nodes.TryGetProperty(nodeId.ToString(), out JsonElement n)
            && n.ValueKind == JsonValueKind.String)
        {
            return n.GetString() ?? $"#pn{classId}/{nodeId}";
        }
        return $"#pn{classId}/{nodeId}";
    }
}
