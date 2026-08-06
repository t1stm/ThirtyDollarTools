using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The import options form (ModalLayer content) shown after dropping a TDW sequence
///     file onto the editor: single track, whole project, or cancel. Mirrors
///     <see cref="ExportDialog" />'s shape - pure form, the owner decides what each
///     button does and closes the modal itself. The tree is ImportDialog.snx.xml.
/// </summary>
public sealed class ImportDialog
{
    public ImportDialog(UIContext context, string fileName)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Import Dialog/ImportDialog.snx.xml");
        Element = component.GetID<FlexPanel>("import-dialog");
        SingleTrackButton = component.GetID<Button>("single-track-button");
        ProjectButton = component.GetID<Button>("project-button");
        CancelButton = component.GetID<Button>("cancel-button");

        component.GetID<Label>("title-label").SetTextContents($"Import \"{fileName}\"");
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    public Button SingleTrackButton { get; }
    public Button ProjectButton { get; }
    public Button CancelButton { get; }
}
