// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;

namespace SkillsRelevance.Services;

/// <summary>
/// Throttles GitHub API requests to respect unauthenticated rate limits (60 requests per hour).
/// Tracks request count and enforces delays to spread requests evenly across the time window.
/// </summary>
public class RequestThrottler
{
    private const int UnauthenticatedRateLimit = 60; // requests per hour
    private const int TimeWindowSeconds = 3600; // 1 hour in seconds
    
    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly double _minimumDelayMilliseconds;

    public RequestThrottler()
    {
        // Calculate minimum delay between requests to stay within 60 req/hr
        // With throttling, we can space requests to ~1 per minute (60 seconds)
        _minimumDelayMilliseconds = (double)TimeWindowSeconds * 1000 / UnauthenticatedRateLimit;
    }

    /// <summary>
    /// Gets the number of requests remaining in the current time window.
    /// </summary>
    public int GetRequestsRemaining()
    {
        CleanupOldRequests();
        return Math.Max(0, UnauthenticatedRateLimit - _requestTimestamps.Count);
    }

    /// <summary>
    /// Records a request and waits if necessary to maintain rate limits.
    /// </summary>
    public async Task ThrottleAsync()
    {
        CleanupOldRequests();

        if (_requestTimestamps.Count >= UnauthenticatedRateLimit)
        {
            // We'"'"'ve hit the limit; calculate how long to wait
            var oldestRequest = _requestTimestamps.Peek();
            var timeSinceOldest = DateTime.UtcNow - oldestRequest;
            var timeToWait = TimeSpan.FromSeconds(TimeWindowSeconds) - timeSinceOldest;

            if (timeToWait.TotalMilliseconds > 0)
            {
                LogFileHelper.WriteDebug($"Rate limit approaching; waiting {timeToWait.TotalSeconds:F1}s before next request");
                await Task.Delay(timeToWait);
                CleanupOldRequests();
            }
        }

        // Even if we'"'"'re within limits, add a small delay to distribute requests evenly
        if (_requestTimestamps.Count > 0)
        {
            var lastRequest = _requestTimestamps.Last();
            var timeSinceLast = (DateTime.UtcNow - lastRequest).TotalMilliseconds;
            var delayNeeded = _minimumDelayMilliseconds - timeSinceLast;

            if (delayNeeded > 0)
            {
                await Task.Delay((int)Math.Ceiling(delayNeeded));
            }
        }

        _requestTimestamps.Enqueue(DateTime.UtcNow);
    }

    private void CleanupOldRequests()
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-TimeWindowSeconds);
        while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < cutoff)
        {
            _requestTimestamps.Dequeue();
        }
    }
}
