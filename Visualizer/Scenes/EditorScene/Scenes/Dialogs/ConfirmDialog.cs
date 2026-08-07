using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     A generic yes/no confirmation form (ModalLayer content) for destructive actions.
///     Pure view - the owner decides what "confirm" does and closes the modal itself.
///     The tree is ConfirmDialog.snx.xml; the three caller-supplied bits are applied here.
/// </summary>
public sealed class ConfirmDialog
{
    /// <param name="confirmClass">
    ///     The confirm button's fill, as one of Controls.snx.ss's dialog-button classes -
    ///     destructive red by default, but the import flows confirm with the accent.
    /// </param>
    public ConfirmDialog(UIContext context, string message, string confirmLabel = "Delete",
        string confirmClass = "dialog-button-danger")
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Confirm Dialog/ConfirmDialog.snx.xml");
        Element = component.GetID<FlexPanel>("confirm-dialog");
        ConfirmButton = component.GetID<Button>("confirm-button");
        CancelButton = component.GetID<Button>("cancel-button");

        component.GetID<Label>("message-label").SetTextContents(message);
        ConfirmButton.Label.SetTextContents(confirmLabel);
        ConfirmButton.SetClass(confirmClass, true);
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    public Button ConfirmButton { get; }
    public Button CancelButton { get; }
}
