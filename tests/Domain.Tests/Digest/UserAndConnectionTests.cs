using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using Xunit;

namespace LinerNotes.Domain.Tests.Digest;

public sealed class UserAndConnectionTests
{
    [Fact]
    public void User_Create_ShouldNormalizeEmailAndSetDefaults()
    {
        var user = User.Create("  User@Example.COM  ");

        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("UTC", user.TimeZone);
        Assert.Equal(DigestDeliveryDay.Sunday, user.DeliveryDay);
        Assert.Equal(8, user.DeliveryHourUtc);
        Assert.False(user.IsDeleted);
        Assert.Empty(user.Connections);
        Assert.Empty(user.TasteSignals);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void User_Create_EmptyEmail_ShouldThrowArgumentException(string? invalidEmail)
    {
        Assert.Throws<ArgumentException>(() => new User(invalidEmail!));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    [InlineData(100)]
    public void User_Create_InvalidDeliveryHour_ShouldThrowArgumentOutOfRangeException(int hour)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new User("user@test.com", deliveryHourUtc: hour));
    }

    [Fact]
    public void User_UpdateSchedule_ValidInputs_ShouldUpdateProperties()
    {
        var user = User.Create("user@test.com");

        user.UpdateSchedule(DigestDeliveryDay.Friday, 18, "Europe/London");

        Assert.Equal(DigestDeliveryDay.Friday, user.DeliveryDay);
        Assert.Equal(18, user.DeliveryHourUtc);
        Assert.Equal("Europe/London", user.TimeZone);
        Assert.NotNull(user.LastModifiedAt);
    }

    [Fact]
    public void User_AddConnection_DuplicateService_ShouldThrowInvalidOperationException()
    {
        var user = User.Create("user@test.com");
        var conn1 = UserMusicConnection.Create(user.Id, MusicServiceType.LastFm, "lastfm_user1");
        var conn2 = UserMusicConnection.Create(user.Id, MusicServiceType.LastFm, "lastfm_user2");

        user.AddConnection(conn1);
        Assert.Single(user.Connections);

        Assert.Throws<InvalidOperationException>(() => user.AddConnection(conn2));
    }

    [Fact]
    public void User_AddTasteSignal_ShouldAddSignalToList()
    {
        var user = User.Create("user@test.com");
        var signal = TasteSignal.CreateSeedTag(user.Id, "shoegaze", 1.0, "Test seed");

        user.AddTasteSignal(signal);

        Assert.Single(user.TasteSignals);
        Assert.Contains(signal, user.TasteSignals);
    }

    [Fact]
    public void User_SoftDelete_ShouldSetIsDeleted()
    {
        var user = User.Create("user@test.com");
        user.SoftDelete();

        Assert.True(user.IsDeleted);
        Assert.NotNull(user.LastModifiedAt);
    }

    [Fact]
    public void UserMusicConnection_StateTransitions_WorkCorrectly()
    {
        var userId = Guid.NewGuid();
        var conn = UserMusicConnection.Create(userId, MusicServiceType.ListenBrainz, "lb_user", "enc_token_1");

        Assert.True(conn.IsActive);
        Assert.Equal("enc_token_1", conn.EncryptedToken);
        Assert.Null(conn.LastSyncedAt);

        var syncTime = DateTime.UtcNow;
        conn.MarkSynced(syncTime);
        Assert.Equal(syncTime, conn.LastSyncedAt);

        conn.Deactivate();
        Assert.False(conn.IsActive);

        conn.Activate();
        Assert.True(conn.IsActive);

        conn.UpdateToken("new_token");
        Assert.Equal("new_token", conn.EncryptedToken);
    }
}
