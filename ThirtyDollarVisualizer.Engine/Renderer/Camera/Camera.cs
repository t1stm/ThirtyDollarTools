using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Engine.Renderer.Camera;

public abstract class Camera
{
    protected bool Disposing;
    protected Vector3 InnerPosition;
    protected Matrix4 InnerVPMatrix;
    protected float RenderScale = 1f;

    protected float Scale = 1f;
    public Vector2i Viewport;

    protected Camera(Vector3 position, Vector2i viewport)
    {
        Position = position;
        Viewport = viewport;
    }

    /// <summary>
    /// The position of the Camera object which is used to calculate the projection matrix.
    /// </summary>
    public Vector3 Position
    {
        get => InnerPosition;
        set
        {
            InnerPosition = value;
            UpdateMatrix();
        }
    }

    /// <summary>
    /// Which direction the camera uses as its front position.
    /// </summary>
    public Vector3 Front { get; set; } = -Vector3.UnitZ;

    /// <summary>
    /// Which direction is the vertical axis of the camera.
    /// </summary>
    public Vector3 Up { get; set; } = Vector3.UnitY;

    protected bool IsBeingUpdated { get; set; }

    public int Width
    {
        get => Viewport.X;
        set
        {
            Viewport.X = value;
            UpdateMatrix();
        }
    }

    public int Height
    {
        get => Viewport.Y;
        set
        {
            Viewport.Y = value;
            UpdateMatrix();
        }
    }

    /// <summary>
    /// Updates the cached projection matrix.
    /// </summary>
    public virtual void UpdateMatrix()
    {
        var left = Position.X;
        var top = Position.Y;

        var right = Position.X + Width;
        var bottom = Position.Y + Height;

        if (Math.Abs(Scale - 1) > 0.001f)
        {
            var add_left = Width - Width / Scale;
            var add_top = Height - Height / Scale;

            add_left /= 2;
            add_top /= 2;

            left += add_left;
            top += add_top;

            right -= add_left;
            bottom -= add_top;
        }

        SetMatrixValue(left, right, bottom, top);
    }

    /// <summary>
    /// Creates a projection matrix from the given values.
    /// </summary>
    /// <param name="left">The left side of the matrix.</param>
    /// <param name="right">The right side of the matrix.</param>
    /// <param name="bottom">The bottom side of the matrix.</param>
    /// <param name="top">The top side of the matrix.</param>
    protected virtual void SetMatrixValue(float left, float right, float bottom, float top)
    {
        InnerVPMatrix = Matrix4.CreateOrthographicOffCenter(left, right, bottom, top, -1f, 1f);
    }

    /// <summary>
    /// Gets the multiplied view and projection matrices.
    /// </summary>
    /// <returns>A Matrix4 object containing view and projection data.</returns>
    public virtual Matrix4 GetVPMatrix()
    {
        // TODO: replace this with a property
        return InnerVPMatrix;
    }

    public void SetRenderScale(float scale)
    {
        RenderScale = Scale = scale;
        UpdateMatrix();
    }

    public float GetRenderScale()
    {
        return RenderScale;
    }

    /// <summary>
    /// Method that stops all camera animations.
    /// </summary>
    public void Die()
    {
        Disposing = true;
    }

    ~Camera()
    {
        Die();
    }
}