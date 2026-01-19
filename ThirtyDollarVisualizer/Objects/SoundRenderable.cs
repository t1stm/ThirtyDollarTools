using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Animations;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Engine.Renderer.Abstract.Extensions;
using ThirtyDollarVisualizer.Engine.Renderer.Enums;
using ThirtyDollarVisualizer.Engine.Text;

namespace ThirtyDollarVisualizer.Objects;

public sealed class SoundRenderable : Renderable
{
    private readonly BounceAnimation? _bounceAnimation;
    private readonly ExpandAnimation? _expandAnimation;
    private readonly FadeAnimation? _fadeAnimation;
    private readonly Memory<Animation> _renderableAnimations;
    private bool _resetAnimationState;

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

    public TextSlice? Pan { get; set; }
    public TextSlice? Value { get; set; }
    public TextSlice? Volume { get; set; }

    public Func<Matrix4> GetModel { get; set; } = () => Matrix4.Identity;
    public Func<Vector4> GetRGBA { get; set; } = () => Vector4.One;
    public Action<Matrix4> SetModel { get; set; } = _ => { };
    public Action<Vector4> SetRGBA { get; set; } = _ => { };

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
            if (_bounceAnimation != null)
                _bounceAnimation.FinalY = value.Y / 4.26666667f;
        }
    }

    public bool IsDivider { get; set; }

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

        UpdateModel(false, _renderableAnimations.Span);
        _resetAnimationState = false;
    }

    public void Bounce()
    {
        _bounceAnimation?.Start();
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
        if (Value is null) return;

        Fade();
        Expand();

        Span<char> characters = stackalloc char[32];
        var written = 0;
        switch (ev.PlayTimes)
        {
            case <= 0 when valueChangeWrapMode == ValueChangeWrapMode.ResetToDefault &&
                           !ev.OriginalLoop.TryFormat(characters, out written, "0.##") &&
                           !ev.OriginalLoop.TryFormat(characters, out written, "0.##"):
                throw new Exception("Failed to format original loop");
            case > 0 when !ev.PlayTimes.TryFormat(characters, out written, "0.##"):
                throw new Exception("Failed to format play times");
        }

        var this_position = Position;
        var this_scale = Scale;

        Value.SetValue(characters[..written]);

        var new_position_x = this_position.X + this_scale.X / 2f - Value.Scale.X / 2f;
        var new_position = new Vector3
        {
            X = new_position_x,
            Y = Value.Position.Y,
            Z = Value.Position.Z
        };

        Value.SetPosition(new_position, PositionAlign.Top | PositionAlign.CenterX);
        Value.Position = new_position;
    }
}