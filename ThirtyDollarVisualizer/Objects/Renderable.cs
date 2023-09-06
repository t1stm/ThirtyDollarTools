using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public abstract class Renderable
{
    protected Vector3 Position;
    protected Vector3 Scale;
    protected Vector3 Offset;
    protected Vector4 Color;
    protected Shader Shader = null!;

    public abstract void Render(Camera camera);
    public abstract void SetOffset(Vector3 position);
    public abstract void SetPosition(Vector3 position);
    public abstract void SetColor(Vector4 color);

    public virtual void Dispose()
    {
        // Add implementation where needed.
    }

    public virtual void ChangeShader(Shader shader)
    {
        // Default: ignore.
    }

    public virtual Vector3 GetScale() => Scale;
    public virtual Vector3 GetPosition() => Position;
    public virtual Vector4 GetColor() => Color;
}