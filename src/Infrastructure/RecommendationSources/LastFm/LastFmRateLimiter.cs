using System.Diagnostics;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>
/// Thread-safe token bucket rate limiter designed to pace outbound requests to Last.fm.
/// Enforces a maximum requests-per-second rate with burst smoothing to comply with Last.fm API fair-use terms.
/// </summary>
public sealed class LastFmRateLimiter : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly double _refillRatePerMillisecond;
    private readonly double _capacity;
    private double _availableTokens;
    private long _lastRefillTimestamp;

    public LastFmRateLimiter(int requestsPerSecond = 4)
    {
        if (requestsPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestsPerSecond), "Rate limit must be greater than zero.");

        _capacity = requestsPerSecond;
        _availableTokens = requestsPerSecond;
        _refillRatePerMillisecond = (double)requestsPerSecond / 1000.0;
        _lastRefillTimestamp = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Waits asynchronously until a token is available to make a request.
    /// </summary>
    public async Task AcquireAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TimeSpan delayTime = TimeSpan.Zero;

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Refill();

                if (_availableTokens >= 1.0)
                {
                    _availableTokens -= 1.0;
                    return;
                }

                // Calculate required wait time for at least 1 token
                double needed = 1.0 - _availableTokens;
                double delayMs = Math.Ceiling(needed / _refillRatePerMillisecond);
                delayTime = TimeSpan.FromMilliseconds(Math.Max(1, delayMs));
            }
            finally
            {
                _semaphore.Release();
            }

            if (delayTime > TimeSpan.Zero)
            {
                await Task.Delay(delayTime, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void Refill()
    {
        long currentTimestamp = Stopwatch.GetTimestamp();
        double elapsedMs = (double)(currentTimestamp - _lastRefillTimestamp) * 1000.0 / Stopwatch.Frequency;

        if (elapsedMs > 0)
        {
            _availableTokens = Math.Min(_capacity, _availableTokens + (elapsedMs * _refillRatePerMillisecond));
            _lastRefillTimestamp = currentTimestamp;
        }
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
