using ThirtyDollarParser;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;
using ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks;

public class PlayfieldChunk
{
    private PlayfieldChunk(int size)
    {
        Renderables = new SoundRenderable[size];
    }
    
    private Dictionary<StaticSoundAtlas, RenderStack<StaticSound>> StaticStacks { get; set; } = [];
    private Dictionary<FramedAtlas, RenderStack<SoundData>> AnimatedStacks { get; set; } = [];

    public SoundRenderable[] Renderables { get; private set; }
    public float StartY { get; set; }
    public float EndY { get; set; }

    public static PlayfieldChunk GenerateFrom(ReadOnlySpan<BaseEvent> slice, LayoutHandler layoutHandler, AtlasStore store)
    {
        var length = slice.Length;
        var chunk = new PlayfieldChunk(length)
        {
            StartY = layoutHandler.Y
        };

        var renderables = new SoundRenderable[length];
        var factory = new RenderableFactory(store);
        
        for (var i = 0; i < length; i++)
        {
            renderables[i] = factory.CookUp(slice[i]);
        }

        chunk.EndY = layoutHandler.Height + layoutHandler.Size;
        chunk.Renderables = renderables;
        chunk.AnimatedStacks = factory.AnimatedAtlases;
        chunk.StaticStacks = factory.StaticAtlases;
        return chunk;
    }
    

    public void Render(DollarStoreCamera temporaryCamera)
    {
        foreach (var renderable in Renderables)
        {
            renderable.Update();
        }

        foreach (var (atlas, render_stack) in StaticStacks)
        {
            atlas.Bind();
            render_stack.Render(temporaryCamera);
        }

        foreach (var (atlas, render_stack) in AnimatedStacks)
        {
            atlas.Bind();
            render_stack.Render(temporaryCamera);
        }
    }

    private void Destroy()
    {
        foreach (var (_, buffer_object) in StaticStacks) buffer_object.Dispose();
        foreach (var (_, buffer_object) in AnimatedStacks) buffer_object.Dispose();

        StaticStacks.Clear();
        AnimatedStacks.Clear();
    }

    ~PlayfieldChunk()
    {
        Destroy();
    }
}