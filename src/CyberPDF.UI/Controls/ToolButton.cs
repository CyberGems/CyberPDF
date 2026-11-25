using System.Drawing.Drawing2D;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>
/// Flat toolbar button with a vector icon and an optional label, painted from
/// the theme palette. It uses its own OnPaint so the three themes apply
/// consistently and no native control paints a white rectangle over the dark
/// background.
/// </summary>
public sealed class ToolButton : Control
{
    private bool _hovered;
    private bool _pressed;

    public ToolButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint
            | ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    public IconKind Icon { get; set; } = IconKind.None;

    /// <summary>Optional caption. Empty for icon-only buttons.</summary>
    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value ?? string.Empty;
            Invalidate();
        }
    }

    public bool ShowBorder { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Focus();
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _pressed)
        {
            _pressed = false;
            Invalidate();
        }
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        ThemePainter.PrepareHighQuality(g);

        var bounds = ClientRectangle;
        if (bounds.Width <= 2 || bounds.Height <= 2)
        {
            return;
        }

        var fill = !Enabled
            ? Color.Transparent
            : _pressed ? Theme.Pressed : _hovered ? Theme.Hover : Color.Transparent;

        var radius = 6;
        if (fill != Color.Transparent)
        {
            ThemePainter.FillRounded(g, bounds, radius, fill);
        }
        if (ShowBorder)
        {
            ThemePainter.DrawRounded(g, Rectangle.Inflate(bounds, -1, -1), radius, Theme.Border);
        }
        if (Focused && Enabled)
        {
            ThemePainter.DrawRounded(g, Rectangle.Inflate(bounds, -1, -1), radius, Theme.Accent);
        }

        var ink = !Enabled ? Theme.TextMuted : Theme.Text;
        var hasText = !string.IsNullOrEmpty(Text);
        var scale = DeviceDpi / 96f;
        if (scale <= 0)
        {
            scale = 1f;
        }
        var iconSize = Math.Max(12, (int)Math.Round((hasText ? 16 : 18) * scale));
        var stroke = Math.Max(1.2f, 1.6f * scale);

        if (Icon != IconKind.None)
        {
            if (hasText)
            {
                var textWidth = ThemePainter.MeasureTextWidth(g, Text, Font);
                var gap = (int)Math.Round(7 * scale);
                var totalWidth = iconSize + gap + textWidth;
                var startX = (bounds.Width - totalWidth) / 2;
                var iconRect = new Rectangle(startX, (bounds.Height - iconSize) / 2, iconSize, iconSize);
                Icons.Draw(g, Icon, iconRect, ink, stroke);
                var textRect = new Rectangle(
                    startX + iconSize + gap,
                    0,
                    textWidth + 4,
                    bounds.Height);
                using var brush = new SolidBrush(ink);
                using var format = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Near,
                    FormatFlags = StringFormatFlags.NoWrap,
                };
                g.DrawString(Text, Font, brush, textRect, format);
            }
            else
            {
                var iconRect = new Rectangle(
                    (bounds.Width - iconSize) / 2,
                    (bounds.Height - iconSize) / 2,
                    iconSize,
                    iconSize);
                Icons.Draw(g, Icon, iconRect, ink, stroke);
            }
        }
        else if (hasText)
        {
            ThemePainter.DrawCenteredText(g, Text, Font, ink, bounds);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    /// <summary>
    /// Preferred size for the current icon, caption and font, in device pixels.
    /// The scale is passed explicitly because a control measures against its own
    /// DPI before it is parented, which would size a localized caption for the
    /// wrong monitor.
    /// </summary>
    public Size MeasurePreferredSize(float scale = 1f)
    {
        if (scale <= 0)
        {
            scale = 1f;
        }

        var hasText = !string.IsNullOrEmpty(Text);
        if (!hasText)
        {
            return new Size((int)Math.Round(34 * scale), (int)Math.Round(32 * scale));
        }

        using var g = CreateGraphics();
        var textWidth = ThemePainter.MeasureTextWidth(g, Text, Font);
        var width = textWidth + (int)Math.Round((16 + 7 + 18) * scale) + (int)Math.Round(12 * scale);
        return new Size(width, (int)Math.Round(32 * scale));
    }
}
