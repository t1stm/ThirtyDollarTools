using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Labels;
using Sundex.Components.Scroll;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Components;

/// <summary>
///     The scrollable track list docked in the track column, with its "+ Add track"
///     trailer row. Lifted out of <see cref="EditorInterface" />.
/// </summary>
public sealed class TrackListPanel : ScrollView
{
    // Subtle-filled look matching the menu bar/transport buttons — code-built children
    // never receive the stylesheet, so the fill/hover swap is set inline here.
    private static readonly Vector4 MenuFillColor = EditorPalette.Divider;
    private static readonly Vector4 MenuFillHoverColor = EditorPalette.DividerHover;

    private readonly UIContext _context;
    private readonly EditorState _state;
    private readonly Button _addTrackRow;

    /// <summary>Tracks + names as of the last actual rebuild, so a rebuild triggered by
    /// an unrelated project change (e.g. a note dragged 60x/s) can be skipped when
    /// nothing about the row list itself would change — rows read their track by
    /// reference, so in-place data needs no rebuild.</summary>
    private readonly List<(ProjectTrack Track, string Name)> _built = [];

    public TrackListPanel(UIContext context, EditorState state) : base(context)
    {
        _context = context;
        _state = state;
        Width = LiteralOrComputable.Percent(100);
        Height = LiteralOrComputable.Percent(100);
        Spacing = 4;

        var addTrackFill = new ColoredPlane { Color = MenuFillColor };
        _addTrackRow = new Button(context, "+ Add track")
        {
            Width = LiteralOrComputable.Percent(100),
            Height = 36,
            FontSizePx = 14f,
            BorderRadius = 6,
            Background = addTrackFill,
            OnClick = _ => state.AddTrack(),
            OnHoverEnter = _ => addTrackFill.Color = MenuFillHoverColor,
            OnHoverExit = _ => addTrackFill.Color = MenuFillColor
        };
        AddChild(_addTrackRow);
    }

    public Action<ProjectTrack>? OnContextMenu { get; set; }

    /// <summary>Full row rebuild: the track list is small by design (the grid is what
    /// scales). The add-track row is pulled out and re-appended last so it always trails.
    /// Skipped entirely when the track set and names match the last rebuild — called on
    /// every project change, including per-frame ones (a note drag fires Touch() ~60x/s)
    /// that never touch the track list itself.</summary>
    public void Rebuild()
    {
        var tracks = _state.Project.Tracks;
        if (Unchanged(tracks)) return;

        RemoveChild(_addTrackRow);
        foreach (var row in Children.OfType<EditorTrack>().ToArray())
            RemoveChild(row);
        foreach (var track in tracks)
            AddChild(new EditorTrack(_context, track, _state) { OnContextMenu = t => OnContextMenu?.Invoke(t) });
        AddChild(_addTrackRow);

        _built.Clear();
        _built.AddRange(tracks.Select(t => (t, t.Name)));
    }

    private bool Unchanged(IReadOnlyList<ProjectTrack> tracks)
    {
        if (tracks.Count != _built.Count) return false;
        for (var i = 0; i < tracks.Count; i++)
            if (tracks[i] != _built[i].Track || tracks[i].Name != _built[i].Name)
                return false;
        return true;
    }

    public void RefreshSelection()
    {
        foreach (var row in Children.OfType<EditorTrack>())
            row.SetSelected(row.Track == _state.SelectedTrack);
    }
}
