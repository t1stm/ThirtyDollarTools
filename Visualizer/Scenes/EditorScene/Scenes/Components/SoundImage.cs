using OpenTK.Mathematics;
using Shared.Atlases;
using Sundex.Components.Abstractions;
using Sundex.Components.Abstractions.Values;
using Sundex.Components.Panels;
using Sundex.Engine.Renderer.Data_Buffers;
using VisualizerScene.Objects.Playfield.Batch.Chunks;
using VisualizerScene.Objects.Playfield.Batch.Objects;

namespace EditorScene.Scenes.Components;

/// <summary>
///     A single sound's image, aspect-fit inside the element's box — the toolbar's
///     "active sound" display. Purely decorative (never takes pointer input); drawn
///     through the same atlas render stacks as <see cref="SoundPicker" />.
/// </summary>
public class SoundImage : Panel
{
    private const string AnimatedShaderLocation = "Assets/Shaders/Playfield/Chunk/Animated";
    private const string StaticShaderLocation = "Assets/Shaders/Playfield/Chunk/Static";

    private readonly AtlasStore _store;
    private readonly StackCollection _stacks = new();
    private TrackedBufferReference<SoundData>? _animatedRef;
    private float _aspect = 1f;
    private string? _current;
    private TrackedBufferReference<StaticSound>? _staticRef;

    public SoundImage(UIContext context, AtlasStore store) : base(context)
    {
        _store = store;
        Computed = new ComputedRectangle { OnUpdate = UpdateMatrix };
    }

    public sealed override ComputedRectangle Computed { get; protected set; }

    public bool HasImage => _staticRef != null || _animatedRef != null;

    /// <summary>Swaps the displayed sound; null clears it.</summary>
    /// <returns>True when the sound has an image in the atlases.</returns>
    public bool ShowSound(string? name)
    {
        if (name == _current) return HasImage;
        _current = name;

        // One entry at a time: drop the old stacks (GL buffers free via the
        // DeleteQueue) and rebuild. The StackCollection instance — what the render
        // queue holds — stays the same.
        _stacks.Dispose();
        _staticRef = null;
        _animatedRef = null;
        if (name != null) Build(name);
        UpdateMatrix();
        return HasImage;
    }

    private void Build(string soundName)
    {
        if (_store.AnimatedSounds.TryGetValue(soundName, out var framedAtlas))
        {
            var shader = Context.AssetProvider.ShaderPool.GetOrLoad(AnimatedShaderLocation);
            var stack = new RenderStack<SoundData>(Context.DeleteQueue) { Shader = shader };
            _stacks.AnimatedStacks.Add(framedAtlas, stack);

            stack.List.Add(new SoundData { Model = Matrix4.Identity, RGBA = Vector4.One });
            _animatedRef = stack.List.GetReferenceAt(0);
            _aspect = framedAtlas.CurrentRectangle.Width / (float)framedAtlas.CurrentRectangle.Height;
        }
        else if (_store.StaticSounds.TryGetValue(soundName, out var staticAtlas))
        {
            if (!staticAtlas.TryGetSound(soundName, out var rect)) return;

            var shader = Context.AssetProvider.ShaderPool.GetOrLoad(StaticShaderLocation);
            var stack = new RenderStack<StaticSound>(Context.DeleteQueue) { Shader = shader };
            _stacks.StaticStacks.Add(staticAtlas, stack);

            stack.List.Add(new StaticSound
            {
                Data = new SoundData { Model = Matrix4.Identity, RGBA = Vector4.One },
                TextureUV = QuadUV.FromRectangle(rect, staticAtlas.Width, staticAtlas.Height)
            });
            _staticRef = stack.List.GetReferenceAt(0);
            _aspect = rect.Width / (float)rect.Height;
        }
    }

    private void UpdateMatrix()
    {
        var w = Computed.Width;
        var h = Computed.Height;
        var drawW = _aspect >= 1 ? w : w * _aspect;
        var drawH = _aspect >= 1 ? h / _aspect : h;
        var matrix = Matrix4.CreateScale(drawW, drawH, 1) *
                     Matrix4.CreateTranslation(
                         Computed.AbsoluteX + (w - drawW) / 2,
                         Computed.AbsoluteY + (h - drawH) / 2, 0);

        if (_staticRef != null)
        {
            var value = _staticRef.Value;
            value.Data.Model = matrix;
            _staticRef.Value = value;
        }

        if (_animatedRef != null)
        {
            var value = _animatedRef.Value;
            value.Model = matrix;
            _animatedRef.Value = value;
        }
    }

    public override UIElement? HitTest(float x, float y)
    {
        return null; // decorative: the button underneath takes the clicks
    }

    protected override void DrawSelf(UIContext context)
    {
        context.QueueRender(_stacks, Index);
    }

    public override void StopRendering()
    {
        Context.DequeueRender(_stacks, Index);
        base.StopRendering();
    }

    public override void ApplyClip(Vector4i? clip)
    {
        _stacks.ClipRect = clip;
        base.ApplyClip(clip);
    }
}
