using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract.Extensions;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;
using ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch;

public class ChunkGenerator(PlayfieldSettings settings)
{
    public const int DefaultChunkSize = 512;

    public readonly LayoutHandler LayoutHandler = new(settings.PlayfieldSizing.SoundSize * settings.RenderScale,
        settings.PlayfieldSizing.SoundsOnASingleLine,
        settings.PlayfieldSizing.SoundMargin * settings.RenderScale / 2,
        15f * settings.RenderScale);

    public int ChunkSize { get; init; } = DefaultChunkSize;

    public List<PlayfieldChunk> GenerateChunks(BaseEvent[] events)
    {
        var chunkCount = (events.Length + ChunkSize - 1) / ChunkSize;
        var chunksList = new PlayfieldChunk[chunkCount];

        Parallel.For(0, chunkCount,
            chunkIndex =>
            {
                var eventsSpan = events.AsSpan();
                var i = chunkIndex * ChunkSize;

                var clampedSize = Math.Min(eventsSpan.Length - i, ChunkSize);
                var slice = eventsSpan.Slice(i, clampedSize);

                var chunk = PlayfieldChunk.GenerateFrom(slice, LayoutHandler, settings);
                chunksList[chunkIndex] = chunk;
            });

        return [..chunksList];
    }

    public void PositionSounds(ReadOnlySpan<PlayfieldChunk> chunks)
    {
        LayoutHandler.Reset();
        foreach (var chunk in chunks)
        {
            chunk.StartY = LayoutHandler.Y;
            foreach (var renderable in chunk.Renderables) PositionSound(renderable);

            chunk.EndY = LayoutHandler.Height + LayoutHandler.Size;
        }
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

        sound.Position = (texture_position.X, texture_position.Y, 0);

        // position value, volume, pan to their box locations
        var bottom_center = box_position + (box_scale.X / 2f, box_scale.Y);
        var top_right = box_position + (box_scale.X + 6f, 0f);

        sound.Value?.PositionAlign = PositionAlign.CenterY | PositionAlign.CenterX;
        sound.Value?.Position = (bottom_center.X, bottom_center.Y - 1f, 0);
        
        sound.Volume?.PositionAlign = PositionAlign.Top | PositionAlign.Right;
        sound.Volume?.Position = (top_right.X, top_right.Y, 0);
        
        sound.Pan?.PositionAlign = PositionAlign.Top | PositionAlign.Left;
        sound.Pan?.Position = (box_position.X, box_position.Y, 0);

        sound.UpdateModel(false);
    }
}