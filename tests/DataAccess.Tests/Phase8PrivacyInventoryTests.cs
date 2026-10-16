using LinerNotes.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using Xunit.Sdk;

namespace LinerNotes.DataAccess.Tests;

public sealed class Phase8PrivacyInventoryTests
{
    // Explicit review contract. Never derive the expected columns from the model under test.
    internal static readonly IReadOnlyDictionary<string, string[]> ReviewedColumns = new Dictionary<string, string[]>
    {
        ["public.Albums"] = Columns("ArtistId,ArtistName,Id,Mbid,Title"),
        ["public.Artists"] = Columns("Id,Mbid,Name,NormalizedName"),
        ["public.DataProtectionKeys"] = Columns("FriendlyName,Id,Xml"),
        ["public.GuestSessions"] = Columns("ProfileId,CredentialHash,CreatedAtUtc"),
        ["public.ListenerProfiles"] = Columns("Id,UserId,Revision,CreatedAtUtc,LastActiveAtUtc,ExpiresAtUtc"),
        ["public.DiscoveryBatches"] = Columns("Id,ProfileId,RequestId,Kind,InputRevision,CreatedAtUtc,CompletedAtUtc,CatalogVersion,EvidenceVersion"),
        ["public.DiscoveryJobs"] = Columns("Id,ProfileId,RequestKey,RequestHash,Kind,State,InputRevision,PayloadJson,Attempts,LeaseToken,LeaseExpiresAtUtc,NextAttemptAtUtc,CreatedAtUtc,UpdatedAtUtc,ProgressCode,ErrorCode,ResultBatchId,ResultJson"),
        ["public.TasteSignals"] = Columns("Context,CreatedAt,CreatedBy,DeletedAt,DeletedBy,Id,IsDeleted,LastModifiedAt,LastModifiedBy,NormalizedTargetValue,ProfileId,Source,TargetType,TargetValue,UserId,Weight"),
        ["public.Tracks"] = Columns("AlbumTitle,ArtistName,DurationSeconds,ExternalSpotifyUrl,ExternalYoutubeUrl,Id,Mbid,NormalizedArtistName,NormalizedTitle,Title"),
        ["public.UserMusicConnections"] = Columns("CreatedAt,CreatedBy,DeletedAt,DeletedBy,EncryptedToken,ExternalUsername,Id,IsActive,IsDeleted,LastModifiedAt,LastModifiedBy,LastSyncedAt,ServiceType,UserId"),
        ["public.Users"] = Columns("CreatedAt,CreatedBy,DeletedAt,DeletedBy,DeliveryDay,DeliveryHourUtc,Email,EmailUnsubscribedAtUtc,Id,IsDeleted,LastModifiedAt,LastModifiedBy,NextDigestAt,TimeZone,WeeklyEmailOptIn"),
        ["public.WeeklyDigests"] = Columns("BatchId,CreatedAt,CreatedBy,DeletedAt,DeletedBy,ErrorMessage,Id,IsDeleted,LastModifiedAt,LastModifiedBy,ProfileId,SentAt,Status,UserId,Week,WeekEndDate,WeekStartDate"),
        ["public.WeeklyRecommendations"] = Columns("BatchId,CreatedAt,CreatedBy,DeletedAt,DeletedBy,Feedback,FeedbackComment,FeedbackGivenAt,Id,IsDeleted,LastModifiedAt,LastModifiedBy,ProfileId,Rank,Rating,ScoreBreakdown,TrackId,UserId,WeeklyDigestId"),
        ["identity.RoleClaims"] = Columns("ClaimType,ClaimValue,Id,RoleId"),
        ["identity.Roles"] = Columns("ConcurrencyStamp,Id,Name,NormalizedName"),
        ["identity.UserClaims"] = Columns("ClaimType,ClaimValue,Id,UserId"),
        ["identity.UserLogins"] = Columns("LoginProvider,ProviderDisplayName,ProviderKey,UserId"),
        ["identity.UserRoles"] = Columns("RoleId,UserId"),
        ["identity.Users"] = Columns("AccessFailedCount,ConcurrencyStamp,CreatedAt,CreatedBy,DeletedAt,DeletedBy,Email,EmailConfirmed,Id,IsDeleted,LastModifiedAt,LastModifiedBy,LockoutEnabled,LockoutEnd,NormalizedEmail,NormalizedUserName,PasswordHash,PhoneNumber,PhoneNumberConfirmed,SecurityStamp,TwoFactorEnabled,UserName"),
        ["identity.UserTokens"] = Columns("LoginProvider,Name,UserId,Value")
    };

    private static string[] Columns(string columns) => columns.Split(',');

    internal static string[] Identities(bool history = false) => ReviewedColumns
        .SelectMany(table => table.Value.Select(column => $"{table.Key}.{column}"))
        .Concat(history ? ["public.__EFMigrationsHistory.MigrationId", "public.__EFMigrationsHistory.ProductVersion"] : [])
        .Order(StringComparer.Ordinal).ToArray();

    private static void AssertCovered(IEnumerable<string> actual, bool history = false) =>
        Assert.Equal(Identities(history), actual.Order(StringComparer.Ordinal).ToArray());

    [Fact]
    public void ReviewedInventory_CoversEveryRelationalTableAndColumn()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused").Options);
        AssertCovered(db.Model.GetRelationalModel().Tables.SelectMany(table => table.Columns
            .Select(column => $"{table.Schema ?? "public"}.{table.Name}.{column.Name}")));
    }

    [Theory]
    [InlineData("public.Users.UnreviewedPersonalField")]
    [InlineData("public.NewOwnedTable.Id")]
    public void CoverageCheck_RejectsAddedColumnOrTable(string addedIdentity) =>
        Assert.ThrowsAny<XunitException>(() => AssertCovered(Identities().Append(addedIdentity)));

    [LocalPostgresMigrationFact]
    public async Task MigratedSchema_MatchesReviewedInventory()
    {
        var admin = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES")!;
        var settings = new NpgsqlConnectionStringBuilder(admin);
        Assert.Contains(settings.Host, new[] { "localhost", "127.0.0.1", "::1" });
        var name = "liner_phase8_inventory_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection))
            await create.ExecuteNonQueryAsync();
        settings.Database = name;
        settings.Pooling = false; // This fixture must not clear pools owned by concurrent test classes.
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(settings.ConnectionString).Options);
            await db.Database.MigrateAsync();
            await using var target = new NpgsqlConnection(settings.ConnectionString);
            await target.OpenAsync();
            await using var command = new NpgsqlCommand("""
                SELECT table_schema || '.' || table_name || '.' || column_name
                FROM information_schema.columns WHERE table_schema IN ('public', 'identity')
                """, target);
            await using var reader = await command.ExecuteReaderAsync();
            var actual = new List<string>();
            while (await reader.ReadAsync()) actual.Add(reader.GetString(0));
            AssertCovered(actual, history: true);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
