using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public class Camera
{
    private Vector3 _position;

    public Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
            UpdateMatrix();
        }
    }

    public Vector3 Front { get; set; }
    public Vector3 Up { get; private set; }
    public Vector2i Viewport;

    public float Scale = 1f;
    public bool IsBeingUpdated { get; set; } = false;

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
    private Matrix4 projection_matrix;

    public Camera(Vector3 position, Vector3 front, Vector3 up, Vector2i viewport)
    {
        Position = position;
        Front = front;
        Up = up;
        Viewport = viewport;

        UpdateMatrix();
    }

    public void UpdateMatrix()
    {
        var left = Position.X;
        var top = Position.Y;
        
        var right = Position.X + Width;
        var bottom = Position.Y + Height;

        if (Math.Abs(Scale - 1) > 0.01f)
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
        
        projection_matrix = Matrix4.CreateOrthographicOffCenter(left, right, bottom, top,-1f, 1f);
    }

    public Matrix4 GetProjectionMatrix()
    {
        return projection_matrix;
    }
}