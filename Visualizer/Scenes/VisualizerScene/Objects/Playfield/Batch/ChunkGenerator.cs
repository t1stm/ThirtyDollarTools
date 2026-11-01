using OpenTK.Mathematics;
using Sundex.Engine.Renderer.Enums;
using ThirtyDollarConverter.Parser;
using VisualizerScene.Objects.Playfield.Batch.Chunks;

namespace VisualizerScene.Objects.Playfield.Batch;

/// <param name="layout">
///     The layout the chunks are positioned with. Null builds the playfield's own, from
///     <paramref name="settings" />; a caller drawing a sequence somewhere other than the
///     playfield (EditorScene's faithful views) passes its own width/origin instead.
/// </param>
public class ChunkGenerator(PlayfieldSettings settings, LayoutHandler? layout = null)
{
    private const int DefaultChunkSize = 512;

    public LayoutHandler LayoutHandler { get; set; } = layout ?? new LayoutHandler(
        settings.PlayfieldSizing.SoundSize * settings.RenderScale,
        settings.PlayfieldSizing.SoundsOnASingleLine,
        settings.PlayfieldSizing.SoundMargin * settings.RenderScale / 2,
        15f * settings.RenderScale);

    public int ChunkSize { get; init; } = DefaultChunkSize;

    public List<PlayfieldChunk> GenerateChunks(BaseEvent[] events)
    {
        var chunkCount = (events.Length + ChunkSize - 1) / ChunkSize;
        var chunksList = new PlayfieldChunk[chunkCount];

        Parallel.For(0, chunkCount, new ParallelOptions { MaxDegreeOfParallelism = 1 },
            chunkIndex =>
            {
                var eventsSpan = events.AsSpan();
                var i = chunkIndex * ChunkSize;

                var clampedSize = Math.Min(eventsSpan.Length - i, ChunkSize);
                var slice = eventsSpan.Slice(i, clampedSize);

                var chunk = PlayfieldChunk.GenerateFrom(slice, LayoutHandler, settings);
                chunksList[chunkIndex] = chunk;
            });

        return [.. chunksList];
    }

    /// <summary>
    ///     Builds one chunk of an event array on its own, with the same slicing
    ///     <see cref="GenerateChunks" /> uses, so a view that changed a single event can
    ///     rebuild just the chunk holding it.
    /// </summary>
    public PlayfieldChunk GenerateChunk(BaseEvent[] events, int chunkIndex)
    {
        var start = chunkIndex * ChunkSize;
        var slice = events.AsSpan(start, Math.Min(events.Length - start, ChunkSize));
        return PlayfieldChunk.GenerateFrom(slice, LayoutHandler, settings);
    }

    /// <summary>
    ///     Lays out every chunk of <paramref name="events" /> without building one: the state each
    ///     starts from and the Y range it covers. A walk of the events and nothing more, so a view
    ///     can keep this for a whole long sequence and build only the chunks it draws.
    ///     <para>
    ///         Each chunk is laid out in its own frame: its <see cref="ChunkLayout.State" /> starts
    ///         at Y 0, and <see cref="ChunkLayout.StartY" /> (a double) is where that 0 sits. A float
    ///         is several pixels coarse tens of millions of pixels down, so a whole long sequence
    ///         laid out in one float frame draws unevenly spaced and judders.
    ///     </para>
    /// </summary>
    /// <param name="height">The whole layout's <see cref="LayoutHandler.Height" />.</param>
    public ChunkLayout[] LayOut(BaseEvent[] events, out double height)
    {
        var layouts = new ChunkLayout[(events.Length + ChunkSize - 1) / ChunkSize];
        var (soundIndex, origin, worldHeight) = StartState();
        double start = origin;
        (int SoundIndex, float Y, float Height) state = (soundIndex, 0, worldHeight - origin);

        for (var chunk = 0; chunk < layouts.Length; chunk++)
        {
            LayoutHandler.SeekTo(state.SoundIndex, state.Y, state.Height);

            var end = Math.Min(events.Length, (chunk + 1) * ChunkSize);
            for (var i = chunk * ChunkSize; i < end; i++)
                LayoutHandler.GetNewPosition(events[i].SoundEvent == "!divider");

            layouts[chunk] = new ChunkLayout(state, start, start + LayoutHandler.Y + LayoutHandler.Size);
            start += LayoutHandler.Y;
            state = (LayoutHandler.CurrentSoundIndex, 0, LayoutHandler.Height - LayoutHandler.Y);
        }

        height = start + state.Height;
        return layouts;
    }

