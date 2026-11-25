namespace CyberPDF.Core.Rendering;

/// <summary>
/// A finished render plus the ownership rule for its pixels. When
/// <see cref="CallerOwns"/> is true the receiver must dispose the page; when it
/// is false the cache owns it and the receiver must only borrow it.
/// </summary>
public readonly struct RenderResult
{
    internal RenderResult(RenderedPage page, bool callerOwns)
    {
        Page = page;
        CallerOwns = callerOwns;
    }

    public RenderedPage Page { get; }

    public bool CallerOwns { get; }
}
