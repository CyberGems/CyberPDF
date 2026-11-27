using CyberPDF.Common.Shell;
using Xunit;

namespace CyberPDF.Tests;

public sealed class TrayTooltipFormatterTests
{
    [Fact]
    public void Format_WithoutSegment_ReturnsAppNameOnly()
    {
        Assert.Equal("CyberPDF", TrayTooltipFormatter.Format(null));
        Assert.Equal("CyberPDF", TrayTooltipFormatter.Format(""));
        Assert.Equal("CyberPDF", TrayTooltipFormatter.Format("   "));
    }

    [Fact]
    public void Format_WithShortSegment_UsesMiddleDotSeparator()
    {
        Assert.Equal("CyberPDF · report.pdf", TrayTooltipFormatter.Format("  report.pdf  "));
    }

    [Fact]
    public void Format_WhenLong_CapsAt63CharsWithEllipsis()
    {
        var longName = new string('a', 80) + ".pdf";
        var formatted = TrayTooltipFormatter.Format(longName);

        Assert.Equal(TrayTooltipFormatter.MaxLength, formatted.Length);
        Assert.EndsWith("…", formatted, StringComparison.Ordinal);
        Assert.StartsWith("CyberPDF · ", formatted, StringComparison.Ordinal);
    }
}
