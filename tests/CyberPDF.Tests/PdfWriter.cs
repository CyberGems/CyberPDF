using System.Globalization;
using System.Text;

namespace CyberPDF.Tests;

/// <summary>
/// Writes minimal but valid PDF files for the integration tests. Each page has a
/// real text stream, so text extraction and rendering have something to find
/// without shipping binary fixtures in the repository.
/// </summary>
internal static class PdfWriter
{
    /// <summary>
    /// Builds a document with the given number of pages and lines of text per
    /// page. Page size is A4 in points.
    /// </summary>
    public static string Write(string path, int pages, int linesPerPage = 20, int rotate = 0)
        => WriteDocument(path, pages, linesPerPage, rotate, outline: null);

    /// <summary>
    /// Builds a document with a two level outline: a chapter on the first page,
    /// a section under it on the second, and a second chapter on the last page.
    /// </summary>
    public static string WriteWithOutline(string path, int pages)
        => WriteDocument(path, pages, 1, 0, outline: new OutlinePlan(pages));

    private sealed record OutlinePlan(int Pages);

    private static string WriteDocument(string path, int pages, int linesPerPage, int rotate, OutlinePlan? outline)
    {
        var objects = new SortedDictionary<int, byte[]>();

        var fontId = 3 + pages * 2;
        var catalog = outline is null
            ? "<</Type/Catalog/Pages 2 0 R>>"
            : $"<</Type/Catalog/Pages 2 0 R/Outlines {fontId + 1} 0 R>>";
        objects[1] = Encoding.ASCII.GetBytes(catalog);

        var kids = string.Join(" ", Enumerable.Range(0, pages).Select(i => $"{3 + i * 2} 0 R"));
        objects[2] = Encoding.ASCII.GetBytes($"<</Type/Pages/Count {pages}/Kids[{kids}]>>");

        for (var i = 0; i < pages; i++)
        {
            var pageId = 3 + i * 2;
            var contentId = pageId + 1;
            var rotateEntry = rotate == 0 ? string.Empty : $"/Rotate {rotate}";
            objects[pageId] = Encoding.ASCII.GetBytes(
                $"<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]{rotateEntry}" +
                $"/Resources<</Font<</F1 {fontId} 0 R>>>>/Contents {contentId} 0 R>>");

            var content = new StringBuilder("BT /F1 11 Tf 50 800 Td 14 TL\n");
            for (var line = 1; line <= linesPerPage; line++)
            {
                content.Append(CultureInfo.InvariantCulture, $"({PageLine(i + 1, line)}) Tj T*\n");
            }
            content.Append("ET");
            var bytes = Encoding.ASCII.GetBytes(content.ToString());
            var stream = new List<byte>();
            stream.AddRange(Encoding.ASCII.GetBytes($"<</Length {bytes.Length}>>stream\n"));
            stream.AddRange(bytes);
            stream.AddRange(Encoding.ASCII.GetBytes("\nendstream"));
            objects[contentId] = stream.ToArray();
        }

        objects[fontId] = Encoding.ASCII.GetBytes("<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>");

        if (outline is not null)
        {
            var root = fontId + 1;
            var chapterOne = root + 1;
            var section = chapterOne + 1;
            var chapterTwo = section + 1;
            var firstPage = 3;
            var secondPage = 5;
            var lastPage = 3 + (outline.Pages - 1) * 2;

            objects[root] = Encoding.ASCII.GetBytes(
                $"<</Type/Outlines/First {chapterOne} 0 R/Last {chapterTwo} 0 R/Count 3>>");
            objects[chapterOne] = Encoding.ASCII.GetBytes(
                $"<</Title(Chapter 1)/Parent {root} 0 R/First {section} 0 R/Last {section} 0 R" +
                $"/Next {chapterTwo} 0 R/Dest[{firstPage} 0 R/XYZ 0 842 0]/Count 1>>");
            objects[section] = Encoding.ASCII.GetBytes(
                $"<</Title(Section 1.1)/Parent {chapterOne} 0 R/Dest[{secondPage} 0 R/XYZ 0 842 0]>>");
            objects[chapterTwo] = Encoding.ASCII.GetBytes(
                $"<</Title(Chapter 2)/Parent {root} 0 R/Prev {chapterOne} 0 R" +
                $"/Dest[{lastPage} 0 R/XYZ 0 842 0]>>");
        }

        using var output = new FileStream(path, FileMode.Create, FileAccess.Write);
        var offsets = new Dictionary<int, long>();
        output.Write(Encoding.ASCII.GetBytes("%PDF-1.7\n"));

        foreach (var (id, body) in objects)
        {
            offsets[id] = output.Position;
            output.Write(Encoding.ASCII.GetBytes($"{id} 0 obj\n"));
            output.Write(body);
            output.Write(Encoding.ASCII.GetBytes("\nendobj\n"));
        }

        var xrefPosition = output.Position;
        var maxId = objects.Keys.Max();
        output.Write(Encoding.ASCII.GetBytes($"xref\n0 {maxId + 1}\n"));
        output.Write(Encoding.ASCII.GetBytes("0000000000 65535 f \n"));
        for (var id = 1; id <= maxId; id++)
        {
            output.Write(Encoding.ASCII.GetBytes($"{offsets[id]:D10} 00000 n \n"));
        }
        output.Write(Encoding.ASCII.GetBytes(
            $"trailer\n<</Size {maxId + 1}/Root 1 0 R>>\nstartxref\n{xrefPosition}\n%%EOF\n"));

        return path;
    }

    private static string PageLine(int page, int line)
        => $"Page {page} line {line}: the quick brown fox jumps over the lazy dog";
}
