using System.Drawing.Drawing2D;

namespace CyberPDF.UI.Theming;

/// <summary>
/// Vector icons drawn with GDI+, so the executable carries no image resources
/// and starts without decoding anything. Each icon is drawn inside a square of
/// the given size and inherits the color it is given.
/// </summary>
public static class Icons
{
    public static void Draw(Graphics g, IconKind kind, Rectangle bounds, Color color, float stroke = 1.6f)
    {
        using var pen = new Pen(color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };
        using var brush = new SolidBrush(color);

        // Work in a normalized 16x16 space and scale, so every glyph has the
        // same optical weight regardless of the target size.
        var state = g.Save();
        g.TranslateTransform(bounds.X, bounds.Y);
        g.ScaleTransform(bounds.Width / 16f, bounds.Height / 16f);

        switch (kind)
        {
            case IconKind.Open:
                g.DrawLines(pen, new[] { new PointF(2, 12), new PointF(2, 4), new PointF(6.5f, 4), new PointF(8, 6), new PointF(13.5f, 6) });
                g.DrawRectangle(pen, 2.5f, 6.5f, 11f, 6.5f);
                break;

            case IconKind.ZoomIn:
                g.DrawEllipse(pen, 2.5f, 2.5f, 8f, 8f);
                g.DrawLine(pen, 10.2f, 10.2f, 14f, 14f);
                g.DrawLine(pen, 5.2f, 6.5f, 7.8f, 6.5f);
                g.DrawLine(pen, 6.5f, 5.2f, 6.5f, 7.8f);
                break;

            case IconKind.ZoomOut:
                g.DrawEllipse(pen, 2.5f, 2.5f, 8f, 8f);
                g.DrawLine(pen, 10.2f, 10.2f, 14f, 14f);
                g.DrawLine(pen, 4.6f, 6.5f, 8.4f, 6.5f);
                break;

            case IconKind.FitWidth:
                g.DrawLine(pen, 2, 3.5f, 14, 3.5f);
                g.DrawLine(pen, 2, 12.5f, 14, 12.5f);
                g.DrawLine(pen, 8, 5.5f, 8, 10.5f);
                g.DrawLines(pen, new[] { new PointF(5.5f, 7.5f), new PointF(8, 5.5f), new PointF(10.5f, 7.5f) });
                g.DrawLines(pen, new[] { new PointF(5.5f, 8.5f), new PointF(8, 10.5f), new PointF(10.5f, 8.5f) });
                break;

            case IconKind.FitPage:
                g.DrawRectangle(pen, 4.5f, 1.5f, 7f, 13f);
                break;

            case IconKind.RotateLeft:
                g.DrawArc(pen, 3f, 3f, 10f, 10f, 130, 250);
                g.DrawLines(pen, new[] { new PointF(2.2f, 6.5f), new PointF(3f, 3.2f), new PointF(6.4f, 3.6f) });
                break;

            case IconKind.RotateRight:
                g.DrawArc(pen, 3f, 3f, 10f, 10f, 50, -250);
                g.DrawLines(pen, new[] { new PointF(13.8f, 6.5f), new PointF(13f, 3.2f), new PointF(9.6f, 3.6f) });
                break;

            case IconKind.ThemeDefault:
                g.FillEllipse(brush, 3f, 3f, 10f, 10f);
                break;

            case IconKind.ThemeGray:
                g.DrawEllipse(pen, 2.5f, 2.5f, 11f, 11f);
                using (var half = new SolidBrush(color))
                {
                    g.FillPie(half, 2.5f, 2.5f, 11f, 11f, 90, 180);
                }
                break;

            case IconKind.ThemeLight:
                g.DrawEllipse(pen, 5.2f, 5.2f, 5.6f, 5.6f);
                for (var i = 0; i < 8; i++)
                {
                    var angle = Math.PI * i / 4;
                    var x1 = 8 + (float)Math.Cos(angle) * 5.4f;
                    var y1 = 8 + (float)Math.Sin(angle) * 5.4f;
                    var x2 = 8 + (float)Math.Cos(angle) * 7f;
                    var y2 = 8 + (float)Math.Sin(angle) * 7f;
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
                break;

            case IconKind.Close:
                g.DrawLine(pen, 4, 4, 12, 12);
                g.DrawLine(pen, 12, 4, 4, 12);
                break;

            case IconKind.PreviousPage:
                g.DrawLines(pen, new[] { new PointF(10, 3.5f), new PointF(5, 8), new PointF(10, 12.5f) });
                break;

            case IconKind.NextPage:
                g.DrawLines(pen, new[] { new PointF(6, 3.5f), new PointF(11, 8), new PointF(6, 12.5f) });
                break;

            case IconKind.Search:
                g.DrawEllipse(pen, 2.5f, 2.5f, 8f, 8f);
                g.DrawLine(pen, 10.2f, 10.2f, 14f, 14f);
                break;

            case IconKind.Copy:
                // Two offset sheets, the usual "copy" metaphor.
                g.DrawRectangle(pen, 2, 2, 8f, 8f);
                g.DrawRectangle(pen, 6, 6, 8f, 8f);
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
    Search,
    Copy,
}
