using System.Diagnostics;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public sealed class DollarStoreCamera : Camera
{
    private const float ScrollLengthMs = 120f;
    private Vector3 _virtualPosition;
    private DateTime LastScaleUpdate = DateTime.Now;

    public DollarStoreCamera(Vector3 VirtualPosition, Vector2i viewport) : base(VirtualPosition, viewport)
    {
        _virtualPosition = VirtualPosition;
        UpdateMatrix();
    }

    public bool IsOutsideOfCameraView(Vector3 position, Vector3 scale, float margin_from_sides = 0)
    {
        var collide_top = position.Y < _virtualPosition.Y + margin_from_sides;
        var collide_bottom = position.Y + scale.Y > _virtualPosition.Y + Height - margin_from_sides;

        var collide_left = position.X < _virtualPosition.X + margin_from_sides;
        var collide_right = position.X + scale.X > _virtualPosition.X + Width - margin_from_sides;

        return collide_top || collide_bottom || collide_left || collide_right;
    }

    public void ScrollTo(Vector3 position)
    {
        _virtualPosition = position;
    }

    public void SetPosition(Vector3 position)
    {
        _virtualPosition = position;
        _position = position;
    }

    public void ScrollDelta(Vector3 delta)
    {
        _virtualPosition += delta;
    }

    private void BlockingPulse(int times, float delay_ms)
    {
        var t = times;
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        const float max_add_scale = .05f;
        var now = LastScaleUpdate = DateTime.Now;
        do
        {
            if (Disposing) return;
            if (now != LastScaleUpdate) break;

            var elapsed = stopwatch.ElapsedMilliseconds;
            var factor = elapsed / delay_ms;
            if (factor > 1)
            {
                t--;
                factor = 1;
                stopwatch.Restart();
            }

            var zoom = RenderScale + MathF.Sin(MathF.PI * factor) * max_add_scale;
            Scale = zoom;
            UpdateMatrix();

            Thread.Sleep(1);
        } while (t > 0);

        if (now == LastScaleUpdate)
        {
            Scale = RenderScale;
            UpdateMatrix();
        }

        stopwatch.Reset();
    }

    /// <summary>
    /// This method copies values from another camera to this one.
    /// </summary>
    /// <param name="camera">The other camera.</param>
    public void CopyFrom(DollarStoreCamera camera)
    {
        Viewport = camera.Viewport;
        _position = camera.Position;
        projection_matrix = camera.GetProjectionMatrix();
    }

    public void Update(float seconds_last_frame)
    {
        // exponentional smoothing by lisyarus
        // https://lisyarus.github.io/blog/posts/exponential-smoothing.html
        
        const float speed = 7.5f;
        var current_y = Position.Y;
        var target_y = _virtualPosition.Y;

        current_y += (target_y - current_y) * (1f - MathF.Exp(- speed * seconds_last_frame));
        Position = current_y * Vector3.UnitY;
    }

    public void Pulse(int times = 1, float frequency = 0)
    {
        new Thread(() =>
        {
            BlockingPulse(times, frequency);
        }).Start();
    }
}