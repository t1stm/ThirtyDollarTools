using System.Diagnostics;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public class DollarStoreCamera : Camera
{
    private Vector3 VirtualPosition;

    private const float ScrollLengthMs = 120f;
    
    public DollarStoreCamera(Vector3 position, Vector2i viewport) : base(position, -Vector3.UnitZ, Vector3.UnitY, viewport)
    {
        VirtualPosition = position;
    }

    public bool IsOutsideOfCameraView(Vector3 position, Vector3 scale, float margin_from_sides = 0)
    {
        var top = position.Y + scale.Y > Position.Y + margin_from_sides;
        var bottom = position.Y < Position.Y + Height - margin_from_sides;
        
        var left = position.X + scale.X > Position.X - margin_from_sides;
        var right = position.X < Position.X + Width - margin_from_sides;

        return !(top && bottom && left && right);
    }

    public void ScrollTo(Vector3 position)
    {
        VirtualPosition = position;
    }

    private async void AsyncUpdate()
    {
        if (IsBeingUpdated) return;
        IsBeingUpdated = true;

        do
        {
            var current_y = Position.Y;
            var delta_y = VirtualPosition.Y - current_y;
            delta_y -= delta_y % 1f;

            var scroll_y = delta_y / ScrollLengthMs;
            if (Math.Abs(scroll_y) < 1f) break;
            
            current_y += scroll_y;
            Position = current_y * Vector3.UnitY;

            await Task.Delay(1);
        } while (true);

        IsBeingUpdated = false;
    }

    public void Update()
    {
        AsyncUpdate();
    }
}