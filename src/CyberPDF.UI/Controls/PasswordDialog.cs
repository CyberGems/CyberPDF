using System.Runtime.InteropServices;
using CyberPDF.Common.I18n;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>
/// Frameless themed prompt for password-protected PDFs.
/// </summary>
public sealed class PasswordDialog : Form
{
    private readonly Panel _titleBar = new();
    private readonly Label _titleLabel = new();
    private readonly ToolButton _closeButton = new();
    private readonly Panel _inputHost = new();
    private readonly UndoableTextBox _passwordBox = new();
    private readonly ToolButton _acceptButton = new();
    private readonly ToolButton _cancelButton = new();
    private readonly Label _fileLabel = new();
    private readonly Label _promptLabel = new();
    private readonly Label _errorLabel = new();

    public PasswordDialog(string fileName, bool wrongPassword)
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

        var scale = DeviceDpi <= 0 ? 1f : DeviceDpi / 96f;
        ClientSize = new Size((int)Math.Round(400 * scale), (int)Math.Round((wrongPassword ? 236 : 210) * scale));

        _titleBar.Dock = DockStyle.Top;
        _titleBar.Height = (int)Math.Round(34 * scale);
        _titleBar.BackColor = Theme.Surface;
        _titleBar.MouseDown += TitleBarMouseDown;

        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.Text = Strings.Get("PasswordRequired");
        _titleLabel.ForeColor = Theme.Text;
        _titleLabel.BackColor = Color.Transparent;
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        _titleLabel.Padding = new Padding((int)Math.Round(12 * scale), 0, 0, 0);
        _titleLabel.MouseDown += TitleBarMouseDown;

        _closeButton.Icon = IconKind.Close;
        _closeButton.Dock = DockStyle.Right;
        _closeButton.Width = (int)Math.Round(40 * scale);
        _closeButton.IsWindowChrome = true;
        _closeButton.CornerRadius = 0;
        _closeButton.IsDanger = true;
        _closeButton.AccessibleName = Strings.Get("Cancel");
        _closeButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(_closeButton);
        Controls.Add(_titleBar);

        var pad = (int)Math.Round(18 * scale);
        var y = _titleBar.Height + (int)Math.Round(16 * scale);
        var contentWidth = ClientSize.Width - pad * 2;

        _fileLabel.Text = fileName;
        _fileLabel.ForeColor = Theme.Text;
        _fileLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point);
        _fileLabel.Bounds = new Rectangle(pad + (int)Math.Round(24 * scale), y, contentWidth - (int)Math.Round(24 * scale), (int)Math.Round(20 * scale));
        _fileLabel.AutoEllipsis = true;
        Controls.Add(_fileLabel);

        var lockIcon = new Panel
        {
            Bounds = new Rectangle(pad, y + (int)Math.Round(2 * scale), (int)Math.Round(16 * scale), (int)Math.Round(16 * scale)),
            BackColor = Color.Transparent,
        };
        lockIcon.Paint += (_, e) =>
        {
            ThemePainter.PrepareHighQuality(e.Graphics);
            Icons.Draw(e.Graphics, IconKind.Lock, lockIcon.ClientRectangle, Theme.Accent, 1.45f);
        };
        Controls.Add(lockIcon);

        y += (int)Math.Round(26 * scale);
        _promptLabel.Text = Strings.Get("PasswordPrompt");
        _promptLabel.ForeColor = Theme.TextMuted;
        _promptLabel.Bounds = new Rectangle(pad, y, contentWidth, (int)Math.Round(20 * scale));
        Controls.Add(_promptLabel);

        y += (int)Math.Round(26 * scale);
        _inputHost.Bounds = new Rectangle(pad, y, contentWidth, (int)Math.Round(34 * scale));
        _inputHost.BackColor = Theme.Bg;
        _inputHost.Padding = new Padding((int)Math.Round(10 * scale), (int)Math.Round(7 * scale), (int)Math.Round(10 * scale), (int)Math.Round(6 * scale));
        _inputHost.Paint += (_, e) =>
        {
            ThemePainter.PrepareHighQuality(e.Graphics);
            var rect = new Rectangle(0, 0, _inputHost.Width - 1, _inputHost.Height - 1);
            ThemePainter.FillRounded(e.Graphics, rect, 6, Theme.Surface);
            ThemePainter.DrawRounded(e.Graphics, rect, 6, _passwordBox.Focused ? Theme.Accent : Theme.Border);
        };

        _passwordBox.BorderStyle = BorderStyle.None;
        _passwordBox.Dock = DockStyle.Fill;
        _passwordBox.BackColor = Theme.Surface;
        _passwordBox.ForeColor = Theme.Text;
        _passwordBox.UseSystemPasswordChar = true;
        _passwordBox.PlaceholderText = Strings.Get("Password");
        _passwordBox.GotFocus += (_, _) => _inputHost.Invalidate();
        _passwordBox.LostFocus += (_, _) => _inputHost.Invalidate();
        _passwordBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                AcceptAndClose();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
        _inputHost.Controls.Add(_passwordBox);
        Controls.Add(_inputHost);

        y += (int)Math.Round(40 * scale);
        if (wrongPassword)
        {
            _errorLabel.Text = Strings.Get("WrongPassword");
            _errorLabel.ForeColor = Theme.DangerHover;
            _errorLabel.Bounds = new Rectangle(pad, y, contentWidth, (int)Math.Round(20 * scale));
            Controls.Add(_errorLabel);
            y += (int)Math.Round(24 * scale);
        }

        _acceptButton.Text = Strings.Get("Accept");
        _acceptButton.ShowBorder = true;
        _acceptButton.IsActive = true;
        _acceptButton.Size = new Size(Math.Max((int)Math.Round(96 * scale), _acceptButton.MeasurePreferredSize(scale).Width), (int)Math.Round(34 * scale));
        _acceptButton.Location = new Point(ClientSize.Width - pad - _acceptButton.Width, ClientSize.Height - pad - _acceptButton.Height);
        _acceptButton.Click += (_, _) => AcceptAndClose();
        Controls.Add(_acceptButton);

        _cancelButton.Text = Strings.Get("Cancel");
        _cancelButton.ShowBorder = true;
        _cancelButton.Size = new Size(Math.Max((int)Math.Round(96 * scale), _cancelButton.MeasurePreferredSize(scale).Width), (int)Math.Round(34 * scale));
        _cancelButton.Location = new Point(_acceptButton.Left - (int)Math.Round(8 * scale) - _cancelButton.Width, _acceptButton.Top);
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        Controls.Add(_cancelButton);

        Shown += (_, _) => _passwordBox.Focus();
    }

    public string Password => _passwordBox.Text;

    private void AcceptAndClose()
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(Theme.BorderStrong);
        e.Graphics.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    private void TitleBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }
        ReleaseCapture();
        SendMessage(Handle, 0x00A1, 2, IntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);
}
