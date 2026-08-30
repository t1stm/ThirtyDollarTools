using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The save/discard/cancel choice (ModalLayer content) shown when leaving the editor
///     with unsaved changes. Pure form - the owner decides what each button does and
///     closes the modal itself.
///     The tree is UnsavedChangesDialog.snx.xml; this only resolves its handles.
/// </summary>
public sealed class UnsavedChangesDialog
{
    public UnsavedChangesDialog(UIContext context)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Unsaved Changes Dialog/UnsavedChangesDialog.snx.xml");
        Element = component.GetID<FlexPanel>("unsaved-changes-dialog");
        SaveButton = component.GetID<Button>("save-button");
        DiscardButton = component.GetID<Button>("discard-button");
        CancelButton = component.GetID<Button>("cancel-button");
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    public Button SaveButton { get; }
    public Button DiscardButton { get; }
    public Button CancelButton { get; }
}
