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
    private const TextFormatFlags CaptionMeasureFlags =
        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

    private const TextFormatFlags CaptionDrawFlags =
        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

    private bool _hovered;
    private bool _pressed;
    private bool _isActive;

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
    [System.Diagnostics.CodeAnalysis.AllowNull]
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

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }
            _isActive = value;
            Invalidate();
        }
    }

    public bool IsDanger { get; set; }

    public bool IsWindowChrome { get; set; }

    public int CornerRadius { get; set; } = 6;

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

        Color fill;
        if (!Enabled)
        {
            fill = Color.Transparent;
        }
        else if (IsDanger)
        {
            fill = _pressed ? Theme.DangerPressed : _hovered ? Theme.DangerHover : Color.Transparent;
        }
        else
        {
            fill = _pressed
                ? Theme.Pressed
                : _hovered
                    ? Theme.Hover
                    : _isActive ? Theme.Selection : Color.Transparent;
        }

        if (fill != Color.Transparent)
        {
            ThemePainter.FillRounded(g, bounds, CornerRadius, fill);
        }
        if (_isActive && Enabled && !IsWindowChrome)
        {
            var barWidth = Math.Max(8, bounds.Width - 12);
            using var accentBrush = new SolidBrush(Theme.Accent);
            g.FillRectangle(accentBrush, (bounds.Width - barWidth) / 2, bounds.Bottom - 2, barWidth, 2);
        }
        if (ShowBorder)
        {
            ThemePainter.DrawRounded(g, Rectangle.Inflate(bounds, -1, -1), CornerRadius, Theme.Border);
        }
        if (Focused && ShowFocusCues && Enabled)
        {
            ThemePainter.DrawRounded(g, Rectangle.Inflate(bounds, -1, -1), CornerRadius, Theme.Accent);
        }

        var ink = !Enabled
            ? Theme.TextMuted
            : IsDanger && (_hovered || _pressed)
                ? Theme.DangerText
                : Theme.Text;

        var hasText = !string.IsNullOrEmpty(Text);
        var scale = DeviceDpi / 96f;
        if (scale <= 0)
        {
            scale = 1f;
        }

        var iconSize = Math.Max(12, (int)Math.Round((IsWindowChrome ? 13 : hasText ? 15 : 17) * scale));
        // Stroke stays in the normalized 16x16 icon grid; ScaleTransform already
        // multiplies it by the DPI-scaled icon size.
        var stroke = IsWindowChrome ? 1.25f : 1.45f;

        if (Icon != IconKind.None)
        {
            if (hasText)
            {
                var textSize = TextRenderer.MeasureText(g, Text, Font, new Size(int.MaxValue, int.MaxValue), CaptionMeasureFlags);
                var gap = (int)Math.Round(6 * scale);
                var totalWidth = iconSize + gap + textSize.Width;
                var startX = Math.Max((int)Math.Round(8 * scale), (bounds.Width - totalWidth) / 2);
                var iconRect = new Rectangle(startX, (bounds.Height - iconSize) / 2, iconSize, iconSize);
                Icons.Draw(g, Icon, iconRect, ink, stroke);

                var textRect = new Rectangle(
                    startX + iconSize + gap,
                    0,
                    Math.Max(1, bounds.Width - (startX + iconSize + gap) - (int)Math.Round(6 * scale)),
                    bounds.Height);
                TextRenderer.DrawText(g, Text, Font, textRect, ink, CaptionDrawFlags);
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
            var textRect = Rectangle.Inflate(bounds, -(int)Math.Round(6 * scale), 0);
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                textRect,
                ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
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

        var textSize = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), CaptionMeasureFlags);
        var padX = (int)Math.Round(10 * scale) * 2;
        var iconAndGap = Icon == IconKind.None
            ? 0
            : (int)Math.Round(15 * scale) + (int)Math.Round(6 * scale);
        return new Size(textSize.Width + iconAndGap + padX, (int)Math.Round(32 * scale));
    }
}
