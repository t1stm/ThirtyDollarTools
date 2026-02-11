using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Serilog;
using Shared.Atlases;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Engine.Asset_Management;
using ThirtyDollarVisualizer.Engine.Asset_Management.Extensions;
using ThirtyDollarVisualizer.Engine.Asset_Management.Helpers;
using ThirtyDollarVisualizer.Engine.Asset_Management.Types.Shader;
using ThirtyDollarVisualizer.Engine.Renderer;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract;
using ThirtyDollarVisualizer.Engine.Renderer.Attributes;
using ThirtyDollarVisualizer.Engine.Renderer.Buffers;
using ThirtyDollarVisualizer.Engine.Renderer.Queues;
using ThirtyDollarVisualizer.Engine.Renderer.Shaders;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace VisualizerScene.Objects.Playfield.Batch.Chunks;

[PreloadGraphicsContext]
public class RenderableFactory(AtlasStore store)
    : IGamePreloadable
{
    private const string AnimatedShaderLocation = "Assets/Shaders/Playfield/Chunk/Animated";
    private const string StaticShaderLocation = "Assets/Shaders/Playfield/Chunk/Static";
    private const string BackgroundBlipShaderLocation = "Assets/Shaders/Playfield/Background/Blip";
    private static ShaderPool _shaderPool = null!;
    private static DeleteQueue _deleteQueue = null!;
    private static ILogger _logger = null!;

    /// <summary>
    ///     Dictionary mapping framed atlases to their corresponding render stacks of animated sound data.
    ///     Used to batch and render sounds with animated textures efficiently by grouping them by their atlas.
    /// </summary>
    public Dictionary<FramedAtlas, RenderStack<SoundData>> AnimatedAtlases { get; } = new();

    /// <summary>
    ///     Dictionary mapping static sound atlases to their corresponding render stacks of static sounds.
    ///     Used to batch and render sounds with static textures efficiently by grouping them by their atlas.
    /// </summary>
    public Dictionary<StaticSoundAtlas, RenderStack<StaticSound>> StaticAtlases { get; } = new();

    /// <summary>
    ///     Contains all blips for background events.
    /// </summary>
    public RenderStack<BackgroundBlip>? BackgroundBlips { get; set; }

    public static void Preload(AssetProvider assetProvider)
    {
        _deleteQueue = assetProvider.DeleteQueue;
        _shaderPool = assetProvider.ShaderPool;
        _logger = assetProvider.Logger.ForContext<RenderableFactory>();

        assetProvider.ShaderPool.PreloadShader(AnimatedShaderLocation, provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader,
                    $"{AnimatedShaderLocation}.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader,
                    $"{AnimatedShaderLocation}.frag")))
        );

        assetProvider.ShaderPool.PreloadShader(StaticShaderLocation, provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader,
                    $"{StaticShaderLocation}.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader,
                    $"{StaticShaderLocation}.frag")))
        );


        assetProvider.ShaderPool.PreloadShader(BackgroundBlipShaderLocation, provider =>
            new Shader(provider, provider.LoadShaders(
                ShaderInfo.CreateFromUnknownStorage(ShaderType.VertexShader,
                    $"{BackgroundBlipShaderLocation}.vert"),
                ShaderInfo.CreateFromUnknownStorage(ShaderType.FragmentShader,
                    $"{BackgroundBlipShaderLocation}.frag")
            ))
        );
    }

    /// <summary>
    ///     Creates a new SoundRenderable from a given Thirty Dollar event.
    /// </summary>
    public SoundRenderable CookUp(BaseEvent baseEvent)
    {
        var soundName = baseEvent.SoundEvent ?? throw new Exception("Sound name is null");
        var soundRenderable = new SoundRenderable
        {
            IsDivider = soundName == "!divider"
        };
        var staticSoundLookup = store.StaticSounds;

        var renderable = store.AnimatedSounds.GetAlternateLookup<ReadOnlySpan<char>>()
            .TryGetValue(soundName, out var storedAnimatedAtlas)
            ? GetAnimatedSoundRenderableData(AnimatedAtlases, storedAnimatedAtlas, soundRenderable)
            : GetStaticSoundRenderableData(soundName, StaticAtlases, staticSoundLookup, soundRenderable) ??
              GetStaticSoundRenderableData("#missing", StaticAtlases, staticSoundLookup, soundRenderable) ??
              throw new Exception("#missing sound is null");

        return renderable;
    }

    public TrackedBufferReference<BackgroundBlip> NewBackgroundBlip(Vector4 color)
    {
        var blip = new BackgroundBlip
        {
            Color = color,
            Model = Matrix4.Identity
        };

        BackgroundBlips ??= new RenderStack<BackgroundBlip>(_deleteQueue, 0, GLQuad.VBOWithUV,
            new VertexBufferLayout().PushFloat(3).PushFloat(2),
            GLQuad.EBO)
        {
            Shader = _shaderPool.GetNamedShader(BackgroundBlipShaderLocation)
        };
        BackgroundBlips.List.Add(blip);
        return BackgroundBlips.List.GetReferenceAt(BackgroundBlips.List.Count - 1);
    }

    private static SoundRenderable? GetStaticSoundRenderableData(
        ReadOnlySpan<char> soundName,
        Dictionary<StaticSoundAtlas, RenderStack<StaticSound>> staticAtlases,
        Dictionary<string, StaticSoundAtlas> storedStaticAtlases,
        SoundRenderable soundRenderable)
    {
        var staticShader = _shaderPool.GetNamedShader(StaticShaderLocation);
        if (!storedStaticAtlases.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(soundName, out var atlas))
        {
            _logger.Error("Unable to find static sound atlas for sound: {SoundName}", soundName.ToString());
            return null;
        }

        var found = atlas.TryGetSound(soundName, out var reference);
        if (!found) return null;

        var soundData = new SoundData
        {
            Model = Matrix4.Identity,
            InverseRGBA = Vector4.One
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

        soundRenderable.GetRGBA = () => trackedReference.Value.Data.InverseRGBA;
        soundRenderable.SetRGBA = rgba =>
        {
            var oldValue = trackedReference.Value;
            trackedReference.Value = oldValue with { Data = oldValue.Data with { InverseRGBA = rgba } };
        };
        return soundRenderable;
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
            InverseRGBA = Vector4.One
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

        soundRenderable.GetRGBA = () => trackedReference.Value.InverseRGBA;
        soundRenderable.SetRGBA = rgba =>
        {
            var oldValue = trackedReference.Value;
            trackedReference.Value = oldValue with { InverseRGBA = rgba };
        };

        soundRenderable.HasAnimatedTexture = true;
        return soundRenderable;
    }
}