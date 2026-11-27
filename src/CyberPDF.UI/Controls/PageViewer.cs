using System.Drawing.Imaging;
using CyberPDF.Common.I18n;
using CyberPDF.Common.Settings;
using CyberPDF.Core.Documents;
using CyberPDF.Core.Rendering;
using CyberPDF.Core.Search;
using CyberPDF.Core.Text;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

public enum ViewerTool
{
    Select,
    Pan,
}

/// <summary>
/// The document surface: a continuous vertical column of pages that scrolls
/// smoothly and renders only what is visible.
/// </summary>
/// <remarks>
/// <para>
/// Renders are split into horizontal bands of at most <see cref="TileHeight"/>
/// pixels. A poster-sized page six screens tall then costs six small bitmaps
/// instead of one 45 MB buffer, and only the bands inside the viewport are
/// requested. PDFium never blocks the UI thread: the control paints what is
/// cached and repaints when a band arrives.
/// </para>
/// <para>
/// Every coordinate in this control is a device pixel, including the layout and
/// the scroll offset. The zoom factor is still logical (1.0 means 100%), but it
/// is multiplied by the DPI scale before the layout is built. Mixing logical
/// units with device pixels makes GDI+ resample each band on every paint, which
/// costs time and memory and softens the text.
/// </para>
/// </remarks>
public sealed class PageViewer : Control
{
    /// <summary>
    /// Maximum height of one rendered band, in device pixels. Roughly a
    /// viewport tall at 100%: big enough that a normal page is one band, small
    /// enough that a huge page never allocates tens of megabytes at once.
    /// </summary>
    private const int TileHeight = 1280;

    private readonly PdfEngine _engine;
    private readonly Dictionary<CacheKey, RenderedPage> _tiles = new();
    private readonly HashSet<CacheKey> _inFlight = new();
    private readonly HashSet<int> _failedPages = new();

    /// <summary>
    /// Text layers, fetched lazily on first use per page. Selection is an
    /// occasional action, so paying for text extraction on every render would
    /// slow the common case down for no benefit.
    /// </summary>
    private readonly Dictionary<int, PageTextLayer> _textLayers = new();
    private readonly Dictionary<int, List<Action>> _textInFlight = new();
    private readonly Dictionary<int, PageLinkLayer> _linkLayers = new();
    private readonly HashSet<int> _linksInFlight = new();
    private PdfLink? _hoveredLink;

    /// <summary>
    /// Pages already searched for the current query. Text extraction can serve
    /// the same request twice (a queued continuation plus the retry that runs
    /// when a layer lands), so this keeps the work and the result list at one
    /// pass per page.
    /// </summary>
    private readonly HashSet<int> _searchedPages = new();
    private readonly Dictionary<int, List<PdfRect>> _highlightCache = new();
    private readonly List<TextHighlight> _userHighlights = [];
    private readonly HashSet<int> _highlightedPages = [];
    private readonly Dictionary<string, List<PdfRect>> _userHighlightRectCache = new(StringComparer.Ordinal);

    private PdfDocument? _document;
    private DocumentLayout? _layout;
    private float _zoom = 1f;
    private int _rotation;
    private int _scrollX;
    private int _scrollY;
    private Point _mouseDownPoint;
    private Point _lastDragPoint;
    private Point _panStartPoint;
    private bool _panning;
    private bool _panMoved;
    private int _panStartScrollX;
    private int _panStartScrollY;
    private bool _selecting;
    private bool _selectionMoved;
    private int _selectionAnchorPage;
    private int _selectionAnchorChar = -1;
    private int _selectionSequence;
    private long _lastClickTick;
    private Point _lastClickPoint;
    private int _clickCount;
    private int _lastDpi = -1;
    private ViewerTool _activeTool = ViewerTool.Pan;
    private PageLayoutMode _layoutMode = PageLayoutMode.Continuous;
    private bool _darkPageMode;
    private ImageAttributes? _darkImageAttributes;

    /// <summary>
    /// Prepared search text per page, so a live search while typing does not
    /// rebuild the string-and-index map on every keystroke.
    /// </summary>
    private readonly Dictionary<int, SearchableText> _searchTexts = new();
    private readonly List<SearchMatch> _matches = new();
    private int _activeMatch = -1;
    private string _searchTerm = string.Empty;
    private SearchOptions _searchOptions = new();

    private DocumentSelection? _documentSelection;
    private readonly System.Windows.Forms.Timer _dragScrollTimer = new() { Interval = 35 };

