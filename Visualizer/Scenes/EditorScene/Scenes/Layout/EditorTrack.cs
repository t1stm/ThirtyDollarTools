using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Views;

namespace EditorScene.Scenes.Layout;

/// <summary>
///     One track-list row: selection highlight, name and a remove button. Pure view -
///     every mutation routes through <see cref="EditorState" />; EditorInterface
///     rebuilds the rows when the project changes. Mute/solo lives on the arrangement
///     lanes (<see cref="LaneHeader" />), not on patterns.
/// </summary>
public sealed class EditorTrack : FlexPanel
{
    private readonly EditorState _state;

    public EditorTrack(UIContext context, ProjectTrack track, EditorState state) : base(context)
    {
        Track = track;
        Classes = ["track-row"];
        UpdateCursorOnHover = true;
        OnClick = _ => state.SelectTrack(track);
        _state = state;

        var remove = new Button(context, "×")
        {
            OnClick = _ => state.RemoveTrack(track),
            OnHoverEnter = _ => OnHint?.Invoke("Remove this track"),
            OnHoverExit = _ => OnHint?.Invoke(null)
        };

        // Percent-width spacer soaks up the free space so remove lands flush against
        // the row's right edge - this framework has no space-between align.
        var spacer = new Panel(context) { Classes = ["spacer"] };

        Children =
        [
            new Label(context, track.Name) { Classes = ["body-label"] },
            spacer,
            remove
        ];
    }

    public ProjectTrack Track { get; }

    /// <summary>
    ///     Fired on right-click with the cursor position; EditorInterface hangs the track's
    ///     context menu off that point.
    /// </summary>
    public Action<ProjectTrack, float, float>? OnContextMenu { get; set; }

    /// <summary>Hover hint text for the remove button; null on hover exit. See EditorInterface.SetHint.</summary>
    public Action<string?>? OnHint { get; set; }

    /// <summary>Double-clicking a row opens the pattern, same as double-clicking a clip.</summary>
    public override bool HandleDoublePress(float x, float y)
    {
        _state.OpenTrack(Track);
        return true;
    }

    /// <summary>
    ///     Right-click opens the track's context menu at the cursor. Right-press is
    ///     level-triggered (fires every held frame) - EditorInterface guards against
    ///     reopening while one is already up.
    /// </summary>
    public override bool HandleRightPress(float x, float y)
    {
        OnContextMenu?.Invoke(Track, x, y);
        return true;
    }

    public void SetSelected(bool selected)
    {
        SetClass("track-row-selected", selected);
    }
}