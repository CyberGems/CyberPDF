using System.Text;

namespace CyberPDF.Core.Text;

/// <summary>
/// A range of selected characters inside one page's text layer.
/// </summary>
public readonly record struct TextSelection(int PageIndex, int Start, int Length)
{
    public int End => Start + Length;

    public bool IsEmpty => Length <= 0;

    /// <summary>True when both ranges touch or overlap, in either order.</summary>
    public bool Overlaps(in TextSelection other)
        => PageIndex == other.PageIndex && Start <= other.End && other.Start <= End;
}

/// <summary>
/// Turns text layers into the thing the user sees: highlighted rectangles and a
/// clipboard string.
/// </summary>
/// <remarks>
/// Highlights are grouped into per-line rectangles rather than one per glyph.
/// A paragraph is then a handful of rectangles to paint instead of one per
/// character, which is what makes selection stay smooth at high zoom. Positions
/// returned here are in PDF points; the UI maps them with
/// <see cref="Rendering.PageTransform"/>.
/// </remarks>
public static class TextSelectionEngine
{
    /// <summary>
    /// Selected glyph boxes merged into line rectangles, in PDF space.
    /// </summary>
    public static List<PdfRect> BuildHighlightRects(PageTextLayer layer, TextSelection selection)
    {
        var result = new List<PdfRect>();
        if (layer.IsEmpty || selection.IsEmpty)
        {
            return result;
        }

        var start = Math.Max(0, selection.Start);
        var end = Math.Min(layer.Characters.Count - 1, selection.End - 1);
        if (start > end)
        {
            return result;
        }

        double? currentLeft = null;
        double currentRight = 0;
        double currentBottom = 0;
        double currentTop = 0;

        for (var i = start; i <= end; i++)
        {
            var character = layer.Characters[i];
            if (character.Width <= 0 || character.Height <= 0)
            {
                // A glyph with no box (a space built from kerning, a ligature
                // artifact): nothing to paint, but the line continues.
                continue;
            }

            // Same visual line when the vertical spans actually overlap. Comparing
            // tops and bottoms with a percentage tolerance merged glyphs from
            // adjacent lines whenever their boxes were tall (a heading next to
            // body text), which painted one rectangle stretched over both.
            var sameLine = currentLeft is not null
                && character.Bottom < currentTop
                && character.Top > currentBottom
                && Math.Min(character.Top, currentTop) - Math.Max(character.Bottom, currentBottom)
                    > Math.Min(character.Height, currentTop - currentBottom) * 0.5;

            if (currentLeft is null || sameLine)
            {
                if (currentLeft is null)
                {
                    currentLeft = character.Left;
                    currentRight = character.Right;
                    currentBottom = character.Bottom;
                    currentTop = character.Top;
                }
                else
                {
                    currentRight = Math.Max(currentRight, character.Right);
                    currentBottom = Math.Min(currentBottom, character.Bottom);
                    currentTop = Math.Max(currentTop, character.Top);
                }
                continue;
            }

            result.Add(PdfRect.FromEdges(currentLeft.Value, currentBottom, currentRight, currentTop));
            currentLeft = character.Left;
            currentRight = character.Right;
            currentBottom = character.Bottom;
            currentTop = character.Top;
        }

        if (currentLeft is not null)
        {
            result.Add(PdfRect.FromEdges(currentLeft.Value, currentBottom, currentRight, currentTop));
        }

        return result;
    }

