namespace CyberPDF.Core.Rendering;

/// <summary>
/// Fixed-capacity LRU cache of rendered pages, bounded by total byte size
/// rather than page count: at high zoom a single page can be tens of megabytes,
/// so a count-based limit would either waste memory or evict too early.
/// </summary>
public sealed class PageBitmapCache : IDisposable
{
    private readonly long _budgetBytes;
    private readonly Dictionary<CacheKey, LinkedListNode<Entry>> _map = new();
    private readonly LinkedList<Entry> _lru = new();
    private readonly object _lock = new();
    private long _currentBytes;
    private bool _disposed;

    public PageBitmapCache(long budgetBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(budgetBytes);
        _budgetBytes = budgetBytes;
    }

    public long BudgetBytes => _budgetBytes;

    public long CurrentBytes
    {
        get { lock (_lock) { return _currentBytes; } }
    }

    public int Count
    {
        get { lock (_lock) { return _map.Count; } }
    }

    /// <summary>
    /// Returns a cached page, moving it to the front of the recency list. The
    /// caller must not dispose the returned page: the cache owns it.
    /// </summary>
    public bool TryGet(in CacheKey key, out RenderedPage? page)
    {
        lock (_lock)
        {
            if (!_map.TryGetValue(key, out var node))
            {
                page = null;
                return false;
            }
            _lru.Remove(node);
            _lru.AddFirst(node);
            page = node.Value.Page;
            return true;
        }
    }

    /// <summary>
    /// Adds a page, evicting least recently used entries until it fits. Returns
    /// false when a single page is larger than the whole budget: it is disposed
    /// instead of cached, so a huge render never evicts everything else.
    /// </summary>
    public bool Add(in CacheKey key, RenderedPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_map.TryGetValue(key, out var existing))
            {
                _lru.Remove(existing);
                _map.Remove(key);
                _currentBytes -= existing.Value.Page.ByteSize;
                existing.Value.Page.Dispose();
            }

            var added = page.ByteSize;
            if (added > _budgetBytes)
            {
                page.Dispose();
                return false;
            }

            while (_currentBytes + added > _budgetBytes && _lru.Last is { } last)
            {
                _lru.RemoveLast();
                _map.Remove(last.Value.Key);
                _currentBytes -= last.Value.Page.ByteSize;
                last.Value.Page.Dispose();
            }

            var node = new LinkedListNode<Entry>(new Entry(key, page));
            _lru.AddFirst(node);
            _map[key] = node;
            _currentBytes += added;
            return true;
        }
    }

    /// <summary>Drops entries for one page across every scale and rotation.</summary>
    public void RemovePage(int pageIndex)
    {
        lock (_lock)
        {
            var doomed = _map.Where(kv => kv.Key.PageIndex == pageIndex).Select(kv => kv.Key).ToList();
            foreach (var key in doomed)
            {
                var node = _map[key];
                _lru.Remove(node);
                _map.Remove(key);
                _currentBytes -= node.Value.Page.ByteSize;
                node.Value.Page.Dispose();
            }
        }
    }

    /// <summary>Drops every cached page belonging to a document being closed.</summary>
    public void RemovePagesOf(Documents.PdfDocument document)
    {
        // Cache keys are page indices, not document identities: a document swap
        // has to drop everything or page 3 of one file would serve page 3 of the
        // next. That is cheap and always correct.
        Clear();
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (var entry in _lru)
            {
                entry.Page.Dispose();
            }
            _lru.Clear();
            _map.Clear();
            _currentBytes = 0;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Clear();
        }
    }

    private readonly record struct Entry(CacheKey Key, RenderedPage Page);
}

/// <summary>
/// Identifies a rendered page: the same page at a different zoom or rotation is
/// a different entry. Pixel size is included so a device-pixel-ratio change
/// cannot return a stale bitmap.
/// </summary>
public readonly record struct CacheKey(
    int PageIndex,
    int PixelWidth,
    int PixelHeight,
    int Rotation,
    int OffsetY = 0,
    int FullPixelHeight = 0);
