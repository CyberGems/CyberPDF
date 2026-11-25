using CyberPDF.Core.Documents;

namespace CyberPDF.Core.Rendering;

/// <summary>
/// Maps between PDF page space and the pixel rectangle a page occupies on
/// screen. PDFium reports character boxes in PDF points with the origin at the
/// bottom left of the page; the screen works in pixels from the top left, with
/// the page possibly rotated and scaled.
/// </summary>
/// <remarks>
/// All the rotation and flip math lives here, in one place, so selection,
/// highlighting and link hit testing cannot drift apart. Every conversion is
/// covered by tests.
/// </remarks>
public readonly struct PageTransform
{
    private readonly PageSize _pageSize;

    /// <summary>
    /// Creates a transform for a page of <paramref name="pageSize"/> points drawn
    /// into a rectangle of <paramref name="targetWidth"/> by
    /// <paramref name="targetHeight"/> pixels.
    /// </summary>
    /// <param name="rotation">
    /// Rotation in degrees, a multiple of 90. Must match the rotation used to
    /// render the page.
    /// </param>
    public PageTransform(PageSize pageSize, int targetWidth, int targetHeight, int rotation)
    {
        _pageSize = pageSize;
        TargetWidth = targetWidth;
        TargetHeight = targetHeight;
        Rotation = Normalize(rotation);
    }

    /// <summary>Width of the drawn page in pixels.</summary>
    public int TargetWidth { get; }

    /// <summary>Height of the drawn page in pixels.</summary>
    public int TargetHeight { get; }

    /// <summary>Rotation in degrees, one of 0, 90, 180 or 270.</summary>
    public int Rotation { get; }

    /// <summary>Page size in points, as reported by PDFium (rotation included).</summary>
    public PageSize PageSize => _pageSize;

    /// <summary>
    /// Converts a point in PDF space to a point in the drawn page's pixels,
    /// relative to the page's top-left corner.
    /// </summary>
    public (double X, double Y) PdfToView(double pdfX, double pdfY)
    {
        var width = Math.Max(0.01, _pageSize.Width);
        var height = Math.Max(0.01, _pageSize.Height);

        // Normalized position inside the page, origin top left, as PDF space has
        // the origin bottom left.
        var u = pdfX / width;
        var v = 1.0 - pdfY / height;

        // Measured against PDFium by rendering a page with a distinct mark in
        // each corner at every rotation. Rotating 90 degrees clockwise moves the
        // page's bottom-left corner to the view's top-left.
        (u, v) = Rotation switch
        {
            90 => (1.0 - v, u),
            180 => (1.0 - u, 1.0 - v),
            270 => (v, 1.0 - u),
            _ => (u, v),
        };

        return (u * TargetWidth, v * TargetHeight);
    }

    /// <summary>
    /// Converts a point in the drawn page's pixels, relative to its top-left
    /// corner, back into PDF space.
    /// </summary>
    public (double X, double Y) ViewToPdf(double viewX, double viewY)
    {
        if (TargetWidth <= 0 || TargetHeight <= 0)
        {
            return (0, 0);
        }

        var u = viewX / TargetWidth;
        var v = viewY / TargetHeight;

        // Inverse of PdfToView, so a point survives a round trip at every
        // rotation. Keeping the two switches mirrored is what makes that true.
        (u, v) = Rotation switch
        {
            90 => (v, 1.0 - u),
            180 => (1.0 - u, 1.0 - v),
            270 => (1.0 - v, u),
            _ => (u, v),
        };

        var pdfX = u * _pageSize.Width;
        var pdfY = (1.0 - v) * _pageSize.Height;
        return (pdfX, pdfY);
    }

    /// <summary>
    /// Fraction of the page that the given scale represents, for callers that
    /// need to know how many pixels one PDF point occupies.
    /// </summary>
    public double PixelsPerPoint
    {
        get
        {
            if (_pageSize.Width <= 0 || TargetWidth <= 0)
            {
                return 1;
            }
            return Rotation is 90 or 270
                ? (double)TargetHeight / _pageSize.Width
                : (double)TargetWidth / _pageSize.Width;
        }
    }

    private static int Normalize(int degrees)
    {
        var normalized = ((degrees % 360) + 360) % 360;
        // Only quarter turns describe a PDFium render, so anything else is
        // treated as unrotated instead of silently shearing the selection.
        return normalized % 90 == 0 ? normalized : 0;
    }
}