    /// <summary>
    /// The selected text, preserving line breaks. Soft breaks inside a line
    /// (the typical result of justified text) are joined, and hyphenated words
    /// at a line end are stitched back together.
    /// </summary>
    public static string BuildText(PageTextLayer layer, TextSelection selection)
    {
        if (layer.IsEmpty || selection.IsEmpty)
        {
            return string.Empty;
        }

        var start = Math.Max(0, selection.Start);
        var end = Math.Min(layer.Characters.Count, selection.End);
        var builder = new StringBuilder(end - start + 8);

        TextCharacter? previous = null;
        foreach (var character in layer.Characters)
        {
            if (character.Index < start || character.Index >= end)
            {
                continue;
            }

            var isNewLine = previous is { } prev
                && character.Width > 0 && prev.Width > 0
                && character.Top < prev.Bottom;

            if (isNewLine)
            {
                // A hyphen followed by a line break is a broken word; joining it
                // is what every reader does.
                if (builder.Length > 0 && builder[^1] == '-')
                {
                    builder.Length--;
                }
                else
                {
                    builder.AppendLine();
                }
            }

            AppendScalar(builder, character.UnicodeScalar);
            previous = character;
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Expands a selection to whole words. Used by double click.
    /// </summary>
    public static TextSelection SelectWordAt(PageTextLayer layer, int characterIndex)
    {
        if (layer.IsEmpty)
        {
            return default;
        }

        var index = Math.Clamp(characterIndex, 0, layer.Characters.Count - 1);
        var reference = layer.Characters[index];
        var start = index;
        var end = index;

        // A word never crosses a line break. Without the line check, double
        // clicking the end of a line swallowed the whole next line, which is
        // exactly what a user notices first.
        while (start > 0 && IsWordChar(layer.Characters[start - 1]) && SameLine(layer.Characters[start - 1], reference))
        {
            start--;
        }
        while (end + 1 < layer.Characters.Count && IsWordChar(layer.Characters[end + 1]) && SameLine(layer.Characters[end + 1], reference))
        {
            end++;
        }

        return new TextSelection(0, layer.Characters[start].Index, layer.Characters[end].Index - layer.Characters[start].Index + 1);
    }

    /// <summary>
    /// Expands a selection to the whole visual line. Used by triple click.
    /// </summary>
    public static TextSelection SelectLineAt(PageTextLayer layer, int characterIndex)
    {
        if (layer.IsEmpty)
        {
            return default;
        }

        var index = Math.Clamp(characterIndex, 0, layer.Characters.Count - 1);
        var reference = layer.Characters[index];
        var start = index;
        var end = index;

        while (start > 0 && SameLine(layer.Characters[start - 1], reference))
        {
            start--;
        }
        while (end + 1 < layer.Characters.Count && SameLine(layer.Characters[end + 1], reference))
        {
            end++;
        }

        return new TextSelection(0, layer.Characters[start].Index, layer.Characters[end].Index - layer.Characters[start].Index + 1);
    }

    /// <summary>
    /// Index of the character closest to a point in PDF space, which is how a
    /// drag position becomes a selection endpoint. Returns -1 when the layer is
    /// empty.
    /// </summary>
    public static int ClosestCharacterIndex(PageTextLayer layer, double x, double y)
    {
        if (layer.IsEmpty)
        {
            return -1;
        }

        var best = -1;
        var bestDistance = double.MaxValue;
        foreach (var character in layer.Characters)
        {
            if (character.Width <= 0 && character.Height <= 0)
            {
                continue;
            }

            // Distance to the box, not to its center: passing the cursor just
            // past the end of a word must select the last character, not the
            // first one of the next line.
            var dx = Math.Max(0, Math.Max(character.Left - x, x - character.Right));
            var dy = Math.Max(0, Math.Max(character.Bottom - y, y - character.Top));
            var distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = character.Index;
            }
        }

        return best;
    }

    private static bool SameLine(in TextCharacter a, in TextCharacter b)
    {
        if (a.Height <= 0 || b.Height <= 0)
        {
            return false;
        }
        return Math.Abs(a.Top - b.Top) < b.Height * 0.5;
    }

    private static bool IsWordChar(in TextCharacter character)
        => !character.IsWhitespace && character.UnicodeScalar > 0;

    private static void AppendScalar(StringBuilder builder, int scalar)
    {
        if (scalar <= 0)
        {
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
}

/// <summary>A rectangle in PDF points, with the origin at the bottom-left.</summary>
public readonly record struct PdfRect(double Left, double Bottom, double Right, double Top)
{
    public double Width => Right - Left;

    public double Height => Top - Bottom;

    public static PdfRect FromEdges(double left, double bottom, double right, double top)
        => new(Math.Min(left, right), Math.Min(bottom, top), Math.Max(left, right), Math.Max(bottom, top));
}
