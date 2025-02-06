using System.Collections.Concurrent;
using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Animations;
using ThirtyDollarVisualizer.Objects.Planes;
using ThirtyDollarVisualizer.Objects.Textures;
using ThirtyDollarVisualizer.Objects.Textures.Static;

namespace ThirtyDollarVisualizer.Objects;

public class SoundRenderable : TexturedPlane
{
    private readonly BounceAnimation BounceAnimation;
    private readonly ExpandAnimation ExpandAnimation;
    private readonly FadeAnimation FadeAnimation;
    private readonly Memory<Animation> RenderableAnimations;
    public bool IsDivider;

    public float Original_Y;
    public TexturedPlane? Pan;
    public TexturedPlane? Value;
    public TexturedPlane? Volume;

    public SoundRenderable(AbstractTexture texture, Vector3 position, Vector2 width_height) : base(texture, position,
        width_height)
    {
        BounceAnimation = new BounceAnimation(() => { UpdateModel(false); });
        ExpandAnimation = new ExpandAnimation(() => { UpdateModel(false); });
        FadeAnimation = new FadeAnimation(() => { UpdateModel(false); });
        RenderableAnimations = new Animation[] { BounceAnimation, ExpandAnimation, FadeAnimation };
    }

    public SoundRenderable(AbstractTexture texture) :
        this(texture, Vector3.Zero, (texture.Width, texture.Height))
    {
    }

    public override void Render(Camera camera)
    {
        if (BounceAnimation.IsRunning || ExpandAnimation.IsRunning || FadeAnimation.IsRunning)
            UpdateModel(false, RenderableAnimations.Span);

        base.Render(camera);
    }

    public override void SetScale(Vector3 scale)
    {
        BounceAnimation.Final_Y = scale.Y / 5f;
        base.SetScale(scale);
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
        BounceAnimation.Start();
    }

    public void Expand()
    {
        ExpandAnimation.Start();
    }

    public void Fade()
    {
        FadeAnimation.Start();
    }

    public void ResetAnimations()
    {
        foreach (var animation in RenderableAnimations.Span) animation.Reset();
    }

    public void SetValue(BaseEvent _event, ConcurrentDictionary<string, AbstractTexture> generated_textures,
        ValueChangeWrapMode value_change_wrap_mode)
    {
        if (Value is null) return;

        Fade();
        Expand();

        var old_texture = Value.GetTexture();
        var found_texture = generated_textures.TryGetValue(_event.PlayTimes.ToString("0.##"), out var texture);
        if (!found_texture) texture = StaticTexture.Transparent1x1;

        if (_event.PlayTimes <= 0)
            texture = value_change_wrap_mode switch
            {
                ValueChangeWrapMode.ResetToDefault =>
                    generated_textures.TryGetValue(_event.OriginalLoop.ToString("0.##"), out var loop_texture)
                        ? loop_texture
                        : StaticTexture.Transparent1x1,
                _ => null
            };

        var this_position = GetPosition();
        var this_scale = GetScale();

        if (texture == null)
        {
            Value.SetTexture(texture);
            return;
        }

        if (texture.Width != old_texture?.Width)
        {
            var new_scale = (texture.Width, texture.Height, 0);
            Value.SetScale(new_scale);

            var new_position_x = this_position.X + this_scale.X / 2f;
            var new_position = new Vector3(Value.GetPosition())
            {
                X = new_position_x
            };

            Value.SetPosition(new_position, PositionAlign.TopCenter);
        }

        Value.SetTexture(texture);
    }
}