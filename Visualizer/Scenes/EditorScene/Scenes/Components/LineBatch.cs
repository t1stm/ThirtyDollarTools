using JetBrains.Annotations;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Extensions;
using Sundex.Engine.Asset_Management.Types.Shader;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Renderer.Shaders;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace EditorScene.Scenes.Components;

/// <summary>
///     A pool of rects (grid/bar lines, markers, block fills) drawn as one instanced draw
///     call instead of one <see cref="Shared.Renderer.Planes.ColoredPlane" /> - and one
///     uniform-buffer upload - per rect. Reuses the visualizer's instanced quad plumbing
///     (<see cref="RenderStack{TDataType}" />) with an instance of its own,
///     <see cref="ShadedRect" />: a rect is flat unless it is given a shape, which rounds
///     its corners, outlines it or lights it (see shaded-instanced.frag).
///     <see cref="Count" /> is a reservation, not a cap: writing past it grows the buffer
///     (doubling, contents preserved), so a range with no natural bound - a project's clips,
///     a note's automation path - can sit last and simply keep going. Ranges before it must
///     be fixed, since growth only ever appends.
/// </summary>
[PreloadGraphicsContext]
internal class LineBatch : IRenderable, IClippable, IGamePreloadable
{
    private static DeleteQueue _deleteQueue = null!;
    private static Shader _shader = null!;
    private RenderStack<ShadedRect>? _stack;

    /// <summary>Slots to reserve up front - enough for the fixed ranges, so only the last one grows.</summary>
    public int Count
    {
        set
        {
            _stack ??= new RenderStack<ShadedRect>(_deleteQueue, value) { Shader = _shader };
            _stack.List.EnsureCount(value);
        }
    }

    public Vector4i? ClipRect { get; set; }

    [UsedImplicitly]
    public static void Preload(AssetProvider assetProvider)
    {
        _deleteQueue = assetProvider.DeleteQueue;
        _shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/Planes/Colored/shaded-instanced", provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader,
                    "Assets/Shaders/Planes/Colored/shaded-instanced.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader,
                    "Assets/Shaders/Planes/Colored/shaded-instanced.frag")))
        );
    }

    public void Render(Camera camera)
    {
        if (_stack is not { List.Count: > 0 }) return;
        _stack.Render(camera);
    }

    /// <summary>
    ///     Assigns one rect, growing the pool if the slot is past its end. Unchanged slots
    ///     are skipped, since the buffer uploads per written index (see GLBuffer's update
    ///     map) and the views re-assign every slot on every layout pass.
    /// </summary>
    public void Set(int index, float x, float y, float width, float height, Vector4 color)
    {
        Set(index, x, y, width, height, color, Vector4.Zero);
    }

    /// <summary>
    ///     Assigns one rect with a shape - see <see cref="ShadedRect.Shape" /> and
    ///     <see cref="ShadedRect.Of" />. A zero shape is the flat rect the overload above writes.
    /// </summary>
    public void Set(int index, float x, float y, float width, float height, Vector4 color, Vector4 shape)
    {
        var list = _stack!.List;
        list.EnsureCount(index + 1);

        // Scale then translate, written out: the views re-assign thousands of slots per layout.
        var model = new Matrix4(width, 0, 0, 0, 0, height, 0, 0, 0, 0, 1, 0, x, y, 0, 1);
        var current = list[index];
        if (current.Model == model && current.Color == color && current.Shape == shape) return;

        list[index] = new ShadedRect { Model = model, Color = color, Shape = shape };
    }

    /// <summary>
    ///     Assigns one rect as a line between two points - the same instanced quad with a
    ///     rotation slipped between its scale and translation, so a diagonal costs no more
    ///     than a straight run. The line is centered on the two points across its thickness.
    /// </summary>
    public void SetLine(int index, float x1, float y1, float x2, float y2, float thickness, Vector4 color)
    {
        var list = _stack!.List;
        list.EnsureCount(index + 1);

        var (dx, dy) = (x2 - x1, y2 - y1);
        var length = MathF.Sqrt(dx * dx + dy * dy);
        var model = Matrix4.CreateScale(length, thickness, 1f) *
                    Matrix4.CreateTranslation(0f, -thickness / 2f, 0f) *
                    Matrix4.CreateRotationZ(MathF.Atan2(dy, dx)) *
                    Matrix4.CreateTranslation(x1, y1, 0f);
        var current = list[index];
        if (current.Model == model && current.Color == color && current.Shape == Vector4.Zero) return;

        list[index] = new ShadedRect { Model = model, Color = color };
    }

    /// <summary>
    ///     Releases a slot - a zero-sized rect draws nothing. A slot past the pool's end
    ///     holds nothing to release, so it is left alone rather than grown into existence.
    /// </summary>
    public void Hide(int index)
    {
        if (_stack is null || index >= _stack.List.Count) return;
        var list = _stack.List;
        // Most released slots were released last layout too: one read, no write.
        if (list[index] is { Color.W: 0 } current && current.Model == Hidden && current.Shape == Vector4.Zero &&
            current.Color == Vector4.Zero) return;

        list[index] = new ShadedRect { Model = Hidden };
    }

    private static readonly Matrix4 Hidden = Matrix4.CreateScale(0, 0, 1);

    /// <summary>The color a slot is currently painted with - a test seam.</summary>
    public Vector4 ColorOf(int index)
    {
        return _stack!.List[index].Color;
    }

    /// <summary>The shape a slot is currently drawn with - a test seam.</summary>
    public Vector4 ShapeOf(int index)
    {
        return _stack!.List[index].Shape;
    }

    /// <summary>
    ///     A slot's rect as (x, y, width, height) in absolute px - a test seam. Only
    ///     meaningful for a slot written by <see cref="Set(int,float,float,float,float,Vector4)" />;
    ///     a line from <see cref="SetLine" /> is rotated and has no rect.
    /// </summary>
    public Vector4 RectOf(int index)
    {
        var model = _stack!.List[index].Model;
        return new Vector4(model.M41, model.M42, model.M11, model.M22);
    }

    /// <summary>How many slots the batch holds, the grown tail included - a test seam.</summary>
    public int SlotCount => _stack?.List.Count ?? 0;
}