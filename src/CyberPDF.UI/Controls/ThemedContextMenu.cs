using System.Drawing.Drawing2D;
using CyberPDF.Common.I18n;
using CyberPDF.UI.Theming;

namespace CyberPDF.UI.Controls;

/// <summary>One row in a themed context menu.</summary>
public sealed class ContextMenuItem
{
    public ContextMenuItem(string text, Action? action, IconKind icon = IconKind.None, bool enabled = true)
    {
        Text = text;
        Action = action;
        Icon = icon;
        Enabled = enabled;
    }

    public string Text { get; }

    public Action? Action { get; }

    public IconKind Icon { get; }

    public bool Enabled { get; }

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
    private const int ItemHeight = 28;
    private const int IconColumn = 26;

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
        foreach (var item in items)
        {
            if (item.Action is null && string.IsNullOrEmpty(item.Text))
            {
                menu.Items.Add(new ToolStripSeparator());
                continue;
            }

            var entry = new ToolStripMenuItem(item.Text)
            {
                Enabled = item.Enabled,
                ForeColor = item.Enabled ? Theme.Text : Theme.TextMuted,
            };
            if (item.Action is { } action)
            {
                entry.Click += (_, _) => action();
            }
            menu.Items.Add(entry);
        }
        return menu;
    }

    /// <summary>
    /// Renders every row with the theme palette instead of the system colors,
    /// so the menu matches the app in all three themes.
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
/// Select all and Delete, each disabled when it cannot apply. Native WinForms
/// text controls already expose these through the clipboard and their own
/// commands, so this only routes them.
/// </summary>
public static class TextFieldContextMenu
{
    /// <summary>
    /// Shows the standard field menu for <paramref name="field"/>.
    /// </summary>
    /// <remarks>
    /// TextBoxBase exposes Undo but not Redo, so the redo entry is offered only
    /// when the concrete control implements <see cref="IUndoRedoField"/>. The
    /// entry still appears when unavailable, disabled, because the suite expects
    /// every row to be visible and explained by its state.
    /// </remarks>
    public static void Show(TextBoxBase field, Point screenLocation)
    {
        ArgumentNullException.ThrowIfNull(field);

        var hasSelection = field.SelectionLength > 0;
        var canPaste = Clipboard.ContainsText();
        var canUndo = field.CanUndo;
        var redo = field as IUndoRedoField;

        var menu = ThemedContextMenu.Create(
            new ContextMenuItem(Common.I18n.Strings.Get("Undo"), () => field.Undo(), IconKind.None, canUndo && field.Enabled),
            new ContextMenuItem(Common.I18n.Strings.Get("Redo"), () => redo?.Redo(), IconKind.None, redo?.CanRedo == true && field.Enabled),
            ContextMenuItem.Separator(),
            new ContextMenuItem(Common.I18n.Strings.Get("Cut"), () => field.Cut(), IconKind.None, hasSelection && !field.ReadOnly),
            new ContextMenuItem(Common.I18n.Strings.Get("Copy"), () => field.Copy(), IconKind.None, hasSelection),
            new ContextMenuItem(Common.I18n.Strings.Get("Paste"), () => field.Paste(), IconKind.None, canPaste && !field.ReadOnly),
            ContextMenuItem.Separator(),
            new ContextMenuItem(Common.I18n.Strings.Get("SelectAll"), () => field.SelectAll(), IconKind.None, field.TextLength > 0),
            new ContextMenuItem(Common.I18n.Strings.Get("Delete"), () =>
            {
                if (field.SelectionLength > 0)
                {
                    field.SelectedText = string.Empty;
                }
                else
                {
                    field.SelectedText = string.Empty;
                }
            }, IconKind.None, !field.ReadOnly && field.TextLength > 0));

        menu.Show(screenLocation);
    }
}

/// <summary>
/// Redo support for a text field. WinForms' <see cref="TextBox"/> has an internal
/// redo buffer but no public API for it, so the app owns the history through this
/// contract and the menu never advertises an action that cannot work.
/// </summary>
public interface IUndoRedoField
{
    bool CanRedo { get; }

    void Redo();
}
