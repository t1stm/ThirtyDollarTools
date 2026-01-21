using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Animations;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Objects.Sound_Values;

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

    public ISoundValue? Value { get; set; }
    public NormalText? Pan { get; set; }
    public NormalText? Volume { get; set; }

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
            _bounceAnimation?.FinalY = value.Y / 4.26666667f;
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
            var transformAdd = _expandAnimation.GetTransform_Add(this);
            transformAdd.X /= Scale.X;
            transformAdd.Y /= Scale.Y;
            
            var scaleMultiplier = _expandAnimation.GetScale_Multiply(this);
            UpdateExpandToTexts(transformAdd, scaleMultiplier.X);
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
    
    private void UpdateExpandToTexts(Vector3 translate, float scale)
    {
        Value?.Translation = Value.Scale * translate / 2;
        Value?.ScaleMultiplier = scale;
        
        Pan?.Translation = Pan.Scale * translate / 2;
        Pan?.ScaleMultiplier = scale;
        
        Volume?.Translation = Volume.Scale * translate / 2;
        Volume?.ScaleMultiplier = scale;
        
        Value?.UpdatePosition();
        Pan?.UpdatePosition();
        Volume?.UpdatePosition();
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
        if (Value is not NormalText wrapper) 
            throw new Exception("SetValue() called on a value that is not NormalText");

        lock (Value)
        {
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

            wrapper.Text.SetValue(characters[..written]);
            wrapper.UpdatePosition();
        }
    }
}