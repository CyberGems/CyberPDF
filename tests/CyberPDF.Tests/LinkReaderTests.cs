using CyberPDF.Core.Documents;
using CyberPDF.Core.Text;
using Xunit;

namespace CyberPDF.Tests;

public sealed class LinkReaderTests
{
    [Theory]
    [InlineData("https://cybergems.org/docs", true)]
    [InlineData("http://localhost:8080/test", true)]
    [InlineData("mailto:hello@cybergems.org", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ExternalUriValidationAllowsOnlySafeSchemes(string? uri, bool expected)
    {
        Assert.Equal(expected, LinkReader.IsSafeExternalUri(uri));
    }

    [Fact]
    public void HitTestMatchesLinkBounds()
    {
        var link = new PdfLink(new PdfRect(10, 20, 100, 40), TargetPage: 3, Uri: null);
        var layer = new PageLinkLayer(new[] { link });

        Assert.Same(link, layer.HitTest(50, 30));
        Assert.Null(layer.HitTest(5, 30));
        Assert.Null(layer.HitTest(50, 50));
    }
}
