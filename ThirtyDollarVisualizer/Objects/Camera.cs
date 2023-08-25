using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public class Camera
{
    public Vector3 Position { get; set; }
    public Vector3 Front { get; set; }
    public Vector3 Up { get; private set; }
    public float Yaw { get; set; } = -90f;
    public float Pitch { get; set; }
    public float AspectRatio { get; set; }
    public float Zoom { get; set; }
    
    public Vector2i Viewport;

    public int Width
    {
        get => Viewport.X;
        set
        {
            Viewport.X = value;
            AspectRatio = (float)Width / Height;
        }
    }

    public int Height
    {
        get => Viewport.Y;
        set
        {
            Viewport.Y = value;
            AspectRatio = (float)Width / Height;
        }
    }

    public Camera(Vector3 position, Vector3 front, Vector3 up, Vector2i viewport)
    {
        Position = position;
        Front = front;
        Up = up;
        
        Viewport = viewport;
    }

    public void ModifyZoom(float zoomAmount)
    {
        Zoom = Math.Clamp(Zoom - zoomAmount, 1.0f, 45f);
    }

    public void ModifyDirection(float xOffset, float yOffset)
    {
        Yaw += xOffset;
        Pitch -= yOffset;
        Pitch = Math.Clamp(Pitch, -89f, 89f);

        var cameraDirection = Vector3.Zero;
        cameraDirection.X = MathF.Cos(MathHelper.DegreesToRadians(Yaw)) * MathF.Cos(MathHelper.DegreesToRadians(Pitch));
        cameraDirection.Y = MathF.Sin(MathHelper.DegreesToRadians(Pitch));
        cameraDirection.Z = MathF.Sin(MathHelper.DegreesToRadians(Yaw)) * MathF.Cos(MathHelper.DegreesToRadians(Pitch));

        Front = Vector3.Normalize(cameraDirection);
    }

    public Matrix4 GetViewMatrix()
    {
        return Matrix4.LookAt(Position, Position + Front, Up);
    }

    public Matrix4 GetProjectionMatrix()
    {
        return Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(Zoom), AspectRatio, 0.1f, 100.0f);
    }
}