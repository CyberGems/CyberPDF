using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>
/// Lightweight themed tooltip shared by every control in the window.
/// </summary>
/// <remarks>
/// The native WinForms tooltip paints an unstyled system popup that ignores the
/// palette, so the suite owns its own: a small window that draws the label and,
/// when there is one, the shortcut on the same line as a chip.
///
/// Guidance: a tooltip is contextual help, not decoration. Attach one to an
/// icon-only control, to a control with a shortcut that is not visible
/// elsewhere, or to a value that can be truncated. Do not repeat the visible
/// label of a text button and never use a tooltip as the only accessible name.
/// </remarks>
public sealed class ThemedToolTip : IDisposable
{
    private const int ShowDelayMs = 450;
    private const int TextPadX = 10;
    private const int TextPadY = 7;
    private const int ChipGap = 8;
    private static readonly Size ChipPadding = new(5, 1);

    private readonly System.Windows.Forms.Timer _delay = new() { Interval = ShowDelayMs };
    private readonly Font _font = new("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font _shortcutFont = new("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Dictionary<Control, Entry> _entries = new();
    private readonly TooltipWindow _window;

    private Control? _pending;

    public ThemedToolTip()
    {
        _window = new TooltipWindow(_font, _shortcutFont);
        _delay.Tick += (_, _) =>
        {
            _delay.Stop();
            if (_pending is not null && _pending.IsHandleCreated && _pending.Visible)
            {
                Show(_pending);
            }
        };
    }

    private sealed class Entry
    {
        public required Func<string> Text { get; init; }

        public Func<string?>? Shortcut { get; init; }
    }

    /// <summary>
    /// Attaches a tooltip. The functions run when the tip is about to show, so
    /// localized text and state-dependent labels stay correct after a language
    /// or window-state change without re-attaching anything.
    /// </summary>
    public void Attach(Control control, Func<string> text, Func<string?>? shortcut = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(text);
        _entries[control] = new Entry { Text = text, Shortcut = shortcut };
        control.MouseEnter += OnMouseEnter;
        control.MouseLeave += OnMouseLeave;
        control.MouseDown += OnMouseDown;
        control.HandleDestroyed += OnHandleDestroyed;
    }

    private void OnMouseEnter(object? sender, EventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }
        _pending = control;
        _delay.Stop();
        _delay.Start();
    }

