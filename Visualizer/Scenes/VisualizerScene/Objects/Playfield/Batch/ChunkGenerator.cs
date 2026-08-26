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
    public const int DefaultChunkSize = 512;

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
    ///     <see cref="GenerateChunks" /> uses - what a view that redraws a single edit needs,
    ///     instead of regenerating every chunk of a sequence to repaint one badge.
    /// </summary>
    public PlayfieldChunk GenerateChunk(BaseEvent[] events, int chunkIndex)
    {
        var start = chunkIndex * ChunkSize;
        var slice = events.AsSpan(start, Math.Min(events.Length - start, ChunkSize));
        return PlayfieldChunk.GenerateFrom(slice, LayoutHandler, settings);
    }

    public void PositionSounds(ReadOnlySpan<PlayfieldChunk> chunks)
    {
        var state = StartState();
        foreach (var chunk in chunks) state = PositionChunk(chunk, state);
    }

    /// <summary>The layout state the first chunk starts from - see <see cref="PositionChunk" />.</summary>
    public (int SoundIndex, float Y, float Height) StartState()
    {
        LayoutHandler.Reset();
        return (LayoutHandler.CurrentSoundIndex, LayoutHandler.Y, LayoutHandler.Height);
    }

    /// <summary>
    ///     Positions one chunk from a recorded layout state and returns the state the next one
    ///     starts from. A view that redraws only what is on screen keeps those states and lays
    ///     out the visible chunks alone, instead of walking the whole sequence every time it
    ///     moves - which is O(events) per scroll frame and, on an imported cover, the frame.
    /// </summary>
    public (int SoundIndex, float Y, float Height) PositionChunk(PlayfieldChunk chunk,
        (int SoundIndex, float Y, float Height) state)
    {
        LayoutHandler.SeekTo(state.SoundIndex, state.Y, state.Height);

        chunk.StartY = LayoutHandler.Y;
        foreach (var renderable in chunk.Renderables) PositionSound(renderable);
        chunk.EndY = LayoutHandler.Height + LayoutHandler.Size;

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