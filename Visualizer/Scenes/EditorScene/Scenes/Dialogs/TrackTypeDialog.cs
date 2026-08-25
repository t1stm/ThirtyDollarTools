using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     Which kind of track "+ Add track" adds. Same shape as <see cref="ImportDialog" /> -
///     pure form, the owner decides what each button does and closes the modal itself. The
///     tree is TrackTypeDialog.snx.xml.
/// </summary>
public sealed class TrackTypeDialog
{
    public TrackTypeDialog(UIContext context)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Track Type Dialog/TrackTypeDialog.snx.xml");
        Element = component.GetID<FlexPanel>("track-type-dialog");
        PianoRollButton = component.GetID<Button>("piano-roll-button");
        FaithfulButton = component.GetID<Button>("faithful-button");
        CancelButton = component.GetID<Button>("cancel-button");
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    public Button PianoRollButton { get; }
    public Button FaithfulButton { get; }
    public Button CancelButton { get; }
}
