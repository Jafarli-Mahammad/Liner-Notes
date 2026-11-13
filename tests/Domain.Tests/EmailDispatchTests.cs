using LinerNotes.Domain.Email;
using Xunit;
namespace LinerNotes.Domain.Tests;

public sealed class EmailDispatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
    private static EmailDispatch Create() => new(Guid.NewGuid(), null, EmailPurpose.Confirmation, "confirmation:fixture", new('a', 64), new('b', 64), "protected", Now, Now.AddDays(1));
    [Fact]
    public void RetryPreservesIdentityAndAmbiguousExpiryCannotResend()
    {
        var row = Create(); var id = row.Id;
        Assert.True(row.BeginAttempt(Now)); row.Retry(Now, Now.AddMinutes(1), "transport_unknown");
        Assert.True(row.BeginAttempt(Now.AddMinutes(2))); Assert.Equal(id, row.Id); Assert.Equal("protected", row.ProtectedPayload);
        Assert.False(row.BeginAttempt(Now.AddHours(23))); Assert.Equal(EmailState.Ambiguous, row.State); Assert.Null(row.ProtectedPayload);
        Assert.False(row.BeginAttempt(Now.AddDays(2)));
    }
    [Fact]
    public void UnattemptedExpiredSecurityMessageIsCancelled()
    { var row = Create(); Assert.False(row.BeginAttempt(Now.AddDays(1))); Assert.Equal(EmailState.Cancelled, row.State); }
    [Fact]
    public void DailyAttemptCountResetsAtUtcMidnightWithoutResettingLifetimeRetries()
    {
        var row = Create();
        Assert.True(row.BeginAttempt(Now));
        row.Retry(Now, Now.AddMinutes(1), "retry");
        Assert.True(row.BeginAttempt(Now.AddMinutes(2)));
        row.Retry(Now.AddMinutes(2), Now.AddDays(1), "retry");
        Assert.True(row.BeginAttempt(Now.AddDays(1)));
        Assert.Equal(3, row.Attempts);
        Assert.Equal(1, row.AttemptsToday);
    }
    [Fact]
    public void AcceptanceIsNotDeliveryAndEventsCannotDowngradeComplaint()
    {
        var row = Create(); row.BeginAttempt(Now); row.Accept("fixture-message", Now);
        Assert.Equal(EmailState.Accepted, row.State); Assert.Null(row.DeliveredAt); Assert.Null(row.ProtectedPayload);
        row.ApplyEvent("delivered", Now.AddMinutes(1)); Assert.Equal(EmailState.Delivered, row.State);
        row.ApplyEvent("complained", Now.AddMinutes(2)); row.ApplyEvent("bounced", Now); row.ApplyEvent("delivered", Now);
        Assert.Equal(EmailState.Complained, row.State); Assert.False(row.BeginAttempt(Now.AddMinutes(3)));
    }
    [Fact]
    public void DeliveryEvidenceCanResolveAnEarlierFailure()
    {
        var row = Create(); row.BeginAttempt(Now); row.Accept("fixture-message", Now);
        row.ApplyEvent("failed", Now.AddMinutes(1)); Assert.Equal(EmailState.Failed, row.State);
        row.ApplyEvent("delivered", Now.AddMinutes(2)); Assert.Equal(EmailState.Delivered, row.State);
        Assert.NotNull(row.DeliveredAt);
    }
    [Fact]
    public void PendingDelayAndInvalidPurposeCannotBypassValidation()
    {
        var row = Create(); row.BeginAttempt(Now); row.Retry(Now, Now.AddMinutes(1), "transport_retry");
        Assert.False(row.BeginAttempt(Now));
        Assert.Throws<ArgumentException>(() => new EmailDispatch(Guid.NewGuid(), null, EmailPurpose.WeeklyDigest, "weekly", new('a', 64), new('b', 64), "protected", Now, Now.AddDays(1)));
    }
}
