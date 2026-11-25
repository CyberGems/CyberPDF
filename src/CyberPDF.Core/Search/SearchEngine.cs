using CyberPDF.Core.Text;

namespace CyberPDF.Core.Search;

/// <summary>One match of a search term inside the document.</summary>
/// <param name="PageIndex">Page the match is on.</param>
/// <param name="Start">First character index in that page's text layer.</param>
/// <param name="Length">Number of characters in the match.</param>
public readonly record struct SearchMatch(int PageIndex, int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>Options for a search run.</summary>
public sealed class SearchOptions
{
    public bool MatchCase { get; set; }

    public bool WholeWord { get; set; }
}

/// <summary>
/// Finds text inside a page's text layer.
/// </summary>
/// <remarks>
/// Matching runs over a page text whose offsets are translated back to character
/// indices through <see cref="SearchableText"/>: without that translation, a page
/// containing an emoji or a glyph PDFium cannot map would highlight the wrong
/// characters. The engine never touches PDFium, so a search cannot block the
/// render thread.
/// </remarks>
public static class SearchEngine
{
    /// <summary>
    /// Finds every match of <paramref name="term"/> on one page. Returned offsets
    /// are character indices, ready for selection and highlighting.
    /// </summary>
    public static List<SearchMatch> FindInPage(PageTextLayer layer, int pageIndex, string term, SearchOptions? options = null)
    {
        var results = new List<SearchMatch>();
        if (layer.IsEmpty || string.IsNullOrEmpty(term))
        {
            return results;
        }

        return FindInSearchableText(SearchableText.FromLayer(layer), pageIndex, term, options);
    }

    /// <summary>
    /// Same as <see cref="FindInPage"/> but reuses an already prepared text, which
    /// is what a live search needs while the user types.
    /// </summary>
    public static List<SearchMatch> FindInSearchableText(
        SearchableText searchable,
        int pageIndex,
        string term,
        SearchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(searchable);

        var results = new List<SearchMatch>();
        if (searchable.IsEmpty || string.IsNullOrEmpty(term))
        {
            return results;
        }

        options ??= new SearchOptions();
        var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var text = searchable.Text;

        var from = 0;
        while (from <= text.Length - term.Length)
        {
            var offset = text.IndexOf(term, from, comparison);
            if (offset < 0)
            {
                break;
            }

            if (!options.WholeWord || IsWholeWord(text, offset, term.Length))
            {
                results.Add(new SearchMatch(
                    pageIndex,
                    searchable.CharacterAt(offset),
                    searchable.CharacterSpan(offset, term.Length)));
            }

            // Advancing by one, not by the term length, keeps overlapping
            // occurrences findable ("aa" in "aaa").
            from = offset + 1;
        }

        return results;
    }

    /// <summary>True when the range is not glued to a letter or digit on either side.</summary>
    private static bool IsWholeWord(string text, int start, int length)
    {
        var before = start > 0 ? text[start - 1] : ' ';
        var afterIndex = start + length;
        var after = afterIndex < text.Length ? text[afterIndex] : ' ';
        return !IsWordCharacter(before) && !IsWordCharacter(after);
    }

    private static bool IsWordCharacter(char value)
        => char.IsLetterOrDigit(value) || value == '_';

    /// <summary>Orders matches for navigation: by page, then by position on the page.</summary>
    public static List<SearchMatch> Sort(IEnumerable<SearchMatch> matches)
        => matches
            .OrderBy(m => m.PageIndex)
            .ThenBy(m => m.Start)
            .ToList();
}
