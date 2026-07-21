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
///     A fixed-size pool of flat-colored rects (grid/bar lines, markers) drawn as one
///     instanced draw call instead of one <see cref="Shared.Renderer.Planes.ColoredPlane" />
///     (and one uniform-buffer upload) per rect — cheaper on the GPU/driver and, at a locked
///     frame rate, on power draw. Reuses the visualizer's flat-color instanced quad plumbing
///     (<see cref="RenderStack{TDataType}" /> + <see cref="BackgroundBlip" />, Model+Color only).
///     Set <see cref="Count" /> once for the pool's lifetime — it fixes the GPU buffer's
///     capacity, so growing it later would write past the allocated buffer.
/// </summary>
[PreloadGraphicsContext]
internal class LineBatch : IRenderable, IClippable, IGamePreloadable
{
    private static DeleteQueue _deleteQueue = null!;
    private static Shader _shader = null!;
    private RenderStack<BackgroundBlip>? _stack;

    public int Count
    {
        set
        {
            _stack ??= new RenderStack<BackgroundBlip>(_deleteQueue, value) { Shader = _shader };
            _stack.List.Count = value;
        }
    }

    public Vector4i? ClipRect { get; set; }

    public void Render(Camera camera)
    {
        if (_stack is not { List.Count: > 0 }) return;
        _stack.Render(camera);
    }

    public void Set(int index, float x, float y, float width, float height, Vector4 color)
    {
        var model = Matrix4.CreateScale(width, height, 1f) * Matrix4.CreateTranslation(x, y, 0f);
        _stack!.List[index] = new BackgroundBlip { Model = model, Color = color };
    }

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
}
