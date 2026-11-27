using CyberPDF.Common.I18n;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>
/// Start screen shown when no document is open: brand header, primary Open CTA,
/// drop target hint, and a clickable list of recent PDFs.
/// </summary>
public sealed class WelcomeView : Control
{
    private readonly Image? _logo;
    private readonly ToolButton _openButton = new();
    private readonly ToolButton _clearRecentButton = new();
    private readonly List<string> _recentFiles = new();
    private readonly List<Rectangle> _itemBounds = new();
    private int _hoverIndex = -1;

    public WelcomeView(Image? logo)
    {
        _logo = logo;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            true);
        BackColor = Theme.Canvas;
        AllowDrop = true;

        _openButton.Icon = IconKind.Open;
        _openButton.Text = Strings.Get("OpenFile");
        _openButton.ShowBorder = true;
        _openButton.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_openButton);

        _clearRecentButton.Icon = IconKind.Delete;
        _clearRecentButton.Text = Strings.Get("ClearRecent");
        _clearRecentButton.Visible = false;
        _clearRecentButton.Click += (_, _) => ClearRecentRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(_clearRecentButton);
    }

    public event EventHandler? OpenRequested;

    public event EventHandler<string>? FileRequested;

    public event EventHandler<string>? RemoveRecentRequested;

    public event EventHandler? ClearRecentRequested;

    public void SetRecentFiles(IEnumerable<string> files)
    {
        _recentFiles.Clear();
        foreach (var path in files)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                _recentFiles.Add(path);
                if (_recentFiles.Count >= 8)
                {
                    break;
                }
            }
        }
        _hoverIndex = -1;
        _clearRecentButton.Visible = _recentFiles.Count > 0;
        LayoutButtons();
        Invalidate();
    }

    public void RefreshLocalization()
    {
        _openButton.Text = Strings.Get("OpenFile");
        _clearRecentButton.Text = Strings.Get("ClearRecent");
        LayoutButtons();
        Invalidate();
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Canvas;
        _openButton.Invalidate();
        _clearRecentButton.Invalidate();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutButtons();
    }

    private float DpiScale => DeviceDpi <= 0 ? 1f : DeviceDpi / 96f;

    private Rectangle CardBounds()
    {
        var scale = DpiScale;
        var maxWidth = Math.Min((int)Math.Round(560 * scale), Math.Max(280, ClientSize.Width - (int)Math.Round(48 * scale)));
        var recentHeight = _recentFiles.Count == 0
            ? (int)Math.Round(36 * scale)
            : _recentFiles.Count * (int)Math.Round(44 * scale);
        var totalHeight = (int)Math.Round(210 * scale) + recentHeight;
        var x = Math.Max(0, (ClientSize.Width - maxWidth) / 2);
        var y = Math.Max((int)Math.Round(24 * scale), (ClientSize.Height - totalHeight) / 2);
        return new Rectangle(x, y, maxWidth, Math.Min(totalHeight, Math.Max(200, ClientSize.Height - y - 16)));
    }

    private void LayoutButtons()
    {
        var scale = DpiScale;
        var card = CardBounds();
        var openSize = _openButton.MeasurePreferredSize(scale);
        openSize = new Size(Math.Max(openSize.Width, (int)Math.Round(150 * scale)), (int)Math.Round(36 * scale));
        _openButton.Size = openSize;
        _openButton.Location = new Point(
            card.X + (card.Width - openSize.Width) / 2,
            card.Y + (int)Math.Round(114 * scale));

        var clearSize = _clearRecentButton.MeasurePreferredSize(scale);
        _clearRecentButton.Size = clearSize;
        _clearRecentButton.Location = new Point(
            card.Right - clearSize.Width,
            card.Y + (int)Math.Round(176 * scale));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        ThemePainter.PrepareHighQuality(g);
        g.Clear(Theme.Canvas);

        var scale = DpiScale;
        var card = CardBounds();
        _itemBounds.Clear();

        var logoSize = (int)Math.Round(48 * scale);
        if (_logo is not null)
        {
            var logoRect = new Rectangle(card.X + (card.Width - logoSize) / 2, card.Y, logoSize, logoSize);
            g.DrawImage(_logo, logoRect);
        }

        using var titleFont = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Point);
        using var subFont = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        using var sectionFont = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
        using var itemTitleFont = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        using var itemPathFont = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point);

        var titleRect = new Rectangle(card.X, card.Y + (int)Math.Round(56 * scale), card.Width, (int)Math.Round(28 * scale));
        ThemePainter.DrawCenteredText(g, Strings.Get("AppTitle"), titleFont, Theme.Text, titleRect);

        var hintRect = new Rectangle(card.X, card.Y + (int)Math.Round(84 * scale), card.Width, (int)Math.Round(22 * scale));
        ThemePainter.DrawCenteredText(g, Strings.Get("DropHint"), subFont, Theme.TextMuted, hintRect);

        var recentHeaderY = card.Y + (int)Math.Round(178 * scale);
        var headerRect = new Rectangle(card.X, recentHeaderY, card.Width - _clearRecentButton.Width - 8, (int)Math.Round(26 * scale));
        TextRenderer.DrawText(
            g,
            Strings.Get("OpenRecent"),
            sectionFont,
            headerRect,
            Theme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

        using var divider = new Pen(Theme.Border);
        var listTop = recentHeaderY + (int)Math.Round(32 * scale);
        g.DrawLine(divider, card.X, listTop - 4, card.Right, listTop - 4);

        if (_recentFiles.Count == 0)
        {
            var emptyRect = new Rectangle(card.X, listTop + 4, card.Width, (int)Math.Round(30 * scale));
            TextRenderer.DrawText(
                g,
                Strings.Get("NoRecent"),
                subFont,
                emptyRect,
                Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return;
        }

        var rowHeight = (int)Math.Round(44 * scale);
        var iconSize = (int)Math.Round(16 * scale);
        for (var i = 0; i < _recentFiles.Count; i++)
        {
            var row = new Rectangle(card.X, listTop + i * rowHeight, card.Width, rowHeight);
            if (row.Bottom > ClientSize.Height - 8)
            {
                break;
            }
            _itemBounds.Add(row);

            if (i == _hoverIndex)
            {
                ThemePainter.FillRounded(g, row, 6, Theme.Hover);
            }

            var iconRect = new Rectangle(row.X + (int)Math.Round(10 * scale), row.Y + (row.Height - iconSize) / 2, iconSize, iconSize);
            Icons.Draw(g, IconKind.Document, iconRect, i == _hoverIndex ? Theme.Accent : Theme.TextMuted, 1.45f);

            var textX = iconRect.Right + (int)Math.Round(10 * scale);
            var textWidth = Math.Max(40, row.Right - textX - (int)Math.Round(10 * scale));
            var fileName = Path.GetFileName(_recentFiles[i]);
            var folder = Path.GetDirectoryName(_recentFiles[i]) ?? string.Empty;

            var nameRect = new Rectangle(textX, row.Y + (int)Math.Round(5 * scale), textWidth, (int)Math.Round(19 * scale));
            var folderRect = new Rectangle(textX, row.Y + (int)Math.Round(23 * scale), textWidth, (int)Math.Round(16 * scale));

            TextRenderer.DrawText(
                g,
                fileName,
                itemTitleFont,
                nameRect,
                Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                g,
                folder,
                itemPathFont,
                folderRect,
                Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPadding);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var index = ItemIndexAt(e.Location);
        if (index != _hoverIndex)
        {
            _hoverIndex = index;
            Cursor = index >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var index = ItemIndexAt(e.Location);
        if (index < 0 || index >= _recentFiles.Count)
        {
            return;
        }

        var path = _recentFiles[index];
        if (e.Button == MouseButtons.Left)
        {
            FileRequested?.Invoke(this, path);
        }
        else if (e.Button == MouseButtons.Right)
        {
            var menu = ThemedContextMenu.Create(
                new ContextMenuItem(Strings.Get("Open"), () => FileRequested?.Invoke(this, path), IconKind.Open),
                new ContextMenuItem(Strings.Get("RemoveFromList"), () => RemoveRecentRequested?.Invoke(this, path), IconKind.Delete),
                ContextMenuItem.Separator(),
                new ContextMenuItem(Strings.Get("ClearRecent"), () => ClearRecentRequested?.Invoke(this, EventArgs.Empty), IconKind.Delete));
            menu.Show(Cursor.Position);
        }
    }

    private int ItemIndexAt(Point location)
    {
        for (var i = 0; i < _itemBounds.Count; i++)
        {
            if (_itemBounds[i].Contains(location))
            {
                return i;
            }
        }
        return -1;
    }
}
