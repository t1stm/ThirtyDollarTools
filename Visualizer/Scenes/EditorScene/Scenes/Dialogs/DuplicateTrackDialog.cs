using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The duplicate-track prompt (ModalLayer content), reached from a track row's
///     right-click menu: a name field, prefilled with the copy's suggested name, plus
///     confirm/cancel. Pure view - the owner decides what "duplicate" does and closes the
///     modal itself. The tree is DuplicateTrackDialog.snx.xml; this only resolves its handles.
/// </summary>
public sealed class DuplicateTrackDialog
{
    private readonly string _suggestedName;

    public DuplicateTrackDialog(UIContext context, string sourceName, string suggestedName)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Duplicate Track Dialog/DuplicateTrackDialog.snx.xml");
        Element = component.GetID<FlexPanel>("duplicate-track-dialog");
        NameInput = component.GetID<TextInput>("duplicate-name-input");
        CancelButton = component.GetID<Button>("cancel-button");
        DuplicateButton = component.GetID<Button>("duplicate-button");
        component.GetID<Label>("source-label").SetTextContents(sourceName);

        _suggestedName = suggestedName;
        NameInput.Value = suggestedName;
        NameInput.OnCommit = _ => DuplicateButton.OnClick?.Invoke(DuplicateButton);

        KeyChip.Add(CancelButton, "Esc");
        KeyChip.Add(DuplicateButton, "Enter");
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    public TextInput NameInput { get; }
    public Button CancelButton { get; }
    public Button DuplicateButton { get; }

    /// <summary>The typed name, or the suggestion when the field was cleared - a copy is never nameless.</summary>
    public string Name => NameInput.Value.Trim() is { Length: > 0 } name ? name : _suggestedName;

    /// <summary>Puts the caret in the name, all of it selected so typing replaces it. Call once the modal is mounted.</summary>
    public void Focus()
    {
        NameInput.Context.Focus(NameInput);
        NameInput.SelectAll();
    }
}
