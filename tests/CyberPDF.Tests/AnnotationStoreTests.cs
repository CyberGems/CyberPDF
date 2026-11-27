using CyberPDF.Common.Settings;
using Xunit;

namespace CyberPDF.Tests;

public sealed class AnnotationStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public AnnotationStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"cyberpdf-annotations-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "annotations.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenMissing_ReturnsEmptyCollection()
    {
        var store = new AnnotationStore(_filePath);

        var loaded = store.Load();

        Assert.Empty(loaded.Documents);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsHighlightsAndBookmarks()
    {
        var store = new AnnotationStore(_filePath);
        var docPath = Path.Combine(_tempDir, "sample.pdf");
        var collection = new AnnotationSet();

        var entry = new DocumentAnnotations();
        entry.Highlights.Add(new TextHighlight
        {
            Id = "hl-1",
            PageIndex = 2,
            Start = 14,
            Length = 9,
            Color = HighlightColors.Blue,
            Snippet = "important",
        });
        entry.Bookmarks.Add(new PersonalBookmark
        {
            PageIndex = 4,
            Label = "Page 5",
            CreatedUtcTicks = 123456789L,
        });

        AnnotationStore.SetForDocument(collection, docPath, entry);
        store.Save(collection);

        var reloaded = store.Load();
        var loadedEntry = AnnotationStore.GetForDocument(reloaded, docPath.ToUpperInvariant());

        var highlight = Assert.Single(loadedEntry.Highlights);
        Assert.Equal("hl-1", highlight.Id);
        Assert.Equal(2, highlight.PageIndex);
        Assert.Equal(14, highlight.Start);
        Assert.Equal(9, highlight.Length);
        Assert.Equal(HighlightColors.Blue, highlight.Color);
        Assert.Equal("important", highlight.Snippet);

        var bookmark = Assert.Single(loadedEntry.Bookmarks);
        Assert.Equal(4, bookmark.PageIndex);
        Assert.Equal("Page 5", bookmark.Label);
        Assert.Equal(123456789L, bookmark.CreatedUtcTicks);
    }

    [Fact]
    public void HighlightColors_NormalizeFallsBackToYellow()
    {
        Assert.Equal(HighlightColors.Yellow, HighlightColors.Normalize(null));
        Assert.Equal(HighlightColors.Yellow, HighlightColors.Normalize("unknown"));
        Assert.Equal(HighlightColors.Green, HighlightColors.Normalize(" GREEN "));
        Assert.Equal(HighlightColors.Blue, HighlightColors.Normalize("Blue"));
        Assert.Equal(HighlightColors.Pink, HighlightColors.Normalize("pink"));
    }

    [Fact]
    public void SetForDocument_WhenEmpty_PrunesDocumentEntry()
    {
        var collection = new AnnotationSet();
        var docPath = Path.Combine(_tempDir, "prune.pdf");

        var entry = new DocumentAnnotations();
        entry.Bookmarks.Add(new PersonalBookmark { PageIndex = 0, Label = "Page 1" });
        AnnotationStore.SetForDocument(collection, docPath, entry);
        Assert.Single(collection.Documents);

        entry.Bookmarks.Clear();
        AnnotationStore.SetForDocument(collection, docPath, entry);

        Assert.Empty(collection.Documents);
    }

    [Fact]
    public void Load_WhenCorruptedJson_ReturnsEmptyCollection()
    {
        File.WriteAllText(_filePath, "{ not valid json !!!");
        var store = new AnnotationStore(_filePath);

        var loaded = store.Load();

        Assert.Empty(loaded.Documents);
    }
}
