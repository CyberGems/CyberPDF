using System.Text;
using CyberPDF.Common.I18n;
using CyberPDF.Common.Settings;
using CyberPDF.Core.Rendering;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI;

/// <summary>
/// Hidden diagnostics mode (<c>CyberPDF.exe --diag file.pdf</c>). Prints the
/// metrics that decide layout correctness: DPI, client size, page geometry and
/// the computed scroll content. Used to verify a build on a machine instead of
/// guessing why a page looks wrong.
/// </summary>
internal static class DiagnosticsProbe
{
    public static bool IsRequested(string[] args)
        => args.Any(a => a.Equals("--diag", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Optional file the report is also written to, from <c>--diag-out path</c>.
    /// </summary>
    public static string? OutputFile(string[] args)
    {
        var index = Array.FindIndex(args, a => a.Equals("--diag-out", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length)
        {
            return null;
        }
        return args[index + 1];
    }

    public static string? TargetFile(string[] args)
    {
        var index = Array.FindIndex(args, a => a.Equals("--diag", StringComparison.OrdinalIgnoreCase));
        for (var i = index + 1; i < args.Length; i++)
        {
            if (File.Exists(args[i]))
            {
                return args[i];
            }
        }
        return null;
    }

    /// <summary>
    /// Builds the report after the window is shown, so every DPI-dependent value
    /// is the one the app actually uses.
    /// </summary>
    public static string Build(MainWindow window, PdfDocumentInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CyberPDF diagnostics");
        sb.AppendLine($"  language         : {Strings.Current}");
        sb.AppendLine($"  theme            : {Theme.CurrentTheme}");
        sb.AppendLine($"  form dpi         : {window.DeviceDpi}");
        sb.AppendLine($"  form size        : {window.Width}x{window.Height} (client {window.ClientSize.Width}x{window.ClientSize.Height})");
        sb.AppendLine($"  viewer dpi       : {info.ViewerDpi}");
        sb.AppendLine($"  viewer client    : {info.ViewerClientWidth}x{info.ViewerClientHeight}");
        sb.AppendLine($"  document         : {info.PageCount} page(s)");
        sb.AppendLine($"  page 0 size      : {info.FirstPageWidth:F1} x {info.FirstPageHeight:F1} pt");
        sb.AppendLine($"  zoom             : {info.Zoom:P0}");
        sb.AppendLine($"  layout slot 0    : {info.SlotWidth}x{info.SlotHeight} at top {info.SlotTop}");
        sb.AppendLine($"  layout content   : {info.ContentWidth} x {info.ContentHeight}");
        sb.AppendLine($"  scrollbar        : max {info.ScrollMax} value {info.ScrollValue} change {info.ScrollChange}");
        sb.AppendLine($"  page raster      : {info.RequestedWidth}x{info.RequestedHeight} px");
        sb.AppendLine($"  render bands     : {info.TileCount} band(s) of max {info.TileHeight} px");
        sb.AppendLine($"  scrollbar bounds : {info.ScrollBounds}");
        sb.AppendLine($"  held bands       : {info.HeldTileCount} ({info.HeldTileMb:N1} MB of pixels)");
        sb.AppendLine($"  cache            : {info.CacheCount} entry(ies), {info.CacheMb:N1} MB of {info.CacheBudgetMb} MB");
        sb.AppendLine($"  process          : private {info.PrivateMb:N1} MB, working set {info.WorkingSetMb:N1} MB");
        sb.AppendLine($"  status text      : '{info.StatusText}'");
        sb.AppendLine($"  title text       : '{info.TitleText}'");
        sb.AppendLine($"  band 0 size      : {info.RequestedWidth}x{info.FirstBandHeight} px");
        foreach (var button in info.Buttons)
        {
            sb.AppendLine($"  button           : '{button.Text}' {button.Width}x{button.Height} enabled={button.Enabled}");
        }
        return sb.ToString();
    }
}

/// <summary>Snapshot of the metrics the diagnostics report prints.</summary>
internal sealed record PdfDocumentInfo(
    int PageCount,
    float FirstPageWidth,
    float FirstPageHeight,
    float Zoom,
    int SlotWidth,
    int SlotHeight,
    int SlotTop,
    int ContentWidth,
    int ContentHeight,
    int ViewerDpi,
    int ViewerClientWidth,
    int ViewerClientHeight,
    int ScrollMax,
    int ScrollValue,
    int ScrollChange,
    int RequestedWidth,
    int RequestedHeight,
    int TileCount,
    int TileHeight,
    int FirstBandHeight,
    Rectangle ScrollBounds,
    string StatusText,
    string TitleText,
    int HeldTileCount,
    double HeldTileMb,
    int CacheCount,
    double CacheMb,
    int CacheBudgetMb,
    double PrivateMb,
    double WorkingSetMb,
    IReadOnlyList<(string Text, int Width, int Height, bool Enabled)> Buttons);
