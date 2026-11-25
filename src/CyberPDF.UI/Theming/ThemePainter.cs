using System.Drawing.Drawing2D;

namespace CyberPDF.UI.Theming;

/// <summary>
/// Drawing helpers shared by the custom-painted controls. Keeping them in one
/// place is what makes every surface respond to a theme change identically.
/// </summary>
public static class ThemePainter
{
    /// <summary>Rounded rectangle path, used by buttons, fields and cards.</summary>
    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void FillRounded(Graphics g, Rectangle bounds, int radius, Color color)
    {
        using var path = RoundedRect(bounds, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void DrawRounded(Graphics g, Rectangle bounds, int radius, Color color, float thickness = 1f)
    {
        using var path = RoundedRect(bounds, radius);
        using var pen = new Pen(color, thickness);
        g.DrawPath(pen, path);
    }

    /// <summary>Enables high quality smoothing for text and shapes on a control.</summary>
    public static void PrepareHighQuality(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
    }

    /// <summary>Vertically and horizontally centered text with ellipsis.</summary>
    public static void DrawCenteredText(Graphics g, string text, Font font, Color color, Rectangle bounds)
    {
        using var brush = new SolidBrush(color);
        var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        g.DrawString(text, font, brush, bounds, format);
        format.Dispose();
    }

    /// <summary>
    /// Measures the width a string needs with a given font. Used by the toolbar
    /// to size labeled buttons, so localization never clips a caption.
    /// </summary>
    public static int MeasureTextWidth(Graphics g, string text, Font font)
        => (int)Math.Ceiling(g.MeasureString(text, font, int.MaxValue, StringFormat.GenericTypographic).Width);
}
