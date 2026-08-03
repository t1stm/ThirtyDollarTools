using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using Serilog;
using Shared.Atlases;
using Sundex.Engine.Asset_Management;
using Sundex.Engine.Asset_Management.Extensions;
using Sundex.Engine.Asset_Management.Helpers;
using Sundex.Engine.Asset_Management.Types.Shader;
using Sundex.Engine.Renderer;
using Sundex.Engine.Renderer.Abstract;
using Sundex.Engine.Renderer.Attributes;
using Sundex.Engine.Renderer.Data_Buffers;
using Sundex.Engine.Renderer.Queues;
using Sundex.Engine.Renderer.Shaders;
using Sundex.Engine.Text;
using ThirtyDollarConverter;
using ThirtyDollarConverter.Parser;
using ThirtyDollarConverter.Parser.Custom_Events;
using VisualizerScene.Objects.Playfield.Batch.Objects;
using VisualizerScene.Objects.Sound_Values;

namespace VisualizerScene.Objects.Playfield.Batch.Chunks;

[PreloadGraphicsContext]
public class RenderableFactory(AtlasStore store)
    : IGamePreloadable
{
    private const string AnimatedShaderLocation = "Assets/Shaders/Playfield/Chunk/Animated";
    private const string StaticShaderLocation = "Assets/Shaders/Playfield/Chunk/Static";
    private const string BackgroundBlipShaderLocation = "Assets/Shaders/Playfield/Background/Blip";

    private const int MaxValueLength = 32;
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

    public static void AssignTextBuffers(SoundRenderable renderable, BaseEvent baseEvent, TextBuffer textBuffer,
        PlayfieldSizing sizing, float renderScale, SampleHolder? sampleHolder = null)
    {
        if (FormatValueText(baseEvent) is { } valueText)
        {
            var valueBuffer = textBuffer.GetTextSlice(valueText, (value, buffer, range) =>
                new TextSlice(buffer, range)
                {
                    Value = value,
                    FontSize = sizing.ValueFontSize * renderScale
                }, MaxValueLength);

            renderable.Value = new NormalText(valueBuffer);
        }

        if (FormatVolumeText(baseEvent) is { } volumeText)
        {
            var volumeBuffer = textBuffer.GetTextSlice(volumeText,
                (value, buffer, range) => new TextSlice(buffer, range)
                {
                    Value = value,
                    FontSize = sizing.VolumeFontSize * renderScale
                });
            renderable.Volume = new NormalText(volumeBuffer);
        }

        if (baseEvent is not ExtendedEvent extendedEvent) return;
        if (extendedEvent is { Pan: 0, OffsetInSeconds: 0 }) return;

        if (FormatPanText(baseEvent) is { } panText)
        {
            var panBuffer = textBuffer.GetTextSlice(panText, (value, buffer, range) =>
                new TextSlice(buffer, range)
                {
                    Value = value,
                    FontSize = sizing.PanFontSize * renderScale
                });
            renderable.Pan = new NormalText(panBuffer);
        }

        if (!(extendedEvent.OffsetInSeconds > 0) || sampleHolder is null) return;

        var soundName = baseEvent.SoundEvent;
        if (soundName is null) return;
        if (!sampleHolder.StringToSoundReferences.TryGetValue(soundName, out var sound)) return;
        if (!sampleHolder.SampleList.TryGetValue(sound, out var pcmData)) return;

        var durationSecs = (float)pcmData.Samples / pcmData.SampleRate;
        if (durationSecs <= 0) return;

        renderable.SetOffsetPercentage(
            (float)Math.Clamp(extendedEvent.OffsetInSeconds / durationSecs, 0, 1));
    }

    /// <summary>
    ///     The value badge's text ("+3", "-6", "/2", "100%" for !volume, ...), or null when
    ///     it's suppressed (a zero value on a sound that doesn't always show one). Pulled out
    ///     of <see cref="AssignTextBuffers" /> so anything that wants these exact conventions —
    ///     e.g. the instrument editor's per-sound adjustment readout — can reuse them without
    ///     owning a TextBuffer/SoundRenderable.
    /// </summary>
    public static string? FormatValueText(BaseEvent baseEvent)
    {
        if (baseEvent.Value == 0 && !SoundShouldAlwaysHaveValue(baseEvent.SoundEvent)) return null;

        if (baseEvent.SoundEvent == "!pulse")
        {
            var parsed_value = (long)baseEvent.Value;
            var repeats = (byte)parsed_value;
            float frequency = (short)(parsed_value >> 8);
            return $"{repeats}, {frequency}";
        }

        var valueText = $"{baseEvent.Value:0.##}";
        valueText = baseEvent.ValueScale switch
        {
            ValueScale.Divide => "/" + valueText,
            ValueScale.Times => "x" + valueText,
            ValueScale.Add when baseEvent is { Value: > 0, SoundEvent: not null } &&
                                baseEvent.SoundEvent.StartsWith('!')
                => "+" + valueText,
            ValueScale.None when baseEvent is { Value: > 0, SoundEvent: not null } &&
                                 !baseEvent.SoundEvent.StartsWith('!')
                => "+" + valueText,
            _ => valueText
        };

        if (baseEvent is { SoundEvent: "!volume" } and not { ValueScale: ValueScale.Times } and not
            { ValueScale: ValueScale.Divide }) valueText += "%";
        return valueText;
    }

    /// <summary>The volume badge's text ("100%"), or null when the event has no explicit volume.</summary>
    public static string? FormatVolumeText(BaseEvent baseEvent)
    {
        return baseEvent.Volume is { } volume ? $"{volume:0.##}%" : null;
    }

    /// <summary>
    ///     The pan badge's text ("12&gt;" right, "&lt;12" left), or null when centered/not an
    ///     <see cref="ExtendedEvent" />.
    /// </summary>
    public static string? FormatPanText(BaseEvent baseEvent)
    {
        if (baseEvent is not ExtendedEvent { Pan: not 0 } extendedEvent) return null;
        var panString = Math.Abs((double)extendedEvent.TDWPan).ToString("0.##");
        return extendedEvent.Pan > 0 ? $"{panString}>" : $"<{panString}";
    }

    private static bool SoundShouldAlwaysHaveValue(ReadOnlySpan<char> sound)
    {
        return sound switch
        {
            "!loopmany" or "!volume" or "!speed" or "!stop" or "!transpose" or "!target" or "!jump" or "!bg"
                or "!pulse" => true,
            _ => false
        };
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

        soundRenderable.GetOffsetPercentage = () => trackedReference.Value.Data.OffsetPercentage;
        soundRenderable.SetOffsetPercentage = pct =>
        {
            var oldValue = trackedReference.Value;
            trackedReference.Value = oldValue with { Data = oldValue.Data with { OffsetPercentage = pct } };
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

        soundRenderable.GetOffsetPercentage = () => trackedReference.Value.OffsetPercentage;
        soundRenderable.SetOffsetPercentage = pct =>
        {
            var oldValue = trackedReference.Value;
            trackedReference.Value = oldValue with { OffsetPercentage = pct };
        };

        soundRenderable.HasAnimatedTexture = true;
        return soundRenderable;
    }
}