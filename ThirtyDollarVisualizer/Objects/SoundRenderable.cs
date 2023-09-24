using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Animations;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.Objects;

public class SoundRenderable : TexturedPlane
{
    private readonly BounceAnimation BounceAnimation;
    private readonly ExpandAnimation ExpandAnimation;
    private readonly FadeAnimation FadeAnimation;
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

        base.Render(camera);
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
}