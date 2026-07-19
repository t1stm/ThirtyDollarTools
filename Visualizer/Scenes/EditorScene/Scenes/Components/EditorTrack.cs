using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Components;

/// <summary>
///     One track-list row: selection highlight, name and a remove button. Pure view —
///     every mutation routes through <see cref="EditorState" />; EditorInterface
///     rebuilds the rows when the project changes. Mute/solo lives on the arrangement
///     lanes (<see cref="LaneHeader" />), not on patterns.
/// </summary>
public class EditorTrack : FlexPanel
{
    private static readonly Vector4 RowColor = new(0.086f, 0.086f, 0.118f, 1f); // #16161e
    private static readonly Vector4 SelectedColor = new(0.255f, 0.282f, 0.408f, 1f); // #414868
    private static readonly Vector4 ToggleOff = new(0.337f, 0.373f, 0.537f, 1f); // #565f89

    private readonly ColoredPlane _background;
    private readonly EditorState _state;

    public EditorTrack(UIContext context, ProjectTrack track, EditorState state) : base(context)
    {
        Track = track;
        Direction = LayoutDirection.Horizontal;
        VerticalAlign = Align.Center;
        Width = LiteralOrComputable.Percent(100);
        Height = 36;
        Padding = 6;
        Spacing = 10;
        Background = _background = new ColoredPlane { Color = RowColor };
        OnClick = _ => state.SelectTrack(track);
        _state = state;

        var remove = new Button(context, "×")
        {
            OnClick = _ => state.RemoveTrack(track),
            Label =
            {
                Color = ToggleOff
            }
        };

        Children =
        [
            new Label(context, track.Name) { FontSizePx = 14f },
            remove
        ];
    }

    public ProjectTrack Track { get; }

    /// <summary>Double-clicking a row opens the pattern, same as double-clicking a clip.</summary>
    public override bool HandleDoublePress(float x, float y)
    {
        _state.OpenTrack(Track);
        return true;
    }

    public void SetSelected(bool selected)
    {
        _background.Color = selected ? SelectedColor : RowColor;
    }
}
