using System.Security.Cryptography;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Domain.Catalog;

namespace LinerNotes.Infrastructure.Catalog;

public sealed class CatalogArtifactReader : ICatalogArtifactReader
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        MaxDepth = 12
    };

    public async IAsyncEnumerable<CatalogArtifactRow> ReadAsync(Stream stream, CatalogPublication publication,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var hash = SHA256.Create();
        using var verified = new CryptoStream(stream, hash, CryptoStreamMode.Read, leaveOpen: true);
        using var input = new StreamReader(verified, new UTF8Encoding(false, true), false, 4096, leaveOpen: true);
        var buffer = new char[4096];
        var line = new StringBuilder();
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            int start = 0;
            for (int i = 0; i < read; i++)
            {
                if (buffer[i] != '\n') continue;
                Append(buffer, start, i - start);
                yield return Parse(line.ToString(), publication);
                line.Clear(); start = i + 1;
            }
            Append(buffer, start, read - start);
        }
        if (line.Length > 0) yield return Parse(line.ToString(), publication);
        if (hash.Hash is not { } digest || Convert.ToHexStringLower(digest) != publication.ContentSha256)
            throw new ArgumentException("catalog_consumed_artifact_hash_mismatch");

        void Append(char[] chars, int start, int length)
        {
            if (line.Length + length > 32_768) throw new ArgumentException("catalog_line_too_large");
            line.Append(chars, start, length);
        }
    }

    private static CatalogArtifactRow Parse(string line, CatalogPublication publication)
    {
        if (Encoding.UTF8.GetByteCount(line) > 32_768) throw new ArgumentException("catalog_line_too_large");
        var row = JsonSerializer.Deserialize<CatalogArtifactRow>(line, Json) ?? throw new ArgumentException("catalog_row_invalid");
        if (row.Id == Guid.Empty || !Enum.IsDefined(row.Kind) || string.IsNullOrWhiteSpace(row.Name) || row.Name.Length > 256 ||
            row.Aliases is null || row.ArtistIds is null || row.Tags is null || row.Tags.Length > 20 || row.Disambiguation?.Length > 500 ||
            (row.Kind == CatalogKind.Artist && row.ArtistIds.Length != 0) ||
            (row.Kind is CatalogKind.Album or CatalogKind.Track && (row.ArtistIds.Length is < 1 or > 32 || row.ArtistIds.Any(id => id == Guid.Empty))) ||
            row.ArtistIds.Distinct().Count() != row.ArtistIds.Length)
            throw new ArgumentException("catalog_row_invalid");
        if (row.Releases is { } releases && (row.Kind != CatalogKind.Track || releases.Length > 10 ||
            releases.Any(r => r.CanonicalId == Guid.Empty || string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 256 || r.Title.Any(char.IsControl) || r.Date is not null && !ValidDate(r.Date))))
            throw new ArgumentException("catalog_recording_release_invalid");
        _ = CatalogText.Terms(row.Name, row.Aliases);
        if (row.Disambiguation is not null && row.Disambiguation.Any(char.IsControl)) throw new ArgumentException("catalog_disambiguation_invalid");
        if (row.FirstReleaseDate is { } date && !ValidDate(date)) throw new ArgumentException("catalog_date_invalid");
        var tags = row.Tags.Select(tag =>
        {
            if (tag is null || string.IsNullOrWhiteSpace(tag.Name) || tag.Name.Length > 100 || tag.Name.Any(char.IsControl) || !double.IsFinite(tag.Weight) || tag.Weight is <= 0 or > 1)
                throw new ArgumentException("catalog_tag_invalid");
            string scope = row.Kind switch { CatalogKind.Artist => "artist", CatalogKind.Album => "release-group", _ => "recording" };
            if (publication.Origin == CatalogOrigin.Public && (publication.TagLicense is null || tag.Scope != scope))
                throw new ArgumentException("catalog_tag_provenance_required");
            return tag with
            {
                Name = tag.Name.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant(),
                Scope = publication.Origin == CatalogOrigin.Synthetic ? "synthetic-" + scope : scope
            };
        }).ToArray();
        if (tags.Select(t => t.Name).Distinct(StringComparer.Ordinal).Count() != tags.Length) throw new ArgumentException("catalog_duplicate_tag");
        return row with { Name = row.Name.Trim(), Tags = tags };
    }

    private static bool ValidDate(string value) => value.Length switch
    {
        4 => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year is >= 1 and <= 9999,
        7 => DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        10 => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        _ => false
    };
}
