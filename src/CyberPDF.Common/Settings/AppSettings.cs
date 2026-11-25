namespace CyberPDF.Common.Settings;

/// <summary>Interface themes, matching the suite's functional scheme.</summary>
public enum AppTheme
{
    /// <summary>Default: deep neutral backgrounds with the CyberGems teal accent.</summary>
    Default,

    /// <summary>Gray: sober desaturated grays, branding excepted.</summary>
    Gray,

    /// <summary>Light: warm off-white, never pure white.</summary>
    Light,
}

/// <summary>
/// User options persisted between sessions. Deliberately a plain mutable class:
/// it is serialized as JSON and small enough that a record with init-only
/// members would only complicate the loader.
/// </summary>
public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Default;

    /// <summary>UI language. Null means "follow Windows".</summary>
    public string? Language { get; set; }

    public bool RestoreSession { get; set; } = true;

    public bool RememberPagePosition { get; set; } = true;

    /// <summary>
    /// Upper bound for rendered bitmap memory, in megabytes. Sized for a reader:
    /// enough for the visible pages at high zoom plus prefetch, without turning
    /// into the kind of memory hog this app exists to avoid.
    /// </summary>
    public int RenderCacheBudgetMb { get; set; } = 64;

    /// <summary>Paths of recently opened documents, most recent first.</summary>
    public List<string> RecentFiles { get; set; } = new();

    /// <summary>Per-document view state, keyed by normalized full path.</summary>
    public Dictionary<string, DocumentViewState> ViewStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public const int MaxRecentFiles = 12;
}

/// <summary>Last position inside a document, restored when it is reopened.</summary>
public sealed class DocumentViewState
{
    public int Page { get; set; }

    /// <summary>Zoom factor where 1.0 is 100%.</summary>
    public float Zoom { get; set; } = 1f;

    /// <summary>Rotation in degrees, always a multiple of 90.</summary>
    public int Rotation { get; set; }
}
