using OpenTK.Mathematics;
using Shared.Animations;
using Sundex.Core;
using Sundex.Core.Animations;
using ThirtyDollarConverter.Parser;
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

    public ISoundValue? Value { get; set; }
    public NormalText? Pan { get; set; }
    public NormalText? Volume { get; set; }

    public Func<Matrix4> GetModel { get; set; } = () => Matrix4.Identity;
    public Func<Vector4> GetRGBA { get; set; } = () => Vector4.One;
    public Func<float> GetOffsetPercentage { get; set; } = () => 0f;
    public Action<Matrix4> SetModel { get; set; } = _ => { };
    public Action<Vector4> SetRGBA { get; set; } = _ => { };
    public Action<float> SetOffsetPercentage { get; set; } = _ => { };

    public override Matrix4 Model
    {
        get => GetModel.Invoke();
        set => SetModel.Invoke(value);
    }

    public override Vector4 Color
    {
        get => GetRGBA();
        set => SetRGBA(value);
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
    ///     smaller hop, and a negative one dips instead. Kept as state so a re-layout mid
    ///     bounce (which rewrites <see cref="Scale" />, and with it the bounce's height)
    ///     doesn't flip the hop back to a full upward one.
    /// </param>
    /// <param name="lengthMs">
    ///     How long the hop takes. The default is what a played slot does; a shorter one reads
    ///     as feedback on an edit rather than as "this played".
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
                               !ev.Value.TryFormat(characters, out written, "0.##"):
                    throw new Exception("Failed to format to original loop value");
                case > 0 when !ev.WorkingValue.TryFormat(characters, out written, "0.##"):
                    throw new Exception("Failed to format to play times");
            }

            wrapper.Text.SetValue(characters[..written]);
            wrapper.UpdatePosition();
        }
    }
}