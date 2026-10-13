using Sundex.Components.Abstractions;
using Sundex.Components.Panels;

namespace EditorScene.Scenes.Views;

/// <summary>
///     The Select tool's rubber band, shared by <see cref="ArrangementView" /> and
///     <see cref="TrackEditorView" />. Anchor and cursor are model-space, on each view's own
///     two axes (quarters/channel, step/value), so mid-drag scrolling can't corrupt the box and
///     items inside it but scrolled out of view still count. The mode is sampled from the
///     modifiers at press and applied at release; the view decides what lies inside the box.
/// </summary>
internal sealed class Marquee(UIContext context)
{
    private (double X, double Y)? _anchor;
    private (double X, double Y) _cursor;
    private Mode _mode;

    /// <summary>
    ///     The box on screen. Never takes input (GhostPanel), fill only, no border; the view
    ///     adds it as its last child so it renders above everything it draws.
    /// </summary>
    public Panel Rect { get; } = new GhostPanel(context) { Width = 0, Height = 0, Classes = ["grid-marquee"] };

    public bool Active => _anchor != null;

    /// <summary>The box in model units, each axis from its low end to its high end.</summary>
    public (double MinX, double MaxX, double MinY, double MaxY) Box
    {
        get
        {
            var anchor = _anchor ?? _cursor;
            return (Math.Min(anchor.X, _cursor.X), Math.Max(anchor.X, _cursor.X),
                Math.Min(anchor.Y, _cursor.Y), Math.Max(anchor.Y, _cursor.Y));
        }
    }

    /// <summary>Starts a box at a point. Shift removes from the selection, Ctrl adds to it, neither replaces it.</summary>
    public void Begin(double x, double y, bool shift, bool ctrl)
    {
        _anchor = _cursor = (x, y);
        _mode = shift ? Mode.Remove : ctrl ? Mode.Append : Mode.Replace;
    }

    public void Drag(double x, double y)
    {
        _cursor = (x, y);
    }

    public void End()
    {
        _anchor = null;
    }

    /// <summary>Hands what the box caught to the selection call the press's modifiers picked.</summary>
    public void Apply<T>(List<T> contained, Action<IEnumerable<T>> replace, Action<IEnumerable<T>> append,
        Action<IEnumerable<T>> remove)
    {
        var apply = _mode switch
        {
            Mode.Append => append,
            Mode.Remove => remove,
            _ => replace // an empty marquee clears the selection
        };
        apply(contained);
    }

    /// <summary>
    ///     Positions <see cref="Rect" /> from the model-space anchor/cursor every frame, so it
    ///     scroll-corrects; zero size (hidden) while no marquee is active.
    ///     <paramref name="toLocal" /> maps a model point to view-local pixels, and the box's
    ///     bottom stops at <paramref name="maxY" />.
    /// </summary>
    public void Layout(Func<double, double, (float X, float Y)> toLocal, float maxY = float.PositiveInfinity)
    {
        if (_anchor is not { } anchor)
        {
            Rect.Width = 0;
            Rect.Height = 0;
            return;
        }

        var (x1, y1) = toLocal(anchor.X, anchor.Y);
        var (x2, y2) = toLocal(_cursor.X, _cursor.Y);
        var top = Math.Min(y1, y2);

        Rect.X = Math.Min(x1, x2);
        Rect.Y = top;
        Rect.Width = Math.Abs(x2 - x1);
        Rect.Height = Math.Max(0, Math.Min(Math.Max(y1, y2), maxY) - top);
    }

    private enum Mode
    {
        Replace,
        Append,
        Remove
    }
}
