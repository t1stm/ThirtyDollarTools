using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public class Camera
{
    public Vector3 Position { get; set; }
    public Vector3 Front { get; set; }
    public Vector3 Up { get; private set; }
    public float AspectRatio { get; set; }
    public Vector2i Viewport;

    public bool IsBeingUpdated { get; set; } = false;

    public int Width
    {
        get => Viewport.X;
        set
        {
            Viewport.X = value;
            AspectRatio = (float)Width / Height;
            UpdateMatrices();
        }
    }

    public int Height
    {
        get => Viewport.Y;
        set
        {
            Viewport.Y = value;
            AspectRatio = (float)Width / Height;
            UpdateMatrices();
        }
    }

    public Camera(Vector3 position, Vector3 front, Vector3 up, Vector2i viewport)
    {
        Position = position;
        Front = front;
        Up = up;
        Viewport = viewport;
    }

    private Matrix4 view_matrix;
    private Matrix4 projection_matrix;
    
    public void UpdateMatrices()
    {
        view_matrix = Matrix4.LookAt(Position, Position + Front, Up);
        projection_matrix = Matrix4.CreateOrthographicOffCenter(0f, Width, 0f, Height, 0.1f, 100f);
    }

    public Matrix4 GetViewMatrix()
    {
        return view_matrix;
    }

    public Matrix4 GetProjectionMatrix()
    {
        return projection_matrix;
    }
}