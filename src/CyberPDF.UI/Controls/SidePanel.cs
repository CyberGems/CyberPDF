using CyberPDF.Common.I18n;
using CyberPDF.Core.Documents;
using CyberPDF.Core.Rendering;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>
/// The left side panel: page thumbnails and the document outline.
/// </summary>
/// <remarks>
/// Thumbnails are rendered as separate pages from the engine queue, with their
/// own small pixel size, so opening the panel never touches the bands the
/// viewer is painting. Only the pages near the scroll position are generated,
/// which keeps a 500 page document from paying for 500 miniatures at once.
///
/// The panel is a single control with two modes instead of two controls that
/// toggle, because a toggling pair leaves both alive and both painting.
/// </remarks>
public sealed class SidePanel : Control
{
    private const int TabHeight = 34;
    private const int ThumbMargin = 10;
    private const int ThumbWidth = 132;
    private const int LabelHeight = 20;
    private const int CacheLimit = 60;

    private readonly PdfEngine _engine;
    private readonly List<ThumbnailSlot> _thumbnails = new();
    private readonly List<BookmarkEntry> _bookmarks = new();
    private readonly Dictionary<int, Bitmap> _thumbCache = new();
    private readonly List<int> _thumbOrder = new();
    private readonly ThinScrollBar _scroll;

    private PdfDocument? _document;
    private int _mode; // 0 = thumbnails, 1 = bookmarks
    private int _currentPage;
    private int _hoverIndex = -1;
    private int _hoverBookmark = -1;
    private int _requestedCount;

    public SidePanel(PdfEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint
            | ControlStyles.Selectable,
            true);
        BackColor = Theme.Surface;
        TabStop = true;

