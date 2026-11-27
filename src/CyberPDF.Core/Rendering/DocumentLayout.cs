using CyberPDF.Core.Documents;

namespace CyberPDF.Core.Rendering;

/// <summary>Page arrangement inside the document viewer.</summary>
public enum PageLayoutMode
{
    Continuous,
    SinglePage,
    TwoPage,
}

/// <summary>Where a page sits in the scroll surface, in device pixels.</summary>
public readonly record struct PageSlot(
    int PageIndex,
    int Top,
    int Width,
    int Height,
    int LeftOffset = 0,
    int RowWidth = 0);

/// <summary>
/// Computes the layout of every page at a given zoom, rotation, and mode.
/// The result is what the viewer scrolls over without needing to render first.
/// </summary>
public sealed class DocumentLayout
{
    public DocumentLayout(
        IReadOnlyList<PageSize> pageSizes,
        float zoom,
        int gap,
        int horizontalPadding,
        int rotation = 0,
        PageLayoutMode mode = PageLayoutMode.Continuous)
    {
        ArgumentNullException.ThrowIfNull(pageSizes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoom);
        ArgumentOutOfRangeException.ThrowIfNegative(horizontalPadding);

        Zoom = zoom;
        Gap = Math.Max(0, gap);
        HorizontalPadding = horizontalPadding;
        Mode = mode;

        var normalizedRotation = ((rotation % 360) + 360) % 360;
        var swapAxes = normalizedRotation is 90 or 270;

        var count = pageSizes.Count;
        var slots = new PageSlot[count];
        var top = horizontalPadding;
        var maxWidth = 0;

        if (mode == PageLayoutMode.TwoPage)
        {
            for (var i = 0; i < count; i += 2)
            {
                var (w0, h0) = MeasureSlotSize(pageSizes[i], zoom, swapAxes);
                if (i + 1 < count)
                {
                    var (w1, h1) = MeasureSlotSize(pageSizes[i + 1], zoom, swapAxes);
                    var rowWidth = w0 + Gap + w1;
                    var rowHeight = Math.Max(h0, h1);
                    slots[i] = new PageSlot(i, top, w0, h0, LeftOffset: 0, RowWidth: rowWidth);
                    slots[i + 1] = new PageSlot(i + 1, top, w1, h1, LeftOffset: w0 + Gap, RowWidth: rowWidth);
                    top += rowHeight + Gap;
                    maxWidth = Math.Max(maxWidth, rowWidth);
                }
                else
                {
                    slots[i] = new PageSlot(i, top, w0, h0, LeftOffset: 0, RowWidth: w0);
                    top += h0 + Gap;
                    maxWidth = Math.Max(maxWidth, w0);
                }
            }
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                var (width, height) = MeasureSlotSize(pageSizes[i], zoom, swapAxes);
                slots[i] = new PageSlot(i, top, width, height);
                top += height + Gap;
                maxWidth = Math.Max(maxWidth, width);
            }
        }

        Slots = slots;
        ContentWidth = maxWidth + horizontalPadding * 2;
        ContentHeight = count == 0 ? horizontalPadding * 2 : top - Gap + horizontalPadding;
    }

    private static (int Width, int Height) MeasureSlotSize(PageSize raw, float zoom, bool swapAxes)
    {
        var widthPt = raw.Width;
        var heightPt = raw.Height;
        if (!float.IsFinite(widthPt) || !float.IsFinite(heightPt) || widthPt <= 0 || heightPt <= 0)
        {
            widthPt = 595f;
            heightPt = 842f;
        }
        if (swapAxes)
        {
            (widthPt, heightPt) = (heightPt, widthPt);
        }
        return (Math.Max(1, (int)Math.Round(widthPt * zoom)), Math.Max(1, (int)Math.Round(heightPt * zoom)));
    }

    /// <summary>Convenience overload for an open document.</summary>
    public static DocumentLayout ForDocument(
        PdfDocument document,
        float zoom,
        int gap,
        int horizontalPadding,
        int rotation = 0,
        PageLayoutMode mode = PageLayoutMode.Continuous)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sizes = new PageSize[document.PageCount];
        for (var i = 0; i < sizes.Length; i++)
        {
            sizes[i] = document.GetPageSize(i);
        }
        return new DocumentLayout(sizes, zoom, gap, horizontalPadding, rotation, mode);
    }

    public float Zoom { get; }

    public int Gap { get; }

    public int HorizontalPadding { get; }

    public PageLayoutMode Mode { get; }

    /// <summary>Full width of the scroll content, in device pixels.</summary>
    public int ContentWidth { get; }

    /// <summary>Full height of the scroll content, in device pixels.</summary>
    public int ContentHeight { get; }

    public IReadOnlyList<PageSlot> Slots { get; }

    public int LeftOf(in PageSlot slot)
    {
        var rowWidth = slot.RowWidth > 0 ? slot.RowWidth : slot.Width;
        return (ContentWidth - rowWidth) / 2 + slot.LeftOffset;
    }

    /// <summary>
    /// Left edge of a page inside a viewport of the given width, centered.
    /// </summary>
    public int CenteredLeft(in PageSlot slot, int viewportWidth)
    {
        var rowWidth = slot.RowWidth > 0 ? slot.RowWidth : slot.Width;
        var effective = Math.Max(ContentWidth, viewportWidth);
        var rowLeft = Math.Max(0, (effective - rowWidth) / 2);
        return rowLeft + slot.LeftOffset;
    }

    /// <summary>
    /// Index of the page the viewport top falls inside.
    /// </summary>
    public int FirstVisiblePage(int y)
    {
        if (Slots.Count == 0)
        {
            return 0;
        }

        var clamped = Math.Max(0, y);
        var last = Slots.Count - 1;
        int result;
        if (clamped >= Slots[last].Top)
        {
            result = last;
        }
        else
        {
            var low = 0;
            var high = last;
            result = 0;
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
        }

        // In TwoPage mode, return the left-most page of the active spread row.
        while (result > 0 && Slots[result - 1].Top == Slots[result].Top)
        {
            result--;
        }
        return result;
    }

    /// <summary>
    /// Pages intersecting the vertical range [top, bottom].
    /// </summary>
    public List<PageSlot> VisiblePages(int top, int bottom, bool includeMargin = true)
    {
        var result = new List<PageSlot>();
        if (Slots.Count == 0)
        {
            return result;
        }

        var first = FirstVisiblePage(Math.Max(0, top));
        while (first > 0)
        {
            var previous = Slots[first - 1];
            if (previous.Top + previous.Height > top)
            {
                first--;
            }
            else
            {
                break;
            }
        }
        while (first > 0 && Slots[first - 1].Top == Slots[first].Top)
        {
            first--;
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
