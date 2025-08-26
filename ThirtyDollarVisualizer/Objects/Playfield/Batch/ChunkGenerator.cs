using ThirtyDollarParser;
using ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch;

public class ChunkGenerator(PlayfieldSettings settings)
{
    private const int DefaultChunkSize = 512;
    public int ChunkSize { get; init; } = DefaultChunkSize;

    private readonly LayoutHandler _layoutHandler = new(settings.SoundSize * settings.RenderScale,
        settings.SoundsOnASingleLine,
        settings.SoundMargin * settings.RenderScale / 2,
        15f * settings.RenderScale);

    public List<PlayfieldChunk> GenerateChunks(BaseEvent[] events)
    {
        var eventsSpan = events.AsSpan();
        _layoutHandler.Reset();
        var chunksList = new List<PlayfieldChunk>();

        for (var i = 0; i < events.Length; i += ChunkSize)
        {
            var clampedSize = Math.Min(eventsSpan.Length - i, ChunkSize);
            var slice = eventsSpan.Slice(i, clampedSize);

            var chunk = PlayfieldChunk.GenerateFrom(slice, _layoutHandler, settings.AtlasStore);
            chunksList.Add(chunk);
        }

        return chunksList;
    }
}