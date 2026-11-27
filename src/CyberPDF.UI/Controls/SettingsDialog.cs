using System.Reflection;
using System.Runtime.InteropServices;
using CyberPDF.Common.I18n;
using CyberPDF.Common.Settings;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>
/// Compact themed settings and about dialog. Theme and language changes apply
/// immediately to the running window.
/// </summary>
public sealed class SettingsDialog : Form
{
    private readonly AppSettings _settings;
    private readonly Action<AppTheme> _onThemeChanged;
    private readonly Action<Lang> _onLanguageChanged;
    private readonly Action _onSettingsSaved;

    private readonly Panel _titleBar = new();
    private readonly Label _titleLabel = new();
    private readonly ToolButton _closeButton = new();
    private readonly ToolButton _okButton = new();

    private readonly Label _themeHeader = new();
    private readonly ToolButton _themeDefaultBtn = new();
    private readonly ToolButton _themeGrayBtn = new();
    private readonly ToolButton _themeLightBtn = new();

    private readonly Label _langHeader = new();
    private readonly ToolButton _langEnBtn = new();
    private readonly ToolButton _langEsBtn = new();

    private readonly ToolButton _rememberPosBtn = new();
    private readonly ToolButton _restoreWinBtn = new();

    private readonly Label _cacheHeader = new();
    private readonly List<ToolButton> _cacheButtons = new();
    private readonly Label _aboutLabel = new();

    public SettingsDialog(
        AppSettings settings,
        Action<AppTheme> onThemeChanged,
        Action<Lang> onLanguageChanged,
        Action onSettingsSaved)
    {
        _settings = settings;
        _onThemeChanged = onThemeChanged;
        _onLanguageChanged = onLanguageChanged;
        _onSettingsSaved = onSettingsSaved;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        Padding = new Padding(1);
        DoubleBuffered = true;
        KeyPreview = true;

        var scale = DeviceDpi <= 0 ? 1f : DeviceDpi / 96f;
        ClientSize = new Size((int)Math.Round(460 * scale), (int)Math.Round(430 * scale));

        BuildControls(scale);
        RefreshContent();
    }

    private void BuildControls(float scale)
    {
        _titleBar.Dock = DockStyle.Top;
        _titleBar.Height = (int)Math.Round(34 * scale);
        _titleBar.MouseDown += TitleBarMouseDown;

        _titleLabel.Dock = DockStyle.Fill;
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
        _closeButton.Click += (_, _) => Close();

        _titleBar.Controls.Add(_titleLabel);
        _titleBar.Controls.Add(_closeButton);
        Controls.Add(_titleBar);

        var pad = (int)Math.Round(20 * scale);
        var width = ClientSize.Width - pad * 2;
        var y = _titleBar.Height + (int)Math.Round(16 * scale);
        var btnH = (int)Math.Round(32 * scale);

        _themeHeader.Bounds = new Rectangle(pad, y, width, (int)Math.Round(20 * scale));
        Controls.Add(_themeHeader);
        y += (int)Math.Round(24 * scale);

        var thirdW = (width - (int)Math.Round(12 * scale)) / 3;
        ConfigureOptionButton(_themeDefaultBtn, IconKind.ThemeDefault, new Rectangle(pad, y, thirdW, btnH), () => SelectTheme(AppTheme.Default));
        ConfigureOptionButton(_themeGrayBtn, IconKind.ThemeGray, new Rectangle(pad + thirdW + (int)Math.Round(6 * scale), y, thirdW, btnH), () => SelectTheme(AppTheme.Gray));
        ConfigureOptionButton(_themeLightBtn, IconKind.ThemeLight, new Rectangle(pad + (thirdW + (int)Math.Round(6 * scale)) * 2, y, thirdW, btnH), () => SelectTheme(AppTheme.Light));

        y += (int)Math.Round(46 * scale);
        _langHeader.Bounds = new Rectangle(pad, y, width, (int)Math.Round(20 * scale));
        Controls.Add(_langHeader);
        y += (int)Math.Round(24 * scale);

        var halfW = (width - (int)Math.Round(8 * scale)) / 2;
        ConfigureOptionButton(_langEnBtn, IconKind.None, new Rectangle(pad, y, halfW, btnH), () => SelectLanguage(Lang.En));
        ConfigureOptionButton(_langEsBtn, IconKind.None, new Rectangle(pad + halfW + (int)Math.Round(8 * scale), y, halfW, btnH), () => SelectLanguage(Lang.Es));

        y += (int)Math.Round(48 * scale);
        ConfigureOptionButton(_rememberPosBtn, IconKind.Check, new Rectangle(pad, y, width, btnH), () =>
        {
            _settings.RememberPagePosition = !_settings.RememberPagePosition;
            _onSettingsSaved();
            RefreshContent();
        });

        y += (int)Math.Round(38 * scale);
        ConfigureOptionButton(_restoreWinBtn, IconKind.Check, new Rectangle(pad, y, width, btnH), () =>
        {
            _settings.RestoreSession = !_settings.RestoreSession;
            _onSettingsSaved();
            RefreshContent();
        });

        y += (int)Math.Round(48 * scale);
        _cacheHeader.Bounds = new Rectangle(pad, y, width, (int)Math.Round(20 * scale));
        Controls.Add(_cacheHeader);
        y += (int)Math.Round(24 * scale);

        var budgets = new[] { 64, 128, 256, 512 };
        var quarterW = (width - (int)Math.Round(18 * scale)) / 4;
        for (var i = 0; i < budgets.Length; i++)
        {
            var mb = budgets[i];
            var btn = new ToolButton { Tag = mb };
            ConfigureOptionButton(
                btn,
                IconKind.None,
                new Rectangle(pad + i * (quarterW + (int)Math.Round(6 * scale)), y, quarterW, btnH),
                () =>
                {
                    _settings.RenderCacheBudgetMb = mb;
                    _onSettingsSaved();
                    RefreshContent();
                });
            _cacheButtons.Add(btn);
        }

        _okButton.ShowBorder = true;
        _okButton.IsActive = true;
        _okButton.Size = new Size((int)Math.Round(96 * scale), (int)Math.Round(34 * scale));
        _okButton.Location = new Point(ClientSize.Width - pad - _okButton.Width, ClientSize.Height - pad - _okButton.Height);
        _okButton.Click += (_, _) => Close();
        Controls.Add(_okButton);

        _aboutLabel.Bounds = new Rectangle(pad, _okButton.Top, ClientSize.Width - pad * 3 - _okButton.Width, _okButton.Height);
        _aboutLabel.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(_aboutLabel);
    }

