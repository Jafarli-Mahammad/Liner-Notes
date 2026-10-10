using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Digests.Commands.GenerateDigest;
using LinerNotes.Application.Services;
using LinerNotes.Domain.Digest;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using LinerNotes.Infrastructure.Storage;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LinerNotes.Presentation.Development;

public sealed record ManualTestDigestRequest(string Week);

public static class ManualTestEndpoints
{
    public static IEndpointRouteBuilder MapManualTestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dev/manual-test").WithTags("Development Manual QA");
        group.MapGet("/status", GetStatus);
        group.MapPost("/storage/reconcile", ReconcileStorage).RequireAuthorization();
        group.MapPost("/digests", GenerateDigest).RequireAuthorization();
        return endpoints;
    }

    private static IResult GetStatus(
        IHostEnvironment environment,
        GenerationConfiguration generation,
        GenerationStorageOptions storage,
        IEnumerable<RecordedLastFmResponse> recordings,
        TimeProvider clock)
    {
        var recordingConfigured = generation.Origin == EvidenceOrigin.Recorded &&
            generation.RecordingExpiresAtUtc is { } expiry && expiry > clock.GetUtcNow() &&
            generation.ApprovedRecordingManifestSha256 is { Length: 64 } hash &&
            hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') && recordings.Any();
        var storageConfigured = storage.DatabaseOverheadBytesPerPick is > 0 &&
            Path.IsPathFullyQualified(storage.InventoryPath ?? string.Empty) &&
            Path.IsPathFullyQualified(storage.LeasePath ?? string.Empty) &&
            LocalGenerationStorage.Categories.All(storage.ArtifactRoots.ContainsKey);

        return Results.Ok(new
        {
            environment = environment.EnvironmentName,
            origin = generation.Origin.ToString(),
            recordingConfigured,
            recordingExpiresAtUtc = generation.RecordingExpiresAtUtc,
            storageConfigured,
            inventoryPresent = storageConfigured && File.Exists(storage.InventoryPath)
        });
    }

    private static async Task<IResult> ReconcileStorage(
        IDigestGenerationStore store,
        LocalGenerationStorage storage,
        CancellationToken cancellationToken)
    {
        try
        {
            var inventory = await storage.ReconcileAsync(
                await store.ReadStorageAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            var categories = inventory.Categories.ToDictionary(item => item.Key, item => item.Value.Bytes, StringComparer.Ordinal);
            var totalBytes = checked(inventory.DatabaseBytes + categories.Values.Sum());
            return Results.Ok(new
            {
                status = "reconciled",
                inventory.MeasuredAtUtc,
                inventory.DatabaseBytes,
                categoryBytes = categories,
                totalBytes
            });
        }
        catch (GenerationStoppedException exception)
        {
            return Results.Ok(new { status = "stopped", reason = exception.Message });
        }
    }

    private static async Task<IResult> GenerateDigest(
        ManualTestDigestRequest request,
        ICurrentUserService currentUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        IsoWeek week;
        try
        {
            week = IsoWeek.Parse(request.Week);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return Results.BadRequest(new { title = "Invalid ISO week", detail = "Use the format YYYY-Www, for example 2026-W41." });
        }

        var result = await sender.Send(new GenerateDigestCommand(currentUser.UserId, week), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
