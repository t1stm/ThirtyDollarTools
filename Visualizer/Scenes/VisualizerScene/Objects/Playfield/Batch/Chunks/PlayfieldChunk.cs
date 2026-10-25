using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Cameras;
using Sundex.Engine.Text;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;
using VisualizerScene.Objects.Playfield.Batch.Objects;
using VisualizerScene.Objects.Sound_Values;

namespace VisualizerScene.Objects.Playfield.Batch.Chunks;

public class PlayfieldChunk : IDisposable, IRenderable, IClippable
{
    private readonly RenderableFactory _factory;

    /// <summary>The renderables this chunk has cooked up, reused by every <see cref="Fill" />.</summary>
    private readonly List<SoundRenderable> _pool = [];

    private readonly TextBuffer _textBuffer;
    private RenderableFactory? _iceFactory;

    /// <summary>An empty chunk, for <see cref="Fill" />. <see cref="GenerateFrom" /> builds and fills one.</summary>
    public PlayfieldChunk(PlayfieldSettings settings)
    {
        var provider = settings.Fonts.LatoBoldProvider;
        _textBuffer = new TextBuffer(provider, provider.AssetProvider.DeleteQueue);
        _factory = new RenderableFactory(settings.AtlasStore);
        MainStackCollection.AnimatedStacks = _factory.AnimatedAtlases;
        MainStackCollection.StaticStacks = _factory.StaticAtlases;
    }

    private StackCollection MainStackCollection { get; } = new();
    private StackCollection? CutSoundsStackCollection { get; set; }
    private RenderStack<BackgroundBlip>? BackgroundBlips { get; set; }

    /// <summary>One per event of the slice, null where the event has no sound.</summary>
    public SoundRenderable[] Renderables { get; private set; } = [];
    public float StartY { get; set; }
    public float EndY { get; set; }

    /// <summary>
    ///     Lets a chunk be queued through <c>UIContext.QueueRender</c> and live inside a
    ///     ScrollView, instead of only being camera-rendered by <see cref="Playfield" /> -
    ///     see EditorScene's faithful views. Fanned out to everything the chunk draws that
    ///     can be scissored; the "!bg" blips have no clip of their own and ride unclipped.
    /// </summary>
    public Vector4i? ClipRect
    {
        get;
        set
        {
            field = value;
            MainStackCollection.ClipRect = value;
            if (CutSoundsStackCollection is { } cuts) cuts.ClipRect = value;
            _textBuffer.ClipRect = value;
        }
    }

    public void Dispose()
    {
        MainStackCollection.Dispose();
        CutSoundsStackCollection?.Dispose();
        BackgroundBlips?.Dispose();

        _textBuffer.Dispose();
        GC.SuppressFinalize(this);
    }

    public static PlayfieldChunk GenerateFrom(ReadOnlySpan<BaseEvent> slice, LayoutHandler layoutHandler,
        PlayfieldSettings settings)
    {
        var chunk = new PlayfieldChunk(settings);
        chunk.Fill(slice, layoutHandler, settings);
        return chunk;
    }

    /// <summary>
    ///     Cooks this chunk up as <paramref name="slice" />, over whatever it held before: the
    ///     renderables, their text and the stacks they draw with are all reused, so a pooled
    ///     chunk refilled with a slice no bigger than its last allocates nothing. "#icut" and
    ///     "!bg" values are the exception - each still builds its own value object.
    /// </summary>
    public void Fill(ReadOnlySpan<BaseEvent> slice, LayoutHandler layoutHandler, PlayfieldSettings settings)
    {
        var sizing = settings.PlayfieldSizing;
        var length = slice.Length;

        _textBuffer.Reset();
        _factory.Clear();
        _iceFactory?.Clear();

        StartY = layoutHandler.Y;
        if (Renderables.Length != length) Renderables = new SoundRenderable[length];
        while (_pool.Count < length) _pool.Add(new SoundRenderable());

        for (var i = 0; i < length; i++)
        {
            var baseEvent = slice[i];
            if (baseEvent.SoundEvent is null)
            {
                Renderables[i] = null!;
                continue;
            }

            var renderable = Renderables[i] = _factory.CookUp(baseEvent, _pool[i].Recycle());

            switch (baseEvent)
            {
                case IndividualCutEvent ice:
                {
                    _iceFactory ??= new RenderableFactory(settings.AtlasStore);
                    renderable.Value = new IndividualCutValue(ice, _iceFactory, settings.RenderScale,
                        layoutHandler.Size);
                    continue;
                }

                case NormalEvent { SoundEvent: "!bg" }:
                    renderable.Value = new BackgroundEventValue(baseEvent.Value, _factory, _textBuffer,
                        sizing.ValueFontSize, settings.RenderScale);
                    continue;
            }

            RenderableFactory.AssignTextBuffers(renderable, baseEvent, _textBuffer, sizing, settings.RenderScale,
                settings.SampleHolder);
        }

        EndY = layoutHandler.Height + layoutHandler.Size;

        if (_iceFactory is not null && CutSoundsStackCollection is null)
            CutSoundsStackCollection = new StackCollection
            {
                AnimatedStacks = _iceFactory.AnimatedAtlases,
                StaticStacks = _iceFactory.StaticAtlases,
                ClipRect = ClipRect
            };

        BackgroundBlips = _factory.BackgroundBlips;
    }

    public void Render(Camera temporaryCamera)
    {
        foreach (var renderable in Renderables) renderable.Update();

        MainStackCollection.Render(temporaryCamera);
        BackgroundBlips?.Render(temporaryCamera);
        _textBuffer.Render(temporaryCamera);

        CutSoundsStackCollection?.Render(temporaryCamera);
    }
}