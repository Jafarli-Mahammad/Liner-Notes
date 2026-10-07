namespace LinerNotes.Application.Tests.Common;

public static class Phase2ProfileMembership
{
    public const string ApprovedHash = "de78f92afd725f4dd22da6446a9c41f5ca78f6959eacf14753eb092902b62c40";
    public const string ReviewReference = "Developer approved both Phase 2 proposals on 2026-10-07";
    public static ProfileLock Locked() => new(Proposed(), ApprovedHash, ReviewReference);

    // Matches the developer-approved metadata in docs/renewal/phase-2-design.md.
    public static IReadOnlyList<EvaluationProfile> Proposed()
    {
        string[][] development = [
            ["Agalloch", "Alcest", "Metallica", "Slayer"],
            ["Billy Woods", "Armand Hammer", "Drake", "Future"],
            ["Skee Mask", "Autechre", "Calvin Harris", "deadmau5"],
            ["Pharoah Sanders", "Alice Coltrane", "Miles Davis", "John Coltrane"],
            ["Julia Holter", "Weyes Blood", "Taylor Swift", "Carly Rae Jepsen"],
            ["The Murder Capital", "Fontaines D.C.", "Arctic Monkeys", "The Strokes"],
            ["Sarah Davachi", "Kali Malone", "Ludovico Einaudi", "Max Richter"],
            ["Altın Gün", "Gaye Su Akyol", "Alim Qasimov", "Fargana Qasimova"]];
        string[][] heldOut = [
            ["Wolves in the Throne Room", "Panopticon", "Judas Priest", "Iron Maiden"],
            ["Open Mike Eagle", "Quelle Chris", "Kendrick Lamar", "J. Cole"],
            ["Aphex Twin", "Squarepusher", "Avicii", "Disclosure"],
            ["Sun Ra", "Albert Ayler", "Herbie Hancock", "Wayne Shorter"],
            ["Kate Bush", "Björk", "Dua Lipa", "Robyn"],
            ["Protomartyr", "Preoccupations", "The National", "Interpol"],
            ["Éliane Radigue", "Pauline Oliveros", "Nils Frahm", "Ólafur Arnalds"],
            ["Erkin Koray", "Selda Bağcan", "Vaqif Mustafazadə", "Aziza Mustafa Zadeh"]];
        var result = new List<EvaluationProfile>();
        for (int g = 0; g < ProfileLock.Genres.Length; g++)
        {
            string genre = ProfileLock.Genres[g];
            for (int n = 0; n < 2; n++)
            {
                result.Add(new($"development-{genre}-{n + 1}", EvaluationSet.Development, genre, development[g].Skip(n * 2).Take(2).ToArray()));
                result.Add(new($"heldout-{genre}-{n + 1}", EvaluationSet.HeldOut, genre, heldOut[g].Skip(n * 2).Take(2).ToArray()));
            }
            result.Add(new($"synthetic-{genre}", EvaluationSet.Synthetic, genre, [$"synthetic:{genre}:seed"]));
        }
        string[][] founder = [["Jakuzi"], ["Son Feci Bisiklet"], ["M.O.O.N.", "Perturbator", "Jasper Byrne"],
            ["Paweł Błaszczak"], ["Heaven Pierce Her"], ["Darren Korb"]];
        string[] clusters = ["Jakuzi", "Son Feci Bisiklet", "Hotline Miami", "Dying Light", "ULTRAKILL", "Hades"];
        for (int i = 0; i < founder.Length; i++)
            result.Add(new($"founder-{i + 1}", EvaluationSet.Founder, clusters[i], founder[i]));
        foreach (var edge in new[] { "missing", "invalid", "tied", "empty", "exclusion" })
            result.Add(new($"synthetic-{edge}", EvaluationSet.Synthetic, "Edge", [$"synthetic:{edge}:seed"]));
        return result;
    }
}
