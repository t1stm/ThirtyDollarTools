using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;
using EditorScene.Scenes.Views;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     Prompts for an action's value before the action is inserted. Pure view - the owner
///     parses the field and closes the modal. The tree is ActionValueDialog.snx.xml; a
///     non-null <c>current</c> edits an existing item, prefilling the field from it instead
///     of from the action's default.
/// </summary>
public sealed class ActionValueDialog
{
    public ActionValueDialog(UIContext context, FaithfulAction action, string? current = null)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Action Value Dialog/ActionValueDialog.snx.xml");
        Element = component.GetID<FlexPanel>("action-value-dialog");
        ValueInput = component.GetID<TextInput>("value-input");
        CancelButton = component.GetID<Button>("cancel-button");
        AddButton = component.GetID<Button>("add-button");

        component.GetID<Label>("title-label").SetTextContents(action.Name);
        component.GetID<Label>("hint-label").SetTextContents(action.Hint);

        component.GetID<Label>("add-label").SetTextContents(current is null ? "Add" : "Save");
        ValueInput.Value = current ?? action.Template ?? action.Name;
        ValueInput.OnCommit = _ => AddButton.OnClick?.Invoke(AddButton);
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    public TextInput ValueInput { get; }
    public Button CancelButton { get; }
    public Button AddButton { get; }
}
