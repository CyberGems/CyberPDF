using System.Collections.Concurrent;
using CyberPDF.Core.Diagnostics;
using CyberPDF.Core.Documents;
using CyberPDF.Core.Interop;
using CyberPDF.Core.Text;

namespace CyberPDF.Core.Rendering;

/// <summary>
/// The only legal entry point to PDFium. Every native call runs on one dedicated
/// thread, because the engine is not thread-safe: concurrent calls from two
/// threads crash inside PDFium with an illegal instruction, not a managed
/// exception. The UI talks to this class and never touches a handle.
/// </summary>
/// <remarks>
/// Ownership rule: a document is opened, rendered, measured and disposed here.
/// Callers receive it only to name it, and must hand it back through
/// <see cref="CloseDocument"/> instead of disposing it themselves.
/// </remarks>
public sealed class PdfEngine : IDisposable
{
    private readonly BlockingCollection<WorkItem> _work = new(new ConcurrentQueue<WorkItem>());
    private readonly Thread _thread;
    private readonly PageBitmapCache _cache;
    private volatile bool _disposed;

    public PdfEngine(long cacheBudgetBytes)
    {
        _cache = new PageBitmapCache(cacheBudgetBytes);
        _thread = new Thread(Pump)
        {
            IsBackground = true,
            Name = "CyberPDF.Pdfium",
            // Below normal so rendering never competes with the UI thread for a
            // core; the user waits on the first visible page, nothing else.
            Priority = ThreadPriority.BelowNormal,
        };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public PageBitmapCache Cache => _cache;

    /// <summary>True while the engine accepts work.</summary>
    public bool IsRunning => !_disposed;

    /// <summary>
    /// Opens a document on the PDFium thread. The returned document is owned by
    /// the engine; use <see cref="CloseDocument"/> when done with it.
    /// </summary>
    public void OpenDocument(string path, string? password, Action<PdfOpenResult> completed)
    {
        ArgumentNullException.ThrowIfNull(completed);
        Enqueue(new OpenWorkItem(path, password, completed));
    }

    /// <summary>
    /// Queues a page render. All callbacks run on the PDFium thread.
    /// Returns false when the engine is shutting down.
    /// </summary>
    /// <summary>
    /// Queues a page render. When <paramref name="tileOffsetY"/> is set the
    /// bitmap holds one horizontal band of a taller page: <paramref name="pixelHeight"/>
    /// is the band height and <paramref name="fullPixelHeight"/> the whole page,
    /// which is what PDFium needs to place the slice.
    /// </summary>
    public bool Render(
        PdfDocument document,
        int pageIndex,
        int pixelWidth,
        int pixelHeight,
        int rotation,
        Action<RenderResult> onSuccess,
        Action<Exception>? onError = null,
        int tileOffsetY = 0,
        int fullPixelHeight = 0)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Enqueue(new RenderWorkItem(
            document, pageIndex, pixelWidth, pixelHeight, rotation,
            onSuccess, onError, tileOffsetY, fullPixelHeight));
    }

    /// <summary>
    /// Extracts the text layer on the PDFium thread.
    /// </summary>
    public bool GetTextLayer(PdfDocument document, int pageIndex, Action<PageTextLayer> completed, Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Enqueue(new TextWorkItem(document, pageIndex, completed, onError));
    }

    /// <summary>
    /// Reads the document outline on the PDFium thread.
    /// </summary>
    public bool GetBookmarks(PdfDocument document, Action<IReadOnlyList<BookmarkEntry>> completed, Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Enqueue(new BookmarkWorkItem(document, completed, onError));
    }

    /// <summary>
    /// Reads link annotations for a single page on the PDFium thread.
    /// </summary>
    public bool GetLinks(PdfDocument document, int pageIndex, Action<PageLinkLayer> completed, Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Enqueue(new LinkWorkItem(document, pageIndex, completed, onError));
    }

    /// <summary>
    /// Reads document metadata fields on the PDFium thread.
    /// </summary>
    public bool GetMetadata(PdfDocument document, Action<PdfMetadata> completed, Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Enqueue(new MetadataWorkItem(document, completed, onError));
    }

    /// <summary>
    /// Destroys the document on the PDFium thread, after every request already
    /// queued for it. Closing from the UI thread would race the renderer.
    /// </summary>
    public void CloseDocument(PdfDocument document)
    {
        if (document is null)
        {
            return;
        }
        if (!Enqueue(new CloseWorkItem(document)))
        {
            // Engine already stopped: nothing else touches the document now.
            document.Dispose();
        }
    }

    /// <summary>
    /// Drops every cached band on the PDFium thread. A zoom, rotation or size
    /// change produces new cache keys for the same pages, so the previous
    /// generation is unreachable and would only be evicted by filling the whole
    /// byte budget first.
    /// </summary>
    public void DropCachedPages()
    {
        Enqueue(new ClearCacheWorkItem());
    }

