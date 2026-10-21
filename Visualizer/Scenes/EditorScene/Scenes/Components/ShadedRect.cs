using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;

namespace EditorScene.Scenes.Components;

/// <summary>
///     One <see cref="LineBatch" /> instance: the visualizer's flat-color quad (model and
///     color) plus a <see cref="Shape" /> that shaded-instanced.frag reads. A zero shape is
///     a plain flat rect, so everything written without one keeps the look it always had.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ShadedRect : IGPUReflection
{
    public Matrix4 Model;
    public Vector4 Color;

    /// <summary>
    ///     x: corner radius in px. y: which corners round (<see cref="TopLeft" /> and the rest,
    ///     OR'd), plus the light's kind times 16. z: outline width in px - 0 fills, anything
    ///     above draws only that band inside the edge, and anything below is a reach: the
    ///     box runs that many px past the rect's left edge, so the rect is the right-hand end
    ///     of a wider rounded box (a note's end cap, clipped by the note's corners). w: light
    ///     strength - 0 is unlit. Build one with <see cref="Of" />.
    /// </summary>
    public Vector4 Shape;

    public const int TopLeft = 1, TopRight = 2, BottomRight = 4, BottomLeft = 8;
    public const int Top = TopLeft | TopRight, Bottom = BottomRight | BottomLeft, All = Top | Bottom;

    /// <summary>Which light the shader paints over the fill - the board each one is drawn from.</summary>
    public enum Light
    {
        /// <summary>A note's body: white at the top, black at the bottom, a top highlight and a lip.</summary>
        Note,

        /// <summary>A keyframe marker: lifted at the top, sunk at the bottom, and a lip.</summary>
        Marker,

        /// <summary>An arrangement clip's body, under its name strip: a lip along the bottom.</summary>
        ClipBody,

        /// <summary>An arrangement clip's name strip: a light fading out down it, and a top highlight.</summary>
        ClipStrip
    }

    public static Vector4 Of(float radius, int corners = All, float outline = 0, float strength = 0,
        Light light = Light.Note)
    {
        return new Vector4(radius, corners | (int)light << 4, outline, strength);
    }

    public static void SelfReflectToGL(VertexBufferLayout layout)
    {
        layout.PushMatrix4(1);
        layout.PushFloat(4, true);
        layout.PushFloat(4, true);
    }
}
