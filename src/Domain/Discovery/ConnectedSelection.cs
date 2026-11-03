using LinerNotes.Domain.Catalog;

namespace LinerNotes.Domain.Discovery;

public sealed record SelectionConfiguration
{
    public double MinimumAffinity { get; init; } = 0.1;
    public double IndividualInterestWeight { get; init; } = 0.35;
    public int MinimumIndividualSharedTags { get; init; } = 2;
    public double GoalWeight { get; init; } = 0.25;
    public double ExplorationWeight { get; init; } = 0.35;
    public double VocalWeight { get; init; } = 0.15;
    public double RecentArtistWeight { get; init; } = 0.15;
    public int BroadTagSlots { get; init; } = 3;
    public int CloseLimit { get; init; } = 3;
    public int BalancedLimit { get; init; } = 6;
    public int SurpriseLimit { get; init; } = 10;

    public void Validate()
    {
        if (!double.IsFinite(MinimumAffinity) || MinimumAffinity is <= 0 or > 1 ||
            !double.IsFinite(IndividualInterestWeight) || IndividualInterestWeight is < 0 or > 1 ||
            MinimumIndividualSharedTags is < 1 or > 10 ||
            new[] { GoalWeight, ExplorationWeight, VocalWeight, RecentArtistWeight }.Any(w => !double.IsFinite(w) || w is < 0 or > 2) ||
            BroadTagSlots is < 0 or > 20 || CloseLimit < 1 || BalancedLimit < CloseLimit || SurpriseLimit < BalancedLimit || SurpriseLimit > 10)
            throw new ArgumentException("invalid_selection_configuration");
    }

    public int AcquisitionLimit(TasteOnboarding? answers, int maximum)
    {
        Validate();
        answers?.Validate();
        if (maximum is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(maximum));
        return Math.Min(maximum, answers?.Exploration switch { "close" => CloseLimit, "surprise" => SurpriseLimit, _ => BalancedLimit });
    }
}

public sealed record SelectionInterest(string Identity, WeightedTagVector Tags, double Influence, string? Label = null);
public sealed record IndividualConnection(double Affinity, string? Identity);
public sealed record SelectionCandidate(string TrackKey, IReadOnlyList<string> Aliases, IReadOnlyList<string> ArtistKeys,
    double BaseScore, double AggregateAffinity, double IndividualAffinity, string? InterestIdentity,
    string? RecordingSeed, double Novelty, int VocalEvidence, string? InterestLabel = null);
public sealed record SelectionInput(SelectionConfiguration Configuration, TasteOnboarding? Answers, int Requested,
    IReadOnlyList<SelectionCandidate> Candidates, IReadOnlyList<string> ExcludedAliases,
    IReadOnlyDictionary<string, int> RecentArtists);
public sealed record SelectionDecision(string TrackKey, double Affinity, double IndividualAdjustment, double GoalAdjustment,
    double ExplorationAdjustment, double VocalAdjustment, double HistoryAdjustment, double Utility, string Reason,
    int? Rank, int? RankWithoutGoal, int? RankWithoutExploration, int? RankWithoutVocals, int? RankWithoutHistory);
public sealed record SelectionReceipt(string Version, SelectionInput Input, IReadOnlyList<SelectionDecision> Decisions,
    IReadOnlyList<string> SelectedKeys, int Shortfall, bool SelectedDecisionsOnly = false);

/// <summary>Pure, bounded selection over stored numerical evidence. No provider access or inferred acoustic facts.</summary>
public static class ConnectedSelection
{
    public const string Version = "connected-selection-v2";

    public static IndividualConnection BestInterest(WeightedTagVector candidate, IReadOnlyList<SelectionInterest> interests, int minimumSharedTags)
    {
        if (minimumSharedTags is < 1 or > 10) throw new ArgumentOutOfRangeException(nameof(minimumSharedTags));
        var best = new IndividualConnection(0, null);
        foreach (var interest in interests)
        {
            if (!double.IsFinite(interest.Influence) || interest.Influence is < 0 or > 1) throw new ArgumentException("invalid_interest_influence");
            if (candidate.Weights.Keys.Count(interest.Tags.Weights.ContainsKey) < minimumSharedTags) continue;
            double affinity = Math.Clamp(candidate.CosineSimilarity(interest.Tags), 0, 1) * interest.Influence;
            if (affinity > best.Affinity || (affinity > 0 && affinity == best.Affinity && string.CompareOrdinal(interest.Identity, best.Identity) < 0))
                best = new(affinity, interest.Identity);
        }
        return best;
    }

