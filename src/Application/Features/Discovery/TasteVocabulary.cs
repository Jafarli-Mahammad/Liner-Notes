using LinerNotes.Domain.Catalog;

namespace LinerNotes.Application.Features.Discovery;

public sealed record TasteTag(string Id, string Label, string Category, IReadOnlyList<string> Aliases);

/// <summary>Original local vocabulary; labels describe taste, not verified provider coverage.</summary>
public static class TasteVocabulary
{
    public const string Version = "taste-vocabulary-v1";
    public static readonly IReadOnlyList<TasteTag> Entries = Build();
    private static readonly IReadOnlyDictionary<string, TasteTag> Index = Entries
        .SelectMany(t => t.Aliases.Append(t.Id).Append(t.Label).Select(a => (Key: CatalogText.Normalize(a), Tag: t)))
        .GroupBy(t => t.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Tag, StringComparer.Ordinal);

    public static string Canonicalize(string value) => Index.TryGetValue(CatalogText.Normalize(value), out var tag)
        ? tag.Id : throw new ArgumentException("unknown_taste_tag");

    public static object Search(string? query, string? category, int offset, int limit)
    {
        if (offset < 0 || limit is < 1 or > 50 || query?.Length > 128 ||
            category is not (null or "" or "genre" or "mood" or "sound")) throw new ArgumentException("invalid_tag_search");
        string term = CatalogText.Normalize(query ?? "");
        var matches = Entries.Where(t => (string.IsNullOrEmpty(category) || t.Category == category) &&
            (term.Length == 0 || t.Aliases.Append(t.Label).Append(t.Id).Any(a => CatalogText.Normalize(a).Contains(term, StringComparison.Ordinal))))
            .Skip(offset).Take(limit + 1).ToArray();
        return new { version = Version, items = matches.Take(limit), hasMore = matches.Length > limit };
    }

    private static IReadOnlyList<TasteTag> Build()
    {
        var genres = "indie|electronic|jazz|rock|pop|hip hop|classical|folk|ambient|experimental|metal|punk|soul|r&b|funk|disco|house|techno|drum and bass|trip hop|shoegaze|dream pop|post punk|post rock|alternative|synthpop|psychedelic|blues|country|reggae|dub|afrobeat|latin|soundtrack|downtempo|acoustic|noise|world|acid house|acid jazz|afro house|afro pop|alternative country|alternative hip hop|alternative metal|alternative rock|americana|art pop|art rock|avant garde jazz|baroque|bebop|black metal|bluegrass|bolero|bossa nova|breakbeat|britpop|chamber music|chillout|chiptune|city pop|contemporary classical|cool jazz|dance pop|dancehall|dark ambient|darkwave|death metal|deep house|desert blues|disco house|doom metal|drill|dub techno|dubstep|electro|electro pop|emo|flamenco|folk metal|folk rock|free jazz|future bass|garage rock|glam rock|glitch|gospel|gothic rock|grime|grindcore|grunge|hard bop|hard rock|hardcore punk|heavy metal|highlife|idm|industrial|indie folk|indie pop|indie rock|jangle pop|jazz fusion|jazz rap|juke|jungle|k pop|krautrock|latin jazz|lo fi hip hop|math rock|melodic death metal|minimal techno|motown|neo soul|new age|new wave|noise pop|noise rock|nu jazz|opera|orchestral|phonk|post hardcore|post metal|power metal|power pop|progressive house|progressive metal|progressive rock|psychedelic folk|psychedelic rock|raga|ragtime|reggaeton|renaissance|rockabilly|romantic classical|salsa|samba|screamo|ska|slowcore|sludge metal|smooth jazz|southern rock|space rock|speed metal|stoner rock|surf rock|swing|synthwave|tango|technical death metal|thrash metal|traditional folk|trance|trap|uk garage|vaporwave|video game music|vocal jazz|witch house|zouk|anatolian rock|arabic pop|azerbaijani folk|balkan brass|celtic folk|fado|gamelan|hindustani classical|iranian classical|j pop|qawwali|sufi music|turkish folk|tuvan throat singing|west african folk";
        var moods = "calm|energetic|melancholic|joyful|dreamy|dark|hopeful|tense|playful|romantic|reflective|uplifting|angry|wistful|peaceful|euphoric|mysterious|solemn|warm|cold|intimate|dramatic|meditative|restless|nostalgic|bittersweet|gentle|triumphant|lonely|bright|somber|relaxed|intense|tender|haunting|optimistic|brooding|carefree|serene|defiant";
        var sounds = "instrumental|vocals|acoustic guitar|electric guitar|piano|strings|brass|woodwinds|synthesizers|drum machine|live drums|distorted|clean guitar|fuzzy|reverb|lo fi|hi fi|minimal|layered|polyrhythmic|syncopated|slow tempo|fast tempo|groovy|percussive|droning|spacious|dense|raw|polished|improvised|composed|solo|ensemble|choir|spoken word|field recordings|tape textures|analog|digital|microtonal|atonal|modal|harmonic|dissonant|fingerpicked|arpeggiated|heavy bass|soft vocals|harsh vocals|wordless vocals";
        return Group(genres, "genre").Concat(Group(moods, "mood")).Concat(Group(sounds, "sound")).ToArray();
    }

    private static IEnumerable<TasteTag> Group(string values, string category) => values.Split('|').Select(id =>
        new TasteTag(id, id, category, id switch
        {
            "hip hop" => ["hip-hop", "hiphop"],
            "r&b" => ["rhythm and blues", "rnb"],
            "drum and bass" => ["dnb", "drum & bass"],
            "synthpop" => ["synth pop"],
            "post punk" => ["post-punk"],
            "vocals" => ["vocal", "singing"],
            "instrumental" => ["instrumentals"],
            "lo fi" => ["lo-fi", "low fidelity"],
            _ => []
        }));
}
