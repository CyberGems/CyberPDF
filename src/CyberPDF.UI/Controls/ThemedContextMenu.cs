using CyberPDF.Common.I18n;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>One row in a themed context menu.</summary>
public sealed class ContextMenuItem
{
    public ContextMenuItem(
        string text,
        Action? action,
        IconKind icon = IconKind.None,
        bool enabled = true,
        Color? swatchColor = null,
        IReadOnlyList<ContextMenuItem>? children = null)
    {
        Text = text;
        Action = action;
        Icon = icon;
        Enabled = enabled;
        SwatchColor = swatchColor;
        Children = children;
    }

    public string Text { get; }

    public Action? Action { get; }

    public IconKind Icon { get; }

    public bool Enabled { get; }

    public Color? SwatchColor { get; }

    public IReadOnlyList<ContextMenuItem>? Children { get; }

    /// <summary>A separator row.</summary>
    public static ContextMenuItem Separator() => new(string.Empty, null);
}

/// <summary>
/// A context menu painted with the app's own palette. WinForms' native menu is
/// drawn by Windows and ignores the theme, which is why every surface in the
/// suite uses a custom one: the page (Copy, Select all) and every editable text
/// field (Undo, Redo, Cut, Copy, Paste, Select all, Delete).
/// </summary>
public sealed class ThemedContextMenu : ContextMenuStrip
{
    public ThemedContextMenu()
    {
        Renderer = new ThemedRenderer();
        BackColor = Theme.Surface;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
        ShowImageMargin = true;
        ShowCheckMargin = false;
        Padding = new Padding(4);
        DropShadowEnabled = true;
    }

    /// <summary>
    /// Builds a menu from items. Disabled rows stay visible, which is what the
    /// suite expects: the user sees the action exists but cannot apply yet.
    /// </summary>
    public static ThemedContextMenu Create(params ContextMenuItem[] items)
    {
        var menu = new ThemedContextMenu();
        PopulateItems(menu.Items, items, (ThemedRenderer)menu.Renderer, menu.Font);
        return menu;
    }

    private static void PopulateItems(
        ToolStripItemCollection target,
        IEnumerable<ContextMenuItem> items,
        ThemedRenderer renderer,
        Font font)
    {
        foreach (var item in items)
        {
            if (item.Action is null && item.Children is null && string.IsNullOrEmpty(item.Text))
            {
                target.Add(new ToolStripSeparator());
                continue;
            }

            var entry = new ToolStripMenuItem(item.Text)
            {
                Enabled = item.Enabled,
                ForeColor = item.Enabled ? Theme.Text : Theme.TextMuted,
                Tag = new MenuItemVisual(item.Icon, item.SwatchColor),
            };
            if (item.Action is { } action)
            {
                entry.Click += (_, _) => action();
            }
            if (item.Children is { Count: > 0 } children)
            {
                if (entry.DropDown is ToolStripDropDownMenu subMenu)
                {
                    subMenu.Renderer = renderer;
                    subMenu.BackColor = Theme.Surface;
                    subMenu.ForeColor = Theme.Text;
                    subMenu.Font = font;
                    subMenu.ShowImageMargin = true;
                    subMenu.ShowCheckMargin = false;
                    subMenu.Padding = new Padding(4);
                    subMenu.DropShadowEnabled = true;
                }
                PopulateItems(entry.DropDownItems, children, renderer, font);
            }
            target.Add(entry);
        }
    }

    private sealed record MenuItemVisual(IconKind Icon, Color? SwatchColor);

    /// <summary>
    /// Renders every row and its vector icon with the theme palette instead of
    /// system colors, so the menu matches the app in all three themes.
    /// </summary>
    private sealed class ThemedRenderer : ToolStripProfessionalRenderer
    {
        public ThemedRenderer() : base(new ThemedColors())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var bounds = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
            var g = e.Graphics;
            ThemePainter.PrepareHighQuality(g);

            if (e.Item.Selected && e.Item.Enabled)
            {
                ThemePainter.FillRounded(g, bounds, 4, Theme.Hover);
            }
            else if (e.Item.Pressed && e.Item.Enabled)
            {
                ThemePainter.FillRounded(g, bounds, 4, Theme.Pressed);
            }

