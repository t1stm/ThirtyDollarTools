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
public sealed class EditorTrack : FlexPanel
{
    private static readonly Vector4 RowColor = new(0.086f, 0.086f, 0.118f, 1f); // #16161e
    private static readonly Vector4 SelectedColor = new(0.255f, 0.282f, 0.408f, 1f); // #414868

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
        BorderRadius = 6;
        Background = _background = new ColoredPlane { Color = RowColor };
        UpdateCursorOnHover = true;
        OnClick = _ => state.SelectTrack(track);
        _state = state;

        var remove = new Button(context, "×")
        {
            OnClick = _ => state.RemoveTrack(track),
            Label =
            {
                Color = Vector4.One
            }
        };

        // Percent-width spacer soaks up the free space so remove lands flush against
        // the row's right edge — this framework has no space-between align.
        var spacer = new Panel(context) { Width = LiteralOrComputable.Percent(100) };

        Children =
        [
            new Label(context, track.Name) { FontSizePx = 14f },
            spacer,
            remove
        ];
    }

    public ProjectTrack Track { get; }

    /// <summary>Fired on right-click; EditorInterface shows the track's context menu.</summary>
    public Action<ProjectTrack>? OnContextMenu { get; set; }

    /// <summary>Double-clicking a row opens the pattern, same as double-clicking a clip.</summary>
    public override bool HandleDoublePress(float x, float y)
    {
        _state.OpenTrack(Track);
        return true;
    }

    /// <summary>
    ///     Right-click opens the context menu (duplicate, for now). Right-press is
    ///     level-triggered (fires every held frame) — EditorInterface guards against
    ///     reopening while one is already up.
    /// </summary>
    public override bool HandleRightPress(float x, float y)
    {
        OnContextMenu?.Invoke(Track);
        return true;
    }

    public void SetSelected(bool selected)
    {
        _background.Color = selected ? SelectedColor : RowColor;
    }
}
