using CyberPDF.Core.Documents;

namespace CyberPDF.Core.Rendering;

/// <summary>Where a page sits in the continuous scroll column, in device pixels.</summary>
public readonly record struct PageSlot(int PageIndex, int Top, int Width, int Height);

/// <summary>
/// Computes the vertical layout of every page at a given zoom. Pages are stacked
/// in a single column with a gap between them, centered horizontally. The result
/// is what the viewer scrolls over: with this there is no need to render
/// anything just to know how tall the document is.
/// </summary>
/// <remarks>
/// The layout only needs page sizes, not an open document. That keeps the math
/// testable without PDFium and lets the UI compute a preview layout for a
/// document that is still loading.
/// </remarks>
public sealed class DocumentLayout
{
    public DocumentLayout(IReadOnlyList<PageSize> pageSizes, float zoom, int gap, int horizontalPadding)
    {
        ArgumentNullException.ThrowIfNull(pageSizes);
        if (zoom <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom));
        }
        if (horizontalPadding < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(horizontalPadding));
        }

        Zoom = zoom;
        Gap = Math.Max(0, gap);
        HorizontalPadding = horizontalPadding;

        var count = pageSizes.Count;
        var slots = new PageSlot[count];
        var top = horizontalPadding;
        var maxWidth = 0;

        for (var i = 0; i < count; i++)
        {
            // A page with no usable size would collapse the column and make
            // scrolling jump; treat it as one unscaled A4 instead.
            var widthPt = pageSizes[i].Width;
            var heightPt = pageSizes[i].Height;
            if (!float.IsFinite(widthPt) || !float.IsFinite(heightPt) || widthPt <= 0 || heightPt <= 0)
            {
                widthPt = 595f;
                heightPt = 842f;
            }

            var width = Math.Max(1, (int)Math.Round(widthPt * zoom));
            var height = Math.Max(1, (int)Math.Round(heightPt * zoom));
            slots[i] = new PageSlot(i, top, width, height);
            top += height + Gap;
            maxWidth = Math.Max(maxWidth, width);
        }

        Slots = slots;
        ContentWidth = maxWidth + horizontalPadding * 2;
        ContentHeight = count == 0 ? horizontalPadding * 2 : top - Gap + horizontalPadding;
    }

    /// <summary>Convenience overload for an open document.</summary>
    public static DocumentLayout ForDocument(PdfDocument document, float zoom, int gap, int horizontalPadding)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sizes = new PageSize[document.PageCount];
        for (var i = 0; i < sizes.Length; i++)
        {
            sizes[i] = document.GetPageSize(i);
        }
        return new DocumentLayout(sizes, zoom, gap, horizontalPadding);
    }

    public float Zoom { get; }

    public int Gap { get; }

    public int HorizontalPadding { get; }

    /// <summary>Full width of the scroll content, in device pixels.</summary>
    public int ContentWidth { get; }

    /// <summary>Full height of the scroll content, in device pixels.</summary>
    public int ContentHeight { get; }

    public IReadOnlyList<PageSlot> Slots { get; }

    /// <summary>
    /// Left edge of a page inside the content, centered horizontally. This is
    /// only correct when the content is the viewport; a viewer paints into its
    /// own client width and must use <see cref="CenteredLeft"/> instead.
    /// </summary>
    public int LeftOf(in PageSlot slot) => (ContentWidth - slot.Width) / 2;

    /// <summary>
    /// Left edge of a page inside a viewport of the given width, centered. The
    /// layout itself has no idea how wide the window is, so the caller passes it.
    /// </summary>
    public int CenteredLeft(in PageSlot slot, int viewportWidth)
    {
        var effective = Math.Max(ContentWidth, viewportWidth);
        return Math.Max(0, (effective - slot.Width) / 2);
    }

    /// <summary>
    /// Index of the page the viewport top falls inside. Past the end of the
    /// document it returns the last page, so the status bar and the scrollbar
    /// agree instead of jumping back to page 1.
    /// </summary>
    public int FirstVisiblePage(int y)
    {
        if (Slots.Count == 0)
        {
            return 0;
        }

        var clamped = Math.Max(0, y);
        var last = Slots.Count - 1;
        if (clamped >= Slots[last].Top)
        {
            return last;
        }

        var low = 0;
        var high = last;
        var result = 0;
        while (low <= high)
        {
            var mid = low + (high - low) / 2;
            var slot = Slots[mid];
            if (slot.Top <= clamped)
            {
                result = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }
        return result;
    }

    /// <summary>
    /// Pages intersecting the vertical range [top, bottom]. With
    /// <paramref name="includeMargin"/> set, pages that only touch the range are
    /// included too, which is what the renderer wants so a page does not start
    /// rendering only when its first pixel becomes visible.
    /// </summary>
    public List<PageSlot> VisiblePages(int top, int bottom, bool includeMargin = true)
    {
        var result = new List<PageSlot>();
        if (Slots.Count == 0)
        {
            return result;
        }

        var first = FirstVisiblePage(Math.Max(0, top));
        if (first > 0)
        {
            // The page above can still overlap the range when scrolling inside it.
            var previous = Slots[first - 1];
            if (previous.Top + previous.Height > top)
            {
                first--;
            }
        }

        for (var i = first; i < Slots.Count; i++)
        {
            var slot = Slots[i];
            if (slot.Top > bottom)
            {
                break;
            }

            var intersects = slot.Top + slot.Height > top && slot.Top < bottom;
            var touches = slot.Top + slot.Height >= top && slot.Top <= bottom;
            if (intersects || (includeMargin && touches))
            {
                result.Add(slot);
            }
        }
        return result;
    }
}
