using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarParser.Custom_Events;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;
using ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;
using ThirtyDollarVisualizer.Renderer.Abstract;
using ThirtyDollarVisualizer.Renderer.Attributes;
using ThirtyDollarVisualizer.Renderer.Shaders;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks;

[PreloadGL] 
public readonly struct RenderableFactory(AtlasStore store, LayoutHandler layoutHandler)
    : IGLPreloadable
{
    /// <summary>
    /// Dictionary mapping framed atlases to their corresponding render stacks of animated sound data.
    /// Used to batch and render sounds with animated textures efficiently by grouping them by their atlas.
    /// </summary>
    public Dictionary<FramedAtlas, RenderStack<SoundData>> AnimatedAtlases { get; } = new();

    /// <summary>
    /// Dictionary mapping static sound atlases to their corresponding render stacks of static sounds.
    /// Used to batch and render sounds with static textures efficiently by grouping them by their atlas.
    /// </summary>
    public Dictionary<StaticSoundAtlas, RenderStack<StaticSound>> StaticAtlases { get; } = new();

    public static void Preload()
    {
        ShaderPool.PreloadShader(AnimatedShaderLocation, static () =>
            Shader.NewFromPathWithDefaultExtension(AnimatedShaderLocation));

        ShaderPool.PreloadShader(StaticShaderLocation, static () =>
            Shader.NewFromPathWithDefaultExtension(StaticShaderLocation));
    }

    private const string AnimatedShaderLocation = "Assets/Shaders/Playfield/Chunk/Animated";
    private const string StaticShaderLocation = "Assets/Shaders/Playfield/Chunk/Static";

    /// <summary>
    /// Creates a new SoundRenderable from a given Thirty Dollar event.
    /// </summary>
    public SoundRenderable CookUp(BaseEvent baseEvent)
    {
        var soundName = baseEvent.SoundEvent ?? throw new Exception("Sound name is null");

        var soundRenderable = new SoundRenderable
        {
            IsDivider = soundName == "!divider",
        };
        var storedStaticAtlases = store.StaticAtlases;

        var renderable = store.AnimatedAtlases.TryGetValue(soundName, out var storedAnimatedAtlas)
            ? GetAnimatedSoundRenderableData(AnimatedAtlases, storedAnimatedAtlas, soundRenderable)
            : GetStaticSoundRenderableData(soundName, StaticAtlases, storedStaticAtlases, soundRenderable) ??
              GetStaticSoundRenderableData("#missing", StaticAtlases, storedStaticAtlases, soundRenderable) ??
              throw new Exception("#missing sound is null");

        PositionSound(renderable);
        return renderable;
    }

    private static SoundRenderable? GetStaticSoundRenderableData(
        ReadOnlySpan<char> soundName,
        Dictionary<StaticSoundAtlas, RenderStack<StaticSound>> staticAtlases,
        List<StaticSoundAtlas> storedStaticAtlases,
        SoundRenderable soundRenderable)
    {
        var staticShader = ShaderPool.GetNamedShader(StaticShaderLocation);

        foreach (var atlas in storedStaticAtlases)
        {
            var found = atlas.TryGetSound(soundName, out var reference);
            if (!found) continue;

            var soundData = new SoundData
            {
                Model = Matrix4.Identity,
                RGBA = Vector4.One
            };

            var staticSound = new StaticSound
            {
                Data = soundData,
                TextureUV = reference.TextureUV
            };

            if (!staticAtlases.TryGetValue(atlas, out var renderStack))
                staticAtlases.Add(atlas, renderStack = new RenderStack<StaticSound>(1024)
                {
                    Shader = staticShader
                });

            renderStack.List.Add(staticSound);

            var trackedReference = renderStack.List.GetReferenceAt(renderStack.List.Count - 1);

            soundRenderable.Scale = (reference.Width, reference.Height, 1);

            soundRenderable.GetModel = () => trackedReference.Value.Data.Model;
            soundRenderable.SetModel = replace =>
            {
                var oldValue = trackedReference.Value;
                trackedReference.Value = oldValue with { Data = oldValue.Data with { Model = replace } };
            };

            soundRenderable.GetRGBA = () => trackedReference.Value.Data.RGBA;
            return soundRenderable;
        }

        return null;
    }

    private static SoundRenderable GetAnimatedSoundRenderableData(
        Dictionary<FramedAtlas, RenderStack<SoundData>> animatedAtlases,
        FramedAtlas animatedAtlas,
        SoundRenderable soundRenderable)
    {
        var animatedShader = ShaderPool.GetNamedShader(AnimatedShaderLocation);

        var uv = animatedAtlas.CurrentUV;
        var (w, h) = (uv.UV0.X * animatedAtlas.Width, uv.UV1.Y * animatedAtlas.Height);
        soundRenderable.Scale = (w, h, 1);

        var soundData = new SoundData
        {
            Model = Matrix4.Identity,
            RGBA = Vector4.One
        };

        if (!animatedAtlases.TryGetValue(animatedAtlas, out var renderStack))
            animatedAtlases.Add(animatedAtlas, renderStack = new RenderStack<SoundData>(1024)
            {
                Shader = animatedShader
            });

        renderStack.List.Add(soundData);
        var trackedReference = renderStack.List.GetReferenceAt(renderStack.List.Count - 1);

        soundRenderable.GetModel = () => trackedReference.Value.Model;
        soundRenderable.SetModel = matrix =>
        {
            var oldValue = trackedReference.Value;
            trackedReference.Value = oldValue with { Model = matrix };
        };
        
        return soundRenderable;
    }
    
    private void PositionSound(in SoundRenderable sound)
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
        var box_position = layoutHandler.GetNewPosition(sound.IsDivider);
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
    }
}