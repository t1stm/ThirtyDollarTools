using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     A generic yes/no confirmation form (ModalLayer content) for destructive actions.
///     Pure view - the owner decides what "confirm" does and closes the modal itself.
/// </summary>
public sealed class ConfirmDialog : FlexPanel
{
    public ConfirmDialog(UIContext context, string message, string confirmLabel = "Delete",
        Vector4? confirmColor = null) : base(context)
    {
        ID = "confirm-dialog";
        Classes = ["dialog-frame"];

        // The confirm fill is a caller argument (delete red by default, but the import
        // flow passes the accent), so it stays a code-built plane; the rest of the
        // button - size, corner, dark label - comes from the sheet.
        ConfirmButton = new Button(context, confirmLabel,
            new ColoredPlane { Color = confirmColor ?? EditorPalette.DangerAccent })
        {
            Classes = ["dialog-button-shape"],
            Label = { Classes = ["dark-label"] }
        };
        CancelButton = new Button(context, "Cancel") { Classes = ["dialog-button"] };

        Children =
        [
            new Label(context, message) { Classes = ["body-label"] },
            new FlexPanel(context)
            {
                Classes = ["dialog-actions"],
                Children = [CancelButton, ConfirmButton]
            }
        ];
    }

    public Button ConfirmButton { get; }
    public Button CancelButton { get; }
}