    /// <summary>
    ///     The top-left of the box event <paramref name="index" /> is laid out in, walked from
    ///     its chunk's recorded <paramref name="state" /> - for an event whose chunk isn't built.
    ///     In the state's frame: from a <see cref="LayOut" /> state, add the chunk's StartY.
    /// </summary>
    public Vector2 BoxPosition(BaseEvent[] events, int index, (int SoundIndex, float Y, float Height) state)
    {
        LayoutHandler.SeekTo(state.SoundIndex, state.Y, state.Height);

        var position = Vector2.Zero;
        for (var i = index / ChunkSize * ChunkSize; i <= index; i++)
            position = LayoutHandler.GetNewPosition(events[i].SoundEvent == "!divider");
        return position;
    }

    /// <summary>The layout state the first chunk starts from - see <see cref="PositionChunk" />.</summary>
    public (int SoundIndex, float Y, float Height) StartState()
    {
        LayoutHandler.Reset();
        return (LayoutHandler.CurrentSoundIndex, LayoutHandler.Y, LayoutHandler.Height);
    }

    /// <summary>
    ///     Positions one chunk from a recorded layout state and returns the state the next one
    ///     starts from. A view that draws only what is on screen keeps those states and lays
    ///     out the visible chunks alone, rather than walking the whole sequence per frame.
    /// </summary>
    public (int SoundIndex, float Y, float Height) PositionChunk(PlayfieldChunk chunk,
        (int SoundIndex, float Y, float Height) state)
    {
        LayoutHandler.SeekTo(state.SoundIndex, state.Y, state.Height);

        chunk.StartY = LayoutHandler.Y;
        foreach (var renderable in chunk.Renderables) PositionSound(renderable);
        // Y, not Height: Height only moves on a line break, so a chunk that never wraps
        // would report a bottom of Size alone and be culled away wherever it is drawn.
        chunk.EndY = LayoutHandler.Y + LayoutHandler.Size;

        return (LayoutHandler.CurrentSoundIndex, LayoutHandler.Y, LayoutHandler.Height);
    }

    private void PositionSound(in SoundRenderable sound)
    {
        // get the current sound's texture information
        var (texture_x, texture_y, _) = sound.Scale;
        // get the aspect ratio for events without an equal size
        var aspect_ratio = texture_x / texture_y;

        // box scale is the maximum size a sound should cover
        Vector2 box_scale = (LayoutHandler.Size, LayoutHandler.Size);
        // wanted scale is the corrected size by the aspect ratio
        Vector2 wanted_scale = (LayoutHandler.Size, LayoutHandler.Size);

        // handle aspect ratio corrections
        switch (aspect_ratio)
        {
            case > 1:
                wanted_scale.Y = LayoutHandler.Size / aspect_ratio;
                break;
            case < 1:
                wanted_scale.X = LayoutHandler.Size * aspect_ratio;
                break;
        }

        // set the size of the sound's texture to the wanted size
        sound.Scale = (wanted_scale.X, wanted_scale.Y, 0);

        // calculates the wanted position to avoid stretching of the texture
        var box_position = LayoutHandler.GetNewPosition(sound.IsDivider);
        var texture_position = (box_position.X, box_position.Y);

        var delta_x = LayoutHandler.Size - wanted_scale.X;
        var delta_y = LayoutHandler.Size - wanted_scale.Y;

        texture_position.X += delta_x / 2f;
        texture_position.Y += delta_y / 2f;

        sound.Position = (texture_position.X, texture_position.Y, -0.5f);

        // position value, volume, pan to their box locations
        var bottom_center = box_position + (box_scale.X / 2f, box_scale.Y);
        var top_right = box_position + (box_scale.X + 6f, 0f);

        sound.Value?.PositionAlign = PositionAlign.Center;
        sound.Value?.Position = (bottom_center.X, bottom_center.Y - 1f, 0.5f);

        sound.Volume?.PositionAlign = PositionAlign.Top | PositionAlign.Right;
        sound.Volume?.Position = (top_right.X, top_right.Y, 0.5f);

        sound.Pan?.PositionAlign = PositionAlign.Top | PositionAlign.Left;
        sound.Pan?.Position = (box_position.X, box_position.Y, 0.5f);

        sound.UpdateModel(false);
    }
}

/// <summary>
///     Where one chunk of a sequence sits, worked out by <see cref="ChunkGenerator.LayOut" />
///     without building it: the layout state it starts from (see
///     <see cref="ChunkGenerator.PositionChunk" />), in the chunk's own frame where its top is
///     0, and the Y range it covers in the sequence's.
/// </summary>
public readonly record struct ChunkLayout((int SoundIndex, float Y, float Height) State, double StartY, double EndY);