            if (e.Item.Tag is MenuItemVisual visual)
            {
                var iconSize = Math.Clamp(e.Item.Height - 10, 12, 16);
                var iconRect = new Rectangle(8, (e.Item.Height - iconSize) / 2, iconSize, iconSize);
                if (visual.SwatchColor is { } swatch)
                {
                    var diameter = 10;
                    var dotRect = new Rectangle(
                        iconRect.X + (iconRect.Width - diameter) / 2,
                        iconRect.Y + (iconRect.Height - diameter) / 2,
                        diameter,
                        diameter);
                    using var dotBrush = new SolidBrush(Color.FromArgb(255, swatch.R, swatch.G, swatch.B));
                    using var dotBorder = new Pen(Theme.BorderStrong, 1f);
                    g.FillEllipse(dotBrush, dotRect);
                    g.DrawEllipse(dotBorder, dotRect);
                    if (visual.Icon == IconKind.Check)
                    {
                        var ringRect = Rectangle.Inflate(dotRect, 2, 2);
                        using var accentPen = new Pen(Theme.Accent, 1.5f);
                        g.DrawEllipse(accentPen, ringRect);
                    }
                }
                else if (visual.Icon != IconKind.None)
                {
                    var ink = e.Item.Enabled ? Theme.Text : Theme.TextMuted;
                    Icons.Draw(g, visual.Icon, iconRect, ink, 1.45f);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Text : Theme.TextMuted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var pen = new Pen(Theme.Border);
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Theme.Surface);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Theme.BorderStrong);
            var bounds = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            e.Graphics.DrawRectangle(pen, bounds);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // No separate margin strip: the whole menu shares one background.
        }
    }

    private sealed class ThemedColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.Surface;
        public override Color MenuBorder => Theme.BorderStrong;
        public override Color MenuItemBorder => Theme.Accent;
        public override Color MenuItemSelected => Theme.Hover;
        public override Color MenuItemSelectedGradientBegin => Theme.Hover;
        public override Color MenuItemSelectedGradientEnd => Theme.Hover;
        public override Color MenuItemPressedGradientBegin => Theme.Pressed;
        public override Color MenuItemPressedGradientEnd => Theme.Pressed;
        public override Color SeparatorDark => Theme.Border;
        public override Color SeparatorLight => Theme.Border;
        public override Color ImageMarginGradientBegin => Theme.Surface;
        public override Color ImageMarginGradientMiddle => Theme.Surface;
        public override Color ImageMarginGradientEnd => Theme.Surface;
    }
}

/// <summary>
/// The context menu for an editable text field: Undo, Redo, Cut, Copy, Paste,
/// Select all and Delete, each disabled when it cannot apply.
/// </summary>
public static class TextFieldContextMenu
{
    public static void Show(TextBoxBase field, Point screenLocation)
    {
        ArgumentNullException.ThrowIfNull(field);

        var hasSelection = field.SelectionLength > 0;
        var canPaste = Clipboard.ContainsText();
        var undoRedo = field as IUndoRedoField;
        var canUndo = undoRedo is not null ? undoRedo.CanUndo : field.CanUndo;
        var canRedo = undoRedo?.CanRedo == true;

        var menu = ThemedContextMenu.Create(
            new ContextMenuItem(
                Strings.Get("Undo"),
                () =>
                {
                    if (undoRedo is not null)
                    {
                        undoRedo.Undo();
                    }
                    else
                    {
                        field.Undo();
                    }
                },
                IconKind.Undo,
                canUndo && field.Enabled && !field.ReadOnly),
            new ContextMenuItem(
                Strings.Get("Redo"),
                () => undoRedo?.Redo(),
                IconKind.Redo,
                canRedo && field.Enabled && !field.ReadOnly),
            ContextMenuItem.Separator(),
            new ContextMenuItem(Strings.Get("Cut"), () => field.Cut(), IconKind.Cut, hasSelection && !field.ReadOnly),
            new ContextMenuItem(Strings.Get("Copy"), () => field.Copy(), IconKind.Copy, hasSelection),
            new ContextMenuItem(Strings.Get("Paste"), () => field.Paste(), IconKind.Paste, canPaste && !field.ReadOnly),
            ContextMenuItem.Separator(),
            new ContextMenuItem(Strings.Get("SelectAll"), () => field.SelectAll(), IconKind.SelectAll, field.TextLength > 0),
            new ContextMenuItem(Strings.Get("Delete"), () => field.SelectedText = string.Empty, IconKind.Delete, hasSelection && !field.ReadOnly));

        menu.Show(screenLocation);
    }
}

