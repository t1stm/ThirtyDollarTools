using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Asset_Management.Extensions;
using ThirtyDollarVisualizer.Engine.Asset_Management.Helpers;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Shader;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Renderer.Queues;
using ThirtyDollarVisualizer.Engine.Renderer.Shaders;
using ThirtyDollarVisualizer.Objects.Playfield.Atlas;
using ThirtyDollarVisualizer.Objects.Playfield.Batch.Objects;

namespace ThirtyDollarVisualizer.Objects.Playfield.Batch.Chunks;

[PreloadGraphicsContext] 
public readonly struct RenderableFactory(AtlasStore store)
    : IGamePreloadable
{
    private static ShaderPool _shaderPool = null!;
    private static DeleteQueue _deleteQueue = null!;
    
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

    public static void Preload(AssetProvider assetProvider)
    {
        _deleteQueue = assetProvider.DeleteQueue;
        _shaderPool = assetProvider.ShaderPool;
        
        assetProvider.ShaderPool.PreloadShader(AnimatedShaderLocation, provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader, "Assets/Shaders/Playfield/Chunk/Animated.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader, "Assets/Shaders/Playfield/Chunk/Animated.frag")))
        );
        
        assetProvider.ShaderPool.PreloadShader(StaticShaderLocation, provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader, "Assets/Shaders/Playfield/Chunk/Static.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader, "Assets/Shaders/Playfield/Chunk/Static.frag")))
        );
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
        
        return renderable;
    }

    private static SoundRenderable? GetStaticSoundRenderableData(
        ReadOnlySpan<char> soundName,
        Dictionary<StaticSoundAtlas, RenderStack<StaticSound>> staticAtlases,
        List<StaticSoundAtlas> storedStaticAtlases,
        SoundRenderable soundRenderable)
    {
        var staticShader = _shaderPool.GetNamedShader(StaticShaderLocation);

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
                TextureUV = QuadUV.FromRectangle(reference, atlas.Width, atlas.Height)
            };

            if (!staticAtlases.TryGetValue(atlas, out var renderStack))
                staticAtlases.Add(atlas, renderStack = new RenderStack<StaticSound>(_deleteQueue)
                {
                    Shader = staticShader
                });

            renderStack.List.Add(staticSound);

            var trackedReference = renderStack.List.GetReferenceAt(renderStack.List.Count - 1);

            soundRenderable.Scale = (reference.Width, reference.Height, 1);

            soundRenderable.GetModel = () => trackedReference.Value.Data.Model;
            soundRenderable.SetModel = model =>
            {
                var oldValue = trackedReference.Value;
                trackedReference.Value = oldValue with { Data = oldValue.Data with { Model = model } };
            };

            soundRenderable.GetRGBA = () => trackedReference.Value.Data.RGBA;
            soundRenderable.SetRGBA = rgba =>
            {
                var oldValue = trackedReference.Value;
                trackedReference.Value = oldValue with { Data = oldValue.Data with { RGBA = rgba } };
            };
            return soundRenderable;
        }

        return null;
    }

    private static SoundRenderable GetAnimatedSoundRenderableData(
        Dictionary<FramedAtlas, RenderStack<SoundData>> animatedAtlases,
        FramedAtlas animatedAtlas,
        SoundRenderable soundRenderable)
    {
        var animatedShader = _shaderPool.GetNamedShader(AnimatedShaderLocation);

        var rect = animatedAtlas.CurrentRectangle;
        soundRenderable.Scale = (rect.Width, rect.Height, 1);

        var soundData = new SoundData
        {
            Model = Matrix4.Identity,
            RGBA = Vector4.One
        };

        if (!animatedAtlases.TryGetValue(animatedAtlas, out var renderStack))
            animatedAtlases.Add(animatedAtlas, renderStack = new RenderStack<SoundData>(_deleteQueue)
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
        
        soundRenderable.GetRGBA = () => trackedReference.Value.RGBA;
        soundRenderable.SetRGBA = rgba =>
        {
            var oldValue = trackedReference.Value;
            trackedReference.Value = oldValue with { RGBA = rgba };
        };

        return soundRenderable;
    }
}