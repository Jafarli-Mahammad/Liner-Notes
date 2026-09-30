using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Scoring;

namespace LinerNotes.Domain.Tests.Fixtures;

/// <summary>
/// Fixed benchmark seed set spanning 8 distinct genres and diverse popularity levels.
/// Used to sanity-check scoring weights and rankings against consistent, known inputs.
/// </summary>
public static class BenchmarkSeedFixtures
{
    public static class Metal
    {
        public static readonly CandidateTrack UndergroundAtmospheric = new(
            track: Track.Create("In the Shadow of Our Pale Companion", "Agalloch", "The Mantle", "mbid:artist-agalloch-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["atmospheric black metal"] = 1.0,
                ["doom metal"] = 0.8,
                ["folk metal"] = 0.7,
                ["post-rock"] = 0.6
            }),
            globalPopularity: 0.15);

        public static readonly CandidateTrack MainstreamMetal = new(
            track: Track.Create("Enter Sandman", "Metallica", "Metallica", "mbid:artist-metallica-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["heavy metal"] = 1.0,
                ["thrash metal"] = 0.9,
                ["hard rock"] = 0.8
            }),
            globalPopularity: 0.95);
    }

    public static class HipHop
    {
        public static readonly CandidateTrack UndergroundConscious = new(
            track: Track.Create("Spongebob", "Billy Woods", "Aethiopes", "mbid:artist-billy-woods-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["abstract hip hop"] = 1.0,
                ["underground hip hop"] = 0.9,
                ["conscious hip-hop"] = 0.8,
                ["experimental hip hop"] = 0.7
            }),
            globalPopularity: 0.18);

        public static readonly CandidateTrack MainstreamRap = new(
            track: Track.Create("God's Plan", "Drake", "Scorpion", "mbid:artist-drake-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["hip-hop"] = 1.0,
                ["pop rap"] = 0.9,
                ["trap"] = 0.8
            }),
            globalPopularity: 0.98);
    }

    public static class Electronic
    {
        public static readonly CandidateTrack UndergroundIdm = new(
            track: Track.Create("Session Add", "Skee Mask", "Compro", "mbid:artist-skee-mask-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["idm"] = 1.0,
                ["breakbeat"] = 0.9,
                ["ambient techno"] = 0.8,
                ["downtempo"] = 0.6
            }),
            globalPopularity: 0.20);

        public static readonly CandidateTrack MainstreamEdm = new(
            track: Track.Create("Summer", "Calvin Harris", "Motion", "mbid:artist-calvin-harris-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["edm"] = 1.0,
                ["dance-pop"] = 0.9,
                ["electro house"] = 0.8
            }),
            globalPopularity: 0.94);
    }

    public static class Jazz
    {
        public static readonly CandidateTrack SpiritualJazz = new(
            track: Track.Create("The Creator Has a Master Plan", "Pharoah Sanders", "Karma", "mbid:artist-pharoah-sanders-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["spiritual jazz"] = 1.0,
                ["modal jazz"] = 0.9,
                ["free jazz"] = 0.8,
                ["avant-garde jazz"] = 0.7
            }),
            globalPopularity: 0.22);

        public static readonly CandidateTrack ClassicJazz = new(
            track: Track.Create("So What", "Miles Davis", "Kind of Blue", "mbid:artist-miles-davis-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["modal jazz"] = 1.0,
                ["cool jazz"] = 0.9,
                ["hard bop"] = 0.8
            }),
            globalPopularity: 0.85);
    }

    public static class Pop
    {
        public static readonly CandidateTrack ArtPop = new(
            track: Track.Create("Feel You", "Julia Holter", "Have You in My Wilderness", "mbid:artist-julia-holter-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["art pop"] = 1.0,
                ["chamber pop"] = 0.9,
                ["baroque pop"] = 0.8,
                ["ambient pop"] = 0.6
            }),
            globalPopularity: 0.22);

        public static readonly CandidateTrack MainstreamPop = new(
            track: Track.Create("Cruel Summer", "Taylor Swift", "Lover", "mbid:artist-taylor-swift-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["pop"] = 1.0,
                ["synthpop"] = 0.9,
                ["dance-pop"] = 0.8
            }),
            globalPopularity: 0.99);
    }

    public static class Indie
    {
        public static readonly CandidateTrack PostPunkUnderground = new(
            track: Track.Create("Green & Blue", "The Murder Capital", "When I Have Fears", "mbid:artist-murder-capital-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["post-punk"] = 1.0,
                ["art punk"] = 0.85,
                ["gothic rock"] = 0.7,
                ["noise rock"] = 0.6
            }),
            globalPopularity: 0.24);

        public static readonly CandidateTrack MainstreamIndie = new(
            track: Track.Create("Do I Wanna Know?", "Arctic Monkeys", "AM", "mbid:artist-arctic-monkeys-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["indie rock"] = 1.0,
                ["garage rock"] = 0.8,
                ["alternative rock"] = 0.7
            }),
            globalPopularity: 0.93);
    }

    public static class Classical
    {
        public static readonly CandidateTrack ContemporaryDrone = new(
            track: Track.Create("Stations II", "Sarah Davachi", "Cantus, Descant", "mbid:artist-sarah-davachi-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["contemporary classical"] = 1.0,
                ["drone"] = 0.9,
                ["minimalism"] = 0.8,
                ["ambient"] = 0.7
            }),
            globalPopularity: 0.14);

        public static readonly CandidateTrack MainstreamNeoclassical = new(
            track: Track.Create("Nuvole Bianche", "Ludovico Einaudi", "Una Mattina", "mbid:artist-ludovico-einaudi-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["neoclassical"] = 1.0,
                ["piano"] = 0.9,
                ["modern classical"] = 0.8
            }),
            globalPopularity: 0.88);
    }

    public static class RegionalScene
    {
        public static readonly CandidateTrack AnatolianPsych = new(
            track: Track.Create("Süpürgesi Yoncadan", "Altın Gün", "Gece", "mbid:artist-altin-gun-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["anatolian rock"] = 1.0,
                ["psychedelic rock"] = 0.9,
                ["turkish psych"] = 0.8,
                ["folk rock"] = 0.7
            }),
            globalPopularity: 0.26);

        public static readonly CandidateTrack MughamFolk = new(
            track: Track.Create("Bayati Shiraz", "Alim Qasimov", "Love's Deep Ocean", "mbid:artist-alim-qasimov-01"),
            tagVector: WeightedTagVector.FromDictionary(new Dictionary<string, double>
            {
                ["mugham"] = 1.0,
                ["azerbaijani folk"] = 0.9,
                ["spiritual"] = 0.8,
                ["world"] = 0.7
            }),
            globalPopularity: 0.15);
    }

    public static IReadOnlyList<CandidateTrack> AllCandidates => new List<CandidateTrack>
    {
        Metal.UndergroundAtmospheric,
        Metal.MainstreamMetal,
        HipHop.UndergroundConscious,
        HipHop.MainstreamRap,
        Electronic.UndergroundIdm,
        Electronic.MainstreamEdm,
        Jazz.SpiritualJazz,
        Jazz.ClassicJazz,
        Pop.ArtPop,
        Pop.MainstreamPop,
        Indie.PostPunkUnderground,
        Indie.MainstreamIndie,
        Classical.ContemporaryDrone,
        Classical.MainstreamNeoclassical,
        RegionalScene.AnatolianPsych,
        RegionalScene.MughamFolk
    };
}
