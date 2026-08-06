using Sundex.Components.Abstractions;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     Track row context menu (ModalLayer content), opened by right-clicking a track in
///     the track list. Just "duplicate" for now: a name field (prefilled "&lt;name&gt; copy")
///     plus confirm/cancel. Pure view - the owner decides what "duplicate" does.
///     The tree is TrackContextMenu.snx.xml; this only resolves its handles.
/// </summary>
public sealed class TrackContextMenu
{
    public TrackContextMenu(UIContext context, string suggestedName)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Track Context Menu/TrackContextMenu.snx.xml");
        Element = component.GetID<FlexPanel>("track-context-menu");
        NameInput = component.GetID<TextInput>("duplicate-name-input");
        CancelButton = component.GetID<Button>("cancel-button");
        DuplicateButton = component.GetID<Button>("duplicate-button");

        NameInput.Value = suggestedName;
        NameInput.OnCommit = _ => DuplicateButton.OnClick?.Invoke(DuplicateButton);
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    public TextInput NameInput { get; }
    public Button CancelButton { get; }
    public Button DuplicateButton { get; }
}
