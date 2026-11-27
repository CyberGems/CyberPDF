using System.Text.Json;

namespace CyberPDF.Common.Settings;

/// <summary>
/// Allowed highlight color identifiers persisted in <c>annotations.json</c>.
/// The actual RGBA values live per theme in <c>Theme.Palette</c>.
/// </summary>
public static class HighlightColors
{
    public const string Yellow = "yellow";
    public const string Green = "green";
    public const string Blue = "blue";
    public const string Pink = "pink";

    public static readonly string[] All = [Yellow, Green, Blue, Pink];

    public static string Normalize(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return Yellow;
        }

        return color.Trim().ToLowerInvariant() switch
        {
            Green => Green,
            Blue => Blue,
            Pink => Pink,
            _ => Yellow,
        };
    }
}

/// <summary>
/// Highlighted span of text on a PDF page, stored as character indices into
/// PDFium's per-page character table so it scales with zoom and rotation.
/// </summary>
public sealed class TextHighlight
{
    public string Id { get; set; } = string.Empty;

    public int PageIndex { get; set; }

    public int Start { get; set; }

    public int Length { get; set; }

    public string Color { get; set; } = HighlightColors.Yellow;

    public string Snippet { get; set; } = string.Empty;
}

/// <summary>
/// User-created bookmark tied to a specific page of a PDF document.
/// </summary>
public sealed class PersonalBookmark
{
    public int PageIndex { get; set; }

    public string Label { get; set; } = string.Empty;

    public long CreatedUtcTicks { get; set; }
}

/// <summary>
/// Per-document highlights and personal bookmarks.
/// </summary>
public sealed class DocumentAnnotations
{
    public List<TextHighlight> Highlights { get; set; } = [];

    public List<PersonalBookmark> Bookmarks { get; set; } = [];
}

/// <summary>
/// Root container persisted to <c>annotations.json</c>.
/// </summary>
public sealed class AnnotationSet
{
    public Dictionary<string, DocumentAnnotations> Documents { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Reads and writes per-document reading annotations to
/// <c>%LOCALAPPDATA%\CyberPDF\annotations.json</c>. Uses an atomic write-and-replace
/// strategy matching <see cref="SettingsStore"/> so crashes never corrupt user data.
/// </summary>
public sealed class AnnotationStore
{
    private readonly string _path;

    public AnnotationStore()
        : this(Path.Combine(AppPaths.DataDirectory, "annotations.json"))
    {
    }

    public AnnotationStore(string path)
    {
        _path = path;
    }

    public string FilePath => _path;

    public AnnotationSet Load()
    {
        if (!File.Exists(_path))
        {
            return new AnnotationSet();
        }

        try
        {
            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize(json, AnnotationJsonContext.Default.AnnotationSet);
            if (loaded is null)
            {
                return new AnnotationSet();
            }

            // Re-index with case-insensitive keys after deserialization so lookups
            // stay case-insensitive on Windows paths.
            var normalized = new AnnotationSet();
            foreach (var pair in loaded.Documents)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
                {
                    continue;
                }

                var sanitized = SanitizeDocument(pair.Value);
                if (sanitized.Highlights.Count == 0 && sanitized.Bookmarks.Count == 0)
                {
                    continue;
                }

                normalized.Documents[NormalizeDocumentKey(pair.Key)] = sanitized;
            }
            return normalized;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AnnotationSet();
        }
    }

    public void Save(AnnotationSet collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(collection, AnnotationJsonContext.Default.AnnotationSet);
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _path, overwrite: true);
    }

    public static string NormalizeDocumentKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmed = path.Trim();
        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return trimmed;
        }
    }

    public static DocumentAnnotations GetForDocument(AnnotationSet collection, string path)
    {
        ArgumentNullException.ThrowIfNull(collection);
        if (string.IsNullOrWhiteSpace(path))
        {
            return new DocumentAnnotations();
        }

        var key = NormalizeDocumentKey(path);
        if (TryFindDocument(collection, key, out _, out var existing))
        {
            return existing;
        }

        return new DocumentAnnotations();
    }

    public static void SetForDocument(AnnotationSet collection, string path, DocumentAnnotations entry)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var key = NormalizeDocumentKey(path);
        if (TryFindDocument(collection, key, out var existingKey, out _))
        {
            collection.Documents.Remove(existingKey);
        }

        var sanitized = SanitizeDocument(entry);
        if (sanitized.Highlights.Count == 0 && sanitized.Bookmarks.Count == 0)
        {
            return;
        }

        collection.Documents[key] = sanitized;
    }

    private static bool TryFindDocument(
        AnnotationSet collection,
        string normalizedKey,
        out string matchedKey,
        out DocumentAnnotations value)
    {
        if (collection.Documents.TryGetValue(normalizedKey, out var exact) && exact is not null)
        {
            matchedKey = normalizedKey;
            value = exact;
            return true;
        }

        foreach (var pair in collection.Documents)
        {
            if (string.Equals(NormalizeDocumentKey(pair.Key), normalizedKey, StringComparison.OrdinalIgnoreCase) &&
                pair.Value is not null)
            {
                matchedKey = pair.Key;
                value = pair.Value;
                return true;
            }
        }

        matchedKey = string.Empty;
        value = new DocumentAnnotations();
        return false;
    }

    private static DocumentAnnotations SanitizeDocument(DocumentAnnotations source)
    {
        var result = new DocumentAnnotations();

        foreach (var h in source.Highlights)
        {
            if (h is null || h.PageIndex < 0 || h.Start < 0 || h.Length <= 0)
            {
                continue;
            }

            result.Highlights.Add(new TextHighlight
            {
                Id = string.IsNullOrWhiteSpace(h.Id) ? Guid.NewGuid().ToString("N") : h.Id,
                PageIndex = h.PageIndex,
                Start = h.Start,
                Length = h.Length,
                Color = HighlightColors.Normalize(h.Color),
                Snippet = h.Snippet ?? string.Empty,
            });
        }

        var seenPages = new HashSet<int>();
        foreach (var b in source.Bookmarks.OrderBy(item => item.PageIndex))
        {
            if (b is null || b.PageIndex < 0 || !seenPages.Add(b.PageIndex))
            {
                continue;
            }

            result.Bookmarks.Add(new PersonalBookmark
            {
                PageIndex = b.PageIndex,
                Label = b.Label ?? string.Empty,
                CreatedUtcTicks = b.CreatedUtcTicks,
            });
        }

        return result;
    }
}
