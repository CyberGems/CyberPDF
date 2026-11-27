using CyberPDF.Common.Shell;
using Xunit;

namespace CyberPDF.Tests;

public sealed class PdfAssociationHelperTests
{
    [Fact]
    public void BuildOpenCommand_QuotesExecutableAndPlaceholder()
    {
        var cmd = PdfAssociationHelper.BuildOpenCommand(@"  ""C:\Program Files\CyberPDF\CyberPDF.exe""  ");
        Assert.Equal("\"C:\\Program Files\\CyberPDF\\CyberPDF.exe\" \"%1\"", cmd);
    }

    [Fact]
    public void BuildDefaultIconValue_QuotesExecutableWithZeroIndex()
    {
        var icon = PdfAssociationHelper.BuildDefaultIconValue(@"C:\Apps\CyberPDF\CyberPDF.exe");
        Assert.Equal("\"C:\\Apps\\CyberPDF\\CyberPDF.exe\",0", icon);
    }

    [Fact]
    public void MatchesDefaultHandler_MatchesProgIdCaseInsensitively()
    {
        Assert.True(PdfAssociationHelper.MatchesDefaultHandler(
            "cyberpdf.document", null, null, null, @"C:\Other\CyberPDF.exe"));
    }

    [Fact]
    public void MatchesDefaultHandler_RespectsOtherUserChoiceWhenExeDiffers()
    {
        Assert.False(PdfAssociationHelper.MatchesDefaultHandler(
            "MSEdgePDF",
            @"C:\Program Files\Edge\msedge.exe",
            "MSEdgePDF",
            null,
            @"C:\Apps\CyberPDF\CyberPDF.exe"));
    }

    [Fact]
    public void MatchesDefaultHandler_FallsBackToCommandMatchWhenNoUserChoice()
    {
        Assert.True(PdfAssociationHelper.MatchesDefaultHandler(
            null,
            null,
            null,
            "\"C:\\Apps\\CyberPDF\\CyberPDF.exe\" \"%1\"",
            @"c:\apps\cyberpdf\CyberPDF.exe"));
    }
}
