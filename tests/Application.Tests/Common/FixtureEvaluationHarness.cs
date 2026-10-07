using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LinerNotes.Application.Tests.Common;

public enum EvaluationSet { Founder, Development, HeldOut, Synthetic }
public sealed record EvaluationProfile(string Id, EvaluationSet Set, string Genre, IReadOnlyList<string> Seeds);

public sealed class ProfileLock
{
    public static ImmutableArray<string> Genres { get; } = ["Metal", "HipHop", "Electronic", "Jazz", "Pop", "Indie", "Classical", "RegionalScene"];
    public ImmutableArray<EvaluationProfile> Profiles { get; }
    public string Hash { get; }
    public string ReviewReference { get; }

    public ProfileLock(IEnumerable<EvaluationProfile> profiles, string expectedHash, string reviewReference)
    {
        Profiles = Snapshot(profiles);
        Validate(Profiles);
        Hash = ComputeHash(Profiles);
        if (!StringComparer.Ordinal.Equals(Hash, expectedHash) || string.IsNullOrWhiteSpace(reviewReference))
            throw new ArgumentException("Profile membership requires its reviewed hash and review reference.");
        ReviewReference = reviewReference;
    }

    public static string Canonical(string value) => value.Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();

    public async Task WriteLocalAsync(string repositoryRoot, string runId, CancellationToken cancellationToken = default)
    {
        // This is a generated lock, never a tracked design artifact.
        if (string.IsNullOrWhiteSpace(runId) || runId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
            throw new ArgumentException("Invalid lock directory ID.");
        string root = Path.GetFullPath(repositoryRoot);
        string recordings = Path.Combine(root, "recordings");
        string output = Path.Combine(recordings, runId, "profile-lock.json");
        if (Directory.Exists(recordings) && new DirectoryInfo(recordings).LinkTarget is not null)
            throw new InvalidOperationException("Recording root cannot be a symlink.");
        foreach (var args in new[] { new[] { "check-ignore", "--no-index", "--quiet", "--", output }, new[] { "ls-files", "--", output } })
        {
            var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Cannot check git containment.");
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0 || (args[0] == "ls-files" && !string.IsNullOrWhiteSpace(stdout)))
                throw new InvalidOperationException("Lock must be ignored and untracked.");
        }
        string directory = Path.GetDirectoryName(output)!;
        if (Directory.Exists(directory)) throw new InvalidOperationException("Refuse to overwrite a previous lock run.");
        Directory.CreateDirectory(directory);
        var payload = new { version = "profile-lock-v1-nfc-invariant", hash = Hash, review_reference = ReviewReference,
            profiles = Profiles.Select(p => new { id = p.Id, set = p.Set.ToString(), genre = p.Genre, seeds = p.Seeds }).ToArray() };
        await using var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await System.Text.Json.JsonSerializer.SerializeAsync(stream, payload, cancellationToken: cancellationToken);
    }

