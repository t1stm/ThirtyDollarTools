using Shared;
using Sundex.Engine.Text;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using VisualizerScene.Objects.Playfield.Batch.Objects;
using VisualizerScene.Objects.Sound_Values;

namespace VisualizerScene.Objects.Playfield.Batch.Chunks;

public class PlayfieldChunk : IDisposable
{
    private const int MaxValueLength = 8;
    private readonly TextBuffer _textBuffer;

    private PlayfieldChunk(int size, TextProvider provider)
    {
        Renderables = new SoundRenderable[size];
        _textBuffer = new TextBuffer(provider, provider.AssetProvider.DeleteQueue);
        _textBuffer.Resize(size * MaxValueLength * 3);
    }

    private StackCollection MainStackCollection { get; } = new();
    private StackCollection? CutSoundsStackCollection { get; set; }
    private RenderStack<BackgroundBlip>? BackgroundBlips { get; set; }

    public SoundRenderable[] Renderables { get; private set; }
    public float StartY { get; set; }
    public float EndY { get; set; }

    public void Dispose()
    {
        MainStackCollection.Dispose();
        BackgroundBlips?.Dispose();

        _textBuffer.Dispose();
        GC.SuppressFinalize(this);
    }

    public static PlayfieldChunk GenerateFrom(ReadOnlySpan<BaseEvent> slice, LayoutHandler layoutHandler,
        PlayfieldSettings settings)
    {
        var fontProvider = settings.Fonts.LatoBoldProvider;
        var store = settings.AtlasStore;
        var sizing = settings.PlayfieldSizing;
        var length = slice.Length;

        var chunk = new PlayfieldChunk(length, fontProvider)
        {
            StartY = layoutHandler.Y
        };

        var renderables = new SoundRenderable[length];
        var factory = new RenderableFactory(store);
        RenderableFactory? iceFactory = null;

        for (var i = 0; i < length; i++)
        {
            var baseEvent = slice[i];
            if (baseEvent.SoundEvent is null) continue;

            var renderable = renderables[i] = factory.CookUp(baseEvent);

            switch (baseEvent)
            {
                case IndividualCutEvent ice:
                {
                    iceFactory ??= new RenderableFactory(store);
                    renderable.Value = new IndividualCutValue(ice, iceFactory, settings.RenderScale);
                    continue;
                }

                case NormalEvent { SoundEvent: "!bg" }:
                    renderable.Value = new BackgroundEventValue(baseEvent.Value, factory, chunk._textBuffer,
                        sizing.ValueFontSize, settings.RenderScale);
                    continue;
            }

            RenderableFactory.AssignTextBuffers(renderable, baseEvent, chunk._textBuffer, sizing, settings.RenderScale,
                settings.SampleHolder);
        }

        chunk.EndY = layoutHandler.Height + layoutHandler.Size;
        chunk.Renderables = renderables;
        chunk.MainStackCollection.AnimatedStacks = factory.AnimatedAtlases;
        chunk.MainStackCollection.StaticStacks = factory.StaticAtlases;

        if (iceFactory is not null)
        {
            chunk.CutSoundsStackCollection ??= new StackCollection();
            chunk.CutSoundsStackCollection.AnimatedStacks = iceFactory.AnimatedAtlases;
            chunk.CutSoundsStackCollection.StaticStacks = iceFactory.StaticAtlases;
        }

        chunk.BackgroundBlips = factory.BackgroundBlips;
        return chunk;
    }

    public void Render(DollarStoreCamera temporaryCamera)
    {
        foreach (var renderable in Renderables) renderable.Update();

        MainStackCollection.Render(temporaryCamera);
        BackgroundBlips?.Render(temporaryCamera);
        _textBuffer.Render(temporaryCamera);

        CutSoundsStackCollection?.Render(temporaryCamera);
    }
}