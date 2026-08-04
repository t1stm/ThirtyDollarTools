using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Inputs;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     Track row context menu (ModalLayer content), opened by right-clicking a track in
///     the track list. Just "duplicate" for now: a name field (prefilled "&lt;name&gt; copy")
///     plus confirm/cancel. Pure view - the owner decides what "duplicate" does.
/// </summary>
public sealed class TrackContextMenu : FlexPanel
{
    public TrackContextMenu(UIContext context, string suggestedName) : base(context)
    {
        Direction = LayoutDirection.Vertical;
        Width = 280;
        Padding = 14;
        Spacing = 12;
        Background = new ColoredPlane { Color = EditorPalette.Panel };

        NameInput = new TextInput(context, suggestedName)
        {
            FontSizePx = 14f,
            Width = LiteralOrComputable.Percent(100),
            BorderRadius = 4,
            Background = new ColoredPlane { Color = EditorPalette.InputBackground }
        };
        CancelButton = new Button(context, "Cancel") { FontSizePx = 14 };
        DuplicateButton = new Button(context, "Duplicate",
            new ColoredPlane { Color = EditorPalette.Header })
        {
            FontSizePx = 14,
            BorderRadius = 6,
            Label = { Color = EditorPalette.Panel }
        };
        NameInput.OnCommit = _ => DuplicateButton.OnClick?.Invoke(DuplicateButton);

        Children =
        [
            new Label(context, "Duplicate track") { FontSizePx = 15f },
            NameInput,
            new FlexPanel(context)
            {
                Width = LiteralOrComputable.Percent(100),
                Height = 36,
                Spacing = 10,
                HorizontalAlign = Align.End,
                VerticalAlign = Align.Center,
                Children = [CancelButton, DuplicateButton]
            }
        ];
    }

    public TextInput NameInput { get; }
    public Button CancelButton { get; }
    public Button DuplicateButton { get; }
}