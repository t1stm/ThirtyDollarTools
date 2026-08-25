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
                OnHint = h => OnHint?.Invoke(h)
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

    public void RefreshSelection()
    {
        foreach (var row in Children.OfType<EditorTrack>())
            row.SetSelected(row.Track == _state.SelectedTrack);
    }
}