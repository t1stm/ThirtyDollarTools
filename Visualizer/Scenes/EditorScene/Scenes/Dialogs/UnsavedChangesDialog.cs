using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The save/discard/cancel choice (ModalLayer content) shown before unsaved changes would
///     be lost - leaving the editor, opening another project - over a card naming the project. Pure form - the owner decides
///     what each button does and closes the modal itself.
///     The tree is UnsavedChangesDialog.snx.xml; this resolves its handles and fills the card.
/// </summary>
public sealed class UnsavedChangesDialog
{
    /// <param name="projectPath">Where the project is saved; null when it never has been.</param>
    /// <param name="title">The question, naming what is about to happen.</param>
    /// <param name="detail">The card's second line in place of where the project is saved (an instrument's sound count).</param>
    public UnsavedChangesDialog(UIContext context, string projectName = "", string? projectPath = null,
        string title = "Save before leaving?", string? detail = null)
    {
        var component = Markup.Build(context, "Scenes/Dialogs/Unsaved Changes Dialog/UnsavedChangesDialog.snx.xml");
        Element = component.GetID<FlexPanel>("unsaved-changes-dialog");
        SaveButton = component.GetID<Button>("save-button");
        DiscardButton = component.GetID<Button>("discard-button");
        CancelButton = component.GetID<Button>("cancel-button");
        CloseButton = component.GetID<Button>("close-button");

        component.GetID<Label>("unsaved-changes-title").SetTextContents(title);
        component.GetID<Label>("project-name").SetTextContents(projectName);
        component.GetID<Label>("project-path").SetTextContents(detail ?? Where(projectPath));

        KeyChip.Add(CancelButton, "Esc");
        KeyChip.Add(SaveButton, "Enter");
    }

    /// <summary>The dialog's root - what the owner mounts into a ModalLayer.</summary>
    public FlexPanel Element { get; }

    public Button SaveButton { get; }
    public Button DiscardButton { get; }
    public Button CancelButton { get; }
    public Button CloseButton { get; }

    /// <summary>The card's second line: the path with the home directory as "~", or why there is none.</summary>
    internal static string Where(string? path)
    {
        if (path == null) return "Not saved yet. Save asks where to put it.";

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && path.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? "~" + path[home.Length..]
            : path;
    }
}
