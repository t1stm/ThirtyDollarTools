using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The import options form (ModalLayer content) shown after dropping a TDW sequence
///     file onto the editor: single track, whole project, or cancel. Mirrors
///     <see cref="ExportDialog" />'s shape - pure form, the owner decides what each
///     button does and closes the modal itself.
/// </summary>
public sealed class ImportDialog : FlexPanel
{
    public ImportDialog(UIContext context, string fileName) : base(context)
    {
        ID = "import-dialog";
        Classes = ["dialog-frame"];

        // The two options are color-coded with the same accents the Draw/Select tool
        // toggles use elsewhere - not because import relates to either tool, but so the
        // dialog reuses the app's two existing accent colors instead of introducing a
        // third just to tell the options apart. See dialog-button-primary/-alt.
        SingleTrackButton = new Button(context, "Import as Single Track")
            { Classes = ["dialog-button-primary", "dialog-button-tall"] };
        ProjectButton = new Button(context, "Import as Project")
        {
            Classes = ["dialog-button-alt", "dialog-button-tall"],
            // Same light-background-needs-dark-text contrast rule as the Select tool toggle.
            Label = { Classes = ["dark-label"] }
        };
        CancelButton = new Button(context, "Cancel") { Classes = ["dialog-button"] };

        var divider = new Panel(context) { Classes = ["import-divider"] };

        Children =
        [
            new Label(context, $"Import \"{fileName}\"") { Classes = ["title-label"] },
            new FlexPanel(context)
            {
                Classes = ["import-options"],
                Children =
                [
                    // Label has no text-wrap support (Sundex.Components.Labels.Label), so each
                    // description is pre-split into short lines that fit the column.
                    Category(context, SingleTrackButton,
                        "One new track for this file,", "one instrument per sound."),
                    divider,
                    Category(context, ProjectButton,
                        "Replaces the current project -", "instrument + track per sound.")
                ]
            },
            new FlexPanel(context)
            {
                Classes = ["dialog-actions-compact"],
                Children = [CancelButton]
            }
        ];
    }

    /// <summary>One import option: its button, with a short explanation underneath.</summary>
    private static FlexPanel Category(UIContext context, Button button, params string[] descriptionLines)
    {
        var description = new FlexPanel(context) { Classes = ["import-column-description"] };
        foreach (var line in descriptionLines)
            description.AddChild(new Label(context, line) { Classes = ["caption-label"] });

        return new FlexPanel(context)
        {
            Classes = ["import-column"],
            Children = [button, description]
        };
    }

    public Button SingleTrackButton { get; }
    public Button ProjectButton { get; }
    public Button CancelButton { get; }
}