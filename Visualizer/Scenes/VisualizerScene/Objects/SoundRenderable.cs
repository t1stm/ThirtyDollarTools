using System.Globalization;
using OpenTK.Mathematics;
using Shared.Animations;
using Sundex.Core;
using Sundex.Core.Animations;
using Sundex.Engine.Renderer.Data_Buffers;
using ThirtyDollarConverter.Parser;
using VisualizerScene.Objects.Playfield.Batch.Objects;
using VisualizerScene.Objects.Sound_Values;

namespace VisualizerScene.Objects;

public sealed class SoundRenderable : Renderable
{
    private readonly BounceAnimation? _bounceAnimation;
    private readonly ExpandAnimation? _expandAnimation;
    private readonly FadeAnimation? _fadeAnimation;
    private readonly Memory<Animation> _renderableAnimations;
    private bool _resetAnimationState;

    /// <summary>How high (and which way) the next bounce goes; see <see cref="Bounce" />.</summary>
    private float _bounceScale = 1f;

    public SoundRenderable() : this(Vector3.Zero, Vector2.Zero)
    {
    }

    public SoundRenderable(Vector3 position, Vector2 widthHeight)
    {
        _bounceAnimation = new BounceAnimation(ResetAnimationState);
        _expandAnimation = new ExpandAnimation(ResetAnimationState);
        _fadeAnimation = new FadeAnimation(ResetAnimationState);
        _renderableAnimations = new Animation[] { _bounceAnimation, _expandAnimation, _fadeAnimation };

        Position = position;
        Scale = (widthHeight.X, widthHeight.Y, 1);
    }

    private static readonly SoundData Unbound = new() { Model = Matrix4.Identity, RGBA = Vector4.One };

    // The instance this renderable draws with: a slot of one of its chunk's render stacks, set
    // by RenderableFactory.CookUp. Held as list + index rather than closures over a tracked
    // reference - those were seven objects per sound, and a long song has millions of sounds.
    private GLBufferList<StaticSound>? _staticList;
    private GLBufferList<SoundData>? _animatedList;
    private int _dataIndex;

    public ISoundValue? Value { get; set; }
    public NormalText? Pan { get; set; }
    public NormalText? Volume { get; set; }

    /// <summary>
    ///     The text this renderable showed before its last <see cref="Recycle" />, for
    ///     <see cref="VisualizerScene.Objects.Playfield.Batch.Chunks.RenderableFactory.AssignTextBuffers" /> to fill again
    ///     instead of allocating.
    /// </summary>
    internal NormalText? SpareValue, SpareVolume, SparePan;

    private SoundData Data
    {
        get => _staticList is { } statics ? statics[_dataIndex].Data :
            _animatedList is { } animated ? animated[_dataIndex] : Unbound;
        set
        {
            if (_staticList is { } statics)
            {
                var sound = statics[_dataIndex];
                sound.Data = value;
                statics[_dataIndex] = sound;
            }
            else
            {
                _animatedList?[_dataIndex] = value;
            }
        }
    }

    public override Matrix4 Model
    {
        get => Data.Model;
        set
        {
            var data = Data;
            data.Model = value;
            Data = data;
        }
    }

    public override Vector4 Color
    {
        get => Data.RGBA;
        set
        {
            var data = Data;
            data.RGBA = value;
            Data = data;
        }
    }

    public override Vector3 Scale
    {
        get => base.Scale;
        set
        {
            base.Scale = value;
            _bounceAnimation?.FinalY = value.Y / 4.26666667f * _bounceScale;
        }
    }

    /// <summary>Whether the bounce is still playing - a slot mid-bounce is one being played.</summary>
    public bool IsBouncing => _bounceAnimation?.IsRunning ?? false;

    public bool IsDivider { get; set; }
    public bool HasAnimatedTexture { get; set; }

    private void ResetAnimationState()
    {
        _resetAnimationState = true;
    }

    /// <summary>Draws with slot <paramref name="index" /> of a static atlas's stack.</summary>
    internal void Bind(GLBufferList<StaticSound> list, int index)
    {
        _staticList = list;
        _animatedList = null;
        _dataIndex = index;
    }

    /// <summary>Draws with slot <paramref name="index" /> of an animated atlas's stack.</summary>
    internal void Bind(GLBufferList<SoundData> list, int index)
    {
        _animatedList = list;
        _staticList = null;
        _dataIndex = index;
    }

    public void SetOffsetPercentage(float percentage)
    {
        var data = Data;
        data.OffsetPercentage = percentage;
        Data = data;
    }

