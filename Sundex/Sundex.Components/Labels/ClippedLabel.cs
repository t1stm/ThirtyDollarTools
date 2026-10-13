using OpenTK.Mathematics;
using Sundex.Components.Abstractions;

namespace Sundex.Components.Labels;

/// <summary>
///     A label cut off at its parent's edges. A Sundex label neither wraps nor ellipsizes, so a
///     long name would otherwise paint over whatever sits beside its box. Give it a parent sized
///     to the room it may take (a percent-width panel, say).
/// </summary>
public class ClippedLabel(UIContext context, string text, string? font = null) : Label(context, text, font)
{
    public override void ApplyClip(Vector4i? clip)
    {
        if (Parent is not { } parent)
        {
            base.ApplyClip(clip);
            return;
        }

        var box = parent.Computed;
        var x = (int)box.AbsoluteX;
        var y = (int)box.AbsoluteY;
        base.ApplyClip(IntersectClip(new Vector4i(x, y, x + (int)box.Width, y + (int)box.Height), clip));
    }
}