/// <summary>
/// Undo and Redo support for a text field. WinForms' <see cref="TextBox"/> has
/// an internal redo buffer without a public API, so <see cref="UndoableTextBox"/>
/// tracks snapshots directly and exposes both operations through this contract.
/// </summary>
public interface IUndoRedoField
{
    bool CanUndo { get; }

    bool CanRedo { get; }

    void Undo();

    void Redo();
}

/// <summary>
/// TextBox with full Undo/Redo history and the app's themed right-click menu.
/// </summary>
public sealed class UndoableTextBox : TextBox, IUndoRedoField
{
    private const int MaxHistory = 100;

    private readonly Stack<(string Text, int Caret)> _undoStack = new();
    private readonly Stack<(string Text, int Caret)> _redoStack = new();
    private string _lastText = string.Empty;
    private int _lastCaret;
    private bool _applyingHistory;
    private string _cueText = string.Empty;

    public string CueText
    {
        get => _cueText;
        set
        {
            _cueText = value ?? string.Empty;
            Invalidate();
        }
    }

    public UndoableTextBox()
    {
        // Suppress the native Windows context menu by assigning an empty strip
        // and opening the themed menu cleanly on mouse-up.
        ContextMenuStrip = new ContextMenuStrip();
        MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                Focus();
                TextFieldContextMenu.Show(this, Cursor.Position);
            }
        };
    }

    public new bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public new void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }
        _redoStack.Push((Text, SelectionStart));
        ApplySnapshot(_undoStack.Pop());
    }

    public void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }
        _undoStack.Push((Text, SelectionStart));
        ApplySnapshot(_redoStack.Pop());
    }

    public void ResetHistory(string initialText = "")
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _applyingHistory = true;
        try
        {
            Text = initialText;
            _lastText = initialText;
            _lastCaret = initialText.Length;
        }
        finally
        {
            _applyingHistory = false;
        }
    }

    private void ApplySnapshot((string Text, int Caret) snapshot)
    {
        _applyingHistory = true;
        try
        {
            Text = snapshot.Text;
            SelectionStart = Math.Clamp(snapshot.Caret, 0, TextLength);
            SelectionLength = 0;
            _lastText = Text;
            _lastCaret = SelectionStart;
        }
        finally
        {
            _applyingHistory = false;
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        if (!_applyingHistory && Text != _lastText)
        {
            _undoStack.Push((_lastText, _lastCaret));
            if (_undoStack.Count > MaxHistory)
            {
                var kept = _undoStack.Take(MaxHistory).Reverse().ToArray();
                _undoStack.Clear();
                foreach (var entry in kept)
                {
                    _undoStack.Push(entry);
                }
            }
            _redoStack.Clear();
            _lastText = Text;
            _lastCaret = SelectionStart;
        }
        base.OnTextChanged(e);
        if (TextLength == 0 && !string.IsNullOrEmpty(_cueText))
        {
            Invalidate();
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        // Paint custom cue text cleanly on WM_PAINT without Windows native
        // EM_SETCUEBANNER flicker when tooltips or child buttons hover.
        if (m.Msg == 0x000F && TextLength == 0 && !string.IsNullOrEmpty(_cueText) && IsHandleCreated)
        {
            using var g = Graphics.FromHwnd(Handle);
            TextRenderer.DrawText(
                g,
                _cueText,
                Font,
                ClientRectangle,
                Theme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    protected override bool ProcessCmdKey(ref Message m, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Z))
        {
            Undo();
            return true;
        }
        if (keyData == (Keys.Control | Keys.Y))
        {
            Redo();
            return true;
        }
        return base.ProcessCmdKey(ref m, keyData);
    }
}
