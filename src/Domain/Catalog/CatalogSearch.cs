using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LinerNotes.Domain.Catalog;

public enum CatalogKind { Artist, Album }
public enum CatalogOrigin { Synthetic, Public }
public enum CatalogPublicationState { Staging, Published, Failed }

public static class CatalogText
{
    public const string Version = "unicode-compatibility-fold-v1";
    public static string Normalize(string value)
    {
        if (value.Length > 256 || value.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) == UnicodeCategory.Format))
            throw new ArgumentException("catalog_text_invalid");
        var result = new StringBuilder(value.Length);
        bool space = false;
        foreach (var rune in value.Normalize(NormalizationForm.FormKD).EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark) continue;
            if (Rune.IsLetterOrDigit(rune))
            {
                if (space && result.Length > 0) result.Append(' ');
                space = false;
                var folded = Rune.ToLowerInvariant(rune);
                result.Append(folded.Value switch { 0x131 => "i", 0x259 => "e", 0xdf => "ss", _ => folded.ToString() });
            }
            else space = true;
        }
        var normalized = result.Length > 0 ? result.ToString() : value.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        return normalized.Length <= 256 ? normalized : throw new ArgumentException("catalog_normalized_text_too_long");
    }

    public static Guid RecordId(Guid publicationId, CatalogKind kind, Guid canonicalId) =>
        new(SHA256.HashData(Encoding.ASCII.GetBytes($"{publicationId:D}:{kind}:{canonicalId:D}"))[..16]);

    public static IReadOnlyList<string> Terms(string name, IReadOnlyList<string> aliases)
    {
        if (aliases.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("catalog_alias_invalid");
        if (aliases.Count > 12) throw new ArgumentException("catalog_alias_limit");
        var terms = new SortedSet<string>(StringComparer.Ordinal);
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in aliases.Prepend(name))
        {
            var term = Normalize(label);
            if (term.Length == 0) throw new ArgumentException("catalog_empty_name");
            terms.Add(term);
            // Retain literal punctuation prefixes for names such as !!! and †††.
            var literal = string.Join(' ', label.Normalize(NormalizationForm.FormKC).ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (literal.Length <= 256) terms.Add(literal);
            foreach (var token in term.Split(' ', StringSplitOptions.RemoveEmptyEntries)) tokens.Add(token);
        }
        foreach (var token in tokens.OrderByDescending(t => t.Length).ThenBy(t => t, StringComparer.Ordinal))
        {
            if (terms.Count >= 64) break;
            terms.Add(token);
        }
        return terms.ToArray();
    }
}
