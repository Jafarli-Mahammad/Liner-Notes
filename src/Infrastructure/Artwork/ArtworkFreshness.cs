namespace LinerNotes.Infrastructure.Artwork;

internal static class ArtworkFreshness
{
    public static TimeSpan Remaining(HttpResponseMessage response, DateTimeOffset now, TimeSpan maximum)
    {
        var control = response.Headers.CacheControl;
        var lifetime = control?.SharedMaxAge ?? control?.MaxAge;
        if (lifetime is null && response.Content.Headers.Expires is { } expires)
            lifetime = expires - (response.Headers.Date ?? now);
        // This local fallback is not an origin lifetime to which Age can be applied.
        if (lifetime is null) return maximum;
        var apparentAge = now - (response.Headers.Date ?? now);
        var age = response.Headers.Age ?? TimeSpan.Zero;
        if (apparentAge > age) age = apparentAge;
        var remaining = lifetime.Value - age;
        return remaining < maximum ? remaining : maximum;
    }
}
