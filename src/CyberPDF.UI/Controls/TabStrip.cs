using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>One open document tab displayed in <see cref="TabStrip"/>.</summary>
public sealed record DocumentTabItem(string Key, string Title, string FullPath);

/// <summary>
/// Compact themed document tab strip supporting multi-PDF sessions, close
/// buttons, middle-click close, and a quick New Tab (+) button.
/// </summary>
public sealed class TabStrip : Control
{
    private readonly List<DocumentTabItem> _tabs = new();
    private readonly List<Rectangle> _tabRects = new();
    private readonly List<Rectangle> _closeRects = new();
    private Rectangle _addRect;
    private int _activeIndex = -1;
    private int _hoverTab = -1;
    private int _hoverClose = -1;
    private bool _hoverAdd;

    public TabStrip()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            true);
        BackColor = Theme.Bg;
        Height = 32;
        Visible = false;
    }

    public event EventHandler<int>? TabSelected;

    public event EventHandler<int>? TabCloseRequested;

    public event EventHandler? NewTabRequested;

    public void SetTabs(IReadOnlyList<DocumentTabItem> tabs, int activeIndex)
    {
        _tabs.Clear();
        _tabs.AddRange(tabs);
        _activeIndex = tabs.Count == 0 ? -1 : Math.Clamp(activeIndex, 0, tabs.Count - 1);
        Visible = _tabs.Count > 0;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        ThemePainter.PrepareHighQuality(g);
        g.Clear(Theme.Bg);

        using var borderPen = new Pen(Theme.Border);
        g.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);

        _tabRects.Clear();
        _closeRects.Clear();

        if (_tabs.Count == 0)
        {
            _addRect = Rectangle.Empty;
            return;
        }

        var scale = DeviceDpi <= 0 ? 1f : DeviceDpi / 96f;
        var padX = (int)Math.Round(6 * scale);
        var addSize = (int)Math.Round(26 * scale);
        var minTabW = (int)Math.Round(100 * scale);
        var maxTabW = (int)Math.Round(220 * scale);
        var available = Math.Max(minTabW, Width - padX * 2 - addSize - (int)Math.Round(8 * scale));
        var tabW = Math.Clamp(available / _tabs.Count, Math.Min(minTabW, available / Math.Max(1, _tabs.Count)), maxTabW);
        var tabTop = (int)Math.Round(3 * scale);
        var tabH = Height - tabTop;
        var iconSize = (int)Math.Round(13 * scale);
        var closeSize = (int)Math.Round(16 * scale);

        using var activeFont = new Font("Segoe UI", 8.75f, FontStyle.Bold, GraphicsUnit.Point);
        using var normalFont = new Font("Segoe UI", 8.75f, FontStyle.Regular, GraphicsUnit.Point);

        var x = padX;
        for (var i = 0; i < _tabs.Count; i++)
        {
            var rect = new Rectangle(x, tabTop, tabW - (int)Math.Round(2 * scale), tabH);
            _tabRects.Add(rect);

            var isActive = i == _activeIndex;
            var isHovered = i == _hoverTab;
            if (isActive)
            {
                using var fill = new SolidBrush(Theme.Surface);
                g.FillRectangle(fill, rect);
                using var accent = new SolidBrush(Theme.Accent);
                g.FillRectangle(accent, rect.X, rect.Y, rect.Width, 2);
                g.DrawLine(borderPen, rect.X, rect.Y, rect.X, rect.Bottom);
                g.DrawLine(borderPen, rect.Right - 1, rect.Y, rect.Right - 1, rect.Bottom);
            }
            else if (isHovered)
            {
                using var hover = new SolidBrush(Theme.Hover);
                g.FillRectangle(hover, rect);
            }

            var docIconRect = new Rectangle(
                rect.X + (int)Math.Round(8 * scale),
                rect.Y + (rect.Height - iconSize) / 2,
                iconSize,
                iconSize);
            Icons.Draw(g, IconKind.Document, docIconRect, isActive ? Theme.Accent : Theme.TextMuted, 1.4f);

            var closeRect = new Rectangle(
                rect.Right - closeSize - (int)Math.Round(6 * scale),
                rect.Y + (rect.Height - closeSize) / 2,
                closeSize,
                closeSize);
            _closeRects.Add(closeRect);

            if (i == _hoverClose)
            {
                ThemePainter.FillRounded(g, closeRect, 4, Theme.Hover);
            }
            var closeGlyphRect = Rectangle.Inflate(closeRect, -(int)Math.Round(3 * scale), -(int)Math.Round(3 * scale));
            Icons.Draw(g, IconKind.Close, closeGlyphRect, i == _hoverClose ? Theme.Text : Theme.TextMuted, 1.35f);

            var textX = docIconRect.Right + (int)Math.Round(6 * scale);
            var textW = Math.Max(10, closeRect.Left - textX - (int)Math.Round(4 * scale));
            var textRect = new Rectangle(textX, rect.Y, textW, rect.Height);
            TextRenderer.DrawText(
                g,
                _tabs[i].Title,
                isActive ? activeFont : normalFont,
                textRect,
                isActive ? Theme.Text : Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            x += tabW;
        }

        _addRect = new Rectangle(x + (int)Math.Round(4 * scale), (Height - addSize) / 2, addSize, addSize);
        if (_addRect.Right <= Width - 2)
        {
            if (_hoverAdd)
            {
                ThemePainter.FillRounded(g, _addRect, 5, Theme.Hover);
            }
            var plusRect = Rectangle.Inflate(_addRect, -(int)Math.Round(5 * scale), -(int)Math.Round(5 * scale));
            Icons.Draw(g, IconKind.Plus, plusRect, _hoverAdd ? Theme.Text : Theme.TextMuted, 1.45f);
        }
        else
        {
            _addRect = Rectangle.Empty;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hoverTab = -1;
        var hoverClose = -1;
        for (var i = 0; i < _tabRects.Count; i++)
        {
            if (_tabRects[i].Contains(e.Location))
            {
                hoverTab = i;
                if (i < _closeRects.Count && _closeRects[i].Contains(e.Location))
                {
                    hoverClose = i;
                }
                break;
            }
        }
        var hoverAdd = !_addRect.IsEmpty && _addRect.Contains(e.Location);

        if (hoverTab != _hoverTab || hoverClose != _hoverClose || hoverAdd != _hoverAdd)
        {
            _hoverTab = hoverTab;
            _hoverClose = hoverClose;
            _hoverAdd = hoverAdd;
            Cursor = (hoverTab >= 0 || hoverAdd) ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverTab != -1 || _hoverClose != -1 || _hoverAdd)
        {
            _hoverTab = -1;
            _hoverClose = -1;
            _hoverAdd = false;
            Cursor = Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Left && !_addRect.IsEmpty && _addRect.Contains(e.Location))
        {
            NewTabRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        for (var i = 0; i < _tabRects.Count; i++)
        {
            if (!_tabRects[i].Contains(e.Location))
            {
                continue;
            }
            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && i < _closeRects.Count && _closeRects[i].Contains(e.Location)))
            {
                TabCloseRequested?.Invoke(this, i);
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                TabSelected?.Invoke(this, i);
                return;
            }
        }
    }
}
