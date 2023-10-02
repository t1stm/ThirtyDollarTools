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
    private TexturedPlane? ValueRenderable { get; set; }

    public SoundRenderable(Texture texture, Vector3 position, Vector2 width_height) : base(texture, position, width_height)
    {
        BounceAnimation = new BounceAnimation(GetScale().Y / 5f, () =>
        {
            UpdateModel(false);
        });
        ExpandAnimation = new ExpandAnimation(() =>
        {
            UpdateModel(false);
        });
        FadeAnimation = new FadeAnimation(() =>
        {
            UpdateModel(false);
        });
    }
    public override void Render(Camera camera)
    {
        if (BounceAnimation.IsRunning || ExpandAnimation.IsRunning || FadeAnimation.IsRunning)
        {
            UpdateModel(false, BounceAnimation, ExpandAnimation, FadeAnimation);
        }
        
        ValueRenderable?.Render(camera);
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

    public void SetValue(Event _event, Dictionary<string, Texture> generated_textures, ValueChangeWrapMode value_change_wrap_mode)
    {
        if (ValueRenderable is null) return;
        
        Fade();
        Expand();

        var old_texture = ValueRenderable.GetTexture;
        var texture = generated_textures[_event.PlayTimes.ToString()];
        
        if (_event.PlayTimes == 0)
        {
            texture = value_change_wrap_mode switch
            {
                ValueChangeWrapMode.ResetToDefault => generated_textures[_event.OriginalLoop.ToString()],
                _ => null
            };
        }

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

            var new_position_x = this_position.X + this_scale.X / 2f - new_scale.Width / 2f;
            var new_position = new Vector3(ValueRenderable.GetPosition())
            {
                X = new_position_x
            };
            
            ValueRenderable.SetPosition(new_position);
            ValueRenderable.SetTranslation(ValueRenderable.GetTranslation() * (Vector3.UnitY + Vector3.UnitZ));
        }
        
        ValueRenderable?.SetTexture(texture);
    }
}