using System.Drawing.Imaging;
using CyberPDF.Common.I18n;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI;

/// <summary>
/// Hidden screenshot mode (<c>CyberPDF.exe --shot out.png file.pdf</c>). Asks the
/// window to paint itself into a bitmap and saves it, which verifies the real
/// painting code on any machine without depending on what is visible on the
/// desktop, on window z-order or on GPU composition.
/// </summary>
internal static class ScreenshotProbe
{
    public static bool IsRequested(string[] args)
        => args.Any(a => a.Equals("--shot", StringComparison.OrdinalIgnoreCase));

    public static string? OutputPath(string[] args)
    {
        var index = Array.FindIndex(args, a => a.Equals("--shot", StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length)
        {
            return null;
        }
        var candidate = args[index + 1];
        if (!candidate.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return candidate;
    }

    public static string? TargetFile(string[] args)
    {
        var output = OutputPath(args);
        return args.FirstOrDefault(a =>
            a.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(a) && a != output);
    }

    /// <summary>
    /// Waits for the render queue to settle, then captures. The delay is not a
    /// guess: the first page needs at least one round trip through the PDFium
    /// thread plus a UI marshal, and pages arrive band by band.
    /// </summary>
    public static async Task CaptureAsync(MainWindow window, string path, bool selectText = false, string? searchTerm = null)
    {
        Theme.Apply(Theme.CurrentTheme);

        if (searchTerm is not null)
        {
            // A search screenshot has to run the search and wait for extraction,
            // which happens page by page on the PDFium thread.
            window.SelfTestSearch(searchTerm);

            // Wait until the count stops moving: stopping at the first hit would
            // capture a partially searched multi-page document.
            var last = -1;
            for (var i = 0; i < 100; i++)
            {
                await Task.Delay(100);
                if (window.SelfTestMatchCount == last)
                {
                    break;
                }
                last = window.SelfTestMatchCount;
            }
            await Task.Delay(700);
        }
        else if (selectText)
        {
            // A screenshot of the selection has to select first, and extraction is
            // asynchronous, so the wait below doubles as the settle time.
            window.DocumentViewer.SelectAllOnCurrentPage();
            await Task.Delay(2500);
        }
        else
        {
            await Task.Delay(3500);
        }

        // Print the state the capture is about to record, so a suspicious image
        // can be told apart from a broken view.
        Console.WriteLine($"capture state: document={(window.HasDocument ? "open" : "none")} " +
                          $"page={(window.HasDocument ? window.DocumentViewer.FirstVisiblePage + 1 : 0)} " +
                          $"zoom={window.DocumentViewer.Zoom:P0} scroll={window.DocumentViewer.ScrollY} " +
                          $"content={window.DocumentViewer.ContentHeight}");

        var size = window.ClientSize;
        using var bitmap = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppArgb);
        window.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));

        // Intentionally only DrawToBitmap: the capture must show what the real
        // OnPaint path produces, not a second, parallel painting of the document
        // surface. If a page is missing here, the on-screen paint is broken too.

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        bitmap.Save(path, ImageFormat.Png);
        Console.WriteLine($"saved {Path.GetFullPath(path)} ({bitmap.Width}x{bitmap.Height})");
    }
}
