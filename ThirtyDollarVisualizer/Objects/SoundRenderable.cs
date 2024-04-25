using OpenTK.Mathematics;
using ThirtyDollarParser;
using ThirtyDollarVisualizer.Animations;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.Objects;

public class SoundRenderable : TexturedPlane
{
    private readonly BounceAnimation BounceAnimation;
    private readonly ExpandAnimation ExpandAnimation;
    private readonly FadeAnimation FadeAnimation;
    private readonly Memory<Animation> RenderableAnimations;

    public SoundRenderable(Texture texture, Vector3 position, Vector2 width_height) : base(texture, position,
        width_height)
    {
        BounceAnimation = new BounceAnimation(GetScale().Y / 5f, () => { UpdateModel(false); });
        ExpandAnimation = new ExpandAnimation(() => { UpdateModel(false); });
        FadeAnimation = new FadeAnimation(() => { UpdateModel(false); });
        RenderableAnimations = new Animation[] { BounceAnimation, ExpandAnimation, FadeAnimation };
    }

    private TexturedPlane? ValueRenderable { get; set; }

    public override void Render(Camera camera)
    {
        if (BounceAnimation.IsRunning || ExpandAnimation.IsRunning || FadeAnimation.IsRunning)
            UpdateModel(false, RenderableAnimations.Span);

        base.Render(camera);
    }

    public void SetValueRenderable(TexturedPlane plane)
    {
        ValueRenderable = plane;
    }

    public void Bounce()
    {
        BounceAnimation.StartAnimation();
    }

    public void Expand()
    {
        ExpandAnimation.StartAnimation();
    }

    public void Fade()
    {
        FadeAnimation.StartAnimation();
    }

    public void SetValue(BaseEvent _event, Dictionary<string, Texture> generated_textures,
        ValueChangeWrapMode value_change_wrap_mode)
    {
        if (ValueRenderable is null) return;

        Fade();
        Expand();

        var old_texture = ValueRenderable.GetTexture();
        var found_texture = generated_textures.TryGetValue(_event.PlayTimes.ToString("0.##"), out var texture);
        if (!found_texture) texture = Texture.Transparent1x1;

        if (_event.PlayTimes <= 0)
            texture = value_change_wrap_mode switch
            {
                ValueChangeWrapMode.ResetToDefault => 
                    generated_textures.TryGetValue(_event.OriginalLoop.ToString("0.##"), out var loop_texture) ? loop_texture : Texture.Transparent1x1,
                _ => null
            };

        var this_position = GetPosition();
        var this_scale = GetScale();

        if (texture == null)
        {
            ValueRenderable.SetTexture(texture);
            return;
        }

        if (texture.Width != old_texture?.Width)
        {
            var new_scale = (texture.Width, texture.Height, 0);
            ValueRenderable.SetScale(new_scale);

            var new_position_x = this_position.X + this_scale.X / 2f;
            var new_position = new Vector3(ValueRenderable.GetPosition())
            {
                X = new_position_x
            };

            ValueRenderable.SetPosition(new_position, PositionAlign.TopCenter);
        }

        ValueRenderable?.SetTexture(texture);
    }
}