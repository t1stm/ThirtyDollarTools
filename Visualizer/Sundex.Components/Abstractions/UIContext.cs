using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Text;
using Sundex.Engine.Text.Fonts;

namespace Sundex.Components.Abstractions;

[PreloadGraphicsContext]
public class UIContext : IGamePreloadable
{
    private static IAssetProvider _assetProvider = null!;
    private static IFontProvider _fontProvider = null!;
    private static ITextProvider _textProvider = null!;

    protected readonly List<List<IRenderable>> LayeredRenderQueue = [];
    public required Camera Camera { get; set; }
    public float ViewportWidth => Camera.Width;
    public float ViewportHeight => Camera.Height;

    public IAssetProvider AssetProvider => _assetProvider;
    public IFontProvider FontProvider => _fontProvider;
    public ITextProvider TextProvider => _textProvider;
    public DeleteQueue DeleteQueue => _assetProvider.DeleteQueue;

    public Action<CursorType> RequestCursor { get; set; } = _ => { };


    public static void Preload(AssetProvider assetProvider)
    {
        _assetProvider = assetProvider;
        _fontProvider = new FontProvider(assetProvider);
        _textProvider = new TextProvider(_assetProvider, _fontProvider, "Lato Bold");
    }

    /// <summary>
    ///     Injects mock provider instances for use in unit tests.
    ///     Should not be called in production code.
    /// </summary>
    internal static void InjectForTesting(
        IAssetProvider assetProvider,
        IFontProvider? fontProvider = null,
        ITextProvider? textProvider = null)
    {
        _assetProvider = assetProvider;
        _fontProvider = fontProvider!;
        _textProvider = textProvider!;
    }

    public void Clear()
    {
        foreach (var queue in LayeredRenderQueue) queue.Clear();
    }

    public void QueueRender(IRenderable renderable, int renderIndex, int queueIndex = -1)
    {
        while (LayeredRenderQueue.Count <= renderIndex)
            LayeredRenderQueue.Add([]);

        var queue = LayeredRenderQueue[renderIndex];
        if (queueIndex < 0 || queueIndex >= queue.Count)
        {
            queue.Add(renderable);
            return;
        }

        queue.Insert(queueIndex, renderable);
    }

    public int DequeueRender(IRenderable renderable, int index)
    {
        if (index < 0 || index >= LayeredRenderQueue.Count) return -1;
        var queue = LayeredRenderQueue[index];

        for (var i = 0; i < queue.Count; i++)
        {
            if (!ReferenceEquals(queue[i], renderable)) continue;

            queue.RemoveAt(i);
            return i;
        }

        return -1;
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