    /// <summary>Drops queued work that has not started yet.</summary>
    public void DropPending()
    {
        while (_work.TryTake(out var item))
        {
            item.Cancel();
        }
    }

    private bool Enqueue(WorkItem item)
    {
        if (_disposed)
        {
            return false;
        }
        try
        {
            _work.Add(item);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void Pump()
    {
        foreach (var item in _work.GetConsumingEnumerable())
        {
            if (item.IsCanceled || _disposed)
            {
                item.Cancel();
                continue;
            }
            try
            {
                item.Run(this);
            }
            catch (Exception ex)
            {
                // A managed exception escaping here would kill the thread and
                // leave the UI waiting forever, so every failure is reported.
                AppLog.Error($"PDFium work item failed ({item.GetType().Name})", ex);
                item.Fail(ex);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _work.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(2));
        _work.Dispose();
        _cache.Dispose();
    }

    // --- Work items ---------------------------------------------------------

    private abstract class WorkItem
    {
        private int _settled;

        public bool IsCanceled { get; private set; }

        public abstract void Run(PdfEngine engine);

        public virtual void Fail(Exception error)
        {
        }

        public void Cancel()
        {
            IsCanceled = true;
            if (Interlocked.Exchange(ref _settled, 1) != 0)
            {
                return;
            }
            OnCanceled();
        }

        protected virtual void OnCanceled()
        {
        }

        protected bool TrySettle() => Interlocked.Exchange(ref _settled, 1) == 0;
    }

    private sealed class OpenWorkItem : WorkItem
    {
        private readonly string _path;
        private readonly string? _password;
        private readonly Action<PdfOpenResult> _completed;

        public OpenWorkItem(string path, string? password, Action<PdfOpenResult> completed)
        {
            _path = path;
            _password = password;
            _completed = completed;
        }

        public override void Run(PdfEngine engine)
        {
            var result = PdfDocument.Open(_path, _password);
            if (!TrySettle())
            {
                result.Document?.Dispose();
                return;
            }
            _completed(result);
        }

        public override void Fail(Exception error)
        {
            if (TrySettle())
            {
                _completed(PdfOpenResult.FromException(error));
            }
        }

        protected override void OnCanceled()
        {
            // Nothing was opened yet when the item is canceled.
        }
    }

    private sealed class RenderWorkItem : WorkItem
    {
        /// <summary>
        /// Guard rail against a zoom or crop combination that would ask for a
        /// bitmap larger than any sane texture or memory budget.
        /// </summary>
        private const int MaxDimension = 16384;

        private readonly PdfDocument _document;
        private readonly int _pageIndex;
        private readonly int _pixelWidth;
        private readonly int _pixelHeight;
        private readonly int _rotation;
        private readonly Action<RenderResult> _onSuccess;
        private readonly Action<Exception>? _onError;
        private readonly int _tileOffsetY;
        private readonly int _fullPixelHeight;

        public RenderWorkItem(
            PdfDocument document,
            int pageIndex,
            int pixelWidth,
            int pixelHeight,
            int rotation,
            Action<RenderResult> onSuccess,
            Action<Exception>? onError,
            int tileOffsetY,
            int fullPixelHeight)
        {
            _document = document;
            _pageIndex = pageIndex;
            _pixelWidth = pixelWidth;
            _pixelHeight = pixelHeight;
            _rotation = NormalizeRotation(rotation);
            _onSuccess = onSuccess;
            _onError = onError;
            _tileOffsetY = tileOffsetY;
            _fullPixelHeight = fullPixelHeight > 0 ? fullPixelHeight : pixelHeight;
        }

        private CacheKey Key => new(_pageIndex, _pixelWidth, _pixelHeight, _rotation, _tileOffsetY, _fullPixelHeight, _document.Id);

        public override void Run(PdfEngine engine)
        {
            if (_document.IsDisposed)
            {
                return;
            }

            var cache = engine._cache;
            if (cache.TryGet(Key, out var cached) && cached is not null)
            {
                if (TrySettle())
                {
                    _onSuccess(new RenderResult(cached, callerOwns: false));
                }
                return;
            }

            if (_pixelWidth <= 0 || _pixelHeight <= 0
                || _pixelWidth > MaxDimension || _pixelHeight > MaxDimension)
            {
                throw new InvalidOperationException(
                    $"Render size {_pixelWidth}x{_pixelHeight} is outside the supported range.");
            }

            var pageHandle = PdfiumNative.FPDF_LoadPage(_document.Handle, _pageIndex);
            if (pageHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Page {_pageIndex} could not be loaded.");
            }

            using var page = new SafePdfPage(pageHandle);

            // RenderedPage paints straight into the System.Drawing bitmap, so the
            // pixels exist exactly once. A separate native bitmap plus a copy
            // doubled the memory of every visible band.
            var rendered = RenderedPage.Render(
                pageHandle,
                _pixelWidth,
                _pixelHeight,
                _rotation,
                _tileOffsetY,
                _fullPixelHeight);

            if (IsCanceled)
            {
                rendered.Dispose();
                return;
            }

            // Pages larger than the whole budget are handed to the caller so the
            // visible page still shows; everything else stays cached.
            var stored = cache.Add(Key, rendered);
            if (TrySettle())
            {
                _onSuccess(new RenderResult(rendered, callerOwns: !stored));
            }
            else if (!stored)
            {
                rendered.Dispose();
            }
        }

        public override void Fail(Exception error) => _onError?.Invoke(error);

        private static int NormalizeRotation(int rotation)
        {
            var normalized = ((rotation % 360) + 360) % 360;
            return normalized % 90 == 0 ? normalized : 0;
        }
    }

    private sealed class TextWorkItem : WorkItem
    {
        private readonly PdfDocument _document;
        private readonly int _pageIndex;
        private readonly Action<PageTextLayer> _completed;
        private readonly Action<Exception>? _onError;

        public TextWorkItem(PdfDocument document, int pageIndex, Action<PageTextLayer> completed, Action<Exception>? onError)
        {
            _document = document;
            _pageIndex = pageIndex;
            _completed = completed;
            _onError = onError;
        }

        public override void Run(PdfEngine engine)
        {
            if (_document.IsDisposed)
            {
                return;
            }
            var layer = TextExtractor.Extract(_document, _pageIndex);
            if (TrySettle())
            {
                _completed(layer);
            }
        }

        public override void Fail(Exception error) => _onError?.Invoke(error);
    }

    private sealed class BookmarkWorkItem : WorkItem
    {
        private readonly PdfDocument _document;
        private readonly Action<IReadOnlyList<BookmarkEntry>> _completed;
        private readonly Action<Exception>? _onError;

        public BookmarkWorkItem(PdfDocument document, Action<IReadOnlyList<BookmarkEntry>> completed, Action<Exception>? onError)
        {
            _document = document;
            _completed = completed;
            _onError = onError;
        }

        public override void Run(PdfEngine engine)
        {
            if (_document.IsDisposed)
            {
                return;
            }
            var bookmarks = BookmarkReader.Read(_document);
            if (TrySettle())
            {
                _completed(bookmarks);
            }
        }

        public override void Fail(Exception error) => _onError?.Invoke(error);
    }

    private sealed class LinkWorkItem : WorkItem
    {
        private readonly PdfDocument _document;
        private readonly int _pageIndex;
        private readonly Action<PageLinkLayer> _completed;
        private readonly Action<Exception>? _onError;

        public LinkWorkItem(PdfDocument document, int pageIndex, Action<PageLinkLayer> completed, Action<Exception>? onError)
        {
            _document = document;
            _pageIndex = pageIndex;
            _completed = completed;
            _onError = onError;
        }

        public override void Run(PdfEngine engine)
        {
            if (_document.IsDisposed)
            {
                return;
            }
            var links = LinkReader.Read(_document, _pageIndex);
            if (TrySettle())
            {
                _completed(links);
            }
        }

        public override void Fail(Exception error) => _onError?.Invoke(error);
    }

    private sealed class MetadataWorkItem : WorkItem
    {
        private readonly PdfDocument _document;
        private readonly Action<PdfMetadata> _completed;
        private readonly Action<Exception>? _onError;

        public MetadataWorkItem(PdfDocument document, Action<PdfMetadata> completed, Action<Exception>? onError)
        {
            _document = document;
            _completed = completed;
            _onError = onError;
        }

        public override void Run(PdfEngine engine)
        {
            if (_document.IsDisposed)
            {
                return;
            }
            var metadata = MetadataReader.Read(_document);
            if (TrySettle())
            {
                _completed(metadata);
            }
        }

        public override void Fail(Exception error) => _onError?.Invoke(error);
    }

    private sealed class ClearCacheWorkItem : WorkItem
    {
        public override void Run(PdfEngine engine)
        {
            engine._cache.Clear();
            TrySettle();
        }
    }

    private sealed class CloseWorkItem : WorkItem
    {
        private readonly PdfDocument _document;

        public CloseWorkItem(PdfDocument document) => _document = document;

        public override void Run(PdfEngine engine)
        {
            engine._cache.RemovePagesOf(_document);
            _document.Dispose();
            TrySettle();
        }

        public override void Fail(Exception error) => _document.Dispose();

        protected override void OnCanceled() => _document.Dispose();
    }
}
