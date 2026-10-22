using OpenTK.Mathematics;
using ThirtyDollarConverter.Editor;
using EditorScene.Scenes.Components;

namespace EditorScene.Scenes.Views;

/// <summary>
///     What a clip's body shows of the track it plays: a piano-roll track's notes as 2 px
///     dashes fitted into the body's rows, a faithful track's sequence as one small tile per
///     beat, colored by the sound it plays. Both fade toward the body as the volume drops,
///     the way the notes do in the note editor. The lists are built once per project change
///     (<see cref="EditorState.Revision" />); the layout only places them.
/// </summary>
public partial class ArrangementView
{
    // The boards' numbers, in px from the clip's top: the dashes fit the track's value range
    // into rows 22 to 34, the beats are 6 px tall from row 23. Those are at the default lane
    // height; a scaled lane moves where both end, keeping the boards' margin to the clip's
    // bottom (DashFoot, BeatFoot) and never going below 2 px a beat. A clip spends at most the
    // budget on them, like the wave bars, and pixel columns that already hold a mark are
    // skipped, so a dense track costs what its width can show.
    private const float DashTop = 22f;
    private const float DashFoot = 5f;
    private const float DashHeight = 2f;
    private const float DashLift = 0.35f;
    private const float SelectedDashLift = 0.55f;
    private const float BeatTop = 23f;
    private const float BeatFoot = 12f;
    private const float BeatRadius = 1f;
    private const int MiniatureBudget = 2048;

    private readonly Dictionary<ProjectTrack, Miniature> _miniatures = [];
    private readonly List<(Vector4 Rect, Vector4 Color)> _miniatureMarks = [];
    private readonly int[] _dashColumns = new int[(int)MaxLaneHeight];
    private int _miniatureRevision = -1;
    private MiniatureKey _miniatureKey;

    /// <summary>Bumped whenever the clips' selected flags may have changed - the miniatures' tint follows them.</summary>
    private int _selectionVersion;

    /// <summary>Test seam: every miniature mark the last layout drew, view-local, with its color.</summary>
    internal IReadOnlyList<(Vector4 Rect, Vector4 Color)> MiniatureMarks => _miniatureMarks;

    /// <summary>One note of a piano-roll track, on the arrangement's clock: quarters at the root BPM.</summary>
    private readonly record struct Dash(double Quarter, double Length, double Value, double? Volume);

    /// <summary>One beat of a faithful track: the first sound the walk plays there, and how long until the next.</summary>
    private readonly record struct Beat(double Quarter, double Length, string Sound, double? Volume);

    private sealed record Miniature(List<Dash> Dashes, double Lowest, double Highest, List<Beat> Beats);

    /// <summary>Everything the miniatures' slots depend on; the palettes compare by reference, as a style reload replaces them.</summary>
    private readonly record struct MiniatureKey(
        int Revision,
        int Selection,
        float Zoom,
        float LaneHeight,
        Vector2 Scroll,
        Vector4 View,
        int Start,
        Vector4[] ClipPalette,
        Vector4[] SoundPalette,
        Vector4 ClipColor);

    private void DrawMiniatures(float absX, float absY, float width, int start, ref int used)
    {
        // The playhead re-lays the view out on every frame of playback without moving a clip:
        // with nothing here changed, the slots already hold this layout's marks.
        var key = new MiniatureKey(_state.Revision, _selectionVersion, PixelsPerQuarter, LaneHeight,
            new Vector2(_nav.ScrollX, _nav.ScrollY), new Vector4(absX, absY, width, Computed.Height), start + used,
            ClipPalette, SoundPalette, ClipColor);
        if (key == _miniatureKey)
        {
            used += _miniatureMarks.Count;
            return;
        }

        _miniatureKey = key;
        _miniatureMarks.Clear();
        if (_miniatureRevision != _state.Revision)
        {
            _miniatures.Clear();
            _miniatureRevision = _state.Revision;
        }

        foreach (var block in _blocks)
        {
            var tile = block.Tile;
            // A lane scrolled off costs nothing, and neither does a clip off either side.
            if (tile.Y + tile.W <= RulerHeight || tile.Y >= Computed.Height) continue;
            if (tile.X > width || tile.X + tile.Z < 0) continue;

            var track = block.Placement.Track;
            if (track is WaveTrack) continue;
            if (!_miniatures.TryGetValue(track, out var miniature))
                _miniatures[track] = miniature = Build(track);

            if (track is FaithfulTrack) DrawBeats(miniature, tile, absX, absY, width, start, ref used);
            else DrawDashes(miniature, ColorOf(track), block.Selected, tile, absX, absY, width, start, ref used);
        }
    }

