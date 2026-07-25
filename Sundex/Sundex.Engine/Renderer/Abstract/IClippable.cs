using OpenTK.Mathematics;

namespace Sundex.Engine.Renderer.Abstract;

/// <summary>
///     A renderable that can be clipped to a rectangle (scissor test).
/// </summary>
public interface IClippable
{
    /// <summary>
    ///     Clip rectangle in UI/camera space as (x1, y1, x2, y2) with a top-left origin;
    ///     null renders unclipped. Applied by <c>UIContext.Render</c>.
    /// </summary>
    Vector4i? ClipRect { get; set; }
}
