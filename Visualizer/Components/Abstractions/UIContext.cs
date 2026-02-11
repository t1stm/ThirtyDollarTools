using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Renderer.Cameras;
using ThirtyDollarVisualizer.Engine.Text;
using ThirtyDollarVisualizer.Engine.Text.Fonts;

namespace Components.Abstractions;

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
        _textProvider = new TextProvider(_assetProvider, _fontProvider, "Lato Regular");
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