    /// <summary>
    ///     Clears this renderable for a pooled chunk to cook it up as another event: unbound,
    ///     animations stopped, no text - its text objects kept as spares.
    /// </summary>
    public SoundRenderable Recycle()
    {
        ResetAnimations();
        _resetAnimationState = false;
        _bounceScale = 1f;

        if (Value is NormalText value) SpareValue = value;
        SpareVolume = Volume ?? SpareVolume;
        SparePan = Pan ?? SparePan;
        Value = null;
        Volume = null;
        Pan = null;

        IsDivider = false;
        HasAnimatedTexture = false;
        Translation = Vector3.Zero;
        _staticList = null;
        _animatedList = null;
        return this;
    }

    public override void Update()
    {
        var animationsRunning = false;
        foreach (var animation in _renderableAnimations.Span)
        {
            animationsRunning = animation.IsRunning;
            if (animationsRunning) break;
        }

        if (!animationsRunning && !_resetAnimationState) return;

        UpdateTextSlicesAndModel();
        _resetAnimationState = false;
    }

    private void UpdateTextSlicesAndModel()
    {
        if (_resetAnimationState)
        {
            Value?.Reset();
            Pan?.Reset();
            Volume?.Reset();
            return;
        }

        if (_bounceAnimation?.IsRunning == true)
        {
            var transformAdd = _bounceAnimation.GetTransform_Add(this);
            UpdateBounceToTexts(transformAdd);
        }

        if (_expandAnimation?.IsRunning == true)
        {
            var scaleMultiplier = _expandAnimation.GetScale_Multiply(this);
            UpdateExpandToTexts(scaleMultiplier.X);
        }

        UpdateModel(false, _renderableAnimations.Span);
    }

    private void UpdateBounceToTexts(Vector3 translation)
    {
        Value?.Translation = translation;
        Pan?.Translation = translation;
        Volume?.Translation = translation;

        Value?.UpdatePosition();
        Pan?.UpdatePosition();
        Volume?.UpdatePosition();
    }

    private void UpdateExpandToTexts(float scale)
    {
        Value?.ScaleMultiplier = scale;
        Pan?.ScaleMultiplier = scale;
        Volume?.ScaleMultiplier = scale;

        Value?.UpdatePosition();
        Pan?.UpdatePosition();
        Volume?.UpdatePosition();
    }


    /// <param name="scale">
    ///     Height against the played bounce: 1 is what the playhead does, a fraction is a
    ///     smaller hop, and a negative one dips instead. Stored as state, so a re-layout
    ///     mid-bounce - which rewrites <see cref="Scale" />, and with it the bounce's height -
    ///     keeps the current hop's size.
    /// </param>
    /// <param name="lengthMs">
    ///     How long the hop takes, in milliseconds. The default is what a played slot does; a
    ///     shorter one reads as feedback on an edit rather than as playback.
    /// </param>
    public void Bounce(float scale = 1f, int lengthMs = BounceAnimation.DefaultLengthMs)
    {
        _bounceScale = scale;
        if (_bounceAnimation is null) return;

        _bounceAnimation.FinalY = Scale.Y / 4.26666667f * scale;
        _bounceAnimation.LengthMs = lengthMs;
        _bounceAnimation.Start();
    }

    public void Expand()
    {
        _expandAnimation?.Start();
    }

    public void Fade()
    {
        _fadeAnimation?.Start();
    }

    public void ResetAnimations()
    {
        foreach (var animation in _renderableAnimations.Span) animation.Reset();
    }

    public void SetValue(BaseEvent ev, ValueChangeWrapMode valueChangeWrapMode)
    {
        if (Value is not NormalText wrapper)
            throw new Exception("SetValue() called on a value that is not NormalText");

        lock (Value)
        {
            Fade();
            Expand();

            Span<char> characters = stackalloc char[32];
            var written = 0;
            switch (ev.WorkingValue)
            {
                case <= 0 when valueChangeWrapMode == ValueChangeWrapMode.ResetToDefault &&
                               !ev.Value.TryFormat(characters, out written, "0.##", CultureInfo.InvariantCulture):
                    throw new Exception("Failed to format to original loop value");
                case > 0 when !ev.WorkingValue.TryFormat(characters, out written, "0.##", CultureInfo.InvariantCulture):
                    throw new Exception("Failed to format to play times");
            }

            wrapper.Text.SetValue(characters[..written]);
            wrapper.UpdatePosition();
        }
    }
}