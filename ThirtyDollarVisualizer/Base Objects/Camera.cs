using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public abstract class Camera
{
    protected Vector3 _position;
    protected Matrix4 projection_matrix;
    protected float RenderScale = 1f;
    protected bool Disposing;

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
        get => _position;
        set
        {
            _position = value;
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

        projection_matrix = Matrix4.CreateOrthographicOffCenter(left, right, bottom, top, -1f, 1f);
    }

    public virtual Matrix4 GetProjectionMatrix()
    {
        return projection_matrix;
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