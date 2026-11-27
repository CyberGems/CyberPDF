namespace CyberPDF.Common.Shell;

/// <summary>
/// Formats the system tray tooltip according to the suite convention:
/// <c>CyberPDF</c> or <c>CyberPDF · &lt;segment&gt;</c>, capped at 63
/// characters with a trailing ellipsis so Windows never truncates mid-word.
/// </summary>
public static class TrayTooltipFormatter
{
    public const string AppName = "CyberPDF";
    public const int MaxLength = 63;

    public static string Format(string? segment = null)
    {
        var clean = segment?.Trim();
        if (string.IsNullOrEmpty(clean))
        {
            return AppName;
        }

        var full = $"{AppName} · {clean}";
        if (full.Length <= MaxLength)
        {
            return full;
        }

        return string.Concat(full.AsSpan(0, MaxLength - 1), "…");
    }
}
