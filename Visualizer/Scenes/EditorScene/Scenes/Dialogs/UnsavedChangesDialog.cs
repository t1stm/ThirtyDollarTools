using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     The save/discard/cancel choice (ModalLayer content) shown when leaving the editor
///     with unsaved changes. Pure form - the owner decides what each button does and
///     closes the modal itself, mirroring <see cref="ConfirmDialog" />/<see cref="ImportDialog" />.
/// </summary>
public sealed class UnsavedChangesDialog : FlexPanel
{
    public UnsavedChangesDialog(UIContext context) : base(context)
    {
        ID = "unsaved-changes-dialog";
        Classes = ["dialog-frame"];

        SaveButton = new Button(context, "Save") { Classes = ["dialog-button-primary"] };
        DiscardButton = new Button(context, "Discard")
        {
            Classes = ["dialog-button-danger"],
            Label = { Classes = ["dark-label"] }
        };
        CancelButton = new Button(context, "Cancel") { Classes = ["dialog-button"] };

        Children =
        [
            new Label(context, "Unsaved changes - save before leaving?") { Classes = ["body-label"] },
            new FlexPanel(context)
            {
                Classes = ["dialog-actions-split"],
                // Percent-width spacer pushes Discard/Save to the right edge while Cancel
                // stays on the left - this framework has no space-between align.
                Children =
                [
                    CancelButton, new Panel(context) { Classes = ["spacer"] },
                    DiscardButton, SaveButton
                ]
            }
        ];
    }

    public Button SaveButton { get; }
    public Button DiscardButton { get; }
    public Button CancelButton { get; }
}