    private void OnMouseLeave(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _pending))
        {
            _pending = null;
            _delay.Stop();
        }
        Hide();
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        // A click means the user already knows what the control does; leaving the
        // tip floating over the result would only hide it.
        _pending = null;
        _delay.Stop();
        Hide();
    }

    private void OnHandleDestroyed(object? sender, EventArgs e) => Hide();

    private void Show(Control control)
    {
        if (!_entries.TryGetValue(control, out var entry))
        {
            return;
        }

        var text = entry.Text();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _window.Show(control, text, entry.Shortcut?.Invoke());
    }

    private void Hide() => _window.Hide();

    /// <summary>Number of attached controls, for diagnostics.</summary>
    public int Count => _entries.Count;

    /// <summary>Shows a tip immediately, used by the visual verification pass.</summary>
    public void ShowNow(Control control) => Show(control);

    /// <summary>Saves the tip as painted, so its theme can be checked.</summary>
    public void CaptureTo(string path)
    {
        Theme.Apply(Theme.CurrentTheme);
        using var bitmap = new Bitmap(Math.Max(1, _window.Width), Math.Max(1, _window.Height));
        using (var graphics = Graphics.FromImage(bitmap))
        {
            // Paint into a bitmap through the real OnPaint path. The popup is
            // WS_EX_NOACTIVATE, and DrawToBitmap returns an empty surface for it,
            // so the tip has to be rendered by asking it to paint itself.
            _window.PaintInto(graphics);
        }
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        Console.Error.WriteLine($"SHELL: tooltip shot {path} ({bitmap.Width}x{bitmap.Height})");
    }

    public void Dispose()
    {
        _delay.Stop();
        _delay.Dispose();
        foreach (var control in _entries.Keys)
        {
            control.MouseEnter -= OnMouseEnter;
            control.MouseLeave -= OnMouseLeave;
            control.MouseDown -= OnMouseDown;
            control.HandleDestroyed -= OnHandleDestroyed;
        }
        _entries.Clear();
        _window.Dispose();
        _font.Dispose();
        _shortcutFont.Dispose();
    }

    /// <summary>
    /// The popup itself. A borderless topmost window that never takes focus, so
    /// hovering a button does not steal it from the page.
    /// </summary>
    private sealed class TooltipWindow : Form
    {
        private readonly Font _textFont;
        private readonly Font _chipFont;
        private string _text = string.Empty;
        private string? _shortcut;

        public TooltipWindow(Font textFont, Font chipFont)
        {
            _textFont = textFont;
            _chipFont = chipFont;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint,
                true);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW: no focus, not in Alt+Tab.
                parameters.ExStyle |= 0x08000000 | 0x00000080;
                return parameters;
            }
        }

        public void Show(Control anchor, string text, string? shortcut)
        {
            _text = text;
            _shortcut = shortcut;

            var textSize = TextRenderer.MeasureText(text, _textFont);
            var chipSize = shortcut is null ? Size.Empty : TextRenderer.MeasureText(shortcut, _chipFont);
            var chipWidth = shortcut is null ? 0 : chipSize.Width + ChipPadding.Width * 2 + ChipGap;
            var chipHeight = shortcut is null ? 0 : chipSize.Height + ChipPadding.Height * 2;

            var width = textSize.Width + TextPadX * 2 + chipWidth;
            var height = Math.Max(textSize.Height, chipHeight) + TextPadY * 2;

            var screen = Screen.FromControl(anchor).WorkingArea;
            var anchorScreen = anchor.PointToScreen(new Point(anchor.Width / 2, anchor.Bottom));

            var x = Math.Clamp(
                anchorScreen.X - width / 2,
                screen.Left + 2,
                Math.Max(screen.Left + 2, screen.Right - width - 2));

            var y = anchorScreen.Y + 4;
            if (y + height > screen.Bottom - 2)
            {
                // No room below: show above the control instead of clipping it.
                y = anchor.PointToScreen(Point.Empty).Y - height - 4;
            }

            BackColor = Theme.Bg;
            Bounds = new Rectangle(x, y, width, height);

            if (!Visible)
            {
                Show();
            }
            else
            {
                // Repositioning a visible tooltip must not reactivate the window.
                SetWindowPos(Handle, HwndTopmost, x, y, width, height, SwpNoActivate);
            }
            Invalidate();
        }

        /// <summary>Runs OnPaint against an arbitrary surface, for verification.</summary>
        public void PaintInto(Graphics graphics)
        {
            using var args = new PaintEventArgs(graphics, new Rectangle(Point.Empty, ClientSize));
            OnPaint(args);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            // No ClearType here: GDI+ refuses to render ClearType text into a
            // 32-bit surface with alpha, which is what a captured tooltip is, and
            // silently paints nothing. Anti-aliased grayscale works on screen and
            // in a capture alike.
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

            using (var fill = new SolidBrush(Theme.SurfaceAlt))
            {
                g.FillRectangle(fill, ClientRectangle);
            }
            using (var border = new Pen(Theme.BorderStrong))
            {
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            }

            // Centered over the full height, not over an offset box: a rect that
            // starts at the padding and keeps the control height centers the text
            // against a taller box and leaves it sitting below the shortcut chip.
            var textRect = new Rectangle(TextPadX, 0, Width - TextPadX, Height);
            TextRenderer.DrawText(
                g,
                _text,
                _textFont,
                textRect,
                Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            if (_shortcut is null)
            {
                return;
            }

            var textSize = TextRenderer.MeasureText(_text, _textFont);
            var chipSize = TextRenderer.MeasureText(_shortcut, _chipFont);
            var chipRect = new Rectangle(
                TextPadX + textSize.Width + ChipGap,
                (Height - (chipSize.Height + ChipPadding.Height * 2)) / 2,
                chipSize.Width + ChipPadding.Width * 2,
                chipSize.Height + ChipPadding.Height * 2);

            ThemePainter.FillRounded(g, chipRect, 3, Theme.Hover);
            ThemePainter.DrawRounded(g, chipRect, 3, Theme.Border);
            TextRenderer.DrawText(
                g,
                _shortcut,
                _chipFont,
                chipRect,
                Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private const uint SwpNoActivate = 0x0010;
        private static readonly IntPtr HwndTopmost = new(-1);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    }
}
