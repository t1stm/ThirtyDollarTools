using System.Diagnostics;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer.Objects;

public sealed class DollarStoreCamera : Camera
{
    private readonly float _scrollSpeed;
    private Vector3 _offset = (0, 0, 0);
    private Vector3 _virtualPosition;
    private long LastScaleUpdate = Stopwatch.GetTimestamp();
    public Action<float>? OnZoom = null;

    public DollarStoreCamera(Vector3 VirtualPosition, Vector2i viewport, float scroll_speed = 7.5f) : base(
        VirtualPosition, viewport)
    {
        _virtualPosition = VirtualPosition;
        _scrollSpeed = scroll_speed;
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
        var now = LastScaleUpdate = Stopwatch.GetTimestamp();
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
    ///     This method copies values from another camera to this one.
    /// </summary>
    /// <param name="camera">The other camera.</param>
    public void CopyFrom(DollarStoreCamera camera)
    {
        Viewport = camera.Viewport;
        _position = camera.Position;
        vp_matrix = camera.GetVPMatrix();
        RenderScale = camera.RenderScale;
        Scale = camera.Scale;
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
        var current = Position;
        var target = _virtualPosition;

        Position = SteppingFunctions.Exponential(current, target, seconds_last_frame, 0.01f, _scrollSpeed);
    }

    public void Pulse(int times = 1, float frequency = 0)
    {
        new Thread(() => { BlockingPulse(times, frequency); }).Start();
    }

    public void SetOffset(Vector3 offset)
    {
        _offset = offset;
    }

    public Vector3 GetOffset()
    {
        return _offset;
    }
    
    public void ZoomStep(float scale)
    {
        const float stepping = .05f;
        var camera_scale = GetRenderScale();
        SetRenderScale(Math.Max(camera_scale + scale * stepping, stepping));
        OnZoom?.Invoke(RenderScale);
    }
}