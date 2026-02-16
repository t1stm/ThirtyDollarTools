using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Text;
using Sundex.Engine.Text.Fonts;

namespace Sundex.Components.Abstractions;

[PreloadGraphicsContext]
public class UIContext : IGamePreloadable
{
    private static AssetProvider _assetProvider = null!;
    private static FontProvider _fontProvider = null!;
    private static TextProvider _textProvider = null!;

    protected readonly List<Queue<IRenderable>> LayeredRenderQueue = [];
    public required Camera Camera { get; set; }
    public float ViewportWidth => Camera.Width;
    public float ViewportHeight => Camera.Height;

    public AssetProvider AssetProvider => _assetProvider;
    public FontProvider FontProvider => _fontProvider;
    public TextProvider TextProvider => _textProvider;

    public Action<CursorType> RequestCursor { get; set; } = _ => { };

    public static void Preload(AssetProvider assetProvider)
    {
        _assetProvider = assetProvider;
        _fontProvider = new FontProvider(assetProvider);
        _textProvider = new TextProvider(_assetProvider, _fontProvider, "Lato Bold");
    }

    public void Clear()
    {
        foreach (var queue in LayeredRenderQueue) queue.Clear();
    }

    public void QueueRender(IRenderable renderable, int index)
    {
        while (LayeredRenderQueue.Count <= index) LayeredRenderQueue.Add(new Queue<IRenderable>());

        var queue = LayeredRenderQueue[index];
        queue.Enqueue(renderable);
    }

    public void Render()
    {
        foreach (var queue in CollectionsMarshal.AsSpan(LayeredRenderQueue))
        foreach (var renderable in queue)
            renderable.Render(Camera);

        GL.Scissor(0, 0, (int)ViewportWidth, (int)ViewportHeight);
    }
}

public enum CursorType
{
    Normal,
    Pointer,
    ResizeX,
    ResizeY
}