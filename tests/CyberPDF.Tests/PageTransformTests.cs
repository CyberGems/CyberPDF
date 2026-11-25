using CyberPDF.Core.Documents;
using CyberPDF.Core.Rendering;
using Xunit;

namespace CyberPDF.Tests;

/// <summary>
/// Verifies the PDF-space to screen-space mapping. This math decides where every
/// selection highlight, link and text cursor lands, so an error here is visible
/// to the user on every click.
/// </summary>
public sealed class PageTransformTests
{
    // A rectangle page makes the orientation of every corner unambiguous.
    private static readonly (float, float) PageSize = (400f, 200f);

    private static PageTransform Create(int rotation, int width = 400, int height = 200)
        => new(new PageSize(PageSize.Item1, PageSize.Item2), width, height, rotation);

    [Fact]
    public void UnrotatedMapsCornersToTheExpectedQuadrants()
    {
        var t = Create(0);

        // PDF bottom-left is the view's bottom-left.
        AssertPoint(t.PdfToView(0, 0), 0, 200);
        // PDF top-left is the view's top-left.
        AssertPoint(t.PdfToView(0, 200), 0, 0);
        // PDF top-right is the view's top-right.
        AssertPoint(t.PdfToView(400, 200), 400, 0);
        // PDF bottom-right is the view's bottom-right.
        AssertPoint(t.PdfToView(400, 0), 400, 200);
    }

    [Fact]
    public void Rotated90MapsTheBottomLeftToTheTopLeft()
    {
        // Ground truth, measured by rendering a page with a distinct square in
        // each corner through PDFium at rotation 1: the PDF bottom-left corner
        // lands at the view's top-left, and the PDF bottom-right at the
        // view's bottom-left.
        var t = Create(90, width: 200, height: 400);

        AssertPoint(t.PdfToView(0, 0), 0, 0);
        AssertPoint(t.PdfToView(400, 0), 0, 400);
        AssertPoint(t.PdfToView(400, 200), 200, 400);
        AssertPoint(t.PdfToView(0, 200), 200, 0);
    }

    [Fact]
    public void Rotated180MapsTheBottomLeftToTheTopRight()
    {
        var t = Create(180);

        AssertPoint(t.PdfToView(0, 0), 400, 0);
        AssertPoint(t.PdfToView(0, 200), 400, 200);
        AssertPoint(t.PdfToView(400, 200), 0, 200);
        AssertPoint(t.PdfToView(400, 0), 0, 0);
    }

    [Fact]
    public void Rotated270MapsTheBottomLeftToTheBottomRight()
    {
        // Measured the same way at rotation 3: the PDF bottom-left corner lands
        // at the view's bottom-right.
        var t = Create(270, width: 200, height: 400);

        AssertPoint(t.PdfToView(0, 0), 200, 400);
        AssertPoint(t.PdfToView(400, 0), 200, 0);
        AssertPoint(t.PdfToView(400, 200), 0, 0);
        AssertPoint(t.PdfToView(0, 200), 0, 400);
    }

    [Theory]
    [InlineData(0, 400, 200)]
    [InlineData(90, 200, 400)]
    [InlineData(180, 400, 200)]
    [InlineData(270, 200, 400)]
    public void RoundTripPreservesThePoint(int rotation, int width, int height)
    {
        var t = Create(rotation, width, height);
        var samples = new[] { (0.0, 0.0), (400.0, 200.0), (123.0, 45.0), (399.0, 199.0), (200.0, 100.0) };

        foreach (var (pdfX, pdfY) in samples)
        {
            var (viewX, viewY) = t.PdfToView(pdfX, pdfY);
            var (backX, backY) = t.ViewToPdf(viewX, viewY);
            Assert.Equal(pdfX, backX, 6);
            Assert.Equal(pdfY, backY, 6);
        }
    }

    [Fact]
    public void CenterOfThePageMapsToTheCenterOfTheViewInEveryRotation()
    {
        foreach (var rotation in new[] { 0, 90, 180, 270 })
        {
            var width = rotation is 90 or 270 ? 200 : 400;
            var height = rotation is 90 or 270 ? 400 : 200;
            var t = Create(rotation, width, height);

            AssertPoint(t.PdfToView(200, 100), width / 2.0, height / 2.0);
        }
    }

    [Fact]
    public void ScaledPageMapsProportionally()
    {
        // Same page drawn at 800x400: PDF points double.
        var t = Create(0, width: 800, height: 400);

        AssertPoint(t.PdfToView(0, 0), 0, 400);
        AssertPoint(t.PdfToView(400, 200), 800, 0);
        AssertPoint(t.PdfToView(200, 100), 400, 200);
    }

    [Fact]
    public void PixelsPerPointReflectsTheDrawSize()
    {
        var t = Create(0, width: 800, height: 400);
        Assert.Equal(2.0, t.PixelsPerPoint, 6);
    }

    [Fact]
    public void RotationIsNormalizedToQuarterTurns()
    {
        Assert.Equal(0, new PageTransform(new PageSize(400, 200), 400, 200, 45).Rotation);
        Assert.Equal(90, new PageTransform(new PageSize(400, 200), 400, 200, 450).Rotation);
        Assert.Equal(270, new PageTransform(new PageSize(400, 200), 400, 200, -90).Rotation);
    }

    [Fact]
    public void DegenerateTargetReturnsOriginInsteadOfThrowing()
    {
        var t = new PageTransform(new PageSize(400, 200), 0, 0, 0);

        var (x, y) = t.ViewToPdf(10, 10);
        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }

    private static void AssertPoint((double X, double Y) actual, double expectedX, double expectedY)
    {
        Assert.Equal(expectedX, actual.X, 6);
        Assert.Equal(expectedY, actual.Y, 6);
    }
}
