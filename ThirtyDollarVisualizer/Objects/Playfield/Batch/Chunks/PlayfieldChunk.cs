using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Engine.Text;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;
using ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks;

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

    private Dictionary<StaticSoundAtlas, RenderStack<StaticSound>> StaticStacks { get; set; } = [];
    private Dictionary<FramedAtlas, RenderStack<SoundData>> AnimatedStacks { get; set; } = [];

    public SoundRenderable[] Renderables { get; private set; }
    public float StartY { get; set; }
    public float EndY { get; set; }

    public void Dispose()
    {
        foreach (var (_, buffer_object) in StaticStacks) buffer_object.Dispose();
        foreach (var (_, buffer_object) in AnimatedStacks) buffer_object.Dispose();

        _textBuffer.Dispose();
        StaticStacks.Clear();
        AnimatedStacks.Clear();
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

        for (var i = 0; i < length; i++)
        {
            var baseEvent = slice[i];
            if (baseEvent.SoundEvent is null) continue;

            var renderable = renderables[i] = factory.CookUp(baseEvent);

            switch (baseEvent.SoundEvent)
            {
                case "!bg":
                case "!pulse":
                    continue;
            }

            if (baseEvent.Value != 0)
            {
                var valueText = $"{baseEvent.Value:0.##}";
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

                switch (baseEvent.SoundEvent)
                {
                    case "!volume":
                        valueText += "%";
                        break;
                }

                renderable.Value = chunk._textBuffer.GetTextSlice(valueText, (value, buffer, range) =>
                    new TextSlice(buffer, range)
                    {
                        Value = value,
                        FontSize = sizing.ValueFontSize * settings.RenderScale
                    }, MaxValueLength);
            }

            if (baseEvent.Volume is not null)
                renderable.Volume = chunk._textBuffer.GetTextSlice($"{baseEvent.Volume:0.##}%",
                    (value, buffer, range) => new TextSlice(buffer, range)
                    {
                        Value = value,
                        FontSize = sizing.VolumeFontSize * settings.RenderScale
                    });

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

            renderable.Pan = chunk._textBuffer.GetTextSlice(panText, (value, buffer, range) =>
                new TextSlice(buffer, range)
                {
                    Value = value,
                    FontSize = sizing.PanFontSize * settings.RenderScale
                });
        }

        chunk.EndY = layoutHandler.Height + layoutHandler.Size;
        chunk.Renderables = renderables;
        chunk.AnimatedStacks = factory.AnimatedAtlases;
        chunk.StaticStacks = factory.StaticAtlases;
        return chunk;
    }


    public void Render(DollarStoreCamera temporaryCamera)
    {
        foreach (var renderable in Renderables) renderable.Update();

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

        _textBuffer.Render(temporaryCamera);
    }
}