using System.Runtime.InteropServices;
using OpenTK.Graphics.Egl;
using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;
using ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks.Reference;
using ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks;

public class PlayfieldChunk(int size)
{
    public SoundRenderable[] Renderables = new SoundRenderable[size];
    public Dictionary<StaticSoundAtlas, BufferObject<StaticSound>> StaticAtlases = [];
    public Dictionary<FramedAtlas, BufferObject<SoundData>> AnimatedAtlases = [];

    public float StartY { get; private set; }
    public float EndY { get; private set; }
    
    public static PlayfieldChunk GenerateFrom(Span<BaseEvent> slice, LayoutHandler layoutHandler, AtlasStore store)
    {
        var length = slice.Length;
        var chunk = new PlayfieldChunk(length)
        {
            StartY = layoutHandler.Y
        };
        
        var renderables = new SoundRenderable[length];
        var animatedAtlases = new Dictionary<FramedAtlas, List<SoundData>>();
        var staticAtlases = new Dictionary<StaticSoundAtlas, List<StaticSound>>();
        
        for (var i = 0; i < slice.Length; i++)
        {
            var baseEvent = slice[i];
            var (soundName, value, volume) = baseEvent;
            soundName = soundName ?? throw new Exception("Sound name is null");
            
            var soundRenderable = new SoundRenderable();

            if (store.AnimatedAtlases.TryGetValue(soundName, out var animatedAtlas))
            {
                var uv = animatedAtlas.CurrentUV;
                var (w, h) = (uv.UV0.X * animatedAtlas.Width, uv.UV1.Y * animatedAtlas.Height);
                soundRenderable.Scale = (w, h, 1);
                
                var soundData = new SoundData();
                
                if (!animatedAtlases.TryGetValue(animatedAtlas, out var list))
                    animatedAtlases.Add(animatedAtlas, list = []);
                
                list.Add(soundData);
            }
            
            renderables[i] = soundRenderable;
        }
        
        

        chunk.EndY = layoutHandler.Height;
        chunk.Renderables = renderables;
        return chunk;
    }

    private static void PositionSound(LayoutHandler layoutHandler, in SoundRenderable sound)
    {
        // get the current sound's texture information
        var (texture_x, texture_y, _) = sound.Scale;
        // get the aspect ratio for events without an equal size
        var aspect_ratio = (float)texture_x / texture_y;

        // box scale is the maximum size a sound should cover
        Vector2 box_scale = (layoutHandler.Size, layoutHandler.Size);
        // wanted scale is the corrected size by the aspect ratio
        Vector2 wanted_scale = (layoutHandler.Size, layoutHandler.Size);

        // handle aspect ratio corrections
        switch (aspect_ratio)
        {
            case > 1:
                wanted_scale.Y = layoutHandler.Size / aspect_ratio;
                break;
            case < 1:
                wanted_scale.X = layoutHandler.Size * aspect_ratio;
                break;
        }

        // set the size of the sound's texture to the wanted size
        sound.Scale = (wanted_scale.X, wanted_scale.Y, 0);

        // calculates the wanted position to avoid stretching of the texture
        var box_position = layoutHandler.GetNewPosition();
        var texture_position = (box_position.X, box_position.Y);

        var delta_x = layoutHandler.Size - wanted_scale.X;
        var delta_y = layoutHandler.Size - wanted_scale.Y;

        texture_position.X += delta_x / 2f;
        texture_position.Y += delta_y / 2f;

        sound.SetPosition((texture_position.X, texture_position.Y, 0));

        // position value, volume, pan to their box locations
        var bottom_center = box_position + (box_scale.X / 2f, box_scale.Y);
        var top_right = box_position + (box_scale.X + 6f, 0f);

        sound.Value?.SetPosition((bottom_center.X, bottom_center.Y, 0), PositionAlign.Center);
        sound.Volume?.SetPosition((top_right.X, top_right.Y, 0), PositionAlign.TopRight);
        sound.Pan?.SetPosition((box_position.X, box_position.Y, 0));
        sound.OriginalY = box_position.Y;
    }
    
    public void Update()
    {
        foreach (var atlas in AnimatedAtlases.Keys)
        {
            atlas.Update();
        }
        
        foreach (var atlas in StaticAtlases.Keys)
        {
            atlas.Update();
        }
    }

    public void Render()
    {
        
    }
    
    public void Destroy() {}
}