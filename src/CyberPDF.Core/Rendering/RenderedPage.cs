using System.Drawing.Imaging;
using CyberPDF.Core.Interop;

namespace CyberPDF.Core.Rendering;

/// <summary>
/// Rendered page pixels, owned as a single <see cref="Bitmap"/>.
/// </summary>
/// <remarks>
/// PDFium paints directly into the bitmap's own memory through
/// <c>FPDFBitmap_CreateEx</c>, so there is exactly one copy of every page in the
/// process. Rendering into a native PDFium bitmap and then copying it doubled
/// the memory cost of every visible page.
/// </remarks>
public sealed class RenderedPage : IDisposable
{
    private Bitmap? _bitmap;

    private RenderedPage(Bitmap bitmap, int offsetY)
    {
        _bitmap = bitmap;
        OffsetY = offsetY;
    }

    public int Width => _bitmap?.Width ?? 0;

    public int Height => _bitmap?.Height ?? 0;

    /// <summary>
    /// Vertical position of this slice inside the full rendered page, in pixels.
    /// Zero for a whole-page render, non-zero for a tile.
    /// </summary>
    public int OffsetY { get; }

    public long ByteSize => _bitmap is null ? 0 : (long)_bitmap.Width * _bitmap.Height * 4;

    public bool IsDisposed => _bitmap is null;

    /// <summary>
    /// The rendered pixels. The page keeps ownership: do not dispose the result.
    /// </summary>
    public Bitmap Bitmap => _bitmap ?? throw new ObjectDisposedException(nameof(RenderedPage));

    /// <summary>
    /// Renders a page, or one horizontal band of a page, straight into a new
    /// 32-bit bitmap. Must run on the PDFium thread.
    /// </summary>
    /// <param name="pageHandle">Loaded page handle.</param>
    /// <param name="width">Bitmap width in pixels.</param>
    /// <param name="height">Bitmap height in pixels (the band height for a tile).</param>
    /// <param name="rotation">Rotation in degrees; normalized to 0, 90, 180 or 270.</param>
    /// <param name="tileOffsetY">Top of this band inside the full page, in pixels.</param>
    /// <param name="fullPixelHeight">Height of the whole page when tiling.</param>
    internal static RenderedPage Render(
        nint pageHandle,
        int width,
        int height,
        int rotation,
        int tileOffsetY,
        int fullPixelHeight)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);

        var data = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppRgb);

        try
        {
            // CreateEx wraps the bitmap's memory: PDFium writes into it and
            // DestroyBitmap intentionally does not free it.
            var native = PdfiumNative.FPDFBitmap_CreateEx(
                width, height, PdfiumNative.BitmapFormatBgra, data.Scan0, data.Stride);
            if (native == IntPtr.Zero)
            {
                // Not OutOfMemoryException: the runtime reserves that type and
                // callers would treat it as fatal. A rejected surface is a
                // render failure like any other.
                throw new InvalidOperationException($"Could not create a {width}x{height} render surface.");
            }

            try
            {
                // The bitmap starts as uninitialized memory, not white paper.
                PdfiumNative.FPDFBitmap_FillRect(native, 0, 0, width, height, 0xFFFFFFFF);

                // A negative start_y shifts the page canvas up so a band lower
                // down lands inside this smaller bitmap. That is what keeps a
                // poster-sized page from needing one huge allocation.
                PdfiumNative.FPDF_RenderPageBitmap(
                    native,
                    pageHandle,
                    0,
                    -tileOffsetY,
                    width,
                    fullPixelHeight > 0 ? fullPixelHeight : height,
                    ToPdfiumRotation(rotation),
                    PdfiumNative.RenderAnnotations | PdfiumNative.RenderLcdText);
            }
            finally
            {
                PdfiumNative.FPDFBitmap_Destroy(native);
            }
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return new RenderedPage(bitmap, tileOffsetY);
    }

    /// <summary>
    /// PDFium counts rotation in clockwise quarter turns from 0 to 3, not in
    /// degrees. Passing 90 where it expects 1 would rotate the page 90 times.
    /// </summary>
    private static int ToPdfiumRotation(int degrees)
    {
        var normalized = ((degrees % 360) + 360) % 360;
        return normalized switch
        {
            90 => 1,
            180 => 2,
            270 => 3,
            _ => 0,
        };
    }

    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
