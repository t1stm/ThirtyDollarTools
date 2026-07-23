using OpenTK.Mathematics;
using Shared.Renderer.Planes;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;

namespace EditorScene.Scenes.Components;

/// <summary>
///     Plots a note's generated automation events as a step path in the note's sound
///     color: a horizontal run at the current value, a vertical jump where a keyframe
///     changes it, and a short tick at every generated event (so pure repeats stay
///     visible as "a horizontal line with small vertical lines"). Time-mode gaps are
///     mapped through the note's own segment step rate — display-only approximation
///     when the path crosses into a segment with another tempo. Renders from a fixed
///     pool of ghost panels reassigned every layout, added as children of the host
///     panel at construction. Split out of <see cref="TrackEditorView" /> so automation
///     display (curves, editing) can grow independently.
/// </summary>
internal sealed class AutomationPath
{
    private readonly List<Panel> _marks = [];

    public AutomationPath(UIContext context, Panel host, int poolSize, Vector4 initialColor)
    {
        for (var i = 0; i < poolSize; i++)
        {
            var mark = new GhostPanel(context)
            {
                Width = 0,
                Height = 0,
                Background = new ColoredPlane { Color = initialColor }
            };
            _marks.Add(mark);
            host.AddChild(mark);
        }
    }

    public IReadOnlyList<Panel> Marks => _marks;

    /// <summary>Parks every mark from <paramref name="used" /> onward — the unused tail of the pool this layout.</summary>
    public void HideUnused(int used)
    {
        for (var i = used; i < _marks.Count; i++)
        {
            _marks[i].Width = 0;
            _marks[i].Height = 0;
        }
    }

    public void Draw(TrackEditorGeometry geometry, ProjectTrack track, TrackSegment segment, Note note,
        float segStartPx, Vector4 color, ref int used)
    {
        var stepMinutes = segment.StepMinutes(track.Timing.BPM);
        if (stepMinutes <= 0) return;

        var pixelsPerStep = geometry.PixelsPerStep;
        var scrollX = geometry.ScrollX;
        var rowHeight = geometry.RowHeight;
        var prevX = TrackEditorGeometry.GutterWidth + segStartPx + (note.Step + 0.5f) * pixelsPerStep - scrollX;
        var prevY = geometry.ValueTop(Math.Clamp(note.Value, -TrackEditorGeometry.MaxValue, TrackEditorGeometry.MaxValue)) +
                    rowHeight / 2;

        foreach (var (minutes, generated) in note.Automation!.ExpandNotes(note, 0, stepMinutes))
        {
            var x = TrackEditorGeometry.GutterWidth + segStartPx +
                    (note.Step + 0.5f + (float)(minutes / stepMinutes)) * pixelsPerStep - scrollX;
            var y = geometry.ValueTop(Math.Clamp(generated.Value, -TrackEditorGeometry.MaxValue, TrackEditorGeometry.MaxValue)) +
                    rowHeight / 2;

            // The horizontal run, the value jump (only when the value moved), the tick.
            if (!Mark(ref used, Math.Min(prevX, x), prevY - 0.5f, Math.Abs(x - prevX), 1f, color)) return;
            if (Math.Abs(y - prevY) >= 1f &&
                !Mark(ref used, x - 0.5f, Math.Min(prevY, y), 1f, Math.Abs(y - prevY), color)) return;
            if (!Mark(ref used, x - 1f, y - rowHeight * 0.3f, 2f, rowHeight * 0.6f, color)) return;

            prevX = x;
            prevY = y;
        }
    }

    private bool Mark(ref int used, float x, float y, float width, float height, Vector4 color)
    {
        if (used >= _marks.Count) return false; // pool cap: the path just ends early
        var mark = _marks[used++];
        mark.X = x;
        mark.Y = y;
        mark.Width = width;
        mark.Height = height;
        ((ColoredPlane)mark.Background!).Color = color;
        return true;
    }
}
