using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Components;

/// <summary>
///     A generic yes/no confirmation form (ModalLayer content) for destructive actions.
///     Pure view - the owner decides what "confirm" does and closes the modal itself.
/// </summary>
public sealed class ConfirmDialog : FlexPanel
{
    public ConfirmDialog(UIContext context, string message, string confirmLabel = "Delete",
        Vector4? confirmColor = null) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        Width = 360;
        Padding = 14;
        Spacing = 14;
        Background = new ColoredPlane { Color = EditorPalette.Panel };

        ConfirmButton = new Button(context, confirmLabel,
            new ColoredPlane { Color = confirmColor ?? EditorPalette.DangerAccent })
        {
            FontSizePx = 14,
            BorderRadius = 6,
            Label = { Color = EditorPalette.Panel }
        };
        CancelButton = new Button(context, "Cancel")
            { FontSizePx = 14, Background = new ColoredPlane { Color = EditorPalette.Divider }, BorderRadius = 6 };

        Children =
        [
            new Label(context, message) { FontSizePx = 14f },
            new FlexPanel(context)
            {
                Width = LiteralOrComputable.Percent(100),
                Height = 40,
                Spacing = 10,
                HorizontalAlign = Align.End,
                VerticalAlign = Align.Center,
                Children = [CancelButton, ConfirmButton]
            }
        ];
    }

    public Button ConfirmButton { get; }
    public Button CancelButton { get; }
}