    public static SelectionReceipt Select(SelectionInput input)
    {
        Validate(input);
        var config = input.Configuration;
        var excluded = input.ExcludedAliases.ToHashSet(StringComparer.Ordinal);
        var terms = input.Candidates.OrderBy(c => c.TrackKey, StringComparer.Ordinal).Select(c =>
        {
            double individual = config.IndividualInterestWeight * Math.Max(0, c.IndividualAffinity - c.AggregateAffinity);
            double affinity = c.AggregateAffinity + individual;
            double connection = c.RecordingSeed is null ? affinity : 1;
            double goal = config.GoalWeight * (input.Answers?.Goal switch
            {
                "new-artists" => c.Novelty,
                "favorites" => Math.Max(c.IndividualAffinity, c.RecordingSeed is null ? 0 : 1),
                "different-styles" => (1 - c.AggregateAffinity) * connection,
                _ => 0
            });
            double exploration = config.ExplorationWeight * (input.Answers?.Exploration switch
            {
                "close" => c.AggregateAffinity,
                "surprise" => (1 - c.AggregateAffinity) * connection,
                _ => 0
            });
            int vocal = input.Answers?.Vocals switch { "vocals" => c.VocalEvidence, "instrumental" => -c.VocalEvidence, _ => 0 };
            int recent = c.ArtistKeys.Select(k => input.RecentArtists.GetValueOrDefault(k)).DefaultIfEmpty().Max();
            double history = -config.RecentArtistWeight * recent / (recent + 1.0);
            string? rejection = c.Aliases.Append(c.TrackKey).Any(excluded.Contains) ? "excluded" :
                affinity < config.MinimumAffinity && c.RecordingSeed is null ? "insufficient_connection" : null;
            return (Candidate: c, Rejection: rejection, Decision: new SelectionDecision(c.TrackKey, affinity, individual, goal, exploration,
                config.VocalWeight * vocal, history, c.BaseScore + individual + goal + exploration + config.VocalWeight * vocal + history,
                "limit", null, null, null, null, null));
        }).ToArray();
        var eligible = terms.Where(t => t.Rejection is null).ToArray();
        var ranks = Choose(0);
        var noGoal = Choose(1);
        var noExploration = Choose(2);
        var noVocals = Choose(3);
        var noHistory = Choose(4);
        var usedArtists = eligible.Where(t => ranks.ContainsKey(t.Candidate.TrackKey)).SelectMany(t => t.Candidate.ArtistKeys).ToHashSet(StringComparer.Ordinal);
        var decisions = terms.Select(t => t.Decision with
        {
            Reason = t.Rejection ?? (ranks.ContainsKey(t.Candidate.TrackKey) ? "selected" :
                t.Candidate.ArtistKeys.Any(usedArtists.Contains) ? "artist_diversity" : "limit"),
            Rank = Rank(ranks, t.Candidate.TrackKey),
            RankWithoutGoal = Rank(noGoal, t.Candidate.TrackKey),
            RankWithoutExploration = Rank(noExploration, t.Candidate.TrackKey),
            RankWithoutVocals = Rank(noVocals, t.Candidate.TrackKey),
            RankWithoutHistory = Rank(noHistory, t.Candidate.TrackKey)
        }).ToArray();
        return new(Version, input, decisions, ranks.OrderBy(p => p.Value).Select(p => p.Key).ToArray(), input.Requested - ranks.Count);

        Dictionary<string, int> Choose(int omit)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            var artists = new HashSet<string>(StringComparer.Ordinal);
            foreach (var term in eligible.OrderByDescending(t => t.Decision.Utility - (omit switch
            {
                1 => t.Decision.GoalAdjustment,
                2 => t.Decision.ExplorationAdjustment,
                3 => t.Decision.VocalAdjustment,
                4 => t.Decision.HistoryAdjustment,
                _ => 0
            })).ThenBy(t => t.Candidate.TrackKey, StringComparer.Ordinal))
            {
                if (term.Candidate.ArtistKeys.Any(artists.Contains)) continue;
                result.Add(term.Candidate.TrackKey, result.Count + 1);
                artists.UnionWith(term.Candidate.ArtistKeys);
                if (result.Count == input.Requested) break;
            }
            return result;
        }
    }

    public static SelectionReceipt Replay(SelectionReceipt receipt)
    {
        if (receipt.Version != Version) throw new NotSupportedException("unsupported_selection_version");
        var replay = Select(receipt.Input);
        var expectedDecisions = receipt.SelectedDecisionsOnly ? replay.Decisions.Where(d => d.Rank.HasValue) : replay.Decisions;
        if (!expectedDecisions.SequenceEqual(receipt.Decisions) || !replay.SelectedKeys.SequenceEqual(receipt.SelectedKeys) || replay.Shortfall != receipt.Shortfall)
            throw new ArgumentException("selection_receipt_mismatch");
        return replay;
    }

    private static int? Rank(IReadOnlyDictionary<string, int> ranks, string key) => ranks.TryGetValue(key, out int rank) ? rank : null;
    private static void Validate(SelectionInput input)
    {
        input.Configuration.Validate();
        input.Answers?.Validate();
        if (input.Requested is < 1 or > 5 || input.Candidates.Count > 400 || input.ExcludedAliases.Count > 800 || input.RecentArtists.Count > 1280 ||
            input.RecentArtists.Any(p => !ValidKey(p.Key) || p.Value is < 0 or > 40) || input.ExcludedAliases.Any(k => !ValidKey(k)) ||
            input.Candidates.Select(c => c.TrackKey).Distinct(StringComparer.Ordinal).Count() != input.Candidates.Count ||
            input.Candidates.Any(c => !ValidKey(c.TrackKey) || c.Aliases.Count > 3 || c.Aliases.Any(k => !ValidKey(k)) ||
                c.ArtistKeys.Count is < 1 or > 32 || c.ArtistKeys.Any(k => !ValidKey(k)) || !double.IsFinite(c.BaseScore) || c.BaseScore < 0 ||
                new[] { c.AggregateAffinity, c.IndividualAffinity, c.Novelty }.Any(v => !double.IsFinite(v) || v is < 0 or > 1) ||
                c.VocalEvidence is < -1 or > 1 || (c.RecordingSeed is not null && (!Guid.TryParseExact(c.RecordingSeed, "D", out var recording) || recording == Guid.Empty))))
            throw new ArgumentException("invalid_selection_input");
    }
    private static bool ValidKey(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 1024 && !key.Any(char.IsControl);
}