        // The app's own thin scrollbar, so the panel follows the palette. The
        // native one is drawn by Windows and ignores it.
        _scroll = new ThinScrollBar { Dock = DockStyle.Right, Width = 10 };
        _scroll.ValueChanged += (_, _) => Invalidate();
        Controls.Add(_scroll);
    }

    /// <summary>Raised when the user picks a page, from a thumbnail or a bookmark.</summary>
    public event EventHandler<int>? PageRequested;

    /// <summary>Raised when the current tab changes, so the window can rescale it.</summary>
    public event EventHandler? ModeChanged;

    public int Mode => _mode;

    public int BookmarkCount => _bookmarks.Count;

    public int ThumbnailCount => _thumbCache.Count;

    public void SetTab(int mode)
    {
        if (mode == _mode)
        {
            return;
        }
        _mode = mode == 0 ? 0 : 1;
        _scroll.Value = 0;
        UpdateScrollBar();
        Invalidate();
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetDocument(PdfDocument? document)
    {
        _document = document;
        _bookmarks.Clear();
        ClearThumbnails();
        _currentPage = 0;
        _scroll.Value = 0;

        if (document is null)
        {
            UpdateScrollBar();
            Invalidate();
            return;
        }

        // A4 at the thumbnail width, so the placeholder list has the right
        // height before a single miniature exists.
        _thumbnails.Clear();
        for (var i = 0; i < document.PageCount; i++)
        {
            var size = document.GetPageSize(i);
            var ratio = size.Width <= 0 ? 1.414 : size.Height / size.Width;
            var height = Math.Max(24, (int)Math.Round(ThumbWidth * ratio));
            _thumbnails.Add(new ThumbnailSlot(i, height));
        }

        UpdateScrollBar();
        Invalidate();
    }

    /// <summary>Reads the outline on the PDFium thread and repaints when it lands.</summary>
    public void LoadBookmarks()
    {
        if (_document is null)
        {
            return;
        }
        var document = _document;
        _engine.GetBookmarks(
            document,
            entries =>
            {
                if (IsDisposed || document != _document)
                {
                    return;
                }
                _bookmarks.Clear();
                _bookmarks.AddRange(entries);
                UpdateScrollBar();
                Invalidate();
            },
            error => Console.Error.WriteLine($"BOOKMARKS: {error.Message}"));
    }

    /// <summary>Marks the page the viewport is on; the thumbnail list highlights it.</summary>
    public void SetCurrentPage(int pageIndex)
    {
        if (pageIndex == _currentPage)
        {
            return;
        }
        _currentPage = pageIndex;
        EnsureThumbnail(pageIndex);
        Invalidate();
    }

    /// <summary>
    /// Generates the miniatures around a page, nearest first. Called when the
    /// scroll position changes, so the panel follows the document instead of
    /// rendering every page up front.
    /// </summary>
    public void EnsureAround(int pageIndex, int radius = 3)
    {
        if (_document is null)
        {
            return;
        }
        EnsureThumbnail(pageIndex);
        for (var offset = 1; offset <= radius; offset++)
        {
            EnsureThumbnail(pageIndex + offset);
            EnsureThumbnail(pageIndex - offset);
        }
    }

    private void EnsureThumbnail(int pageIndex)
    {
        if (_document is null || pageIndex < 0 || pageIndex >= _thumbnails.Count)
        {
            return;
        }
        if (_thumbCache.ContainsKey(pageIndex) || _requestedCount > CacheLimit * 2)
        {
            return;
        }
        if (!_thumbnails[pageIndex].Requested)
        {
            _thumbnails[pageIndex].Requested = true;
            _requestedCount++;
        }
        else
        {
            return;
        }

        var document = _document;
        var slot = _thumbnails[pageIndex];
        var size = document.GetPageSize(pageIndex);

        // Same aspect as the viewer's page, at thumbnail width.
        var width = ThumbWidth;
        var height = Math.Max(1, slot.Height);
        _engine.Render(
            document,
            pageIndex,
            width,
            height,
            rotation: 0,
            onSuccess: result =>
            {
                if (IsDisposed)
                {
                    return;
                }
                try
                {
                    BeginInvoke(() =>
                    {
                        if (document != _document)
                        {
                            return;
                        }
                        Store(pageIndex, result);
                    });
                }
                catch (InvalidOperationException)
                {
                    // The window closed while the miniature was rendering.
                }
            },
            onError: error => Console.Error.WriteLine($"THUMB {pageIndex}: {error.Message}"));
    }

    private void Store(int pageIndex, RenderResult result)
    {
        var page = result.Page;

        // Copy instead of keeping the rendered page: the engine may hand a
        // caller-owned page that nobody else frees, and a cached band must not be
        // disposed by the panel. A thumbnail is a few hundred kilobytes.
        var copy = new Bitmap(page.Width, page.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(copy))
        {
            graphics.Clear(Color.White);
            graphics.DrawImageUnscaled(page.Bitmap, 0, 0);
        }
        if (result.CallerOwns)
        {
            page.Dispose();
        }

        _thumbCache[pageIndex] = copy;
        _thumbOrder.Add(pageIndex);
        while (_thumbOrder.Count > CacheLimit)
        {
            var evicted = _thumbOrder[0];
            _thumbOrder.RemoveAt(0);
            if (evicted == pageIndex)
            {
                continue;
            }
            if (_thumbCache.Remove(evicted, out var bitmap))
            {
                bitmap.Dispose();
            }
        }
        Invalidate();
    }

    private void ClearThumbnails()
    {
        foreach (var bitmap in _thumbCache.Values)
        {
            bitmap.Dispose();
        }
        _thumbCache.Clear();
        _thumbOrder.Clear();
        _requestedCount = 0;
        foreach (var slot in _thumbnails)
        {
            slot.Requested = false;
        }
    }

    private void UpdateScrollBar()
    {
        _scroll.Maximum = Math.Max(0, ContentHeight);
        _scroll.LargeChange = Math.Max(1, ClientSize.Height - TabHeight);
        if (_scroll.Value > _scroll.Maximum)
        {
            _scroll.Value = 0;
        }
    }

    private bool IsBookmarks => _mode == 1;

    private int RowHeight => IsBookmarks ? 30 : 0;

    private int ContentHeight
    {
        get
        {
            if (IsBookmarks)
            {
                return Math.Max(0, _bookmarks.Count * RowHeight + ThumbMargin);
            }
            var total = ThumbMargin;
            foreach (var slot in _thumbnails)
            {
                total += slot.Height + LabelHeight + ThumbMargin;
            }
            return total;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateScrollBar();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        ThemePainter.PrepareHighQuality(g);
        g.Clear(Theme.Surface);

        DrawTabs(g);

        var state = g.Save();
        g.SetClip(new Rectangle(0, TabHeight, Width, Math.Max(0, Height - TabHeight)));
        if (IsBookmarks)
        {
            DrawBookmarks(g);
        }
        else
        {
            DrawThumbnails(g);
        }
        g.Restore(state);

        using var border = new Pen(Theme.Border);
        g.DrawLine(border, Width - 1, 0, Width - 1, Height);
    }

    private void DrawTabs(Graphics g)
    {
        var half = Width / 2;
        var thumbs = new Rectangle(0, 0, half, TabHeight);
        var marks = new Rectangle(half, 0, Width - half, TabHeight);

        using (var strip = new SolidBrush(Theme.Bg))
        {
            g.FillRectangle(strip, 0, 0, Width, TabHeight);
        }

        DrawTabLabel(g, Strings.Get("Thumbnails"), thumbs, _mode == 0);
        DrawTabLabel(g, Strings.Get("Bookmarks"), marks, _mode == 1);

        using var border = new Pen(Theme.Border);
        g.DrawLine(border, 0, TabHeight - 1, Width, TabHeight - 1);
        if (_mode == 0)
        {
            g.DrawLine(new Pen(Theme.Accent, 2f), 0, TabHeight - 2, half, TabHeight - 2);
        }
        else
        {
            g.DrawLine(new Pen(Theme.Accent, 2f), half, TabHeight - 2, Width, TabHeight - 2);
        }
    }

    private static void DrawTabLabel(Graphics g, string text, Rectangle bounds, bool active)
    {
        using var font = new Font("Segoe UI", 9f, active ? FontStyle.Bold : FontStyle.Regular);
        ThemePainter.DrawCenteredText(g, text, font, active ? Theme.Text : Theme.TextMuted, bounds);
    }

    private void DrawThumbnails(Graphics g)
    {
        var top = TabHeight - _scroll.Value;
        foreach (var slot in _thumbnails)
        {
            var rowTop = top + ThumbMargin;
            var rowHeight = slot.Height + LabelHeight + ThumbMargin;
            top += rowHeight;

            if (top < TabHeight || rowTop > Height)
            {
                continue;
            }

            var left = (Width - _scroll.Width - ThumbWidth) / 2;
            var paper = new Rectangle(left, rowTop, ThumbWidth, slot.Height);

            if (_thumbCache.TryGetValue(slot.PageIndex, out var bitmap))
            {
                g.DrawImage(bitmap, paper);
            }
            else
            {
                using var placeholder = new SolidBrush(Theme.Canvas);
                g.FillRectangle(placeholder, paper);
                EnsureThumbnail(slot.PageIndex);
            }

            var selected = slot.PageIndex == _currentPage;
            using (var pen = new Pen(selected ? Theme.Accent : Theme.Border, selected ? 2f : 1f))
            {
                g.DrawRectangle(pen, paper);
            }

            using var font = new Font("Segoe UI", 8.5f, selected ? FontStyle.Bold : FontStyle.Regular);
            ThemePainter.DrawCenteredText(
                g,
                Strings.Format("PageNumber", slot.PageIndex + 1),
                font,
                selected ? Theme.Text : Theme.TextMuted,
                new Rectangle(left, rowTop + slot.Height, ThumbWidth, LabelHeight));
        }
    }

    private void DrawBookmarks(Graphics g)
    {
        if (_bookmarks.Count == 0)
        {
            using var font = new Font("Segoe UI", 9f);
            ThemePainter.DrawCenteredText(
                g,
                Strings.Get("NoBookmarks"),
                font,
                Theme.TextMuted,
                new Rectangle(ThumbMargin, TabHeight + 40, Width - ThumbMargin * 2 - 12, 60));
            return;
        }

        var top = TabHeight - _scroll.Value;
        for (var i = 0; i < _bookmarks.Count; i++)
        {
            var entry = _bookmarks[i];
            var row = new Rectangle(0, top, Width - _scroll.Width, RowHeight);
            top += RowHeight;

            if (row.Bottom < TabHeight || row.Top > Height)
            {
                continue;
            }

            if (i == _hoverBookmark)
            {
                using var hover = new SolidBrush(Theme.Hover);
                g.FillRectangle(hover, row);
            }

            var indent = ThumbMargin + entry.Depth * 14;
            using var font = new Font("Segoe UI", 9f);
            var textBounds = new Rectangle(indent, row.Top, Math.Max(10, row.Width - indent - 6), row.Height);
            using var brush = new SolidBrush(entry.PageIndex == _currentPage ? Theme.Text : Theme.TextMuted);
            using var format = new StringFormat
            {
                LineAlignment = StringAlignment.Center,
                Alignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
            };
            g.DrawString(entry.Title, font, brush, textBounds, format);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button != MouseButtons.Left)
        {
            return;
        }
        if (e.Y < TabHeight)
        {
            SetTab(e.X < Width / 2 ? 0 : 1);
            return;
        }

        if (IsBookmarks)
        {
            var index = (e.Y + _scroll.Value - TabHeight) / RowHeight;
            if (index >= 0 && index < _bookmarks.Count)
            {
                PageRequested?.Invoke(this, _bookmarks[index].PageIndex);
            }
            return;
        }

        var page = PageAt(e.Location);
        if (page >= 0)
        {
            _currentPage = page;
            Invalidate();
            PageRequested?.Invoke(this, page);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsBookmarks)
        {
            var index = e.Y < TabHeight
                ? -1
                : (e.Y + _scroll.Value - TabHeight) / RowHeight;
            if (index >= _bookmarks.Count)
            {
                index = -1;
            }
            if (index != _hoverBookmark)
            {
                _hoverBookmark = index;
                Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            return;
        }

        var page = PageAt(e.Location);
        if (page != _hoverIndex)
        {
            _hoverIndex = page;
            Cursor = page >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverIndex = -1;
        _hoverBookmark = -1;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollByWheel(e.Delta);
        if (e is HandledMouseEventArgs handled)
        {
            handled.Handled = true;
        }
    }

    private void ScrollByWheel(int delta)
    {
        var step = Math.Max(40, ClientSize.Height / 4);
        _scroll.Value = Math.Clamp(
            _scroll.Value - (int)(delta / 120f * step),
            0,
            Math.Max(0, _scroll.Maximum));
        Invalidate();
    }

    private int PageAt(Point location)
    {
        var top = TabHeight - _scroll.Value;
        foreach (var slot in _thumbnails)
        {
            var rowTop = top + ThumbMargin;
            var rowHeight = slot.Height + LabelHeight + ThumbMargin;
            if (location.Y >= rowTop && location.Y <= rowTop + slot.Height)
            {
                return slot.PageIndex;
            }
            top += rowHeight;
            if (rowTop > Height)
            {
                break;
            }
        }
        return -1;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearThumbnails();
        }
        base.Dispose(disposing);
    }

    private sealed class ThumbnailSlot
    {
        public ThumbnailSlot(int pageIndex, int height)
        {
            PageIndex = pageIndex;
            Height = height;
        }

        public int PageIndex { get; }

        public int Height { get; }

        public bool Requested { get; set; }
    }
}
