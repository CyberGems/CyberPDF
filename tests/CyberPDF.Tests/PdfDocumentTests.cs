using CyberPDF.Core.Documents;
using CyberPDF.Core.Text;
using Xunit;

namespace CyberPDF.Tests;

/// <summary>
/// Integration tests against the real PDFium engine. They need the native DLL,
/// which scripts/Fetch-Pdfium.ps1 restores; without it the tests are skipped
/// rather than failing, so a fresh clone can still run the rest of the suite.
/// </summary>
[Collection("pdfium")]
public sealed class PdfDocumentTests : IDisposable
{
    private readonly string _directory;

    public PdfDocumentTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "cyberpdf-doc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string NewPdf(int pages, int linesPerPage = 20, int rotate = 0)
        => PdfWriter.Write(Path.Combine(_directory, $"doc-{Guid.NewGuid():N}.pdf"), pages, linesPerPage, rotate);

    [Fact]
    public void OpensDocumentAndReportsPageGeometry()
    {
        var path = NewPdf(3);
        var result = PdfDocument.Open(path);

        Assert.True(result.Success, $"Open failed: {result.Error}");
        using var document = result.Document!;

        Assert.Equal(3, document.PageCount);
        var size = document.GetPageSize(0);
        Assert.Equal(595f, size.Width, 1f);
        Assert.Equal(842f, size.Height, 1f);
    }

    [Fact]
    public void DocumentRotationIsReflectedInReportedSize()
    {
        // A /Rotate 90 page must report 842x595: the layout depends on it.
        var path = NewPdf(1, rotate: 90);
        using var document = PdfDocument.Open(path).Document!;

        var size = document.GetPageSize(0);
        Assert.Equal(842f, size.Width, 1f);
        Assert.Equal(595f, size.Height, 1f);
    }

    [Fact]
    public void MissingFileReportsNotFound()
    {
        var result = PdfDocument.Open(Path.Combine(_directory, "nope.pdf"));

        Assert.False(result.Success);
        Assert.Equal(PdfOpenError.NotFound, result.Error);
    }

    [Fact]
    public void NonPdfFileReportsInvalidFormat()
    {
        var path = Path.Combine(_directory, "fake.pdf");
        File.WriteAllText(path, "this is not a pdf at all");

        var result = PdfDocument.Open(path);

        Assert.False(result.Success);
        Assert.Equal(PdfOpenError.InvalidFormat, result.Error);
    }

    [Fact]
    public void EmptyFileReportsInvalidFormat()
    {
        var path = Path.Combine(_directory, "empty.pdf");
        File.WriteAllBytes(path, Array.Empty<byte>());

        var result = PdfDocument.Open(path);

        Assert.False(result.Success);
        Assert.Equal(PdfOpenError.InvalidFormat, result.Error);
    }

    [Fact]
    public void TextLayerContainsThePageText()
    {
        var path = NewPdf(1, linesPerPage: 5);
        using var document = PdfDocument.Open(path).Document!;

        var layer = TextExtractor.Extract(document, 0);

        Assert.False(layer.IsEmpty);
        Assert.Contains("Page 1 line 1", layer.Text);
        Assert.Contains("quick brown fox", layer.Text);
        Assert.All(layer.Characters, c => Assert.True(c.Index >= 0));
    }

    [Fact]
    public void TextLayerCharactersHaveGeometryInsideThePage()
    {
        var path = NewPdf(1, linesPerPage: 3);
        using var document = PdfDocument.Open(path).Document!;
        var layer = TextExtractor.Extract(document, 0);

        var positioned = layer.Characters.Where(c => c.Width > 0 && c.Height > 0).ToArray();
        Assert.NotEmpty(positioned);
        Assert.All(positioned, c =>
        {
            Assert.InRange(c.Left, -1, 596);
            Assert.InRange(c.Top, -1, 843);
            Assert.True(c.Right >= c.Left);
            Assert.True(c.Top >= c.Bottom);
        });
    }

    [Fact]
    public void TextLayerHitTestFindsACharacterWhoseBoxContainsThePoint()
    {
        var path = NewPdf(1, linesPerPage: 3);
        using var document = PdfDocument.Open(path).Document!;
        var layer = TextExtractor.Extract(document, 0);

        var target = layer.Characters.First(c => c.Width > 0 && c.UnicodeScalar == 'q');
        var x = (target.Left + target.Right) / 2;
        var y = (target.Bottom + target.Top) / 2;
        var hit = layer.HitTest(x, y);

        Assert.NotNull(hit);
        // The contract is that the returned character covers the point. Picking
        // the smallest box means an overlapping neighbour can legitimately win,
        // so asserting identity with the target would test the wrong thing.
        Assert.InRange(x, hit!.Value.Left, hit.Value.Right);
        Assert.InRange(y, hit.Value.Bottom, hit.Value.Top);
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        var path = NewPdf(1);
        var document = PdfDocument.Open(path).Document!;

        document.Dispose();
        document.Dispose();

        Assert.True(document.IsDisposed);
    }
}
