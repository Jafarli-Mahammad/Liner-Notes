using LinerNotes.Application.Common.Mappings;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.DTOs.Export;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class DiscoveryProfileReader(DataContext context) : IDiscoveryProfileReader
{
    public async Task<DiscoveryExportDto> GetForAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await context.ListenerProfiles.AsNoTracking()
            .Where(p => p.UserId == userId && context.Users.Any(u => u.Id == userId))
            .Select(p => new ListenerProfileExportDto(p.Id, p.UserId, p.Revision,
                p.CreatedAtUtc, p.LastActiveAtUtc, p.ExpiresAtUtc))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (profile is null) return new(null, [], []);

        return await ReadAsync(profile, cancellationToken);
    }

    public async Task<DiscoveryExportDto> GetByProfileAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await context.ListenerProfiles.AsNoTracking().Where(p => p.Id == profileId)
            .Select(p => new ListenerProfileExportDto(p.Id, p.UserId, p.Revision,
                p.CreatedAtUtc, p.LastActiveAtUtc, p.ExpiresAtUtc)).SingleOrDefaultAsync(cancellationToken);
        return profile is null ? new(null, [], []) : await ReadAsync(profile, cancellationToken);
    }

    private async Task<DiscoveryExportDto> ReadAsync(ListenerProfileExportDto profile, CancellationToken cancellationToken)
    {
        var batches = await context.DiscoveryBatches.AsNoTracking().Where(b => b.ProfileId == profile.Id)
            .OrderBy(b => b.CreatedAtUtc).ThenBy(b => b.Id)
            .Select(b => new DiscoveryBatchExportDto(b.Id, b.ProfileId, b.RequestId, b.Kind.ToString(),
                b.InputRevision, b.CreatedAtUtc, b.CompletedAtUtc, b.CatalogVersion, b.EvidenceVersion))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var jobs = await context.DiscoveryJobs.AsNoTracking().Where(j => j.ProfileId == profile.Id)
            .OrderBy(j => j.CreatedAtUtc).ThenBy(j => j.Id)
            .Select(j => new DiscoveryJobExportDto(j.Id, j.ProfileId, j.RequestKey, j.RequestHash,
                j.Kind.ToString(), j.State.ToString(), j.InputRevision,
                JsonSerializer.Deserialize<JsonElement>(j.PayloadJson, (JsonSerializerOptions?)null), j.Attempts,
                j.LeaseExpiresAtUtc, j.NextAttemptAtUtc, j.CreatedAtUtc, j.UpdatedAtUtc, j.ProgressCode,
                j.ErrorCode, j.ResultBatchId, j.ResultJson == null ? (JsonElement?)null
                    : JsonSerializer.Deserialize<JsonElement>(j.ResultJson, (JsonSerializerOptions?)null)))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var recommendations = await context.WeeklyRecommendations.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.ProfileId == profile.Id).Include(r => r.Track).OrderBy(r => r.BatchId).ThenBy(r => r.Rank)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var signals = await context.TasteSignals.IgnoreQueryFilters().AsNoTracking().Where(s => s.ProfileId == profile.Id)
            .OrderBy(s => s.Id).ToArrayAsync(cancellationToken);
        var session = await context.GuestSessions.AsNoTracking().Where(s => s.ProfileId == profile.Id)
            .Select(s => new GuestSessionExportDto(s.ProfileId, s.CreatedAtUtc)).SingleOrDefaultAsync(cancellationToken);
        return new(profile, batches, jobs, recommendations.Select(r => r.ToDto()).ToArray(),
            signals.Select(s => s.ToDto()).ToArray(), session);
    }
}
