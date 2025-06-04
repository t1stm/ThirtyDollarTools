using System.Collections.Concurrent;
using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Animations;
using ThirtyDollarVisualizer.Base_Objects;
using ThirtyDollarVisualizer.Base_Objects.Planes;
using ThirtyDollarVisualizer.Base_Objects.Textures;
using ThirtyDollarVisualizer.Base_Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Objects;

public sealed class SoundRenderable : TexturedPlane
{
    private readonly BounceAnimation _bounceAnimation;
    private readonly ExpandAnimation _expandAnimation;
    private readonly FadeAnimation _fadeAnimation;
    private readonly Memory<Animation> _renderableAnimations;
    public bool IsDivider;

    public float OriginalY;
    public TexturedPlane? Pan;
    public TexturedPlane? Value;
    public TexturedPlane? Volume;

    public SoundRenderable(SingleTexture texture, Vector3 position, Vector2 widthHeight) : base(texture)
    {
        _bounceAnimation = new BounceAnimation(() => { UpdateModel(false); });
        _expandAnimation = new ExpandAnimation(() => { UpdateModel(false); });
        _fadeAnimation = new FadeAnimation(() => { UpdateModel(false); });
        _renderableAnimations = new Animation[] { _bounceAnimation, _expandAnimation, _fadeAnimation };
        
        Position = position;
        Scale = (widthHeight.X, widthHeight.Y, 1);
    }

    public SoundRenderable(SingleTexture texture) :
        this(texture, Vector3.Zero, (texture.Width, texture.Height))
    {
    }

    public override Vector3 Scale
    {
        get => base.Scale;
        set
        {
            base.Scale = value;
            _bounceAnimation.FinalY = value.Y / 5f;
        }
    }

    public override void Render(Camera camera)
    {
        if (_bounceAnimation.IsRunning || _expandAnimation.IsRunning || _fadeAnimation.IsRunning)
            UpdateModel(false, _renderableAnimations.Span);

        base.Render(camera);
    }

    public void UpdateChildren()
    {
        Children.Clear();

        AddChildIfNotNull(Value);
        AddChildIfNotNull(Volume);
        AddChildIfNotNull(Pan);
    }

    private void AddChildIfNotNull(Renderable? child)
    {
        if (child is not null) Children.Add(child);
    }

    public void Bounce()
    {
        _bounceAnimation.Start();
    }

    public void Expand()
    {
        _expandAnimation.Start();
    }

    public void Fade()
    {
        _fadeAnimation.Start();
    }

    public void ResetAnimations()
    {
        foreach (var animation in _renderableAnimations.Span) animation.Reset();
    }

    public void SetValue(BaseEvent @event, ConcurrentDictionary<string, SingleTexture> generatedTextures,
        ValueChangeWrapMode valueChangeWrapMode)
    {
        if (Value is null) return;

        Fade();
        Expand();

        var old_texture = Value.GetTexture();
        var found_texture = generatedTextures.TryGetValue(@event.PlayTimes.ToString("0.##"), out var texture);
        if (!found_texture) texture = StaticTexture.TransparentPixel;

        if (@event.PlayTimes <= 0)
            texture = valueChangeWrapMode switch
            {
                ValueChangeWrapMode.ResetToDefault =>
                    generatedTextures.TryGetValue(@event.OriginalLoop.ToString("0.##"), out var loop_texture)
                        ? loop_texture
                        : StaticTexture.TransparentPixel,
                _ => null
            };

        var this_position = Position;
        var this_scale = Scale;

        if (texture == null)
        {
            Value.SetTexture(texture);
            return;
        }

        if (texture.Width != old_texture?.Width)
        {
            var new_scale = (texture.Width, texture.Height, 0);
            Value.Scale = new_scale;

            var new_position_x = this_position.X + this_scale.X / 2f;
            var new_position = new Vector3(Value.Position)
            {
                X = new_position_x
            };

            Value.SetPosition(new_position, PositionAlign.TopCenter);
        }

        Value.SetTexture(texture);
    }
}