using System.Globalization;
using System.Runtime.InteropServices;
using CyberPDF.Common.I18n;
using CyberPDF.Core.Documents;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>
/// Frameless themed modal displaying PDF metadata, file size, page count, and
/// physical dimensions.
/// </summary>
public sealed class DocumentPropertiesDialog : Form
{
    private readonly Panel _titleBar = new();
    private readonly Label _titleLabel = new();
    private readonly ToolButton _closeButton = new();
    private readonly ToolButton _okButton = new();

    public DocumentPropertiesDialog(PdfDocument document, PdfMetadata metadata, string? filePath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(metadata);

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
        ClientSize = new Size((int)Math.Round(480 * scale), (int)Math.Round(390 * scale));

        _titleBar.Dock = DockStyle.Top;
        _titleBar.Height = (int)Math.Round(34 * scale);
        _titleBar.BackColor = Theme.Surface;
        _titleBar.MouseDown += TitleBarMouseDown;

        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.Text = Strings.Get("DocumentProperties");
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
        _closeButton.AccessibleName = Strings.Get("CloseWindow");
        _closeButton.Click += (_, _) => Close();

        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(_closeButton);
        Controls.Add(_titleBar);

        var none = Strings.Get("PropNone");
        var fileSizeText = none;
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            var bytes = new FileInfo(filePath).Length;
            fileSizeText = FormatBytes(bytes);
        }

        var pageSizeText = none;
        if (document.PageCount > 0)
        {
            var size = document.GetPageSize(0);
            var mmW = size.Width * 25.4 / 72.0;
            var mmH = size.Height * 25.4 / 72.0;
            var inW = size.Width / 72.0;
            var inH = size.Height / 72.0;
            pageSizeText = string.Create(
                CultureInfo.CurrentCulture,
                $"{mmW:F0} × {mmH:F0} mm ({inW:F1} × {inH:F1} in)");
        }

        var rows = new (string Label, string Value)[]
        {
            (Strings.Get("PropFile"), string.IsNullOrEmpty(filePath) ? none : filePath),
            (Strings.Get("PropSize"), fileSizeText),
            (Strings.Get("PropPages"), document.PageCount.ToString(CultureInfo.CurrentCulture)),
            (Strings.Get("PropPageDimensions"), pageSizeText),
            (Strings.Get("PropTitle"), metadata.Title ?? none),
            (Strings.Get("PropAuthor"), metadata.Author ?? none),
            (Strings.Get("PropSubject"), metadata.Subject ?? none),
            (Strings.Get("PropCreator"), metadata.Creator ?? none),
            (Strings.Get("PropProducer"), metadata.Producer ?? none),
        };

        var pad = (int)Math.Round(20 * scale);
        var y = _titleBar.Height + (int)Math.Round(14 * scale);
        var labelW = (int)Math.Round(135 * scale);
        var valueW = ClientSize.Width - pad * 2 - labelW - (int)Math.Round(8 * scale);
        var rowH = (int)Math.Round(28 * scale);

        foreach (var (label, value) in rows)
        {
            var lbl = new Label
            {
                Text = label,
                ForeColor = Theme.TextMuted,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(pad, y, labelW, rowH),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
            };
            var val = new Label
            {
                Text = value,
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Bounds = new Rectangle(pad + labelW + (int)Math.Round(8 * scale), y, valueW, rowH),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
            };
            Controls.Add(lbl);
            Controls.Add(val);
            y += rowH;
        }

        _okButton.Text = Strings.Get("Accept");
        _okButton.ShowBorder = true;
        _okButton.IsActive = true;
        _okButton.Size = new Size((int)Math.Round(96 * scale), (int)Math.Round(34 * scale));
        _okButton.Location = new Point(ClientSize.Width - pad - _okButton.Width, ClientSize.Height - pad - _okButton.Height);
        _okButton.Click += (_, _) => Close();
        Controls.Add(_okButton);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }
        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F1} KB";
        }
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Escape or Keys.Enter)
        {
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
