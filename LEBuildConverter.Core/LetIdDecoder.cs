// ================================================================
//  LetIdDecoder.cs  -  lastepochtools.com ID decoder (C# port)
//
//  LET encodes item / affix / set IDs with lz-string's
//  compressToEncodedURIComponent applied to a zero-padded, fixed-width
//  digit string, with a single-letter tag prepended.
//
//  Format (verified from planner.js on 2026-04-14):
//    "I" + lzEnc("1" + pad(baseTypeId,3) + pad(subTypeId,3) + pad(rarity,1) + pad(uniqueId,2))
//         -> body has a leading '1' literal, then 9 digits = 10 total
//    "U" + lzEnc(pad(subTypeId,3) + pad(uniqueId,3))
//         -> 6 digits.  For uniques the 2nd field IS the unique item ID.
//    "A" + lzEnc(pad(affixId,3))  -> affix
//    "S" + lzEnc(pad(setId,3))    -> set
// ================================================================

using LZStringCSharp;

namespace LEBuildConverter.Core;

public enum LetTag
{
    Unknown,
    Unique,   // "I" prefix — baseTypeId/subTypeId/rarity/uniqueId fields
    Rare,     // "U" prefix — subTypeId/uniqueId fields
    Set,      // "S" prefix
    Affix,    // "A" prefix
}

public sealed class DecodedItem
{
    public LetTag Tag { get; init; }
    public int BaseTypeId { get; init; }
    public int SubTypeId { get; init; }
    public int Rarity { get; init; }
    public int UniqueId { get; init; }
    public string RawDigits { get; init; } = "";
}

public static class LetIdDecoder
{
    /// <summary>
    /// Decode a lastepochtools item ID into game-internal integer fields.
    /// </summary>
    public static DecodedItem DecodeItemId(string letId)
    {
        if (string.IsNullOrEmpty(letId) || letId.Length < 2)
            throw new ArgumentException($"let_id too short: {letId}", nameof(letId));

        char tag = letId[0];
        string body = letId[1..];
        string digits = LzDecode(body);

        switch (tag)
        {
            case 'I':
                {
                    if (!digits.StartsWith("1"))
                        throw new InvalidOperationException($"Unique ID missing '1' prefix: {digits}");
                    string d = digits[1..];
                    if (d.Length < 9)
                        d = d.PadRight(9, '0');
                    return new DecodedItem
                    {
                        Tag = LetTag.Unique,
                        BaseTypeId = int.Parse(d[0..3]),
                        SubTypeId = int.Parse(d[3..6]),
                        Rarity = int.Parse(d[6..7]),
                        UniqueId = int.Parse(d[7..9]),
                        RawDigits = digits,
                    };
                }

            case 'U':
                {
                    if (digits.Length < 6)
                        digits = digits.PadLeft(6, '0');
                    return new DecodedItem
                    {
                        Tag = LetTag.Rare,
                        SubTypeId = int.Parse(digits[0..3]),
                        UniqueId = int.Parse(digits[3..6]),
                        RawDigits = digits,
                    };
                }

            case 'S':
                return new DecodedItem
                {
                    Tag = LetTag.Set,
                    UniqueId = int.Parse(digits),
                    RawDigits = digits,
                };

            default:
                return new DecodedItem { Tag = LetTag.Unknown, RawDigits = digits };
        }
    }

    /// <summary>
    /// Decode an LET affix ID to the game-internal integer affixId.
    /// </summary>
    public static int DecodeAffixId(string letId)
    {
        if (string.IsNullOrEmpty(letId) || letId[0] != 'A')
            throw new ArgumentException($"Not an affix id: {letId}", nameof(letId));
        return int.Parse(LzDecode(letId[1..]));
    }

    private static string LzDecode(string body)
    {
        string? s = LZString.DecompressFromEncodedURIComponent(body);
        if (s is null)
            throw new InvalidOperationException($"lz-string decode failed for {body}");
        return s;
    }
}
