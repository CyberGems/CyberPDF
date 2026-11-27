using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CyberPDF.Common.I18n;

namespace CyberPDF.Common.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON under the user's local
/// application data folder. Reads never throw: a corrupt or unreadable file
/// falls back to defaults so the app always starts.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // Keep accents readable in the file instead of escaping them.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = SettingsJsonContext.Default,
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        TypeInfoResolver = SettingsJsonContext.Default,
    };

    // Resolve the generated metadata once. Serializing through JsonTypeInfo is
    // what keeps the trimmer and the AOT compiler able to see every property;
    // the generic overloads are reflection based and warn under both analyzers.
    private static readonly JsonTypeInfo<AppSettings> WriteInfo =
        (JsonTypeInfo<AppSettings>)WriteOptions.GetTypeInfo(typeof(AppSettings));

    private static readonly JsonTypeInfo<AppSettings> ReadInfo =
        (JsonTypeInfo<AppSettings>)ReadOptions.GetTypeInfo(typeof(AppSettings));

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(AppPaths.DataDirectory, "settings.json");
    }

    public string FilePath { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }
            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppSettings();
            }
            var settings = JsonSerializer.Deserialize(json, ReadInfo) ?? new AppSettings();
            Sanitize(settings);
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to a temporary file and replace, so a crash mid-write cannot
            // leave a truncated settings file behind.
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, WriteInfo));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing settings is not worth interrupting the reader.
        }
    }

    private static void Sanitize(AppSettings settings)
    {
        if (settings.RenderCacheBudgetMb is < 32 or > 2048)
        {
            settings.RenderCacheBudgetMb = 64;
        }
        settings.HighlightColor = HighlightColors.Normalize(settings.HighlightColor);
        settings.RecentFiles ??= new List<string>();
        if (settings.RecentFiles.Count > AppSettings.MaxRecentFiles)
        {
            settings.RecentFiles.RemoveRange(AppSettings.MaxRecentFiles, settings.RecentFiles.Count - AppSettings.MaxRecentFiles);
        }
        settings.ViewStates ??= new Dictionary<string, DocumentViewState>(StringComparer.OrdinalIgnoreCase);

        // A width outside the allowed range, or a tab that does not exist, comes
        // from a hand-edited file or an older build; fall back to the defaults
        // instead of opening a panel of zero width.
        if (settings.SidePanelWidth < AppSettings.MinSidePanelWidth
            || settings.SidePanelWidth > AppSettings.MaxSidePanelWidth)
        {
            settings.SidePanelWidth = 190;
        }
        if (settings.SidePanelTab is < 0 or > 1)
        {
            settings.SidePanelTab = 0;
        }
    }

    /// <summary>Maps the stored language to a supported value, following Windows when unset.</summary>
    public static Lang ResolveLanguage(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Language))
        {
            return settings.Language.Trim().ToLowerInvariant() switch
            {
                "es" or "spa" or "spanish" or "español" => Lang.Es,
                "en" or "eng" or "english" or "inglés" => Lang.En,
                _ => Lang.Es,
            };
        }

        // Unset means follow the OS. Spanish for any Spanish locale, English for
        // everything else: the suite ships both languages and English is the
        // safer default for anyone outside a Spanish Windows.
        var culture = System.Globalization.CultureInfo.CurrentUICulture;
        return culture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase)
            ? Lang.Es
            : Lang.En;
    }
}
