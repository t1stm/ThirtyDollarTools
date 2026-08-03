using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Components;

/// <summary>
///     The save/discard/cancel choice (ModalLayer content) shown when leaving the editor
///     with unsaved changes. Pure form - the owner decides what each button does and
///     closes the modal itself, mirroring <see cref="ConfirmDialog" />/<see cref="ImportDialog" />.
/// </summary>
public sealed class UnsavedChangesDialog : FlexPanel
{
    private static readonly Vector4 ButtonBlandColor = EditorPalette.Divider;
    private static readonly Vector4 ButtonMainColor = EditorPalette.Accent;

    public UnsavedChangesDialog(UIContext context) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        Width = 360;
        Padding = 14;
        Spacing = 14;
        Background = new ColoredPlane { Color = EditorPalette.Panel };

        SaveButton = new Button(context, "Save")
            { FontSizePx = 14, Background = new ColoredPlane { Color = ButtonMainColor }, BorderRadius = 6 };
        DiscardButton = new Button(context, "Discard", new ColoredPlane { Color = EditorPalette.DangerAccent })
        {
            FontSizePx = 14,
            BorderRadius = 6,
            Label = { Color = EditorPalette.Panel }
        };
        CancelButton = new Button(context, "Cancel")
            { FontSizePx = 14, Background = new ColoredPlane { Color = ButtonBlandColor }, BorderRadius = 6 };

        Children =
        [
            new Label(context, "Unsaved changes - save before leaving?") { FontSizePx = 14f },
            new FlexPanel(context)
            {
                Width = LiteralOrComputable.Percent(100),
                Height = 40,
                Spacing = 10,
                VerticalAlign = Align.Center,
                // Percent-width spacer pushes Discard/Save to the right edge while Cancel
                // stays on the left - this framework has no space-between align.
                Children =
                [
                    CancelButton, new Panel(context) { Width = LiteralOrComputable.Percent(100) },
                    DiscardButton, SaveButton
                ]
            }
        ];
    }

    public Button SaveButton { get; }
    public Button DiscardButton { get; }
    public Button CancelButton { get; }
}