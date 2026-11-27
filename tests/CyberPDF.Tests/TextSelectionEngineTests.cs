using CyberPDF.Core.Documents;
using CyberPDF.Core.Text;
using Xunit;

namespace CyberPDF.Tests;

public sealed class TextSelectionEngineTests
{
    /// <summary>
    /// Builds a two-line layer by hand: line one has five characters, line two
    /// has three, on a 200x100 page.
    /// </summary>
    private static PageTextLayer Layer()
    {
        var characters = new List<TextCharacter>();
        var index = 0;

        // Line 1 at y 80..92, five 10-wide glyphs starting at x=10.
        for (var i = 0; i < 5; i++)
        {
            characters.Add(new TextCharacter(index++, 'A' + i, 10 + i * 10, 20 + i * 10, 80, 92));
        }

        // Line 2 at y 60..72, three glyphs starting at x=10.
        for (var i = 0; i < 3; i++)
        {
            characters.Add(new TextCharacter(index++, 'a' + i, 10 + i * 10, 20 + i * 10, 60, 72));
        }

        return PageTextLayer.Create(characters.ToArray(), new PageSize(200, 100));
    }

    [Fact]
    public void SelectingOneLineProducesASingleRectangle()
    {
        var rects = TextSelectionEngine.BuildHighlightRects(Layer(), new TextSelection(0, 0, 3));

        Assert.Single(rects);
        Assert.Equal(10, rects[0].Left, 3);
        Assert.Equal(40, rects[0].Right, 3);
    }

    [Fact]
    public void SelectingTwoLinesProducesTwoRectangles()
    {
        var rects = TextSelectionEngine.BuildHighlightRects(Layer(), new TextSelection(0, 0, 8));

        Assert.Equal(2, rects.Count);
    }

    [Fact]
    public void EmptySelectionProducesNothing()
    {
        Assert.Empty(TextSelectionEngine.BuildHighlightRects(Layer(), new TextSelection(0, 2, 0)));
        Assert.Equal(string.Empty, TextSelectionEngine.BuildText(Layer(), new TextSelection(0, 2, 0)));
    }

    [Fact]
    public void TextIncludesLineBreaksBetweenLines()
    {
        var text = TextSelectionEngine.BuildText(Layer(), new TextSelection(0, 0, 8));

        // Five from the first line, three from the second.
        Assert.Contains("ABCDE", text);
        Assert.Contains("abc", text);
        Assert.Contains(Environment.NewLine, text);
    }

    [Fact]
    public void SelectionIsClampedToTheLayer()
    {
        var rects = TextSelectionEngine.BuildHighlightRects(Layer(), new TextSelection(0, -5, 500));
        Assert.NotEmpty(rects);
    }

    [Fact]
    public void DoubleClickSelectsTheWholeRunOfCharacters()
    {
        // Every glyph here is a word character, so the whole line is one word.
        var selection = TextSelectionEngine.SelectWordAt(Layer(), 2, pageIndex: 4);

        Assert.Equal(4, selection.PageIndex);
        Assert.Equal(0, selection.Start);
        Assert.Equal(5, selection.Length);
    }

    [Fact]
    public void TripleClickSelectsTheVisualLine()
    {
        var selection = TextSelectionEngine.SelectLineAt(Layer(), 6, pageIndex: 2);

        // Second line starts at index 5 and has three characters.
        Assert.Equal(2, selection.PageIndex);
        Assert.Equal(5, selection.Start);
        Assert.Equal(3, selection.Length);
    }

    [Fact]
    public void ClosestCharacterIndexPicksTheGlyphUnderThePoint()
    {
        var layer = Layer();

        // Center of the third glyph of line one (x 30..40, y 80..92).
        var index = TextSelectionEngine.ClosestCharacterIndex(layer, 35, 86);
        Assert.Equal(2, index);
    }

    [Fact]
    public void ClosestCharacterIndexPastTheEndOfALinePicksThatLine()
    {
        var layer = Layer();

        // Far to the right on line one: must not jump to line two.
        var index = TextSelectionEngine.ClosestCharacterIndex(layer, 190, 86);
        Assert.InRange(index, 0, 4);
    }

    [Fact]
    public void ClosestCharacterIndexOnAnEmptyLayerReturnsMinusOne()
    {
        var empty = PageTextLayer.Empty(new PageSize(200, 100));
        Assert.Equal(-1, TextSelectionEngine.ClosestCharacterIndex(empty, 10, 10));
    }

    [Fact]
    public void SelectionRangesOverlapOnlyOnTheSamePage()
    {
        var a = new TextSelection(0, 0, 10);
        var b = new TextSelection(0, 5, 10);
        var otherPage = new TextSelection(1, 0, 10);

        Assert.True(a.Overlaps(b));
        Assert.False(a.Overlaps(otherPage));
    }
}
