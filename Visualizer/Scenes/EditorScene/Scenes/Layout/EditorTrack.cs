using EditorScene.State;
using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Panels;
using Sundex.Style.DSL;
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
    private readonly Label _name;
    private readonly EditorState _state;

    /// <param name="color">
    ///     The track's clip fill, painted as the row's leading blip so the list reads in the
    ///     same colors as the arrangement. Null leaves the blip off (tests build rows without
    ///     an ArrangementView to resolve the palette).
    /// </param>
    public EditorTrack(UIContext context, ProjectTrack track, EditorState state, Vector4? color = null)
        : base(context)
    {
        Track = track;
        Classes = ["track-row"];
        UpdateCursorOnHover = true;
        OnClick = _ => (OnSelect ?? state.SelectTrack)(track);
        _state = state;

        var remove = new Button(context, "×")
        {
            OnClick = _ => state.RemoveTrack(track),
            OnHoverEnter = _ => OnHint?.Invoke("Remove this track"),
            OnHoverExit = _ => OnHint?.Invoke(null)
        };

        // The name takes the row's free space itself, so remove lands flush against the
        // row's right edge with no space-between align. Its percent width ignores the text's
        // measured size, so a long name can't push remove out; overflow is clipped in
        // ApplyClip below. Same shape as InstrumentRow.
        _name = new Label(context, track.Name) { Classes = ["body-label"] };

        // The fill is a palette entry, not a look, so it is set here rather than in the
        // sheet - same split as the color dialog's chip. track-row centers and spaces it.
        if (color is { } fill)
        {
            // Only the non-default kind is marked, with a letter inside the blip rather
            // than a word beside the name. The blip grows a little to hold the letter.
            var faithful = track.Kind == TrackKind.Faithful;
            DragHandle = new FlexPanel(context)
            {
                Classes = faithful ? ["track-color-blip", "track-color-blip-faithful"] : ["track-color-blip"],
                Background = new ColoredPlane { Color = fill }
            };
            if (faithful) DragHandle.AddChild(new BlipLetter(context) { Classes = ["track-blip-letter"] });
        }

        var children = new List<UIElement>();
        if (DragHandle is not null) children.Add(DragHandle);
        children.Add(_name);
        children.Add(remove);
        Children = children;
    }

    public ProjectTrack Track { get; }

    /// <summary>
    ///     The color blip, which doubles as the row's reorder handle - the press is caught
    ///     by <see cref="TrackListPanel" /> (it owns the row order, and it survives the
    ///     rebuilds a reorder triggers, which this row does not). Null when the row has no
    ///     blip.
    /// </summary>
    public Panel? DragHandle { get; }

    /// <summary>
    ///     Fired on right-click with the cursor position; EditorInterface hangs the track's
    ///     context menu off that point.
    /// </summary>
    public Action<ProjectTrack, float, float>? OnContextMenu { get; set; }

    /// <summary>
    ///     Clicking the row selects its track. Set by <see cref="TrackListPanel" /> so a
    ///     Ctrl-click (Cmd on macOS) toggles the multi-selection instead; unset, the row
    ///     selects on its own.
    /// </summary>
    public Action<ProjectTrack>? OnSelect { get; set; }

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

    /// <summary>
    ///     Re-claims the name's free-space width after styling. Applying <c>font-size</c>
    ///     makes Label remeasure and overwrite its own Width, and the sheet sets properties
    ///     in reflection order - so this can't just be a <c>width</c> on the label's class.
    /// </summary>
    public override void ApplyStyleSheet(StyleSheet styleSheet)
    {
        base.ApplyStyleSheet(styleSheet);
        _name.Width = LiteralOrComputable.Percent(100);
    }

    /// <summary>Cuts the name off at its own box so a long one never paints over the remove button.</summary>
    public override void ApplyClip(Vector4i? clip)
    {
        base.ApplyClip(clip);

        var x = (int)Computed.AbsoluteX;
        var y = (int)Computed.AbsoluteY;
        var right = (int)(_name.Computed.AbsoluteX + _name.Computed.Width);
        _name.ApplyClip(IntersectClip(new Vector4i(x, y, right, y + (int)Computed.Height), clip));
    }

    public void SetSelected(bool selected)
    {
        SetClass("track-row-selected", selected);
    }

    /// <summary>
    ///     The kind letter inside the blip, nudged up onto the dot's optical center: a Label
    ///     measures to the font's em box, whose descender space an "F" never fills, so
    ///     centering it in a fixed box leaves it sitting low.
    ///     ponytail: one pixel for one dot, not a font metric - the box and the size are
    ///     both fixed in Panels.snx.ss. Read the real descender if this ever gets reused.
    /// </summary>
    private sealed class BlipLetter(UIContext context) : Label(context, "F")
    {
        protected override void DoLayout()
        {
            TextSlice?.Position = new Vector3(Computed.AbsoluteX, Computed.AbsoluteY - 1, 0);
        }
    }
}