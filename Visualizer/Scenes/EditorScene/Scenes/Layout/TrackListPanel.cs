using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Labels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Layout;

/// <summary>
///     The scrollable track list docked in the track column, with its "+ Add track"
///     trailer row. Lifted out of <see cref="EditorInterface" />.
/// </summary>
public sealed class TrackListPanel : ScrollView
{
    private readonly Button _addTrackRow;

    /// <summary>
    ///     Tracks + names as of the last actual rebuild, so a rebuild triggered by
    ///     an unrelated project change (e.g. a note dragged 60x/s) can be skipped when
    ///     nothing about the row list itself would change - rows read their track by
    ///     reference, so in-place data needs no rebuild.
    /// </summary>
    private readonly List<(ProjectTrack Track, string Name, int? ColorIndex)> _built = [];

    private readonly UIContext _context;
    private readonly EditorState _state;

    /// <summary>True while a blip drag is reordering the selected tracks.</summary>
    private bool _dragging;

    public TrackListPanel(UIContext context, EditorState state) : base(context)
    {
        _context = context;
        _state = state;
        ID = "track-list";

        // The fill and its hover state come from menu-row now that code-built elements
        // are styled: no ColoredPlane to swap by hand.
        _addTrackRow = new Button(context, "+ Add track")
        {
            Classes = ["menu-row"],
            OnClick = _ => state.AddTrack()
        };
        AddChild(_addTrackRow);
    }

    public Action<ProjectTrack, float, float>? OnContextMenu { get; set; }

    /// <summary>
    ///     The primary modifier held - Ctrl, or Cmd on macOS (set from
    ///     EditorInterface.SetModifiers, which reads Keybinds.PrimaryDown). Row clicks then
    ///     toggle the selection instead of replacing it.
    /// </summary>
    public bool CtrlHeld { get; set; }

    /// <summary>
    ///     Resolves a track's clip fill for its row blip - the arrangement's
    ///     <c>ColorOf</c>, so the list and the clips can never disagree. Null leaves the
    ///     blips off.
    /// </summary>
    public Func<ProjectTrack, Vector4>? TrackColor { get; set; }

    /// <summary>Relayed from each row's hover hint; see <see cref="EditorTrack.OnHint" />.</summary>
    public Action<string?>? OnHint { get; set; }

    /// <summary>
    ///     Full row rebuild: the track list is small by design (the grid is what
    ///     scales). The add-track row is pulled out and re-appended last so it always trails.
    ///     Skipped entirely when the track set and names match the last rebuild - called on
    ///     every project change, including per-frame ones (a note drag fires Touch() ~60x/s)
    ///     that never touch the track list itself.
    /// </summary>
    public void Rebuild()
    {
        var tracks = _state.Project.Tracks;
        if (Unchanged(tracks)) return;

        RemoveChild(_addTrackRow);
        foreach (var row in Children.OfType<EditorTrack>().ToArray())
            RemoveChild(row);
        foreach (var track in tracks)
            AddChild(new EditorTrack(_context, track, _state, TrackColor?.Invoke(track))
            {
                OnContextMenu = (t, x, y) => OnContextMenu?.Invoke(t, x, y),
                OnHint = h => OnHint?.Invoke(h),
                OnSelect = Select
            });
        AddChild(_addTrackRow);

        _built.Clear();
        _built.AddRange(tracks.Select(t => (t, t.Name, t.ColorIndex)));
    }

    private bool Unchanged(IReadOnlyList<ProjectTrack> tracks)
    {
        if (tracks.Count != _built.Count) return false;
        for (var i = 0; i < tracks.Count; i++)
            if (tracks[i] != _built[i].Track || tracks[i].Name != _built[i].Name ||
                tracks[i].ColorIndex != _built[i].ColorIndex)
                return false;
        return true;
    }

    /// <summary>Row click: Ctrl/Cmd toggles the track in the selection, a plain click replaces it.</summary>
    private void Select(ProjectTrack track)
    {
        if (CtrlHeld) _state.ToggleTrackSelection(track);
        else _state.SelectTrack(track);
    }

    /// <summary>
    ///     A press on a row's color blip starts a reorder drag of the whole track selection.
    ///     Handled here rather than on the row so the capture survives the rebuilds each
    ///     reorder triggers - the row that was pressed is detached by them, which would drop
    ///     the capture mid-gesture.
    /// </summary>
    public override bool HandlePress(float x, float y)
    {
        var track = Children.OfType<EditorTrack>()
            .FirstOrDefault(row => row.DragHandle?.ContainsPoint(x, y) == true)?.Track;
        _dragging = track is not null;
        if (track is null) return false;

        // Grabbing a track that is already part of a multi-selection drags the lot; grabbing
        // any other row selects it first (the row's own OnClick never fires while we hold
        // the capture).
        if (!_state.SelectedTracks.Contains(track)) Select(track);
        _state.BeginGesture(); // whole drag = one undo entry
        return true;
    }

    /// <summary>Drops the selection onto whichever row the pointer is over; outside the rows, nothing moves.</summary>
    public override void HandlePointerDrag(float x, float y)
    {
        if (!_dragging) return;
        var hovered = Children.OfType<EditorTrack>().FirstOrDefault(row => row.ContainsPoint(x, y));
        if (hovered is not null) _state.MoveSelectedTracks(hovered.Track);
    }

    public void RefreshSelection()
    {
        foreach (var row in Children.OfType<EditorTrack>())
            row.SetSelected(_state.SelectedTracks.Contains(row.Track));
    }
}