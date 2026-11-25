using System.Text;

namespace CyberPDF.Core.Text;

/// <summary>
/// A page's text prepared for searching: the string that matching runs over,
/// plus the mapping back to character indices.
/// </summary>
/// <remarks>
/// The mapping is not optional. <see cref="PageTextLayer.Text"/> skips glyphs
/// PDFium reports without a Unicode value and stores characters outside the BMP
/// as two UTF-16 units, so a string offset and a character index drift apart.
/// Selection and highlighting work in character indices, so a match found at a
/// string offset has to be translated before it can be highlighted.
/// </remarks>
public sealed class SearchableText
{
    private readonly int[] _characterByOffset;

    private SearchableText(string text, int[] characterByOffset)
    {
        Text = text;
        _characterByOffset = characterByOffset;
    }

    /// <summary>The text matching runs over.</summary>
    public string Text { get; }

    public bool IsEmpty => Text.Length == 0;

    /// <summary>
    /// Character index for a string offset. Returns the last character for an
    /// offset past the end, so callers cannot read out of range.
    /// </summary>
    public int CharacterAt(int offset)
    {
        if (_characterByOffset.Length == 0)
        {
            return 0;
        }
        var index = Math.Clamp(offset, 0, _characterByOffset.Length - 1);
        return _characterByOffset[index];
    }

    /// <summary>
    /// Number of characters covered by a string range starting at
    /// <paramref name="offset"/> with <paramref name="length"/> units. A range
    /// that starts and ends inside one surrogate pair still counts as one
    /// character, never zero.
    /// </summary>
    public int CharacterSpan(int offset, int length)
    {
        if (_characterByOffset.Length == 0 || length <= 0)
        {
            return 0;
        }

        var first = CharacterAt(offset);
        var lastOffset = Math.Clamp(offset + length - 1, 0, _characterByOffset.Length - 1);
        var last = _characterByOffset[lastOffset];
        return Math.Max(1, last - first + 1);
    }

    /// <summary>Builds the searchable form of a page's text layer.</summary>
    public static SearchableText FromLayer(PageTextLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);

        var builder = new StringBuilder(layer.Characters.Count + 32);
        var map = new List<int>(layer.Characters.Count + 32);

        foreach (var character in layer.Characters)
        {
            var scalar = character.UnicodeScalar;
            if (scalar <= 0)
            {
                // PDFium has no Unicode value for this glyph: nothing to match
                // on, and including a placeholder would create false matches.
                continue;
            }

            if (scalar <= 0xFFFF)
            {
                builder.Append((char)scalar);
                map.Add(character.Index);
                continue;
            }

            if (Rune.IsValid(scalar))
            {
                var pair = char.ConvertFromUtf32(scalar);
                builder.Append(pair);
                // Both UTF-16 units belong to the same character.
                map.Add(character.Index);
                map.Add(character.Index);
            }
        }

        return new SearchableText(builder.ToString(), map.ToArray());
    }
}
