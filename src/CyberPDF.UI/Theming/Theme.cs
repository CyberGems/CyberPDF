using CyberPDF.Common.Settings;

namespace CyberPDF.UI.Theming;

/// <summary>
/// Central palette for the whole app. Every control reads colors from here, so
/// adding a theme means adding one entry instead of hunting for scattered
/// literals. Values follow the suite's three functional schemes: a deep default
/// with the CyberGems teal, a sober gray, and a warm off-white light theme that
/// is never pure white.
/// </summary>
public static class Theme
{
    public record Palette(
        string Bg,
        string Surface,
        string SurfaceAlt,
        string Canvas,
        string PageShadow,
        string Text,
        string TextMuted,
        string Accent,
        string AccentText,
        string Border,
        string BorderStrong,
        string Hover,
        string Pressed,
        string Selection,
        string ScrollThumb,
        string ScrollThumbHover,
        string SearchMatch,
        string SearchMatchActive);

    private static readonly Dictionary<AppTheme, Palette> Palettes = new()
    {
        [AppTheme.Default] = new Palette(
            Bg: "#0A0E14",
            Surface: "#111823",
            SurfaceAlt: "#18212E",
            Canvas: "#080B10",
            PageShadow: "#1AFFFFFF",
            Text: "#EDF3FA",
            TextMuted: "#8A9AAD",
            Accent: "#2AA9B8",
            AccentText: "#04181B",
            Border: "#22303F",
            BorderStrong: "#33465A",
            Hover: "#1B2634",
            Pressed: "#233243",
            Selection: "#332AA9B8",
            ScrollThumb: "#2C3B4C",
            ScrollThumbHover: "#3E536A",
            SearchMatch: "#66E0B341",
            SearchMatchActive: "#B3F2A03D"),

        [AppTheme.Gray] = new Palette(
            Bg: "#151517",
            Surface: "#1D1D20",
            SurfaceAlt: "#252529",
            Canvas: "#121214",
            PageShadow: "#1AFFFFFF",
            Text: "#ECECEF",
            TextMuted: "#9C9CA6",
            Accent: "#B4BBC4",
            AccentText: "#151517",
            Border: "#2E2E34",
            BorderStrong: "#42424A",
            Hover: "#27272C",
            Pressed: "#32323A",
            Selection: "#33B4BBC4",
            ScrollThumb: "#3A3A42",
            ScrollThumbHover: "#4E4E58",
            SearchMatch: "#66C9A227",
            SearchMatchActive: "#B3E0B93A"),

        [AppTheme.Light] = new Palette(
            Bg: "#F4F1EC",
            Surface: "#FBF9F5",
            SurfaceAlt: "#EFEBE4",
            Canvas: "#E8E4DC",
            PageShadow: "#28000000",
            Text: "#1C1A17",
            TextMuted: "#6B655D",
            Accent: "#1F6F7A",
            AccentText: "#FFFFFF",
            Border: "#D8D2C8",
            BorderStrong: "#BFB7AA",
            Hover: "#EDE8E0",
            Pressed: "#E2DCD2",
            Selection: "#331F6F7A",
            ScrollThumb: "#C4BCB0",
            ScrollThumbHover: "#A9A093",
            SearchMatch: "#66E8C547",
            SearchMatchActive: "#B3F2B01E"),
    };

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Default;

    public static Palette Current => Palettes[CurrentTheme];

    public static bool IsDark => CurrentTheme != AppTheme.Light;

    public static event Action? Changed;

    public static void Apply(AppTheme theme)
    {
        if (!Palettes.ContainsKey(theme))
        {
            theme = AppTheme.Default;
        }
        CurrentTheme = theme;
        Changed?.Invoke();
    }

    // Frequently used colors as properties, so call sites read naturally.

    public static Color Bg => FromHex(Current.Bg);

    public static Color Surface => FromHex(Current.Surface);

    public static Color SurfaceAlt => FromHex(Current.SurfaceAlt);

    public static Color Canvas => FromHex(Current.Canvas);

    public static Color Text => FromHex(Current.Text);

    public static Color TextMuted => FromHex(Current.TextMuted);

    public static Color Accent => FromHex(Current.Accent);

    public static Color AccentText => FromHex(Current.AccentText);

    public static Color Border => FromHex(Current.Border);

    public static Color BorderStrong => FromHex(Current.BorderStrong);

    public static Color Hover => FromHex(Current.Hover);

    public static Color Pressed => FromHex(Current.Pressed);

    public static Color Selection => FromHex(Current.Selection);

    public static Color ScrollThumb => FromHex(Current.ScrollThumb);

    public static Color ScrollThumbHover => FromHex(Current.ScrollThumbHover);

    public static Color PageShadow => FromHex(Current.PageShadow);

    /// <summary>Search hit highlight, drawn under the selection.</summary>
    public static Color SearchMatch => FromHex(Current.SearchMatch);

    /// <summary>Highlight for the match the navigation is currently on.</summary>
    public static Color SearchMatchActive => FromHex(Current.SearchMatchActive);

    /// <summary>Paper color. Always white, in every theme: it is the document.</summary>
    public static Color Paper => Color.White;

    private static Color FromHex(string hex)
    {
        var value = hex.TrimStart('#');
        if (value.Length == 8)
        {
            return Color.FromArgb(
                Convert.ToInt32(value.Substring(0, 2), 16),
                Convert.ToInt32(value.Substring(2, 2), 16),
                Convert.ToInt32(value.Substring(4, 2), 16),
                Convert.ToInt32(value.Substring(6, 2), 16));
        }
        if (value.Length == 6)
        {
            return Color.FromArgb(
                Convert.ToInt32(value.Substring(0, 2), 16),
                Convert.ToInt32(value.Substring(2, 2), 16),
                Convert.ToInt32(value.Substring(4, 2), 16));
        }
        throw new FormatException($"Unsupported color literal '{hex}'.");
    }
}
