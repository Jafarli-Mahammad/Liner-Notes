using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm.Fixtures;

/// <summary>
/// Deterministic offline fixtures for Last.fm API responses.
/// Provides explicitly synthetic offline test data,
/// specifically covering founder seeds (Jakuzi, Son Feci Bisiklet, Hotline Miami, Dying Light, ULTRAKILL, Hades).
/// </summary>
public static class LastFmFixtureProvider
{
    private static readonly Dictionary<string, IReadOnlyList<LastFmArtistSummary>> SimilarArtists = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jakuzi"] = new List<LastFmArtistSummary>
        {
            new("Son Feci Bisiklet", "mbid:artist-sfb-01", "0.85", "https://www.last.fm/music/Son+Feci+Bisiklet"),
            new("She Past Away", "mbid:artist-spa-01", "0.82", "https://www.last.fm/music/She+Past+Away"),
            new("Adamlar", "mbid:artist-adamlar-01", "0.78", "https://www.last.fm/music/Adamlar"),
            new("Brek", null, "0.74", "https://www.last.fm/music/Brek"),
            new("Dolu Kadehi Ters Tut", "mbid:artist-dktt-01", "0.71", "https://www.last.fm/music/Dolu+Kadehi+Ters+Tut"),
            new("Yüzyüzeyken Konuşuruz", "mbid:artist-yyk-01", "0.68", "https://www.last.fm/music/Y%C3%BCzy%C3%BCzeyken+Konu%C5%9Furuz"),
            new("Kutay Soyocak", null, "0.65", "https://www.last.fm/music/Kutay+Soyocak")
        },
        ["Son Feci Bisiklet"] = new List<LastFmArtistSummary>
        {
            new("Adamlar", "mbid:artist-adamlar-01", "0.90", "https://www.last.fm/music/Adamlar"),
            new("Dolu Kadehi Ters Tut", "mbid:artist-dktt-01", "0.88", "https://www.last.fm/music/Dolu+Kadehi+Ters+Tut"),
            new("Yüzyüzeyken Konuşuruz", "mbid:artist-yyk-01", "0.85", "https://www.last.fm/music/Y%C3%BCzy%C3%BCzeyken+Konu%C5%9Furuz"),
            new("Jakuzi", "mbid:artist-jakuzi-01", "0.80", "https://www.last.fm/music/Jakuzi"),
            new("Büyük Ev Ablukada", "mbid:artist-bea-01", "0.75", "https://www.last.fm/music/B%C3%BCy%C3%BCk+Ev+Ablukada")
        },
        ["M.O.O.N."] = new List<LastFmArtistSummary>
        {
            new("Perturbator", "mbid:artist-perturbator-01", "0.88", "https://www.last.fm/music/Perturbator"),
            new("Carpenter Brut", "mbid:artist-cbrut-01", "0.86", "https://www.last.fm/music/Carpenter+Brut"),
            new("Jasper Byrne", null, "0.82", "https://www.last.fm/music/Jasper+Byrne"),
            new("Scattle", null, "0.80", "https://www.last.fm/music/Scattle"),
            new("El Huervo", null, "0.78", "https://www.last.fm/music/El+Huervo"),
            new("Miami Nights 1984", "mbid:artist-mn1984-01", "0.74", "https://www.last.fm/music/Miami+Nights+1984")
        },
        ["Perturbator"] = new List<LastFmArtistSummary>
        {
            new("Carpenter Brut", "mbid:artist-cbrut-01", "0.92", "https://www.last.fm/music/Carpenter+Brut"),
            new("GosT", "mbid:artist-gost-01", "0.85", "https://www.last.fm/music/GosT"),
            new("Dance With the Dead", "mbid:artist-dwtd-01", "0.83", "https://www.last.fm/music/Dance+With+the+Dead"),
            new("M.O.O.N.", null, "0.80", "https://www.last.fm/music/M.O.O.N."),
            new("Daniel Deluxe", null, "0.77", "https://www.last.fm/music/Daniel+Deluxe")
        },
        ["Jasper Byrne"] = new List<LastFmArtistSummary>
        {
            new("M.O.O.N.", null, "0.85", "https://www.last.fm/music/M.O.O.N."),
            new("Scattle", null, "0.83", "https://www.last.fm/music/Scattle"),
            new("Perturbator", "mbid:artist-perturbator-01", "0.80", "https://www.last.fm/music/Perturbator"),
            new("Disasterpeace", "mbid:artist-disasterpeace-01", "0.75", "https://www.last.fm/music/Disasterpeace")
        },
        ["Paweł Błaszczak"] = new List<LastFmArtistSummary>
        {
            new("Marcin Przybyłowicz", "mbid:artist-marcin-01", "0.82", "https://www.last.fm/music/Marcin+Przyby%C5%82owicz"),
            new("Mikolai Stroinski", "mbid:artist-mikolai-01", "0.78", "https://www.last.fm/music/Mikolai+Stroinski"),
            new("Jesper Kyd", "mbid:artist-jkyd-01", "0.75", "https://www.last.fm/music/Jesper+Kyd"),
            new("Gareth Coker", "mbid:artist-coker-01", "0.71", "https://www.last.fm/music/Gareth+Coker")
        },
        ["Heaven Pierce Her"] = new List<LastFmArtistSummary>
        {
            new("Keygen Church", null, "0.86", "https://www.last.fm/music/Keygen+Church"),
            new("Master Boot Record", "mbid:artist-mbr-01", "0.83", "https://www.last.fm/music/Master+Boot+Record"),
            new("Mick Gordon", "mbid:artist-mickgordon-01", "0.81", "https://www.last.fm/music/Mick+Gordon"),
            new("Meganeko", null, "0.76", "https://www.last.fm/music/Meganeko")
        },
        ["Darren Korb"] = new List<LastFmArtistSummary>
        {
            new("Christopher Larkin", "mbid:artist-larkin-01", "0.84", "https://www.last.fm/music/Christopher+Larkin"),
            new("Austin Wintory", "mbid:artist-wintory-01", "0.81", "https://www.last.fm/music/Austin+Wintory"),
            new("Lena Raine", "mbid:artist-raine-01", "0.79", "https://www.last.fm/music/Lena+Raine"),
            new("Disasterpeace", "mbid:artist-disasterpeace-01", "0.76", "https://www.last.fm/music/Disasterpeace")
        },
        ["Mick Gordon"] = new List<LastFmArtistSummary>
        {
            new("Andrew Hulshult", null, "0.85", "https://www.last.fm/music/Andrew+Hulshult"),
            new("Heaven Pierce Her", null, "0.81", "https://www.last.fm/music/Heaven+Pierce+Her"),
            new("Geoffplayguitar", null, "0.79", "https://www.last.fm/music/Geoffplayguitar"),
            new("Perturbator", "mbid:artist-perturbator-01", "0.72", "https://www.last.fm/music/Perturbator")
        }
    };

    private static readonly Dictionary<string, IReadOnlyList<LastFmTagItem>> ArtistTopTags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jakuzi"] = new List<LastFmTagItem>
        {
            new("synthpop", 100, null),
            new("darkwave", 88, null),
            new("post-punk", 76, null),
            new("turkish", 65, null),
            new("new wave", 52, null),
            new("seen live", 12, null) // intentional noise tag for testing
        },
        ["Son Feci Bisiklet"] = new List<LastFmTagItem>
        {
            new("indie rock", 100, null),
            new("turkish rock", 92, null),
            new("alternative", 80, null),
            new("turkish", 74, null),
            new("indie", 62, null),
            new("favorite", 8, null) // intentional noise tag
        },
        ["M.O.O.N."] = new List<LastFmTagItem>
        {
            new("synthwave", 100, null),
            new("electronic", 86, null),
            new("darksynth", 78, null),
            new("soundtrack", 64, null),
            new("hotline miami", 58, null)
        },
        ["Perturbator"] = new List<LastFmTagItem>
        {
            new("darksynth", 100, null),
            new("synthwave", 94, null),
            new("cyberpunk", 82, null),
            new("electronic", 76, null),
            new("industrial", 60, null)
        },
        ["Jasper Byrne"] = new List<LastFmTagItem>
        {
            new("chiptune", 100, null),
            new("electronic", 88, null),
            new("soundtrack", 79, null),
            new("synthwave", 68, null)
        },
        ["Paweł Błaszczak"] = new List<LastFmTagItem>
        {
            new("soundtrack", 100, null),
            new("dark ambient", 85, null),
            new("ambient", 78, null),
            new("instrumental", 64, null),
            new("game soundtrack", 55, null)
        },
        ["Heaven Pierce Her"] = new List<LastFmTagItem>
        {
            new("breakcore", 100, null),
            new("heavy metal", 88, null),
            new("industrial metal", 80, null),
            new("video game music", 72, null),
            new("soundtrack", 65, null)
        },
        ["Darren Korb"] = new List<LastFmTagItem>
        {
            new("soundtrack", 100, null),
            new("video game music", 89, null),
            new("indie game", 82, null),
            new("acoustic", 68, null),
            new("folk rock", 55, null)
        },
        ["Mick Gordon"] = new List<LastFmTagItem>
        {
            new("industrial metal", 100, null),
            new("djent", 91, null),
            new("soundtrack", 87, null),
            new("heavy metal", 79, null),
            new("instrumental metal", 65, null)
        },
        ["She Past Away"] = new List<LastFmTagItem>
        {
            new("darkwave", 100, null),
            new("post-punk", 92, null),
            new("gothic rock", 84, null),
            new("turkish", 60, null)
        },
        ["Adamlar"] = new List<LastFmTagItem>
        {
            new("turkish rock", 100, null),
            new("indie rock", 88, null),
            new("alternative rock", 75, null)
        },
        ["Dolu Kadehi Ters Tut"] = new List<LastFmTagItem>
        {
            new("turkish indie", 100, null),
            new("indie pop", 85, null),
            new("alternative", 72, null)
        }
    };

    private static readonly Dictionary<string, IReadOnlyList<LastFmTrackItem>> ArtistTopTracks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jakuzi"] = new List<LastFmTrackItem>
        {
            new("Koca Bir Saçmalık", "mbid:track-jakuzi-01", "https://www.last.fm/music/Jakuzi/_/Koca+Bir+Sa%C3%A7mal%C4%B1k", "214", "26000", "160000", new LastFmArtistRef("Jakuzi", "mbid:artist-jakuzi-01", null)),
            new("Bir Düşün", "mbid:track-jakuzi-02", "https://www.last.fm/music/Jakuzi/_/Bir+D%C3%BC%C5%9F%C3%BCn", "195", "18000", "110000", new LastFmArtistRef("Jakuzi", "mbid:artist-jakuzi-01", null)),
            new("Şüphe", "mbid:track-jakuzi-03", "https://www.last.fm/music/Jakuzi/_/%C5%9E%C3%BCphe", "230", "15000", "89000", new LastFmArtistRef("Jakuzi", "mbid:artist-jakuzi-01", null))
        },
        ["Son Feci Bisiklet"] = new List<LastFmTrackItem>
        {
            new("Bikini Gecesi", "mbid:track-sfb-01", "https://www.last.fm/music/Son+Feci+Bisiklet/_/Bikini+Gecesi", "210", "42000", "310000", new LastFmArtistRef("Son Feci Bisiklet", "mbid:artist-sfb-01", null)),
            new("Bu Kız", "mbid:track-sfb-02", "https://www.last.fm/music/Son+Feci+Bisiklet/_/Bu+K%C4%B1z", "185", "55000", "420000", new LastFmArtistRef("Son Feci Bisiklet", "mbid:artist-sfb-01", null)),
            new("Niyazi Gül Bayırda", "mbid:track-sfb-03", "https://www.last.fm/music/Son+Feci+Bisiklet/_/Niyazi+G%C3%BCl+Bay%C4%B1rda", "198", "31000", "220000", new LastFmArtistRef("Son Feci Bisiklet", "mbid:artist-sfb-01", null))
        },
        ["M.O.O.N."] = new List<LastFmTrackItem>
        {
            new("Paris", null, "https://www.last.fm/music/M.O.O.N./_/Paris", "271", "68000", "490000", new LastFmArtistRef("M.O.O.N.", null, null)),
            new("Hydrogen", null, "https://www.last.fm/music/M.O.O.N./_/Hydrogen", "289", "85000", "620000", new LastFmArtistRef("M.O.O.N.", null, null)),
            new("Crystals", null, "https://www.last.fm/music/M.O.O.N./_/Crystals", "295", "54000", "380000", new LastFmArtistRef("M.O.O.N.", null, null))
        },
        ["Perturbator"] = new List<LastFmTrackItem>
        {
            new("Future Club", "mbid:track-perturbator-01", "https://www.last.fm/music/Perturbator/_/Future+Club", "289", "95000", "710000", new LastFmArtistRef("Perturbator", "mbid:artist-perturbator-01", null)),
            new("She Is Young, She Is Beautiful", "mbid:track-perturbator-02", "https://www.last.fm/music/Perturbator/_/She+Is+Young", "275", "82000", "610000", new LastFmArtistRef("Perturbator", "mbid:artist-perturbator-01", null)),
            new("Venger", "mbid:track-perturbator-03", "https://www.last.fm/music/Perturbator/_/Venger", "308", "67000", "520000", new LastFmArtistRef("Perturbator", "mbid:artist-perturbator-01", null))
        },
        ["Paweł Błaszczak"] = new List<LastFmTrackItem>
        {
            new("Empowering Yourself", null, "https://www.last.fm/music/Pawe%C5%82+B%C5%82aszczak/_/Empowering+Yourself", "240", "12000", "75000", new LastFmArtistRef("Paweł Błaszczak", null, null)),
            new("Breath of the City", null, "https://www.last.fm/music/Pawe%C5%82+B%C5%82aszczak/_/Breath+of+the+City", "215", "14000", "88000", new LastFmArtistRef("Paweł Błaszczak", null, null)),
            new("There Is Hope", null, "https://www.last.fm/music/Pawe%C5%82+B%C5%82aszczak/_/There+Is+Hope", "260", "9500", "54000", new LastFmArtistRef("Paweł Błaszczak", null, null)),
            new("Wandering in the Wastelands", null, "https://www.last.fm/music/Pawe%C5%82+B%C5%82aszczak/_/Wandering+in+the+Wastelands", "230", "8200", "46000", new LastFmArtistRef("Paweł Błaszczak", null, null))
        },
        ["Heaven Pierce Her"] = new List<LastFmTrackItem>
        {
            new("The Cyber Grind", null, "https://www.last.fm/music/Heaven+Pierce+Her/_/The+Cyber+Grind", "282", "38000", "280000", new LastFmArtistRef("Heaven Pierce Her", null, null)),
            new("ORDER", null, "https://www.last.fm/music/Heaven+Pierce+Her/_/ORDER", "275", "41000", "310000", new LastFmArtistRef("Heaven Pierce Her", null, null)),
            new("Versus", null, "https://www.last.fm/music/Heaven+Pierce+Her/_/Versus", "240", "32000", "220000", new LastFmArtistRef("Heaven Pierce Her", null, null))
        },
        ["Darren Korb"] = new List<LastFmTrackItem>
        {
            new("The Unseen Ones", "mbid:track-hades-01", "https://www.last.fm/music/Darren+Korb/_/The+Unseen+Ones", "257", "49000", "360000", new LastFmArtistRef("Darren Korb", "mbid:artist-korb-01", null)),
            new("Good Riddance", "mbid:track-hades-02", "https://www.last.fm/music/Darren+Korb/_/Good+Riddance", "184", "88000", "720000", new LastFmArtistRef("Darren Korb", "mbid:artist-korb-01", null)),
            new("In the Blood", "mbid:track-hades-03", "https://www.last.fm/music/Darren+Korb/_/In+the+Blood", "250", "62000", "470000", new LastFmArtistRef("Darren Korb", "mbid:artist-korb-01", null))
        },
        ["Mick Gordon"] = new List<LastFmTrackItem>
        {
            new("The Only Thing They Fear Is You", "mbid:track-mick-01", "https://www.last.fm/music/Mick+Gordon/_/The+Only+Thing+They+Fear+Is+You", "413", "120000", "980000", new LastFmArtistRef("Mick Gordon", "mbid:artist-mickgordon-01", null)),
            new("BFG Division", "mbid:track-mick-02", "https://www.last.fm/music/Mick+Gordon/_/BFG+Division", "506", "180000", "1450000", new LastFmArtistRef("Mick Gordon", "mbid:artist-mickgordon-01", null))
        },
        ["She Past Away"] = new List<LastFmTrackItem>
        {
            new("Kasvetli Kutlama", "mbid:track-spa-01", "https://www.last.fm/music/She+Past+Away/_/Kasvetli+Kutlama", "264", "48000", "390000", new LastFmArtistRef("She Past Away", "mbid:artist-spa-01", null)),
            new("Ruh", "mbid:track-spa-02", "https://www.last.fm/music/She+Past+Away/_/Ruh", "288", "36000", "280000", new LastFmArtistRef("She Past Away", "mbid:artist-spa-01", null))
        },
        ["Adamlar"] = new List<LastFmTrackItem>
        {
            new("Koca Yaşlı Şişko Dünya", "mbid:track-adamlar-01", "https://www.last.fm/music/Adamlar/_/Koca+Ya%C5%9Fl%C4%B1+%C5%9Ei%C5%9Fko+D%C3%BCnya", "245", "65000", "510000", new LastFmArtistRef("Adamlar", "mbid:artist-adamlar-01", null)),
            new("Rüyalarda Buruşmuşuz", "mbid:track-adamlar-02", "https://www.last.fm/music/Adamlar/_/R%C3%BCyalarda+Buru%C5%9Fmu%C5%9Fuz", "280", "51000", "390000", new LastFmArtistRef("Adamlar", "mbid:artist-adamlar-01", null))
        }
    };

    private static readonly Dictionary<string, IReadOnlyList<LastFmTrackItem>> TagTopTracks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["darkwave"] = new List<LastFmTrackItem>
        {
            new("Kasvetli Kutlama", "mbid:track-spa-01", "https://www.last.fm/music/She+Past+Away/_/Kasvetli+Kutlama", "264", "48000", "390000", new LastFmArtistRef("She Past Away", "mbid:artist-spa-01", null)),
            new("Koca Bir Saçmalık", "mbid:track-jakuzi-01", "https://www.last.fm/music/Jakuzi/_/Koca+Bir+Sa%C3%A7mal%C4%B1k", "214", "26000", "160000", new LastFmArtistRef("Jakuzi", "mbid:artist-jakuzi-01", null))
        },
        ["synthwave"] = new List<LastFmTrackItem>
        {
            new("Future Club", "mbid:track-perturbator-01", "https://www.last.fm/music/Perturbator/_/Future+Club", "289", "95000", "710000", new LastFmArtistRef("Perturbator", "mbid:artist-perturbator-01", null)),
            new("Paris", null, "https://www.last.fm/music/M.O.O.N./_/Paris", "271", "68000", "490000", new LastFmArtistRef("M.O.O.N.", null, null))
        },
        ["soundtrack"] = new List<LastFmTrackItem>
        {
            new("The Unseen Ones", "mbid:track-hades-01", "https://www.last.fm/music/Darren+Korb/_/The+Unseen+Ones", "257", "49000", "360000", new LastFmArtistRef("Darren Korb", "mbid:artist-korb-01", null)),
            new("Empowering Yourself", null, "https://www.last.fm/music/Pawe%C5%82+B%C5%82aszczak/_/Empowering+Yourself", "240", "12000", "75000", new LastFmArtistRef("Paweł Błaszczak", null, null))
        },
        ["turkish rock"] = new List<LastFmTrackItem>
        {
            new("Bu Kız", "mbid:track-sfb-02", "https://www.last.fm/music/Son+Feci+Bisiklet/_/Bu+K%C4%B1z", "185", "55000", "420000", new LastFmArtistRef("Son Feci Bisiklet", "mbid:artist-sfb-01", null)),
            new("Koca Yaşlı Şişko Dünya", "mbid:track-adamlar-01", "https://www.last.fm/music/Adamlar/_/Koca+Ya%C5%9Fl%C4%B1+%C5%9Ei%C5%9Fko+D%C3%BCnya", "245", "65000", "510000", new LastFmArtistRef("Adamlar", "mbid:artist-adamlar-01", null))
        }
    };

    public static IReadOnlyList<LastFmArtistSummary> GetSimilarArtists(string artistName) =>
        SimilarArtists.TryGetValue(artistName.Trim(), out var list) ? list.ToArray() : [];

    public static IReadOnlyList<LastFmTagItem> GetArtistTopTags(string artistName) =>
        ArtistTopTags.TryGetValue(artistName.Trim(), out var list) ? list.ToArray() : [];

    public static IReadOnlyList<LastFmTrackItem> GetArtistTopTracks(string artistName) =>
        ArtistTopTracks.TryGetValue(artistName.Trim(), out var list) ? list.ToArray() : [];

    public static IReadOnlyList<LastFmTrackItem> GetTagTopTracks(string tag) =>
        TagTopTracks.TryGetValue(tag.Trim(), out var list) ? list.ToArray() : [];
}
