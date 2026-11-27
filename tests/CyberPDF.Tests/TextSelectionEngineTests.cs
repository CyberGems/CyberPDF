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

    [Fact]
    public void ClosestCharacterIndexPastShortLineStaysOnThatLineOverWiderLineBelow()
    {
        var chars = new List<TextCharacter>();
        var idx = 0;
        // Short line 1: x=10..40, y=80..92
        for (var i = 0; i < 3; i++)
        {
            chars.Add(new TextCharacter(idx++, 'A' + i, 10 + i * 10, 20 + i * 10, 80, 92));
        }
        // Wide line 2: x=10..180, y=62..74 (closer in raw Euclidean distance to x=150, y=79 than line 1 at x=40!)
        for (var i = 0; i < 17; i++)
        {
            chars.Add(new TextCharacter(idx++, 'a' + i, 10 + i * 10, 20 + i * 10, 62, 74));
        }
        var layer = PageTextLayer.Create(chars.ToArray(), new PageSize(200, 100));

        // Pointer is within line 1's vertical span (y=85) at x=150: must hit line 1's last char (index 2).
        var onLine = TextSelectionEngine.ClosestCharacterIndex(layer, 150, 85);
        Assert.Equal(2, onLine);

        // Pointer slightly below line 1's bottom (y=79) at x=45: line 1 is closer vertically than line 2 (y=74), stay on line 1.
        var nearLine1 = TextSelectionEngine.ClosestCharacterIndex(layer, 45, 79);
        Assert.Equal(2, nearLine1);
    }

    [Fact]
    public void ClosestCharacterIndexReturnsMinusOneBeyondMaxDistance()
    {
        var layer = Layer();
        var missed = TextSelectionEngine.ClosestCharacterIndex(layer, 150, 20, maxDistance: 24);
        Assert.Equal(-1, missed);
    }

    [Fact]
    public void BuildHighlightRectsCoversOutOfOrderLeftCoordinateOnSameLine()
    {
        var chars = new[]
        {
            new TextCharacter(0, 'B', 30, 40, 80, 92),
            new TextCharacter(1, 'A', 15, 25, 80, 92),
        };
        var layer = PageTextLayer.Create(chars, new PageSize(200, 100));

        var rects = TextSelectionEngine.BuildHighlightRects(layer, new TextSelection(0, 0, 2));

        var rect = Assert.Single(rects);
        Assert.Equal(15, rect.Left, 3);
        Assert.Equal(40, rect.Right, 3);
    }

    [Fact]
    public void DocumentSelectionNormalizesForwardAndReverseOrder()
    {
        var forward = DocumentSelection.Create(0, 2, 2, 4);
        var reversePages = DocumentSelection.Create(2, 4, 0, 2);
        var reverseSamePage = DocumentSelection.Create(1, 9, 1, 3);

        Assert.Equal(forward, reversePages);
        Assert.Equal(1, reverseSamePage.StartPage);
        Assert.Equal(3, reverseSamePage.StartChar);
        Assert.Equal(9, reverseSamePage.EndChar);
    }

    [Fact]
    public void DocumentSelectionForPageSlicesStartMiddleAndEnd()
    {
        var docSel = DocumentSelection.Create(1, 3, 3, 2);

        Assert.False(docSel.ContainsPage(0));
        Assert.False(docSel.ContainsPage(4));
        Assert.Equal(default, docSel.ForPage(0, 10));
        Assert.Equal(new TextSelection(1, 3, 7), docSel.ForPage(1, 10));
        Assert.Equal(new TextSelection(2, 0, 8), docSel.ForPage(2, 8));
        Assert.Equal(new TextSelection(3, 0, 3), docSel.ForPage(3, 10));
    }

    [Fact]
    public void BuildMultiPageTextAndSliceByPageSkipEmptyMiddlePage()
    {
        var page0 = PageTextLayer.Create(
            new[]
            {
                new TextCharacter(0, 'O', 10, 20, 80, 92),
                new TextCharacter(1, 'n', 20, 30, 80, 92),
                new TextCharacter(2, 'e', 30, 40, 80, 92),
            },
            new PageSize(200, 100));
        var emptyPage1 = PageTextLayer.Empty(new PageSize(200, 100));
        var page2 = PageTextLayer.Create(
            new[]
            {
                new TextCharacter(0, 'T', 10, 20, 80, 92),
                new TextCharacter(1, 'w', 20, 30, 80, 92),
                new TextCharacter(2, 'o', 30, 40, 80, 92),
            },
            new PageSize(200, 100));

        PageTextLayer? Lookup(int p) => p switch
        {
            0 => page0,
            1 => emptyPage1,
            2 => page2,
            _ => null,
        };

        var selection = DocumentSelection.Create(0, 1, 2, 1);
        var slices = TextSelectionEngine.SliceByPage(selection, Lookup);
        var text = TextSelectionEngine.BuildMultiPageText(selection, Lookup);

        Assert.Equal(2, slices.Count);
        Assert.Equal(new TextSelection(0, 1, 2), slices[0]);
        Assert.Equal(new TextSelection(2, 0, 2), slices[1]);
        Assert.Equal("ne" + Environment.NewLine + Environment.NewLine + "Tw", text);
    }
}