    private void ConfigureOptionButton(ToolButton button, IconKind icon, Rectangle bounds, Action onClick)
    {
        button.Icon = icon;
        button.Bounds = bounds;
        button.ShowBorder = true;
        button.Click += (_, _) => onClick();
        Controls.Add(button);
    }

    private void SelectTheme(AppTheme theme)
    {
        _onThemeChanged(theme);
        RefreshContent();
    }

    private void SelectLanguage(Lang lang)
    {
        _onLanguageChanged(lang);
        RefreshContent();
    }

    private void RefreshContent()
    {
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        _titleBar.BackColor = Theme.Surface;
        _titleLabel.ForeColor = Theme.Text;
        _themeHeader.ForeColor = Theme.TextMuted;
        _langHeader.ForeColor = Theme.TextMuted;
        _cacheHeader.ForeColor = Theme.TextMuted;
        _aboutLabel.ForeColor = Theme.TextMuted;

        _titleLabel.Text = Strings.Get("Settings");
        _closeButton.AccessibleName = Strings.Get("CloseWindow");
        _themeHeader.Text = Strings.Get("Theme");
        _themeDefaultBtn.Text = Strings.Get("ThemeDefault");
        _themeGrayBtn.Text = Strings.Get("ThemeGray");
        _themeLightBtn.Text = Strings.Get("ThemeLight");
        _themeDefaultBtn.IsActive = Theme.CurrentTheme == AppTheme.Default;
        _themeGrayBtn.IsActive = Theme.CurrentTheme == AppTheme.Gray;
        _themeLightBtn.IsActive = Theme.CurrentTheme == AppTheme.Light;

        _langHeader.Text = Strings.Get("Language");
        _langEnBtn.Text = Strings.Get("LanguageEn");
        _langEsBtn.Text = Strings.Get("LanguageEs");
        _langEnBtn.IsActive = Strings.Current == Lang.En;
        _langEsBtn.IsActive = Strings.Current == Lang.Es;

        _rememberPosBtn.Text = Strings.Get("RememberPagePosition");
        _rememberPosBtn.Icon = _settings.RememberPagePosition ? IconKind.Check : IconKind.None;
        _rememberPosBtn.IsActive = _settings.RememberPagePosition;

        _restoreWinBtn.Text = Strings.Get("RestoreSession");
        _restoreWinBtn.Icon = _settings.RestoreSession ? IconKind.Check : IconKind.None;
        _restoreWinBtn.IsActive = _settings.RestoreSession;

        _cacheHeader.Text = Strings.Get("RenderCacheBudget");
        foreach (var btn in _cacheButtons)
        {
            if (btn.Tag is int mb)
            {
                btn.Text = Strings.Format("CacheMbFormat", mb);
                btn.IsActive = _settings.RenderCacheBudgetMb == mb;
            }
            btn.Invalidate();
        }

        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        var verText = ver is null ? "0.1.0" : $"{ver.Major}.{ver.Minor}.{ver.Build}";
        _aboutLabel.Text = $"{Strings.Get("AppTitle")} · {Strings.Format("VersionLabel", verText)} · PDFium";
        _okButton.Text = Strings.Get("Accept");

        foreach (Control control in Controls)
        {
            control.Invalidate();
        }
        Invalidate();
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
