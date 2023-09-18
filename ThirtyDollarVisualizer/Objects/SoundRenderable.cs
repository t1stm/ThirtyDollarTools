using OpenTK.Mathematics;
using ThirtyDollarVisualizer.Animations;
using ThirtyDollarVisualizer.Objects.Planes;

namespace ThirtyDollarVisualizer.Objects;

public class SoundRenderable : TexturedPlane
{
    private readonly Animation BounceAnimation;
    private readonly Animation ExpandAnimation;
    public SoundRenderable(Texture texture, Vector3 position, Vector2 width_height) : base(texture, position, width_height)
    {
        BounceAnimation = new BounceAnimation(() =>
        {
            UpdateModel();
        });
        ExpandAnimation = new ExpandAnimation(() =>
        {
            UpdateModel();
        });
    }
    public override void Render(Camera camera)
    {
        if (BounceAnimation.IsRunning || ExpandAnimation.IsRunning)
        {
            UpdateModel(BounceAnimation, ExpandAnimation);
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
}