using System.Drawing.Drawing2D;

namespace CyberPDF.UI.Theming;

/// <summary>
/// Vector icons drawn with GDI+, so the executable carries no image resources
/// and starts without decoding anything. Each icon is drawn inside a square of
/// the given size and inherits the color it is given.
/// </summary>
public static class Icons
{
    public static void Draw(Graphics g, IconKind kind, Rectangle bounds, Color color, float stroke = 1.45f)
    {
        if (kind == IconKind.None || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var pen = new Pen(color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        using var brush = new SolidBrush(color);

        // Work in a normalized 16x16 space and scale, so every glyph has the
        // same optical weight regardless of the target DPI or size.
        var state = g.Save();
        g.TranslateTransform(bounds.X, bounds.Y);
        g.ScaleTransform(bounds.Width / 16f, bounds.Height / 16f);

        switch (kind)
        {
            case IconKind.Open:
            {
                using var folder = new GraphicsPath();
                folder.AddPolygon(new[]
                {
                    new PointF(2f, 3.5f),
                    new PointF(6.2f, 3.5f),
                    new PointF(7.6f, 5.2f),
                    new PointF(14f, 5.2f),
                    new PointF(14f, 12.5f),
                    new PointF(2f, 12.5f),
                });
                g.DrawPath(pen, folder);
                g.DrawLine(pen, 2f, 6.8f, 14f, 6.8f);
                break;
            }

            case IconKind.ZoomIn:
                g.DrawEllipse(pen, 2.2f, 2.2f, 8.6f, 8.6f);
                g.DrawLine(pen, 9.6f, 9.6f, 13.8f, 13.8f);
                g.DrawLine(pen, 4.5f, 6.5f, 8.5f, 6.5f);
                g.DrawLine(pen, 6.5f, 4.5f, 6.5f, 8.5f);
                break;

            case IconKind.ZoomOut:
                g.DrawEllipse(pen, 2.2f, 2.2f, 8.6f, 8.6f);
                g.DrawLine(pen, 9.6f, 9.6f, 13.8f, 13.8f);
                g.DrawLine(pen, 4.5f, 6.5f, 8.5f, 6.5f);
                break;

            case IconKind.FitWidth:
                // Two vertical walls with outward horizontal arrows: |<->|
                g.DrawLine(pen, 2f, 3f, 2f, 13f);
                g.DrawLine(pen, 14f, 3f, 14f, 13f);
                g.DrawLine(pen, 4.2f, 8f, 11.8f, 8f);
                g.DrawLines(pen, new[] { new PointF(6.2f, 5.8f), new PointF(4f, 8f), new PointF(6.2f, 10.2f) });
                g.DrawLines(pen, new[] { new PointF(9.8f, 5.8f), new PointF(12f, 8f), new PointF(9.8f, 10.2f) });
                break;

            case IconKind.FitPage:
                // Four corner brackets framing a centered page outline.
                g.DrawLines(pen, new[] { new PointF(2f, 5f), new PointF(2f, 2f), new PointF(5f, 2f) });
                g.DrawLines(pen, new[] { new PointF(11f, 2f), new PointF(14f, 2f), new PointF(14f, 5f) });
                g.DrawLines(pen, new[] { new PointF(2f, 11f), new PointF(2f, 14f), new PointF(5f, 14f) });
                g.DrawLines(pen, new[] { new PointF(11f, 14f), new PointF(14f, 14f), new PointF(14f, 11f) });
                g.DrawRectangle(pen, 5.2f, 4.2f, 5.6f, 7.6f);
                break;

            case IconKind.RotateLeft:
                g.DrawArc(pen, 3f, 3.5f, 10f, 10f, -155, -255);
                g.DrawLines(pen, new[] { new PointF(1.8f, 5.6f), new PointF(3.5f, 6.4f), new PointF(4.6f, 4.2f) });
                break;

            case IconKind.RotateRight:
                g.DrawArc(pen, 3f, 3.5f, 10f, 10f, -25, 255);
                g.DrawLines(pen, new[] { new PointF(14.2f, 5.6f), new PointF(12.5f, 6.4f), new PointF(11.4f, 4.2f) });
                break;

            case IconKind.ThemeDefault:
            {
                // Crisp crescent moon for the dark default theme.
                using var moon = new GraphicsPath();
                moon.AddArc(2.5f, 2.5f, 11f, 11f, -55, 250);
                moon.AddArc(6f, 2f, 8.5f, 8.5f, 115, -195);
                moon.CloseFigure();
                g.DrawPath(pen, moon);
                break;
            }

            case IconKind.ThemeGray:
                g.DrawEllipse(pen, 2.5f, 2.5f, 11f, 11f);
                g.FillPie(brush, 2.5f, 2.5f, 11f, 11f, 90, 180);
                break;

            case IconKind.ThemeLight:
                g.DrawEllipse(pen, 5.2f, 5.2f, 5.6f, 5.6f);
                for (var i = 0; i < 8; i++)
                {
                    var angle = Math.PI * i / 4;
                    var x1 = 8f + (float)Math.Cos(angle) * 5.1f;
                    var y1 = 8f + (float)Math.Sin(angle) * 5.1f;
                    var x2 = 8f + (float)Math.Cos(angle) * 6.8f;
                    var y2 = 8f + (float)Math.Sin(angle) * 6.8f;
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
                break;

            case IconKind.SidePanel:
                g.DrawRectangle(pen, 2f, 3f, 12f, 10f);
                g.DrawLine(pen, 6.2f, 3f, 6.2f, 13f);
                g.DrawLine(pen, 3.6f, 6f, 4.8f, 6f);
                g.DrawLine(pen, 3.6f, 8.5f, 4.8f, 8.5f);
                break;

            case IconKind.Close:
                g.DrawLine(pen, 4.2f, 4.2f, 11.8f, 11.8f);
                g.DrawLine(pen, 11.8f, 4.2f, 4.2f, 11.8f);
                break;

            case IconKind.PreviousPage:
                g.DrawLines(pen, new[] { new PointF(10f, 3.5f), new PointF(5f, 8f), new PointF(10f, 12.5f) });
                break;

            case IconKind.NextPage:
                g.DrawLines(pen, new[] { new PointF(6f, 3.5f), new PointF(11f, 8f), new PointF(6f, 12.5f) });
                break;

            case IconKind.ChevronUp:
                g.DrawLines(pen, new[] { new PointF(3.5f, 10f), new PointF(8f, 5.5f), new PointF(12.5f, 10f) });
                break;

            case IconKind.ChevronDown:
                g.DrawLines(pen, new[] { new PointF(3.5f, 6f), new PointF(8f, 10.5f), new PointF(12.5f, 6f) });
                break;

            case IconKind.Search:
                g.DrawEllipse(pen, 2.2f, 2.2f, 8.4f, 8.4f);
                g.DrawLine(pen, 9.5f, 9.5f, 13.8f, 13.8f);
                break;

            case IconKind.Minimize:
                g.DrawLine(pen, 3.5f, 8f, 12.5f, 8f);
                break;

            case IconKind.Maximize:
                g.DrawRectangle(pen, 3.5f, 3.5f, 9f, 9f);
                break;

            case IconKind.Restore:
                g.DrawRectangle(pen, 3.5f, 5.5f, 7f, 7f);
                g.DrawLines(pen, new[]
                {
                    new PointF(5.5f, 3.5f),
                    new PointF(12.5f, 3.5f),
                    new PointF(12.5f, 10.5f),
                    new PointF(10.5f, 10.5f),
                });
                break;

            case IconKind.Copy:
                // Front sheet plus occluded rear sheet, avoiding intersecting strokes.
                g.DrawRectangle(pen, 2.5f, 5.5f, 7.8f, 8.2f);
                g.DrawLines(pen, new[]
                {
                    new PointF(5.5f, 5.5f),
                    new PointF(5.5f, 2.5f),
                    new PointF(13.2f, 2.5f),
                    new PointF(13.2f, 10.5f),
                    new PointF(10.3f, 10.5f),
                });
                break;

            case IconKind.Undo:
                g.DrawArc(pen, 4f, 5f, 9f, 7.5f, -155, 225);
                g.DrawLines(pen, new[] { new PointF(2.5f, 4f), new PointF(3.8f, 6.8f), new PointF(6.5f, 5.4f) });
                break;

            case IconKind.Redo:
                g.DrawArc(pen, 3f, 5f, 9f, 7.5f, -25, -225);
                g.DrawLines(pen, new[] { new PointF(13.5f, 4f), new PointF(12.2f, 6.8f), new PointF(9.5f, 5.4f) });
                break;

            case IconKind.Cut:
                g.DrawEllipse(pen, 2.2f, 9.5f, 3.6f, 3.6f);
                g.DrawEllipse(pen, 10.2f, 9.5f, 3.6f, 3.6f);
                g.DrawLine(pen, 5.2f, 10.2f, 12f, 2.5f);
                g.DrawLine(pen, 10.8f, 10.2f, 4f, 2.5f);
                break;

            case IconKind.Paste:
                g.DrawRectangle(pen, 3f, 3.5f, 10f, 10.5f);
                g.DrawRectangle(pen, 5.5f, 2f, 5f, 2.5f);
                g.DrawLine(pen, 5.5f, 7.5f, 10.5f, 7.5f);
                g.DrawLine(pen, 5.5f, 10.5f, 9.5f, 10.5f);
                break;

            case IconKind.SelectAll:
                g.DrawLines(pen, new[] { new PointF(2.5f, 5f), new PointF(2.5f, 2.5f), new PointF(5f, 2.5f) });
                g.DrawLines(pen, new[] { new PointF(11f, 2.5f), new PointF(13.5f, 2.5f), new PointF(13.5f, 5f) });
                g.DrawLines(pen, new[] { new PointF(2.5f, 11f), new PointF(2.5f, 13.5f), new PointF(5f, 13.5f) });
                g.DrawLines(pen, new[] { new PointF(11f, 13.5f), new PointF(13.5f, 13.5f), new PointF(13.5f, 11f) });
                g.DrawLine(pen, 5.5f, 6.2f, 10.5f, 6.2f);
                g.DrawLine(pen, 5.5f, 9.8f, 10.5f, 9.8f);
                break;

            case IconKind.Delete:
                g.DrawLine(pen, 2.5f, 4.2f, 13.5f, 4.2f);
                g.DrawLine(pen, 6f, 2.4f, 10f, 2.4f);
                g.DrawLines(pen, new[]
                {
                    new PointF(4f, 4.2f),
                    new PointF(4.6f, 13.5f),
                    new PointF(11.4f, 13.5f),
                    new PointF(12f, 4.2f),
                });
                g.DrawLine(pen, 6.8f, 6.8f, 6.8f, 11f);
                g.DrawLine(pen, 9.2f, 6.8f, 9.2f, 11f);
                break;

            case IconKind.Print:
                g.DrawRectangle(pen, 4.2f, 2.2f, 7.6f, 3.6f);
                g.DrawRectangle(pen, 2.2f, 5.8f, 11.6f, 5.4f);
                g.DrawRectangle(pen, 4.2f, 9.5f, 7.6f, 4.3f);
                break;

            case IconKind.Settings:
                g.DrawEllipse(pen, 5.6f, 5.6f, 4.8f, 4.8f);
                for (var i = 0; i < 8; i++)
                {
                    var angle = Math.PI * i / 4;
                    var x1 = 8f + (float)Math.Cos(angle) * 4.7f;
                    var y1 = 8f + (float)Math.Sin(angle) * 4.7f;
                    var x2 = 8f + (float)Math.Cos(angle) * 6.5f;
                    var y2 = 8f + (float)Math.Sin(angle) * 6.5f;
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
                break;

            case IconKind.Hand:
                g.DrawLines(pen, new[]
                {
                    new PointF(4.2f, 8.8f),
                    new PointF(4.2f, 4.8f),
                    new PointF(6f, 4.8f),
                    new PointF(6f, 2.8f),
                    new PointF(8f, 2.8f),
                    new PointF(8f, 7.5f),
                });
                g.DrawLines(pen, new[]
                {
                    new PointF(8f, 3.4f),
                    new PointF(10f, 3.4f),
                    new PointF(10f, 7.8f),
                });
                g.DrawLines(pen, new[]
                {
                    new PointF(10f, 4.5f),
                    new PointF(11.8f, 4.5f),
                    new PointF(11.8f, 11f),
                    new PointF(9.5f, 13.5f),
                    new PointF(5.5f, 13.5f),
                    new PointF(3.2f, 10.2f),
                    new PointF(4.2f, 8.8f),
                });
                break;

            case IconKind.TextCursor:
                g.DrawLine(pen, 5.2f, 2.8f, 10.8f, 2.8f);
                g.DrawLine(pen, 8f, 2.8f, 8f, 13.2f);
                g.DrawLine(pen, 5.2f, 13.2f, 10.8f, 13.2f);
                g.DrawLine(pen, 6.2f, 8f, 9.8f, 8f);
                break;

            case IconKind.Reload:
                g.DrawArc(pen, 3f, 3f, 10f, 10f, -45, 290);
                g.DrawLines(pen, new[] { new PointF(10.5f, 2.6f), new PointF(12.8f, 4.6f), new PointF(10f, 6f) });
                break;

            case IconKind.Info:
                g.DrawEllipse(pen, 2.2f, 2.2f, 11.6f, 11.6f);
                g.DrawLine(pen, 8f, 7.2f, 8f, 11.2f);
                g.FillEllipse(brush, 7.1f, 4.3f, 1.8f, 1.8f);
                break;

            case IconKind.Lock:
                g.DrawRectangle(pen, 3.2f, 7.2f, 9.6f, 6.6f);
                g.DrawArc(pen, 5f, 2.4f, 6f, 7f, 180, 180);
                g.DrawLine(pen, 5f, 5.9f, 5f, 7.2f);
                g.DrawLine(pen, 11f, 5.9f, 11f, 7.2f);
                g.FillEllipse(brush, 7.1f, 9.5f, 1.8f, 1.8f);
                break;

            case IconKind.SinglePage:
                g.DrawRectangle(pen, 4f, 2.2f, 8f, 11.6f);
                g.DrawLine(pen, 6f, 5.5f, 10f, 5.5f);
                g.DrawLine(pen, 6f, 8f, 10f, 8f);
                g.DrawLine(pen, 6f, 10.5f, 8.8f, 10.5f);
                break;

            case IconKind.ContinuousPage:
                g.DrawRectangle(pen, 4.2f, 1.8f, 7.6f, 5.2f);
                g.DrawRectangle(pen, 4.2f, 9f, 7.6f, 5.2f);
                break;

            case IconKind.TwoPages:
                g.DrawRectangle(pen, 1.8f, 3f, 5.6f, 10f);
                g.DrawRectangle(pen, 8.6f, 3f, 5.6f, 10f);
                break;

            case IconKind.InvertColors:
                g.DrawEllipse(pen, 2.5f, 2.5f, 11f, 11f);
                g.FillPie(brush, 2.5f, 2.5f, 11f, 11f, -90, 180);
                break;

            case IconKind.Plus:
                g.DrawLine(pen, 3.5f, 8f, 12.5f, 8f);
                g.DrawLine(pen, 8f, 3.5f, 8f, 12.5f);
                break;

            case IconKind.Document:
                g.DrawLines(pen, new[]
                {
                    new PointF(3.5f, 2f),
                    new PointF(9.8f, 2f),
                    new PointF(12.5f, 4.7f),
                    new PointF(12.5f, 14f),
                    new PointF(3.5f, 14f),
                    new PointF(3.5f, 2f),
                });
                g.DrawLines(pen, new[] { new PointF(9.6f, 2f), new PointF(9.6f, 4.9f), new PointF(12.5f, 4.9f) });
                break;

            case IconKind.Check:
                g.DrawLines(pen, new[] { new PointF(3f, 8.4f), new PointF(6.5f, 11.8f), new PointF(13f, 4.5f) });
                break;
        }

        g.Restore(state);
    }
}

/// <summary>Icons available to custom-painted controls.</summary>
public enum IconKind
{
    None,
    Open,
    ZoomIn,
    ZoomOut,
    FitWidth,
    FitPage,
    RotateLeft,
    RotateRight,
    ThemeDefault,
    ThemeGray,
    ThemeLight,
    Close,
    PreviousPage,
    NextPage,
    ChevronUp,
    ChevronDown,
    Search,
    SidePanel,
    Copy,
    Minimize,
    Maximize,
    Restore,
    Undo,
    Redo,
    Cut,
    Paste,
    SelectAll,
    Delete,
    Print,
    Settings,
    Hand,
    TextCursor,
    Reload,
    Info,
    Lock,
    SinglePage,
    ContinuousPage,
    TwoPages,
    InvertColors,
    Plus,
    Document,
    Check,
}
