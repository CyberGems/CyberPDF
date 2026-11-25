using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>
/// Thin custom scrollbar matching the theme. The native WinForms scrollbar is
/// drawn by Windows and cannot follow the palette, and it also steals vertical
/// space the reader needs.
/// </summary>
public sealed class ThinScrollBar : Control
{
    private int _maximum = 100;
    private int _largeChange = 10;
    private int _value;
    private bool _dragging;
    private int _dragOffset;
    private bool _hoverTrack;

    public ThinScrollBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.UserPaint,
            true);
        Width = 12;
        Cursor = Cursors.Default;
        TabStop = false;
    }

    public event EventHandler? ValueChanged;

    public int Maximum
    {
        get => _maximum;
        set
        {
            var clamped = Math.Max(0, value);
            if (_maximum == clamped)
            {
                return;
            }
            _maximum = clamped;
            CoerceValue();
            Invalidate();
        }
    }

    public int LargeChange
    {
        get => _largeChange;
        set
        {
            _largeChange = Math.Max(1, value);
            CoerceValue();
            Invalidate();
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, 0, MaxValue);
            if (_value == clamped)
            {
                return;
            }
            _value = clamped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private int MaxValue => Math.Max(0, _maximum - _largeChange);

    private Rectangle ThumbBounds()
    {
        var track = ClientRectangle;
        if (track.Height <= 0 || _maximum <= 0)
        {
            return Rectangle.Empty;
        }

        var ratio = (double)_largeChange / (_maximum + _largeChange);
        var thumbHeight = Math.Max(30, (int)(track.Height * Math.Min(1, ratio)));
        var scrollable = Math.Max(1, track.Height - thumbHeight);
        var offset = MaxValue <= 0 ? 0 : (int)Math.Round((double)_value / MaxValue * scrollable);

        return new Rectangle(3, offset, Math.Max(1, track.Width - 6), thumbHeight);
    }

    private void CoerceValue()
    {
        var coerced = Math.Clamp(_value, 0, MaxValue);
        if (coerced != _value)
        {
            _value = coerced;
        }
    }

    public void ScrollTo(int value) => Value = value;

    /// <summary>Scrolls by a number of pixels, used by the mouse wheel.</summary>
    public void ScrollBy(int delta) => Value = _value + delta;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var thumb = ThumbBounds();
        if (thumb.Contains(e.Location))
        {
            _dragging = true;
            _dragOffset = e.Y - thumb.Y;
            Capture = true;
            return;
        }

        // Page up / page down when clicking the empty track above or below the
        // thumb, the behavior every scrollbar has.
        ScrollBy(e.Y < thumb.Y ? -_largeChange : _largeChange);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var thumb = ThumbBounds();

        if (_dragging)
        {
            var track = ClientRectangle;
            var scrollable = Math.Max(1, track.Height - thumb.Height);
            var target = e.Y - _dragOffset;
            var ratio = Math.Clamp((double)target / scrollable, 0, 1);
            Value = (int)Math.Round(ratio * MaxValue);
            return;
        }

        var over = thumb.Contains(e.Location);
        if (over != _hoverTrack)
        {
            _hoverTrack = over;
            Cursor = over ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging)
        {
            _dragging = false;
            Capture = false;
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_dragging)
        {
            _hoverTrack = false;
            Cursor = Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        ThemePainter.PrepareHighQuality(g);
        g.Clear(Theme.Canvas);

        var thumb = ThumbBounds();
        if (thumb.IsEmpty)
        {
            return;
        }

        var radius = Math.Min(thumb.Width / 2, 5);
        ThemePainter.FillRounded(g, thumb, radius, _dragging || _hoverTrack ? Theme.ScrollThumbHover : Theme.ScrollThumb);
    }
}
