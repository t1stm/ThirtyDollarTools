using System.Text.Json;
using OpenTK.Windowing.Common;
using Sundex.Components;
using Sundex.Components.Abstractions;
using Sundex.Components.File_Selector;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Components.Scroll;
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

    /// <summary>Puts text on the system clipboard; set by the owner, which has the window. Null copies nothing.</summary>
    public Action<string>? CopyText { get; set; }

    /// <summary>
    ///     <see cref="Alert(string, string, string?)" /> for a caught exception: says what happened in
    ///     plain words, and keeps the exception's own text as the details.
    /// </summary>
    /// <param name="writing">Whether the failure was writing a file rather than reading one, for the wording.</param>
    public void Alert(string title, Exception error, bool writing = false)
    {
        var (message, details) = Explain(error, writing);
        Alert(title, message, details);
    }

    /// <summary>
    ///     What an exception means to someone using the editor, and the raw text worth keeping for
    ///     a bug report (null when the message already is that text). The editor's own failures
    ///     (InvalidOperation, InvalidData) are written to be read as they are.
    /// </summary>
    internal static (string Message, string? Details) Explain(Exception error, bool writing) => error switch
    {
        InvalidOperationException or InvalidDataException => (error.Message, null),
        JsonException => ("This file isn't a project the editor can read. It may be damaged, or not a project at all.",
            error.Message),
        DirectoryNotFoundException => (writing ? "That folder doesn't exist. Pick another one." : "That folder doesn't exist any more.",
            error.Message),
        FileNotFoundException => ("That file doesn't exist any more.", error.Message),
        UnauthorizedAccessException => (writing
            ? "The editor isn't allowed to write there. Pick another folder."
            : "The editor isn't allowed to read that file.", error.Message),
        IOException => (writing ? "The file couldn't be written." : "The file couldn't be read.", error.Message),
        _ => ("The editor ran into an error it doesn't know how to explain.", error.Message)
    };

    /// <summary>
    ///     One-button informational dialog, for surfacing failures that would otherwise be
    ///     silent (e.g. a failed save). Not for anything the user has to confirm. Enter, Esc, the
    ///     × and OK all close it.
    /// </summary>
    /// <param name="title">What failed, and on what: "Couldn't open broken.tdwproj".</param>
    /// <param name="message">What happened, in plain words, and what to do about it if anything.</param>
    /// <param name="details">The raw error, shown dim under the message with a Copy error button; null for none.</param>
    public void Alert(string title, string message, string? details = null)
    {
        var ok = KeyChip.Add(new Button(context, "OK") { Classes = ["dialog-button-primary"] }, "Enter");
        var close = new Button(context, Icons.X, font: Icons.Font)
        {
            ID = "close-button",
            Classes = ["icon-button"],
            Label = { Classes = ["icon-button-icon"] }
        };
        var content = new FlexPanel(context)
        {
            ID = "alert-dialog",
            Classes = ["dialog-frame"],
            Children =
            [
                new FlexPanel(context)
                {
                    Classes = ["dialog-header"],
                    Children =
                    [
                        new Label(context, Icons.TriangleAlert, Icons.Font) { Classes = ["alert-icon"] },
                        // Percent-wide and clipped, so a long file name stops short of the ×.
                        new FlexPanel(context)
                        {
                            Classes = ["alert-title"],
                            Children = [new ClippedLabel(context, title) { Classes = ["dialog-title"] }]
                        },
                        close
                    ]
                },
                new Label(context, message) { Classes = ["body-label", "alert-message"] }
            ]
        };

        var actions = new FlexPanel(context) { Classes = ["dialog-actions-split"] };
        if (details != null)
        {
            content.AddChild(Details(details));
            var copy = new Button(context, "Copy error")
            {
                Classes = ["dialog-button-quiet"],
                Label = { Classes = ["quiet-label"] }
            };
            copy.OnClick = _ =>
            {
                CopyText?.Invoke($"{title}\n{message}\n\n{details}");
                copy.Label.SetTextContents("Copied");
            };
            actions.AddChild(copy);
        }

        actions.AddChild(new Panel(context) { Classes = ["spacer"] });
        actions.AddChild(ok);
        content.AddChild(actions);

        var modal = Show(content);
        modal.OnConfirmRequested = Close;
        ok.OnClick = _ => Close(modal);
    }

    /// <summary>The raw error, dim and wrapped in an inset box; past four lines it scrolls.</summary>
    private UIElement Details(string details)
    {
        var text = new Label(context, details) { Classes = ["alert-details-text"] };
        var box = new FlexPanel(context) { Classes = ["alert-details"] };
        // Styled by the box's sheet only once it is mounted, so the wrap is measured here.
        text.WrapWidth = AlertDetailsWrapWidth;
        text.FontSizePx = 12;
        if (text.Height.Value <= AlertDetailsMaxHeight)
        {
            box.AddChild(text);
            return box;
        }

        var scroll = new ScrollView(context) { Classes = ["alert-details-scroll"] };
        scroll.AddChild(text);
        box.AddChild(scroll);
        return box;
    }

    // The alert is 420 wide: 16 frame padding and 10 box padding a side leave 368 for the text.
    private const float AlertDetailsWrapWidth = 368;

    // Four 12 px lines.
    private const float AlertDetailsMaxHeight = 4 * 12 * 1.2f + 1;
}