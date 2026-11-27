using System.Runtime.InteropServices;
using CyberPDF.Common.I18n;
using CyberPDF.Common.Settings;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>
/// Suite-standard confirmation modal shown when closing the window with
/// <see cref="CloseBehaviors.Ask"/>: lets the user minimize to the system tray
/// (<c>Enter</c>), quit (<c>Space</c>), and optionally remember the choice.
/// </summary>
public sealed class CloseConfirmDialog : Form
{
    private readonly ThemedToolTip _toolTip = new();
    private readonly Panel _header = new();
    private readonly Label _titleLabel = new();
    private readonly ToolButton _closeBtn = new();
    private readonly Label _descLabel = new();
    private readonly ActionCard _trayCard;
    private readonly ActionCard _quitCard;
    private readonly RememberCheckRow _rememberRow;

    public CloseConfirmDialog()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Padding = new Padding(1);
        DoubleBuffered = true;
        KeyPreview = true;
        Text = Strings.Get("CloseDialogTitle");

        var scale = DeviceDpi <= 0 ? 1f : DeviceDpi / 96f;
        int S(int px) => (int)Math.Round(px * scale);

        ClientSize = new Size(S(500), S(326));
        var pad = S(24);
        var width = ClientSize.Width - pad * 2;

        _header.Bounds = new Rectangle(1, 1, ClientSize.Width - 2, S(52));
        _header.BackColor = Color.Transparent;
        _header.MouseDown += DragMouseDown;

        _titleLabel.Bounds = new Rectangle(pad - 1, S(14), width - S(40), S(28));
        _titleLabel.BackColor = Color.Transparent;
        _titleLabel.ForeColor = Theme.Text;
        _titleLabel.Font = new Font("Segoe UI", 12.5f, FontStyle.Bold, GraphicsUnit.Point);
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        _titleLabel.Text = Strings.Get("CloseDialogTitle");
        _titleLabel.MouseDown += DragMouseDown;

        _closeBtn.Icon = IconKind.Close;
        _closeBtn.Bounds = new Rectangle(_header.Width - S(44), S(12), S(30), S(28));
        _closeBtn.IsWindowChrome = true;
        _closeBtn.IsDanger = true;
        _closeBtn.CornerRadius = 6;
        _closeBtn.AccessibleName = Strings.Get("CloseWindow");
        _closeBtn.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        _toolTip.Attach(_closeBtn, () => Strings.Get("CloseWindow"), () => "Esc");

        _header.Controls.Add(_titleLabel);
        _header.Controls.Add(_closeBtn);
        Controls.Add(_header);

        var y = S(58);
        _descLabel.Bounds = new Rectangle(pad, y, width, S(42));
        _descLabel.BackColor = Color.Transparent;
        _descLabel.ForeColor = Theme.TextMuted;
        _descLabel.Font = new Font("Segoe UI", 9.25f, FontStyle.Regular, GraphicsUnit.Point);
        _descLabel.Text = Strings.Get("CloseDialogDesc");
        Controls.Add(_descLabel);
        y += S(54);

        _trayCard = new ActionCard(
            IconKind.TrayMinimize,
            Theme.Accent,
            Color.FromArgb(32, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B),
            Color.FromArgb(95, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B),
            Strings.Get("CloseOptionTrayTitle"),
            Strings.Get("CloseOptionTrayDesc"),
            "↵",
            scale)
        {
            Bounds = new Rectangle(pad, y, width, S(68)),
        };
        _trayCard.Click += (_, _) => SelectChoice(CloseBehaviors.Tray);
        Controls.Add(_trayCard);
        y += S(80);

        var danger = Theme.DangerHover;
        _quitCard = new ActionCard(
            IconKind.Power,
            danger,
            Color.FromArgb(36, danger.R, danger.G, danger.B),
            Color.FromArgb(95, danger.R, danger.G, danger.B),
            Strings.Get("CloseOptionQuitTitle"),
            Strings.Get("CloseOptionQuitDesc"),
            Strings.Get("CloseKeySpace"),
            scale)
        {
            Bounds = new Rectangle(pad, y, width, S(68)),
        };
        _quitCard.Click += (_, _) => SelectChoice(CloseBehaviors.Quit);
        Controls.Add(_quitCard);
        y += S(84);

