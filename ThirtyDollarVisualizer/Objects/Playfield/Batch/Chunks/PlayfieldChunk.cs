using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;
using ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;
using ThirtyDollarVisualizer.Renderer;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks;

public class PlayfieldChunk
{
    public SoundRenderable[] Renderables { get; private set; }

    /*
     TODO decide how to handle atlas storage, since we need VAOs too
     In a perfect world, we should have a single object that handles VAOs and atlases with a basic call to update and render.
     */
    protected Dictionary<StaticSoundAtlas, GLBufferList<StaticSound>> StaticAtlasesToUpdate { get; private set; } = [];
    protected Dictionary<FramedAtlas, GLBufferList<SoundData>> AnimatedAtlasesToUpdate { get; private set; } = [];

    protected VertexArrayObject? StaticVAO { get; set; }
    protected VertexArrayObject? AnimatedVAO { get; set; }

    private PlayfieldChunk(int size)
    {
        Renderables = new SoundRenderable[size];
    }

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
            var soundRenderable = GenerateSoundRenderable(slice, store, i, animatedAtlases, staticAtlases);
            renderables[i] = soundRenderable;
            PositionSound(layoutHandler, renderables[i]);
        }

        chunk.EndY = layoutHandler.Height;
        chunk.Renderables = renderables;

        return chunk;
    }

    private static SoundRenderable GenerateSoundRenderable(Span<BaseEvent> slice, AtlasStore store, int i,
        Dictionary<FramedAtlas, List<SoundData>> animatedAtlases,
        Dictionary<StaticSoundAtlas, List<StaticSound>> staticAtlases)
    {
        var baseEvent = slice[i];
        var (soundName, value, volume) = baseEvent;
        soundName = soundName ?? throw new Exception("Sound name is null");

        var soundRenderable = new SoundRenderable();

        return store.AnimatedAtlases.TryGetValue(soundName, out var animatedAtlas)
            ? GetAnimatedSoundRenderableData(animatedAtlases, animatedAtlas, soundRenderable)
            : GetStaticSoundRenderableData(soundName, staticAtlases, soundRenderable);
    }

    private static SoundRenderable GetStaticSoundRenderableData(string soundName,
        Dictionary<StaticSoundAtlas, List<StaticSound>> staticAtlases, SoundRenderable soundRenderable)
    {
        foreach (var (atlas, static_sounds) in staticAtlases)
        {
            var found = atlas.TryGetSound(soundName, out var reference);
            if (!found) continue;

            var soundData = new SoundData
            {
                Model = Matrix4.Identity,
                InverseRGBA = Vector4.One
            };

            var staticSound = new StaticSound
            {
                Data = soundData,
                TextureUV = reference.TextureUV,
            };

            static_sounds.Add(staticSound);
            soundRenderable.Scale = (reference.Width, reference.Height, 1);
            return soundRenderable;
        }

        throw new Exception("Sound not found in static atlases.");
    }

    private static SoundRenderable GetAnimatedSoundRenderableData(
        Dictionary<FramedAtlas, List<SoundData>> animatedAtlases,
        FramedAtlas animatedAtlas, SoundRenderable soundRenderable)
    {
        var uv = animatedAtlas.CurrentUV;
        var (w, h) = (uv.UV0.X * animatedAtlas.Width, uv.UV1.Y * animatedAtlas.Height);
        soundRenderable.Scale = (w, h, 1);

        var soundData = new SoundData
        {
            Model = Matrix4.Identity,
            InverseRGBA = Vector4.One
        };

        if (!animatedAtlases.TryGetValue(animatedAtlas, out var list))
            animatedAtlases.Add(animatedAtlas, list = []);

        list.Add(soundData);
        return soundRenderable;
    }

    private static void PositionSound(LayoutHandler layoutHandler, in SoundRenderable sound)
    {
        // get the current sound's texture information
        var (texture_x, texture_y, _) = sound.Scale;
        // get the aspect ratio for events without an equal size
        var aspect_ratio = texture_x / texture_y;

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

    public void Render(DollarStoreCamera temporaryCamera)
    {
        var matrix = temporaryCamera.GetVPMatrix();
        if (StaticVAO != null)
        {
            StaticVAO.Bind();
            StaticVAO.Update();
        }

        if (AnimatedVAO != null)
        {
            AnimatedVAO.Bind();
            AnimatedVAO.Update();
        }
    }

    private void Destroy()
    {
        foreach (var (_, buffer_object) in StaticAtlasesToUpdate)
        {
            buffer_object.Dispose();
        }

        foreach (var (_, buffer_object) in AnimatedAtlasesToUpdate)
        {
            buffer_object.Dispose();
        }

        StaticAtlasesToUpdate.Clear();
        AnimatedAtlasesToUpdate.Clear();
    }

    ~PlayfieldChunk() => Destroy();
}