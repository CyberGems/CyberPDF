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
    {
        var objects = new SortedDictionary<int, byte[]>();

        objects[1] = Encoding.ASCII.GetBytes("<</Type/Catalog/Pages 2 0 R>>");
        var kids = string.Join(" ", Enumerable.Range(0, pages).Select(i => $"{3 + i * 2} 0 R"));
        objects[2] = Encoding.ASCII.GetBytes($"<</Type/Pages/Count {pages}/Kids[{kids}]>>");

        var fontId = 3 + pages * 2;
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
                content.Append($"({PageLine(i + 1, line)}) Tj T*\n");
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

        using var output = new FileStream(path, FileMode.Create, FileAccess.Write);
        var offsets = new Dictionary<int, long>();
        var header = Encoding.ASCII.GetBytes("%PDF-1.7\n");
        output.Write(header);

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
