using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using CyberPDF.Common.I18n;
using CyberPDF.Core.Diagnostics;
using CyberPDF.Core.Documents;
using CyberPDF.Core.Rendering;

namespace CyberPDF.UI.Services;

/// <summary>
/// Prints an open PDF document by rendering each requested page on the PDFium
/// worker thread and painting it onto the Windows print surface.
/// </summary>
public static class PdfPrintService
{
    private const int MaxPrintPixelDimension = 4096;

    public static void Print(IWin32Window owner, PdfEngine engine, PdfDocument document, string? documentPath)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(document);
        if (document.PageCount <= 0)
        {
            return;
        }

        using var printDoc = new PrintDocument
        {
            DocumentName = string.IsNullOrWhiteSpace(documentPath)
                ? Strings.Get("AppTitle")
                : Path.GetFileName(documentPath),
        };
        printDoc.PrinterSettings.MinimumPage = 1;
        printDoc.PrinterSettings.MaximumPage = document.PageCount;
        printDoc.PrinterSettings.FromPage = 1;
        printDoc.PrinterSettings.ToPage = document.PageCount;

        using var dialog = new PrintDialog
        {
            Document = printDoc,
            UseEXDialog = true,
            AllowSomePages = true,
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return;
        }

        var fromPage = 1;
        var toPage = document.PageCount;
        if (printDoc.PrinterSettings.PrintRange == PrintRange.SomePages)
        {
            fromPage = Math.Clamp(printDoc.PrinterSettings.FromPage, 1, document.PageCount);
            toPage = Math.Clamp(printDoc.PrinterSettings.ToPage, fromPage, document.PageCount);
        }

        var currentIndex = fromPage - 1;
        var lastIndex = toPage - 1;

        printDoc.QueryPageSettings += (_, e) =>
        {
            if (currentIndex <= lastIndex)
            {
                var size = document.GetPageSize(currentIndex);
                e.PageSettings.Landscape = size.Width > size.Height;
            }
        };

        printDoc.PrintPage += (_, e) =>
        {
            if (e.Graphics is null || currentIndex > lastIndex)
            {
                e.HasMorePages = false;
                return;
            }

            var pageIndex = currentIndex;
            var size = document.GetPageSize(pageIndex);
            var target = e.MarginBounds.Width > 0 && e.MarginBounds.Height > 0
                ? e.MarginBounds
                : e.PageBounds;

            // Render at ~200 DPI scaled to the target print area.
            var pixelScale = 200f / 100f;
            var fitRatio = Math.Min(
                target.Width / Math.Max(1f, size.Width),
                target.Height / Math.Max(1f, size.Height));
            var drawWidth = Math.Max(1f, size.Width * fitRatio);
            var drawHeight = Math.Max(1f, size.Height * fitRatio);
            var drawX = target.X + (target.Width - drawWidth) / 2f;
            var drawY = target.Y + (target.Height - drawHeight) / 2f;

            var rasterWidth = Math.Clamp((int)Math.Round(drawWidth * pixelScale), 1, MaxPrintPixelDimension);
            var rasterHeight = Math.Clamp((int)Math.Round(drawHeight * pixelScale), 1, MaxPrintPixelDimension);

            RenderResult? renderResult = null;
            using var done = new ManualResetEventSlim(false);
            var queued = engine.Render(
                document,
                pageIndex,
                rasterWidth,
                rasterHeight,
                rotation: 0,
                onSuccess: result =>
                {
                    renderResult = result;
                    done.Set();
                },
                onError: ex =>
                {
                    AppLog.Error($"Print render failed for page {pageIndex}", ex);
                    done.Set();
                });

            if (queued)
            {
                done.Wait(TimeSpan.FromSeconds(15));
            }

            if (renderResult is { } res)
            {
                try
                {
                    e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    e.Graphics.DrawImage(res.Page.Bitmap, drawX, drawY, drawWidth, drawHeight);
                }
                finally
                {
                    if (res.CallerOwns)
                    {
                        res.Page.Dispose();
                    }
                }
            }

            currentIndex++;
            e.HasMorePages = currentIndex <= lastIndex;
        };

        try
        {
            printDoc.Print();
        }
        catch (Exception ex) when (ex is InvalidPrinterException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            AppLog.Error("Printing failed", ex);
            MessageBox.Show(owner, Strings.Get("PrintError"), Strings.Get("ErrorTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