        _rememberRow = new RememberCheckRow(Strings.Get("RememberChoice"), scale)
        {
            Bounds = new Rectangle(pad, y, width, S(28)),
        };
        Controls.Add(_rememberRow);
    }

    public string Choice { get; private set; } = CloseBehaviors.Quit;

    public bool RememberChoice => _rememberRow.Checked;

    private void SelectChoice(string choice)
    {
        Choice = choice;
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }
        if (keyData == Keys.Enter)
        {
            SelectChoice(CloseBehaviors.Tray);
            return true;
        }
        if (keyData == Keys.Space && !_rememberRow.Focused)
        {
            SelectChoice(CloseBehaviors.Quit);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(Theme.BorderStrong);
        e.Graphics.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    private void DragMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }
        ReleaseCapture();
        SendMessage(Handle, 0x00A1, 2, IntPtr.Zero);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    private sealed class ActionCard : Control
    {
        private readonly IconKind _icon;
        private readonly Color _iconInk;
        private readonly Color _badgeFill;
        private readonly Color _badgeBorder;
        private readonly string _title;
        private readonly string _subtitle;
        private readonly string _keyLabel;
        private readonly float _scale;
        private bool _hovered;

        public ActionCard(
            IconKind icon,
            Color iconInk,
            Color badgeFill,
            Color badgeBorder,
            string title,
            string subtitle,
            string keyLabel,
            float scale)
        {
            _icon = icon;
            _iconInk = iconInk;
            _badgeFill = badgeFill;
            _badgeBorder = badgeBorder;
            _title = title;
            _subtitle = subtitle;
            _keyLabel = keyLabel;
            _scale = scale;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint
                | ControlStyles.Selectable,
                true);
            Cursor = Cursors.Hand;
            AccessibleName = $"{title}. {subtitle}";
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            ThemePainter.PrepareHighQuality(g);
            int S(int px) => (int)Math.Round(px * _scale);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            ThemePainter.FillRounded(g, rect, S(10), _hovered ? Theme.Hover : Theme.Surface);
            ThemePainter.DrawRounded(g, rect, S(10), _hovered ? Theme.BorderStrong : Theme.Border);

            var badgeSize = S(40);
            var badgeRect = new Rectangle(S(14), (Height - badgeSize) / 2, badgeSize, badgeSize);
            ThemePainter.FillRounded(g, badgeRect, S(10), _badgeFill);
            ThemePainter.DrawRounded(g, badgeRect, S(10), _badgeBorder);

            var glyphRect = Rectangle.Inflate(badgeRect, -S(11), -S(11));
            Icons.Draw(g, _icon, glyphRect, _iconInk, 1.55f);

            using var keyFont = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
            var keyMeasured = TextRenderer.MeasureText(g, _keyLabel, keyFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            var chipW = Math.Max(S(24), keyMeasured.Width + S(12));
            var chipH = keyMeasured.Height + S(6);
            var chipRect = new Rectangle(Width - S(14) - chipW, (Height - chipH) / 2, chipW, chipH);
            ThemePainter.FillRounded(g, chipRect, S(5), Theme.SurfaceAlt);
            ThemePainter.DrawRounded(g, chipRect, S(5), Theme.BorderStrong);
            TextRenderer.DrawText(
                g,
                _keyLabel,
                keyFont,
                chipRect,
                Theme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            var textX = badgeRect.Right + S(12);
            var textW = Math.Max(40, chipRect.Left - textX - S(10));
            using var titleFont = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Point);
            using var subFont = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            var midY = Height / 2;

            TextRenderer.DrawText(
                g,
                _title,
                titleFont,
                new Rectangle(textX, midY - S(20), textW, S(20)),
                Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.Bottom | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(
                g,
                _subtitle,
                subFont,
                new Rectangle(textX, midY + S(2), textW, S(18)),
                Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    private sealed class RememberCheckRow : Control
    {
        private readonly string _label;
        private readonly float _scale;
        private bool _checked;
        private bool _hovered;

        public RememberCheckRow(string label, float scale)
        {
            _label = label;
            _scale = scale;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint
                | ControlStyles.Selectable,
                true);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleName = label;
        }

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value)
                {
                    return;
                }
                _checked = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Checked = !Checked;
            }
            base.OnMouseClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                Checked = !Checked;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            ThemePainter.PrepareHighQuality(g);
            int S(int px) => (int)Math.Round(px * _scale);

            var boxSize = S(16);
            var boxRect = new Rectangle(S(2), (Height - boxSize) / 2, boxSize, boxSize);
            if (_checked)
            {
                ThemePainter.FillRounded(g, boxRect, S(4), Theme.Accent);
                Icons.Draw(g, IconKind.Check, Rectangle.Inflate(boxRect, -S(2), -S(2)), Theme.AccentText, 1.65f);
            }
            else
            {
                ThemePainter.FillRounded(g, boxRect, S(4), Theme.Surface);
                ThemePainter.DrawRounded(g, boxRect, S(4), _hovered || Focused ? Theme.Accent : Theme.BorderStrong);
            }

            using var font = new Font("Segoe UI", 9.25f, FontStyle.Bold, GraphicsUnit.Point);
            var textRect = new Rectangle(boxRect.Right + S(10), 0, Math.Max(20, Width - boxRect.Right - S(10)), Height);
            TextRenderer.DrawText(
                g,
                _label,
                font,
                textRect,
                Theme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
