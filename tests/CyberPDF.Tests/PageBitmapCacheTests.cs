using CyberPDF.Core.Rendering;
using Xunit;

namespace CyberPDF.Tests;

public sealed class PageBitmapCacheTests
{
    [Fact]
    public void AddThenTryGetReturnsTheSamePage()
    {
        using var cache = new PageBitmapCache(1024 * 1024);
        var page = TestPage.Create(100, 100);
        var key = new CacheKey(0, 100, 100, 0);

        Assert.True(cache.Add(key, page));
        Assert.True(cache.TryGet(key, out var fetched));
        Assert.Same(page, fetched);
    }

    [Fact]
    public void UnknownKeyReturnsFalse()
    {
        using var cache = new PageBitmapCache(1024 * 1024);
        Assert.False(cache.TryGet(new CacheKey(5, 10, 10, 0), out var page));
        Assert.Null(page);
    }

    [Fact]
    public void DifferentZoomIsADifferentEntry()
    {
        using var cache = new PageBitmapCache(1024 * 1024);
        var key100 = new CacheKey(0, 100, 100, 0);
        var key200 = new CacheKey(0, 200, 200, 0);

        cache.Add(key100, TestPage.Create(100, 100));
        cache.Add(key200, TestPage.Create(200, 200));

        Assert.True(cache.TryGet(key100, out var first));
        Assert.True(cache.TryGet(key200, out var second));
        Assert.NotSame(first, second);
    }

    [Fact]
    public void DifferentRotationIsADifferentEntry()
    {
        using var cache = new PageBitmapCache(1024 * 1024);
        var upright = new CacheKey(0, 100, 100, 0);
        var rotated = new CacheKey(0, 100, 100, 90);

        cache.Add(upright, TestPage.Create(100, 100));
        cache.Add(rotated, TestPage.Create(100, 100));

        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void DifferentTileOffsetIsADifferentEntry()
    {
        using var cache = new PageBitmapCache(1024 * 1024);
        var band0 = new CacheKey(0, 100, 50, 0, OffsetY: 0, FullPixelHeight: 200);
        var band1 = new CacheKey(0, 100, 50, 0, OffsetY: 50, FullPixelHeight: 200);

        cache.Add(band0, TestPage.Create(100, 50));
        cache.Add(band1, TestPage.Create(100, 50));

        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void LeastRecentlyUsedEntryIsEvictedFirst()
    {
        // Budget fits two 100x100 pages of 40 KB each, so a third forces an eviction.
        using var cache = new PageBitmapCache(100_000);
        var keyA = new CacheKey(0, 100, 100, 0);
        var keyB = new CacheKey(1, 100, 100, 0);
        var keyC = new CacheKey(2, 100, 100, 0);

        cache.Add(keyA, TestPage.Create(100, 100));
        cache.Add(keyB, TestPage.Create(100, 100));

        // Touch A so B becomes the least recently used.
        Assert.True(cache.TryGet(keyA, out _));

        cache.Add(keyC, TestPage.Create(100, 100));

        Assert.True(cache.TryGet(keyA, out _));
        Assert.False(cache.TryGet(keyB, out _));
        Assert.True(cache.TryGet(keyC, out _));
    }

    [Fact]
    public void PageLargerThanTheWholeBudgetIsNotCached()
    {
        using var cache = new PageBitmapCache(64 * 1024);
        var huge = TestPage.Create(200, 200);

        var stored = cache.Add(new CacheKey(0, 200, 200, 0), huge);

        Assert.False(stored);
        Assert.Equal(0, cache.Count);
        // The caller keeps ownership of a page the cache refused.
        huge.Dispose();
    }

    [Fact]
    public void AddingTheSameKeyReplacesAndReleasesTheOldPage()
    {
        using var cache = new PageBitmapCache(1024 * 1024);
        var key = new CacheKey(0, 100, 100, 0);
        var first = TestPage.Create(100, 100);
        var second = TestPage.Create(100, 100);

        cache.Add(key, first);
        cache.Add(key, second);

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet(key, out var current));
        Assert.Same(second, current);
    }

    [Fact]
    public void ClearReleasesEverything()
    {
        using var cache = new PageBitmapCache(1024 * 1024);
        cache.Add(new CacheKey(0, 100, 100, 0), TestPage.Create(100, 100));
        cache.Add(new CacheKey(1, 100, 100, 0), TestPage.Create(100, 100));

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.CurrentBytes);
    }

    [Fact]
    public void ByteBudgetIsRespected()
    {
        const long budget = 200_000;
        using var cache = new PageBitmapCache(budget);
        for (var i = 0; i < 20; i++)
        {
            cache.Add(new CacheKey(i, 100, 100, 0), TestPage.Create(100, 100));
        }

        Assert.True(cache.CurrentBytes <= budget, $"Cache exceeded budget: {cache.CurrentBytes} > {budget}");
    }

    [Fact]
    public void NonPositiveBudgetIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageBitmapCache(0));
    }
}
