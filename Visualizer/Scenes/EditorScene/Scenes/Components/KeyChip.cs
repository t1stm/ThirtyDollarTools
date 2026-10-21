using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Components;

/// <summary>
///     Puts a key's name in front of a dialog button's label, the way the instrument builder's
///     Preview button shows the play key: the chip, the label, then a tail panel that makes the
///     right side 16 (padding is one value on every side). The button keeps its fill class;
///     key-button, added after it, changes only the layout. A button whose key does nothing
///     shouldn't get a chip.
/// </summary>
public static class KeyChip
{
    /// <param name="key">The key's name ("Esc", "Enter"), or a glyph when <paramref name="font" /> is set.</param>
    /// <param name="font">The chip label's font, e.g. Icons.Font for a "+" glyph instead of a key.</param>
    public static T Add<T>(T button, string key, string? font = null) where T : Button
    {
        var context = button.Context;
        var label = new Label(context, key, font) { Classes = ["key-chip-label", .. ShadeFor(button)] };
        button.Children =
        [
            new FlexPanel(context) { Classes = ["key-chip", .. VeilFor(button)], Children = [label] },
            button.Label,
            new Panel(context) { Classes = ["button-tail"] }
        ];
        button.SetClass("key-button", true);
        return button;
    }

    /// <summary>Renames the key on a button <see cref="Add{T}" /> gave a chip - after a rebind.</summary>
    public static void SetKey(Button button, string key)
    {
        ChipLabel(button)?.SetTextContents(key);
    }

    /// <summary>
    ///     Turns the chip amber: the key it names has work waiting on it (Save, with unsaved
    ///     changes). Both classes go last, so they win over the fill's own shading.
    /// </summary>
    public static void SetLit(Button button, bool lit)
    {
        if (ChipLabel(button) is not { Parent: FlexPanel chip } label) return;
        chip.SetClass("key-chip-lit", lit);
        label.SetClass("key-chip-label-lit", lit);
    }

    private static Label? ChipLabel(Button button)
    {
        return button.Children is [FlexPanel { Children: [Label label] }, ..] ? label : null;
    }

    /// <summary>The chip label's color modifier for the button's fill: dark on a light fill.</summary>
    private static string[] ShadeFor(Button button)
    {
        if (button.Classes.Contains("dialog-button-primary")) return ["key-chip-label-on-accent"];
        if (button.Classes.Contains("dialog-button") || button.Classes.Contains("header-button") ||
            button.Classes.Contains("menu-row") || button is DropdownItem)
            return ["key-chip-label-on-quiet"];
        return [];
    }

    /// <summary>
    ///     A menu row's chip takes the light veil: the dark one vanishes on the menu's fill, and
    ///     the light one shows on its idle and hovered rows alike.
    /// </summary>
    private static string[] VeilFor(Button button)
    {
        return button is DropdownItem ? ["key-chip-on-card"] : [];
    }
}
