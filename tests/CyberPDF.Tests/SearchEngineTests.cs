using CyberPDF.Core.Documents;
using CyberPDF.Core.Search;
using CyberPDF.Core.Text;
using Xunit;

namespace CyberPDF.Tests;

public sealed class SearchEngineTests
{
    private static readonly int[] TheOffsets = { 0, 12, 24 };

    private static PageTextLayer LayerOf(string text)
    {
        var characters = new List<TextCharacter>();
        for (var i = 0; i < text.Length; i++)
        {
            characters.Add(new TextCharacter(i, text[i], i * 10, i * 10 + 9, 80, 92));
        }
        return PageTextLayer.Create(characters.ToArray(), new PageSize(500, 100));
    }

    [Fact]
    public void FindsEveryOccurrenceOnAPage()
    {
        var matches = SearchEngine.FindInPage(LayerOf("the cat and the dog and the bird"), 0, "the");

        Assert.Equal(3, matches.Count);
        Assert.Equal(TheOffsets, matches.Select(m => m.Start).ToArray());
    }

    [Fact]
    public void MatchIsCaseInsensitiveByDefault()
    {
        var matches = SearchEngine.FindInPage(LayerOf("PDF pdf Pdf"), 0, "pdf");
        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void MatchCaseHonoursCapitalization()
    {
        var options = new SearchOptions { MatchCase = true };
        var matches = SearchEngine.FindInPage(LayerOf("PDF pdf Pdf"), 0, "PDF", options);

        Assert.Single(matches);
        Assert.Equal(0, matches[0].Start);
    }

    [Fact]
    public void OverlappingOccurrencesAreAllFound()
    {
        // "aa" appears at offsets 0, 1 and 2 in "aaaa".
        var matches = SearchEngine.FindInPage(LayerOf("aaaa"), 0, "aa");
        Assert.Equal(3, matches.Count);
    }

    [Fact]
    public void WholeWordSkipsMatchesInsideLargerWords()
    {
        var options = new SearchOptions { WholeWord = true };
        var matches = SearchEngine.FindInPage(LayerOf("cat concatenate cat"), 0, "cat", options);

        Assert.Equal(2, matches.Count);
        Assert.Equal(0, matches[0].Start);
        Assert.Equal(16, matches[1].Start);
    }

    [Fact]
    public void EmptyTermFindsNothing()
    {
        Assert.Empty(SearchEngine.FindInPage(LayerOf("some text"), 0, string.Empty));
    }

    [Fact]
    public void EmptyLayerFindsNothing()
    {
        var empty = PageTextLayer.Empty(new PageSize(500, 100));
        Assert.Empty(SearchEngine.FindInPage(empty, 0, "anything"));
    }

    [Fact]
    public void MatchLengthIsTermLengthInCharacters()
    {
        var matches = SearchEngine.FindInPage(LayerOf("hello world"), 0, "world");

        Assert.Single(matches);
        Assert.Equal(6, matches[0].Start);
        Assert.Equal(5, matches[0].Length);
    }

    [Fact]
    public void SortingOrdersByPageThenPosition()
    {
        var unsorted = new[]
        {
            new SearchMatch(2, 50, 3),
            new SearchMatch(0, 90, 3),
            new SearchMatch(0, 10, 3),
            new SearchMatch(1, 5, 3),
        };

        var sorted = SearchEngine.Sort(unsorted);

        Assert.Equal(0, sorted[0].PageIndex);
        Assert.Equal(10, sorted[0].Start);
        Assert.Equal(0, sorted[1].PageIndex);
        Assert.Equal(90, sorted[1].Start);
        Assert.Equal(1, sorted[2].PageIndex);
        Assert.Equal(2, sorted[3].PageIndex);
    }

    [Fact]
    public void OffsetsAreTranslatedForPagesWithUnmappedGlyphs()
    {
        // A glyph without Unicode is skipped from the searchable text, so a raw
        // string offset would be one short of the real character index.
        var characters = new[]
        {
            new TextCharacter(0, 0, 0, 0, 0, 0),
            new TextCharacter(1, 'a', 10, 20, 80, 92),
            new TextCharacter(2, 'b', 20, 30, 80, 92),
            new TextCharacter(3, 'c', 30, 40, 80, 92),
        };
        var layer = PageTextLayer.Create(characters, new PageSize(500, 100));

        var matches = SearchEngine.FindInPage(layer, 0, "bc");

        Assert.Single(matches);
        Assert.Equal(2, matches[0].Start);
    }

    [Fact]
    public void OffsetsAreTranslatedForSurrogatePairs()
    {
        // An emoji occupies two UTF-16 units but one character in the layer, so
        // the match after it must start at character 1, not at offset 2.
        var characters = new[]
        {
            new TextCharacter(0, 0x1F600, 10, 30, 80, 92),
            new TextCharacter(1, 'a', 30, 40, 80, 92),
            new TextCharacter(2, 'b', 40, 50, 80, 92),
        };
        var layer = PageTextLayer.Create(characters, new PageSize(500, 100));

        var matches = SearchEngine.FindInPage(layer, 0, "ab");

        Assert.Single(matches);
        Assert.Equal(1, matches[0].Start);
        Assert.Equal(2, matches[0].Length);
    }
}
