using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Components;

/// <summary>
///     The import options form (ModalLayer content) shown after dropping a TDW sequence
///     file onto the editor: single track, whole project, or cancel. Mirrors
///     <see cref="ExportDialog" />'s shape - pure form, the owner decides what each
///     button does and closes the modal itself.
/// </summary>
public sealed class ImportDialog : FlexPanel
{
    private static readonly Vector4 ButtonBlandColor = EditorPalette.Divider;
    private static readonly Vector4 ButtonMainColor = EditorPalette.Accent;

    public ImportDialog(UIContext context, string fileName) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        Width = 360;
        Padding = 14;
        Spacing = 14;
        Background = new ColoredPlane { Color = EditorPalette.Panel };

        SingleTrackButton = new Button(context, "Import as Single Track")
            { FontSizePx = 14, Background = new ColoredPlane { Color = ButtonMainColor }, BorderRadius = 6 };
        ProjectButton = new Button(context, "Import as Project")
            { FontSizePx = 14, Background = new ColoredPlane { Color = ButtonMainColor }, BorderRadius = 6 };
        CancelButton = new Button(context, "Cancel")
            { FontSizePx = 14, Background = new ColoredPlane { Color = ButtonBlandColor }, BorderRadius = 6 };

        Children =
        [
            new Label(context, $"Import \"{fileName}\"") { FontSizePx = 15f, Color = EditorPalette.Header },
            new FlexPanel(context)
            {
                Direction = LayoutDirection.Vertical,
                Width = LiteralOrComputable.Percent(100),
                Spacing = 10,
                Children = [SingleTrackButton, ProjectButton]
            },
            new FlexPanel(context)
            {
                Width = LiteralOrComputable.Percent(100),
                Height = 36,
                HorizontalAlign = Align.End,
                VerticalAlign = Align.Center,
                Children = [CancelButton]
            }
        ];
    }

    public Button SingleTrackButton { get; }
    public Button ProjectButton { get; }
    public Button CancelButton { get; }
}