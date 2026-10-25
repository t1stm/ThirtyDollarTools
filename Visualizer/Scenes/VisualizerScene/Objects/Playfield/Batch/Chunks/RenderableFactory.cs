using System.Globalization;
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
        Span<char> text = stackalloc char[MaxValueLength];
        if (TryFormatValueText(baseEvent, text, out var written))
            // Room to spare only for the two actions whose number counts down as they play
            // (SoundRenderable.SetValue); every other value is drawn once, as it is.
            renderable.Value = PlaceText(renderable.SpareValue, textBuffer, text[..written],
                baseEvent.SoundEvent is "!loopmany" or "!stop" ? MaxValueLength : written,
                sizing.ValueFontSize * renderScale);

        if (TryFormatVolumeText(baseEvent, text, out written))
            renderable.Volume = PlaceText(renderable.SpareVolume, textBuffer, text[..written], written,
                sizing.VolumeFontSize * renderScale);

        if (baseEvent is not ExtendedEvent extendedEvent) return;
        if (extendedEvent is { Pan: 0, OffsetInSeconds: 0 }) return;

        if (TryFormatPanText(baseEvent, text, out written))
            renderable.Pan = PlaceText(renderable.SparePan, textBuffer, text[..written], written,
                sizing.PanFontSize * renderScale);

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
    ///     A text slice for one of a renderable's labels: the renderable's spare from its last
    ///     use when a pooled chunk refills it, else a new one.
    /// </summary>
    private static NormalText PlaceText(NormalText? spare, TextBuffer textBuffer, ReadOnlySpan<char> text,
        int capacity, float fontSize)
    {
        if (spare is null) return NewText(textBuffer, text, capacity, fontSize);

        textBuffer.Reuse(spare.Text, text, capacity);
        spare.Translation = Vector3.Zero;
        spare.ScaleMultiplier = 1;
        return spare;
    }

    /// <summary>
    ///     A method of its own because the lambda captures <paramref name="fontSize" />: a closure
    ///     over a parameter is allocated on entry to the method that declares it, whether or not
    ///     the lambda runs, and in <see cref="PlaceText" /> that was every refilled label.
    /// </summary>
    private static NormalText NewText(TextBuffer textBuffer, ReadOnlySpan<char> text, int capacity, float fontSize)
    {
        return new NormalText(textBuffer.GetTextSlice(text, (value, buffer, range) => new TextSlice(buffer, range)
        {
            Value = value,
            FontSize = fontSize
        }, capacity));
    }

    /// <summary>
    ///     The value badge's text ("+3", "-6", "/2", "100%" for !volume, ...), or null when it
    ///     is suppressed (a zero value on a sound that doesn't always show one). Static, so a
    ///     caller with no TextBuffer or SoundRenderable can use the same conventions.
    /// </summary>
    public static string? FormatValueText(BaseEvent baseEvent)
    {
        Span<char> text = stackalloc char[MaxValueLength];
        return TryFormatValueText(baseEvent, text, out var written) ? new string(text[..written]) : null;
    }

    /// <summary>
    ///     <see cref="FormatValueText" /> into <paramref name="destination" />, for a chunk that
    ///     lays out hundreds of these per refill and allocates none of them. False when the badge
    ///     is suppressed, or doesn't fit.
    /// </summary>
    private static bool TryFormatValueText(BaseEvent baseEvent, Span<char> destination, out int written)
    {
        written = 0;
        if (baseEvent.Value == 0 && !SoundShouldAlwaysHaveValue(baseEvent.SoundEvent)) return false;

        if (baseEvent.SoundEvent == "!pulse")
        {
            // Same order the slot's text has: pulses first, then how many beats apart.
            var (pulses, frequency) = NormalEvent.UnpackPulse(baseEvent.Value);
            return destination.TryWrite($"{pulses}, {frequency}", out written);
        }

        var prefix = baseEvent.ValueScale switch
        {
            ValueScale.Divide => "/",
            ValueScale.Times => "x",
            ValueScale.Add when baseEvent is { Value: > 0, SoundEvent: not null } &&
                                baseEvent.SoundEvent.StartsWith('!')
                => "+",
            ValueScale.None when baseEvent is { Value: > 0, SoundEvent: not null } &&
                                 !baseEvent.SoundEvent.StartsWith('!')
                => "+",
            _ => ""
        };

        var suffix = baseEvent is { SoundEvent: "!volume" } and not { ValueScale: ValueScale.Times } and not
            { ValueScale: ValueScale.Divide }
            ? "%"
            : "";
        return destination.TryWrite(CultureInfo.InvariantCulture, $"{prefix}{baseEvent.Value:0.##}{suffix}",
            out written);
    }

    /// <summary>The volume badge's text ("100%"), or null when the event has no explicit volume.</summary>
    public static string? FormatVolumeText(BaseEvent baseEvent)
    {
        Span<char> text = stackalloc char[MaxValueLength];
        return TryFormatVolumeText(baseEvent, text, out var written) ? new string(text[..written]) : null;
    }

    /// <summary><see cref="FormatVolumeText" /> into <paramref name="destination" />.</summary>
    private static bool TryFormatVolumeText(BaseEvent baseEvent, Span<char> destination, out int written)
    {
        written = 0;
        return baseEvent.Volume is { } volume &&
               destination.TryWrite(CultureInfo.InvariantCulture, $"{volume:0.##}%", out written);
    }

    /// <summary>
    ///     The pan badge's text ("12&gt;" right, "&lt;12" left) into <paramref name="destination" />,
    ///     or false when centered/not an <see cref="ExtendedEvent" />.
    /// </summary>
    private static bool TryFormatPanText(BaseEvent baseEvent, Span<char> destination, out int written)
    {
        written = 0;
        if (baseEvent is not ExtendedEvent { Pan: not 0 } extendedEvent) return false;

        var pan = Math.Abs((double)extendedEvent.TDWPan);
        return extendedEvent.Pan > 0
            ? destination.TryWrite(CultureInfo.InvariantCulture, $"{pan:0.##}>", out written)
            : destination.TryWrite(CultureInfo.InvariantCulture, $"<{pan:0.##}", out written);
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
    ///     Creates a SoundRenderable from a given Thirty Dollar event, drawn by this factory's
    ///     stacks.
    /// </summary>
    /// <param name="baseEvent">The event.</param>
    /// <param name="recycled">
    ///     A <see cref="SoundRenderable.Recycle">recycled</see> renderable to cook up instead of a
    ///     new one - a pooled chunk's, after <see cref="Clear" />.
    /// </param>
    public SoundRenderable CookUp(BaseEvent baseEvent, SoundRenderable? recycled = null)
    {
        var soundName = baseEvent.SoundEvent ?? throw new Exception("Sound name is null");
        var soundRenderable = recycled ?? new SoundRenderable();
        soundRenderable.IsDivider = soundName == "!divider";
        var staticSoundLookup = store.StaticSounds;

        var renderable = store.AnimatedSounds.GetAlternateLookup<ReadOnlySpan<char>>()
            .TryGetValue(soundName, out var storedAnimatedAtlas)
            ? GetAnimatedSoundRenderableData(AnimatedAtlases, storedAnimatedAtlas, soundRenderable)
            : GetStaticSoundRenderableData(soundName, StaticAtlases, staticSoundLookup, soundRenderable) ??
              GetStaticSoundRenderableData("#missing", StaticAtlases, staticSoundLookup, soundRenderable) ??
              throw new Exception("#missing sound is null");

        return renderable;
    }

    /// <summary>
    ///     Empties every stack this factory has filled, keeping the stacks and their storage, so
    ///     a pooled chunk can cook up its next slice into them without allocating.
    /// </summary>
    public void Clear()
    {
        foreach (var (_, stack) in StaticAtlases) stack.List.Count = 0;
        foreach (var (_, stack) in AnimatedAtlases) stack.List.Count = 0;
        BackgroundBlips?.List.Count = 0;
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
        soundRenderable.Bind(renderStack.List, renderStack.List.Count - 1);
        soundRenderable.Scale = (reference.Width, reference.Height, 1);
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
        soundRenderable.Bind(renderStack.List, renderStack.List.Count - 1);
        soundRenderable.HasAnimatedTexture = true;
        return soundRenderable;
    }
}