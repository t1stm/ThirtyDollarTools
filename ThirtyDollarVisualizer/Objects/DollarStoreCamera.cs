using System.Diagnostics;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public class DollarStoreCamera : Camera
{
    private Vector3 VirtualPosition;
    private const float ScrollLengthMs = 120f;
    private DateTime LastScaleUpdate = DateTime.Now;
    
    public DollarStoreCamera(Vector3 position, Vector2i viewport) : base(position, -Vector3.UnitZ, Vector3.UnitY, viewport)
    {
        VirtualPosition = position;
    }

    public bool IsOutsideOfCameraView(Vector3 position, Vector3 scale, float margin_from_sides = 0)
    {
        var collide_top = position.Y < VirtualPosition.Y + margin_from_sides;
        var collide_bottom = position.Y + scale.Y > VirtualPosition.Y + Height - margin_from_sides;
        
        var collide_left = position.X < VirtualPosition.X + margin_from_sides;
        var collide_right = position.X + scale.X > VirtualPosition.X + Width - margin_from_sides;

        return collide_top || collide_bottom || collide_left || collide_right;
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

            if (Math.Abs(delta_y) < 1f) break;
            var scroll_y = delta_y / ScrollLengthMs;

            current_y += scroll_y;
            Position = current_y * Vector3.UnitY;

            await Task.Delay(1);
        } while (true);

        Position = VirtualPosition;

        IsBeingUpdated = false;
    }

    private async void AsyncPulse(int times, float delay_ms)
    {
        var t = times;
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        
        const float max_add_scale = .05f;
        var now = LastScaleUpdate = DateTime.Now;
        do
        {
            if (now != LastScaleUpdate) break;
            
            var elapsed = stopwatch.ElapsedMilliseconds;
            var factor = elapsed / delay_ms;
            if (factor > 1)
            {
                t--;
                factor = 1;
                stopwatch.Restart();
            }
            
            var zoom = 1 + (float) Math.Sin(Math.PI * factor) * max_add_scale;
            Scale = zoom;
            UpdateMatrix();
            
            await Task.Delay(1);
        } while (t > 0);

        if (now == LastScaleUpdate)
        {
            Scale = 1f;
            UpdateMatrix();   
        }
        
        stopwatch.Reset();
    }

    public void Update()
    {
        AsyncUpdate();
    }

    public void Pulse(int times = 1, float frequency = 0)
    {
        AsyncPulse(times, frequency);
    }
}