    public static string ComputeHash(IEnumerable<EvaluationProfile> profiles)
    {
        using var stream = new MemoryStream();
        void Add(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> size = stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(size, bytes.Length);
            stream.Write(size); stream.Write(bytes);
        }
        Add("profile-lock-v1-nfc-invariant");
        foreach (var p in Snapshot(profiles))
        {
            Add(p.Id); Add(p.Set.ToString()); Add(p.Genre); Add(p.Seeds.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var seed in p.Seeds) Add(seed);
        }
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static ImmutableArray<EvaluationProfile> Snapshot(IEnumerable<EvaluationProfile> profiles) =>
        profiles.Select(p => new EvaluationProfile(Canonical(p.Id), p.Set, p.Genre,
            p.Seeds.Select(Canonical).Order(StringComparer.Ordinal).ToImmutableArray()))
            .OrderBy(p => p.Id, StringComparer.Ordinal).ToImmutableArray();

    private static void Validate(ImmutableArray<EvaluationProfile> profiles)
    {
        if (profiles.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != profiles.Length ||
            profiles.Any(p => string.IsNullOrWhiteSpace(p.Id) || !Enum.IsDefined(p.Set) || p.Seeds.Count == 0 ||
                p.Seeds.Any(string.IsNullOrWhiteSpace) || p.Seeds.Distinct(StringComparer.Ordinal).Count() != p.Seeds.Count))
            throw new ArgumentException("Invalid or duplicate profile/seed identities.");
        foreach (var set in new[] { EvaluationSet.Development, EvaluationSet.HeldOut })
        {
            var group = profiles.Where(p => p.Set == set).ToArray();
            if (group.Length != 16 || Genres.Any(g => group.Count(p => p.Genre == g) != 2) ||
                group.Any(p => p.Seeds.Count is < 2 or > 5)) throw new ArgumentException("Expected two profiles per genre, two to five seeds each.");
        }
        if (profiles.Count(p => p.Set == EvaluationSet.Founder) != 6 ||
            Genres.Any(g => !profiles.Any(p => p.Set == EvaluationSet.Synthetic && p.Genre == g)))
            throw new ArgumentException("Expected six founder profiles and eight synthetic genres.");
        var real = profiles.Where(p => p.Set != EvaluationSet.Synthetic).ToArray();
        foreach (var held in real.Where(p => p.Set == EvaluationSet.HeldOut))
            if (real.Where(p => p.Set != EvaluationSet.HeldOut).SelectMany(p => p.Seeds)
                .Intersect(held.Seeds, StringComparer.Ordinal).Any()) throw new ArgumentException("Held-out seed leakage.");
        var development = real.Where(p => p.Set == EvaluationSet.Development).SelectMany(p => p.Seeds).ToArray();
        if (development.Distinct(StringComparer.Ordinal).Count() != development.Length)
            throw new ArgumentException("Development profiles must have distinct seeds.");
        var heldSeeds = real.Where(p => p.Set == EvaluationSet.HeldOut).SelectMany(p => p.Seeds).ToArray();
        if (heldSeeds.Distinct(StringComparer.Ordinal).Count() != heldSeeds.Length)
            throw new ArgumentException("Held-out profiles must have distinct seeds.");
        if (profiles.Where(p => p.Set == EvaluationSet.Synthetic).SelectMany(p => p.Seeds)
            .Intersect(real.SelectMany(p => p.Seeds), StringComparer.Ordinal).Any())
            throw new ArgumentException("Synthetic identities overlap real seeds.");
    }
}

public sealed class OutboundRejectingHandler : HttpMessageHandler
{
    public int Attempts { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Attempts++;
        throw new InvalidOperationException("Outbound HTTP is forbidden in fixture evaluation.");
    }
}

public sealed record FixtureCandidate(EvaluationPick Pick, string Provenance = "synthetic");
public sealed record MechanicsReport(string Label, string MembershipHash, string ProfileId,
    IReadOnlyList<EvaluationPick> TopThree, IReadOnlyList<EvaluationPick> TopFive, IReadOnlyList<string> Gaps);

public sealed class FixtureEvaluationHarness(ProfileLock profileLock,
    IReadOnlyDictionary<string, IReadOnlyList<FixtureCandidate>> fixtures)
{
    private readonly ProfileLock locked = profileLock ?? throw new ArgumentNullException(nameof(profileLock));
    private readonly ImmutableDictionary<string, ImmutableArray<FixtureCandidate>> inputs = fixtures.ToImmutableDictionary(
        p => ProfileLock.Canonical(p.Key), p => p.Value.Select(c => c with
        { Pick = c.Pick with { Contributions = c.Pick.Contributions.Order().ToImmutableArray() } }).ToImmutableArray(), StringComparer.Ordinal);

    public MechanicsReport Run(string profileId, IReadOnlySet<string> known, IReadOnlySet<string> disliked,
        Func<IReadOnlyList<EvaluationPick>, IReadOnlyList<EvaluationPick>> rank,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = ProfileLock.Canonical(profileId);
        var profile = locked.Profiles.SingleOrDefault(p => p.Id == id);
        if (profile is null || profile.Set != EvaluationSet.Synthetic)
            throw new InvalidOperationException("Phase 2 accepts only locked synthetic profiles; real/held-out content stays sealed.");
        if (!inputs.TryGetValue(id, out var candidates))
            return new(EvaluationMetrics.Label, locked.Hash, id, [], [], ["No fixture for profile."]);
        if (candidates.Any(c => c.Provenance != "synthetic")) throw new InvalidOperationException("Real or mixed provenance is forbidden.");
        var excluded = known.Concat(disliked).Select(ProfileLock.Canonical).ToHashSet(StringComparer.Ordinal);
        var eligible = candidates.Where(c => !excluded.Contains(ProfileLock.Canonical(c.Pick.Key)))
            .Select(c => c.Pick).OrderBy(p => p.Key, StringComparer.Ordinal).ToImmutableArray();
        if (eligible.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count() != eligible.Length ||
            eligible.Any(p => !EvaluationMetrics.ExplanationValid(p))) throw new ArgumentException("Invalid fixture candidate.");
        var ranked = rank(eligible).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        // The callback may score, but may not fabricate or drop candidates.
        if (ranked.Length != eligible.Length || ranked.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count() != ranked.Length ||
            ranked.Any(p => !eligible.Any(c => c.Key == p.Key && c.Artist == p.Artist) || !EvaluationMetrics.ExplanationValid(p)))
            throw new InvalidOperationException("Ranking must retain eligible identities and finite reconciled explanations.");
        var ordered = ranked.Select(p => p with { Contributions = p.Contributions.Order().ToImmutableArray() })
            .OrderByDescending(p => p.Score).ThenBy(p => p.Key, StringComparer.Ordinal).ToArray();
        return new(EvaluationMetrics.Label, locked.Hash, id, ordered.Take(3).ToImmutableArray(),
            ordered.Take(5).ToImmutableArray(), ordered.Length < 3 ? ["Fewer than three supported picks."] : []);
    }

    public static string CanonicalOutput(MechanicsReport report) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(report))));
}
