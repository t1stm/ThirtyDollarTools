using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.File_Selector;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Components;

/// <summary>
///     Wraps the root panel's modal add/remove pattern (ModalLayer only, never
///     DropDownLabel — the standing rule for popups in this editor).
/// </summary>
public sealed class DialogHost(UIContext context, Panel root)
{
    /// <summary>The panel modals mount on — for components that manage their own
    /// hand-built ModalLayer (toggled open/closed, not wrapped fresh per <see cref="Show" />).</summary>
    public Panel Root => root;

    /// <summary>Adds a modal wrapping the given content; the backdrop dismiss removes it by default.</summary>
    public ModalLayer Show(UIElement content)
    {
        var modal = new ModalLayer(context)
        {
            OnDismissRequested = Close
        };
        modal.AddChild(content);
        root.AddChild(modal);
        return modal;
    }

    public void Close(ModalLayer modal)
    {
        root.RemoveChild(modal);
    }

    /// <summary>Dismisses the topmost open modal, if any. Used so Escape closes a dialog instead of the editor.</summary>
    public bool TryCloseTop()
    {
        var modal = root.Children.OfType<ModalLayer>().LastOrDefault();
        if (modal == null) return false;
        modal.OnDismissRequested?.Invoke(modal);
        return true;
    }

    /// <summary>Open (null name) or save-as (suggested name) dialog for one extension.</summary>
    public void ShowFileDialog(string? saveFileName, string extension, Action<string> onPicked)
    {
        var selection = new FileSelection(context, saveFileName, extension)
        {
            Width = 560,
            Height = 440
        };
        var modal = Show(selection);
        selection.OnSelect = _ =>
        {
            if (selection.SelectedPath is not { } path) return;
            Close(modal);
            onPicked(path);
        };
        selection.OnCancel = _ => Close(modal);
    }

    /// <summary>Generic yes/no confirmation; closes itself either way.</summary>
    public void Confirm(string message, Action onConfirm, Action? onCancel = null)
    {
        var dialog = new ConfirmDialog(context, message);
        var modal = Show(dialog);
        dialog.CancelButton.OnClick = _ =>
        {
            Close(modal);
            onCancel?.Invoke();
        };
        dialog.ConfirmButton.OnClick = _ =>
        {
            Close(modal);
            onConfirm();
        };
    }

    /// <summary>One-button informational dialog — for surfacing failures that would
    /// otherwise be silent (e.g. a failed save), not for anything the user confirms.</summary>
    public void Alert(string message)
    {
        var ok = new Button(context, "OK") { FontSizePx = 14 };
        var content = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = 360,
            Padding = 14,
            Spacing = 14,
            Background = new ColoredPlane { Color = EditorPalette.Panel },
            Children =
            [
                new Label(context, message) { FontSizePx = 14f },
                new FlexPanel(context)
                {
                    Width = LiteralOrComputable.Percent(100),
                    Height = 40,
                    HorizontalAlign = Align.End,
                    VerticalAlign = Align.Center,
                    Children = [ok]
                }
            ]
        };
        var modal = Show(content);
        ok.OnClick = _ => Close(modal);
    }
}
