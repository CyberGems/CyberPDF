using System.Globalization;
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
        sb.AppendLine(CultureInfo.InvariantCulture, $"  language         : {Strings.Current}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  theme            : {Theme.CurrentTheme}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  form dpi         : {window.DeviceDpi}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  form size        : {window.Width}x{window.Height} (client {window.ClientSize.Width}x{window.ClientSize.Height})");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  viewer dpi       : {info.ViewerDpi}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  viewer client    : {info.ViewerClientWidth}x{info.ViewerClientHeight}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  document         : {info.PageCount} page(s)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  page 0 size      : {info.FirstPageWidth:F1} x {info.FirstPageHeight:F1} pt");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  zoom             : {info.Zoom:P0}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  layout slot 0    : {info.SlotWidth}x{info.SlotHeight} at top {info.SlotTop}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  layout content   : {info.ContentWidth} x {info.ContentHeight}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  scrollbar        : max {info.ScrollMax} value {info.ScrollValue} change {info.ScrollChange}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  page raster      : {info.RequestedWidth}x{info.RequestedHeight} px");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  render bands     : {info.TileCount} band(s) of max {info.TileHeight} px");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  scrollbar bounds : {info.ScrollBounds}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  chrome           : title {info.TitleBarBounds} toolbar {info.ToolbarBounds}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"                     search {info.SearchBarBounds} status {info.StatusBarBounds} parent '{info.SearchBarParent}'");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  viewer bounds    : {info.ViewerBounds}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  held bands       : {info.HeldTileCount} ({info.HeldTileMb:N1} MB of pixels)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  cache            : {info.CacheCount} entry(ies), {info.CacheMb:N1} MB of {info.CacheBudgetMb} MB");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  process          : private {info.PrivateMb:N1} MB, working set {info.WorkingSetMb:N1} MB");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  status text      : '{info.StatusText}'");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  title text       : '{info.TitleText}'");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  band 0 size      : {info.RequestedWidth}x{info.FirstBandHeight} px");
        foreach (var button in info.Buttons)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  button           : '{button.Text}' {button.Width}x{button.Height} enabled={button.Enabled}");
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
    Rectangle TitleBarBounds,
    Rectangle ToolbarBounds,
    Rectangle SearchBarBounds,
    Rectangle StatusBarBounds,
    Rectangle ViewerBounds,
    string SearchBarParent,
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
