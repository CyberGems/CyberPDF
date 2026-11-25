namespace CyberPDF.Core.Documents;

/// <summary>
/// Page geometry in PDF points (1/72 inch), before any UI scaling.
/// Width and height already include the page's own rotation.
/// </summary>
public readonly record struct PageSize(float Width, float Height)
{
    public double AspectRatio => Height <= 0 ? 1 : Width / Height;
}
