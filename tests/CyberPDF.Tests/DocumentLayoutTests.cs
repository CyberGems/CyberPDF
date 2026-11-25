using CyberPDF.Core.Rendering;
using Xunit;

namespace CyberPDF.Tests;

public sealed class DocumentLayoutTests
{
    private static readonly int[] FirstThreePages = { 0, 1, 2 };

    [Fact]
    public void SlotsStackVerticallyWithGaps()
    {
        var sizes = new[] { (600f, 800f), (600f, 400f), (600f, 800f) };
        var layout = TestDocuments.Layout(sizes, zoom: 1f, gap: 10, padding: 0);

        Assert.Equal(3, layout.Slots.Count);
        Assert.Equal(0, layout.Slots[0].Top);
        Assert.Equal(800, layout.Slots[0].Height);
        Assert.Equal(810, layout.Slots[1].Top);
        Assert.Equal(1220, layout.Slots[2].Top);
        Assert.Equal(800 + 10 + 400 + 10 + 800, layout.ContentHeight);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void ZoomScalesEverySlot(float zoom)
    {
        var sizes = new[] { (600f, 800f), (595f, 842f) };
        var layout = TestDocuments.Layout(sizes, zoom, gap: 12, padding: 0);

        Assert.Equal((int)Math.Round(600 * zoom), layout.Slots[0].Width);
        Assert.Equal((int)Math.Round(800 * zoom), layout.Slots[0].Height);
        Assert.Equal((int)Math.Round(595 * zoom), layout.Slots[1].Width);
        Assert.Equal((int)Math.Round(842 * zoom), layout.Slots[1].Height);
    }

    [Fact]
    public void PagesAreCenteredInsideTheContentWidth()
    {
        var sizes = new[] { (400f, 600f), (800f, 600f) };
        var layout = TestDocuments.Layout(sizes, zoom: 1f, gap: 0, padding: 0);

        Assert.Equal(800, layout.ContentWidth);
        Assert.Equal(200, layout.LeftOf(layout.Slots[0]));
        Assert.Equal(0, layout.LeftOf(layout.Slots[1]));
    }

    [Fact]
    public void FirstVisiblePageFindsThePageUnderTheViewportTop()
    {
        var sizes = Enumerable.Repeat((600f, 1000f), 5).ToArray();
        var layout = TestDocuments.Layout(sizes, zoom: 1f, gap: 0, padding: 0);

        Assert.Equal(0, layout.FirstVisiblePage(0));
        Assert.Equal(0, layout.FirstVisiblePage(999));
        Assert.Equal(1, layout.FirstVisiblePage(1000));
        Assert.Equal(2, layout.FirstVisiblePage(2500));
        Assert.Equal(4, layout.FirstVisiblePage(4000));
        Assert.Equal(4, layout.FirstVisiblePage(999999));
    }

    [Fact]
    public void VisiblePagesReturnsOnlyIntersectingSlots()
    {
        var sizes = Enumerable.Repeat((600f, 1000f), 10).ToArray();
        var layout = TestDocuments.Layout(sizes, zoom: 1f, gap: 0, padding: 0);

        var visible = layout.VisiblePages(950, 2050, includeMargin: false);
        Assert.Equal(FirstThreePages, visible.Select(s => s.PageIndex).ToArray());
    }

    [Fact]
    public void VisiblePagesKeepsThePageTheViewportStartsInside()
    {
        // Regression: scrolling inside a tall page must keep rendering that page,
        // even though its top edge is far above the viewport.
        var sizes = new[] { (600f, 5000f), (600f, 800f) };
        var layout = TestDocuments.Layout(sizes, zoom: 1f, gap: 0, padding: 0);

        var visible = layout.VisiblePages(2000, 2600, includeMargin: false);
        Assert.Single(visible);
        Assert.Equal(0, visible[0].PageIndex);
    }

    [Fact]
    public void EmptyDocumentProducesEmptyLayout()
    {
        var layout = TestDocuments.Layout(Array.Empty<(float, float)>(), 1f, gap: 10, padding: 0);

        Assert.Empty(layout.Slots);
        Assert.Empty(layout.VisiblePages(0, 1000));
        Assert.Equal(0, layout.FirstVisiblePage(0));
    }

    [Fact]
    public void InvalidPageSizeFallsBackToA4()
    {
        var sizes = new[] { (0f, 0f), (float.NaN, 100f) };
        var layout = TestDocuments.Layout(sizes, zoom: 1f, gap: 0, padding: 0);

        Assert.Equal(595, layout.Slots[0].Width);
        Assert.Equal(842, layout.Slots[0].Height);
        Assert.Equal(595, layout.Slots[1].Width);
    }

    [Fact]
    public void NonPositiveZoomIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TestDocuments.Layout(new[] { (600f, 800f) }, 0f, gap: 10, padding: 0));
    }
}
