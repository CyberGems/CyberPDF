using System.Text;
using CyberPDF.Core.Documents;
using CyberPDF.Core.Interop;

namespace CyberPDF.Core.Text;

/// <summary>A single character with its position in PDF page space.</summary>
public readonly record struct TextCharacter(
    int Index,
    int UnicodeScalar,
    double Left,
    double Right,
    double Bottom,
    double Top)
{
    public double Width => Right - Left;

    public double Height => Top - Bottom;

    public bool IsWhitespace => UnicodeScalar is ' ' or '\t' or '\r' or '\n';
}

/// <summary>
/// The text layer of one page: the characters PDFium extracted plus their
/// geometry. This is what makes selection and search possible without
/// re-rendering anything.
/// </summary>
/// <remarks>
/// Positions are in PDF points, origin at the bottom-left of the page, matching
/// PDFium. The UI converts them to the page's rendered rectangle.
/// </remarks>
public sealed class PageTextLayer
{
    private readonly TextCharacter[] _characters;
    private readonly string _text;

    private PageTextLayer(TextCharacter[] characters, string text, PageSize size)
    {
        _characters = characters;
        _text = text;
        Size = size;
    }

    public PageSize Size { get; }

    /// <summary>Plain text of the page, in reading order as PDFium reports it.</summary>
    public string Text => _text;

    public IReadOnlyList<TextCharacter> Characters => _characters;

    public bool IsEmpty => _characters.Length == 0;

    internal static PageTextLayer Create(TextCharacter[] characters, PageSize size)
    {
        var builder = new System.Text.StringBuilder(characters.Length);
        foreach (var c in characters)
        {
            AppendUnicode(builder, c.UnicodeScalar);
        }
        return new PageTextLayer(characters, builder.ToString(), size);
    }

    private static void AppendUnicode(System.Text.StringBuilder builder, int scalar)
    {
        if (scalar <= 0)
        {
            // PDFium reports 0 for glyphs with no Unicode mapping.
            return;
        }
        if (scalar <= 0xFFFF)
        {
            builder.Append((char)scalar);
            return;
        }
        if (Rune.IsValid(scalar))
        {
            builder.Append(char.ConvertFromUtf32(scalar));
        }
    }

    /// <summary>
    /// Finds the character at a point in PDF page space. Returns null when the
    /// point is outside every glyph.
    /// </summary>
    /// <remarks>
    /// Two passes: a glyph whose box actually contains the point always wins
    /// over one that only matched through the tolerance. Without that ordering,
    /// a slightly-larger neighbour within tolerance could be returned instead of
    /// the character under the cursor.
    /// </remarks>
    public TextCharacter? HitTest(double x, double y, double tolerance = 3)
    {
        TextCharacter? exact = null;
        var exactArea = double.MaxValue;
        TextCharacter? near = null;
        var nearArea = double.MaxValue;

        foreach (var candidate in _characters)
        {
            var inside = x >= candidate.Left && x <= candidate.Right
                && y >= candidate.Bottom && y <= candidate.Top;
            var close = x >= candidate.Left - tolerance && x <= candidate.Right + tolerance
                && y >= candidate.Bottom - tolerance && y <= candidate.Top + tolerance;
            if (!close)
            {
                continue;
            }

            var area = Math.Max(candidate.Width, 0.01) * Math.Max(candidate.Height, 0.01);
            if (inside)
            {
                if (area < exactArea)
                {
                    exactArea = area;
                    exact = candidate;
                }
            }
            else if (area < nearArea)
            {
                nearArea = area;
                near = candidate;
            }
        }

        return exact ?? near;
    }

    /// <summary>
    /// Character indices whose boxes intersect the given rectangle. The UI uses
    /// this to paint the selection highlight.
    /// </summary>
    public List<int> CharactersIn(double left, double bottom, double right, double top)
    {
        var result = new List<int>();
        foreach (var c in _characters)
        {
            if (c.Right < left || c.Left > right || c.Top < bottom || c.Bottom > top)
            {
                continue;
            }
            result.Add(c.Index);
        }
        return result;
    }

    public static PageTextLayer Empty(PageSize size) => new(Array.Empty<TextCharacter>(), string.Empty, size);
}
