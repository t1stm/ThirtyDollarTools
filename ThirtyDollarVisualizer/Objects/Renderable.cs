using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public abstract class Renderable
{
    protected Vector3 Position;
    
    public abstract void Render(Camera camera);
    public abstract void SetPosition(Vector3 position);

    public virtual void Dispose()
    {
        // Add implementation where needed.
    }
}