    private void DrawDashes(Miniature miniature, Vector4 color, bool selected, Vector4 tile, float absX, float absY,
        float width, int start, ref int used)
    {
        var span = Math.Max(1, miniature.Highest - miniature.Lowest);
        var tint = Vector4.Lerp(color, Vector4.One, selected ? SelectedDashLift : DashLift);
        var right = Math.Min(tile.X + tile.Z - 2, width);
        var range = (int)Math.Max(0, tile.W - DashTop - DashHeight - DashFoot);
        Array.Fill(_dashColumns, int.MinValue, 0, range + 1);

        var drawn = 0;
        foreach (var dash in miniature.Dashes)
        {
            var x = tile.X + (float)(dash.Quarter * PixelsPerQuarter);
            if (x >= right) break;
            var dashWidth = Math.Max(2, Math.Min((float)(dash.Length * PixelsPerQuarter) - 1, right - x));
            if (x + dashWidth < 0) continue;

            // One dash per row and pixel column: the first one there wins.
            var row = (int)Math.Round((miniature.Highest - dash.Value) / span * range);
            var column = (int)MathF.Floor(x);
            if (_dashColumns[row] == column) continue;
            _dashColumns[row] = column;

            var rect = new Vector4(x, tile.Y + DashTop + row, dashWidth, DashHeight);
            Mark(start, ref used, rect, absX, absY, tint with { W = Presence(dash.Volume) }, Vector4.Zero);
            if (++drawn >= MiniatureBudget) break;
        }
    }

    private void DrawBeats(Miniature miniature, Vector4 tile, float absX, float absY, float width, int start,
        ref int used)
    {
        var right = Math.Min(tile.X + tile.Z - 2, width);
        var beatHeight = Math.Max(2, tile.W - BeatTop - BeatFoot);
        var lastColumn = int.MinValue;
        var drawn = 0;
        foreach (var beat in miniature.Beats)
        {
            var x = tile.X + (float)(beat.Quarter * PixelsPerQuarter);
            if (x >= right) break;
            var beatWidth = Math.Max(2, Math.Min((float)(beat.Length * PixelsPerQuarter) - 1, right - x));
            if (x + beatWidth < 0) continue;

            // A fast "!speed" packs many beats into a pixel: the first one there wins.
            var column = (int)MathF.Floor(x);
            if (column == lastColumn) continue;
            lastColumn = column;

            var color = TrackEditorView.PaletteColor(beat.Sound, SoundPalette);
            var rect = new Vector4(x, tile.Y + BeatTop, beatWidth, beatHeight);
            Mark(start, ref used, rect, absX, absY, color with { W = color.W * Presence(beat.Volume) },
                ShadedRect.Of(BeatRadius));
            if (++drawn >= MiniatureBudget) break;
        }
    }

    private void Mark(int start, ref int used, Vector4 rect, float absX, float absY, Vector4 color, Vector4 shape)
    {
        _lineBatch.Set(start + used++, absX + rect.X, absY + rect.Y, rect.Z, rect.W, color, shape);
        _miniatureMarks.Add((rect, color));
    }

    /// <summary>How solid a mark is at a volume: a silent one keeps a trace, a full one is opaque.</summary>
    private static float Presence(double? volume)
    {
        return (float)(0.35 + 0.65 * Math.Clamp((volume ?? 100) / 100, 0, 1));
    }

    private Miniature Build(ProjectTrack track)
    {
        var bpm = _state.Project.RootTiming.BPM;
        if (track is FaithfulTrack faithful)
        {
            // One beat per distinct instant the walk plays at; a "!combine" stack or a layered
            // instrument shows the first sound in stream order.
            var beats = new List<Beat>();
            foreach (var (minutes, ev) in faithful.PlayedSounds())
            {
                if (beats.Count > 0 && Math.Abs(beats[^1].Quarter - minutes * bpm) < 1e-9) continue;
                beats.Add(new Beat(minutes * bpm, 0, ev.SoundEvent ?? "", ev.Volume));
            }

            var end = track.DurationMinutes() * bpm;
            for (var i = 0; i < beats.Count; i++)
                beats[i] = beats[i] with { Length = (i + 1 < beats.Count ? beats[i + 1].Quarter : end) - beats[i].Quarter };
            return new Miniature([], 0, 0, beats);
        }

        var dashes = new List<Dash>();
        double lowest = double.MaxValue, highest = double.MinValue;
        foreach (var segment in track.Segments)
        foreach (var note in segment.Notes)
        {
            if (note.IsCut) continue;
            var step = track.GlobalStepOf(segment, note);
            // The note's drawn length, as the note editor draws it: its automation's end, or a step.
            var steps = note.Automation is { End: > 0 } automation
                ? Math.Max(1, automation.EndSteps(segment.StepMinutes(track.Timing.BPM)))
                : 1;
            var startMinutes = track.MinutesAtStepPosition(step);
            var endMinutes = track.MinutesAtStepPosition(step + steps);
            dashes.Add(new Dash(startMinutes * bpm, (endMinutes - startMinutes) * bpm, note.Value, note.Volume));
            lowest = Math.Min(lowest, note.Value);
            highest = Math.Max(highest, note.Value);
        }

        dashes.Sort((a, b) => a.Quarter.CompareTo(b.Quarter));
        return new Miniature(dashes, lowest, highest, []);
    }
}
