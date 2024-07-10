using System.Diagnostics;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public sealed class DollarStoreCamera : Camera
{
    private Vector3 _virtualPosition;
    private Vector3 _offset = (0,0,0);
    private DateTime LastScaleUpdate = DateTime.Now;

    public DollarStoreCamera(Vector3 VirtualPosition, Vector2i viewport) : base(VirtualPosition, viewport)
    {
        _virtualPosition = VirtualPosition;
        UpdateMatrix();
    }

    public bool IsOutsideOfCameraView(Vector3 position, Vector3 scale, float margin_from_top_bottom = 0)
    {
        var collide_top = position.Y < _virtualPosition.Y + margin_from_top_bottom;
        var collide_bottom = position.Y + scale.Y > _virtualPosition.Y + Height - margin_from_top_bottom;

        var collide_left = position.X < _virtualPosition.X;
        var collide_right = position.X + scale.X > _virtualPosition.X + Width;

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
        SetRenderScale(camera.RenderScale);
    }

    protected override void SetMatrixValue(float left, float right, float bottom, float top)
    {
        left += _offset.X;
        right += _offset.X;
        
        bottom += _offset.Y;
        top += _offset.Y;
        base.SetMatrixValue(left, right, bottom, top);
    }

    public void Update(float seconds_last_frame)
    {
        // exponentional smoothing by lisyarus
        // https://lisyarus.github.io/blog/posts/exponential-smoothing.html
        
        const float speed = 7.5f;
        var current_y = Position.Y;
        var target_y = _virtualPosition.Y;

        if (Math.Abs(current_y - target_y) < 0.01f)
        {
            Position = target_y * Vector3.UnitY;
            return;
        }

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

    public void SetOffset(Vector3 offset)
    {
        _offset = offset;
    }

    public Vector3 GetOffset()
    {
        return _offset;
    }
}