    public PageViewer(PdfEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint
            | ControlStyles.Selectable,
            true);
        BackColor = Theme.Canvas;
        TabStop = true;
        _dragScrollTimer.Tick += OnDragScrollTick;
    }

    /// <summary>Raised when the visible page or zoom changes, for the status bar.</summary>
    public event EventHandler? ViewStateChanged;

    /// <summary>Raised on Ctrl+Wheel so the host window updates zoom cleanly.</summary>
    public event EventHandler<float>? ZoomRequested;

    /// <summary>Raised when the pointer hovers or leaves a PDF link.</summary>
    public event EventHandler<string?>? LinkHovered;

    /// <summary>Raised when an internal page link is activated.</summary>
    public event EventHandler<int>? LinkPageRequested;

    public PdfDocument? Document => _document;

    public ViewerTool ActiveTool
    {
        get => _activeTool;
        set
        {
            if (_activeTool == value)
            {
                return;
            }
            _activeTool = value;
            UpdateHoverCursor(PointToClient(Cursor.Position));
        }
    }

    public float Zoom => _zoom;

    public int Rotation => _rotation;

    public PageLayoutMode LayoutMode => _layoutMode;

    public bool DarkPageMode => _darkPageMode;

    public int ScrollX => _scrollX;

    public int ScrollY => _scrollY;

    public int ContentHeight => _layout?.ContentHeight ?? 0;

    public int ContentWidth => _layout?.ContentWidth ?? 0;

    /// <summary>Computed page slots, exposed for diagnostics and hit testing.</summary>
    public IReadOnlyList<PageSlot> LayoutSlots => _layout?.Slots ?? (IReadOnlyList<PageSlot>)Array.Empty<PageSlot>();

    public int ViewportHeight => ClientSize.Height;

    /// <summary>Cached text layers, for diagnostics.</summary>
    internal int DebugTextLayerCount => _textLayers.Count;

    /// <summary>Number of rendered bands currently referenced by this control.</summary>
    public int TileCount => _tiles.Count;

    /// <summary>
    /// Total pixel bytes of the bands this control references. Computed from the
    /// cache keys instead of reading each Bitmap: holding a disposed bitmap in a
    /// stale entry must not be able to throw from a diagnostics property.
    /// </summary>
    public long TileBytes => _tiles.Keys.Sum(k => (long)k.PixelWidth * k.PixelHeight * 4);

    /// <summary>Bands the engine cache holds, for diagnostics.</summary>
    public long CacheBytes => _engine.Cache.CurrentBytes;

    /// <summary>Bands the engine cache holds, for diagnostics.</summary>
    public int CacheCount => _engine.Cache.Count;

    public int FirstVisiblePage => _layout is null || _layout.Slots.Count == 0
        ? 0
        : _layout.FirstVisiblePage(_scrollY);

    public void SetDocument(PdfDocument? document, bool dropCache = true)
    {
        _document = document;
        ResetTiles();
        if (dropCache)
        {
            _engine.DropCachedPages();
        }
        _textLayers.Clear();
        _textInFlight.Clear();
        _selectionSequence++;
        _linkLayers.Clear();
        _linksInFlight.Clear();
        _hoveredLink = null;
        LinkHovered?.Invoke(this, null);
        _highlightCache.Clear();
        _userHighlightRectCache.Clear();
        _searchTexts.Clear();
        _searchedPages.Clear();
        _searchTerm = string.Empty;
        _matches.Clear();
        _activeMatch = -1;
        _documentSelection = null;
        _dragScrollTimer.Stop();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        _layout = document is null ? null : DocumentLayout.ForDocument(document, LayoutZoom, GapForZoom(), 0, _rotation, _layoutMode);
        _scrollX = 0;
        _scrollY = 0;
        Invalidate();
        RequestVisibleRenders();
        ViewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RestoreSessionDocument(
        PdfDocument document,
        float zoom,
        int rotation,
        int scrollX,
        int scrollY,
        PageLayoutMode layoutMode,
        bool darkPageMode)
    {
        _document = document;
        ResetTiles();
        _textLayers.Clear();
        _textInFlight.Clear();
        _selectionSequence++;
        _linkLayers.Clear();
        _linksInFlight.Clear();
        _hoveredLink = null;
        LinkHovered?.Invoke(this, null);
        _highlightCache.Clear();
        _userHighlightRectCache.Clear();
        _searchTexts.Clear();
        _searchedPages.Clear();
        _searchTerm = string.Empty;
        _matches.Clear();
        _activeMatch = -1;
        _documentSelection = null;
        _dragScrollTimer.Stop();
        SelectionChanged?.Invoke(this, EventArgs.Empty);

        _zoom = Math.Clamp(zoom, 0.1f, 8f);
        _rotation = ((rotation % 360) + 360) % 360;
        _layoutMode = layoutMode;
        _darkPageMode = darkPageMode;
        _layout = DocumentLayout.ForDocument(document, LayoutZoom, GapForZoom(), 0, _rotation, _layoutMode);
        _scrollX = Math.Clamp(scrollX, 0, Math.Max(0, _layout.ContentWidth - ClientSize.Width));
        _scrollY = Math.Clamp(scrollY, 0, Math.Max(0, _layout.ContentHeight - ClientSize.Height));
        Invalidate();
        RequestVisibleRenders();
        ViewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetLayoutMode(PageLayoutMode mode)
    {
        if (_layoutMode == mode)
        {
            return;
        }
        var anchorPage = FirstVisiblePage;
        _layoutMode = mode;
        RebuildLayout(anchorPage, 0);
        ViewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetDarkPageMode(bool enabled)
    {
        if (_darkPageMode == enabled)
        {
            return;
        }
        _darkPageMode = enabled;
        Invalidate();
        ViewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetZoom(float zoom, bool keepCenter = true)
    {
        var clamped = Math.Clamp(zoom, 0.1f, 8f);
        if (Math.Abs(clamped - _zoom) < 0.001f)
        {
            return;
        }

        var anchorPage = FirstVisiblePage;
        var anchorOffset = 0;
        if (_layout is { Slots.Count: > 0 })
        {
            anchorOffset = _scrollY - _layout.Slots[anchorPage].Top;
        }

        _zoom = clamped;
        RebuildLayout(keepCenter ? anchorPage : null, anchorOffset);
        ViewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Rebuilds the layout at the current zoom, keeping the visible page. Used
    /// when the client width changes without a zoom change, which happens when
    /// the side panel opens or closes.
    /// </summary>
    public void RefreshLayout()
    {
        if (_document is null)
        {
            return;
        }
        var anchorPage = FirstVisiblePage;
        var anchorOffset = 0;
        if (_layout is { Slots.Count: > 0 })
        {
            anchorOffset = _scrollY - _layout.Slots[anchorPage].Top;
        }
        RebuildLayout(anchorPage, anchorOffset);
        ViewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetRotation(int degrees)
    {
        var normalized = ((degrees % 360) + 360) % 360;
        if (_rotation == normalized)
        {
            return;
        }
        _rotation = normalized;
        RebuildLayout(null, 0);
        ViewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ScrollTo(int value)
    {
        var clamped = Math.Clamp(value, 0, Math.Max(0, ContentHeight - ClientSize.Height));
        if (clamped == _scrollY)
        {
            return;
        }
        _scrollY = clamped;
        TrimDistantTiles();
        Invalidate();
        RequestVisibleRenders();
        ViewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ScrollHorizontalTo(int value)
    {
        var clamped = Math.Clamp(value, 0, Math.Max(0, ContentWidth - ClientSize.Width));
        if (clamped == _scrollX)
        {
            return;
        }
        _scrollX = clamped;
        Invalidate();
        ViewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ScrollHorizontalBy(int delta) => ScrollHorizontalTo(_scrollX + delta);

    public void ScrollToPage(int pageIndex)
    {
        if (_layout is null || pageIndex < 0 || pageIndex >= _layout.Slots.Count)
        {
            return;
        }
        ScrollTo(_layout.Slots[pageIndex].Top);
    }

    public void ScrollBy(int delta) => ScrollTo(_scrollY + delta);

    /// <summary>Raised when the selection changes, so the window can enable Copy.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Current selection on its start page, or null when nothing is selected.</summary>
    public TextSelection? Selection
    {
        get
        {
            if (_documentSelection is not { IsEmpty: false } docSel
                || !_textLayers.TryGetValue(docSel.StartPage, out var layer))
            {
                return null;
            }
            var slice = docSel.ForPage(docSel.StartPage, layer.Characters.Count);
            return slice.IsEmpty ? null : slice;
        }
    }

    /// <summary>Returns every non-empty page slice spanned by the current selection.</summary>
    public IReadOnlyList<TextSelection> GetSelectionRanges()
    {
        if (_documentSelection is not { IsEmpty: false } docSel)
        {
            return Array.Empty<TextSelection>();
        }
        return TextSelectionEngine.SliceByPage(docSel, page => _textLayers.GetValueOrDefault(page));
    }

    /// <summary>True when there is text that can be copied.</summary>
    public bool HasSelection => _documentSelection is { IsEmpty: false };

    /// <summary>
    /// Text of the current selection across all spanned pages, empty when
    /// nothing is selected. Reads cached layers, so it never blocks on PDFium.
    /// </summary>
    public string GetSelectedText()
    {
        if (_documentSelection is not { IsEmpty: false } docSel)
        {
            return string.Empty;
        }
        return TextSelectionEngine.BuildMultiPageText(docSel, page => _textLayers.GetValueOrDefault(page));
    }

    public void ClearSelection()
    {
        if (_documentSelection is null)
        {
            return;
        }
        _documentSelection = null;
        _dragScrollTimer.Stop();
        _highlightCache.Clear();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Replaces the persisted text highlights rendered over the document.</summary>
    public void SetHighlights(IReadOnlyList<TextHighlight>? highlights)
    {
        _userHighlights.Clear();
        _highlightedPages.Clear();
        _userHighlightRectCache.Clear();

        if (highlights is not null)
        {
            foreach (var highlight in highlights)
            {
                if (highlight is null || highlight.PageIndex < 0 || highlight.Start < 0 || highlight.Length <= 0)
                {
                    continue;
                }
                _userHighlights.Add(highlight);
                _highlightedPages.Add(highlight.PageIndex);
            }
        }

        if (_document is not null && _layout is not null && _highlightedPages.Count > 0)
        {
            var top = Math.Max(0, _scrollY - ClientSize.Height);
            var bottom = _scrollY + ClientSize.Height * 2;
            foreach (var slot in _layout.VisiblePages(top, bottom))
            {
                if (_highlightedPages.Contains(slot.PageIndex) && !_textLayers.ContainsKey(slot.PageIndex))
                {
                    EnsureTextLayer(slot.PageIndex, Invalidate);
                }
            }
        }

        Invalidate();
    }

    /// <summary>
    /// Returns the topmost user highlight at <paramref name="clientPoint"/>, or
    /// <see langword="null"/> when no highlight is hit.
    /// </summary>
    public TextHighlight? HitTestHighlight(Point clientPoint)
    {
        if (_userHighlights.Count == 0 || HitTestPdfPoint(clientPoint) is not var (pageIndex, pdfX, pdfY))
        {
            return null;
        }
        if (!_highlightedPages.Contains(pageIndex) || !_textLayers.TryGetValue(pageIndex, out var layer))
        {
            return null;
        }

        const double tolerance = 1.5;
        for (var i = _userHighlights.Count - 1; i >= 0; i--)
        {
            var highlight = _userHighlights[i];
            if (highlight.PageIndex != pageIndex)
            {
                continue;
            }

            foreach (var rect in GetUserHighlightRects(highlight, layer))
            {
                if (pdfX >= rect.Left - tolerance && pdfX <= rect.Right + tolerance &&
                    pdfY >= rect.Bottom - tolerance && pdfY <= rect.Top + tolerance)
                {
                    return highlight;
                }
            }
        }

        return null;
    }

    /// <summary>Current search term, empty when no search is active.</summary>
    public string SearchTerm => _searchTerm;

    /// <summary>Number of matches found, zero when nothing is active.</summary>
    public int MatchCount => _matches.Count;

    /// <summary>One-based index of the active match, or zero when there is none.</summary>
    public int ActiveMatchNumber => _activeMatch < 0 ? 0 : _activeMatch + 1;

    /// <summary>Raised when search results change, so the window can update its bar.</summary>
    public event EventHandler? SearchChanged;

    /// <summary>
    /// Runs a search over the whole document and highlights every match.
    /// </summary>
    /// <remarks>
    /// Text extraction is asynchronous, so this walks the pages one at a time
    /// and reports progress through <see cref="SearchChanged"/>. Pages already
    /// cached are searched immediately; the rest arrive as their text layer
    /// finishes, which is what makes the count climb instead of freezing.
    /// </remarks>
    public void Search(string term, SearchOptions? options = null)
    {
        _searchTerm = term ?? string.Empty;
        _searchOptions = options ?? new SearchOptions();
        _matches.Clear();
        _activeMatch = -1;
        _searchTexts.Clear();
        _searchedPages.Clear();
        InvalidateOverlays();
        Invalidate();

        if (_document is null || string.IsNullOrWhiteSpace(_searchTerm))
        {
            SearchChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        for (var page = 0; page < _document.PageCount; page++)
        {
            var pageIndex = page;
            EnsureTextLayer(pageIndex, () => SearchPage(pageIndex));
        }

        SearchChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Drops overlay geometry. Rectangles are derived from the selection and the
    /// active search, so any change to either has to discard them or the previous
    /// result stays on screen.
    /// </summary>
    private void InvalidateOverlays() => _highlightCache.Clear();

    private void SearchPage(int pageIndex)
    {
        if (_document is null || _searchTerm.Length == 0)
        {
            return;
        }
        if (!_textLayers.TryGetValue(pageIndex, out var layer))
        {
            return;
        }
        if (!_searchedPages.Add(pageIndex))
        {
            return;
        }

        if (!_searchTexts.TryGetValue(pageIndex, out var searchable))
        {
            searchable = SearchableText.FromLayer(layer);
            _searchTexts[pageIndex] = searchable;
        }

        var found = SearchEngine.FindInSearchableText(searchable, pageIndex, _searchTerm, _searchOptions);

        // Results stay ordered by page, so a late page appends in place.
        _matches.RemoveAll(m => m.PageIndex == pageIndex);
        _matches.AddRange(found);
        _matches.Sort((a, b) => a.PageIndex != b.PageIndex
            ? a.PageIndex.CompareTo(b.PageIndex)
            : a.Start.CompareTo(b.Start));

        if (_activeMatch < 0 && _matches.Count > 0)
        {
            _activeMatch = 0;
        }

        InvalidateOverlays();
        Invalidate();
        SearchChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves to the next match, wrapping at the end.</summary>
    public void GoToNextMatch()
    {
        if (_matches.Count == 0)
        {
            return;
        }
        _activeMatch = (_activeMatch + 1) % _matches.Count;
        InvalidateOverlays();
        RevealActiveMatch();
    }

    /// <summary>Moves to the previous match, wrapping at the start.</summary>
    public void GoToPreviousMatch()
    {
        if (_matches.Count == 0)
        {
            return;
        }
        _activeMatch = (_activeMatch - 1 + _matches.Count) % _matches.Count;
        InvalidateOverlays();
        RevealActiveMatch();
    }

    /// <summary>Clears the search and its highlights.</summary>
    public void ClearSearch()
    {
        if (_searchTerm.Length == 0 && _matches.Count == 0)
        {
            return;
        }
        _searchTerm = string.Empty;
        _matches.Clear();
        _activeMatch = -1;
        _searchTexts.Clear();
        _searchedPages.Clear();
        InvalidateOverlays();
        Invalidate();
        SearchChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Scrolls the active match into view, keeping a third of a screen of context
    /// above it so the user can read around the hit.
    /// </summary>
    private void RevealActiveMatch()
    {
        if (_activeMatch < 0 || _activeMatch >= _matches.Count || _layout is null || _document is null)
        {
            return;
        }

        var match = _matches[_activeMatch];
        if (match.PageIndex >= _layout.Slots.Count)
        {
            return;
        }

        var slot = _layout.Slots[match.PageIndex];
        var context = Math.Max(40, ClientSize.Height / 3);
        var matchOffsetY = 0;
        var matchCenterX = slot.Width / 2;

        if (_textLayers.TryGetValue(match.PageIndex, out var layer))
        {
            var rects = TextSelectionEngine.BuildHighlightRects(
                layer,
                new TextSelection(match.PageIndex, match.Start, match.Length));
            if (rects.Count > 0)
            {
                var size = _document.GetPageSize(match.PageIndex);
                var transform = new PageTransform(size, slot.Width, slot.Height, _rotation);
                var cornerA = transform.PdfToView(rects[0].Left, rects[0].Top);
                var cornerB = transform.PdfToView(rects[0].Right, rects[0].Bottom);
                matchOffsetY = (int)Math.Floor(Math.Min(cornerA.Y, cornerB.Y));
                matchCenterX = (int)Math.Round((cornerA.X + cornerB.X) * 0.5);
            }
        }

        ScrollTo(slot.Top + matchOffsetY - context);
        if (ContentWidth > ClientSize.Width)
        {
            var pageLeft = _layout.CenteredLeft(slot, ClientSize.Width);
            ScrollHorizontalTo(pageLeft + matchCenterX - ClientSize.Width / 2);
        }
        Invalidate();
        SearchChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Selects everything on the page under the viewport top.</summary>
    public void SelectAllOnCurrentPage()
    {
        if (_document is null || _layout is null || _layout.Slots.Count == 0)
        {
            return;
        }

        var pageIndex = Math.Clamp(FirstVisiblePage, 0, _document.PageCount - 1);
        EnsureTextLayer(pageIndex, () =>
        {
            if (!_textLayers.TryGetValue(pageIndex, out var layer) || layer.IsEmpty)
            {
                return;
            }
            SetSelection(new TextSelection(pageIndex, 0, layer.Characters.Count));
        });
    }

    private float DpiScale => DeviceDpi / 96f;

    /// <summary>
    /// Zoom as the layout sees it: logical zoom times the DPI scale, so a page
    /// rasterized at device resolution occupies exactly its pixel count.
    /// </summary>
    private float LayoutZoom => Math.Max(0.01f, _zoom * DpiScale);

    private void ResetTiles()
    {
        foreach (var page in _tiles.Values)
        {
            page.Dispose();
        }
        _tiles.Clear();
        _inFlight.Clear();
        _failedPages.Clear();
    }

    /// <summary>
    /// Fetches the text layer of a page once, then runs the continuation with it
    /// available. Extraction happens on the PDFium thread like everything else.
    /// </summary>
    private void EnsureTextLayer(int pageIndex, Action continuation)
    {
        if (_document is null)
        {
            return;
        }
        if (_textLayers.ContainsKey(pageIndex))
        {
            continuation();
            return;
        }
        if (_textInFlight.TryGetValue(pageIndex, out var waiters))
        {
            waiters.Add(continuation);
            return;
        }

        var doc = _document;
        var queue = new List<Action> { continuation };
        _textInFlight[pageIndex] = queue;
        _engine.GetTextLayer(
            doc,
            pageIndex,
            layer =>
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    return;
                }
                try
                {
                    BeginInvoke(() =>
                    {
                        if (doc != _document)
                        {
                            return;
                        }

                        if (_textInFlight.Remove(pageIndex, out var pending))
                        {
                            _textLayers[pageIndex] = layer;
                            foreach (var callback in pending)
                            {
                                callback();
                            }
                        }
                        else
                        {
                            _textLayers[pageIndex] = layer;
                        }

                        // A newer query may have arrived while this page was
                        // extracting; its request was dropped as a duplicate,
                        // so run it now or that page stays out of the results.
                        if (_searchTerm.Length > 0)
                        {
                            SearchPage(pageIndex);
                        }
                    });
                }
                catch (InvalidOperationException)
                {
                    // The window closed while extracting.
                }
            },
            _ =>
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    return;
                }
                try
                {
                    BeginInvoke(() =>
                    {
                        if (doc == _document)
                        {
                            _textInFlight.Remove(pageIndex);
                        }
                    });
                }
                catch (InvalidOperationException)
                {
                }
            });
    }

    private void SetSelection(TextSelection selection)
    {
        if (selection.IsEmpty)
        {
            ClearSelection();
            return;
        }
        SetDocumentSelection(DocumentSelection.Create(selection.PageIndex, selection.Start, selection.PageIndex, selection.End - 1));
    }

    private void SetDocumentSelection(DocumentSelection selection)
    {
        if (selection.IsEmpty)
        {
            ClearSelection();
            return;
        }

        _documentSelection = selection;
        _highlightCache.Clear();
        for (var page = selection.StartPage; page <= selection.EndPage; page++)
        {
            if (!_textLayers.ContainsKey(page))
            {
                EnsureTextLayer(page, Invalidate);
            }
        }
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Maps a control-space point to a page and PDF coordinates, or null when the
    /// point is outside every page.
    /// </summary>
    private (int PageIndex, double PdfX, double PdfY)? HitTestPdfPoint(Point location)
    {
        if (_document is null || _layout is null)
        {
            return null;
        }

        var contentY = location.Y + _scrollY;
        var singlePageIndex = _layoutMode == PageLayoutMode.SinglePage ? FirstVisiblePage : -1;
        foreach (var slot in _layout.VisiblePages(contentY, contentY, includeMargin: false))
        {
            if (singlePageIndex >= 0 && slot.PageIndex != singlePageIndex)
            {
                continue;
            }
            var left = _layout.CenteredLeft(slot, ClientSize.Width) - _scrollX;
            if (location.X < left || location.X > left + slot.Width)
            {
                continue;
            }
            var top = slot.Top - _scrollY;
            if (location.Y < top || location.Y > top + slot.Height)
            {
                continue;
            }

            var size = _document.GetPageSize(slot.PageIndex);
            var transform = new PageTransform(size, slot.Width, slot.Height, _rotation);
            var (pdfX, pdfY) = transform.ViewToPdf(location.X - left, location.Y - top);
            return (slot.PageIndex, pdfX, pdfY);
        }

        return null;
    }

    /// <summary>
    /// Forgets rendered bands that are far outside the viewport. The engine cache
    /// still holds them within its budget; this only stops the viewer from
    /// pinning bitmaps for pages the user has scrolled away from. Without it the
    /// viewer grew without bound on a long document.
    /// </summary>
    private void TrimDistantTiles()
    {
        if (_layout is null || _document is null)
        {
            return;
        }

        var margin = ClientSize.Height * 3;
        var top = _scrollY - margin;
        var bottom = _scrollY + ClientSize.Height + margin;

        var keep = new HashSet<int>();
        foreach (var slot in _layout.VisiblePages(Math.Max(0, top), bottom))
        {
            keep.Add(slot.PageIndex);
        }

        var stale = _tiles.Keys.Where(k => !keep.Contains(k.PageIndex)).ToList();
        foreach (var key in stale)
        {
            if (_tiles.Remove(key, out var evicted))
            {
                evicted.Dispose();
            }
        }
    }

    private void RebuildLayout(int? anchorPage, int anchorOffset)
    {
        ResetTiles();

        // Every band is keyed by pixel size, so a rebuild makes the previous
        // generation unreachable. Leaving it to the byte budget would keep
        // ~100 MB of dead bands resident after a couple of zoom changes.
        _engine.DropCachedPages();

        if (_document is null)
        {
            _layout = null;
            _scrollX = 0;
            _scrollY = 0;
            Invalidate();
            return;
        }

        _layout = DocumentLayout.ForDocument(_document, LayoutZoom, GapForZoom(), 0, _rotation, _layoutMode);
        _scrollX = Math.Clamp(_scrollX, 0, Math.Max(0, _layout.ContentWidth - ClientSize.Width));

        if (anchorPage is int page && page < _layout.Slots.Count)
        {
            _scrollY = _layout.Slots[page].Top + anchorOffset;
        }
        else
        {
            _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, _layout.ContentHeight - ClientSize.Height));
        }

        Invalidate();
        RequestVisibleRenders();
    }

    private int GapForZoom() => Math.Max(8, (int)Math.Round(12 * LayoutZoom));

    /// <summary>
    /// Queues every band that intersects the viewport plus one viewport of
    /// margin, so a fast wheel finds the next band already painted.
    /// </summary>
    private void RequestVisibleRenders()
    {
        if (_document is null || _layout is null || !_engine.IsRunning)
        {
            return;
        }

        _lastDpi = DeviceDpi;
        var margin = ClientSize.Height;
        var top = Math.Max(0, _scrollY - margin);
        var bottom = _scrollY + ClientSize.Height + margin;

        foreach (var slot in _layout.VisiblePages(top, bottom))
        {
            EnsureLinkLayer(slot.PageIndex);
            if (_highlightedPages.Contains(slot.PageIndex) && !_textLayers.ContainsKey(slot.PageIndex))
            {
                EnsureTextLayer(slot.PageIndex, Invalidate);
            }
            if (_failedPages.Contains(slot.PageIndex))
            {
                continue;
            }

            // Slots are already device pixels, so the raster matches them 1:1.
            var pageWidth = Math.Max(1, slot.Width);
            var pageHeight = Math.Max(1, slot.Height);

            // Bands are relative to the page, not to the viewport, so scrolling
            // reuses them instead of re-rendering the same content shifted.
            for (var bandTop = 0; bandTop < pageHeight; bandTop += TileHeight)
            {
                var bandHeight = Math.Min(TileHeight, pageHeight - bandTop);
                var key = new CacheKey(slot.PageIndex, pageWidth, bandHeight, _rotation, bandTop, pageHeight, _document.Id);
                if (_tiles.ContainsKey(key) || _inFlight.Contains(key))
                {
                    continue;
                }

                _inFlight.Add(key);
                var captured = key;
                _engine.Render(
                    _document,
                    slot.PageIndex,
                    pageWidth,
                    bandHeight,
                    _rotation,
                    result => OnTileRendered(captured, result),
                    error => OnTileFailed(captured, error),
                    tileOffsetY: bandTop,
                    fullPixelHeight: pageHeight);
            }
        }
    }

    private void OnTileRendered(CacheKey key, RenderResult result)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            if (result.CallerOwns)
            {
                result.Page.Dispose();
            }
            return;
        }

        try
        {
            BeginInvoke(() =>
            {
                var adopted = false;
                try
                {
                    if (IsDisposed)
                    {
                        return;
                    }

                    // A render can finish after a zoom, rotation or resize has
                    // rebuilt the layout. Its key no longer describes the current
                    // geometry, so reject it and release our ref below.
                    if (!IsCurrentKey(key))
                    {
                        return;
                    }

                    // Each render callback holds its own reference-counted
                    // RenderedPage (sharing the single underlying Bitmap without
                    // copying), so adopting it keeps pixels valid even if the
                    // engine cache later evicts its copy.
                    if (_tiles.Remove(key, out var old))
                    {
                        old.Dispose();
                    }

                    _tiles[key] = result.Page;
                    adopted = true;
                    _inFlight.Remove(key);
                    Invalidate();
                }
                finally
                {
                    if (!adopted && result.CallerOwns)
                    {
                        result.Page.Dispose();
                    }
                }
            });
        }
        catch (InvalidOperationException)
        {
            // The handle went away between the check and the marshal.
            if (result.CallerOwns)
            {
                result.Page.Dispose();
            }
        }
    }

    /// <summary>
    /// True when the key still matches the current layout geometry, meaning the
    /// band belongs to the generation the viewer is drawing right now.
    /// </summary>
    private bool IsCurrentKey(CacheKey key)
    {
        if (_document is null || _layout is null || key.PageIndex < 0 || key.PageIndex >= _layout.Slots.Count)
        {
            return false;
        }

        var slot = _layout.Slots[key.PageIndex];
        return key.DocumentId == _document.Id
            && key.PixelWidth == slot.Width
            && key.FullPixelHeight == slot.Height
            && key.Rotation == _rotation;
    }

    private void OnTileFailed(CacheKey key, Exception error)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }
        try
        {
            BeginInvoke(() =>
            {
                _inFlight.Remove(key);
                _failedPages.Add(key.PageIndex);
                Invalidate();
            });
        }
        catch (InvalidOperationException)
        {
            // Window already gone.
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RequestVisibleRenders();
    }

    /// <summary>
    /// Moving to a monitor with another scale invalidates every band, since they
    /// were rasterized for the previous DPI.
    /// </summary>
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        if (_lastDpi != -1 && _lastDpi != DeviceDpi)
        {
            ResetTiles();
            Invalidate();
            RequestVisibleRenders();
        }
    }

    /// <summary>
    /// Paints the document surface into an arbitrary Graphics. OnPaint delegates
    /// here, so a screenshot or a test exercises exactly the same code the user
    /// sees instead of a parallel implementation.
    /// </summary>
    public void PaintSurface(Graphics graphics, Size viewport)
    {
        ThemePainter.PrepareHighQuality(graphics);
        graphics.Clear(Theme.Canvas);

        if (_layout is null || _document is null)
        {
            using var emptyFont = new Font(Font.FontFamily, 11f, FontStyle.Regular);
            var hint = Strings.Get("DropHint");
            var bounds = new Rectangle(0, viewport.Height / 2 - 20, viewport.Width, 40);
            ThemePainter.DrawCenteredText(graphics, hint, emptyFont, Theme.TextMuted, bounds);
            return;
        }

        var visible = _layout.VisiblePages(_scrollY, _scrollY + viewport.Height, includeMargin: false);
        if (_layoutMode == PageLayoutMode.SinglePage && visible.Count > 0)
        {
            var active = FirstVisiblePage;
            visible = visible.Where(s => s.PageIndex == active).ToList();
        }

        foreach (var slot in visible)
        {
            // Center against the viewport, not the content: fit-width makes both
            // equal, which would pin every page to the left edge.
            var left = _layout.CenteredLeft(slot, viewport.Width) - _scrollX;
            var top = slot.Top - _scrollY;
            var pageRect = new Rectangle(left, top, slot.Width, slot.Height);

            // Paper, border and soft drop shadow: a band that has not arrived yet
            // still reads as a page rather than a hole in the document.
            var shadowRect = new Rectangle(left + 2, top + 2, slot.Width, slot.Height);
            using (var shadow = new SolidBrush(Theme.PageShadow))
            {
                graphics.FillRectangle(shadow, shadowRect);
            }
            using (var paper = new SolidBrush(_darkPageMode ? Theme.DarkPaper : Theme.Paper))
            {
                graphics.FillRectangle(paper, pageRect);
            }
            using (var pageBorder = new Pen(Theme.PageBorder))
            {
                graphics.DrawRectangle(pageBorder, left - 1, top - 1, slot.Width + 1, slot.Height + 1);
            }

            var attrs = _darkPageMode ? GetDarkImageAttributes() : null;
            for (var bandTop = 0; bandTop < slot.Height; bandTop += TileHeight)
            {
                var bandHeight = Math.Min(TileHeight, slot.Height - bandTop);
                var key = new CacheKey(slot.PageIndex, slot.Width, bandHeight, _rotation, bandTop, slot.Height, _document.Id);
                if (!_tiles.TryGetValue(key, out var tile) || tile.IsDisposed)
                {
                    continue;
                }

                var bitmap = tile.Bitmap;
                // 1:1 in device pixels: the bitmap was rasterized at exactly this
                // size, so GDI+ does not resample and keeps no scaled copy.
                var destination = new Rectangle(left, top + bandTop, slot.Width, bandHeight);
                if (attrs is not null)
                {
                    graphics.DrawImage(bitmap, destination, 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attrs);
                }
                else
                {
                    graphics.DrawImage(bitmap, destination, 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel);
                }
            }

            // Overlays belong to the real paint path, not only to screenshots.
            // User highlights sit underneath search hits and active selection.
            DrawUserHighlights(graphics, slot, left, top);
            DrawSearchHighlights(graphics, slot, left, top);
            DrawSelection(graphics, slot, left, top);
        }
    }

    private void DrawUserHighlights(Graphics graphics, PageSlot slot, int pageLeft, int pageTop)
    {
        if (_document is null || !_highlightedPages.Contains(slot.PageIndex))
        {
            return;
        }
        if (!_textLayers.TryGetValue(slot.PageIndex, out var layer))
        {
            EnsureTextLayer(slot.PageIndex, Invalidate);
            return;
        }

        var size = _document.GetPageSize(slot.PageIndex);
        var transform = new PageTransform(size, slot.Width, slot.Height, _rotation);
        var brushes = new Dictionary<string, SolidBrush>(StringComparer.Ordinal);
        try
        {
            foreach (var highlight in _userHighlights)
            {
                if (highlight.PageIndex != slot.PageIndex)
                {
                    continue;
                }

                var key = HighlightColors.Normalize(highlight.Color);
                if (!brushes.TryGetValue(key, out var brush))
                {
                    brush = new SolidBrush(Theme.HighlightColor(key));
                    brushes[key] = brush;
                }

                FillPdfRects(graphics, transform, brush, GetUserHighlightRects(highlight, layer), pageLeft, pageTop);
            }
        }
        finally
        {
            foreach (var brush in brushes.Values)
            {
                brush.Dispose();
            }
        }
    }

    private List<PdfRect> GetUserHighlightRects(TextHighlight highlight, PageTextLayer layer)
    {
        var cacheKey = $"{highlight.PageIndex}:{highlight.Start}:{highlight.Length}";
        if (_userHighlightRectCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var rects = TextSelectionEngine.BuildHighlightRects(
            layer,
            new TextSelection(highlight.PageIndex, highlight.Start, highlight.Length));
        _userHighlightRectCache[cacheKey] = rects;
        return rects;
    }

    private static void FillPdfRects(
        Graphics graphics,
        PageTransform transform,
        Brush brush,
        IEnumerable<PdfRect> rects,
        int pageLeft,
        int pageTop)
    {
        foreach (var rect in rects)
        {
            // Same corner mapping as the selection: a rotation swaps which
            // corner ends up top-left, so min and max are what keep the fill
            // on the glyphs.
            var cornerA = transform.PdfToView(rect.Left, rect.Top);
            var cornerB = transform.PdfToView(rect.Right, rect.Bottom);
            var x = pageLeft + (int)Math.Floor(Math.Min(cornerA.X, cornerB.X));
            var y = pageTop + (int)Math.Floor(Math.Min(cornerA.Y, cornerB.Y));
            var width = Math.Max(1, (int)Math.Ceiling(Math.Abs(cornerB.X - cornerA.X)));
            var height = Math.Max(1, (int)Math.Ceiling(Math.Abs(cornerB.Y - cornerA.Y)));
            graphics.FillRectangle(brush, x, y, width, height);
        }
    }

    /// <summary>
    /// Paints every match on a page, with the active one in a stronger tone. Hits
    /// come from the Core already merged per line, so a page with many matches
    /// still costs a handful of fills.
    /// </summary>
    private void DrawSearchHighlights(Graphics graphics, PageSlot slot, int pageLeft, int pageTop)
    {
        if (_matches.Count == 0 || _document is null)
        {
            return;
        }
        if (!_textLayers.TryGetValue(slot.PageIndex, out var layer))
        {
            return;
        }

        var indices = new List<int>();
        for (var i = 0; i < _matches.Count; i++)
        {
            if (_matches[i].PageIndex == slot.PageIndex)
            {
                indices.Add(i);
            }
        }
        if (indices.Count == 0)
        {
            return;
        }

        var size = _document.GetPageSize(slot.PageIndex);
        var transform = new PageTransform(size, slot.Width, slot.Height, _rotation);

        using var normal = new SolidBrush(Theme.SearchMatch);
        using var active = new SolidBrush(Theme.SearchMatchActive);

        foreach (var index in indices)
        {
            var match = _matches[index];
            var brush = index == _activeMatch ? active : normal;
            var rects = TextSelectionEngine.BuildHighlightRects(
                layer,
                new TextSelection(slot.PageIndex, match.Start, match.Length));
            FillPdfRects(graphics, transform, brush, rects, pageLeft, pageTop);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        PaintSurface(e.Graphics, ClientSize);
    }

    /// <summary>
    /// Paints the selection for one page. Rectangles come from the Core already
    /// merged per line, so a paragraph costs a handful of fills instead of one
    /// per character, which is what keeps selection smooth at high zoom.
    /// </summary>
    private void DrawSelection(Graphics graphics, PageSlot slot, int pageLeft, int pageTop)
    {
        if (_documentSelection is not { IsEmpty: false } docSel || !docSel.ContainsPage(slot.PageIndex) || _document is null)
        {
            return;
        }
        if (!_textLayers.TryGetValue(slot.PageIndex, out var layer))
        {
            EnsureTextLayer(slot.PageIndex, Invalidate);
            return;
        }

        var selection = docSel.ForPage(slot.PageIndex, layer.Characters.Count);
        if (selection.IsEmpty)
        {
            return;
        }

        if (!_highlightCache.TryGetValue(slot.PageIndex, out var rects))
        {
            rects = TextSelectionEngine.BuildHighlightRects(layer, selection);
            _highlightCache[slot.PageIndex] = rects;
        }

        var size = _document.GetPageSize(slot.PageIndex);
        var transform = new PageTransform(size, slot.Width, slot.Height, _rotation);

        using var brush = new SolidBrush(Theme.PageSelection);
        foreach (var rect in rects)
        {
            // Map opposite corners and normalize: a rotated page swaps which one
            // ends up top-left, so taking min and max is what keeps the
            // highlight on the glyphs at every rotation.
            var cornerA = transform.PdfToView(rect.Left, rect.Top);
            var cornerB = transform.PdfToView(rect.Right, rect.Bottom);
            var x = pageLeft + (int)Math.Floor(Math.Min(cornerA.X, cornerB.X));
            var y = pageTop + (int)Math.Floor(Math.Min(cornerA.Y, cornerB.Y));
            var width = Math.Max(1, (int)Math.Ceiling(Math.Abs(cornerB.X - cornerA.X)));
            var height = Math.Max(1, (int)Math.Ceiling(Math.Abs(cornerB.Y - cornerA.Y)));
            graphics.FillRectangle(brush, x, y, width, height);
        }
    }

    // --- Input --------------------------------------------------------------

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (_document is not null && (ModifierKeys & Keys.Control) != 0)
        {
            if (e.Delta != 0)
            {
                var factor = e.Delta > 0 ? 1.15f : 1f / 1.15f;
                ZoomRequested?.Invoke(this, factor);
            }
        }
        else if (_document is not null && (ModifierKeys & Keys.Shift) != 0)
        {
            if (e.Delta != 0)
            {
                var step = Math.Max(32, ClientSize.Width / 8);
                ScrollHorizontalBy(-Math.Sign(e.Delta) * step);
            }
        }
        else
        {
            ScrollByWheel(e.Delta);
        }

        // Marked handled: without it WinForms bubbles the same wheel event to the
        // parent form, which would scroll a second time.
        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }
    }

    /// <summary>
    /// Scrolls by one wheel notch, in the direction of <paramref name="delta"/>.
    /// Exposed because the window forwards wheel events it receives when the
    /// focus is on a button instead of the page.
    /// </summary>
    public void ScrollByWheel(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        // A high-resolution wheel sends deltas smaller than 120. Scaling by the
        // fraction keeps touchpad scrolling proportional instead of ignoring it.
        var notches = delta / 120f;
        var lines = SystemInformation.MouseWheelScrollLines;
        var step = lines > 0 ? (int)(Font.Height * 3.2f * lines) : ClientSize.Height / 3;
        var amount = (int)Math.Round(-notches * step);
        if (amount == 0)
        {
            amount = -Math.Sign(delta);
        }
        ScrollBy(amount);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (_document is null || _layout is null)
        {
            return;
        }

        if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && _activeTool == ViewerTool.Pan))
        {
            _panning = true;
            _panMoved = false;
            _panStartPoint = e.Location;
            _panStartScrollX = _scrollX;
            _panStartScrollY = _scrollY;
            Capture = true;
            Cursor = Cursors.SizeAll;
            return;
        }

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var now = Environment.TickCount64;
        var withinDoubleClick = now - _lastClickTick <= SystemInformation.DoubleClickTime
            && Math.Abs(e.X - _lastClickPoint.X) <= SystemInformation.DoubleClickSize.Width
            && Math.Abs(e.Y - _lastClickPoint.Y) <= SystemInformation.DoubleClickSize.Height;
        _clickCount = withinDoubleClick ? _clickCount + 1 : 1;
        _lastClickTick = now;
        _lastClickPoint = e.Location;

        var hit = HitTestPdfPoint(e.Location);
        if (hit is not var (pageIndex, pdfX, pdfY))
        {
            _selectionSequence++;
            ClearSelection();
            return;
        }

        var seq = ++_selectionSequence;

        if (_clickCount == 2)
        {
            EnsureTextLayer(pageIndex, () =>
            {
                if (seq != _selectionSequence)
                {
                    return;
                }
                if (_textLayers.TryGetValue(pageIndex, out var layer))
                {
                    var index = TextSelectionEngine.ClosestCharacterIndex(layer, pdfX, pdfY, maxDistance: 24);
                    if (index >= 0)
                    {
                        SetSelection(TextSelectionEngine.SelectWordAt(layer, index, pageIndex));
                    }
                    else
                    {
                        ClearSelection();
                    }
                }
            });
            return;
        }

        if (_clickCount >= 3)
        {
            EnsureTextLayer(pageIndex, () =>
            {
                if (seq != _selectionSequence)
                {
                    return;
                }
                if (_textLayers.TryGetValue(pageIndex, out var layer))
                {
                    var index = TextSelectionEngine.ClosestCharacterIndex(layer, pdfX, pdfY, maxDistance: 24);
                    if (index >= 0)
                    {
                        SetSelection(TextSelectionEngine.SelectLineAt(layer, index, pageIndex));
                    }
                    else
                    {
                        ClearSelection();
                    }
                }
            });
            return;
        }

        _selecting = true;
        _selectionMoved = false;
        _selectionAnchorPage = pageIndex;
        _selectionAnchorChar = -1;
        _mouseDownPoint = e.Location;
        _lastDragPoint = e.Location;
        Capture = true;

        EnsureTextLayer(pageIndex, () =>
        {
            if (seq != _selectionSequence)
            {
                return;
            }
            if (_textLayers.TryGetValue(pageIndex, out var layer))
            {
                _selectionAnchorChar = TextSelectionEngine.ClosestCharacterIndex(layer, pdfX, pdfY);
                if (_selectionMoved && _selectionAnchorChar >= 0)
                {
                    UpdateDragSelection(_selecting ? PointToClient(Cursor.Position) : _lastDragPoint);
                }
            }
        });
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_panning)
        {
            if (!_panMoved
                && (Math.Abs(e.X - _panStartPoint.X) >= 3 || Math.Abs(e.Y - _panStartPoint.Y) >= 3))
            {
                _panMoved = true;
            }
            ScrollTo(_panStartScrollY + (_panStartPoint.Y - e.Y));
            ScrollHorizontalTo(_panStartScrollX + (_panStartPoint.X - e.X));
            return;
        }

        if (_selecting)
        {
            _lastDragPoint = e.Location;
            if (!_selectionMoved
                && (Math.Abs(e.X - _mouseDownPoint.X) >= 3 || Math.Abs(e.Y - _mouseDownPoint.Y) >= 3))
            {
                _selectionMoved = true;
            }
            if (_selectionMoved && _selectionAnchorChar >= 0 && _document is not null && _layout is not null)
            {
                UpdateDragSelection(e.Location);
            }
            if (e.Y < DragScrollEdgePixels || e.Y > ClientSize.Height - DragScrollEdgePixels)
            {
                if (!_dragScrollTimer.Enabled)
                {
                    _dragScrollTimer.Start();
                }
            }
            else if (_dragScrollTimer.Enabled)
            {
                _dragScrollTimer.Stop();
            }
            return;
        }

        UpdateHoverCursor(e.Location);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_panning && e.Button is MouseButtons.Middle or MouseButtons.Left)
        {
            var clickedWithoutPan = e.Button == MouseButtons.Left && !_panMoved;
            _panning = false;
            Capture = false;
            if (clickedWithoutPan)
            {
                TryActivateLink(e.Location);
            }
            UpdateHoverCursor(e.Location);
            return;
        }

        if (_selecting && e.Button == MouseButtons.Left)
        {
            _lastDragPoint = e.Location;
            _dragScrollTimer.Stop();
            var moved = _selectionMoved;
            _selecting = false;
            Capture = false;
            if (!moved && _clickCount == 1)
            {
                _selectionSequence++;
                ClearSelection();
                if (TryActivateLink(e.Location))
                {
                    UpdateHoverCursor(e.Location);
                    return;
                }
            }
            UpdateHoverCursor(e.Location);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_panning && !_selecting)
        {
            Cursor = Cursors.Default;
        }
        SetHoveredLink(null);
    }

    private void EnsureLinkLayer(int pageIndex)
    {
        if (_document is null || _linkLayers.ContainsKey(pageIndex) || _linksInFlight.Contains(pageIndex))
        {
            return;
        }

        var doc = _document;
        _linksInFlight.Add(pageIndex);
        _engine.GetLinks(
            doc,
            pageIndex,
            layer =>
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    return;
                }
                try
                {
                    BeginInvoke(() =>
                    {
                        _linksInFlight.Remove(pageIndex);
                        if (doc == _document)
                        {
                            _linkLayers[pageIndex] = layer;
                        }
                    });
                }
                catch (InvalidOperationException)
                {
                }
            },
            _ =>
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    return;
                }
                try
                {
                    BeginInvoke(() => _linksInFlight.Remove(pageIndex));
                }
                catch (InvalidOperationException)
                {
                }
            });
    }

    private PdfLink? HitTestLink(Point location)
    {
        if (HitTestPdfPoint(location) is not var (pageIndex, pdfX, pdfY))
        {
            return null;
        }
        if (!_linkLayers.TryGetValue(pageIndex, out var layer))
        {
            EnsureLinkLayer(pageIndex);
            return null;
        }
        return layer.HitTest(pdfX, pdfY);
    }

    private bool TryActivateLink(Point location)
    {
        var link = HitTestLink(location);
        if (link is null)
        {
            return false;
        }
        if (link.TargetPage is int targetPage)
        {
            ScrollToPage(targetPage);
            LinkPageRequested?.Invoke(this, targetPage);
            return true;
        }
        if (link.Uri is string uri && LinkReader.IsSafeExternalUri(uri))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri)
                {
                    UseShellExecute = true,
                });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                Core.Diagnostics.AppLog.Warn($"Could not launch link '{uri}': {ex.Message}");
            }
            return true;
        }
        return false;
    }

    private void SetHoveredLink(PdfLink? link)
    {
        if (Equals(link, _hoveredLink))
        {
            return;
        }
        _hoveredLink = link;
        var preview = link is null
            ? null
            : link.Uri ?? (link.TargetPage is int page ? Strings.Format("GoToPageNumber", page + 1) : null);
        LinkHovered?.Invoke(this, preview);
    }

    private const int DragScrollEdgePixels = 28;

    private void OnDragScrollTick(object? sender, EventArgs e)
    {
        if (!_selecting || _document is null || _layout is null)
        {
            _dragScrollTimer.Stop();
            return;
        }

        var clientPoint = PointToClient(Cursor.Position);
        var step = 0;
        if (clientPoint.Y < DragScrollEdgePixels)
        {
            var depth = DragScrollEdgePixels - clientPoint.Y;
            step = -Math.Clamp(10 + depth / 2, 10, 42);
        }
        else if (clientPoint.Y > ClientSize.Height - DragScrollEdgePixels)
        {
            var depth = clientPoint.Y - (ClientSize.Height - DragScrollEdgePixels);
            step = Math.Clamp(10 + depth / 2, 10, 42);
        }

        if (step == 0)
        {
            _dragScrollTimer.Stop();
            return;
        }

        ScrollBy(step);
        _lastDragPoint = clientPoint;
        if (_selectionAnchorChar >= 0)
        {
            _selectionMoved = true;
            UpdateDragSelection(clientPoint);
        }
    }

    private (int PageIndex, double PdfX, double PdfY)? ResolveDragTarget(Point location)
    {
        if (HitTestPdfPoint(location) is { } exact)
        {
            return exact;
        }
        if (_document is null || _layout is null || _layout.Slots.Count == 0)
        {
            return null;
        }

        var singlePageIndex = _layoutMode == PageLayoutMode.SinglePage ? FirstVisiblePage : -1;
        PageSlot? bestSlot = null;
        var bestDistSq = double.MaxValue;
        foreach (var slot in _layout.Slots)
        {
            if (singlePageIndex >= 0 && slot.PageIndex != singlePageIndex)
            {
                continue;
            }
            var left = _layout.CenteredLeft(slot, ClientSize.Width) - _scrollX;
            var top = slot.Top - _scrollY;
            var dx = Math.Max(0, Math.Max(left - location.X, location.X - (left + slot.Width)));
            var dy = Math.Max(0, Math.Max(top - location.Y, location.Y - (top + slot.Height)));
            var distSq = (double)dx * dx + (double)dy * dy;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestSlot = slot;
            }
        }

        if (bestSlot is not { } chosen)
        {
            return null;
        }

        var chosenLeft = _layout.CenteredLeft(chosen, ClientSize.Width) - _scrollX;
        var chosenTop = chosen.Top - _scrollY;
        var clampedX = Math.Clamp(location.X - chosenLeft, 0, Math.Max(0, chosen.Width));
        var clampedY = Math.Clamp(location.Y - chosenTop, 0, Math.Max(0, chosen.Height));
        var size = _document.GetPageSize(chosen.PageIndex);
        var transform = new PageTransform(size, chosen.Width, chosen.Height, _rotation);
        var (pdfX, pdfY) = transform.ViewToPdf(clampedX, clampedY);
        return (chosen.PageIndex, pdfX, pdfY);
    }

    private void UpdateDragSelection(Point location)
    {
        if (_document is null || _layout is null
            || _selectionAnchorPage < 0 || _selectionAnchorPage >= _layout.Slots.Count
            || _selectionAnchorChar < 0)
        {
            return;
        }

        if (ResolveDragTarget(location) is not var (targetPage, pdfX, pdfY))
        {
            return;
        }

        if (!_textLayers.TryGetValue(targetPage, out var targetLayer))
        {
            var seq = _selectionSequence;
            var capturedPoint = location;
            EnsureTextLayer(targetPage, () =>
            {
                if (seq == _selectionSequence && _selectionAnchorChar >= 0)
                {
                    UpdateDragSelection(_selecting ? PointToClient(Cursor.Position) : capturedPoint);
                }
            });
            return;
        }

        var endChar = TextSelectionEngine.ClosestCharacterIndex(targetLayer, pdfX, pdfY);
        if (endChar < 0)
        {
            return;
        }

        var next = DocumentSelection.Create(_selectionAnchorPage, _selectionAnchorChar, targetPage, endChar);
        if (_documentSelection != next)
        {
            SetDocumentSelection(next);
        }
    }

    private void UpdateHoverCursor(Point location)
    {
        if (_document is null)
        {
            SetHoveredLink(null);
            Cursor = Cursors.Default;
            return;
        }
        if (_activeTool == ViewerTool.Pan)
        {
            SetHoveredLink(HitTestLink(location));
            Cursor = Cursors.Hand;
            return;
        }
        var link = HitTestLink(location);
        SetHoveredLink(link);
        if (link is not null)
        {
            Cursor = Cursors.Hand;
            return;
        }
        if (HitTestPdfPoint(location) is var (hoverPage, _, _))
        {
            if (!_textLayers.ContainsKey(hoverPage))
            {
                EnsureTextLayer(hoverPage, static () => { });
            }
            Cursor = Cursors.IBeam;
            return;
        }
        Cursor = Cursors.Default;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        Focus();
    }

    protected override bool IsInputKey(Keys keyData) => keyData switch
    {
        Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Up or Keys.Down or Keys.Left or Keys.Right => true,
        _ => base.IsInputKey(keyData),
    };

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.PageDown:
                if (_layoutMode == PageLayoutMode.SinglePage)
                {
                    ScrollToPage(FirstVisiblePage + 1);
                }
                else
                {
                    ScrollBy(ClientSize.Height - 40);
                }
                e.Handled = true;
                break;
            case Keys.PageUp:
                if (_layoutMode == PageLayoutMode.SinglePage)
                {
                    ScrollToPage(FirstVisiblePage - 1);
                }
                else
                {
                    ScrollBy(-(ClientSize.Height - 40));
                }
                e.Handled = true;
                break;
            case Keys.Down:
                ScrollBy(Font.Height * 3);
                e.Handled = true;
                break;
            case Keys.Up:
                ScrollBy(-(Font.Height * 3));
                e.Handled = true;
                break;
            case Keys.Right:
                ScrollHorizontalBy(Font.Height * 3);
                e.Handled = true;
                break;
            case Keys.Left:
                ScrollHorizontalBy(-(Font.Height * 3));
                e.Handled = true;
                break;
            case Keys.Home:
                ScrollTo(0);
                e.Handled = true;
                break;
            case Keys.End:
                ScrollTo(ContentHeight);
                e.Handled = true;
                break;
        }
        base.OnKeyDown(e);
    }

    private ImageAttributes GetDarkImageAttributes()
    {
        if (_darkImageAttributes is not null)
        {
            return _darkImageAttributes;
        }
        var matrix = new ColorMatrix(new float[][]
        {
            [-0.82f, 0f, 0f, 0f, 0f],
            [0f, -0.82f, 0f, 0f, 0f],
            [0f, 0f, -0.80f, 0f, 0f],
            [0f, 0f, 0f, 1f, 0f],
            [0.92f, 0.92f, 0.93f, 0f, 1f],
        });
        _darkImageAttributes = new ImageAttributes();
        _darkImageAttributes.SetColorMatrix(matrix);
        return _darkImageAttributes;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dragScrollTimer.Stop();
            _dragScrollTimer.Dispose();
            ResetTiles();
            _darkImageAttributes?.Dispose();
            _darkImageAttributes = null;
        }
        base.Dispose(disposing);
    }
}
