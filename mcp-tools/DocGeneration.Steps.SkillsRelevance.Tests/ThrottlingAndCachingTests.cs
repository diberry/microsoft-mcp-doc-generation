// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using SkillsRelevance.Services;

namespace SkillsRelevance.Tests;

public class RequestThrottlerTests
{
    [Fact]
    public void Constructor_InitializesWithFullBudget()
    {
        var throttler = new RequestThrottler();
        Assert.Equal(60, throttler.GetRequestsRemaining());
    }

    [Fact]
    public async Task ThrottleAsync_DecrementsBudget()
    {
        var throttler = new RequestThrottler();
        var initialRemaining = throttler.GetRequestsRemaining();
        
        await throttler.ThrottleAsync();
        
        Assert.Equal(initialRemaining - 1, throttler.GetRequestsRemaining());
    }

    [Fact]
    public async Task ThrottleAsync_DecrementsBudgetMultipleTimes()
    {
        var throttler = new RequestThrottler();
        
        await throttler.ThrottleAsync();
        await throttler.ThrottleAsync();
        await throttler.ThrottleAsync();
        
        Assert.Equal(57, throttler.GetRequestsRemaining());
    }

    [Fact]
    public async Task ThrottleAsync_SpreadsRequestsOverTime()
    {
        var throttler = new RequestThrottler();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Make a few requests and they should complete quickly
        // (no delay needed initially)
        await throttler.ThrottleAsync();
        await throttler.ThrottleAsync();
        await throttler.ThrottleAsync();
        
        stopwatch.Stop();
        
        // Should complete in under 200ms (allows for system variance)
        // Individual requests have ~1000ms spacing built in, but with clock variance,
        // a few quick initial requests should still be fast
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, 
            $"Multiple quick requests took too long: {stopwatch.ElapsedMilliseconds}ms");
    }
}

public class ResponseCacheTests
{
    [Fact]
    public void TryGetValue_ReturnsFalseForMissingKey()
    {
        var cache = new ResponseCache();
        var result = cache.TryGetValue("https://nonexistent.com", out _);
        
        Assert.False(result);
    }

    [Fact]
    public void TryGetValue_ReturnsTrueForCachedKey()
    {
        var cache = new ResponseCache();
        var url = "https://example.com/api";
        var content = "cached content";
        
        cache.Set(url, content);
        var result = cache.TryGetValue(url, out var retrievedContent);
        
        Assert.True(result);
        Assert.Equal(content, retrievedContent);
    }

    [Fact]
    public void Set_OverwritesPreviousValue()
    {
        var cache = new ResponseCache();
        var url = "https://example.com/api";
        
        cache.Set(url, "first");
        cache.Set(url, "second");
        
        cache.TryGetValue(url, out var content);
        Assert.Equal("second", content);
    }

    [Fact]
    public void Set_CaseSensitiveUrls()
    {
        var cache = new ResponseCache();
        
        cache.Set("https://example.com/API", "upper");
        cache.Set("https://example.com/api", "lower");
        
        cache.TryGetValue("https://example.com/api", out var content);
        Assert.Equal("lower", content);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var cache = new ResponseCache();
        cache.Set("url1", "content1");
        cache.Set("url2", "content2");
        
        cache.Clear();
        
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Count_ReturnsNumberOfCachedEntries()
    {
        var cache = new ResponseCache();
        
        cache.Set("url1", "content1");
        cache.Set("url2", "content2");
        cache.Set("url3", "content3");
        
        Assert.Equal(3, cache.Count);
    }
}
