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
///     A pool of flat-colored rects (grid/bar lines, markers, block fills) drawn as one
///     instanced draw call instead of one <see cref="Shared.Renderer.Planes.ColoredPlane" />
///     (and one uniform-buffer upload) per rect - cheaper on the GPU/driver and, at a locked
///     frame rate, on power draw. Reuses the visualizer's flat-color instanced quad plumbing
///     (<see cref="RenderStack{TDataType}" /> + <see cref="BackgroundBlip" />, Model+Color only).
///     <see cref="Count" /> is a reservation, not a cap: writing past it grows the buffer
///     (doubling, contents preserved), so a range with no natural bound - a project's clips,
///     a note's automation path - can sit last and simply keep going. Ranges before it are
///     fixed, since growth only ever appends.
/// </summary>
[PreloadGraphicsContext]
internal class LineBatch : IRenderable, IClippable, IGamePreloadable
{
    private static DeleteQueue _deleteQueue = null!;
    private static Shader _shader = null!;
    private RenderStack<BackgroundBlip>? _stack;

    /// <summary>Slots to reserve up front - enough for the fixed ranges, so only the last one grows.</summary>
    public int Count
    {
        set
        {
            _stack ??= new RenderStack<BackgroundBlip>(_deleteQueue, value) { Shader = _shader };
            _stack.List.EnsureCount(value);
        }
    }

    public Vector4i? ClipRect { get; set; }

    [UsedImplicitly]
    public static void Preload(AssetProvider assetProvider)
    {
        _deleteQueue = assetProvider.DeleteQueue;
        _shader = assetProvider.ShaderPool.GetOrLoad("Assets/Shaders/Planes/Colored/instanced", provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader,
                    "Assets/Shaders/Planes/Colored/instanced.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader,
                    "Assets/Shaders/Planes/Colored/instanced.frag")))
        );
    }

    public void Render(Camera camera)
    {
        if (_stack is not { List.Count: > 0 }) return;
        _stack.Render(camera);
    }

    /// <summary>
    ///     Assigns one rect, growing the pool if the slot is past its end. Unchanged slots
    ///     are skipped: the buffer uploads per written index (see GLBuffer's update map),
    ///     and the views re-assign - and re-release - every slot of a multi-thousand-slot
    ///     pool on every layout pass, of which only a handful actually moved.
    /// </summary>
    public void Set(int index, float x, float y, float width, float height, Vector4 color)
    {
        var list = _stack!.List;
        list.EnsureCount(index + 1);

        var model = Matrix4.CreateScale(width, height, 1f) * Matrix4.CreateTranslation(x, y, 0f);
        var current = list[index];
        if (current.Model == model && current.Color == color) return;

        list[index] = new BackgroundBlip { Model = model, Color = color };
    }

    /// <summary>
    ///     Releases a slot - a zero-sized rect draws nothing. A slot past the pool's end
    ///     holds nothing to release, so it is left alone rather than grown into existence.
    /// </summary>
    public void Hide(int index)
    {
        if (_stack is null || index >= _stack.List.Count) return;
        Set(index, 0, 0, 0, 0, default);
    }

    /// <summary>The color a slot is currently painted with - a test seam.</summary>
    public Vector4 ColorOf(int index)
    {
        return _stack!.List[index].Color;
    }
}