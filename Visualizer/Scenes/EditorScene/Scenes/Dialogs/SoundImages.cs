using OpenTK.Mathematics;
using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Panels;
using ThirtyDollarConverter.Editor;
using ThirtyDollarConverter.Parser;
using VisualizerScene.Objects.Playfield.Batch.Chunks;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace EditorScene.Scenes.Dialogs;

/// <summary>
///     Sound images for a Sundex panel, drawn from the atlases' instanced render stacks: every
///     image is one instance in the batch of the atlas holding it, so a whole grid draws in one
///     call per atlas. The owner queues <see cref="Stacks" /> as one renderable and hands it the
///     clip its images live under.
/// </summary>
internal sealed class SoundImages(UIContext context, AtlasStore store)
{
    private const string AnimatedShaderLocation = "Assets/Shaders/Playfield/Chunk/Animated";
    private const string StaticShaderLocation = "Assets/Shaders/Playfield/Chunk/Static";

    public StackCollection Stacks { get; } = new();

    /// <summary>
    ///     An image of the sound sized to fit a <paramref name="size" /> square at its atlas aspect
    ///     ratio, or null when no atlas holds it (still loading, or unknown). Atlases are keyed by
    ///     <see cref="Sound.Filename" />, which may be an emoji.
    /// </summary>
    public SoundImage? Create(Sound sound, float size)
    {
        var quad = Add(sound.Filename);
        return quad == null ? null : new SoundImage(context, quad, size);
    }

    /// <summary>
    ///     An instrument's first <paramref name="max" /> sounds as images, the instrument lists'
    ///     preview of what it plays. <paramref name="sounds" /> maps the IDs (or emoji, in older
    ///     projects) instruments name sounds by; a sound it or the atlases don't know is skipped.
    /// </summary>
    public SoundImage[] Icons(Instrument instrument, IReadOnlyDictionary<string, Sound>? sounds, float size, int max)
    {
        return instrument.Sounds
            .Select(sound => sounds?.GetValueOrDefault(sound.Sound) is { } known ? Create(known, size) : null)
            .OfType<SoundImage>()
            .Take(max)
            .ToArray();
    }

    /// <summary>The same, for an element that keeps its own quad; see <see cref="SoundImage" />.</summary>
    public SoundQuad? Add(string atlasKey)
    {
        if (store.AnimatedSounds.TryGetValue(atlasKey, out var framedAtlas))
        {
            if (!Stacks.AnimatedStacks.TryGetValue(framedAtlas, out var stack))
            {
                var shader = context.AssetProvider.ShaderPool.GetOrLoad(AnimatedShaderLocation);
                stack = new RenderStack<SoundData>(context.DeleteQueue) { Shader = shader };
                Stacks.AnimatedStacks.Add(framedAtlas, stack);
            }

            stack.List.Add(new SoundData { Model = Matrix4.Identity, RGBA = Vector4.One });
            var reference = stack.List.GetReferenceAt(stack.List.Count - 1);

            return new SoundQuad(framedAtlas.CurrentRectangle.Width / (float)framedAtlas.CurrentRectangle.Height,
                model =>
                {
                    var value = reference.Value;
                    value.Model = model;
                    reference.Value = value;
                },
                () => stack.List.Remove(reference));
        }

        if (store.StaticSounds.TryGetValue(atlasKey, out var staticAtlas) &&
            staticAtlas.TryGetSound(atlasKey, out var rect))
        {
            if (!Stacks.StaticStacks.TryGetValue(staticAtlas, out var stack))
            {
                var shader = context.AssetProvider.ShaderPool.GetOrLoad(StaticShaderLocation);
                stack = new RenderStack<StaticSound>(context.DeleteQueue) { Shader = shader };
                Stacks.StaticStacks.Add(staticAtlas, stack);
            }

            stack.List.Add(new StaticSound
            {
                Data = new SoundData { Model = Matrix4.Identity, RGBA = Vector4.One },
                TextureUV = QuadUV.FromRectangle(rect, staticAtlas.Width, staticAtlas.Height)
            });
            var reference = stack.List.GetReferenceAt(stack.List.Count - 1);

            return new SoundQuad(rect.Width / (float)rect.Height,
                model =>
                {
                    var value = reference.Value;
                    value.Data.Model = model;
                    reference.Value = value;
                },
                () => stack.List.Remove(reference));
        }

        return null;
    }
}

/// <summary>One instance in a <see cref="SoundImages" /> batch.</summary>
internal sealed class SoundQuad(float aspect, Action<Matrix4> place, Action release)
{
    /// <summary>Width and height that fit a <paramref name="size" /> square at the image's aspect ratio.</summary>
    public (float Width, float Height) Fit(float size) => aspect > 1 ? (size, size / aspect) : (size * aspect, size);

    public void Place(float x, float y, float width, float height) =>
        place(Matrix4.CreateScale(width, height, 1) * Matrix4.CreateTranslation(x, y, 0));

    /// <summary>
    ///     Frees the instance. The batch is queued as a whole, so an instance left behind keeps
    ///     painting at its last position after its element is gone.
    /// </summary>
    public void Release() => release();
}

/// <summary>
///     A sound's image as an element: its <see cref="SoundQuad" /> follows the element's layout.
///     With no quad (the atlases have no image for it yet, or a headless test) it is an empty box
///     of the same size.
/// </summary>
internal class SoundImage : Panel
{
    private readonly SoundQuad? _quad;

    /// <param name="square">
    ///     Keep the box a <paramref name="size" /> square and centre the image in it, rather than
    ///     shrinking the box to the image - a grid of these then lines up in equal columns.
    /// </param>
    public SoundImage(UIContext context, SoundQuad? quad, float size, bool square = false) : base(context)
    {
        _quad = quad;
        (var width, var height) = square ? (size, size) : quad?.Fit(size) ?? (size, size);
        Width = width;
        Height = height;
        // Chained onto the base hook, not replacing it: its InvalidateCoordinates is what keeps
        // children laid out when a grid moves this element.
        Computed.OnUpdate += () =>
        {
            if (_quad == null) return;
            // Fitting to the box's long side gives the box itself when it already has the
            // image's aspect ratio, and the centred image when the box is a square.
            (var w, var h) = _quad.Fit(MathF.Max(Computed.Width, Computed.Height));
            _quad.Place(Computed.AbsoluteX + (Computed.Width - w) / 2, Computed.AbsoluteY + (Computed.Height - h) / 2, w, h);
        };
    }

    /// <summary>Frees the quad; call when the element leaves for good. See <see cref="SoundQuad.Release" />.</summary>
    public void Release() => _quad?.Release();
}
