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
    private const float ColumnWidth = 240f;
    private const float ColumnHeight = 84f; // button + spacing + 2-line description, tall enough for the divider to span

    private static readonly Vector4 ButtonBlandColor = EditorPalette.Divider;

    // The two options are color-coded with the same accents the Draw/Select tool
    // toggles use elsewhere (see EditorInterface.ToolAccent) - not because import
    // relates to either tool, but so the dialog reuses the app's two existing
    // accent colors instead of introducing a third just to tell the options apart.
    private static readonly Vector4 TrackColor = EditorPalette.Accent;
    private static readonly Vector4 ProjectColor = EditorPalette.AccentYellow;

    public ImportDialog(UIContext context, string fileName) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        Width = 560;
        Padding = 16;
        Spacing = 18;
        Background = new ColoredPlane { Color = EditorPalette.Panel };

        SingleTrackButton = new Button(context, "Import as Single Track")
            { FontSizePx = 14, Height = 40, Background = new ColoredPlane { Color = TrackColor }, BorderRadius = 6 };
        ProjectButton = new Button(context, "Import as Project")
        {
            FontSizePx = 14, Height = 40, Background = new ColoredPlane { Color = ProjectColor }, BorderRadius = 6,
            // Same light-background-needs-dark-text contrast rule as the Select tool toggle.
            Label = { Color = EditorPalette.Panel }
        };
        CancelButton = new Button(context, "Cancel")
            { FontSizePx = 14, Background = new ColoredPlane { Color = ButtonBlandColor }, BorderRadius = 6 };

        var divider = new Panel(context)
        {
            Width = 1,
            Height = ColumnHeight,
            Background = new ColoredPlane { Color = EditorPalette.Divider }
        };

        Children =
        [
            new Label(context, $"Import \"{fileName}\"") { FontSizePx = 15f, Color = EditorPalette.Header },
            new FlexPanel(context)
            {
                Width = LiteralOrComputable.Percent(100),
                Spacing = 20,
                HorizontalAlign = Align.Center,
                Children =
                [
                    // Label has no text-wrap support (Sundex.Components.Labels.Label), so each
                    // description is pre-split into short lines that fit ColumnWidth.
                    Category(context, SingleTrackButton,
                        "One new track for this file,", "one instrument per sound."),
                    divider,
                    Category(context, ProjectButton,
                        "Replaces the current project -", "instrument + track per sound.")
                ]
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

    /// <summary>One import option: its button, with a short explanation underneath.</summary>
    private static FlexPanel Category(UIContext context, Button button, params string[] descriptionLines)
    {
        var description = new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = LiteralOrComputable.Percent(100),
            Spacing = 2
        };
        foreach (var line in descriptionLines)
            description.AddChild(new Label(context, line) { FontSizePx = 12f, Color = EditorPalette.TextMuted });

        return new FlexPanel(context)
        {
            Direction = LayoutDirection.Vertical,
            Width = ColumnWidth,
            Spacing = 6,
            Children = [button, description]
        };
    }

    public Button SingleTrackButton { get; }
    public Button ProjectButton { get; }
    public Button CancelButton { get; }
}