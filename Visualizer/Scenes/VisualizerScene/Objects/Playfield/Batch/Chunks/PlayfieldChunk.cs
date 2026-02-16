using Shared;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using Sundex.Engine.Text;
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
        _textBuffer = new TextBuffer(provider);
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
                    renderable.Value = new IndividualCutValue(ice, iceFactory);
                    continue;
                }

                case NormalEvent { SoundEvent: "!bg" }:
                    renderable.Value = new BackgroundEventValue(baseEvent.Value, factory, chunk._textBuffer,
                        sizing.ValueFontSize)
                    {
                        ScaleMultiplier = settings.RenderScale
                    };
                    continue;
            }

            if (baseEvent.Value != 0 || SoundShouldAlwaysHaveValue(baseEvent.SoundEvent))
            {
                string valueText;
                switch (baseEvent.SoundEvent)
                {
                    case "!pulse":
                    {
                        var parsed_value = (long)baseEvent.Value;
                        var repeats = (byte)parsed_value;
                        float frequency = (short)(parsed_value >> 8);
                        valueText = $"{repeats}, {frequency}";
                        break;
                    }

                    default:
                    {
                        valueText = $"{baseEvent.Value:0.##}";
                        valueText = baseEvent.ValueScale switch
                        {
                            ValueScale.Divide => "/" + valueText,
                            ValueScale.Times => "x" + valueText,
                            ValueScale.Add when baseEvent.Value > 0 && baseEvent.SoundEvent.StartsWith('!')
                                => "+" + valueText,
                            ValueScale.None when baseEvent.Value > 0 && !baseEvent.SoundEvent.StartsWith('!')
                                => "+" + valueText,
                            _ => valueText
                        };

                        if (baseEvent is { SoundEvent: "!volume" } and not { ValueScale: ValueScale.Times } and not
                            { ValueScale: ValueScale.Divide }) valueText += "%";
                        break;
                    }
                }

                var valueBuffer = chunk._textBuffer.GetTextSlice(valueText, (value, buffer, range) =>
                    new TextSlice(buffer, range)
                    {
                        Value = value,
                        FontSize = sizing.ValueFontSize * settings.RenderScale
                    }, MaxValueLength);

                renderable.Value = new NormalText(valueBuffer);
            }

            if (baseEvent.Volume is not null)
            {
                var volumeBuffer = chunk._textBuffer.GetTextSlice($"{baseEvent.Volume:0.##}%",
                    (value, buffer, range) => new TextSlice(buffer, range)
                    {
                        Value = value,
                        FontSize = sizing.VolumeFontSize * settings.RenderScale
                    });
                renderable.Volume = new NormalText(volumeBuffer);
            }

            if (baseEvent is not PannedEvent pannedEvent) continue;
            if (pannedEvent.Pan == 0) continue;

            string panText;
            if (pannedEvent.IsStandardImplementation)
            {
                var panString = Math.Abs(pannedEvent.TDWPan).ToString("0.##");
                panText = pannedEvent.Pan > 0
                    ? $"{panString}>"
                    : $"<{panString}";
            }
            else
            {
                var panString = Math.Abs(pannedEvent.Pan).ToString("0.##");
                if (panString.StartsWith("0."))
                    panString = panString[1..];

                panText = pannedEvent.Pan > 0
                    ? $"|{panString}"
                    : $"{panString}|";
            }

            var panBuffer = chunk._textBuffer.GetTextSlice(panText, (value, buffer, range) =>
                new TextSlice(buffer, range)
                {
                    Value = value,
                    FontSize = sizing.PanFontSize * settings.RenderScale
                });
            renderable.Pan = new NormalText(panBuffer);
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

    private static bool SoundShouldAlwaysHaveValue(ReadOnlySpan<char> sound)
    {
        return sound switch
        {
            "!loopmany" or "!volume" or "!speed" or "!stop" or "!transpose" or "!target" or "!jump" or "!bg"
                or "!pulse" => true,
            _ => false
        };
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