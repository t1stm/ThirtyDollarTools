using OpenTK.Windowing.Common;
using Sundex.Components.Abstractions;
using Sundex.Components.File_Selector;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     Mounts and removes the editor's modals on the root panel. Popups here are always a
///     ModalLayer, never a DropDownLabel.
/// </summary>
public sealed class DialogHost(UIContext context, Panel root)
{
    /// <summary>
    ///     The panel modals mount on, for components that own a hand-built ModalLayer they
    ///     add and remove themselves rather than wrapping fresh per <see cref="Show" />.
    /// </summary>
    public Panel Root => root;

    /// <summary>Adds a modal wrapping the given content; the backdrop dismiss removes it by default.</summary>
    /// <param name="content">The dialog itself.</param>
    /// <param name="onConfirm">The content's primary action, for Enter (<see cref="TryConfirmTop" />).</param>
    /// <param name="onKey">The content's other keys (<see cref="TryKeyTop" />); true for a key it used.</param>
    public ModalLayer Show(UIElement content, Action? onConfirm = null, Func<KeyboardKeyEventArgs, bool>? onKey = null)
    {
        // A field behind the modal would otherwise keep taking keys: the first Esc only
        // blurred it, and typing went into a box the modal covers.
        if (context.FocusedElement is { } focused && !Contains(content, focused)) context.Blur();

        var modal = new ModalLayer(context)
        {
            OnDismissRequested = Close,
            OnConfirmRequested = onConfirm == null ? null : _ => onConfirm(),
            OnKeyRequested = onKey
        };
        modal.AddChild(content);
        // Every dialog's × is its dismiss - Esc and the backdrop's - read when clicked, so an
        // owner that replaces OnDismissRequested after this still gets it.
        if (Find(content, "close-button") is { } close) close.OnClick = _ => modal.OnDismissRequested?.Invoke(modal);
        root.AddChild(modal);
        return modal;
    }

    public void Close(ModalLayer modal)
    {
        root.RemoveChild(modal);
    }

    private static UIElement? Find(UIElement element, string id)
    {
        if (element.ID == id) return element;
        if (element is not Panel panel) return null;
        foreach (var child in panel.Children)
            if (Find(child, id) is { } found)
                return found;
        return null;
    }

    private static bool Contains(UIElement root, UIElement element)
    {
        for (var e = element; e != null; e = e.Parent)
            if (ReferenceEquals(e, root))
                return true;
        return false;
    }

    /// <summary>Whether any modal is mounted; the editor's keybinds stay inactive while one is.</summary>
    public bool HasOpenModal => root.Children.OfType<ModalLayer>().Any();

    /// <summary>Dismisses the topmost open modal, if any, so Escape closes a dialog instead of the editor.</summary>
    public bool TryCloseTop()
    {
        var modal = root.Children.OfType<ModalLayer>().LastOrDefault();
        if (modal == null) return false;
        modal.OnDismissRequested?.Invoke(modal);
        return true;
    }

    /// <summary>
    ///     Offers a key no focused field took to the topmost modal's content (the file dialog's
    ///     list keys). False when there is no modal or its content doesn't want the key.
    /// </summary>
    public bool TryKeyTop(KeyboardKeyEventArgs e)
    {
        return root.Children.OfType<ModalLayer>().LastOrDefault()?.HandleKeyDown(e) == true;
    }

    /// <summary>
    ///     Runs the topmost modal's primary action, if it has one, so Enter confirms a dialog.
    ///     False when there is no modal or the top one has no primary action.
    /// </summary>
    public bool TryConfirmTop()
    {
        var modal = root.Children.OfType<ModalLayer>().LastOrDefault();
        if (modal?.OnConfirmRequested is not { } confirm) return false;
        confirm(modal);
        return true;
    }

    /// <summary>
    ///     Open (null name) or save-as (suggested name) dialog for one extension, headed
    ///     <paramref name="title" />. It opens in <paramref name="directory" />, or the home
    ///     directory when that is null or gone.
    /// </summary>
    public void ShowFileDialog(string title, string? saveFileName, string extension, Action<string> onPicked,
        string confirmLabel = "Open", string? directory = null)
    {
        var selection = new FileSelection(context, saveFileName, extension, confirmLabel, directory, title)
            { ID = "file-dialog", Classes = ["dialog-frame"] };
        // FileSelection leaves its two actions unstyled, so they match every other dialog's.
        selection.CancelButton.Classes = ["dialog-button"];
        selection.ConfirmButton.Classes = ["dialog-button-primary"];
        KeyChip.Add(selection.CancelButton, "Esc");
        KeyChip.Add(selection.ConfirmButton, "Enter");
        var modal = Show(selection, selection.Confirm);
        selection.OnSelect = _ =>
        {
            if (selection.SelectedPath is not { } path) return;
            Close(modal);
            onPicked(path);
        };
        selection.OnCancel = _ => Close(modal);
    }

    /// <summary>
    ///     Generic yes/no confirmation; closes itself either way. Enter confirms, and Esc or the
    ///     backdrop cancel as the Cancel button does, <paramref name="onCancel" /> included.
    /// </summary>
    public void Confirm(string message, Action onConfirm, Action? onCancel = null,
        string confirmLabel = "Delete", string confirmClass = "dialog-button-danger")
    {
        var dialog = new ConfirmDialog(context, message, confirmLabel, confirmClass);
        var modal = Show(dialog.Element);
        modal.OnDismissRequested = _ => Cancel();
        modal.OnConfirmRequested = _ => Accept();
        dialog.CancelButton.OnClick = _ => Cancel();
        dialog.ConfirmButton.OnClick = _ => Accept();
        return;

        void Cancel()
        {
            Close(modal);
            onCancel?.Invoke();
        }

        void Accept()
        {
            Close(modal);
            onConfirm();
        }
    }

    /// <summary>
    ///     One-button informational dialog, for surfacing failures that would otherwise be
    ///     silent (e.g. a failed save). Not for anything the user has to confirm.
    /// </summary>
    public void Alert(string message)
    {
        var ok = new Button(context, "OK") { Classes = ["dialog-button-primary"] };
        var content = new FlexPanel(context)
        {
            ID = "alert-dialog",
            Classes = ["dialog-frame"],
            Children =
            [
                new Label(context, message) { Classes = ["body-label"] },
                new FlexPanel(context)
                {
                    Classes = ["dialog-actions"],
                    Children = [ok]
                }
            ]
        };
        var modal = Show(content);
        ok.OnClick = _ => Close(modal);
    }
}